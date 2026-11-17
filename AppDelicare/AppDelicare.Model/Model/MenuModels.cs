namespace AppDelicare.Model.Model
{
    /// <summary>
    /// T13 (spec 6.2 calendar): thực đơn tuần dạng lịch – hàng = (loại suất, lựa chọn, nhóm món), cột = thứ 1..7 (1 = Thứ 2 … 7 = CN).
    /// Lựa chọn (Lựa chọn 1/2/3) = MenuLine.SequenceNo (GAP-MENU-01, PROPOSED).
    /// </summary>
    public class MenuCalendarModel
    {
        public Guid MenuId { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? Status { get; set; }
        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public string? MealType { get; set; }
        public List<int> Days { get; set; } = [1, 2, 3, 4, 5, 6, 7];
        /// <summary>Giá bán / suất (chưa VAT) dùng tính FC%; null = không tính FC%.</summary>
        public decimal? SellingPrice { get; set; }
        public string? SellingPriceSource { get; set; }
        /// <summary>Ngưỡng cảnh báo FC% (BusinessRules:KpiIngredientToRevenueMaxPct).</summary>
        public decimal FoodCostTargetPct { get; set; }
        public List<MenuCalendarRowModel> ListRow { get; set; } = [];
        public List<MenuCalendarTotalModel> ListTotal { get; set; } = [];
    }

    public class MenuCalendarRowModel
    {
        public string? MealType { get; set; }
        public int OptionNo { get; set; }
        public Guid? DishCategoryId { get; set; }
        public string? DishCategoryName { get; set; }
        /// <summary>Khóa = thứ ("1".."7").</summary>
        public Dictionary<int, List<MenuCalendarDishModel>> Cells { get; set; } = [];
    }

    public class MenuCalendarDishModel
    {
        public Guid MenuLineId { get; set; }
        public Guid DishId { get; set; }
        public string? DishCode { get; set; }
        public string? DishName { get; set; }
        public decimal? PortionQty { get; set; }
        public int SortOrder { get; set; }
        /// <summary>Giá vốn NVL / suất theo công thức đang khóa (null = món chưa có công thức hiệu lực).</summary>
        public decimal? CostPerMeal { get; set; }
        public bool CostComplete { get; set; }
    }

    /// <summary>Giá vốn / suất và FC% theo (loại suất, lựa chọn, thứ).</summary>
    public class MenuCalendarTotalModel
    {
        public string? MealType { get; set; }
        public int OptionNo { get; set; }
        public int DayOfWeek { get; set; }
        public int DishCount { get; set; }
        public decimal CostPerMeal { get; set; }
        /// <summary>false = có món thiếu công thức / thiếu giá NCC.</summary>
        public bool CostComplete { get; set; }
        /// <summary>FC% = giá vốn / giá bán × 100 (PROPOSED); null khi không có giá bán.</summary>
        public decimal? FoodCostPercent { get; set; }
        public bool FoodCostOverTarget { get; set; }
    }

    /// <summary>T13 (spec 6.3 resolve): thực đơn áp dụng.</summary>
    public class MenuResolveModel
    {
        public Guid MenuId { get; set; }
        public string? MenuCode { get; set; }
        public string? MenuName { get; set; }
        public Guid MenuAssignmentId { get; set; }
        /// <summary>LOCATION = gán theo điểm giao; CUSTOMER = gán theo khách.</summary>
        public string? Level { get; set; }
    }
}
