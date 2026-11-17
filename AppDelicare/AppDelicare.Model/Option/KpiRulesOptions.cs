namespace AppDelicare.Model.Option
{
    /// <summary>
    /// BE3.4 (spec 11.7, Catalog 221–225): KPI – section <c>BusinessRules:Kpi</c>. PROPOSED – BA/PO CONFIRM (DECISIONS "BE-3 – đề xuất" w–ab).
    /// </summary>
    public sealed class KpiRulesOptions
    {
        /// <summary>DEL_ONTIME: giao trễ tối đa N phút so với giờ giao dự kiến (<c>DeliveryOrder.PlannedDeliveryTime</c>) vẫn tính đúng giờ.</summary>
        public int OnTimeToleranceMinutes { get; set; } = 15;

        /// <summary>KPI ngày: không gửi from / to → N ngày gần nhất (tính cả hôm nay).</summary>
        public int DailyDefaultRangeDays { get; set; } = 30;

        /// <summary>KPI ngày: khoảng tối đa 1 lần gọi (ngày).</summary>
        public int DailyMaxRangeDays { get; set; } = 366;

        /// <summary>KPI tháng: không gửi fromMonth / toMonth → N tháng gần nhất (tính cả tháng này).</summary>
        public int MonthlyDefaultMonths { get; set; } = 12;

        /// <summary>KPI tháng: khoảng tối đa 1 lần gọi (tháng).</summary>
        public int MonthlyMaxMonths { get; set; } = 36;
    }
}
