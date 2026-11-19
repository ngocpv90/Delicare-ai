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
    /// T19: đơn giao sinh khi lập chuyến. BE2.1 (spec 10.2): đơn giao lập tay / tách / gán – chuyển chuyến / hủy; 1 đơn hàng có thể có nhiều đơn giao.
    /// </summary>
    public class DeliveryOrderService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService, IDocumentNumberService documentNumberService, ICurrentActor currentActor
        , IOptions<BusinessRulesOptions> rules) : IDeliveryOrderService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;
        private readonly IDocumentNumberService _documentNumberService = documentNumberService;
        private readonly ICurrentActor _currentActor = currentActor;
        private readonly BusinessRulesOptions _rules = rules.Value;

        public async Task<IResults<DeliveryOrderViewModel>> GetAll(DeliveryOrderSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@OrderId", request.OrderId == Guid.Empty ? null : request.OrderId);
                dynamicParameters.Add("@CustomerDeliveryLocationId", request.CustomerDeliveryLocationId);
                dynamicParameters.Add("@DeliveryRouteId", request.DeliveryRouteId);
                dynamicParameters.Add("@DeliveryNumber", request.DeliveryNumber);
                //dynamicParameters.Add("@PlannedDeliveryTime", request.PlannedDeliveryTime);
                //dynamicParameters.Add("@ActualDeliveryTime", request.ActualDeliveryTime);
                dynamicParameters.Add("@Status", request.Status);
                //dynamicParameters.Add("@Note", request.Note);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<DeliveryOrderViewModel>("uspDeliveryOrder_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<DeliveryOrderViewModel>
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

        public IResults<DeliveryOrder> GetAllData()
        {
            try
            {
                var list = _unitOfWork.DeliveryOrderRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<DeliveryOrder>
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

        public async Task<IResult<bool>> Save(DeliveryOrderRequest request, string typeSave)
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

        /// <summary>T19 / BE2.1: đơn giao chỉ sinh qua nghiệp vụ (DeliveryRoute/Create / Plan, DeliveryOrder/Create / Split – số PH-, số suất) – CRUD Insert không ghi.</summary>
        private Task<string> Insert(DeliveryOrderRequest request, string typeSave)
            => Task.FromResult(string.Format(ConstantHelper.Message.DeliveryCrudBlocked, "Đơn giao", "DeliveryOrder/Create, DeliveryRoute/Create"));

        /// <summary>T19: CRUD Update chỉ đổi ghi chú; trạng thái / số / chuyến / giờ giao đổi qua DeliveryRoute / DeliveryStop.</summary>
        private async Task<string> Update(DeliveryOrderRequest request, string typeSave)
        {
            var exist = _unitOfWork.DeliveryOrderRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (request.Id == null || exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // BE2.1: giữ tag số suất [SUAT:n] (GAP-DLV-08) khi sửa ghi chú.
            decimal? meals = DeliveryRules.TaggedMeals(exist.Note);
            string? note = string.IsNullOrWhiteSpace(request.Note) ? exist.Note : request.Note.Trim();
            exist.Note = meals is { } m ? DeliveryRules.WithMeals(DeliveryRules.NoteText(note), m) : note;
            _unitOfWork.DeliveryOrderRepository.Update(exist);
            await _unitOfWork.DeliveryOrderRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        /// <summary>T19: không xóa đơn giao lẻ – xóa cả chuyến Kế hoạch (DeliveryRoute/Delete) hoặc báo thất bại (DeliveryStop/Fail).</summary>
        private Task<string> Delete(Guid? id, string typeSave)
            => Task.FromResult(string.Format(ConstantHelper.Message.DeliveryCrudBlocked, "Đơn giao", "DeliveryOrder/Cancel, DeliveryRoute/Delete (chuyến Kế hoạch) hoặc DeliveryStop/Fail"));

        // ============================================================ BE2.1 (spec 10.2 – đơn giao thủ công): lập / tách / gán – chuyển chuyến / hủy
        // 1 đơn hàng có thể có nhiều đơn giao (BRD). DeliveryOrder không có cột số suất / dòng đơn giao → số suất ở tag [SUAT:n] trong Note,
        // đã gán chuyến thì DeliveryStop.PlannedMealQty (GAP-DLV-08, PROPOSED – BA/PO CONFIRM). Σ suất các đơn giao còn hiệu lực ≤ tổng suất đơn hàng.

        private static readonly string[] EligibleOrderStatus = [OrderStatus.Locked, OrderStatus.InProduction];

        private static string Number(DeliveryOrder d) => d.DeliveryNumber ?? d.DeliveryNo ?? d.Id.ToString("N")[..8];

        private static string OrderNumber(Order o) => o.OrderNumber ?? o.OrderNo ?? o.Id.ToString("N")[..8];

        /// <summary>Dòng hiển thị của các đơn giao (số suất, đơn, khách, điểm giao, khu vực, chuyến, điểm dừng, đã xếp lên xe, thao tác được phép).</summary>
        private async Task<List<DeliveryOrderItemModel>> ItemsAsync(List<DeliveryOrder> list)
        {
            var db = _unitOfWork.DbContext;
            var ids = list.Select(d => d.Id).ToList();
            var meals = await DeliveryRules.DeliveryOrderMealsAsync(db, list);
            var orderIds = list.Select(d => d.OrderId).Distinct().ToList();
            var orders = await db.Order.AsNoTracking().Where(o => orderIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id);
            var customerIds = list.Select(d => d.CustomerId).Distinct().ToList();
            var customers = await db.Customer.AsNoTracking().Where(c => customerIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id);
            var locationIds = list.Select(d => d.CustomerDeliveryLocationId ?? d.DeliveryLocationId).Where(x => x != null).Select(x => x!.Value).Distinct().ToList();
            var locations = await db.CustomerDeliveryLocation.AsNoTracking().Where(l => locationIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id);
            var routeIds = list.Where(d => d.DeliveryRouteId != null).Select(d => d.DeliveryRouteId!.Value).Distinct().ToList();
            var routes = await db.DeliveryRoute.AsNoTracking().Where(r => routeIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id);
            var stops = await db.DeliveryStop.AsNoTracking().Where(s => !s.IsDeleted && s.DeliveryOrderId != null && ids.Contains(s.DeliveryOrderId.Value)).ToListAsync();
            var loadings = await db.DeliveryLoading.AsNoTracking().Where(l => !l.IsDeleted && ids.Contains(l.DeliveryOrderId) && l.Status == DeliveryLoadingStatus.Loaded).ToListAsync();
            return [.. list.Select(d =>
            {
                var order = orders.GetValueOrDefault(d.OrderId);
                var customer = customers.GetValueOrDefault(d.CustomerId);
                var location = (d.CustomerDeliveryLocationId ?? d.DeliveryLocationId) is { } lid ? locations.GetValueOrDefault(lid) : null;
                var stop = stops.FirstOrDefault(s => s.DeliveryOrderId == d.Id);
                var route = stop != null ? routes.GetValueOrDefault(stop.DeliveryRouteId) : null;
                var loading = loadings.Where(l => l.DeliveryOrderId == d.Id).OrderByDescending(l => l.LoadingTime).FirstOrDefault();
                bool open = d.Status == DeliveryOrderStatus.Planned && loading == null;
                return new DeliveryOrderItemModel
                {
                    DeliveryOrder = d,
                    StatusName = DeliveryOrderStatus.Label(d.Status),
                    MealQuantity = meals[d.Id],
                    OrderNumber = order == null ? null : OrderNumber(order),
                    OrderStatus = order?.Status,
                    CustomerName = customer?.DisplayName ?? customer?.Name,
                    LocationName = location?.Name,
                    Address = location?.Address,
                    Area = DeliveryRules.AreaOf(location, customer),
                    DeliveryRouteId = route?.Id,
                    RouteNo = route == null ? null : DeliveryRules.RouteNumber(route),
                    RouteStatus = route?.Status,
                    DeliveryStopId = stop?.Id,
                    SequenceNo = stop?.SequenceNo,
                    LoadedQty = loading?.LoadedQty,
                    IsLoaded = loading != null,
                    NoteText = DeliveryRules.NoteText(d.Note),
                    AllowedActions = !open ? [] : ["split", "assignRoute", "cancel"],
                };
            }).OrderBy(x => x.DeliveryOrder.DeliveryDate).ThenBy(x => x.RouteNo ?? "~").ThenBy(x => x.SequenceNo).ThenBy(x => x.DeliveryOrder.DeliveryNumber, StringComparer.Ordinal)];
        }

        private async Task<DeliveryOrderActionModel> ActionModelAsync(IEnumerable<Guid> deliveryOrderIds, IEnumerable<Guid?> routeIds, List<string> warnings)
        {
            var db = _unitOfWork.DbContext;
            var ids = deliveryOrderIds.Distinct().ToList();
            var list = await db.DeliveryOrder.AsNoTracking().Where(d => ids.Contains(d.Id)).ToListAsync();
            var rids = routeIds.Where(r => r != null).Select(r => r!.Value).Distinct().ToList();
            var routes = await db.DeliveryRoute.AsNoTracking().Where(r => rids.Contains(r.Id) && !r.IsDeleted).OrderBy(r => r.RouteNo).ToListAsync();
            return new DeliveryOrderActionModel
            {
                ListDeliveryOrder = await ItemsAsync(list),
                ListRoute = await DeliveryRules.RouteDetailsAsync(db, routes, _rules, withBatches: false),
                ListWarning = warnings,
            };
        }

        /// <summary>BE2.1 (spec 10.2 GET delivery-orders): đơn giao theo ngày / đơn / khách / chuyến / trạng thái; <c>Unassigned</c> = chưa gán chuyến (bỏ đơn giao Hủy khi không lọc trạng thái).</summary>
        public async Task<IResults<DeliveryOrderItemModel>> GetList(DeliveryOrderListRequest request)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                var query = db.DeliveryOrder.AsNoTracking().Where(d => !d.IsDeleted);
                if (request.DeliveryDate is { } day)
                {
                    DateTime date = day.Date, next = date.AddDays(1);
                    query = query.Where(d => d.DeliveryDate >= date && d.DeliveryDate < next);
                }
                if (request.OrderId is { } oid && oid != Guid.Empty)
                {
                    query = query.Where(d => d.OrderId == oid);
                }
                if (request.CustomerId is { } cid && cid != Guid.Empty)
                {
                    query = query.Where(d => d.CustomerId == cid);
                }
                if (request.DeliveryRouteId is { } rid && rid != Guid.Empty)
                {
                    query = query.Where(d => d.DeliveryRouteId == rid);
                }
                query = string.IsNullOrWhiteSpace(request.Status) ? query.Where(d => d.Status != DeliveryOrderStatus.Cancelled) : query.Where(d => d.Status == request.Status);
                if (request.Unassigned == true)
                {
                    query = query.Where(d => d.DeliveryRouteId == null);
                }
                var list = await ItemsAsync(await query.ToListAsync());
                return Results<DeliveryOrderItemModel>.Success(list, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetList: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>Điểm giao hợp lệ của đơn hàng (thuộc khách của đơn); trống = điểm giao của đơn.</summary>
        private async Task<(CustomerDeliveryLocation? Location, string? Error)> LocationAsync(Order order, Guid? locationId)
        {
            Guid? id = locationId is { } l && l != Guid.Empty ? l : order.DeliveryLocationId;
            if (id == null)
            {
                return (null, null);
            }
            var location = await _unitOfWork.DbContext.CustomerDeliveryLocation.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            return location == null || location.CustomerId != order.CustomerId ? (null, ConstantHelper.Message.DeliveryOrderLocationInvalid) : (location, null);
        }

        private static DateTime PlannedTime(DateTime date, DateTime? requested, CustomerDeliveryLocation? location)
            => requested ?? (location?.DeliveryWindowFrom is { } from ? date.Add(from) : date);

        /// <summary>
        /// BE2.1 (spec 10.2 POST delivery-orders): lập đơn giao tay. Đơn hàng Đã chốt / Đang sản xuất; điểm giao thuộc khách của đơn; số suất = Σ dòng đơn
        /// (mỗi dòng 0 &lt; số ≤ số suất của dòng) / MealQuantity / trống = toàn bộ suất còn lại; không vượt suất còn lại (tổng suất − Σ đơn giao còn hiệu lực).
        /// Số <c>PH-</c> theo ngày giao (T11, GAP-DOC-01), CustomerId từ đơn, Kế hoạch, chưa gán chuyến.
        /// </summary>
        public async Task<IResult<DeliveryOrderActionModel>> Create(DeliveryOrderCreateRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                _currentActor.RequireActor();
                var db = _unitOfWork.DbContext;
                var order = await db.Order.AsNoTracking().FirstOrDefaultAsync(o => o.Id == request.OrderId && !o.IsDeleted);
                if (order == null)
                {
                    return Fail(ConstantHelper.Message.ReconcileOrderNotFound);
                }
                if (!EligibleOrderStatus.Contains(order.Status))
                {
                    return Fail(string.Format(ConstantHelper.Message.PackingOrderNotEligible, OrderNumber(order), OrderStatus.Label(order.Status)));
                }
                var (location, locationError) = await LocationAsync(order, request.CustomerDeliveryLocationId);
                if (locationError != null)
                {
                    return Fail(locationError);
                }
                decimal quantity;
                var remaining = (await DeliveryRules.RemainingMealsAsync(db, [order]))[order.Id];
                if (request.ListLine.Count != 0)
                {
                    var lines = await db.OrderLine.AsNoTracking().Where(l => l.OrderId == order.Id && !l.IsDeleted).ToDictionaryAsync(l => l.Id);
                    foreach (var line in request.ListLine)
                    {
                        if (!lines.TryGetValue(line.OrderLineId, out var orderLine) || line.Quantity <= 0 || line.Quantity > orderLine.Quantity)
                        {
                            return Fail(string.Format(ConstantHelper.Message.DeliveryOrderLineInvalid, line.OrderLineId, DeliveryRules.Num(orderLine?.Quantity ?? 0m)));
                        }
                    }
                    quantity = request.ListLine.Sum(l => l.Quantity);
                }
                else
                {
                    quantity = request.MealQuantity ?? remaining;
                }
                if (quantity <= 0)
                {
                    return Fail(remaining <= 0
                        ? string.Format(ConstantHelper.Message.DeliveryOrderOverQuantity, OrderNumber(order), DeliveryRules.Num(remaining), DeliveryRules.Num(quantity))
                        : ConstantHelper.Message.DeliveryOrderQuantityRequired);
                }
                if (quantity > remaining)
                {
                    return Fail(string.Format(ConstantHelper.Message.DeliveryOrderOverQuantity, OrderNumber(order), DeliveryRules.Num(remaining), DeliveryRules.Num(quantity)));
                }
                DateTime date = order.MealDate.Date;
                string number = await _documentNumberService.NextAsync(DocumentKind.DeliveryOrder, date);
                var deliveryOrder = new DeliveryOrder
                {
                    Id = Guid.NewGuid(),
                    DeliveryNumber = number,
                    DeliveryNo = number,
                    OrderId = order.Id,
                    CustomerId = order.CustomerId,
                    DeliveryLocationId = location?.Id,
                    CustomerDeliveryLocationId = location?.Id,
                    DeliveryDate = date,
                    PlannedDeliveryTime = PlannedTime(date, request.PlannedDeliveryTime, location),
                    Status = DeliveryOrderStatus.Planned,
                    Note = DeliveryRules.WithMeals(DeliveryRules.Clean(request.Note) ?? "Lập tay", quantity),
                };
                _unitOfWork.DeliveryOrderRepository.Add(deliveryOrder);
                await _unitOfWork.DeliveryOrderRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
                _unitOfWork.CommitTransaction();
                var warnings = new List<string>();
                if (quantity < remaining)
                {
                    warnings.Add($"Đơn {OrderNumber(order)} còn {DeliveryRules.Num(remaining - quantity)} suất chưa lập đơn giao");
                }
                return Result<DeliveryOrderActionModel>.Success(await ActionModelAsync([deliveryOrder.Id], [], warnings), ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Create: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                _unitOfWork.DbContext.ChangeTracker.Clear();
                throw;
            }

            IResult<DeliveryOrderActionModel> Fail(string message)
            {
                _unitOfWork.RollbackTransaction();
                _unitOfWork.DbContext.ChangeTracker.Clear();
                return Result<DeliveryOrderActionModel>.Fail(message);
            }
        }

        /// <summary>Đơn giao Kế hoạch, chưa xếp lên xe (đang theo dõi) – hoặc lỗi.</summary>
        private async Task<(DeliveryOrder? DeliveryOrder, string? Error)> OpenAsync(Guid id, string action)
        {
            var deliveryOrder = _unitOfWork.DeliveryOrderRepository.GetSingle(d => d.Id == id && !d.IsDeleted);
            if (deliveryOrder == null)
            {
                return (null, ConstantHelper.Message.DeliveryOrderNotFound);
            }
            if (deliveryOrder.Status != DeliveryOrderStatus.Planned)
            {
                return (null, string.Format(ConstantHelper.Message.StatusActionInvalid, action, "đơn giao " + Number(deliveryOrder),
                    DeliveryOrderStatus.Label(DeliveryOrderStatus.Planned), DeliveryOrderStatus.Label(deliveryOrder.Status)));
            }
            if (await DeliveryRules.IsLoadedAsync(_unitOfWork.DbContext, deliveryOrder.Id))
            {
                return (null, string.Format(ConstantHelper.Message.DeliveryOrderLoaded, Number(deliveryOrder), action));
            }
            return (deliveryOrder, null);
        }

        /// <summary>Gỡ điểm dừng của đơn giao khỏi chuyến: đánh số lại các điểm còn lại (giải phóng số – UNIQUE gồm cả dòng đã xóa mềm) rồi xóa mềm điểm.</summary>
        private async Task RemoveStopAsync(DeliveryStop stop)
        {
            var others = _unitOfWork.DeliveryStopRepository.GetAll()
                .Where(s => s.DeliveryRouteId == stop.DeliveryRouteId && !s.IsDeleted && s.Id != stop.Id).OrderBy(s => s.SequenceNo).ToList();
            await DeliveryRules.ResequenceAsync(_unitOfWork, stop.DeliveryRouteId, others, [stop]);
            _unitOfWork.DeliveryStopRepository.Update(stop);
            await _unitOfWork.DeliveryStopRepository.CommitAsync(ConstantHelper.TypeSave.Delete);
        }

        /// <summary>
        /// BE2.1 (spec 10.2 split): tách đơn giao Kế hoạch chưa xếp lên xe thành ≥ 2 phần (mỗi phần &gt; 0, Σ = số suất gốc). Phần đầu giữ đơn gốc (cập nhật số suất,
        /// điểm giao, giờ giao – và điểm dừng nếu đã gán chuyến); các phần sau là đơn giao mới <c>PH-</c> Kế hoạch, chưa gán chuyến. Điểm giao phải thuộc khách.
        /// </summary>
        public async Task<IResult<DeliveryOrderActionModel>> Split(DeliveryOrderSplitRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                _currentActor.RequireActor();
                var db = _unitOfWork.DbContext;
                var (deliveryOrder, error) = await OpenAsync(request.Id, "tách đơn giao");
                if (deliveryOrder == null)
                {
                    return Fail(error!);
                }
                if (request.ListPart.Count < 2 || request.ListPart.Any(p => p.MealQuantity <= 0))
                {
                    return Fail(ConstantHelper.Message.SplitPartsInvalid);
                }
                decimal meals = (await DeliveryRules.DeliveryOrderMealsAsync(db, [deliveryOrder]))[deliveryOrder.Id];
                decimal sum = request.ListPart.Sum(p => p.MealQuantity);
                if (sum != meals)
                {
                    return Fail(string.Format(ConstantHelper.Message.SplitSumMismatch, DeliveryRules.Num(sum), DeliveryRules.Num(meals)));
                }
                var order = await db.Order.AsNoTracking().FirstAsync(o => o.Id == deliveryOrder.OrderId);
                var locations = new List<CustomerDeliveryLocation?>();
                foreach (var part in request.ListPart)
                {
                    var (location, locationError) = await LocationAsync(order, part.CustomerDeliveryLocationId ?? deliveryOrder.CustomerDeliveryLocationId ?? deliveryOrder.DeliveryLocationId);
                    if (locationError != null)
                    {
                        return Fail(locationError);
                    }
                    locations.Add(location);
                }
                DateTime date = deliveryOrder.DeliveryDate.Date;
                var first = request.ListPart[0];
                deliveryOrder.CustomerDeliveryLocationId = locations[0]?.Id ?? deliveryOrder.CustomerDeliveryLocationId;
                deliveryOrder.DeliveryLocationId = deliveryOrder.CustomerDeliveryLocationId;
                deliveryOrder.PlannedDeliveryTime = first.PlannedDeliveryTime ?? deliveryOrder.PlannedDeliveryTime;
                deliveryOrder.Note = DeliveryRules.WithMeals(DeliveryRules.NoteText(deliveryOrder.Note), first.MealQuantity);
                _unitOfWork.DeliveryOrderRepository.Update(deliveryOrder);
                var stop = _unitOfWork.DeliveryStopRepository.GetSingle(s => s.DeliveryOrderId == deliveryOrder.Id && !s.IsDeleted);
                if (stop != null)
                {
                    stop.PlannedMealQty = first.MealQuantity;
                    stop.DeliveryLocationId = deliveryOrder.CustomerDeliveryLocationId;
                    stop.PlannedArrival = first.PlannedDeliveryTime ?? stop.PlannedArrival;
                    _unitOfWork.DeliveryStopRepository.Update(stop);
                }
                string firstNumber = await _documentNumberService.NextAsync(DocumentKind.DeliveryOrder, date);
                var ids = new List<Guid> { deliveryOrder.Id };
                for (int i = 1; i < request.ListPart.Count; i++)
                {
                    var part = request.ListPart[i];
                    string number = ProductionRules.Increment(firstNumber, i - 1);
                    var created = new DeliveryOrder
                    {
                        Id = Guid.NewGuid(),
                        DeliveryNumber = number,
                        DeliveryNo = number,
                        OrderId = deliveryOrder.OrderId,
                        CustomerId = deliveryOrder.CustomerId,
                        DeliveryLocationId = locations[i]?.Id,
                        CustomerDeliveryLocationId = locations[i]?.Id,
                        DeliveryDate = date,
                        PlannedDeliveryTime = PlannedTime(date, part.PlannedDeliveryTime, locations[i]),
                        Status = DeliveryOrderStatus.Planned,
                        Note = DeliveryRules.WithMeals("Tách từ " + Number(deliveryOrder), part.MealQuantity),
                    };
                    _unitOfWork.DeliveryOrderRepository.Add(created);
                    ids.Add(created.Id);
                }
                await _unitOfWork.DeliveryOrderRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                return Result<DeliveryOrderActionModel>.Success(await ActionModelAsync(ids, [stop?.DeliveryRouteId], []), ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Split: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                _unitOfWork.DbContext.ChangeTracker.Clear();
                throw;
            }

            IResult<DeliveryOrderActionModel> Fail(string message)
            {
                _unitOfWork.RollbackTransaction();
                _unitOfWork.DbContext.ChangeTracker.Clear();
                return Result<DeliveryOrderActionModel>.Fail(message);
            }
        }

        /// <summary>
        /// BE2.1 (spec 10.2 PUT delivery-orders/{id}/route): gán / chuyển đơn giao Kế hoạch chưa xếp lên xe vào chuyến Kế hoạch cùng ngày, ở vị trí <c>SequenceNo</c>
        /// (trống / lớn hơn số điểm = cuối chuyến). Đơn hàng phải Đã chốt / Đang sản xuất. Chuyển chuyến: chuyến cũ phải còn Kế hoạch, điểm cũ bị gỡ (đánh số lại).
        /// Vượt tải xe → cảnh báo (không chặn, giống lập chuyến – PROPOSED).
        /// </summary>
        public async Task<IResult<DeliveryOrderActionModel>> AssignRoute(DeliveryOrderAssignRouteRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                _currentActor.RequireActor();
                var db = _unitOfWork.DbContext;
                var (deliveryOrder, error) = await OpenAsync(request.Id, "gán / chuyển chuyến");
                if (deliveryOrder == null)
                {
                    return Fail(error!);
                }
                var target = _unitOfWork.DeliveryRouteRepository.GetSingle(r => r.Id == request.DeliveryRouteId && !r.IsDeleted);
                if (target == null)
                {
                    return Fail(ConstantHelper.Message.RouteNotFound);
                }
                if (target.Status != DeliveryRouteStatus.Planned)
                {
                    return Fail(string.Format(ConstantHelper.Message.RouteTargetInvalid, DeliveryRules.RouteNumber(target), DeliveryRouteStatus.Label(target.Status)));
                }
                if (target.DeliveryDate.Date != deliveryOrder.DeliveryDate.Date)
                {
                    return Fail(string.Format(ConstantHelper.Message.RouteDateMismatch, ProductionRules.Day(deliveryOrder.DeliveryDate), ProductionRules.Day(target.DeliveryDate)));
                }
                var order = await db.Order.AsNoTracking().FirstAsync(o => o.Id == deliveryOrder.OrderId);
                if (!EligibleOrderStatus.Contains(order.Status))
                {
                    return Fail(string.Format(ConstantHelper.Message.PackingOrderNotEligible, OrderNumber(order), OrderStatus.Label(order.Status)));
                }
                decimal meals = (await DeliveryRules.DeliveryOrderMealsAsync(db, [deliveryOrder]))[deliveryOrder.Id];
                var oldStop = _unitOfWork.DeliveryStopRepository.GetSingle(s => s.DeliveryOrderId == deliveryOrder.Id && !s.IsDeleted);
                Guid? sourceRouteId = oldStop?.DeliveryRouteId;
                if (oldStop != null && oldStop.DeliveryRouteId != target.Id)
                {
                    var source = _unitOfWork.DeliveryRouteRepository.GetSingle(r => r.Id == oldStop.DeliveryRouteId && !r.IsDeleted);
                    if (source != null && source.Status != DeliveryRouteStatus.Planned)
                    {
                        return Fail(string.Format(ConstantHelper.Message.RouteTargetInvalid, DeliveryRules.RouteNumber(source), DeliveryRouteStatus.Label(source.Status)));
                    }
                    await RemoveStopAsync(oldStop);
                    oldStop = null;
                }
                var targetStops = _unitOfWork.DeliveryStopRepository.GetAll()
                    .Where(s => s.DeliveryRouteId == target.Id && !s.IsDeleted && (oldStop == null || s.Id != oldStop.Id)).OrderBy(s => s.SequenceNo).ToList();
                int position = Math.Clamp((request.SequenceNo ?? int.MaxValue) - 1, 0, targetStops.Count);
                var warnings = new List<string>();
                if (oldStop == null)
                {
                    // Chừa chỗ ở vị trí mới: đánh số các điểm cũ 1..n bỏ qua (position + 1), rồi thêm điểm mới.
                    var numbers = targetStops.Select((s, i) => (s.Id, No: i < position ? i + 1 : i + 2)).ToDictionary(x => x.Id, x => x.No);
                    await DeliveryRules.ResequenceAsync(_unitOfWork, target.Id, targetStops, null, numbers);
                    var location = (deliveryOrder.CustomerDeliveryLocationId ?? deliveryOrder.DeliveryLocationId) is { } lid
                        ? await db.CustomerDeliveryLocation.AsNoTracking().FirstOrDefaultAsync(l => l.Id == lid) : null;
                    DateTime date = target.DeliveryDate.Date;
                    _unitOfWork.DeliveryStopRepository.Add(new DeliveryStop
                    {
                        Id = Guid.NewGuid(),
                        DeliveryRouteId = target.Id,
                        DeliveryOrderId = deliveryOrder.Id,
                        CustomerId = deliveryOrder.CustomerId,
                        DeliveryLocationId = deliveryOrder.CustomerDeliveryLocationId ?? deliveryOrder.DeliveryLocationId,
                        SequenceNo = position + 1,
                        PlannedArrival = deliveryOrder.PlannedDeliveryTime.TimeOfDay != TimeSpan.Zero ? deliveryOrder.PlannedDeliveryTime
                            : location?.DeliveryWindowFrom is { } from ? date.Add(from) : null,
                        PlannedMealQty = meals,
                        StopStatus = DeliveryStopStatus.Planned,
                    });
                }
                else
                {
                    // Cùng chuyến: chỉ đổi vị trí.
                    targetStops.Insert(position, oldStop);
                    await DeliveryRules.ResequenceAsync(_unitOfWork, target.Id, targetStops);
                }
                deliveryOrder.DeliveryRouteId = target.Id;
                deliveryOrder.Note = DeliveryRules.RouteNote(DeliveryRules.RouteNumber(target), deliveryOrder.Note, meals);
                _unitOfWork.DeliveryOrderRepository.Update(deliveryOrder);
                await _unitOfWork.DeliveryOrderRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                var vehicle = target.VehicleId is { } vid ? await db.Vehicle.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vid) : null;
                decimal load = await db.DeliveryStop.Where(s => s.DeliveryRouteId == target.Id && !s.IsDeleted).SumAsync(s => s.PlannedMealQty ?? 0m);
                if (vehicle?.Capacity is > 0 && load > vehicle.Capacity.Value)
                {
                    warnings.Add(string.Format(ConstantHelper.Message.RouteOverCapacity, DeliveryRules.Num(load), vehicle.Code ?? vehicle.LicensePlate, DeliveryRules.Num(vehicle.Capacity.Value)));
                }
                _unitOfWork.CommitTransaction();
                return Result<DeliveryOrderActionModel>.Success(await ActionModelAsync([deliveryOrder.Id], [target.Id, sourceRouteId], warnings), ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " AssignRoute: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                _unitOfWork.DbContext.ChangeTracker.Clear();
                throw;
            }

            IResult<DeliveryOrderActionModel> Fail(string message)
            {
                _unitOfWork.RollbackTransaction();
                _unitOfWork.DbContext.ChangeTracker.Clear();
                return Result<DeliveryOrderActionModel>.Fail(message);
            }
        }

        /// <summary>
        /// BE2.1 (spec 10.2 cancel): hủy đơn giao Kế hoạch chưa xếp lên xe (lý do bắt buộc) – Hủy, gỡ điểm dừng khỏi chuyến (Kế hoạch / Đang xếp), suất trả lại đơn hàng
        /// (lập đơn giao / xếp chuyến lại được). Không xóa đơn giao (giữ lịch sử).
        /// </summary>
        public async Task<IResult<DeliveryOrderActionModel>> Cancel(DeliveryOrderCancelRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                _currentActor.RequireActor();
                if (string.IsNullOrWhiteSpace(request.Reason))
                {
                    return Fail(ConstantHelper.Message.DeliveryOrderReasonRequired);
                }
                var (deliveryOrder, error) = await OpenAsync(request.Id, "hủy đơn giao");
                if (deliveryOrder == null)
                {
                    return Fail(error!);
                }
                var stop = _unitOfWork.DeliveryStopRepository.GetSingle(s => s.DeliveryOrderId == deliveryOrder.Id && !s.IsDeleted);
                Guid? routeId = stop?.DeliveryRouteId;
                if (stop != null)
                {
                    var route = _unitOfWork.DeliveryRouteRepository.GetSingle(r => r.Id == stop.DeliveryRouteId && !r.IsDeleted);
                    if (route != null && route.Status is not (DeliveryRouteStatus.Planned or DeliveryRouteStatus.Loading))
                    {
                        return Fail(string.Format(ConstantHelper.Message.StatusActionInvalid, "hủy đơn giao", "chuyến " + DeliveryRules.RouteNumber(route),
                            DeliveryRouteStatus.Label(DeliveryRouteStatus.Planned) + " / " + DeliveryRouteStatus.Label(DeliveryRouteStatus.Loading), DeliveryRouteStatus.Label(route.Status)));
                    }
                }
                deliveryOrder.Status = DeliveryOrderStatus.Cancelled;
                string reason = request.Reason.Trim();
                deliveryOrder.Note = string.Join(" – ", new[] { deliveryOrder.Note, "Hủy: " + reason }.Where(x => !string.IsNullOrEmpty(x)));
                if (deliveryOrder.Note.Length > 4000)
                {
                    deliveryOrder.Note = deliveryOrder.Note[..4000];
                }
                _unitOfWork.DeliveryOrderRepository.Update(deliveryOrder);
                await _unitOfWork.DeliveryOrderRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                if (stop != null)
                {
                    await RemoveStopAsync(stop);
                }
                _unitOfWork.CommitTransaction();
                return Result<DeliveryOrderActionModel>.Success(await ActionModelAsync([deliveryOrder.Id], [routeId], []), ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Cancel: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                _unitOfWork.DbContext.ChangeTracker.Clear();
                throw;
            }

            IResult<DeliveryOrderActionModel> Fail(string message)
            {
                _unitOfWork.RollbackTransaction();
                _unitOfWork.DbContext.ChangeTracker.Clear();
                return Result<DeliveryOrderActionModel>.Fail(message);
            }
        }

        /// <summary>
        /// BE3.1 (Catalog 192, spec 10.3 delivery-orders/{id}/events): timeline sự kiện của 1 đơn giao theo <c>EventAt</c> – sự kiện gắn đơn,
        /// sự kiện tại điểm dừng của đơn, và sự kiện cấp chuyến (xếp hàng, xuất phát, nhiệt độ xuất phát, vị trí xe) của chuyến chứa đơn (PROPOSED D-57 l).
        /// </summary>
        public async Task<IResults<DeliveryEventItemModel>> GetEvents(Guid id)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                var order = await db.DeliveryOrder.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted);
                if (order == null)
                {
                    return new Results<DeliveryEventItemModel> { ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest), ResponseMessage = ConstantHelper.Message.DeliveryOrderNotFound, ListData = [] };
                }
                var stopIds = await db.DeliveryStop.AsNoTracking().Where(s => !s.IsDeleted && s.DeliveryOrderId == id).Select(s => s.Id).ToListAsync();
                Guid? routeId = order.DeliveryRouteId;
                var events = await db.DeliveryEvent.AsNoTracking()
                    .Where(e => !e.IsDeleted && (e.DeliveryOrderId == id
                        || (e.DeliveryStopId != null && stopIds.Contains(e.DeliveryStopId.Value))
                        || (routeId != null && e.DeliveryRouteId == routeId && e.DeliveryStopId == null && e.DeliveryOrderId == null)))
                    .OrderBy(e => e.EventAt).ThenBy(e => e.CreatedDate)
                    .ToListAsync();
                var items = await ChartRules.DeliveryEventItemsAsync(db, events);
                return new Results<DeliveryEventItemModel>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.GetAllSuccessfully,
                    ListData = items,
                    TotalRecords = items.Count,
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetEvents: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<DeliveryOrder>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.DeliveryOrderRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<DeliveryOrder>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<DeliveryOrder>
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
