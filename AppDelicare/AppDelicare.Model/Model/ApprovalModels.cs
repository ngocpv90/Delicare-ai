namespace AppDelicare.Model.Model
{
    /// <summary>T12: một yêu cầu đang chờ người dùng hiện tại duyệt (hộp duyệt wf-inbox).</summary>
    public class ApprovalPendingModel
    {
        public Guid Id { get; set; }
        public string? RequestNo { get; set; }
        public string? BusinessType { get; set; }
        public Guid? BusinessId { get; set; }
        /// <summary>Màn hình nghiệp vụ (screen-id) – frontend mở chi tiết theo màn này.</summary>
        public string? ScreenId { get; set; }
        public string? Title { get; set; }
        public DateTime RequestedAt { get; set; }
        public Guid? RequestedBy { get; set; }
        public string? RequestedByName { get; set; }
    }
}
