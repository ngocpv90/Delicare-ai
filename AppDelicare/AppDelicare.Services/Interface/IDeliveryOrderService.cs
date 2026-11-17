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
    public interface IDeliveryOrderService
    {
        public Task<IResults<DeliveryOrderViewModel>> GetAll(DeliveryOrderSearchRequest request);
        public Task<IResult<bool>> Save(DeliveryOrderRequest request, string typeSave);
        public IResults<DeliveryOrder> GetAllData();
        public Task<IResult<DeliveryOrder>> GetById(Guid id);

        // BE2.1 (spec 10.2) – đơn giao thủ công: danh sách, lập, tách, gán / chuyển chuyến, hủy.
        public Task<IResults<DeliveryOrderItemModel>> GetList(DeliveryOrderListRequest request);
        public Task<IResult<DeliveryOrderActionModel>> Create(DeliveryOrderCreateRequest request);
        public Task<IResult<DeliveryOrderActionModel>> Split(DeliveryOrderSplitRequest request);
        public Task<IResult<DeliveryOrderActionModel>> AssignRoute(DeliveryOrderAssignRouteRequest request);
        public Task<IResult<DeliveryOrderActionModel>> Cancel(DeliveryOrderCancelRequest request);
        /// <summary>BE3.1 (Catalog 192): timeline sự kiện giao của 1 đơn giao.</summary>
        public Task<IResults<AppDelicare.Model.Model.DeliveryEventItemModel>> GetEvents(Guid id);
    }
}
