using AppDelicare.Services.Interface;

namespace AppDelicare.Services.Implement.Approval
{
    /// <summary>
    /// ACC-4 (D-131 h): duyệt mở khóa kỳ kế toán – <c>PERIOD_UNLOCK</c>, 1 cấp COO (mục <c>#/wf-inbox?type=acc-period-unlock</c> "Phê duyệt › Mở khóa kỳ").
    /// BusinessId = mã kỳ (AccountingPeriodRules.PeriodId); duyệt → AuditChange Unlock; từ chối / hủy → kỳ vẫn khóa.
    /// </summary>
    public class PeriodUnlockApprovalHandler(IAccountingPeriodService periods) : IApprovalHandler
    {
        private readonly IAccountingPeriodService _periods = periods;

        public string BusinessType => AccountingPeriodService.UnlockBusinessType;

        public string ScreenId => AccountingPeriodService.UnlockScreenId;

        public Task<string?> BuildPayloadAsync(Guid businessId) => Task.FromResult<string?>($"{{\"type\":\"{BusinessType}\",\"periodId\":\"{businessId}\"}}");

        public Task<string?> DescribeAsync(Guid businessId) => Task.FromResult<string?>(AccountingPeriodService.DescribePeriod(businessId));

        public Task<string?> ApproveAsync(Guid businessId, Guid actorUserId, string? note) => _periods.ApplyUnlockAsync(businessId, actorUserId, null, note);

        public Task<string?> ApproveAsync(Guid businessId, Guid actorUserId, string? note, string? proposalJson) => _periods.ApplyUnlockAsync(businessId, actorUserId, proposalJson, note);

        public Task<string?> RejectAsync(Guid businessId, Guid actorUserId, string note) => Task.FromResult<string?>(null);
    }
}
