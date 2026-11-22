namespace AppDelicare.Model.Helper
{
    /// <summary>T13 – trạng thái đơn hàng (STATUS_CATALOG: Order.Status, PROPOSED – BA/PO CONFIRM).</summary>
    public static class OrderStatus
    {
        public const string Draft = "Draft";
        public const string Submitted = "Submitted";
        public const string Confirmed = "Confirmed";
        public const string Locked = "Locked";
        public const string InProduction = "InProduction";
        public const string Delivered = "Delivered";
        public const string Reconciled = "Reconciled";
        public const string Cancelled = "Cancelled";

        /// <summary>ApprovalRequest.BusinessType: sửa số suất đơn đã xác nhận lệch quá ngưỡng (BusinessRules:PortionDeviationPctNeedApproval).</summary>
        public const string DeviationBusinessType = "ORDER_DEVIATION";

        /// <summary>BE2.2 (BUSINESS_RULES §2): thêm / sửa đơn sau giờ chốt – chỉ ghi khi được duyệt. Quyền duyệt = mục <c>#/wf-inbox?type=crm-order</c>.</summary>
        public const string ChangeAfterCutoffBusinessType = "ORDER_CHANGE_AFTER_CUTOFF";

        /// <summary>Các loại yêu cầu duyệt gắn với 1 đơn hàng (chặn sửa tiếp khi đang chờ).</summary>
        public static readonly string[] ApprovalBusinessTypes = [DeviationBusinessType, ChangeAfterCutoffBusinessType];

        /// <summary>Tên hiển thị tiếng Việt (STATUS_CATALOG).</summary>
        public static string Label(string? status) => status switch
        {
            Draft => "Nháp",
            Submitted => "Đã gửi",
            Confirmed => "Đã xác nhận",
            Locked => "Đã chốt",
            InProduction => "Đang sản xuất",
            Delivered => "Đã giao",
            Reconciled => "Đã đối soát",
            Cancelled => "Hủy",
            _ => status ?? string.Empty,
        };
    }

    /// <summary>T13 – trạng thái thực đơn (STATUS_CATALOG: Menu.Status, PROPOSED – BA/PO CONFIRM).</summary>
    /// <summary>ORD-OPT-1.2 (D-74 f): loại thực đơn (<c>Menu.MenuType</c>, ReferenceLists:MenuType). Trống = thực đơn riêng (dữ liệu cũ).</summary>
    public static class MenuScope
    {
        public const string Common = "COMMON";
        public const string Customer = "CUSTOMER";

        public static bool IsCommon(string? menuType) => string.Equals(menuType?.Trim(), Common, StringComparison.OrdinalIgnoreCase);
    }

    public static class MenuStatus
    {
        public const string Draft = "Draft";
        public const string Approved = "Approved";
        public const string Published = "Published";
        public const string Expired = "Expired";

        public static string Label(string? status) => status switch
        {
            Draft => "Nháp",
            Approved => "Đã duyệt",
            Published => "Đã công bố",
            Expired => "Hết hiệu lực",
            _ => status ?? string.Empty,
        };
    }

    /// <summary>BE2.2 – trạng thái hợp đồng (STATUS_CATALOG: Contract.Status, PROPOSED – BA/PO CONFIRM).</summary>
    public static class ContractStatus
    {
        public const string Draft = "Draft";
        public const string PendingApproval = "PendingApproval";
        public const string Active = "Active";
        public const string Expired = "Expired";
        public const string Terminated = "Terminated";

        /// <summary>ApprovalRequest.BusinessType của hợp đồng (spec 6.1 submit – 1 cấp; BE2.3 chuyển sang nhiều cấp).</summary>
        public const string BusinessType = "CONTRACT";
        public const string ScreenId = "crm-contract";

        public static string Label(string? status) => status switch
        {
            Draft => "Nháp",
            PendingApproval => "Chờ duyệt",
            Active => "Hiệu lực",
            Expired => "Hết hạn",
            Terminated => "Chấm dứt",
            _ => status ?? string.Empty,
        };
    }

    /// <summary>BE2.2 – trạng thái dự báo (STATUS_CATALOG: Forecast.Status, PROPOSED – BA/PO CONFIRM).</summary>
    public static class ForecastStatus
    {
        public const string Draft = "Draft";
        public const string Published = "Published";
        public const string Superseded = "Superseded";

        public static string Label(string? status) => status switch
        {
            Draft => "Nháp",
            Published => "Đã công bố",
            Superseded => "Bị thay thế",
            _ => status ?? string.Empty,
        };
    }

    /// <summary>BE2.2 – trạng thái yêu cầu đổi món (STATUS_CATALOG: MenuChangeRequest.RequestStatus).</summary>
    public static class MenuChangeStatus
    {
        public const string Pending = "Pending";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
        public const string Cancelled = "Cancelled";

        /// <summary>ApprovalRequest.BusinessType (spec 6.4 – MENU_CHANGE).</summary>
        public const string BusinessType = "MENU_CHANGE";
        public const string ScreenId = "menu-change-request";

        public static string Label(string? status) => status switch
        {
            Pending => "Chờ duyệt",
            Approved => "Đã duyệt",
            Rejected => "Từ chối",
            Cancelled => "Đã hủy",
            _ => status ?? string.Empty,
        };
    }
}
