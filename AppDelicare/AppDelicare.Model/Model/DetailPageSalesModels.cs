using AppDelicare.Model.Entity;

namespace AppDelicare.Model.Model
{
    // UI-6 (6.2 nhóm A – D-80): dữ liệu trang chi tiết toàn màn của nhóm Kinh doanh (…/GetOverview) – gộp từ dữ liệu có sẵn, không bảng / cột mới.
    // Quy ước như StockCountOverviewModel (DetailPageModels.cs): Detail = model chi tiết đang có (dùng lại, không viết lại luật) + phần bổ sung cho phần đầu / chỉ số / tab.

    /// <summary>UI-6: trang chi tiết Đơn đặt suất (Order/GetOverview).</summary>
    public class OrderPageModel
    {
        public OrderDetailModel Detail { get; set; } = new();
        /// <summary>Yêu cầu duyệt mới nhất của đơn (lệch suất / sửa sau giờ chốt, mọi trạng thái) – tab Duyệt.</summary>
        public Guid? ApprovalRequestId { get; set; }
        /// <summary>NV phụ trách khách hàng (Customer.UserAssignId → Person.FullName, không có thì tên đăng nhập).</summary>
        public string? ResponsibleName { get; set; }
        public string? CreatedByName { get; set; }
        public string? CustomerCode { get; set; }
        public int LineCount { get; set; }
        public int DeliveryCount { get; set; }
        /// <summary>Σ suất khách nhận theo xác nhận giao (POD) của các đơn giao còn hiệu lực; null = chưa có xác nhận.</summary>
        public decimal? AcceptedQuantity { get; set; }
        public int ComplaintCount { get; set; }
        public int OpenComplaintCount { get; set; }
        public List<SalesDeliveryBriefModel> ListDelivery { get; set; } = [];
        public SalesReconciliationBriefModel? Reconciliation { get; set; }
        public List<SalesComplaintBriefModel> ListComplaint { get; set; } = [];
    }

    /// <summary>UI-6: trang chi tiết Hợp đồng (Contract/GetOverview).</summary>
    public class ContractPageModel
    {
        public ContractDetailModel Detail { get; set; } = new();
        public Guid? ApprovalRequestId { get; set; }
        public string? ResponsibleName { get; set; }
        public string? CustomerCode { get; set; }
        public int MealCount { get; set; }
        public int OrderCount { get; set; }
        /// <summary>Số ngày còn tới ngày hiệu lực đến (âm = đã quá); null = không thời hạn.</summary>
        public int? DaysToEnd { get; set; }
        /// <summary>Σ suất các đơn (trừ Hủy) có ngày ăn trong 30 ngày gần nhất.</summary>
        public decimal OrderedQuantity30d { get; set; }
        /// <summary>Σ tiền chốt đối soát của các đơn thuộc hợp đồng.</summary>
        public decimal ReconciledAmount { get; set; }
        public int LocationCount { get; set; }
        public List<OrderBriefModel> ListRecentOrder { get; set; } = [];
    }

    /// <summary>UI-6: trang chi tiết Phản ánh khách hàng (CustomerComplaint/GetOverview).</summary>
    public class ComplaintPageModel
    {
        public CustomerComplaintDetailModel Detail { get; set; } = null!;
        public string? ResponsibleName { get; set; }
        public string? CustomerCode { get; set; }
        /// <summary>Số giờ còn tới hạn SLA (âm = quá hạn); null = không có hạn hoặc đã xong.</summary>
        public decimal? HoursToDue { get; set; }
        /// <summary>Số giờ từ tiếp nhận tới xử lý xong; null = chưa xử lý xong.</summary>
        public decimal? ResolveHours { get; set; }
        public int HistoryCount { get; set; }
        /// <summary>Số phản ánh đang mở khác của cùng khách hàng.</summary>
        public int CustomerOpenComplaintCount { get; set; }
        /// <summary>BIZ-3.1: còn được tạo đơn bù (loại thiếu suất / hỏng / nhiệt độ, có đơn gốc, đang mở, chưa có đơn bù).</summary>
        public bool CanCompensate { get; set; }
        public List<ComplaintHistoryItemModel> ListHistory { get; set; } = [];
        /// <summary>Đơn bù (Note chứa [DON_BU:…|số phản ánh]).</summary>
        public List<OrderBriefModel> ListCompensationOrder { get; set; } = [];
    }

