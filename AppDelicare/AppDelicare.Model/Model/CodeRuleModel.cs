namespace AppDelicare.Model.Model
{
    /// <summary>
    /// MA-1.7 (D-63 k, BUSINESS_RULES §11.3): quy tắc của 1 cột mã – API <c>Common/GetCodeRules</c> trả cho giao diện.
    /// G1: ô Mã chỉ đọc "(tự sinh khi lưu)", không gửi. G2: tự chuyển chữ hoa không dấu, khóa ở chế độ sửa khi đã dùng. G3: không áp quy tắc.
    /// </summary>
    public class CodeRuleModel
    {
        /// <summary>Tên entity C# (vd. <c>Customer</c>, <c>Dish</c>).</summary>
        public string Entity { get; set; } = string.Empty;
        /// <summary>Tên bảng thật (vd. <c>Dishe</c>).</summary>
        public string Table { get; set; } = string.Empty;
        /// <summary>Cột mã (vd. <c>Code</c>, <c>OrderNumber</c>).</summary>
        public string Column { get; set; } = string.Empty;
        /// <summary>G1 / G2 / G3.</summary>
        public string Group { get; set; } = string.Empty;
        /// <summary>G1: loại sinh mã (DocumentKind).</summary>
        public string? Kind { get; set; }
        /// <summary>G1: tiền tố (KHB, DH…).</summary>
        public string? Prefix { get; set; }
        /// <summary>Mẫu hiển thị (KHB-NNNNN, DH-YYMMDD-NNNN, A–Z 0–9 - _).</summary>
        public string? Pattern { get; set; }
        /// <summary>G1: true = danh mục (dạng A), false = chứng từ (dạng B).</summary>
        public bool Catalog { get; set; }
        public string? Note { get; set; }
    }
}
