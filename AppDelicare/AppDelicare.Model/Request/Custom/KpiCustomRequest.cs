using System.ComponentModel.DataAnnotations;

namespace AppDelicare.Model.Request.Custom
{
    /// <summary>
    /// BE3.4 (Catalog 221): thêm / sửa chỉ tiêu KPI (form) – cột cũ + mục tiêu. KpiMetricDefinition không có cột mục tiêu (GAP-KPI-01) →
    /// mục tiêu ghi thành thẻ <c>[TARGET &gt;= x]</c> / <c>[TARGET &lt;= x]</c> ở cuối FormulaDescription. Đổi công thức / mục tiêu / đơn vị / cách tổng hợp
    /// → phiên bản mới (VersionNo + 1), phiên bản cũ đóng EffectiveTo, không sửa số cũ.
    /// </summary>
    public class KpiMetricDefinitionCustomRequest : KpiMetricDefinitionRequest
    {
        /// <summary>Mục tiêu (cùng đơn vị với giá trị: % → 0–100; VND/suất…). Trống = không có mục tiêu (met = null).</summary>
        public decimal? TargetValue { get; set; }

        /// <summary>HIGHER (≥ mục tiêu là đạt) / LOWER (≤). Trống = theo mã (WASTE, REJECT, EXPIRY, COST, LOSS → LOWER).</summary>
        [MaxLength(10)]
        public string? TargetDirection { get; set; }
    }

    /// <summary>BE3.4 (Catalog 224): làm mới KPI bấm tay (form).</summary>
    public class KpiRefreshRequest
    {
        /// <summary>KPI_DAILY | KPI_MONTHLY.</summary>
        [Required(ErrorMessage = "Chọn loại làm mới (KPI_DAILY / KPI_MONTHLY).")]
        [MaxLength(50)]
        public string? JobCode { get; set; }

        /// <summary>KPI_DAILY: ngày tính (trống = hôm nay).</summary>
        public DateTime? Date { get; set; }

        /// <summary>KPI_MONTHLY: tháng "yyyy-MM" (trống = tháng này; nhận cả "yyyy-MM-dd").</summary>
        [MaxLength(10)]
        public string? Month { get; set; }

        /// <summary>Trống = toàn công ty (BranchId NULL trong snapshot). Có = chỉ số liệu của chi nhánh (khách / kho / kế hoạch SX thuộc chi nhánh).</summary>
        public Guid? BranchId { get; set; }
    }

    /// <summary>BE3.4 (Catalog 222): KPI ngày (query). Trống from / to = 30 ngày gần nhất.</summary>
    public class KpiDailyRequest
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public Guid? BranchId { get; set; }
        [MaxLength(50)]
        public string? DomainCode { get; set; }
    }

    /// <summary>BE3.4 (Catalog 223): KPI tháng (query, "yyyy-MM"). Trống = 12 tháng gần nhất.</summary>
    public class KpiMonthlyRequest
    {
        [MaxLength(10)]
        public string? FromMonth { get; set; }
        [MaxLength(10)]
        public string? ToMonth { get; set; }
        public Guid? BranchId { get; set; }
        [MaxLength(50)]
        public string? DomainCode { get; set; }
    }

    /// <summary>BE3.4 (Catalog 225): nhật ký làm mới KPI / chạy job (query). Lọc theo giờ bắt đầu (StartedAt), mới nhất trước.</summary>
    public class AnalyticsRefreshLogListRequest
    {
        [MaxLength(100)]
        public string? JobCode { get; set; }
        /// <summary>Running / Succeeded / Failed / Skipped / Locked.</summary>
        [MaxLength(30)]
        public string? Status { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }
}
