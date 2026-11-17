namespace AppDelicare.Model.ViewModels.Custom
{
    /// <summary>BE3.4 (Catalog 221): dòng danh sách chỉ tiêu KPI + mục tiêu tách từ thẻ <c>[TARGET …]</c> của FormulaDescription (GAP-KPI-01).</summary>
    public class KpiMetricDefinitionCustomViewModel : KpiMetricDefinitionViewModel
    {
        /// <summary>Công thức (chữ) – không gồm thẻ mục tiêu.</summary>
        public string? Formula { get; set; }
        public decimal? TargetValue { get; set; }
        public string? TargetDirection { get; set; }
        public string? TargetDirectionName { get; set; }
        /// <summary>Phiên bản đang hiệu lực hôm nay.</summary>
        public bool IsCurrent { get; set; }
    }
}
