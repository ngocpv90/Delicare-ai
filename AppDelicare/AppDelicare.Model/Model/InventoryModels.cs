using AppDelicare.Model.Entity;

namespace AppDelicare.Model.Model
{
    /// <summary>T16 (spec 7.6 summary): tồn 1 nguyên liệu tại 1 kho.</summary>
    public class InventorySummaryModel
    {
        public int STT { get; set; }
        public Guid WarehouseId { get; set; }
        public string? WarehouseName { get; set; }
        public Guid IngredientId { get; set; }
        public string? IngredientCode { get; set; }
        public string? IngredientName { get; set; }
        public Guid UnitId { get; set; }
        public string? UnitName { get; set; }
        /// <summary>Tồn thực tế (Σ OnHandQuantity).</summary>
        public decimal OnHandQuantity { get; set; }
        public decimal ReservedQuantity { get; set; }
        /// <summary>Khả dụng theo sổ = tồn − giữ chỗ (Σ AvailableQuantity).</summary>
        public decimal AvailableQuantity { get; set; }
        /// <summary>Xuất được = khả dụng của các lô không bị chặn (không hết hạn, không QC KHONG_DAT / REJECT / HOLD, không tạm giữ).</summary>
        public decimal UsableQuantity { get; set; }
        /// <summary>Tồn nằm ở lô bị chặn (hết hạn / QC / tạm giữ).</summary>
        public decimal BlockedQuantity { get; set; }
        public decimal ExpiredQuantity { get; set; }
        public int LotCount { get; set; }
        /// <summary>Số lô còn tồn sắp hết hạn (≤ ExpiryWarningDays của chính sách, mặc định BusinessRules:ExpiringLotDays).</summary>
        public int ExpiringLotCount { get; set; }
        public int ExpiredLotCount { get; set; }
        public DateTime? NearestExpiryDate { get; set; }
        public decimal? MinimumStock { get; set; }
        public decimal? SafetyStock { get; set; }
        public decimal? ReorderPoint { get; set; }
        public decimal? CriticalLevel { get; set; }
        /// <summary>critical / reorder / ok (PROPOSED – so UsableQuantity với chính sách tồn).</summary>
        public string Level { get; set; } = "ok";
        /// <summary>true = FEFO, false = FIFO (InventoryPolicie.UseFefo; không có chính sách = FEFO, D-24).</summary>
        public bool UseFefo { get; set; }
    }

    /// <summary>T16 (spec 7.6 by-lot / 7.5 lots): 1 dòng tồn theo (kho, vị trí, nguyên liệu, lô) + thông tin lô.</summary>
    public class InventoryLotBalanceModel
    {
        public int STT { get; set; }
        public Guid BalanceId { get; set; }
        public Guid WarehouseId { get; set; }
        public string? WarehouseName { get; set; }
        public Guid? StorageLocationId { get; set; }
        public string? StorageLocationName { get; set; }
        public Guid IngredientId { get; set; }
        public string? IngredientCode { get; set; }
        public string? IngredientName { get; set; }
        public Guid? MaterialLotId { get; set; }
        public string? LotNumber { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public DateTime? ReceivedDate { get; set; }
        public string? QcStatus { get; set; }
        public string? LotStatus { get; set; }
        public string? LotStatusName { get; set; }
        public decimal? UnitCost { get; set; }
        public Guid UnitId { get; set; }
        public string? UnitName { get; set; }
        public decimal OnHandQuantity { get; set; }
        public decimal ReservedQuantity { get; set; }
        public decimal AvailableQuantity { get; set; }
        public int? DaysToExpiry { get; set; }
        public bool IsExpired { get; set; }
        public bool IsExpiring { get; set; }
        /// <summary>Lý do không được xuất (null = xuất được).</summary>
        public string? BlockReason { get; set; }
        public bool IsBlocked => BlockReason != null;
        /// <summary>Thứ tự xuất theo FEFO / FIFO trong (kho, nguyên liệu), 1 = xuất trước. Lô bị chặn không có thứ tự.</summary>
        public int? PickRank { get; set; }
        public bool UseFefo { get; set; }
    }

    /// <summary>T16 (spec 7.7 suggest): gợi ý lô xuất cho 1 nguyên liệu.</summary>
    public class LotSuggestionModel
    {
        public Guid WarehouseId { get; set; }
        public Guid IngredientId { get; set; }
        public string? IngredientName { get; set; }
        public decimal RequestedQuantity { get; set; }
        public bool UseFefo { get; set; }
        /// <summary>FEFO hoặc FIFO.</summary>
        public string Method => UseFefo ? "FEFO" : "FIFO";
        public List<LotPickModel> ListPick { get; set; } = [];
        public decimal PickedQuantity { get; set; }
        /// <summary>Phần còn thiếu (&gt; 0 = không đủ tồn xuất được).</summary>
        public decimal ShortQuantity { get; set; }
        /// <summary>Lô còn tồn nhưng không được xuất (hết hạn, QC không đạt / tạm giữ, lô tạm giữ) – để hiển thị.</summary>
        public List<InventoryLotBalanceModel> ListBlocked { get; set; } = [];
    }

