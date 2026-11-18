using System.Security.Claims;
using AppDelicare.Data.Interface;
using AppDelicare.Model.Helper;
using Microsoft.AspNetCore.Http;
using AppDelicare.Model.Option;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// T07 / T45R: UserRole × ObjectMenu theo UserGroup của user (qua UserClaim), cache theo <c>Permissions:CacheMinutes</c> (mặc định 5′).
    /// <c>User.IsAdmin</c> bỏ qua kiểm tra. User không tồn tại / không hoạt động / đã xóa → không có quyền.
    /// Quyền duyệt (D-31) = AllowEdit trên mục menu <c>#/wf-inbox?type=&lt;screen-id&gt;</c>.
    /// BOT-1.2 (D-62): request mang JWT bot hạn chế (claim <c>bot_limited = 1</c> – người có quyền duyệt / admin mới khớp số điện thoại, chưa nhập mã web)
    /// của CHÍNH user đó → bỏ quyền admin + mọi mục duyệt (bản cache giữ nguyên quyền đầy đủ cho web).
    /// </summary>
    public class PermissionService(IUnitOfWork unitOfWork, IMemoryCache cache, IOptions<PermissionOptions> options, IHttpContextAccessor? httpContextAccessor = null) : IPermissionService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly PermissionOptions _options = options.Value;

        /// <summary>Thế hệ cache: tăng lên khi <see cref="InvalidateAll"/> → mọi khóa cũ không còn được đọc.</summary>
        private static long _generation;

        private static string CacheKey(Guid userId) => $"perm:{Interlocked.Read(ref _generation)}:{userId:N}";

        public void Invalidate(Guid userId) => cache.Remove(CacheKey(userId));

        public void InvalidateAll() => Interlocked.Increment(ref _generation);

        public async Task<UserPermissions> GetAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            string key = CacheKey(userId);
            if (cache.TryGetValue(key, out UserPermissions? cached) && cached != null)
            {
                return Restrict(cached, userId);
            }
            var db = _unitOfWork.DbContext;
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
                // Chỉ nhóm chưa xóa mới cấp quyền.
                var liveGroupIds = await db.UserGroup.AsNoTracking().Where(g => groupIds.Contains(g.Id) && !g.IsDeleted).Select(g => g.Id).ToListAsync(cancellationToken);
                var rows = await (from r in db.UserRole.AsNoTracking()
                                  join m in db.ObjectMenu.AsNoTracking() on r.ObjectMenuId equals m.Id
                                  where r.UserGroupId != null && liveGroupIds.Contains(r.UserGroupId.Value) && !r.IsDeleted && !m.IsDeleted && m.IsActive && m.MetaLink != null
                                  select new { m.MetaLink, r.AllowAccess, r.AllowAdd, r.AllowEdit, r.AllowDelete, r.AllowPrint, r.AllowExport, r.AllowImport })
                                 .ToListAsync(cancellationToken);
                var menus = rows.GroupBy(r => r.MetaLink!, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => new MenuRights(
                        g.Any(r => r.AllowAccess), g.Any(r => r.AllowAdd), g.Any(r => r.AllowEdit), g.Any(r => r.AllowDelete),
                        g.Any(r => r.AllowPrint), g.Any(r => r.AllowExport), g.Any(r => r.AllowImport)), StringComparer.OrdinalIgnoreCase);
                result = new UserPermissions(user.IsAdmin, new HashSet<string>(groups, StringComparer.OrdinalIgnoreCase), menus);
            }
            cache.Set(key, result, TimeSpan.FromMinutes(Math.Max(0, _options.CacheMinutes)));
            return Restrict(result, userId);
        }

        private UserPermissions Restrict(UserPermissions full, Guid userId)
        {
            var principal = httpContextAccessor?.HttpContext?.User;
            if (principal?.FindFirstValue(MessagingCode.Claims.BotLimited) != "1"
                || !Guid.TryParse(principal.FindFirstValue(ConstantHelper.JwtClaimIdentifiers.Id), out Guid tokenUser) || tokenUser != userId)
            {
                return full;
            }
            var menus = full.Menus.Where(m => !m.Key.StartsWith(IPermissionService.ApproveMenuPrefix, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(m => m.Key, m => m.Value, StringComparer.OrdinalIgnoreCase);
            return new UserPermissions(false, full.Groups, menus);
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
            if (action == PermissionAction.Approve)
            {
                return permissions.Menus.TryGetValue(IPermissionService.ApproveMenuPrefix + screenId, out var approve) && approve.Access && approve.Edit;
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
                _ => false,
            };
        }
    }
}
