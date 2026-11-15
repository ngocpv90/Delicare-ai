namespace AppDelicare.Model.SearchRequest
{
    /// <summary>T45R: lọc danh sách xung đột dữ liệu (spec 3.6).</summary>
    public class CoreDataMergeConflictSearchRequest : BaseSearchRequest
    {
        /// <summary>true = đã xử lý, false = chưa xử lý, trống = tất cả.</summary>
        public bool? IsResolved { get; set; }
        public string? EntityName { get; set; }
        public string? MigrationCode { get; set; }
    }
}
