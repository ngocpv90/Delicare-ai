using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Services.Interface;

namespace AppDelicare.Services.Implement.Approval
{
    /// <summary>
    /// HR-ACC-1.6 (D-82 f–h): 3 loại duyệt tài khoản – BusinessId = Employee.Id, người duyệt FIN-IT (mục <c>#/wf-inbox?type=adm-user</c> "Phê duyệt › Tài khoản").
    /// Khuôn tóm tắt APR-1.2: nhân viên, đơn vị, chức danh, nhóm gợi ý, lý do; IT sửa nhóm quyền được trước khi Duyệt (IApprovalProposalEditor).
    /// Từ chối / hủy không đổi gì (chưa tạo tài khoản / giữ quyền cũ / giữ khóa).
    /// </summary>
    public abstract class UserAccountApprovalHandlerBase(IAccountLifecycleService accounts) : IApprovalHandler, IApprovalProposalEditor
    {
        protected IAccountLifecycleService Accounts { get; } = accounts;

        public abstract string BusinessType { get; }

        public string ScreenId => AccountLifecycleCodes.ScreenId;

        public Task<string?> BuildPayloadAsync(Guid businessId) => Accounts.BuildStateAsync(BusinessType, businessId);

        public Task<string?> DescribeAsync(Guid businessId) => Accounts.DescribeAsync(BusinessType, businessId);

        public Task<string?> ApproveAsync(Guid businessId, Guid actorUserId, string? note) => ApproveAsync(businessId, actorUserId, note, null);

        public abstract Task<string?> ApproveAsync(Guid businessId, Guid actorUserId, string? note, string? proposalJson);

        public Task<string?> RejectAsync(Guid businessId, Guid actorUserId, string note) => Task.FromResult<string?>(null);

        public Task<ApprovalSummaryModel?> GetSummaryAsync(Guid businessId, ApprovalSummaryContext ctx) => Accounts.SummaryAsync(BusinessType, businessId, ctx);

        /// <summary>Không ai duyệt yêu cầu tài khoản của chính mình (D-79 a5 / D-82 i).</summary>
        public Task<Guid?> SubjectEmployeeIdAsync(Guid businessId) => Task.FromResult<Guid?>(businessId);

        public virtual Task<(string? Proposal, string? Error)> ReviseProposalAsync(Guid businessId, string? submitted, string revised)
            => Accounts.ReviseProposalAsync(submitted, revised);
    }

    /// <summary>USER_ACCOUNT_CREATE – cấp tài khoản cho nhân viên mới.</summary>
    public class UserAccountCreateApprovalHandler(IAccountLifecycleService accounts) : UserAccountApprovalHandlerBase(accounts)
    {
        public override string BusinessType => AccountLifecycleCodes.CreateBusinessType;

        public override Task<string?> ApproveAsync(Guid businessId, Guid actorUserId, string? note, string? proposalJson)
            => Accounts.ApplyCreateAsync(businessId, proposalJson, actorUserId);
    }

    /// <summary>USER_ACCOUNT_GROUP – đổi nhóm quyền khi đổi chức danh.</summary>
    public class UserAccountGroupApprovalHandler(IAccountLifecycleService accounts) : UserAccountApprovalHandlerBase(accounts)
    {
        public override string BusinessType => AccountLifecycleCodes.GroupBusinessType;

        public override Task<string?> ApproveAsync(Guid businessId, Guid actorUserId, string? note, string? proposalJson)
            => Accounts.ApplyGroupAsync(businessId, proposalJson);
    }

    /// <summary>USER_ACCOUNT_REOPEN – mở lại tài khoản khi nhân viên quay lại làm việc (không sửa đề xuất).</summary>
    public class UserAccountReopenApprovalHandler(IAccountLifecycleService accounts) : UserAccountApprovalHandlerBase(accounts)
    {
        public override string BusinessType => AccountLifecycleCodes.ReopenBusinessType;

        public override Task<string?> ApproveAsync(Guid businessId, Guid actorUserId, string? note, string? proposalJson)
            => Accounts.ApplyReopenAsync(businessId);

        public override Task<(string? Proposal, string? Error)> ReviseProposalAsync(Guid businessId, string? submitted, string revised)
            => Task.FromResult<(string?, string?)>((null, ConstantHelper.Message.ApprovalProposalNotEditable));
    }
}
