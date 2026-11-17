using AppDelicare.Model.Helper;
using AppDelicare.Services.Interface;

namespace AppDelicare.Services.Implement.Approval
{
    /// <summary>
    /// BE2.3 (spec 7.8 submit / post, 2.3; BUSINESS_RULES §5) – duyệt phiếu kiểm kê có dòng lệch &gt; <c>StockCountVariancePctNeedApproval</c> (2%), BusinessType
    /// STOCK_COUNT. Mặc định 2 cấp theo spec 2.3: Quản lý kho (mục <c>#/wf-inbox?type=scm-stockcount</c>) → Kế toán (<c>scm-stockcount-l2</c>) – PROPOSED (D-52);
    /// đổi số cấp bằng <c>BusinessRules:ApprovalSteps:STOCK_COUNT</c>. Cấp cuối duyệt → ghi ApprovedBy (cho phép ghi sổ); từ chối → phiếu về Đang kiểm.
    /// </summary>
    public class StockCountApprovalHandler(IStockCountService stockCountService) : IApprovalHandler
    {
        private readonly IStockCountService _stockCountService = stockCountService;

        public string BusinessType => StockCountStatus.BusinessType;

        public string ScreenId => StockCountStatus.ScreenId;

        public IReadOnlyCollection<string> ApprovalScreenIds { get; } = [StockCountStatus.ScreenId, StockCountStatus.Level2ScreenId];

        public Task<IReadOnlyList<string>> StepScreenIdsAsync(Guid businessId)
            => Task.FromResult<IReadOnlyList<string>>([StockCountStatus.ScreenId, StockCountStatus.Level2ScreenId]);

        public Task<string?> BuildPayloadAsync(Guid businessId) => _stockCountService.BuildApprovalStateAsync(businessId);

        public Task<string?> DescribeAsync(Guid businessId) => _stockCountService.DescribeAsync(businessId);

        public Task<string?> ApproveAsync(Guid businessId, Guid actorUserId, string? note) => _stockCountService.ApplyDecisionAsync(businessId, true, actorUserId, note);

        public Task<string?> RejectAsync(Guid businessId, Guid actorUserId, string note) => _stockCountService.ApplyDecisionAsync(businessId, false, actorUserId, note);
    }
}
