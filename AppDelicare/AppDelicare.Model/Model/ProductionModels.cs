using AppDelicare.Model.Entity;

namespace AppDelicare.Model.Model
{
    /// <summary>
    /// T17 (spec 8.1, Pilot P7): chi tiết kế hoạch sản xuất – dòng món × phần, lệnh SX + mẻ (lịch ngược từ giờ xuất bếp), thao tác được phép.
    /// </summary>
    /// <summary>BIZ-2.4: kết quả tự lập + tự phát hành kế hoạch sau chốt đơn. Status: RELEASED / HELD (Nháp, chờ COO) / SKIPPED (đã duyệt trước) / NONE (không có đơn chốt).</summary>
    /// <summary>BIZ-2.5: nhóm phiếu xuất Nháp theo ca (thẻ trong Note).</summary>
    public class MaterialIssueShiftGroupModel
    {
        public string Tag { get; set; } = string.Empty;
        public string? PlanNumber { get; set; }
        public string? ShiftCode { get; set; }
        public DateTime? IssueDate { get; set; }
        public int DraftCount { get; set; }
        public int IssuedCount { get; set; }
        public List<MaterialIssueShiftItemModel> ListIssue { get; set; } = [];
    }

    public class MaterialIssueShiftItemModel
    {
        public Guid Id { get; set; }
        public string? IssueNumber { get; set; }
        public string? Status { get; set; }
        public string? ProductionOrderNumber { get; set; }
        public int LineCount { get; set; }
    }

    public class ProductionAutoPlanModel
    {
        public DateTime ProductionDate { get; set; }
        public string Status { get; set; } = "NONE";
        public Guid? PlanId { get; set; }
        public string? PlanNumber { get; set; }
        public int ProductionOrderCount { get; set; }
        public List<string> ListReason { get; set; } = [];
        public List<string> ListWarning { get; set; } = [];
    }

    public class ProductionPlanDetailModel
    {
        public ProductionPlan ProductionPlan { get; set; } = null!;
        public string? StatusName { get; set; }
        public decimal TotalPortions { get; set; }
        public List<ProductionPlanLineDetailModel> ListLine { get; set; } = [];
        public List<ProductionOrderBriefModel> ListOrder { get; set; } = [];
        public List<ProductionBatchBriefModel> ListBatch { get; set; } = [];
        /// <summary>Giờ xuất bếp muộn nhất trong các ca đã phát hành (mẻ cuối + đóng gói); null = chưa phát hành. Theo từng ca: <see cref="ListShift"/>.</summary>
        public DateTime? DispatchAt { get; set; }
        /// <summary>T17.1: các ca có nhu cầu trong ngày (sắp theo giờ bắt đầu) – kế hoạch Đã phát hành khi mọi ca đã phát hành.</summary>
        public List<ProductionPlanShiftModel> ListShift { get; set; } = [];
        /// <summary>BE2.4 (spec 8.1 release reservations[]): giữ chỗ nguyên liệu của các lệnh SX của kế hoạch.</summary>
        public List<MaterialReservationModel> ListReservation { get; set; } = [];
        public List<string> AllowedActions { get; set; } = [];
        public List<string> ListWarning { get; set; } = [];
    }

    public class ProductionPlanLineDetailModel
    {
        public Guid Id { get; set; }
        public Guid DishId { get; set; }
        public string? DishCode { get; set; }
        public string? DishName { get; set; }
        public decimal PlannedQuantity { get; set; }
        public Guid? ShiftId { get; set; }
        public string? Status { get; set; }
        /// <summary>T17.1: số phần của món theo ca (chia theo suất của ca trong đơn Đã chốt / Đang SX).</summary>
        public List<ProductionPlanLineShiftModel> ListShift { get; set; } = [];
    }

    public class ProductionPlanLineShiftModel
    {
        public Guid ShiftId { get; set; }
        public string? ShiftCode { get; set; }
        public decimal Quantity { get; set; }
    }

