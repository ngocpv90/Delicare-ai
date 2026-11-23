using AppDelicare.Data.Connection;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Implement
{
    /// <summary>UI-2R (D-69 b): dữ liệu dùng chung cho khối thông tin rút gọn của các màn chi tiết (nhóm "Hệ thống": tạo / cập nhật bởi).</summary>
    public static class InfoPanelRules
    {
        /// <summary>Tên hiển thị của người dùng theo Id: họ tên hồ sơ Person, không có thì tên đăng nhập. Id không tìm thấy → không có trong kết quả.</summary>
        public static async Task<Dictionary<Guid, string>> UserNamesAsync(DatabaseContext db, IEnumerable<Guid?> userIds)
        {
            var ids = userIds.Where(x => x != null).Select(x => x!.Value).Distinct().ToList();
            if (ids.Count == 0)
            {
                return [];
            }
            var users = await db.User.AsNoTracking().Where(u => ids.Contains(u.Id)).Select(u => new { u.Id, u.UserName, u.PersonId }).ToListAsync();
            var pids = users.Where(u => u.PersonId != null).Select(u => u.PersonId!.Value).ToList();
            var names = await db.Person.AsNoTracking().Where(p => pids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.FullName);
            return users.ToDictionary(u => u.Id, u => u.PersonId is { } pid && names.TryGetValue(pid, out var n) && !string.IsNullOrWhiteSpace(n) ? n! : u.UserName ?? string.Empty);
        }

        public static string? NameOf(this Dictionary<Guid, string> names, Guid? id) => id is { } v && names.TryGetValue(v, out var n) ? n : null;
    }
}
