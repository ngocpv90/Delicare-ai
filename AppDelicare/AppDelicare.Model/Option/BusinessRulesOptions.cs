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
        /// <summary>T13: ca giao mặc định theo loại suất (MealType → Shift.Code), D-19 / BUSINESS_RULES mục 0 (PROPOSED – BA/PO CONFIRM).</summary>
        public Dictionary<string, string> MealTypeDefaultShift { get; set; } = new(StringComparer.OrdinalIgnoreCase)
        {
            ["SANG"] = "SANG_6H", ["TRUA"] = "TRUA", ["TOI"] = "TOI", ["DEM"] = "DEM_23H",
        };
        /// <summary>T14 / D-12: MRP cộng tồn an toàn (InventoryPolicie.SafetyStock, không có thì Ingredient.SafetyStock) vào nhu cầu (PROPOSED – BA/PO CONFIRM).</summary>
        public bool MrpIncludeSafetyStock { get; set; } = true;
        /// <summary>T15: PO có tổng tiền ≤ ngưỡng này được tự duyệt khi gửi (0 = mọi PO phải duyệt) – PROPOSED – BA/PO CONFIRM (GAP-PUR-03).</summary>
        public decimal PurchaseOrderAutoApproveMaxAmount { get; set; }
        /// <summary>T15 / D-11: PO lớn hơn ngưỡng này cần GĐ duyệt. Pilot duyệt 1 cấp – chỉ ghi chú trong tiêu đề hộp duyệt (PROPOSED).</summary>
        public decimal PurchaseOrderDirectorApprovalAmount { get; set; } = 50_000_000m;
        /// <summary>T15: được nhận vượt số đặt tối đa bao nhiêu % mỗi dòng PO – PROPOSED (GAP-PUR-04).</summary>
        public decimal GoodsReceiptOverReceivePct { get; set; } = 10m;
        /// <summary>T15: nhiệt độ nhận hàng tối đa (°C) theo Ingredient.StorageType – PROPOSED theo spec 9.1 (CHILLED &gt; 5, FROZEN &gt; −18 là ngoài ngưỡng).</summary>
        public Dictionary<string, decimal> ReceivingTempMaxC { get; set; } = new(StringComparer.OrdinalIgnoreCase) { ["CHILLED"] = 5m, ["FROZEN"] = -18m };
        /// <summary>T15: hạn xử lý sự cố NCC (SupplierIssue.DueAt) = lúc ghi sổ + số ngày này – PROPOSED (spec 9.1: +2 ngày).</summary>
        public int SupplierIssueDueDays { get; set; } = 2;
        /// <summary>T17: ca sản xuất mặc định khi phát hành kế hoạch không chọn ca (Shift.Code) – Pilot chỉ ca trưa (D-35), PROPOSED – BA/PO CONFIRM (GAP-PRD-01).</summary>
        public string ProductionDefaultShiftCode { get; set; } = "TRUA";
        /// <summary>T17: số phút chia suất / đóng gói trước giờ xuất bếp – mẻ cuối của mỗi món phải nấu xong trước (giờ xuất bếp − số phút này). PROPOSED (GAP-PRD-07).</summary>
        public int ProductionPackingMinutes { get; set; } = 30;
        /// <summary>T17: số phút mỗi mẻ khi dây chuyền chưa có MinutesPerBatch (hoặc không chọn dây chuyền). PROPOSED (GAP-PRD-07).</summary>
        public int ProductionDefaultMinutesPerBatch { get; set; } = 60;
        /// <summary>T17: bắt buộc lưu mẫu (SAMPLE_RETAINED) trước khi mẻ đạt QC (BUSINESS_RULES §6 "mỗi mẻ phải lưu mẫu"). PROPOSED – BA/PO CONFIRM.</summary>
        public bool ProductionRequireSampleBeforeRelease { get; set; } = true;
    }
}
