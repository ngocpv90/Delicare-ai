using AppDelicare.Model.Entity;

namespace AppDelicare.Model.Model
{
    /// <summary>BE2.1 (spec 9.3 POST temperature-logs): lần đo vừa ghi + cảnh báo ngoài ngưỡng.</summary>
    public class TemperatureLogRecordModel
    {
        public TemperatureLog Log { get; set; } = null!;
        public string? TargetName { get; set; }
        public List<string> ListWarning { get; set; } = [];
    }

    /// <summary>BE2.1 (spec 8.4 waste-records): 1 dòng hao hụt + tên món / ca / mẻ / loại.</summary>
    public class MealWasteItemModel
    {
        public MealWasteRecord Record { get; set; } = null!;
        public string? WasteTypeName { get; set; }
        public string? DishName { get; set; }
        public string? ShiftCode { get; set; }
        public string? BatchNumber { get; set; }
        public string? ProductionOrderNumber { get; set; }
        public string? RecordedByName { get; set; }
    }

    /// <summary>BE2.1: danh sách hao hụt theo kỳ + tổng theo loại (biểu đồ tổng hợp 157 để sau – dashboard).</summary>
    public class MealWasteListModel
    {
        public List<MealWasteItemModel> ListItem { get; set; } = [];
        public decimal TotalMeals { get; set; }
        public decimal TotalKg { get; set; }
        public Dictionary<string, decimal> MealsByType { get; set; } = [];
    }

    /// <summary>BE2.1 (spec 6.6 GET orders view=board): 1 thẻ đơn trên bảng.</summary>
    public class OrderBoardItemModel
    {
        public Guid Id { get; set; }
        public string? OrderNumber { get; set; }
        public Guid CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public Guid? DeliveryLocationId { get; set; }
        public string? LocationName { get; set; }
        public DateTime MealDate { get; set; }
        public DateTime DeliveryDate { get; set; }
        public decimal? TotalQuantity { get; set; }
        public decimal? TotalAmount { get; set; }
        public string? Status { get; set; }
    }

    public class OrderBoardColumnModel
    {
        public string Status { get; set; } = string.Empty;
        public string StatusName { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal TotalQuantity { get; set; }
        public List<OrderBoardItemModel> ListItem { get; set; } = [];
    }

    /// <summary>BE2.1: bảng đơn hàng 8 cột trạng thái (STATUS_CATALOG Order.Status) + đếm theo trạng thái.</summary>
    public class OrderBoardModel
    {
        public List<OrderBoardColumnModel> ListColumn { get; set; } = [];
        public Dictionary<string, int> StatusCounts { get; set; } = [];
        public int TotalRecords { get; set; }
    }

    /// <summary>BE2.1 (spec 6.2 GET menus): thực đơn + số dòng, số món, số khách được phân công, tổng suất đã đặt.</summary>
    public class MenuListItemModel
    {
        public Menu Menu { get; set; } = null!;
        public string? StatusName { get; set; }
        public int LineCount { get; set; }
        public int DishCount { get; set; }
        public int AssignedCustomerCount { get; set; }
        public decimal OrderedMeals { get; set; }
    }

    /// <summary>BE2.1 (spec 10.5 GET complaints): 1 dòng phản ánh đủ trường hiển thị.</summary>
    public class CustomerComplaintListItemModel
    {
        public CustomerComplaint Complaint { get; set; } = null!;
        public string? StatusName { get; set; }
        public string? SeverityName { get; set; }
        public string? CustomerName { get; set; }
        public string? OrderNumber { get; set; }
        public string? DeliveryNumber { get; set; }
        public string? RouteNo { get; set; }
        public string? AssignedToName { get; set; }
        /// <summary>SlaDueAt &lt; bây giờ và chưa Đã xử lý / Đóng / Từ chối.</summary>
        public bool IsOverdue { get; set; }
        /// <summary>Số giờ còn lại tới hạn SLA (âm = quá hạn); null khi không có hạn / đã xong.</summary>
        public decimal? HoursToDue { get; set; }
    }

    public class CustomerComplaintListModel
    {
        public List<CustomerComplaintListItemModel> ListItem { get; set; } = [];
        public int TotalRecords { get; set; }
        public Dictionary<string, int> StatusCounts { get; set; } = [];
        public int OverdueCount { get; set; }
    }

