using AppDelicare.Model.Entity;

namespace AppDelicare.Model.Model
{
    /// <summary>T13 (spec 6.6 new-template): khung tạo đơn của khách cho 1 ngày ăn.</summary>
    public class OrderTemplateModel
    {
        public Guid CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public DateTime MealDate { get; set; }
        public Guid ContractId { get; set; }
        public string? ContractNumber { get; set; }
        public string? ContractName { get; set; }
        /// <summary>Hạn chốt đơn của ngày ăn (20h ngày D-1 theo BusinessRules).</summary>
        public DateTime CutoffAt { get; set; }
        public bool IsAfterCutoff { get; set; }
        /// <summary>Mỗi điểm giao = 1 đơn (OrderLine không có cột điểm giao – GAP-ORD-01).</summary>
        public List<OrderTemplateLocationModel> ListLocation { get; set; } = [];
    }

    public class OrderTemplateLocationModel
    {
        public Guid DeliveryLocationId { get; set; }
        public string? LocationCode { get; set; }
        public string? LocationName { get; set; }
        /// <summary>Đơn chưa hủy đã có cho (khách, điểm, ngày) – có thì sửa đơn đó thay vì tạo mới.</summary>
        public Guid? ExistingOrderId { get; set; }
        public string? ExistingOrderNumber { get; set; }
        public List<OrderTemplateLineModel> ListLine { get; set; } = [];
    }

    public class OrderTemplateLineModel
    {
        public Guid ContractMealId { get; set; }
        public string? MealType { get; set; }
        public string? MealName { get; set; }
        public string? ServiceType { get; set; }
        public Guid? ShiftId { get; set; }
        public string? ShiftCode { get; set; }
        public string? ShiftName { get; set; }
        public decimal? UnitPrice { get; set; }
        public string? PriceSource { get; set; }
        public Guid? MenuId { get; set; }
        public string? MenuCode { get; set; }
        /// <summary>Số lượng gợi ý: Pilot = 0 (dự báo Forecast → DEFERRED(PILOT)).</summary>
        public decimal SuggestedQuantity { get; set; }
    }

    /// <summary>T13: chi tiết đơn + dòng + thao tác được phép + yêu cầu duyệt lệch suất đang chờ.</summary>
    public class OrderDetailModel
    {
        public Order? Order { get; set; }
        public string? StatusName { get; set; }
        public string? CustomerName { get; set; }
        public string? LocationName { get; set; }
        public string? ContractNumber { get; set; }
        public List<OrderLineDetailModel> ListLine { get; set; } = [];
        public DateTime CutoffAt { get; set; }
        public bool IsAfterCutoff { get; set; }
        /// <summary>Theo máy trạng thái + giờ chốt: updateLines, submit, confirm, lock, cancel.</summary>
        public List<string> AllowedActions { get; set; } = [];
        public Guid? PendingApprovalId { get; set; }
        public string? PendingApprovalNo { get; set; }
        /// <summary>Kết quả UpdateLines: true = đã gửi duyệt lệch suất (chưa ghi số mới).</summary>
        public bool ApprovalRequested { get; set; }
        public decimal? MaxDeviationPercent { get; set; }
        /// <summary>Cảnh báo không chặn (vd. dòng chưa có thực đơn được gán).</summary>
        public List<string> ListWarning { get; set; } = [];
    }

    public class OrderLineDetailModel
    {
        public Guid Id { get; set; }
        public Guid? ContractMealId { get; set; }
        public string? MealType { get; set; }
        public string? MealName { get; set; }
        public Guid? ShiftId { get; set; }
        public string? ShiftName { get; set; }
        public Guid? MenuId { get; set; }
        public string? MenuCode { get; set; }
        public decimal Quantity { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal LineAmount { get; set; }
        public string? Note { get; set; }
    }

    /// <summary>T13: kết quả nút chốt đơn theo ngày ăn.</summary>
    public class OrderLockSummaryModel
    {
        public DateTime MealDate { get; set; }
        public DateTime CutoffAt { get; set; }
        public int LockedCount { get; set; }
        public List<string> ListLockedNumber { get; set; } = [];
        /// <summary>Đơn chưa xác nhận (Draft / Submitted) của ngày đó – không khóa, cần xử lý tay.</summary>
        public List<string> ListNotConfirmedNumber { get; set; } = [];
        /// <summary>Đơn Confirmed còn yêu cầu duyệt lệch suất đang chờ – vẫn khóa, yêu cầu sẽ không duyệt được.</summary>
        public List<string> ListPendingApprovalNumber { get; set; } = [];
    }
}
