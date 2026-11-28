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
        /// <summary>UI-6.51 (D-137 e): quyết định thay thế theo ngày cần (cặp chuẩn – thay thế).</summary>
        public List<MaterialHorizonSubstitutionModel> ListSubstitution { get; set; } = [];
        /// <summary>UI-6.51: lượng mua theo món của ngày chi tiết (chỉ khi gọi kèm ngày chi tiết – trang chi tiết ô kế hoạch mua).</summary>
        public List<MaterialHorizonDishUseModel> ListDishUse { get; set; } = [];
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
        /// <summary>UI-6.51 (D-137): bước "trừ hàng đã có" – nhu cầu lũy kế tới ngày, tồn khả dụng, hàng PO sắp về ≤ ngày, dòng đề nghị mua đang mở ≤ ngày (đơn vị cơ sở).</summary>
        public decimal CumRequired { get; set; }
        public decimal Available { get; set; }
        public decimal Incoming { get; set; }
        public decimal Requested { get; set; }
        public decimal SafetyStock { get; set; }
        public int? LeadTimeDays { get; set; }
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

namespace AppDelicare.Model.Model
{
    /// <summary>UI-6.51 (D-137 e): quyết định thay thế của một ngày cần hàng.</summary>
    public class MaterialHorizonSubstitutionModel
    {
        public DateTime Date { get; set; }
        public string? Group { get; set; }
        public Guid StdIngredientId { get; set; }
        public string? StdIngredientName { get; set; }
        public Guid SubIngredientId { get; set; }
        public string? SubIngredientName { get; set; }
        public decimal SharePct { get; set; }
        public decimal? MaxSharePct { get; set; }
        public decimal? StdUnitPrice { get; set; }
        public decimal? SubUnitPrice { get; set; }
        public decimal? StdUsablePrice { get; set; }
        public decimal? SubUsablePrice { get; set; }
        public decimal? StdWastePct { get; set; }
        public decimal? SubWastePct { get; set; }
        public decimal? SavingPct { get; set; }
        public string? StdSupplierName { get; set; }
        public string? SubSupplierName { get; set; }
        /// <summary>AUTO / OVERRIDE.</summary>
        public string? Source { get; set; }
        public bool Stored { get; set; }
        public string? Reason { get; set; }
        /// <summary>Tiết kiệm ước tính = phần thay thế (kg dùng được) × chênh giá dùng được.</summary>
        public decimal? SavingAmount { get; set; }
        public List<MaterialHorizonSubstitutionDishModel> ListDish { get; set; } = [];
    }

    public class MaterialHorizonSubstitutionDishModel
    {
        public Guid DishId { get; set; }
        public string? DishName { get; set; }
        public decimal Portions { get; set; }
        public decimal StdOnlyPortions { get; set; }
        public decimal EffectivePct { get; set; }
        /// <summary>Lượng mua (đơn vị dòng công thức) phần chuẩn / phần thay thế của món.</summary>
        public decimal StdGross { get; set; }
        public decimal SubGross { get; set; }
    }
}

namespace AppDelicare.Model.Model
{
    public class MaterialHorizonDishUseModel
    {
        public DateTime Date { get; set; }
        public Guid IngredientId { get; set; }
        public Guid DishId { get; set; }
        public string? DishName { get; set; }
        public decimal Portions { get; set; }
        /// <summary>Lượng mua (đơn vị cơ sở của nguyên liệu, sau hao hụt).</summary>
        public decimal Gross { get; set; }
    }
}
