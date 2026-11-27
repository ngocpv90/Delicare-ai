namespace AppDelicare.Model.Model
{
    // RETAIL-1 (docs/frontend-handoff/RETAIL-1.md mục 5): kết quả API bán lẻ – tên trường theo đặc tả (JSON camelCase).

    public class RetailCategoryModel
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class RetailCatalogProductModel
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string CategoryCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string? Description { get; set; }
        public List<string> Tags { get; set; } = [];
        /// <summary>Bữa được bán (nhãn: Sáng / Trưa / Tối).</summary>
        public List<string> MealTypes { get; set; } = [];
        public string? Icon { get; set; }
        public string? ImageUrl { get; set; }
    }

    public class RetailCatalogModel
    {
        public List<RetailCategoryModel> Categories { get; set; } = [];
        public List<RetailCatalogProductModel> Products { get; set; } = [];
    }

    public class RetailWeeklyMenuDayModel
    {
        /// <summary>yyyy-MM-dd.</summary>
        public string Date { get; set; } = string.Empty;
        public string Day { get; set; } = string.Empty;
        /// <summary>Bữa → tên món.</summary>
        public Dictionary<string, List<string>> Meals { get; set; } = [];
    }

    public class RetailWeeklyMenuModel
    {
        public string Title { get; set; } = string.Empty;
        public List<string> Meals { get; set; } = [];
        public List<RetailWeeklyMenuDayModel> Days { get; set; } = [];
    }

    public class RetailSubmitResultModel
    {
        public Guid Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string DeliveryDate { get; set; } = string.Empty;
        public string MealType { get; set; } = string.Empty;
    }

    public class RetailTrackLineModel
    {
        public string ProductName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string Unit { get; set; } = string.Empty;
        public decimal LineAmount { get; set; }
    }

    /// <summary>Tra cứu đơn của khách – KHÔNG có địa chỉ.</summary>
    public class RetailTrackModel
    {
        public string OrderNumber { get; set; } = string.Empty;
        public string OrderStatus { get; set; } = string.Empty;
        public string DeliveryDate { get; set; } = string.Empty;
        public string MealType { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
        public string? CancelReason { get; set; }
        public List<RetailTrackLineModel> Lines { get; set; } = [];
    }

    public class RetailOrderItemModel
    {
        public Guid Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string Channel { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public DateTime DeliveryDate { get; set; }
        public string MealType { get; set; } = string.Empty;
        public string OrderStatus { get; set; } = string.Empty;
        public decimal SubTotal { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal TotalAmount { get; set; }
        public string? PaymentMethod { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
        public string? Note { get; set; }
        /// <summary>"Suất cơm trưa × 2; …".</summary>
        public string LineSummary { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public string? CancelReason { get; set; }
    }

    public class RetailOrderListModel
    {
        public List<RetailOrderItemModel> ListItem { get; set; } = [];
        public int TotalRecords { get; set; }
        /// <summary>Số đơn theo trạng thái (theo lọc kênh / ngày / tìm, không theo lọc trạng thái).</summary>
        public Dictionary<string, int> StatusCounts { get; set; } = [];
        /// <summary>Đơn còn hiệu lực có ngày nhận hôm nay.</summary>
        public int TodayCount { get; set; }
        /// <summary>Doanh thu đơn hoàn thành có ngày nhận hôm nay.</summary>
        public decimal TodayAmount { get; set; }
    }

    public class RetailOrderLineItemModel
    {
        public Guid Id { get; set; }
        public Guid RetailProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineAmount { get; set; }
    }

    public class RetailOrderDetailModel
    {
        public RetailOrderItemModel Order { get; set; } = new();
        public List<RetailOrderLineItemModel> Lines { get; set; } = [];
        public List<string> AllowedTransitions { get; set; } = [];
        /// <summary>RETAIL-2: chuyến giao đã gán + chuyến cùng ngày để chọn + tiến độ như khách thấy.</summary>
        public Guid? DeliveryRouteId { get; set; }
        public RetailRouteModel? Route { get; set; }
        public List<RetailRouteOptionModel> ListRouteOption { get; set; } = [];
        public string StageLabel { get; set; } = string.Empty;
        public List<RetailProgressStepModel> ListStep { get; set; } = [];
        public bool HasAccount { get; set; }
    }

    public class RetailPrepItemModel
    {
        public string ProductName { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
    }

    public class RetailPrepMealModel
    {
        public string MealType { get; set; } = string.Empty;
        public int OrderCount { get; set; }
        public List<RetailPrepItemModel> Items { get; set; } = [];
    }

    public class RetailPrepSummaryModel
    {
        public string Date { get; set; } = string.Empty;
        public List<RetailPrepMealModel> Meals { get; set; } = [];
    }

    public class RetailCreatedModel
    {
        public Guid Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
    }
}
