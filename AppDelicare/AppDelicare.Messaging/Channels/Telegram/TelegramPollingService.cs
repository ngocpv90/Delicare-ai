using AppDelicare.Messaging.Options;
using AppDelicare.Messaging.Processing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AppDelicare.Messaging.Channels.Telegram
{
    /// <summary>
    /// BOT-1.3: chỉ khi <c>Messaging:Mode = Polling</c> (máy dev – bộ bot DEV, không cần domain). Mỗi bot có token: deleteWebhook rồi getUpdates (long polling);
    /// update lưu qua api như webhook; api không trả lời → giữ offset, thử lại (không mất update).
    /// </summary>
    public sealed class TelegramPollingService(ITelegramClient client, UpdateIngestor ingestor, IOptionsMonitor<MessagingHostOptions> host,
        IOptionsMonitor<TelegramOptions> telegram, ILogger<TelegramPollingService> logger) : BackgroundService
    {
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (host.CurrentValue.Mode != TelegramMode.Polling)
            {
                return Task.CompletedTask;
            }
            var bots = telegram.CurrentValue.Bots.Keys.Where(k => telegram.CurrentValue.Find(k) != null).Select(k => k.ToUpperInvariant()).ToList();
            if (bots.Count == 0)
            {
                logger.LogWarning("Polling: chưa có bot nào có token (Telegram:Bots:<VH|DH>:Token)");
                return Task.CompletedTask;
            }
            return Task.WhenAll(bots.Select(b => PollAsync(b, stoppingToken)));
        }

        private async Task PollAsync(string bot, CancellationToken ct)
        {
            long offset = 0;
            try
            {
                await client.DeleteWebhookAsync(bot, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning("Polling {Bot}: deleteWebhook lỗi ({Error})", bot, ex.Message);
            }
            logger.LogInformation("Polling {Bot}: bắt đầu", bot);
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var updates = await client.GetUpdatesAsync(bot, offset, Math.Clamp(telegram.CurrentValue.PollingTimeoutSeconds, 1, 50), TelegramClient.DefaultAllowedUpdates, ct);
                    foreach (var raw in updates)
                    {
                        long id = raw.TryGetProperty("update_id", out var u) ? u.GetInt64() : 0;
                        var result = await ingestor.IngestAsync(bot, raw.GetRawText(), ct);
                        if (result == IngestResult.Unavailable)
                        {
                            throw new InvalidOperationException("api không trả lời");
                        }
                        offset = Math.Max(offset, id + 1);
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogWarning("Polling {Bot}: {Error} – thử lại sau 5″", bot, ex.Message);
                    await Task.Delay(TimeSpan.FromSeconds(5), ct);
                }
            }
        }
    }
}
