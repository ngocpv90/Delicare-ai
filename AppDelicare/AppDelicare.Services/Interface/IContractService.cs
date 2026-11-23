using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IContractService
    {
        public Task<IResults<ContractViewModel>> GetAll(ContractSearchRequest request);
        public Task<IResult<bool>> Save(ContractRequest request, string typeSave);
        public IResults<Contract> GetAllData();
        public Task<IResult<Contract>> GetById(Guid id);
        // BE2.2 (spec 6.1, Catalog 78–80): tạo kèm suất, gửi duyệt (engine T12 – CONTRACT), duyệt / từ chối, chấm dứt, chi tiết.
        public Task<IResult<AppDelicare.Model.Model.ContractDetailModel>> Create(AppDelicare.Model.Request.Custom.ContractCreateRequest request);
        public Task<IResult<AppDelicare.Model.Model.ContractDetailModel>> Submit(AppDelicare.Model.Request.Custom.ContractActionRequest request);
        public Task<IResult<bool>> Decide(AppDelicare.Model.Request.Custom.ContractActionRequest request, bool approve);
        public Task<IResult<AppDelicare.Model.Model.ContractDetailModel>> Terminate(AppDelicare.Model.Request.Custom.ContractTerminateRequest request);
        public Task<IResult<AppDelicare.Model.Model.ContractDetailModel>> GetDetail(Guid id);
        /// <summary>D-78 (2): đổi cách tính tiền đối soát của hợp đồng.</summary>
        public Task<IResult<AppDelicare.Model.Model.ContractDetailModel>> UpdateBillingBasis(AppDelicare.Model.Request.Custom.ContractBillingBasisRequest request);
        /// <summary>T23: hợp đồng Hiệu lực quá ngày hết hiệu lực → Hết hạn (job ContractExpiry 00:30 và nút bấm tay).</summary>
        public Task<IResult<AppDelicare.Model.Model.ContractExpireResultModel>> ExpireOverdue(AppDelicare.Model.Request.Custom.ContractExpireRequest request);
        /// <summary>Nội bộ (handler CONTRACT): nội dung được duyệt, tiêu đề, thực thi duyệt / từ chối – chạy trong transaction của engine.</summary>
        public Task<string?> BuildApprovalStateAsync(Guid id);
        public Task<string?> DescribeAsync(Guid id);
        public Task<string?> ApproveFromEngineAsync(Guid id, string? note);
        public Task<string?> RejectFromEngineAsync(Guid id, string note);
    }
}
