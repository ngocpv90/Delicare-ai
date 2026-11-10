using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IGoodsReceiptLineService
    {
        public Task<IResults<GoodsReceiptLineViewModel>> GetAll(GoodsReceiptLineSearchRequest request);
        public Task<IResult<bool>> Save(GoodsReceiptLineRequest request, string typeSave);
        public IResults<GoodsReceiptLine> GetAllData();
        public Task<IResult<GoodsReceiptLine>> GetById(Guid id);
    }
}
