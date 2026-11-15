using System.ComponentModel.DataAnnotations;

namespace AppDelicare.Model.Request.Custom
{
    /// <summary>T45R: đánh dấu đã xử lý một xung đột dữ liệu.</summary>
    public class CoreDataMergeConflictResolveRequest
    {
        [Required]
        public long Id { get; set; }
        /// <summary>Cách xử lý (bắt buộc, tối đa 1000 ký tự).</summary>
        public string? ResolutionNote { get; set; }
    }
}
