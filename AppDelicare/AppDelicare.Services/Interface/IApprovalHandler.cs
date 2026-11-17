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

        /// <summary>
        /// T15.1: mọi mục quyền duyệt handler có thể dùng (để hộp duyệt lọc theo <c>type</c> / quyền). Mặc định chỉ <see cref="ScreenId"/> – handler cũ không phải sửa.
        /// </summary>
        IReadOnlyCollection<string> ApprovalScreenIds => new[] { ScreenId };

        /// <summary>
        /// T15.1: mục quyền duyệt bắt buộc cho 1 nghiệp vụ cụ thể (vd. PO ≥ ngưỡng GĐ → <c>scm-po-director</c>). Mặc định <see cref="ScreenId"/>.
        /// Vẫn duyệt 1 cấp – chỉ đổi mục quyền được kiểm.
        /// </summary>
        Task<string> RequiredScreenIdAsync(Guid businessId) => Task.FromResult(ScreenId);

        /// <summary>Nội dung được duyệt (JSON ổn định). Lưu khi gửi; lúc duyệt dựng lại và so hash – khác thì không cho duyệt.</summary>
        Task<string?> BuildPayloadAsync(Guid businessId);

        /// <summary>Tiêu đề hiển thị trong hộp duyệt.</summary>
        Task<string?> DescribeAsync(Guid businessId);

        /// <summary>Thực thi khi được duyệt.</summary>
        Task<string?> ApproveAsync(Guid businessId, Guid actorUserId, string? note);

        /// <summary>
        /// T13: thực thi khi được duyệt, kèm <paramref name="proposalJson"/> = nội dung đề xuất lưu lúc gửi (vd. số suất mới của đơn lệch &gt; 30%).
        /// Mặc định bỏ qua đề xuất và gọi bản 3 tham số – handler cũ không phải sửa.
        /// </summary>
        Task<string?> ApproveAsync(Guid businessId, Guid actorUserId, string? note, string? proposalJson)
            => ApproveAsync(businessId, actorUserId, note);

        /// <summary>Xử lý khi bị từ chối (lý do bắt buộc).</summary>
        Task<string?> RejectAsync(Guid businessId, Guid actorUserId, string note);

        /// <summary>
        /// BE2.1 (spec 11.5 cancel): người gửi hủy yêu cầu còn chờ duyệt – trả bản ghi nghiệp vụ về Nháp. Mặc định dùng đúng xử lý từ chối (mọi handler hiện có
        /// đều trả về Nháp / không đổi khi từ chối) với ghi chú "Người gửi hủy: …" – handler cũ không phải sửa.
        /// </summary>
        Task<string?> CancelAsync(Guid businessId, Guid actorUserId, string reason)
            => RejectAsync(businessId, actorUserId, "Người gửi hủy: " + reason);
    }
}
