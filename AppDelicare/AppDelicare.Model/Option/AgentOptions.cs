namespace AppDelicare.Model.Option
{
    /// <summary>
    /// AI-2 (D-133): công tắc + giới hạn của Trợ lý AI (ToolGateway). Mục cấu hình <c>Agent</c>.
    /// Khóa từng agent KHÔNG ở đây: biến môi trường có tên ghi ở <c>BotConfiguration.SecretReference</c> (vd <c>AGENT_KEY_WH</c>).
    /// </summary>
    public sealed class AgentOptions
    {
        public const string Section = "Agent";

        /// <summary>Tắt hết agent (ToolGateway trả 503) – nghiệp vụ lõi vẫn chạy đúng giờ.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Chặn mọi công cụ ghi <c>propose_*</c> (chỉ đọc).</summary>
        public bool ReadOnly { get; set; }

        /// <summary>Số lời gọi tối đa / phút / agent (vượt → 429). 0 = không giới hạn.</summary>
        public int RateLimitPerMinute { get; set; } = 60;

        /// <summary>Số dòng tối đa một công cụ đọc trả về (AI-3).</summary>
        public int MaxRows { get; set; } = 50;

        /// <summary>Mã bot Telegram nơi người hỏi liên kết (tra <c>X-On-Behalf-Of</c>); trống = mọi kênh Telegram.</summary>
        public string? AskingBotCode { get; set; }

        /// <summary>AI-4: webhook API server Hermes nhận sự kiện gấp; trống = tắt.</summary>
        public string? HermesWebhookUrl { get; set; }

        /// <summary>AI-6: tệp lịch chạy agent (một nguồn cho màn Lịch chạy + cron Hermes); tương đối = tìm từ ContentRoot lên 5 cấp (Docker: gắn thư mục agents).</summary>
        public string SchedulesFile { get; set; } = "agents/schedules.json";
    }
}
