using AppDelicare.Data.Interface;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Services.Implement;
using AppDelicare.Services.Implement.Messaging;
using AppDelicare.Services.Interface;

namespace AppDelicare.Services.Jobs
{
    // UI-6.6 (D-85 c – anh chốt 2026-11-23, phương án a): điểm giao chưa có số lúc chốt → KHÔNG tự tạo đơn (tiền tính theo số khách ký nhận).
    // 18:00 nhắc KD phụ trách các điểm chưa có số ngày mai; 20:05 (sau LockOrders 20:00) vẫn chưa có → bot đỏ KD + Trưởng KD + COO kèm danh sách.

    internal static class OrderPointJobRules
    {
        public static string Lines(IEnumerable<(Guid CustomerId, string? CustomerName, Guid? UserAssignId, string? LocationName)> points, int take = 25)
        {
            var list = points.ToList();
            return string.Join("\n", list.Take(take).Select(p => $"• {p.CustomerName} · {p.LocationName}")) + (list.Count > take ? $"\n… và {list.Count - take} điểm khác" : string.Empty);
        }
    }

    /// <summary>18:00 – điểm giao có hợp đồng hiệu lực chưa có đơn ngày mai → nhắc NV KD phụ trách khách (bot Điều hành).</summary>
    public sealed class OrderMissingReminderJob(IUnitOfWork unitOfWork, INotificationService notifications) : IScheduledJob
    {
        public string Code => "OrderMissingReminder";
        public string Name => "Nhắc điểm giao chưa có số ngày mai";
        public string Description => "18:00 – điểm giao có hợp đồng hiệu lực chưa có đơn ngày mai → nhắc NV KD phụ trách (20:00 hệ thống chốt, không tự tạo đơn).";
        public string DefaultAt => "18:00";

        public async Task<JobOutcome> RunAsync(DateTime now, CancellationToken cancellationToken)
        {
            DateTime tomorrow = now.Date.AddDays(1);
            var points = await OrderService.PointsWithoutOrderAsync(unitOfWork, tomorrow);
            int sent = 0;
            foreach (var g in points.Where(p => p.UserAssignId != null).GroupBy(p => p.UserAssignId!.Value))
            {
                string text = $"⏰ {g.Count()} điểm giao chưa có số suất ngày {tomorrow:dd/MM} – 20:00 hệ thống chốt, không tự tạo đơn thay khách.\n"
                    + OrderPointJobRules.Lines(g) + "\nMở Đăng ký suất tuần để nhập số khách gửi.";
                sent += await BusinessNotifier.SafeEnqueueAsync(notifications, new NotificationRequest
                {
                    Template = NotificationCatalog.ManualText, UserIds = [g.Key], BotCode = MessagingCode.BotDieuHanh, Data = new { text },
                    IdempotencyKey = $"ORDER_MISSING_1800:{tomorrow:yyyyMMdd}:{g.Key:N}",
                });
            }
            int noOwner = points.Count(p => p.UserAssignId == null);
            return new JobOutcome(JobRunStatus.Succeeded, sent, tomorrow,
                $"Ngày {tomorrow:dd/MM}: {points.Count} điểm chưa có số ({noOwner} điểm khách chưa có NV phụ trách) – {sent} tin");
        }
    }

    /// <summary>20:05 – sau giờ chốt vẫn còn điểm chưa có số ngày mai → bot đỏ NV KD phụ trách + Trưởng KD (SAL-HEAD) + COO kèm danh sách, để gọi khách và nhập qua yêu cầu sửa sau chốt.</summary>
    public sealed class OrderMissingAtCutoffJob(IUnitOfWork unitOfWork, INotificationService notifications) : IScheduledJob
    {
        public string Code => "OrderMissingAtCutoff";
        public string Name => "Báo đỏ điểm giao chưa có số lúc chốt";
        public string Description => "20:05 – đã chốt mà điểm giao vẫn chưa có số ngày mai → báo đỏ KD phụ trách + Trưởng KD + COO kèm danh sách (không tự tạo đơn; nhập qua yêu cầu sửa sau chốt).";
        public string DefaultAt => "20:05";

        public async Task<JobOutcome> RunAsync(DateTime now, CancellationToken cancellationToken)
        {
            DateTime tomorrow = now.Date.AddDays(1);
            var points = await OrderService.PointsWithoutOrderAsync(unitOfWork, tomorrow);
            if (points.Count == 0)
            {
                return new JobOutcome(JobRunStatus.Succeeded, 0, tomorrow, $"Ngày {tomorrow:dd/MM}: mọi điểm giao đã có số");
            }
            var users = new HashSet<Guid>(points.Where(p => p.UserAssignId != null).Select(p => p.UserAssignId!.Value));
            users.UnionWith(await RecipientResolver.UsersInGroupsAsync(unitOfWork.DbContext, ["SAL-HEAD", "COO"]));
            string text = $"🔴 Đã chốt đơn ngày {tomorrow:dd/MM} nhưng {points.Count} điểm giao chưa có số suất – hệ thống KHÔNG tự tạo đơn.\n"
                + OrderPointJobRules.Lines(points) + "\nGọi khách lấy số rồi nhập qua \"Yêu cầu thay đổi sau giờ chốt\".";
            int sent = users.Count == 0 ? 0 : await BusinessNotifier.SafeEnqueueAsync(notifications, new NotificationRequest
            {
                Template = NotificationCatalog.ManualText, UserIds = [.. users], BotCode = MessagingCode.BotDieuHanh, Data = new { text },
                IdempotencyKey = $"ORDER_MISSING_CUTOFF:{tomorrow:yyyyMMdd}",
            });
            return new JobOutcome(JobRunStatus.Succeeded, sent, tomorrow, $"Ngày {tomorrow:dd/MM}: {points.Count} điểm chưa có số lúc chốt – {sent} tin");
        }
    }
}
