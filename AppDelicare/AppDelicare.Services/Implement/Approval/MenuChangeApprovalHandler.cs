using AppDelicare.Model.Helper;
using AppDelicare.Services.Interface;

namespace AppDelicare.Services.Implement.Approval
{
    /// <summary>
    /// BE2.2 – duyệt yêu cầu đổi món (BusinessType MENU_CHANGE, spec 6.4; quyền duyệt = mục menu <c>#/wf-inbox?type=menu-change-request</c>
    /// "Duyệt – Yêu cầu đổi món", D-31). Duyệt → Đã duyệt + cập nhật kế hoạch Nháp của ngày; từ chối → Từ chối; người gửi hủy → Đã hủy.
    /// Engine 1 cấp T12 (BE2.3 đổi engine nhiều cấp, handler không đổi).
    /// </summary>
    public class MenuChangeApprovalHandler(IMenuChangeRequestService menuChangeRequestService) : IApprovalHandler
    {
        private readonly IMenuChangeRequestService _service = menuChangeRequestService;

        public string BusinessType => MenuChangeStatus.BusinessType;

        public string ScreenId => MenuChangeStatus.ScreenId;

        public Task<string?> BuildPayloadAsync(Guid businessId) => _service.BuildApprovalStateAsync(businessId);

        public Task<string?> DescribeAsync(Guid businessId) => _service.DescribeAsync(businessId);

        public Task<string?> ApproveAsync(Guid businessId, Guid actorUserId, string? note) => _service.ApproveFromEngineAsync(businessId, actorUserId);

        public Task<string?> RejectAsync(Guid businessId, Guid actorUserId, string note) => _service.CloseFromEngineAsync(businessId, false);

        public Task<string?> CancelAsync(Guid businessId, Guid actorUserId, string reason) => _service.CloseFromEngineAsync(businessId, true);
    }
}
