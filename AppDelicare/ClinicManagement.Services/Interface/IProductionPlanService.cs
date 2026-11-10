using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IProductionPlanService
    {
        public Task<IResults<ProductionPlanViewModel>> GetAll(ProductionPlanSearchRequest request);
        public Task<IResult<bool>> Save(ProductionPlanRequest request, string typeSave);
        public IResults<ProductionPlan> GetAllData();
        public Task<IResult<ProductionPlan>> GetById(Guid id);
    }
}
