using System.ComponentModel.DataAnnotations;

namespace AppDelicare.Model.Request.Custom
{
    /// <summary>
    /// BE2.1 (spec 9.3 POST temperature-logs, Catalog 165): ghi nhiệt độ tự do (form). Ít nhất 1 đối tượng đo; ngưỡng + IsWithinRange tính ở server.
    /// CORE / DELIVERY / RECEIVING ghi qua nghiệp vụ mẻ / chuyến / nhận hàng.
    /// </summary>
    public class TemperatureRecordRequest
    {
        public Guid? StorageZoneId { get; set; }
        public Guid? StorageLocationId { get; set; }
        public Guid? MaterialLotId { get; set; }
        public Guid? ProductionBatchId { get; set; }
        [Required]
        [MaxLength(40)]
        public string? MeasurementType { get; set; }
        [Required]
        public decimal? TemperatureC { get; set; }
        public DateTime? MeasuredAt { get; set; }
        [MaxLength(100)]
        public string? DeviceCode { get; set; }
        [MaxLength(50)]
        public string? SourceChannel { get; set; }
        [MaxLength(2000)]
        public string? CorrectiveAction { get; set; }
        [MaxLength(1000)]
        public string? EvidenceUrl { get; set; }
    }

    /// <summary>BE2.1 (spec 8.4 POST waste-records, Catalog 156): ghi hao hụt (form) – gắn mẻ / lệnh SX / món + ca.</summary>
    public class MealWasteRecordCreateRequest
    {
        public Guid? ProductionBatchId { get; set; }
        public Guid? ProductionOrderId { get; set; }
        public Guid? DishId { get; set; }
        public Guid? ShiftId { get; set; }
        [Required]
        [MaxLength(50)]
        public string? WasteType { get; set; }
        public decimal? QuantityMeals { get; set; }
        public decimal? QuantityKg { get; set; }
        [MaxLength(1000)]
        public string? Reason { get; set; }
        [MaxLength(500)]
        public string? Disposition { get; set; }
        public DateTime? OccurredAt { get; set; }
    }

    /// <summary>BE2.1: lọc hao hụt theo kỳ / món / ca / loại / mẻ.</summary>
    public class MealWasteListRequest
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public Guid? DishId { get; set; }
        public Guid? ShiftId { get; set; }
        public Guid? ProductionBatchId { get; set; }
        [MaxLength(50)]
        public string? WasteType { get; set; }
    }

    /// <summary>BE2.1 (spec 6.6 GET orders view=board, Catalog 90): bảng đơn theo trạng thái.</summary>
    public class OrderBoardRequest
    {
        public DateTime? MealDate { get; set; }
        public DateTime? MealDateFrom { get; set; }
        public DateTime? MealDateTo { get; set; }
        [MaxLength(50)]
        public string? Status { get; set; }
        public Guid? CustomerId { get; set; }
        /// <summary>Chi nhánh / đơn vị của khách (Customer.BranchId).</summary>
        public Guid? BranchId { get; set; }
        [MaxLength(200)]
        public string? SearchText { get; set; }
    }

    /// <summary>BE2.1 (spec 6.2 GET menus, Catalog 82): danh sách thực đơn + số liệu.</summary>
    public class MenuListRequest
    {
        [MaxLength(50)]
        public string? Status { get; set; }
        /// <summary>Thực đơn hiệu lực tại ngày này (EffectiveFrom ≤ ngày ≤ EffectiveTo, trống = không giới hạn).</summary>
        public DateTime? EffectiveOn { get; set; }
        [MaxLength(200)]
        public string? SearchText { get; set; }
    }

    /// <summary>BE2.1 (spec 10.5 GET complaints, Catalog 197): danh sách phản ánh + quá hạn.</summary>
    public class ComplaintListRequest
    {
        [MaxLength(50)]
        public string? Status { get; set; }
        [MaxLength(50)]
        public string? Severity { get; set; }
        public Guid? CustomerId { get; set; }
        /// <summary>true = chỉ phản ánh quá hạn SLA.</summary>
        public bool? Overdue { get; set; }
        /// <summary>BOT-3.1 (Việc tôi đã báo): true = chỉ phản ánh do chính người gọi ghi (CreatedBy).</summary>
        public bool? Mine { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        [MaxLength(200)]
        public string? SearchText { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    /// <summary>BE2.1 (spec 11.5 POST approvals/{id}/cancel, Catalog 213): người gửi hủy yêu cầu còn chờ duyệt (form).</summary>
    public class ApprovalCancelRequest
    {
        [Required]
        public Guid Id { get; set; }
        [MaxLength(1000)]
        public string? Reason { get; set; }
    }

    /// <summary>BE2.1 (spec 7.5 GET lots, Catalog 118): lọc lô.</summary>
    public class MaterialLotListRequest
    {
        public Guid? IngredientId { get; set; }
        public Guid? SupplierId { get; set; }
        [MaxLength(50)]
        public string? Status { get; set; }
        [MaxLength(50)]
        public string? QcStatus { get; set; }
        /// <summary>Lô còn ≤ N ngày tới hạn dùng (kể cả đã hết hạn).</summary>
        public int? ExpiringWithinDays { get; set; }
        [MaxLength(200)]
        public string? SearchText { get; set; }
        /// <summary>true = chỉ lô còn tồn.</summary>
        public bool? InStock { get; set; }
    }
}
