using AppDelicare.Model.Helper;
using AppDelicare.Services.Interface;

namespace AppDelicare.Services.Implement.Approval
{
    /// <summary>
    /// BE2.2 – duyệt hợp đồng (BusinessType CONTRACT, spec 6.1 "1 cấp Ban giám đốc"; quyền duyệt = mục menu <c>#/wf-inbox?type=crm-contract</c>
    /// "Duyệt – Hợp đồng suất ăn", D-31). Chạy trên engine 1 cấp T12; BE2.3 (duyệt nhiều cấp) chỉ đổi engine – <see cref="ApproveAsync(Guid, Guid, string?)"/>
    /// được gọi ở bước cuối, handler không phải sửa. Nội dung được duyệt = đầu hợp đồng + loại suất (sửa sau khi gửi → không duyệt được).
    /// </summary>
    public class ContractApprovalHandler(IContractService contractService) : IApprovalHandler
    {
        private readonly IContractService _contractService = contractService;

        public string BusinessType => ContractStatus.BusinessType;

        public string ScreenId => ContractStatus.ScreenId;

        public Task<string?> BuildPayloadAsync(Guid businessId) => _contractService.BuildApprovalStateAsync(businessId);

        public Task<string?> DescribeAsync(Guid businessId) => _contractService.DescribeAsync(businessId);

        public Task<string?> ApproveAsync(Guid businessId, Guid actorUserId, string? note) => _contractService.ApproveFromEngineAsync(businessId, note);

        public Task<string?> RejectAsync(Guid businessId, Guid actorUserId, string note) => _contractService.RejectFromEngineAsync(businessId, note);
    }
}
