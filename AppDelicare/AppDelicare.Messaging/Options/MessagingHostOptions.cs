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

        /// <summary>InboundPump quét ClaimInbound mỗi N giây (ngoài lần xử lý ngay khi nhận) – khởi động lại không mất tin.</summary>
        public int InboundScanSeconds { get; set; } = 5;

        /// <summary>OutboundPump quét ClaimOutbound mỗi N giây.</summary>
        public int OutboundScanSeconds { get; set; } = 2;

        /// <summary>Cache định danh (ResolveIdentity) N giây.</summary>
        public int IdentityCacheSeconds { get; set; } = 30;
    }
}
