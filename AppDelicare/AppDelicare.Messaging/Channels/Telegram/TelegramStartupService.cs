using AppDelicare.Messaging.Channels.Telegram.Models;
using AppDelicare.Messaging.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AppDelicare.Messaging.Channels.Telegram
{
    /// <summary>
    /// BOT-1.3: khi khởi động – mọi chế độ: đặt danh sách lệnh "/" mặc định; <c>Mode = Webhook</c>: setWebhook {WebhookBaseUrl}/tg/{bot} + secret_token.
    /// Lỗi chỉ ghi log (không chặn khởi động – webhook cũ vẫn chạy). Lệnh theo quyền từng chat: BOT-1.4 (CommandScopeSync).
    /// </summary>
    public sealed class TelegramStartupService(ITelegramClient client, IOptionsMonitor<MessagingHostOptions> host, IOptionsMonitor<TelegramOptions> telegram,
        ILogger<TelegramStartupService> logger) : BackgroundService
    {
        public static readonly TgBotCommand[] DefaultCommands =
        [
            new() { Command = "start", Description = "Liên kết / mở lại bot" },
            new() { Command = "menu", Description = "Chức năng" },
            new() { Command = "help", Description = "Trợ giúp" },
            new() { Command = "huylienket", Description = "Hủy liên kết tài khoản" },
        ];

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var o = host.CurrentValue;
            foreach (var bot in telegram.CurrentValue.Bots.Keys.Where(k => telegram.CurrentValue.Find(k) != null).Select(k => k.ToUpperInvariant()))
            {
                try
                {
                    await client.SetMyCommandsAsync(bot, DefaultCommands, null, stoppingToken);
                    if (o.Mode == TelegramMode.Webhook)
                    {
                        var cfg = telegram.CurrentValue.Find(bot)!;
                        if (string.IsNullOrWhiteSpace(o.WebhookBaseUrl) || string.IsNullOrWhiteSpace(cfg.WebhookSecret))
                        {
                            logger.LogError("Webhook {Bot}: thiếu Messaging:WebhookBaseUrl hoặc Telegram:Bots:{Bot}:WebhookSecret", bot, bot);
                            continue;
                        }
                        await client.SetWebhookAsync(bot, o.WebhookBaseUrl.TrimEnd('/') + "/tg/" + bot, cfg.WebhookSecret, TelegramClient.DefaultAllowedUpdates, stoppingToken);
                        logger.LogInformation("Webhook {Bot}: đã đặt", bot);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError("Khởi động bot {Bot} lỗi: {Error}", bot, ex.Message);
                }
            }
        }
    }
}
