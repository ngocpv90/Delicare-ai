using AppDelicare.Model.Entity;

namespace AppDelicare.Model.Model
{
    // UI-6 (D-80) nhóm B – trang chi tiết toàn màn Mua hàng / Kho: Đơn mua (PO), Phiếu nhập, Đề nghị mua, Lô hàng, Nguyên liệu.
    // Quy ước như DetailPageModels.cs: Detail = model chi tiết đang có (GetDetail), cộng chỉ số / tên cho phần đầu + tab; không bảng / cột mới.

    /// <summary>PurchaseOrder/GetOverview: chi tiết PO + yêu cầu duyệt mới nhất + tiến độ nhận hàng + phiếu nhập kèm kết quả QC.</summary>
    public class PurchaseOrderOverviewModel
    {
        public PurchaseOrderDetailModel Detail { get; set; } = new();
        public Guid? ApprovalRequestId { get; set; }
        public int LineCount { get; set; }
        /// <summary>Số phiếu nhập chưa hủy.</summary>
        public int ReceiptCount { get; set; }
        /// <summary>Σ số đặt các dòng (đơn vị dòng PO – cộng thẳng, chỉ dùng cho % nhận).</summary>
        public decimal OrderedQuantity { get; set; }
        /// <summary>Σ số đạt của phiếu nhập đã ghi sổ.</summary>
        public decimal ReceivedQuantity { get; set; }
        /// <summary>Σ đã nhận / Σ đặt × 100 (0 khi PO không dòng).</summary>
        public decimal ReceivedPercent { get; set; }
        /// <summary>Số dòng còn hàng chưa nhận.</summary>
        public int RemainingLineCount { get; set; }
        /// <summary>Σ số bị loại QC của các phiếu nhập chưa hủy.</summary>
        public decimal RejectedQuantity { get; set; }
        public List<ScmReceiptQcModel> ListReceiptQc { get; set; } = [];
    }

    /// <summary>Phiếu nhập của 1 PO kèm phiếu QC đầu vào mới nhất và số đạt / loại.</summary>
    public class ScmReceiptQcModel
    {
        public Guid Id { get; set; }
        public string? ReceiptNumber { get; set; }
        public DateTime ReceiptDate { get; set; }
        public string? Status { get; set; }
        public string? StatusName { get; set; }
        public string? WarehouseName { get; set; }
        public Guid? QcInspectionId { get; set; }
        public string? InspectionNumber { get; set; }
        public string? QcResult { get; set; }
        public string? QcResultName { get; set; }
        public decimal ReceivedQuantity { get; set; }
        public decimal AcceptedQuantity { get; set; }
        public decimal RejectedQuantity { get; set; }
    }

    /// <summary>GoodsReceipt/GetOverview: chi tiết phiếu nhập (dòng, QC, lô) + số dòng loại / lệch nhiệt độ + giá trị.</summary>
    public class GoodsReceiptOverviewModel
    {
        public GoodsReceiptDetailModel Detail { get; set; } = new();
        public int LineCount { get; set; }
        /// <summary>Dòng có số bị loại hoặc kết quả KHONG_DAT.</summary>
        public int RejectedLineCount { get; set; }
        /// <summary>Dòng nhiệt độ nhận ngoài vùng đạt (có điều kiện / loại / thiếu số đo).</summary>
        public int TemperatureIssueCount { get; set; }
        /// <summary>Số lô NL- đã tạo (sau ghi sổ).</summary>
        public int LotCount { get; set; }
        /// <summary>Σ số nhận × đơn giá.</summary>
        public decimal ReceivedValue { get; set; }
        /// <summary>Σ số đạt × đơn giá.</summary>
        public decimal AcceptedValue { get; set; }
    }

    /// <summary>PurchaseRequest/GetOverview: chi tiết đề nghị mua + yêu cầu duyệt mới nhất + người đề nghị + độ phủ NCC.</summary>
    public class PurchaseRequestOverviewModel
    {
        public PurchaseRequestDetailModel Detail { get; set; } = new();
        public Guid? ApprovalRequestId { get; set; }
        public string? RequesterName { get; set; }
        public int LineCount { get; set; }
        /// <summary>Số NCC gợi ý khác nhau (bảng giá đang hiệu lực).</summary>
        public int SupplierCount { get; set; }
        /// <summary>Dòng chưa có NCC / giá → lập PO sẽ bỏ qua hoặc cảnh báo.</summary>
        public int NoSupplierLineCount { get; set; }
        public int PurchaseOrderCount { get; set; }
    }

    /// <summary>MaterialLot/GetOverview: hồ sơ lô – nguồn (NCC / phiếu nhập / PO), tồn theo kho, hạn dùng, giao dịch kho của lô.</summary>
    public class MaterialLotOverviewModel
    {
        public MaterialLot Lot { get; set; } = new();
        public string? StatusName { get; set; }
        public string? IngredientCode { get; set; }
        public string? IngredientName { get; set; }
        public string? UnitName { get; set; }
        public Guid? SupplierId { get; set; }
        public string? SupplierName { get; set; }
        public Guid? GoodsReceiptId { get; set; }
        public string? GoodsReceiptNumber { get; set; }
        public Guid? PurchaseOrderId { get; set; }
        public string? PurchaseOrderNumber { get; set; }
        /// <summary>Số nhận / đạt trên dòng phiếu nhập sinh ra lô (null khi lô không từ phiếu nhập).</summary>
        public decimal? ReceivedQuantity { get; set; }
        public decimal? AcceptedQuantity { get; set; }
        public decimal OnHandQuantity { get; set; }
        public decimal ReservedQuantity { get; set; }
        public decimal AvailableQuantity { get; set; }
        /// <summary>Σ xuất (QuantityOut) của các giao dịch kho của lô.</summary>
        public decimal IssuedQuantity { get; set; }
        public int? DaysToExpiry { get; set; }
        public bool IsExpired { get; set; }
        /// <summary>Lý do chặn xuất (hết hạn / QC không đạt / tạm giữ…) – null = xuất được.</summary>
        public string? BlockReason { get; set; }
        public List<ScmBalanceModel> ListBalance { get; set; } = [];
        /// <summary>Giao dịch kho của lô, mới nhất trước (tối đa 100).</summary>
        public List<ScmTransactionModel> ListTransaction { get; set; } = [];
    }

