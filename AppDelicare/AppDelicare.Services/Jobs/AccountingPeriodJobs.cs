using AppDelicare.Data.Interface;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Services.Implement;
using AppDelicare.Services.Implement.Messaging;
using AppDelicare.Services.Interface;
using Microsoft.Extensions.Options;

namespace AppDelicare.Services.Jobs
{
    /// <summary>
    /// ACC-4 (D-131 h): 08:00 hằng ngày từ ngày <c>Integration:Misa:CloseRemindFromDay</c> (5) – kỳ tháng trước chưa khóa → bot Điều hành nhắc Kế toán
    /// (FIN-ACC): hạn khóa, số việc còn lại. Đã khóa / chưa tới ngày → không gửi. Chạy lại cùng ngày = 0 tin.
    /// </summary>
    public sealed class AccountingPeriodReminderJob(IAccountingPeriodService periods, IUnitOfWork unitOfWork, INotificationService notifications, IOptions<MisaOptions> misa) : IScheduledJob
    {
        public string Code => "AccountingPeriodReminder";
        public string Name => "Nhắc khóa kỳ kế toán";
        public string Description => "08:00 từ ngày 5 – kỳ tháng trước chưa khóa thì nhắc Kế toán hạn khóa và số việc còn lại.";
        public string DefaultAt => "08:00";

        public async Task<JobOutcome> RunAsync(DateTime now, CancellationToken cancellationToken)
        {
            DateTime today = now.Date;
            if (today.Day < Math.Clamp(misa.Value.CloseRemindFromDay, 1, 28))
            {
                return new JobOutcome(JobRunStatus.Succeeded, 0, today, "Chưa tới ngày nhắc");
            }
            var prev = new DateTime(today.Year, today.Month, 1).AddMonths(-1);
            var p = (await periods.GetOverview(prev.ToString("yyyy-MM"))).Data;
            if (p == null || p.Locked)
            {
                return new JobOutcome(JobRunStatus.Succeeded, 0, prev, "Kỳ trước đã khóa");
            }
            string left = p.Remaining == 0 ? "đủ điều kiện – khóa kỳ ngay" : "còn " + p.Remaining + " việc: " + string.Join("; ", p.ListCondition.Where(c => !c.Ok).Select(c => c.Title + " (" + c.Detail + ")"));
            string text = $"🔒 Kỳ kế toán tháng {prev:MM/yyyy} chưa khóa – hạn {p.DueDate:dd/MM}" + (today > p.DueDate ? " (đã quá hạn)" : string.Empty) + $": {left}.\nMở Kế toán › Khóa kỳ.";
            var users = await RecipientResolver.UsersInGroupsAsync(unitOfWork.DbContext, ["FIN-ACC"]);
            int sent = users.Count == 0 ? 0 : await BusinessNotifier.SafeEnqueueAsync(notifications, new NotificationRequest
            {
                Template = NotificationCatalog.ManualText, UserIds = [.. users], BotCode = MessagingCode.BotDieuHanh, Data = new { text }, IdempotencyKey = $"ACC_PERIOD_REMIND:{prev:yyyyMM}:{today:yyyyMMdd}",
            });
            return new JobOutcome(JobRunStatus.Succeeded, sent, prev, $"Kỳ {prev:MM/yyyy}: {left} – {sent} tin");
        }
    }
}
