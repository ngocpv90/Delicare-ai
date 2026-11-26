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
    }
}
