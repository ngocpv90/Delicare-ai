namespace AppDelicare.Model.Model
{
    /// <summary>BE3.4 (spec 11.7): định nghĩa chỉ tiêu KPI đã tách mục tiêu khỏi FormulaDescription (GAP-KPI-01).</summary>
    public class KpiDefinitionModel
    {
        public Guid Id { get; set; }
        public string? MetricCode { get; set; }
        public string? MetricName { get; set; }
        public string? DomainCode { get; set; }
        public string? UnitCode { get; set; }
        /// <summary>Công thức (chữ) – không gồm thẻ mục tiêu.</summary>
        public string? Formula { get; set; }
        public string? AggregationCode { get; set; }
        public int VersionNo { get; set; }
        public DateTime EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public bool IsActive { get; set; }
        public decimal? TargetValue { get; set; }
        /// <summary>HIGHER = càng cao càng tốt (met khi ≥ mục tiêu); LOWER = càng thấp càng tốt (met khi ≤).</summary>
        public string TargetDirection { get; set; } = string.Empty;
        public string TargetDirectionName { get; set; } = string.Empty;
        /// <summary>Đã có công thức tính trong hệ thống (mã không có công thức → số liệu INCOMPLETE).</summary>
        public bool HasCalculator { get; set; }
    }

    /// <summary>Giá trị mới nhất trong khoảng xem (spec: latest { value, target, variance, met }).</summary>
    public class KpiLatestModel
    {
        public DateTime Date { get; set; }
        public decimal? Value { get; set; }
        public decimal? Target { get; set; }
        public decimal? Variance { get; set; }
        public bool? Met { get; set; }
        public decimal? Numerator { get; set; }
        public decimal? Denominator { get; set; }
        public int MetricVersion { get; set; }
        public string? DataQualityStatus { get; set; }
        public DateTime CalculatedAt { get; set; }
    }

    /// <summary>1 điểm của chuỗi (ngày hoặc tháng = ngày đầu tháng).</summary>
    public class KpiSeriesPointModel
    {
        public DateTime Date { get; set; }
        public decimal? Value { get; set; }
        public decimal? Target { get; set; }
        public bool? Met { get; set; }
        public string? DataQualityStatus { get; set; }
    }

    public class KpiMetricSeriesModel
    {
        public KpiDefinitionModel Definition { get; set; } = new();
        public KpiLatestModel? Latest { get; set; }
        public List<KpiSeriesPointModel> Series { get; set; } = [];
    }

    /// <summary>BE3.4 (Catalog 222 / 223): <c>{ metrics: [{ definition, latest, series }] }</c>.</summary>
    public class KpiDashboardModel
    {
        /// <summary>DAILY / MONTHLY.</summary>
        public string Period { get; set; } = string.Empty;
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        /// <summary>null = toàn công ty.</summary>
        public Guid? BranchId { get; set; }
        public string? DomainCode { get; set; }
        public List<KpiMetricSeriesModel> Metrics { get; set; } = [];
    }

    /// <summary>Kết quả tính 1 chỉ tiêu (1 dòng snapshot).</summary>
    public class KpiCalculatedMetricModel
    {
        public string MetricCode { get; set; } = string.Empty;
        public string? MetricName { get; set; }
        public int MetricVersion { get; set; }
        public decimal? Value { get; set; }
        public decimal? Numerator { get; set; }
        public decimal? Denominator { get; set; }
        public decimal? Target { get; set; }
        public decimal? Variance { get; set; }
        public bool? Met { get; set; }
        public string DataQualityStatus { get; set; } = string.Empty;
        /// <summary>Lý do thiếu dữ liệu / ghi chú (không lưu – bảng snapshot không có cột ghi chú).</summary>
        public string? Note { get; set; }
    }

    /// <summary>BE3.4 (Catalog 224): kết quả làm mới KPI (spec: { runId }).</summary>
    public class KpiRefreshResultModel
    {
        public Guid RunId { get; set; }
        public string JobCode { get; set; } = string.Empty;
        public string RunStatus { get; set; } = string.Empty;
        /// <summary>Ngày (KPI_DAILY) hoặc ngày đầu tháng (KPI_MONTHLY).</summary>
        public DateTime PeriodDate { get; set; }
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public Guid? BranchId { get; set; }
        public long RowsProcessed { get; set; }
        public int IncompleteCount { get; set; }
        public string? SourceWatermark { get; set; }
        public List<KpiCalculatedMetricModel> Metrics { get; set; } = [];
    }

    /// <summary>BE3.4 (Catalog 225): 1 lần làm mới / chạy job trong <c>AnalyticsRefreshLog</c>.</summary>
    public class AnalyticsRefreshLogItemModel
    {
        public int STT { get; set; }
        public Guid Id { get; set; }
        public string? JobCode { get; set; }
        public Guid RunId { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public double? DurationSeconds { get; set; }
        public string? RunStatus { get; set; }
        public string RunStatusName { get; set; } = string.Empty;
        public long? RowsProcessed { get; set; }
        public string? SourceWatermark { get; set; }
        /// <summary>Tách từ SourceWatermark "&lt;trigger&gt;|&lt;ngày yyyy-MM-dd&gt;|…": MANUAL / SCHEDULE.</summary>
        public string? Trigger { get; set; }
        public DateTime? TargetDate { get; set; }
        public string? Message { get; set; }
        public Guid? CreatedBy { get; set; }
        public string? CreatedByName { get; set; }
    }
}
