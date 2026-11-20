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

using AppDelicare.Services.Implement.Messaging;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// T19 (Pilot P8) – chuyến xe: Create (CX-yyMMdd-&lt;XE&gt;-NN, đơn giao PH-, điểm dừng) → RecordTemperature (xuất phát) → Dispatch (Planned / Loading → InTransit)
    /// → (điểm dừng: DeliveryStopService Arrive / Deliver / Fail) → Completed tự động khi mọi điểm Đã giao / Thất bại.
    /// Xuất phát bị chặn khi nhiệt độ xuất phát gần nhất &lt; DeliveryTempMinC (60 °C), mẻ của ngày / ca chưa đóng gói xong, mẻ chưa lưu mẫu (anh chốt 2026-11-16).
    /// </summary>
    public class DeliveryRouteService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService, IDocumentNumberService documentNumberService
        , IClock clock, ICurrentActor currentActor, IOptions<BusinessRulesOptions> rules, INotificationService notifications) : IDeliveryRouteService
    {
        private readonly INotificationService _notifications = notifications; // BOT-1.7
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
            // BE2.1 (spec 10.2 PUT routes): 1 tài xế không trùng khung giờ cùng ngày.
            string? overlap = await DriverOverlapAsync(exist.DriverId, exist.DeliveryDate, exist.PlannedStart, exist.PlannedEnd, exist.Id);
            if (overlap != null)
            {
                return overlap;
            }
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

        /// <summary>
        /// Đơn hàng Đang sản xuất của ngày (dòng thuộc ca hoặc chưa gán ca) còn suất chưa nằm trong đơn giao còn hiệu lực + số suất còn lại
        /// (BE2.1: 1 đơn hàng có thể có nhiều đơn giao – lập tay / tách; T19: chưa có đơn giao → toàn bộ suất).
        /// </summary>
        private async Task<List<(Order Order, decimal Quantity)>> CandidatesAsync(DateTime deliveryDate, Guid? shiftId)
        {
            var db = _unitOfWork.DbContext;
            DateTime date = deliveryDate.Date, next = date.AddDays(1);
            var orders = await db.Order.AsNoTracking().Where(o => !o.IsDeleted && o.Status == OrderStatus.InProduction && o.MealDate >= date && o.MealDate < next).ToListAsync();
            var orderIds = orders.Select(o => o.Id).ToList();
            var lines = await db.OrderLine.AsNoTracking().Where(l => !l.IsDeleted && orderIds.Contains(l.OrderId)).ToListAsync();
            var remaining = await DeliveryRules.RemainingMealsAsync(db, orders);
            return [.. orders
                .Where(o => remaining[o.Id] > 0)
                .Where(o => shiftId == null || shiftId == Guid.Empty || lines.Any(l => l.OrderId == o.Id && (l.ShiftId == null || l.ShiftId == shiftId)))
                .Select(o => (o, remaining[o.Id]))];
        }

        /// <summary>Đơn giao Kế hoạch chưa gán chuyến của ngày giao (BE2.1 – lập tay / tách), lọc ca theo dòng đơn hàng.</summary>
        private async Task<List<(DeliveryOrder DeliveryOrder, Order Order, decimal Quantity)>> UnassignedAsync(DateTime deliveryDate, Guid? shiftId, IReadOnlyCollection<Guid>? onlyIds = null)
        {
            var db = _unitOfWork.DbContext;
            DateTime date = deliveryDate.Date, next = date.AddDays(1);
            var list = await db.DeliveryOrder.AsNoTracking()
                .Where(d => !d.IsDeleted && d.DeliveryRouteId == null && d.Status == DeliveryOrderStatus.Planned && d.DeliveryDate >= date && d.DeliveryDate < next)
                .ToListAsync();
            if (onlyIds != null)
            {
                list = [.. list.Where(d => onlyIds.Contains(d.Id))];
            }
            var orderIds = list.Select(d => d.OrderId).Distinct().ToList();
            var orders = await db.Order.AsNoTracking().Where(o => orderIds.Contains(o.Id) && !o.IsDeleted).ToDictionaryAsync(o => o.Id);
            var lines = await db.OrderLine.AsNoTracking().Where(l => !l.IsDeleted && orderIds.Contains(l.OrderId)).ToListAsync();
            var meals = await DeliveryRules.DeliveryOrderMealsAsync(db, list);
            return [.. list
                .Where(d => orders.ContainsKey(d.OrderId))
                .Where(d => shiftId == null || shiftId == Guid.Empty || lines.Any(l => l.OrderId == d.OrderId && (l.ShiftId == null || l.ShiftId == shiftId)))
                .Select(d => (d, orders[d.OrderId], meals[d.Id]))];
        }

        /// <summary>1 điểm dừng sẽ lập: đơn hàng (phần suất còn lại → đơn giao mới) hoặc đơn giao có sẵn chưa gán chuyến.</summary>
        private sealed record RouteItem(Order Order, DeliveryOrder? Existing, decimal Quantity, Guid? LocationId);

        /// <summary>Kiểm xe (không Bảo trì / Ngừng, có mã) + tài xế. Trả (xe, mã xe, lỗi).</summary>
        private async Task<(Vehicle? Vehicle, string Code, string? Error)> VehicleAsync(Guid vehicleId, Guid? driverId)
        {
            var db = _unitOfWork.DbContext;
            var vehicle = await db.Vehicle.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vehicleId && !v.IsDeleted);
            if (vehicle == null)
            {
                return (null, string.Empty, ConstantHelper.Message.VehicleNotFound);
            }
            string code = vehicle.Code ?? vehicle.LicensePlate ?? string.Empty;
            if (vehicle.Status != null && VehicleBlockedStatus.Contains(vehicle.Status.ToUpperInvariant()))
            {
                return (null, code, string.Format(ConstantHelper.Message.VehicleUnavailable, code, vehicle.Status));
            }
            if (string.IsNullOrWhiteSpace(code))
            {
                return (null, code, ConstantHelper.Message.VehicleNotFound + " (xe chưa có mã / biển số để đặt số chuyến)");
            }
            if (driverId is { } driver && driver != Guid.Empty && !await db.Employee.AsNoTracking().AnyAsync(e => e.Id == driver && !e.IsDeleted))
            {
                return (null, code, ConstantHelper.Message.DriverNotFound);
            }
            return (vehicle, code, null);
        }

        /// <summary>
        /// BE2.1 (spec 10.2 POST routes: "một xe / tài xế không trùng khung giờ cùng ngày"): tài xế đã có chuyến chưa hoàn thành cùng ngày mà khung giờ kế hoạch giao nhau
        /// (chỉ xét khi cả 2 chuyến có giờ bắt đầu + kết thúc) → lỗi. PROPOSED – BA/PO CONFIRM.
        /// </summary>
        private async Task<string?> DriverOverlapAsync(Guid? driverId, DateTime date, DateTime? start, DateTime? end, Guid? exceptRouteId)
        {
            if (driverId is not { } driver || driver == Guid.Empty || start == null || end == null)
            {
                return null;
            }
            DateTime next = date.Date.AddDays(1);
            var clash = await _unitOfWork.DbContext.DeliveryRoute.AsNoTracking()
                .Where(r => !r.IsDeleted && r.DriverId == driver && r.Id != exceptRouteId && r.DeliveryDate >= date.Date && r.DeliveryDate < next
                    && r.Status != DeliveryRouteStatus.Completed && r.PlannedStart != null && r.PlannedEnd != null
                    && r.PlannedStart < end && start < r.PlannedEnd)
                .Select(r => r.RouteNo).ToListAsync();
            return clash.Count == 0 ? null : string.Format(ConstantHelper.Message.DriverBusy, string.Join(", ", clash));
        }

        /// <summary>
        /// Lập 1 chuyến (trong transaction đang mở, có lưu): số CX-, đơn giao PH- cho phần suất mới (tag [SUAT:n] – GAP-DLV-08) hoặc gắn đơn giao có sẵn, điểm dừng theo giờ giao;
        /// cảnh báo vượt tải / xe bận / sản xuất chưa sẵn sàng vào <paramref name="warnings"/>.
        /// </summary>
        private async Task<DeliveryRoute> BuildRouteAsync(DateTime date, Vehicle vehicle, string vehicleCode, Guid? driverId, Shift? shift, List<RouteItem> items,
            DateTime? plannedStart, DateTime? plannedEnd, string? name, string? description, List<string> warnings)
        {
            var db = _unitOfWork.DbContext;
            var locationIds = items.Where(s => s.LocationId != null).Select(s => s.LocationId!.Value).Distinct().ToList();
            var locations = await db.CustomerDeliveryLocation.AsNoTracking().Where(l => locationIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id);
            var plannedStops = items.Select(s =>
            {
                var location = s.LocationId is { } lid ? locations.GetValueOrDefault(lid) : null;
                DateTime? arrival = s.Existing?.PlannedDeliveryTime is { } t && t.Date == date && t.TimeOfDay != TimeSpan.Zero ? t
                    : location?.DeliveryWindowFrom is { } from ? date.Add(from) : null;
                return (Item: s, Location: location, Arrival: arrival);
            })
            .OrderBy(s => s.Arrival ?? DateTime.MaxValue).ThenBy(s => s.Location?.Name).ThenBy(s => s.Item.Order.OrderNumber ?? s.Item.Order.OrderNo)
            .ToList();

            decimal load = plannedStops.Sum(s => s.Item.Quantity);
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
            var orderIds = items.Select(s => s.Order.Id).Distinct().ToList();
            List<Guid> shiftIds = shift != null ? [shift.Id] : await DeliveryRules.OrderShiftIdsAsync(db, orderIds, _rules);
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
                DriverId = driverId is { } dr && dr != Guid.Empty ? dr : null,
                PlannedStart = plannedStart ?? firstArrival?.AddMinutes(-_rules.TransportMinutesBeforeCutoff),
                PlannedEnd = plannedEnd,
                Status = DeliveryRouteStatus.Planned,
                Name = DeliveryRules.Clean(name) ?? $"Chuyến {(shift?.Code is { } sc ? sc + " " : string.Empty)}{date:dd/MM} – xe {vehicleCode}",
                Description = DeliveryRules.Clean(description),
            };
            _unitOfWork.DeliveryRouteRepository.Add(route);
            int newCount = plannedStops.Count(s => s.Item.Existing == null);
            string first = newCount == 0 ? string.Empty : await _documentNumberService.NextAsync(DocumentKind.DeliveryOrder, date);
            int seq = 0, created = 0;
            foreach (var s in plannedStops)
            {
                seq++;
                var deliveryOrder = s.Item.Existing;
                if (deliveryOrder == null)
                {
                    string number = ProductionRules.Increment(first, created);
                    created++;
                    deliveryOrder = new DeliveryOrder
                    {
                        Id = Guid.NewGuid(),
                        DeliveryNumber = number,
                        DeliveryNo = number,
                        OrderId = s.Item.Order.Id,
                        CustomerId = s.Item.Order.CustomerId,
                        DeliveryLocationId = s.Item.LocationId,
                        CustomerDeliveryLocationId = s.Item.LocationId,
                        DeliveryDate = date,
                        DeliveryRouteId = route.Id,
                        PlannedDeliveryTime = s.Arrival ?? date,
                        Status = DeliveryOrderStatus.Planned,
                        Note = DeliveryRules.WithMeals("Chuyến " + routeNo, s.Item.Quantity),
                    };
                    _unitOfWork.DeliveryOrderRepository.Add(deliveryOrder);
                }
                else
                {
                    deliveryOrder = _unitOfWork.DeliveryOrderRepository.GetSingle(d => d.Id == deliveryOrder.Id)!;
                    deliveryOrder.DeliveryRouteId = route.Id;
                    deliveryOrder.PlannedDeliveryTime = s.Arrival ?? deliveryOrder.PlannedDeliveryTime;
                    deliveryOrder.Note = DeliveryRules.RouteNote(routeNo, deliveryOrder.Note, s.Item.Quantity);
                    _unitOfWork.DeliveryOrderRepository.Update(deliveryOrder);
                }
                _unitOfWork.DeliveryStopRepository.Add(new DeliveryStop
                {
                    Id = Guid.NewGuid(),
                    DeliveryRouteId = route.Id,
                    DeliveryOrderId = deliveryOrder.Id,
                    CustomerId = s.Item.Order.CustomerId,
                    DeliveryLocationId = s.Item.LocationId,
                    SequenceNo = seq,
                    PlannedArrival = s.Arrival,
                    PlannedMealQty = s.Item.Quantity,
                    StopStatus = DeliveryStopStatus.Planned,
                });
            }
            await _unitOfWork.DeliveryRouteRepository.CommitAsync(ConstantHelper.TypeSave.Update);
            return route;
        }

        /// <summary>T19: đơn hàng chờ xếp chuyến của ngày giao (+ ca).</summary>
        public async Task<IResults<DeliveryReadyOrderModel>> GetReadyOrders(DeliveryDateRequest request)
        {
            if (request.DeliveryDate == default)
            {
                return Results<DeliveryReadyOrderModel>.Fail(ConstantHelper.Message.DeliveryDateRequired);
            }
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
        /// T19 (spec 10.2 POST routes, Pilot P8) + BE2.1: lập chuyến cho 1 ngày / ca / xe trong 1 transaction.
        /// <list type="bullet">
        /// <item>Số chuyến <c>CX-yyMMdd-&lt;mã xe&gt;-NN</c> (T11, hậu tố = Vehicle.Code, trống thì biển số); xe Bảo trì / Ngừng → chặn; tài xế phải là Employee;
        /// BE2.1: tài xế đã có chuyến trùng khung giờ kế hoạch cùng ngày → chặn (khi có đủ giờ bắt đầu / kết thúc).</item>
        /// <item>Đơn: Đang sản xuất, ngày ăn = ngày giao, dòng thuộc ca, còn suất chưa nằm trong đơn giao còn hiệu lực. BE2.1: <c>ListDeliveryOrderId</c> = đơn giao lập tay / tách
        /// (Kế hoạch, chưa gán chuyến). Cả 2 danh sách trống = lấy hết đơn hàng chờ xếp.</item>
        /// <item>Mỗi đơn hàng → 1 đơn giao <c>PH-</c> cho phần suất còn lại + 1 điểm dừng; đơn giao có sẵn → 1 điểm dừng. Giờ dự kiến = giờ giao của đơn giao,
        /// không có thì ngày + DeliveryWindowFrom; thứ tự theo giờ rồi tên điểm. Giờ xuất phát kế hoạch (trống) = giờ sớm nhất − TransportMinutesBeforeCutoff.</item>
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
                var (vehicle, vehicleCode, vehicleError) = await VehicleAsync(request.VehicleId, request.DriverId);
                if (vehicle == null)
                {
                    return Fail(vehicleError!);
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
                string? overlap = await DriverOverlapAsync(request.DriverId, date, request.PlannedStart, request.PlannedEnd, null);
                if (overlap != null)
                {
                    return Fail(overlap);
                }

                var items = new List<RouteItem>();
                var candidates = await CandidatesAsync(date, shift?.Id);
                if (request.ListOrderId.Count == 0 && request.ListDeliveryOrderId.Count == 0)
                {
                    items.AddRange(candidates.Select(c => new RouteItem(c.Order, null, c.Quantity, c.Order.DeliveryLocationId)));
                    if (items.Count == 0)
                    {
                        return Fail(string.Format(ConstantHelper.Message.RouteNoOrders, ProductionRules.Day(date)));
                    }
                }
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
                    items.Add(new RouteItem(hit.Order, null, hit.Quantity, hit.Order.DeliveryLocationId));
                }
                if (request.ListDeliveryOrderId.Count != 0)
                {
                    var ids = request.ListDeliveryOrderId.Distinct().ToList();
                    var unassigned = await UnassignedAsync(date, null, ids);
                    foreach (Guid id in ids)
                    {
                        var hit = unassigned.FirstOrDefault(u => u.DeliveryOrder.Id == id);
                        if (hit.DeliveryOrder == null)
                        {
                            var d = await db.DeliveryOrder.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
                            return Fail(string.Format(ConstantHelper.Message.RouteOrderInvalid, d?.DeliveryNumber ?? id.ToString(),
                                d == null ? "không tồn tại" : d.DeliveryRouteId != null ? "đơn giao đã có chuyến" : "đơn giao ở trạng thái " + DeliveryOrderStatus.Label(d.Status) + " hoặc khác ngày giao"));
                        }
                        items.Add(new RouteItem(hit.Order, hit.DeliveryOrder, hit.Quantity, hit.DeliveryOrder.CustomerDeliveryLocationId ?? hit.DeliveryOrder.DeliveryLocationId));
                    }
                }

                var warnings = new List<string>();
                var route = await BuildRouteAsync(date, vehicle, vehicleCode, request.DriverId, shift, items, request.PlannedStart, request.PlannedEnd, request.Name, request.Description, warnings);
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
        /// BE2.1 (spec 10.2 plan, strategy "byArea"): lập chuyến cho nhiều xe trong 1 transaction.
        /// <list type="number">
        /// <item>Hàng cần xếp = đơn hàng Đang sản xuất của ngày (dòng thuộc ca) còn suất chưa lập đơn giao + đơn giao Kế hoạch chưa gán chuyến.</item>
        /// <item>Gom theo khu vực (<see cref="DeliveryRules.AreaOf"/> – GAP-DLV-09), khu vực lớn trước; mỗi khu vực vào xe còn trống nhiều nhất chứa vừa cả khu vực,
        /// không vừa → chia theo điểm (đơn lớn trước) vào xe còn trống nhiều nhất; không xe nào vừa → xe còn trống nhiều nhất + cảnh báo vượt tải.
        /// Xe không khai tải = không giới hạn. Không tách đơn tự động (tách tay bằng DeliveryOrder/Split). PROPOSED – BA/PO CONFIRM.</item>
        /// <item>Mỗi xe có hàng → 1 chuyến CX- (như Create), điểm dừng theo giờ giao.</item>
        /// </list>
        /// </summary>
        public async Task<IResult<DeliveryRoutePlanModel>> Plan(DeliveryRoutePlanRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                _currentActor.RequireActor();
                var db = _unitOfWork.DbContext;
                DateTime date = request.DeliveryDate.Date;
                if (request.ListVehicle.Count == 0)
                {
                    return Fail(ConstantHelper.Message.RoutePlanNoVehicle);
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
                var vehicles = new List<(Vehicle Vehicle, string Code, Guid? DriverId)>();
                foreach (var v in request.ListVehicle.GroupBy(x => x.VehicleId).Select(g => g.First()))
                {
                    var (vehicle, code, error) = await VehicleAsync(v.VehicleId, v.DriverId);
                    if (vehicle == null)
                    {
                        return Fail(error!);
                    }
                    vehicles.Add((vehicle, code, v.DriverId));
                }

                var items = (await CandidatesAsync(date, shift?.Id)).Select(c => new RouteItem(c.Order, null, c.Quantity, c.Order.DeliveryLocationId)).ToList();
                items.AddRange((await UnassignedAsync(date, shift?.Id)).Select(u => new RouteItem(u.Order, u.DeliveryOrder, u.Quantity,
                    u.DeliveryOrder.CustomerDeliveryLocationId ?? u.DeliveryOrder.DeliveryLocationId)));
                if (items.Count == 0)
                {
                    return Fail(string.Format(ConstantHelper.Message.RoutePlanNothing, ProductionRules.Day(date)));
                }
                var locationIds = items.Where(i => i.LocationId != null).Select(i => i.LocationId!.Value).Distinct().ToList();
                var locations = await db.CustomerDeliveryLocation.AsNoTracking().Where(l => locationIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id);
                var customerIds = items.Select(i => i.Order.CustomerId).Distinct().ToList();
                var customers = await db.Customer.AsNoTracking().Where(c => customerIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id);
                var areas = items
                    .GroupBy(i => DeliveryRules.AreaOf(i.LocationId is { } lid ? locations.GetValueOrDefault(lid) : null, customers.GetValueOrDefault(i.Order.CustomerId)))
                    .Select(g => (Area: g.Key, Items: g.OrderByDescending(i => i.Quantity).ToList(), Total: g.Sum(i => i.Quantity)))
                    .OrderByDescending(a => a.Total).ThenBy(a => a.Area, StringComparer.Ordinal).ToList();

                var warnings = new List<string>();
                var assigned = vehicles.ToDictionary(v => v.Vehicle.Id, _ => new List<RouteItem>());
                decimal Free(Vehicle v) => v.Capacity is > 0 ? v.Capacity.Value - assigned[v.Id].Sum(i => i.Quantity) : decimal.MaxValue;
                var planAreas = new List<DeliveryPlanAreaModel>();
                foreach (var area in areas)
                {
                    var model = new DeliveryPlanAreaModel { Area = area.Area, MealQuantity = area.Total, StopCount = area.Items.Count };
                    planAreas.Add(model);
                    var whole = vehicles.Where(v => Free(v.Vehicle) >= area.Total).OrderByDescending(v => Free(v.Vehicle)).ThenBy(v => v.Code, StringComparer.Ordinal).FirstOrDefault();
                    if (whole.Vehicle != null)
                    {
                        assigned[whole.Vehicle.Id].AddRange(area.Items);
                        model.ListVehicleCode.Add(whole.Code);
                        continue;
                    }
                    foreach (var item in area.Items)
                    {
                        var fit = vehicles.Where(v => Free(v.Vehicle) >= item.Quantity).OrderByDescending(v => Free(v.Vehicle)).ThenBy(v => v.Code, StringComparer.Ordinal).FirstOrDefault();
                        if (fit.Vehicle == null)
                        {
                            fit = vehicles.OrderByDescending(v => Free(v.Vehicle)).ThenBy(v => v.Code, StringComparer.Ordinal).First();
                            warnings.Add(string.Format(ConstantHelper.Message.RoutePlanOverflow,
                                item.Existing?.DeliveryNumber ?? item.Order.OrderNumber ?? item.Order.OrderNo, DeliveryRules.Num(item.Quantity), fit.Code));
                        }
                        assigned[fit.Vehicle.Id].Add(item);
                        if (!model.ListVehicleCode.Contains(fit.Code))
                        {
                            model.ListVehicleCode.Add(fit.Code);
                        }
                    }
                }

                var routeIds = new List<Guid>();
                foreach (var v in vehicles.Where(v => assigned[v.Vehicle.Id].Count != 0))
                {
                    var route = await BuildRouteAsync(date, v.Vehicle, v.Code, v.DriverId, shift, assigned[v.Vehicle.Id], null, null, null,
                        "Lập theo khu vực: " + string.Join(", ", planAreas.Where(a => a.ListVehicleCode.Contains(v.Code)).Select(a => a.Area)), warnings);
                    routeIds.Add(route.Id);
                }
                _unitOfWork.CommitTransaction();
                var routes = await db.DeliveryRoute.AsNoTracking().Where(r => routeIds.Contains(r.Id)).OrderBy(r => r.RouteNo).ToListAsync();
                return Result<DeliveryRoutePlanModel>.Success(new DeliveryRoutePlanModel
                {
                    DeliveryDate = date,
                    ListRoute = await DeliveryRules.RouteDetailsAsync(db, routes, _rules, withBatches: false),
                    ListArea = planAreas,
                    ListWarning = warnings.Distinct().ToList(),
                }, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Plan: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                _unitOfWork.DbContext.ChangeTracker.Clear();
                throw;
            }

            IResult<DeliveryRoutePlanModel> Fail(string message)
            {
                _unitOfWork.RollbackTransaction();
                _unitOfWork.DbContext.ChangeTracker.Clear();
                return Result<DeliveryRoutePlanModel>.Fail(message);
            }
        }

        /// <summary>
        /// BE2.1 (spec 10.2 PUT routes/{id}/stops): đổi thứ tự / giờ dự kiến điểm dừng khi chuyến Kế hoạch. Số thứ tự ≥ 1, không trùng (điểm không gửi giữ số cũ);
        /// lưu 2 bước qua số âm tạm vì UNIQUE (DeliveryRouteId, SequenceNo) – <see cref="DeliveryRules.ResequenceAsync"/>.
        /// </summary>
        public async Task<IResult<DeliveryRouteDetailModel>> UpdateStops(DeliveryRouteStopsRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                _currentActor.RequireActor();
                var db = _unitOfWork.DbContext;
                var route = _unitOfWork.DeliveryRouteRepository.GetSingle(r => r.Id == request.Id && !r.IsDeleted);
                if (route == null)
                {
                    return Fail(ConstantHelper.Message.RouteNotFound);
                }
                if (route.Status != DeliveryRouteStatus.Planned)
                {
                    return Fail(InvalidStatus(route, "đổi thứ tự điểm dừng", DeliveryRouteStatus.Planned));
                }
                var stops = _unitOfWork.DeliveryStopRepository.GetAll().Where(s => s.DeliveryRouteId == route.Id && !s.IsDeleted).ToList();
                var numbers = stops.ToDictionary(s => s.Id, s => s.SequenceNo);
                foreach (var item in request.ListStop)
                {
                    var stop = stops.FirstOrDefault(s => s.DeliveryOrderId == item.DeliveryOrderId);
                    if (stop == null)
                    {
                        var d = await db.DeliveryOrder.AsNoTracking().FirstOrDefaultAsync(x => x.Id == item.DeliveryOrderId);
                        return Fail(string.Format(ConstantHelper.Message.StopNotOnRoute, d?.DeliveryNumber ?? item.DeliveryOrderId.ToString()));
                    }
                    if (item.SequenceNo < 1)
                    {
                        return Fail(ConstantHelper.Message.StopSequenceInvalid);
                    }
                    numbers[stop.Id] = item.SequenceNo;
                    if (item.PlannedArrival != null)
                    {
                        stop.PlannedArrival = item.PlannedArrival;
                    }
                }
                if (numbers.Values.Distinct().Count() != numbers.Count)
                {
                    return Fail(ConstantHelper.Message.StopSequenceInvalid);
                }
                var ordered = stops.OrderBy(s => numbers[s.Id]).ToList();
                await DeliveryRules.ResequenceAsync(_unitOfWork, route.Id, ordered, null, numbers);
                _unitOfWork.CommitTransaction();
                return Result<DeliveryRouteDetailModel>.Success((await DeliveryRules.RouteDetailAsync(db, route.Id, _rules))!, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " UpdateStops: ", ex.ToString()));
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
        /// BE2.1 (spec 10.2 load): xếp hàng lên xe – chuyến Kế hoạch / Đang xếp → Đang xếp. Mỗi đơn giao Kế hoạch của chuyến (hoặc trong <c>ListItem</c>):
        /// DeliveryLoading (LoadedQty = số gửi, trống = suất kế hoạch; LoadingTime / LoadingDate = bây giờ; VehicleCode = mã xe; Status Loaded; nhân viên xếp)
        /// + DeliveryExecution LOADED (tài xế, mã xe); sự kiện LOADED. Xếp lại đơn giao đã xếp → cập nhật số. Xếp thiếu / thừa so với kế hoạch → cảnh báo (PROPOSED).
        /// Chưa liên kết PackingOrder (spec GAP-C-03).
        /// </summary>
        public async Task<IResult<DeliveryLoadModel>> Load(DeliveryRouteLoadRequest request)
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
                    return Fail(InvalidStatus(route, "xếp hàng lên xe", DeliveryRouteStatus.Planned, DeliveryRouteStatus.Loading));
                }
                if (request.EmployeeId is { } emp && emp != Guid.Empty && !await db.Employee.AsNoTracking().AnyAsync(e => e.Id == emp && !e.IsDeleted))
                {
                    return Fail(ConstantHelper.Message.DriverNotFound);
                }
                var stops = await db.DeliveryStop.AsNoTracking().Where(s => s.DeliveryRouteId == route.Id && !s.IsDeleted && s.DeliveryOrderId != null).ToListAsync();
                var stopOrderIds = stops.Select(s => s.DeliveryOrderId!.Value).ToList();
                var deliveryOrders = _unitOfWork.DeliveryOrderRepository.GetAll()
                    .Where(d => stopOrderIds.Contains(d.Id) && !d.IsDeleted && d.Status == DeliveryOrderStatus.Planned).ToList();
                var wanted = request.ListItem.GroupBy(i => i.DeliveryOrderId).ToDictionary(g => g.Key, g => g.Last().LoadedQty);
                foreach (var id in wanted.Keys.Where(id => deliveryOrders.All(d => d.Id != id)))
                {
                    var d = await db.DeliveryOrder.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
                    return Fail(string.Format(ConstantHelper.Message.StopNotOnRoute, d?.DeliveryNumber ?? id.ToString()));
                }
                // Danh sách trống = các đơn giao chưa xếp (giữ số đã xếp của đơn giao đã xếp); có danh sách = xếp / xếp lại đúng các đơn giao đó.
                var deliveryOrderIds = deliveryOrders.Select(d => d.Id).ToList();
                var alreadyLoaded = await db.DeliveryLoading.AsNoTracking()
                    .Where(l => !l.IsDeleted && deliveryOrderIds.Contains(l.DeliveryOrderId) && l.Status == DeliveryLoadingStatus.Loaded).Select(l => l.DeliveryOrderId).ToListAsync();
                var targets = wanted.Count == 0 ? deliveryOrders.Where(d => !alreadyLoaded.Contains(d.Id)).ToList() : deliveryOrders.Where(d => wanted.ContainsKey(d.Id)).ToList();
                if (targets.Count == 0)
                {
                    return Fail(string.Format(ConstantHelper.Message.RouteLoadNothing, DeliveryRules.RouteNumber(route)));
                }
                var vehicle = route.VehicleId is { } vid ? await db.Vehicle.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vid) : null;
                string? vehicleCode = vehicle?.Code ?? vehicle?.LicensePlate;
                string? driverName = route.DriverId is { } did ? await db.Employee.AsNoTracking().Where(e => e.Id == did).Select(e => e.FullName).FirstOrDefaultAsync() : null;
                var warnings = new List<string>();
                DateTime now = _clock.Now;
                var loadings = new List<DeliveryLoading>();
                foreach (var deliveryOrder in targets.OrderBy(d => stops.First(s => s.DeliveryOrderId == d.Id).SequenceNo))
                {
                    var stop = stops.First(s => s.DeliveryOrderId == deliveryOrder.Id);
                    decimal planned = stop.PlannedMealQty ?? DeliveryRules.TaggedMeals(deliveryOrder.Note) ?? 0m;
                    decimal qty = wanted.GetValueOrDefault(deliveryOrder.Id) ?? planned;
                    string number = deliveryOrder.DeliveryNumber ?? deliveryOrder.DeliveryNo ?? deliveryOrder.Id.ToString("N")[..8];
                    if (qty <= 0)
                    {
                        return Fail(string.Format(ConstantHelper.Message.LoadQuantityInvalid, number));
                    }
                    if (qty != planned)
                    {
                        warnings.Add(string.Format(ConstantHelper.Message.LoadShortWarning, number, DeliveryRules.Num(qty), DeliveryRules.Num(planned)));
                    }
                    var loading = _unitOfWork.DeliveryLoadingRepository.GetAll()
                        .FirstOrDefault(l => l.DeliveryOrderId == deliveryOrder.Id && !l.IsDeleted && l.Status == DeliveryLoadingStatus.Loaded);
                    if (loading == null)
                    {
                        loading = new DeliveryLoading { Id = Guid.NewGuid(), DeliveryRouteId = route.Id, DeliveryOrderId = deliveryOrder.Id, Status = DeliveryLoadingStatus.Loaded };
                        _unitOfWork.DeliveryLoadingRepository.Add(loading);
                    }
                    else
                    {
                        _unitOfWork.DeliveryLoadingRepository.Update(loading);
                    }
                    loading.LoadedQty = qty;
                    loading.LoadingTime = now;
                    loading.LoadingDate = now;
                    loading.VehicleCode = vehicleCode;
                    loading.EmployeeId = request.EmployeeId is { } e2 && e2 != Guid.Empty ? e2 : loading.EmployeeId;
                    loading.Note = DeliveryRules.Clean(request.Note) ?? loading.Note;
                    loadings.Add(loading);
                    DeliveryRules.SyncExecution(_unitOfWork, deliveryOrder, route.Id, DeliveryExecutionStatus.Loaded, now, null, driverName, vehicleCode);
                }
                route.Status = DeliveryRouteStatus.Loading;
                _unitOfWork.DeliveryRouteRepository.Update(route);
                DeliveryRules.AddEvent(_unitOfWork, actor, now, route.Id, null, null, DeliveryCode.EventLoaded,
                    $"Xếp hàng lên xe {vehicleCode}: {loadings.Count} đơn giao, {DeliveryRules.Num(loadings.Sum(l => l.LoadedQty ?? 0m))} suất");
                await _unitOfWork.DeliveryRouteRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                var detail = (await DeliveryRules.RouteDetailAsync(db, route.Id, _rules))!;
                detail.ListWarning.InsertRange(0, warnings);
                var loadedIds = loadings.Select(l => l.DeliveryOrderId).ToList();
                return Result<DeliveryLoadModel>.Success(new DeliveryLoadModel
                {
                    Route = detail,
                    ListLoading = await db.DeliveryLoading.AsNoTracking().Where(l => l.DeliveryRouteId == route.Id && !l.IsDeleted && l.Status == DeliveryLoadingStatus.Loaded).ToListAsync(),
                    ListExecution = await db.DeliveryExecution.AsNoTracking().Where(x => x.DeliveryRouteId == route.Id && !x.IsDeleted).ToListAsync(),
                }, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Load: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                _unitOfWork.DbContext.ChangeTracker.Clear();
                throw;
            }

            IResult<DeliveryLoadModel> Fail(string message)
            {
                _unitOfWork.RollbackTransaction();
                _unitOfWork.DbContext.ChangeTracker.Clear();
                return Result<DeliveryLoadModel>.Fail(message);
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
                if (TemperatureTime.Resolve(request.MeasuredAt, _clock, out DateTime measuredAt) is string timeError)
                {
                    return Fail(timeError); // Rà soát BE: giờ đo ngoài khoảng → không ghi (chống "che" lần đo thật).
                }
                var warnings = new List<string>();
                string? error = DeliveryRules.AddTemperature(_unitOfWork, _rules, actor, measuredAt, route.Id, stopRef, deliveryOrderId,
                    request.TemperatureC, request.CorrectiveAction, request.DeviceCode, request.EvidenceUrl, warnings, out bool within);
                if (error != null)
                {
                    return Fail(error);
                }
                await _unitOfWork.DeliveryEventRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
                if (!within)
                {
                    await DeliveryRules.NotifyTempLowAsync(_notifications, db, route, stopRef, request.TemperatureC, _rules, request.CorrectiveAction, measuredAt, actor.UserId);
                }
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
        /// <summary>
        /// BOT-3.2 (D-72): đổi xe và / hoặc tài xế của chuyến chưa xuất phát (Kế hoạch / Đang xếp). Xe phải đang dùng được; xe đã có chuyến khác chưa xong trong ngày
        /// → cảnh báo (giống lập chuyến). Ghi DeliveryEvent REASSIGNED (lịch sử chuyến) + đồng bộ tài xế / xe ở thực hiện giao.
        /// </summary>
        public async Task<IResult<DeliveryRouteDetailModel>> Reassign(DeliveryRouteReassignRequest request)
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
                    return Fail(InvalidStatus(route, "đổi xe / tài xế", DeliveryRouteStatus.Planned, DeliveryRouteStatus.Loading));
                }
                bool newVehicle = request.VehicleId is { } nv && nv != Guid.Empty && nv != route.VehicleId;
                bool newDriver = request.DriverId is { } nd && nd != Guid.Empty && nd != route.DriverId;
                if (!newVehicle && !newDriver)
                {
                    return Fail(ConstantHelper.Message.RouteReassignNothing);
                }
                var warnings = new List<string>();
                var changes = new List<string>();
                if (newVehicle)
                {
                    var oldVehicle = route.VehicleId is { } ov ? await db.Vehicle.AsNoTracking().FirstOrDefaultAsync(v => v.Id == ov) : null;
                    var vehicle = await db.Vehicle.AsNoTracking().FirstOrDefaultAsync(v => v.Id == request.VehicleId && !v.IsDeleted);
                    if (vehicle == null)
                    {
                        return Fail(ConstantHelper.Message.VehicleNotFound);
                    }
                    string code = vehicle.LicensePlate ?? vehicle.Code ?? string.Empty;
                    if (string.Equals(vehicle.Status, "Inactive", StringComparison.OrdinalIgnoreCase))
                    {
                        return Fail(string.Format(ConstantHelper.Message.VehicleInactive, code));
                    }
                    DateTime date = route.DeliveryDate.Date, next = date.AddDays(1);
                    var busy = await db.DeliveryRoute.AsNoTracking()
                        .Where(r => !r.IsDeleted && r.Id != route.Id && r.VehicleId == vehicle.Id && r.DeliveryDate >= date && r.DeliveryDate < next && r.Status != DeliveryRouteStatus.Completed)
                        .Select(r => r.RouteNo).ToListAsync();
                    if (busy.Count != 0)
                    {
                        warnings.Add(string.Format(ConstantHelper.Message.RouteVehicleBusy, code, string.Join(", ", busy)));
                    }
                    changes.Add($"xe {oldVehicle?.LicensePlate ?? oldVehicle?.Code ?? "–"} → {code}");
                    route.VehicleId = vehicle.Id;
                }
                if (newDriver)
                {
                    var driver = await db.Employee.AsNoTracking().FirstOrDefaultAsync(e => e.Id == request.DriverId && !e.IsDeleted);
                    if (driver == null || !EmployeeStatus.IsWorking(driver.Status))
                    {
                        return Fail(ConstantHelper.Message.DriverNotFound);
                    }
                    string? oldName = route.DriverId is { } od ? await db.Employee.AsNoTracking().Where(e => e.Id == od).Select(e => e.FullName).FirstOrDefaultAsync() : null;
                    changes.Add($"tài xế {oldName ?? "–"} → {driver.FullName}");
                    route.DriverId = driver.Id;
                }
                DateTime now = _clock.Now;
                _unitOfWork.DeliveryRouteRepository.Update(route);
                DeliveryRules.AddEvent(_unitOfWork, actor, now, route.Id, null, null, DeliveryCode.EventReassigned,
                    "Đổi " + string.Join("; ", changes) + (string.IsNullOrWhiteSpace(request.Note) ? string.Empty : " – " + request.Note.Trim()), null, null);
                await _unitOfWork.DeliveryRouteRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                var detail = await DeliveryRules.RouteDetailAsync(db, route.Id, _rules);
                detail!.ListWarning.InsertRange(0, warnings);
                return Result<DeliveryRouteDetailModel>.Success(detail, string.Format(ConstantHelper.Message.RouteReassignDone,
                    newVehicle && newDriver ? "xe và tài xế" : newVehicle ? "xe" : "tài xế", DeliveryRules.RouteNumber(route)));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Reassign: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }

            IResult<DeliveryRouteDetailModel> Fail(string message)
            {
                _unitOfWork.RollbackTransaction();
                _unitOfWork.DbContext.ChangeTracker.Clear();
                return Result<DeliveryRouteDetailModel>.Fail(message);
            }
        }

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
                        temperature, request.CorrectiveAction, request.DeviceCode, null, warnings, out bool within);
                    if (error != null)
                    {
                        return Fail(error);
                    }
                    await _unitOfWork.DeliveryEventRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
                    if (!within)
                    {
                        await DeliveryRules.NotifyTempLowAsync(_notifications, db, route, null, temperature, _rules, request.CorrectiveAction, now, actor.UserId);
                    }
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
                var vehicle = route.VehicleId is { } vid ? await db.Vehicle.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vid) : null;
                string? driverName = route.DriverId is { } did ? await db.Employee.AsNoTracking().Where(e => e.Id == did).Select(e => e.FullName).FirstOrDefaultAsync() : null;
                foreach (var deliveryOrder in _unitOfWork.DeliveryOrderRepository.GetAll().Where(d => d.DeliveryRouteId == route.Id && !d.IsDeleted && d.Status == DeliveryOrderStatus.Planned).ToList())
                {
                    deliveryOrder.Status = DeliveryOrderStatus.InTransit;
                    _unitOfWork.DeliveryOrderRepository.Update(deliveryOrder);
                    // BE2.1 (spec 10.2 dispatch): thực hiện giao LOADED → DISPATCHED (DispatchedAt, DepartedAt).
                    DeliveryRules.SyncExecution(_unitOfWork, deliveryOrder, route.Id, DeliveryExecutionStatus.Dispatched, now, null, driverName, vehicle?.Code ?? vehicle?.LicensePlate);
                }
                DeliveryRules.AddEvent(_unitOfWork, actor, now, route.Id, null, null, DeliveryCode.EventDispatched,
                    "Xuất phát chuyến " + DeliveryRules.RouteNumber(route) + (string.IsNullOrWhiteSpace(request.Note) ? string.Empty : " – " + request.Note.Trim()),
                    request.Latitude, request.Longitude);
                await _unitOfWork.DeliveryRouteRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                // BOT-1.7 (Catalog 181): TRIP_DEPARTED → điểm phát suất (quyền Thêm trên đối soát – DECISIONS p32).
                await BusinessNotifier.ToMenusAsync(_notifications, db, NotificationCatalog.TripDeparted, ["#/crm-reconciliation"],
                    new { title = "Chuyến " + DeliveryRules.RouteNumber(route), detail = $"Xe {vehicle?.LicensePlate ?? vehicle?.Code} · tài xế {driverName} · xuất phát {now:HH:mm}" },
                    $"{NotificationCatalog.TripDeparted}:{route.Id:N}",
                    buttons: [new NotificationButton("🚚 Xem chuyến", BusinessNotifier.Callback("dlv", "r", route.Id))], except: actor.UserId, requireAdd: true);
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
            if (request.DeliveryDate == default)
            {
                return Result<DeliveryBoardModel>.Fail(ConstantHelper.Message.DeliveryDateRequired);
            }
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

        /// <summary>
        /// BOT-1.5b (bot Vận hành – tài xế "🚚 Chuyến của tôi"): chuyến của ngày giao (mặc định hôm nay) có DriverId = nhân viên của tài khoản đang gọi
        /// (HrRules.ResolveEmployeeAsync – như ShiftAssignment/GetMine). Không cần quyền màn chuyến xe để XEM chuyến của mình; thao tác ghi vẫn qua action có quyền.
        /// </summary>
        public async Task<IResult<DeliveryBoardModel>> GetMine(DateTime? deliveryDate)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                var employee = await HrRules.ResolveEmployeeAsync(db, _currentActor.RequireActor().UserId);
                if (employee == null)
                {
                    return Result<DeliveryBoardModel>.Fail(ConstantHelper.Message.HrUserNoEmployee);
                }
                DateTime date = (deliveryDate ?? _clock.Now).Date, next = date.AddDays(1);
                var routes = await db.DeliveryRoute.AsNoTracking()
                    .Where(r => !r.IsDeleted && r.DriverId == employee.Id && r.DeliveryDate >= date && r.DeliveryDate < next)
                    .OrderBy(r => r.PlannedStart).ThenBy(r => r.RouteNo).ToListAsync();
                var list = await DeliveryRules.RouteDetailsAsync(db, routes, _rules, withBatches: false);
                // BOT-2.4: điểm bếp có tọa độ (như chấm công GPS) – RadiusMeters = 0 → bot dùng bán kính mặc định của mình.
                var kitchens = await db.KitchenWorkstation.AsNoTracking()
                    .Where(w => !w.IsDeleted && w.Latitude != null && w.Longitude != null)
                    .Select(w => new DeliveryGeoPointModel { Code = w.Code, Latitude = w.Latitude!.Value, Longitude = w.Longitude!.Value, RadiusMeters = w.RadiusMeters ?? 0m })
                    .ToListAsync();
                return Result<DeliveryBoardModel>.Success(new DeliveryBoardModel
                {
                    DeliveryDate = date,
                    ListRoute = list,
                    StatusCounts = list.GroupBy(r => r.Route.Status ?? string.Empty).ToDictionary(g => g.Key, g => g.Count()),
                    ListKitchen = kitchens,
                }, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetMine: ", ex.ToString()));
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
