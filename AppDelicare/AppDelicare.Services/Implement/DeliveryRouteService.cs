using AutoMapper;
using Dapper;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Model.Request;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using System.Net;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// T19 (Pilot P8) – chuyến xe: Create (CX-yyMMdd-&lt;XE&gt;-NN, đơn giao PH-, điểm dừng) → RecordTemperature (xuất phát) → Dispatch (Planned / Loading → InTransit)
    /// → (điểm dừng: DeliveryStopService Arrive / Deliver / Fail) → Completed tự động khi mọi điểm Đã giao / Thất bại.
    /// Xuất phát bị chặn khi nhiệt độ xuất phát gần nhất &lt; DeliveryTempMinC (60 °C), mẻ của ngày / ca chưa đóng gói xong, mẻ chưa lưu mẫu (anh chốt 2026-11-16).
    /// </summary>
    public class DeliveryRouteService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService, IDocumentNumberService documentNumberService
        , IClock clock, ICurrentActor currentActor, IOptions<BusinessRulesOptions> rules) : IDeliveryRouteService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;
        private readonly IDocumentNumberService _documentNumberService = documentNumberService;
        private readonly IClock _clock = clock;
        private readonly ICurrentActor _currentActor = currentActor;
        private readonly BusinessRulesOptions _rules = rules.Value;

        /// <summary>Trạng thái xe không được xếp chuyến (Vehicle.Status – PROPOSED).</summary>
        private static readonly string[] VehicleBlockedStatus = ["MAINTENANCE", "INACTIVE"];

        public async Task<IResults<DeliveryRouteViewModel>> GetAll(DeliveryRouteSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@Code", request.Code);
                dynamicParameters.Add("@Name", request.Name);
                //dynamicParameters.Add("@Description", request.Description);
                dynamicParameters.Add("@Status", request.Status);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<DeliveryRouteViewModel>("uspDeliveryRoute_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<DeliveryRouteViewModel>
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

        public IResults<DeliveryRoute> GetAllData()
        {
            try
            {
                var list = _unitOfWork.DeliveryRouteRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<DeliveryRoute>
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

        public async Task<IResult<bool>> Save(DeliveryRouteRequest request, string typeSave)
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

        /// <summary>T19: chuyến chỉ lập qua DeliveryRoute/Create (số CX-, đơn giao, điểm dừng) – CRUD Insert không ghi.</summary>
        private Task<string> Insert(DeliveryRouteRequest request, string typeSave)
            => Task.FromResult(string.Format(ConstantHelper.Message.DeliveryCrudBlocked, "Chuyến xe", "DeliveryRoute/Create"));

        /// <summary>T19: CRUD Update chỉ khi chuyến Kế hoạch – tên, mô tả, tài xế, giờ kế hoạch; số, xe, ngày, trạng thái chỉ đổi qua action nghiệp vụ.</summary>
        private async Task<string> Update(DeliveryRouteRequest request, string typeSave)
        {
            var exist = _unitOfWork.DeliveryRouteRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (request.Id == null || exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            if (exist.Status != DeliveryRouteStatus.Planned)
            {
                return InvalidStatus(exist, "sửa chuyến", DeliveryRouteStatus.Planned);
            }
            if (request.DriverId is { } driver && driver != Guid.Empty
                && !await _unitOfWork.DbContext.Employee.AsNoTracking().AnyAsync(e => e.Id == driver && !e.IsDeleted))
            {
                return ConstantHelper.Message.DriverNotFound;
            }
            exist.Name = DeliveryRules.Clean(request.Name) ?? exist.Name;
            exist.Description = DeliveryRules.Clean(request.Description);
            exist.DriverId = request.DriverId is { } d && d != Guid.Empty ? d : null;
            exist.PlannedStart = request.PlannedStart ?? exist.PlannedStart;
            exist.PlannedEnd = request.PlannedEnd ?? exist.PlannedEnd;
            _unitOfWork.DeliveryRouteRepository.Update(exist);
            await _unitOfWork.DeliveryRouteRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        /// <summary>T19: xóa (mềm) chuyến Kế hoạch chưa có sự kiện / nhiệt độ – xóa kèm điểm dừng + đơn giao; đơn hàng trở lại danh sách chờ xếp chuyến.</summary>
        private async Task<string> Delete(Guid? id, string typeSave)
        {
            var route = id == null ? null : _unitOfWork.DeliveryRouteRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (route == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            if (route.Status != DeliveryRouteStatus.Planned)
            {
                return InvalidStatus(route, "xóa chuyến", DeliveryRouteStatus.Planned);
            }
            var db = _unitOfWork.DbContext;
            if (await db.DeliveryEvent.AsNoTracking().AnyAsync(e => e.DeliveryRouteId == route.Id && !e.IsDeleted))
            {
                return ConstantHelper.Message.RouteDeleteHasEvents;
            }
            foreach (var stop in _unitOfWork.DeliveryStopRepository.GetAll().Where(s => s.DeliveryRouteId == route.Id && !s.IsDeleted).ToList())
            {
                _unitOfWork.DeliveryStopRepository.Update(stop);
            }
            foreach (var deliveryOrder in _unitOfWork.DeliveryOrderRepository.GetAll().Where(d => d.DeliveryRouteId == route.Id && !d.IsDeleted).ToList())
            {
                _unitOfWork.DeliveryOrderRepository.Update(deliveryOrder);
            }
            _unitOfWork.DeliveryRouteRepository.Update(route);
            await _unitOfWork.DeliveryRouteRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        public async Task<IResult<DeliveryRoute>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.DeliveryRouteRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<DeliveryRoute>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<DeliveryRoute>
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

        private static string InvalidStatus(DeliveryRoute route, string action, params string[] expected)
            => string.Format(ConstantHelper.Message.StatusActionInvalid, action, "chuyến " + DeliveryRules.RouteNumber(route),
                string.Join(" / ", expected.Select(DeliveryRouteStatus.Label)), DeliveryRouteStatus.Label(route.Status));

        /// <summary>Đơn hàng Đang sản xuất của ngày (dòng thuộc ca hoặc chưa gán ca) chưa có đơn giao còn hiệu lực + số suất.</summary>
        private async Task<List<(Order Order, decimal Quantity)>> CandidatesAsync(DateTime deliveryDate, Guid? shiftId)
        {
            var db = _unitOfWork.DbContext;
            DateTime date = deliveryDate.Date, next = date.AddDays(1);
            var orders = await db.Order.AsNoTracking().Where(o => !o.IsDeleted && o.Status == OrderStatus.InProduction && o.MealDate >= date && o.MealDate < next).ToListAsync();
            var orderIds = orders.Select(o => o.Id).ToList();
            var lines = await db.OrderLine.AsNoTracking().Where(l => !l.IsDeleted && orderIds.Contains(l.OrderId)).ToListAsync();
            var routed = await db.DeliveryOrder.AsNoTracking()
                .Where(d => !d.IsDeleted && orderIds.Contains(d.OrderId) && DeliveryOrderStatus.Active.Contains(d.Status!))
                .Select(d => d.OrderId).Distinct().ToListAsync();
            return [.. orders
                .Where(o => !routed.Contains(o.Id))
                .Where(o => shiftId == null || shiftId == Guid.Empty || lines.Any(l => l.OrderId == o.Id && (l.ShiftId == null || l.ShiftId == shiftId)))
                .Select(o => (o, DeliveryRules.OrderQuantity(o, lines)))];
        }

        /// <summary>T19: đơn hàng chờ xếp chuyến của ngày giao (+ ca).</summary>
        public async Task<IResults<DeliveryReadyOrderModel>> GetReadyOrders(DeliveryDateRequest request)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                var candidates = await CandidatesAsync(request.DeliveryDate, request.ShiftId);
                var customerIds = candidates.Select(c => c.Order.CustomerId).Distinct().ToList();
                var customers = await db.Customer.AsNoTracking().Where(c => customerIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.DisplayName ?? c.Name);
                var locationIds = candidates.Where(c => c.Order.DeliveryLocationId != null).Select(c => c.Order.DeliveryLocationId!.Value).Distinct().ToList();
                var locations = await db.CustomerDeliveryLocation.AsNoTracking().Where(l => locationIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id);
                var list = candidates.Select(c =>
                {
                    var location = c.Order.DeliveryLocationId is { } lid ? locations.GetValueOrDefault(lid) : null;
                    return new DeliveryReadyOrderModel
                    {
                        OrderId = c.Order.Id,
                        OrderNumber = c.Order.OrderNumber ?? c.Order.OrderNo,
                        CustomerId = c.Order.CustomerId,
                        CustomerName = customers.GetValueOrDefault(c.Order.CustomerId),
                        DeliveryLocationId = c.Order.DeliveryLocationId,
                        LocationName = location?.Name,
                        Address = location?.Address,
                        DeliveryWindowFrom = location?.DeliveryWindowFrom,
                        MealQuantity = c.Quantity,
                        Status = c.Order.Status,
                    };
                }).OrderBy(x => x.DeliveryWindowFrom ?? TimeSpan.MaxValue).ThenBy(x => x.LocationName).ToList();
                return Results<DeliveryReadyOrderModel>.Success(list, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetReadyOrders: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>
        /// T19 (spec 10.2 POST routes + plan, Pilot P8): lập chuyến cho 1 ngày / ca / xe trong 1 transaction.
        /// <list type="bullet">
        /// <item>Số chuyến <c>CX-yyMMdd-&lt;mã xe&gt;-NN</c> (T11, hậu tố = Vehicle.Code, trống thì biển số); xe Bảo trì / Ngừng → chặn; tài xế phải là Employee.</item>
        /// <item>Đơn: Đang sản xuất, ngày ăn = ngày giao, dòng thuộc ca, chưa có đơn giao còn hiệu lực (Kế hoạch / Đang giao / Đã giao). Trống danh sách = lấy hết.</item>
        /// <item>Mỗi đơn → 1 đơn giao <c>PH-</c> (DeliveryNumber + DeliveryNo) + 1 điểm dừng: suất kế hoạch = tổng suất đơn, giờ dự kiến = ngày + DeliveryWindowFrom của điểm giao;
        /// thứ tự theo giờ dự kiến rồi tên điểm. Giờ xuất phát kế hoạch (trống) = giờ dự kiến sớm nhất − TransportMinutesBeforeCutoff.</item>
        /// <item>Cảnh báo (không chặn): vượt tải xe, xe đã có chuyến chưa xong trong ngày, sản xuất chưa sẵn sàng (mẻ chưa đóng gói / chưa lưu mẫu).</item>
        /// </list>
        /// </summary>
        public async Task<IResult<DeliveryRouteDetailModel>> Create(DeliveryRouteCreateRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                _currentActor.RequireActor();
                var db = _unitOfWork.DbContext;
                DateTime date = request.DeliveryDate.Date;
                var vehicle = await db.Vehicle.AsNoTracking().FirstOrDefaultAsync(v => v.Id == request.VehicleId && !v.IsDeleted);
                if (vehicle == null)
                {
                    return Fail(ConstantHelper.Message.VehicleNotFound);
                }
                string vehicleCode = vehicle.Code ?? vehicle.LicensePlate ?? string.Empty;
                if (vehicle.Status != null && VehicleBlockedStatus.Contains(vehicle.Status.ToUpperInvariant()))
                {
                    return Fail(string.Format(ConstantHelper.Message.VehicleUnavailable, vehicleCode, vehicle.Status));
                }
                if (string.IsNullOrWhiteSpace(vehicleCode))
                {
                    return Fail(ConstantHelper.Message.VehicleNotFound + " (xe chưa có mã / biển số để đặt số chuyến)");
                }
                if (request.DriverId is { } driver && driver != Guid.Empty && !await db.Employee.AsNoTracking().AnyAsync(e => e.Id == driver && !e.IsDeleted))
                {
                    return Fail(ConstantHelper.Message.DriverNotFound);
                }
                Shift? shift = null;
                if (request.ShiftId is { } sid && sid != Guid.Empty)
                {
                    shift = await db.Shift.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sid && !s.IsDeleted);
                    if (shift == null)
                    {
                        return Fail(ConstantHelper.Message.ShiftNotFound);
                    }
                }

                var candidates = await CandidatesAsync(date, shift?.Id);
                List<(Order Order, decimal Quantity)> selected;
                if (request.ListOrderId.Count == 0)
                {
                    selected = candidates;
                    if (selected.Count == 0)
                    {
                        return Fail(string.Format(ConstantHelper.Message.RouteNoOrders, ProductionRules.Day(date)));
                    }
                }
                else
                {
                    selected = [];
                    foreach (Guid orderId in request.ListOrderId.Distinct())
                    {
                        var hit = candidates.FirstOrDefault(c => c.Order.Id == orderId);
                        if (hit.Order == null)
                        {
                            var order = await db.Order.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted);
                            string reason = order == null ? "không tồn tại"
                                : order.Status != OrderStatus.InProduction ? "đơn ở trạng thái " + OrderStatus.Label(order.Status) + " (cần Đang sản xuất)"
                                : order.MealDate.Date != date ? "ngày ăn " + ProductionRules.Day(order.MealDate) + " khác ngày giao"
                                : "đã có chuyến hoặc không có dòng thuộc ca";
                            return Fail(string.Format(ConstantHelper.Message.RouteOrderInvalid, order?.OrderNumber ?? order?.OrderNo ?? orderId.ToString(), reason));
                        }
                        selected.Add(hit);
                    }
                }

                var warnings = new List<string>();
                var locationIds = selected.Where(s => s.Order.DeliveryLocationId != null).Select(s => s.Order.DeliveryLocationId!.Value).Distinct().ToList();
                var locations = await db.CustomerDeliveryLocation.AsNoTracking().Where(l => locationIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id);
                var plannedStops = selected.Select(s =>
                {
                    var location = s.Order.DeliveryLocationId is { } lid ? locations.GetValueOrDefault(lid) : null;
                    DateTime? arrival = location?.DeliveryWindowFrom is { } from ? date.Add(from) : null;
                    return (s.Order, s.Quantity, Location: location, Arrival: arrival);
                })
                .OrderBy(s => s.Arrival ?? DateTime.MaxValue).ThenBy(s => s.Location?.Name).ThenBy(s => s.Order.OrderNumber ?? s.Order.OrderNo)
                .ToList();

                decimal load = plannedStops.Sum(s => s.Quantity);
                if (vehicle.Capacity is > 0 && load > vehicle.Capacity.Value)
                {
                    warnings.Add(string.Format(ConstantHelper.Message.RouteOverCapacity, DeliveryRules.Num(load), vehicleCode, DeliveryRules.Num(vehicle.Capacity.Value)));
                }
                DateTime next = date.AddDays(1);
                var busy = await db.DeliveryRoute.AsNoTracking()
                    .Where(r => !r.IsDeleted && r.VehicleId == vehicle.Id && r.DeliveryDate >= date && r.DeliveryDate < next && r.Status != DeliveryRouteStatus.Completed)
                    .Select(r => r.RouteNo).ToListAsync();
                if (busy.Count != 0)
                {
                    warnings.Add(string.Format(ConstantHelper.Message.RouteVehicleBusy, vehicleCode, string.Join(", ", busy)));
                }
                var orderIds = selected.Select(s => s.Order.Id).ToList();
                List<Guid> shiftIds = shift != null ? [shift.Id]
                    : await DeliveryRules.OrderShiftIdsAsync(db, orderIds, _rules);
                var notReady = DeliveryRules.ProductionBlockers(await DeliveryRules.BatchesAsync(db, date, shiftIds), _rules);
                if (notReady.Count != 0)
                {
                    warnings.Add(string.Format(ConstantHelper.Message.RouteNotReadyWarning, string.Join("; ", notReady)));
                }

                DateTime? firstArrival = plannedStops.Select(s => s.Arrival).Where(a => a != null).Min();
                string routeNo = await _documentNumberService.NextAsync(DocumentKind.DeliveryRoute, date, vehicleCode);
                var route = new DeliveryRoute
                {
                    Id = Guid.NewGuid(),
                    RouteNo = routeNo,
                    Code = routeNo,
                    DeliveryDate = date,
                    VehicleId = vehicle.Id,
                    DriverId = request.DriverId is { } dr && dr != Guid.Empty ? dr : null,
                    PlannedStart = request.PlannedStart ?? firstArrival?.AddMinutes(-_rules.TransportMinutesBeforeCutoff),
                    PlannedEnd = request.PlannedEnd,
                    Status = DeliveryRouteStatus.Planned,
                    Name = DeliveryRules.Clean(request.Name) ?? $"Chuyến {(shift?.Code is { } sc ? sc + " " : string.Empty)}{date:dd/MM} – xe {vehicleCode}",
                    Description = DeliveryRules.Clean(request.Description),
                };
                _unitOfWork.DeliveryRouteRepository.Add(route);
                string first = await _documentNumberService.NextAsync(DocumentKind.Voucher, date);
                int seq = 0;
                foreach (var s in plannedStops)
                {
                    string number = ProductionRules.Increment(first, seq);
                    seq++;
                    var deliveryOrder = new DeliveryOrder
                    {
                        Id = Guid.NewGuid(),
                        DeliveryNumber = number,
                        DeliveryNo = number,
                        OrderId = s.Order.Id,
                        CustomerId = s.Order.CustomerId,
                        DeliveryLocationId = s.Order.DeliveryLocationId,
                        CustomerDeliveryLocationId = s.Order.DeliveryLocationId,
                        DeliveryDate = date,
                        DeliveryRouteId = route.Id,
                        PlannedDeliveryTime = s.Arrival ?? date,
                        Status = DeliveryOrderStatus.Planned,
                        Note = "Chuyến " + routeNo,
                    };
                    _unitOfWork.DeliveryOrderRepository.Add(deliveryOrder);
                    _unitOfWork.DeliveryStopRepository.Add(new DeliveryStop
                    {
                        Id = Guid.NewGuid(),
                        DeliveryRouteId = route.Id,
                        DeliveryOrderId = deliveryOrder.Id,
                        CustomerId = s.Order.CustomerId,
                        DeliveryLocationId = s.Order.DeliveryLocationId,
                        SequenceNo = seq,
                        PlannedArrival = s.Arrival,
                        PlannedMealQty = s.Quantity,
                        StopStatus = DeliveryStopStatus.Planned,
                    });
                }
                await _unitOfWork.DeliveryRouteRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
                _unitOfWork.CommitTransaction();
                var detail = await DeliveryRules.RouteDetailAsync(db, route.Id, _rules);
                detail!.ListWarning.InsertRange(0, warnings);
                return Result<DeliveryRouteDetailModel>.Success(detail, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Create: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                _unitOfWork.DbContext.ChangeTracker.Clear();
                throw;
            }

            IResult<DeliveryRouteDetailModel> Fail(string message)
            {
                _unitOfWork.RollbackTransaction();
                _unitOfWork.DbContext.ChangeTracker.Clear();
                return Result<DeliveryRouteDetailModel>.Fail(message);
            }
        }

        /// <summary>
        /// T19 (BUSINESS_RULES §7): ghi nhiệt độ giao – cấp chuyến (xuất phát, chuyến Kế hoạch / Đang xếp) hoặc cấp điểm dừng (bàn giao, chuyến Đang giao, điểm chưa xong).
        /// TemperatureLog DELIVERY + DeliveryEvent TEMP_CHECK (sổ chỉ ghi thêm). &lt; 60 °C bắt buộc hành động khắc phục; lần đo gần nhất quyết định chặn.
        /// </summary>
        public async Task<IResult<DeliveryRouteDetailModel>> RecordTemperature(DeliveryTemperatureRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var actor = _currentActor.RequireActor();
                var db = _unitOfWork.DbContext;
                var route = await db.DeliveryRoute.AsNoTracking().FirstOrDefaultAsync(r => r.Id == request.Id && !r.IsDeleted);
                if (route == null)
                {
                    return Fail(ConstantHelper.Message.RouteNotFound);
                }
                Guid? stopRef = request.DeliveryStopId is { } stopId && stopId != Guid.Empty ? stopId : null;
                Guid? deliveryOrderId = null;
                if (stopRef != null)
                {
                    var stop = await db.DeliveryStop.AsNoTracking().FirstOrDefaultAsync(s => s.Id == stopRef && s.DeliveryRouteId == route.Id && !s.IsDeleted);
                    if (stop == null)
                    {
                        return Fail(ConstantHelper.Message.StopNotFound);
                    }
                    if (route.Status != DeliveryRouteStatus.InTransit || DeliveryStopStatus.Closed.Contains(stop.StopStatus))
                    {
                        return Fail(ConstantHelper.Message.RouteStopTempInvalid);
                    }
                    deliveryOrderId = stop.DeliveryOrderId;
                }
                else if (route.Status is not (DeliveryRouteStatus.Planned or DeliveryRouteStatus.Loading))
                {
                    return Fail(ConstantHelper.Message.RouteDepartureTempInvalid);
                }
                var warnings = new List<string>();
                string? error = DeliveryRules.AddTemperature(_unitOfWork, _rules, actor, request.MeasuredAt ?? _clock.Now, route.Id, stopRef, deliveryOrderId,
                    request.TemperatureC, request.CorrectiveAction, request.DeviceCode, request.EvidenceUrl, warnings, out _);
                if (error != null)
                {
                    return Fail(error);
                }
                await _unitOfWork.DeliveryEventRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
                _unitOfWork.CommitTransaction();
                var detail = await DeliveryRules.RouteDetailAsync(db, route.Id, _rules);
                detail!.ListWarning.InsertRange(0, warnings);
                return Result<DeliveryRouteDetailModel>.Success(detail, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " RecordTemperature: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                _unitOfWork.DbContext.ChangeTracker.Clear();
                throw;
            }

            IResult<DeliveryRouteDetailModel> Fail(string message)
            {
                _unitOfWork.RollbackTransaction();
                _unitOfWork.DbContext.ChangeTracker.Clear();
                return Result<DeliveryRouteDetailModel>.Fail(message);
            }
        }

        /// <summary>
        /// T19 (spec 10.2 dispatch, BUSINESS_RULES §7): Kế hoạch / Đang xếp → Đang giao, ActualStart = bây giờ; đơn giao → Đang giao; sự kiện DISPATCHED.
        /// Có <c>TemperatureC</c> → ghi lần đo xuất phát trước. Chặn khi: nhiệt độ xuất phát gần nhất &lt; 60 °C / chưa đo; mẻ của ngày / ca chưa đóng gói xong
        /// (<c>DeliveryRequirePackedBatches</c>); mẻ chưa lưu mẫu (<c>DeliveryRequireSampleRetained</c> – anh chốt 2026-11-16). Bị chặn mà đã ghi nhiệt độ ở lần gọi này
        /// (dưới ngưỡng kèm khắc phục) → vẫn lưu lần đo (sổ ATTP) và trả lỗi nghiệp vụ.
        /// </summary>
        public async Task<IResult<DeliveryRouteDetailModel>> Dispatch(DeliveryRouteDispatchRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var actor = _currentActor.RequireActor();
                var db = _unitOfWork.DbContext;
                var route = _unitOfWork.DeliveryRouteRepository.GetSingle(r => r.Id == request.Id && !r.IsDeleted);
                if (route == null)
                {
                    return Fail(ConstantHelper.Message.RouteNotFound);
                }
                if (route.Status is not (DeliveryRouteStatus.Planned or DeliveryRouteStatus.Loading))
                {
                    return Fail(InvalidStatus(route, "xuất phát", DeliveryRouteStatus.Planned, DeliveryRouteStatus.Loading));
                }
                var warnings = new List<string>();
                DateTime now = _clock.Now;
                bool recorded = false;
                if (request.TemperatureC is { } temperature)
                {
                    string? error = DeliveryRules.AddTemperature(_unitOfWork, _rules, actor, now, route.Id, null, null,
                        temperature, request.CorrectiveAction, request.DeviceCode, null, warnings, out _);
                    if (error != null)
                    {
                        return Fail(error);
                    }
                    await _unitOfWork.DeliveryEventRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
                    recorded = true;
                }
                var blockers = await DeliveryRules.DispatchBlockersAsync(db, route, _rules);
                if (blockers.Count != 0)
                {
                    string message = string.Format(ConstantHelper.Message.RouteCannotDispatch, DeliveryRules.RouteNumber(route), string.Join("; ", blockers));
                    if (recorded)
                    {
                        _unitOfWork.CommitTransaction();
                        return Result<DeliveryRouteDetailModel>.Fail("Đã lưu lần đo nhiệt độ. " + message, await DeliveryRules.RouteDetailAsync(db, route.Id, _rules));
                    }
                    return Fail(message);
                }
                route.Status = DeliveryRouteStatus.InTransit;
                route.ActualStart = now;
                _unitOfWork.DeliveryRouteRepository.Update(route);
                foreach (var deliveryOrder in _unitOfWork.DeliveryOrderRepository.GetAll().Where(d => d.DeliveryRouteId == route.Id && !d.IsDeleted && d.Status == DeliveryOrderStatus.Planned).ToList())
                {
                    deliveryOrder.Status = DeliveryOrderStatus.InTransit;
                    _unitOfWork.DeliveryOrderRepository.Update(deliveryOrder);
                }
                DeliveryRules.AddEvent(_unitOfWork, actor, now, route.Id, null, null, DeliveryCode.EventDispatched,
                    "Xuất phát chuyến " + DeliveryRules.RouteNumber(route) + (string.IsNullOrWhiteSpace(request.Note) ? string.Empty : " – " + request.Note.Trim()),
                    request.Latitude, request.Longitude);
                await _unitOfWork.DeliveryRouteRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                var detail = await DeliveryRules.RouteDetailAsync(db, route.Id, _rules);
                detail!.ListWarning.InsertRange(0, warnings);
                return Result<DeliveryRouteDetailModel>.Success(detail, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Dispatch: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                _unitOfWork.DbContext.ChangeTracker.Clear();
                throw;
            }

            IResult<DeliveryRouteDetailModel> Fail(string message)
            {
                _unitOfWork.RollbackTransaction();
                _unitOfWork.DbContext.ChangeTracker.Clear();
                return Result<DeliveryRouteDetailModel>.Fail(message);
            }
        }

        /// <summary>T19 (spec 10.2 GET routes): chuyến của ngày giao – xe, tài xế, điểm dừng, tải / % tải, đếm theo trạng thái.</summary>
        public async Task<IResult<DeliveryBoardModel>> GetBoard(DeliveryDateRequest request)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                DateTime date = request.DeliveryDate.Date, next = date.AddDays(1);
                var routes = await db.DeliveryRoute.AsNoTracking()
                    .Where(r => !r.IsDeleted && r.DeliveryDate >= date && r.DeliveryDate < next && (string.IsNullOrEmpty(request.Status) || r.Status == request.Status))
                    .OrderBy(r => r.PlannedStart).ThenBy(r => r.RouteNo).ToListAsync();
                var list = await DeliveryRules.RouteDetailsAsync(db, routes, _rules, withBatches: false);
                return Result<DeliveryBoardModel>.Success(new DeliveryBoardModel
                {
                    DeliveryDate = date,
                    ListRoute = list,
                    StatusCounts = list.GroupBy(r => r.Route.Status ?? string.Empty).ToDictionary(g => g.Key, g => g.Count()),
                }, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetBoard: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>T19: chi tiết chuyến – điểm dừng + POD, sự kiện / nhiệt độ, mẻ của ngày / ca, điều kiện xuất phát còn thiếu, thao tác được phép.</summary>
        public async Task<IResult<DeliveryRouteDetailModel>> GetDetail(Guid id)
        {
            try
            {
                var detail = await DeliveryRules.RouteDetailAsync(_unitOfWork.DbContext, id, _rules);
                return detail == null ? Result<DeliveryRouteDetailModel>.Fail(ConstantHelper.Message.RouteNotFound)
                    : Result<DeliveryRouteDetailModel>.Success(detail, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetDetail: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>T19: in Biên bản giao hàng / phiếu chuyến (.xlsx).</summary>
        public async Task<IResult<byte[]>> ExportDeliveryNote(Guid id)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                var detail = await DeliveryRules.RouteDetailAsync(db, id, _rules);
                if (detail == null)
                {
                    return Result<byte[]>.Fail(ConstantHelper.Message.RouteNotFound);
                }
                var company = await db.Company.AsNoTracking().Where(c => !c.IsDeleted).OrderBy(c => c.CreatedDate).FirstOrDefaultAsync();
                return Result<byte[]>.Success(DeliveryRules.BuildDeliveryNote(company?.Name, detail), ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ExportDeliveryNote: ", ex.ToString()));
                throw;
            }
        }
    }
}
