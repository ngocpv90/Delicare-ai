using AppDelicare.Data.Interface;
using AppDelicare.Model.Model;

namespace AppDelicare.Services.Interface
{
    /// <summary>BOT-1.2 (D-62): JWT 10′ thay mặt user đã liên kết bot (claim client = MESSAGING; PendingCode → bot_limited).</summary>
    public interface IUserTokenIssuer
    {
        Task<IResult<UserTokenModel>> Issue(BotIdentityRequest request);
    }
}
