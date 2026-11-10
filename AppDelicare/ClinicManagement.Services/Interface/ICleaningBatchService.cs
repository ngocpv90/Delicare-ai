using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface ICleaningBatchService
    {
        public Task<IResults<CleaningBatchViewModel>> GetAll(CleaningBatchSearchRequest request);
        public Task<IResult<bool>> Save(CleaningBatchRequest request, string typeSave);
        public IResults<CleaningBatch> GetAllData();
        public Task<IResult<CleaningBatch>> GetById(Guid id);
    }
}
