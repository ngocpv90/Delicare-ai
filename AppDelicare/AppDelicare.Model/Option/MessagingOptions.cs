namespace AppDelicare.Model.Option
{
    /// <summary>
    /// BOT-1.1 (D-62): cấu hình kênh tin nhắn ở container <c>api</c> – section <c>Messaging</c>. Giá trị mặc định PROPOSED (BA / PO chỉnh khi UAT).
    /// </summary>
    public sealed class MessagingOptions
    {
        public const string Section = "Messaging";

        /// <summary>Tối đa N dòng mỗi lần ClaimInbound / ClaimOutbound.</summary>
        public int MaxClaim { get; set; } = 20;

        /// <summary>Dòng đang Processing / Sending quá N phút (container messaging chết giữa chừng) → được lấy lại.</summary>
        public int LeaseMinutes { get; set; } = 5;

        /// <summary>
        /// Giãn cách thử lại tin gửi lỗi (phút) theo số lần đã lỗi; hết danh sách = không thử nữa. Mặc định (để trống) = <see cref="DefaultRetryDelaysMinutes"/>.
        /// Khởi tạo RỖNG vì binder cấu hình .NET NỐI mảng trong appsettings vào danh sách mặc định (thành 10 phần tử).
        /// </summary>
        public List<int> RetryDelaysMinutes { get; set; } = [];

        public static readonly IReadOnlyList<int> DefaultRetryDelaysMinutes = [1, 5, 15, 60, 240];

        /// <summary>Danh sách giãn cách đang dùng.</summary>
        public IReadOnlyList<int> EffectiveRetryDelays => RetryDelaysMinutes.Count > 0 ? RetryDelaysMinutes : DefaultRetryDelaysMinutes;

        /// <summary>Tối đa N ký tự nội dung tin gửi tay (Telegram: 4096).</summary>
        public int MaxManualTextLength { get; set; } = 4000;

        /// <summary>Tối đa N bản ghi / trang của GetConversation.</summary>
        public int ConversationMaxPageSize { get; set; } = 200;

        /// <summary>Kích thước tối đa PayloadJson của 1 update nhận vào (ký tự) – quá → từ chối.</summary>
        public int MaxUpdateLength { get; set; } = 64000;

        /// <summary>BOT-1.2: khóa ký mã liên kết (HMAC-SHA256) – BÍ MẬT ở env <c>Messaging__LinkSigningKey</c> (≥ 32 ký tự); trống = không tạo / nhận mã.</summary>
        public string? LinkSigningKey { get; set; }

        /// <summary>BOT-1.2 (D-62 p10): hạn mã user tự tạo (phút).</summary>
        public int UserCodeMinutes { get; set; } = 15;

        /// <summary>BOT-1.2 (D-62 p10): hạn mã HR cấp cho nhân viên (giờ).</summary>
        public int EmployeeCodeHours { get; set; } = 72;

        /// <summary>BOT-1.2: hạn JWT cấp thay mặt user cho bot (phút).</summary>
        public int UserTokenMinutes { get; set; } = 10;

        /// <summary>BOT-2.3 (BOT_TELEGRAM §5.3): giờ yên lặng theo ca.</summary>
        public QuietHoursOptions QuietHours { get; set; } = new();

        /// <summary>BOT-2.3: ngưỡng các việc kiểm tra định kỳ (BotChannel/RunWatch).</summary>
        public WatchOptions Watch { get; set; } = new();
        /// <summary>APR-1.7 (D-81): giờ gửi tin tổng hợp duyệt cuối ngày (giờ VN, 0–23; &lt; 0 = tắt). Gửi ở lần RunWatch đầu tiên từ giờ này, 1 lần / người / ngày.</summary>
        public int ApprovalDigestHour { get; set; } = 18;
    }

    /// <summary>
    /// BOT-2.3 (PROPOSED – BOT_TELEGRAM §5.3): tin 🟡 / ⚪ chỉ gửi khi người nhận đang trong ca hoặc trong <see cref="LeadMinutes"/> phút trước ca;
    /// ngoài ca → hẹn (notBefore) tới <see cref="LeadMinutes"/> phút trước ca kế tiếp. Người không có phân ca: chỉ gửi trong [<see cref="DayStart"/>, <see cref="DayEnd"/>),
    /// ngoài khung → hẹn tới <see cref="DayStart"/> kế tiếp. 🔴 gửi ngay cho người đang trong ca / không có phân ca; người có phân ca nhưng ngoài ca không nhận
    /// (trừ tin leo thang, hoặc khi không còn ai khác nhận – không để mất tin khẩn).
    /// </summary>
    public sealed class QuietHoursOptions
    {
        public bool Enabled { get; set; } = true;
        public int LeadMinutes { get; set; } = 60;
        public string DayStart { get; set; } = "07:00";
        public string DayEnd { get; set; } = "21:00";
        /// <summary>Tìm ca kế tiếp trong N ngày; không có → coi như người không có phân ca.</summary>
        public int LookaheadDays { get; set; } = 3;
    }

    /// <summary>BOT-2.3 (PROPOSED): ngưỡng phút của các việc kiểm tra định kỳ – BOT_TELEGRAM §5.1.</summary>
    public sealed class WatchOptions
    {
        /// <summary>Mẻ chưa bắt đầu / chưa xong quá N phút so với kế hoạch → BATCH_LATE.</summary>
        public int BatchLateMinutes { get; set; } = 15;
        /// <summary>Xe chưa xuất phát quá N phút so với giờ dự kiến → TRIP_NOT_DEPARTED.</summary>
        public int TripLateMinutes { get; set; } = 10;
        /// <summary>Điểm giao chưa đến quá N phút so với giờ dự kiến → STOP_LATE.</summary>
        public int StopLateMinutes { get; set; } = 15;
        /// <summary>Chưa vào ca quá N phút sau giờ bắt đầu → NOT_CHECKED_IN (1 tin / ca, gom danh sách).</summary>
        public int NotCheckedInMinutes { get; set; } = 10;
        /// <summary>Phản ánh còn ≤ N phút tới hạn SLA → COMPLAINT_SLA (lần 2 khi đã quá hạn).</summary>
        public int ComplaintSlaWarnMinutes { get; set; } = 60;
        /// <summary>Duyệt quá hạn: quá hạn → người duyệt của cấp; quá thêm N giờ → nhóm <see cref="ApprovalOverdueGroups"/> (bot DH).</summary>
        public int ApprovalOverdueEscalateHours { get; set; } = 24;
        /// <summary>Chưa có dữ liệu "cấp trên trực tiếp" → nhóm quyền theo tên (PROPOSED: COO). Khởi tạo rỗng (binder nối mảng).</summary>
        public List<string> ApprovalOverdueGroups { get; set; } = [];
        public static readonly IReadOnlyList<string> DefaultApprovalOverdueGroups = ["COO"];
        /// <summary>BIZ-1.6 (D-77 f): bậc 3 – quá thêm ApprovalOverdueEscalateHours nữa (mặc định tổng 48 giờ sau hạn) → nhóm này (mặc định CEO).</summary>
        public List<string> ApprovalOverdueTopGroups { get; set; } = [];
        public static readonly IReadOnlyList<string> DefaultApprovalOverdueTopGroups = ["CEO"];
        public IReadOnlyList<string> EffectiveApprovalOverdueTopGroups => ApprovalOverdueTopGroups.Count > 0 ? ApprovalOverdueTopGroups : DefaultApprovalOverdueTopGroups;
        public IReadOnlyList<string> EffectiveApprovalOverdueGroups => ApprovalOverdueGroups.Count > 0 ? ApprovalOverdueGroups : DefaultApprovalOverdueGroups;
    }

    /// <summary>
    /// BOT-1.1 (D-62): khách nội bộ gọi API bằng header <c>X-Client-Key</c> – section <c>Clients:&lt;code&gt;</c> (vd. <c>Clients:MESSAGING</c>).
    /// <c>KeyHash</c> = SHA-256 (hex) của khóa – BÍ MẬT ở env / user-secrets (<c>Clients__MESSAGING__KeyHash</c>); repo chỉ có placeholder trống.
    /// </summary>
    public sealed class ClientOptions
    {
        public const string Section = "Clients";

        public string? KeyHash { get; set; }

        public bool Enabled { get; set; } = true;
    }
}
