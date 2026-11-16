using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using System;
using System.Threading.Tasks;

namespace AppDelicare.Services.Interface
{
    public interface IOrderReconciliationService
    {
        public Task<IResults<OrderReconciliationViewModel>> GetAll(OrderReconciliationSearchRequest request);
        public Task<IResult<bool>> Save(OrderReconciliationRequest request, string typeSave);
        public IResults<OrderReconciliation> GetAllData();
        public Task<IResult<OrderReconciliation>> GetById(Guid id);
    }
}
