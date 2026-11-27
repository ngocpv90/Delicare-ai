namespace AppDelicare.Model.Request.Custom
{
    /// <summary>RETAIL-1: 1 dòng món gửi lên – nhận <c>ProductId</c> (ưu tiên) hoặc <c>ProductCode</c>. Giá máy chủ tính lại.</summary>
    public class RetailOrderLineInput
    {
        public Guid? ProductId { get; set; }
        public string? ProductCode { get; set; }
        public decimal Quantity { get; set; }
    }

    /// <summary>RETAIL-1: khách gửi đơn trên trang khách lẻ (RetailPortal/SubmitOrder – [FromBody] vì có danh sách dòng).</summary>
    public class RetailSubmitOrderRequest
    {
        public string? CustomerName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
        /// <summary>Ngày nhận (yyyy-MM-dd).</summary>
        public string? DeliveryDate { get; set; }
        /// <summary>Bữa: nhãn (Trưa) hoặc mã (TRUA) của ReferenceLists:MealType.</summary>
        public string? MealType { get; set; }
        public string? Note { get; set; }
        /// <summary>Ô bẫy (honeypot) ẩn trên trang – có giá trị = máy gửi tự động.</summary>
        public string? Website { get; set; }
        /// <summary>Mã lần gửi do trang sinh – gửi lại cùng mã trả lại đơn cũ.</summary>
        public string? ClientRequestId { get; set; }
        public List<RetailOrderLineInput> Lines { get; set; } = [];
    }

    /// <summary>RETAIL-1: nhân viên lập đơn khi khách gọi / nhắn Zalo (RetailOrder/Create – [FromBody]).</summary>
    public class RetailOrderCreateRequest : RetailSubmitOrderRequest
    {
        /// <summary>PHONE / ZALO.</summary>
        public string? Channel { get; set; }
        public decimal? ShippingFee { get; set; }
        /// <summary>COD / BANK.</summary>
        public string? PaymentMethod { get; set; }
    }

    /// <summary>RETAIL-1: sửa đơn Mới / Đã xác nhận (RetailOrder/UpdateLines – [FromBody]).</summary>
    public class RetailOrderUpdateLinesRequest
    {
        public Guid Id { get; set; }
        public string? Address { get; set; }
        public string? DeliveryDate { get; set; }
        public string? MealType { get; set; }
        public string? Note { get; set; }
        public decimal? ShippingFee { get; set; }
        public string? PaymentMethod { get; set; }
        public List<RetailOrderLineInput> Lines { get; set; } = [];
    }

    /// <summary>RETAIL-1: chuyển trạng thái đơn ([FromForm]).</summary>
    public class RetailOrderTransitionRequest
    {
        public Guid Id { get; set; }
        public string? To { get; set; }
        /// <summary>Bắt buộc khi hủy.</summary>
        public string? Reason { get; set; }
        /// <summary>Khi xác nhận: phí giao, hình thức thanh toán.</summary>
        public decimal? ShippingFee { get; set; }
        public string? PaymentMethod { get; set; }
        /// <summary>RETAIL-2: khi chuyển "Đang giao" – chuyến giao chở đơn (không bắt buộc; khách theo dõi chuyến).</summary>
        public Guid? DeliveryRouteId { get; set; }
    }

    ///<summary>RETAIL-1: lọc danh sách đơn khách lẻ.</summary>
    public class RetailOrderListRequest
    {
        /// <summary>Số đơn / tên / SĐT.</summary>
        public string? SearchText { get; set; }
        /// <summary>Một hoặc nhiều trạng thái, cách dấu phẩy (New,Confirmed,Delivering).</summary>
        public string? OrderStatus { get; set; }
        public string? Channel { get; set; }
        /// <summary>Ngày nhận từ / đến (yyyy-MM-dd).</summary>
        public string? FromDate { get; set; }
        public string? ToDate { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 100;
    }

    /// <summary>RETAIL-2: đăng ký tài khoản khách lẻ (SĐT + mật khẩu).</summary>
    public class RetailRegisterRequest
    {
        /// <summary>D-136 j: INDIVIDUAL (mặc định) / BUSINESS. Doanh nghiệp bắt buộc CompanyName + TaxCode + Email.</summary>
        public string? AccountType { get; set; }
        public string? CompanyName { get; set; }
        public string? TaxCode { get; set; }
        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Password { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        /// <summary>Ô bẫy chống máy tự động – người thật để trống.</summary>
        public string? Website { get; set; }
    }

    public class RetailLoginRequest
    {
        public string? PhoneNumber { get; set; }
        public string? Password { get; set; }
    }

    public class RetailProfileRequest
    {
        /// <summary>Chỉ áp cho tài khoản doanh nghiệp (loại tài khoản không đổi được).</summary>
        public string? CompanyName { get; set; }
        public string? TaxCode { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
    }

    public class RetailChangePasswordRequest
    {
        public string? CurrentPassword { get; set; }
        public string? NewPassword { get; set; }
    }

    /// <summary>RETAIL-2: gắn đơn đặt trước khi có tài khoản vào tài khoản (cần đúng số đơn; SĐT đơn phải trùng SĐT tài khoản).</summary>
    public class RetailClaimOrderRequest
    {
        public string? OrderNumber { get; set; }
    }

    /// <summary>RETAIL-2 (nội bộ): gán / bỏ chuyến giao cho đơn khách lẻ (DeliveryRouteId trống = bỏ gán).</summary>
    public class RetailOrderAssignRouteRequest
    {
        public Guid Id { get; set; }
        public Guid? DeliveryRouteId { get; set; }
    }
}