    /// <summary>
    /// T17.1 (anh chốt 2026-11-16 – 4 ca / ngày): phần của 1 ca trong kế hoạch ngày – số phần, đã phát hành chưa, giờ xuất bếp, số lệnh / mẻ.
    /// Ca đã phát hành = có lệnh SX của kế hoạch gắn ca đó (không có cột trạng thái theo ca – GAP-PRD-01).
    /// </summary>
    public class ProductionPlanShiftModel
    {
        public Guid ShiftId { get; set; }
        public string? ShiftCode { get; set; }
        public string? ShiftName { get; set; }
        public TimeSpan? StartTime { get; set; }
        public decimal Portions { get; set; }
        public bool Released { get; set; }
        public DateTime? DispatchAt { get; set; }
        public int OrderCount { get; set; }
        public int BatchCount { get; set; }
    }

    public class ProductionOrderBriefModel
    {
        public Guid Id { get; set; }
        public string? ProductionOrderNumber { get; set; }
        public Guid DishId { get; set; }
        public string? DishName { get; set; }
        public Guid RecipeId { get; set; }
        public string? RecipeVersion { get; set; }
        public Guid? ShiftId { get; set; }
        public string? ShiftCode { get; set; }
        public Guid? ProductionLineId { get; set; }
        public decimal PlannedQuantity { get; set; }
        public decimal? ActualQuantity { get; set; }
        public DateTime PlannedStartDate { get; set; }
        public DateTime? PlannedEndDate { get; set; }
        public string? Status { get; set; }
        public string? StatusName { get; set; }
        public int BatchCount { get; set; }
    }

    /// <summary>T17: 1 mẻ trên bảng điều độ (spec 8.3 batch-board) – giờ kế hoạch (StartDate / EndDate), giờ thực tế (StartTime / EndTime), ATTP.</summary>
    public class ProductionBatchBriefModel
    {
        public Guid Id { get; set; }
        public string? BatchNumber { get; set; }
        public Guid ProductionOrderId { get; set; }
        public string? ProductionOrderNumber { get; set; }
        public Guid DishId { get; set; }
        public string? DishName { get; set; }
        public Guid? ShiftId { get; set; }
        public string? ShiftCode { get; set; }
        public Guid? ProductionLineId { get; set; }
        public string? ProductionLineName { get; set; }
        public decimal PlannedQuantity { get; set; }
        public decimal? ActualQuantity { get; set; }
        public decimal? AcceptedQuantity { get; set; }
        public DateTime PlannedStart { get; set; }
        public DateTime? PlannedEnd { get; set; }
        public DateTime? ActualStart { get; set; }
        public DateTime? ActualEnd { get; set; }
        public string? Status { get; set; }
        public string? StatusName { get; set; }
        public string? QcStatus { get; set; }
        /// <summary>Nhiệt độ lõi đo gần nhất (°C) và đạt ngưỡng hay không.</summary>
        public decimal? LastCoreTemperatureC { get; set; }
        public bool? CoreTemperatureOk { get; set; }
        public bool SampleRetained { get; set; }
        /// <summary>Đã có phiếu xuất NL ghi sổ cho mẻ.</summary>
        public bool MaterialIssued { get; set; }
    }

    /// <summary>T17 (spec 8.3): bảng mẻ theo ngày + đếm theo trạng thái.</summary>
    public class ProductionBoardModel
    {
        public DateTime ProductionDate { get; set; }
        public List<ProductionBatchBriefModel> ListBatch { get; set; } = [];
        public Dictionary<string, int> StatusCounts { get; set; } = [];
        public decimal PlannedPortions { get; set; }
        public decimal PassedPortions { get; set; }
    }

