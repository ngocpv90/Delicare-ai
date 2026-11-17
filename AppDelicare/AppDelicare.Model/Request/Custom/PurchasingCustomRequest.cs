using System.ComponentModel.DataAnnotations;

namespace AppDelicare.Model.Request.Custom
{
    /// <summary>T15: thao tác trạng thái 1 chứng từ mua hàng (form): Id + ghi chú (nối vào Note).</summary>
    public class PurchaseActionRequest
    {
        [Required]
        public Guid Id { get; set; }
        [MaxLength(1000)]
        public string? Note { get; set; }
    }

    /// <summary>T15: hủy đề nghị mua / PO / phiếu nhập (form) – lý do bắt buộc.</summary>
    public class PurchaseCancelRequest
    {
        [Required]
        public Guid Id { get; set; }
        [MaxLength(1000)]
        public string? Reason { get; set; }
    }

    /// <summary>
    /// T15 (spec 7.2 convert-to-po): lập PO từ đề nghị mua (form) – nhóm dòng theo NCC gợi ý (SupplierMaterial ưu tiên → giá → lead time),
    /// mỗi NCC 1 PO Nháp. Bỏ trống OrderDate = hôm nay.
    /// </summary>
    public class PurchaseRequestConvertRequest
    {
        [Required]
        public Guid Id { get; set; }
        public DateTime? OrderDate { get; set; }
        [MaxLength(1000)]
        public string? Note { get; set; }
    }

    /// <summary>T15: sửa toàn bộ dòng PO Nháp (JSON – danh sách lồng nhau). Dòng không có trong danh sách bị xóa (mềm).</summary>
    public class PurchaseOrderUpdateLinesRequest
    {
        [Required]
        public Guid Id { get; set; }
        public DateTime? ExpectedDeliveryDate { get; set; }
        [MaxLength(2000)]
        public string? Note { get; set; }
        public List<PurchaseOrderLineInput> ListLine { get; set; } = [];
    }

    public class PurchaseOrderLineInput
    {
        /// <summary>Dòng có sẵn (trống = dòng mới).</summary>
        public Guid? Id { get; set; }
        [Required]
        public Guid IngredientId { get; set; }
        public decimal Quantity { get; set; }
        /// <summary>Trống = đơn vị mua của bảng giá NCC, không có thì đơn vị cơ sở của nguyên liệu.</summary>
        public Guid? UnitId { get; set; }
        /// <summary>Trống = đơn giá bảng giá NCC (SupplierMaterial) đang hiệu lực, không có thì 0.</summary>
        public decimal? UnitPrice { get; set; }
        [MaxLength(1000)]
        public string? Note { get; set; }
    }

    /// <summary>
    /// T15 (spec 7.3 receive): lập phiếu nhập theo PO đã duyệt (JSON – danh sách dòng). Mỗi dòng = 1 dòng PO; nhiệt độ nhận hàng (°C) ghi theo dòng
    /// (TemperatureLog RECEIVING lúc ghi sổ). Bỏ trống ReceiptDate = bây giờ; ExpiryDate trống = ngày nhận + Ingredient.ShelfLifeDays (nếu có).
    /// </summary>
    public class GoodsReceiptCreateRequest
    {
        [Required]
        public Guid PurchaseOrderId { get; set; }
        [Required]
        public Guid WarehouseId { get; set; }
        public DateTime? ReceiptDate { get; set; }
        [MaxLength(2000)]
        public string? Note { get; set; }
        public List<GoodsReceiptLineInput> ListLine { get; set; } = [];
    }

    public class GoodsReceiptLineInput
    {
        [Required]
        public Guid PurchaseOrderLineId { get; set; }
        public decimal ReceivedQuantity { get; set; }
        /// <summary>Số lô của NCC (in trên bao bì). Lô nội bộ NL-… sinh khi ghi sổ.</summary>
        [MaxLength(255)]
        public string? SupplierLotNumber { get; set; }
        public DateTime? ManufactureDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        /// <summary>Nhiệt độ đo khi nhận (°C).</summary>
        public decimal? TemperatureC { get; set; }
    }

    /// <summary>T15 (spec 7.4 post): ghi sổ phiếu nhập đã kết luận QC (form). StorageLocationId trống = không gắn vị trí.</summary>
    public class GoodsReceiptPostRequest
    {
        [Required]
        public Guid Id { get; set; }
        public Guid? StorageLocationId { get; set; }
        [MaxLength(1000)]
        public string? Note { get; set; }
    }

    /// <summary>
    /// T15 (spec 9.1 conclude): kết luận QC đầu vào từng dòng (JSON). Kết quả DAT / DAT_MOT_PHAN / KHONG_DAT (nhận cả PASS / HOLD / REJECT).
    /// DAT_MOT_PHAN bắt buộc AcceptedQuantity (0 &lt; đạt &lt; nhận) và Remark; KHONG_DAT bắt buộc Remark.
    /// </summary>
    public class QcInspectionConcludeRequest
    {
        /// <summary>QCInspection.Id.</summary>
        [Required]
        public Guid Id { get; set; }
        public Guid? InspectorEmployeeId { get; set; }
        [MaxLength(2000)]
        public string? Note { get; set; }
        public List<QcInspectionLineInput> ListLine { get; set; } = [];
    }

    public class QcInspectionLineInput
    {
        [Required]
        public Guid GoodsReceiptLineId { get; set; }
        [Required]
        [MaxLength(50)]
        public string Result { get; set; } = string.Empty;
        /// <summary>Chỉ dùng cho DAT_MOT_PHAN.</summary>
        public decimal? AcceptedQuantity { get; set; }
        /// <summary>Trống = giữ nhiệt độ ghi lúc nhận.</summary>
        public decimal? Temperature { get; set; }
        public decimal? Weight { get; set; }
        [MaxLength(1000)]
        public string? Appearance { get; set; }
        [MaxLength(1000)]
        public string? Packaging { get; set; }
        [MaxLength(1000)]
        public string? Remark { get; set; }
        /// <summary>
        /// T15.1: hàng đông lạnh nhận có điều kiện (−18 &lt; nhiệt độ ≤ −15 °C) – người kiểm xác nhận "không có dấu hiệu rã đông" (bắt buộc khi kết luận đạt).
        /// Lưu vào QCInspectionLine.Note (không có cột riêng).
        /// </summary>
        public bool? NoThawConfirmed { get; set; }
    }
}
