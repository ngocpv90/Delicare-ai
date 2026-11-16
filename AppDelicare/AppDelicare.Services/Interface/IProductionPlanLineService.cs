using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IProductionPlanLineService
    {
        public Task<IResults<ProductionPlanLineViewModel>> GetAll(ProductionPlanLineSearchRequest request);
        public Task<IResult<bool>> Save(ProductionPlanLineRequest request, string typeSave);
        public IResults<ProductionPlanLine> GetAllData();
        public Task<IResult<ProductionPlanLine>> GetById(Guid id);
    }
}
