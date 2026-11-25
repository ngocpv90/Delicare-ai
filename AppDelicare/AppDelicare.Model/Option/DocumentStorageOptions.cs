namespace AppDelicare.Model.Option
{
    /// <summary>
    /// BE2.7 (spec 2.4, Catalog 19–21): nơi lưu tệp đính kèm <c>DocumentFile</c> – section <c>DocumentStorage</c> của appsettings.
    /// Thư mục NGOÀI wwwroot (không phục vụ tĩnh); đường dẫn tương đối tính từ thư mục chạy ứng dụng (ContentRoot).
    /// Staging / Docker: gắn volume vào thư mục này (deploy/README.md). Giá trị PROPOSED – BA/PO CONFIRM (D-56).
    /// </summary>
    public sealed class DocumentStorageOptions
    {
        public const string Section = "DocumentStorage";

        /// <summary>Thư mục gốc lưu tệp (tuyệt đối, hoặc tương đối với ContentRoot). Mặc định <c>App_Data/DocumentFiles</c>.</summary>
        public string RootPath { get; set; } = "App_Data/DocumentFiles";

        /// <summary>Dung lượng tối đa 1 tệp (MB) – spec 2.4: 20 MB.</summary>
        public int MaxFileSizeMb { get; set; } = 20;

        /// <summary>Đuôi tệp được nhận (spec 2.4: pdf, jpg, png, xlsx, docx). Không bao giờ nhận tệp chạy được (.exe, .js, .html…) dù cấu hình.</summary>
        public List<string> AllowedExtensions { get; set; } = [".pdf", ".jpg", ".jpeg", ".png", ".xlsx", ".docx"];

        /// <summary>Loại tài liệu (spec 2.4: signature, photo, evidence, contract, other).</summary>
        public List<string> Categories { get; set; } = ["signature", "photo", "evidence", "contract", "health", "other"];
    }
}
