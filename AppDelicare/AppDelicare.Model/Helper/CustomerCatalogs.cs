namespace AppDelicare.Model.Helper
{
    /// <summary>Một mục danh mục: mã lưu trong cột + nhãn tiếng Việt hiển thị.</summary>
    public sealed record CatalogItem(string Code, string Name);

    /// <summary>
    /// UI-5.3 (D-63 r): danh mục trường khách hàng – lưu MÃ ở cột có sẵn, hiển thị tiếng Việt. Giao diện lấy cùng bộ mã qua
    /// <c>Customer/GetFormOptions</c> (catalog FE <c>core/store.js</c> giữ bản sao cho chế độ DEMO – sửa cả hai chỗ).
    /// Máy chủ chỉ nhận mã trong danh mục; giá trị cũ ngoài danh mục được giữ nếu không sửa trường đó.
    /// </summary>
    public static class CustomerCatalogs
    {
        public static readonly IReadOnlyList<CatalogItem> Industry =
        [
            new("HOSPITAL", "Bệnh viện"), new("FACTORY", "Nhà máy"), new("CONSTRUCTION", "Công trường"),
            new("SCHOOL", "Trường học"), new("OFFICE", "Văn phòng"), new("OTHER", "Khác"),
        ];

        /// <summary>Quy mô = số suất / ngày.</summary>
        public static readonly IReadOnlyList<CatalogItem> CompanySize =
        [
            new("LT200", "Dưới 200 suất / ngày"), new("200_500", "200 – 500 suất / ngày"), new("500_1000", "500 – 1.000 suất / ngày"), new("GT1000", "Trên 1.000 suất / ngày"),
        ];

        public static readonly IReadOnlyList<CatalogItem> Gender = [new("MALE", "Nam"), new("FEMALE", "Nữ"), new("OTHER", "Khác")];

        public static readonly IReadOnlyList<CatalogItem> Source = [new("REFERRAL", "Giới thiệu"), new("TENDER", "Đấu thầu"), new("SELF", "Tự tìm"), new("OTHER", "Khác")];

        /// <summary>Trạng thái khách hàng (UI-5.5) – mặc định Đang hoạt động.</summary>
        public static readonly IReadOnlyList<CatalogItem> Status = [new("Active", "Đang hoạt động"), new("Inactive", "Ngừng hoạt động")];

        /// <summary>Tên cột → (nhãn trường, danh mục).</summary>
        public static readonly IReadOnlyDictionary<string, (string Label, IReadOnlyList<CatalogItem> Items)> ByField = new Dictionary<string, (string, IReadOnlyList<CatalogItem>)>
        {
            ["Industry"] = ("Ngành", Industry),
            ["CompanySize"] = ("Quy mô", CompanySize),
            ["Gender"] = ("Giới tính", Gender),
            ["Source"] = ("Nguồn", Source),
        };

        /// <summary>
        /// Chuẩn hóa + kiểm 1 trường danh mục. Trống → null. Đúng mã (không phân biệt hoa thường) → mã chuẩn.
        /// Bằng đúng giá trị đang lưu (dữ liệu cũ ngoài danh mục, không sửa) → giữ nguyên. Còn lại → lỗi.
        /// </summary>
        public static (string? Value, string? Error) Check(string field, string? value, string? stored)
        {
            var (label, items) = ByField[field];
            string? v = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            if (v == null)
            {
                return (null, null);
            }
            var hit = items.FirstOrDefault(x => string.Equals(x.Code, v, StringComparison.OrdinalIgnoreCase));
            if (hit != null)
            {
                return (hit.Code, null);
            }
            if (stored != null && v == stored.Trim())
            {
                return (stored, null);
            }
            return (null, string.Format(ConstantHelper.Message.CustomerCatalogInvalid, label, v, string.Join(", ", items.Select(x => x.Name))));
        }
    }
}