    /// <summary>T17 (spec 8.3 batches/{id}): chi tiết mẻ – lệnh SX, nguyên liệu cần theo định lượng, phiếu xuất, nhiệt độ lõi, lưu mẫu, sản lượng, hao hụt.</summary>
    public class ProductionBatchDetailModel
    {
        public ProductionBatchBriefModel Batch { get; set; } = null!;
        public ProductionOrderBriefModel Order { get; set; } = null!;
        public decimal CoreTempMinC { get; set; }
        public List<ProductionMaterialModel> ListMaterial { get; set; } = [];
        public List<ProductionIssueBriefModel> ListIssue { get; set; } = [];
        public List<TemperatureLog> ListTemperature { get; set; } = [];
        public List<FoodSafetyChecklistResult> ListSample { get; set; } = [];
        public ProductionOutput? Output { get; set; }
        public List<MealWasteRecord> ListWaste { get; set; } = [];
        /// <summary>BE2.4: công đoạn theo tổ (Sơ chế → Nấu → Chia suất) của mẻ – trạng thái, giờ, tổ, người.</summary>
        public List<ProductionStageModel> ListStage { get; set; } = [];
        public List<string> AllowedActions { get; set; } = [];
        /// <summary>Lý do chưa được đạt QC / đóng gói (nhiệt độ lõi, lưu mẫu, sản lượng).</summary>
        public List<string> ListBlocker { get; set; } = [];
        public List<string> ListWarning { get; set; } = [];
    }

    /// <summary>T17: nguyên liệu của mẻ = định lượng công thức (ảnh chụp trong lệnh SX) × phần kế hoạch ÷ suất chuẩn × (1 + hao hụt %).</summary>
    public class ProductionMaterialModel
    {
        public Guid IngredientId { get; set; }
        public string? IngredientCode { get; set; }
        public string? IngredientName { get; set; }
        /// <summary>Định lượng / suất theo đơn vị công thức.</summary>
        public decimal QuantityPerServing { get; set; }
        public Guid RecipeUnitId { get; set; }
        public string? RecipeUnitName { get; set; }
        public decimal WastePercent { get; set; }
        /// <summary>Số cần theo đơn vị công thức.</summary>
        public decimal RequiredQuantity { get; set; }
        /// <summary>Số đã xuất (phiếu xuất đã ghi sổ của mẻ) theo đơn vị tồn kho.</summary>
        public decimal IssuedQuantity { get; set; }
        public string? IssuedUnitName { get; set; }
    }

    /// <summary>T17 (spec 8.2 orders/{id}): chi tiết lệnh SX.</summary>
    public class ProductionOrderDetailModel
    {
        public ProductionOrder ProductionOrder { get; set; } = null!;
        public ProductionOrderBriefModel Order { get; set; } = null!;
        /// <summary>Ảnh chụp định lượng lúc phát hành (RecipeSnapshotJson) – không đổi khi công thức sửa sau.</summary>
        public List<ProductionRecipeLineModel> ListRecipeLine { get; set; } = [];
        public List<ProductionBatchBriefModel> ListBatch { get; set; } = [];
        public List<ProductionIssueBriefModel> ListIssue { get; set; } = [];
        /// <summary>BE2.4 (spec 8.2 orders/{id} reservations[]): giữ chỗ nguyên liệu của lệnh.</summary>
        public List<MaterialReservationModel> ListReservation { get; set; } = [];
        /// <summary>BE2.4: chênh lệch tiêu hao (sinh khi đóng lệnh).</summary>
        public List<ConsumptionVarianceModel> ListVariance { get; set; } = [];
        /// <summary>BE2.4: giá thành thực tế của lệnh (cộng các mẻ), null = chưa tính.</summary>
        public ActualCostModel? ActualCost { get; set; }
        public List<string> AllowedActions { get; set; } = [];
        public List<string> ListWarning { get; set; } = [];
    }

    public class ProductionRecipeLineModel
    {
        public Guid IngredientId { get; set; }
        public string? IngredientName { get; set; }
        public decimal Quantity { get; set; }
        public Guid UnitId { get; set; }
        public string? UnitName { get; set; }
        public decimal? WastePercent { get; set; }
    }

    public class ProductionIssueBriefModel
    {
        public Guid Id { get; set; }
        public string? IssueNumber { get; set; }
        public DateTime IssueDate { get; set; }
        public string? Status { get; set; }
        public string? StatusName { get; set; }
        public Guid WarehouseId { get; set; }
    }
}
