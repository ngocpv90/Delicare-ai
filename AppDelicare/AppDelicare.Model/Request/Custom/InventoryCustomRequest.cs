using System.ComponentModel.DataAnnotations;

namespace AppDelicare.Model.Request.Custom
{
    /// <summary>
    /// T16 (spec 7.6 adjust, GAP-F-04): bút toán điều chỉnh tay (form). <c>Quantity</c> có dấu: &gt; 0 nhập thêm, &lt; 0 giảm.
    /// Lý do bắt buộc. Bỏ trống UnitId = đơn vị của dòng tồn đang có, không có thì đơn vị cơ sở của nguyên liệu.
    /// </summary>
    public class InventoryAdjustRequest
    {
        [Required]
        public Guid WarehouseId { get; set; }
        public Guid? StorageLocationId { get; set; }
        [Required]
        public Guid IngredientId { get; set; }
        public Guid? MaterialLotId { get; set; }
        public Guid? UnitId { get; set; }
        public decimal Quantity { get; set; }
        public decimal? UnitCost { get; set; }
        [MaxLength(1000)]
        public string? Reason { get; set; }
        [MaxLength(1000)]
        public string? Note { get; set; }
    }

    /// <summary>T16 (spec 7.6 transfer, GAP-F-03): chuyển kho / vị trí 1 lô (form) – ghi cặp TRANSFER_OUT / TRANSFER_IN cùng ReferenceId.</summary>
    public class InventoryTransferRequest
    {
        [Required]
        public Guid FromWarehouseId { get; set; }
        public Guid? FromStorageLocationId { get; set; }
        [Required]
        public Guid ToWarehouseId { get; set; }
        public Guid? ToStorageLocationId { get; set; }
        [Required]
        public Guid IngredientId { get; set; }
        public Guid? MaterialLotId { get; set; }
        public decimal Quantity { get; set; }
        [MaxLength(1000)]
        public string? Reason { get; set; }
    }

    /// <summary>T16 (spec 7.6 inventory-transactions): sổ cái kho có lọc khoảng ngày (GET, query).</summary>
    public class InventoryLedgerSearchRequest
    {
        public string? SearchText { get; set; }
        public Guid? WarehouseId { get; set; }
        public Guid? IngredientId { get; set; }
        public Guid? MaterialLotId { get; set; }
        public string? TransactionType { get; set; }
        public string? ReferenceType { get; set; }
        public Guid? ReferenceId { get; set; }
        /// <summary>Từ ngày (tính cả ngày).</summary>
        public DateTime? FromDate { get; set; }
        /// <summary>Đến ngày (tính cả ngày).</summary>
        public DateTime? ToDate { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string? SortField { get; set; }
        public string? SortOrder { get; set; }
    }

    /// <summary>T16 (spec 7.6 summary): tồn theo kho × nguyên liệu (GET, query).</summary>
    public class InventorySummaryRequest
    {
        public string? SearchText { get; set; }
        public Guid? WarehouseId { get; set; }
        public Guid? IngredientId { get; set; }
        public Guid? IngredientCategoryId { get; set; }
        /// <summary>Chỉ nguyên liệu mức "critical" / "reorder".</summary>
        public bool? BelowMin { get; set; }
        /// <summary>Có lấy dòng tồn = 0 không (mặc định không).</summary>
        public bool? IncludeZero { get; set; }
    }

    /// <summary>T16 (spec 7.5 lots, 7.6 by-lot): tồn theo lô, sắp FEFO / FIFO (GET, query).</summary>
    public class InventoryByLotRequest
    {
        public Guid? WarehouseId { get; set; }
        public Guid? IngredientId { get; set; }
        public Guid? MaterialLotId { get; set; }
        /// <summary>BOT-2.4 (QR <c>start=LOC_&lt;mã&gt;</c>): chỉ tồn tại vị trí kho có mã này.</summary>
        [System.ComponentModel.DataAnnotations.MaxLength(50)]
        public string? StorageLocationCode { get; set; }
        /// <summary>Chỉ lô hết hạn trong N ngày tới (kể cả đã hết hạn). Bỏ trống = không lọc.</summary>
        public int? ExpiringWithinDays { get; set; }
        /// <summary>Có lấy dòng tồn = 0 không (mặc định không).</summary>
        public bool? IncludeZero { get; set; }
    }

    /// <summary>T16: tính lại InventoryBalance từ sổ cái (chỉ admin).</summary>
    public class InventoryRebuildRequest
    {
        public Guid? WarehouseId { get; set; }
    }

    /// <summary>T16 (spec 7.5 hold / release): tạm giữ / mở lô (form), lý do bắt buộc khi giữ.</summary>
    public class MaterialLotHoldRequest
    {
        [Required]
        public Guid Id { get; set; }
        [MaxLength(1000)]
        public string? Reason { get; set; }
    }

