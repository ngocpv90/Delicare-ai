namespace AppDelicare.Model.Helper
{
    /// <summary>
    /// UI-6.4 (D-83, anh chốt 2026-11-23): "Phụ lục điều chỉnh giá" của hợp đồng = dòng <c>PricePolicy</c> có <c>ContractId</c> (không thêm bảng / cột).
    /// Nháp → (đính kèm phụ lục đã ký) Gửi duyệt → Chờ duyệt → Hiệu lực / Từ chối. "Hết hiệu lực" là trạng thái suy ra (Hiệu lực nhưng đã qua ngày đến).
    /// Duyệt qua engine – mục quyền <c>#/wf-inbox?type=crm-price-policy</c> "Phê duyệt › Chính sách giá" (CEO – D-79 d).
    /// </summary>
    public static class PriceAdjustmentStatus
    {
        public const string BusinessType = "PRICE_ADJUSTMENT";
        public const string ScreenId = "crm-price-policy";
        /// <summary>Tài nguyên đính kèm (CommonRegistry / DocumentFile) – file phụ lục gắn theo từng dòng điều chỉnh.</summary>
        public const string Resource = "PricePolicy";

        public const string Draft = "Draft";
        public const string PendingApproval = "PendingApproval";
        public const string Active = "Active";
        public const string Rejected = "Rejected";
        public const string Expired = "Expired";

        public static string Label(string? status) => status switch
        {
            Draft => "Nháp",
            PendingApproval => "Chờ duyệt",
            Active => "Hiệu lực",
            Rejected => "Từ chối",
            Expired => "Hết hiệu lực",
            _ => status ?? string.Empty,
        };
    }
}