    public class ComplaintHistoryItemModel
    {
        public DateTime EventAt { get; set; }
        public string? EventType { get; set; }
        public string? OldStatusName { get; set; }
        public string? NewStatusName { get; set; }
        public string? Comment { get; set; }
        public string? ActorName { get; set; }
    }

    /// <summary>UI-6: trang chi tiết Điểm giao hàng (CustomerDeliveryLocation/GetOverview).</summary>
    public class DeliveryLocationPageModel
    {
        public CustomerDeliveryLocation Location { get; set; } = null!;
        public string? CustomerName { get; set; }
        public string? CustomerCode { get; set; }
        public string? ResponsibleName { get; set; }
        /// <summary>30 ngày gần nhất (theo ngày ăn, trừ đơn Hủy).</summary>
        public int OrderCount30d { get; set; }
        public decimal MealQuantity30d { get; set; }
        /// <summary>Σ suất 30 ngày ÷ số ngày có đơn.</summary>
        public decimal AvgMealsPerDay30d { get; set; }
        /// <summary>Đơn giao Đã giao / Thất bại trong 30 ngày (theo ngày giao).</summary>
        public int DeliveryCount30d { get; set; }
        /// <summary>% giao đúng giờ (giờ giao thực tế ≤ dự kiến + BusinessRules:Kpi:OnTimeToleranceMinutes); null = chưa có đơn giao.</summary>
        public decimal? OnTimePct30d { get; set; }
        public int ComplaintCount { get; set; }
        public int OpenComplaintCount { get; set; }
        public List<OrderBriefModel> ListRecentOrder { get; set; } = [];
        public List<SalesDeliveryBriefModel> ListRecentDelivery { get; set; } = [];
        public List<SalesComplaintBriefModel> ListComplaint { get; set; } = [];
    }

    public class SalesDeliveryBriefModel
    {
        public Guid Id { get; set; }
        public string? DeliveryNumber { get; set; }
        public string? OrderNumber { get; set; }
        public string? Status { get; set; }
        public string? StatusName { get; set; }
        public string? LocationName { get; set; }
        public string? RouteNo { get; set; }
        public DateTime DeliveryDate { get; set; }
        public DateTime PlannedDeliveryTime { get; set; }
        public DateTime? ActualDeliveryTime { get; set; }
        public decimal? AcceptedQuantity { get; set; }
        public decimal? RejectedQuantity { get; set; }
        public string? ReceiverName { get; set; }
    }

    public class SalesReconciliationBriefModel
    {
        public Guid Id { get; set; }
        public string? Status { get; set; }
        public string? StatusName { get; set; }
        public DateTime ReconciliationDate { get; set; }
        public decimal OrderedQuantity { get; set; }
        public decimal? ProducedQty { get; set; }
        public decimal? PackedQty { get; set; }
        public decimal DeliveredQuantity { get; set; }
        public decimal AcceptedQuantity { get; set; }
        public decimal? VariancePercent { get; set; }
        public decimal? FinalAmount { get; set; }
    }

    public class SalesComplaintBriefModel
    {
        public Guid Id { get; set; }
        public string? ComplaintNo { get; set; }
        public string? OrderNumber { get; set; }
        public string? CategoryCode { get; set; }
        public string? Severity { get; set; }
        public string? SeverityName { get; set; }
        public string? Status { get; set; }
        public string? StatusName { get; set; }
        public DateTime ReceivedAt { get; set; }
        public DateTime? SlaDueAt { get; set; }
        public bool IsOverdue { get; set; }
        public string? Description { get; set; }
    }
}
