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
    public interface IStandardCostService
    {
        public Task<IResults<StandardCostViewModel>> GetAll(StandardCostSearchRequest request);
        public Task<IResult<bool>> Save(StandardCostRequest request, string typeSave);
        public IResults<StandardCost> GetAllData();
        public Task<IResult<StandardCost>> GetById(Guid id);
        // <summary>T45R: tính lại giá thành chuẩn.</summary>
        public Task<IResult<int>> Recalculate(StandardCostRecalculateRequest request);
    }
}
