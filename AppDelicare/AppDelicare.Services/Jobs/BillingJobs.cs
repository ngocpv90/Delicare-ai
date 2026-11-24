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
    /// UI-6.9 (D-87 b): ngày <c>BusinessRules:BillingMonthNoticeDay</c> (1) hằng tháng 08:00 – bot Điều hành báo Kế toán (FIN-ACC) + Trưởng KD:
    /// "Bảng kê tháng MM/YYYY: X/Y khách sẵn sàng, Z khách còn N đơn chờ đối soát".
    /// </summary>
    public sealed class BillingMonthReadyJob(IBillingStatementService billing, IUnitOfWork unitOfWork, INotificationService notifications, IOptions<BusinessRulesOptions> rules) : IScheduledJob
    {
        public string Code => "BillingMonthReady";
        public string Name => "Báo bảng kê tháng trước";
        public string Description => "08:00 ngày 1 hằng tháng – báo Kế toán + Trưởng KD số khách có bảng kê tháng trước sẵn sàng / còn đơn chờ đối soát.";
        public string DefaultAt => "08:00";

        public async Task<JobOutcome> RunAsync(DateTime now, CancellationToken cancellationToken)
        {
            DateTime today = now.Date;
            if (today.Day != Math.Clamp(rules.Value.BillingMonthNoticeDay, 1, 28))
            {
                return new JobOutcome(JobRunStatus.Succeeded, 0, today, "Không phải ngày báo");
            }
            DateTime month = today.AddMonths(-1);
            var m = (await billing.GetMonthOverview(new BillingMonthRequest { Month = month })).Data;
            if (m == null)
            {
                return new JobOutcome(JobRunStatus.Succeeded, 0, month, "Không tính được bảng kê");
            }
            string text = $"🧾 Bảng kê tháng {month:MM/yyyy}: {m.ReadyCount}/{m.ActiveCount} khách sẵn sàng"
                + (m.PendingCustomers > 0 ? $", {m.PendingCustomers} khách còn {m.PendingOrders} đơn chờ đối soát" : string.Empty)
                + $". Tổng phải thu (có VAT) {m.GrandTotal:#,0} đ.\nMở màn Bảng kê thanh toán để kiểm tra và xuất hàng loạt.";
            var users = await RecipientResolver.UsersInGroupsAsync(unitOfWork.DbContext, ["FIN-ACC", "SAL-HEAD"]);
            int sent = users.Count == 0 ? 0 : await BusinessNotifier.SafeEnqueueAsync(notifications, new NotificationRequest
            {
                Template = NotificationCatalog.ManualText, UserIds = [.. users], BotCode = MessagingCode.BotDieuHanh, Data = new { text }, IdempotencyKey = $"BILLING_MONTH:{month:yyyyMM}",
            });
            return new JobOutcome(JobRunStatus.Succeeded, sent, month, $"Tháng {month:MM/yyyy}: {m.ReadyCount}/{m.ActiveCount} sẵn sàng, {m.PendingOrders} đơn chờ – {sent} tin");
        }
    }
}