    /// <summary>BE2.1 (spec 11.5 GET approvals/{id}): yêu cầu duyệt + bước + quyết định + mục + bản ghi nghiệp vụ.</summary>
    public class ApprovalDetailModel
    {
        public ApprovalRequest Request { get; set; } = null!;
        public string? StatusName { get; set; }
        public string? RequestedByName { get; set; }
        public string? ScreenId { get; set; }
        public string? Title { get; set; }
        public List<ApprovalStep> ListStep { get; set; } = [];
        public List<ApprovalDecisionItemModel> ListDecision { get; set; } = [];
        public List<ApprovalItem> ListItem { get; set; } = [];
        /// <summary>Bảng nghiệp vụ (PurchaseOrder / Recipe / Order) + bản ghi + dòng.</summary>
        public ApprovalBusinessModel Business { get; set; } = new();
        /// <summary>Đề xuất lưu lúc gửi (vd. số suất mới của đơn lệch &gt; 30%).</summary>
        public string? ProposalJson { get; set; }
        public bool CanDecide { get; set; }
        public bool CanCancel { get; set; }
    }

    public class ApprovalDecisionItemModel
    {
        public ApprovalDecision Decision { get; set; } = null!;
        public string? DecidedByName { get; set; }
    }

    public class ApprovalBusinessModel
    {
        public string? Table { get; set; }
        public Guid? Id { get; set; }
        public string? Label { get; set; }
        public object? Record { get; set; }
        public object? Lines { get; set; }
    }

    /// <summary>BE2.1 (spec 7.5 GET lots): lô + tồn, nguyên liệu, NCC, cờ hết hạn / sắp hết hạn / bị chặn.</summary>
    public class MaterialLotListItemModel
    {
        public MaterialLot Lot { get; set; } = null!;
        public string? IngredientCode { get; set; }
        public string? IngredientName { get; set; }
        public string? SupplierName { get; set; }
        public decimal OnHandQuantity { get; set; }
        public decimal AvailableQuantity { get; set; }
        public int? DaysToExpiry { get; set; }
        public bool IsExpired { get; set; }
        public bool IsBlocked { get; set; }
    }

    /// <summary>
    /// BE2.1 (spec 7.5 trace, Catalog 118 / 119) – truy xuất 2 chiều. Xuôi: lô NL → phiếu xuất → lệnh SX → mẻ → lệnh đóng gói → đơn → đơn giao → khách.
    /// Ngược: mẻ → phiếu xuất → lô NL → phiếu nhập → PO → NCC. <c>Inferred</c> = đơn / khách suy ra theo ngày + ca của lệnh SX (mẻ chưa có lệnh đóng gói – GAP-TRC-01).
    /// </summary>
    public class TraceModel
    {
        public string Direction { get; set; } = string.Empty;
        public TraceNodeModel Root { get; set; } = new();
        public List<TraceNodeModel> ListSupplier { get; set; } = [];
        public List<TraceNodeModel> ListPurchaseOrder { get; set; } = [];
        public List<TraceNodeModel> ListGoodsReceipt { get; set; } = [];
        public List<TraceNodeModel> ListLot { get; set; } = [];
        public List<TraceNodeModel> ListIssue { get; set; } = [];
        public List<TraceNodeModel> ListProductionOrder { get; set; } = [];
        public List<TraceNodeModel> ListBatch { get; set; } = [];
        public List<TraceNodeModel> ListPackingOrder { get; set; } = [];
        public List<TraceNodeModel> ListOrder { get; set; } = [];
        public List<TraceNodeModel> ListDeliveryOrder { get; set; } = [];
        public List<TraceNodeModel> ListCustomer { get; set; } = [];
        public List<TraceNodeModel> ListTemperature { get; set; } = [];
        public List<string> ListWarning { get; set; } = [];
    }

    public class TraceNodeModel
    {
        public Guid Id { get; set; }
        public string? Type { get; set; }
        public string? Number { get; set; }
        public string? Name { get; set; }
        public string? Status { get; set; }
        public DateTime? Date { get; set; }
        public decimal? Quantity { get; set; }
        public string? Note { get; set; }
        /// <summary>Liên kết suy luận (không có cột liên kết trực tiếp).</summary>
        public bool Inferred { get; set; }
    }
}
