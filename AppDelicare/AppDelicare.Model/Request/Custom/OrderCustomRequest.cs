using System.ComponentModel.DataAnnotations;

namespace AppDelicare.Model.Request.Custom
{
    /// <summary>
    /// T13 (spec 6.6): tạo đơn kèm dòng suất. Gửi JSON (có danh sách dòng lồng nhau – CLAUDE.md 4.2).
    /// 1 đơn = 1 điểm giao (OrderLine không có cột điểm giao – GAP-ORD-01); dòng có DeliveryLocationId khác đầu đơn → lỗi.
    /// </summary>
    public class OrderCreateRequest
    {
        [Required]
        public Guid CustomerId { get; set; }
        /// <summary>Bỏ trống = hợp đồng Active hiệu lực tại MealDate.</summary>
        public Guid? ContractId { get; set; }
        [Required]
        public Guid DeliveryLocationId { get; set; }
        [Required]
        public DateTime MealDate { get; set; }
        /// <summary>Bỏ trống = MealDate.</summary>
        public DateTime? DeliveryDate { get; set; }
        [MaxLength(2000)]
        public string? Note { get; set; }
        public List<OrderCreateLineRequest> ListLine { get; set; } = [];
    }

    public class OrderCreateLineRequest
    {
        [Required]
        public Guid ContractMealId { get; set; }
        /// <summary>Bỏ trống = ca mặc định theo loại suất (BusinessRules:MealTypeDefaultShift).</summary>
        public Guid? ShiftId { get; set; }
        public decimal Quantity { get; set; }
        /// <summary>Chỉ để kiểm tra khớp điểm giao của đầu đơn (có thể bỏ trống).</summary>
        public Guid? DeliveryLocationId { get; set; }
        [MaxLength(1000)]
        public string? Note { get; set; }
    }

    /// <summary>T13 (spec 6.6 PUT lines): sửa số suất các dòng đã có. Số lượng 0 = bỏ dòng. Gửi JSON (danh sách lồng nhau).</summary>
    public class OrderUpdateLinesRequest
    {
        [Required]
        public Guid Id { get; set; }
        [MaxLength(1000)]
        public string? Note { get; set; }
        public List<OrderLineQuantityRequest> ListLine { get; set; } = [];
    }

    public class OrderLineQuantityRequest
    {
        [Required]
        public Guid Id { get; set; }
        public decimal Quantity { get; set; }
    }

    /// <summary>T13: Submit / Confirm / Lock 1 đơn (form).</summary>
    public class OrderActionRequest
    {
        [Required]
        public Guid Id { get; set; }
        [MaxLength(1000)]
        public string? Note { get; set; }
    }

    /// <summary>T13: hủy đơn – lý do bắt buộc (form).</summary>
    public class OrderCancelRequest
    {
        [Required]
        public Guid Id { get; set; }
        [MaxLength(1000)]
        public string? Reason { get; set; }
    }

    /// <summary>T13: nút "Chốt đơn 20h" (thay job OrderLock trong Pilot) – khóa mọi đơn Confirmed của ngày ăn (form).</summary>
    public class OrderLockByMealDateRequest
    {
        [Required]
        public DateTime MealDate { get; set; }
    }
}
