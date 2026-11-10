using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IGoodsReceiptService
    {
        public Task<IResults<GoodsReceiptViewModel>> GetAll(GoodsReceiptSearchRequest request);
        public Task<IResult<bool>> Save(GoodsReceiptRequest request, string typeSave);
        public IResults<GoodsReceipt> GetAllData();
        public Task<IResult<GoodsReceipt>> GetById(Guid id);
    }
}
