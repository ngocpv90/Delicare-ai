namespace AppDelicare.Model.Helper
{
    /// <summary>
    /// MA-1.2 (D-63 f): loại khách hàng – chỉ chọn trong danh mục, lưu ở cột có sẵn <c>Customer.CustomerType</c>.
    /// Giá trị cũ (HOSPITAL, FACTORY…) là ngành → script MA-1.4 chuyển sang <c>Industry</c>, loại = BUSINESS; tới lúc đó máy chủ coi giá trị cũ là BUSINESS.
    /// </summary>
    public static class CustomerTypes
    {
        /// <summary>Doanh nghiệp → mã <c>KHB-NNNNN</c>.</summary>
        public const string Business = "BUSINESS";
        /// <summary>Cá nhân → mã <c>KHC-NNNNN</c>.</summary>
        public const string Individual = "INDIVIDUAL";

        public static readonly IReadOnlyList<string> All = [Business, Individual];

        public static string Title(string? type) => Normalize(type) == Individual ? "Cá nhân" : "Doanh nghiệp";

        /// <summary>Loại hiệu lực của dữ liệu đã lưu: INDIVIDUAL giữ nguyên, mọi giá trị khác (kể cả giá trị cũ) = BUSINESS.</summary>
        public static string Effective(string? stored) => Normalize(stored) == Individual ? Individual : Business;

        /// <summary>Chuẩn hóa giá trị gửi lên; không thuộc danh mục → null.</summary>
        public static string? Normalize(string? type)
        {
            string t = (type ?? string.Empty).Trim().ToUpperInvariant();
            return All.Contains(t) ? t : null;
        }
    }
}
