namespace AppDelicare.Model.Model
{
    /// <summary>UI-6.23 (D-104): màn "7 ngày tới" của Kế hoạch sản xuất – mỗi ngày một cột (đơn chốt / chưa chốt, kế hoạch duy nhất của ngày, ca, việc gợi ý).</summary>
    public class ProductionPlanDaysModel
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public DateTime Today { get; set; }
        /// <summary>Giờ job chốt đơn (Jobs:LockOrders:At, mặc định 20:00) – chữ "Tự lập sau chốt HH:mm".</summary>
        public string LockAt { get; set; } = "20:00";
        /// <summary>Bật tự lập + tự duyệt + tự ra lệnh sau chốt (BusinessRules:ProductionAutoReleaseAfterLock).</summary>
        public bool AutoRelease { get; set; }
        /// <summary>Ca có số liệu trong kỳ (sắp theo giờ bắt đầu) – mỗi ca một dòng của nhóm "Kế hoạch theo ca".</summary>
        public List<ProductionPlanDayShiftModel> ListShift { get; set; } = [];
        public List<ProductionPlanDayModel> ListDay { get; set; } = [];
    }

    public class ProductionPlanDayModel
    {
        public DateTime Date { get; set; }
        public decimal LockedMeals { get; set; }
        public decimal OpenMeals { get; set; }
        public int LockedOrderCount { get; set; }
        public int OpenOrderCount { get; set; }
        public Guid? PlanId { get; set; }
        public string? PlanNumber { get; set; }
        public string? PlanStatus { get; set; }
        /// <summary>Trạng thái cột: released (đã ra lệnh đủ) · approved (đã duyệt, còn ca chưa ra lệnh) · draft · held (hệ thống giữ lại) · none (chưa có kế hoạch, đã có đơn chốt) · open (chưa chốt đơn) · empty (không có đơn).</summary>
        public string DayStatus { get; set; } = "empty";
        public string DayStatusName { get; set; } = string.Empty;
        public int DishCount { get; set; }
        public decimal PlanPortions { get; set; }
        public int ProductionOrderCount { get; set; }
        public int ShiftReleased { get; set; }
        public int ShiftTotal { get; set; }
        /// <summary>Suất của đơn chốt sau lần lập kế hoạch (so <c>Note.GeneratedAt</c>) – chữ "+N suất chốt sau lần lập".</summary>
        public decimal LateMeals { get; set; }
        /// <summary>Lý do hệ thống giữ lại (Note.AutoRun) khi kế hoạch Nháp chờ COO.</summary>
        public string? HeldReason { get; set; }
        /// <summary>Lần chạy tự động gần nhất (Note.AutoRun) – tab Nhật ký tự động.</summary>
        public string? AutoRun { get; set; }
        /// <summary>Việc gợi ý: recalc (Lập / Tính lại) · release (Ra lệnh n ca) · viewReason (Xem lý do) · view (Xem) · none.</summary>
        public string Action { get; set; } = "none";
        public List<string> AllowedActions { get; set; } = [];
        public List<ProductionPlanDayShiftModel> ListShift { get; set; } = [];
    }

    public class ProductionPlanDayShiftModel
    {
        public Guid ShiftId { get; set; }
        public string? ShiftCode { get; set; }
        public string? ShiftName { get; set; }
        public TimeSpan? StartTime { get; set; }
        /// <summary>Suất (đơn đã chốt / đang SX) của ca – dòng chưa gán ca tính vào ca mặc định.</summary>
        public decimal Meals { get; set; }
        /// <summary>released · approved · draft · held · none (chưa có kế hoạch).</summary>
        public string Status { get; set; } = "none";
        public int ProductionOrderCount { get; set; }
    }

    /// <summary>UI-6.23: kết quả Lập / Tính lại một ngày (máy chủ tự quyết luồng).</summary>
    public class ProductionPlanRecalculateModel
    {
        public DateTime ProductionDate { get; set; }
        /// <summary>RELEASED (tự duyệt + ra lệnh) · HELD (Nháp, có lý do) · SKIPPED (đã duyệt trước) · DRAFT (còn đơn chưa chốt → chỉ lập Nháp) · NONE.</summary>
        public string Status { get; set; } = "NONE";
        public Guid? PlanId { get; set; }
        public string? PlanNumber { get; set; }
        public int ProductionOrderCount { get; set; }
        public decimal Portions { get; set; }
        public int DishCount { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> ListReason { get; set; } = [];
        public List<string> ListWarning { get; set; } = [];
    }

    /// <summary>UI-6.23: nguyên liệu thiếu nếu ra lệnh lúc này (chỉ đọc) + đơn mua đang mở của nguyên liệu đó.</summary>
    public class ProductionPlanShortageModel
    {
        public Guid PlanId { get; set; }
        public string? PlanNumber { get; set; }
        public DateTime ProductionDate { get; set; }
        public string? HeldReason { get; set; }
        public List<ProductionPlanShortageLineModel> ListLine { get; set; } = [];
        public List<string> ListWarning { get; set; } = [];
    }

    public class ProductionPlanShortageLineModel
    {
        public Guid IngredientId { get; set; }
        public string? IngredientName { get; set; }
        public string? UnitName { get; set; }
        public decimal Required { get; set; }
        public decimal Reserved { get; set; }
        public decimal Short { get; set; }
        public List<ProductionPlanIncomingModel> ListIncoming { get; set; } = [];
    }

    public class ProductionPlanIncomingModel
    {
        public Guid PurchaseOrderId { get; set; }
        public string? PurchaseOrderNumber { get; set; }
        public string? Status { get; set; }
        public decimal Quantity { get; set; }
        public string? UnitName { get; set; }
        public DateTime? ExpectedDate { get; set; }
    }
}
