namespace AppDelicare.Model.Model
{
    /// <summary>ACC-5 (D-131 i): trạng thái phiếu trả hàng NCC (hiển thị) – lưu IssueStatus chuẩn của SupplierIssue + "st" trong thẻ [TRA_NCC:…].</summary>
    public static class SupplierReturnState
    {
        public const string Pending = "RETURN_PENDING";   // Thủ kho lập – lô tạm giữ, chờ Mua hàng xác nhận NCC
        public const string Agreed = "RETURN_AGREED";     // NCC đồng ý – chờ NCC nhận hàng để ghi sổ
        public const string Refused = "RETURN_REFUSED";   // NCC không nhận – lô bỏ tạm giữ
        public const string Posted = "RETURN_POSTED";     // Đã ghi sổ xuất trả – chứng từ "Trả lại hàng mua"
        public const string Cancelled = "RETURN_CANCELLED";

        public static string Label(string? s) => s switch
        {
            Pending => "Chờ NCC xác nhận", Agreed => "NCC đồng ý – chờ giao trả", Refused => "NCC không nhận", Posted => "Đã trả – đã ghi sổ", Cancelled => "Đã hủy", _ => s ?? string.Empty,
        };
    }

    public sealed class SupplierReturnLotModel
    {
        public Guid MaterialLotId { get; set; }
        public string? LotNo { get; set; }
        public Guid IngredientId { get; set; }
        public string? IngredientCode { get; set; }
        public string? IngredientName { get; set; }
        public Guid WarehouseId { get; set; }
        public string? WarehouseName { get; set; }
        public Guid UnitId { get; set; }
        public string? UnitName { get; set; }
        public Guid? SupplierId { get; set; }
        public string? SupplierName { get; set; }
        public Guid? GoodsReceiptId { get; set; }
        public string? GoodsReceiptNumber { get; set; }
        public decimal? ReceivedQuantity { get; set; }
        public decimal OnHand { get; set; }
        public decimal? UnitCost { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string? LotStatus { get; set; }
    }

    public sealed class SupplierReturnHistoryModel
    {
        public string Action { get; set; } = string.Empty;
        public string ActionName { get; set; } = string.Empty;
        public string? By { get; set; }
        public DateTime At { get; set; }
        public string? Note { get; set; }
    }

    public sealed class SupplierReturnModel
    {
        public Guid Id { get; set; }
        public string Number { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string StateName { get; set; } = string.Empty;
        public DateTime ReportedAt { get; set; }
        public Guid SupplierId { get; set; }
        public string? SupplierCode { get; set; }
        public string? SupplierName { get; set; }
        public Guid? GoodsReceiptId { get; set; }
        public string? GoodsReceiptNumber { get; set; }
        public Guid? LinkedIssueId { get; set; }
        public Guid MaterialLotId { get; set; }
        public string? LotNo { get; set; }
        public string? IngredientCode { get; set; }
        public string? IngredientName { get; set; }
        public string? WarehouseName { get; set; }
        public string? UnitName { get; set; }
        public decimal? ReceivedQuantity { get; set; }
        public decimal OnHand { get; set; }
        public decimal Quantity { get; set; }
        public decimal? UnitCost { get; set; }
        public decimal? Amount { get; set; }
        public string? ReasonCode { get; set; }
        public string? ReasonName { get; set; }
        public string? Note { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime? PostedAt { get; set; }
        public bool CanConfirm { get; set; }
        public bool CanPost { get; set; }
        public bool CanCancel { get; set; }
        public List<SupplierReturnHistoryModel> ListHistory { get; set; } = [];
    }

    public sealed class SupplierReturnListModel
    {
        public int Pending { get; set; }
        public int Agreed { get; set; }
        public int PostedThisMonth { get; set; }
        public decimal PostedAmountThisMonth { get; set; }
        public List<SupplierReturnModel> ListData { get; set; } = [];
        public List<ReferenceOptionModel> ListReason { get; set; } = [];
    }

    public sealed class ReferenceOptionModel
    {
        public string Code { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }

    public sealed class SupplierReturnCreateRequest
    {
        public Guid MaterialLotId { get; set; }
        public Guid? WarehouseId { get; set; }
        public decimal Quantity { get; set; }
        public string? Reason { get; set; }
        public string? Note { get; set; }
        public Guid? LinkedIssueId { get; set; }
    }

    public sealed class SupplierReturnActionRequest
    {
        public Guid Id { get; set; }
        /// <summary>Confirm: true = NCC đồng ý, false = không nhận.</summary>
        public bool? Agree { get; set; }
        public string? Note { get; set; }
        public DateTime? Date { get; set; }
    }
}
