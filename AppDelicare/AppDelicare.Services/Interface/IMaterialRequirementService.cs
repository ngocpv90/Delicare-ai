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
        /// <summary>T14: kết quả đã lưu theo ngày sản xuất (+ chi nhánh).</summary>
        public Task<IResult<MaterialRequirementResultModel>> GetByDate(MaterialRequirementByDateRequest request);
        /// <summary>T14: kết quả đã lưu theo kế hoạch sản xuất.</summary>
        public Task<IResult<MaterialRequirementResultModel>> GetDetail(Guid productionPlanId);
    }
}
