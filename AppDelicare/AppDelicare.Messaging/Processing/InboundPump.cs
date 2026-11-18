using AppDelicare.Messaging.Core;
using AppDelicare.Messaging.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AppDelicare.Messaging.Processing
{
    /// <summary>
    /// BOT-1.3: xử lý update đã lưu – ngay khi webhook / polling báo (<see cref="InboundSignal"/>) và quét <c>BotChannel/ClaimInbound</c> mỗi
    /// <c>Messaging:InboundScanSeconds</c> (5″) → khởi động lại không mất tin (dòng Received / Processing quá hạn được lấy lại). Mỗi dòng → CompleteInbound.
    /// </summary>
    public sealed class InboundPump(IBotChannelApi api, UpdateDispatcher dispatcher, InboundSignal signal, IOptionsMonitor<MessagingHostOptions> options,
        ILogger<InboundPump> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                int handled = 0;
                try
                {
                    handled = await RunOnceAsync(stoppingToken);
                }
                catch (ApiUnavailableException ex)
                {
                    logger.LogWarning("InboundPump: api không trả lời ({Error})", ex.Message);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "InboundPump lỗi");
                }
                if (handled == 0)
                {
                    await signal.WaitAsync(TimeSpan.FromSeconds(Math.Max(1, options.CurrentValue.InboundScanSeconds)), stoppingToken);
                }
            }
        }

        /// <summary>1 lượt: lấy tối đa 20 update → xử lý lần lượt (giữ thứ tự) → báo kết quả. Trả số update đã xử lý.</summary>
        public async Task<int> RunOnceAsync(CancellationToken ct)
        {
            var items = await api.ClaimInboundAsync(null, 20, ct);
            foreach (var item in items)
            {
                DispatchResult result;
                try
                {
                    result = await dispatcher.HandleAsync(item, ct);
                }
                catch (ApiUnavailableException)
                {
                    throw; // giữ Processing – hết hạn giữ (5′) thì được lấy lại
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Xử lý update {Id} lỗi", item.Id);
                    result = new DispatchResult("Failed", ex.GetType().Name + ": " + ex.Message);
                }
                await api.CompleteInboundAsync(item.Id, result.Status, result.Error, ct);
            }
            return items.Count;
        }
    }
}
