// Sinh bởi tools/AppDelicare.CodeGen (T05), nay là file của dev (T12: duyệt 1 cấp) – CodeGen không ghi đè.
#nullable enable
using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;

namespace AppDelicare.Services.Interface
{
    public interface IApprovalRequestService
    {
        public Task<IResults<ApprovalRequestViewModel>> GetAll(ApprovalRequestSearchRequest request);
        public Task<IResult<bool>> Save(ApprovalRequestRequest request, string typeSave);
        public IResults<ApprovalRequest> GetAllData();
        public Task<IResult<ApprovalRequest>> GetById(Guid id);
        /// <summary>T12: tạo yêu cầu duyệt (gọi trong transaction của nghiệp vụ gửi duyệt).</summary>
        /// <param name="proposalJson">T13 (tùy chọn): thay đổi đề xuất, thực thi khi được duyệt. Có giá trị → PayloadJson = {"state": "&lt;nội dung&gt;", "proposal": "&lt;đề xuất&gt;"}; null → giữ định dạng cũ.</param>
        public Task<(ApprovalRequest? Request, string? Error)> CreateAsync(string businessType, Guid businessId, string? proposalJson = null);
        /// <summary>T12: yêu cầu chờ người đang đăng nhập duyệt; type = screen-id.</summary>
        public Task<IResults<ApprovalPendingModel>> GetMyPending(string? type, bool explicitOnly = false);
        /// <summary>T12: duyệt / từ chối 1 cấp và thực thi nghiệp vụ.</summary>
        public Task<IResult<bool>> Decide(ApprovalDecideRequest request);

        // BE2.1 (spec 11.5) – chi tiết + hủy yêu cầu duyệt.
        public Task<IResult<AppDelicare.Model.Model.ApprovalDetailModel>> GetDetail(Guid id);
        public Task<IResult<bool>> Cancel(AppDelicare.Model.Request.Custom.ApprovalCancelRequest request);

        /// <summary>BE2.3: yêu cầu đang mở (Pending / InProgress) của 1 chứng từ + cấp đang chờ; không có → null.</summary>
        public Task<ApprovalProgressModel?> OpenProgressAsync(string businessType, Guid businessId);

        /// <summary>BOT-2.3: duyệt quá hạn (DueAt của cấp đang chờ) → APPROVAL_OVERDUE tới người duyệt của cấp; quá thêm <paramref name="escalateHours"/> giờ → nhóm <paramref name="escalateGroups"/> (bot DH). Trả số tin đã xếp.</summary>
        public Task<int> NotifyOverdueAsync(int escalateHours, IReadOnlyCollection<string> escalateGroups, IReadOnlyCollection<string>? topGroups = null);

        // APR-UI-1 (D-81) – màn Phê duyệt tổng.
        /// <summary>APR-1.3: yêu cầu tôi gửi còn mở – cấp, người / nhóm đang giữ, đã chờ, nhắc được không.</summary>
        public Task<IResults<ApprovalSentModel>> GetMySent();
        /// <summary>APR-1.3: yêu cầu tôi đã duyệt / từ chối (mới nhất trước, phân trang).</summary>
        public Task<IResults<ApprovalDecidedModel>> GetMyDecided(int page, int pageSize);
        /// <summary>APR-1.3 (D-81 d): duyệt nhiều – mỗi id kiểm lại canBulk + quyền ở máy chủ; trả kết quả từng id. Không từ chối hàng loạt.</summary>
        public Task<IResults<ApprovalBulkResultModel>> DecideBulk(ApprovalBulkRequest request);
        /// <summary>APR-1.3 (D-81 h): người gửi nhắc người duyệt cấp hiện tại qua bot – tối đa 1 lần / BusinessRules:ApprovalRemindHours.</summary>
        public Task<IResult<bool>> Remind(ApprovalRemindRequest request);
        /// <summary>APR-1.3 (D-81 k): thống kê theo loại trong khoảng ngày gửi (mặc định 30 ngày).</summary>
        public Task<IResults<ApprovalStatsModel>> GetStats(DateTime? fromDate, DateTime? toDate);
        /// <summary>APR-1.4 (D-81 i): người duyệt của cấp đang chờ đều có đơn nghỉ Đã duyệt hôm nay → báo ngay bậc leo thang kế (COO, rồi CEO), ghi dấu. Trả số tin.</summary>
        public Task<int> EscalateAbsentAsync(IReadOnlyCollection<string> escalateGroups, IReadOnlyCollection<string> topGroups);
        /// <summary>APR-1.7: tin tổng hợp cuối ngày cho từng người duyệt (còn chờ, quá hạn, có cảnh báo) – 1 lần / người / ngày.</summary>
        public Task<int> SendDigestAsync();
        /// <summary>UI-6 (D-80): Id yêu cầu duyệt mới nhất của 1 chứng từ (mọi trạng thái) – tab Duyệt của trang chi tiết; không có → null.</summary>
        public Task<Guid?> LatestRequestIdAsync(string businessType, Guid businessId);
    }
}
