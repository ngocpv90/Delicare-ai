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
        /// <summary>BE2.3 (D-46): yêu cầu duyệt 2 cấp đang mở (cấp đang chờ, mục quyền, hạn) – null khi không chờ duyệt.</summary>
        public ApprovalProgressModel? PendingApproval { get; set; }
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
        /// <summary>UI-6.18: nguồn ("Tự lập / từ đề nghị" / "Lập tay") + đã tự duyệt.</summary>
        public string? Source { get; set; }
        public bool AutoApproved { get; set; }
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
        /// <summary>UI-6.18 (D-99): dòng chưa có NCC đủ điều kiện – chưa lập PO, chờ chọn ở màn Chọn nhà cung cấp.</summary>
        public List<SupplierPickLineModel> ListNeedPick { get; set; } = [];
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
        /// <summary>T15.1 (D-11): tổng tiền ≥ ngưỡng GĐ duyệt → chỉ người có quyền mục "Phê duyệt › Đơn mua hàng (Giám đốc)" duyệt được.</summary>
        public bool NeedsDirectorApproval { get; set; }
        /// <summary>T15.1: mục quyền duyệt áp cho PO này: <c>scm-po</c> hoặc <c>scm-po-director</c> (MetaLink <c>#/wf-inbox?type=…</c>).</summary>
        public string? ApprovalScreenId { get; set; }
        /// <summary>BE2.3: yêu cầu duyệt đang mở (cấp đang chờ / tổng số cấp, mục quyền, hạn) – null khi không chờ duyệt.</summary>
        public ApprovalProgressModel? PendingApproval { get; set; }
        /// <summary>T15.1 (VAT phương án A – tính khi đọc, KHÔNG lưu DB, GAP-PUR-09): tổng chưa VAT = Σ thành tiền dòng.</summary>
        public decimal SubtotalAmount { get; set; }
        /// <summary>Σ tiền VAT các dòng (mỗi dòng làm tròn đồng).</summary>
        public decimal VatAmount { get; set; }
        /// <summary>Tổng tiền gồm VAT = SubtotalAmount + VatAmount.</summary>
        public decimal TotalAmountInclVat { get; set; }
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
        /// <summary>T15.1 (tính khi đọc, không lưu – GAP-PUR-09): % VAT theo nhóm nguyên liệu / mặc định (BusinessRules:PurchaseVat).</summary>
        public decimal VatPct { get; set; }
        /// <summary>LineAmount × VatPct / 100, làm tròn đồng.</summary>
        public decimal VatAmount { get; set; }
        public decimal AmountInclVat { get; set; }
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
        /// <summary>Nhiệt độ tối đa để "đạt" theo kiểu bảo quản (BusinessRules:ReceivingTemp.AcceptMax); trống = không kiểm.</summary>
        public decimal? MaxAllowedTemperature { get; set; }
        /// <summary>T15.1: nhiệt độ tối đa vùng "nhận có điều kiện" (ReceivingTemp.ConditionalMax); lớn hơn → loại.</summary>
        public decimal? ConditionalMaxTemperature { get; set; }
        public bool? TemperatureWithinRange { get; set; }
        /// <summary>T15.1: ACCEPT / CONDITIONAL / REJECT / MISSING / NOT_CHECKED (ReceivingTempZone).</summary>
        public string? TemperatureZone { get; set; }
        public string? TemperatureZoneName { get; set; }
        /// <summary>T15.1: vùng có điều kiện của hàng đông lạnh → khi kết luận đạt phải gửi <c>noThawConfirmed = true</c>.</summary>
        public bool NoThawConfirmationRequired { get; set; }
        /// <summary>T15.1: lô nhận có điều kiện – "ưu tiên dùng trước" (đánh dấu trong Note, GAP-PUR-10).</summary>
        public bool UseFirst { get; set; }
        /// <summary>Kết quả QC gợi ý (nhiệt độ vùng loại / hết hạn → KHONG_DAT) – người kiểm vẫn quyết định.</summary>
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

