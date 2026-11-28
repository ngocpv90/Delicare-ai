using AppDelicare.Data.Connection;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Services.Implement.Messaging;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Serilog;

namespace AppDelicare.Services.Implement.Identity
{
    /// <summary>
    /// BOT-1.2 (D-62, BOT_TELEGRAM §2): định danh Telegram ↔ tài khoản web / nhân viên. Mỗi (bot, Telegram user id) 1 dòng <c>UserExternalIdentity</c>;
    /// mỗi người 1 Telegram trên 1 bot (liên kết tài khoản Telegram mới → định danh cũ của người đó trên bot đó chuyển Revoked).
    /// <list type="bullet">
    /// <item><b>Số điện thoại</b>: chỉ số của chính tài khoản Telegram đang chat (contact.user_id = from.id); khớp DUY NHẤT (≤ 1 nhân viên, ≤ 1 tài khoản) – 0 / nhiều → từ chối,
    /// gợi ý liên hệ Nhân sự. Người có quyền duyệt (mục A) hoặc admin → <c>PendingCode</c>: dùng bot với token hạn chế (không duyệt / quản trị) tới khi nhập mã web.</item>
    /// <item><b>Mã web</b> (<see cref="LinkCodeSigner"/>): user tự tạo (15′) hoặc HR cấp cho nhân viên (72 giờ); 1 lần (IntegrationLog LINK theo băm mã).</item>
    /// </list>
    /// Thao tác của bot ghi dưới tài khoản <c>system</c>; mọi liên kết / gỡ ghi IntegrationLog LINK (CorrelationId = Id định danh).
    /// </summary>
    public class TelegramLinkService(IUnitOfWork unitOfWork, IClock clock, ICurrentActor currentActor, IConfiguration configuration,
        IOptionsMonitor<MessagingOptions> options, IPermissionService permissionService) : ITelegramLinkService
    {
        private MessagingOptions Options => options.CurrentValue;
        private DatabaseContext Db => unitOfWork.DbContext;

        // ================================================================ phía bot (BotChannel, khóa client)

        public async Task<IResult<BotIdentityModel>> Resolve(BotIdentityRequest request)
        {
            var (channel, error) = await ChannelAsync(request);
            if (error != null)
            {
                return Result<BotIdentityModel>.Fail(error);
            }
            var identity = await FindAsync(channel, request.ExternalUserId!);
            return Result<BotIdentityModel>.Success(await ModelAsync(identity, request.BotCode!), ConstantHelper.Message.GetAllSuccessfully);
        }

        public async Task<IResult<BotIdentityModel>> LinkByPhone(BotLinkByPhoneRequest request)
        {
            var (channel, error) = await ChannelAsync(request);
            if (error != null)
            {
                return Result<BotIdentityModel>.Fail(error);
            }
            if (string.IsNullOrWhiteSpace(request.ContactUserId) || request.ContactUserId.Trim() != request.ExternalUserId!.Trim())
            {
                return Result<BotIdentityModel>.Fail(ConstantHelper.Message.BotPhoneNotOwn);
            }
            var match = await PhoneMatcher.MatchAsync(Db, request.Phone);
            if (!match.IsUnique)
            {
                Log.Information("[TelegramLink] khớp số điện thoại không duy nhất trên bot {Bot}: {Employees} nhân viên, {Users} tài khoản",
                    request.BotCode, match.EmployeeIds.Count, match.UserIds.Count);
                return Result<BotIdentityModel>.Fail(ConstantHelper.Message.BotPhoneNoMatch);
            }
            return await LinkAsync(channel, request, match.UserIds.FirstOrDefault().NullIfEmpty(), match.EmployeeIds.FirstOrDefault().NullIfEmpty(),
                MessagingCode.Log.OpLinkPhone, codeVerified: false, usedKey: null);
        }

        public async Task<IResult<BotIdentityModel>> LinkByCode(BotLinkByCodeRequest request)
        {
            var (channel, error) = await ChannelAsync(request);
            if (error != null)
            {
                return Result<BotIdentityModel>.Fail(error);
            }
            if (string.IsNullOrWhiteSpace(Options.LinkSigningKey))
            {
                return Result<BotIdentityModel>.Fail(ConstantHelper.Message.BotLinkKeyMissing);
            }
            string? reason = LinkCodeSigner.Verify(request.Code, Options.LinkSigningKey, clock.UtcNow, out char kind, out Guid id, out _);
            if (reason != null)
            {
                return Result<BotIdentityModel>.Fail(reason);
            }
            string usedKey = LinkCodeSigner.UsedKey(request.Code!);
            if (await Db.IntegrationLog.AsNoTracking().AnyAsync(l => !l.IsDeleted && l.IdempotencyKey == usedKey && l.LogStatus == MessagingCode.Log.StatusSuccess))
            {
                return Result<BotIdentityModel>.Fail(ConstantHelper.Message.BotLinkCodeUsed);
            }
            if (kind == MessagingCode.LinkKind.CustomerContact)
            {
                return Result<BotIdentityModel>.Fail(ConstantHelper.Message.BotLinkCodeInvalid); // AI-19: mã khách chỉ dùng ở bot Zalo khách
            }
            Guid? userId = null, employeeId = null;
            if (kind == MessagingCode.LinkKind.User)
            {
                if (!await Db.User.AsNoTracking().AnyAsync(u => u.Id == id && !u.IsDeleted && u.IsActive && (u.AccountType == null || u.AccountType == PersonLink.Internal))) // HR-ACC-1.4: bot chỉ nhận tài khoản nội bộ
                {
                    return Result<BotIdentityModel>.Fail(ConstantHelper.Message.BotUserInactive);
                }
                userId = id;
            }
            else
            {
                var employee = await Db.Employee.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);
                if (employee == null || !EmployeeStatus.IsWorking(employee.Status))
                {
                    return Result<BotIdentityModel>.Fail(ConstantHelper.Message.HrEmployeeNotFound);
                }
                employeeId = id;
                // Nhân viên đã liên kết bằng số điện thoại cùng tài khoản web → giữ UserId (cùng người).
                var existing = await FindAsync(channel, request.ExternalUserId!);
                if (existing?.EmployeeId == id)
                {
                    userId = existing.UserId;
                }
            }
            return await LinkAsync(channel, request, userId, employeeId, MessagingCode.Log.OpLinkCode, codeVerified: true, usedKey: usedKey);
        }

