using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IPurchaseOrderLineService
    {
        public Task<IResults<PurchaseOrderLineViewModel>> GetAll(PurchaseOrderLineSearchRequest request);
        public Task<IResult<bool>> Save(PurchaseOrderLineRequest request, string typeSave);
        public IResults<PurchaseOrderLine> GetAllData();
        public Task<IResult<PurchaseOrderLine>> GetById(Guid id);
    }
}
