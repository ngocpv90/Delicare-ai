using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IProductionBatchService
    {
        public Task<IResults<ProductionBatchViewModel>> GetAll(ProductionBatchSearchRequest request);
        public Task<IResult<bool>> Save(ProductionBatchRequest request, string typeSave);
        public IResults<ProductionBatch> GetAllData();
        public Task<IResult<ProductionBatch>> GetById(Guid id);
    }
}
