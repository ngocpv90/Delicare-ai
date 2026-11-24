namespace AppDelicare.Model.Model
{
    /// <summary>UI-6.24 (D-105): "Bảng sản xuất ngày" – ma trận Món × Ca của kế hoạch 1 ngày (chỉ đọc), kèm nguyên liệu & giữ chỗ gộp cả ngày, chênh lệch & giá thành.</summary>
    public class ProductionDayBoardModel
    {
        public Guid PlanId { get; set; }
        public string? PlanNumber { get; set; }
        public DateTime ProductionDate { get; set; }
        public string? Status { get; set; }
        public string? StatusName { get; set; }
        public List<string> AllowedActions { get; set; } = [];
        /// <summary>Số ca còn chưa ra lệnh (nút "Ra lệnh n ca").</summary>
        public int PendingShiftCount { get; set; }
        public decimal TotalPortions { get; set; }
        public int BatchDone { get; set; }
        public int BatchTotal { get; set; }
        public int BatchQcOrProblem { get; set; }
        public int OrderClosed { get; set; }
        public int OrderTotal { get; set; }
        public bool AutoClose { get; set; }
        public List<ProductionDayBoardShiftModel> ListShift { get; set; } = [];
        public List<ProductionDayBoardGroupModel> ListGroup { get; set; } = [];
        public List<ProductionDayBoardMaterialModel> ListMaterial { get; set; } = [];
        public List<ProductionDayBoardVarianceModel> ListVariance { get; set; } = [];
        public List<ProductionDayBoardCostModel> ListCost { get; set; } = [];
    }

    public class ProductionDayBoardShiftModel
    {
        public Guid ShiftId { get; set; }
        public string? ShiftCode { get; set; }
        public string? ShiftName { get; set; }
        public TimeSpan? StartTime { get; set; }
        public bool Released { get; set; }
        public decimal Portions { get; set; }
    }

    public class ProductionDayBoardGroupModel
    {
        public Guid? CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public List<ProductionDayBoardRowModel> ListRow { get; set; } = [];
    }

    public class ProductionDayBoardRowModel
    {
        public Guid DishId { get; set; }
        public string? DishName { get; set; }
        public List<ProductionDayBoardCellModel> ListCell { get; set; } = [];
    }

    public class ProductionDayBoardCellModel
    {
        public Guid ShiftId { get; set; }
        public decimal Portions { get; set; }
        /// <summary>planned (kế hoạch chưa ra lệnh) · waiting (chờ nấu) · cooking (đang nấu) · qc (chờ QC) · passed (đạt / đã đóng gói) · problem (có mẻ loại / tạm giữ) · cancelled.</summary>
        public string State { get; set; } = "planned";
        public string StateName { get; set; } = string.Empty;
        public int BatchDone { get; set; }
        public int BatchTotal { get; set; }
        public string? LineName { get; set; }
        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }
        public decimal ShortQuantityLines { get; set; }
        public List<ProductionDayBoardOrderModel> ListOrder { get; set; } = [];
    }

    public class ProductionDayBoardOrderModel
    {
        public Guid Id { get; set; }
        public string? Number { get; set; }
        public string? Status { get; set; }
        public string? StatusName { get; set; }
        public decimal PlannedQty { get; set; }
        public List<string> AllowedActions { get; set; } = [];
    }

    public class ProductionDayBoardMaterialModel
    {
        public Guid IngredientId { get; set; }
        public string? IngredientName { get; set; }
        public string? UnitName { get; set; }
        public decimal Required { get; set; }
        public decimal Reserved { get; set; }
        public decimal Short { get; set; }
        public int OrderCount { get; set; }
    }

    public class ProductionDayBoardVarianceModel
    {
        public Guid ProductionOrderId { get; set; }
        public string? OrderNumber { get; set; }
        public string? DishName { get; set; }
        public string? IngredientName { get; set; }
        public decimal StandardQuantity { get; set; }
        public decimal ActualQuantity { get; set; }
        public decimal? VariancePercent { get; set; }
        public decimal? VarianceCost { get; set; }
        public bool IsOverThreshold { get; set; }
    }

    public class ProductionDayBoardCostModel
    {
        public Guid ProductionOrderId { get; set; }
        public string? OrderNumber { get; set; }
        public string? DishName { get; set; }
        public string? ShiftCode { get; set; }
        public string? Status { get; set; }
        public decimal AcceptedPortions { get; set; }
        public decimal TotalCost { get; set; }
        public decimal? CostPerPortion { get; set; }
    }
}
