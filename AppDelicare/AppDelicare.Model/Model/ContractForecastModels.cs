using AppDelicare.Model.Entity;

namespace AppDelicare.Model.Model
{
    /// <summary>BE2.2 (spec 6.1): hợp đồng + loại suất + thao tác được phép + yêu cầu duyệt đang chờ.</summary>
    public class ContractDetailModel
    {
        public Contract? Contract { get; set; }
        public string? StatusName { get; set; }
        public string? CustomerName { get; set; }
        public List<ContractMeal> ListMeal { get; set; } = [];
        /// <summary>Theo máy trạng thái: update, submit, approve, reject, terminate, delete.</summary>
        public List<string> AllowedActions { get; set; } = [];
        public Guid? PendingApprovalId { get; set; }
        public string? PendingApprovalNo { get; set; }
        /// <summary>Số đơn chưa giao (Nháp … Đang SX) của hợp đồng – dùng cảnh báo trước khi chấm dứt.</summary>
        public int OpenOrderCount { get; set; }
        public List<string> ListWarning { get; set; } = [];
    }

    /// <summary>BE2.2 (spec 6.5 GET forecasts/matrix): khách × ngày, Σ ForecastQuantity bản Đã công bố mới nhất (+ bản Nháp chưa công bố).</summary>
    public class ForecastMatrixModel
    {
        public List<DateTime> Dates { get; set; } = [];
        public List<ForecastMatrixRowModel> Rows { get; set; } = [];
        /// <summary>Tổng bản Đã công bố theo ngày (khóa yyyy-MM-dd).</summary>
        public Dictionary<string, decimal> Totals { get; set; } = [];
        public decimal GrandTotal { get; set; }
    }

    public class ForecastMatrixRowModel
    {
        public Guid CustomerId { get; set; }
        public string? CustomerCode { get; set; }
        public string? CustomerName { get; set; }
        /// <summary>Đã công bố (khóa yyyy-MM-dd).</summary>
        public Dictionary<string, decimal> Values { get; set; } = [];
        /// <summary>Bản Nháp chưa công bố (khóa yyyy-MM-dd) – để màn hình hiện "chờ công bố".</summary>
        public Dictionary<string, decimal> DraftValues { get; set; } = [];
        public decimal Total { get; set; }
        /// <summary>FC-1.3: bản Nháp mới nhất từng khóa trong ngày (để sửa trực tiếp trên ma trận).</summary>
        public Dictionary<string, List<ForecastDraftCellModel>> Drafts { get; set; } = [];
        /// <summary>FC-1.3 (khi WithSuggestion): gợi ý của hệ thống theo ngày.</summary>
        public Dictionary<string, ForecastSuggestInfoModel> Suggest { get; set; } = [];
    }

    public class ForecastDraftCellModel
    {
        public Guid Id { get; set; }
        public Guid? DeliveryLocationId { get; set; }
        public string? DeliveryLocationName { get; set; }
        public Guid? ShiftId { get; set; }
        public string? MealType { get; set; }
        public decimal ForecastQuantity { get; set; }
        public string? Note { get; set; }
        /// <summary>Số hệ thống gợi ý (null = bản nhập tay).</summary>
        public decimal? Suggested { get; set; }
        public bool Edited { get; set; }
    }

    public class ForecastHistoryPointModel
    {
        public DateTime Date { get; set; }
        /// <summary>Suất thực nhận; null = không có đơn ngày đó (không tính vào trung bình).</summary>
        public decimal? Actual { get; set; }
    }

    /// <summary>FC-1.3: gợi ý theo khách × ngày (Σ các điểm giao / loại suất).</summary>
    public class ForecastSuggestInfoModel
    {
        public decimal? Suggested { get; set; }
        public decimal? Average { get; set; }
        public decimal? LastWeek { get; set; }
        /// <summary>Mọi khóa của ngày &lt; 2 tuần có dữ liệu.</summary>
        public bool Insufficient { get; set; }
        public bool PartlyInsufficient { get; set; }
        /// <summary>Có bản Nháp người đã sửa khác số gợi ý / nhập tay.</summary>
        public bool Edited { get; set; }
        public List<ForecastHistoryPointModel> ListHistory { get; set; } = [];
    }

