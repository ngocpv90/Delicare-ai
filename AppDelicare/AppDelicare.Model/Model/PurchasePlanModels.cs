namespace AppDelicare.Model.Model
{
    /// <summary>UI-6.51 (D-137 h): màn "Kế hoạch mua" – ma trận nguyên liệu × ngày cần hàng.</summary>
    public class PurchasePlanBoardModel
    {
        public DateTime Today { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public List<PurchasePlanDayModel> ListDay { get; set; } = [];
        public List<PurchasePlanRowModel> ListRow { get; set; } = [];
        public List<string> ListWarning { get; set; } = [];
        public PurchasePlanKpiModel Kpi { get; set; } = new();
        /// <summary>Lần chạy gần nhất của job (giờ, kết quả).</summary>
        public DateTime? LastRunAt { get; set; }
        public string? LastRunSummary { get; set; }
        public List<string> RunTimes { get; set; } = [];
        public string? SendTime { get; set; }
        /// <summary>Số nguyên liệu đến hạn đặt (hôm nay + quá hạn) chưa có đề nghị / đơn mua – nút "Đặt phần hôm nay (N)".</summary>
        public int DueCount { get; set; }
    }

    public class PurchasePlanDayModel
    {
        public DateTime Date { get; set; }
        public bool IsToday { get; set; }
        public string? Source { get; set; }
        public string? SourceName { get; set; }
    }

    public class PurchasePlanKpiModel
    {
        public int DueToday { get; set; }
        public decimal DueTodayAmount { get; set; }
        public int Late { get; set; }
        public string? LateNote { get; set; }
        public int PendingApproval { get; set; }
        public string? PendingNote { get; set; }
        public int UnsentPurchaseOrder { get; set; }
        public decimal SavingMonth { get; set; }
        public string? SavingNote { get; set; }
    }

    public class PurchasePlanRowModel
    {
        public Guid IngredientId { get; set; }
        public string? IngredientCode { get; set; }
        public string? IngredientName { get; set; }
        public string? CategoryName { get; set; }
        public string? UnitName { get; set; }
        /// <summary>Nguyên liệu chuẩn có thay thế (nhãn ⇄) / dòng thay thế (nền xanh nhạt, dưới dòng chuẩn).</summary>
        public bool HasSubstitute { get; set; }
        public bool IsSubstitute { get; set; }
        public Guid? SubstituteOfId { get; set; }
        public string? Note { get; set; }
        public List<PurchasePlanCellModel> ListCell { get; set; } = [];
    }

    public class PurchasePlanCellModel
    {
        public DateTime Date { get; set; }
        public decimal Quantity { get; set; }
        public decimal Required { get; set; }
        /// <summary>ORDERED (Đã đặt) / APPROVED (Tự duyệt – chờ gửi NCC) / PENDING (Chờ duyệt) / DRAFT (Nháp) / NEED_PICK (Chờ chọn NCC) /
        /// DUE_TODAY (Đặt hôm nay) / LATE (Quá hạn đặt) / PLANNED (Dự kiến) / ENOUGH (Đủ hàng) / NONE.</summary>
        public string Status { get; set; } = "NONE";
        public string? StatusName { get; set; }
        public string? Sub { get; set; }
        public DateTime? OrderByDate { get; set; }
        public Guid? PurchaseRequestId { get; set; }
        public List<Guid> ListPurchaseOrderId { get; set; } = [];
    }

    /// <summary>UI-6.51: trang chi tiết ô – 5 bước (công thức → so giá dùng được → trừ hàng đã có → chia NCC → kết quả đơn mua).</summary>
    public class PurchasePlanCellDetailModel
    {
        public Guid IngredientId { get; set; }
        public string? IngredientName { get; set; }
        public string? UnitName { get; set; }
        public DateTime NeedDate { get; set; }
        public DateTime? OrderByDate { get; set; }
        public string? Status { get; set; }
        public string? StatusName { get; set; }
        public string? Title { get; set; }
        public string? Subtitle { get; set; }
        public List<PurchasePlanDishLineModel> ListDish { get; set; } = [];
        public MaterialHorizonSubstitutionModel? Substitution { get; set; }
        public List<PurchasePlanNetModel> ListNet { get; set; } = [];
        public List<PurchasePlanSplitModel> ListSplit { get; set; } = [];
        public List<PurchasePlanOrderModel> ListOrder { get; set; } = [];
        public string? Message { get; set; }
        public bool CanOrder { get; set; }
    }

    public class PurchasePlanDishLineModel
    {
        public string? DishName { get; set; }
        public decimal Portions { get; set; }
        public string? StandardText { get; set; }
        public string? SubstituteText { get; set; }
        public bool StandardOnly { get; set; }
    }

    public class PurchasePlanNetModel
    {
        public Guid IngredientId { get; set; }
        public string? IngredientName { get; set; }
        public string? UnitName { get; set; }
        public decimal Required { get; set; }
        public decimal SafetyStock { get; set; }
        public decimal Available { get; set; }
        public decimal Incoming { get; set; }
        public decimal Requested { get; set; }
        public decimal ToBuy { get; set; }
    }

    public class PurchasePlanSplitModel
    {
        public Guid IngredientId { get; set; }
        public string? IngredientName { get; set; }
        public string? UnitName { get; set; }
        public decimal Quantity { get; set; }
        public bool Manual { get; set; }
        public string? Note { get; set; }
        public List<SupplierSplitModel> ListPart { get; set; } = [];
    }

    public class PurchasePlanOrderModel
    {
        public Guid? PurchaseOrderId { get; set; }
        public string? PurchaseOrderNumber { get; set; }
        public Guid? PurchaseRequestId { get; set; }
        public string? PurchaseRequestNumber { get; set; }
        public string? SupplierName { get; set; }
        public string? Content { get; set; }
        public decimal Amount { get; set; }
        public string? ApprovalText { get; set; }
        public string? SendText { get; set; }
        public bool Planned { get; set; }
    }

    /// <summary>Kết quả một lần chạy kế hoạch mua (job / nút).</summary>
    public class PurchasePlanRunResultModel
    {
        public int Created { get; set; }
        public int Updated { get; set; }
        public int AutoApproved { get; set; }
        public int Submitted { get; set; }
        public int PurchaseOrders { get; set; }
        public int Sent { get; set; }
        public List<string> ListRequestNumber { get; set; } = [];
        public List<string> ListMessage { get; set; } = [];
        public string Summary => $"Lập {Created} · cập nhật {Updated} đề nghị · tự duyệt {AutoApproved} · gửi duyệt {Submitted} · {PurchaseOrders} đơn mua · gửi NCC {Sent}";
    }
}

namespace AppDelicare.Model.Request.Custom
{
    public class PurchasePlanBoardRequest
    {
        public DateTime? From { get; set; }
        public int? Days { get; set; }
        public Guid? OrganizationUnitId { get; set; }
    }

    public class PurchasePlanCellRequest
    {
        public Guid IngredientId { get; set; }
        public DateTime NeedDate { get; set; }
        public Guid? OrganizationUnitId { get; set; }
    }

    /// <summary>Chạy kế hoạch mua: OnlyDue = phần đến hạn đặt (như job); IngredientId + NeedDate = "Đặt ngay" một ô (kể cả chưa tới hạn).</summary>
    public class PurchasePlanRunRequest
    {
        public bool OnlyDue { get; set; } = true;
        public Guid? IngredientId { get; set; }
        public DateTime? NeedDate { get; set; }
        public Guid? OrganizationUnitId { get; set; }
        public string? Note { get; set; }
    }

    /// <summary>Người mua giữ 100% chuẩn (SharePct = 0) / đổi tỷ lệ thay thế cho một ngày cần.</summary>
    public class PurchasePlanOverrideSubstituteRequest
    {
        public DateTime NeedDate { get; set; }
        public Guid StdIngredientId { get; set; }
        public Guid SubIngredientId { get; set; }
        public decimal SharePct { get; set; }
        public string? Reason { get; set; }
    }

    public class PurchasePlanOverrideSplitRequest
    {
        public DateTime NeedDate { get; set; }
        public Guid IngredientId { get; set; }
        /// <summary>NCC → %, tổng 100. Trống = bỏ sửa tay (về tỷ lệ tính).</summary>
        public Dictionary<Guid, decimal>? Pct { get; set; }
        public string? Reason { get; set; }
    }

    /// <summary>Bảng giá NCC: lưu tỷ lệ chia của các NCC một nguyên liệu (tổng các NCC có tỷ lệ = 100).</summary>
    public class SupplierMaterialAllocationRequest
    {
        public Guid IngredientId { get; set; }
        /// <summary>Id dòng bảng giá (SupplierMaterial) → %; null / 0 = bỏ tỷ lệ.</summary>
        public Dictionary<Guid, decimal?> Pct { get; set; } = [];
    }
}
