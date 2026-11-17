#nullable enable
using AppDelicare.Data.Connection;
using AppDelicare.Data.Share;
using AppDelicare.Model.Helper;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// BE2.7: kiểm quyền động cho API dùng chung (màn hình phụ thuộc tham số Resource / loại phiếu) – cùng cơ chế cũ D-31
    /// (<see cref="IPermissionService"/>: UserClaim → UserGroup → UserRole × ObjectMenu, admin có mọi quyền). Action dùng <c>[SkipPermission]</c> + kiểm ở đây.
    /// </summary>
    public static class CommonAccess
    {
        /// <summary>Mã trả về khi không có quyền (controller đổi thành HTTP 403).</summary>
        public const int Forbidden = 403;

        public static string ActionLabel(PermissionAction action) => action switch
        {
            PermissionAction.View => "Xem",
            PermissionAction.Add => "Thêm",
            PermissionAction.Edit => "Sửa",
            PermissionAction.Delete => "Xóa",
            PermissionAction.Print => "In",
            PermissionAction.Export => "Xuất",
            PermissionAction.Import => "Nhập",
            PermissionAction.Approve => "Duyệt",
            _ => action.ToString(),
        };

        /// <summary>null = được phép; khác null = thông báo từ chối.</summary>
        public static async Task<string?> DenyAsync(IPermissionService permissions, ICurrentActor currentActor, string screenId, PermissionAction action)
        {
            var actor = currentActor.RequireActor();
            var perms = await permissions.GetAsync(actor.UserId);
            return permissions.IsAllowed(perms, screenId, action) ? null : string.Format(ConstantHelper.Message.CommonNoRight, ActionLabel(action), "#/" + screenId);
        }

        /// <summary>Tên hiển thị người thao tác: "Họ tên (tên đăng nhập)" – không bao giờ đọc mật khẩu.</summary>
        public static async Task<Dictionary<Guid, string>> UserNamesAsync(DatabaseContext db, IEnumerable<Guid?> ids)
        {
            var list = ids.Where(x => x != null).Select(x => x!.Value).Distinct().ToList();
            var result = new Dictionary<Guid, string>();
            foreach (var chunk in list.Chunk(500))
            {
                var part = chunk.ToList();
                var users = await db.User.AsNoTracking().Where(u => part.Contains(u.Id)).Select(u => new { u.Id, u.UserName, u.PersonId }).ToListAsync();
                var personIds = users.Where(u => u.PersonId != null).Select(u => u.PersonId!.Value).Distinct().ToList();
                var persons = await db.Person.AsNoTracking().Where(p => personIds.Contains(p.Id)).Select(p => new { p.Id, p.FullName }).ToDictionaryAsync(p => p.Id, p => p.FullName);
                foreach (var u in users)
                {
                    string? full = u.PersonId is { } pid ? persons.GetValueOrDefault(pid) : null;
                    result[u.Id] = string.IsNullOrWhiteSpace(full) ? u.UserName ?? u.Id.ToString() : $"{full} ({u.UserName})";
                }
            }
            return result;
        }
    }
}
