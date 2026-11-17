namespace AppDelicare.Model.Model
{
    /// <summary>BE3.5 – số liệu dùng chung với chuông cảnh báo (BE3.3) – cùng quy tắc, không lọc quyền.</summary>
    public class SharedAlertCountsModel
    {
        public int StockBelowMin { get; set; }
        public int LotOnHold { get; set; }
        public int OpenComplaints { get; set; }
    }

    /// <summary>BE3.5 (Catalog 231, spec 12.1): dashboard điều hành của 1 ngày (lọc ca / chi nhánh).</summary>
    public class DashboardExecutiveModel
    {
        public DateTime Date { get; set; }
        public Guid? ShiftId { get; set; }
        public Guid? BranchId { get; set; }
        /// <summary>Thời điểm tính số liệu (giờ VN) – phần chung được cache 60 giây.</summary>
        public DateTime GeneratedAt { get; set; }
        public DashboardKpiModel Kpi { get; set; } = new();
        public DashboardChartModel Chart { get; set; } = new();
        public List<DashboardShiftProgressModel> ShiftProgress { get; set; } = [];
        public List<DashboardKpiSnapshotModel> KpiSnapshot { get; set; } = [];
        /// <summary>N đơn đầu của ngày (theo số đơn) kèm trạng thái giao.</summary>
        public List<DashboardOrderModel> Orders { get; set; } = [];
        /// <summary>Tổng số đơn của ngày (để FE hiện "xem tất cả").</summary>
        public int OrdersTotal { get; set; }
        /// <summary>Cảnh báo = Common/GetAlerts của người đang đăng nhập (lọc quyền Xem từng màn).</summary>
        public List<AlertModel> Alerts { get; set; } = [];
    }

    public class DashboardKpiModel
    {
        /// <summary>Σ suất dòng đơn (đơn ≠ Nháp / Hủy) của ngày ăn.</summary>
        public decimal PlannedMeals { get; set; }
        public DashboardOrderCountModel Orders { get; set; } = new();
        public DashboardProductionProgressModel ProductionProgress { get; set; } = new();
        public DashboardDeliveryModel Delivery { get; set; } = new();
        /// <summary>Số nguyên liệu dưới tồn tối thiểu (cùng quy tắc chuông STOCK_BELOW_MIN).</summary>
        public int StockAlerts { get; set; }
        /// <summary>= mẻ trong ngày QC HOLD + REJECT + lô còn tồn đang tạm giữ.</summary>
        public int QcHoldReject { get; set; }
        public DashboardQcDetailModel QcDetail { get; set; } = new();
        public DashboardStaffingModel Staffing { get; set; } = new();
        /// <summary>Phản ánh Mới / Đang xử lý.</summary>
        public int OpenComplaints { get; set; }
    }

    public class DashboardOrderCountModel
    {
        public int Total { get; set; }
        /// <summary>Trạng thái → số đơn (mã trạng thái Order.Status).</summary>
        public Dictionary<string, int> ByStatus { get; set; } = [];
        public List<DashboardStatusCountModel> ListStatus { get; set; } = [];
    }

    public class DashboardStatusCountModel
    {
        public string Status { get; set; } = string.Empty;
        public string StatusName { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class DashboardProductionProgressModel
    {
        /// <summary>% = donePortions / plannedPortions × 100 (1 số lẻ); không có mẻ → null.</summary>
        public decimal? Percent { get; set; }
        public decimal DonePortions { get; set; }
        public decimal PlannedPortions { get; set; }
        public int BatchCount { get; set; }
    }

    public class DashboardDeliveryModel
    {
        public int Delivered { get; set; }
        public int Total { get; set; }
        /// <summary>Trạng thái thực hiện giao (READY / LOADED / DISPATCHED / ARRIVED / DELIVERED / FAILED) → số đơn giao.</summary>
        public Dictionary<string, int> ByStatus { get; set; } = [];
    }

    public class DashboardQcDetailModel
    {
        public int BatchHold { get; set; }
        public int BatchReject { get; set; }
        public int LotOnHold { get; set; }
    }

    public class DashboardStaffingModel
    {
        /// <summary>Ca Đã vào ca / Hoàn thành.</summary>
        public int CheckedIn { get; set; }
        /// <summary>Tổng ca được phân của ngày (mọi trạng thái).</summary>
        public int Assigned { get; set; }
    }

    public class DashboardChartModel
    {
        public List<DashboardMealPointModel> Meals7d { get; set; } = [];
    }

    public class DashboardMealPointModel
    {
        public DateTime Date { get; set; }
        /// <summary>Σ suất dòng đơn (đơn ≠ Nháp / Hủy) theo ngày ăn.</summary>
        public decimal Planned { get; set; }
        /// <summary>Σ suất khách nhận (POD) theo ngày giao.</summary>
        public decimal Accepted { get; set; }
    }

    public class DashboardShiftProgressModel
    {
        public Guid ShiftId { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }
        public int BatchCount { get; set; }
        public decimal? Percent { get; set; }
        public decimal DonePortions { get; set; }
        public decimal PlannedPortions { get; set; }
    }

    public class DashboardKpiSnapshotModel
    {
        public string MetricCode { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? UnitCode { get; set; }
        public decimal? Value { get; set; }
        public decimal? Target { get; set; }
        /// <summary>Đạt mục tiêu theo chiều chỉ tiêu (chi phí càng thấp càng tốt); thiếu số / mục tiêu → null.</summary>
        public bool? Met { get; set; }
        /// <summary>true = càng thấp càng tốt.</summary>
        public bool LowerIsBetter { get; set; }
        public string? DataQualityStatus { get; set; }
        public DateTime? CalculatedAt { get; set; }
    }

    public class DashboardOrderModel
    {
        public Guid Id { get; set; }
        public string? OrderNumber { get; set; }
        public Guid CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public decimal? TotalQuantity { get; set; }
        public string? Status { get; set; }
        public string? StatusName { get; set; }
        public string Route { get; set; } = string.Empty;
        /// <summary>Trạng thái thực hiện giao của từng đơn giao (không tính đơn giao Hủy); rỗng = chưa lập đơn giao.</summary>
        public List<DashboardDeliveryStatusModel> ListDelivery { get; set; } = [];
    }

    public class DashboardDeliveryStatusModel
    {
        public Guid DeliveryOrderId { get; set; }
        public string? DeliveryNumber { get; set; }
        public string Status { get; set; } = string.Empty;
        public string StatusName { get; set; } = string.Empty;
    }

    /// <summary>BE3.5 (Catalog 232, spec 12.2): hành trình 1 đơn hàng qua các chặng – chỉ tên màn hình + số chứng từ (D-58: không tên bảng / cột).</summary>
    public class OrderJourneyModel
    {
        public Guid OrderId { get; set; }
        public string? OrderNumber { get; set; }
        public string? CustomerName { get; set; }
        public string? ContractNumber { get; set; }
        public DateTime MealDate { get; set; }
        public string? Status { get; set; }
        public string? StatusName { get; set; }
        /// <summary>Mã chặng đầu tiên chưa xong (null = mọi chặng đã xong).</summary>
        public string? CurrentStage { get; set; }
        public List<JourneyStageModel> Stages { get; set; } = [];
    }

    public class JourneyStageModel
    {
        /// <summary>ORDER / PRODUCTION_PLAN / PRODUCTION / PACKING / DELIVERY / POD / RECONCILIATION / COMPLAINT.</summary>
        public string Stage { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        /// <summary>Route màn hình của chặng.</summary>
        public string Route { get; set; } = string.Empty;
        /// <summary>NotStarted / InProgress / Done / Issue / Cancelled / None.</summary>
        public string Status { get; set; } = string.Empty;
        public string StatusName { get; set; } = string.Empty;
        /// <summary>Mốc thời gian gần nhất của chặng (giờ VN).</summary>
        public DateTime? At { get; set; }
        public bool Done { get; set; }
        public int DocumentCount { get; set; }
        /// <summary>false = người dùng không có quyền Xem màn của chặng → không trả chứng từ (vẫn có trạng thái).</summary>
        public bool CanView { get; set; }
        public string? Note { get; set; }
        public List<JourneyDocumentModel> Documents { get; set; } = [];
    }

    public class JourneyDocumentModel
    {
        public Guid Id { get; set; }
        public string? Number { get; set; }
        public string? Status { get; set; }
        public string? StatusName { get; set; }
        public DateTime? At { get; set; }
        public string Route { get; set; } = string.Empty;
    }
}
