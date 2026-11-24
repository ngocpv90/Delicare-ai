using System.Globalization;
using AppDelicare.Data.Connection;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Interface;
using AppDelicare.Services.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// BE3.4 (spec 11.7 "Job KpiDailyJob", Catalog 224, D-57 a / b, D-48): tính KPI từ bảng giao dịch có sẵn, upsert snapshot theo
    /// UNIQUE (SnapshotDate / SnapshotMonth, BranchId, MetricCode, MetricVersion) – chạy lại không nhân đôi (gồm cả dòng đã xóa mềm: dùng lại dòng đó).
    /// Mỗi chỉ tiêu đang hiệu lực (IsActive, EffectiveFrom ≤ ngày ≤ EffectiveTo) → 1 dòng; mẫu số 0 / thiếu dữ liệu / chưa có công thức → INCOMPLETE.
    /// Công thức: xem <see cref="KpiCode"/> + seed <c>DB/seed/KpiMetricDefinition_seed.sql</c>. PROPOSED – BA/PO CONFIRM (D-57 w–ab).
    /// </summary>
    public class KpiCalculationService(IUnitOfWork unitOfWork, IClock clock, ICurrentActor currentActor, IConfiguration configuration,
        IOptions<BusinessRulesOptions> rules) : IKpiCalculationService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly ICurrentActor _currentActor = currentActor;
        private readonly IConfiguration _configuration = configuration;
        private readonly BusinessRulesOptions _rules = rules.Value;

        /// <summary>Mã chỉ tiêu có công thức trong hệ thống.</summary>
        public static readonly IReadOnlySet<string> Calculators = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            KpiCode.ProdAchievement, KpiCode.WasteRate, KpiCode.QcRejectRate, KpiCode.QcCoreTempPass, KpiCode.QcSampleRetained,
            KpiCode.DelOnTime, KpiCode.InvAccuracy, KpiCode.FinCostMeal, KpiCode.FinFoodCost, KpiCode.PkgLossRate, KpiCode.CsComplaintSla,
        };

        public async Task<IResult<KpiRefreshResultModel>> Refresh(KpiRefreshRequest request)
        {
            try
            {
                string jobCode = (request.JobCode ?? string.Empty).Trim().ToUpperInvariant();
                if (jobCode == KpiCode.JobInventoryBalance)
                {
                    return Result<KpiRefreshResultModel>.Fail(ConstantHelper.Message.KpiInventoryBalanceNotSupported);
                }
                if (jobCode is not (KpiCode.JobDaily or KpiCode.JobMonthly))
                {
                    return Result<KpiRefreshResultModel>.Fail(string.Format(ConstantHelper.Message.KpiJobCodeInvalid, request.JobCode));
                }
                DateTime today = _clock.Today;
                DateTime period;
                if (jobCode == KpiCode.JobDaily)
                {
                    period = (request.Date ?? today).Date;
                    if (period > today)
                    {
                        return Result<KpiRefreshResultModel>.Fail(ConstantHelper.Message.KpiDateInFuture);
                    }
                }
                else
                {
                    DateTime? month = string.IsNullOrWhiteSpace(request.Month) ? today : ParseMonth(request.Month);
                    if (month == null)
                    {
                        return Result<KpiRefreshResultModel>.Fail(string.Format(ConstantHelper.Message.KpiMonthInvalid, request.Month));
                    }
                    period = MonthStart(month.Value);
                    if (period > MonthStart(today))
                    {
                        return Result<KpiRefreshResultModel>.Fail(ConstantHelper.Message.KpiDateInFuture);
                    }
                }
                Guid? branchId = request.BranchId is { } b && b != Guid.Empty ? b : null;
                var db = _unitOfWork.DbContext;
                if (branchId != null && !await db.OrganizationUnit.AsNoTracking().AnyAsync(o => o.Id == branchId && !o.IsDeleted))
                {
                    return Result<KpiRefreshResultModel>.Fail(ConstantHelper.Message.KpiBranchNotFound);
                }
                var actor = _currentActor.RequireActor();
                var gate = new AnalyticsRunGate(_configuration, _clock, db);
                var entry = new AnalyticsRunEntry
                {
                    RunId = Guid.NewGuid(),
                    JobCode = jobCode,
                    StartedAt = _clock.Now,
                    SourceWatermark = Watermark(JobTrigger.Manual, period, branchId),
                };
                await using var runLock = await gate.TryAcquireAsync(jobCode);
                if (runLock == null)
                {
                    entry.RunStatus = JobRunStatus.Locked;
                    entry.CompletedAt = _clock.Now;
                    entry.Message = string.Format(ConstantHelper.Message.KpiRefreshRunning, KpiCode.JobLabel(jobCode));
                    await gate.InsertLogAsync(entry, actor.UserId);
                    return Result<KpiRefreshResultModel>.Fail(entry.Message);
                }
                entry.RunStatus = JobRunStatus.Running;
                await gate.InsertLogAsync(entry, actor.UserId);
                Log.Information("[KPI] {JobCode} bấm tay bắt đầu: {Period:yyyy-MM-dd}, chi nhánh {BranchId}, RunId {RunId}, actor {Actor}", jobCode, period, branchId, entry.RunId, actor);
                KpiRefreshResultModel result;
                try
                {
                    result = await Calculate(jobCode, period, branchId, JobTrigger.Manual);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[KPI] {JobCode} lỗi (RunId {RunId})", jobCode, entry.RunId);
                    entry.RunStatus = JobRunStatus.Failed;
                    entry.CompletedAt = _clock.Now;
                    entry.Message = ex.Message;
                    await gate.UpdateLogAsync(entry, actor.UserId);
                    return Result<KpiRefreshResultModel>.Fail(string.Format(ConstantHelper.Message.KpiRefreshFailed, KpiCode.JobLabel(jobCode), ConstantHelper.Message.InternalServer));
                }
                entry.RunStatus = JobRunStatus.Succeeded;
                entry.RowsProcessed = result.RowsProcessed;
                entry.CompletedAt = _clock.Now;
                entry.Message = string.Format(ConstantHelper.Message.KpiRefreshDone, KpiCode.JobLabel(jobCode), result.Metrics.Count, result.IncompleteCount);
                await gate.UpdateLogAsync(entry, actor.UserId);
                Log.Information("[KPI] {JobCode} xong: {Rows} dòng, {Incomplete} thiếu dữ liệu (RunId {RunId})", jobCode, result.RowsProcessed, result.IncompleteCount, entry.RunId);
                result.RunId = entry.RunId;
                result.RunStatus = entry.RunStatus;
                result.SourceWatermark = entry.SourceWatermark;
                return Result<KpiRefreshResultModel>.Success(result, entry.Message);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Refresh: ", ex.ToString()));
                throw;
            }
        }

        public async Task<KpiRefreshResultModel> Calculate(string jobCode, DateTime periodDate, Guid? branchId, string trigger, CancellationToken cancellationToken = default)
        {
            bool monthly = string.Equals(jobCode, KpiCode.JobMonthly, StringComparison.OrdinalIgnoreCase);
            DateTime today = _clock.Today;
            DateTime from = monthly ? MonthStart(periodDate) : periodDate.Date;
            DateTime toExclusive = monthly ? Min(from.AddMonths(1), today.AddDays(1)) : from.AddDays(1);
            if (toExclusive <= from)
            {
                toExclusive = from.AddDays(1);
            }
            // Phiên bản hiệu lực: KPI ngày = tại ngày đó; KPI tháng = tại ngày cuối đã tính của tháng.
            DateTime reference = toExclusive.AddDays(-1);
            var db = _unitOfWork.DbContext;
            var definitions = (await db.KpiMetricDefinition.AsNoTracking()
                    .Where(d => !d.IsDeleted && d.IsActive && d.EffectiveFrom <= reference && (d.EffectiveTo == null || d.EffectiveTo >= reference))
                    .ToListAsync(cancellationToken))
                .GroupBy(d => d.MetricCode!, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(d => d.VersionNo).First())
                .OrderBy(d => d.DomainCode).ThenBy(d => d.MetricCode)
                .ToList();
            var source = new Source(db, from, toExclusive, branchId, _clock.Now, _rules);
            var metrics = new List<KpiCalculatedMetricModel>();
            foreach (var definition in definitions)
            {
                var raw = await CalculateMetricAsync(source, definition.MetricCode!, cancellationToken);
                var (_, target, direction) = KpiCode.ParseFormula(definition.FormulaDescription, definition.MetricCode);
                decimal? value = raw.Denominator is { } den && den != 0 && raw.Numerator is { } num ? Math.Round(num / den * raw.Scale, 4) : null;
                metrics.Add(new KpiCalculatedMetricModel
                {
                    MetricCode = definition.MetricCode!,
                    MetricName = definition.MetricName,
                    MetricVersion = definition.VersionNo,
                    Value = value,
                    Numerator = raw.Numerator,
                    Denominator = raw.Denominator,
                    Target = target,
                    Variance = value != null && target != null ? value - target : null,
                    Met = KpiCode.Met(value, target, direction),
                    DataQualityStatus = value == null || raw.Issues.Count > 0 ? KpiCode.QualityIncomplete : KpiCode.QualityOk,
                    Note = raw.Issues.Count > 0 ? string.Join("; ", raw.Issues) : null,
                });
            }

            DateTime now = _clock.Now;
            string watermark = $"{trigger}|{now:yyyy-MM-ddTHH:mm:ss}";
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var codes = metrics.Select(m => m.MetricCode).ToList();
                if (monthly)
                {
                    var existing = await db.KpiMonthlySnapshot.Where(x => x.SnapshotMonth == from && x.BranchId == branchId && codes.Contains(x.MetricCode!)).ToListAsync(cancellationToken);
                    foreach (var m in metrics)
                    {
                        var row = existing.FirstOrDefault(x => string.Equals(x.MetricCode, m.MetricCode, StringComparison.OrdinalIgnoreCase) && x.MetricVersion == m.MetricVersion);
                        if (row == null)
                        {
                            row = new KpiMonthlySnapshot { Id = Guid.NewGuid(), SnapshotMonth = from, BranchId = branchId, MetricCode = m.MetricCode, MetricVersion = m.MetricVersion };
                            _unitOfWork.KpiMonthlySnapshotRepository.Add(row);
                        }
                        else
                        {
                            _unitOfWork.KpiMonthlySnapshotRepository.Update(row);
                        }
                        row.MetricValue = m.Value;
                        row.Numerator = m.Numerator;
                        row.Denominator = m.Denominator;
                        row.TargetValue = m.Target;
                        row.VarianceValue = m.Variance;
                        row.CalculationVersion = KpiCode.CalculationVersion;
                        row.CalculatedAt = now;
                        row.SourceWatermark = watermark;
                        row.DataQualityStatus = m.DataQualityStatus;
                        row.IsDeleted = false;
                    }
                    await _unitOfWork.KpiMonthlySnapshotRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                }
                else
                {
                    var existing = await db.KpiDailySnapshot.Where(x => x.SnapshotDate == from && x.BranchId == branchId && codes.Contains(x.MetricCode!)).ToListAsync(cancellationToken);
                    foreach (var m in metrics)
                    {
                        var row = existing.FirstOrDefault(x => string.Equals(x.MetricCode, m.MetricCode, StringComparison.OrdinalIgnoreCase) && x.MetricVersion == m.MetricVersion);
                        if (row == null)
                        {
                            row = new KpiDailySnapshot { Id = Guid.NewGuid(), SnapshotDate = from, BranchId = branchId, MetricCode = m.MetricCode, MetricVersion = m.MetricVersion };
                            _unitOfWork.KpiDailySnapshotRepository.Add(row);
                        }
                        else
                        {
                            _unitOfWork.KpiDailySnapshotRepository.Update(row);
                        }
                        row.MetricValue = m.Value;
                        row.Numerator = m.Numerator;
                        row.Denominator = m.Denominator;
                        row.TargetValue = m.Target;
                        row.VarianceValue = m.Variance;
                        row.CalculationVersion = KpiCode.CalculationVersion;
                        row.CalculatedAt = now;
                        row.SourceWatermark = watermark;
                        row.DataQualityStatus = m.DataQualityStatus;
                        row.IsDeleted = false;
                    }
                    await _unitOfWork.KpiDailySnapshotRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                }
                _unitOfWork.CommitTransaction();
            }
            catch
            {
                _unitOfWork.RollbackTransaction();
                throw;
            }
            return new KpiRefreshResultModel
            {
                JobCode = monthly ? KpiCode.JobMonthly : KpiCode.JobDaily,
                PeriodDate = from,
                From = from,
                To = toExclusive.AddDays(-1),
                BranchId = branchId,
                RowsProcessed = metrics.Count,
                IncompleteCount = metrics.Count(m => m.DataQualityStatus == KpiCode.QualityIncomplete),
                Metrics = metrics,
            };
        }

        /// <summary>SourceWatermark của AnalyticsRefreshLog: "&lt;trigger&gt;|&lt;ngày / ngày đầu tháng yyyy-MM-dd&gt;[|branch=&lt;id&gt;]" (cùng dạng T23).</summary>
        public static string Watermark(string trigger, DateTime period, Guid? branchId)
            => $"{trigger}|{period:yyyy-MM-dd}" + (branchId is { } b ? $"|branch={b}" : string.Empty);

        /// <summary>"2026-11" / "2026-11-05" → ngày đầu tháng; sai → null.</summary>
        public static DateTime? ParseMonth(string? text)
        {
            string value = (text ?? string.Empty).Trim();
            return DateTime.TryParseExact(value, ["yyyy-MM", "yyyy-M", "yyyy-MM-dd", "yyyy-M-d"], CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime d)
                ? MonthStart(d) : null;
        }

        public static DateTime MonthStart(DateTime d) => new(d.Year, d.Month, 1);

        private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;

        private sealed record Raw(decimal? Numerator, decimal? Denominator, decimal Scale, List<string> Issues);

        private static Raw Ratio(decimal numerator, decimal denominator, decimal scale, List<string> issues, string emptyReason)
        {
            if (denominator == 0)
            {
                issues.Insert(0, emptyReason);
            }
            return new Raw(numerator, denominator, scale, issues);
        }

        private async Task<Raw> CalculateMetricAsync(Source s, string metricCode, CancellationToken ct)
        {
            var issues = new List<string>();
            switch (metricCode.ToUpperInvariant())
            {
                case KpiCode.ProdAchievement:
                {
                    // Σ suất đạt (ProductionOutput.AcceptedQuantity) ÷ Σ suất kế hoạch (ProductionBatche.PlannedQuantity) của mẻ trong kỳ (theo StartDate, trừ mẻ Hủy).
                    var batches = await s.BatchesAsync(ct);
                    var outputs = await s.OutputsAsync(ct);
                    decimal planned = batches.Sum(b => b.PlannedQuantity ?? b.PlannedQty ?? 0m);
                    decimal accepted = outputs.Sum(o => o.AcceptedQuantity);
                    int missing = batches.Count(b => b.Status != ProductionBatchStatus.Failed && !outputs.Any(o => o.ProductionBatchId == b.Id));
                    if (missing > 0)
                    {
                        issues.Add($"{missing} mẻ chưa ghi sản lượng");
                    }
                    return Ratio(accepted, planned, 100m, issues, "Không có mẻ kế hoạch trong kỳ");
                }
                case KpiCode.WasteRate:
                {
                    // Σ suất hao hụt (MealWasteRecord.QuantityMeals, OccurredAt trong kỳ) ÷ Σ suất sản xuất (ProductionOutput.ProducedQuantity của mẻ trong kỳ).
                    var outputs = await s.OutputsAsync(ct);
                    var waste = await s.WasteAsync(ct);
                    return Ratio(waste.Sum(w => w.QuantityMeals ?? 0m), outputs.Sum(o => o.ProducedQuantity), 100m, issues, "Chưa có sản lượng mẻ trong kỳ");
                }
                case KpiCode.QcRejectRate:
                {
                    // Số mẻ QC Từ chối ÷ số mẻ đã có kết luận QC (Đạt / Từ chối); mẻ Tạm giữ chưa tính.
                    var batches = await s.BatchesAsync(ct);
                    var decided = batches.Where(b => b.QcStatus is BatchQcStatus.Pass or BatchQcStatus.Reject).ToList();
                    return Ratio(decided.Count(b => b.QcStatus == BatchQcStatus.Reject), decided.Count, 100m, issues, "Chưa có mẻ có kết luận QC trong kỳ");
                }
                case KpiCode.QcCoreTempPass:
                {
                    // Mẻ đã nấu xong (Chờ QC / Đạt / Không đạt / Đã đóng gói) có nhiệt độ lõi đo gần nhất ≥ CoreTempMinC ÷ số mẻ đó (mẻ chưa đo = không đạt).
                    var batches = (await s.BatchesAsync(ct)).Where(b => b.Status is ProductionBatchStatus.QcPending or ProductionBatchStatus.Passed
                        or ProductionBatchStatus.Failed or ProductionBatchStatus.Packed).ToList();
                    var ids = batches.Select(b => b.Id).ToList();
                    var logs = await s.Db.TemperatureLog.AsNoTracking()
                        .Where(t => !t.IsDeleted && t.ProductionBatchId != null && ids.Contains(t.ProductionBatchId.Value) && t.MeasurementType == ProductionCode.CoreMeasurement)
                        .Select(t => new { t.ProductionBatchId, t.TemperatureC, t.MeasuredAt, t.CreatedDate }).ToListAsync(ct);
                    var last = logs.GroupBy(t => t.ProductionBatchId!.Value)
                        .ToDictionary(g => g.Key, g => g.OrderByDescending(t => t.MeasuredAt).ThenByDescending(t => t.CreatedDate).First().TemperatureC);
                    int noReading = batches.Count(b => !last.ContainsKey(b.Id));
                    if (noReading > 0)
                    {
                        issues.Add($"{noReading} mẻ chưa đo nhiệt độ lõi (tính là không đạt)");
                    }
                    decimal min = s.Rules.CoreTempMinC;
                    return Ratio(batches.Count(b => last.TryGetValue(b.Id, out decimal t) && t >= min), batches.Count, 100m, issues, "Chưa có mẻ nấu xong trong kỳ");
                }
                case KpiCode.QcSampleRetained:
                {
                    // Mẻ Đạt / Đã đóng gói có kết quả checklist SAMPLE_RETAINED = PASS ÷ số mẻ Đạt / Đã đóng gói.
                    var batches = (await s.BatchesAsync(ct)).Where(b => b.Status is ProductionBatchStatus.Passed or ProductionBatchStatus.Packed).ToList();
                    var ids = batches.Select(b => b.Id).ToList();
                    var itemIds = await s.Db.FoodSafetyChecklistItem.AsNoTracking().Where(i => i.ItemCode == ProductionCode.SampleRetainedItem).Select(i => i.Id).ToListAsync(ct);
                    var sampled = (await s.Db.FoodSafetyChecklistResult.AsNoTracking()
                        .Where(r => !r.IsDeleted && r.ProductionBatchId != null && ids.Contains(r.ProductionBatchId.Value) && itemIds.Contains(r.ChecklistItemId)
                                    && r.ResultCode == ProductionCode.ResultPass)
                        .Select(r => r.ProductionBatchId!.Value).ToListAsync(ct)).ToHashSet();
                    return Ratio(batches.Count(b => sampled.Contains(b.Id)), batches.Count, 100m, issues, "Chưa có mẻ Đạt QC trong kỳ");
                }
                case KpiCode.DelOnTime:
                {
                    // Đơn giao Đã giao trước (giờ giao dự kiến + OnTimeToleranceMinutes) ÷ đơn giao Đã giao + Thất bại theo ngày giao.
                    // Giờ dự kiến không có giờ (00:00) → hạn = hết ngày giao.
                    var orders = await s.DeliveryOrdersAsync(ct);
                    var done = orders.Where(o => o.Status is DeliveryOrderStatus.Delivered or DeliveryOrderStatus.Failed).ToList();
                    int tolerance = s.Rules.Kpi.OnTimeToleranceMinutes;
                    bool OnTime(DeliveryOrder o)
                    {
                        if (o.Status != DeliveryOrderStatus.Delivered || o.ActualDeliveryTime is not { } actual)
                        {
                            return false;
                        }
                        DateTime deadline = o.PlannedDeliveryTime.TimeOfDay == TimeSpan.Zero ? o.DeliveryDate.Date.AddDays(1) : o.PlannedDeliveryTime.AddMinutes(tolerance);
                        return actual <= deadline;
                    }
                    int noTime = done.Count(o => o.Status == DeliveryOrderStatus.Delivered && o.ActualDeliveryTime == null);
                    if (noTime > 0)
                    {
                        issues.Add($"{noTime} đơn giao thiếu giờ giao thực tế");
                    }
                    return Ratio(done.Count(OnTime), done.Count, 100m, issues, "Chưa có đơn giao hoàn tất trong kỳ");
                }
                case KpiCode.InvAccuracy:
                {
                    // 1 − Σ|giá trị chênh lệch| ÷ Σ giá trị tồn hệ thống, phiếu kiểm kê Đã ghi sổ theo ngày kiểm kê.
                    // Giá trị tồn dòng = |SystemQuantity| × MaterialLot.UnitCost; dòng không có đơn giá → bỏ, ghi thiếu dữ liệu.
                    var counts = await s.Db.StockCount.AsNoTracking()
                        .Where(c => !c.IsDeleted && c.Status == StockCountStatus.Posted && c.CountDate >= s.From && c.CountDate < s.To).ToListAsync(ct);
                    if (s.BranchId != null)
                    {
                        var warehouses = await s.WarehouseIdsAsync(ct);
                        counts = counts.Where(c => warehouses.Contains(c.WarehouseId)).ToList();
                    }
                    var countIds = counts.Select(c => c.Id).ToList();
                    var lines = await s.Db.StockCountLine.AsNoTracking().Where(l => !l.IsDeleted && countIds.Contains(l.StockCountId)).ToListAsync(ct);
                    var lotIds = lines.Select(l => l.MaterialLotId ?? l.LotId).Where(x => x != null).Select(x => x!.Value).Distinct().ToList();
                    var costs = await s.Db.MaterialLot.AsNoTracking().Where(l => lotIds.Contains(l.Id) && l.UnitCost != null).ToDictionaryAsync(l => l.Id, l => l.UnitCost!.Value, ct);
                    decimal systemValue = 0m, varianceValue = 0m;
                    int noCost = 0;
                    foreach (var line in lines)
                    {
                        Guid? lot = line.MaterialLotId ?? line.LotId;
                        if (lot == null || !costs.TryGetValue(lot.Value, out decimal unitCost))
                        {
                            noCost++;
                            continue;
                        }
                        systemValue += Math.Abs(line.SystemQuantity) * unitCost;
                        varianceValue += Math.Abs(line.VarianceValue ?? line.VarianceQuantity * unitCost);
                    }
                    if (noCost > 0)
                    {
                        issues.Add($"{noCost} dòng kiểm kê không có đơn giá lô (không tính)");
                    }
                    return Ratio(Math.Max(0m, systemValue - varianceValue), systemValue, 100m, issues, "Chưa có phiếu kiểm kê đã ghi sổ (có giá trị) trong kỳ");
                }
                case KpiCode.FinCostMeal:
                case KpiCode.FinFoodCost:
                {
                    // Giá vốn suất (MealCost – MealCost/Calculate) của đơn Đã giao / Đã đối soát theo ngày ăn.
                    // FIN_COST_MEAL = Σ TotalActualCost ÷ Σ suất nhận (AcceptedMealQuantity – 1 lần / dòng đơn); FIN_FOOD_COST = Σ TotalActualCost ÷ Σ Revenue × 100.
                    var orders = await s.FinishedOrdersAsync(ct);
                    var orderIds = orders.Select(o => o.Id).ToList();
                    var costs = await s.Db.MealCost.AsNoTracking().Where(m => !m.IsDeleted && orderIds.Contains(m.OrderId)).ToListAsync(ct);
                    int noCost = orders.Count(o => !costs.Any(m => m.OrderId == o.Id));
                    if (noCost > 0)
                    {
                        issues.Add($"{noCost} đơn chưa tính giá vốn suất (MealCost/Calculate)");
                    }
                    decimal totalCost = costs.Sum(m => m.TotalActualCost);
                    if (metricCode.Equals(KpiCode.FinCostMeal, StringComparison.OrdinalIgnoreCase))
                    {
                        decimal meals = costs.GroupBy(m => (object?)m.OrderLineId ?? m.OrderId).Sum(g => g.Max(m => m.AcceptedMealQuantity));
                        return Ratio(totalCost, meals, 1m, issues, "Chưa có giá vốn suất của đơn đã giao trong kỳ");
                    }
                    return Ratio(totalCost, costs.Sum(m => m.Revenue ?? 0m), 100m, issues, "Chưa có doanh thu (giá vốn suất) của đơn đã giao trong kỳ");
                }
                case KpiCode.PkgLossRate:
                {
                    // Σ hộp thất thoát (PackagingLedger LOSS) ÷ Σ hộp giao cho khách (OUT) theo ngày phát sinh.
                    var ledger = await s.Db.PackagingLedger.AsNoTracking()
                        .Where(p => !p.IsDeleted && p.TransactionAt >= s.From && p.TransactionAt < s.To
                                    && (p.TransactionType == PackagingTransactionType.Out || p.TransactionType == PackagingTransactionType.Loss))
                        .Select(p => new { p.CustomerId, p.TransactionType, p.Quantity }).ToListAsync(ct);
                    if (s.BranchId != null)
                    {
                        var customers = await s.CustomerIdsAsync(ct);
                        ledger = ledger.Where(p => p.CustomerId != null && customers.Contains(p.CustomerId.Value)).ToList();
                    }
                    return Ratio(ledger.Where(p => p.TransactionType == PackagingTransactionType.Loss).Sum(p => p.Quantity),
                        ledger.Where(p => p.TransactionType == PackagingTransactionType.Out).Sum(p => p.Quantity), 100m, issues, "Chưa có hộp giao cho khách trong kỳ");
                }
                case KpiCode.CsComplaintSla:
                {
                    // Phản ánh có hạn SLA trong kỳ (trừ Từ chối): xử lý xong trước hạn ÷ (xong + đã quá hạn mà chưa xong). Chưa tới hạn, chưa xong → chưa tính.
                    var complaints = await s.Db.CustomerComplaint.AsNoTracking()
                        .Where(c => !c.IsDeleted && c.SlaDueAt != null && c.SlaDueAt >= s.From && c.SlaDueAt < s.To
                                    && (c.ComplaintStatus == null || c.ComplaintStatus != ComplaintStatus.Rejected))
                        .Select(c => new { c.CustomerId, c.SlaDueAt, c.ResolvedAt }).ToListAsync(ct);
                    if (s.BranchId != null)
                    {
                        var customers = await s.CustomerIdsAsync(ct);
                        complaints = complaints.Where(c => customers.Contains(c.CustomerId)).ToList();
                    }
                    var due = complaints.Where(c => c.ResolvedAt != null || c.SlaDueAt < s.Now).ToList();
                    return Ratio(due.Count(c => c.ResolvedAt != null && c.ResolvedAt <= c.SlaDueAt), due.Count, 100m, issues, "Chưa có phản ánh tới hạn xử lý trong kỳ");
                }
                default:
                    issues.Add(ConstantHelper.Message.KpiNoFormula);
                    return new Raw(null, null, 1m, issues);
            }
        }

        /// <summary>Dữ liệu nguồn của 1 kỳ (nạp 1 lần, dùng chung giữa các chỉ tiêu) + phạm vi chi nhánh.</summary>
        private sealed class Source(DatabaseContext db, DateTime from, DateTime to, Guid? branchId, DateTime now, BusinessRulesOptions rules)
        {
            public DatabaseContext Db { get; } = db;
            public DateTime From { get; } = from;
            public DateTime To { get; } = to;
            public Guid? BranchId { get; } = branchId;
            public DateTime Now { get; } = now;
            public BusinessRulesOptions Rules { get; } = rules;

            private List<ProductionBatch>? _batches;
            private List<ProductionOutput>? _outputs;
            private List<MealWasteRecord>? _waste;
            private List<DeliveryOrder>? _deliveryOrders;
            private List<Order>? _orders;
            private HashSet<Guid>? _customers;
            private HashSet<Guid>? _productionOrders;
            private HashSet<Guid>? _warehouses;

            /// <summary>Chi nhánh: khách có Customer.BranchId = chi nhánh.</summary>
            public async Task<HashSet<Guid>> CustomerIdsAsync(CancellationToken ct)
                => _customers ??= (await Db.Customer.AsNoTracking().Where(c => c.BranchId == BranchId).Select(c => c.Id).ToListAsync(ct)).ToHashSet();

            /// <summary>Chi nhánh: lệnh SX của kế hoạch có ProductionPlan.OrganizationUnitId = chi nhánh.</summary>
            public async Task<HashSet<Guid>> ProductionOrderIdsAsync(CancellationToken ct)
            {
                if (_productionOrders == null)
                {
                    var plans = await Db.ProductionPlan.AsNoTracking().Where(p => p.OrganizationUnitId == BranchId).Select(p => p.Id).ToListAsync(ct);
                    _productionOrders = (await Db.ProductionOrder.AsNoTracking().Where(o => plans.Contains(o.ProductionPlanId)).Select(o => o.Id).ToListAsync(ct)).ToHashSet();
                }
                return _productionOrders;
            }

            /// <summary>Chi nhánh: kho có Warehouse.OrganizationUnitId = chi nhánh.</summary>
            public async Task<HashSet<Guid>> WarehouseIdsAsync(CancellationToken ct)
                => _warehouses ??= (await Db.Warehouse.AsNoTracking().Where(w => w.OrganizationUnitId == BranchId).Select(w => w.Id).ToListAsync(ct)).ToHashSet();

            /// <summary>Mẻ nấu bắt đầu trong kỳ (StartDate), trừ mẻ Hủy.</summary>
            public async Task<List<ProductionBatch>> BatchesAsync(CancellationToken ct)
            {
                if (_batches == null)
                {
                    var list = await Db.ProductionBatch.AsNoTracking()
                        .Where(b => !b.IsDeleted && b.StartDate >= From && b.StartDate < To && (b.Status == null || b.Status != ProductionBatchStatus.Cancelled)).ToListAsync(ct);
                    if (BranchId != null)
                    {
                        var orders = await ProductionOrderIdsAsync(ct);
                        list = list.Where(b => orders.Contains(b.ProductionOrderId)).ToList();
                    }
                    _batches = list;
                }
                return _batches;
            }

            public async Task<List<ProductionOutput>> OutputsAsync(CancellationToken ct)
            {
                if (_outputs == null)
                {
                    var ids = (await BatchesAsync(ct)).Select(b => b.Id).ToList();
                    _outputs = await Db.ProductionOutput.AsNoTracking().Where(o => !o.IsDeleted && ids.Contains(o.ProductionBatchId)).ToListAsync(ct);
                }
                return _outputs;
            }

            /// <summary>Hao hụt phát sinh trong kỳ (OccurredAt). Chi nhánh: theo mẻ / lệnh SX của chi nhánh.</summary>
            public async Task<List<MealWasteRecord>> WasteAsync(CancellationToken ct)
            {
                if (_waste == null)
                {
                    var list = await Db.MealWasteRecord.AsNoTracking().Where(w => !w.IsDeleted && w.OccurredAt >= From && w.OccurredAt < To).ToListAsync(ct);
                    if (BranchId != null)
                    {
                        var orders = await ProductionOrderIdsAsync(ct);
                        var batchIds = (await Db.ProductionBatch.AsNoTracking().Where(b => orders.Contains(b.ProductionOrderId)).Select(b => b.Id).ToListAsync(ct)).ToHashSet();
                        list = list.Where(w => (w.ProductionOrderId is { } po && orders.Contains(po)) || (w.ProductionBatchId is { } pb && batchIds.Contains(pb))).ToList();
                    }
                    _waste = list;
                }
                return _waste;
            }

            /// <summary>Đơn giao theo ngày giao (DeliveryDate) trong kỳ.</summary>
            public async Task<List<DeliveryOrder>> DeliveryOrdersAsync(CancellationToken ct)
            {
                if (_deliveryOrders == null)
                {
                    var list = await Db.DeliveryOrder.AsNoTracking().Where(o => !o.IsDeleted && o.DeliveryDate >= From && o.DeliveryDate < To).ToListAsync(ct);
                    if (BranchId != null)
                    {
                        var customers = await CustomerIdsAsync(ct);
                        list = list.Where(o => customers.Contains(o.CustomerId)).ToList();
                    }
                    _deliveryOrders = list;
                }
                return _deliveryOrders;
            }

            /// <summary>Đơn hàng Đã giao / Đã đối soát theo ngày ăn (MealDate) trong kỳ.</summary>
            public async Task<List<Order>> FinishedOrdersAsync(CancellationToken ct)
            {
                if (_orders == null)
                {
                    var list = await Db.Order.AsNoTracking()
                        .Where(o => !o.IsDeleted && o.MealDate >= From && o.MealDate < To && (o.Status == OrderStatus.Delivered || o.Status == OrderStatus.Reconciled)).ToListAsync(ct);
                    if (BranchId != null)
                    {
                        var customers = await CustomerIdsAsync(ct);
                        list = list.Where(o => customers.Contains(o.CustomerId)).ToList();
                    }
                    _orders = list;
                }
                return _orders;
            }
        }
    }
}