        public async Task<IResult<BotIdentityModel>> Touch(BotTouchRequest request)
        {
            var (channel, error) = await ChannelAsync(request);
            if (error != null)
            {
                return Result<BotIdentityModel>.Fail(error);
            }
            var identity = await FindAsync(channel, request.ExternalUserId!, tracking: true);
            if (identity == null)
            {
                return Result<BotIdentityModel>.Success(new BotIdentityModel(), ConstantHelper.Message.GetAllSuccessfully);
            }
            using var scope = SystemScope(out string? missing);
            if (missing != null)
            {
                return Result<BotIdentityModel>.Fail(missing);
            }
            identity.LastSeenAt = clock.Now;
            identity.ExternalUsername = MessagingRules.Truncate(request.ExternalUsername, 255) ?? identity.ExternalUsername;
            identity.DisplayName = MessagingRules.Truncate(request.DisplayName, 255) ?? identity.DisplayName;
            if (request.Blocked == true && MessagingCode.Identity.IsUsable(identity.IdentityStatus))
            {
                identity.IdentityStatus = MessagingCode.Identity.Blocked;
            }
            else if (request.Blocked == false && identity.IdentityStatus == MessagingCode.Identity.Blocked)
            {
                // Người dùng /start lại sau khi chặn: trả về trạng thái đúng (người duyệt chưa nhập mã → PendingCode).
                identity.IdentityStatus = await StatusForAsync(identity.UserId, identity.Id);
            }
            unitOfWork.UserExternalIdentityRepository.Update(identity);
            await unitOfWork.UserExternalIdentityRepository.CommitAsync(ConstantHelper.TypeSave.Update);
            return Result<BotIdentityModel>.Success(await ModelAsync(identity, request.BotCode!), ConstantHelper.Message.SaveSuccess);
        }

        /// <summary>BOT-1.3: người dùng gõ /huylienket – định danh của chính Telegram id đó trên bot đó → Revoked (ghi dưới system).</summary>
        public async Task<IResult<bool>> UnlinkByBot(BotIdentityRequest request)
        {
            var (channel, error) = await ChannelAsync(request);
            if (error != null)
            {
                return Result<bool>.Fail(error);
            }
            var identity = await FindAsync(channel, request.ExternalUserId!, tracking: true);
            if (identity == null || identity.IdentityStatus == MessagingCode.Identity.Revoked)
            {
                return Result<bool>.Fail(ConstantHelper.Message.BotNotLinked);
            }
            using var scope = SystemScope(out string? missing);
            return missing != null ? Result<bool>.Fail(missing) : await RevokeAsync(identity);
        }

