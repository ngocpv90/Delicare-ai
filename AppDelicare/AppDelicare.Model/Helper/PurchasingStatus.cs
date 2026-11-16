namespace AppDelicare.Model.Helper
{
    /// <summary>T14 – trạng thái dòng nhu cầu NL (STATUS_CATALOG: MaterialRequirement.Status, PROPOSED – BA/PO CONFIRM).</summary>
    public static class MaterialRequirementStatus
    {
        public const string Calculated = "Calculated";
        public const string Shortage = "Shortage";
        public const string Sufficient = "Sufficient";
        public const string Requested = "Requested";

        public static string Label(string? status) => status switch
        {
            Calculated => "Đã tính",
            Shortage => "Thiếu",
            Sufficient => "Đủ",
            Requested => "Đã đề nghị mua",
            _ => status ?? string.Empty,
        };
    }

    /// <summary>T14 – trạng thái đề nghị mua (STATUS_CATALOG: PurchaseRequest.Status, PROPOSED – BA/PO CONFIRM).</summary>
    public static class PurchaseRequestStatus
    {
        public const string Draft = "Draft";
        public const string PendingApproval = "PendingApproval";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
        public const string Converted = "Converted";
        public const string Cancelled = "Cancelled";

        public static string Label(string? status) => status switch
        {
            Draft => "Nháp",
            PendingApproval => "Chờ duyệt",
            Approved => "Đã duyệt",
            Rejected => "Từ chối",
            Converted => "Đã lập PO",
            Cancelled => "Hủy",
            _ => status ?? string.Empty,
        };
    }

    /// <summary>T14 – trạng thái kế hoạch sản xuất (STATUS_CATALOG: ProductionPlan.Status, PROPOSED – BA/PO CONFIRM).</summary>
    public static class ProductionPlanStatus
    {
        public const string Draft = "Draft";
        public const string Approved = "Approved";
        public const string Released = "Released";
        public const string Completed = "Completed";

        public static string Label(string? status) => status switch
        {
            Draft => "Nháp",
            Approved => "Đã duyệt",
            Released => "Đã phát hành",
            Completed => "Hoàn thành",
            _ => status ?? string.Empty,
        };
    }
}
