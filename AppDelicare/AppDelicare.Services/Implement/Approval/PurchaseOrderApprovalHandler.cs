using AppDelicare.Model.Helper;
using AppDelicare.Services.Interface;

namespace AppDelicare.Services.Implement.Approval
{
    /// <summary>
    /// T15 – duyệt đơn mua NCC 1 cấp (BusinessType PURCHASE_ORDER, màn scm-po; quyền duyệt = mục menu <c>#/wf-inbox?type=scm-po</c>
    /// "Duyệt – Đơn mua hàng", D-31). Nội dung được duyệt = trạng thái + NCC + tổng tiền + dòng (sửa sau khi gửi → không duyệt được).
    /// Duyệt → Đã duyệt; từ chối → về Nháp (lý do ghi vào PO). Ngưỡng GĐ duyệt (D-11) chỉ hiển thị trong tiêu đề (Pilot 1 cấp).
    /// </summary>
    public class PurchaseOrderApprovalHandler(IPurchaseOrderService purchaseOrderService) : IApprovalHandler
    {
        private readonly IPurchaseOrderService _purchaseOrderService = purchaseOrderService;

        public string BusinessType => PurchaseOrderStatus.BusinessType;

        public string ScreenId => PurchaseOrderStatus.ScreenId;

        public Task<string?> BuildPayloadAsync(Guid businessId) => _purchaseOrderService.BuildApprovalStateAsync(businessId);

        public Task<string?> DescribeAsync(Guid businessId) => _purchaseOrderService.DescribeAsync(businessId);

        public Task<string?> ApproveAsync(Guid businessId, Guid actorUserId, string? note) => _purchaseOrderService.ApplyDecisionAsync(businessId, true, note);

        public Task<string?> RejectAsync(Guid businessId, Guid actorUserId, string note) => _purchaseOrderService.ApplyDecisionAsync(businessId, false, note);
    }
}
