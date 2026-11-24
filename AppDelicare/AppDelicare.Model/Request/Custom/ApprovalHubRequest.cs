using System.ComponentModel.DataAnnotations;

namespace AppDelicare.Model.Request.Custom
{
    /// <summary>APR-1.3: duyệt nhiều yêu cầu Bình thường (JSON – danh sách id). Từ chối không cho hàng loạt.</summary>
    public class ApprovalBulkRequest
    {
        [Required]
        [MinLength(1)]
        [MaxLength(100)]
        public List<Guid> ListId { get; set; } = [];
        [MaxLength(1000)]
        public string? Note { get; set; }
    }

    /// <summary>APR-1.3: người gửi nhắc người duyệt cấp hiện tại qua bot.</summary>
    public class ApprovalRemindRequest
    {
        [Required]
        public Guid Id { get; set; }
    }
}
