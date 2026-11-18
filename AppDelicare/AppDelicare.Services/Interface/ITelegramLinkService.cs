using AppDelicare.Data.Interface;
using AppDelicare.Model.Model;

namespace AppDelicare.Services.Interface
{
    /// <summary>BOT-1.2 (D-62): liên kết Telegram ↔ tài khoản / nhân viên (bot: số điện thoại, mã; web: tạo mã, xem, gỡ; admin: liên kết tay – Catalog 220).</summary>
    public interface ITelegramLinkService
    {
        Task<IResult<BotIdentityModel>> Resolve(BotIdentityRequest request);
        Task<IResult<BotIdentityModel>> LinkByPhone(BotLinkByPhoneRequest request);
        Task<IResult<BotIdentityModel>> LinkByCode(BotLinkByCodeRequest request);
        Task<IResult<BotIdentityModel>> Touch(BotTouchRequest request);
        /// <summary>BOT-1.3: /huylienket trên bot (cả người chỉ có EmployeeId – không JWT).</summary>
        Task<IResult<bool>> UnlinkByBot(BotIdentityRequest request);
        Task<IResult<LinkCodeModel>> CreateCode();
        Task<IResult<LinkCodeModel>> CreateCodeForEmployee(Guid? employeeId);
        Task<IResults<MyIdentityModel>> GetMine();
        Task<IResult<bool>> UnlinkMine(Guid id);
        Task<IResult<BotIdentityModel>> AdminLink(IdentityLinkRequest request);
        Task<IResult<bool>> AdminUnlink(Guid id);
    }
}
