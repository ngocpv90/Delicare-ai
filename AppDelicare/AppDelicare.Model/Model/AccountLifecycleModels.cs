#nullable enable
using System.ComponentModel.DataAnnotations;

namespace AppDelicare.Model.Model
{
    /// <summary>HR-ACC-1.6 (D-82 f, g): nội dung đề xuất của yêu cầu tài khoản (PayloadJson.proposal). IT đổi <see cref="Groups"/> được trước khi Duyệt.</summary>
    public class AccountProposalModel
    {
        /// <summary>Tên nhóm quyền (UserGroup.Name) sẽ cấp – ghi đè toàn bộ nhóm hiện tại khi duyệt.</summary>
        public List<string> Groups { get; set; } = [];
        /// <summary>Nhóm hiện tại lúc gửi (Đổi nhóm quyền) – chỉ để hiển thị.</summary>
        public List<string> PreviousGroups { get; set; } = [];
        public string? PositionCode { get; set; }
        public string? PositionName { get; set; }
        public string? Reason { get; set; }
    }

    /// <summary>HR-ACC-1.8 (D-82 j): khối Tài khoản trên Nhân viên 360.</summary>
    public class EmployeeAccountModel
    {
        /// <summary>None · Pending · Active · Locked (AccountLifecycleCodes).</summary>
        public string Status { get; set; } = "None";
        public string? StatusName { get; set; }
        public Guid? UserId { get; set; }
        public string? UserName { get; set; }
        public bool? UserIsActive { get; set; }
        public string? AccountType { get; set; }
        public List<string> Groups { get; set; } = [];
        /// <summary>Nhóm gợi ý theo chức danh (BusinessRules:PositionDefaultGroups).</summary>
        public List<string> SuggestedGroups { get; set; } = [];
        /// <summary>Lần đăng nhập cuối – chưa có chỗ lưu (GAP-ACC-01) → luôn null.</summary>
        public DateTime? LastLoginAt { get; set; }
        /// <summary>Đã gắn qua Employee.PersonId (false = còn đoán cũ / chưa có).</summary>
        public bool Linked { get; set; }
        public Guid? PendingRequestId { get; set; }
        public string? PendingRequestNo { get; set; }
        public string? PendingType { get; set; }
        public string? PendingTypeName { get; set; }
        /// <summary>Nút theo quyền của người xem: request (NS – Đề nghị cấp), create / lock / reset (IT).</summary>
        public List<string> Actions { get; set; } = [];
    }

    /// <summary>HR-ACC-1.8: 1 dòng màn Tài khoản – nhân viên gắn với tài khoản + nhãn loại + cảnh báo đã nghỉ còn mở.</summary>
    public class AccountLinkRowModel
    {
        public Guid UserId { get; set; }
        public string? UserName { get; set; }
        public bool IsActive { get; set; }
        public string AccountType { get; set; } = "INTERNAL";
        public string? KindName { get; set; }
        public Guid? EmployeeId { get; set; }
        public string? EmployeeCode { get; set; }
        public string? EmployeeName { get; set; }
        public string? OrganizationUnitName { get; set; }
        public string? EmployeeStatus { get; set; }
        public string? EmployeeStatusName { get; set; }
        /// <summary>Đã gắn qua PersonId (false + có nhân viên = đoán cũ).</summary>
        public bool Linked { get; set; }
        /// <summary>Nhân viên đã nghỉ nhưng tài khoản còn mở (D-82 j – chỉ tiêu = 0).</summary>
        public bool ResignedButActive { get; set; }
    }

    public class AccountLinkSummaryModel
    {
        public List<AccountLinkRowModel> ListRow { get; set; } = [];
        public int ResignedButActiveCount { get; set; }
        public int UnlinkedCount { get; set; }
        /// <summary>Nhân viên đang làm chưa có tài khoản.</summary>
        public int EmployeeWithoutAccountCount { get; set; }
    }

