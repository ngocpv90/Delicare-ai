using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request;
using AppDelicare.Model.Request.Custom;
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

        // T19 (Pilot P8) – đối soát suất thực tế phục vụ.
        public Task<IResult<OrderReconciliationDetailModel>> Reconcile(OrderReconcileRequest request);
        public Task<IResult<OrderReconciliationDetailModel>> Close(ReconciliationCloseRequest request);
        public Task<IResult<OrderReconciliationDetailModel>> GetDetail(Guid id);
        public Task<IResult<OrderReconciliationDetailModel>> GetByOrder(Guid orderId);
        public Task<IResults<ReconciliationPendingModel>> GetPendingOrders(ReconciliationPendingRequest request);
    }
}