        // ================================================================ phía web (JWT user)

        public async Task<IResult<LinkCodeModel>> CreateCode()
        {
            Guid userId = currentActor.RequireActor().UserId;
            if (!await Db.User.AsNoTracking().AnyAsync(u => u.Id == userId && !u.IsDeleted && u.IsActive && (u.AccountType == null || u.AccountType == PersonLink.Internal))) // HR-ACC-1.4
            {
                return Result<LinkCodeModel>.Fail(ConstantHelper.Message.BotUserInactive);
            }
            return await CodeAsync(MessagingCode.LinkKind.User, userId, TimeSpan.FromMinutes(Math.Max(1, Options.UserCodeMinutes)));
        }

        public async Task<IResult<LinkCodeModel>> CreateCodeForEmployee(Guid? employeeId)
        {
            var employee = employeeId == null ? null : await Db.Employee.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId && !e.IsDeleted);
            if (employee == null || !EmployeeStatus.IsWorking(employee.Status))
            {
                return Result<LinkCodeModel>.Fail(ConstantHelper.Message.HrEmployeeNotFound);
            }
            return await CodeAsync(MessagingCode.LinkKind.Employee, employee.Id, TimeSpan.FromHours(Math.Max(1, Options.EmployeeCodeHours)));
        }

        public async Task<IResults<MyIdentityModel>> GetMine()
        {
            Guid userId = currentActor.RequireActor().UserId;
            var botOf = await BotOfChannelAsync();
            var rows = await Db.UserExternalIdentity.AsNoTracking().Where(i => !i.IsDeleted && i.UserId == userId).OrderByDescending(i => i.LinkedAt).ToListAsync();
            return Results<MyIdentityModel>.Success(rows.Select(i => new MyIdentityModel
            {
                Id = i.Id, BotCode = botOf.GetValueOrDefault(i.IntegrationChannelId), ExternalUsername = i.ExternalUsername, DisplayName = i.DisplayName,
                IdentityStatus = i.IdentityStatus, LinkedAt = i.LinkedAt, LastSeenAt = i.LastSeenAt,
            }).ToList(), ConstantHelper.Message.GetAllSuccessfully);
        }

        /// <summary>Gỡ liên kết của chính mình (web) – chỉ định danh có UserId = người đăng nhập.</summary>
        public async Task<IResult<bool>> UnlinkMine(Guid id)
        {
            Guid userId = currentActor.RequireActor().UserId;
            var identity = await Db.UserExternalIdentity.FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted && i.UserId == userId);
            return identity == null ? Result<bool>.Fail(ConstantHelper.Message.RecordNotFoundMessage) : await RevokeAsync(identity);
        }

        /// <summary>Catalog 220: admin gỡ liên kết bất kỳ.</summary>
        public async Task<IResult<bool>> AdminUnlink(Guid id)
        {
            var identity = await Db.UserExternalIdentity.FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
            return identity == null ? Result<bool>.Fail(ConstantHelper.Message.RecordNotFoundMessage) : await RevokeAsync(identity);
        }

        /// <summary>Catalog 220: admin liên kết tay (đã xác minh trực tiếp) → Active, coi như đã xác nhận mã.</summary>
        public async Task<IResult<BotIdentityModel>> AdminLink(IdentityLinkRequest request)
        {
            var (channel, error) = await ChannelAsync(new BotIdentityRequest { BotCode = request.BotCode, ExternalUserId = request.ExternalUserId });
            if (error != null)
            {
                return Result<BotIdentityModel>.Fail(error);
            }
            if (request.UserId == null && request.EmployeeId == null)
            {
                return Result<BotIdentityModel>.Fail(ConstantHelper.Message.BotRecipientRequired);
            }
            if (request.UserId is { } u && !await Db.User.AsNoTracking().AnyAsync(x => x.Id == u && !x.IsDeleted && x.IsActive))
            {
                return Result<BotIdentityModel>.Fail(ConstantHelper.Message.BotUserInactive);
            }
            if (request.EmployeeId is { } e && !await Db.Employee.AsNoTracking().AnyAsync(x => x.Id == e && !x.IsDeleted))
            {
                return Result<BotIdentityModel>.Fail(ConstantHelper.Message.HrEmployeeNotFound);
            }
            return await LinkAsync(channel, new BotIdentityRequest
            {
                BotCode = request.BotCode, ExternalUserId = request.ExternalUserId, ExternalUsername = request.ExternalUsername, DisplayName = request.DisplayName,
            }, request.UserId, request.EmployeeId, MessagingCode.Log.OpLinkAdmin, codeVerified: true, usedKey: null, asSystem: false);
        }

        // ================================================================ dùng chung (cả UserTokenIssuer / BotEmployeeService)

        /// <summary>Người có quyền duyệt (mục A – AllowEdit trên #/wf-inbox?type=…) hoặc admin → phải nhập mã web (BOT_TELEGRAM §2).</summary>
        public static bool RequiresCode(UserPermissions full)
            => full.IsAdmin || full.Menus.Any(m => m.Key.StartsWith(IPermissionService.ApproveMenuPrefix, StringComparison.OrdinalIgnoreCase) && m.Value.Access && m.Value.Edit);

        public static string MenuOfBot(string botCode)
            => string.Equals(botCode, MessagingCode.BotDieuHanh, StringComparison.OrdinalIgnoreCase) ? MessagingCode.MenuBotDieuHanh : MessagingCode.MenuBotVanHanh;

        public static bool CanUseBot(UserPermissions full, string botCode)
            => full.IsAdmin || (full.Menus.TryGetValue(MenuOfBot(botCode), out var r) && r.Access);

        public static async Task<UserExternalIdentity?> FindIdentityAsync(DatabaseContext db, string botCode, string externalUserId, bool tracking = false)
        {
            var bots = await RecipientResolver.ActiveBotsAsync(db);
            if (!bots.TryGetValue(botCode.Trim(), out Guid channel))
            {
                return null;
            }
            var q = db.UserExternalIdentity.Where(i => !i.IsDeleted && i.IntegrationChannelId == channel && i.ExternalUserId == externalUserId.Trim());
            return await (tracking ? q : q.AsNoTracking()).FirstOrDefaultAsync();
        }

        // ================================================================ nội bộ

        private async Task<IResult<BotIdentityModel>> LinkAsync(Guid channel, BotIdentityRequest request, Guid? userId, Guid? employeeId, string operation,
            bool codeVerified, string? usedKey, bool asSystem = true)
        {
            using var scope = asSystem ? SystemScope(out string? missing) : null;
            if (asSystem && scope == null)
            {
                return Result<BotIdentityModel>.Fail(ConstantHelper.Message.JobUserMissing);
            }
            using var tran = await unitOfWork.OpenTransaction();
            try
            {
                string external = request.ExternalUserId!.Trim();
                DateTime now = clock.Now;
                // Dòng cũ của (bot, Telegram id) – kể cả đã xóa mềm (UQ_UserExternalIdentity_Channel_User) → dùng lại.
                var identity = await Db.UserExternalIdentity.FirstOrDefaultAsync(i => i.IntegrationChannelId == channel && i.ExternalUserId == external && !i.IsDeleted)
                    ?? await Db.UserExternalIdentity.FirstOrDefaultAsync(i => i.IntegrationChannelId == channel && i.ExternalUserId == external);
                bool isNew = identity == null;
                identity ??= new UserExternalIdentity { Id = Guid.NewGuid(), IntegrationChannelId = channel, ExternalUserId = MessagingRules.Truncate(external, 255)! };
                identity.UserId = userId;
                identity.EmployeeId = employeeId;
                identity.ExternalUsername = MessagingRules.Truncate(request.ExternalUsername, 255) ?? identity.ExternalUsername;
                identity.DisplayName = MessagingRules.Truncate(request.DisplayName, 255) ?? identity.DisplayName;
                identity.LinkedAt = now;
                identity.LastSeenAt = now;
                identity.IsDeleted = false;
                // Người có quyền duyệt chỉ khớp số → PendingCode; đã nhập mã / admin liên kết → Active (ghi log trước để StatusFor thấy).
                identity.IdentityStatus = codeVerified ? MessagingCode.Identity.Active : await StatusForAsync(userId, isNew ? null : identity.Id);
                // Mỗi người 1 Telegram trên 1 bot: định danh khác của cùng người trên bot này → Revoked.
                var others = await Db.UserExternalIdentity.Where(i => !i.IsDeleted && i.IntegrationChannelId == channel && i.Id != identity.Id
                        && (i.IdentityStatus == MessagingCode.Identity.Active || i.IdentityStatus == MessagingCode.Identity.PendingCode)
                        && ((userId != null && i.UserId == userId) || (employeeId != null && i.EmployeeId == employeeId)))
                    .ToListAsync();
                foreach (var other in others)
                {
                    other.IdentityStatus = MessagingCode.Identity.Revoked;
                    unitOfWork.UserExternalIdentityRepository.Update(other);
                }
                if (isNew)
                {
                    unitOfWork.UserExternalIdentityRepository.Add(identity);
                }
                else
                {
                    unitOfWork.UserExternalIdentityRepository.Update(identity);
                }
                AddLog(channel, MessagingCode.Log.Link, operation, identity.Id, usedKey, MessagingCode.Log.StatusSuccess, now);
                await unitOfWork.UserExternalIdentityRepository.CommitAsync(isNew ? ConstantHelper.TypeSave.Insert : ConstantHelper.TypeSave.Update);
                unitOfWork.CommitTransaction();
                return Result<BotIdentityModel>.Success(await ModelAsync(identity, request.BotCode!), ConstantHelper.Message.BotLinked);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " LinkAsync: ", ex.ToString()));
                unitOfWork.RollbackTransaction();
                throw;
            }
        }

        private async Task<IResult<bool>> RevokeAsync(UserExternalIdentity identity)
        {
            identity.IdentityStatus = MessagingCode.Identity.Revoked;
            unitOfWork.UserExternalIdentityRepository.Update(identity);
            AddLog(identity.IntegrationChannelId, MessagingCode.Log.Link, MessagingCode.Log.OpUnlink, identity.Id, null, MessagingCode.Log.StatusSuccess, clock.Now);
            await unitOfWork.UserExternalIdentityRepository.CommitAsync(ConstantHelper.TypeSave.Update);
            return Result<bool>.Success(true, ConstantHelper.Message.BotUnlinked);
        }

        private async Task<string> StatusForAsync(Guid? userId, Guid? identityId)
        {
            if (userId == null)
            {
                return MessagingCode.Identity.Active;
            }
            var full = await permissionService.GetAsync(userId.Value);
            if (!RequiresCode(full))
            {
                return MessagingCode.Identity.Active;
            }
            if (identityId is { } iid)
            {
                string corr = iid.ToString("N");
                bool verified = await Db.IntegrationLog.AsNoTracking().AnyAsync(l => !l.IsDeleted && l.CorrelationId == corr && l.LogStatus == MessagingCode.Log.StatusSuccess
                    && (l.OperationCode == MessagingCode.Log.OpLinkCode || l.OperationCode == MessagingCode.Log.OpLinkAdmin));
                if (verified)
                {
                    return MessagingCode.Identity.Active;
                }
            }
            return MessagingCode.Identity.PendingCode;
        }

        private async Task<BotIdentityModel> ModelAsync(UserExternalIdentity? identity, string botCode)
        {
            if (identity == null || !MessagingCode.Identity.IsUsable(identity.IdentityStatus))
            {
                return new BotIdentityModel { IdentityId = identity?.Id, Status = identity?.IdentityStatus };
            }
            var model = new BotIdentityModel
            {
                Linked = true, IdentityId = identity.Id, UserId = identity.UserId, EmployeeId = identity.EmployeeId, Status = identity.IdentityStatus,
                NeedsCode = identity.IdentityStatus == MessagingCode.Identity.PendingCode, HasWebAccount = identity.UserId != null,
            };
            if (identity.UserId is { } uid)
            {
                var full = await permissionService.GetAsync(uid);
                model.CanUseBot = CanUseBot(full, botCode);
                model.CanUseDieuHanh = CanUseBot(full, MessagingCode.BotDieuHanh);
                // BOT-1.4: quyền để bot dựng menu – menu được vào + mục được Duyệt rõ (không tính admin); PendingCode → không có mục duyệt.
                bool pending = identity.IdentityStatus == MessagingCode.Identity.PendingCode;
                model.IsAdmin = full.IsAdmin && !pending;
                model.Menus = [.. full.Menus.Where(m => m.Value.Access && !m.Key.StartsWith(IPermissionService.ApproveMenuPrefix, StringComparison.OrdinalIgnoreCase)).Select(m => m.Key)];
                model.ApproveScreens = pending ? []
                    : [.. full.Menus.Where(m => m.Key.StartsWith(IPermissionService.ApproveMenuPrefix, StringComparison.OrdinalIgnoreCase) && m.Value.Access && m.Value.Edit)
                        .Select(m => m.Key[IPermissionService.ApproveMenuPrefix.Length..])];
                model.FullName = await (from u in Db.User.AsNoTracking() join p in Db.Person.AsNoTracking() on u.PersonId equals p.Id
                                        where u.Id == uid select p.FullName).FirstOrDefaultAsync();
            }
            else
            {
                // Chỉ có EmployeeId: dùng bot VH (tự phục vụ nhân sự – BotChannel/Employee*), không dùng bot DH.
                model.CanUseBot = !string.Equals(botCode, MessagingCode.BotDieuHanh, StringComparison.OrdinalIgnoreCase);
            }
            if (identity.EmployeeId is { } eid && model.FullName == null)
            {
                model.FullName = await Db.Employee.AsNoTracking().Where(e => e.Id == eid).Select(e => e.FullName).FirstOrDefaultAsync();
            }
            return model;
        }

        private async Task<IResult<LinkCodeModel>> CodeAsync(char kind, Guid id, TimeSpan validFor)
        {
            if (string.IsNullOrWhiteSpace(Options.LinkSigningKey) || Options.LinkSigningKey.Length < 32)
            {
                return Result<LinkCodeModel>.Fail(ConstantHelper.Message.BotLinkKeyMissing);
            }
            DateTime expiresUtc = clock.UtcNow.Add(validFor);
            string code = LinkCodeSigner.Create(kind, id, expiresUtc, Options.LinkSigningKey);
            var bots = await Db.BotConfiguration.AsNoTracking()
                .Where(b => !b.IsDeleted && b.BotStatus == MessagingCode.StatusActive && b.BotUsername != null && b.BotUsername != "")
                .Select(b => new { b.BotCode, b.BotUsername }).ToListAsync();
            return Result<LinkCodeModel>.Success(new LinkCodeModel
            {
                Code = code,
                ExpiresAt = clock.Now.Add(validFor),
                DeepLinks = bots.Where(b => b.BotCode is MessagingCode.BotVanHanh or MessagingCode.BotDieuHanh)
                    .ToDictionary(b => b.BotCode, b => $"https://t.me/{b.BotUsername!.TrimStart('@')}?start={code}"),
            }, ConstantHelper.Message.BotCodeCreated);
        }

        private async Task<(Guid Channel, string? Error)> ChannelAsync(BotIdentityRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.BotCode) || string.IsNullOrWhiteSpace(request.ExternalUserId))
            {
                return (Guid.Empty, ConstantHelper.Message.BotIdentityInvalid);
            }
            request.BotCode = request.BotCode.Trim().ToUpperInvariant();
            var bots = await RecipientResolver.ActiveBotsAsync(Db);
            return bots.TryGetValue(request.BotCode, out Guid channel) ? (channel, null) : (Guid.Empty, string.Format(ConstantHelper.Message.BotUnknown, request.BotCode));
        }

        private async Task<UserExternalIdentity?> FindAsync(Guid channel, string externalUserId, bool tracking = false)
        {
            var q = Db.UserExternalIdentity.Where(i => !i.IsDeleted && i.IntegrationChannelId == channel && i.ExternalUserId == externalUserId.Trim());
            return await (tracking ? q : q.AsNoTracking()).FirstOrDefaultAsync();
        }

        private async Task<Dictionary<Guid, string>> BotOfChannelAsync()
            => (await Db.BotConfiguration.AsNoTracking().Where(b => !b.IsDeleted).Select(b => new { b.IntegrationChannelId, b.BotCode }).ToListAsync())
                .GroupBy(b => b.IntegrationChannelId).ToDictionary(g => g.Key, g => g.First().BotCode);

        private IDisposable? SystemScope(out string? missing)
        {
            missing = null;
            if (Guid.TryParse(configuration[$"{SystemUsersOptions.Section}:{nameof(SystemUsersOptions.JobUserId)}"], out Guid system) && system != Guid.Empty)
            {
                return currentActor.BeginScope(ActorInfo.ForSystem(system));
            }
            missing = ConstantHelper.Message.JobUserMissing;
            return null;
        }

        private void AddLog(Guid channel, string direction, string operation, Guid identityId, string? key, string status, DateTime now)
        {
            unitOfWork.IntegrationLogRepository.Add(new IntegrationLog
            {
                Id = Guid.NewGuid(), IntegrationChannelId = channel, DirectionCode = direction, OperationCode = operation,
                CorrelationId = identityId.ToString("N"), IdempotencyKey = key, RequestAt = now, ResponseAt = now, LogStatus = status,
            });
        }
    }

    internal static class GuidExtensions
    {
        public static Guid? NullIfEmpty(this Guid value) => value == Guid.Empty ? null : value;
    }
}
