using AppDelicare.Data.Connection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace AppDelicare.WebApi.Security
{
    /// <summary>Cờ quyền trên một menu (OR qua mọi nhóm của user).</summary>
    public sealed record MenuRights(bool Access, bool Add, bool Edit, bool Delete, bool Print, bool Export, bool Import);

    /// <summary>Quyền của một user: admin, các nhóm (UserGroup.Name), quyền theo MetaLink.</summary>
    public sealed record UserPermissions(bool IsAdmin, IReadOnlySet<string> Groups, IReadOnlyDictionary<string, MenuRights> Menus)
    {
        public static readonly UserPermissions None = new(false, new HashSet<string>(), new Dictionary<string, MenuRights>());
    }

    public interface IPermissionService
    {
        Task<UserPermissions> GetAsync(Guid userId, CancellationToken cancellationToken = default);
        bool IsAllowed(UserPermissions permissions, string screenId, PermissionAction action);
        void Invalidate(Guid userId);
    }

    /// <summary>
    /// T07: UserRole × ObjectMenu theo UserGroup của user (qua UserClaim), cache theo <c>Permissions:CacheMinutes</c> (mặc định 5′).
    /// <c>User.IsAdmin</c> bỏ qua kiểm tra. User không tồn tại / không hoạt động / đã xóa → không có quyền.
    /// </summary>
    public sealed class PermissionService(DatabaseContext db, IMemoryCache cache, IOptions<PermissionOptions> options) : IPermissionService
    {
        private readonly PermissionOptions _options = options.Value;

        private static string CacheKey(Guid userId) => "perm:" + userId.ToString("N");

        public void Invalidate(Guid userId) => cache.Remove(CacheKey(userId));

        public async Task<UserPermissions> GetAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            if (cache.TryGetValue(CacheKey(userId), out UserPermissions? cached) && cached != null)
            {
                return cached;
            }

            var user = await db.User.AsNoTracking()
                .Where(u => u.Id == userId && !u.IsDeleted && u.IsActive)
                .Select(u => new { u.IsAdmin })
                .FirstOrDefaultAsync(cancellationToken);
            UserPermissions result;
            if (user == null)
            {
                result = UserPermissions.None;
            }
            else
            {
                var groupIds = await db.UserClaim.AsNoTracking()
                    .Where(c => c.UserId == userId && !c.IsDeleted && c.UserGroupId != null)
                    .Select(c => c.UserGroupId!.Value)
                    .Distinct()
                    .ToListAsync(cancellationToken);
                var groups = await db.UserGroup.AsNoTracking()
                    .Where(g => groupIds.Contains(g.Id) && !g.IsDeleted && g.Name != null)
                    .Select(g => g.Name!)
                    .ToListAsync(cancellationToken);
                var rows = await (from r in db.UserRole.AsNoTracking()
                                  join m in db.ObjectMenu.AsNoTracking() on r.ObjectMenuId equals m.Id
                                  where r.UserGroupId != null && groupIds.Contains(r.UserGroupId.Value) && !r.IsDeleted && !m.IsDeleted && m.IsActive && m.MetaLink != null
                                  select new { m.MetaLink, r.AllowAccess, r.AllowAdd, r.AllowEdit, r.AllowDelete, r.AllowPrint, r.AllowExport, r.AllowImport })
                                 .ToListAsync(cancellationToken);
                var menus = rows.GroupBy(r => r.MetaLink!, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => new MenuRights(
                        g.Any(r => r.AllowAccess), g.Any(r => r.AllowAdd), g.Any(r => r.AllowEdit), g.Any(r => r.AllowDelete),
                        g.Any(r => r.AllowPrint), g.Any(r => r.AllowExport), g.Any(r => r.AllowImport)), StringComparer.OrdinalIgnoreCase);
                result = new UserPermissions(user.IsAdmin, new HashSet<string>(groups, StringComparer.OrdinalIgnoreCase), menus);
            }

            cache.Set(CacheKey(userId), result, TimeSpan.FromMinutes(Math.Max(0, _options.CacheMinutes)));
            return result;
        }

        public bool IsAllowed(UserPermissions permissions, string screenId, PermissionAction action)
        {
            if (permissions.IsAdmin)
            {
                return true;
            }
            if (action == PermissionAction.AdminOnly)
            {
                return false;
            }
            permissions.Menus.TryGetValue("#/" + screenId, out var rights);
            if (rights == null || !rights.Access)
            {
                return false;
            }
            return action switch
            {
                PermissionAction.View => true,
                PermissionAction.Add => rights.Add,
                PermissionAction.Edit => rights.Edit,
                PermissionAction.Delete => rights.Delete,
                PermissionAction.Print => rights.Print,
                PermissionAction.Export => rights.Export,
                PermissionAction.Import => rights.Import,
                PermissionAction.Approve => ScreenMap.MatrixGroup.TryGetValue(screenId, out var column)
                    && _options.ApproverGroups.TryGetValue(column, out var approvers)
                    && approvers.Any(permissions.Groups.Contains),
                _ => false,
            };
        }
    }
}
