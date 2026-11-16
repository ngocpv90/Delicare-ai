using System.Security.Cryptography;
using AppDelicare.Application.Common;
using AppDelicare.Data.Connection;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Application.Admin
{
    public sealed record PersonDto(string? FullName, string? PhoneNumber, string? Sex, string? Address, DateTime? DateOfBirth);

    public sealed record GroupBrief(Guid Id, string? Name);

    /// <summary>Người dùng – KHÔNG BAO GIỜ có mật khẩu.</summary>
    public sealed record UserDto(Guid Id, string? UserName, bool IsActive, bool IsAdmin, Guid? PersonId, PersonDto? Person,
        IReadOnlyList<GroupBrief> Groups, DateTime CreatedDate, DateTime? ModifiedDate);

    public sealed record UserPage(IReadOnlyList<UserDto> Items, int Total, int Page, int PageSize);

    /// <summary>
    /// Băm mật khẩu – một chỗ duy nhất. Hiện dùng cách app đang dùng (MD5, CODE_AUDIT A19) để đăng nhập cũ vẫn chạy;
    /// đổi sang PBKDF2 khi chốt D-29 (chỉ sửa lớp này + LoginService).
    /// </summary>
    public static class PasswordHasher
    {
        public const int MinLength = 8;

        public static string Hash(string password) => EncriptFunctionsHelper.GeneratePassword(password);

        public static bool Verify(string password, string? hash) => hash != null && EncriptFunctionsHelper.VerifyPassword(password, hash);

        /// <summary>Mật khẩu tạm 12 ký tự (chữ hoa, thường, số), sinh bằng RNG mật mã.</summary>
        public static string Temporary()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
            return new string(Enumerable.Range(0, 12).Select(_ => chars[RandomNumberGenerator.GetInt32(chars.Length)]).ToArray());
        }
    }

    internal static class UserQueries
    {
        public static async Task<List<UserDto>> LoadAsync(DatabaseContext db, IQueryable<User> users, CancellationToken ct)
        {
            var list = await users.AsNoTracking().ToListAsync(ct);
            var ids = list.Select(u => u.Id).ToList();
            var personIds = list.Where(u => u.PersonId != null).Select(u => u.PersonId!.Value).ToList();
            var persons = await db.Person.AsNoTracking().Where(p => personIds.Contains(p.Id) && !p.IsDeleted).ToDictionaryAsync(p => p.Id, ct);
            var groups = await (from c in db.UserClaim.AsNoTracking()
                                join g in db.UserGroup.AsNoTracking() on c.UserGroupId equals g.Id
                                where c.UserId != null && ids.Contains(c.UserId.Value) && !c.IsDeleted && !g.IsDeleted
                                select new { UserId = c.UserId!.Value, g.Id, g.Name }).ToListAsync(ct);
            return list.Select(u => new UserDto(u.Id, u.UserName, u.IsActive, u.IsAdmin, u.PersonId,
                u.PersonId is { } pid && persons.TryGetValue(pid, out var p) ? new PersonDto(p.FullName, p.PhoneNumber, p.Sex, p.Address, p.DateOfBirth) : null,
                groups.Where(g => g.UserId == u.Id).Select(g => new GroupBrief(g.Id, g.Name)).Distinct().OrderBy(g => g.Name).ToList(),
                u.CreatedDate, u.ModifiedDate)).ToList();
        }

        public static async Task<User> RequireAsync(DatabaseContext db, Guid id, CancellationToken ct)
            => await db.User.FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted, ct)
                ?? throw new BusinessRuleException(404, "NOT_FOUND", "Không tìm thấy người dùng.");

        public static async Task EnsureGroupsAsync(DatabaseContext db, IReadOnlyCollection<Guid> groupIds, CancellationToken ct)
        {
            var known = await db.UserGroup.AsNoTracking().Where(g => groupIds.Contains(g.Id) && !g.IsDeleted).Select(g => g.Id).ToListAsync(ct);
            var missing = groupIds.Except(known).ToList();
            if (missing.Count > 0)
            {
                throw new BusinessRuleException(422, "REF_NOT_FOUND", $"Nhóm không tồn tại: {string.Join(", ", missing)}.", "groupIds");
            }
        }

        public static string Normalize(string userName) => userName.Trim().ToLowerInvariant();
    }

    // ---------------------------------------------------------------- GET /admin/users
    public sealed record UserListQuery(int Page, int PageSize, string? Q, bool? IsActive, Guid? GroupId) : IQuery<UserPage>;

    public sealed class UserListHandler(DatabaseContext db) : IRequestHandler<UserListQuery, UserPage>
    {
        public async Task<UserPage> HandleAsync(UserListQuery q, CancellationToken ct)
        {
            IQueryable<User> users = db.User.Where(u => !u.IsDeleted);
            if (q.IsActive != null)
            {
                users = users.Where(u => u.IsActive == q.IsActive);
            }
            if (q.GroupId != null)
            {
                users = users.Where(u => db.UserClaim.Any(c => c.UserId == u.Id && c.UserGroupId == q.GroupId && !c.IsDeleted));
            }
            if (!string.IsNullOrWhiteSpace(q.Q))
            {
                string term = q.Q.Trim();
                users = users.Where(u => u.UserName!.Contains(term)
                    || db.Person.Any(p => p.Id == u.PersonId && (p.FullName!.Contains(term) || p.PhoneNumber!.Contains(term))));
            }
            int total = await users.CountAsync(ct);
            var page = await UserQueries.LoadAsync(db, users.OrderBy(u => u.UserName).Skip((q.Page - 1) * q.PageSize).Take(q.PageSize), ct);
            return new UserPage(page, total, q.Page, q.PageSize);
        }
    }

    public sealed record UserGetQuery(Guid Id) : IQuery<UserDto>;

    public sealed class UserGetHandler(DatabaseContext db) : IRequestHandler<UserGetQuery, UserDto>
    {
        public async Task<UserDto> HandleAsync(UserGetQuery q, CancellationToken ct)
            => (await UserQueries.LoadAsync(db, db.User.Where(u => u.Id == q.Id && !u.IsDeleted), ct)).FirstOrDefault()
                ?? throw new BusinessRuleException(404, "NOT_FOUND", "Không tìm thấy người dùng.");
    }

    // ---------------------------------------------------------------- POST /admin/users
    public sealed record CreateUserCommand(string? UserName, string? Password, PersonDto? Person, IReadOnlyList<Guid>? GroupIds) : ICommand<UserDto>;

    public sealed class CreateUserValidator : AbstractValidator<CreateUserCommand>
    {
        public CreateUserValidator()
        {
            RuleFor(x => x.UserName).NotEmpty().WithMessage("Tên đăng nhập là bắt buộc.")
                .Matches("^[A-Za-z0-9._@-]{3,100}$").WithMessage("Tên đăng nhập 3–100 ký tự: chữ, số, . _ @ -.");
            RuleFor(x => x.Password).NotEmpty().WithMessage("Mật khẩu là bắt buộc.")
                .MinimumLength(PasswordHasher.MinLength).WithMessage($"Mật khẩu tối thiểu {PasswordHasher.MinLength} ký tự.");
            RuleFor(x => x.Person!.FullName).NotEmpty().When(x => x.Person != null).WithMessage("Họ tên là bắt buộc.");
        }
    }

    /// <summary>Spec 3.2: transaction tạo Person → User (IsActive = 1) → UserClaim từng nhóm. UserName không trùng (service).</summary>
    public sealed class CreateUserHandler(DatabaseContext db, IDispatcher dispatcher, IDomainEventCollector events) : IRequestHandler<CreateUserCommand, UserDto>
    {
        public async Task<UserDto> HandleAsync(CreateUserCommand c, CancellationToken ct)
        {
            string userName = UserQueries.Normalize(c.UserName!);
            if (await db.User.AnyAsync(u => !u.IsDeleted && u.UserName == userName, ct))
            {
                throw new BusinessRuleException(409, "DUPLICATE", $"Tên đăng nhập '{userName}' đã tồn tại.", "userName");
            }
            var groupIds = (c.GroupIds ?? []).Distinct().ToList();
            await UserQueries.EnsureGroupsAsync(db, groupIds, ct);

            await using var tx = db.Database.IsRelational() && db.Database.CurrentTransaction == null ? await db.Database.BeginTransactionAsync(ct) : null;
            Person? person = null;
            if (c.Person != null)
            {
                person = new Person { Id = Guid.NewGuid(), FullName = c.Person.FullName?.Trim(), PhoneNumber = c.Person.PhoneNumber, Sex = c.Person.Sex, Address = c.Person.Address, DateOfBirth = c.Person.DateOfBirth };
                db.Person.Add(person);
            }
            var user = new User { Id = Guid.NewGuid(), UserName = userName, PassWord = PasswordHasher.Hash(c.Password!), IsActive = true, IsAdmin = false, PersonId = person?.Id };
            db.User.Add(user);
            foreach (var g in groupIds)
            {
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = user.Id, UserGroupId = g });
            }
            await db.SaveChangesAsync(ConstantHelper.TypeSave.Insert, ct);
            if (tx != null)
            {
                await tx.CommitAsync(ct);
            }
            events.Raise(new PermissionsChanged([user.Id]));
            return await dispatcher.SendAsync(new UserGetQuery(user.Id), ct);
        }
    }

    // ---------------------------------------------------------------- PUT /admin/users/{id}
    public sealed record UpdateUserCommand(Guid Id, PersonDto? Person) : ICommand<UserDto>;

    /// <summary>Sửa hồ sơ (Person). Tên đăng nhập / mật khẩu / quyền admin không sửa ở đây.</summary>
    public sealed class UpdateUserHandler(DatabaseContext db, IDispatcher dispatcher) : IRequestHandler<UpdateUserCommand, UserDto>
    {
        public async Task<UserDto> HandleAsync(UpdateUserCommand c, CancellationToken ct)
        {
            var user = await UserQueries.RequireAsync(db, c.Id, ct);
            if (c.Person != null)
            {
                var person = user.PersonId is { } pid ? await db.Person.FirstOrDefaultAsync(p => p.Id == pid && !p.IsDeleted, ct) : null;
                bool insert = person == null;
                if (person == null)
                {
                    person = new Person { Id = Guid.NewGuid() };
                    db.Person.Add(person);
                    user.PersonId = person.Id;
                }
                person.FullName = c.Person.FullName?.Trim();
                person.PhoneNumber = c.Person.PhoneNumber;
                person.Sex = c.Person.Sex;
                person.Address = c.Person.Address;
                person.DateOfBirth = c.Person.DateOfBirth;
                await db.SaveChangesAsync(insert ? ConstantHelper.TypeSave.Insert : ConstantHelper.TypeSave.Update, ct);
            }
            return await dispatcher.SendAsync(new UserGetQuery(c.Id), ct);
        }
    }

    // ---------------------------------------------------------------- PUT /admin/users/{id}/groups
    public sealed record SetUserGroupsCommand(Guid UserId, IReadOnlyList<Guid> GroupIds) : ICommand<IReadOnlyList<GroupBrief>>;

    /// <summary>Spec 3.2: so với UserClaim hiện tại – thêm mới, xóa mềm bản ghi bị bỏ.</summary>
    public sealed class SetUserGroupsHandler(DatabaseContext db, IDomainEventCollector events) : IRequestHandler<SetUserGroupsCommand, IReadOnlyList<GroupBrief>>
    {
        public async Task<IReadOnlyList<GroupBrief>> HandleAsync(SetUserGroupsCommand c, CancellationToken ct)
        {
            await UserQueries.RequireAsync(db, c.UserId, ct);
            var target = (c.GroupIds ?? []).Distinct().ToList();
            await UserQueries.EnsureGroupsAsync(db, target, ct);
            var current = await db.UserClaim.Where(x => x.UserId == c.UserId && !x.IsDeleted).ToListAsync(ct);
            foreach (var removed in current.Where(x => x.UserGroupId == null || !target.Contains(x.UserGroupId.Value)))
            {
                db.Entry(removed).State = EntityState.Modified;
                await db.SaveChangesAsync(ConstantHelper.TypeSave.Delete, ct);
            }
            foreach (var g in target.Where(g => !current.Any(x => x.UserGroupId == g)))
            {
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = c.UserId, UserGroupId = g });
            }
            await db.SaveChangesAsync(ConstantHelper.TypeSave.Insert, ct);
            events.Raise(new PermissionsChanged([c.UserId]));
            return await db.UserGroup.AsNoTracking().Where(g => target.Contains(g.Id)).OrderBy(g => g.Name).Select(g => new GroupBrief(g.Id, g.Name)).ToListAsync(ct);
        }
    }

    // ---------------------------------------------------------------- POST /admin/users/{id}/lock · /unlock
    public sealed record SetUserActiveCommand(Guid UserId, bool IsActive, string? Reason, Guid ActorUserId) : ICommand<bool>;

    public sealed class SetUserActiveValidator : AbstractValidator<SetUserActiveCommand>
    {
        public SetUserActiveValidator()
            => RuleFor(x => x.Reason).NotEmpty().When(x => !x.IsActive).WithMessage("Khóa tài khoản phải ghi lý do.").MaximumLength(500);
    }

    /// <summary>Đặt IsActive. Không tự khóa chính mình. Lý do: chưa có cột lưu → ghi log (GAP-ADM-01).</summary>
    public sealed class SetUserActiveHandler(DatabaseContext db, IDomainEventCollector events) : IRequestHandler<SetUserActiveCommand, bool>
    {
        public async Task<bool> HandleAsync(SetUserActiveCommand c, CancellationToken ct)
        {
            if (!c.IsActive && c.UserId == c.ActorUserId)
            {
                throw new BusinessRuleException(422, "BUSINESS_RULE", "Không thể tự khóa tài khoản của chính mình.");
            }
            var user = await UserQueries.RequireAsync(db, c.UserId, ct);
            user.IsActive = c.IsActive;
            await db.SaveChangesAsync(ConstantHelper.TypeSave.Update, ct);
            Serilog.Log.Information("{Action} tài khoản {UserId}: {Reason}", c.IsActive ? "Mở khóa" : "Khóa", c.UserId, c.Reason);
            events.Raise(new PermissionsChanged([c.UserId]));
            return user.IsActive;
        }
    }

    // ---------------------------------------------------------------- POST /admin/users/{id}/reset-password
    public sealed record ResetPasswordResult(string TemporaryPassword);

    public sealed record ResetPasswordCommand(Guid UserId) : ICommand<ResetPasswordResult>;

    /// <summary>Sinh mật khẩu tạm (trả MỘT lần trong response, không ghi log). Chỉ admin gọi được (controller).</summary>
    public sealed class ResetPasswordHandler(DatabaseContext db) : IRequestHandler<ResetPasswordCommand, ResetPasswordResult>
    {
        public async Task<ResetPasswordResult> HandleAsync(ResetPasswordCommand c, CancellationToken ct)
        {
            var user = await UserQueries.RequireAsync(db, c.UserId, ct);
            string temp = PasswordHasher.Temporary();
            user.PassWord = PasswordHasher.Hash(temp);
            await db.SaveChangesAsync(ConstantHelper.TypeSave.Update, ct);
            return new ResetPasswordResult(temp);
        }
    }

    // ---------------------------------------------------------------- POST /auth/change-password
    public sealed record ChangePasswordCommand(Guid UserId, string? OldPassword, string? NewPassword) : ICommand<bool>;

    public sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordCommand>
    {
        public ChangePasswordValidator()
        {
            RuleFor(x => x.OldPassword).NotEmpty().WithMessage("Mật khẩu cũ là bắt buộc.");
            RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(PasswordHasher.MinLength).WithMessage($"Mật khẩu mới tối thiểu {PasswordHasher.MinLength} ký tự.")
                .NotEqual(x => x.OldPassword).WithMessage("Mật khẩu mới phải khác mật khẩu cũ.");
        }
    }

    /// <summary>Spec 3.1: đổi mật khẩu của chính mình; audit không chứa giá trị mật khẩu (DatabaseContext chỉ ghi hash – PassWord bị ẩn ở API).</summary>
    public sealed class ChangePasswordHandler(DatabaseContext db) : IRequestHandler<ChangePasswordCommand, bool>
    {
        public async Task<bool> HandleAsync(ChangePasswordCommand c, CancellationToken ct)
        {
            var user = await UserQueries.RequireAsync(db, c.UserId, ct);
            if (!PasswordHasher.Verify(c.OldPassword!, user.PassWord))
            {
                throw new BusinessRuleException(422, "BUSINESS_RULE", "Mật khẩu cũ không đúng.", "oldPassword");
            }
            user.PassWord = PasswordHasher.Hash(c.NewPassword!);
            await db.SaveChangesAsync(ConstantHelper.TypeSave.Update, ct);
            return true;
        }
    }
}
