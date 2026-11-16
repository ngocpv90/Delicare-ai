using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IPackingResultService
    {
        public Task<IResults<PackingResultViewModel>> GetAll(PackingResultSearchRequest request);
        public Task<IResult<bool>> Save(PackingResultRequest request, string typeSave);
        public IResults<PackingResult> GetAllData();
        public Task<IResult<PackingResult>> GetById(Guid id);
    }
}
