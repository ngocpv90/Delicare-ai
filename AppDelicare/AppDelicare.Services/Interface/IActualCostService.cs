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
        // BE2.4 (spec 8.5 GET actual-costs, Catalog 152).
        public Task<IResult<AppDelicare.Model.Model.ActualCostListModel>> GetList(AppDelicare.Model.Request.Custom.ActualCostListRequest request);
    }
}
