namespace AppDelicare.Model.Model
{
    /// <summary>
    /// BIZ-1.3 (D-77 c): nhu cầu nguyên liệu nhiều ngày (D+1 → D+N, mặc định 7) – chỉ tính khi xem, không lưu.
    /// Nguồn từng ngày theo (khách, điểm giao, loại suất): đơn Đã chốt → đăng ký / đơn Đã xác nhận → dự báo Đã công bố.
    /// Tồn trừ LŨY KẾ: thiếu của ngày k = thiếu lũy kế tới ngày k − thiếu lũy kế tới ngày trước.
    /// </summary>
    public class MaterialHorizonModel
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public DateTime Today { get; set; }
        public List<MaterialHorizonDayModel> ListDay { get; set; } = [];
        public List<MaterialHorizonItemModel> ListItem { get; set; } = [];
        public List<string> ListWarning { get; set; } = [];
        /// <summary>Số nguyên liệu phải đặt hôm nay / đã trễ ngày đặt.</summary>
        public int DueTodayCount { get; set; }
        public int LateCount { get; set; }
    }

    public class MaterialHorizonDayModel
    {
        public DateTime Date { get; set; }
        /// <summary>Số suất theo nguồn: đơn Đã chốt / đăng ký – đơn Đã xác nhận / dự báo Đã công bố.</summary>
        public decimal LockedMeals { get; set; }
        public decimal ConfirmedMeals { get; set; }
        public decimal ForecastMeals { get; set; }
        /// <summary>LOCKED / CONFIRMED / FORECAST / MIXED / NONE + nhãn tiếng Việt.</summary>
        public string Source { get; set; } = "NONE";
        public string? SourceName { get; set; }
        public List<string> ListWarning { get; set; } = [];
    }

    public class MaterialHorizonItemModel
    {
        public Guid IngredientId { get; set; }
        public string? IngredientCode { get; set; }
        public string? IngredientName { get; set; }
        public Guid UnitId { get; set; }
        public string? UnitName { get; set; }
        public decimal SafetyStock { get; set; }
        /// <summary>Tồn xuất được tại ngày đầu kỳ.</summary>
        public decimal AvailableQuantity { get; set; }
        public decimal TotalRequired { get; set; }
        /// <summary>Tổng phải mua thêm trong kỳ (sau khi trừ tồn, hàng sắp về, đề nghị mua đang mở).</summary>
        public decimal TotalShortage { get; set; }
        public DateTime? FirstShortageDate { get; set; }
        /// <summary>Ngày phải đặt = ngày cần đầu tiên − lead time NCC ưu tiên − PurchaseOrderLeadBufferDays.</summary>
        public DateTime? OrderByDate { get; set; }
        /// <summary>LATE (đã trễ) / TODAY (phải đặt hôm nay) / UPCOMING (đặt trước ngày …) / NONE (đủ hàng).</summary>
        public string OrderStatus { get; set; } = "NONE";
        public string? OrderStatusName { get; set; }
        public string? SupplierName { get; set; }
        public int? LeadTimeDays { get; set; }
        public List<MaterialHorizonCellModel> ListDay { get; set; } = [];
    }

    public class MaterialHorizonCellModel
    {
        public DateTime Date { get; set; }
        public decimal Required { get; set; }
        /// <summary>Phần phải mua thêm cho ngày này (sau khi trừ lũy kế).</summary>
        public decimal Shortage { get; set; }
        public DateTime? OrderByDate { get; set; }
    }
}

namespace AppDelicare.Model.Request.Custom
{
    /// <summary>BIZ-1.3: kỳ tính nhu cầu nhiều ngày. Từ ngày trống = ngày mai; số ngày trống = BusinessRules:PurchaseHorizonDays (1 – 31).</summary>
    public class MaterialHorizonRequest
    {
        public DateTime? FromDate { get; set; }
        public int? Days { get; set; }
        public Guid? OrganizationUnitId { get; set; }
        public Guid? WarehouseId { get; set; }
    }

    /// <summary>BIZ-1.3: lập đề nghị mua Nháp cho phần phải đặt – mỗi dòng theo NGÀY CẦN HÀNG. OnlyDue = chỉ phần có ngày phải đặt ≤ hôm nay.</summary>
    public class MaterialHorizonPurchaseRequest : MaterialHorizonRequest
    {
        public List<Guid>? ListIngredientId { get; set; }
        public bool OnlyDue { get; set; } = true;
        public string? Note { get; set; }
    }
}
