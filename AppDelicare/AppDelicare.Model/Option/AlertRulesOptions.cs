namespace AppDelicare.Model.Option
{
    /// <summary>
    /// BE3.3 (spec 12.1 chuông cảnh báo, 2.x tìm kiếm toàn cục / tab Liên quan – Catalog 16, 17, 18): ngưỡng và giới hạn – section
    /// <c>BusinessRules:Alerts</c>. PROPOSED – BA/PO CONFIRM (D-57 h, i, p–u).
    /// </summary>
    public sealed class AlertRulesOptions
    {
        /// <summary>Cache kết quả chuông theo (người, chi nhánh) – giây (D-57 h: FE hỏi định kỳ 60 giây).</summary>
        public int CacheSeconds { get; set; } = 60;

        /// <summary>Nhiệt độ ngoài ngưỡng: tính các lần đo trong N giờ gần nhất (spec 12.1: 48 giờ).</summary>
        public int TemperatureHours { get; set; } = 48;

        /// <summary>Lô Khả dụng còn tồn hết hạn trong N ngày tới (tính cả hôm nay; spec 12.1: 2 ngày).</summary>
        public int LotExpiryDays { get; set; } = 2;

        /// <summary>Tìm kiếm toàn cục: số ký tự tối thiểu của từ khóa (spec: ≥ 2).</summary>
        public int SearchMinLength { get; set; } = 2;

        /// <summary>Tìm kiếm toàn cục: độ dài tối đa của từ khóa.</summary>
        public int SearchMaxLength { get; set; } = 100;

        /// <summary>Tìm kiếm toàn cục: tối đa N bản ghi mỗi loại (spec: 5).</summary>
        public int SearchMaxPerType { get; set; } = 5;

        /// <summary>Tìm kiếm toàn cục: tối đa N màn hình (chức năng) trả về.</summary>
        public int SearchMaxScreens { get; set; } = 10;

        /// <summary>Tab Liên quan: tối đa N dòng mỗi nhóm (spec: 8); <c>count</c> vẫn là tổng.</summary>
        public int RelatedMaxItems { get; set; } = 8;
    }
}
