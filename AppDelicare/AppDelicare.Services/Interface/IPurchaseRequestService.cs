using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IPurchaseRequestService
    {
        public Task<IResults<PurchaseRequestViewModel>> GetAll(PurchaseRequestSearchRequest request);
        public Task<IResult<bool>> Save(PurchaseRequestRequest request, string typeSave);
        public IResults<PurchaseRequest> GetAllData();
        public Task<IResult<PurchaseRequest>> GetById(Guid id);
        /// <summary>T15: chi tiết đề nghị mua (dòng + NCC gợi ý + tiền ước tính + PO đã lập).</summary>
        public Task<IResult<PurchaseRequestDetailModel>> GetDetail(Guid id);
        /// <summary>T15 (spec 7.2 convert-to-po): lập PO Nháp theo NCC gợi ý, đề nghị → Converted.</summary>
        public Task<IResult<PurchaseRequestConvertResultModel>> ConvertToPurchaseOrder(PurchaseRequestConvertRequest request);
        /// <summary>T15: hủy đề nghị Nháp / Đã duyệt (lý do bắt buộc).</summary>
        public Task<IResult<PurchaseRequestDetailModel>> Cancel(PurchaseCancelRequest request);

        // BE2.3 (D-46) – duyệt đề nghị mua 2 cấp qua engine (PURCHASE_REQUEST).
        public Task<IResult<PurchaseRequestDetailModel>> Submit(PurchaseActionRequest request);
        public Task<IResult<bool>> Decide(PurchaseActionRequest request, bool approve);
        public Task<string?> BuildApprovalStateAsync(Guid id);
        public Task<string?> DescribeAsync(Guid id);
        public Task<string?> ApplyDecisionAsync(Guid id, bool approved, string? note);
    }
}
