namespace AppDelicare.Model.Option
{
    /// <summary>Section <c>BusinessRules</c> của appsettings (BUSINESS_RULES.md). Đổi ngưỡng không cần sửa code.</summary>
    public sealed class BusinessRulesOptions
    {
        public const string Section = "BusinessRules";

        public string TimeZone { get; set; } = "Asia/Ho_Chi_Minh";
        public int OrderCutoffHourPreviousDay { get; set; } = 20;
        public string OrderLockJobTime { get; set; } = "20:00";
        public string MrpJobTime { get; set; } = "20:30";
        public string PurchaseProposalJobTime { get; set; } = "21:00";
        public decimal StockCountVariancePctNeedApproval { get; set; } = 2.0m;
        public decimal PortionDeviationPctNeedApproval { get; set; } = 30.0m;
        public int TransportMinutesBeforeCutoff { get; set; } = 60;
        public decimal CoreTempMinC { get; set; } = 70.0m;
        public decimal DeliveryTempMinC { get; set; } = 60.0m;
        public decimal KpiOnTimeDeliveryTargetPct { get; set; } = 95.0m;
        public decimal KpiIngredientToRevenueMaxPct { get; set; } = 55.0m;
        public decimal KpiContainerLossMaxPct { get; set; } = 3.0m;
        public Dictionary<string, int> ComplaintSlaHours { get; set; } = new() { ["HIGH"] = 4, ["MEDIUM"] = 24, ["LOW"] = 72 };
        public Dictionary<string, int> ChecklistOffsetsMinutes { get; set; } = new()
        {
            ["PREP"] = -150, ["PREP_AREA_CLEAN"] = -90, ["BLAST_CHILL"] = -20, ["KITCHEN_CLEAN"] = 90,
        };
        public int ExpiringLotDays { get; set; } = 7;
        public int ShortageForecastDays { get; set; } = 3;
        /// <summary>Spec 5.2 bước 4: chi phí nhân công / suất khi khóa công thức (PROPOSED – BA/PO CONFIRM).</summary>
        public decimal RecipeLaborCostPerServing { get; set; }
        /// <summary>Spec 5.2 bước 4: chi phí chung / suất (PROPOSED – BA/PO CONFIRM).</summary>
        public decimal RecipeOverheadCostPerServing { get; set; }
        /// <summary>Số suất chuẩn của định lượng công thức khi Recipe.StandardOutputQty trống (spec 5.2: 100 suất).</summary>
        public decimal RecipeStandardServings { get; set; } = 100;
    }
}
