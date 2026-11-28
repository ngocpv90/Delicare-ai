using AppDelicare.Data.Interface;
using AppDelicare.Data.Share;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;

namespace AppDelicare.Services.Interface
{
    /// <summary>UI-6.51 (D-137): kế hoạch mua tự động – job lập / cập nhật đề nghị theo hạn đặt, duyệt theo rủi ro, tự gửi NCC; màn "Kế hoạch mua".</summary>
    public interface IPurchasePlanService
    {
        Task<IResult<PurchasePlanBoardModel>> GetBoard(PurchasePlanBoardRequest request);
        Task<IResult<PurchasePlanCellDetailModel>> GetCell(PurchasePlanCellRequest request);
        /// <summary>Chạy ngay đúng luồng job (phần đến hạn / một ô "Đặt ngay").</summary>
        Task<IResult<PurchasePlanRunResultModel>> Run(PurchasePlanRunRequest request);
        Task<IResult<bool>> OverrideSubstitute(PurchasePlanOverrideSubstituteRequest request);
        Task<IResult<bool>> OverrideSplit(PurchasePlanOverrideSplitRequest request);
        /// <summary>Job 14:00 / 20:15 (<paramref name="first"/> = lần 14:00 – báo nguyên liệu tới hạn chưa có đơn mua).</summary>
        Task<PurchasePlanRunResultModel> RunJobAsync(bool first);
        /// <summary>Job giờ gửi NCC: gửi đơn mua Đã duyệt của kế hoạch mua; lỗi / không kênh → chờ gửi tay + bot; NCC chưa xác nhận sát giờ giao → bot.</summary>
        Task<(int Sent, int Failed, int Unconfirmed)> SendDueAsync();
        /// <summary>Gửi các đơn mua Đã duyệt lập từ một đề nghị (sau giờ gửi gọi ngay khi duyệt).</summary>
        Task<int> SendForRequestAsync(Guid purchaseRequestId);
    }
}
