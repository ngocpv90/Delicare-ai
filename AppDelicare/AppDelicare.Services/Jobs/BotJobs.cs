using AppDelicare.Data.Interface;
using AppDelicare.Data.Share;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Services.Implement.Messaging;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Jobs
{
    /// <summary>
    /// BOT-2.2 (BOT_TELEGRAM §5.2): 16:00 – người có đơn nghỉ ĐÃ DUYỆT bắt đầu ngày mai mà còn việc chờ CHÍNH MÌNH duyệt → nhắc "Anh/chị nghỉ từ … còn N việc chờ duyệt".
    /// Người ↔ tài khoản qua định danh bot (UserId + EmployeeId). Ủy quyền duyệt thật = quy định nghiệp vụ, chưa làm (chờ BA).
    /// </summary>
    public sealed class LeaveHandoverJob(IUnitOfWork unitOfWork, ICurrentActor currentActor, IApprovalRequestService approvals, INotificationService notifications) : IScheduledJob
    {
        public string Code => "LeaveHandover";
        public string Name => "Nhắc người duyệt trước ngày nghỉ";
        public string Description => "16:00 – người có đơn nghỉ đã duyệt từ ngày mai mà còn việc chờ mình duyệt → nhắc qua bot.";
        public string DefaultAt => "16:00";

        public async Task<JobOutcome> RunAsync(DateTime now, CancellationToken cancellationToken)
        {
            var db = unitOfWork.DbContext;
            DateTime tomorrow = now.Date.AddDays(1);
            var leaves = await db.LeaveRequest.AsNoTracking()
                .Where(l => !l.IsDeleted && l.RequestStatus == LeaveRequestStatus.Approved && l.FromDate >= tomorrow && l.FromDate < tomorrow.AddDays(1))
                .Select(l => new { l.Id, l.EmployeeId, l.FromDate, l.ToDate }).ToListAsync(cancellationToken);
            int sent = 0;
            foreach (var leave in leaves)
            {
                var users = await db.UserExternalIdentity.AsNoTracking().Where(i => !i.IsDeleted && i.EmployeeId == leave.EmployeeId && i.UserId != null)
                    .Select(i => i.UserId!.Value).Distinct().ToListAsync(cancellationToken);
                foreach (Guid user in users)
                {
                    int pending;
                    using (currentActor.BeginScope(ActorInfo.ForUser(user)))
                    {
                        pending = ((await approvals.GetMyPending(null, explicitOnly: true)).ListData ?? []).Count;
                    }
                    if (pending == 0)
                    {
                        continue;
                    }
                    sent += await BusinessNotifier.SafeEnqueueAsync(notifications, new NotificationRequest
                    {
                        Template = NotificationCatalog.LeaveHandover, UserIds = [user],
                        Data = new { title = $"Bạn nghỉ từ {leave.FromDate:dd/MM} đến {leave.ToDate:dd/MM}", detail = $"Còn {pending} việc chờ bạn duyệt – xử lý trước khi nghỉ hoặc báo cấp trên." },
                        Buttons = new[] { new NotificationButton("⏳ Chờ tôi duyệt", "sc:run:approvals") },
                        IdempotencyKey = $"{NotificationCatalog.LeaveHandover}:{leave.Id:N}",
                    });
                }
            }
            return new JobOutcome(JobRunStatus.Succeeded, sent, tomorrow, $"Nhắc {sent} người duyệt sắp nghỉ");
        }
    }

    /// <summary>
    /// BOT-2.1 (BOT_TELEGRAM §1.1 – Thẻ ngày): 07:00 gửi quản lý (có mục #/dash-exec, đã liên kết bot DH) 1 thẻ ghim: việc chờ CHÍNH MÌNH duyệt (GetMyPending explicitOnly),
    /// cảnh báo đang mở (chuông – theo quyền của người đó), số liệu chính hôm nay (Dashboard/GetExecutive). Chống trùng theo ngày + người (chạy lại không gửi lại).
    /// </summary>
    public sealed class DayCardJob(IUnitOfWork unitOfWork, ICurrentActor currentActor, IApprovalRequestService approvals, ICommonService common,
        IDashboardService dashboard, INotificationService notifications) : IScheduledJob
    {
        public string Code => "DayCard";
        public string Name => "Thẻ ngày cho quản lý (bot Điều hành)";
        public string Description => "07:00 – gửi Thẻ ngày (ghim) cho người có mục Dashboard điều hành đã liên kết bot DH: việc chờ tôi duyệt, cảnh báo đang mở, số liệu chính hôm nay.";
        public string DefaultAt => "07:00";

        public async Task<JobOutcome> RunAsync(DateTime now, CancellationToken cancellationToken)
        {
            var db = unitOfWork.DbContext;
            var bots = await RecipientResolver.ActiveBotsAsync(db, cancellationToken);
            if (!bots.TryGetValue(MessagingCode.BotDieuHanh, out Guid dh))
            {
                return new JobOutcome(JobRunStatus.Skipped, 0, now.Date, "Bot Điều hành chưa bật");
            }
            var managers = (await RecipientResolver.UsersWithMenuAsync(db, "#/dash-exec", cancellationToken)).ToHashSet();
            var linked = await db.UserExternalIdentity.AsNoTracking()
                .Where(i => !i.IsDeleted && i.IntegrationChannelId == dh && i.UserId != null && i.IdentityStatus == MessagingCode.Identity.Active)
                .Select(i => i.UserId!.Value).Distinct().ToListAsync(cancellationToken);
            var users = linked.Where(managers.Contains).ToList();
            var kpi = (await dashboard.GetExecutive(now.Date, null, null)).Data?.Kpi;
            int sent = 0;
            foreach (Guid user in users)
            {
                var lines = new List<string>();
                using (currentActor.BeginScope(ActorInfo.ForUser(user)))
                {
                    var pending = await approvals.GetMyPending(null, explicitOnly: true);
                    var list = pending.ListData ?? [];
                    lines.Add(list.Count == 0 ? "⏳ Không có việc chờ bạn duyệt" : $"⏳ Chờ bạn duyệt: {list.Count}" + (list.Count(p => p.IsOverdue) is > 0 and var od ? $" ({od} quá hạn)" : string.Empty));
                    foreach (var a in ((await common.GetAlerts(null)).ListData ?? []).Take(6))
                    {
                        lines.Add($"{(a.Severity is "High" or "Critical" ? "🔴" : "🟡")} {a.Title}: {a.Count}");
                    }
                }
                if (kpi != null)
                {
                    lines.Add($"🍱 Hôm nay: {kpi.PlannedMeals:#,0} suất · {kpi.Orders.Total} đơn · {kpi.Delivery.Total} điểm giao · {kpi.Staffing.Assigned} người có ca");
                }
                sent += await BusinessNotifier.SafeEnqueueAsync(notifications, new NotificationRequest
                {
                    Template = NotificationCatalog.DayCard, UserIds = [user], BotCode = MessagingCode.BotDieuHanh,
                    Data = new { title = $"☀️ Thẻ ngày {now:dd/MM}", lines },
                    Buttons = new[] { new NotificationButton("📊 Tổng quan", "sc:run:dh_overview"), new NotificationButton("⏳ Chờ tôi duyệt", "sc:run:approvals"), new NotificationButton("🚨 Việc đang mở", "sc:run:dh_open") },
                    IdempotencyKey = $"{NotificationCatalog.DayCard}:{now:yyyyMMdd}:{user:N}", CorrelationId = $"DAYCARD:{now:yyyyMMdd}",
                });
            }
            return new JobOutcome(JobRunStatus.Succeeded, sent, now.Date, $"Thẻ ngày: {sent} / {users.Count} người");
        }
    }

    /// <summary>
    /// BOT-2.3 (BOT_TELEGRAM §3.3, §5.1 MORNING_BRIEF): 07:00 – bản tin sáng ⚪ cho người có mục Dashboard điều hành (#/dash-exec), bot DH:
    /// kết quả hôm qua (giao, sản xuất, QC, phản ánh) + kế hoạch hôm nay (suất, đơn, điểm giao, người có ca). Số liệu = Dashboard/GetExecutive (như web).
    /// Khác Thẻ ngày (ghim, việc của riêng người đó): bản tin là số liệu chung công ty. Chống trùng theo ngày.
    /// </summary>
    public sealed class MorningBriefJob(IUnitOfWork unitOfWork, IDashboardService dashboard, INotificationService notifications) : IScheduledJob
    {
        public string Code => "MorningBrief";
        public string Name => "Bản tin sáng (bot Điều hành)";
        public string Description => "07:00 – kết quả hôm qua + kế hoạch hôm nay cho người có mục Dashboard điều hành (bot DH).";
        public string DefaultAt => "07:00";

        public async Task<JobOutcome> RunAsync(DateTime now, CancellationToken cancellationToken)
        {
            DateTime today = now.Date, yesterday = today.AddDays(-1);
            var y = (await dashboard.GetExecutive(yesterday, null, null)).Data?.Kpi;
            var t = (await dashboard.GetExecutive(today, null, null)).Data?.Kpi;
            var lines = new List<string>();
            if (y != null)
            {
                lines.Add($"📅 Hôm qua {yesterday:dd/MM}:");
                lines.AddRange(BotReports.Result(y));
            }
            if (t != null)
            {
                lines.Add($"☀️ Hôm nay {today:dd/MM}:");
                lines.Add(BotReports.Plan(t));
            }
            int sent = await BusinessNotifier.ToMenusAsync(notifications, unitOfWork.DbContext, NotificationCatalog.MorningBrief, [BotReports.ExecutiveMenu],
                new { title = $"Bản tin sáng {today:dd/MM}", lines }, $"{NotificationCatalog.MorningBrief}:{today:yyyyMMdd}",
                buttons: [new NotificationButton("📊 Tổng quan", "sc:run:dh_overview"), new NotificationButton("🚨 Việc đang mở", "sc:run:dh_open")],
                botCode: MessagingCode.BotDieuHanh);
            return new JobOutcome(JobRunStatus.Succeeded, sent, today, $"Bản tin sáng: {sent} tin");
        }
    }

    /// <summary>BOT-2.3 (§5.1 DAILY_REPORT): 17:00 – báo cáo cuối ngày ⚪ (kết quả hôm nay tới giờ chạy + đơn ngày mai), cùng người nhận với bản tin sáng.</summary>
    public sealed class DailyReportJob(IUnitOfWork unitOfWork, IDashboardService dashboard, INotificationService notifications) : IScheduledJob
    {
        public string Code => "DailyReport";
        public string Name => "Báo cáo cuối ngày (bot Điều hành)";
        public string Description => "17:00 – kết quả hôm nay (giao, sản xuất, QC, nhân sự, phản ánh) + đơn ngày mai cho người có mục Dashboard điều hành (bot DH).";
        public string DefaultAt => "17:00";

        public async Task<JobOutcome> RunAsync(DateTime now, CancellationToken cancellationToken)
        {
            DateTime today = now.Date, tomorrow = today.AddDays(1);
            var t = (await dashboard.GetExecutive(today, null, null)).Data?.Kpi;
            var n = (await dashboard.GetExecutive(tomorrow, null, null)).Data?.Kpi;
            var lines = new List<string>();
            if (t != null)
            {
                lines.AddRange(BotReports.Result(t));
                lines.Add($"👥 Nhân sự: {t.Staffing.CheckedIn}/{t.Staffing.Assigned} người đã vào ca");
            }
            if (n != null)
            {
                lines.Add($"🗓 Ngày mai {tomorrow:dd/MM}: {n.Orders.Total} đơn · {n.PlannedMeals:#,0} suất");
            }
            int sent = await BusinessNotifier.ToMenusAsync(notifications, unitOfWork.DbContext, NotificationCatalog.DailyReport, [BotReports.ExecutiveMenu],
                new { title = $"Báo cáo cuối ngày {today:dd/MM}", lines }, $"{NotificationCatalog.DailyReport}:{today:yyyyMMdd}",
                buttons: [new NotificationButton("📊 Tổng quan", "sc:run:dh_overview"), new NotificationButton("🎯 KPI", "sc:run:dh_kpi")],
                botCode: MessagingCode.BotDieuHanh);
            return new JobOutcome(JobRunStatus.Succeeded, sent, today, $"Báo cáo cuối ngày: {sent} tin");
        }
    }

    /// <summary>
    /// BOT-3.3 (D-72, BOT_TELEGRAM §3.3 "báo cáo tuần / tháng"): 07:30 – thứ Hai gửi báo cáo TUẦN trước (KPI trung bình 7 ngày so mục tiêu, KpiSnapshot/GetDaily),
    /// ngày 1 gửi báo cáo THÁNG trước (KpiSnapshot/GetMonthly) ⚪ cho người có mục Dashboard điều hành, bot DH. Ngày khác: không gửi. Chống trùng theo kỳ.
    /// </summary>
    public sealed class PeriodReportJob(IUnitOfWork unitOfWork, IKpiSnapshotService kpi, INotificationService notifications) : IScheduledJob
    {
        public string Code => "PeriodReport";
        public string Name => "Báo cáo tuần / tháng (bot Điều hành)";
        public string Description => "07:30 – thứ Hai: KPI tuần trước; ngày 1: KPI tháng trước – cho người có mục Dashboard điều hành (bot DH).";
        public string DefaultAt => "07:30";

        public async Task<JobOutcome> RunAsync(DateTime now, CancellationToken cancellationToken)
        {
            DateTime today = now.Date;
            int sent = 0;
            var notes = new List<string>();
            if (today.DayOfWeek == DayOfWeek.Monday)
            {
                DateTime from = today.AddDays(-7), to = today.AddDays(-1);
                var d = (await kpi.GetDaily(new AppDelicare.Model.Request.Custom.KpiDailyRequest { From = from, To = to })).Data;
                var lines = Lines(d, weekly: true);
                sent += await SendAsync($"Báo cáo tuần {from:dd/MM}–{to:dd/MM}", lines, $"{NotificationCatalog.PeriodReport}:W:{from:yyyyMMdd}");
                notes.Add("tuần");
            }
            if (today.Day == 1)
            {
                DateTime month = today.AddMonths(-1);
                string key = month.ToString("yyyy-MM");
                var d = (await kpi.GetMonthly(new AppDelicare.Model.Request.Custom.KpiMonthlyRequest { FromMonth = key, ToMonth = key })).Data;
                sent += await SendAsync($"Báo cáo tháng {month:MM/yyyy}", Lines(d, weekly: false), $"{NotificationCatalog.PeriodReport}:M:{month:yyyyMM}");
                notes.Add("tháng");
            }
            return new JobOutcome(JobRunStatus.Succeeded, sent, today, notes.Count == 0 ? "Hôm nay không phải kỳ báo cáo (thứ Hai / ngày 1)" : $"Báo cáo {string.Join(" + ", notes)}: {sent} tin");
        }

        /// <summary>Mỗi chỉ tiêu 1 dòng: tuần = trung bình các ngày có số liệu; tháng = giá trị tháng. ✅ đạt / ⚠️ chưa đạt theo hướng mục tiêu.</summary>
        public static List<string> Lines(KpiDashboardModel? d, bool weekly)
        {
            var lines = new List<string>();
            foreach (var m in d?.Metrics ?? [])
            {
                var values = m.Series.Where(p => p.Value != null).Select(p => p.Value!.Value).ToList();
                decimal? value = weekly ? (values.Count > 0 ? Math.Round(values.Average(), 2) : null) : m.Latest?.Value ?? (values.Count > 0 ? values.Last() : null);
                decimal? target = m.Latest?.Target ?? m.Definition.TargetValue;
                bool? met = value == null || target == null ? null
                    : string.Equals(m.Definition.TargetDirection, "LOWER", StringComparison.OrdinalIgnoreCase) ? value <= target : value >= target;
                string icon = met switch { true => "✅", false => "⚠️", _ => "•" };
                lines.Add($"{icon} {m.Definition.MetricName ?? m.Definition.MetricCode}: {(value == null ? "chưa có" : value.Value.ToString("#,0.##"))}"
                    + (target == null ? string.Empty : $" / mục tiêu {target.Value:#,0.##}") + (m.Definition.UnitCode is { } u ? " " + u : string.Empty));
            }
            if (lines.Count == 0)
            {
                lines.Add("Chưa có số liệu KPI cho kỳ này (job KPI chưa chạy hoặc chưa khai báo chỉ tiêu).");
            }
            return lines;
        }

        private Task<int> SendAsync(string title, List<string> lines, string key)
            => BusinessNotifier.ToMenusAsync(notifications, unitOfWork.DbContext, NotificationCatalog.PeriodReport, [BotReports.ExecutiveMenu],
                new { title, lines }, key, buttons: [new NotificationButton("🎯 KPI", "sc:run:dh_kpi"), new NotificationButton("📅 Chỉ số tháng", "sc:run:dh_kpi_month")],
                botCode: MessagingCode.BotDieuHanh);
    }

    /// <summary>
    /// BOT-2.3 (§5.1 ORDERS_UNCONFIRMED – thay phần gửi tin của T23.2 RemindUnconfirmedOrders): 19:00 – đơn ngày mai còn Nháp / Đã gửi (chưa xác nhận)
    /// → 🔴 NV KD phụ trách khách (Customer.UserAssignId), 1 tin / người / ngày, kèm danh sách; khách chưa có người phụ trách → người có quyền Thêm trên đơn hàng.
    /// Khách CHƯA có đơn ngày mai không tính (cần lịch giao theo hợp đồng – chờ BA).
    /// </summary>
    public sealed class RemindUnconfirmedOrdersJob(IUnitOfWork unitOfWork, INotificationService notifications) : IScheduledJob
    {
        public string Code => "RemindUnconfirmedOrders";
        public string Name => "Nhắc khách chưa chốt đơn";
        public string Description => "19:00 – đơn ngày mai chưa xác nhận → nhắc NV kinh doanh phụ trách khách qua bot.";
        public string DefaultAt => "19:00";

        public async Task<JobOutcome> RunAsync(DateTime now, CancellationToken cancellationToken)
        {
            var db = unitOfWork.DbContext;
            DateTime tomorrow = now.Date.AddDays(1);
            var rows = await (from o in db.Order.AsNoTracking()
                              join c in db.Customer.AsNoTracking() on o.CustomerId equals c.Id
                              where !o.IsDeleted && o.MealDate >= tomorrow && o.MealDate < tomorrow.AddDays(1)
                                    && (o.Status == OrderStatus.Draft || o.Status == OrderStatus.Submitted)
                              orderby c.Name
                              select new { No = o.OrderNo ?? o.OrderNumber, o.Status, o.TotalQuantity, Customer = c.Name, c.UserAssignId })
                             .ToListAsync(cancellationToken);
            int sent = 0;
            foreach (var g in rows.GroupBy(r => r.UserAssignId))
            {
                var data = new
                {
                    title = $"{g.Select(r => r.Customer).Distinct().Count()} khách chưa chốt đơn ngày {tomorrow:dd/MM}",
                    detail = "Liên hệ khách xác nhận trước giờ khóa đơn.",
                    lines = g.Take(20).Select(r => $"• {r.Customer}: {r.No} – {OrderStatus.Label(r.Status)} – {r.TotalQuantity:#,0.##} suất").ToList(),
                };
                string key = $"{NotificationCatalog.OrdersUnconfirmed}:{tomorrow:yyyyMMdd}:{(g.Key is { } u ? u.ToString("N") : "none")}";
                sent += g.Key is { } user
                    ? await BusinessNotifier.SafeEnqueueAsync(notifications, new NotificationRequest { Template = NotificationCatalog.OrdersUnconfirmed, UserIds = [user], Data = data, IdempotencyKey = key })
                    : await BusinessNotifier.ToMenusAsync(notifications, db, NotificationCatalog.OrdersUnconfirmed, ["#/crm-order"], data, key, requireAdd: true);
            }
            return new JobOutcome(JobRunStatus.Succeeded, sent, tomorrow, $"{rows.Count} đơn chưa chốt, {sent} tin");
        }
    }

    /// <summary>
    /// BOT-2.3 (ROADMAP IngredientsTonight): 21:00 (sau MRP 20:30) – thủ kho nhận tóm tắt nguyên liệu cho sản xuất ngày mai: số nguyên liệu cần, còn phải mua
    /// (MaterialRequirement.PurchaseQuantity &gt; 0) và số PO hẹn giao ngày mai – để chuẩn bị xuất kho / nhận hàng sáng sớm. Chưa có kế hoạch ngày mai → không gửi.
    /// </summary>
    public sealed class IngredientsTonightJob(IUnitOfWork unitOfWork, INotificationService notifications) : IScheduledJob
    {
        public string Code => "IngredientsTonight";
        public string Name => "Nguyên liệu cho ngày mai (thủ kho)";
        public string Description => "21:00 – tóm tắt nhu cầu nguyên liệu ngày mai (MRP), phần còn thiếu, PO hẹn giao ngày mai → thủ kho qua bot.";
        public string DefaultAt => "21:00";

        public async Task<JobOutcome> RunAsync(DateTime now, CancellationToken cancellationToken)
        {
            var db = unitOfWork.DbContext;
            DateTime tomorrow = now.Date.AddDays(1);
            var plans = await db.ProductionPlan.AsNoTracking().Where(p => !p.IsDeleted && p.ProductionDate >= tomorrow && p.ProductionDate < tomorrow.AddDays(1))
                .Select(p => p.Id).ToListAsync(cancellationToken);
            if (plans.Count == 0)
            {
                return new JobOutcome(JobRunStatus.Skipped, 0, tomorrow, "Chưa có kế hoạch sản xuất ngày mai");
            }
            var items = await (from m in db.MaterialRequirement.AsNoTracking()
                               join i in db.Ingredient.AsNoTracking() on m.IngredientId equals i.Id
                               join u in db.UnitMeasure.AsNoTracking() on m.UnitId equals u.Id into uu
                               from u in uu.DefaultIfEmpty()
                               where !m.IsDeleted && plans.Contains(m.ProductionPlanId)
                               select new { i.Name, Unit = u != null ? (u.Symbol ?? u.Name) : null, m.RequiredQuantity, m.PurchaseQuantity })
                              .ToListAsync(cancellationToken);
            var pos = await db.PurchaseOrder.AsNoTracking()
                .Where(p => !p.IsDeleted && (p.Status == PurchaseOrderStatus.Approved || p.Status == PurchaseOrderStatus.Sent || p.Status == PurchaseOrderStatus.PartiallyReceived)
                            && (p.ExpectedDeliveryDate ?? p.ExpectedDate) >= tomorrow && (p.ExpectedDeliveryDate ?? p.ExpectedDate) < tomorrow.AddDays(1))
                .CountAsync(cancellationToken);
            var shortage = items.Where(i => i.PurchaseQuantity > 0).OrderByDescending(i => i.PurchaseQuantity).ToList();
            var lines = new List<string> { $"🥬 Cần {items.Select(i => i.Name).Distinct().Count()} nguyên liệu", $"🧾 {pos} PO hẹn giao ngày mai" };
            lines.Add(shortage.Count == 0 ? "✅ Đủ nguyên liệu (theo MRP)" : $"⚠️ Còn thiếu {shortage.Count} nguyên liệu:");
            lines.AddRange(shortage.Take(10).Select(i => $"• {i.Name}: thiếu {i.PurchaseQuantity:0.##} {i.Unit}"));
            int sent = await BusinessNotifier.ToMenusAsync(notifications, db, NotificationCatalog.IngredientsTonight, ["#/scm-issue", "#/scm-requirement"],
                new { title = $"Sản xuất ngày {tomorrow:dd/MM}", lines }, $"{NotificationCatalog.IngredientsTonight}:{tomorrow:yyyyMMdd}", requireAdd: true);
            return new JobOutcome(JobRunStatus.Succeeded, sent, tomorrow, $"{items.Count} dòng nhu cầu, thiếu {shortage.Count}, {sent} tin");
        }
    }

    /// <summary>BOT-2.3: câu chữ dùng chung cho bản tin / báo cáo (số liệu từ Dashboard/GetExecutive).</summary>
    public static class BotReports
    {
        /// <summary>Người nhận bản tin / báo cáo = người có mục Dashboard điều hành (PROPOSED – CEO / COO / PMO / trưởng phòng theo cấp).</summary>
        public const string ExecutiveMenu = "#/dash-exec";

        public static IEnumerable<string> Result(DashboardKpiModel k)
        {
            yield return $"🚚 Giao: {k.Delivery.Delivered}/{k.Delivery.Total} điểm";
            yield return $"🍳 Sản xuất: {(k.ProductionProgress.Percent is { } p ? $"{p:0.#}%" : "–")} ({k.ProductionProgress.DonePortions:#,0}/{k.ProductionProgress.PlannedPortions:#,0} suất, {k.ProductionProgress.BatchCount} mẻ)";
            yield return $"🧪 QC: {k.QcDetail.BatchHold} mẻ giữ · {k.QcDetail.BatchReject} mẻ loại · {k.QcDetail.LotOnHold} lô giữ";
            yield return $"📣 Phản ánh đang mở: {k.OpenComplaints} · 📦 Cảnh báo tồn: {k.StockAlerts}";
        }

        public static string Plan(DashboardKpiModel k)
            => $"🍱 {k.PlannedMeals:#,0} suất · {k.Orders.Total} đơn · {k.Delivery.Total} điểm giao · {k.Staffing.Assigned} người có ca";
    }
}
