using AppDelicare.Data.Interface;
using AppDelicare.Data.Share;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Services.Implement.Messaging;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AppDelicare.Services.Jobs
{
    // BIZ-3.4 (D-77 – C11, C12, anh chốt 2026-11-23): thực đơn tự hết hiệu lực + nhắc thực đơn chung / đăng ký suất tuần sau.
    // Giờ chạy: Jobs:<Code>; thứ chạy: BusinessRules:MenuReminderWeekday (3 = thứ Tư) / OrderWeekReminderWeekday (5 = thứ Sáu).

    /// <summary>00:30 – thực đơn Đã công bố hết ngày hiệu lực cuối (hiệu lực đến; trống = hiệu lực từ + 6) → Hết hiệu lực (cùng lúc hợp đồng hết hạn).</summary>
    public sealed class MenuExpiryJob(IUnitOfWork unitOfWork) : IScheduledJob
    {
        public string Code => "MenuExpiry";
        public string Name => "Thực đơn hết hiệu lực";
        public string Description => "Thực đơn Đã công bố đã qua ngày hiệu lực cuối (trống ngày đến = hiệu lực từ + 6 ngày) → Hết hiệu lực.";
        public string DefaultAt => "00:30";

        public async Task<JobOutcome> RunAsync(DateTime now, CancellationToken cancellationToken)
        {
            DateTime today = now.Date;
            var menus = unitOfWork.MenuRepository.GetAll()
                .Where(m => !m.IsDeleted && m.Status == MenuStatus.Published && m.EffectiveFrom != null).ToList()
                .Where(m => (m.EffectiveTo ?? m.EffectiveFrom!.Value.AddDays(6)).Date < today).ToList();
            foreach (var menu in menus)
            {
                menu.Status = MenuStatus.Expired;
                menu.Note = string.IsNullOrWhiteSpace(menu.Note) ? $"Hệ thống tự Hết hiệu lực {today:dd/MM/yyyy}" : $"{menu.Note}\nHệ thống tự Hết hiệu lực {today:dd/MM/yyyy}";
                unitOfWork.MenuRepository.Update(menu);
            }
            if (menus.Count != 0)
            {
                await unitOfWork.MenuRepository.CommitAsync(ConstantHelper.TypeSave.Update);
            }
            return new JobOutcome(JobRunStatus.Succeeded, menus.Count, today,
                menus.Count == 0 ? "Không có thực đơn hết hiệu lực" : $"{menus.Count} thực đơn → Hết hiệu lực: {string.Join(", ", menus.Select(m => m.Code).Take(20))}");
        }
    }

    /// <summary>09:00 thứ Tư – chưa có thực đơn chung Đã công bố cho tuần sau → bot 🔴 Kế hoạch (người làm thực đơn) + COO.</summary>
    public sealed class MenuWeekReminderJob(IUnitOfWork unitOfWork, INotificationService notifications, IOptions<BusinessRulesOptions> rules) : IScheduledJob
    {
        public string Code => "MenuWeekReminder";
        public string Name => "Nhắc thực đơn chung tuần sau";
        public string Description => "09:00 thứ Tư – chưa có thực đơn chung Đã công bố phủ tuần sau → bot Kế hoạch + COO.";
        public string DefaultAt => "09:00";

        public async Task<JobOutcome> RunAsync(DateTime now, CancellationToken cancellationToken)
        {
            DateTime today = now.Date;
            if ((int)today.DayOfWeek != rules.Value.MenuReminderWeekday % 7)
            {
                return new JobOutcome(JobRunStatus.Succeeded, 0, today, "Không phải ngày nhắc");
            }
            DateTime monday = WeekRules.NextMonday(today), sunday = monday.AddDays(6);
            var menus = await unitOfWork.DbContext.Menu.AsNoTracking()
                .Where(m => !m.IsDeleted && m.MenuType == MenuScope.Common && m.Status == MenuStatus.Published && m.EffectiveFrom != null && m.EffectiveFrom <= sunday)
                .ToListAsync(cancellationToken);
            if (menus.Any(m => (m.EffectiveTo ?? m.EffectiveFrom!.Value.AddDays(6)).Date >= monday))
            {
                return new JobOutcome(JobRunStatus.Succeeded, 0, monday, "Đã có thực đơn chung tuần sau");
            }
            var db = unitOfWork.DbContext;
            var users = new HashSet<Guid>(await RecipientResolver.UsersWithMenuOnlyAsync(db, "#/menu-menu", true));
            users.UnionWith(await RecipientResolver.UsersInGroupsAsync(db, ["COO"]));
            int sent = await BusinessNotifier.SafeEnqueueAsync(notifications, new NotificationRequest
            {
                Template = NotificationCatalog.MenuMissing, UserIds = [.. users],
                Data = new { title = $"🔴 Chưa có thực đơn chung tuần {monday:dd/MM} – {sunday:dd/MM}", detail = "Lập và công bố thực đơn chung để khách đăng ký suất tuần sau.", lines = new List<string>() },
                IdempotencyKey = $"{NotificationCatalog.MenuMissing}:{monday:yyyyMMdd}",
            });
            return new JobOutcome(JobRunStatus.Succeeded, sent, monday, $"Chưa có thực đơn chung tuần {monday:dd/MM} – {sent} tin");
        }
    }

    /// <summary>16:00 thứ Sáu – khách có hợp đồng hiệu lực tuần sau mà chưa có đơn / đăng ký nào tuần sau → bot NV KD phụ trách (chưa gán → người có mục đơn đặt suất).</summary>
    public sealed class OrderWeekReminderJob(IUnitOfWork unitOfWork, INotificationService notifications, IOptions<BusinessRulesOptions> rules) : IScheduledJob
    {
        public string Code => "OrderWeekReminder";
        public string Name => "Nhắc khách chưa đăng ký suất tuần sau";
        public string Description => "16:00 thứ Sáu – khách có hợp đồng hiệu lực chưa gửi đăng ký suất tuần sau → NV KD phụ trách qua bot.";
        public string DefaultAt => "16:00";

        public async Task<JobOutcome> RunAsync(DateTime now, CancellationToken cancellationToken)
        {
            DateTime today = now.Date;
            if ((int)today.DayOfWeek != rules.Value.OrderWeekReminderWeekday % 7)
            {
                return new JobOutcome(JobRunStatus.Succeeded, 0, today, "Không phải ngày nhắc");
            }
            var db = unitOfWork.DbContext;
            DateTime monday = WeekRules.NextMonday(today), end = monday.AddDays(7);
            var customers = await (from ct in db.Contract.AsNoTracking()
                                   join c in db.Customer.AsNoTracking() on ct.CustomerId equals c.Id
                                   where !ct.IsDeleted && !c.IsDeleted && ct.Status == ContractStatus.Active && ct.EffectiveFrom < end && (ct.EffectiveTo == null || ct.EffectiveTo >= monday)
                                   select new { c.Id, c.Name, c.DisplayName, c.UserAssignId }).Distinct().ToListAsync(cancellationToken);
            var ids = customers.Select(c => c.Id).ToList();
            var ordered = (await db.Order.AsNoTracking().Where(o => !o.IsDeleted && ids.Contains(o.CustomerId) && o.MealDate >= monday && o.MealDate < end && o.Status != OrderStatus.Cancelled)
                .Select(o => o.CustomerId).Distinct().ToListAsync(cancellationToken)).ToHashSet();
            var missing = customers.Where(c => !ordered.Contains(c.Id)).OrderBy(c => c.DisplayName ?? c.Name).ToList();
            int sent = 0;
            foreach (var g in missing.GroupBy(c => c.UserAssignId))
            {
                var data = new
                {
                    title = $"{g.Count()} khách chưa đăng ký suất tuần {monday:dd/MM} – {monday.AddDays(6):dd/MM}",
                    detail = "Liên hệ khách gửi đăng ký suất tuần sau (Đăng ký suất tuần).",
                    lines = g.Take(30).Select(c => "• " + (c.DisplayName ?? c.Name)).ToList(),
                };
                string key = $"{NotificationCatalog.OrderWeekMissing}:{monday:yyyyMMdd}:{(g.Key is { } u ? u.ToString("N") : "none")}";
                sent += g.Key is { } user
                    ? await BusinessNotifier.SafeEnqueueAsync(notifications, new NotificationRequest { Template = NotificationCatalog.OrderWeekMissing, UserIds = [user], Data = data, IdempotencyKey = key })
                    : await BusinessNotifier.ToMenusAsync(notifications, db, NotificationCatalog.OrderWeekMissing, ["#/crm-order-week"], data, key, requireAdd: true);
            }
            return new JobOutcome(JobRunStatus.Succeeded, sent, monday, $"{missing.Count} khách chưa đăng ký tuần {monday:dd/MM}, {sent} tin");
        }
    }

    public static class WeekRules
    {
        /// <summary>Thứ Hai của tuần sau (hôm nay thứ Hai → thứ Hai tuần sau).</summary>
        public static DateTime NextMonday(DateTime today)
        {
            int days = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
            return today.Date.AddDays(days == 0 ? 7 : days);
        }
    }
}
