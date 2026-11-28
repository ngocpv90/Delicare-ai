using AppDelicare.Data.Share;
using AppDelicare.Model.Option;
using AppDelicare.Services.Implement.Agent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;

namespace AppDelicare.Job
{
    /// <summary>
    /// AI-25.5 (D-143): chạy <see cref="IAgentEventPumpService"/> mỗi <c>Agent:EventPumpMinutes</c> (mặc định 5) – thay lịch Hermes vivi-events (mode template, 0 token).
    /// Mỗi vòng 1 DI scope, dưới tài khoản <c>SystemUsers:JobUserId</c>; lỗi 1 vòng chỉ ghi log.
    /// </summary>
    public sealed class AgentEventPumpHost(IServiceProvider services, IConfiguration configuration, IOptionsMonitor<AgentOptions> options) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!Guid.TryParse(configuration[$"{SystemUsersOptions.Section}:{nameof(SystemUsersOptions.JobUserId)}"], out Guid jobUser) || jobUser == Guid.Empty)
            {
                Log.Warning("[AgentEventPump] thiếu SystemUsers:JobUserId – không chạy cảnh báo theo mẫu");
                return;
            }
            Log.Information("[AgentEventPump] bắt đầu – mỗi {Minutes} phút", Math.Clamp(options.CurrentValue.EventPumpMinutes, 1, 60));
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await using var scope = services.CreateAsyncScope();
                    using (scope.ServiceProvider.GetRequiredService<ICurrentActor>().BeginScope(ActorInfo.ForSystem(jobUser), "evt-pump"))
                    {
                        var r = await scope.ServiceProvider.GetRequiredService<IAgentEventPumpService>().RunOnceAsync(stoppingToken);
                        if (r.Sent > 0)
                        {
                            Log.Information("[AgentEventPump] {Alerts} cảnh báo, gửi {Sent} tin, {Ai} nhận xét AI", r.Alerts, r.Sent, r.AiComments);
                        }
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[AgentEventPump] vòng lỗi");
                }
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(Math.Clamp(options.CurrentValue.EventPumpMinutes, 1, 60)), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
