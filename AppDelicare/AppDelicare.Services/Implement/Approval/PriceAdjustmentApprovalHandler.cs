using AppDelicare.Model.Helper;
using AppDelicare.Services.Interface;

namespace AppDelicare.Services.Implement.Approval
{
    /// <summary>
    /// UI-6.4 (D-83): duyệt "Phụ lục điều chỉnh giá" (BusinessId = PricePolicy.Id) – mục quyền <c>#/wf-inbox?type=crm-price-policy</c> (CEO – D-79 d).
    /// Duyệt → Hiệu lực (giá áp từ ngày hiệu lực; đơn đã chốt giữ giá – BIZ-1.5); Từ chối → Từ chối kèm lý do.
    /// </summary>
    public class PriceAdjustmentApprovalHandler(IPriceAdjustmentService service) : IApprovalHandler
    {
        public string BusinessType => PriceAdjustmentStatus.BusinessType;

        public string ScreenId => PriceAdjustmentStatus.ScreenId;

        public Task<string?> BuildPayloadAsync(Guid businessId) => service.BuildStateAsync(businessId);

        public Task<string?> DescribeAsync(Guid businessId) => service.DescribeAsync(businessId);

        public Task<string?> ApproveAsync(Guid businessId, Guid actorUserId, string? note) => service.ApproveFromEngineAsync(businessId, note);

        public Task<string?> RejectAsync(Guid businessId, Guid actorUserId, string note) => service.RejectFromEngineAsync(businessId, note);

        public Task<AppDelicare.Model.Model.ApprovalSummaryModel?> GetSummaryAsync(Guid businessId, ApprovalSummaryContext ctx) => service.SummaryAsync(businessId, ctx);
    }
}
