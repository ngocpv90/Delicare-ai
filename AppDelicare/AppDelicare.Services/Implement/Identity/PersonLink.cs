#nullable enable
using AppDelicare.Data.Connection;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AppDelicare.Services.Implement.Identity
{
    /// <summary>
    /// HR-ACC-1.3 (D-82 a, i): MỘT cách tra duy nhất tài khoản ↔ con người ↔ nhân viên: <c>User.PersonId</c> → <c>Person</c> ← <c>Employee.PersonId</c>.
    /// Mọi nơi (chấm công, đơn nghỉ / đổi ca, Việc của tôi, bot, chặn tự duyệt, người duyệt vắng, chuyến của tôi, Nhân viên 360) đi qua đây
    /// (<see cref="HrRules.ResolveEmployeeAsync"/> gọi sang). Chỉ tài khoản <c>INTERNAL</c> có nhân viên (D-82 b).
    /// Dự phòng 1 bản phát hành (<c>BusinessRules:Hr:LegacyEmployeeMatch</c>, mặc định true): chưa gắn → đoán cũ D-55 b (liên kết bot → tên đăng nhập = mã NV →
    /// SĐT khớp đúng 1), MỖI lần dùng ghi nhật ký cảnh báo; nhân viên đã gắn người khác thì không bao giờ được đoán sang.
    /// </summary>
    public static class PersonLink
    {
        public const string Internal = "INTERNAL";
        public const string Customer = "CUSTOMER";
        public const string Supplier = "SUPPLIER";
        public static readonly IReadOnlyList<string> AccountTypes = [Internal, Customer, Supplier];

        /// <summary>Đặt lúc khởi động từ <c>BusinessRules:Hr:LegacyEmployeeMatch</c> (ServiceRegistration).</summary>
        public static bool LegacyMatch { get; set; } = true;

        public static bool IsInternal(string? accountType) => string.IsNullOrWhiteSpace(accountType) || string.Equals(accountType.Trim(), Internal, StringComparison.OrdinalIgnoreCase);

        /// <summary>Nhân viên của tài khoản (null = chưa gắn / không phải tài khoản nội bộ).</summary>
        public static async Task<Employee?> EmployeeOfUserAsync(DatabaseContext db, Guid userId, bool? legacy = null)
        {
            var user = await db.User.AsNoTracking().Where(u => u.Id == userId && !u.IsDeleted).Select(u => new { u.Id, u.UserName, u.PersonId, u.AccountType }).FirstOrDefaultAsync();
            if (user == null || !IsInternal(user.AccountType))
            {
                return null;
            }
            if (user.PersonId is { } pid)
            {
                var linked = await db.Employee.AsNoTracking().FirstOrDefaultAsync(e => e.PersonId == pid && !e.IsDeleted);
                if (linked != null)
                {
                    return linked;
                }
            }
            if (!(legacy ?? LegacyMatch))
            {
                return null;
            }
            var guess = await LegacyEmployeeOfUserAsync(db, user.Id, user.UserName, user.PersonId);
            if (guess != null)
            {
                Log.Warning("[PersonLink] Đoán cũ (D-55 b): tài khoản {UserName} ({UserId}) → nhân viên {Code} ({EmployeeId}) – chưa gắn Employee.PersonId; NS gắn ở Nhân viên › Đối chiếu",
                    user.UserName, user.Id, guess.Code ?? guess.EmployeeCode, guess.Id);
            }
            return guess;
        }

        /// <summary>Tài khoản nội bộ của nhân viên (ưu tiên tài khoản đang mở). null = chưa có.</summary>
        public static async Task<User?> UserOfEmployeeAsync(DatabaseContext db, Employee employee, bool? legacy = null)
        {
            if (employee.PersonId is { } pid)
            {
                var linked = await db.User.AsNoTracking().Where(u => u.PersonId == pid && !u.IsDeleted && (u.AccountType == null || u.AccountType == Internal))
                    .OrderByDescending(u => u.IsActive).FirstOrDefaultAsync();
                if (linked != null)
                {
                    return linked;
                }
            }
            if (!(legacy ?? LegacyMatch))
            {
                return null;
            }
            var guess = await LegacyUserOfEmployeeAsync(db, employee);
            if (guess != null)
            {
                Log.Warning("[PersonLink] Đoán cũ (D-55 b): nhân viên {Code} ({EmployeeId}) → tài khoản {UserName} ({UserId}) – chưa gắn Employee.PersonId",
                    employee.Code ?? employee.EmployeeCode, employee.Id, guess.UserName, guess.Id);
            }
            return guess;
        }

        /// <summary>Ứng viên Person cho nhân viên chưa gắn (tab Đối chiếu, script D82_link_employee_person.sql – cùng 3 cách).</summary>
        public static async Task<List<PersonCandidate>> CandidatesAsync(DatabaseContext db, Employee e)
        {
            var result = new List<PersonCandidate>();
            void Add(Guid personId, string source, Guid? userId)
            {
                var c = result.FirstOrDefault(x => x.PersonId == personId);
                if (c == null)
                {
                    result.Add(c = new PersonCandidate { PersonId = personId });
                }
                if (!c.Sources.Contains(source))
                {
                    c.Sources.Add(source);
                }
                c.UserId ??= userId;
            }
            var bot = await (from x in db.UserExternalIdentity.AsNoTracking()
                             join u in db.User.AsNoTracking() on x.UserId equals u.Id
                             where !x.IsDeleted && x.EmployeeId == e.Id && x.UserId != null && !u.IsDeleted && u.PersonId != null
                                   && (x.IdentityStatus == null || x.IdentityStatus == "Active" || x.IdentityStatus == "PendingCode")
                             select new { u.Id, u.PersonId }).ToListAsync();
            bot.ForEach(b => Add(b.PersonId!.Value, "BOT", b.Id));
            string? code = (e.Code ?? string.Empty).Trim().ToLower(), old = (e.EmployeeCode ?? string.Empty).Trim().ToLower();
            if (code.Length > 0 || old.Length > 0)
            {
                var byName = await db.User.AsNoTracking().Where(u => !u.IsDeleted && u.PersonId != null && u.UserName != null
                        && ((code.Length > 0 && u.UserName.Trim().ToLower() == code) || (old.Length > 0 && u.UserName.Trim().ToLower() == old)))
                    .Select(u => new { u.Id, u.PersonId }).ToListAsync();
                byName.ForEach(b => Add(b.PersonId!.Value, "USERNAME", b.Id));
            }
            string? phone = PhoneMatcher.Normalize(e.Phone);
            if (phone != null)
            {
                var persons = (await db.Person.AsNoTracking().Where(p => !p.IsDeleted && p.PhoneNumber != null && p.PhoneNumber != "")
                        .Select(p => new { p.Id, p.PhoneNumber }).ToListAsync())
                    .Where(p => PhoneMatcher.Normalize(p.PhoneNumber) == phone).ToList();
                persons.ForEach(p => Add(p.Id, "PHONE", null));
            }
            if (result.Count == 0)
            {
                return result;
            }
            var ids = result.Select(c => c.PersonId).ToList();
            var people = await db.Person.AsNoTracking().Where(p => ids.Contains(p.Id) && !p.IsDeleted).ToDictionaryAsync(p => p.Id);
            var users = await db.User.AsNoTracking().Where(u => u.PersonId != null && ids.Contains(u.PersonId.Value) && !u.IsDeleted)
                .Select(u => new { u.Id, u.PersonId, u.UserName, u.IsActive, u.AccountType }).ToListAsync();
            var taken = await db.Employee.AsNoTracking().Where(x => x.PersonId != null && ids.Contains(x.PersonId.Value) && !x.IsDeleted && x.Id != e.Id)
                .Select(x => new { x.PersonId, x.Code, x.EmployeeCode, x.FullName }).ToListAsync();
            foreach (var c in result)
            {
                if (people.TryGetValue(c.PersonId, out var p))
                {
                    c.FullName = p.FullName;
                    c.PhoneNumber = p.PhoneNumber;
                }
                var u = users.Where(x => x.PersonId == c.PersonId).OrderByDescending(x => x.IsActive).FirstOrDefault();
                if (u != null)
                {
                    c.UserId = u.Id;
                    c.UserName = u.UserName;
                    c.UserIsActive = u.IsActive;
                    c.AccountType = u.AccountType;
                }
                var t = taken.FirstOrDefault(x => x.PersonId == c.PersonId);
                c.LinkedEmployee = t == null ? null : $"{t.Code ?? t.EmployeeCode} – {t.FullName}";
            }
            return result.Where(c => people.ContainsKey(c.PersonId)).ToList();
        }

        // ------------------------------------------------------------------ đoán cũ (D-55 b) – chỉ dự phòng

        private static async Task<Employee?> LegacyEmployeeOfUserAsync(DatabaseContext db, Guid userId, string? userName, Guid? personId)
        {
            static bool Free(Employee e, Guid? personId) => e.PersonId == null || e.PersonId == personId;
            var linked = await db.UserExternalIdentity.AsNoTracking()
                .Where(x => !x.IsDeleted && x.UserId == userId && x.EmployeeId != null && (x.IdentityStatus == null || x.IdentityStatus == "Active" || x.IdentityStatus == "PendingCode"))
                .Select(x => x.EmployeeId).FirstOrDefaultAsync();
            if (linked is { } eid)
            {
                var byLink = await db.Employee.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eid && !e.IsDeleted);
                if (byLink != null && Free(byLink, personId))
                {
                    return byLink;
                }
            }
            if (!string.IsNullOrWhiteSpace(userName))
            {
                string name = userName.Trim().ToLower();
                var byCode = (await db.Employee.AsNoTracking().Where(e => !e.IsDeleted && ((e.Code != null && e.Code.ToLower() == name) || (e.EmployeeCode != null && e.EmployeeCode.ToLower() == name))).Take(2).ToListAsync()).Where(e => Free(e, personId)).ToList();
                if (byCode.Count == 1)
                {
                    return byCode[0];
                }
            }
            if (personId is { } pid)
            {
                string? phone = PhoneMatcher.Normalize(await db.Person.AsNoTracking().Where(p => p.Id == pid).Select(p => p.PhoneNumber).FirstOrDefaultAsync());
                if (phone != null)
                {
                    var candidates = (await db.Employee.AsNoTracking().Where(e => !e.IsDeleted && e.Phone != null && e.Phone != "").ToListAsync())
                        .Where(e => PhoneMatcher.Normalize(e.Phone) == phone && Free(e, personId)).Take(2).ToList();
                    if (candidates.Count == 1)
                    {
                        return candidates[0];
                    }
                }
            }
            return null;
        }

        private static async Task<User?> LegacyUserOfEmployeeAsync(DatabaseContext db, Employee e)
        {
            var linkedUser = await db.UserExternalIdentity.AsNoTracking()
                .Where(x => !x.IsDeleted && x.EmployeeId == e.Id && x.UserId != null && (x.IdentityStatus == null || x.IdentityStatus == "Active" || x.IdentityStatus == "PendingCode"))
                .OrderByDescending(x => x.LinkedAt).Select(x => x.UserId).FirstOrDefaultAsync();
            User? user = linkedUser is { } uid ? await db.User.AsNoTracking().FirstOrDefaultAsync(u => u.Id == uid && !u.IsDeleted) : null;
            string? code = e.Code ?? e.EmployeeCode;
            string? login = code?.Trim().ToLower();
            user ??= string.IsNullOrWhiteSpace(login) ? null : await db.User.AsNoTracking().FirstOrDefaultAsync(u => !u.IsDeleted && u.UserName != null && u.UserName.ToLower() == login);
            if (user == null || !IsInternal(user.AccountType))
            {
                return null;
            }
            // Tài khoản đã thuộc người khác (Person gắn nhân viên khác) → không đoán.
            if (user.PersonId is { } pid && await db.Employee.AsNoTracking().AnyAsync(x => x.PersonId == pid && x.Id != e.Id && !x.IsDeleted))
            {
                return null;
            }
            return user;
        }
    }

    /// <summary>1 ứng viên Person của nhân viên (tab Đối chiếu).</summary>
    public sealed class PersonCandidate
    {
        public Guid PersonId { get; set; }
        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
        /// <summary>BOT (liên kết bot) · USERNAME (tên đăng nhập = mã NV) · PHONE (SĐT).</summary>
        public List<string> Sources { get; set; } = [];
        public Guid? UserId { get; set; }
        public string? UserName { get; set; }
        public bool? UserIsActive { get; set; }
        public string? AccountType { get; set; }
        /// <summary>Đã gắn nhân viên khác (mã – tên) – không chọn được.</summary>
        public string? LinkedEmployee { get; set; }
    }
}
