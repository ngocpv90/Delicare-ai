using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// BE3.4 (spec 11.7, Catalog 222 / 223): KPI ngày / tháng cho màn <c>#/kpi-daily</c>, <c>#/kpi-monthly</c> – đọc <c>KpiDailySnapshot</c> / <c>KpiMonthlySnapshot</c>
    /// (số do <c>KpiSnapshot/Refresh</c> ghi), trả <c>{ metrics: [{ definition, latest: { value, target, variance, met }, series: [{ date, value }] }] }</c>.
    /// Chỉ tiêu = phiên bản mới nhất đang dùng (IsActive) của mỗi mã, lọc domainCode; mỗi ngày / tháng lấy dòng có phiên bản cao nhất.
    /// <c>met</c> = value ≥ target (≤ khi càng thấp càng tốt – WASTE, REJECT, EXPIRY, COST, LOSS hoặc theo thẻ mục tiêu).
    /// </summary>
    public class KpiSnapshotService(IUnitOfWork unitOfWork, IClock clock, IOptions<BusinessRulesOptions> rules) : IKpiSnapshotService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly KpiRulesOptions _kpi = rules.Value.Kpi;

        private sealed record Point(DateTime Date, int Version, decimal? Value, decimal? Numerator, decimal? Denominator, decimal? Target, decimal? Variance, string? Quality, DateTime CalculatedAt);

        public async Task<IResult<KpiDashboardModel>> GetDaily(KpiDailyRequest request)
        {
            try
            {
                string? error = ChartRules.DayRange(request.From, request.To, _clock.Today, _kpi.DailyDefaultRangeDays, _kpi.DailyMaxRangeDays, out DateTime from, out DateTime to);
                if (error != null)
                {
                    return Result<KpiDashboardModel>.Fail(error);
                }
                Guid? branchId = request.BranchId is { } b && b != Guid.Empty ? b : null;
                var db = _unitOfWork.DbContext;
                var rows = await db.KpiDailySnapshot.AsNoTracking()
                    .Where(x => !x.IsDeleted && x.SnapshotDate >= from && x.SnapshotDate <= to && x.BranchId == branchId)
                    .Select(x => new { x.MetricCode, P = new Point(x.SnapshotDate, x.MetricVersion, x.MetricValue, x.Numerator, x.Denominator, x.TargetValue, x.VarianceValue, x.DataQualityStatus, x.CalculatedAt) })
                    .ToListAsync();
                var model = await BuildAsync("DAILY", from, to, branchId, request.DomainCode, rows.Select(r => (r.MetricCode, r.P)).ToList());
                return Result<KpiDashboardModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetDaily: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<KpiDashboardModel>> GetMonthly(KpiMonthlyRequest request)
        {
            try
            {
                DateTime? fromMonth = null, toMonth = null;
                if (!string.IsNullOrWhiteSpace(request.FromMonth) && (fromMonth = KpiCalculationService.ParseMonth(request.FromMonth)) == null)
                {
                    return Result<KpiDashboardModel>.Fail(string.Format(ConstantHelper.Message.KpiMonthInvalid, request.FromMonth));
                }
                if (!string.IsNullOrWhiteSpace(request.ToMonth) && (toMonth = KpiCalculationService.ParseMonth(request.ToMonth)) == null)
                {
                    return Result<KpiDashboardModel>.Fail(string.Format(ConstantHelper.Message.KpiMonthInvalid, request.ToMonth));
                }
                int months = Math.Max(1, _kpi.MonthlyDefaultMonths);
                DateTime to = toMonth ?? fromMonth?.AddMonths(months - 1) ?? KpiCalculationService.MonthStart(_clock.Today);
                DateTime from = fromMonth ?? to.AddMonths(-(months - 1));
                int span = (to.Year - from.Year) * 12 + to.Month - from.Month + 1;
                if (from > to || span > Math.Max(1, _kpi.MonthlyMaxMonths))
                {
                    return Result<KpiDashboardModel>.Fail(string.Format(ConstantHelper.Message.KpiMonthRangeInvalid, _kpi.MonthlyMaxMonths));
                }
                Guid? branchId = request.BranchId is { } b && b != Guid.Empty ? b : null;
                var db = _unitOfWork.DbContext;
                var rows = await db.KpiMonthlySnapshot.AsNoTracking()
                    .Where(x => !x.IsDeleted && x.SnapshotMonth >= from && x.SnapshotMonth <= to && x.BranchId == branchId)
                    .Select(x => new { x.MetricCode, P = new Point(x.SnapshotMonth, x.MetricVersion, x.MetricValue, x.Numerator, x.Denominator, x.TargetValue, x.VarianceValue, x.DataQualityStatus, x.CalculatedAt) })
                    .ToListAsync();
                var model = await BuildAsync("MONTHLY", from, to, branchId, request.DomainCode, rows.Select(r => (r.MetricCode, r.P)).ToList());
                return Result<KpiDashboardModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetMonthly: ", ex.ToString()));
                throw;
            }
        }

        private async Task<KpiDashboardModel> BuildAsync(string period, DateTime from, DateTime to, Guid? branchId, string? domainCode, List<(string? Code, Point Point)> rows)
        {
            var db = _unitOfWork.DbContext;
            string? domain = string.IsNullOrWhiteSpace(domainCode) ? null : domainCode.Trim();
            var definitions = (await db.KpiMetricDefinition.AsNoTracking().Where(d => !d.IsDeleted && d.IsActive && (domain == null || d.DomainCode == domain)).ToListAsync())
                .GroupBy(d => d.MetricCode!, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(d => d.VersionNo).First())
                .OrderBy(d => d.DomainCode).ThenBy(d => d.MetricCode)
                .ToList();
            var byCode = rows.Where(r => r.Code != null).GroupBy(r => r.Code!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Select(r => r.Point)
                    .GroupBy(p => p.Date).Select(d => d.OrderByDescending(p => p.Version).First()).OrderBy(p => p.Date).ToList(), StringComparer.OrdinalIgnoreCase);
            var metrics = definitions.Select(d =>
            {
                var definition = ToModel(d);
                var points = byCode.GetValueOrDefault(d.MetricCode!) ?? [];
                var last = points.LastOrDefault();
                return new KpiMetricSeriesModel
                {
                    Definition = definition,
                    Latest = last == null ? null : new KpiLatestModel
                    {
                        Date = last.Date,
                        Value = last.Value,
                        Target = last.Target,
                        Variance = last.Variance,
                        Met = KpiCode.Met(last.Value, last.Target, definition.TargetDirection),
                        Numerator = last.Numerator,
                        Denominator = last.Denominator,
                        MetricVersion = last.Version,
                        DataQualityStatus = last.Quality,
                        CalculatedAt = last.CalculatedAt,
                    },
                    Series = points.Select(p => new KpiSeriesPointModel
                    {
                        Date = p.Date,
                        Value = p.Value,
                        Target = p.Target,
                        Met = KpiCode.Met(p.Value, p.Target, definition.TargetDirection),
                        DataQualityStatus = p.Quality,
                    }).ToList(),
                };
            }).ToList();
            return new KpiDashboardModel { Period = period, From = from, To = to, BranchId = branchId, DomainCode = domain, Metrics = metrics };
        }

        /// <summary>Định nghĩa → model (tách mục tiêu khỏi FormulaDescription – GAP-KPI-01).</summary>
        public static KpiDefinitionModel ToModel(KpiMetricDefinition d)
        {
            var (formula, target, direction) = KpiCode.ParseFormula(d.FormulaDescription, d.MetricCode);
            return new KpiDefinitionModel
            {
                Id = d.Id,
                MetricCode = d.MetricCode,
                MetricName = d.MetricName,
                DomainCode = d.DomainCode,
                UnitCode = d.UnitCode,
                Formula = formula,
                AggregationCode = d.AggregationCode,
                VersionNo = d.VersionNo,
                EffectiveFrom = d.EffectiveFrom,
                EffectiveTo = d.EffectiveTo,
                IsActive = d.IsActive,
                TargetValue = target,
                TargetDirection = direction,
                TargetDirectionName = KpiCode.DirectionLabel(direction),
                HasCalculator = KpiCalculationService.Calculators.Contains(d.MetricCode ?? string.Empty),
            };
        }
    }
}
