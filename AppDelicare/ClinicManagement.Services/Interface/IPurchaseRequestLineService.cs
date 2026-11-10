using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IPurchaseRequestLineService
    {
        public Task<IResults<PurchaseRequestLineViewModel>> GetAll(PurchaseRequestLineSearchRequest request);
        public Task<IResult<bool>> Save(PurchaseRequestLineRequest request, string typeSave);
        public IResults<PurchaseRequestLine> GetAllData();
        public Task<IResult<PurchaseRequestLine>> GetById(Guid id);
    }
}
