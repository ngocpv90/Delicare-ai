namespace AppDelicare.Model.Model
{
    /// <summary>BE3.1 (Catalog 100): 1 dòng đối soát trong danh sách.</summary>
    public class ReconciliationListItemModel
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public string? OrderNumber { get; set; }
        public DateTime MealDate { get; set; }
        public Guid CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public DateTime ReconciliationDate { get; set; }
        public decimal OrderedQuantity { get; set; }
        public decimal DeliveredQuantity { get; set; }
        public decimal AcceptedQuantity { get; set; }
        public decimal? VarianceQty { get; set; }
        public decimal? VariancePercent { get; set; }
        public decimal? FinalAmount { get; set; }
        public string? Status { get; set; }
        public string? StatusName { get; set; }
    }

    /// <summary>BE3.1 (Catalog 100): 1 điểm biểu đồ Đặt – Giao – Nhận theo ngày ăn.</summary>
    public class ReconciliationChartPointModel
    {
        public DateTime Date { get; set; }
        public decimal Ordered { get; set; }
        public decimal Delivered { get; set; }
        public decimal Accepted { get; set; }
    }

    /// <summary>BE3.1 (Catalog 100): danh sách đối soát (phân trang) + biểu đồ cả kỳ (không phân trang) + số theo trạng thái.</summary>
    public class ReconciliationListModel
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public long TotalRecords { get; set; }
        public List<ReconciliationListItemModel> ListItem { get; set; } = [];
        public List<ReconciliationChartPointModel> Chart { get; set; } = [];
        public Dictionary<string, int> StatusCounts { get; set; } = [];
    }

    /// <summary>BE3.1 (Catalog 157): 1 nhóm hao hụt.</summary>
    public class WasteSummaryItemModel
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public decimal QuantityMeals { get; set; }
        public decimal QuantityKg { get; set; }
        /// <summary>Σ suất × giá thành chuẩn / suất của món (StandardCost hiệu lực) – null nếu không tính được dòng nào.</summary>
        public decimal? Cost { get; set; }
        public int RecordCount { get; set; }
    }

    /// <summary>BE3.1 (Catalog 157): tổng hợp hao hụt theo nhóm.</summary>
    public class WasteSummaryModel
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public string GroupBy { get; set; } = string.Empty;
        public decimal TotalMeals { get; set; }
        public decimal TotalKg { get; set; }
        public decimal? TotalCost { get; set; }
        /// <summary>Số dòng có suất hao hụt nhưng không tính được giá (không có món / chưa có giá thành chuẩn).</summary>
        public int RecordsWithoutCost { get; set; }
        public List<WasteSummaryItemModel> ListItem { get; set; } = [];
    }

    /// <summary>BE3.1 (Catalog 163): 1 điểm chuỗi nhiệt độ (gộp: nhiệt độ trung bình, min / max, đạt nếu mọi lần đo trong nhóm đạt).</summary>
    public class TemperaturePointModel
    {
        public DateTime MeasuredAt { get; set; }
        public decimal TemperatureC { get; set; }
        public decimal? MinC { get; set; }
        public decimal? MaxC { get; set; }
        public bool? IsWithinRange { get; set; }
        public int Count { get; set; } = 1;
    }

    /// <summary>BE3.1 (Catalog 163): khu vực + ngưỡng + chuỗi điểm.</summary>
    public class TemperatureSeriesModel
    {
        public Guid StorageZoneId { get; set; }
        public string? ZoneCode { get; set; }
        public string? ZoneName { get; set; }
        public decimal? TemperatureMin { get; set; }
        public decimal? TemperatureMax { get; set; }
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public int MeasurementCount { get; set; }
        public int OutOfRangeCount { get; set; }
        /// <summary>0 = từng lần đo; &gt; 0 = gộp theo khoảng N phút (khi &gt; giới hạn điểm).</summary>
        public int BucketMinutes { get; set; }
        public List<TemperaturePointModel> Points { get; set; } = [];
    }

    /// <summary>BE3.1 (Catalog 192 / 193): 1 sự kiện giao (timeline / vị trí).</summary>
    public class DeliveryEventItemModel
    {
        public Guid Id { get; set; }
        public Guid DeliveryRouteId { get; set; }
        public string? RouteNumber { get; set; }
        public string? VehiclePlate { get; set; }
        public string? DriverName { get; set; }
        public Guid? DeliveryStopId { get; set; }
        public int? StopSequenceNo { get; set; }
        public Guid? DeliveryOrderId { get; set; }
        public string? EventType { get; set; }
        public string? EventTypeName { get; set; }
        public DateTime EventAt { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public decimal? TemperatureC { get; set; }
        public string? PhotoUrl { get; set; }
        public string? Description { get; set; }
        public string? RecordedByName { get; set; }
        public string? SourceChannel { get; set; }
    }

    /// <summary>BE3.1 (Catalog 193): vị trí theo sự kiện của 1 ngày giao + vị trí mới nhất mỗi chuyến.</summary>
    public class VehiclePositionModel
    {
        public DateTime DeliveryDate { get; set; }
        public List<DeliveryEventItemModel> ListPosition { get; set; } = [];
        public List<DeliveryEventItemModel> ListLatest { get; set; } = [];
    }
}
