using System.ComponentModel.DataAnnotations;

namespace AppDelicare.Model.Request.Custom
{
    /// <summary>T12: duyệt / từ chối một yêu cầu. Từ chối bắt buộc ghi lý do (Note).</summary>
    public class ApprovalDecideRequest
    {
        /// <summary>ApprovalRequest.Id.</summary>
        [Required]
        public Guid Id { get; set; }
        /// <summary>true = duyệt, false = từ chối.</summary>
        public bool Approve { get; set; }
        [MaxLength(1000)]
        public string? Note { get; set; }
    }
}
