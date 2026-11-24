using AppDelicare.Data.Connection;
using AppDelicare.Model.Helper;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Implement.Identity
{
    /// <summary>Kết quả khớp số: nhân viên (Employee.Phone) và tài khoản web (Person.PhoneNumber → User.PersonId) cùng số.</summary>
    public sealed record PhoneMatch(IReadOnlyList<Guid> EmployeeIds, IReadOnlyList<Guid> UserIds, string? FullName)
    {
        /// <summary>Khớp DUY NHẤT: ≤ 1 nhân viên, ≤ 1 tài khoản và có ít nhất 1 (D-62 p9).</summary>
        public bool IsUnique => EmployeeIds.Count <= 1 && UserIds.Count <= 1 && EmployeeIds.Count + UserIds.Count > 0;
    }

    /// <summary>
    /// BOT-1.2 (D-62 p9): chuẩn hóa số điện thoại VN (<c>+84</c> / <c>84</c> / <c>0</c>, bỏ khoảng trắng, dấu chấm, gạch) và khớp hồ sơ.
    /// Employee và User không có cột nối nhau → 1 số khớp được tối đa 1 nhân viên + 1 tài khoản (cùng 1 người), định danh ghi cả hai.
    /// Số trong DB lưu nhiều kiểu (khoảng trắng, dấu chấm, +84) → đọc Id + số của mọi hồ sơ có số rồi so sau khi chuẩn hóa ở bộ nhớ
    /// (vài trăm đến vài nghìn dòng nhỏ, chỉ khi liên kết – không phải đường nóng).
    /// </summary>
    public static class PhoneMatcher
    {
        /// <summary>Về dạng "84" + 9 số; số nước ngoài (+ khác 84) giữ nguyên chữ số; quá ngắn → null.</summary>
        public static string? Normalize(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                return null;
            }
            string trimmed = phone.Trim();
            bool plus = trimmed.StartsWith('+');
            string digits = new(trimmed.Where(char.IsAsciiDigit).ToArray());
            if (digits.StartsWith("0084"))
            {
                digits = digits[2..];
                plus = true;
            }
            if (digits.StartsWith('0') && !plus)
            {
                digits = "84" + digits[1..];
            }
            else if (!plus && !digits.StartsWith("84") && digits.Length == 9)
            {
                digits = "84" + digits; // mất số 0 đầu (Excel)
            }
            return digits.Length >= 10 ? digits : null;
        }

        public static async Task<PhoneMatch> MatchAsync(DatabaseContext db, string? phone, CancellationToken ct = default)
        {
            string? wanted = Normalize(phone);
            if (wanted == null)
            {
                return new PhoneMatch([], [], null);
            }
            var employees = (await db.Employee.AsNoTracking().Where(e => !e.IsDeleted && e.Phone != null && e.Phone != "")
                    .Select(e => new { e.Id, e.Phone, e.FullName, e.Status }).ToListAsync(ct))
                .Where(e => Normalize(e.Phone) == wanted && EmployeeStatus.IsWorking(e.Status)).ToList();
            var persons = (await db.Person.AsNoTracking().Where(p => !p.IsDeleted && p.PhoneNumber != null && p.PhoneNumber != "")
                    .Select(p => new { p.Id, p.PhoneNumber, p.FullName }).ToListAsync(ct))
                .Where(p => Normalize(p.PhoneNumber) == wanted).ToList();
            var personIds = persons.Select(p => p.Id).ToList();
            List<Guid> users = personIds.Count == 0 ? [] : await db.User.AsNoTracking()
                .Where(u => !u.IsDeleted && u.IsActive && (u.AccountType == null || u.AccountType == PersonLink.Internal) && u.PersonId != null && personIds.Contains(u.PersonId.Value)).Select(u => u.Id).ToListAsync(ct);
            string? name = employees.FirstOrDefault()?.FullName ?? persons.FirstOrDefault()?.FullName;
            return new PhoneMatch(employees.Select(e => e.Id).ToList(), users, name);
        }
    }
}
