namespace AppDelicare.Model.Model
{
    /// <summary>UI-6.27 (D-108): báo cáo "Giá vốn & lãi gộp" theo tháng – GroupBy customer (khách × hợp đồng) | dish | day. Nhân công / chung = 0 → "Lãi sau nguyên liệu".</summary>
    public class MealMarginRequest
    {
        /// <summary>"yyyy-MM" – trống = tháng hiện tại (tính tới hôm nay).</summary>
        public string? Month { get; set; }
        public string? GroupBy { get; set; }
        public Guid? CustomerId { get; set; }
        public Guid? ContractId { get; set; }
    }

    public class MealMarginModel
    {
        public string Month { get; set; } = string.Empty;
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public string GroupBy { get; set; } = "customer";
        public decimal ThresholdPct { get; set; }
        /// <summary>Có định mức nhân công / chi phí chung (cấu hình) → nhãn "Lãi gộp"; không → "Lãi sau nguyên liệu".</summary>
        public bool LaborIncluded { get; set; }
        public MealMarginSummaryModel Summary { get; set; } = new();
        public List<MealMarginRowModel> ListRow { get; set; } = [];
        public List<MealMarginDishModel> ListDish { get; set; } = [];
        public List<MealMarginDayModel> ListDay { get; set; } = [];
    }

    public class MealMarginSummaryModel
    {
        public int OrderCount { get; set; }
        public decimal Meals { get; set; }
        public decimal Revenue { get; set; }
        public decimal Cost { get; set; }
        public decimal Margin { get; set; }
        public decimal? RatioPct { get; set; }
        public decimal? CostPerMeal { get; set; }
        public decimal? PrevCostPerMeal { get; set; }
        public decimal FaultCost { get; set; }
        public decimal FaultMeals { get; set; }
        /// <summary>Đơn đang tạm tính theo giá thành chuẩn (lệnh SX của món chưa có giá thành thực tế) – tự tính lại khi lệnh đóng.</summary>
        public int ProvisionalOrders { get; set; }
        public decimal? ProvisionalMealsPct { get; set; }
    }

    public class MealMarginRowModel
    {
        public Guid? CustomerId { get; set; }
        public Guid? ContractId { get; set; }
        public string? Name { get; set; }
        public string? Sub { get; set; }
        public int OrderCount { get; set; }
        public decimal Meals { get; set; }
        public decimal Revenue { get; set; }
        public decimal Cost { get; set; }
        public decimal Margin { get; set; }
        public decimal? PricePerMeal { get; set; }
        public decimal? CostPerMeal { get; set; }
        public decimal? RatioPct { get; set; }
        public decimal FaultCost { get; set; }
        public int ProvisionalOrders { get; set; }
    }

    public class MealMarginDishModel
    {
        public Guid DishId { get; set; }
        public string? DishName { get; set; }
        public decimal Portions { get; set; }
        public decimal? StandardPerPortion { get; set; }
        public decimal? ActualPerPortion { get; set; }
        public decimal? VariancePct { get; set; }
        public decimal? VarianceAmount { get; set; }
        public string? MainCause { get; set; }
        public Guid? LastPlanId { get; set; }
    }

    public class MealMarginDayModel
    {
        public DateTime Date { get; set; }
        public int OrderCount { get; set; }
        public decimal Meals { get; set; }
        public decimal Revenue { get; set; }
        public decimal Cost { get; set; }
        public decimal? RatioPct { get; set; }
        public int ProvisionalOrders { get; set; }
    }
}
