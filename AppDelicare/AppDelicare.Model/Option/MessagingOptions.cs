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