    /// <summary>FC-1.2: kết quả tạo dự báo gợi ý.</summary>
    public class ForecastSuggestResultModel
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public int LookbackWeeks { get; set; }
        public int Created { get; set; }
        /// <summary>Bản gợi ý chưa sửa được thay bằng số mới.</summary>
        public int Refreshed { get; set; }
        public int Unchanged { get; set; }
        /// <summary>Bản Nháp người đã sửa bị ghi đè (Overwrite = true).</summary>
        public int Overwritten { get; set; }
        /// <summary>Bản Nháp người đã sửa được giữ – màn hình hỏi "Ghi đè bản nháp?".</summary>
        public int SkippedEdited { get; set; }
        public int InsufficientData { get; set; }
        public List<ForecastSuggestCellModel> ListCell { get; set; } = [];
    }

    public class ForecastSuggestCellModel
    {
        public Guid CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public Guid? DeliveryLocationId { get; set; }
        public string? DeliveryLocationName { get; set; }
        public DateTime Date { get; set; }
        public string? MealType { get; set; }
        public decimal? Suggested { get; set; }
        public decimal? Average { get; set; }
        public decimal? LastWeek { get; set; }
        public int WeeksWithData { get; set; }
        public decimal? DraftQuantity { get; set; }
        /// <summary>CREATED, REFRESHED, UNCHANGED, OVERWRITTEN, SKIPPED_EDITED, INSUFFICIENT, PREVIEW.</summary>
        public string? Result { get; set; }
        public List<ForecastHistoryPointModel> ListHistory { get; set; } = [];
    }

    /// <summary>FC-1.5: độ chính xác dự báo.</summary>
    public class ForecastAccuracyModel
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public decimal ForecastTotal { get; set; }
        public decimal ActualTotal { get; set; }
        /// <summary>Σ |dự báo − thực nhận| / Σ thực nhận × 100.</summary>
        public decimal? ErrorPct { get; set; }
        /// <summary>(Σ dự báo − Σ thực nhận) / Σ thực nhận × 100 – dương = dự báo cao.</summary>
        public decimal? BiasPct { get; set; }
        public decimal WarningPct { get; set; }
        public int CustomerCount { get; set; }
        public int CustomerOverWarning { get; set; }
        public List<ForecastAccuracyRowModel> ListCustomer { get; set; } = [];
        public List<ForecastAccuracyRowModel> ListWeek { get; set; } = [];
    }

    public class ForecastAccuracyRowModel
    {
        public Guid? CustomerId { get; set; }
        public string? CustomerCode { get; set; }
        public string? CustomerName { get; set; }
        /// <summary>Thứ Hai của tuần (dòng theo tuần).</summary>
        public DateTime? WeekStart { get; set; }
        public int Days { get; set; }
        public decimal Forecast { get; set; }
        public decimal Actual { get; set; }
        public decimal? ErrorPct { get; set; }
        public decimal? BiasPct { get; set; }
    }

    /// <summary>BE2.2 (spec 6.5 POST forecasts/bulk): kết quả nhập.</summary>
    public class ForecastImportResultModel
    {
        public int Created { get; set; }
        /// <summary>Phiên bản lớn nhất vừa tạo.</summary>
        public int VersionNo { get; set; }
        /// <summary>Số bản Nháp cũ bị thay thế ngay.</summary>
        public int Superseded { get; set; }
        public List<Forecast> ListItem { get; set; } = [];
    }

    /// <summary>BE2.2 (spec 6.5 POST forecasts/publish).</summary>
    public class ForecastPublishResultModel
    {
        public int Published { get; set; }
        /// <summary>Số bản Đã công bố cũ (cùng khóa, phiên bản nhỏ hơn) chuyển Bị thay thế.</summary>
        public int Superseded { get; set; }
    }

    /// <summary>BE2.2 (spec 6.4): yêu cầu đổi món + tên món / đơn / ca + ảnh hưởng tới kế hoạch sản xuất của ngày.</summary>
    public class MenuChangeDetailModel
    {
        public MenuChangeRequest? Request { get; set; }
        public string? StatusName { get; set; }
        public string? OrderNumber { get; set; }
        public string? CustomerName { get; set; }
        public string? MenuCode { get; set; }
        public string? OriginalDishName { get; set; }
        public string? RequestedDishName { get; set; }
        public string? ShiftCode { get; set; }
        public string? RequestedByName { get; set; }
        public Guid? ApprovalRequestId { get; set; }
        public string? ApprovalRequestNo { get; set; }
        /// <summary>Kế hoạch sản xuất của ngày ăn (nếu có).</summary>
        public Guid? ProductionPlanId { get; set; }
        public string? ProductionPlanNumber { get; set; }
        public string? ProductionPlanStatus { get; set; }
        /// <summary>Kế hoạch đã Duyệt / Phát hành → đổi món không tự áp vào lệnh SX – bếp trưởng xử lý tay.</summary>
        public bool PlanNeedsManualAction { get; set; }
        public List<string> ListWarning { get; set; } = [];
    }
}
