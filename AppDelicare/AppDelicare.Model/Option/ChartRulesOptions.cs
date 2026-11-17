namespace AppDelicare.Model.Option
{
    /// <summary>
    /// BE3.1 (spec 6.7 / 8.4 / 9.3 / 10.3): giới hạn dữ liệu biểu đồ – section <c>BusinessRules:Charts</c>. PROPOSED – BA/PO CONFIRM (D-57 j–n).
    /// </summary>
    public sealed class ChartRulesOptions
    {
        /// <summary>Đối soát / hao hụt: không gửi from / to → N ngày gần nhất (tính cả hôm nay).</summary>
        public int DefaultRangeDays { get; set; } = 30;

        /// <summary>Đối soát / hao hụt: khoảng tối đa 1 lần gọi (ngày).</summary>
        public int MaxRangeDays { get; set; } = 366;

        /// <summary>Chuỗi nhiệt độ: không gửi from / to → N giờ gần nhất (spec 9.3: 48 giờ).</summary>
        public int TemperatureDefaultHours { get; set; } = 48;

        /// <summary>Chuỗi nhiệt độ: khoảng tối đa 1 lần gọi (ngày).</summary>
        public int TemperatureMaxRangeDays { get; set; } = 31;

        /// <summary>Chuỗi nhiệt độ: quá N lần đo → gộp theo khoảng phút (trung bình, min / max, đạt khi mọi lần đo đạt).</summary>
        public int TemperatureMaxPoints { get; set; } = 2000;
    }
}
