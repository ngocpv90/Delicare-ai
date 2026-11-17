using AppDelicare.Model.Helper;
using AppDelicare.Services.Interface;

namespace AppDelicare.Services.Jobs
{
    // BE3.4 (spec 11.7 "Job KpiDailyJob", D-48): job KPI gọi cùng code với nút KpiSnapshot/Refresh (IKpiCalculationService.Calculate).
    // Mã job = KPI_DAILY / KPI_MONTHLY = AnalyticsRefreshLog.JobCode = khóa sp_getapplock của nút bấm tay → không chạy trùng.
    // Cấu hình Jobs:KPI_DAILY / Jobs:KPI_MONTHLY đang TẮT (Enabled = false – D-45, D-57); bật khi anh duyệt (PROPOSED D-57 aa).

    /// <summary>23:30 – KPI ngày hôm nay, toàn công ty (spec: 17:00 và 23:30 – 1 mốc / job; 17:00 bấm tay nếu cần).</summary>
    public sealed class KpiDailyJob(IKpiCalculationService kpiCalculationService) : IScheduledJob
    {
        public string Code => KpiCode.JobDaily;
        public string Name => "KPI ngày";
        public string Description => "Tính KPI hôm nay (toàn công ty) rồi ghi KpiDailySnapshot – chạy lại không nhân đôi (= nút KpiSnapshot/Refresh KPI_DAILY).";
        public string DefaultAt => "23:30";

        public async Task<JobOutcome> RunAsync(DateTime now, CancellationToken cancellationToken)
        {
            var result = await kpiCalculationService.Calculate(KpiCode.JobDaily, now.Date, null, JobTrigger.Schedule, cancellationToken);
            return new JobOutcome(JobRunStatus.Succeeded, result.RowsProcessed, now.Date,
                string.Format(ConstantHelper.Message.KpiRefreshDone, KpiCode.JobDaily, result.Metrics.Count, result.IncompleteCount));
        }
    }

    /// <summary>23:45 – KPI tháng này (tới hôm nay), toàn công ty.</summary>
    public sealed class KpiMonthlyJob(IKpiCalculationService kpiCalculationService) : IScheduledJob
    {
        public string Code => KpiCode.JobMonthly;
        public string Name => "KPI tháng";
        public string Description => "Tính KPI tháng này tới hôm nay (toàn công ty) rồi ghi KpiMonthlySnapshot – chạy lại không nhân đôi (= nút KpiSnapshot/Refresh KPI_MONTHLY).";
        public string DefaultAt => "23:45";

        public async Task<JobOutcome> RunAsync(DateTime now, CancellationToken cancellationToken)
        {
            var result = await kpiCalculationService.Calculate(KpiCode.JobMonthly, now.Date, null, JobTrigger.Schedule, cancellationToken);
            return new JobOutcome(JobRunStatus.Succeeded, result.RowsProcessed, new DateTime(now.Year, now.Month, 1),
                string.Format(ConstantHelper.Message.KpiRefreshDone, KpiCode.JobMonthly, result.Metrics.Count, result.IncompleteCount));
        }
    }
}