    public class LotPickModel
    {
        public Guid? MaterialLotId { get; set; }
        public string? LotNumber { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public DateTime? ReceivedDate { get; set; }
        public Guid UnitId { get; set; }
        public decimal AvailableQuantity { get; set; }
        public decimal Quantity { get; set; }
        public int PickRank { get; set; }
    }

    /// <summary>T16: chi tiết phiếu xuất kho + dòng + thao tác được phép + cảnh báo (FEFO).</summary>
    public class MaterialIssueDetailModel
    {
        public MaterialIssue? MaterialIssue { get; set; }
        public string? StatusName { get; set; }
        public string? WarehouseName { get; set; }
        public List<MaterialIssueLineDetailModel> ListLine { get; set; } = [];
        /// <summary>post, cancel (theo trạng thái).</summary>
        public List<string> AllowedActions { get; set; } = [];
        /// <summary>Bút toán kho đã ghi cho phiếu (ISSUE và bút toán đảo khi hủy).</summary>
        public List<InventoryTransaction> ListTransaction { get; set; } = [];
        /// <summary>Cảnh báo không chặn (vd. còn lô nên xuất trước theo FEFO / FIFO).</summary>
        public List<string> ListWarning { get; set; } = [];
    }

    public class MaterialIssueLineDetailModel
    {
        public Guid Id { get; set; }
        public Guid IngredientId { get; set; }
        public string? IngredientCode { get; set; }
        public string? IngredientName { get; set; }
        public Guid? MaterialLotId { get; set; }
        public string? LotNumber { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string? QcStatus { get; set; }
        public decimal Quantity { get; set; }
        public Guid UnitId { get; set; }
        public string? UnitName { get; set; }
        public string? Note { get; set; }
    }

    /// <summary>T16: chi tiết phiếu kiểm kê + dòng + % chênh lệch (GAP-C-06: không có cột % – tính khi trả về).</summary>
    public class StockCountDetailModel
    {
        public StockCount? StockCount { get; set; }
        public string? StatusName { get; set; }
        public string? WarehouseName { get; set; }
        public List<StockCountLineDetailModel> ListLine { get; set; } = [];
        /// <summary>% chênh lệch lớn nhất trong các dòng.</summary>
        public decimal MaxVariancePercent { get; set; }
        /// <summary>Ngưỡng BusinessRules:StockCountVariancePctNeedApproval.</summary>
        public decimal VarianceThresholdPercent { get; set; }
        /// <summary>true = có dòng lệch vượt ngưỡng → BE2.3: phải gửi duyệt STOCK_COUNT (StockCount/Submit) và duyệt xong mới ghi sổ.</summary>
        public bool NeedsApproval { get; set; }
        /// <summary>BE2.3: đã duyệt đủ các cấp (ApprovedBy có giá trị, phiếu còn Chờ duyệt) → chờ ghi sổ.</summary>
        public bool IsApproved { get; set; }
        public decimal TotalVarianceValue { get; set; }
        /// <summary>recordCounts, submit, approve, reject, post (theo trạng thái).</summary>
        public List<string> AllowedActions { get; set; } = [];
        public List<InventoryTransaction> ListTransaction { get; set; } = [];
        /// <summary>BE2.3: yêu cầu duyệt đang mở (cấp đang chờ / tổng số cấp, mục quyền, hạn).</summary>
        public ApprovalProgressModel? PendingApproval { get; set; }
    }

    /// <summary>
    /// BE2.3 (spec 7.6 adjust, GAP-F-04): kết quả điều chỉnh tồn tay – ghi ngay (<see cref="Transaction"/>) hoặc đã gửi duyệt STOCK_ADJUSTMENT
    /// (<see cref="PendingApproval"/> = true, bút toán ghi khi duyệt với <c>ReferenceId</c> = <see cref="ReferenceId"/>).
    /// </summary>
    public class InventoryAdjustResultModel
    {
        public InventoryTransaction? Transaction { get; set; }
        public bool PendingApproval { get; set; }
        public Guid? ApprovalRequestId { get; set; }
        public string? ApprovalRequestNo { get; set; }
        /// <summary>Khóa nối yêu cầu duyệt ↔ bút toán (ApprovalRequest.BusinessId = InventoryTransaction.ReferenceId).</summary>
        public Guid? ReferenceId { get; set; }
    }

    public class StockCountLineDetailModel
    {
        public Guid Id { get; set; }
        public Guid IngredientId { get; set; }
        public string? IngredientCode { get; set; }
        public string? IngredientName { get; set; }
        public Guid? MaterialLotId { get; set; }
        public string? LotNumber { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public Guid UnitId { get; set; }
        public string? UnitName { get; set; }
        public decimal SystemQuantity { get; set; }
        public decimal PhysicalQuantity { get; set; }
        public decimal VarianceQuantity { get; set; }
        public decimal? VarianceValue { get; set; }
        public decimal VariancePercent { get; set; }
        public bool OverThreshold { get; set; }
        public string? VarianceReason { get; set; }
    }

    /// <summary>T16: kết quả tính lại InventoryBalance từ sổ cái.</summary>
    public class InventoryRebuildModel
    {
        /// <summary>Số dòng tồn sau khi tính (kể cả dòng = 0).</summary>
        public int Rows { get; set; }
        /// <summary>Số dòng tồn có giá trị thay đổi hoặc mới tạo.</summary>
        public int Changed { get; set; }
    }
}
