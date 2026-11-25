namespace AppDelicare.Model.Model
{
    /// <summary>UI-6.35 (D-116): thu khay kỳ trước khi bàn giao (DeliveryStop/Deliver ListReturn).</summary>
    public class DeliveryReturnItem
    {
        public string? PackagingType { get; set; }
        public int ReturnedQty { get; set; }
        public int DamagedQty { get; set; }
    }

    /// <summary>UI-6.35: dữ liệu form "Bàn giao" của tài xế (DeliveryStop/GetHandover).</summary>
    public class StopHandoverModel
    {
        public Guid StopId { get; set; }
        public string? CustomerName { get; set; }
        public string? LocationName { get; set; }
        public decimal PlannedMeals { get; set; }
        public string? ContactName { get; set; }
        public string? ContactPhone { get; set; }
        public string? DefaultPackagingType { get; set; }
        public List<StopTrayModel> ListTray { get; set; } = [];
    }

    public class StopTrayModel
    {
        public string PackagingType { get; set; } = string.Empty;
        public string? Label { get; set; }
        /// <summary>Số khay còn ở khách (sổ bao bì) – điền sẵn số thu.</summary>
        public decimal AtCustomer { get; set; }
    }

    /// <summary>UI-6.35: "Theo dõi giao hàng" – mỗi dòng một chuyến + dải điểm (DeliveryRoute/GetTrackBoard).</summary>
    public class TrackBoardModel
    {
        public DateTime Date { get; set; }
        public int RoutesInTransit { get; set; }
        public int PointsDelivered { get; set; }
        public int PointsTotal { get; set; }
        public int PointsLate { get; set; }
        public int PointsFailed { get; set; }
        public List<TrackRouteModel> ListRoute { get; set; } = [];
    }

    public class TrackRouteModel
    {
        public Guid Id { get; set; }
        public string? RouteNo { get; set; }
        public string? VehicleName { get; set; }
        public string? DriverName { get; set; }
        public Guid? DriverId { get; set; }
        public DateTime? PlannedStart { get; set; }
        public DateTime? ActualStart { get; set; }
        public decimal? DispatchTemperature { get; set; }
        public string? Status { get; set; }
        public string? StatusName { get; set; }
        public string? LastPosition { get; set; }
        public DateTime? LastPositionAt { get; set; }
        public List<TrackStopModel> ListStop { get; set; } = [];
    }

    public class TrackStopModel
    {
        public Guid StopId { get; set; }
        public Guid? DeliveryOrderId { get; set; }
        public int SequenceNo { get; set; }
        public string? CustomerName { get; set; }
        public string? LocationName { get; set; }
        public string? Address { get; set; }
        /// <summary>PLANNED | ARRIVED | DELIVERED | FAILED.</summary>
        public string Status { get; set; } = "PLANNED";
        public bool Late { get; set; }
        public DateTime? PlannedArrival { get; set; }
        public DateTime? ActualArrival { get; set; }
        public DateTime? WindowEnd { get; set; }
        public decimal PlannedMeals { get; set; }
        public decimal? Delivered { get; set; }
        public decimal? Accepted { get; set; }
        public decimal TraysReturned { get; set; }
        public string? FailReason { get; set; }
    }

    public class PodBookRequest
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public Guid? CustomerId { get; set; }
        public Guid? RouteId { get; set; }
    }

    public class PodBookRowModel
    {
        public Guid Id { get; set; }
        public DateTime? ReceivedAt { get; set; }
        public string? RouteNo { get; set; }
        public string? DeliveryNumber { get; set; }
        public string? CustomerName { get; set; }
        public string? LocationName { get; set; }
        public decimal Delivered { get; set; }
        public decimal Accepted { get; set; }
        public decimal Rejected { get; set; }
        public string? ReceiverName { get; set; }
        public decimal? Temperature { get; set; }
        public string? PhotoUrl { get; set; }
        public string? SignatureUrl { get; set; }
        public Guid DeliveryOrderId { get; set; }
    }
}
