using AppDelicare.Model.Entity;

namespace AppDelicare.Model.Model
{
    /// <summary>T19 (spec 10.2, Pilot P8): chi tiết chuyến xe – xe, tài xế, điểm dừng, sự kiện / nhiệt độ, tải, điều kiện xuất phát còn thiếu, thao tác được phép.</summary>
    public class DeliveryRouteDetailModel
    {
        public DeliveryRoute Route { get; set; } = null!;
        public string? StatusName { get; set; }
        public string? VehicleCode { get; set; }
        public string? LicensePlate { get; set; }
        public decimal? Capacity { get; set; }
        public string? DriverName { get; set; }
        public decimal LoadMeals { get; set; }
        public decimal? LoadPercent { get; set; }
        public decimal DeliveryTempMinC { get; set; }
        /// <summary>Nhiệt độ xuất phát đo gần nhất (null = chưa đo).</summary>
        public decimal? DepartureTemperatureC { get; set; }
        public List<DeliveryStopDetailModel> ListStop { get; set; } = [];
        public List<DeliveryEvent> ListEvent { get; set; } = [];
        /// <summary>Mẻ nấu của ngày / ca phục vụ chuyến (số ME-, trạng thái, đã lưu mẫu).</summary>
        public List<DeliveryBatchBriefModel> ListBatch { get; set; } = [];
        /// <summary>Điều kiện xuất phát còn thiếu (khi Kế hoạch / Đang xếp).</summary>
        public List<string> ListBlocker { get; set; } = [];
        public List<string> AllowedActions { get; set; } = [];
        public List<string> ListWarning { get; set; } = [];
    }

    public class DeliveryStopDetailModel
    {
        public Guid Id { get; set; }
        public int SequenceNo { get; set; }
        public string? StopStatus { get; set; }
        public string? StatusName { get; set; }
        public Guid? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public Guid? DeliveryLocationId { get; set; }
        public string? LocationName { get; set; }
        public string? Address { get; set; }
        public Guid? OrderId { get; set; }
        public string? OrderNumber { get; set; }
        public Guid? DeliveryOrderId { get; set; }
        public string? DeliveryNumber { get; set; }
        public DateTime? PlannedArrival { get; set; }
        public DateTime? ActualArrival { get; set; }
        public decimal? PlannedMealQty { get; set; }
        public decimal? DeliveredMealQty { get; set; }
        /// <summary>Thiếu (+) / thừa (−) = kế hoạch − giao.</summary>
        public decimal? ShortageQty { get; set; }
        /// <summary>Nhiệt độ bàn giao đo gần nhất tại điểm.</summary>
        public decimal? HandoverTemperatureC { get; set; }
        public ProofOfDelivery? Pod { get; set; }
        /// <summary>BE2.1: số suất đã xếp lên xe (DeliveryLoading Loaded) – null = chưa xếp.</summary>
        public decimal? LoadedQty { get; set; }
        /// <summary>BE2.1: trạng thái thực hiện giao (DeliveryExecution): READY / LOADED / DISPATCHED / ARRIVED / DELIVERED / FAILED.</summary>
        public string? ExecutionStatus { get; set; }
        public string? Note { get; set; }
        public List<string> AllowedActions { get; set; } = [];
    }

    /// <summary>BE2.1 (spec 10.2 GET delivery-orders): đơn giao + số suất, chuyến, điểm giao, đã xếp lên xe.</summary>
    public class DeliveryOrderItemModel
    {
        public DeliveryOrder DeliveryOrder { get; set; } = null!;
        public string? StatusName { get; set; }
        public decimal MealQuantity { get; set; }
        public string? OrderNumber { get; set; }
        public string? OrderStatus { get; set; }
        public string? CustomerName { get; set; }
        public string? LocationName { get; set; }
        public string? Address { get; set; }
        public string? Area { get; set; }
        public Guid? DeliveryRouteId { get; set; }
        public string? RouteNo { get; set; }
        public string? RouteStatus { get; set; }
        public Guid? DeliveryStopId { get; set; }
        public int? SequenceNo { get; set; }
        public decimal? LoadedQty { get; set; }
        public bool IsLoaded { get; set; }
        public string? NoteText { get; set; }
        public List<string> AllowedActions { get; set; } = [];
    }

