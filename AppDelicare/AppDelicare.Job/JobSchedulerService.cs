using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AppDelicare.Services.Jobs;
using Serilog;

namespace AppDelicare.Job
{
    /// <summary>
    /// T23 (D-48): mỗi <c>JobHost:PollSeconds</c> (mặc định 30 giây) hỏi <see cref="IJobRunService.GetDueJobCodes"/> – job đến giờ (giờ VN)
    /// mà hôm nay chưa có lần chạy – rồi chạy lần lượt, mỗi job 1 DI scope riêng. Chống chạy trùng bằng <c>sp_getapplock</c> trong JobRunService
    /// (kể cả khi lỡ chạy 2 bản Job). Lỗi 1 job không dừng vòng lặp.
    /// </summary>
    public sealed class JobSchedulerService(IServiceProvider services, IConfiguration configuration) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            int pollSeconds = Math.Clamp(configuration.GetValue("JobHost:PollSeconds", 30), 5, 300);
            // T48.4 (D-60): file nhịp cho Docker HEALTHCHECK (--healthcheck) + cảnh báo lệch giờ SQL Server.
            string heartbeat = configuration[JobHeartbeat.FileKey] is { Length: > 0 } hb ? hb : JobHeartbeat.DefaultPath;
            Log.Information("[Job] AppDelicare.Job bắt đầu – kiểm job mỗi {Seconds} giây, file nhịp {Heartbeat}", pollSeconds, heartbeat);
            await WarnDatabaseClockAsync();
            AppDelicare.Services.Ioc.ConfigDefaultsCheck.Warn(configuration, "Job"); // UI-6.47
            await LogScheduleAsync();
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    Beat(heartbeat);
                    List<string> due;
                    await using (var scope = services.CreateAsyncScope())
                    {
                        due = await scope.ServiceProvider.GetRequiredService<IJobRunService>().GetDueJobCodes();
                    }
                    foreach (string code in due)
                    {
                        if (stoppingToken.IsCancellationRequested)
                        {
                            break;
                        }
                        await using var scope = services.CreateAsyncScope();
                        await scope.ServiceProvider.GetRequiredService<IJobRunService>().Run(code, JobTrigger.Schedule, stoppingToken);
                        Beat(heartbeat);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Mất kết nối DB… – ghi log, thử lại ở vòng sau.
                    Log.Error(ex, "[Job] vòng kiểm job lỗi");
                }
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(pollSeconds), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
            Log.Information("[Job] AppDelicare.Job dừng");
        }

        private static void Beat(string path)
        {
            try
            {
                JobHeartbeat.Write(path, DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[Job] không ghi được file nhịp {Path}", path);
            }
        }

        private async Task WarnDatabaseClockAsync()
        {
            try
            {
                await using var scope = services.CreateAsyncScope();
                var clock = scope.ServiceProvider.GetRequiredService<AppDelicare.Data.Share.IClock>();
                string connection = configuration.GetConnectionString(AppDelicare.Model.Helper.ConstantHelper.MainConnectionString) ?? string.Empty;
                TimeSpan drift = await AppDelicare.Services.Ioc.DatabaseClockCheck.GetDriftAsync(connection, clock);
                if (drift.Duration() > AppDelicare.Services.Ioc.DatabaseClockCheck.MaxDrift)
                {
                    Log.Warning("[Job] {Message}", AppDelicare.Services.Ioc.DatabaseClockCheck.Describe(drift));
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[Job] không kiểm được giờ SQL Server");
            }
        }

        private async Task LogScheduleAsync()
        {
            try
            {
                await using var scope = services.CreateAsyncScope();
                foreach (var job in await scope.ServiceProvider.GetRequiredService<IJobRunService>().GetJobs())
                {
                    Log.Information("[Job] {JobCode} {State} {At} – kế tiếp {Next:dd/MM/yyyy HH:mm}", job.JobCode, job.Enabled ? "bật" : "TẮT", job.At, job.NextRunAt);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[Job] không đọc được danh sách job");
            }
        }
    }
}
