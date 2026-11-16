using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IProductionOutputService
    {
        public Task<IResults<ProductionOutputViewModel>> GetAll(ProductionOutputSearchRequest request);
        public Task<IResult<bool>> Save(ProductionOutputRequest request, string typeSave);
        public IResults<ProductionOutput> GetAllData();
        public Task<IResult<ProductionOutput>> GetById(Guid id);
    }
}
