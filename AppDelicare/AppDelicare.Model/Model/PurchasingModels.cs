using AppDelicare.Model.Entity;

namespace AppDelicare.Model.Model
{
    /// <summary>T15 (spec 7.2): chi tiết đề nghị mua – dòng kèm NCC gợi ý (SupplierMaterial đang hiệu lực) và tiền ước tính.</summary>
    public class PurchaseRequestDetailModel
    {
        public PurchaseRequest PurchaseRequest { get; set; } = null!;
        public string? StatusName { get; set; }
        public decimal EstimatedAmount { get; set; }
        public List<string> AllowedActions { get; set; } = [];
        public List<PurchaseRequestLineDetailModel> ListLine { get; set; } = [];
        /// <summary>PO đã lập từ đề nghị này.</summary>
        public List<PurchaseOrderBriefModel> ListPurchaseOrder { get; set; } = [];
    }

    public class PurchaseRequestLineDetailModel
    {
        public Guid Id { get; set; }
        public Guid IngredientId { get; set; }
        public string? IngredientCode { get; set; }
        public string? IngredientName { get; set; }
        public decimal Quantity { get; set; }
        public Guid UnitId { get; set; }
        public string? UnitName { get; set; }
        public DateTime? RequiredDate { get; set; }
        public string? Note { get; set; }
        public Guid? SupplierId { get; set; }
        public string? SupplierName { get; set; }
        public decimal? UnitPrice { get; set; }
        public string? PurchaseUnitName { get; set; }
        public decimal? EstimatedAmount { get; set; }
    }

    public class PurchaseOrderBriefModel
    {
        public Guid Id { get; set; }
        public string? PurchaseOrderNumber { get; set; }
        public Guid SupplierId { get; set; }
        public string? SupplierName { get; set; }
        public string? Status { get; set; }
        public string? StatusName { get; set; }
        public decimal TotalAmount { get; set; }
        public int LineCount { get; set; }
    }

    /// <summary>T15 (spec 7.2 convert-to-po): kết quả lập PO từ đề nghị mua.</summary>
    public class PurchaseRequestConvertResultModel
    {
        public Guid PurchaseRequestId { get; set; }
        public string? RequestNumber { get; set; }
        public List<PurchaseOrderBriefModel> ListPurchaseOrder { get; set; } = [];
        /// <summary>Cảnh báo không chặn: dưới MOQ, chưa có giá, thiếu quy đổi đơn vị mua.</summary>
        public List<string> ListWarning { get; set; } = [];
    }

    /// <summary>T15 (spec 7.3): chi tiết PO – dòng kèm số đã nhận (phiếu đã ghi sổ: số đạt) / đang chờ QC / còn lại, thao tác được phép, yêu cầu duyệt đang chờ.</summary>
    public class PurchaseOrderDetailModel
    {
        public PurchaseOrder PurchaseOrder { get; set; } = null!;
        public string? StatusName { get; set; }
        public string? SupplierCode { get; set; }
        public string? SupplierName { get; set; }
        public string? SupplierAddress { get; set; }
        public string? SupplierPhone { get; set; }
        public string? PurchaseRequestNumber { get; set; }
        public Guid? PendingApprovalId { get; set; }
        public string? PendingApprovalNo { get; set; }
        /// <summary>Tổng tiền vượt ngưỡng GĐ duyệt (D-11) – Pilot chỉ cảnh báo.</summary>
        public bool NeedsDirectorApproval { get; set; }
        public List<string> AllowedActions { get; set; } = [];
        public List<PurchaseOrderLineDetailModel> ListLine { get; set; } = [];
        public List<GoodsReceiptBriefModel> ListGoodsReceipt { get; set; } = [];
        public List<string> ListWarning { get; set; } = [];
    }

