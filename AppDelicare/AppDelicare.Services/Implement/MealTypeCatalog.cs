using AppDelicare.Model.Helper;
using AppDelicare.Model.Option;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// Lỗi UAT 2026-11-21: loại suất của hợp đồng (ContractMeal.MealType) phải lấy từ danh mục <c>ReferenceLists:MealType</c> (appsettings – GAP-F-06, D-10:
    /// SANG / TRUA / TOI / DEM), không nhận chữ tự do. Nhận mã hoặc nhãn, không phân biệt hoa thường; trả về MÃ chuẩn.
    /// Danh mục chưa cấu hình → không chặn (giữ hành vi cũ). Giá trị cũ không có trong danh mục được giữ khi KHÔNG đổi (<paramref name="stored"/>).
    /// </summary>
    public static class MealTypeCatalog
    {
        public const string ListName = "MealType";

        public static IReadOnlyList<ReferenceItem> Items(ReferenceListsOptions? options)
            => options?.Lists != null && options.Lists.TryGetValue(ListName, out var list) && list != null ? list : [];

        /// <summary>(Mã chuẩn, lỗi). Trống → (null, null) – bắt buộc kiểm ở nơi gọi.</summary>
        public static (string? Code, string? Error) Check(ReferenceListsOptions? options, string? value, string? stored = null)
        {
            string v = (value ?? string.Empty).Trim();
            if (v.Length == 0)
            {
                return (null, null);
            }
            var items = Items(options);
            if (items.Count == 0)
            {
                return (v, null);
            }
            var hit = items.FirstOrDefault(i => string.Equals(i.Code, v, StringComparison.OrdinalIgnoreCase))
                ?? items.FirstOrDefault(i => string.Equals(i.Label, v, StringComparison.OrdinalIgnoreCase));
            if (hit != null)
            {
                return (hit.Code, null);
            }
            if (stored != null && string.Equals(stored.Trim(), v, StringComparison.Ordinal))
            {
                return (stored.Trim(), null);
            }
            string allowed = string.Join(", ", items.Select(i => $"{i.Code} – {i.Label}"));
            return (null, string.Format(ConstantHelper.Message.ContractMealTypeNotInCatalog, v, allowed));
        }
    }
}
