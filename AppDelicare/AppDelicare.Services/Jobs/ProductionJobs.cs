using AppDelicare.Data.Interface;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Services.Implement;
using AppDelicare.Services.Implement.Messaging;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AppDelicare.Services.Jobs
{
    /// <summary>
    /// UI-6.24 (D-105): cuối ngày (BusinessRules:ProductionAutoCloseTime, mặc định 22:00) đóng mọi lệnh SX Hoàn thành chưa đóng (lệnh tự đóng lỗi / cờ vừa bật lại)
    /// bằng đúng <c>ProductionOrder/Close</c>; rồi gửi 1 tin gộp chênh lệch tiêu hao vượt <c>ConsumptionVariancePctWarning</c> của các lệnh thuộc kế hoạch hôm nay
    /// cho Bếp trưởng (OPS-KIT) + COO (khóa idempotent theo ngày). Cờ <c>ProductionAutoCloseOrder</c> tắt → không đóng, không gửi. Chạy lại = 0 lệnh.
    /// </summary>
    public sealed class ProductionAutoCloseJob(IUnitOfWork unitOfWork, IServiceProvider services, IOptions<BusinessRulesOptions> rules, INotificationService notifications) : IScheduledJob
    {
        public const string JobCode = "ProductionAutoClose";
        public string Code => JobCode;
        public string Name => "Đóng lệnh sản xuất đã xong";
        public string Description => "Đóng mọi lệnh sản xuất Hoàn thành còn sót (tính chênh lệch tiêu hao, giá thành thực tế, giải phóng giữ chỗ dư) và gửi 1 tin chênh lệch vượt ngưỡng của ngày cho Bếp trưởng + COO.";
        public string DefaultAt => string.IsNullOrWhiteSpace(rules.Value.ProductionAutoCloseTime) ? "22:00" : rules.Value.ProductionAutoCloseTime;

        public async Task<JobOutcome> RunAsync(DateTime now, CancellationToken cancellationToken)
        {
            var r = rules.Value;
            DateTime day = now.Date, next = day.AddDays(1);
            if (!r.ProductionAutoCloseOrder)
            {
                return new JobOutcome(JobRunStatus.Succeeded, 0, day, "Tự đóng lệnh sản xuất đang tắt – đóng tay ở trang lệnh");
            }
            var db = unitOfWork.DbContext;
            var pending = await db.ProductionOrder.AsNoTracking().Where(o => !o.IsDeleted && o.Status == ProductionOrderStatus.Completed)
                .OrderBy(o => o.ActualEndDate).Select(o => new { o.Id, Number = o.ProductionOrderNumber ?? o.ProductionOrderNo }).ToListAsync(cancellationToken);
            int closed = 0;
            var failed = new List<string>();
            foreach (var o in pending)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ProductionAutoClose.CloseAsync(services, o.Id, o.Number, "Hệ thống đóng lệnh cuối ngày");
                db.ChangeTracker.Clear();
                if (await db.ProductionOrder.AsNoTracking().AnyAsync(x => x.Id == o.Id && x.Status == ProductionOrderStatus.Closed, cancellationToken))
                {
                    closed++;
                }
                else
                {
                    failed.Add(o.Number ?? o.Id.ToString());
                }
            }

            // 1 tin gộp chênh lệch vượt ngưỡng của các lệnh (đã đóng) thuộc kế hoạch hôm nay.
            decimal limit = r.ConsumptionVariancePctWarning;
            var over = await (from v in db.MaterialConsumptionVariance.AsNoTracking()
                              join o in db.ProductionOrder.AsNoTracking() on v.ProductionOrderId equals o.Id
                              join p in db.ProductionPlan.AsNoTracking() on o.ProductionPlanId equals p.Id
                              join i in db.Ingredient.AsNoTracking() on v.IngredientId equals i.Id
                              where !v.IsDeleted && !o.IsDeleted && o.Status == ProductionOrderStatus.Closed && p.ProductionDate >= day && p.ProductionDate < next
                                    && v.VariancePercent != null && (v.VariancePercent > limit || v.VariancePercent < -limit)
                              select new { Order = o.ProductionOrderNumber ?? o.ProductionOrderNo, Ingredient = i.Name ?? i.Code, v.VariancePercent, v.VarianceCost })
                             .ToListAsync(cancellationToken);
            int sent = 0;
            if (over.Count != 0)
            {
                var users = new HashSet<Guid>(await RecipientResolver.UsersInGroupsAsync(db, ["OPS-KIT", "COO"], cancellationToken));
                sent = await BusinessNotifier.SafeEnqueueAsync(notifications, new NotificationRequest
                {
                    Template = NotificationCatalog.ProductionVariance, UserIds = [.. users],
                    Data = new
                    {
                        title = $"⚠️ Chênh lệch tiêu hao vượt {ProductionRules.Num(limit)}% ngày {day:dd/MM}: {over.Select(x => x.Ingredient).Distinct().Count()} nguyên liệu ở {over.Select(x => x.Order).Distinct().Count()} lệnh",
                        lines = over.OrderByDescending(x => Math.Abs(x.VariancePercent ?? 0)).Take(8)
                            .Select(x => $"{x.Order} · {x.Ingredient} {(x.VariancePercent > 0 ? "+" : string.Empty)}{ProductionRules.Num(Math.Round(x.VariancePercent ?? 0, 1))}%").ToList(),
                    },
                    IdempotencyKey = $"{NotificationCatalog.ProductionVariance}:{day:yyyyMMdd}",
                });
            }
            string message = $"Đã đóng {closed} lệnh sản xuất"
                + (failed.Count != 0 ? $"; {failed.Count} lệnh chưa đóng được: {string.Join(", ", failed.Take(10))}" : string.Empty)
                + (over.Count != 0 ? $"; {over.Count} dòng chênh lệch vượt ngưỡng (đã báo Bếp trưởng + COO)" : string.Empty);
            return new JobOutcome(failed.Count == 0 ? JobRunStatus.Succeeded : JobRunStatus.Failed, closed, day, message);
        }
    }
}
