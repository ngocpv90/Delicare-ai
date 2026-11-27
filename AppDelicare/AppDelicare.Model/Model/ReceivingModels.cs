namespace AppDelicare.Model.Model
{
    /// <summary>UI-6.50 (D-132): thẻ nhà cung cấp trên màn Nhận hàng.</summary>
    public sealed class ReceivingSupplierCardModel
    {
        public Guid SupplierId { get; set; }
        public string? SupplierCode { get; set; }
        public string? SupplierName { get; set; }
        public int OrderCount { get; set; }
        public int LineCount { get; set; }
        public DateTime? EarliestExpected { get; set; }
        /// <summary>Quá giờ hẹn sớm nhất bao nhiêu phút (null = chưa quá).</summary>
        public int? OverdueMinutes { get; set; }
        public List<string> ListOrderNumber { get; set; } = [];
        /// <summary>Đã nhận đủ trong ngày (không còn dòng chờ) – thẻ làm mờ + "Xem phiếu".</summary>
        public bool ReceivedFull { get; set; }
        public int ReceiptCount { get; set; }
        public Guid? LastReceiptId { get; set; }
    }

    public sealed class ReceivingDueModel
    {
        public DateTime Date { get; set; }
        public int SupplierCount { get; set; }
        public int OrderCount { get; set; }
        public int LineCount { get; set; }
        public int ReceivedFullCount { get; set; }
        public int OverdueCount { get; set; }
        public int QcPendingCount { get; set; }
        public List<ReceivingSupplierCardModel> ListSupplier { get; set; } = [];
    }

    public sealed class ReceivingOrderModel
    {
        public Guid PurchaseOrderId { get; set; }
        public string? PurchaseOrderNumber { get; set; }
        public DateTime? ExpectedDate { get; set; }
        public int LineCount { get; set; }
        /// <summary>Hẹn giao ≤ ngày xem (mặc định chọn); false = đơn hẹn ngày khác được gom thêm.</summary>
        public bool Due { get; set; }
        /// <summary>Đơn được tìm / quét (QR) – luôn chọn.</summary>
        public bool Matched { get; set; }
    }

    public sealed class ReceivingPartModel
    {
        public Guid PurchaseOrderId { get; set; }
        public string? PurchaseOrderNumber { get; set; }
        public Guid PurchaseOrderLineId { get; set; }
        public decimal Ordered { get; set; }
        public decimal Remaining { get; set; }
    }

    public sealed class ReceivingLineModel
    {
        public string Key { get; set; } = string.Empty;
        public Guid IngredientId { get; set; }
        public string? IngredientCode { get; set; }
        public string? IngredientName { get; set; }
        public string? SupplierMaterialCode { get; set; }
        public Guid UnitId { get; set; }
        public string? UnitName { get; set; }
        public string? StorageType { get; set; }
        public bool LotControl { get; set; }
        public bool ExpiryControl { get; set; }
        public bool QcRequired { get; set; }
        public int? ShelfLifeDays { get; set; }
        public decimal? TempAcceptMax { get; set; }
        public decimal? TempConditionalMax { get; set; }
        public decimal Ordered { get; set; }
        public decimal Remaining { get; set; }
        /// <summary>Dòng đơn mua – đơn cũ trước.</summary>
        public List<ReceivingPartModel> ListPart { get; set; } = [];
    }

    public sealed class ReceivingBundleModel
    {
        public Guid SupplierId { get; set; }
        public string? SupplierCode { get; set; }
        public string? SupplierName { get; set; }
        /// <summary>supplier | order | invoice | keyword (cách tìm ra NCC).</summary>
        public string? MatchedBy { get; set; }
        public string? MatchedText { get; set; }
        public Guid? DefaultWarehouseId { get; set; }
        public bool NeedsVehicleTemperature { get; set; }
        public List<ReceivingOrderModel> ListOrder { get; set; } = [];
        public List<ReceivingLineModel> ListLine { get; set; } = [];
    }

    public sealed class ReceivingManyResultModel
    {
        public List<GoodsReceiptDetailModel> ListReceipt { get; set; } = [];
        public int PostedCount { get; set; }
        public int QcPendingCount { get; set; }
        public int IssueCount { get; set; }
        public List<string> ListWarning { get; set; } = [];
    }

    /// <summary>Excel "Phiếu giao hàng" NCC tải lên → dòng điền form (không lưu tệp).</summary>
    public sealed class DeliveryNoteRowModel
    {
        public int Row { get; set; }
        public string? PurchaseOrderNumber { get; set; }
        public string? ItemCode { get; set; }
        public string? ItemName { get; set; }
        public decimal? Quantity { get; set; }
        public string? SupplierLotNumber { get; set; }
        public DateTime? ManufactureDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public Guid? IngredientId { get; set; }
        public Guid? PurchaseOrderLineId { get; set; }
        public string? Problem { get; set; }
    }
}

namespace AppDelicare.Model.Request.Custom
{
    /// <summary>UI-6.50 (D-132): 1 dòng gộp theo nguyên liệu – máy chủ chia về từng dòng đơn mua (đơn cũ trước).</summary>
    public sealed class ReceivingLineInput
    {
        public Guid IngredientId { get; set; }
        public List<Guid> ListPurchaseOrderLineId { get; set; } = [];
        public decimal ReceivedQuantity { get; set; }
        public string? SupplierLotNumber { get; set; }
        public DateTime? ManufactureDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public decimal? TemperatureC { get; set; }
        /// <summary>DAT / DAT_MOT_PHAN / KHONG_DAT – chỉ dùng khi <c>Conclude</c> (người có quyền Duyệt Kiểm tra hàng nhập).</summary>
        public string? Result { get; set; }
        public decimal? AcceptedQuantity { get; set; }
        public string? Appearance { get; set; }
        public string? Packaging { get; set; }
        public string? Remark { get; set; }
        public bool? NoThawConfirmed { get; set; }
    }

    public sealed class GoodsReceiptCreateManyRequest
    {
        public Guid WarehouseId { get; set; }
        public DateTime? ReceiptDate { get; set; }
        /// <summary>Nhiệt độ thùng xe – áp cho dòng mát / đông lạnh không đo riêng.</summary>
        public decimal? VehicleTemperatureC { get; set; }
        /// <summary>Số hóa đơn / phiếu giao NCC – ghi chú mỗi phiếu nhập dạng "[HĐ NCC] số".</summary>
        public string? SupplierInvoiceNo { get; set; }
        public string? Note { get; set; }
        /// <summary>true = kết luận kiểm tra ngay (QA tại cửa kho – quyền Duyệt qc-inspection); false = gửi QA như UI-6.29.</summary>
        public bool Conclude { get; set; }
        public List<ReceivingLineInput> ListLine { get; set; } = [];
    }
}
