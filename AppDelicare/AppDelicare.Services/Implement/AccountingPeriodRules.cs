using AppDelicare.Data.Connection;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// ACC-4 (D-131 h): trạng thái kỳ kế toán lưu bằng <c>AuditChange</c> (EntityName <c>AccountingPeriod</c>, IdChange = Guid mã kỳ
    /// <c>00000000-0000-0000-0000-YYYYMM000000</c>, Action <c>Lock</c> / <c>Unlock</c>) – không bảng mới. Kỳ khóa = hành động mới nhất là Lock.
    /// Kỳ đã khóa: máy chủ chặn hủy / đảo / sửa nghiệp vụ có ngày trong kỳ (thông báo tiếng Việt).
    /// </summary>
    public static class AccountingPeriodRules
    {
        public const string Entity = "AccountingPeriod";
        public const string ActionLock = "Lock";
        public const string ActionUnlock = "Unlock";

        public static Guid PeriodId(int year, int month) => Guid.Parse($"00000000-0000-0000-0000-{year:0000}{month:00}000000");
        public static Guid PeriodId(DateTime d) => PeriodId(d.Year, d.Month);

        public static async Task<bool> IsLockedAsync(DatabaseContext db, DateTime date)
        {
            var id = PeriodId(date);
            var last = await db.AuditChange.AsNoTracking()
                .Where(a => !a.IsDeleted && a.EntityName == Entity && a.IdChange == id && (a.Action == ActionLock || a.Action == ActionUnlock))
                .OrderByDescending(a => a.CreatedDate).Select(a => a.Action).FirstOrDefaultAsync();
            return last == ActionLock;
        }

        /// <summary>Kỳ chứa <paramref name="date"/> đã khóa → câu báo lỗi; chưa khóa → null.</summary>
        public static async Task<string?> LockedMessageAsync(DatabaseContext db, DateTime date, string action)
            => await IsLockedAsync(db, date)
                ? $"Kỳ kế toán tháng {date:MM/yyyy} đã khóa – không {action}. Cần sửa thì gửi yêu cầu mở khóa kỳ (COO duyệt) hoặc lập chứng từ điều chỉnh ở kỳ đang mở."
                : null;
    }
}