    /// <summary>BE2.1: kết quả thao tác đơn giao (tạo / tách / gán chuyến / hủy) – các đơn giao liên quan + chuyến bị ảnh hưởng + cảnh báo.</summary>
    public class DeliveryOrderActionModel
    {
        public List<DeliveryOrderItemModel> ListDeliveryOrder { get; set; } = [];
        public List<DeliveryRouteDetailModel> ListRoute { get; set; } = [];
        public List<string> ListWarning { get; set; } = [];
    }

    /// <summary>BE2.1 (spec 10.2 plan): kết quả lập chuyến nhiều xe theo khu vực.</summary>
    public class DeliveryRoutePlanModel
    {
        public DateTime DeliveryDate { get; set; }
        public List<DeliveryRouteDetailModel> ListRoute { get; set; } = [];
        /// <summary>Khu vực → xe (mã) đã xếp.</summary>
        public List<DeliveryPlanAreaModel> ListArea { get; set; } = [];
        public List<string> ListWarning { get; set; } = [];
    }

    public class DeliveryPlanAreaModel
    {
        public string Area { get; set; } = string.Empty;
        public decimal MealQuantity { get; set; }
        public int StopCount { get; set; }
        public List<string> ListVehicleCode { get; set; } = [];
    }

    /// <summary>BE2.1 (spec 10.2 load): kết quả xếp hàng lên xe.</summary>
    public class DeliveryLoadModel
    {
        public DeliveryRouteDetailModel Route { get; set; } = null!;
        public List<DeliveryLoading> ListLoading { get; set; } = [];
        public List<DeliveryExecution> ListExecution { get; set; } = [];
    }

    /// <summary>BE2.1 (spec 10.3 GET executions): 1 dòng thực hiện giao.</summary>
    public class DeliveryExecutionItemModel
    {
        public Guid DeliveryOrderId { get; set; }
        public string? DeliveryNumber { get; set; }
        public string? DeliveryOrderStatus { get; set; }
        public decimal MealQuantity { get; set; }
        public Guid OrderId { get; set; }
        public string? OrderNumber { get; set; }
        public Guid CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public Guid? DeliveryLocationId { get; set; }
        public string? LocationName { get; set; }
        public string? Address { get; set; }
        public Guid? DeliveryRouteId { get; set; }
        public string? RouteNo { get; set; }
        public string? RouteStatus { get; set; }
        public string? VehicleCode { get; set; }
        public string? DriverName { get; set; }
        public Guid? DeliveryStopId { get; set; }
        public int? SequenceNo { get; set; }
        public string? StopStatus { get; set; }
        public DateTime? PlannedArrival { get; set; }
        public DateTime? ActualArrival { get; set; }
        public decimal? LoadedQty { get; set; }
        /// <summary>READY / LOADED / DISPATCHED / ARRIVED / DELIVERED / FAILED (không có dòng DeliveryExecution → suy theo đơn giao / điểm dừng).</summary>
        public string Status { get; set; } = Helper.DeliveryExecutionStatus.Ready;
        public string? StatusName { get; set; }
        public DeliveryExecution? Execution { get; set; }
        public ProofOfDelivery? Pod { get; set; }
    }

    public class DeliveryExecutionBoardModel
    {
        public DateTime DeliveryDate { get; set; }
        public List<DeliveryExecutionItemModel> ListItem { get; set; } = [];
        public Dictionary<string, int> StatusCounts { get; set; } = [];
    }

    /// <summary>BE2.1 (spec 10.1): lệnh đóng gói + kết quả, mẻ, đơn.</summary>
    public class PackingOrderDetailModel
    {
        public PackingOrder PackingOrder { get; set; } = null!;
        public string? StatusName { get; set; }
        public string? BatchNumber { get; set; }
        public string? BatchStatus { get; set; }
        public string? DishName { get; set; }
        public string? OrderNumber { get; set; }
        public string? CustomerName { get; set; }
        public List<PackingResult> ListResult { get; set; } = [];
        public List<string> AllowedActions { get; set; } = [];
    }

    /// <summary>BE2.1: các lệnh đóng gói của 1 mẻ + số phần đạt / đã chia / còn lại.</summary>
    public class PackingBatchModel
    {
        public Guid ProductionBatchId { get; set; }
        public string? BatchNumber { get; set; }
        public string? BatchStatus { get; set; }
        public decimal AcceptedQuantity { get; set; }
        public decimal AllocatedQuantity { get; set; }
        public decimal RemainingQuantity { get; set; }
        public List<PackingOrderDetailModel> ListPackingOrder { get; set; } = [];
        public List<string> ListWarning { get; set; } = [];
    }

