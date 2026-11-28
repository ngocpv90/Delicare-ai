using AppDelicare.Data.Connection;
using AppDelicare.Model.Option;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// PKG-FEE (anh duyệt 2026-11-28): danh mục loại hộp / khay chuyển từ appsettings (ReferenceLists:PackagingType) sang bảng <c>PackagingType</c> (màn Loại hộp / khay).
    /// Bảng có dòng → danh sách loại = các loại ĐANG DÙNG trong bảng (thay danh sách appsettings); bảng trống (DB cũ chưa seed) → giữ appsettings như trước.
    /// Phí thất thoát / cái: bảng (<c>LossFee</c>) → không có thì <c>BusinessRules:ContainerLossFee</c> (cũ).
    /// </summary>
    public static class PackagingTypeCatalog
    {
        public const string ListName = "PackagingType";

        public static ReferenceListsOptions Merge(DatabaseContext db, ReferenceListsOptions lists)
        {
            var rows = db.PackagingType.Where(x => !x.IsDeleted && x.Status == "Active").OrderBy(x => x.SortOrder ?? int.MaxValue).ThenBy(x => x.Code)
                .Select(x => new ReferenceItem { Code = x.Code, Label = x.Name }).ToList();
            if (rows.Count == 0)
            {
                return lists;
            }
            var copy = new ReferenceListsOptions { Lists = new Dictionary<string, List<ReferenceItem>>(lists.Lists, StringComparer.OrdinalIgnoreCase) };
            copy.Lists[ListName] = rows;
            return copy;
        }

        /// <summary>Tên loại theo mã (kể cả loại đã ngừng dùng – dữ liệu cũ vẫn hiện đúng tên).</summary>
        public static string Label(DatabaseContext db, ReferenceListsOptions lists, string? code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return string.Empty;
            }
            string? name = db.PackagingType.Where(x => !x.IsDeleted && x.Code == code).Select(x => x.Name).FirstOrDefault();
            return name ?? (lists.Lists.TryGetValue(ListName, out var l) && l.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase)) is { } i ? i.Label : code);
        }

        public static decimal LossFee(DatabaseContext db, BusinessRulesOptions rules, string? code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return 0m;
            }
            decimal? fee = db.PackagingType.Where(x => !x.IsDeleted && x.Code == code).Select(x => (decimal?)x.LossFee).FirstOrDefault();
            return fee ?? rules.ContainerLossFee.GetValueOrDefault(code);
        }
    }
}
