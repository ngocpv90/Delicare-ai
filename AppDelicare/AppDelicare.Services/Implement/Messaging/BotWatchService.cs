using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Serilog;

namespace AppDelicare.Services.Implement.Messaging
{
    /// <summary>
    /// BOT-2.3 (BOT_TELEGRAM §5.1 – tin theo lịch, ngưỡng <c>Messaging:Watch</c>): kiểm tra định kỳ, messaging gọi <c>BotChannel/RunWatch</c> mỗi
    /// <c>Messaging:WatchIntervalSeconds</c> giây (job hằng ngày của AppDelicare.Job không chạy theo phút). Mỗi việc chống trùng bằng IdempotencyKey →
    /// gọi dày / chạy lại không gửi trùng. Chỉ ĐỌC dữ liệu nghiệp vụ + xếp tin (không đổi nghiệp vụ). Một việc lỗi không chặn việc khác.
    /// <list type="bullet">
    /// <item>BATCH_LATE: mẻ chưa bắt đầu / chưa xong quá N′ so với kế hoạch (StartDate / EndDate) → người có quyền Thêm trên mẻ nấu.</item>
    /// <item>TRIP_NOT_DEPARTED: chuyến hôm nay Kế hoạch / Đang xếp quá N′ giờ xuất phát → điều phối (sự việc – có leo thang).</item>
    /// <item>STOP_LATE: điểm của chuyến đang giao chưa đến quá N′ giờ dự kiến → điều phối (sự việc).</item>
    /// <item>NOT_CHECKED_IN: ca đã bắt đầu quá N′ mà còn người chưa vào ca → 1 tin / ca (danh sách tên) tới người chấm công hộ.</item>
    /// <item>COMPLAINT_SLA: phản ánh chưa xong còn ≤ N′ tới hạn SLA (lần 2 khi đã quá hạn) → CSKH.</item>
    /// <item>PO_LATE: PO đã duyệt / đã gửi / nhận một phần quá ngày hẹn giao → mua hàng, thủ kho (1 lần / PO).</item>
    /// <item>APPROVAL_OVERDUE: <see cref="IApprovalRequestService.NotifyOverdueAsync"/>.</item>
    /// <item>TEMPERATURE_DUE (UI-6.31): khu đo tay đến giờ chưa đo (1 tin / ca), cảm biến mất tín hiệu (1 tin / khoảng mất).</item>
    /// <item>CHECKLIST_DUE (UI-6.30): lượt kiểm tra ATTP theo ca quá hạn → 1 tin gộp / ca (trưởng ca + QA).</item>
    /// </list>
    /// </summary>
    public class BotWatchService(IUnitOfWork unitOfWork, IClock clock, IConfiguration configuration, ICurrentActor currentActor,
        IOptionsMonitor<MessagingOptions> options, INotificationService notifications, IApprovalRequestService approvals, IFoodSafetyChecklistResultService checklists, ITemperatureLogService temperatures, ISupplierIssueService supplierIssues, IDeliveryRouteService routes, IPackagingLedgerService packaging, IShiftAssignmentService shifts, IShiftSwapRequestService swaps) : IBotWatchService
    {
        private static readonly string[] PoOpen = [PurchaseOrderStatus.Approved, PurchaseOrderStatus.Sent, PurchaseOrderStatus.PartiallyReceived];

        public async Task<IResult<int>> Run()
        {
            if (!Guid.TryParse(configuration[$"{SystemUsersOptions.Section}:{nameof(SystemUsersOptions.JobUserId)}"], out Guid systemUser) || systemUser == Guid.Empty)
            {
                return Result<int>.Success(0, ConstantHelper.Message.JobUserMissing);
            }
            var o = options.CurrentValue.Watch;
            DateTime now = clock.Now;
            int sent = 0;
            using var scope = currentActor.BeginScope(ActorInfo.ForSystem(systemUser));
            sent += await SafeAsync("BatchLate", () => BatchLateAsync(now, o));
            sent += await SafeAsync("TripNotDeparted", () => TripNotDepartedAsync(now, o));
            sent += await SafeAsync("StopLate", () => StopLateAsync(now, o));
            sent += await SafeAsync("NotCheckedIn", () => NotCheckedInAsync(now, o));
            sent += await SafeAsync("ComplaintSla", () => ComplaintSlaAsync(now, o));
            sent += await SafeAsync("PoLate", () => PoLateAsync(now));
            sent += await SafeAsync("ChecklistOverdue", () => checklists.RemindOverdueAsync(now));
            sent += await SafeAsync("TemperatureDue", () => temperatures.RemindAsync(now));
            sent += await SafeAsync("SupplierIssueOverdue", () => supplierIssues.RemindOverdueAsync(now));
            sent += await SafeAsync("RouteReady", () => routes.RemindDispatchAsync(now));
            sent += await SafeAsync("StopLate", () => routes.RemindLateStopsAsync(now));
            sent += await SafeAsync("ContainerOverdue", () => packaging.RemindOverdueReturnsAsync(now));
            // UI-6.37 (D-118 f): Thứ 6 sau 15:00 – tuần sau còn ca nháp / thiếu người → 1 tin / tuần.
            sent += await SafeAsync("ShiftWeekRemind", () => shifts.RemindNextWeekAsync());
            // UI-6.39 (D-120): đơn đổi / nhường ca quá hạn chờ người nhận → tự hủy + báo người xin.
            sent += await SafeAsync("SwapConsentExpire", () => swaps.ExpireConsentsAsync());
            sent += await SafeAsync("ApprovalOverdue", () => approvals.NotifyOverdueAsync(o.ApprovalOverdueEscalateHours, o.EffectiveApprovalOverdueGroups, o.EffectiveApprovalOverdueTopGroups));
            // APR-1.4 (D-81 i): người duyệt của cấp đều nghỉ hôm nay → báo ngay bậc leo thang (1 lần / yêu cầu / cấp / ngày).
            sent += await SafeAsync("ApprovalAbsent", () => approvals.EscalateAbsentAsync(o.EffectiveApprovalOverdueGroups, o.EffectiveApprovalOverdueTopGroups));
            // APR-1.7: tổng hợp duyệt cuối ngày từ Messaging:ApprovalDigestHour (IdempotencyKey theo người + ngày → 1 lần / ngày).
            int digestHour = options.CurrentValue.ApprovalDigestHour;
            if (digestHour >= 0 && now.Hour >= digestHour)
            {
                sent += await SafeAsync("ApprovalDigest", approvals.SendDigestAsync);
            }
            return Result<int>.Success(sent, ConstantHelper.Message.GetAllSuccessfully);
        }

        private static async Task<int> SafeAsync(string name, Func<Task<int>> check)
        {
            try
            {
                return await check();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[BotWatch] {Check}: lỗi – bỏ qua lần này", name);
                return 0;
            }
        }

        private async Task<int> BatchLateAsync(DateTime now, WatchOptions o)
        {
            var db = unitOfWork.DbContext;
            DateTime late = now.AddMinutes(-o.BatchLateMinutes), since = now.AddHours(-12);
            var rows = await (from b in db.ProductionBatch.AsNoTracking()
                              join d in db.Dish.AsNoTracking() on b.DishId equals d.Id into dd
                              from d in dd.DefaultIfEmpty()
                              where !b.IsDeleted && b.StartDate >= since
                                    && ((b.Status == ProductionBatchStatus.Planned && b.StartDate < late)
                                        || (b.Status == ProductionBatchStatus.Cooking && b.EndDate != null && b.EndDate < late))
                              orderby b.StartDate
                              select new { b.Id, No = b.BatchNo ?? b.BatchNumber, Dish = d != null ? d.Name : null, b.Status, b.StartDate, b.EndDate })
                             .Take(50).ToListAsync();
            int sent = 0;
            foreach (var b in rows)
            {
                bool notStarted = b.Status == ProductionBatchStatus.Planned;
                sent += await BusinessNotifier.ToMenusAsync(notifications, db, NotificationCatalog.BatchLate, ["#/prod-batch"],
                    new
                    {
                        title = $"Mẻ {b.No} – {b.Dish}",
                        detail = notStarted ? $"Chưa bắt đầu – kế hoạch {b.StartDate:HH:mm}" : $"Chưa xong – dự kiến xong {b.EndDate:HH:mm}",
                    },
                    $"{NotificationCatalog.BatchLate}:{b.Id:N}:{(notStarted ? "start" : "finish")}",
                    buttons: [new NotificationButton("🍳 Mở mẻ", BusinessNotifier.Callback("bat", "view", b.Id))], requireAdd: true);
            }
            return sent;
        }

        private async Task<int> TripNotDepartedAsync(DateTime now, WatchOptions o)
        {
            var db = unitOfWork.DbContext;
            DateTime late = now.AddMinutes(-o.TripLateMinutes), since = now.AddHours(-12);
            var rows = await db.DeliveryRoute.AsNoTracking()
                .Where(r => !r.IsDeleted && (r.Status == DeliveryRouteStatus.Planned || r.Status == DeliveryRouteStatus.Loading)
                            && r.PlannedStart != null && r.PlannedStart < late && r.PlannedStart >= since)
                .OrderBy(r => r.PlannedStart).Select(r => new { r.Id, No = r.RouteNo ?? r.Code, r.PlannedStart }).Take(50).ToListAsync();
            int sent = 0;
            foreach (var r in rows)
            {
                sent += await BusinessNotifier.ToMenusAsync(notifications, db, NotificationCatalog.TripNotDeparted, ["#/log-route"],
                    new { title = $"Chuyến {r.No}", detail = $"Dự kiến xuất phát {r.PlannedStart:HH:mm} – xe chưa rời bếp" },
                    $"{NotificationCatalog.TripNotDeparted}:{r.Id:N}",
                    buttons: [new NotificationButton("🚚 Mở chuyến", BusinessNotifier.Callback("dlv", "r", r.Id))], requireAdd: true);
            }
            return sent;
        }

        private async Task<int> StopLateAsync(DateTime now, WatchOptions o)
        {
            var db = unitOfWork.DbContext;
            DateTime late = now.AddMinutes(-o.StopLateMinutes), since = now.AddHours(-12);
            var rows = await (from s in db.DeliveryStop.AsNoTracking()
                              join r in db.DeliveryRoute.AsNoTracking() on s.DeliveryRouteId equals r.Id
                              join c in db.Customer.AsNoTracking() on s.CustomerId equals c.Id into cc
                              from c in cc.DefaultIfEmpty()
                              where !s.IsDeleted && !r.IsDeleted && r.Status == DeliveryRouteStatus.InTransit && s.StopStatus == DeliveryStopStatus.Planned
                                    && s.PlannedArrival != null && s.PlannedArrival < late && s.PlannedArrival >= since
                              orderby s.PlannedArrival
                              select new { s.Id, s.SequenceNo, Customer = c != null ? c.Name : null, Route = r.RouteNo ?? r.Code, s.PlannedArrival })
                             .Take(50).ToListAsync();
            int sent = 0;
            foreach (var s in rows)
            {
                sent += await BusinessNotifier.ToMenusAsync(notifications, db, NotificationCatalog.StopLate, ["#/log-route", "#/log-stop"],
                    new { title = $"Điểm {s.SequenceNo} – {s.Customer}", detail = $"Chuyến {s.Route} · dự kiến đến {s.PlannedArrival:HH:mm} – chưa đến" },
                    $"{NotificationCatalog.StopLate}:{s.Id:N}",
                    buttons: [new NotificationButton("📍 Mở điểm giao", BusinessNotifier.Callback("dlv", "s", s.Id))], requireAdd: true);
            }
            return sent;
        }

        private async Task<int> NotCheckedInAsync(DateTime now, WatchOptions o)
        {
            var db = unitOfWork.DbContext;
            DateTime late = now.AddMinutes(-o.NotCheckedInMinutes);
            var rows = await (from a in db.ShiftAssignment.AsNoTracking()
                              join e in db.Employee.AsNoTracking() on a.EmployeeId equals e.Id
                              join sh in db.Shift.AsNoTracking() on a.ShiftId equals sh.Id into ss
                              from sh in ss.DefaultIfEmpty()
                              where !a.IsDeleted && a.WorkDate >= now.Date.AddDays(-1) && a.WorkDate <= now.Date
                                    && (a.AssignmentStatus == ShiftAssignmentStatus.Planned || a.AssignmentStatus == ShiftAssignmentStatus.Confirmed)
                                    && a.PlannedStart != null && a.PlannedEnd != null && a.PlannedStart < late && a.PlannedEnd > now
                                    && !db.AttendanceEvent.Any(ev => !ev.IsDeleted && ev.ShiftAssignmentId == a.Id && ev.EventType == AttendanceEventType.CheckIn)
                              select new { a.ShiftId, a.WorkDate, Start = a.PlannedStart!.Value, ShiftName = sh != null ? sh.Name : null, Name = e.FullName ?? e.Code, e.OrganizationUnitId })
                             .ToListAsync();
            int sent = 0;
            foreach (var g in rows.GroupBy(r => new { r.ShiftId, r.WorkDate }))
            {
                var first = g.OrderBy(x => x.Start).First();
                var names = g.Select(x => x.Name).Distinct().OrderBy(x => x).ToList();
                sent += await BusinessNotifier.ToMenusAsync(notifications, db, NotificationCatalog.NotCheckedIn, ["#/hr-attendance"],
                    new
                    {
                        title = $"Ca {first.ShiftName} {first.Start:HH:mm}: {names.Count} người chưa vào ca",
                        lines = names.Take(15).Select(n => "• " + n).Concat(names.Count > 15 ? [$"… và {names.Count - 15} người khác"] : []).ToList(),
                    },
                    $"{NotificationCatalog.NotCheckedIn}:{g.Key.ShiftId:N}:{g.Key.WorkDate:yyyyMMdd}",
                    buttons: [new NotificationButton("👥 Ai chưa vào ca", "sc:run:hr_absent")], requireAdd: true);
                // UI-6.38 (D-119 b): thêm trưởng đơn vị của người chưa vào ca – 1 tin / ca / đơn vị.
                foreach (var u in g.Where(x => x.OrganizationUnitId != null).GroupBy(x => x.OrganizationUnitId!.Value))
                {
                    var manager = await db.OrganizationUnit.AsNoTracking().Where(o => o.Id == u.Key && o.ManagerId != null).Select(o => o.ManagerId).FirstOrDefaultAsync();
                    if (manager is not { } mid)
                    {
                        continue;
                    }
                    var un = u.Select(x => x.Name).Distinct().OrderBy(x => x).ToList();
                    sent += await BusinessNotifier.SafeEnqueueAsync(notifications, new NotificationRequest
                    {
                        Template = NotificationCatalog.NotCheckedIn, EmployeeIds = [mid],
                        Data = new { title = $"Ca {first.ShiftName} {first.Start:HH:mm}: {un.Count} người trong tổ chưa vào ca", lines = un.Take(15).Select(n => "• " + n).ToList() },
                        IdempotencyKey = $"{NotificationCatalog.NotCheckedIn}:{g.Key.ShiftId:N}:{g.Key.WorkDate:yyyyMMdd}:MGR:{u.Key:N}",
                    });
                }
            }
            return sent;
        }

        private async Task<int> ComplaintSlaAsync(DateTime now, WatchOptions o)
        {
            var db = unitOfWork.DbContext;
            DateTime warn = now.AddMinutes(o.ComplaintSlaWarnMinutes), since = now.AddHours(-24);
            var rows = await (from c in db.CustomerComplaint.AsNoTracking()
                              join cu in db.Customer.AsNoTracking() on c.CustomerId equals cu.Id into cc
                              from cu in cc.DefaultIfEmpty()
                              where !c.IsDeleted && !ComplaintStatus.Done.Contains(c.ComplaintStatus!) && c.SlaDueAt != null && c.SlaDueAt <= warn && c.SlaDueAt >= since
                              orderby c.SlaDueAt
                              select new { c.Id, c.ComplaintNo, Customer = cu != null ? cu.Name : null, c.SlaDueAt, c.Severity })
                             .Take(50).ToListAsync();
            int sent = 0;
            foreach (var c in rows)
            {
                bool over = c.SlaDueAt <= now;
                sent += await BusinessNotifier.ToMenusAsync(notifications, db, NotificationCatalog.ComplaintSla, ["#/log-complaint"],
                    new
                    {
                        title = $"Phản ánh {c.ComplaintNo} – {c.Customer}",
                        detail = over ? $"Đã quá hạn xử lý ({c.SlaDueAt:HH:mm dd/MM})" : $"Hạn xử lý {c.SlaDueAt:HH:mm} – còn {(int)Math.Ceiling((c.SlaDueAt!.Value - now).TotalMinutes)}′",
                    },
                    $"{NotificationCatalog.ComplaintSla}:{c.Id:N}:{(over ? "over" : "warn")}",
                    buttons: [new NotificationButton("📣 Mở phản ánh", BusinessNotifier.Callback("cmp", "v", c.Id))], requireAdd: true);
            }
            return sent;
        }

        private async Task<int> PoLateAsync(DateTime now)
        {
            var db = unitOfWork.DbContext;
            DateTime since = now.Date.AddDays(-7);
            var rows = await (from p in db.PurchaseOrder.AsNoTracking()
                              join s in db.Supplier.AsNoTracking() on p.SupplierId equals s.Id into ss
                              from s in ss.DefaultIfEmpty()
                              let due = p.ExpectedDeliveryDate ?? p.ExpectedDate
                              where !p.IsDeleted && PoOpen.Contains(p.Status!) && due != null && due < now && due >= since
                              orderby due
                              select new { p.Id, No = p.PONo ?? p.PurchaseOrderNumber, Supplier = s != null ? s.Name : null, Due = due })
                             .Take(50).ToListAsync();
            int sent = 0;
            foreach (var p in rows)
            {
                sent += await BusinessNotifier.ToMenusAsync(notifications, db, NotificationCatalog.PoLate, ["#/scm-po", "#/scm-gr"],
                    new { title = $"PO {p.No} – {p.Supplier}", detail = $"Hẹn giao {p.Due:HH:mm dd/MM} – chưa nhận đủ hàng" },
                    $"{NotificationCatalog.PoLate}:{p.Id:N}", requireAdd: true);
            }
            return sent;
        }
    }
}
