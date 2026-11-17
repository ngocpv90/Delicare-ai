using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace AppDelicare.Services.Jobs
{
    public interface IJobRunService
    {
        /// <summary>Danh sách job + bật / tắt + giờ chạy + lần chạy kế tiếp + lần chạy gần nhất.</summary>
        Task<List<JobInfoModel>> GetJobs();

        /// <summary>Job đến giờ mà hôm nay chưa chạy (kể cả lỡ giờ – chạy bù trong ngày). Chỉ job Enabled.</summary>
        Task<List<string>> GetDueJobCodes();

        /// <summary>
        /// Chạy 1 job: khóa <c>sp_getapplock</c> theo mã job (không chạy trùng kể cả nhiều instance) → ghi <c>AnalyticsRefreshLog</c> Running →
        /// gọi job → cập nhật kết quả. SCHEDULE chạy dưới tài khoản <c>SystemUsers:JobUserId</c>; MANUAL chạy dưới người bấm.
        /// </summary>
        Task<IResult<JobRunResultModel>> Run(string jobCode, string trigger, CancellationToken cancellationToken = default);
    }

    /// <summary>T23 (D-48): điều phối job – dùng chung cho AppDelicare.Job (hẹn giờ) và <c>api/Job/Run</c> (bấm tay).</summary>
    public sealed class JobRunService(IEnumerable<IScheduledJob> jobs, IConfiguration configuration, IClock clock, ICurrentActor currentActor) : IJobRunService
    {
        private readonly List<IScheduledJob> _jobs = jobs.OrderBy(j => j.Code, StringComparer.Ordinal).ToList();
        private readonly IConfiguration _configuration = configuration;
        private readonly IClock _clock = clock;
        private readonly ICurrentActor _currentActor = currentActor;
        // BE3.4: khóa sp_getapplock + AnalyticsRefreshLog tách ra AnalyticsRunGate (dùng chung với nút làm mới KPI) – hành vi T23 giữ nguyên.
        private readonly AnalyticsRunGate _gate = new(configuration, clock);

        private string ConnectionString => _configuration.GetConnectionString(ConstantHelper.MainConnectionString)
            ?? throw new InvalidOperationException("Thiếu ConnectionStrings:MainConnectionString.");

        private JobScheduleOptions Schedule(IScheduledJob job)
            => _configuration.GetSection($"{JobScheduleOptions.Section}:{job.Code}").Get<JobScheduleOptions>() ?? new JobScheduleOptions();

        private TimeSpan At(IScheduledJob job) => JobSchedule.ParseAt(Schedule(job).At, job.DefaultAt);

        private IScheduledJob? Find(string? code)
            => _jobs.FirstOrDefault(j => string.Equals(j.Code, code?.Trim(), StringComparison.OrdinalIgnoreCase));

        public async Task<List<JobInfoModel>> GetJobs()
        {
            DateTime now = _clock.Now;
            var last = await LastRunsAsync();
            return _jobs.Select(job =>
            {
                TimeSpan at = At(job);
                return new JobInfoModel
                {
                    JobCode = job.Code,
                    JobName = job.Name,
                    Description = job.Description,
                    Enabled = Schedule(job).Enabled,
                    At = at.ToString(@"hh\:mm"),
                    NextRunAt = JobSchedule.NextRun(now, at),
                    LastRun = last.GetValueOrDefault(job.Code),
                };
            }).ToList();
        }

        public async Task<List<string>> GetDueJobCodes()
        {
            DateTime now = _clock.Now;
            var last = await LastRunsAsync();
            return _jobs
                .Where(job => Schedule(job).Enabled)
                .Where(job => JobSchedule.DueSlot(now, At(job)) is { } slot
                              && !(last.GetValueOrDefault(job.Code) is { } run && run.StartedAt >= slot))
                .Select(job => job.Code)
                .ToList();
        }

        public async Task<IResult<JobRunResultModel>> Run(string jobCode, string trigger, CancellationToken cancellationToken = default)
        {
            IScheduledJob? job = Find(jobCode);
            if (job == null)
            {
                return Result<JobRunResultModel>.Fail(string.Format(ConstantHelper.Message.JobNotFound, jobCode, string.Join(", ", _jobs.Select(j => j.Code))));
            }
            ActorInfo actor;
            if (trigger == JobTrigger.Schedule)
            {
                string? configured = _configuration[$"{SystemUsersOptions.Section}:{nameof(SystemUsersOptions.JobUserId)}"];
                if (!Guid.TryParse(configured, out Guid jobUserId) || jobUserId == Guid.Empty)
                {
                    return Result<JobRunResultModel>.Fail(ConstantHelper.Message.JobUserMissing);
                }
                actor = ActorInfo.ForSystem(jobUserId);
            }
            else
            {
                actor = _currentActor.RequireActor();
            }

            var run = new JobRunResultModel
            {
                JobCode = job.Code,
                JobName = job.Name,
                RunId = Guid.NewGuid(),
                Trigger = trigger,
                StartedAt = _clock.Now,
            };
            await using var runLock = await _gate.TryAcquireAsync(job.Code, cancellationToken);
            if (runLock == null)
            {
                run.RunStatus = JobRunStatus.Locked;
                run.CompletedAt = _clock.Now;
                run.Message = ConstantHelper.Message.JobAlreadyRunning;
                await InsertLogAsync(run, actor.UserId);
                Log.Warning("[Job] {JobCode} {Trigger}: đang có lần chạy khác – bỏ qua (RunId {RunId})", job.Code, trigger, run.RunId);
                return Result<JobRunResultModel>.Fail(run.Message);
            }
            run.RunStatus = JobRunStatus.Running;
            await InsertLogAsync(run, actor.UserId);
            Log.Information("[Job] {JobCode} {Trigger} bắt đầu (RunId {RunId}, actor {Actor})", job.Code, trigger, run.RunId, actor);
            JobOutcome outcome;
            try
            {
                using (_currentActor.BeginScope(actor, $"job-{job.Code}-{run.RunId:N}"))
                {
                    outcome = await job.RunAsync(run.StartedAt, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[Job] {JobCode} lỗi (RunId {RunId})", job.Code, run.RunId);
                outcome = new JobOutcome(JobRunStatus.Failed, 0, null, ex.Message);
            }
            run.RunStatus = outcome.Status;
            run.RowsProcessed = outcome.RowsProcessed;
            run.TargetDate = outcome.TargetDate;
            run.Message = outcome.Message;
            run.CompletedAt = _clock.Now;
            await UpdateLogAsync(run, actor.UserId);
            Log.Information("[Job] {JobCode} xong: {RunStatus}, {Rows} dòng, ngày {TargetDate:dd/MM/yyyy} – {Message}",
                job.Code, run.RunStatus, run.RowsProcessed, run.TargetDate, run.Message);
            return run.RunStatus == JobRunStatus.Failed
                ? Result<JobRunResultModel>.Fail(string.Format(ConstantHelper.Message.JobFailed, job.Code, run.Message))
                : Result<JobRunResultModel>.Success(run, run.Message ?? ConstantHelper.Message.SaveSuccess);
        }

        // AnalyticsRefreshLog (bảng có sẵn): JobCode, RunId, StartedAt, CompletedAt, RunStatus, RowsProcessed,
        // SourceWatermark = "<trigger>|<ngày nghiệp vụ yyyy-MM-dd>", ErrorMessage = thông báo (≤ 2000 – độ dài cột).
        private Task InsertLogAsync(JobRunResultModel run, Guid actorId) => _gate.InsertLogAsync(Entry(run), actorId);

        private Task UpdateLogAsync(JobRunResultModel run, Guid actorId) => _gate.UpdateLogAsync(Entry(run), actorId);

        private static AnalyticsRunEntry Entry(JobRunResultModel run) => new()
        {
            RunId = run.RunId,
            JobCode = run.JobCode,
            StartedAt = run.StartedAt,
            CompletedAt = run.CompletedAt,
            RunStatus = run.RunStatus,
            RowsProcessed = run.RowsProcessed,
            SourceWatermark = Watermark(run),
            Message = run.Message,
        };

        private async Task<Dictionary<string, JobRunResultModel>> LastRunsAsync()
        {
            if (_jobs.Count == 0)
            {
                return [];
            }
            await using var connection = new SqlConnection(ConnectionString);
            var rows = await connection.QueryAsync<LogRow>(
                "SELECT l.JobCode, l.RunId, l.StartedAt, l.CompletedAt, l.RunStatus, l.RowsProcessed, l.SourceWatermark, l.ErrorMessage"
                + " FROM (SELECT *, ROW_NUMBER() OVER (PARTITION BY JobCode ORDER BY StartedAt DESC) AS rn"
                + "       FROM dbo.AnalyticsRefreshLog WHERE IsDeleted = 0 AND JobCode IN @Codes) AS l WHERE l.rn = 1;",
                new { Codes = _jobs.Select(j => j.Code).ToArray() });
            return rows.ToDictionary(r => r.JobCode, r => ToModel(r), StringComparer.OrdinalIgnoreCase);
        }

        private JobRunResultModel ToModel(LogRow row)
        {
            string[] mark = (row.SourceWatermark ?? string.Empty).Split('|');
            return new JobRunResultModel
            {
                JobCode = row.JobCode,
                JobName = Find(row.JobCode)?.Name ?? row.JobCode,
                RunId = row.RunId,
                Trigger = mark.ElementAtOrDefault(0) ?? string.Empty,
                TargetDate = DateTime.TryParse(mark.ElementAtOrDefault(1), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime d) ? d : null,
                RunStatus = row.RunStatus,
                StartedAt = row.StartedAt,
                CompletedAt = row.CompletedAt,
                RowsProcessed = row.RowsProcessed ?? 0,
                Message = row.ErrorMessage,
            };
        }

        private static string Watermark(JobRunResultModel run) => $"{run.Trigger}|{run.TargetDate:yyyy-MM-dd}";

        private sealed class LogRow
        {
            public string JobCode { get; set; } = string.Empty;
            public Guid RunId { get; set; }
            public DateTime StartedAt { get; set; }
            public DateTime? CompletedAt { get; set; }
            public string RunStatus { get; set; } = string.Empty;
            public long? RowsProcessed { get; set; }
            public string? SourceWatermark { get; set; }
            public string? ErrorMessage { get; set; }
        }
    }
}
