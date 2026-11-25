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
    public sealed class ProductionAutoCloseJob(IUnitOfWork unitOfWork, IServiceProvider services, IOptions<BusinessRulesOptions> rules, INotificationService notifications, IMealMarginService margin) : IScheduledJob
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
            // UI-6.49 (3) (QĐ-26, D-127 c): tự ghi Nấu dư cho mẻ của lệnh đã đóng thuộc kế hoạch hôm nay (sau chia suất – nguồn RecordResultMany đáng tin).
            var actor = (services.GetService(typeof(AppDelicare.Data.Share.ICurrentActor)) as AppDelicare.Data.Share.ICurrentActor)?.Actor?.UserId;
            int overRecorded = r.ProductionAutoRecordOverProduction ? await RecordOverProductionAsync(db, day, now, actor, cancellationToken) : 0;
            // UI-6.27 (D-108): lệnh vừa đóng có giá thành thực tế → tính lại giá vốn suất các đơn đã đối soát 7 ngày gần nhất còn tạm tính theo giá chuẩn.
            int recalculated = await margin.RecalcProvisionalAsync(day.AddDays(-7), day, cancellationToken);
            string message = $"Đã đóng {closed} lệnh sản xuất" + (overRecorded != 0 ? $"; tự ghi nấu dư {overRecorded} mẻ" : string.Empty) + (recalculated != 0 ? $"; tính lại giá vốn {recalculated} đơn đã đối soát" : string.Empty)
                + (failed.Count != 0 ? $"; {failed.Count} lệnh chưa đóng được: {string.Join(", ", failed.Take(10))}" : string.Empty)
                + (over.Count != 0 ? $"; {over.Count} dòng chênh lệch vượt ngưỡng (đã báo Bếp trưởng + COO)" : string.Empty);
            return new JobOutcome(failed.Count == 0 ? JobRunStatus.Succeeded : JobRunStatus.Failed, closed + overRecorded, day, message);
        }

        public const string OverProductionReason = "Tự ghi nấu dư khi đóng lệnh";

        /// <summary>
        /// UI-6.49 (3): mẻ Đạt / Đã đóng gói của lệnh Đã đóng thuộc kế hoạch ngày <paramref name="day"/>, CÓ lệnh đóng gói (đã chia suất):
        /// Nấu dư = Σ suất đạt (ProductionOutput) − Σ suất đóng gói − hỏng − loại (PackingResult; hỏng / loại đã ghi PACKING_DAMAGE riêng) − Nấu dư đã ghi (tay hoặc lần trước).
        /// &gt; 0 → thêm 1 dòng MealWasteRecord OVERPRODUCTION. Chạy lại = 0 (phần đã ghi bị trừ).
        /// </summary>
        public static async Task<int> RecordOverProductionAsync(AppDelicare.Data.Connection.DatabaseContext db, DateTime day, DateTime now, Guid? actor, CancellationToken ct)
        {
            DateTime next = day.AddDays(1);
            var batches = await (from b in db.ProductionBatch.AsNoTracking()
                                 join o in db.ProductionOrder.AsNoTracking() on b.ProductionOrderId equals o.Id
                                 join p in db.ProductionPlan.AsNoTracking() on o.ProductionPlanId equals p.Id
                                 where !b.IsDeleted && !o.IsDeleted && o.Status == ProductionOrderStatus.Closed && p.ProductionDate >= day && p.ProductionDate < next
                                       && (b.Status == ProductionBatchStatus.Passed || b.Status == ProductionBatchStatus.Packed)
                                 select new { b.Id, b.DishId, OrderId = o.Id, o.ShiftId }).ToListAsync(ct);
            if (batches.Count == 0)
            {
                return 0;
            }
            var ids = batches.Select(b => b.Id).ToList();
            var accepted = (await db.ProductionOutput.AsNoTracking().Where(x => !x.IsDeleted && ids.Contains(x.ProductionBatchId))
                .GroupBy(x => x.ProductionBatchId).Select(g => new { g.Key, Q = g.Sum(x => x.AcceptedQuantity) }).ToListAsync(ct)).ToDictionary(x => x.Key, x => x.Q);
            var packing = await (from po in db.PackingOrder.AsNoTracking()
                                 join pr in db.PackingResult.AsNoTracking() on po.Id equals pr.PackingOrderId into prs
                                 from pr in prs.DefaultIfEmpty()
                                 where !po.IsDeleted && ids.Contains(po.ProductionBatchId) && (pr == null || !pr.IsDeleted)
                                 select new { po.ProductionBatchId, Used = pr == null ? 0m : pr.PackedQuantity + (pr.DamagedQty ?? 0m) + pr.RejectedQuantity, HasResult = pr != null }).ToListAsync(ct);
            var over = (await db.MealWasteRecord.AsNoTracking().Where(w => !w.IsDeleted && w.ProductionBatchId != null && ids.Contains(w.ProductionBatchId.Value) && w.WasteType == ProductionCode.WasteOverProduction)
                .GroupBy(w => w.ProductionBatchId!.Value).Select(g => new { g.Key, Q = g.Sum(w => w.QuantityMeals ?? 0m) }).ToListAsync(ct)).ToDictionary(x => x.Key, x => x.Q);
            int added = 0;
            foreach (var b in batches)
            {
                var mine = packing.Where(x => x.ProductionBatchId == b.Id).ToList();
                if (mine.Count == 0 || !mine.Any(x => x.HasResult) || !accepted.TryGetValue(b.Id, out decimal ok))
                {
                    continue; // chưa chia suất / chưa ghi kết quả đóng gói → không đoán
                }
                decimal used = mine.Sum(x => x.Used), already = over.GetValueOrDefault(b.Id);
                decimal remain = ok - used - already;
                if (remain <= 0)
                {
                    continue;
                }
                db.MealWasteRecord.Add(new AppDelicare.Model.Entity.MealWasteRecord
                {
                    Id = Guid.NewGuid(), ProductionBatchId = b.Id, ProductionOrderId = b.OrderId, DishId = b.DishId, ShiftId = b.ShiftId,
                    WasteType = ProductionCode.WasteOverProduction, QuantityMeals = remain,
                    Reason = $"{OverProductionReason}: đạt {ProductionRules.Num(ok)} − đã chia / hỏng {ProductionRules.Num(used)}" + (already > 0 ? $" − đã ghi {ProductionRules.Num(already)}" : string.Empty),
                    OccurredAt = now, RecordedBy = actor, CreatedDate = now, CreatedBy = actor,
                });
                var batch = await db.ProductionBatch.FirstAsync(x => x.Id == b.Id, ct);
                batch.WasteQty = (batch.WasteQty ?? 0m) + remain;
                added++;
            }
            if (added != 0)
            {
                await db.SaveChangesAsync(ConstantHelper.TypeSave.Insert);
            }
            return added;
        }
    }

    /// <summary>
    /// UI-6.27 (D-108): 01:00 (cùng giờ chỉ số tháng) – ngày 1 quét lại các đơn đã đối soát của THÁNG TRƯỚC còn tạm tính theo giá chuẩn → tính lại giá vốn suất
    /// theo giá thành thực tế (MealCost/Calculate, cập nhật tại chỗ – chạy lại không nhân đôi). Ngày khác không làm gì.
    /// </summary>
    public sealed class MealCostRecalcJob(IMealMarginService margin) : IScheduledJob
    {
        public string Code => "MealCostRecalc";
        public string Name => "Tính lại giá vốn suất tạm tính";
        public string Description => "Ngày 1 hằng tháng: tính lại giá vốn suất các đơn đã đối soát của tháng trước còn tạm tính theo giá thành chuẩn (lệnh sản xuất đóng muộn).";
        public string DefaultAt => "01:00";

        public async Task<JobOutcome> RunAsync(DateTime now, CancellationToken cancellationToken)
        {
            if (now.Day != 1)
            {
                return new JobOutcome(JobRunStatus.Succeeded, 0, now.Date, "Chỉ chạy ngày 1 hằng tháng");
            }
            DateTime first = new DateTime(now.Year, now.Month, 1).AddMonths(-1);
            int n = await margin.RecalcProvisionalAsync(first, first.AddMonths(1).AddDays(-1), cancellationToken);
            return new JobOutcome(JobRunStatus.Succeeded, n, first, $"Tính lại giá vốn {n} đơn tháng {first:MM/yyyy}");
        }
    }
}
