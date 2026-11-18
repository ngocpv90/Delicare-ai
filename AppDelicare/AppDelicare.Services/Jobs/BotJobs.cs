using AppDelicare.Data.Interface;
using AppDelicare.Data.Share;
using AppDelicare.Data.Interface;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Services.Implement.Messaging;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Jobs
{
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
}
