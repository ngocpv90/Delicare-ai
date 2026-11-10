using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IPurchaseRequestService
    {
        public Task<IResults<PurchaseRequestViewModel>> GetAll(PurchaseRequestSearchRequest request);
        public Task<IResult<bool>> Save(PurchaseRequestRequest request, string typeSave);
        public IResults<PurchaseRequest> GetAllData();
        public Task<IResult<PurchaseRequest>> GetById(Guid id);
    }
}
