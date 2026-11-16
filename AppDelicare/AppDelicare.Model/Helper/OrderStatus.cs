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
}
