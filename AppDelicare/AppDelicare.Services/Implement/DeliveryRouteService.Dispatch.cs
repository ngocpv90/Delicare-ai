using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Implement.Messaging;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// UI-6.34 (D-115): Điều phối giao hàng – bảng chuyến × điểm giao (đủ thực đơn / còn thiếu món), chuyển điểm sang chuyến khác, tự lập chuyến sau chốt đơn,
    /// tin bot chuyến đủ hàng / sắp xuất bến mà chưa đủ. Không thêm bảng / cột.
    /// </summary>
    public partial class DeliveryRouteService
    {
        private static readonly string[] OpenRoute = [DeliveryRouteStatus.Planned, DeliveryRouteStatus.Loading];

        public async Task<IResult<DispatchBoardModel>> GetDispatchBoard(DateTime? date)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                DateTime day = (date ?? _clock.Today).Date, next = day.AddDays(1);
                var routes = await db.DeliveryRoute.AsNoTracking().Where(r => !r.IsDeleted && r.DeliveryDate >= day && r.DeliveryDate < next && r.Status != "Cancelled")
                    .OrderBy(r => r.PlannedStart).ThenBy(r => r.RouteNo).ToListAsync();
                var dos = await db.DeliveryOrder.AsNoTracking().Where(d => !d.IsDeleted && d.DeliveryDate >= day && d.DeliveryDate < next && d.Status != DeliveryOrderStatus.Cancelled).ToListAsync();
                var stops = await db.DeliveryStop.AsNoTracking().Where(s => !s.IsDeleted && s.DeliveryOrderId != null && dos.Select(d => d.Id).Contains(s.DeliveryOrderId.Value)).ToListAsync();
                var missing = await PackingReadiness.MissingByOrderAsync(db, _rules, day, [.. dos.Select(d => d.OrderId).Distinct()]);
                var custIds = dos.Select(d => d.CustomerId).Distinct().ToList();
                var custs = await db.Customer.AsNoTracking().Where(c => custIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.DisplayName ?? c.Name);
                var locIds = dos.Select(d => d.CustomerDeliveryLocationId ?? d.DeliveryLocationId).Where(x => x != null).Select(x => x!.Value).Distinct().ToList();
                var locs = await db.CustomerDeliveryLocation.AsNoTracking().Where(l => locIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.Name);
                var vehIds = routes.Where(r => r.VehicleId != null).Select(r => r.VehicleId!.Value).Distinct().ToList();
                var vehs = await db.Vehicle.AsNoTracking().Where(v => vehIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, v => (v.LicensePlate ?? v.Code) + (v.VehicleType == null ? string.Empty : " · " + v.VehicleType));
                var drvIds = routes.Where(r => r.DriverId != null).Select(r => r.DriverId!.Value).Distinct().ToList();
                var drvs = await db.Employee.AsNoTracking().Where(e => drvIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.FullName);
                var loadedIds = (await db.DeliveryLoading.AsNoTracking().Where(l => !l.IsDeleted && l.Status == DeliveryLoadingStatus.Loaded).Select(l => l.DeliveryOrderId).ToListAsync()).ToHashSet();
                DispatchPointModel Point(Model.Entity.DeliveryOrder d)
                {
                    var stop = stops.FirstOrDefault(s => s.DeliveryOrderId == d.Id);
                    var miss = missing.GetValueOrDefault(d.OrderId) ?? [];
                    return new DispatchPointModel
                    {
                        DeliveryOrderId = d.Id, OrderId = d.OrderId, DeliveryNumber = d.DeliveryNumber ?? d.DeliveryNo, CustomerName = custs.GetValueOrDefault(d.CustomerId),
                        LocationName = (d.CustomerDeliveryLocationId ?? d.DeliveryLocationId) is { } l ? locs.GetValueOrDefault(l) : null,
                        Meals = stop?.PlannedMealQty ?? DeliveryRules.TaggedMeals(d.Note) ?? 0m, PlannedTime = d.PlannedDeliveryTime, Complete = miss.Count == 0, ListMissing = miss,
                    };
                }
                var model = new DispatchBoardModel { Date = day };
                foreach (var r in routes)
                {
                    var points = dos.Where(d => d.DeliveryRouteId == r.Id).OrderBy(d => stops.FirstOrDefault(s => s.DeliveryOrderId == d.Id)?.SequenceNo ?? 999).Select(Point).ToList();
                    model.ListRoute.Add(new DispatchRouteModel
                    {
                        Id = r.Id, RouteNo = DeliveryRules.RouteNumber(r), Name = r.Name, VehicleName = r.VehicleId is { } v ? vehs.GetValueOrDefault(v) : null,
                        DriverName = r.DriverId is { } dr ? drvs.GetValueOrDefault(dr) : null, PlannedStart = r.PlannedStart, Status = r.Status, StatusName = DeliveryRouteStatus.Label(r.Status),
                        PointsComplete = points.Count(p => p.Complete), CanLoad = points.Count != 0 && points.All(p => p.Complete) && OpenRoute.Contains(r.Status),
                        Loaded = points.Count != 0 && dos.Where(d => d.DeliveryRouteId == r.Id).All(d => loadedIds.Contains(d.Id)), ListPoint = points,
                    });
                }
                model.ListUnassigned = [.. dos.Where(d => d.DeliveryRouteId == null && d.Status == DeliveryOrderStatus.Planned).OrderBy(d => d.PlannedDeliveryTime).Select(Point)];
                model.ListRedeliver = [.. dos.Where(d => d.Status == DeliveryOrderStatus.Failed).OrderBy(d => d.PlannedDeliveryTime).Select(Point)];
                model.RoutesReady = model.ListRoute.Count(r => r.CanLoad);
                var allPoints = model.ListRoute.SelectMany(r => r.ListPoint).Concat(model.ListUnassigned).ToList();
                model.PointsTotal = allPoints.Count;
                model.PointsComplete = allPoints.Count(p => p.Complete);
                return Result<DispatchBoardModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetDispatchBoard: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>Chuyển điểm giao sang chuyến khác (dùng DeliveryOrder/AssignRoute – kiểm trạng thái, giờ, tải ở đó).</summary>
        public async Task<IResult<DeliveryOrderActionModel>> MovePoint(DispatchMovePointRequest request)
        {
            // UI-6.35 (D-116): điểm giao thất bại → mở lại (Kế hoạch, bỏ chuyến cũ) rồi gán chuyến mới.
            var failed = _unitOfWork.DeliveryOrderRepository.GetSingle(d => d.Id == request.DeliveryOrderId && !d.IsDeleted && d.Status == DeliveryOrderStatus.Failed);
            if (failed != null)
            {
                failed.Status = DeliveryOrderStatus.Planned;
                failed.DeliveryRouteId = null;
                _unitOfWork.DeliveryOrderRepository.Update(failed);
                await _unitOfWork.DeliveryOrderRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.DbContext.ChangeTracker.Clear();
            }
            return await _services.GetRequiredService<IDeliveryOrderService>().AssignRoute(new DeliveryOrderAssignRouteRequest { Id = request.DeliveryOrderId, DeliveryRouteId = request.DeliveryRouteId });
        }

        /// <summary>
        /// Tự lập chuyến theo khu vực cho ngày giao sau khi chốt đơn (cờ <c>DeliveryAutoPlanAfterLock</c>): mọi xe đang chạy được; không có xe → tin bot cho điều phối,
        /// điểm giao nằm ở "Chưa gán chuyến". Chạy lại không nhân chuyến (Plan chỉ xếp đơn chưa gán). Lỗi không làm hỏng việc chốt đơn.
        /// </summary>
        public async Task<string?> AutoPlanAfterLockAsync(DateTime date)
        {
            if (!_rules.DeliveryAutoPlanAfterLock)
            {
                return null;
            }
            try
            {
                var db = _unitOfWork.DbContext;
                var vehicles = (await db.Vehicle.AsNoTracking().Where(v => !v.IsDeleted).ToListAsync())
                    .Where(v => !VehicleBlockedStatus.Contains((v.Status ?? string.Empty).ToUpperInvariant())).ToList();
                if (vehicles.Count == 0)
                {
                    await BusinessNotifier.ToMenusAsync(_notifications, db, NotificationCatalog.RouteReady, ["#/log-route"],
                        new { title = $"Chưa tự lập chuyến ngày {date:dd/MM}", detail = "Chưa có xe sẵn sàng – điểm giao nằm ở \"Chưa gán chuyến\" của Điều phối giao hàng" },
                        $"{NotificationCatalog.RouteReady}:NOVEH:{date:yyyyMMdd}");
                    return "Chưa có xe sẵn sàng – chưa tự lập chuyến";
                }
                var r = await Plan(new DeliveryRoutePlanRequest { DeliveryDate = date.Date, ListVehicle = [.. vehicles.Select(v => new DeliveryPlanVehicleRequest { VehicleId = v.Id })] });
                return r.ResponseMessage;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[DeliveryRouteService] AutoPlanAfterLockAsync {Date}", date);
                return null;
            }
        }

        /// <summary>BotWatch: chuyến hôm nay vừa đủ thực đơn mọi điểm → 1 tin / chuyến; còn ≤ DispatchShortWarnMinutes tới giờ xuất bến mà chưa đủ → 1 tin cảnh báo / chuyến.</summary>
        public async Task<int> RemindDispatchAsync(DateTime now)
        {
            var board = (await GetDispatchBoard(now.Date)).Data;
            if (board == null)
            {
                return 0;
            }
            var db = _unitOfWork.DbContext;
            int sent = 0;
            foreach (var r in board.ListRoute.Where(r => OpenRoute.Contains(r.Status) && r.ListPoint.Count != 0))
            {
                if (r.CanLoad && !r.Loaded)
                {
                    sent += await BusinessNotifier.ToMenusAsync(_notifications, db, NotificationCatalog.RouteReady, ["#/log-route"],
                        new { title = $"Chuyến {r.RouteNo} đủ hàng", detail = $"{r.ListPoint.Count} điểm đủ thực đơn – xếp lên xe" + (r.PlannedStart == null ? string.Empty : $", xuất bến {r.PlannedStart:HH:mm}") },
                        $"{NotificationCatalog.RouteReady}:{r.Id:N}");
                }
                else if (!r.CanLoad && r.PlannedStart is { } start && start > now && start <= now.AddMinutes(_rules.DispatchShortWarnMinutes))
                {
                    sent += await BusinessNotifier.ToMenusAsync(_notifications, db, NotificationCatalog.RouteReady, ["#/log-route"],
                        new
                        {
                            title = $"Chuyến {r.RouteNo} sắp xuất bến mà chưa đủ hàng",
                            detail = $"Xuất bến {start:HH:mm} – {r.PointsComplete}/{r.ListPoint.Count} điểm đủ thực đơn",
                            lines = r.ListPoint.Where(p => !p.Complete).Select(p => $"⏳ {p.CustomerName} {p.LocationName}: thiếu {string.Join(", ", p.ListMissing)}").ToList(),
                        },
                        $"{NotificationCatalog.RouteReady}:SHORT:{r.Id:N}");
                }
            }
            return sent;
        }
    }
}
