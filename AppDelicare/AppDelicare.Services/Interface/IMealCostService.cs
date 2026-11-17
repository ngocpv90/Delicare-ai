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
        // BE2.4 (spec 8.5 meal-costs, Catalog 154): tính giá vốn suất của đơn đã giao / đối soát; danh sách đã tính.
        public Task<IResult<AppDelicare.Model.Model.MealCostResultModel>> Calculate(AppDelicare.Model.Request.Custom.MealCostCalculateRequest request);
        public Task<IResults<AppDelicare.Model.Model.MealCostResultModel>> GetList(AppDelicare.Model.Request.Custom.MealCostListRequest request);
    }
}