    public class DeliveryBatchBriefModel
    {
        public Guid Id { get; set; }
        public string? BatchNumber { get; set; }
        public string? DishName { get; set; }
        public string? Status { get; set; }
        public string? StatusName { get; set; }
        public bool SampleRetained { get; set; }
    }

    /// <summary>T19: chuyến của 1 ngày giao (spec 10.2 GET routes) + đếm theo trạng thái.</summary>
    public class DeliveryBoardModel
    {
        public DateTime DeliveryDate { get; set; }
        public List<DeliveryRouteDetailModel> ListRoute { get; set; } = [];
        public Dictionary<string, int> StatusCounts { get; set; } = [];
    }

    /// <summary>T19: đơn hàng Đang sản xuất của ngày giao chưa có chuyến (chọn khi lập chuyến).</summary>
    public class DeliveryReadyOrderModel
    {
        public Guid OrderId { get; set; }
        public string? OrderNumber { get; set; }
        public Guid CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public Guid? DeliveryLocationId { get; set; }
        public string? LocationName { get; set; }
        public string? Address { get; set; }
        public TimeSpan? DeliveryWindowFrom { get; set; }
        public decimal MealQuantity { get; set; }
        public string? Status { get; set; }
    }

    /// <summary>T19 (spec 6.7): chi tiết đối soát – đặt / giao / nhận (POD) / phục vụ / trả về / hủy, cơ sở tính tiền, thao tác được phép.</summary>
    public class OrderReconciliationDetailModel
    {
        public OrderReconciliation Reconciliation { get; set; } = null!;
        public string? StatusName { get; set; }
        public string? OrderNumber { get; set; }
        public string? OrderStatus { get; set; }
        public DateTime MealDate { get; set; }
        public Guid CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public decimal OrderTotalAmount { get; set; }
        public decimal OrderedQuantity { get; set; }
        public decimal DeliveredQuantity { get; set; }
        /// <summary>Σ suất khách nhận khi bàn giao (POD).</summary>
        public decimal PodAcceptedQuantity { get; set; }
        /// <summary>Suất thực tế phục vụ (khách xác nhận) = OrderReconciliation.AcceptedQuantity.</summary>
        public decimal ServedQuantity { get; set; }
        public decimal? ReturnedQuantity { get; set; }
        public decimal? WastedQuantity { get; set; }
        /// <summary>Suất nhận nhưng không phục vụ = nhận (POD) − phục vụ.</summary>
        public decimal LeftoverQuantity { get; set; }
        public string? BillingBasis { get; set; }
        public decimal BillingQuantity { get; set; }
        public List<ProofOfDelivery> ListPod { get; set; } = [];
        public List<string> AllowedActions { get; set; } = [];
        public List<string> ListWarning { get; set; } = [];
    }

    /// <summary>T19: đơn Đã giao chờ đối soát (hoặc đã có đối soát chưa đóng).</summary>
    public class ReconciliationPendingModel
    {
        public Guid OrderId { get; set; }
        public string? OrderNumber { get; set; }
        public DateTime MealDate { get; set; }
        public Guid CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? OrderStatus { get; set; }
        public decimal OrderedQuantity { get; set; }
        public decimal DeliveredQuantity { get; set; }
        public decimal PodAcceptedQuantity { get; set; }
        public Guid? ReconciliationId { get; set; }
        public string? ReconciliationStatus { get; set; }
    }

    /// <summary>T19 (spec 10.5): chi tiết phản ánh + lịch sử xử lý.</summary>
    public class CustomerComplaintDetailModel
    {
        public CustomerComplaint Complaint { get; set; } = null!;
        public string? StatusName { get; set; }
        public string? SeverityName { get; set; }
        public string? CustomerName { get; set; }
        public string? OrderNumber { get; set; }
        public string? DeliveryNumber { get; set; }
        public string? RouteNo { get; set; }
        public string? AssignedToName { get; set; }
        /// <summary>Quá hạn SLA: SlaDueAt &lt; bây giờ và chưa Đã xử lý / Đóng / Từ chối (cảnh báo tự động → sau Pilot).</summary>
        public bool IsOverdue { get; set; }
        public List<ComplaintHistory> ListHistory { get; set; } = [];
        public List<string> AllowedActions { get; set; } = [];
    }
}
