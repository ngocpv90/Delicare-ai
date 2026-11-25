namespace AppDelicare.Model.Model
{
    /// <summary>UI-6.34 (D-115): "Điều phối giao hàng" – cột = chuyến + "Chưa gán chuyến", thẻ = điểm giao (DeliveryRoute/GetDispatchBoard).</summary>
    public class DispatchBoardModel
    {
        public DateTime Date { get; set; }
        public int RoutesReady { get; set; }
        public int PointsComplete { get; set; }
        public int PointsTotal { get; set; }
        public List<DispatchRouteModel> ListRoute { get; set; } = [];
        public List<DispatchPointModel> ListUnassigned { get; set; } = [];
    }

    public class DispatchRouteModel
    {
        public Guid Id { get; set; }
        public string? RouteNo { get; set; }
        public string? Name { get; set; }
        public string? VehicleName { get; set; }
        public string? DriverName { get; set; }
        public DateTime? PlannedStart { get; set; }
        public string? Status { get; set; }
        public string? StatusName { get; set; }
        public int PointsComplete { get; set; }
        public bool CanLoad { get; set; }
        public bool Loaded { get; set; }
        public List<DispatchPointModel> ListPoint { get; set; } = [];
    }

    public class DispatchPointModel
    {
        public Guid DeliveryOrderId { get; set; }
        public Guid OrderId { get; set; }
        public string? DeliveryNumber { get; set; }
        public string? CustomerName { get; set; }
        public string? LocationName { get; set; }
        public decimal Meals { get; set; }
        public DateTime PlannedTime { get; set; }
        public bool Complete { get; set; }
        public List<string> ListMissing { get; set; } = [];
    }

    /// <summary>UI-6.34: chuyển điểm giao sang chuyến khác.</summary>
    public class DispatchMovePointRequest
    {
        public Guid DeliveryOrderId { get; set; }
        public Guid DeliveryRouteId { get; set; }
    }
}
