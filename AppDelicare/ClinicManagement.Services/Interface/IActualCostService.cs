using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IActualCostService
    {
        public Task<IResults<ActualCostViewModel>> GetAll(ActualCostSearchRequest request);
        public Task<IResult<bool>> Save(ActualCostRequest request, string typeSave);
        public IResults<ActualCost> GetAllData();
        public Task<IResult<ActualCost>> GetById(Guid id);
    }
}
