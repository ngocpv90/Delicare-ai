using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IStockCountService
    {
        public Task<IResults<StockCountViewModel>> GetAll(StockCountSearchRequest request);
        public Task<IResult<bool>> Save(StockCountRequest request, string typeSave);
        public IResults<StockCount> GetAllData();
        public Task<IResult<StockCount>> GetById(Guid id);
    }
}
