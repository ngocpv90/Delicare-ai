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
        /// <summary>
        /// HR-ACC-1.6 (D-82 f): nội dung đề xuất sửa lúc Duyệt (JSON) – chỉ loại duyệt cho sửa (IApprovalProposalEditor, vd. cấp tài khoản: đổi nhóm quyền trước khi duyệt).
        /// Lưu lại vào PayloadJson.proposal của yêu cầu. Bỏ trống = giữ đề xuất lúc gửi.
        /// </summary>
        [MaxLength(4000)]
        public string? Proposal { get; set; }
    }
}
