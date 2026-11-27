namespace AppDelicare.Model.Helper
{
    /// <summary>
    /// RETAIL-1 (docs/frontend-handoff/RETAIL-1.md mục 4, STATUS_CATALOG): máy trạng thái <c>RetailOrder.OrderStatus</c>.
    /// New → Confirmed / Cancelled · Confirmed → Delivering / Cancelled · Delivering → Completed / Cancelled. PROPOSED – BA/PO CONFIRM.
    /// </summary>
    public static class RetailOrderStatus
    {
        public const string New = "New";
        public const string Confirmed = "Confirmed";
        public const string Delivering = "Delivering";
        public const string Completed = "Completed";
        public const string Cancelled = "Cancelled";

        public static readonly string[] All = [New, Confirmed, Delivering, Completed, Cancelled];

        /// <summary>Trạng thái còn xử lý (sửa dòng / phí giao được khi New / Confirmed).</summary>
        public static readonly string[] Editable = [New, Confirmed];

        private static readonly Dictionary<string, string[]> Next = new(StringComparer.Ordinal)
        {
            [New] = [Confirmed, Cancelled],
            [Confirmed] = [Delivering, Cancelled],
            [Delivering] = [Completed, Cancelled],
            [Completed] = [],
            [Cancelled] = [],
        };

        private static readonly Dictionary<string, string> Labels = new(StringComparer.Ordinal)
        {
            [New] = "Mới", [Confirmed] = "Đã xác nhận", [Delivering] = "Đang giao", [Completed] = "Hoàn thành", [Cancelled] = "Đã hủy",
        };

        public static string[] AllowedFrom(string? from) => from != null && Next.TryGetValue(from, out var n) ? n : [];

        public static bool CanMove(string? from, string to) => AllowedFrom(from).Contains(to);

        public static string Label(string? status) => status != null && Labels.TryGetValue(status, out var l) ? l : status ?? string.Empty;
    }

    /// <summary>RETAIL-1: kênh đặt, thanh toán của đơn khách lẻ.</summary>
    public static class RetailCodes
    {
        public const string ChannelWeb = "WEB";
        public const string ChannelPhone = "PHONE";
        public const string ChannelZalo = "ZALO";
        public static readonly string[] StaffChannels = [ChannelPhone, ChannelZalo];

        public const string PayCod = "COD";
        public const string PayBank = "BANK";
        public static readonly string[] PaymentMethods = [PayCod, PayBank];

        public const string Unpaid = "UNPAID";
        public const string Paid = "PAID";

        public const string ProductActive = "Active";
        public const string ProductInactive = "Inactive";
        public static readonly string[] ProductStatuses = [ProductActive, ProductInactive];

        /// <summary>Danh mục tham chiếu (appsettings ReferenceLists).</summary>
        public const string CategoryList = "RetailCategory";
        public const string MealTypeList = "MealType";
    }

    /// <summary>RETAIL-1: thông báo tiếng Việt của module Bán lẻ (gom một chỗ như SupplierReturnService.Msg).</summary>
    public static class RetailMessage
    {
        public const string NameRequired = "Vui lòng nhập tên người nhận.";
        public const string PhoneInvalid = "Số điện thoại chưa đúng (10 số, bắt đầu bằng 03, 05, 07, 08 hoặc 09).";
        public const string AddressRequired = "Vui lòng nhập địa chỉ giao hàng.";
        public const string DateInvalid = "Ngày nhận hàng chưa đúng.";
        public const string DateTooEarly = "Ngày nhận sớm nhất là {0} (đơn đặt sau {1}:00 giao từ ngày kia) – vui lòng chọn ngày khác.";
        public const string DateTooLate = "Chỉ nhận đặt trước tối đa {0} ngày (đến {1}).";
        public const string MealInvalid = "Bữa nhận hàng chưa đúng ({0}).";
        public const string LinesRequired = "Đơn chưa có món nào.";
        public const string TooManyLines = "Đơn có quá nhiều món (tối đa {0}) – vui lòng gọi điện đặt hàng.";
        public const string QuantityInvalid = "Số lượng của \"{0}\" phải từ 1 đến {1}.";
        public const string ProductNotFound = "Có món không còn trong thực đơn bán lẻ – vui lòng tải lại trang và đặt lại.";
        public const string ProductStopped = "Sản phẩm \"{0}\" đã ngừng bán.";
        public const string MealNotSold = "Bữa {0} không bán: {1}.";
        public const string TooManyRequests = "Bạn đã gửi nhiều đơn trong thời gian ngắn – vui lòng thử lại sau hoặc gọi điện đặt hàng.";
        public const string TrackTooMany = "Bạn đã tra cứu quá nhiều lần – vui lòng thử lại sau ít phút.";
        public const string TrackNotFound = "Không tìm thấy đơn – kiểm tra lại số đơn và số điện thoại đã dùng khi đặt.";
        public const string OrderSubmitted = "Đã gửi đơn – nhân viên sẽ gọi xác nhận.";
        public const string OrderNotFound = "Không tìm thấy đơn khách lẻ.";
        public const string ChannelInvalid = "Kênh đặt hàng chưa đúng (Điện thoại hoặc Zalo).";
        public const string PaymentInvalid = "Hình thức thanh toán chưa đúng.";
        public const string ShippingFeeInvalid = "Phí giao không được âm.";
        public const string TargetInvalid = "Trạng thái chuyển tới chưa đúng.";
        public const string TransitionInvalid = "Đơn đang ở trạng thái \"{0}\" – không chuyển sang \"{1}\" được.";
        public const string CancelReasonRequired = "Vui lòng nhập lý do hủy đơn.";
        public const string NotEditable = "Chỉ sửa được đơn Mới hoặc Đã xác nhận (đơn đang \"{0}\").";
        public const string JobUserMissing = "Máy chủ chưa cấu hình tài khoản hệ thống để nhận đơn trên trang web – vui lòng gọi điện đặt hàng.";
        public const string ProductNameRequired = "Vui lòng nhập tên món.";
        public const string ProductCategoryInvalid = "Nhóm sản phẩm chưa đúng.";
        public const string ProductUnitRequired = "Vui lòng chọn đơn vị.";
        public const string ProductPriceInvalid = "Giá bán phải lớn hơn 0.";
        public const string ProductMealsRequired = "Chọn ít nhất 1 bữa bán.";
        public const string ProductStatusInvalid = "Trạng thái sản phẩm chưa đúng.";
        public const string ProductInUse = "Sản phẩm đã có trong đơn khách lẻ – không xóa được, hãy chuyển sang Ngừng bán.";

        // RETAIL-2: tài khoản khách lẻ + Đơn của tôi.
        public const string PasswordTooShort = "Mật khẩu cần tối thiểu {0} ký tự.";
        public const string PhoneTaken = "Số điện thoại này đã có tài khoản – vui lòng đăng nhập (hoặc gọi hotline nếu quên mật khẩu).";
        public const string LoginFailed = "Số điện thoại hoặc mật khẩu chưa đúng.";
        public const string LoginLocked = "Đăng nhập sai quá nhiều lần – vui lòng thử lại sau {0} phút.";
        public const string AccountInactive = "Tài khoản đang bị khóa – vui lòng gọi hotline để được hỗ trợ.";
        public const string SessionExpired = "Phiên đăng nhập đã hết hạn – vui lòng đăng nhập lại.";
        public const string CurrentPasswordWrong = "Mật khẩu hiện tại chưa đúng.";
        public const string EmailInvalid = "Email chưa đúng.";
        // D-136 j: tài khoản doanh nghiệp bắt buộc tên DN + mã số thuế + email.
        public const string AccountTypeInvalid = "Loại tài khoản chưa đúng – chọn Cá nhân hoặc Doanh nghiệp.";
        public const string CompanyNameRequired = "Vui lòng nhập tên doanh nghiệp.";
        public const string TaxCodeInvalid = "Mã số thuế chưa đúng (10 số, hoặc 10 số - 3 số với chi nhánh).";
        public const string TaxCodeTaken = "Mã số thuế này đã có tài khoản – vui lòng đăng nhập bằng số điện thoại đã đăng ký (hoặc gọi hotline).";
        public const string BusinessEmailRequired = "Tài khoản doanh nghiệp cần email để nhận hóa đơn / thông báo.";
        public const string Registered = "Đã tạo tài khoản.";
        public const string LoggedIn = "Đăng nhập thành công.";
        public const string ProfileSaved = "Đã lưu thông tin.";
        public const string PasswordChanged = "Đã đổi mật khẩu.";
        public const string RegisterTooMany = "Đã tạo quá nhiều tài khoản trong thời gian ngắn – vui lòng thử lại sau.";
        public const string ClaimNotFound = "Không tìm thấy đơn đặt bằng số điện thoại của tài khoản này.";
        public const string ClaimOtherAccount = "Đơn này đã gắn với tài khoản khác.";
        public const string Claimed = "Đã thêm đơn vào Đơn của tôi.";
        public const string RouteNotFound = "Không tìm thấy chuyến giao.";
        public const string RouteDateMismatch = "Chuyến giao {0} không cùng ngày nhận hàng của đơn ({1}).";
        public const string TraceNotReady = "Bếp chưa lên mẻ nấu cho ngày {0} – nguồn gốc sẽ có khi bếp bắt đầu sơ chế.";
        public const string AccountUnavailable = "Máy chủ chưa sẵn sàng cho tài khoản khách – vui lòng gọi hotline để đặt hàng.";
    }
}
