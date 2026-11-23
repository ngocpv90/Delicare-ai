using System.ComponentModel.DataAnnotations;

namespace AppDelicare.Model.Request.Custom
{
    /// <summary>T19: lọc theo ngày giao (+ ca) – danh sách chuyến của ngày / đơn hàng chờ xếp chuyến.</summary>
    public class DeliveryDateRequest
    {
        [Required]
        public DateTime DeliveryDate { get; set; }
        /// <summary>Ca giao (Shift) – lọc đơn có dòng thuộc ca; trống = mọi ca.</summary>
        public Guid? ShiftId { get; set; }
        [MaxLength(50)]
        public string? Status { get; set; }
    }

    /// <summary>
    /// T19 (spec 10.2, Pilot P8): lập chuyến xe <c>CX-yyMMdd-&lt;XE&gt;-NN</c> cho 1 ngày / ca / xe. JSON (<c>[FromBody]</c>) vì có danh sách đơn hàng.
    /// <c>ListOrderId</c> trống = mọi đơn Đang sản xuất của ngày (dòng thuộc ca) chưa có chuyến. Mỗi đơn → 1 đơn giao (PH-) + 1 điểm dừng (1 đơn = 1 điểm giao, GAP-ORD-01).
    /// </summary>
    public class DeliveryRouteCreateRequest
    {
        [Required]
        public DateTime DeliveryDate { get; set; }
        public Guid? ShiftId { get; set; }
        [Required]
        public Guid VehicleId { get; set; }
        /// <summary>Tài xế (Employee) – tùy chọn.</summary>
        public Guid? DriverId { get; set; }
        public DateTime? PlannedStart { get; set; }
        public DateTime? PlannedEnd { get; set; }
        [MaxLength(500)]
        public string? Name { get; set; }
        [MaxLength(1000)]
        public string? Description { get; set; }
        public List<Guid> ListOrderId { get; set; } = [];
        /// <summary>BE2.1: đơn giao lập tay / tách (Kế hoạch, chưa gán chuyến, cùng ngày giao) đưa vào chuyến. Có danh sách này mà <c>ListOrderId</c> trống → chỉ lấy các đơn giao này.</summary>
        public List<Guid> ListDeliveryOrderId { get; set; } = [];
    }

    /// <summary>BE2.1 (spec 10.2 load): xếp hàng lên xe – JSON vì có danh sách. <c>ListItem</c> trống = mọi đơn giao Kế hoạch của chuyến, số suất = suất kế hoạch.</summary>
    public class DeliveryRouteLoadRequest
    {
        /// <summary>Id chuyến xe.</summary>
        [Required]
        public Guid Id { get; set; }
        /// <summary>Nhân viên xếp hàng (Employee) – tùy chọn.</summary>
        public Guid? EmployeeId { get; set; }
        public List<DeliveryLoadItemRequest> ListItem { get; set; } = [];
        [MaxLength(1000)]
        public string? Note { get; set; }
    }

    public class DeliveryLoadItemRequest
    {
        [Required]
        public Guid DeliveryOrderId { get; set; }
        /// <summary>Số suất xếp lên xe; trống = suất kế hoạch của đơn giao.</summary>
        public decimal? LoadedQty { get; set; }
    }

    /// <summary>BE2.1 (spec 10.2 PUT routes/{id}/stops): đổi thứ tự / giờ dự kiến các điểm dừng của chuyến Kế hoạch – JSON.</summary>
    public class DeliveryRouteStopsRequest
    {
        /// <summary>Id chuyến xe.</summary>
        [Required]
        public Guid Id { get; set; }
        public List<DeliveryRouteStopItemRequest> ListStop { get; set; } = [];
    }

    public class DeliveryRouteStopItemRequest
    {
        [Required]
        public Guid DeliveryOrderId { get; set; }
        public int SequenceNo { get; set; }
        public DateTime? PlannedArrival { get; set; }
    }

