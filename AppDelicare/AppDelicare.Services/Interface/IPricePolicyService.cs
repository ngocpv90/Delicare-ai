using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;

namespace AppDelicare.Services.Interface
{
    public interface IPricePolicyService
    {
        public Task<IResults<PricePolicyViewModel>> GetAll(PricePolicySearchRequest request);
        public Task<IResult<bool>> Save(PricePolicyRequest request, string typeSave);
        public IResults<PricePolicy> GetAllData();
        public Task<IResult<PricePolicy>> GetById(Guid id);
        // <summary>T45R: tra đơn giá áp dụng.</summary>
        public Task<IResult<PriceResolveModel>> Resolve(PriceResolveRequest request);
    }
}
