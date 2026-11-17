namespace AppDelicare.Model.Model
{
    /// <summary>BE3.3 (Catalog 18, spec 12.1): 1 cảnh báo của chuông trên header – chỉ trả khi <see cref="Count"/> &gt; 0.</summary>
    public class AlertModel
    {
        /// <summary>Mã cảnh báo cố định (STOCK_BELOW_MIN, APPROVAL_PENDING, …) – FE dùng làm khóa / chọn biểu tượng.</summary>
        public string Code { get; set; } = string.Empty;
        /// <summary>HIGH / MEDIUM / LOW.</summary>
        public string Severity { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        /// <summary>Route Angular để điều hướng (vd. <c>#/scm-inv-balance</c>).</summary>
        public string Route { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    /// <summary>BE3.3 (Catalog 17): 1 màn hình (mục menu người dùng có quyền) khớp từ khóa.</summary>
    public class SearchScreenModel
    {
        public string Title { get; set; } = string.Empty;
        public string Route { get; set; } = string.Empty;
    }

    /// <summary>BE3.3 (Catalog 17): 1 bản ghi khớp từ khóa (chỉ 8 loại trong danh sách trắng – D-58: không có tên bảng / cột).</summary>
    public class SearchRecordModel
    {
        /// <summary>Loại bản ghi = tên danh sách của API dùng chung (như <c>Common/GetResources</c>, dùng tiếp cho <c>Common/GetRelated</c> / <c>GetHistory</c>).</summary>
        public string Type { get; set; } = string.Empty;
        public string TypeTitle { get; set; } = string.Empty;
        public string Route { get; set; } = string.Empty;
        public Guid Id { get; set; }
        public string? Code { get; set; }
        public string? Label { get; set; }
        public string? Status { get; set; }
        public string? StatusName { get; set; }
    }

    /// <summary>BE3.3 (Catalog 17): kết quả tìm kiếm toàn cục.</summary>
    public class GlobalSearchModel
    {
        public string Q { get; set; } = string.Empty;
        public List<SearchScreenModel> Screens { get; set; } = [];
        public List<SearchRecordModel> Records { get; set; } = [];
    }

    /// <summary>BE3.3 (Catalog 16): 1 dòng chứng từ liên quan.</summary>
    public class RelatedItemModel
    {
        public Guid Id { get; set; }
        public string? Label { get; set; }
        public string? Status { get; set; }
        public string? StatusName { get; set; }
    }

    /// <summary>BE3.3 (Catalog 16): 1 nhóm của tab "Liên quan" = 1 màn hình (tên màn hình + route), tối đa N dòng + tổng số.</summary>
    public class RelatedGroupModel
    {
        /// <summary>Khóa nhóm (tên danh sách của API dùng chung) – dùng tiếp cho <c>Common/GetRelated</c> / <c>GetHistory</c> khi có trong danh sách trắng.</summary>
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Route { get; set; } = string.Empty;
        public int Count { get; set; }
        public List<RelatedItemModel> Items { get; set; } = [];
    }
}
