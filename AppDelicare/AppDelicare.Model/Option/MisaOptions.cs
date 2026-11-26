namespace AppDelicare.Model.Option
{
    /// <summary>
    /// ACC-1 (D-131 c): kết nối kế toán MISA AMIS – section <c>Integration:Misa</c>. AMIS là nguồn mã chuẩn; các ánh xạ không có cột trong DB
    /// (thành phẩm theo buổi, kho, lý do xuất) nằm ở cấu hình. Không chứa bí mật (khóa API AMIS: kênh IntegrationChannel MISA_AMIS – ACC-7).
    /// </summary>
    public sealed class MisaOptions
    {
        public const string Section = "Integration:Misa";

        /// <summary>Buổi / loại suất Delicare → mã thành phẩm AMIS (SANG, TRUA, TOI, DEM; COM_SUAT = cơm suất bán lẻ).</summary>
        public Dictionary<string, string> ProductByMeal { get; set; } = new(StringComparer.OrdinalIgnoreCase)
        {
            ["SANG"] = "TP0001", ["TRUA"] = "TP0002", ["TOI"] = "TP0003", ["DEM"] = "TP0007", ["COM_SUAT"] = "TP0005",
        };

        /// <summary>Kho Delicare (mã hoặc tên) → mã kho AMIS. Nhiều kho cùng 1 mã AMIS → chuyển kho nội bộ giữa chúng không gửi sang AMIS.</summary>
        public Dictionary<string, string> WarehouseMap { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Lý do xuất / nhập Delicare → mã lý do (diễn giải) trên AMIS (vd. PRODUCTION, PACKAGING, WASTE, ADJUSTMENT, RETURN).</summary>
        public Dictionary<string, string> ReasonCodes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>ACC-2 (D-131 e): cột Excel theo mẫu "Thêm bằng Excel" của AMIS – loại chứng từ (SALE, PURCHASE, ISSUE, FG_IN, FG_OUT, STOCK_OTHER) → ["Tiêu đề cột:trường"]
        /// (trường: date, number, desc, objectCode, objectName, code, name, unit, qty, price, amount, vat, target, note). Trống = bộ cột mặc định.</summary>
        public Dictionary<string, List<string>> ExcelColumns { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>ACC-4 (D-131 h): hạn khóa kỳ = ngày N tháng sau (10); bot nhắc Kế toán từ ngày N (5) nếu kỳ trước chưa khóa.</summary>
        public int CloseDueDay { get; set; } = 10;
        public int CloseRemindFromDay { get; set; } = 5;

        /// <summary>Danh mục AMIS lưu tối đa N bản chụp (cũ hơn vẫn còn trong tài liệu đính kèm, chỉ không đọc).</summary>
        public int SnapshotKeep { get; set; } = 30;
    }
}
