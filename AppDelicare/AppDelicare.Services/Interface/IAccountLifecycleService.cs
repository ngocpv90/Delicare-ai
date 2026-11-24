using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Model;

namespace AppDelicare.Services.Interface
{
    /// <summary>
    /// HR-ACC-1 (D-82): tài khoản ↔ con người ↔ nhân viên + vòng đời tài khoản. Yêu cầu qua engine duyệt (USER_ACCOUNT_CREATE / GROUP / REOPEN, người duyệt FIN-IT),
    /// tự khóa khi nghỉ việc, tab Đối chiếu, khối Tài khoản trên Nhân viên 360, cột nhân viên trên màn Tài khoản.
    /// </summary>
    public interface IAccountLifecycleService
    {
        // ---- API
        Task<IResult<Guid>> RequestCreate(EmployeeAccountRequest request);
        Task<IResult<Guid>> RequestReopen(EmployeeAccountRequest request);
        Task<IResult<AccountCreatedModel>> CreateForEmployee(UserCreateForEmployeeRequest request);
        Task<IResult<EmployeeAccountModel>> GetEmployeeAccount(Guid employeeId);
        Task<IResult<AccountLinkSummaryModel>> GetAccountLinks();
        Task<IResult<LinkReviewModel>> GetLinkReview(string? group);
        Task<IResult<bool>> LinkPerson(EmployeeLinkPersonRequest request);
        /// <summary>Mật khẩu tạm của tài khoản vừa tạo khi không gửi được qua bot – trả 1 lần (IT), sau đó xóa.</summary>
        Task<IResult<AccountCreatedModel>> TakeCreated(Guid employeeId);
        /// <summary>"Việc của tôi": tài khoản đang đăng nhập đã gắn nhân viên chưa (thông báo trỏ tới NS).</summary>
        Task<IResult<MyEmployeeLinkModel>> GetMyEmployee();
        /// <summary>Màn Chức vụ: nhóm quyền gợi ý theo mã chức danh (BusinessRules:PositionDefaultGroups).</summary>
        IResult<Dictionary<string, List<string>>> GetPositionGroups();

        // ---- dùng trong transaction của nghiệp vụ khác (EmployeeService, handler duyệt, job)
        Task<string?> ApplyCreateAsync(Guid employeeId, string? proposalJson, Guid actorUserId);
        Task<string?> ApplyGroupAsync(Guid employeeId, string? proposalJson);
        Task<string?> ApplyReopenAsync(Guid employeeId);
        /// <summary>Sau khi lưu nhân viên: đổi trạng thái (nghỉ → khóa; quay lại → yêu cầu mở lại), đổi chức danh (→ yêu cầu đổi nhóm), đồng bộ Person (D-82 e).</summary>
        Task<string?> AfterEmployeeSavedAsync(string? oldStatus, Guid? oldPositionId, Employee saved, bool requestAccount, string? accountGroups);
        Task<AccountLockResultModel> LockForResignedAsync(Guid employeeId);
        /// <summary>Job hằng ngày: khóa tài khoản còn mở của nhân viên đã nghỉ (đổi trạng thái ngoài API) + báo FIN-IT.</summary>
        Task<AccountLockResultModel> LockAllResignedAsync();

        // ---- cho handler duyệt
        Task<string?> BuildStateAsync(string businessType, Guid employeeId);
        Task<string?> DescribeAsync(string businessType, Guid employeeId);
        Task<ApprovalSummaryModel?> SummaryAsync(string businessType, Guid employeeId, ApprovalSummaryContext ctx);
        Task<(string? Proposal, string? Error)> ReviseProposalAsync(string? submitted, string revised);
    }
}
