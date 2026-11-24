using AppDelicare.Data.Interface;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Services.Implement.Messaging;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AppDelicare.Services.Jobs
{
    // UI-6.5 (D-84 d, anh chốt 2026-11-23): vòng dự báo tuần tự động – thứ Tư 22:00 tự tạo gợi ý tuần sau → thứ Năm 08:00 báo KD →
    // thứ Sáu 12:00 nhắc nếu chưa công bố → thứ Sáu 16:00 tự công bố bản Nháp. Giờ: Jobs:<Code>; thứ: BusinessRules:Forecast*Weekday;
    // bật / tắt: BusinessRules:ForecastAutoSuggest / ForecastAutoPublish. Người nhận: NV KD phụ trách khách (Customer.UserAssignId) + Trưởng KD (SAL-HEAD).

    internal static class ForecastJobRules
    {
        public static bool IsDay(DateTime today, int weekday) => (int)today.DayOfWeek == weekday % 7;

        /// <summary>NV KD phụ trách các khách có dòng trong tuần + nhóm SAL-HEAD.</summary>
        public static async Task<List<Guid>> RecipientsAsync(IUnitOfWork unitOfWork, IEnumerable<Guid> customerIds)
        {
            var db = unitOfWork.DbContext;
            var ids = customerIds.Distinct().ToList();
            var users = new HashSet<Guid>(await db.Customer.AsNoTracking().Where(c => ids.Contains(c.Id) && c.UserAssignId != null).Select(c => c.UserAssignId!.Value).ToListAsync());
            users.UnionWith(await RecipientResolver.UsersInGroupsAsync(db, ["SAL-HEAD"]));
            return [.. users];
        }

        public static Task<int> SendAsync(INotificationService notifications, List<Guid> users, string text, string key)
            => users.Count == 0 ? Task.FromResult(0) : BusinessNotifier.SafeEnqueueAsync(notifications, new NotificationRequest
            {
                Template = NotificationCatalog.ManualText, UserIds = users, BotCode = MessagingCode.BotDieuHanh, Data = new { text }, IdempotencyKey = key,
            });
    }

    /// <summary>22:00 thứ Tư – tự tạo gợi ý dự báo tuần sau cho mọi khách có hợp đồng hiệu lực (giữ ô người đã sửa).</summary>
    public sealed class ForecastAutoSuggestJob(IForecastService forecasts, IOptions<BusinessRulesOptions> rules) : IScheduledJob
    {
        public string Code => "ForecastAutoSuggest";
        public string Name => "Tự tạo gợi ý dự báo tuần sau";
        public string Description => "22:00 thứ Tư – tạo gợi ý dự báo tuần sau (trung bình 4 tuần cùng thứ) cho mọi khách có hợp đồng hiệu lực; ô người đã sửa giữ nguyên.";
        public string DefaultAt => "22:00";

        public async Task<JobOutcome> RunAsync(DateTime now, CancellationToken cancellationToken)
        {
            DateTime today = now.Date;
            if (!rules.Value.ForecastAutoSuggest || !ForecastJobRules.IsDay(today, rules.Value.ForecastSuggestWeekday))
            {
                return new JobOutcome(JobRunStatus.Succeeded, 0, today, "Không phải ngày tạo gợi ý (hoặc đã tắt)");
            }
            DateTime monday = WeekRules.NextMonday(today);
            var r = await forecasts.SuggestWeek(new ForecastWeekActionRequest { WeekStart = monday });
            var d = r.Data;
            return new JobOutcome(JobRunStatus.Succeeded, d == null ? 0 : d.Created + d.Refreshed, monday,
                d == null ? r.ResponseMessage ?? "Không có khách có hợp đồng hiệu lực" : $"Tuần {monday:dd/MM}: tạo {d.Created}, cập nhật {d.Refreshed}, giữ ô đã sửa {d.SkippedEdited}, thiếu dữ liệu {d.InsufficientData}");
        }
    }

    /// <summary>08:00 thứ Năm – bot KD phụ trách + Trưởng KD: "Dự báo tuần … đã sẵn – N ô cần xem, M ô thiếu dữ liệu".</summary>
    public sealed class ForecastReadyNoticeJob(IForecastService forecasts, IUnitOfWork unitOfWork, INotificationService notifications, IOptions<BusinessRulesOptions> rules) : IScheduledJob
    {
        public string Code => "ForecastReadyNotice";
        public string Name => "Báo dự báo tuần sau đã sẵn";
        public string Description => "08:00 thứ Năm – báo KD phụ trách + Trưởng KD: dự báo tuần sau đã có gợi ý, số ô cần xem / thiếu dữ liệu.";
        public string DefaultAt => "08:00";

        public async Task<JobOutcome> RunAsync(DateTime now, CancellationToken cancellationToken)
        {
            DateTime today = now.Date;
            if (!rules.Value.ForecastAutoSuggest || !ForecastJobRules.IsDay(today, rules.Value.ForecastNoticeWeekday))
            {
                return new JobOutcome(JobRunStatus.Succeeded, 0, today, "Không phải ngày báo (hoặc đã tắt)");
            }
            DateTime monday = WeekRules.NextMonday(today);
            var w = (await forecasts.GetWeek(new ForecastWeekRequest { WeekStart = monday })).Data;
            if (w == null || w.ListCustomer.Count == 0)
            {
                return new JobOutcome(JobRunStatus.Succeeded, 0, monday, "Không có khách có hợp đồng hiệu lực tuần sau");
            }
            var users = await ForecastJobRules.RecipientsAsync(unitOfWork, w.ListCustomer.Select(c => c.CustomerId));
            string text = $"📊 Dự báo tuần {monday:dd/MM} – {monday.AddDays(6):dd/MM} đã sẵn: {w.DraftCells} ô Nháp cần xem, {w.MissingCells} ô thiếu dữ liệu, {w.ListCustomer.Count} khách.\n"
                + $"Mở màn Dự báo suất ăn để sửa ô cần rồi Công bố trước 16:00 thứ Sáu (quá giờ hệ thống tự công bố).";
            int sent = await ForecastJobRules.SendAsync(notifications, users, text, $"FORECAST_READY:{monday:yyyyMMdd}");
            return new JobOutcome(JobRunStatus.Succeeded, sent, monday, $"Tuần {monday:dd/MM}: {w.DraftCells} ô Nháp, {w.MissingCells} thiếu dữ liệu – {sent} tin");
        }
    }

    /// <summary>12:00 thứ Sáu – tuần sau còn ô Nháp chưa công bố → bot nhắc KD phụ trách + Trưởng KD.</summary>
    public sealed class ForecastPublishReminderJob(IForecastService forecasts, IUnitOfWork unitOfWork, INotificationService notifications, IOptions<BusinessRulesOptions> rules) : IScheduledJob
    {
        public string Code => "ForecastPublishReminder";
        public string Name => "Nhắc công bố dự báo tuần sau";
        public string Description => "12:00 thứ Sáu – còn ô dự báo Nháp tuần sau chưa công bố → nhắc KD phụ trách + Trưởng KD (16:00 hệ thống tự công bố).";
        public string DefaultAt => "12:00";

        public async Task<JobOutcome> RunAsync(DateTime now, CancellationToken cancellationToken)
        {
            DateTime today = now.Date;
            if (!rules.Value.ForecastAutoPublish || !ForecastJobRules.IsDay(today, rules.Value.ForecastPublishWeekday))
            {
                return new JobOutcome(JobRunStatus.Succeeded, 0, today, "Không phải ngày nhắc (hoặc đã tắt)");
            }
            DateTime monday = WeekRules.NextMonday(today);
            var w = (await forecasts.GetWeek(new ForecastWeekRequest { WeekStart = monday })).Data;
            if (w == null || w.DraftCells == 0)
            {
                return new JobOutcome(JobRunStatus.Succeeded, 0, monday, "Không còn ô Nháp chưa công bố");
            }
            var custs = w.ListCustomer.Where(c => c.ListRow.Any(r => r.ListCell.Any(x => x.Status == "DRAFT"))).ToList();
            var users = await ForecastJobRules.RecipientsAsync(unitOfWork, custs.Select(c => c.CustomerId));
            string text = $"⏰ Dự báo tuần {monday:dd/MM} còn {w.DraftCells} ô Nháp chưa công bố ({custs.Count} khách). 16:00 hôm nay hệ thống sẽ tự công bố bản Nháp.\n"
                + string.Join("\n", custs.Take(15).Select(c => "• " + c.CustomerName));
            int sent = await ForecastJobRules.SendAsync(notifications, users, text, $"FORECAST_REMIND:{monday:yyyyMMdd}");
            return new JobOutcome(JobRunStatus.Succeeded, sent, monday, $"Tuần {monday:dd/MM}: {w.DraftCells} ô Nháp – {sent} tin");
        }
    }

    /// <summary>16:00 thứ Sáu – vẫn còn Nháp tuần sau → hệ thống tự công bố ("Hệ thống tự công bố"), ô thiếu dữ liệu để trống và báo KD.</summary>
    public sealed class ForecastAutoPublishJob(IForecastService forecasts, IUnitOfWork unitOfWork, INotificationService notifications, IOptions<BusinessRulesOptions> rules) : IScheduledJob
    {
        public string Code => "ForecastAutoPublish";
        public string Name => "Tự công bố dự báo tuần sau";
        public string Description => "16:00 thứ Sáu – công bố mọi ô dự báo Nháp tuần sau (ghi \u201cHệ thống tự công bố\u201d); ô thiếu dữ liệu để trống, báo KD danh sách ô trống.";
        public string DefaultAt => "16:00";

        public async Task<JobOutcome> RunAsync(DateTime now, CancellationToken cancellationToken)
        {
            DateTime today = now.Date;
            if (!rules.Value.ForecastAutoPublish || !ForecastJobRules.IsDay(today, rules.Value.ForecastPublishWeekday))
            {
                return new JobOutcome(JobRunStatus.Succeeded, 0, today, "Không phải ngày tự công bố (hoặc đã tắt)");
            }
            DateTime monday = WeekRules.NextMonday(today);
            var r = (await forecasts.PublishWeek(new ForecastWeekActionRequest { WeekStart = monday }, auto: true)).Data;
            if (r == null || (r.Published == 0 && r.Missing == 0))
            {
                return new JobOutcome(JobRunStatus.Succeeded, 0, monday, "Không còn ô Nháp / ô trống tuần sau");
            }
            var w = (await forecasts.GetWeek(new ForecastWeekRequest { WeekStart = monday })).Data;
            var users = await ForecastJobRules.RecipientsAsync(unitOfWork, w?.ListCustomer.Select(c => c.CustomerId) ?? []);
            string text = $"✅ Hệ thống đã tự công bố {r.Published} ô dự báo tuần {monday:dd/MM}."
                + (r.Missing > 0 ? $"\n⚠️ {r.Missing} ô thiếu dữ liệu để trống – nhập tay ở màn Dự báo suất ăn:\n" + string.Join("\n", r.ListMissing.Take(15).Select(x => "• " + x)) : string.Empty);
            int sent = await ForecastJobRules.SendAsync(notifications, users, text, $"FORECAST_AUTOPUB:{monday:yyyyMMdd}");
            return new JobOutcome(JobRunStatus.Succeeded, r.Published, monday, $"Tuần {monday:dd/MM}: tự công bố {r.Published}, bỏ qua đã đăng ký {r.SkippedRegistered}, thiếu dữ liệu {r.Missing} – {sent} tin");
        }
    }
}
