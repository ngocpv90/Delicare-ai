namespace AppDelicare.Model.Option
{
    /// <summary>
    /// AI-5 (D-133): kho tri thức = thư mục markdown chuẩn Obsidian (volume <c>knowledge</c>). Mục cấu hình <c>Knowledge</c>.
    /// Quyền KHÔNG ở đây – theo mục menu <c>knowledge?folder=&lt;mã&gt;</c> (ma trận quyền cũ).
    /// </summary>
    public sealed class KnowledgeOptions
    {
        public const string Section = "Knowledge";

        /// <summary>Thư mục gốc của kho; tương đối thì tính từ ContentRoot (như DocumentStorage).</summary>
        public string VaultPath { get; set; } = "App_Data/Knowledge";

        /// <summary>Số ghi chú tối đa một lần tìm của agent (§7.8-4).</summary>
        public int MaxNotesPerQuery { get; set; } = 5;

        /// <summary>Độ dài đoạn trích trả cho agent (ký tự).</summary>
        public int SnippetLength { get; set; } = 400;

        /// <summary>AI-12: nhật ký agent (96) quá số ngày này được gộp thành tóm tắt tháng.</summary>
        public int LogRetentionDays { get; set; } = 90;

        /// <summary>AI-12: bản tin ngày (10 Ban GĐ/Bản tin) giữ số tháng này, cũ hơn chuyển Lưu trữ bản tin.</summary>
        public int BriefKeepMonths { get; set; } = 13;

        /// <summary>AI-18: cho phép đồng bộ HAI CHIỀU thư mục 80 Đào tạo với Obsidian máy cá nhân (tools/kb-sync). Mặc định tắt – mọi thư mục chỉ một chiều (máy chủ → máy).</summary>
        public bool SyncTwoWayTraining { get; set; }

        /// <summary>AI-22.1 (D-141 g, §7.9 luật 3): agent được đọc BOD Bảo mật (khi người hỏi được cấp đích danh). Mặc định false – nội dung agent đọc được gửi tới nhà cung cấp mô hình AI.</summary>
        public bool AgentCanReadBod { get; set; }
    }
}
