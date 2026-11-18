namespace AppDelicare.Model.Model
{
    /// <summary>BOT-1.2: người dùng Telegram trên 1 bot (container messaging gửi kèm mọi lệnh định danh).</summary>
    public class BotIdentityRequest
    {
        public string? BotCode { get; set; }
        /// <summary>Telegram user id (from.id).</summary>
        public string? ExternalUserId { get; set; }
        public string? ExternalUsername { get; set; }
        public string? DisplayName { get; set; }
    }

    /// <summary>BOT-1.2: liên kết bằng nút chia sẻ số điện thoại (request_contact). messaging đã kiểm contact.user_id = from.id; api kiểm lại.</summary>
    public class BotLinkByPhoneRequest : BotIdentityRequest
    {
        public string? Phone { get; set; }
        /// <summary>contact.user_id – phải bằng ExternalUserId (số của chính tài khoản đang chat).</summary>
        public string? ContactUserId { get; set; }
    }

    /// <summary>BOT-1.2: liên kết bằng mã lấy trên web (/start &lt;mã&gt;).</summary>
    public class BotLinkByCodeRequest : BotIdentityRequest
    {
        public string? Code { get; set; }
    }

    /// <summary>BOT-1.2: cập nhật lần thấy cuối; <c>Blocked</c> = true khi Telegram báo người dùng chặn bot, false khi họ /start lại.</summary>
    public class BotTouchRequest : BotIdentityRequest
    {
        public bool? Blocked { get; set; }
    }

    public class BotIdentityModel
    {
        public bool Linked { get; set; }
        public Guid? IdentityId { get; set; }
        public Guid? UserId { get; set; }
        public Guid? EmployeeId { get; set; }
        public string? FullName { get; set; }
        public string? Status { get; set; }
        /// <summary>true = người có quyền duyệt / admin mới khớp số điện thoại – cần nhập mã web để dùng chức năng duyệt.</summary>
        public bool NeedsCode { get; set; }
        /// <summary>Có tài khoản web (UserId) → xin JWT được; chỉ EmployeeId → dùng BotChannel/Employee*.</summary>
        public bool HasWebAccount { get; set; }
        /// <summary>Có quyền vào bot Điều hành (#/bot-dieu-hanh) – bot VH gửi nút mở bot DH.</summary>
        public bool CanUseDieuHanh { get; set; }
        /// <summary>Có quyền vào bot đang gọi (#/bot-van-hanh / #/bot-dieu-hanh). false → bot chỉ trả lời "chưa được cấp quyền".</summary>
        public bool CanUseBot { get; set; }
        /// <summary>BOT-1.4: MetaLink các menu được vào (AllowAccess) – bot dựng menu chức năng theo quyền.</summary>
        public List<string> Menus { get; set; } = [];
        /// <summary>BOT-1.4: screen-id các mục được Duyệt rõ (AllowEdit trên #/wf-inbox?type=…) – PendingCode → rỗng.</summary>
        public List<string> ApproveScreens { get; set; } = [];
        /// <summary>BOT-1.4: admin (PendingCode → false).</summary>
        public bool IsAdmin { get; set; }
    }

    /// <summary>BOT-1.4: 1 tin đã gửi cùng sự việc (CorrelationId) – messaging sửa đồng loạt (đã duyệt / đã nhận).</summary>
    public class BotCorrelatedMessageModel
    {
        public Guid Id { get; set; }
        public string? BotCode { get; set; }
        public string? ExternalConversationId { get; set; }
        public string? ProviderMessageId { get; set; }
        public string? MessageType { get; set; }
    }

    public class BotCorrelatedRequest
    {
        public string? CorrelationId { get; set; }
    }

    public class UserTokenModel
    {
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public Guid UserId { get; set; }
        /// <summary>Token hạn chế (PendingCode): không quyền duyệt / quản trị.</summary>
        public bool Limited { get; set; }
    }

    /// <summary>BOT-1.2: mã liên kết (≤ 64 ký tự [A-Za-z0-9_-] – dùng được trong /start của Telegram) + link mở thẳng từng bot.</summary>
    public class LinkCodeModel
    {
        public string Code { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        /// <summary>BotCode → https://t.me/&lt;BotUsername&gt;?start=&lt;mã&gt; (bot chưa khai báo BotUsername → không có).</summary>
        public Dictionary<string, string> DeepLinks { get; set; } = [];
    }

    public class TelegramCodeForEmployeeRequest
    {
        public Guid? EmployeeId { get; set; }
    }

    public class MyIdentityModel
    {
        public Guid Id { get; set; }
        public string? BotCode { get; set; }
        public string? ExternalUsername { get; set; }
        public string? DisplayName { get; set; }
        public string? IdentityStatus { get; set; }
        public DateTime LinkedAt { get; set; }
        public DateTime? LastSeenAt { get; set; }
    }

    /// <summary>BOT-1.2 (Catalog 220): admin liên kết tay 1 tài khoản / nhân viên với Telegram user id trên 1 bot.</summary>
    public class IdentityLinkRequest
    {
        public string? BotCode { get; set; }
        public Guid? UserId { get; set; }
        public Guid? EmployeeId { get; set; }
        public string? ExternalUserId { get; set; }
        public string? ExternalUsername { get; set; }
        public string? DisplayName { get; set; }
    }

    /// <summary>BOT-1.2: chấm công cho người chỉ có EmployeeId (qua bot, ghi dưới system).</summary>
    public class BotEmployeeCheckRequest : BotIdentityRequest
    {
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        /// <summary>GPS (mặc định) / QR.</summary>
        public string? SourceChannel { get; set; }
        public string? WorkstationCode { get; set; }
        /// <summary>Khóa chống trùng (vd. TG:{bot}:{update_id}).</summary>
        public string? ExternalEventId { get; set; }
    }

    /// <summary>BOT-1.5a: xin nghỉ qua bot (người chỉ có EmployeeId).</summary>
    public class BotEmployeeLeaveRequest : BotIdentityRequest
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? LeaveTypeCode { get; set; }
        public string? Reason { get; set; }
    }

    public class BotEmployeeShiftModel
    {
        public Guid AssignmentId { get; set; }
        public DateTime WorkDate { get; set; }
        public string? ShiftCode { get; set; }
        public string? ShiftName { get; set; }
        public DateTime? PlannedStart { get; set; }
        public DateTime? PlannedEnd { get; set; }
        public string? AssignmentStatus { get; set; }
    }

    /// <summary>BOT-1.5c (DECISIONS p24): ca có thể đổi với ca của tôi – chỉ tên đồng nghiệp cùng đơn vị + ca (không lộ dữ liệu khác).</summary>
    public class BotSwapCandidatesModel
    {
        public BotEmployeeShiftModel Current { get; set; } = null!;
        /// <summary>Ca còn mở của đồng nghiệp (đổi chéo).</summary>
        public List<BotSwapCandidateModel> ListShift { get; set; } = [];
        /// <summary>Đồng nghiệp đang làm cùng đơn vị (nhường ca).</summary>
        public List<BotSwapCandidateModel> ListColleague { get; set; } = [];
    }

    public class BotSwapCandidateModel
    {
        public Guid EmployeeId { get; set; }
        public string? EmployeeName { get; set; }
        public Guid? AssignmentId { get; set; }
        public DateTime? WorkDate { get; set; }
        public string? ShiftName { get; set; }
        public DateTime? PlannedStart { get; set; }
        public DateTime? PlannedEnd { get; set; }
    }

    public class BotEmployeeRequestItemModel
    {
        public Guid Id { get; set; }
        /// <summary>LEAVE / SWAP.</summary>
        public string Kind { get; set; } = string.Empty;
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? Status { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
