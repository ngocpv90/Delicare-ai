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
    public interface IDeliveryExecutionService
    {
        public Task<IResults<DeliveryExecutionViewModel>> GetAll(DeliveryExecutionSearchRequest request);
        public Task<IResult<bool>> Save(DeliveryExecutionRequest request, string typeSave);
        public IResults<DeliveryExecution> GetAllData();
        public Task<IResult<DeliveryExecution>> GetById(Guid id);

        // BE2.1 (spec 10.3 GET executions).
        public Task<IResult<DeliveryExecutionBoardModel>> GetBoard(DeliveryExecutionBoardRequest request);
    }
}
