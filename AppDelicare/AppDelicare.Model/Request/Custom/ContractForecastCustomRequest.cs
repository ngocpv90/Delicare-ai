using System.ComponentModel.DataAnnotations;

namespace AppDelicare.Model.Request.Custom
{
    /// <summary>
    /// BE2.2 (spec 6.1 POST contracts, Catalog 78): tạo hợp đồng Nháp kèm các loại suất trong 1 transaction. Gửi JSON (danh sách suất lồng nhau – CLAUDE.md 4.2).
    /// ContractNumber trống → tự sinh <c>HD-YYMMDD-NNN</c>; có giá trị (số hợp đồng giấy) → giữ, không trùng.
    /// </summary>
    public class ContractCreateRequest
    {
        [Required]
        public Guid CustomerId { get; set; }
        [MaxLength(255)]
        public string? ContractNumber { get; set; }
        [MaxLength(500)]
        public string? Name { get; set; }
        public DateTime? SignedDate { get; set; }
        [Required]
        public DateTime EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        [MaxLength(2000)]
        public string? PaymentTerm { get; set; }
        public decimal? ContractValue { get; set; }
        [MaxLength(2000)]
        public string? Note { get; set; }
        public List<ContractMealCreateRequest> ListMeal { get; set; } = [];
    }

    public class ContractMealCreateRequest
    {
        [Required]
        [MaxLength(100)]
        public string? MealType { get; set; }
        [MaxLength(500)]
        public string? MealName { get; set; }
        [MaxLength(100)]
        public string? ServiceType { get; set; }
        public decimal? UnitPrice { get; set; }
        public Guid? UnitId { get; set; }
    }

    /// <summary>BE2.2: gửi duyệt / duyệt / từ chối hợp đồng (form). Từ chối bắt buộc Note.</summary>
    public class ContractActionRequest
    {
        [Required]
        public Guid Id { get; set; }
        [MaxLength(1000)]
        public string? Note { get; set; }
    }

    /// <summary>T23 (Catalog 81 ContractExpiryJob 00:30): chuyển hợp đồng Hiệu lực có ngày hết hiệu lực &lt; hôm nay sang Hết hạn (form). Preview = chỉ liệt kê.</summary>
    public class ContractExpireRequest
    {
        public bool Preview { get; set; }
    }

    /// <summary>BE2.2 (spec 6.1 terminate, Catalog 80): chấm dứt hợp đồng Hiệu lực (form). EffectiveTo trống = hôm nay.</summary>
    public class ContractTerminateRequest
    {
        [Required]
        public Guid Id { get; set; }
        [MaxLength(1000)]
        public string? Reason { get; set; }
        public DateTime? EffectiveTo { get; set; }
    }

    /// <summary>BE2.2 (spec 6.5 GET forecasts/matrix, Catalog 87): ma trận khách × ngày (query).</summary>
    public class ForecastMatrixRequest
    {
        [Required]
        public DateTime From { get; set; }
        [Required]
        public DateTime To { get; set; }
        /// <summary>Chi nhánh của khách (<c>Customer.BranchId</c>).</summary>
        public Guid? BranchId { get; set; }
        public Guid? CustomerId { get; set; }
        public string? MealType { get; set; }
        /// <summary>FC-1.3: kèm số gợi ý / trung bình 4 tuần / tuần trước theo khách × ngày (khoảng ≤ 31 ngày).</summary>
        public bool WithSuggestion { get; set; }
    }

