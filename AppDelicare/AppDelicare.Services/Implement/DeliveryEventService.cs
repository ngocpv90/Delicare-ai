#nullable enable
using AutoMapper;
using Dapper;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Share;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Net;

namespace AppDelicare.Services.Implement
{
    public class DeliveryEventService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper, IReferenceGuard referenceGuard
        , IClock clock, ICurrentActor currentActor) : IDeliveryEventService
    {
        private readonly IClock _clock = clock;
        private readonly ICurrentActor _currentActor = currentActor;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IReferenceGuard _referenceGuard = referenceGuard;

        public async Task<IResults<DeliveryEventViewModel>> GetAll(DeliveryEventSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@DeliveryRouteId", request.DeliveryRouteId);
                dynamicParameters.Add("@DeliveryStopId", request.DeliveryStopId);
                dynamicParameters.Add("@DeliveryOrderId", request.DeliveryOrderId);
                dynamicParameters.Add("@EventType", request.EventType);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<DeliveryEventViewModel>("uspDeliveryEvent_GetAll", dynamicParameters)).ToList();
                int i = 0;
                list.ForEach(x =>
                {
                    i++;
                    x.STT = (request.Page - 1) * request.PageSize + i;
                });
                return new Results<DeliveryEventViewModel>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.GetAllSuccessfully,
                    ListData = list,
                    TotalRecords = list.FirstOrDefault()?.TotalRow ?? 0
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetAll: ", ex.ToString()));
                throw;
            }
        }

        public IResults<DeliveryEvent> GetAllData()
        {
            try
            {
                var list = _unitOfWork.DeliveryEventRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<DeliveryEvent>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.GetAllSuccessfully,
                    ListData = list,
                    TotalRecords = list.Count
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetAllData: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<bool>> Save(DeliveryEventRequest request, string typeSave)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var result = string.Empty;
                switch (typeSave)
                {
      case ConstantHelper.TypeSave.Insert:
          result = await Insert(request, typeSave);
          break;
      case ConstantHelper.TypeSave.Update:
      case ConstantHelper.TypeSave.Delete:
          result = "Sổ chỉ ghi thêm: không sửa / xóa. Dùng bút toán đảo (CLAUDE.md 3.3).";
          break;                    default:
                        result = "Loại thao tác lưu dữ liệu không hợp lệ.";
                        break;
                }
                _unitOfWork.CommitTransaction();
                if (!string.IsNullOrEmpty(result))
                {
                    return new Result<bool>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = result,
                        Data = false
                    };
                }
                return new Result<bool>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = typeSave == ConstantHelper.TypeSave.Delete ? ConstantHelper.Message.DeleteSuccess : ConstantHelper.Message.SaveSuccess,
                    Data = true
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Save: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        /// <summary>
        /// T19: sự kiện giao hàng (gồm lần đo nhiệt độ TEMP_CHECK là căn cứ chặn xuất phát / bàn giao) chỉ ghi qua action của chuyến / điểm dừng
        /// (DeliveryRoute/RecordTemperature, Dispatch; DeliveryStop/Arrive, Deliver, Fail) – CRUD Insert không ghi để không giả được nhiệt độ đạt.
        /// </summary>
        private Task<string> Insert(DeliveryEventRequest request, string typeSave)
            => Task.FromResult(string.Format(ConstantHelper.Message.DeliveryCrudBlocked, "Sự kiện giao hàng", "DeliveryRoute/RecordTemperature, Dispatch; DeliveryStop/Arrive, Deliver, Fail"));


        /// <summary>
        /// BE3.1 (Catalog 193, spec 10.3 vehicle-positions, GAP-F-05): tọa độ <b>theo sự kiện</b> (DeliveryEvent có vĩ độ / kinh độ – đến điểm, giao không
        /// thành công, vị trí tài xế ghi…) của các chuyến có ngày giao = <c>deliveryDate</c> (lọc chuyến tùy chọn). Không dùng LocationTracking (nhật ký IP).
        /// <c>listLatest</c> = vị trí mới nhất của mỗi chuyến (vẽ xe trên bản đồ).
        /// </summary>
        public async Task<IResult<VehiclePositionModel>> GetVehiclePositions(VehiclePositionRequest request)
        {
            try
            {
                if (request.DeliveryDate is not { } date)
                {
                    return Result<VehiclePositionModel>.Fail(ConstantHelper.Message.DeliveryDateRequired);
                }
                var db = _unitOfWork.DbContext;
                DateTime day = date.Date, next = day.AddDays(1);
                var routeIds = db.DeliveryRoute.AsNoTracking().Where(r => !r.IsDeleted && r.DeliveryDate >= day && r.DeliveryDate < next).Select(r => r.Id);
                var query = db.DeliveryEvent.AsNoTracking()
                    .Where(e => !e.IsDeleted && e.Latitude != null && e.Longitude != null && routeIds.Contains(e.DeliveryRouteId));
                if (request.RouteId is { } route && route != Guid.Empty)
                {
                    query = query.Where(e => e.DeliveryRouteId == route);
                }
                var events = await query.OrderBy(e => e.EventAt).ThenBy(e => e.CreatedDate).ToListAsync();
                var items = await ChartRules.DeliveryEventItemsAsync(db, events);
                return Result<VehiclePositionModel>.Success(new VehiclePositionModel
                {
                    DeliveryDate = day,
                    ListPosition = items,
                    ListLatest = items.GroupBy(i => i.DeliveryRouteId).Select(g => g.Last()).OrderBy(i => i.RouteNumber).ToList(),
                }, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetVehiclePositions: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>
        /// BE3.1 (Catalog 193, PROPOSED D-57 g / m): tài xế ghi vị trí hiện tại của chuyến – thêm 1 DeliveryEvent <c>POSITION</c> (giờ máy chủ, người ghi = người gọi).
        /// Chỉ khi chuyến Đang xếp / Đang giao; vĩ độ -90…90, kinh độ -180…180. Chỉ ghi vào bảng có sẵn.
        /// </summary>
        public async Task<IResult<DeliveryEventItemModel>> RecordPosition(DeliveryPositionRequest request)
        {
            try
            {
                var actor = _currentActor.RequireActor();
                if (request.Latitude is not { } lat || request.Longitude is not { } lng || lat < -90m || lat > 90m || lng < -180m || lng > 180m)
                {
                    return Result<DeliveryEventItemModel>.Fail(ConstantHelper.Message.DeliveryPositionInvalid);
                }
                var db = _unitOfWork.DbContext;
                var route = await db.DeliveryRoute.AsNoTracking().FirstOrDefaultAsync(r => r.Id == request.RouteId && !r.IsDeleted);
                if (route == null)
                {
                    return Result<DeliveryEventItemModel>.Fail(ConstantHelper.Message.RouteNotFound);
                }
                if (route.Status != DeliveryRouteStatus.Loading && route.Status != DeliveryRouteStatus.InTransit)
                {
                    return Result<DeliveryEventItemModel>.Fail(string.Format(ConstantHelper.Message.DeliveryPositionRouteNotRunning, DeliveryRouteStatus.Label(route.Status)));
                }
                var ev = new DeliveryEvent
                {
                    Id = Guid.NewGuid(),
                    DeliveryRouteId = route.Id,
                    EventType = DeliveryCode.EventPosition,
                    EventAt = _clock.Now,
                    Latitude = Math.Round(lat, 7),
                    Longitude = Math.Round(lng, 7),
                    Description = string.IsNullOrWhiteSpace(request.Note) ? "Vị trí xe" : request.Note.Trim(),
                    RecordedBy = actor.UserId,
                    SourceChannel = DeliveryCode.SourceManual,
                };
                _unitOfWork.DeliveryEventRepository.Add(ev);
                await _unitOfWork.DeliveryEventRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
                var item = (await ChartRules.DeliveryEventItemsAsync(db, [ev])).Single();
                return Result<DeliveryEventItemModel>.Success(item, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " RecordPosition: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<DeliveryEvent>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.DeliveryEventRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<DeliveryEvent>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<DeliveryEvent>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.GetAllSuccessfully,
                    Data = data,
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetById: ", ex.ToString()));
                throw;
            }
        }
    }
}