    /// <summary>
    /// BE2.1 (spec 10.2 plan): lập chuyến cho nhiều xe theo khu vực – JSON. Đơn Đang sản xuất của ngày còn suất chưa lập đơn giao + đơn giao Kế hoạch chưa gán chuyến,
    /// gom theo khu vực (GAP-DLV-09), chia cho các xe theo tải còn trống; mỗi xe có hàng → 1 chuyến CX-.
    /// </summary>
    public class DeliveryRoutePlanRequest
    {
        [Required]
        public DateTime DeliveryDate { get; set; }
        public Guid? ShiftId { get; set; }
        public List<DeliveryPlanVehicleRequest> ListVehicle { get; set; } = [];
        /// <summary>Chỉ hỗ trợ "byArea" (mặc định).</summary>
        [MaxLength(50)]
        public string? Strategy { get; set; }
    }

    public class DeliveryPlanVehicleRequest
    {
        [Required]
        public Guid VehicleId { get; set; }
        public Guid? DriverId { get; set; }
    }

    /// <summary>
    /// BE2.1 (spec 10.2 POST delivery-orders): lập đơn giao tay cho 1 phần / toàn bộ suất của đơn hàng – JSON vì có dòng đơn.
    /// Số suất = Σ <c>ListLine.Quantity</c>, hoặc <c>MealQuantity</c>, hoặc trống cả hai = toàn bộ suất còn lại của đơn.
    /// </summary>
    public class DeliveryOrderCreateRequest
    {
        [Required]
        public Guid OrderId { get; set; }
        /// <summary>Điểm giao (thuộc khách của đơn); trống = điểm giao của đơn hàng.</summary>
        public Guid? CustomerDeliveryLocationId { get; set; }
        public DateTime? PlannedDeliveryTime { get; set; }
        public decimal? MealQuantity { get; set; }
        public List<DeliveryOrderLineRequest> ListLine { get; set; } = [];
        [MaxLength(1000)]
        public string? Note { get; set; }
    }

    public class DeliveryOrderLineRequest
    {
        [Required]
        public Guid OrderLineId { get; set; }
        public decimal Quantity { get; set; }
    }

    /// <summary>BE2.1 (spec 10.2 split): tách đơn giao chưa xếp lên xe thành nhiều phần (phần đầu giữ đơn gốc, phần sau là đơn giao mới chưa gán chuyến) – JSON.</summary>
    public class DeliveryOrderSplitRequest
    {
        /// <summary>Id đơn giao.</summary>
        [Required]
        public Guid Id { get; set; }
        public List<DeliveryOrderSplitPartRequest> ListPart { get; set; } = [];
    }

    public class DeliveryOrderSplitPartRequest
    {
        public Guid? CustomerDeliveryLocationId { get; set; }
        public DateTime? PlannedDeliveryTime { get; set; }
        public decimal MealQuantity { get; set; }
    }

    /// <summary>BE2.1 (spec 10.2 PUT delivery-orders/{id}/route): gán / chuyển đơn giao vào chuyến Kế hoạch (form). Trống SequenceNo = cuối chuyến.</summary>
    public class DeliveryOrderAssignRouteRequest
    {
        /// <summary>Id đơn giao.</summary>
        [Required]
        public Guid Id { get; set; }
        [Required]
        public Guid DeliveryRouteId { get; set; }
        public int? SequenceNo { get; set; }
    }

    /// <summary>BE2.1 (spec 10.2 cancel): hủy đơn giao chưa xếp lên xe (form) – lý do bắt buộc.</summary>
    public class DeliveryOrderCancelRequest
    {
        [Required]
        public Guid Id { get; set; }
        [MaxLength(1000)]
        public string? Reason { get; set; }
    }

    /// <summary>BE2.1 (spec 10.2 GET delivery-orders): lọc đơn giao. <c>Unassigned = true</c> = chưa gán chuyến.</summary>
    public class DeliveryOrderListRequest
    {
        public DateTime? DeliveryDate { get; set; }
        public Guid? OrderId { get; set; }
        public Guid? CustomerId { get; set; }
        public Guid? DeliveryRouteId { get; set; }
        [MaxLength(50)]
        public string? Status { get; set; }
        public bool? Unassigned { get; set; }
    }

    /// <summary>BE2.1 (spec 10.3 GET executions): thực hiện giao của ngày (+ trạng thái, chuyến).</summary>
    public class DeliveryExecutionBoardRequest
    {
        [Required]
        public DateTime DeliveryDate { get; set; }
        [MaxLength(50)]
        public string? Status { get; set; }
        public Guid? DeliveryRouteId { get; set; }
    }

