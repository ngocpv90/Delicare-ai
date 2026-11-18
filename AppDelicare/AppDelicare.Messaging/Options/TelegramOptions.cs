namespace AppDelicare.Messaging.Options
{
    /// <summary>BOT-1.3: section <c>Telegram</c>. Token / WebhookSecret là BÍ MẬT (env <c>Telegram__Bots__VH__Token</c>, <c>Telegram__Bots__VH__WebhookSecret</c>).</summary>
    public sealed class TelegramOptions
    {
        public const string Section = "Telegram";

        public string ApiBaseUrl { get; set; } = "https://api.telegram.org";

        /// <summary>Mã bot (VH / DH) → cấu hình. Bot không có Token = tắt.</summary>
        public Dictionary<string, TelegramBotOptions> Bots { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Polling: giây chờ của getUpdates (long polling).</summary>
        public int PollingTimeoutSeconds { get; set; } = 25;

        public TelegramBotOptions? Find(string? botCode)
            => botCode != null && Bots.TryGetValue(botCode, out var b) && b.Enabled && !string.IsNullOrWhiteSpace(b.Token) ? b : null;
    }

    public sealed class TelegramBotOptions
    {
        /// <summary>BÍ MẬT.</summary>
        public string? Token { get; set; }

        /// <summary>BÍ MẬT – Telegram gửi lại ở header X-Telegram-Bot-Api-Secret-Token (setWebhook secret_token).</summary>
        public string? WebhookSecret { get; set; }

        /// <summary>Tên bot không có @ (link t.me) – không bí mật.</summary>
        public string? Username { get; set; }

        public bool Enabled { get; set; } = true;
    }
}