namespace AppDelicare.Model.Model
{
    /// <summary>UI-6.18 (D-99): gợi ý NCC theo dòng (SupplierPick) – dùng cho form đề nghị, tự lập PO, màn Chọn nhà cung cấp.</summary>
    public class SupplierPickLineModel
    {
        public Guid Key { get; set; }
        public Guid IngredientId { get; set; }
        public string? IngredientName { get; set; }
        public string? CategoryName { get; set; }
        public decimal Quantity { get; set; }
        public Guid UnitId { get; set; }
        public string? UnitName { get; set; }
        public DateTime NeedDate { get; set; }
        public Guid? SupplierId { get; set; }
        public string? SupplierName { get; set; }
        public Guid? PriceId { get; set; }
        /// <summary>Đơn giá quy về đơn vị của dòng.</summary>
        public decimal? UnitPrice { get; set; }
        public decimal? OrderQuantity { get; set; }
        /// <summary>Lý do "Cần chọn NCC" (hoặc lý do đổi NCC ưu tiên).</summary>
        public string? Reason { get; set; }
        public decimal StockQuantity { get; set; }
        public bool BelowMinimum { get; set; }
        public Guid? PurchaseRequestId { get; set; }
        public string? PurchaseRequestNumber { get; set; }
        public Guid? PurchaseRequestLineId { get; set; }
        public List<SupplierPickCardModel> ListCard { get; set; } = [];
        /// <summary>UI-6.51 (D-137 g, GAP-PUR-SPLIT): chia số lượng cho nhiều NCC theo tỷ lệ + bù lệch lũy kế tháng; trống = 1 NCC như cũ.</summary>
        public List<SupplierSplitModel> ListSplit { get; set; } = [];
        /// <summary>UI-6.51: người mua đã sửa phần chia (không còn đúng định mức để tự duyệt).</summary>
        public bool SplitManual { get; set; }
    }

    /// <summary>UI-6.51 (D-137 g): một phần chia của dòng cho một NCC.</summary>
    public class SupplierSplitModel
    {
        public Guid SupplierId { get; set; }
        public string? SupplierName { get; set; }
        public Guid? PriceId { get; set; }
        public decimal? UnitPrice { get; set; }
        public int? LeadTimeDays { get; set; }
        public bool IsPreferred { get; set; }
        /// <summary>Tỷ lệ mục tiêu (bảng giá), lũy kế tháng (% số lượng PO không hủy), tỷ lệ lần này.</summary>
        public decimal TargetPct { get; set; }
        public decimal? CumPct { get; set; }
        public decimal Pct { get; set; }
        public decimal Quantity { get; set; }
        public decimal? MinQuantity { get; set; }
        public string? Note { get; set; }
    }

    public class SupplierPickCardModel
    {
        public Guid SupplierId { get; set; }
        public string? SupplierName { get; set; }
        public Guid? PriceId { get; set; }
        public decimal? UnitPrice { get; set; }
        public string? PriceUnitName { get; set; }
        public decimal? PricePerLineUnit { get; set; }
        public decimal OrderQuantity { get; set; }
        public decimal? Amount { get; set; }
        public int? LeadTimeDays { get; set; }
        public decimal? OnTimePct { get; set; }
        public int IssueCount90 { get; set; }
        public decimal? ChangePct { get; set; }
        public bool IsPreferred { get; set; }
        public bool Eligible { get; set; }
        public bool Suggested { get; set; }
        public string? Reason { get; set; }
        public string? Note { get; set; }
    }
}

namespace AppDelicare.Model.Request.Custom
{
    /// <summary>UI-6.18 A (D-99): tạo đề nghị mua kèm dòng.</summary>
    public class PurchaseRequestCreateRequest
    {
        public DateTime? RequiredDate { get; set; }
        public string? Note { get; set; }
        public bool Submit { get; set; }
        public List<PurchaseRequestLineInput> ListLine { get; set; } = [];
    }

    public class PurchaseRequestUpdateLinesRequest
    {
        public Guid Id { get; set; }
        public DateTime? RequiredDate { get; set; }
        public string? Note { get; set; }
        public List<PurchaseRequestLineInput> ListLine { get; set; } = [];
    }

    public class PurchaseRequestLineInput
    {
        public Guid? Key { get; set; }
        public Guid IngredientId { get; set; }
        public decimal Quantity { get; set; }
        public Guid? UnitId { get; set; }
        public DateTime? RequiredDate { get; set; }
        public string? Note { get; set; }
    }

    public class SupplierSuggestRequest
    {
        public DateTime? RequiredDate { get; set; }
        public List<PurchaseRequestLineInput> ListLine { get; set; } = [];
    }

    /// <summary>UI-6.18 D: màn Chọn nhà cung cấp – lập đơn mua từ các dòng đã chọn NCC.</summary>
    public class PurchasePickCreateRequest
    {
        public List<PurchasePickLineInput> ListLine { get; set; } = [];
    }

    public class PurchasePickLineInput
    {
        public Guid PurchaseRequestLineId { get; set; }
        public Guid SupplierId { get; set; }
    }

    public class PurchaseOrderSendManyRequest
    {
        public List<Guid> ListId { get; set; } = [];
    }
}

namespace AppDelicare.Model.Model
{
    /// <summary>UI-6.18 D (D-99): màn Đơn mua hàng – Chọn nhà cung cấp + Chờ gửi.</summary>
    public class SupplierPickBoardRequest
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
    }

    public class SupplierPickBoardModel
    {
        public int NeedPickCount { get; set; }
        public int ToSendCount { get; set; }
        public int PendingApprovalCount { get; set; }
        public decimal PickedAmount { get; set; }
        public List<SupplierPickLineModel> ListLine { get; set; } = [];
        public List<PurchaseOrderBriefModel> ListUnsent { get; set; } = [];
    }
}
