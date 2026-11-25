using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Services.Implement.Messaging;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// UI-6.35 (D-116): "Theo dõi giao hàng" – mỗi dòng một chuyến + dải điểm (chưa đến / đã đến / đã giao / thất bại / trễ khung nhận), vị trí gần nhất theo sự kiện;
    /// Sổ giao nhận (biên bản, chỉ xem); bot điểm sắp trễ / đã trễ. Không thêm bảng / cột.
    /// </summary>
    public partial class DeliveryRouteService
    {
        private static string StopCode(string? s) => s switch
        {
            DeliveryStopStatus.Arrived => "ARRIVED",
            DeliveryStopStatus.Delivered => "DELIVERED",
            DeliveryStopStatus.Failed => "FAILED",
            _ => "PLANNED",
        };

        public async Task<IResult<TrackBoardModel>> GetTrackBoard(DateTime? date, Guid? driverId = null)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                DateTime day = (date ?? _clock.Today).Date, next = day.AddDays(1), now = _clock.Now;
                var routes = await db.DeliveryRoute.AsNoTracking().Where(r => !r.IsDeleted && r.DeliveryDate >= day && r.DeliveryDate < next && r.Status != "Cancelled"
                        && (driverId == null || r.DriverId == driverId)).OrderBy(r => r.PlannedStart).ThenBy(r => r.RouteNo).ToListAsync();
                var routeIds = routes.Select(r => r.Id).ToList();
                var stops = await db.DeliveryStop.AsNoTracking().Where(s => !s.IsDeleted && routeIds.Contains(s.DeliveryRouteId)).OrderBy(s => s.SequenceNo).ToListAsync();
                var doIds = stops.Where(s => s.DeliveryOrderId != null).Select(s => s.DeliveryOrderId!.Value).Distinct().ToList();
                var dos = await db.DeliveryOrder.AsNoTracking().Where(d => doIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id);
                var pods = (await db.ProofOfDelivery.AsNoTracking().Where(p => !p.IsDeleted && doIds.Contains(p.DeliveryOrderId)).ToListAsync()).GroupBy(p => p.DeliveryOrderId).ToDictionary(g => g.Key, g => g.Last());
                var custIds = stops.Where(s => s.CustomerId != null).Select(s => s.CustomerId!.Value).Distinct().ToList();
                var custs = await db.Customer.AsNoTracking().Where(c => custIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.DisplayName ?? c.Name);
                var locIds = stops.Where(s => s.DeliveryLocationId != null).Select(s => s.DeliveryLocationId!.Value).Distinct().ToList();
                var locs = await db.CustomerDeliveryLocation.AsNoTracking().Where(l => locIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id);
                var events = await db.DeliveryEvent.AsNoTracking().Where(e => !e.IsDeleted && routeIds.Contains(e.DeliveryRouteId)).OrderBy(e => e.EventAt).ToListAsync();
                var vehIds = routes.Where(r => r.VehicleId != null).Select(r => r.VehicleId!.Value).ToList();
                var vehs = await db.Vehicle.AsNoTracking().Where(v => vehIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, v => v.LicensePlate ?? v.Code);
                var drvIds = routes.Where(r => r.DriverId != null).Select(r => r.DriverId!.Value).ToList();
                var drvs = await db.Employee.AsNoTracking().Where(e => drvIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.FullName);
                var refs = dos.Values.Select(d => d.DeliveryNumber ?? d.DeliveryNo).Where(n => n != null).ToList();
                var trays = (await db.PackagingLedger.AsNoTracking().Where(l => !l.IsDeleted && l.TransactionType == PackagingTransactionType.In && l.TransactionAt >= day && l.TransactionAt < next)
                    .Select(l => new { l.Note, l.Quantity }).ToListAsync());
                var model = new TrackBoardModel { Date = day };
                foreach (var r in routes)
                {
                    var rStops = stops.Where(s => s.DeliveryRouteId == r.Id).ToList();
                    var last = events.LastOrDefault(e => e.DeliveryRouteId == r.Id);
                    var dispatch = events.FirstOrDefault(e => e.DeliveryRouteId == r.Id && e.TemperatureC != null);
                    var tr = new TrackRouteModel
                    {
                        Id = r.Id, RouteNo = DeliveryRules.RouteNumber(r), VehicleName = r.VehicleId is { } v ? vehs.GetValueOrDefault(v) : null, DriverId = r.DriverId,
                        DriverName = r.DriverId is { } dr ? drvs.GetValueOrDefault(dr) : null, PlannedStart = r.PlannedStart, ActualStart = r.ActualStart, DispatchTemperature = dispatch?.TemperatureC,
                        Status = r.Status, StatusName = DeliveryRouteStatus.Label(r.Status), LastPosition = last?.Description, LastPositionAt = last?.EventAt,
                    };
                    foreach (var s in rStops)
                    {
                        var d = s.DeliveryOrderId is { } did ? dos.GetValueOrDefault(did) : null;
                        var loc = s.DeliveryLocationId is { } lid ? locs.GetValueOrDefault(lid) : null;
                        DateTime? windowEnd = loc?.DeliveryWindowTo is { } w ? day + w : null;
                        var pod = s.DeliveryOrderId is { } pid ? pods.GetValueOrDefault(pid) : null;
                        string code = StopCode(s.StopStatus);
                        bool late = windowEnd != null && ((code is "PLANNED" or "ARRIVED" && now > windowEnd) || (code == "DELIVERED" && (pod?.ReceivedAt ?? s.ActualArrival) > windowEnd));
                        string? number = d?.DeliveryNumber ?? d?.DeliveryNo;
                        tr.ListStop.Add(new TrackStopModel
                        {
                            StopId = s.Id, DeliveryOrderId = s.DeliveryOrderId, SequenceNo = s.SequenceNo, CustomerName = s.CustomerId is { } c ? custs.GetValueOrDefault(c) : null,
                            LocationName = loc?.Name, Address = loc?.Address, Status = code, Late = late, PlannedArrival = s.PlannedArrival, ActualArrival = s.ActualArrival, WindowEnd = windowEnd,
                            PlannedMeals = s.PlannedMealQty ?? 0m, Delivered = pod?.DeliveredQuantity ?? s.DeliveredMealQty, Accepted = pod?.AcceptedQuantity,
                            TraysReturned = number == null ? 0 : trays.Where(t => t.Note != null && t.Note.Contains("bàn giao " + number)).Sum(t => t.Quantity),
                            FailReason = code == "FAILED" ? s.Note : null,
                        });
                    }
                    model.ListRoute.Add(tr);
                }
                var all = model.ListRoute.SelectMany(r => r.ListStop).ToList();
                model.RoutesInTransit = model.ListRoute.Count(r => r.Status == DeliveryRouteStatus.InTransit);
                model.PointsTotal = all.Count;
                model.PointsDelivered = all.Count(s => s.Status == "DELIVERED");
                model.PointsLate = all.Count(s => s.Late);
                model.PointsFailed = all.Count(s => s.Status == "FAILED");
                return Result<TrackBoardModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetTrackBoard: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>Chế độ tài xế: chỉ chuyến của tài xế (nhân viên gắn tài khoản) – như GetMine.</summary>
        public async Task<IResult<TrackBoardModel>> GetMyTrack(DateTime? date)
        {
            var actor = _currentActor.RequireActor();
            var me = await Identity.PersonLink.EmployeeOfUserAsync(_unitOfWork.DbContext, actor.UserId);
            return me == null ? Result<TrackBoardModel>.Success(new TrackBoardModel { Date = (date ?? _clock.Today).Date }, ConstantHelper.Message.GetAllSuccessfully)
                : await GetTrackBoard(date, me.Id);
        }

        public async Task<IResult<List<PodBookRowModel>>> GetPodBook(PodBookRequest request)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                DateTime dTo = (request?.To ?? _clock.Today).Date, dFrom = (request?.From ?? dTo.AddDays(-6)).Date, end = dTo.AddDays(1);
                var rows = await (from p in db.ProofOfDelivery.AsNoTracking()
                                  join d in db.DeliveryOrder.AsNoTracking() on p.DeliveryOrderId equals d.Id
                                  where !p.IsDeleted && d.DeliveryDate >= dFrom && d.DeliveryDate < end
                                        && (request!.CustomerId == null || d.CustomerId == request.CustomerId) && (request.RouteId == null || d.DeliveryRouteId == request.RouteId)
                                  select new { p, d }).ToListAsync();
                var custIds = rows.Select(x => x.d.CustomerId).Distinct().ToList();
                var custs = await db.Customer.AsNoTracking().Where(c => custIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.DisplayName ?? c.Name);
                var locIds = rows.Select(x => x.d.CustomerDeliveryLocationId ?? x.d.DeliveryLocationId).Where(x => x != null).Select(x => x!.Value).Distinct().ToList();
                var locs = await db.CustomerDeliveryLocation.AsNoTracking().Where(l => locIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.Name);
                var routeIds = rows.Where(x => x.d.DeliveryRouteId != null).Select(x => x.d.DeliveryRouteId!.Value).Distinct().ToList();
                var routes = await db.DeliveryRoute.AsNoTracking().Where(r => routeIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id);
                var temps = (await db.DeliveryEvent.AsNoTracking().Where(e => !e.IsDeleted && e.DeliveryOrderId != null && e.TemperatureC != null && rows.Select(x => x.d.Id).Contains(e.DeliveryOrderId!.Value))
                    .Select(e => new { e.DeliveryOrderId, e.TemperatureC, e.EventAt }).ToListAsync()).GroupBy(e => e.DeliveryOrderId!.Value).ToDictionary(g => g.Key, g => g.OrderBy(e => e.EventAt).Last().TemperatureC);
                var list = rows.Select(x => new PodBookRowModel
                {
                    Id = x.p.Id, ReceivedAt = x.p.ReceivedAt ?? x.p.ConfirmedAt, RouteNo = x.d.DeliveryRouteId is { } rid && routes.TryGetValue(rid, out var r) ? DeliveryRules.RouteNumber(r) : null,
                    DeliveryNumber = x.d.DeliveryNumber ?? x.d.DeliveryNo, CustomerName = custs.GetValueOrDefault(x.d.CustomerId),
                    LocationName = (x.d.CustomerDeliveryLocationId ?? x.d.DeliveryLocationId) is { } l ? locs.GetValueOrDefault(l) : null,
                    Delivered = x.p.DeliveredQuantity ?? x.p.DeliveredQty ?? 0m, Accepted = x.p.AcceptedQuantity ?? x.p.AcceptedQty ?? 0m, Rejected = x.p.RejectedQuantity ?? x.p.RejectedQty ?? 0m, ReceiverName = x.p.ReceiverName,
                    Temperature = temps.GetValueOrDefault(x.d.Id), PhotoUrl = x.p.PhotoUrl, SignatureUrl = x.p.SignatureUrl, DeliveryOrderId = x.d.Id,
                }).OrderByDescending(x => x.ReceivedAt).ToList();
                return Result<List<PodBookRowModel>>.Success(list, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetPodBook: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>BotWatch: điểm sắp trễ (còn ≤ DeliveryLateWarnMinutes tới cuối khung nhận) / đã trễ mà chưa giao → điều phối, một tin / điểm / mức.</summary>
        public async Task<int> RemindLateStopsAsync(DateTime now)
        {
            var board = (await GetTrackBoard(now.Date)).Data;
            if (board == null)
            {
                return 0;
            }
            int sent = 0;
            foreach (var r in board.ListRoute)
            {
                foreach (var s in r.ListStop.Where(s => s.Status is "PLANNED" or "ARRIVED" && s.WindowEnd != null))
                {
                    bool late = now > s.WindowEnd, soon = !late && s.WindowEnd!.Value.AddMinutes(-_rules.DeliveryLateWarnMinutes) <= now;
                    if (!late && !soon)
                    {
                        continue;
                    }
                    sent += await BusinessNotifier.ToMenusAsync(_notifications, _unitOfWork.DbContext, NotificationCatalog.RouteReady, ["#/log-route", "#/log-execution"],
                        new { title = $"{(late ? "Đã trễ" : "Sắp trễ")} – {s.CustomerName} {s.LocationName}", detail = $"Chuyến {r.RouteNo}, điểm #{s.SequenceNo}: khung nhận đến {s.WindowEnd:HH:mm}, chưa giao" },
                        $"{NotificationCatalog.RouteReady}:{(late ? "LATE" : "SOON")}:{s.StopId:N}");
                }
            }
            return sent;
        }
    }
}
