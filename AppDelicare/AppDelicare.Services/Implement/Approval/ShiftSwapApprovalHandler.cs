using AppDelicare.Model.Helper;
using AppDelicare.Services.Interface;

namespace AppDelicare.Services.Implement.Approval
{
    /// <summary>
    /// BE2.6 (spec 11.3, T21) – duyệt đơn đổi ca (BusinessType SHIFT_SWAP; quyền duyệt = mục menu <c>#/wf-inbox?type=hr-swap</c> "Phê duyệt › Đơn đổi ca", seed T07:
    /// CEO / COO / FIN-HR). Mặc định 1 cấp (đổi bằng <c>BusinessRules:ApprovalSteps:SHIFT_SWAP</c>). Duyệt → hoán đổi / chuyển ca trong transaction của engine (kiểm lại
    /// chưa chấm công, không trùng ca, không nghỉ phép); từ chối → Từ chối; người gửi hủy → Đã hủy.
    /// </summary>
    public class ShiftSwapApprovalHandler(IShiftSwapRequestService shiftSwapRequestService) : IApprovalHandler
    {
        private readonly IShiftSwapRequestService _service = shiftSwapRequestService;

        public string BusinessType => ShiftSwapStatus.BusinessType;

        public string ScreenId => ShiftSwapStatus.ScreenId;

        public Task<string?> BuildPayloadAsync(Guid businessId) => _service.BuildApprovalStateAsync(businessId);

        public Task<string?> DescribeAsync(Guid businessId) => _service.DescribeAsync(businessId);

        public Task<string?> ApproveAsync(Guid businessId, Guid actorUserId, string? note) => _service.ApplyDecisionAsync(businessId, ShiftSwapStatus.Approved);

        public Task<string?> RejectAsync(Guid businessId, Guid actorUserId, string note) => _service.ApplyDecisionAsync(businessId, ShiftSwapStatus.Rejected);

        public Task<string?> CancelAsync(Guid businessId, Guid actorUserId, string reason) => _service.ApplyDecisionAsync(businessId, ShiftSwapStatus.Cancelled);
    }
}
