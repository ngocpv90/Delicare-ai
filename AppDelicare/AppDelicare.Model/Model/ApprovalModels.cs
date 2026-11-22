namespace AppDelicare.Model.Model
{
    /// <summary>T12: một yêu cầu đang chờ người dùng hiện tại duyệt (hộp duyệt wf-inbox).</summary>
    public class ApprovalPendingModel
    {
        public Guid Id { get; set; }
        public string? RequestNo { get; set; }
        public string? BusinessType { get; set; }
        public Guid? BusinessId { get; set; }
        /// <summary>Mục quyền duyệt (screen-id) của bước đang chờ – T15.1 / BE2.3 (vd. <c>scm-pr-l1</c>, <c>scm-po-director</c>).</summary>
        public string? ScreenId { get; set; }
        public string? Title { get; set; }
        public DateTime RequestedAt { get; set; }
        public Guid? RequestedBy { get; set; }
        public string? RequestedByName { get; set; }
        /// <summary>BE2.3: Pending (cấp 1) / InProgress (đã qua ít nhất 1 cấp).</summary>
        public string? RequestStatus { get; set; }
        /// <summary>BE2.3: cấp đang chờ / tổng số cấp.</summary>
        public int StepNo { get; set; } = 1;
        public int StepCount { get; set; } = 1;
        /// <summary>BE2.3: tên bước = tên mục menu "Phê duyệt › …" của bước.</summary>
        public string? StepName { get; set; }
        /// <summary>BE2.3: hạn của bước đang chờ; quá hạn → <see cref="IsOverdue"/> (cờ, không job – D-45).</summary>
        public DateTime? DueAt { get; set; }
        public bool IsOverdue { get; set; }
    }

    /// <summary>BE2.3: 1 bước của quy trình duyệt (ApprovalStep + mục quyền + tên người quyết định).</summary>
    public class ApprovalStepInfoModel
    {
        public Guid Id { get; set; }
        public int StepNo { get; set; }
        /// <summary>Mục quyền duyệt của bước (<c>#/wf-inbox?type=&lt;ScreenId&gt;</c>).</summary>
        public string? ScreenId { get; set; }
        public string? StepName { get; set; }
        /// <summary>Waiting / Approved / Rejected / Skipped / Cancelled.</summary>
        public string? DecisionStatus { get; set; }
        public string? DecisionStatusName { get; set; }
        /// <summary>true = bước đang chờ quyết định (bước Waiting nhỏ nhất của yêu cầu còn mở).</summary>
        public bool IsCurrent { get; set; }
        public Guid? ApproverUserId { get; set; }
        public string? ApproverName { get; set; }
        public DateTime? AssignedAt { get; set; }
        public DateTime? DueAt { get; set; }
        public bool IsOverdue { get; set; }
        public DateTime? DecidedAt { get; set; }
        public string? DecisionNote { get; set; }
    }

    /// <summary>BE2.3: tóm tắt yêu cầu duyệt đang mở của 1 chứng từ (trả trong chi tiết chứng từ: đề nghị mua, kiểm kê…).</summary>
    public class ApprovalProgressModel
    {
        public Guid Id { get; set; }
        public string? RequestNo { get; set; }
        public string? RequestStatus { get; set; }
        public int StepNo { get; set; }
        public int StepCount { get; set; }
        public string? ScreenId { get; set; }
        public string? StepName { get; set; }
        public DateTime? DueAt { get; set; }
        public bool IsOverdue { get; set; }
    }
}
