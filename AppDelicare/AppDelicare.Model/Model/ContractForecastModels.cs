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