    /// <summary>T19: đo nhiệt độ giao (form). DeliveryStopId trống = nhiệt độ lúc xuất phát của chuyến; có = lúc bàn giao tại điểm. &lt; 60 °C bắt buộc hành động khắc phục.</summary>
    public class DeliveryTemperatureRequest
    {
        /// <summary>Id chuyến xe.</summary>
        [Required]
        public Guid Id { get; set; }
        public Guid? DeliveryStopId { get; set; }
        [Required]
        public decimal TemperatureC { get; set; }
        [MaxLength(2000)]
        public string? CorrectiveAction { get; set; }
        [MaxLength(100)]
        public string? DeviceCode { get; set; }
        [MaxLength(1000)]
        public string? EvidenceUrl { get; set; }
        public DateTime? MeasuredAt { get; set; }
    }

    /// <summary>
    /// T19: xuất phát chuyến (form) – Kế hoạch / Đang xếp → Đang giao. Có <c>TemperatureC</c> thì ghi lần đo xuất phát trước khi kiểm.
    /// Chặn khi nhiệt độ xuất phát gần nhất &lt; 60 °C / chưa đo, mẻ của ngày / ca chưa đóng gói xong, mẻ chưa lưu mẫu.
    /// </summary>
    /// <summary>BOT-3.2 (D-72): đổi xe và / hoặc tài xế của chuyến chưa xuất phát (Kế hoạch / Đang xếp). Bỏ trống = giữ nguyên.</summary>
    public class DeliveryRouteReassignRequest
    {
        [Required]
        public Guid Id { get; set; }
        public Guid? VehicleId { get; set; }
        public Guid? DriverId { get; set; }
        [MaxLength(500)]
        public string? Note { get; set; }
    }

    public class DeliveryRouteDispatchRequest
    {
        [Required]
        public Guid Id { get; set; }
        public decimal? TemperatureC { get; set; }
        [MaxLength(2000)]
        public string? CorrectiveAction { get; set; }
        [MaxLength(100)]
        public string? DeviceCode { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        [MaxLength(1000)]
        public string? Note { get; set; }
    }

    /// <summary>T19: đến điểm dừng (form) – Kế hoạch → Đã đến. Có <c>TemperatureC</c> thì ghi lần đo tại điểm.</summary>
    public class DeliveryStopArriveRequest
    {
        /// <summary>Id điểm dừng.</summary>
        [Required]
        public Guid Id { get; set; }
        public decimal? TemperatureC { get; set; }
        [MaxLength(2000)]
        public string? CorrectiveAction { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        [MaxLength(1000)]
        public string? PhotoUrl { get; set; }
        [MaxLength(1000)]
        public string? Note { get; set; }
    }

    /// <summary>
    /// T19 (spec 10.3 pod): bàn giao tại điểm + POD (form). Nhiệt độ bàn giao gần nhất tại điểm phải ≥ 60 °C (gửi kèm <c>TemperatureC</c> hoặc đo trước).
    /// <c>AcceptedQuantity</c> trống = bằng số giao; số từ chối = giao − nhận. Chữ ký / ảnh chỉ lưu đường dẫn file.
    /// </summary>
    public class DeliveryStopDeliverRequest
    {
        [Required]
        public Guid Id { get; set; }
        public decimal? TemperatureC { get; set; }
        [MaxLength(2000)]
        public string? CorrectiveAction { get; set; }
        [MaxLength(100)]
        public string? DeviceCode { get; set; }
        [Required]
        public decimal DeliveredQuantity { get; set; }
        public decimal? AcceptedQuantity { get; set; }
        [Required]
        [MaxLength(500)]
        public string ReceiverName { get; set; } = string.Empty;
        [MaxLength(100)]
        public string? ReceiverPhone { get; set; }
        public DateTime? ReceivedAt { get; set; }
        [MaxLength(1000)]
        public string? SignatureUrl { get; set; }
        [MaxLength(1000)]
        public string? PhotoUrl { get; set; }
        [MaxLength(2000)]
        public string? Remark { get; set; }
        /// <summary>
        /// BE2.5 (spec 10.3 pod "Ghi PackagingLedger OUT nếu đóng khay"): số khay / hộp để lại ở khách. Trống = theo cấu hình <c>PackagingOutOnDelivery</c>
        /// (suất khách nhận × <c>PackagingUnitsPerMeal</c>); 0 = không ghi sổ bao bì (hộp dùng 1 lần).
        /// </summary>
        public decimal? PackagingQuantity { get; set; }
        /// <summary>BE2.5: loại khay / hộp; trống = loại bao bì của kết quả đóng gói của đơn, không có thì <c>PackagingDefaultType</c>.</summary>
        [MaxLength(100)]
        public string? PackagingType { get; set; }
    }

