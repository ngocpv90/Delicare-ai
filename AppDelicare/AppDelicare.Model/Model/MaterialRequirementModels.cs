namespace AppDelicare.Model.Model
{
    /// <summary>
    /// T14 (spec 7.1, Pilot P5): kết quả tính nhu cầu NL của 1 ngày sản xuất – kế hoạch neo (ProductionPlan Draft, GAP-MRP-01),
    /// món × suất (ProductionPlanLine), dòng nhu cầu theo nguyên liệu (MaterialRequirement) và đề nghị mua nháp (PurchaseRequest).
    /// </summary>
    public class MaterialRequirementResultModel
    {
        public Guid ProductionPlanId { get; set; }
        public string? PlanNumber { get; set; }
        public DateTime ProductionDate { get; set; }
        public Guid? OrganizationUnitId { get; set; }
        public string? OrganizationUnitName { get; set; }
        public string? PlanStatus { get; set; }
        public string? PlanStatusName { get; set; }
        /// <summary>Kho đã dùng để trừ tồn (trống = mọi kho / mọi kho của chi nhánh).</summary>
        public Guid? WarehouseId { get; set; }
        public string? WarehouseName { get; set; }
        public DateTime? CalculatedAt { get; set; }
        public Guid? CalculatedBy { get; set; }
        /// <summary>Số đơn Đã chốt (Locked) của ngày được tính.</summary>
        public int OrderCount { get; set; }
        /// <summary>Tổng suất của các đơn Đã chốt.</summary>
        public decimal TotalMeals { get; set; }
        public int DishCount { get; set; }
        public int ShortageCount { get; set; }
        /// <summary>Σ tiền ước tính theo giá NCC gợi ý của các dòng cần mua.</summary>
        public decimal EstimatedAmount { get; set; }
        public List<MaterialRequirementDishModel> ListDish { get; set; } = [];
        public List<MaterialRequirementItemModel> ListItem { get; set; } = [];
        public MaterialRequirementPurchaseRequestModel? PurchaseRequest { get; set; }
        /// <summary>Cảnh báo không chặn: món chưa có công thức khóa, thiếu quy đổi đơn vị, đơn chưa chốt, NCC cần nhiều ngày giao…</summary>
        public List<string> ListWarning { get; set; } = [];
    }

    /// <summary>T14: món × số suất phải nấu (từ đơn Locked × thực đơn) và công thức đã dùng.</summary>
    public class MaterialRequirementDishModel
    {
        public Guid DishId { get; set; }
        public string? DishCode { get; set; }
        public string? DishName { get; set; }
        public decimal Portions { get; set; }
        public Guid? RecipeId { get; set; }
        public string? RecipeVersion { get; set; }
        /// <summary><c>Snapshot</c> = SnapshotJson của công thức đã khóa; <c>Lines</c> = dòng định lượng hiện tại; trống = không có công thức.</summary>
        public string? RecipeSource { get; set; }
        public string? Warning { get; set; }
    }

    /// <summary>T14: 1 dòng nhu cầu theo nguyên liệu (đơn vị cơ sở của nguyên liệu).</summary>
    public class MaterialRequirementItemModel
    {
        public int STT { get; set; }
        public Guid Id { get; set; }
        public Guid IngredientId { get; set; }
        public string? IngredientCode { get; set; }
        public string? IngredientName { get; set; }
        public Guid UnitId { get; set; }
        public string? UnitName { get; set; }
        /// <summary>Nhu cầu = Σ định lượng × suất ÷ suất chuẩn × (1 + hao hụt %), quy đổi về đơn vị cơ sở.</summary>
        public decimal RequiredQuantity { get; set; }
        /// <summary>Tồn xuất được (lô không bị chặn tại ngày sản xuất, đã trừ giữ chỗ).</summary>
        public decimal AvailableQuantity { get; set; }
        /// <summary>Tồn nằm ở lô bị chặn (hết hạn trước ngày SX / QC không đạt / tạm giữ) – không tính.</summary>
        public decimal BlockedQuantity { get; set; }
        public decimal SafetyStock { get; set; }
        public decimal ExpectedIncomingQuantity { get; set; }
        /// <summary>Thiếu = max(0, nhu cầu + tồn an toàn − khả dụng − hàng sắp về).</summary>
        public decimal ShortageQuantity { get; set; }
        /// <summary>Số đề nghị mua (thiếu, làm tròn lên MOQ của NCC gợi ý).</summary>
        public decimal PurchaseQuantity { get; set; }
        /// <summary>min(100, khả dụng ÷ nhu cầu × 100).</summary>
        public decimal CoveragePercent { get; set; }
        public string? Status { get; set; }
        public string? StatusName { get; set; }
        public Guid? PurchaseRequestLineId { get; set; }
        public MaterialRequirementSupplierModel? Supplier { get; set; }
        /// <summary>Có dòng định lượng không quy đổi được về đơn vị cơ sở (nhu cầu có thể thiếu).</summary>
        public bool Incomplete { get; set; }
        public string? Warning { get; set; }
    }

    /// <summary>T14: NCC gợi ý (SupplierMaterial đang hiệu lực: ưu tiên → giá thấp → giao nhanh).</summary>
    public class MaterialRequirementSupplierModel
    {
        public Guid SupplierMaterialId { get; set; }
        public Guid SupplierId { get; set; }
        public string? SupplierCode { get; set; }
        public string? SupplierName { get; set; }
        public bool IsPreferred { get; set; }
        /// <summary>Đơn giá theo đơn vị mua (PurchaseUomId, trống = đơn vị cơ sở).</summary>
        public decimal? UnitPrice { get; set; }
        public Guid? PurchaseUnitId { get; set; }
        public string? PurchaseUnitName { get; set; }
        /// <summary>MOQ theo đơn vị mua.</summary>
        public decimal? MinimumOrderQuantity { get; set; }
        public int? LeadTimeDays { get; set; }
        /// <summary>Số đề nghị mua quy đổi sang đơn vị mua.</summary>
        public decimal? PurchaseQuantityInPurchaseUnit { get; set; }
        public decimal? EstimatedAmount { get; set; }
    }

    /// <summary>T14: đề nghị mua nháp sinh từ kết quả tính nhu cầu.</summary>
    public class MaterialRequirementPurchaseRequestModel
    {
        public Guid Id { get; set; }
        public string? RequestNumber { get; set; }
        public DateTime RequestDate { get; set; }
        public DateTime? RequiredDate { get; set; }
        public string? Status { get; set; }
        public string? StatusName { get; set; }
        public int LineCount { get; set; }
        public decimal EstimatedAmount { get; set; }
    }
}
