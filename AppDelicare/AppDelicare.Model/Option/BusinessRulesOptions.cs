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
        /// <summary>
        /// T14.1 (anh chốt 2026-11-16, GAP-MRP-03): thực đơn có ≥ 2 lựa chọn cho cùng loại suất → số suất chia đều cho các lựa chọn rồi cộng thêm
        /// % dự phòng này cho mỗi lựa chọn (0 = tắt). Thực đơn 1 lựa chọn không cộng.
        /// </summary>
        public decimal MrpOptionBufferPct { get; set; } = 15m;
        /// <summary>T15: PO có tổng tiền ≤ ngưỡng này được tự duyệt khi gửi (0 = mọi PO phải duyệt) – PROPOSED – BA/PO CONFIRM (GAP-PUR-03).</summary>
        public decimal PurchaseOrderAutoApproveMaxAmount { get; set; }
        /// <summary>
        /// T15.1 / D-11 (anh chốt 2026-11-16): PO có tổng tiền ≥ ngưỡng này phải do Giám đốc duyệt – quyền Duyệt trên mục menu
        /// <c>#/wf-inbox?type=scm-po-director</c>; dưới ngưỡng duyệt trên <c>#/wf-inbox?type=scm-po</c>. Vẫn duyệt 1 cấp.
        /// </summary>
        public decimal PurchaseOrderDirectorApprovalAmount { get; set; } = 50_000_000m;
        /// <summary>T15 (anh chốt 2026-11-16): được nhận vượt số đặt tối đa bao nhiêu % mỗi dòng PO (GAP-PUR-04).</summary>
        public decimal GoodsReceiptOverReceivePct { get; set; } = 10m;
        /// <summary>
        /// T15.1 (anh chốt 2026-11-16, thay <c>ReceivingTempMaxC</c> của T15): nhiệt độ nhận hàng theo Ingredient.StorageType, 3 vùng –
        /// ≤ AcceptMax: nhận; ≤ ConditionalMax: nhận có điều kiện (bắt buộc lý do, lô "ưu tiên dùng trước", sự cố NCC; FROZEN còn phải xác nhận
        /// "không có dấu hiệu rã đông"); &gt; ConditionalMax: loại (chỉ KHONG_DAT). Kiểu bảo quản không có trong bảng (DRY / AMBIENT) → không kiểm.
        /// </summary>
        public Dictionary<string, ReceivingTempRule> ReceivingTemp { get; set; } = new(StringComparer.OrdinalIgnoreCase)
        {
            ["CHILLED"] = new ReceivingTempRule { AcceptMax = 5m, ConditionalMax = 8m },
            ["FROZEN"] = new ReceivingTempRule { AcceptMax = -18m, ConditionalMax = -15m, RequireNoThawConfirmation = true },
        };
        /// <summary>T15: hạn xử lý sự cố NCC (SupplierIssue.DueAt) = lúc ghi sổ + số ngày này – PROPOSED (spec 9.1: +2 ngày).</summary>
        public int SupplierIssueDueDays { get; set; } = 2;
        /// <summary>T15.1 (anh chốt 2026-11-16, phương án A – không lưu DB, GAP-PUR-09): % VAT in trên Đơn đặt hàng NCC và trả trong PurchaseOrder/GetDetail.</summary>
        public PurchaseVatOptions PurchaseVat { get; set; } = new();
    }

    /// <summary>T15.1: ngưỡng nhiệt độ nhận hàng của 1 kiểu bảo quản (°C).</summary>
    public sealed class ReceivingTempRule
    {
        /// <summary>Nhiệt độ ≤ ngưỡng này → nhận bình thường.</summary>
        public decimal AcceptMax { get; set; }
        /// <summary>AcceptMax &lt; nhiệt độ ≤ ngưỡng này → nhận có điều kiện; lớn hơn → loại (chỉ KHONG_DAT).</summary>
        public decimal ConditionalMax { get; set; }
        /// <summary>Nhận có điều kiện phải kèm xác nhận "không có dấu hiệu rã đông" (FROZEN).</summary>
        public bool RequireNoThawConfirmation { get; set; }
    }

    /// <summary>
    /// T15.1: VAT mua hàng – % mặc định và % theo mã nhóm nguyên liệu (<c>IngredientCategorie.Code</c>; không khớp thì xét nhóm cha).
    /// Chỉ tính khi đọc / in, không lưu (GAP-PUR-09).
    /// </summary>
    public sealed class PurchaseVatOptions
    {
        public decimal DefaultPct { get; set; } = 8m;
        public Dictionary<string, decimal> ByIngredientCategoryCode { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
