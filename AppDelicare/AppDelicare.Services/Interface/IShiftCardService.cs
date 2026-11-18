using AppDelicare.Data.Interface;
using AppDelicare.Model.Model;

namespace AppDelicare.Services.Interface
{
    /// <summary>BOT-2.1: Thẻ ca (BOT_TELEGRAM §1.1) – xếp thẻ tới hạn qua outbox + nội dung mới nhất để messaging sửa tin đã ghim.</summary>
    public interface IShiftCardService
    {
        Task<IResults<ShiftCardModel>> Sync();
        Task<IResult<ShiftCardModel>> Mine(BotIdentityRequest request);
    }
}
