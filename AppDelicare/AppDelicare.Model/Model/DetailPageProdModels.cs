using AppDelicare.Model.Entity;

namespace AppDelicare.Model.Model
{
    /// <summary>
    /// UI-6 (D-80) nhóm C – trang chi tiết toàn màn Sản xuất / Thực đơn / Giao hàng (…/GetOverview). Gộp từ dữ liệu có sẵn, không bảng / cột mới.
    /// Quy ước như <see cref="StockCountOverviewModel"/>: <c>Detail</c> = model chi tiết đang có (GetDetail – dùng lại, không viết lại luật) + phần bổ sung cho
    /// phần đầu / hàng chỉ số / tab Tổng quan.
    /// </summary>
    public class ProductionBatchOverviewModel
    {
        public ProductionBatchDetailModel Detail { get; set; } = new();
        public Guid? ProductionPlanId { get; set; }
        public string? PlanNumber { get; set; }
        public DateTime? ProductionDate { get; set; }
        public string? ShiftName { get; set; }
        public string? RecipeVersion { get; set; }
        public string? ResponsibleName { get; set; }
        public int TemperatureCount { get; set; }
        public int TemperatureFailCount { get; set; }
        public int IssueCount { get; set; }
        public int StageCount { get; set; }
        public int StageDoneCount { get; set; }
        public decimal WasteMeals { get; set; }
    }

    public class ProductionOrderOverviewModel
    {
        public ProductionOrderDetailModel Detail { get; set; } = new();
        public string? PlanNumber { get; set; }
        public DateTime? ProductionDate { get; set; }
        public string? ShiftName { get; set; }
        public string? ProductionLineName { get; set; }
        public string? ResponsibleName { get; set; }
        public int BatchCount { get; set; }
        /// <summary>Mẻ Đạt / Đã đóng gói.</summary>
        public int BatchDoneCount { get; set; }
        public int BatchFailedCount { get; set; }
        public decimal AcceptedPortions { get; set; }
        /// <summary>Dòng giữ chỗ còn thiếu nguyên liệu (ShortQuantity &gt; 0).</summary>
        public int ShortReservationCount { get; set; }
        public int VarianceOverCount { get; set; }
    }

    public class ProductionPlanOverviewModel
    {
        public ProductionPlanDetailModel Detail { get; set; } = new();
        /// <summary>Kế hoạch giữ Nháp chờ COO (D-81 g) – Id yêu cầu duyệt ảo = Id kế hoạch (ApprovalRequest/GetDetail nhận Id này); null = không chờ duyệt.</summary>
        public Guid? ApprovalRequestId { get; set; }
        /// <summary>Lý do hệ thống không tự phát hành (Note JSON AutoRun – BIZ-2.4), null = không giữ.</summary>
        public string? HeldReason { get; set; }
        public string? OrganizationUnitName { get; set; }
        public string? ShiftName { get; set; }
        public int DishCount { get; set; }
        public int OrderCount { get; set; }
        public int BatchCount { get; set; }
        public int BatchDoneCount { get; set; }
        public int ShortReservationCount { get; set; }
    }

    /// <summary>Hồ sơ món ăn: công thức hiệu lực + giá vốn NVL / suất, các phiên bản, menu đang dùng, lịch sử nấu (mẻ).</summary>
    public class DishOverviewModel
    {
        public Dish Dish { get; set; } = null!;
        public string? CategoryName { get; set; }
        public string? UnitName { get; set; }
        public Recipe? ActiveRecipe { get; set; }
        /// <summary>Giá vốn NVL của công thức hiệu lực cho 1 suất (RecipeService.CalculateCostAsync); null = chưa có công thức hiệu lực.</summary>
        public RecipeCostModel? Cost { get; set; }
        public int IngredientCount { get; set; }
        public List<DishRecipeBriefModel> ListRecipe { get; set; } = [];
        public int MenuCount { get; set; }
        public List<DishMenuBriefModel> ListMenu { get; set; } = [];
        public int BatchCount { get; set; }
        public decimal PortionsAccepted { get; set; }
        /// <summary>% mẻ Đạt (Đạt + Đã đóng gói) trên mẻ đã kết luận (+ Loại); null = chưa có mẻ kết luận.</summary>
        public decimal? QcPassPercent { get; set; }
        /// <summary>50 mẻ gần nhất.</summary>
        public List<DishBatchBriefModel> ListBatch { get; set; } = [];
    }

    public class DishRecipeBriefModel
    {
        public Guid Id { get; set; }
        public string? VersionCode { get; set; }
        public int VersionNo { get; set; }
        public string? Status { get; set; }
        public bool IsActive { get; set; }
        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public int LineCount { get; set; }
    }

    public class DishMenuBriefModel
    {
        public Guid Id { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? Status { get; set; }
        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        /// <summary>Số dòng thực đơn có món (ngày × bữa).</summary>
        public int LineCount { get; set; }
        public List<string> ListMealType { get; set; } = [];
    }

    public class DishBatchBriefModel
    {
        public Guid Id { get; set; }
        public string? BatchNumber { get; set; }
        public string? ProductionOrderNumber { get; set; }
        public DateTime StartDate { get; set; }
        public decimal? PlannedQuantity { get; set; }
        public decimal? ActualQuantity { get; set; }
        public decimal? AcceptedQuantity { get; set; }
        public string? Status { get; set; }
        public string? QcStatus { get; set; }
    }

    public class DeliveryRouteOverviewModel
    {
        public DeliveryRouteDetailModel Detail { get; set; } = new();
        /// <summary>Ca suy từ các đơn trên chuyến (DeliveryRoute không có ShiftId).</summary>
        public List<string> ShiftNames { get; set; } = [];
        public int StopCount { get; set; }
        public int DeliveredStopCount { get; set; }
        public int FailedStopCount { get; set; }
        public decimal PlannedMeals { get; set; }
        public decimal DeliveredMeals { get; set; }
        public int PodCount { get; set; }
        public int ComplaintCount { get; set; }
        public int OpenComplaintCount { get; set; }
        public List<DeliveryRouteComplaintModel> ListComplaint { get; set; } = [];
    }

    public class DeliveryRouteComplaintModel
    {
        public Guid Id { get; set; }
        public string? ComplaintNo { get; set; }
        public string? CustomerName { get; set; }
        public string? CategoryCode { get; set; }
        public string? Severity { get; set; }
        public string? ComplaintStatus { get; set; }
        public DateTime ReceivedAt { get; set; }
        public string? Description { get; set; }
    }
}

namespace AppDelicare.Model.Model
{
    /// <summary>UI-6.17: kết quả Dish/Create.</summary>
    public class DishCreatedModel
    {
        public Guid Id { get; set; }
        public string? Code { get; set; }
    }
}
