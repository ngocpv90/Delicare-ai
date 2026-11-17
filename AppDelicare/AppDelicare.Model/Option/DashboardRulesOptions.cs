namespace AppDelicare.Model.Option
{
    /// <summary>
    /// BE3.5 (spec 12.1 dashboard điều hành, 12.2 hành trình đơn – Catalog 231, 232): giới hạn – section <c>BusinessRules:Dashboard</c>.
    /// PROPOSED – BA/PO CONFIRM (D-57 ad–ai).
    /// </summary>
    public sealed class DashboardRulesOptions
    {
        /// <summary>Cache phần số liệu chung (không gồm cảnh báo theo người) theo (ngày, ca, chi nhánh) – giây (spec 12.1: 60 giây). 0 = không cache.</summary>
        public int CacheSeconds { get; set; } = 60;

        /// <summary>Biểu đồ suất ăn: số ngày tính tới ngày chọn (spec 12.1: 7 ngày).</summary>
        public int ChartDays { get; set; } = 7;

        /// <summary>Khối "đơn hàng trong ngày": số đơn đầu tiên trả về (spec 12.1: 6).</summary>
        public int OrdersTake { get; set; } = 6;

        /// <summary>Hành trình đơn: tối đa N chứng từ mỗi chặng (tổng vẫn trả ở <c>documentCount</c>).</summary>
        public int JourneyMaxDocuments { get; set; } = 20;
    }
}
