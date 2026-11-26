using AppDelicare.Data.Interface;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Services.Implement;
using AppDelicare.Services.Implement.Messaging;
using AppDelicare.Services.Interface;

namespace AppDelicare.Services.Jobs
{
    /// <summary>
    /// ACC-2 (D-131 e): 07:30 hằng ngày – bot Điều hành gửi Kế toán (FIN-ACC) tóm tắt chứng từ kế toán của tháng này (và tháng trước tới ngày 10 – hạn khóa kỳ):
    /// bao nhiêu sẵn sàng chưa gửi, chờ lập trên AMIS, cần xử lý (lỗi / sửa sau khi gửi). Không có gì cần làm → không gửi. Chạy lại cùng ngày = 0 tin.
    /// </summary>
    public sealed class AccountingVoucherDigestJob(IAccountingVoucherService vouchers, IUnitOfWork unitOfWork, INotificationService notifications) : IScheduledJob
    {
        public string Code => "AccountingVoucherDigest";
        public string Name => "Tóm tắt chứng từ kế toán";
        public string Description => "07:30 hằng ngày – báo Kế toán số chứng từ AMIS sẵn sàng chưa gửi, chờ lập, cần xử lý (tháng này + tháng trước tới ngày 10).";
        public string DefaultAt => "07:30";

        public async Task<JobOutcome> RunAsync(DateTime now, CancellationToken cancellationToken)
        {
            DateTime today = now.Date;
            var months = new List<DateTime> { new(today.Year, today.Month, 1) };
            if (today.Day <= 10)
            {
                months.Insert(0, months[0].AddMonths(-1));
            }
            var parts = new List<string>();
            foreach (var m in months)
            {
                var d = (await vouchers.GetMonth(m.ToString("yyyy-MM"), "all")).Data;
                if (d == null)
                {
                    continue;
                }
                int waiting = d.ListVoucher.Count(v => v.Status == VoucherStatus.Waiting);
                if (d.ReadyCount + waiting + d.NeedsAction == 0)
                {
                    continue;
                }
                parts.Add($"Tháng {m:MM/yyyy}: {d.ReadyCount} sẵn sàng chưa gửi, {waiting} chờ lập trên AMIS, {d.NeedsAction} cần xử lý"
                    + (d.NeedsAction > 0 ? $" ({d.ErrorCount} lỗi, {d.ChangedCount} sửa sau khi gửi)" : string.Empty));
            }
            if (parts.Count == 0)
            {
                return new JobOutcome(JobRunStatus.Succeeded, 0, today, "Không có chứng từ cần làm");
            }
            string text = "📒 Chứng từ kế toán (MISA AMIS)\n" + string.Join("\n", parts) + "\nMở Kế toán › Chứng từ kế toán để xử lý / tải Excel mẫu AMIS.";
            var users = await RecipientResolver.UsersInGroupsAsync(unitOfWork.DbContext, ["FIN-ACC"]);
            int sent = users.Count == 0 ? 0 : await BusinessNotifier.SafeEnqueueAsync(notifications, new NotificationRequest
            {
                Template = NotificationCatalog.ManualText, UserIds = [.. users], BotCode = MessagingCode.BotDieuHanh, Data = new { text }, IdempotencyKey = $"ACC_VOUCHER_DIGEST:{today:yyyyMMdd}",
            });
            return new JobOutcome(JobRunStatus.Succeeded, sent, today, string.Join(" · ", parts) + $" – {sent} tin");
        }
    }
}
