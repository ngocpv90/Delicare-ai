using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IPurchaseOrderService
    {
        public Task<IResults<PurchaseOrderViewModel>> GetAll(PurchaseOrderSearchRequest request);
        public Task<IResult<bool>> Save(PurchaseOrderRequest request, string typeSave);
        public IResults<PurchaseOrder> GetAllData();
        public Task<IResult<PurchaseOrder>> GetById(Guid id);
    }
}
