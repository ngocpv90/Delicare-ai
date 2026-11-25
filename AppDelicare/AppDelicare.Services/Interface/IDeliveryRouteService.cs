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
        /// <summary>BOT-1.5b: chuyến của chính tài xế (DriverId = nhân viên của tài khoản) trong ngày giao.</summary>
        public Task<IResult<DeliveryBoardModel>> GetMine(DateTime? deliveryDate);
        public Task<IResults<DeliveryReadyOrderModel>> GetReadyOrders(DeliveryDateRequest request);
        public Task<IResult<DeliveryRouteDetailModel>> GetDetail(Guid id);
        /// <summary>UI-6 (D-80): trang chi tiết toàn màn (…/GetOverview).</summary>
        public Task<IResult<DeliveryRouteOverviewModel>> GetOverview(Guid id);
        public Task<IResult<DeliveryRouteDetailModel>> Create(DeliveryRouteCreateRequest request);
        public Task<IResult<DeliveryRouteDetailModel>> RecordTemperature(DeliveryTemperatureRequest request);
        public Task<IResult<DeliveryRouteDetailModel>> Dispatch(DeliveryRouteDispatchRequest request);
        /// <summary>BOT-3.2 (D-72): đổi xe / tài xế của chuyến chưa xuất phát.</summary>
        public Task<IResult<DeliveryRouteDetailModel>> Reassign(DeliveryRouteReassignRequest request);
        public Task<IResult<byte[]>> ExportDeliveryNote(Guid id);

        // BE2.1 – lập chuyến nhiều xe theo khu vực, đổi thứ tự điểm dừng, xếp hàng lên xe.
        public Task<IResult<DeliveryRoutePlanModel>> Plan(DeliveryRoutePlanRequest request);
        public Task<IResult<DeliveryRouteDetailModel>> UpdateStops(DeliveryRouteStopsRequest request);
        public Task<IResult<DeliveryLoadModel>> Load(DeliveryRouteLoadRequest request);
        /// <summary>UI-6.34 (D-115): bảng điều phối chuyến × điểm giao.</summary>
        public Task<IResult<AppDelicare.Model.Model.DispatchBoardModel>> GetDispatchBoard(DateTime? date);
        public Task<IResult<AppDelicare.Model.Model.DeliveryOrderActionModel>> MovePoint(AppDelicare.Model.Model.DispatchMovePointRequest request);
        public Task<string?> AutoPlanAfterLockAsync(DateTime date);
        public Task<int> RemindDispatchAsync(DateTime now);
    }
}
