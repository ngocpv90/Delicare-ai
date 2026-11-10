using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IContractMealService
    {
        public Task<IResults<ContractMealViewModel>> GetAll(ContractMealSearchRequest request);
        public Task<IResult<bool>> Save(ContractMealRequest request, string typeSave);
        public IResults<ContractMeals> GetAllData();
        public Task<IResult<ContractMeals>> GetById(Guid id);
    }
}
