namespace AppDelicare.Model.Model
{
    /// <summary>
    /// ORD-OPT-1.4 (D-74 g): lưới đăng ký suất tuần của 1 điểm giao – hàng = (suất hợp đồng / loại suất, ca, lựa chọn), cột = 7 ngày ăn (T2 … CN).
    /// Mỗi ngày = 1 đơn (khách, điểm giao, ngày ăn).
    /// </summary>
    public class OrderWeekModel
    {
        public Guid CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public Guid DeliveryLocationId { get; set; }
        public string? LocationCode { get; set; }
        public string? LocationName { get; set; }
        /// <summary>Thứ Hai của tuần.</summary>
        public DateTime WeekStart { get; set; }
        public DateTime WeekEnd { get; set; }
        public string? ContractNumber { get; set; }
        /// <summary>Hạn gửi phiếu = giờ chốt của ngày đầu tuần (20:00 Chủ nhật trước).</summary>
        public DateTime Deadline { get; set; }
        public List<OrderWeekDayModel> ListDay { get; set; } = [];
        public List<OrderWeekRowModel> ListRow { get; set; } = [];
        /// <summary>Giá theo loại suất (giá ngày đầu tiên có hợp đồng).</summary>
        public List<OrderWeekPriceModel> ListPrice { get; set; } = [];
        public List<string> ListWarning { get; set; } = [];
    }

    public class OrderWeekDayModel
    {
        public DateTime MealDate { get; set; }
        /// <summary>1 = Thứ 2 … 7 = Chủ nhật.</summary>
        public int DayOfWeek { get; set; }
        public Guid? OrderId { get; set; }
        public string? OrderNumber { get; set; }
        public string? Status { get; set; }
        public string? StatusName { get; set; }
        /// <summary>Đã quá giờ chốt / đơn đã chốt trở đi / đang chờ duyệt / không có hợp đồng → không sửa ở màn tuần.</summary>
        public bool Locked { get; set; }
        public string? LockReason { get; set; }
        public DateTime CutoffAt { get; set; }
        public decimal TotalQuantity { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class OrderWeekRowModel
    {
        public Guid ContractMealId { get; set; }
        public string? MealType { get; set; }
        public string? MealName { get; set; }
        public Guid? ShiftId { get; set; }
        public string? ShiftName { get; set; }
        /// <summary>Số lựa chọn; null = dòng không theo lựa chọn (thực đơn chưa có / dòng đơn cũ).</summary>
        public int? MenuOptionNo { get; set; }
        /// <summary>7 ô theo thứ tự ngày.</summary>
        public List<OrderWeekCellModel> ListCell { get; set; } = [];
    }

    public class OrderWeekCellModel
    {
        public DateTime MealDate { get; set; }
        /// <summary>Lựa chọn có trong thực đơn áp dụng ngày đó (hoặc đã có dòng đơn) → nhập được.</summary>
        public bool Available { get; set; }
        public bool Locked { get; set; }
        public string? DishText { get; set; }
        public string? MenuCode { get; set; }
        /// <summary>LOCATION / CUSTOMER / COMMON (thực đơn chung).</summary>
        public string? MenuLevel { get; set; }
        public decimal Quantity { get; set; }
        public decimal? UnitPrice { get; set; }
        public Guid? OrderLineId { get; set; }
    }

    public class OrderWeekPriceModel
    {
        public string? MealType { get; set; }
        public string? MealName { get; set; }
        public decimal? UnitPrice { get; set; }
    }

    /// <summary>ORD-OPT-1.4: kết quả lưu / xác nhận tuần – từng ngày.</summary>
    public class OrderWeekSaveModel
    {
        public int Created { get; set; }
        public int Updated { get; set; }
        public int Confirmed { get; set; }
        public int Skipped { get; set; }
        public List<OrderWeekSaveDayModel> ListDay { get; set; } = [];
        public List<string> ListWarning { get; set; } = [];
        public OrderWeekModel? Week { get; set; }
    }

    public class OrderWeekSaveDayModel
    {
        public DateTime MealDate { get; set; }
        public Guid? OrderId { get; set; }
        public string? OrderNumber { get; set; }
        /// <summary>CREATED / UPDATED / UNCHANGED / SKIPPED / ERROR / APPROVAL / EMPTY.</summary>
        public string? Action { get; set; }
        public string? Status { get; set; }
        public string? StatusName { get; set; }
        public string? Message { get; set; }
    }

    /// <summary>ORD-OPT-1.5: phiếu đăng ký đọc từ Excel (chưa lưu) – ô đọc được + dòng lỗi.</summary>
    public class OrderWeekSlipModel
    {
        public Guid CustomerId { get; set; }
        public Guid DeliveryLocationId { get; set; }
        public DateTime WeekStart { get; set; }
        public List<OrderWeekSlipCellModel> ListCell { get; set; } = [];
        public List<string> ListError { get; set; } = [];
    }

    public class OrderWeekSlipCellModel
    {
        public DateTime MealDate { get; set; }
        public Guid ContractMealId { get; set; }
        public Guid? ShiftId { get; set; }
        public int? MenuOptionNo { get; set; }
        public decimal Quantity { get; set; }
    }
}
