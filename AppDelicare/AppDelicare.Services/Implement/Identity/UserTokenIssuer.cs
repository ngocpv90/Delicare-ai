using System.Security.Claims;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace AppDelicare.Services.Implement.Identity
{
    /// <summary>
    /// BOT-1.2 (D-62 §1.4): JWT thay mặt user đã liên kết cho container messaging – cùng khóa ký / issuer / audience với đăng nhập web (JwtFactoryService cũ),
    /// hạn <c>Messaging:UserTokenMinutes</c> (10′), claim <c>client = MESSAGING</c>. Bot gọi API nghiệp vụ bằng token này → PermissionFilter / ObjectMenu như web,
    /// CreatedBy = user thật. Chỉ cấp khi: định danh Active / PendingCode có UserId, User còn hoạt động, có quyền vào bot (#/bot-van-hanh / #/bot-dieu-hanh).
    /// PendingCode → thêm claim <c>bot_limited = 1</c> (PermissionService bỏ quyền duyệt + admin). Mỗi lần cấp / từ chối ghi IntegrationLog TOKEN.
    /// </summary>
    public class UserTokenIssuer(IUnitOfWork unitOfWork, IClock clock, ICurrentActor currentActor, IConfiguration configuration,
        IOptionsMonitor<MessagingOptions> options, IPermissionService permissionService, IJwtFactoryService jwtFactory) : IUserTokenIssuer
    {
        public async Task<IResult<UserTokenModel>> Issue(BotIdentityRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.BotCode) || string.IsNullOrWhiteSpace(request.ExternalUserId))
            {
                return Result<UserTokenModel>.Fail(ConstantHelper.Message.BotIdentityInvalid);
            }
            string bot = request.BotCode.Trim().ToUpperInvariant();
            var db = unitOfWork.DbContext;
            var identity = await TelegramLinkService.FindIdentityAsync(db, bot, request.ExternalUserId);
            string? reason = null;
            User? user = null;
            if (identity == null || !MessagingCode.Identity.IsUsable(identity.IdentityStatus))
            {
                reason = ConstantHelper.Message.BotNotLinked;
            }
            else if (identity.UserId == null)
            {
                reason = ConstantHelper.Message.BotNoWebAccount;
            }
            else
            {
                user = await db.User.AsNoTracking().FirstOrDefaultAsync(u => u.Id == identity.UserId && !u.IsDeleted);
                if (user == null || !user.IsActive)
                {
                    reason = ConstantHelper.Message.BotUserInactive;
                }
                else if (!TelegramLinkService.CanUseBot(await permissionService.GetAsync(user.Id), bot))
                {
                    reason = string.Format(ConstantHelper.Message.BotNoBotAccess, bot);
                }
            }
            if (!Guid.TryParse(configuration[$"{SystemUsersOptions.Section}:{nameof(SystemUsersOptions.JobUserId)}"], out Guid system) || system == Guid.Empty)
            {
                return Result<UserTokenModel>.Fail(ConstantHelper.Message.JobUserMissing);
            }
            DateTime now = clock.Now;
            bool limited = identity?.IdentityStatus == MessagingCode.Identity.PendingCode;
            using (currentActor.BeginScope(ActorInfo.ForSystem(system)))
            {
                unitOfWork.IntegrationLogRepository.Add(new IntegrationLog
                {
                    Id = Guid.NewGuid(), IntegrationChannelId = identity?.IntegrationChannelId, DirectionCode = MessagingCode.Log.Token, OperationCode = MessagingCode.Log.OpIssueToken,
                    CorrelationId = identity?.Id.ToString("N"), RequestAt = now, ResponseAt = now,
                    LogStatus = reason == null ? MessagingCode.Log.StatusSuccess : MessagingCode.Log.StatusFailed,
                    ErrorMessage = reason, ErrorCode = reason == null ? (limited ? "LIMITED" : null) : "DENIED",
                });
                await unitOfWork.IntegrationLogRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
            }
            if (reason != null)
            {
                return Result<UserTokenModel>.Fail(reason);
            }
            TimeSpan validFor = TimeSpan.FromMinutes(Math.Clamp(options.CurrentValue.UserTokenMinutes, 1, 60));
            var claims = new List<Claim> { new(MessagingCode.Claims.Client, MessagingCode.ClientMessaging) };
            if (limited)
            {
                claims.Add(new Claim(MessagingCode.Claims.BotLimited, "1"));
            }
            string token = await jwtFactory.GenerateEncodedToken(user!.UserName ?? user.Id.ToString(), jwtFactory.GenerateClaimsIdentity(user.UserName ?? string.Empty, user.Id), claims, validFor);
            return Result<UserTokenModel>.Success(new UserTokenModel { Token = token, ExpiresAt = now.Add(validFor), UserId = user.Id, Limited = limited },
                ConstantHelper.Message.BotTokenIssued);
        }
    }
}