    /// <summary>T16 (spec 7.7 suggest): gợi ý lô xuất FEFO / FIFO cho 1 nguyên liệu (GET, query).</summary>
    public class MaterialIssueSuggestRequest
    {
        [Required]
        public Guid WarehouseId { get; set; }
        [Required]
        public Guid IngredientId { get; set; }
        public decimal Quantity { get; set; }
    }

    /// <summary>
    /// T16 (spec 7.7 material-issues): tạo phiếu xuất Nháp kèm dòng. Gửi JSON (danh sách dòng lồng nhau – CLAUDE.md 4.2).
    /// Dòng không chọn lô → tự phân bổ theo FEFO / FIFO (có thể tách thành nhiều dòng theo lô).
    /// </summary>
    /// <summary>BIZ-2.5: ghi sổ mọi phiếu xuất Nháp cùng thẻ ca <c>[XUAT_CA:&lt;số KH&gt;|&lt;mã ca&gt;]</c> (form).</summary>
    public class MaterialIssueShiftRequest
    {
        [Required]
        [MaxLength(200)]
        public string Tag { get; set; } = string.Empty;
    }

    public class MaterialIssueCreateRequest
    {
        [Required]
        public Guid WarehouseId { get; set; }
        /// <summary>Lệnh sản xuất (T17). Pilot: bỏ trống = xuất không gắn lệnh SX (GAP-INV-02).</summary>
        public Guid? ProductionOrderId { get; set; }
        /// <summary>Bỏ trống = bây giờ.</summary>
        public DateTime? IssueDate { get; set; }
        [MaxLength(2000)]
        public string? Note { get; set; }
        public List<MaterialIssueCreateLineRequest> ListLine { get; set; } = [];
    }

    public class MaterialIssueCreateLineRequest
    {
        [Required]
        public Guid IngredientId { get; set; }
        /// <summary>Bỏ trống = phân bổ tự động theo FEFO / FIFO.</summary>
        public Guid? MaterialLotId { get; set; }
        public decimal Quantity { get; set; }
        /// <summary>Bỏ trống = đơn vị của dòng tồn. Pilot không quy đổi đơn vị: phải trùng đơn vị tồn.</summary>
        public Guid? UnitId { get; set; }
        [MaxLength(1000)]
        public string? Note { get; set; }
    }

    /// <summary>T16: ghi sổ 1 phiếu xuất / kiểm kê (form).</summary>
    public class InventoryDocumentActionRequest
    {
        [Required]
        public Guid Id { get; set; }
        [MaxLength(1000)]
        public string? Note { get; set; }
    }

    /// <summary>T16: hủy phiếu xuất – lý do bắt buộc (form). Phiếu đã xuất → ghi bút toán đảo.</summary>
    public class MaterialIssueCancelRequest
    {
        [Required]
        public Guid Id { get; set; }
        [MaxLength(1000)]
        public string? Reason { get; set; }
    }

    /// <summary>T16 (spec 7.8 create): tạo phiếu kiểm kê Nháp, sinh dòng từ tồn hiện tại của kho (form).</summary>
    public class StockCountCreateRequest
    {
        [Required]
        public Guid WarehouseId { get; set; }
        /// <summary>DAILY / WEEKLY / MONTHLY / ADHOC (mặc định ADHOC).</summary>
        public string? CountType { get; set; }
        /// <summary>Bỏ trống = hôm nay.</summary>
        public DateTime? CountDate { get; set; }
        public Guid? ResponsibleEmployeeId { get; set; }
        /// <summary>Chỉ kiểm 1 nguyên liệu (bỏ trống = mọi nguyên liệu đang có tồn trong kho).</summary>
        public Guid? IngredientId { get; set; }
        [MaxLength(2000)]
        public string? Note { get; set; }
    }

    /// <summary>T16 (spec 7.8 lines): ghi số đếm thực tế. Gửi JSON (danh sách lồng nhau). Dòng không có Id = hàng tìm thấy ngoài sổ.</summary>
    public class StockCountRecordRequest
    {
        [Required]
        public Guid Id { get; set; }
        public List<StockCountRecordLineRequest> ListLine { get; set; } = [];
    }

    public class StockCountRecordLineRequest
    {
        /// <summary>Id dòng kiểm kê có sẵn; bỏ trống = thêm dòng mới (phải có IngredientId).</summary>
        public Guid? Id { get; set; }
        public Guid? IngredientId { get; set; }
        public Guid? MaterialLotId { get; set; }
        public Guid? UnitId { get; set; }
        public decimal PhysicalQuantity { get; set; }
        [MaxLength(1000)]
        public string? VarianceReason { get; set; }
    }
}
