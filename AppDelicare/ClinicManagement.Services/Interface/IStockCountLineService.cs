using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IStockCountLineService
    {
        public Task<IResults<StockCountLineViewModel>> GetAll(StockCountLineSearchRequest request);
        public Task<IResult<bool>> Save(StockCountLineRequest request, string typeSave);
        public IResults<StockCountLine> GetAllData();
        public Task<IResult<StockCountLine>> GetById(Guid id);
    }
}
