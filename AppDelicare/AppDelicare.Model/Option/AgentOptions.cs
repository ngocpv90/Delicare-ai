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

        /// <summary>AI-7: mã nhóm (UserGroup.Code, cách nhau dấu phẩy) được hỏi Trợ lý qua bot; trống = mọi người đã liên kết. Người hỏi chưa liên kết luôn bị từ chối.</summary>
        public string? AskingGroups { get; set; }

        /// <summary>AI-9: hạn xác nhận đề xuất ghi của agent (phút) – quá hạn đề xuất bị hủy, agent đề xuất lại.</summary>
        public int ProposalTtlMinutes { get; set; } = 15;

        /// <summary>AI-4: webhook API server Hermes nhận sự kiện gấp; trống = tắt.</summary>
        public string? HermesWebhookUrl { get; set; }

        /// <summary>AI-6: tệp lịch chạy agent (một nguồn cho màn Lịch chạy + cron Hermes); tương đối = tìm từ ContentRoot lên 5 cấp (Docker: gắn thư mục agents).</summary>
        public string SchedulesFile { get; set; } = "agents/schedules.json";

        /// <summary>AI-13 (D-133 PROPOSED – anh điền): ngân sách chi phí AI / tháng (VND); trống / 0 = không cảnh báo ngân sách.</summary>
        public decimal? MonthlyBudget { get; set; }

        /// <summary>AI-13: tỷ giá quy đổi chi phí Hermes báo bằng USD sang VND.</summary>
        public decimal UsdToVnd { get; set; } = 25500m;

        /// <summary>AI-13: ngưỡng % ngân sách tháng gửi cảnh báo CEO / FIN-IT (một lần / tháng; tới 100% gửi thêm một lần).</summary>
        public decimal CostAlertPct { get; set; } = 80m;

        /// <summary>AI-14: API server Hermes kiểu OpenAI cho chat trên web, gồm /v1 (vd http://hermes-vivi:8642/v1); trống = chat tắt, màn báo tiếng Việt.</summary>
        public string? HermesApiUrl { get; set; }

        /// <summary>AI-15: agent ở container Hermes khác → URL riêng theo mã (vd KITCHEN → http://hermes-van-hanh:8642/v1). Không có mục → HermesApiUrl.</summary>
        public Dictionary<string, string> HermesApiUrlByAgent { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>AI-14: TÊN biến môi trường giữ khóa gọi API Hermes (không ghi khóa ở đây).</summary>
        public string HermesApiKeyEnv { get; set; } = "HERMES_API_KEY";

        /// <summary>AI-14: thời gian chờ Hermes trả lời 1 câu chat (giây).</summary>
        public int ChatTimeoutSeconds { get; set; } = 60;
    }
}
