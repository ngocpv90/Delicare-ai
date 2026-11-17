namespace AppDelicare.Model.Helper
{
    /// <summary>T19 – trạng thái chuyến xe (STATUS_CATALOG: DeliveryRoute.Status, PROPOSED – BA/PO CONFIRM): Planned → (Loading) → InTransit → Completed.</summary>
    public static class DeliveryRouteStatus
    {
        public const string Planned = "Planned";
        /// <summary>Có trong STATUS_CATALOG; Pilot chưa dùng bước xếp hàng (DeliveryLoading) – Dispatch nhận cả Planned / Loading.</summary>
        public const string Loading = "Loading";
        public const string InTransit = "InTransit";
        public const string Completed = "Completed";

        public static string Label(string? status) => status switch
        {
            Planned => "Kế hoạch",
            Loading => "Đang xếp",
            InTransit => "Đang giao",
            Completed => "Hoàn thành",
            _ => status ?? string.Empty,
        };
    }

    /// <summary>T19 – trạng thái điểm dừng (STATUS_CATALOG: DeliveryStop.StopStatus): Planned → Arrived → Delivered / Failed.</summary>
    public static class DeliveryStopStatus
    {
        public const string Planned = "Planned";
        public const string Arrived = "Arrived";
        public const string Delivered = "Delivered";
        public const string Failed = "Failed";

        public static readonly string[] Closed = [Delivered, Failed];

        public static string Label(string? status) => status switch
        {
            Planned => "Kế hoạch",
            Arrived => "Đã đến",
            Delivered => "Đã giao",
            Failed => "Thất bại",
            _ => status ?? string.Empty,
        };
    }

    /// <summary>
    /// T19 – trạng thái đơn giao (DeliveryOrder.Status – chưa có trong STATUS_CATALOG, PROPOSED – BA/PO CONFIRM, GAP-DLV-02):
    /// Planned → InTransit → Delivered / Failed (đi cùng điểm dừng). Đơn giao Failed không còn hiệu lực → đơn hàng được xếp chuyến lại.
    /// </summary>
    public static class DeliveryOrderStatus
    {
        public const string Planned = "Planned";
        public const string InTransit = "InTransit";
        public const string Delivered = "Delivered";
        public const string Failed = "Failed";
        /// <summary>BE2.1 (spec 10.2 delivery-orders cancel): hủy đơn giao chưa xếp lên xe – suất trả lại đơn hàng (PROPOSED – BA/PO CONFIRM).</summary>
        public const string Cancelled = "Cancelled";

        /// <summary>Đơn giao còn hiệu lực (giữ suất của đơn hàng – phần còn lại mới được lập đơn giao / xếp chuyến).</summary>
        public static readonly string[] Active = [Planned, InTransit, Delivered];

        public static string Label(string? status) => status switch
        {
            Planned => "Kế hoạch",
            InTransit => "Đang giao",
            Delivered => "Đã giao",
            Failed => "Thất bại",
            Cancelled => "Hủy",
            _ => status ?? string.Empty,
        };
    }

    /// <summary>BE2.1 (spec 10.2 load) – trạng thái dòng xếp hàng lên xe (DeliveryLoading.Status – chưa có trong STATUS_CATALOG, PROPOSED – BA/PO CONFIRM).</summary>
    public static class DeliveryLoadingStatus
    {
        public const string Loaded = "Loaded";
        /// <summary>Dòng xếp hàng của đơn giao bị hủy / tách lại trước khi xuất phát.</summary>
        public const string Cancelled = "Cancelled";

        public static string Label(string? status) => status switch
        {
            Loaded => "Đã xếp lên xe",
            Cancelled => "Hủy",
            _ => status ?? string.Empty,
        };
    }

    /// <summary>
    /// BE2.1 (spec 10 – BRD) – trạng thái thực hiện giao (DeliveryExecution.Status): READY → LOADED → DISPATCHED → ARRIVED → DELIVERED / FAILED.
    /// Đơn giao chưa có dòng DeliveryExecution (chưa xếp hàng) hiển thị READY.
    /// </summary>
    public static class DeliveryExecutionStatus
    {
        public const string Ready = "READY";
        public const string Loaded = "LOADED";
        public const string Dispatched = "DISPATCHED";
        public const string Arrived = "ARRIVED";
        public const string Delivered = "DELIVERED";
        public const string Failed = "FAILED";
        public const string Cancelled = "CANCELLED";

        public static string Label(string? status) => status switch
        {
            Ready => "Chờ xếp hàng",
            Loaded => "Đã xếp lên xe",
            Dispatched => "Đang giao",
            Arrived => "Đã đến",
            Delivered => "Đã giao",
            Failed => "Thất bại",
            Cancelled => "Hủy",
            _ => status ?? string.Empty,
        };
    }

    /// <summary>
    /// BE2.1 (spec 10.1) – trạng thái lệnh đóng gói (PackingOrder.Status, PROPOSED – BA/PO CONFIRM):
    /// Planned → Packing (Start) → Packed (RecordResult); Planned → Cancelled (Cancel).
    /// </summary>
    public static class PackingOrderStatus
    {
        public const string Planned = "Planned";
        public const string Packing = "Packing";
        public const string Packed = "Packed";
        public const string Cancelled = "Cancelled";

        /// <summary>Lệnh còn hiệu lực (giữ số phần của mẻ).</summary>
        public static readonly string[] Active = [Planned, Packing, Packed];

        public static string Label(string? status) => status switch
        {
            Planned => "Kế hoạch",
            Packing => "Đang đóng gói",
            Packed => "Đã đóng gói",
            Cancelled => "Hủy",
            _ => status ?? string.Empty,
        };
    }

    /// <summary>T19 – trạng thái đối soát (STATUS_CATALOG: OrderReconciliation.Status): Open → Matched / Variance → Closed.</summary>
    public static class ReconciliationStatus
    {
        public const string Open = "Open";
        public const string Matched = "Matched";
        public const string Variance = "Variance";
        public const string Closed = "Closed";

        public static string Label(string? status) => status switch
        {
            Open => "Mở",
            Matched => "Khớp",
            Variance => "Chênh lệch",
            Closed => "Đóng",
            _ => status ?? string.Empty,
        };
    }

    /// <summary>T19 – trạng thái phản ánh (STATUS_CATALOG: CustomerComplaint.ComplaintStatus): Open → InProgress → Resolved → Closed; Rejected.</summary>
    public static class ComplaintStatus
    {
        public const string Open = "Open";
        public const string InProgress = "InProgress";
        public const string Resolved = "Resolved";
        public const string Closed = "Closed";
        public const string Rejected = "Rejected";

        /// <summary>Đã xong (không tính quá hạn SLA).</summary>
        public static readonly string[] Done = [Resolved, Closed, Rejected];

        public static string Label(string? status) => status switch
        {
            Open => "Mới",
            InProgress => "Đang xử lý",
            Resolved => "Đã xử lý",
            Closed => "Đóng",
            Rejected => "Từ chối",
            _ => status ?? string.Empty,
        };
    }

    /// <summary>
    /// T19 – mức độ phản ánh (BUSINESS_RULES §9: HIGH / MEDIUM / LOW, hạn 4 / 24 / 72 giờ). Lưu dạng <c>High / Medium / Low</c> cho khớp mặc định SQL (<c>N'Medium'</c>);
    /// tra hạn trong <c>BusinessRules:ComplaintSlaHours</c> không phân biệt hoa / thường.
    /// </summary>
    public static class ComplaintSeverity
    {
        public const string High = "High";
        public const string Medium = "Medium";
        public const string Low = "Low";

        /// <summary>Chuẩn hóa (HIGH / high / Cao…) → High / Medium / Low; trống → Medium; sai → null.</summary>
        public static string? Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Medium;
            }
            return value.Trim().ToUpperInvariant() switch
            {
                "HIGH" or "CAO" => High,
                "MEDIUM" or "TRUNG BÌNH" or "TRUNG BINH" => Medium,
                "LOW" or "THẤP" or "THAP" => Low,
                _ => null,
            };
        }

        public static string Label(string? severity) => severity?.ToUpperInvariant() switch
        {
            "HIGH" => "Cao",
            "MEDIUM" => "Trung bình",
            "LOW" => "Thấp",
            _ => severity ?? string.Empty,
        };
    }

    /// <summary>T19 – mã dùng chung của giao vận (BUSINESS_RULES §7, §9).</summary>
    public static class DeliveryCode
    {
        /// <summary>TemperatureLog.MeasurementType của nhiệt độ giao (BUSINESS_RULES §7).</summary>
        public const string DeliveryMeasurement = "DELIVERY";
        /// <summary>DeliveryEvent.EventType: đo nhiệt độ (DeliveryStopId trống = lúc xuất phát; có = lúc bàn giao tại điểm).</summary>
        public const string EventTemperature = "TEMP_CHECK";
        public const string EventDispatched = "DISPATCHED";
        /// <summary>BE2.1: xếp hàng lên xe (DeliveryRoute/Load).</summary>
        public const string EventLoaded = "LOADED";
        public const string EventArrived = "ARRIVED";
        public const string EventDelivered = "DELIVERED";
        public const string EventFailed = "FAILED";
        /// <summary>Tiền tố DeliveryEvent.ExternalEventId trỏ tới dòng TemperatureLog cùng lần đo (TemperatureLog không có cột chuyến / điểm – GAP-DLV-01).</summary>
        public const string TemperatureLogRef = "TemperatureLog:";
        public const string SourceManual = "MANUAL";

        /// <summary>ComplaintHistory.EventType.</summary>
        public const string ComplaintCreated = "CREATED";
        public const string ComplaintAssigned = "ASSIGNED";
        public const string ComplaintResolved = "RESOLVED";
        public const string ComplaintClosed = "CLOSED";
        public const string ComplaintRejected = "REJECTED";
        public const string ComplaintComment = "COMMENT";
        /// <summary>Phản ánh tự tạo khi bàn giao có suất bị từ chối (spec 10.3 pod).</summary>
        public const string ComplaintSourcePod = "POD";
        public const string ComplaintCategoryShortage = "SHORTAGE";

        /// <summary>Cơ sở tính tiền khi đối soát (BusinessRules:ReconciliationBillingBasis, PROPOSED – GAP-DLV-05).</summary>
        public const string BillingOrdered = "ORDERED";
        public const string BillingDelivered = "DELIVERED";
        public const string BillingAccepted = "ACCEPTED";
        public const string BillingServed = "SERVED";

        /// <summary>
        /// BE2.1 (GAP-DLV-08): DeliveryOrder không có cột số suất → số suất của đơn giao ghi ở đầu <c>DeliveryOrder.Note</c> dạng <c>[SUAT:60]</c>
        /// (đã gán chuyến thì lấy <c>DeliveryStop.PlannedMealQty</c>; tag vẫn cập nhật song song).
        /// </summary>
        public const string MealTagPrefix = "[SUAT:";

        /// <summary>BE2.1: MealWasteRecord.WasteType khi đóng gói có suất hỏng / loại (ReferenceLists WasteType).</summary>
        public const string WastePacking = "PACKING_DAMAGE";
    }
}
