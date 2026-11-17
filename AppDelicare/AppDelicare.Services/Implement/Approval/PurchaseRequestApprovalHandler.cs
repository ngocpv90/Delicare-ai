using AppDelicare.Model.Helper;
using AppDelicare.Services.Interface;

namespace AppDelicare.Services.Implement.Approval
{
    /// <summary>
    /// BE2.3 (D-46, anh chốt 2026-11-16) – duyệt đề nghị mua **2 cấp** (BusinessType PURCHASE_REQUEST): cấp 1 Trưởng bộ phận Mua hàng (mục
    /// <c>#/wf-inbox?type=scm-pr-l1</c>) → cấp 2 Giám đốc vận hành (<c>#/wf-inbox?type=scm-pr-l2</c>); nhóm được duyệt từng cấp = UserRole trên mục đó
    /// (seed PROPOSED: OPS-SCM / COO, anh sửa trên ma trận quyền). Engine chỉ gọi <see cref="ApproveAsync"/> ở cấp cuối → Đã duyệt; từ chối ở cấp nào → về Nháp.
    /// Cấu hình <c>BusinessRules:ApprovalSteps:PURCHASE_REQUEST</c> đổi được số cấp.
    /// </summary>
    public class PurchaseRequestApprovalHandler(IPurchaseRequestService purchaseRequestService) : IApprovalHandler
    {
        private readonly IPurchaseRequestService _purchaseRequestService = purchaseRequestService;

        public string BusinessType => PurchaseRequestStatus.BusinessType;

        public string ScreenId => PurchaseRequestStatus.Level1ScreenId;

        public IReadOnlyCollection<string> ApprovalScreenIds { get; } = [PurchaseRequestStatus.Level1ScreenId, PurchaseRequestStatus.Level2ScreenId];

        public Task<IReadOnlyList<string>> StepScreenIdsAsync(Guid businessId)
            => Task.FromResult<IReadOnlyList<string>>([PurchaseRequestStatus.Level1ScreenId, PurchaseRequestStatus.Level2ScreenId]);

        public Task<string?> BuildPayloadAsync(Guid businessId) => _purchaseRequestService.BuildApprovalStateAsync(businessId);

        public Task<string?> DescribeAsync(Guid businessId) => _purchaseRequestService.DescribeAsync(businessId);

        public Task<string?> ApproveAsync(Guid businessId, Guid actorUserId, string? note) => _purchaseRequestService.ApplyDecisionAsync(businessId, true, note);

        public Task<string?> RejectAsync(Guid businessId, Guid actorUserId, string note) => _purchaseRequestService.ApplyDecisionAsync(businessId, false, note);
    }
}