    /// <summary>HR-ACC-1.8: tab Đối chiếu – nhân viên chưa gắn Person + ứng viên (cùng 3 cách của script D82).</summary>
    public class LinkReviewRowModel
    {
        public Guid EmployeeId { get; set; }
        public string? EmployeeCode { get; set; }
        public string? EmployeeName { get; set; }
        public string? Phone { get; set; }
        public string? OrganizationUnitName { get; set; }
        public string? Status { get; set; }
        /// <summary>SURE (khớp chắc) · MULTI (nhiều ứng viên) · NONE (không khớp).</summary>
        public string Group { get; set; } = "NONE";
        public string? GroupName { get; set; }
        public List<LinkCandidateModel> ListCandidate { get; set; } = [];
    }

    public class LinkCandidateModel
    {
        public Guid PersonId { get; set; }
        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
        public List<string> Sources { get; set; } = [];
        public string? SourceText { get; set; }
        public Guid? UserId { get; set; }
        public string? UserName { get; set; }
        public bool? UserIsActive { get; set; }
        public string? LinkedEmployee { get; set; }
    }

    public class LinkReviewModel
    {
        public List<LinkReviewRowModel> ListRow { get; set; } = [];
        public int SureCount { get; set; }
        public int MultiCount { get; set; }
        public int NoneCount { get; set; }
        public int LinkedCount { get; set; }
    }

    /// <summary>Employee/RequestAccount – NS đề nghị cấp tài khoản (hoặc Employee/RequestReopen).</summary>
    public class EmployeeAccountRequest
    {
        [Required]
        public Guid EmployeeId { get; set; }
        /// <summary>Nhóm đề xuất (tên nhóm, cách nhau dấu phẩy) – trống = gợi ý theo chức danh.</summary>
        [MaxLength(1000)]
        public string? Groups { get; set; }
        [MaxLength(500)]
        public string? Reason { get; set; }
    }

    /// <summary>Employee/LinkPerson – gắn nhân viên với 1 Person (hoặc với Person của 1 tài khoản). Cả 2 trống = gỡ liên kết.</summary>
    public class EmployeeLinkPersonRequest
    {
        [Required]
        public Guid EmployeeId { get; set; }
        public Guid? PersonId { get; set; }
        public Guid? UserId { get; set; }
        public bool Unlink { get; set; }
    }

    /// <summary>User/CreateForEmployee – IT (Thêm adm-user) tạo thẳng tài khoản cho nhân viên, không qua duyệt.</summary>
    public class UserCreateForEmployeeRequest
    {
        [Required]
        public Guid EmployeeId { get; set; }
        [MaxLength(1000)]
        public string? Groups { get; set; }
    }

    /// <summary>Kết quả tạo tài khoản: mật khẩu tạm trả 1 lần khi không gửi được qua bot.</summary>
    public class AccountCreatedModel
    {
        public Guid UserId { get; set; }
        public string? UserName { get; set; }
        public List<string> Groups { get; set; } = [];
        /// <summary>Có = IT xem 1 lần trên màn (nhân viên chưa liên kết bot). null = đã gửi qua bot.</summary>
        public string? TemporaryPassword { get; set; }
        public bool SentByBot { get; set; }
    }

    /// <summary>HR-ACC-1.8 (D-82 j): "Việc của tôi" – tài khoản đang đăng nhập đã gắn nhân viên chưa.</summary>
    public class MyEmployeeLinkModel
    {
        public bool Linked { get; set; }
        /// <summary>Chưa gắn nhưng đang dùng tạm cách đoán cũ (tên đăng nhập = mã NV / liên kết bot / SĐT) – vẫn chấm công được, NS cần đối chiếu.</summary>
        public bool Guessed { get; set; }
        public Guid? EmployeeId { get; set; }
        public string? EmployeeCode { get; set; }
        public string? EmployeeName { get; set; }
    }

    /// <summary>Khóa tài khoản theo nghỉ việc (D-82 h) – kết quả cho job / test.</summary>
    public class AccountLockResultModel
    {
        public int LockedUsers { get; set; }
        public int RevokedIdentities { get; set; }
        public int EscalatedRequests { get; set; }
        public int NotifiedRequests { get; set; }
    }
}
