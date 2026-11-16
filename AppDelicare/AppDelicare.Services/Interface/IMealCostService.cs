using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IMealCostService
    {
        public Task<IResults<MealCostViewModel>> GetAll(MealCostSearchRequest request);
        public Task<IResult<bool>> Save(MealCostRequest request, string typeSave);
        public IResults<MealCost> GetAllData();
        public Task<IResult<MealCost>> GetById(Guid id);
    }
}
