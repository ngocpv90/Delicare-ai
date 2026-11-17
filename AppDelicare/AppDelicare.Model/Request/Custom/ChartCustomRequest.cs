using System.ComponentModel.DataAnnotations;

namespace AppDelicare.Model.Request.Custom
{
    /// <summary>BE3.1 (Catalog 100, spec 6.7): danh sách đối soát + biểu đồ Đặt – Giao – Nhận theo ngày ăn (query). Trống from / to = 30 ngày gần nhất.</summary>
    public class ReconciliationListRequest
    {
        /// <summary>Ngày ăn từ (Order.MealDate).</summary>
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        /// <summary>Open / Matched / Variance / Closed.</summary>
        [MaxLength(50)]
        public string? Status { get; set; }
        public Guid? CustomerId { get; set; }
        [MaxLength(200)]
        public string? SearchText { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    /// <summary>BE3.1 (Catalog 157, spec 8.4): tổng hợp hao hụt cho biểu đồ (query). Trống from / to = 30 ngày gần nhất.</summary>
    public class MealWasteSummaryRequest
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        /// <summary>wasteType (mặc định) / dish / shift / date.</summary>
        [MaxLength(20)]
        public string? GroupBy { get; set; }
    }

    /// <summary>BE3.1 (Catalog 163, spec 9.3): chuỗi nhiệt độ 1 khu vực kho cho biểu đồ (query). Trống from / to = 48 giờ gần nhất.</summary>
    public class TemperatureSeriesRequest
    {
        [Required]
        public Guid StorageZoneId { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
    }

    /// <summary>BE3.1 (Catalog 193, spec 10.3): vị trí xe theo sự kiện giao của 1 ngày giao (query) – GAP-F-05, không phải GPS liên tục.</summary>
    public class VehiclePositionRequest
    {
        public DateTime? DeliveryDate { get; set; }
        public Guid? RouteId { get; set; }
    }

    /// <summary>BE3.1 (Catalog 193): tài xế ghi vị trí hiện tại của chuyến đang chạy (form) – DeliveryEvent EventType = POSITION.</summary>
    public class DeliveryPositionRequest
    {
        /// <summary>Id chuyến (DeliveryRoute).</summary>
        [Required]
        public Guid RouteId { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        [MaxLength(1000)]
        public string? Note { get; set; }
    }
}
