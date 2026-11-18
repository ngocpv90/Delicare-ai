#nullable enable
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
    /// T19 (Pilot P8) – điểm dừng của chuyến Đang giao: Arrive (Kế hoạch → Đã đến), Deliver (bàn giao + POD, nhiệt độ bàn giao ≥ 60 °C → Đã giao),
    /// Fail (→ Thất bại). Mọi đơn giao của đơn hàng Đã giao → đơn hàng Đã giao; mọi điểm xong → chuyến Hoàn thành.
    /// CRUD: chỉ sửa ghi chú / thứ tự / giờ dự kiến khi chuyến + điểm còn Kế hoạch (điểm dừng sinh từ DeliveryRoute/Create).
    /// </summary>
    public class DeliveryStopService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper, IReferenceGuard referenceGuard
        , IDocumentNumberService documentNumberService, IClock clock, ICurrentActor currentActor, IOptions<BusinessRulesOptions> rules, INotificationService notifications) : IDeliveryStopService
    {
        private readonly INotificationService _notifications = notifications; // BOT-1.7d
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IReferenceGuard _referenceGuard = referenceGuard;
        private readonly IDocumentNumberService _documentNumberService = documentNumberService;
        private readonly IClock _clock = clock;
        private readonly ICurrentActor _currentActor = currentActor;
        private readonly BusinessRulesOptions _rules = rules.Value;

        public async Task<IResults<DeliveryStopViewModel>> GetAll(DeliveryStopSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@DeliveryRouteId", request.DeliveryRouteId);
                dynamicParameters.Add("@DeliveryOrderId", request.DeliveryOrderId);
                dynamicParameters.Add("@CustomerId", request.CustomerId);
                dynamicParameters.Add("@DeliveryLocationId", request.DeliveryLocationId);
                dynamicParameters.Add("@StopStatus", request.StopStatus);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<DeliveryStopViewModel>("uspDeliveryStop_GetAll", dynamicParameters)).ToList();
                int i = 0;
                list.ForEach(x =>
                {
                    i++;
                    x.STT = (request.Page - 1) * request.PageSize + i;
                });
                return new Results<DeliveryStopViewModel>
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

        public IResults<DeliveryStop> GetAllData()
        {
            try
            {
                var list = _unitOfWork.DeliveryStopRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<DeliveryStop>
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

        public async Task<IResult<bool>> Save(DeliveryStopRequest request, string typeSave)
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

        /// <summary>T19: điểm dừng chỉ sinh khi lập chuyến (DeliveryRoute/Create) – CRUD Insert không ghi.</summary>
        private Task<string> Insert(DeliveryStopRequest request, string typeSave)
            => Task.FromResult(string.Format(ConstantHelper.Message.DeliveryCrudBlocked, "Điểm dừng", "DeliveryRoute/Create"));

        /// <summary>T19: CRUD Update chỉ đổi thứ tự / giờ dự kiến / ghi chú khi chuyến và điểm còn Kế hoạch; trạng thái, số suất qua Arrive / Deliver / Fail.</summary>
        private async Task<string> Update(DeliveryStopRequest request, string typeSave)
        {
            var exist = _unitOfWork.DeliveryStopRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (request.Id == null || exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            var route = _unitOfWork.DeliveryRouteRepository.GetSingle(r => r.Id == exist.DeliveryRouteId && !r.IsDeleted);
            if (route == null || route.Status != DeliveryRouteStatus.Planned || exist.StopStatus != DeliveryStopStatus.Planned)
            {
                return string.Format(ConstantHelper.Message.StatusActionInvalid, "sửa điểm dừng", "chuyến", DeliveryRouteStatus.Label(DeliveryRouteStatus.Planned),
                    DeliveryRouteStatus.Label(route?.Status));
            }
            if (request.SequenceNo is { } sequence && sequence != exist.SequenceNo)
            {
                if (_unitOfWork.DeliveryStopRepository.GetSingle(x => x.Id != exist.Id && x.DeliveryRouteId == exist.DeliveryRouteId && x.SequenceNo == sequence && !x.IsDeleted) != null)
                {
                    return string.Format(ConstantHelper.Message.StopSequenceExists, sequence);
                }
                exist.SequenceNo = sequence;
            }
            exist.PlannedArrival = request.PlannedArrival ?? exist.PlannedArrival;
            exist.Note = DeliveryRules.Clean(request.Note);
            _unitOfWork.DeliveryStopRepository.Update(exist);
            await _unitOfWork.DeliveryStopRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        /// <summary>T19: không xóa điểm dừng lẻ – xóa cả chuyến Kế hoạch (DeliveryRoute/Delete).</summary>
        private Task<string> Delete(Guid? id, string typeSave)
            => Task.FromResult(string.Format(ConstantHelper.Message.DeliveryCrudBlocked, "Điểm dừng", "DeliveryRoute/Delete (chuyến Kế hoạch)"));

        /// <summary>Kết quả của thân thao tác: lỗi nghiệp vụ (null = thành công) + có lưu phần đã ghi (lần đo nhiệt độ) dù lỗi.</summary>
        private sealed class StopContext
        {
            public List<string> Warnings { get; } = [];
            public bool KeepOnError { get; set; }
        }

        /// <summary>
        /// Khung chung thao tác điểm dừng (1 transaction): nạp điểm + chuyến (phải Đang giao) + đơn giao, kiểm trạng thái điểm, chạy thân, lưu, commit, trả chi tiết chuyến.
        /// Thân báo lỗi mà <c>KeepOnError</c> (đã ghi lần đo nhiệt độ dưới ngưỡng kèm khắc phục) → vẫn commit lần đo, trả lỗi nghiệp vụ kèm chi tiết.
        /// </summary>
        private async Task<IResult<DeliveryRouteDetailModel>> RunAsync(Guid stopId, string action, string[] allowed,
            Func<DeliveryStop, DeliveryRoute, DeliveryOrder?, ActorInfo, StopContext, Task<string?>> body)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var actor = _currentActor.RequireActor();
                var stop = _unitOfWork.DeliveryStopRepository.GetSingle(s => s.Id == stopId && !s.IsDeleted);
                if (stop == null)
                {
                    return Fail(ConstantHelper.Message.StopNotFound);
                }
                var route = _unitOfWork.DeliveryRouteRepository.GetSingle(r => r.Id == stop.DeliveryRouteId && !r.IsDeleted);
                if (route == null)
                {
                    return Fail(ConstantHelper.Message.RouteNotFound);
                }
                if (route.Status != DeliveryRouteStatus.InTransit)
                {
                    return Fail(string.Format(ConstantHelper.Message.StatusActionInvalid, action, "chuyến " + DeliveryRules.RouteNumber(route),
                        DeliveryRouteStatus.Label(DeliveryRouteStatus.InTransit), DeliveryRouteStatus.Label(route.Status)));
                }
                if (!allowed.Contains(stop.StopStatus))
                {
                    return Fail(string.Format(ConstantHelper.Message.StatusActionInvalid, action, "điểm dừng #" + stop.SequenceNo,
                        string.Join(" / ", allowed.Select(DeliveryStopStatus.Label)), DeliveryStopStatus.Label(stop.StopStatus)));
                }
                var deliveryOrder = stop.DeliveryOrderId is { } doid ? _unitOfWork.DeliveryOrderRepository.GetSingle(d => d.Id == doid && !d.IsDeleted) : null;
                var ctx = new StopContext();
                string? error = await body(stop, route, deliveryOrder, actor, ctx);
                var db = _unitOfWork.DbContext;
                if (error != null)
                {
                    if (!ctx.KeepOnError)
                    {
                        return Fail(error);
                    }
                    _unitOfWork.CommitTransaction();
                    return Result<DeliveryRouteDetailModel>.Fail("Đã lưu lần đo nhiệt độ. " + error, await DeliveryRules.RouteDetailAsync(db, route.Id, _rules));
                }
                _unitOfWork.DeliveryStopRepository.Update(stop);
                if (await CompleteRouteIfDoneAsync(route, stop))
                {
                    ctx.Warnings.Add("Chuyến " + DeliveryRules.RouteNumber(route) + " đã hoàn thành");
                }
                await _unitOfWork.DeliveryStopRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                var detail = await DeliveryRules.RouteDetailAsync(db, route.Id, _rules);
                detail!.ListWarning.InsertRange(0, ctx.Warnings);
                return Result<DeliveryRouteDetailModel>.Success(detail, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ", action, ": ", ex.ToString()));
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

        /// <summary>Mọi điểm dừng Đã giao / Thất bại → chuyến Hoàn thành (ActualEnd).</summary>
        private async Task<bool> CompleteRouteIfDoneAsync(DeliveryRoute route, DeliveryStop current)
        {
            var others = await _unitOfWork.DbContext.DeliveryStop.AsNoTracking()
                .Where(s => s.DeliveryRouteId == route.Id && !s.IsDeleted && s.Id != current.Id).Select(s => s.StopStatus).ToListAsync();
            if (!DeliveryStopStatus.Closed.Contains(current.StopStatus) || !others.All(s => DeliveryStopStatus.Closed.Contains(s)))
            {
                return false;
            }
            route.Status = DeliveryRouteStatus.Completed;
            route.ActualEnd = _clock.Now;
            _unitOfWork.DeliveryRouteRepository.Update(route);
            return true;
        }

        /// <summary>Ghi lần đo tại điểm (nếu có) và lưu ngay trong transaction; lỗi = thiếu hành động khắc phục.</summary>
        private async Task<string?> RecordAsync(DeliveryStop stop, DeliveryRoute route, ActorInfo actor, StopContext ctx, decimal? temperature, string? corrective, string? deviceCode, string? evidenceUrl)
        {
            if (temperature is not { } t)
            {
                return null;
            }
            DateTime at = _clock.Now;
            string? error = DeliveryRules.AddTemperature(_unitOfWork, _rules, actor, at, route.Id, stop.Id, stop.DeliveryOrderId,
                t, corrective, deviceCode, evidenceUrl, ctx.Warnings, out bool within);
            if (error != null)
            {
                return error;
            }
            await _unitOfWork.DeliveryEventRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
            if (!within)
            {
                await DeliveryRules.NotifyTempLowAsync(_notifications, _unitOfWork.DbContext, route, stop.Id, t, _rules, corrective, at, actor.UserId);
            }
            ctx.KeepOnError = true;
            return null;
        }

        /// <summary>T19 (spec 10.3 arrive): Kế hoạch → Đã đến, ActualArrival; sự kiện ARRIVED (tọa độ, ảnh); có nhiệt độ thì ghi lần đo tại điểm.</summary>
        public Task<IResult<DeliveryRouteDetailModel>> Arrive(DeliveryStopArriveRequest request)
            => RunAsync(request.Id, "đến điểm", [DeliveryStopStatus.Planned], async (stop, route, deliveryOrder, actor, ctx) =>
            {
                string? error = await RecordAsync(stop, route, actor, ctx, request.TemperatureC, request.CorrectiveAction, null, request.PhotoUrl);
                if (error != null)
                {
                    return error;
                }
                ctx.KeepOnError = false;
                DateTime now = _clock.Now;
                stop.StopStatus = DeliveryStopStatus.Arrived;
                stop.ActualArrival = now;
                if (deliveryOrder != null)
                {
                    // BE2.1 (spec 10.3 arrive): thực hiện giao DISPATCHED → ARRIVED.
                    DeliveryRules.SyncExecution(_unitOfWork, deliveryOrder, route.Id, DeliveryExecutionStatus.Arrived, now);
                }
                if (!string.IsNullOrWhiteSpace(request.Note))
                {
                    stop.Note = request.Note.Trim();
                }
                DeliveryRules.AddEvent(_unitOfWork, actor, now, route.Id, stop.Id, stop.DeliveryOrderId, DeliveryCode.EventArrived,
                    "Đến điểm #" + stop.SequenceNo, request.Latitude, request.Longitude, request.PhotoUrl);
                return null;
            });

        /// <summary>
        /// T19 (spec 10.3 pod, BUSINESS_RULES §7, §8b): bàn giao tại điểm. Nhiệt độ bàn giao gần nhất tại điểm (gửi kèm hoặc đo trước) phải ≥ 60 °C – dưới ngưỡng / chưa đo → chặn.
        /// Ghi ProofOfDeliverie (giao / nhận / từ chối = giao − nhận, người nhận, giờ nhận, đường dẫn chữ ký / ảnh); điểm → Đã giao (DeliveredMealQty, ShortageQty = kế hoạch − giao,
        /// âm = thừa); đơn giao → Đã giao (ActualDeliveryTime); sự kiện DELIVERED. Mọi đơn giao còn hiệu lực của đơn hàng Đã giao → đơn hàng Đã giao.
        /// Có suất bị từ chối → tự tạo phản ánh PA- (POD / SHORTAGE, <c>DeliveryAutoComplaintOnRejected</c>, PROPOSED).
        /// </summary>
        public Task<IResult<DeliveryRouteDetailModel>> Deliver(DeliveryStopDeliverRequest request)
            => RunAsync(request.Id, "bàn giao", [DeliveryStopStatus.Planned, DeliveryStopStatus.Arrived], async (stop, route, deliveryOrder, actor, ctx) =>
            {
                decimal delivered = request.DeliveredQuantity;
                decimal accepted = request.AcceptedQuantity ?? delivered;
                if (delivered <= 0 || accepted < 0 || accepted > delivered)
                {
                    return ConstantHelper.Message.PodQuantityInvalid;
                }
                if (request.PackagingQuantity is < 0)
                {
                    return ConstantHelper.Message.PackagingQuantityInvalid;
                }
                if (string.IsNullOrWhiteSpace(request.ReceiverName))
                {
                    return ConstantHelper.Message.PodReceiverRequired;
                }
                if (deliveryOrder == null)
                {
                    return ConstantHelper.Message.RecordNotFoundMessage;
                }
                string? error = await RecordAsync(stop, route, actor, ctx, request.TemperatureC, request.CorrectiveAction, request.DeviceCode, null);
                if (error != null)
                {
                    return error;
                }
                var db = _unitOfWork.DbContext;
                var temp = await DeliveryRules.LatestTemperatureAsync(db, route.Id, stop.Id);
                string stopName = "#" + stop.SequenceNo + " (" + (deliveryOrder.DeliveryNumber ?? deliveryOrder.DeliveryNo) + ")";
                if (temp == null)
                {
                    return string.Format(ConstantHelper.Message.RouteCannotDeliver, stopName, string.Format(ConstantHelper.Message.HandoverTempMissing, DeliveryRules.Num(_rules.DeliveryTempMinC)));
                }
                if (temp.TemperatureC < _rules.DeliveryTempMinC)
                {
                    return string.Format(ConstantHelper.Message.RouteCannotDeliver, stopName,
                        string.Format(ConstantHelper.Message.HandoverTempBelow, DeliveryRules.Num(temp.TemperatureC!.Value), DeliveryRules.Num(_rules.DeliveryTempMinC)));
                }
                ctx.KeepOnError = false;
                DateTime now = _clock.Now;
                decimal rejected = delivered - accepted;
                var pod = new ProofOfDelivery
                {
                    Id = Guid.NewGuid(),
                    DeliveryOrderId = deliveryOrder.Id,
                    DeliveredQuantity = delivered,
                    DeliveredQty = delivered,
                    AcceptedQuantity = accepted,
                    AcceptedQty = accepted,
                    RejectedQuantity = rejected,
                    RejectedQty = rejected,
                    ReceiverName = request.ReceiverName.Trim(),
                    ReceiverPhone = DeliveryRules.Clean(request.ReceiverPhone),
                    ReceivedAt = request.ReceivedAt ?? now,
                    ConfirmedAt = now,
                    SignatureUrl = DeliveryRules.Clean(request.SignatureUrl),
                    PhotoUrl = DeliveryRules.Clean(request.PhotoUrl),
                    Remark = DeliveryRules.Clean(request.Remark),
                };
                _unitOfWork.ProofOfDeliveryRepository.Add(pod);
                stop.StopStatus = DeliveryStopStatus.Delivered;
                stop.ActualArrival ??= now;
                stop.DeliveredMealQty = delivered;
                stop.ShortageQty = (stop.PlannedMealQty ?? delivered) - delivered;
                deliveryOrder.Status = DeliveryOrderStatus.Delivered;
                deliveryOrder.ActualDeliveryTime = now;
                _unitOfWork.DeliveryOrderRepository.Update(deliveryOrder);
                DeliveryRules.SyncExecution(_unitOfWork, deliveryOrder, route.Id, DeliveryExecutionStatus.Delivered, now);
                DeliveryRules.AddEvent(_unitOfWork, actor, now, route.Id, stop.Id, deliveryOrder.Id, DeliveryCode.EventDelivered,
                    $"Bàn giao {DeliveryRules.Num(delivered)} suất, nhận {DeliveryRules.Num(accepted)}" + (rejected > 0 ? $", từ chối {DeliveryRules.Num(rejected)}" : string.Empty)
                    + " – người nhận " + pod.ReceiverName, null, null, request.PhotoUrl);
                if (stop.ShortageQty is > 0)
                {
                    ctx.Warnings.Add($"Điểm {stopName} giao thiếu {DeliveryRules.Num(stop.ShortageQty.Value)} suất so với kế hoạch");
                }

                // Đơn hàng → Đã giao khi mọi đơn giao còn hiệu lực (không tính Thất bại) đã giao.
                var order = _unitOfWork.OrderRepository.GetSingle(o => o.Id == deliveryOrder.OrderId && !o.IsDeleted);
                if (order != null && order.Status == OrderStatus.InProduction)
                {
                    bool othersOpen = await db.DeliveryOrder.AsNoTracking().AnyAsync(d => d.OrderId == order.Id && !d.IsDeleted && d.Id != deliveryOrder.Id
                        && d.Status != DeliveryOrderStatus.Delivered && d.Status != DeliveryOrderStatus.Failed && d.Status != DeliveryOrderStatus.Cancelled);
                    // BE2.1: đơn hàng có nhiều đơn giao (lập tay / tách) – chỉ Đã giao khi mọi suất đã nằm trong đơn giao (không còn phần chưa lập đơn giao).
                    bool unallocated = (await DeliveryRules.RemainingMealsAsync(db, [order], deliveryOrder.Id))[order.Id] - (stop.PlannedMealQty ?? DeliveryRules.TaggedMeals(deliveryOrder.Note) ?? 0m) > 0;
                    if (!othersOpen && !unallocated)
                    {
                        order.Status = OrderStatus.Delivered;
                        _unitOfWork.OrderRepository.Update(order);
                    }
                }
                if (rejected > 0 && _rules.DeliveryAutoComplaintOnRejected)
                {
                    var complaint = await DeliveryRules.NewComplaintAsync(_unitOfWork, _documentNumberService, _rules, actor, now, deliveryOrder.CustomerId, deliveryOrder.OrderId,
                        deliveryOrder.Id, route.Id, DeliveryCode.ComplaintSourcePod, DeliveryCode.ComplaintCategoryShortage, ComplaintSeverity.Medium,
                        $"Bàn giao {deliveryOrder.DeliveryNumber ?? deliveryOrder.DeliveryNo} (chuyến {DeliveryRules.RouteNumber(route)}): khách từ chối {DeliveryRules.Num(rejected)} / {DeliveryRules.Num(delivered)} suất"
                        + (pod.Remark == null ? string.Empty : " – " + pod.Remark));
                    ctx.Warnings.Add("Đã tạo phản ánh " + complaint.ComplaintNo + " cho số suất bị từ chối");
                }

                // BE2.5 (spec 10.3 pod "Ghi PackagingLedger OUT nếu đóng khay", Catalog 190; PROPOSED D-54 a): khay / hộp để lại ở khách → sổ bao bì OUT
                // (số = gửi kèm, trống = suất khách nhận × PackagingUnitsPerMeal khi PackagingOutOnDelivery) + phiếu thu hồi Chờ thu hồi (ContainerReturn Expected).
                decimal? packaging = request.PackagingQuantity ?? (_rules.PackagingOutOnDelivery ? accepted * _rules.PackagingUnitsPerMeal : null);
                if (packaging is > 0 and { } qty)
                {
                    qty = Math.Ceiling(qty);
                    string type = DeliveryRules.Clean(request.PackagingType) ?? await PackagingRules.DefaultTypeAsync(db, deliveryOrder.OrderId, _rules);
                    PackagingRules.Add(_unitOfWork, actor, now, PackagingTransactionType.Out, type, qty, deliveryOrder, null, PackagingCondition.Good,
                        $"Bàn giao chuyến {DeliveryRules.RouteNumber(route)} điểm {stopName} – {DeliveryRules.Num(accepted)} suất");
                    _unitOfWork.ContainerReturnRepository.Add(new ContainerReturn
                    {
                        Id = Guid.NewGuid(),
                        DeliveryOrderId = deliveryOrder.Id,
                        CustomerId = deliveryOrder.CustomerId,
                        ReturnDate = deliveryOrder.DeliveryDate.Date,
                        ExpectedQuantity = (int)qty,
                        Status = ContainerReturnStatus.Expected,
                        Note = PackagingRules.WithType(null, type),
                    });
                    ctx.Warnings.Add(string.Format(ConstantHelper.Message.PackagingOutRecorded, DeliveryRules.Num(qty), type));
                }
                return null;
            });

        /// <summary>T19 (spec 10.3 fail): Kế hoạch / Đã đến → Thất bại (lý do bắt buộc); đơn giao → Thất bại (đơn hàng giữ Đang sản xuất, xếp chuyến lại được); sự kiện FAILED.</summary>
        public Task<IResult<DeliveryRouteDetailModel>> Fail(DeliveryStopFailRequest request)
            => RunAsync(request.Id, "báo giao thất bại", [DeliveryStopStatus.Planned, DeliveryStopStatus.Arrived], (stop, route, deliveryOrder, actor, ctx) =>
            {
                if (string.IsNullOrWhiteSpace(request.Reason))
                {
                    return Task.FromResult<string?>(ConstantHelper.Message.StopReasonRequired);
                }
                DateTime now = _clock.Now;
                stop.StopStatus = DeliveryStopStatus.Failed;
                stop.Note = request.Reason.Trim().Length > 1000 ? request.Reason.Trim()[..1000] : request.Reason.Trim();
                if (deliveryOrder != null)
                {
                    deliveryOrder.Status = DeliveryOrderStatus.Failed;
                    deliveryOrder.Note = string.Join(" – ", new[] { deliveryOrder.Note, "Thất bại: " + request.Reason.Trim() }.Where(x => !string.IsNullOrEmpty(x)));
                    _unitOfWork.DeliveryOrderRepository.Update(deliveryOrder);
                    DeliveryRules.SyncExecution(_unitOfWork, deliveryOrder, route.Id, DeliveryExecutionStatus.Failed, now, "Giao thất bại: " + request.Reason.Trim());
                }
                DeliveryRules.AddEvent(_unitOfWork, actor, now, route.Id, stop.Id, stop.DeliveryOrderId, DeliveryCode.EventFailed,
                    "Giao thất bại: " + request.Reason.Trim(), request.Latitude, request.Longitude);
                ctx.Warnings.Add("Đơn hàng của điểm này vẫn Đang sản xuất – điều phối xếp chuyến giao bù");
                return Task.FromResult<string?>(null);
            });

        public async Task<IResult<DeliveryStop>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.DeliveryStopRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<DeliveryStop>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<DeliveryStop>
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
