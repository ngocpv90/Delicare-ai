namespace AppDelicare.Services.Interface
{
    /// <summary>
    /// T12 – bộ xử lý nghiệp vụ được duyệt theo <c>ApprovalRequest.BusinessType</c> (registry). Mỗi nghiệp vụ cần duyệt (công thức, PO,
    /// đơn lệch suất…) có 1 lớp cài đặt; <c>ApprovalRequestService</c> gọi khi tạo / duyệt / từ chối. Đăng ký trong <c>Ioc/ComponentRegistrar</c>.
    /// Các method chạy TRONG transaction của ApprovalRequestService; trả thông báo lỗi (tiếng Việt) hoặc null nếu thành công.
    /// </summary>
    public interface IApprovalHandler
    {
        /// <summary>Giá trị ApprovalRequest.BusinessType, vd. RECIPE_VERSION.</summary>
        string BusinessType { get; }

        /// <summary>Màn hình của nghiệp vụ – quyền duyệt = mục menu <c>#/wf-inbox?type=&lt;ScreenId&gt;</c> (D-31).</summary>
        string ScreenId { get; }

        /// <summary>Nội dung được duyệt (JSON ổn định). Lưu khi gửi; lúc duyệt dựng lại và so hash – khác thì không cho duyệt.</summary>
        Task<string?> BuildPayloadAsync(Guid businessId);

        /// <summary>Tiêu đề hiển thị trong hộp duyệt.</summary>
        Task<string?> DescribeAsync(Guid businessId);

        /// <summary>Thực thi khi được duyệt.</summary>
        Task<string?> ApproveAsync(Guid businessId, Guid actorUserId, string? note);

        /// <summary>Xử lý khi bị từ chối (lý do bắt buộc).</summary>
        Task<string?> RejectAsync(Guid businessId, Guid actorUserId, string note);
    }
}
