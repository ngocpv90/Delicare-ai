namespace AppDelicare.Model.Model
{
    /// <summary>UI-6.29 (D-110): "Sổ kiểm tra hàng nhập" – bộ lọc chung cho QcInspection/GetBook và GetSupplierSummary.</summary>
    public class QcBookRequest
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public string? Keyword { get; set; }
        /// <summary>DAT | DAT_MOT_PHAN | KHONG_DAT | CONDITIONAL (nhận có điều kiện) | PENDING (chưa kết luận).</summary>
        public string? Result { get; set; }
        public string? StorageType { get; set; }
        public Guid? SupplierId { get; set; }
        /// <summary>Chỉ dòng nhiệt độ ngoài vùng đạt (có điều kiện / loại / chưa đo).</summary>
        public bool OnlyTempOut { get; set; }
    }

    public class QcBookModel
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public QcBookSummaryModel Summary { get; set; } = new();
        public List<QcBookDayModel> ListDay { get; set; } = [];
    }

    public class QcBookSummaryModel
    {
        public int Receipts { get; set; }
        public int Lines { get; set; }
        public int Dat { get; set; }
        public int Conditional { get; set; }
        public int KhongDat { get; set; }
        public int Pending { get; set; }
        public int TempOut { get; set; }
        public int Suppliers { get; set; }
        public decimal? PassPct { get; set; }
    }

    public class QcBookDayModel
    {
        public DateTime Date { get; set; }
        public int Lines { get; set; }
        public int KhongDat { get; set; }
        public int TempOut { get; set; }
        public List<QcBookRowModel> ListRow { get; set; } = [];
    }

    public class QcBookRowModel
    {
        public Guid InspectionId { get; set; }
        public Guid InspectionLineId { get; set; }
        public string? InspectionNumber { get; set; }
        public DateTime InspectionDate { get; set; }
        public Guid GoodsReceiptId { get; set; }
        public string? ReceiptNumber { get; set; }
        public string? ReceiptStatus { get; set; }
        public Guid? PurchaseOrderId { get; set; }
        public Guid SupplierId { get; set; }
        public string? SupplierName { get; set; }
        public Guid IngredientId { get; set; }
        public string? IngredientName { get; set; }
        public string? StorageType { get; set; }
        public decimal? Temperature { get; set; }
        public string TempZone { get; set; } = string.Empty;
        public string TempZoneLabel { get; set; } = string.Empty;
        public DateTime? ExpiryDate { get; set; }
        public string? Appearance { get; set; }
        public string? Packaging { get; set; }
        /// <summary>DAT | DAT_MOT_PHAN | KHONG_DAT | null (chưa kết luận).</summary>
        public string? Result { get; set; }
        public bool Conditional { get; set; }
        public bool AutoPass { get; set; }
        public string? Remark { get; set; }
        public string? Inspector { get; set; }
        public decimal ReceivedQuantity { get; set; }
        public decimal AcceptedQuantity { get; set; }
        public decimal RejectedQuantity { get; set; }
        public Guid? LotId { get; set; }
        public string? LotNo { get; set; }
    }

    public class QcSupplierSummaryModel
    {
        public Guid SupplierId { get; set; }
        public string? SupplierName { get; set; }
        public int Receipts { get; set; }
        public int Lines { get; set; }
        public int Dat { get; set; }
        public int Conditional { get; set; }
        public int KhongDat { get; set; }
        public int TempOut { get; set; }
        public decimal? PassPct { get; set; }
        public DateTime? LastDate { get; set; }
    }

    /// <summary>UI-6.29 (D-110): đơn mua đến hạn nhận (khối đầu màn "Nhận hàng").</summary>
    public class PoDueModel
    {
        public Guid PurchaseOrderId { get; set; }
        public string? PurchaseOrderNumber { get; set; }
        public Guid SupplierId { get; set; }
        public string? SupplierName { get; set; }
        public DateTime? ExpectedDate { get; set; }
        public string? Status { get; set; }
        public bool Overdue { get; set; }
        public int LineCount { get; set; }
        public int RemainingLines { get; set; }
        public decimal? TotalAmount { get; set; }
    }
}
