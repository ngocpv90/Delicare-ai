namespace AppDelicare.Model.Model
{
    /// <summary>UI-6.15 (D-96): màn "Đơn hàng theo ngày" – <c>Order/GetDay</c>.</summary>
    public class OrderDayRequest
    {
        /// <summary>Ngày ăn (mặc định ngày mai).</summary>
        public DateTime? MealDate { get; set; }
        public Guid? BranchId { get; set; }
    }

    public class OrderDayModel
    {
        public DateTime MealDate { get; set; }
        public DateTime WeekStart { get; set; }
        public DateTime CutoffAt { get; set; }
        /// <summary>OPEN (chưa chốt) · LOCKED (đã chốt) · DELIVERED (đã giao xong).</summary>
        public string DayState { get; set; } = "OPEN";
        public bool ReminderSent { get; set; }
        public int PointCount { get; set; }
        public int PointsWithOrder { get; set; }
        public int CustomerCount { get; set; }
        public decimal TotalQuantity { get; set; }
        public decimal? ForecastTotal { get; set; }
        public decimal? ForecastDeviationPct { get; set; }
        public int AttentionCount { get; set; }
        public int ExtraCount { get; set; }
        public int CancelledCount { get; set; }
        public decimal TotalAmount { get; set; }
        /// <summary>Buổi có trong hợp đồng hiệu lực của ngày (SANG / TRUA / TOI / DEM) – cột của bảng.</summary>
        public List<string> ListMealType { get; set; } = [];
        public Dictionary<string, decimal> MealTotal { get; set; } = [];
        public List<OrderDayCustomerModel> ListCustomer { get; set; } = [];
    }

    public class OrderDayCustomerModel
    {
        public Guid CustomerId { get; set; }
        public string? CustomerCode { get; set; }
        public string? CustomerName { get; set; }
        public int PointCount { get; set; }
        public int AttentionCount { get; set; }
        public decimal Total { get; set; }
        public Dictionary<string, decimal> MealTotal { get; set; } = [];
        public List<OrderDayRowModel> ListRow { get; set; } = [];
    }

    public class OrderDayRowModel
    {
        public Guid DeliveryLocationId { get; set; }
        public string? LocationCode { get; set; }
        public string? LocationName { get; set; }
        public Guid? OrderId { get; set; }
        public string? OrderNumber { get; set; }
        /// <summary>WEEK (từ đăng ký suất tuần) · EXTRA (đơn phát sinh) · null (điểm chưa có đơn).</summary>
        public string? Source { get; set; }
        public string? ExtraReason { get; set; }
        public string? Status { get; set; }
        public string? StatusName { get; set; }
        /// <summary>0 = chưa xác nhận (Nháp / Đã gửi) · 1 Đã xác nhận · 2 Đã chốt · 3 Đang nấu · 4 Đã giao · 5 Đã đối soát · -1 Đã hủy.</summary>
        public int Progress { get; set; }
        public decimal Total { get; set; }
        public decimal? Forecast { get; set; }
        public decimal Amount { get; set; }
        public List<OrderDayMealModel> ListMeal { get; set; } = [];
        public List<OrderDayAttentionModel> ListAttention { get; set; } = [];
    }

    public class OrderDayMealModel
    {
        public string MealType { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        /// <summary>Chia theo lựa chọn (rê chuột).</summary>
        public List<OrderDayOptionModel> ListOption { get; set; } = [];
    }

    public class OrderDayOptionModel
    {
        public int OptionNo { get; set; }
        public decimal Quantity { get; set; }
    }

    public class OrderDayAttentionModel
    {
        /// <summary>NO_NUMBER · UNCONFIRMED · DEVIATION · PENDING_APPROVAL · EXTRA.</summary>
        public string Code { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        /// <summary>warn (cam – tính vào "cần xử lý") · info (xanh – không tính).</summary>
        public string Tone { get; set; } = "warn";
        /// <summary>INPUT (mở Đăng ký suất tuần) · CONFIRM · APPROVE · null.</summary>
        public string? Action { get; set; }
        public Guid? ApprovalRequestId { get; set; }
    }

    /// <summary>UI-6.15: "Thêm đơn phát sinh" – đơn ngoài đăng ký tuần (cho phép thêm vào điểm đã có đơn tuần).</summary>
    public class OrderExtraRequest
    {
        public Guid CustomerId { get; set; }
        public Guid DeliveryLocationId { get; set; }
        public DateTime MealDate { get; set; }
        /// <summary>EVENT (sự kiện / tiệc) · ADDON (khách đặt thêm) · COMPENSATION (đơn bù do phản ánh) · OTHER.</summary>
        public string? Reason { get; set; }
        public string? Note { get; set; }
        public List<AppDelicare.Model.Request.Custom.OrderCreateLineRequest> ListLine { get; set; } = [];
    }
}
