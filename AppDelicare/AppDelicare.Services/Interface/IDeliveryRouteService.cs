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
    public interface IDeliveryRouteService
    {
        public Task<IResults<DeliveryRouteViewModel>> GetAll(DeliveryRouteSearchRequest request);
        public Task<IResult<bool>> Save(DeliveryRouteRequest request, string typeSave);
        public IResults<DeliveryRoute> GetAllData();
        public Task<IResult<DeliveryRoute>> GetById(Guid id);

        // T19 (Pilot P8) – chuyến xe CX-, nhiệt độ giao ≥ 60 °C, xuất phát.
        public Task<IResult<DeliveryBoardModel>> GetBoard(DeliveryDateRequest request);
        public Task<IResults<DeliveryReadyOrderModel>> GetReadyOrders(DeliveryDateRequest request);
        public Task<IResult<DeliveryRouteDetailModel>> GetDetail(Guid id);
        public Task<IResult<DeliveryRouteDetailModel>> Create(DeliveryRouteCreateRequest request);
        public Task<IResult<DeliveryRouteDetailModel>> RecordTemperature(DeliveryTemperatureRequest request);
        public Task<IResult<DeliveryRouteDetailModel>> Dispatch(DeliveryRouteDispatchRequest request);
        public Task<IResult<byte[]>> ExportDeliveryNote(Guid id);
    }
}