    public class ScmBalanceModel
    {
        public Guid WarehouseId { get; set; }
        public string? WarehouseName { get; set; }
        public Guid? MaterialLotId { get; set; }
        public string? LotNumber { get; set; }
        public decimal OnHandQuantity { get; set; }
        public decimal ReservedQuantity { get; set; }
        public decimal AvailableQuantity { get; set; }
    }

    public class ScmTransactionModel
    {
        public Guid Id { get; set; }
        public string? TransactionNumber { get; set; }
        public string? TransactionType { get; set; }
        public DateTime TransactionDate { get; set; }
        public string? WarehouseName { get; set; }
        public Guid? MaterialLotId { get; set; }
        public string? LotNumber { get; set; }
        public decimal QuantityIn { get; set; }
        public decimal QuantityOut { get; set; }
        public decimal? UnitCost { get; set; }
        public string? ReferenceType { get; set; }
        public Guid? ReferenceId { get; set; }
        public string? Reason { get; set; }
    }

    /// <summary>Ingredient/GetOverview: hồ sơ nguyên liệu – tồn (theo kho), lô còn tồn, giá mua gần nhất, bảng giá NCC, lịch sử giá, giao dịch kho.</summary>
    public class IngredientOverviewModel
    {
        public Ingredient Ingredient { get; set; } = new();
        public string? CategoryName { get; set; }
        public string? BaseUnitName { get; set; }
        public decimal OnHandQuantity { get; set; }
        public decimal AvailableQuantity { get; set; }
        public decimal? MinimumStock { get; set; }
        public decimal? SafetyStock { get; set; }
        public bool BelowMinimum { get; set; }
        /// <summary>Số lô còn tồn.</summary>
        public int ActiveLotCount { get; set; }
        /// <summary>Lô còn tồn hết hạn trong <see cref="ExpiryWarningDays"/> ngày (gồm lô đã quá hạn).</summary>
        public int ExpiringLotCount { get; set; }
        /// <summary>Ngưỡng cảnh báo hạn dùng: InventoryPolicy.ExpiryWarningDays nhỏ nhất của nguyên liệu, mặc định 3 ngày.</summary>
        public int ExpiryWarningDays { get; set; }
        /// <summary>Giá mua gần nhất = đơn giá dòng PO (chưa hủy, không Nháp) mới nhất theo ngày đặt.</summary>
        public decimal? LastPurchasePrice { get; set; }
        public DateTime? LastPurchaseDate { get; set; }
        public string? LastPurchaseSupplierName { get; set; }
        public string? LastPurchaseOrderNumber { get; set; }
        public string? LastPurchaseUnitName { get; set; }
        public string? PreferredSupplierName { get; set; }
        /// <summary>Số NCC có bảng giá đang hiệu lực.</summary>
        public int SupplierCount { get; set; }
        public List<ScmBalanceModel> ListWarehouse { get; set; } = [];
        public List<ScmIngredientLotModel> ListLot { get; set; } = [];
        public List<ScmSupplierPriceModel> ListSupplierPrice { get; set; } = [];
        /// <summary>Đơn giá các dòng PO gần nhất (tối đa 30).</summary>
        public List<ScmPriceHistoryModel> ListPriceHistory { get; set; } = [];
        /// <summary>Giao dịch kho mới nhất (tối đa 100).</summary>
        public List<ScmTransactionModel> ListTransaction { get; set; } = [];
    }

    public class ScmIngredientLotModel
    {
        public Guid Id { get; set; }
        public string? LotNumber { get; set; }
        public string? SupplierName { get; set; }
        public DateTime? ReceivedDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public int? DaysToExpiry { get; set; }
        public decimal OnHandQuantity { get; set; }
        public decimal AvailableQuantity { get; set; }
        public string? Status { get; set; }
        public string? StatusName { get; set; }
        public string? QcStatus { get; set; }
        public bool IsBlocked { get; set; }
    }

    public class ScmSupplierPriceModel
    {
        public Guid Id { get; set; }
        public Guid SupplierId { get; set; }
        public string? SupplierName { get; set; }
        public decimal? UnitPrice { get; set; }
        public string? PurchaseUnitName { get; set; }
        public decimal? MinimumOrderQuantity { get; set; }
        public int? LeadTimeDays { get; set; }
        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public bool IsPreferred { get; set; }
        public bool IsEffective { get; set; }
        public string? Status { get; set; }
    }

    public class ScmPriceHistoryModel
    {
        public Guid PurchaseOrderId { get; set; }
        public string? PurchaseOrderNumber { get; set; }
        public DateTime OrderDate { get; set; }
        public string? SupplierName { get; set; }
        public decimal Quantity { get; set; }
        public string? UnitName { get; set; }
        public decimal UnitPrice { get; set; }
        public string? Status { get; set; }
        public string? StatusName { get; set; }
    }
}