    /// <summary>FC-1.1 / 1.2 (D-76): tạo dự báo gợi ý (form). Trống = thứ Hai tuần sau → Chủ nhật tuần kế tiếp; tối đa ForecastSuggestMaxDays (14) ngày.</summary>
    public class ForecastSuggestRequest
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        /// <summary>Trống = mọi khách có hợp đồng hiệu lực.</summary>
        public Guid? CustomerId { get; set; }
        /// <summary>true = ghi đè cả bản Nháp người đã sửa / nhập tay (mặc định không).</summary>
        public bool Overwrite { get; set; }
        /// <summary>true = chỉ tính, không ghi.</summary>
        public bool Preview { get; set; }
    }

    /// <summary>FC-1.5: độ chính xác dự báo (query). Trống = 28 ngày đã qua gần nhất.</summary>
    public class ForecastAccuracyRequest
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public Guid? BranchId { get; set; }
        public Guid? CustomerId { get; set; }
    }

    /// <summary>
    /// BE2.2 (spec 6.5 POST forecasts/bulk, Catalog 88): nhập hàng loạt dự báo (JSON – danh sách). Mỗi dòng = 1 khóa (khách, điểm giao, ngày, ca, loại suất):
    /// bản mới Nháp VersionNo = lớn nhất + 1; bản Nháp cũ cùng khóa → Bị thay thế; bản Đã công bố cũ giữ tới khi bản mới được công bố.
    /// </summary>
    public class ForecastImportRequest
    {
        [MaxLength(1000)]
        public string? Note { get; set; }
        public List<ForecastImportItemRequest> ListItem { get; set; } = [];
    }

    public class ForecastImportItemRequest
    {
        [Required]
        public Guid CustomerId { get; set; }
        public Guid? DeliveryLocationId { get; set; }
        [Required]
        public DateTime ForecastDate { get; set; }
        public Guid? ShiftId { get; set; }
        [MaxLength(100)]
        public string? MealType { get; set; }
        public decimal ForecastQuantity { get; set; }
    }

    /// <summary>BE2.2 (spec 6.5 POST forecasts/publish, Catalog 89): Nháp → Đã công bố trong khoảng ngày (form; ListCustomerId lặp key để lọc khách).</summary>
    public class ForecastPublishRequest
    {
        [Required]
        public DateTime From { get; set; }
        [Required]
        public DateTime To { get; set; }
        public List<Guid> ListCustomerId { get; set; } = [];
    }

    /// <summary>
    /// BE2.2 (spec 6.4 POST change-requests, Catalog 104): yêu cầu đổi món (form). OrderId có → ngày ăn / thực đơn lấy theo đơn; Quantity trống = cả số suất món cũ của đơn.
    /// </summary>
    public class MenuChangeCreateRequest
    {
        public Guid? OrderId { get; set; }
        public Guid? MenuId { get; set; }
        [Required]
        public Guid OriginalDishId { get; set; }
        [Required]
        public Guid RequestedDishId { get; set; }
        public DateTime? MealDate { get; set; }
        public Guid? ShiftId { get; set; }
        public decimal? Quantity { get; set; }
        [MaxLength(1000)]
        public string? Reason { get; set; }
    }

    /// <summary>BE2.2: duyệt / từ chối yêu cầu đổi món (form). Từ chối bắt buộc Note.</summary>
    public class MenuChangeActionRequest
    {
        [Required]
        public Guid Id { get; set; }
        [MaxLength(1000)]
        public string? Note { get; set; }
    }

    /// <summary>
    /// BE2.2 (BUSINESS_RULES §2 ORDER_CHANGE_AFTER_CUTOFF): xin sửa đơn sau giờ chốt (JSON – danh sách dòng). Không ghi ngay: tạo yêu cầu duyệt kèm đề xuất,
    /// duyệt → ghi số mới + thêm dòng mới, đơn → Đã chốt. ListLine = dòng có sẵn (0 = bỏ dòng); ListNewLine = loại suất hợp đồng chưa có trên đơn.
    /// </summary>
    public class OrderChangeAfterCutoffRequest
    {
        [Required]
        public Guid Id { get; set; }
        [MaxLength(1000)]
        public string? Reason { get; set; }
        public List<OrderLineQuantityRequest> ListLine { get; set; } = [];
        public List<OrderCreateLineRequest> ListNewLine { get; set; } = [];
    }
}
