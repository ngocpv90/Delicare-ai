namespace AppDelicare.Model.ViewModels
{
    /// <summary>T45R: dòng danh sách xung đột dữ liệu. Id kiểu bigint nên không kế thừa BaseViewModel (Id Guid).</summary>
    public class CoreDataMergeConflictViewModel
    {
        public long Id { get; set; }
        public int STT { get; set; }
        public long TotalRow { get; set; }
        public string? MigrationCode { get; set; }
        public string? EntityName { get; set; }
        public Guid? RecordId { get; set; }
        public string? FieldName { get; set; }
        public string? CanonicalValue { get; set; }
        public string? LegacyValue { get; set; }
        public DateTime DetectedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string? ResolutionNote { get; set; }
    }
}
