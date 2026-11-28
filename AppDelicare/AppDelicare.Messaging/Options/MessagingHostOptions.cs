namespace AppDelicare.Messaging.Options
{
    /// <summary>Chế độ nhận tin: Polling (máy dev – getUpdates) / Webhook (server test / production – Telegram gọi /tg/{bot}).</summary>
    public enum TelegramMode
    {
        Polling,
        Webhook,
    }

    /// <summary>BOT-1.3: section <c>Messaging</c> – nối tới container api. <c>ClientKey</c> là BÍ MẬT (env <c>Messaging__ClientKey</c>).</summary>
    public sealed class MessagingHostOptions
    {
        public const string Section = "Messaging";

        /// <summary>vd. http://api:8080 (Docker) / http://localhost:5125 (dev).</summary>
        public string ApiBaseUrl { get; set; } = "http://localhost:5125";

        /// <summary>Khóa client gửi ở header X-Client-Key – api so với Clients:MESSAGING:KeyHash. BÍ MẬT.</summary>
        public string? ClientKey { get; set; }

        public TelegramMode Mode { get; set; } = TelegramMode.Polling;

        /// <summary>Webhook: URL công khai (https://domain) – Telegram gọi {WebhookBaseUrl}/tg/{botCode}.</summary>
        public string? WebhookBaseUrl { get; set; }

        /// <summary>AI-25.3: địa chỉ web người dùng (https://…, vd. https://erp.delicare.vn) – nút 🌐 Mở trên web của tin Hỏi nhanh; trống = không có nút.</summary>
        public string? WebUrl { get; set; }

        /// <summary>InboundPump quét ClaimInbound mỗi N giây (ngoài lần xử lý ngay khi nhận) – khởi động lại không mất tin.</summary>
        public int InboundScanSeconds { get; set; } = 5;

        /// <summary>OutboundPump quét ClaimOutbound mỗi N giây.</summary>
        public int OutboundScanSeconds { get; set; } = 2;

        /// <summary>Cache định danh (ResolveIdentity) N giây.</summary>
        public int IdentityCacheSeconds { get; set; } = 30;

        /// <summary>
        /// BOT-1.4 (BOT_TELEGRAM §4, PROPOSED): mức rủi ro duyệt theo BusinessType (hằng trong *ApprovalHandler). Thấp = 1 chạm + hoàn tác + duyệt hàng loạt;
        /// loại không có trong <c>Low</c> = Cao (xác nhận 2 bước). Rỗng = mặc định trong code.
        /// </summary>
        public List<string> ApprovalRiskLow { get; set; } = [];

        public static readonly IReadOnlyList<string> DefaultApprovalRiskLow = ["LEAVE_REQUEST", "SHIFT_SWAP", "MENU_CHANGE"];

        public IReadOnlyList<string> EffectiveRiskLow => ApprovalRiskLow.Count > 0 ? ApprovalRiskLow : DefaultApprovalRiskLow;

        /// <summary>
        /// BOT-1.4 (BOT_TELEGRAM §1.4) + MA-1.5 (D-63): tiền tố mã ĐANG SINH trong DocumentNumberService (BUSINESS_RULES §11 – chứng từ + danh mục)
        /// cộng tiền tố cũ trước D-63 (PH, KH – chứng từ cũ giữ số) → gõ mã là tra.
        /// Rỗng = mặc định trong code (không đặt tiền tố mới).
        /// </summary>
        public List<string> CodePrefixes { get; set; } = [];

        public static readonly IReadOnlyList<string> DefaultCodePrefixes = [
            // Chứng từ (dạng B) – D-63.
            "DH", "HD", "NU", "DM", "MH", "PO", "PN", "PX", "KK", "GD", "LO", "KS", "LS", "ME", "DG", "GH", "CX", "VS", "PA", "XN", "DN", "DC", "QC", "MR",
            // Danh mục (dạng A) – D-63.
            "KHB", "KHC", "NC", "NL", "MA", "KO", "XE", "NV", "DP", "BG",
            // Trước D-63 (chứng từ cũ giữ số).
            "PH", "KH",
        ];

        public IReadOnlyList<string> EffectiveCodePrefixes => CodePrefixes.Count > 0 ? CodePrefixes : DefaultCodePrefixes;
    }
}
