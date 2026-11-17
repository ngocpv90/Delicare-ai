using AutoMapper;
using Dapper;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;
using Microsoft.EntityFrameworkCore;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using AppDelicare.Services.Interface;
using Serilog;
using System.Net;

namespace AppDelicare.Services.Implement
{
    public class DeliveryExecutionService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService) : IDeliveryExecutionService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;

        public async Task<IResults<DeliveryExecutionViewModel>> GetAll(DeliveryExecutionSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@DeliveryOrderId", request.DeliveryOrderId == Guid.Empty ? null : request.DeliveryOrderId);
                dynamicParameters.Add("@DepartedAt", request.DepartedAt);
                dynamicParameters.Add("@ArrivedAt", request.ArrivedAt);
                dynamicParameters.Add("@DeliveredAt", request.DeliveredAt);
                //dynamicParameters.Add("@DriverName", request.DriverName);
                //dynamicParameters.Add("@VehicleCode", request.VehicleCode);
                dynamicParameters.Add("@Status", request.Status);
                //dynamicParameters.Add("@Note", request.Note);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<DeliveryExecutionViewModel>("uspDeliveryExecution_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<DeliveryExecutionViewModel>
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

        public IResults<DeliveryExecution> GetAllData()
        {
            try
            {
                var list = _unitOfWork.DeliveryExecutionRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<DeliveryExecution>
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

        public async Task<IResult<bool>> Save(DeliveryExecutionRequest request, string typeSave)
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
                        result = await Update(request, typeSave);
                        break;
                    case ConstantHelper.TypeSave.Delete:
                        result = await Delete(request.Id, typeSave);
                        break;
                    default:
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

        /// <summary>BE2.1: Thực hiện giao chỉ ghi qua nghiệp vụ (DeliveryRoute/Load / Dispatch, DeliveryStop/Arrive / Deliver / Fail) – CRUD thêm / sửa / xóa không ghi (giữ máy trạng thái, người thao tác, số liệu).</summary>
        private Task<string> Insert(DeliveryExecutionRequest request, string typeSave)
            => Task.FromResult(string.Format(ConstantHelper.Message.DeliveryCrudBlocked, "Thực hiện giao", "DeliveryRoute/Load / Dispatch, DeliveryStop/Arrive / Deliver / Fail"));

        private Task<string> Update(DeliveryExecutionRequest request, string typeSave)
            => Task.FromResult(string.Format(ConstantHelper.Message.DeliveryCrudBlocked, "Thực hiện giao", "DeliveryRoute/Load / Dispatch, DeliveryStop/Arrive / Deliver / Fail"));

        private Task<string> Delete(Guid? id, string typeSave)
            => Task.FromResult(string.Format(ConstantHelper.Message.DeliveryCrudBlocked, "Thực hiện giao", "DeliveryRoute/Load / Dispatch, DeliveryStop/Arrive / Deliver / Fail"));

        /// <summary>
        /// BE2.1 (spec 10.3 GET executions): thực hiện giao của ngày giao – mỗi đơn giao (không tính Hủy) kèm điểm giao, chuyến, điểm dừng, xếp hàng, POD và trạng thái
        /// READY → LOADED → DISPATCHED → ARRIVED → DELIVERED / FAILED (theo DeliveryExecution; chưa có dòng → suy theo đơn giao / điểm dừng) + đếm theo trạng thái.
        /// </summary>
        public async Task<IResult<DeliveryExecutionBoardModel>> GetBoard(DeliveryExecutionBoardRequest request)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                DateTime date = request.DeliveryDate.Date, next = date.AddDays(1);
                var query = db.DeliveryOrder.AsNoTracking()
                    .Where(d => !d.IsDeleted && d.DeliveryDate >= date && d.DeliveryDate < next && d.Status != DeliveryOrderStatus.Cancelled);
                if (request.DeliveryRouteId is { } rid && rid != Guid.Empty)
                {
                    query = query.Where(d => d.DeliveryRouteId == rid);
                }
                var deliveryOrders = await query.ToListAsync();
                var ids = deliveryOrders.Select(d => d.Id).ToList();
                var meals = await DeliveryRules.DeliveryOrderMealsAsync(db, deliveryOrders);
                var stops = await db.DeliveryStop.AsNoTracking().Where(s => !s.IsDeleted && s.DeliveryOrderId != null && ids.Contains(s.DeliveryOrderId.Value)).ToListAsync();
                var routeIds = stops.Select(s => s.DeliveryRouteId).Distinct().ToList();
                var routes = await db.DeliveryRoute.AsNoTracking().Where(r => routeIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id);
                var vehicleIds = routes.Values.Where(r => r.VehicleId != null).Select(r => r.VehicleId!.Value).Distinct().ToList();
                var vehicles = await db.Vehicle.AsNoTracking().Where(v => vehicleIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id);
                var driverIds = routes.Values.Where(r => r.DriverId != null).Select(r => r.DriverId!.Value).Distinct().ToList();
                var drivers = await db.Employee.AsNoTracking().Where(e => driverIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.FullName);
                var orderIds = deliveryOrders.Select(d => d.OrderId).Distinct().ToList();
                var orders = await db.Order.AsNoTracking().Where(o => orderIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id);
                var customerIds = deliveryOrders.Select(d => d.CustomerId).Distinct().ToList();
                var customers = await db.Customer.AsNoTracking().Where(c => customerIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.DisplayName ?? c.Name);
                var locationIds = deliveryOrders.Select(d => d.CustomerDeliveryLocationId ?? d.DeliveryLocationId).Where(x => x != null).Select(x => x!.Value).Distinct().ToList();
                var locations = await db.CustomerDeliveryLocation.AsNoTracking().Where(l => locationIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id);
                var executions = await db.DeliveryExecution.AsNoTracking().Where(e => !e.IsDeleted && ids.Contains(e.DeliveryOrderId)).ToListAsync();
                var loadings = await db.DeliveryLoading.AsNoTracking().Where(l => !l.IsDeleted && ids.Contains(l.DeliveryOrderId) && l.Status == DeliveryLoadingStatus.Loaded).ToListAsync();
                var pods = await db.ProofOfDelivery.AsNoTracking().Where(p => !p.IsDeleted && ids.Contains(p.DeliveryOrderId)).ToListAsync();

                var list = new List<DeliveryExecutionItemModel>();
                foreach (var d in deliveryOrders)
                {
                    var stop = stops.FirstOrDefault(s => s.DeliveryOrderId == d.Id);
                    var route = stop != null ? routes.GetValueOrDefault(stop.DeliveryRouteId) : null;
                    var vehicle = route?.VehicleId is { } vid ? vehicles.GetValueOrDefault(vid) : null;
                    var execution = executions.Where(e => e.DeliveryOrderId == d.Id).OrderByDescending(e => e.CreatedDate).FirstOrDefault();
                    var location = (d.CustomerDeliveryLocationId ?? d.DeliveryLocationId) is { } lid ? locations.GetValueOrDefault(lid) : null;
                    var order = orders.GetValueOrDefault(d.OrderId);
                    string status = execution?.Status ?? d.Status switch
                    {
                        DeliveryOrderStatus.InTransit => stop?.StopStatus == DeliveryStopStatus.Arrived ? DeliveryExecutionStatus.Arrived : DeliveryExecutionStatus.Dispatched,
                        DeliveryOrderStatus.Delivered => DeliveryExecutionStatus.Delivered,
                        DeliveryOrderStatus.Failed => DeliveryExecutionStatus.Failed,
                        _ => DeliveryExecutionStatus.Ready,
                    };
                    list.Add(new DeliveryExecutionItemModel
                    {
                        DeliveryOrderId = d.Id,
                        DeliveryNumber = d.DeliveryNumber ?? d.DeliveryNo,
                        DeliveryOrderStatus = d.Status,
                        MealQuantity = meals[d.Id],
                        OrderId = d.OrderId,
                        OrderNumber = order?.OrderNumber ?? order?.OrderNo,
                        CustomerId = d.CustomerId,
                        CustomerName = customers.GetValueOrDefault(d.CustomerId),
                        DeliveryLocationId = location?.Id,
                        LocationName = location?.Name,
                        Address = location?.Address,
                        DeliveryRouteId = route?.Id,
                        RouteNo = route == null ? null : DeliveryRules.RouteNumber(route),
                        RouteStatus = route?.Status,
                        VehicleCode = vehicle?.Code ?? vehicle?.LicensePlate,
                        DriverName = route?.DriverId is { } did ? drivers.GetValueOrDefault(did) : null,
                        DeliveryStopId = stop?.Id,
                        SequenceNo = stop?.SequenceNo,
                        StopStatus = stop?.StopStatus,
                        PlannedArrival = stop?.PlannedArrival ?? d.PlannedDeliveryTime,
                        ActualArrival = stop?.ActualArrival,
                        LoadedQty = loadings.Where(l => l.DeliveryOrderId == d.Id).OrderByDescending(l => l.LoadingTime).FirstOrDefault()?.LoadedQty,
                        Status = status,
                        StatusName = DeliveryExecutionStatus.Label(status),
                        Execution = execution,
                        Pod = pods.Where(p => p.DeliveryOrderId == d.Id).OrderByDescending(p => p.ConfirmedAt).FirstOrDefault(),
                    });
                }
                var counts = list.GroupBy(x => x.Status).ToDictionary(g => g.Key, g => g.Count());
                if (!string.IsNullOrWhiteSpace(request.Status))
                {
                    list = [.. list.Where(x => string.Equals(x.Status, request.Status.Trim(), StringComparison.OrdinalIgnoreCase))];
                }
                return Result<DeliveryExecutionBoardModel>.Success(new DeliveryExecutionBoardModel
                {
                    DeliveryDate = date,
                    ListItem = [.. list.OrderBy(x => x.RouteNo ?? "~", StringComparer.Ordinal).ThenBy(x => x.SequenceNo).ThenBy(x => x.DeliveryNumber, StringComparer.Ordinal)],
                    StatusCounts = counts,
                }, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetBoard: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<DeliveryExecution>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.DeliveryExecutionRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<DeliveryExecution>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<DeliveryExecution>
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
