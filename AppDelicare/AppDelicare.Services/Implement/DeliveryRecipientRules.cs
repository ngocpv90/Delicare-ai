using AppDelicare.Data.Connection;
using AppDelicare.Model.Helper;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// LOC-1 (anh yêu cầu 2026-11-22): người nhận của điểm giao (khách doanh nghiệp) phải nằm trong danh sách người liên hệ của khách (<c>CustomerContact</c> đang hoạt động).
    /// Bảng điểm giao không có cột trỏ tới người liên hệ (không thêm cột) → khớp theo tên (Name / FullName, không phân biệt hoa thường), SĐT để phân biệt người trùng tên;
    /// máy chủ ghi lại tên + SĐT theo người liên hệ (SĐT trống → lấy SĐT của người liên hệ).
    /// Khách cá nhân: không áp dụng. Bản ghi cũ giữ nguyên người nhận (không đổi tên / SĐT) thì vẫn lưu được.
    /// </summary>
    public static class DeliveryRecipientRules
    {
        private const string Active = "Active";

        public static async Task<(string? Name, string? Phone, string? Error)> ResolveAsync(DatabaseContext db, Guid customerId, string? name, string? phone,
            bool required, string? storedName = null, string? storedPhone = null)
        {
            string? n = Clean(name), p = Clean(phone);
            var customerType = await db.Customer.AsNoTracking().Where(c => c.Id == customerId && !c.IsDeleted).Select(c => c.CustomerType).FirstOrDefaultAsync();
            if (!string.Equals(customerType, CustomerTypes.Business, StringComparison.OrdinalIgnoreCase))
            {
                return (n, p, null);
            }
            if (n == null)
            {
                return required || Clean(storedName) != null ? (null, null, ConstantHelper.Message.LocationRecipientRequired) : (null, p, null);
            }
            // Dữ liệu cũ: giữ nguyên người nhận đang lưu.
            if (Same(n, storedName) && (p == null || Same(p, storedPhone)))
            {
                return (Clean(storedName), Clean(storedPhone) ?? p, null);
            }
            var contacts = await db.CustomerContact.AsNoTracking()
                .Where(c => c.CustomerId == customerId && !c.IsDeleted && (c.Status == null || c.Status == Active))
                .Select(c => new { c.Name, c.FullName, c.Phone }).ToListAsync();
            var byName = contacts.Where(c => Same(n, c.FullName) || Same(n, c.Name)).ToList();
            var hit = byName.FirstOrDefault(c => p == null || Same(Digits(p), Digits(c.Phone))) ?? (p == null ? null : byName.FirstOrDefault(c => Clean(c.Phone) == null));
            if (hit == null)
            {
                string list = contacts.Count == 0 ? "khách chưa có người liên hệ"
                    : string.Join(", ", contacts.Select(c => (Clean(c.FullName) ?? Clean(c.Name)) + (Clean(c.Phone) is { } ph ? $" ({ph})" : string.Empty)));
                return (null, null, string.Format(byName.Count == 0 ? ConstantHelper.Message.LocationRecipientNotContact : ConstantHelper.Message.LocationRecipientPhoneMismatch, n, list));
            }
            return (Clean(hit.FullName) ?? Clean(hit.Name), Clean(hit.Phone) ?? p, null);
        }

        private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        private static bool Same(string? a, string? b) => Clean(a) is { } x && Clean(b) is { } y && string.Equals(x, y, StringComparison.OrdinalIgnoreCase);

        private static string? Digits(string? s) => Clean(s) is { } v ? new string(v.Where(char.IsDigit).ToArray()) : null;
    }
}
