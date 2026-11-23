namespace AppDelicare.Model.Model
{
    /// <summary>BE2.4 (spec 7.7 reservations, Catalog 126): 1 dòng giữ chỗ nguyên liệu của lệnh SX (số theo đơn vị tồn kho).</summary>
    public class MaterialReservationModel
    {
        public Guid Id { get; set; }
        public Guid ProductionOrderId { get; set; }
        public string? ProductionOrderNumber { get; set; }
        public Guid IngredientId { get; set; }
        public string? IngredientCode { get; set; }
        public string? IngredientName { get; set; }
        public Guid UnitId { get; set; }
        public string? UnitName { get; set; }
        /// <summary>Số cần lúc giữ chỗ (định lượng × phần kế hoạch của lệnh).</summary>
        public decimal RequiredQuantity { get; set; }
        /// <summary>Số còn đang giữ (giảm khi xuất cho lệnh, về 0 khi giải phóng).</summary>
        public decimal ReservedQuantity { get; set; }
        /// <summary>Số chưa giữ được lúc giữ chỗ (thiếu tồn) = cần − giữ ban đầu.</summary>
        public decimal ShortQuantity { get; set; }
        public string? Status { get; set; }
        public string? StatusName { get; set; }
    }

    /// <summary>BE2.4 (T17 công đoạn theo tổ): 1 công đoạn của mẻ.</summary>
    public class ProductionStageModel
    {
        public string StageCode { get; set; } = string.Empty;
        public string? StageName { get; set; }
        public int SortOrder { get; set; }
        public bool IsRequired { get; set; }
        public string Status { get; set; } = ProductionStageStatusText.NotStarted;
        public string? StatusName { get; set; }
        public Guid? TeamId { get; set; }
        public string? TeamCode { get; set; }
        public string? TeamName { get; set; }
        public Guid? KitchenWorkstationId { get; set; }
        public List<Guid> EmployeeIds { get; set; } = [];
        public List<string> EmployeeNames { get; set; } = [];
        public DateTime? StartedAt { get; set; }
        public Guid? StartedBy { get; set; }
        public DateTime? FinishedAt { get; set; }
        public Guid? FinishedBy { get; set; }
        /// <summary>Số phút làm (xong − bắt đầu), null khi chưa xong.</summary>
        public decimal? Minutes { get; set; }
    }

    /// <summary>Hằng trạng thái dùng trong model (tránh tham chiếu Helper ở khởi tạo mặc định).</summary>
    public static class ProductionStageStatusText
    {
        public const string NotStarted = "NotStarted";
    }

    /// <summary>BE2.4: 1 mẻ trên bảng tiến độ bếp theo tổ.</summary>
    public class ProductionStageBoardBatchModel
    {
        public ProductionBatchBriefModel Batch { get; set; } = null!;
        public List<ProductionStageModel> ListStage { get; set; } = [];
        /// <summary>Công đoạn hiện tại (đang làm, không có thì công đoạn kế tiếp chưa làm); null = đã xong mọi công đoạn.</summary>
        public string? CurrentStageCode { get; set; }
    }

    /// <summary>BE2.4: tổng hợp theo tổ – số mẻ đang làm / đã xong / chờ ở công đoạn của tổ.</summary>
    public class ProductionTeamProgressModel
    {
        public Guid? TeamId { get; set; }
        public string? TeamCode { get; set; }
        public string? TeamName { get; set; }
        public string StageCode { get; set; } = string.Empty;
        public string? StageName { get; set; }
        public int Waiting { get; set; }
        public int InProgress { get; set; }
        public int Done { get; set; }
        public decimal InProgressPortions { get; set; }
        public decimal DonePortions { get; set; }
    }

    /// <summary>BE2.4: tiến độ bếp theo tổ của 1 ngày sản xuất.</summary>
    public class ProductionStageBoardModel
    {
        public DateTime ProductionDate { get; set; }
        public List<ProductionTeamProgressModel> ListTeam { get; set; } = [];
        public List<ProductionStageBoardBatchModel> ListBatch { get; set; } = [];
        public List<string> ListWarning { get; set; } = [];
    }

    /// <summary>BE2.4 (spec 7.9, Catalog 135): 1 dòng chênh lệch tiêu hao (theo đơn vị xuất kho).</summary>
    public class ConsumptionVarianceModel
    {
        public Guid Id { get; set; }
        public Guid ProductionOrderId { get; set; }
        public string? ProductionOrderNumber { get; set; }
        public DateTime? ProductionDate { get; set; }
        public Guid? DishId { get; set; }
        public string? DishName { get; set; }
        public Guid IngredientId { get; set; }
        public string? IngredientCode { get; set; }
        public string? IngredientName { get; set; }
        public string? UnitName { get; set; }
        public decimal StandardQuantity { get; set; }
        public decimal ActualQuantity { get; set; }
        public decimal VarianceQuantity { get; set; }
        public decimal? VariancePercent { get; set; }
        public decimal? VarianceCost { get; set; }
        public bool IsOverThreshold { get; set; }
        public string? Reason { get; set; }
    }

    /// <summary>BE2.4 (spec 8.5): giá thành thực tế (1 mẻ hoặc cộng các mẻ của lệnh).</summary>
    public class ActualCostModel
    {
        public Guid? ProductionOrderId { get; set; }
        public string? ProductionOrderNumber { get; set; }
        public Guid? ProductionBatchId { get; set; }
        public string? BatchNumber { get; set; }
        public DateTime? ProductionDate { get; set; }
        public Guid? DishId { get; set; }
        public string? DishName { get; set; }
        public string? ShiftCode { get; set; }
        public string? Status { get; set; }
        public decimal MaterialCost { get; set; }
        public decimal LaborCost { get; set; }
        public decimal OverheadCost { get; set; }
        /// <summary>Giá trị hao hụt (đã nằm trong chi phí NVL – chỉ để phân tích, không cộng thêm vào tổng – PROPOSED D-53).</summary>
        public decimal WasteCost { get; set; }
        public decimal OtherCost { get; set; }
        public decimal TotalCost { get; set; }
        /// <summary>Số phần đạt (đã đóng gói / đạt) – mẫu số của giá thành / phần.</summary>
        public decimal AcceptedPortions { get; set; }
        public decimal? CostPerPortion { get; set; }
        /// <summary>Giá thành chuẩn / phần (StandardCost đang hiệu lực của món).</summary>
        public decimal? StandardCostPerPortion { get; set; }
        public DateTime? CalculatedAt { get; set; }
        public List<string> ListWarning { get; set; } = [];
    }

    /// <summary>BE2.4 (Catalog 152): danh sách giá thành thực tế + cơ cấu chi phí.</summary>
    public class ActualCostListModel
    {
        public List<ActualCostModel> ListData { get; set; } = [];
        public ActualCostModel Breakdown { get; set; } = new();
    }

    /// <summary>BE2.4 (spec 8.2 close, Catalog 144): kết quả đóng lệnh SX.</summary>
    public class ProductionOrderCloseResultModel
    {
        public ProductionOrderDetailModel Detail { get; set; } = null!;
        public ActualCostModel ActualCost { get; set; } = null!;
        public List<ConsumptionVarianceModel> ListVariance { get; set; } = [];
    }

    /// <summary>BE2.4 (spec 8.5 meal-costs, Catalog 154): giá vốn 1 dòng đơn (cộng các món).</summary>
    public class MealCostLineModel
    {
        public Guid OrderLineId { get; set; }
        public string? MealType { get; set; }
        public string? ShiftCode { get; set; }
        public decimal OrderedMeals { get; set; }
        public decimal AcceptedMeals { get; set; }
        public decimal TotalActualCost { get; set; }
        public decimal? CostPerMeal { get; set; }
        public decimal Revenue { get; set; }
        public decimal GrossMargin { get; set; }
        public decimal? GrossMarginPercent { get; set; }
        public List<MealCostDishModel> ListDish { get; set; } = [];
    }

    public class MealCostDishModel
    {
        public Guid Id { get; set; }
        public Guid DishId { get; set; }
        public string? DishName { get; set; }
        public decimal Portions { get; set; }
        public decimal TotalActualCost { get; set; }
        public decimal? CostPerPortion { get; set; }
        /// <summary>Nguồn giá: ACTUAL (giá thành thực tế lệnh SX) / STANDARD (giá thành chuẩn – lệnh chưa tính giá) / NONE.</summary>
        public string CostSource { get; set; } = "NONE";
        public decimal? Revenue { get; set; }
        public decimal? GrossMargin { get; set; }
    }

    public class MealCostResultModel
    {
        public Guid OrderId { get; set; }
        public string? OrderNumber { get; set; }
        public DateTime MealDate { get; set; }
        public string? OrderStatus { get; set; }
        /// <summary>D-78 (3): đơn bù do lỗi Delicare (giá 0, ghi chú [DON_BU:]) – giá vốn là <b>Chi phí lỗi</b>, báo cáo tách riêng, không tính vào giá vốn / lãi gộp suất bán.</summary>
        public bool IsFaultCost { get; set; }
        public string CostCategoryName => IsFaultCost ? "Chi phí lỗi" : "Suất bán";
        public decimal OrderedMeals { get; set; }
        public decimal AcceptedMeals { get; set; }
        /// <summary>Nguồn số suất nhận: RECONCILIATION (OrderReconciliation) / POD / ORDERED.</summary>
        public string AcceptedSource { get; set; } = "ORDERED";
        public decimal TotalActualCost { get; set; }
        public decimal? CostPerMeal { get; set; }
        public decimal Revenue { get; set; }
        public decimal GrossMargin { get; set; }
        public decimal? GrossMarginPercent { get; set; }
        public List<MealCostLineModel> ListLine { get; set; } = [];
        public List<string> ListWarning { get; set; } = [];
    }

    /// <summary>BE2.4 (spec 7.5 LotExpiryJob → MaterialLot/ProcessExpired): 1 lô hết hạn đã / sẽ xử lý.</summary>
    public class LotExpiryItemModel
    {
        public Guid MaterialLotId { get; set; }
        public string? LotNumber { get; set; }
        public Guid IngredientId { get; set; }
        public string? IngredientName { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string? LotStatus { get; set; }
        public decimal WasteQuantity { get; set; }
        public string? UnitName { get; set; }
        public decimal? WasteValue { get; set; }
        public int TransactionCount { get; set; }
        /// <summary>Lý do bỏ qua (vd. lô đang tạm giữ) – null = đã / sẽ xử lý.</summary>
        public string? SkippedReason { get; set; }
    }

    public class LotExpiryResultModel
    {
        public bool Preview { get; set; }
        public DateTime Today { get; set; }
        public string? TransactionNumber { get; set; }
        public int ExpiredLotCount { get; set; }
        public decimal TotalWasteValue { get; set; }
        public List<LotExpiryItemModel> ListLot { get; set; } = [];
        /// <summary>Lô còn ≤ ngày cảnh báo (ExpiryWarningDays / ExpiringLotDays) – chỉ cảnh báo.</summary>
        public List<LotExpiryItemModel> ListExpiring { get; set; } = [];
        public List<string> ListWarning { get; set; } = [];
    }
}
