using AppDelicare.Model.Option;
using AppDelicare.Services.Interface;
using AppDelicare.Services.Jobs;
using Microsoft.Extensions.Options;

namespace AppDelicare.Services.Implement.PurchasePlan
{
    /// <summary>UI-6.51 (D-137 a): lần chạy 1 (mặc định 14:00) – lập / cập nhật đề nghị mua cho phần đến hạn đặt D+1..D+7, duyệt theo rủi ro, báo nguyên liệu tới hạn chưa có đơn mua.</summary>
    public sealed class PurchasePlanJob(IPurchasePlanService service, IOptions<PurchasePlanOptions> plan) : IScheduledJob
    {
        public string Code => "PurchasePlan";
        public string Name => "Kế hoạch mua tự động (lần 1)";
        public string Description => "Tính kế hoạch mua 7 ngày, lập / cập nhật đề nghị mua cho nguyên liệu đến hạn đặt (một đề nghị / ngày cần), đúng định mức tự duyệt + lập đơn mua, vượt định mức gửi Trưởng mua hàng; báo Mua hàng nguyên liệu tới hạn chưa có đơn.";
        public string DefaultAt => plan.Value.RunTime(0);

        public async Task<JobOutcome> RunAsync(DateTime now, CancellationToken cancellationToken)
        {
            if (!plan.Value.Enabled)
            {
                return new JobOutcome(JobRunStatus.Skipped, 0, now.Date, "Đã tắt (PurchasePlan:Enabled = false)");
            }
            var r = await service.RunJobAsync(true);
            return new JobOutcome(JobRunStatus.Succeeded, r.Created + r.Updated, now.Date, r.Summary);
        }
    }

    /// <summary>UI-6.51 (D-137 a): lần chạy 2 (mặc định 20:15 – sau chốt đơn 20:00, trước MrpJob 20:30): cập nhật theo đơn đã chốt; phần tăng thêm của đề nghị đã gửi duyệt → đề nghị bổ sung.</summary>
    public sealed class PurchasePlanEveningJob(IPurchasePlanService service, IOptions<PurchasePlanOptions> plan) : IScheduledJob
    {
        public string Code => "PurchasePlanEvening";
        public string Name => "Kế hoạch mua tự động (lần 2 – sau chốt đơn)";
        public string Description => "Như lần 1 sau khi chốt đơn ngày mai: cập nhật đề nghị Nháp, phần tăng thêm của đề nghị đã gửi duyệt lập đề nghị bổ sung; MrpJob 20:30 chỉ còn bù phần thiếu thêm của ngày mai.";
        public string DefaultAt => plan.Value.RunTime(1);

        public async Task<JobOutcome> RunAsync(DateTime now, CancellationToken cancellationToken)
        {
            if (!plan.Value.Enabled)
            {
                return new JobOutcome(JobRunStatus.Skipped, 0, now.Date, "Đã tắt (PurchasePlan:Enabled = false)");
            }
            var r = await service.RunJobAsync(false);
            return new JobOutcome(JobRunStatus.Succeeded, r.Created + r.Updated, now.Date, r.Summary);
        }
    }

    /// <summary>UI-6.51 (D-137 d): giờ gửi NCC (mặc định 15:00) – gửi đơn mua Đã duyệt của kế hoạch mua; lỗi / không kênh → chờ gửi tay + bot; đơn sắp giao chưa nhận → nhắc gọi NCC.</summary>
    public sealed class PurchasePlanSendJob(IPurchasePlanService service, IOptions<PurchasePlanOptions> plan) : IScheduledJob
    {
        public string Code => "PurchasePlanSend";
        public string Name => "Gửi đơn mua cho NCC";
        public string Description => "Gửi phiếu đặt hàng (PDF có QR) của đơn mua đã duyệt do kế hoạch mua lập qua email / Zalo của NCC; chưa cấu hình kênh hoặc lỗi → giữ “Chờ gửi NCC” và báo Mua hàng; đơn đã gửi sắp tới giờ giao chưa nhận hàng → nhắc gọi NCC xác nhận.";
        public string DefaultAt => plan.Value.SendTime;

        public async Task<JobOutcome> RunAsync(DateTime now, CancellationToken cancellationToken)
        {
            var (sent, failed, unconfirmed) = await service.SendDueAsync();
            return new JobOutcome(JobRunStatus.Succeeded, sent, now.Date, $"Đã gửi {sent} đơn · chờ gửi tay {failed} · nhắc xác nhận {unconfirmed}");
        }
    }
}
