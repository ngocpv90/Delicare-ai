#nullable enable
using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;

namespace AppDelicare.Services.Interface
{
    public interface IDeliveryStopService
    {
        public Task<IResults<DeliveryStopViewModel>> GetAll(DeliveryStopSearchRequest request);
        public Task<IResult<bool>> Save(DeliveryStopRequest request, string typeSave);
        public IResults<DeliveryStop> GetAllData();
        public Task<IResult<DeliveryStop>> GetById(Guid id);

        // T19 (Pilot P8) – thực hiện giao tại điểm: đến, bàn giao + POD (nhiệt độ ≥ 60 °C), thất bại.
        public Task<IResult<DeliveryRouteDetailModel>> Arrive(DeliveryStopArriveRequest request);
        public Task<IResult<DeliveryRouteDetailModel>> Deliver(DeliveryStopDeliverRequest request);
        public Task<IResult<DeliveryRouteDetailModel>> Fail(DeliveryStopFailRequest request);
    }
}
