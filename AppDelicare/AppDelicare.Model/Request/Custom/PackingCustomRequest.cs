using System.ComponentModel.DataAnnotations;

namespace AppDelicare.Model.Request.Custom
{
    /// <summary>BE2.1 (spec 10.1 GET packing-orders): lọc lệnh đóng gói theo ngày đóng gói / trạng thái / đơn / mẻ.</summary>
    public class PackingOrderListRequest
    {
        public DateTime? PackingDate { get; set; }
        [MaxLength(50)]
        public string? Status { get; set; }
        public Guid? OrderId { get; set; }
        public Guid? ProductionBatchId { get; set; }
    }

    /// <summary>
    /// BE2.1: sinh lệnh đóng gói từ mẻ Đạt QC (form) – số phần đạt còn lại của mẻ chia cho các đơn Đã chốt / Đang sản xuất của ngày + ca của mẻ
    /// theo tỉ lệ suất món trong đơn (GAP-PCK-01, PROPOSED – BA/PO CONFIRM).
    /// </summary>
    public class PackingOrderGenerateRequest
    {
        [Required]
        public Guid ProductionBatchId { get; set; }
        public DateTime? PackingDate { get; set; }
        [MaxLength(1000)]
        public string? Note { get; set; }
    }

    /// <summary>BE2.1 (spec 10.1 POST packing-orders): lập lệnh đóng gói tay (1 mẻ chia cho nhiều đơn / đóng bù) – form.</summary>
    public class PackingOrderCreateRequest
    {
        [Required]
        public Guid ProductionBatchId { get; set; }
        [Required]
        public Guid OrderId { get; set; }
        public decimal PlannedQty { get; set; }
        public DateTime? PackingDate { get; set; }
        [MaxLength(1000)]
        public string? Note { get; set; }
    }

    /// <summary>BE2.1 (spec 10.1 POST packing-orders/{id}/result): ghi kết quả đóng gói – đóng gói + hỏng + loại = số kế hoạch của lệnh (form).</summary>
    public class PackingOrderResultRequest
    {
        /// <summary>Id lệnh đóng gói.</summary>
        [Required]
        public Guid Id { get; set; }
        public Guid? ProductionOutputId { get; set; }
        public decimal PackedQuantity { get; set; }
        public decimal? DamagedQty { get; set; }
        public decimal? RejectedQuantity { get; set; }
        [MaxLength(200)]
        public string? PackagingType { get; set; }
        [MaxLength(50)]
        public string? QualityStatus { get; set; }
        [MaxLength(1000)]
        public string? Note { get; set; }
    }

    /// <summary>BE2.1: bắt đầu / hủy lệnh đóng gói (form). Hủy: lý do bắt buộc.</summary>
    public class PackingOrderActionRequest
    {
        [Required]
        public Guid Id { get; set; }
        [MaxLength(1000)]
        public string? Reason { get; set; }
    }
}
