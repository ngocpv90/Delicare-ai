using AppDelicare.Model.Helper;
using AppDelicare.Services.Interface;

namespace AppDelicare.Services.Implement.Approval
{
    /// <summary>
    /// BE2.6 (spec 11.3, T21) – duyệt đơn nghỉ (BusinessType LEAVE_REQUEST; quyền duyệt = mục menu <c>#/wf-inbox?type=hr-leave</c> "Phê duyệt › Đơn xin nghỉ", seed T07:
    /// CEO / COO / FIN-HR). Mặc định 1 cấp (đổi bằng <c>BusinessRules:ApprovalSteps:LEAVE_REQUEST</c>). Duyệt → Đã duyệt + bỏ các ca chưa chấm công trong khoảng nghỉ;
    /// từ chối → Từ chối; người gửi hủy → Đã hủy.
    /// </summary>
    public class LeaveRequestApprovalHandler(ILeaveRequestService leaveRequestService) : IApprovalHandler
    {
        private readonly ILeaveRequestService _service = leaveRequestService;

        public string BusinessType => LeaveRequestStatus.BusinessType;

        public string ScreenId => LeaveRequestStatus.ScreenId;

        public Task<string?> BuildPayloadAsync(Guid businessId) => _service.BuildApprovalStateAsync(businessId);

        public Task<string?> DescribeAsync(Guid businessId) => _service.DescribeAsync(businessId);

        public Task<string?> ApproveAsync(Guid businessId, Guid actorUserId, string? note) => _service.ApplyDecisionAsync(businessId, LeaveRequestStatus.Approved);

        public Task<string?> RejectAsync(Guid businessId, Guid actorUserId, string note) => _service.ApplyDecisionAsync(businessId, LeaveRequestStatus.Rejected);

        public Task<string?> CancelAsync(Guid businessId, Guid actorUserId, string reason) => _service.ApplyDecisionAsync(businessId, LeaveRequestStatus.Cancelled);

        /// <summary>APR-1.2 (D-81 e): khuôn tóm tắt chung – ApprovalSummaries.</summary>
        public Task<AppDelicare.Model.Model.ApprovalSummaryModel?> GetSummaryAsync(Guid businessId, ApprovalSummaryContext ctx) => ApprovalSummaries.LeaveAsync(businessId, ctx);
    }
}
