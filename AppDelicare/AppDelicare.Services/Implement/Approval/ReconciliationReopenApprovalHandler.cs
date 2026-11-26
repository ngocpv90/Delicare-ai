using AppDelicare.Services.Interface;

namespace AppDelicare.Services.Implement.Approval
{
    /// <summary>
    /// ACC-3 (D-131 g): duyệt mở lại đối soát Đã chốt – <c>RECON_REOPEN</c>, 1 cấp COO (mục <c>#/wf-inbox?type=crm-recon-reopen</c> "Phê duyệt › Mở lại đối soát").
    /// Nội dung được duyệt = khóa đối soát (lý do nằm ở đề xuất lưu lúc gửi). Duyệt → đối soát về Khớp / Chênh lệch, đơn về Đã giao; từ chối / hủy → không đổi.
    /// </summary>
    public class ReconciliationReopenApprovalHandler(IOrderReconciliationService reconciliation) : IApprovalHandler
    {
        private readonly IOrderReconciliationService _reconciliation = reconciliation;

        public string BusinessType => OrderReconciliationService.ReopenBusinessType;

        public string ScreenId => OrderReconciliationService.ReopenScreenId;

        public Task<string?> BuildPayloadAsync(Guid businessId) => Task.FromResult<string?>($"{{\"type\":\"{BusinessType}\",\"reconciliationId\":\"{businessId}\"}}");

        public Task<string?> DescribeAsync(Guid businessId) => _reconciliation.DescribeReopenAsync(businessId);

        public Task<string?> ApproveAsync(Guid businessId, Guid actorUserId, string? note) => _reconciliation.ApplyReopenAsync(businessId, null, note);

        public Task<string?> ApproveAsync(Guid businessId, Guid actorUserId, string? note, string? proposalJson) => _reconciliation.ApplyReopenAsync(businessId, proposalJson, note);

        public Task<string?> RejectAsync(Guid businessId, Guid actorUserId, string note) => Task.FromResult<string?>(null);
    }
}
