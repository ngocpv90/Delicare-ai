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
    public interface IShiftSwapRequestService
    {
        public Task<IResults<ShiftSwapRequestViewModel>> GetAll(ShiftSwapRequestSearchRequest request);
        public Task<IResult<bool>> Save(ShiftSwapRequestRequest request, string typeSave);
        public IResults<ShiftSwapRequest> GetAllData();
        public Task<IResult<ShiftSwapRequest>> GetById(Guid id);

        // BE2.6 (spec 11.3, Catalog 208, 209) – đơn đổi ca + duyệt qua engine SHIFT_SWAP.
        public Task<IResult<ShiftSwapDetailModel>> Create(ShiftSwapCreateRequest request);
        public Task<IResult<bool>> Decide(HrDecisionRequest request, bool approve);
        public Task<IResult<bool>> Cancel(HrCancelRequest request);
        public Task<IResult<ShiftSwapDetailModel>> GetDetail(Guid id);
        public Task<IResults<ShiftSwapDetailModel>> GetMine();

        // Handler SHIFT_SWAP (engine).
        public Task<string?> BuildApprovalStateAsync(Guid id);
        public Task<string?> DescribeAsync(Guid id);
        /// <summary>decision = Approved / Rejected / Cancelled.</summary>
        public Task<string?> ApplyDecisionAsync(Guid id, string decision);
        // UI-6.39 (D-120)
        Task<IResult<ShiftSwapDetailModel>> Consent(ShiftSwapConsentRequest request);
        Task<IResult<ShiftSwapDetailModel>> ConsentForEmployee(Guid employeeId, ShiftSwapConsentRequest request);
        Task<int> ExpireConsentsAsync();
    }
}
