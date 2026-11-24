namespace AppDelicare.Services.Interface
{
    /// <summary>
    /// APR-1.2 (D-81 e): dữ liệu engine truyền cho <see cref="IApprovalHandler.GetSummaryAsync"/> – DbContext (chỉ đọc), ngưỡng BusinessRules, đồng hồ,
    /// người gửi + nội dung đề xuất lưu lúc gửi (sửa đơn sau chốt, lệch đơn, điều chỉnh tồn), cấp đang chờ.
    /// </summary>
    public sealed class ApprovalSummaryContext
    {
        public required AppDelicare.Data.Connection.DatabaseContext Db { get; init; }
        public required AppDelicare.Model.Option.BusinessRulesOptions Rules { get; init; }
        public required DateTime Now { get; init; }
        public Guid? RequestedBy { get; init; }
        public string? ProposalJson { get; init; }
        public int StepNo { get; init; } = 1;
        public int StepCount { get; init; } = 1;
        public string? StepScreenId { get; init; }
    }

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

        /// <summary>
        /// BE2.3 – quy trình nhiều cấp: mục quyền duyệt của từng bước, theo thứ tự (bước 1, 2…). Engine tạo 1 <c>ApprovalStep</c> / phần tử lúc gửi và
        /// chỉ gọi <see cref="ApproveAsync(Guid, Guid, string?, string?)"/> ở bước cuối (từ chối / hủy ở bất kỳ bước nào gọi Reject / Cancel).
        /// Mặc định 1 bước = <see cref="RequiredScreenIdAsync"/> – handler cũ không phải sửa. Cấu hình <c>BusinessRules:ApprovalSteps</c> ghi đè (engine).
        /// </summary>
        async Task<IReadOnlyList<string>> StepScreenIdsAsync(Guid businessId) => [await RequiredScreenIdAsync(businessId)];

        /// <summary>Nội dung được duyệt (JSON ổn định). Lưu khi gửi; lúc duyệt dựng lại và so hash – khác thì không cho duyệt.</summary>
        Task<string?> BuildPayloadAsync(Guid businessId);

        /// <summary>Tiêu đề hiển thị trong hộp duyệt.</summary>
        Task<string?> DescribeAsync(Guid businessId);

        /// <summary>
        /// APR-1.2 (D-81 e): khuôn tóm tắt chung (tiêu đề, giá trị chính, vì sao, cảnh báo ≤ 3, thông tin, dòng, trước → sau, link). Chỉ ĐỌC dữ liệu có sẵn.
        /// Mặc định null → engine dựng khuôn mặc định từ <see cref="DescribeAsync"/> (loại mới chỉ cần viết method này).
        /// </summary>
        Task<AppDelicare.Model.Model.ApprovalSummaryModel?> GetSummaryAsync(Guid businessId, ApprovalSummaryContext ctx)
            => Task.FromResult<AppDelicare.Model.Model.ApprovalSummaryModel?>(null);

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

        /// <summary>
        /// HR-ACC-1.3 (D-82 i, D-79 a5): nhân viên mà yêu cầu nói tới (đơn nghỉ / đổi ca / tài khoản của ai). Người duyệt là CHÍNH nhân viên đó
        /// (PersonLink) thì không được duyệt, kể cả khi người khác gửi hộ. Mặc định null = không kiểm.
        /// </summary>
        Task<Guid?> SubjectEmployeeIdAsync(Guid businessId) => Task.FromResult<Guid?>(null);
    }

    /// <summary>
    /// HR-ACC-1.6 (D-82 f): loại duyệt cho người duyệt sửa đề xuất trước khi Duyệt (ApprovalDecideRequest.Proposal). Trả đề xuất đã chuẩn hóa
    /// hoặc thông báo lỗi; engine ghi lại vào PayloadJson.proposal rồi gọi ApproveAsync với đề xuất mới.
    /// </summary>
    public interface IApprovalProposalEditor
    {
        Task<(string? Proposal, string? Error)> ReviseProposalAsync(Guid businessId, string? submitted, string revised);
    }
}
