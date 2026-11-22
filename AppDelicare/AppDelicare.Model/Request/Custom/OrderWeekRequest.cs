using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace AppDelicare.Model.Request.Custom
{
    /// <summary>ORD-OPT-1.4: lưới tuần (GET) – khách, điểm giao, ngày bất kỳ trong tuần (quy về thứ Hai).</summary>
    public class OrderWeekRequest
    {
        [Required]
        public Guid CustomerId { get; set; }
        [Required]
        public Guid DeliveryLocationId { get; set; }
        [Required]
        public DateTime WeekStart { get; set; }
    }

    /// <summary>
    /// ORD-OPT-1.4 (D-74 g): lưu nháp / xác nhận cả tuần – mỗi ngày 1 đơn cho điểm giao. Gửi JSON.
    /// Ngày đã chốt / đang sản xuất / chờ duyệt bị bỏ qua và báo lại. <see cref="Confirm"/> = true → đơn chuyển Đã xác nhận.
    /// </summary>
    public class OrderWeekSaveRequest
    {
        [Required]
        public Guid CustomerId { get; set; }
        [Required]
        public Guid DeliveryLocationId { get; set; }
        [Required]
        public DateTime WeekStart { get; set; }
        public bool Confirm { get; set; }
        [MaxLength(1000)]
        public string? Note { get; set; }
        public List<OrderWeekCellRequest> ListCell { get; set; } = [];
    }

    public class OrderWeekCellRequest
    {
        [Required]
        public DateTime MealDate { get; set; }
        [Required]
        public Guid ContractMealId { get; set; }
        public Guid? ShiftId { get; set; }
        [Range(1, 9)]
        public int? MenuOptionNo { get; set; }
        public decimal Quantity { get; set; }
    }

    /// <summary>ORD-OPT-1.5: tải phiếu đăng ký (Excel) lên – đọc vào lưới, chưa lưu.</summary>
    public class OrderWeekSlipImportRequest
    {
        [Required]
        public Guid CustomerId { get; set; }
        [Required]
        public Guid DeliveryLocationId { get; set; }
        [Required]
        public DateTime WeekStart { get; set; }
        [Required]
        public IFormFile? File { get; set; }
    }
}
