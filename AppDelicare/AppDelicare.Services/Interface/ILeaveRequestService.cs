// Sinh bởi tools/AppDelicare.CodeGen (T05), nay là file của dev (BE2.6) – CodeGen không ghi đè.
#nullable enable
using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;

namespace AppDelicare.Services.Interface
{
    public interface ILeaveRequestService
    {
        public Task<IResults<LeaveRequestViewModel>> GetAll(LeaveRequestSearchRequest request);
        public Task<IResult<bool>> Save(LeaveRequestRequest request, string typeSave);
        public IResults<LeaveRequest> GetAllData();
        public Task<IResult<LeaveRequest>> GetById(Guid id);

        // BE2.6 (spec 11.3, Catalog 207, 209) – đơn nghỉ + duyệt qua engine LEAVE_REQUEST.
        public Task<IResult<LeaveRequestDetailModel>> Create(LeaveCreateRequest request);
        public Task<IResult<bool>> Decide(HrDecisionRequest request, bool approve);
        public Task<IResult<bool>> Cancel(HrCancelRequest request);
        public Task<IResult<LeaveRequestDetailModel>> GetDetail(Guid id);
        public Task<IResults<LeaveRequestDetailModel>> GetMine();

        // Handler LEAVE_REQUEST (engine).
        public Task<string?> BuildApprovalStateAsync(Guid id);
        public Task<string?> DescribeAsync(Guid id);
        /// <summary>decision = Approved / Rejected / Cancelled.</summary>
        public Task<string?> ApplyDecisionAsync(Guid id, string decision);
    }
}
