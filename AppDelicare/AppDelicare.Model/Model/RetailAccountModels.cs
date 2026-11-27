using System.Text.Json.Serialization;

namespace AppDelicare.Model.Model
{
    /// <summary>RETAIL-2: hồ sơ khách lẻ (không bao giờ trả mật khẩu / mã băm).</summary>
    public class RetailCustomerProfileModel
    {
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Address { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    /// <summary>RETAIL-2: kết quả đăng ký / đăng nhập / đổi mật khẩu – phiên gửi lại ở header <c>X-Retail-Token</c>.</summary>
    public class RetailSessionModel
    {
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public RetailCustomerProfileModel Profile { get; set; } = new();
    }

    /// <summary>RETAIL-2: 1 bước tiến độ đơn (Đã nhận đơn → Đã xác nhận → Đang sơ chế → Đang nấu → Đang đóng gói → Đang giao → Đã giao).</summary>
    public class RetailProgressStepModel
    {
        public string Code { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        /// <summary>done / current / pending / cancelled.</summary>
        public string State { get; set; } = string.Empty;
        public DateTime? At { get; set; }
        public string? Note { get; set; }
    }

    public class RetailRouteEventModel
    {
        public string Label { get; set; } = string.Empty;
        public DateTime At { get; set; }
        public string? Note { get; set; }
        public decimal? TemperatureC { get; set; }
    }

    /// <summary>RETAIL-2: chuyến giao chở đơn – chỉ thông tin chung của chuyến, KHÔNG lộ tên / địa chỉ điểm giao của khách khác.</summary>
    public class RetailRouteModel
    {
        public string RouteNumber { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusLabel { get; set; } = string.Empty;
        public string? DriverName { get; set; }
        public string? LicensePlate { get; set; }
        public DateTime? PlannedStart { get; set; }
        public DateTime? PlannedEnd { get; set; }
        public DateTime? ActualStart { get; set; }
        public DateTime? ActualEnd { get; set; }
        public int StopCount { get; set; }
        public int StopDoneCount { get; set; }
        public decimal? LastLatitude { get; set; }
        public decimal? LastLongitude { get; set; }
        public DateTime? LastPositionAt { get; set; }
        public List<RetailRouteEventModel> ListEvent { get; set; } = [];
    }

    public class RetailMyOrderItemModel
    {
        public string OrderNumber { get; set; } = string.Empty;
        public string DeliveryDate { get; set; } = string.Empty;
        public string MealType { get; set; } = string.Empty;
        public string OrderStatus { get; set; } = string.Empty;
        /// <summary>Bước tiến độ hiện tại (vd "Đang nấu").</summary>
        public string StageLabel { get; set; } = string.Empty;
        public string StageCode { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string LineSummary { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public bool HasRoute { get; set; }
    }

    public class RetailMyOrderListModel
    {
        public List<RetailMyOrderItemModel> ListItem { get; set; } = [];
        /// <summary>Đếm theo nhóm: active (chưa giao xong) / done (đã giao) / cancelled.</summary>
        public Dictionary<string, int> GroupCounts { get; set; } = [];
    }

    /// <summary>RETAIL-2: chi tiết đơn cho khách (Đơn của tôi / tra cứu): dòng món + tiến độ + chuyến giao.</summary>
    public class RetailMyOrderDetailModel : RetailTrackModel
    {
        /// <summary>Chỉ có ở Đơn của tôi (đã đăng nhập). Tra cứu công khai (số đơn + SĐT) KHÔNG trả địa chỉ / ghi chú – null thì không xuất trường.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Address { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Note { get; set; }
        public DateTime CreatedDate { get; set; }
        public string StageCode { get; set; } = string.Empty;
        public string StageLabel { get; set; } = string.Empty;
        public List<RetailProgressStepModel> ListStep { get; set; } = [];
        public RetailRouteModel? Route { get; set; }
        /// <summary>Có dữ liệu nguồn gốc (mẻ nấu của ngày / bữa) để xem.</summary>
        public bool HasTrace { get; set; }
    }

    public class RetailTraceIngredientModel
    {
        public string IngredientName { get; set; } = string.Empty;
        public string? LotNumber { get; set; }
        public string? SupplierName { get; set; }
        public DateTime? ReceivedDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string QcLabel { get; set; } = string.Empty;
    }

    public class RetailTraceTemperatureModel
    {
        public string Label { get; set; } = string.Empty;
        public decimal TemperatureC { get; set; }
        public DateTime MeasuredAt { get; set; }
        public bool? IsWithinRange { get; set; }
    }

    public class RetailTraceBatchModel
    {
        public string DishName { get; set; } = string.Empty;
        public string BatchNumber { get; set; } = string.Empty;
        public string StatusLabel { get; set; } = string.Empty;
        public string QcLabel { get; set; } = string.Empty;
        public DateTime? CookStart { get; set; }
        public DateTime? CookEnd { get; set; }
        /// <summary>Thời điểm lưu mẫu thực phẩm của lệnh SX chứa mẻ (khâu "Lưu mẫu" trong quy trình ATTP); null = chưa lưu.</summary>
        public DateTime? SampleRetainedAt { get; set; }
        public List<RetailTraceTemperatureModel> ListTemperature { get; set; } = [];
        public List<RetailTraceIngredientModel> ListIngredient { get; set; } = [];
    }

    /// <summary>
    /// RETAIL-2: nguồn gốc suất ăn của đơn – mẻ nấu của ngày giao + bữa (món theo thực đơn chung), lô nguyên liệu, NCC, nhiệt độ.
    /// Đơn lẻ không gắn mẻ trực tiếp → suy theo ngày + bữa (<see cref="Inferred"/> – PROPOSED – BA/PO CONFIRM). Không lộ PO / giá / khách khác.
    /// </summary>
    public class RetailTraceModel
    {
        public string OrderNumber { get; set; } = string.Empty;
        public string DeliveryDate { get; set; } = string.Empty;
        public string MealType { get; set; } = string.Empty;
        public bool Inferred { get; set; } = true;
        public string? Note { get; set; }
        public List<RetailTraceBatchModel> ListBatch { get; set; } = [];
        public List<string> ListSupplier { get; set; } = [];
    }

    /// <summary>RETAIL-2 (nội bộ): chuyến giao cùng ngày để gán cho đơn lẻ.</summary>
    public class RetailRouteOptionModel
    {
        public Guid Id { get; set; }
        public string Label { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
