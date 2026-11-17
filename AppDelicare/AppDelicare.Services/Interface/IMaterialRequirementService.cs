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
    public interface IMaterialRequirementService
    {
        public Task<IResults<MaterialRequirementViewModel>> GetAll(MaterialRequirementSearchRequest request);
        public Task<IResult<bool>> Save(MaterialRequirementRequest request, string typeSave);
        public IResults<MaterialRequirement> GetAllData();
        public Task<IResult<MaterialRequirement>> GetById(Guid id);
        /// <summary>T14: tính nhu cầu NL của 1 ngày sản xuất + đề nghị mua nháp (1 transaction).</summary>
        public Task<IResult<MaterialRequirementResultModel>> Calculate(MaterialRequirementCalculateRequest request);
        /// <summary>T14.1: đề nghị mua đã gửi duyệt / duyệt / lập PO → tính lại và lập đề nghị mua bổ sung Nháp cho phần thiếu thêm (1 transaction).</summary>
        public Task<IResult<MaterialRequirementResultModel>> CalculateTopUp(MaterialRequirementCalculateRequest request);
        /// <summary>BE2.1 (spec 7.1 create-purchase-request): đề nghị mua Nháp từ dòng thiếu được chọn + % dự phòng.</summary>
        public Task<IResult<MaterialRequirementResultModel>> CreatePurchaseRequest(MaterialRequirementPurchaseRequest request);
        /// <summary>T14: kết quả đã lưu theo ngày sản xuất (+ chi nhánh).</summary>
        public Task<IResult<MaterialRequirementResultModel>> GetByDate(MaterialRequirementByDateRequest request);
        /// <summary>T14: kết quả đã lưu theo kế hoạch sản xuất.</summary>
        public Task<IResult<MaterialRequirementResultModel>> GetDetail(Guid productionPlanId);
        /// <summary>T15: in Phiếu trước sản xuất (.xlsx) từ kết quả tính nhu cầu của kế hoạch.</summary>
        public Task<IResult<byte[]>> ExportPreProduction(Guid productionPlanId);
    }
}
