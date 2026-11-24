using AppDelicare.Model.Helper;
using AppDelicare.Services.Interface;

namespace AppDelicare.Services.Jobs
{
    // BE3.4 (spec 11.7 "Job KpiDailyJob", D-48): job KPI gọi cùng code với nút KpiSnapshot/Refresh (IKpiCalculationService.Calculate).
    // Mã job = KPI_DAILY / KPI_MONTHLY = AnalyticsRefreshLog.JobCode = khóa sp_getapplock của nút bấm tay → không chạy trùng.
    // BIZ-3.5 (D-77 – C13): BẬT – KPI ngày 22:00; KPI tháng 01:00 tính tháng của hôm qua (ngày 1 chốt tháng trước). Lỗi job → bot IT (JobRunService).

    /// <summary>23:30 – KPI ngày hôm nay, toàn công ty (spec: 17:00 và 23:30 – 1 mốc / job; 17:00 bấm tay nếu cần).</summary>
    public sealed class KpiDailyJob(IKpiCalculationService kpiCalculationService) : IScheduledJob
    {
        public string Code => KpiCode.JobDaily;
        public string Name => "KPI ngày";
        public string Description => "Tính KPI hôm nay (toàn công ty) rồi lưu chỉ số ngày – chạy lại không nhân đôi (như nút Tính lại chỉ số ngày).";
        public string DefaultAt => "22:00";

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
        public string Description => "01:00 – tính KPI tháng của hôm qua (ngày 1 = chốt tháng trước; toàn công ty) rồi lưu chỉ số tháng – chạy lại không nhân đôi (= nút KpiSnapshot/Refresh KPI_MONTHLY).";
        public string DefaultAt => "01:00";

        public async Task<JobOutcome> RunAsync(DateTime now, CancellationToken cancellationToken)
        {
            DateTime day = now.Date.AddDays(-1);
            var result = await kpiCalculationService.Calculate(KpiCode.JobMonthly, day, null, JobTrigger.Schedule, cancellationToken);
            return new JobOutcome(JobRunStatus.Succeeded, result.RowsProcessed, new DateTime(day.Year, day.Month, 1),
                string.Format(ConstantHelper.Message.KpiRefreshDone, KpiCode.JobMonthly, result.Metrics.Count, result.IncompleteCount));
        }
    }
}
