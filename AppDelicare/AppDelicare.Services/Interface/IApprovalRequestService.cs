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
        public Task<IResults<ApprovalPendingModel>> GetMyPending(string? type);
        /// <summary>T12: duyệt / từ chối 1 cấp và thực thi nghiệp vụ.</summary>
        public Task<IResult<bool>> Decide(ApprovalDecideRequest request);
    }
}