    /// <summary>T19: giao thất bại tại điểm (form) – lý do bắt buộc.</summary>
    public class DeliveryStopFailRequest
    {
        [Required]
        public Guid Id { get; set; }
        [Required]
        [MaxLength(2000)]
        public string Reason { get; set; } = string.Empty;
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
    }

    /// <summary>
    /// T19 (spec 6.7, BUSINESS_RULES §8b): đối soát 1 đơn hàng Đã giao (form) – suất thực tế phục vụ (khách xác nhận), suất trả về / hủy (tùy chọn).
    /// </summary>
    public class OrderReconcileRequest
    {
        [Required]
        public Guid OrderId { get; set; }
        [Required]
        public decimal ServedQuantity { get; set; }
        public decimal? ReturnedQuantity { get; set; }
        public decimal? WastedQuantity { get; set; }
        [MaxLength(2000)]
        public string? Remark { get; set; }
        /// <summary>
        /// ORD-OPT-1.7 (D-74 i): suất phục vụ theo từng dòng đơn (lựa chọn) – tùy chọn. Có gửi thì <see cref="ServedQuantity"/> = Σ các dòng (máy chủ tự cộng)
        /// và tiền (cơ sở SERVED) = Σ suất phục vụ dòng × giá dòng. Lưu trong JSON OrderReconciliation.Note (không thêm cột). Form: ListLine[0].OrderLineId…
        /// </summary>
        public List<ReconcileLineRequest> ListLine { get; set; } = [];
    }

    public class ReconcileLineRequest
    {
        [Required]
        public Guid OrderLineId { get; set; }
        public decimal ServedQuantity { get; set; }
    }

    /// <summary>T19: đóng đối soát (form) – Khớp / Chênh lệch → Đóng; đơn hàng → Đã đối soát.</summary>
    public class ReconciliationCloseRequest
    {
        [Required]
        public Guid Id { get; set; }
        [MaxLength(2000)]
        public string? Remark { get; set; }
    }

    /// <summary>T19: lọc đơn chờ đối soát (đơn Đã giao) / danh sách đối soát theo ngày ăn.</summary>
    public class ReconciliationPendingRequest
    {
        public DateTime? MealDate { get; set; }
        public Guid? CustomerId { get; set; }
    }

    /// <summary>
    /// T19 (spec 10.5): tạo phản ánh <c>PA-yyMMdd-NNNN</c> (form). Gắn được đơn hàng / điểm dừng (→ đơn giao + chuyến) / mẻ (ghi vào mô tả – GAP-DLV-06).
    /// Mức độ High / Medium / Low (mặc định Medium) → hạn xử lý theo BusinessRules:ComplaintSlaHours.
    /// </summary>
    public class ComplaintCreateRequest
    {
        [Required]
        public Guid CustomerId { get; set; }
        public Guid? OrderId { get; set; }
        public Guid? DeliveryStopId { get; set; }
        public Guid? DeliveryOrderId { get; set; }
        public Guid? DeliveryRouteId { get; set; }
        public Guid? ProductionBatchId { get; set; }
        [MaxLength(50)]
        public string? SourceChannel { get; set; }
        [MaxLength(50)]
        public string? CategoryCode { get; set; }
        [MaxLength(30)]
        public string? Severity { get; set; }
        [Required]
        [MaxLength(1800)]
        public string Description { get; set; } = string.Empty;
        public DateTime? ReceivedAt { get; set; }
    }

    /// <summary>T19: thao tác phản ánh (form): Assign (AssignedTo = User.Id), Resolve (Comment = cách xử lý), Close, Reject, AddComment.</summary>
    public class ComplaintActionRequest
    {
        [Required]
        public Guid Id { get; set; }
        public Guid? AssignedTo { get; set; }
        [MaxLength(2000)]
        public string? Comment { get; set; }
    }
}