    public class PurchaseOrderLineDetailModel
    {
        public Guid Id { get; set; }
        public Guid IngredientId { get; set; }
        public string? IngredientCode { get; set; }
        public string? IngredientName { get; set; }
        public decimal Quantity { get; set; }
        public Guid UnitId { get; set; }
        public string? UnitName { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineAmount { get; set; }
        public string? Note { get; set; }
        /// <summary>Σ số đạt của phiếu nhập đã ghi sổ (GAP-PUR-01: PurchaseOrderLine không có cột đã nhận).</summary>
        public decimal ReceivedQuantity { get; set; }
        /// <summary>Σ số nhận của phiếu nhập chưa ghi sổ (chờ QC / chờ ghi sổ).</summary>
        public decimal PendingQuantity { get; set; }
        /// <summary>max(0, đặt − đã nhận − đang chờ).</summary>
        public decimal RemainingQuantity { get; set; }
    }

    public class GoodsReceiptBriefModel
    {
        public Guid Id { get; set; }
        public string? ReceiptNumber { get; set; }
        public DateTime ReceiptDate { get; set; }
        public string? Status { get; set; }
        public string? StatusName { get; set; }
    }

    /// <summary>T15 (spec 7.4 / 9.1): chi tiết phiếu nhập + phiếu QC + lô đã tạo.</summary>
    public class GoodsReceiptDetailModel
    {
        public GoodsReceipt GoodsReceipt { get; set; } = null!;
        public string? StatusName { get; set; }
        public string? PurchaseOrderNumber { get; set; }
        public string? SupplierCode { get; set; }
        public string? SupplierName { get; set; }
        public string? WarehouseName { get; set; }
        public Guid? QcInspectionId { get; set; }
        public string? InspectionNumber { get; set; }
        /// <summary>Kết luận chung của phiếu QC (DAT / DAT_MOT_PHAN / KHONG_DAT; trống = chưa kết luận).</summary>
        public string? QcResult { get; set; }
        public string? QcResultName { get; set; }
        public List<string> AllowedActions { get; set; } = [];
        public List<GoodsReceiptLineDetailModel> ListLine { get; set; } = [];
        /// <summary>Sự cố NCC đã ghi khi ghi sổ (dòng DAT_MOT_PHAN / KHONG_DAT).</summary>
        public Guid? SupplierIssueId { get; set; }
        public List<string> ListWarning { get; set; } = [];
    }

    public class GoodsReceiptLineDetailModel
    {
        public Guid Id { get; set; }
        public Guid? PurchaseOrderLineId { get; set; }
        public Guid IngredientId { get; set; }
        public string? IngredientCode { get; set; }
        public string? IngredientName { get; set; }
        public string? StorageType { get; set; }
        public Guid UnitId { get; set; }
        public string? UnitName { get; set; }
        public decimal? OrderedQuantity { get; set; }
        public decimal ReceivedQuantity { get; set; }
        public decimal AcceptedQuantity { get; set; }
        public decimal RejectedQuantity { get; set; }
        public decimal? UnitCost { get; set; }
        public string? SupplierLotNumber { get; set; }
        public DateTime? ManufactureDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public Guid? QcInspectionLineId { get; set; }
        public string? QualityStatus { get; set; }
        public string? QualityStatusName { get; set; }
        public decimal? Temperature { get; set; }
        /// <summary>Nhiệt độ tối đa cho phép theo kiểu bảo quản (BusinessRules:ReceivingTempMaxC); trống = không giới hạn.</summary>
        public decimal? MaxAllowedTemperature { get; set; }
        public bool? TemperatureWithinRange { get; set; }
        /// <summary>Kết quả QC gợi ý (nhiệt độ vượt ngưỡng / hết hạn → KHONG_DAT) – người kiểm vẫn quyết định.</summary>
        public string? SuggestedResult { get; set; }
        public decimal? Weight { get; set; }
        public string? Appearance { get; set; }
        public string? Packaging { get; set; }
        public string? Remark { get; set; }
        /// <summary>Lô nội bộ NL-… (sau khi ghi sổ).</summary>
        public Guid? MaterialLotId { get; set; }
        public string? LotNumber { get; set; }
        public string? LotStatus { get; set; }
    }
}
