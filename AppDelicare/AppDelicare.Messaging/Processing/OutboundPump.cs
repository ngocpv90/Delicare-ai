using System.Collections.Concurrent;
using AppDelicare.Messaging.Channels;
using AppDelicare.Messaging.Channels.Telegram;
using AppDelicare.Messaging.Core;
using AppDelicare.Messaging.Core.Contracts;
using AppDelicare.Messaging.Options;
using AppDelicare.Messaging.Presentation;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AppDelicare.Messaging.Processing
{
    /// <summary>
    /// BOT-1.3: giới hạn tốc độ gửi – tối đa <c>Sending:MaxPerSecond</c> (25) tin / giây / bot và <c>PerChatPerSecond</c> (1) tin / giây / chat;
    /// bot bị Telegram trả 429 → tạm dừng bot đó <c>retry_after</c> giây.
    /// </summary>
    public sealed class SendRateLimiter(IOptionsMonitor<SendingOptions> options, TimeProvider time)
    {
        private readonly ConcurrentDictionary<string, Queue<DateTime>> _perBot = new();
        private readonly ConcurrentDictionary<string, DateTime> _lastPerChat = new();
        private readonly ConcurrentDictionary<string, DateTime> _pausedUntil = new();

        public void Pause(string bot, int seconds) => _pausedUntil[bot] = time.GetUtcNow().UtcDateTime.AddSeconds(seconds);

        public bool IsPaused(string bot) => _pausedUntil.TryGetValue(bot, out var until) && until > time.GetUtcNow().UtcDateTime;

        /// <summary>Thời gian phải chờ trước khi gửi (0 = gửi được ngay) – gọi xong <see cref="Record"/> sau khi gửi.</summary>
        public TimeSpan Delay(string bot, string chat)
        {
            var o = options.CurrentValue;
            DateTime now = time.GetUtcNow().UtcDateTime;
            TimeSpan wait = TimeSpan.Zero;
            var q = _perBot.GetOrAdd(bot, _ => new Queue<DateTime>());
            lock (q)
            {
                while (q.Count > 0 && (now - q.Peek()).TotalSeconds >= 1)
                {
                    q.Dequeue();
                }
                if (q.Count >= Math.Max(1, o.MaxPerSecond))
                {
                    wait = q.Peek().AddSeconds(1) - now;
                }
            }
            if (_lastPerChat.TryGetValue(bot + ":" + chat, out var last))
            {
                var chatWait = last.AddSeconds(1.0 / Math.Max(1, o.PerChatPerSecond)) - now;
                if (chatWait > wait)
                {
                    wait = chatWait;
                }
            }
            return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
        }

        public void Record(string bot, string chat)
        {
            DateTime now = time.GetUtcNow().UtcDateTime;
            var q = _perBot.GetOrAdd(bot, _ => new Queue<DateTime>());
            lock (q)
            {
                q.Enqueue(now);
            }
            _lastPerChat[bot + ":" + chat] = now;
        }
    }

    /// <summary>
    /// BOT-1.3: gửi outbox – <c>BotChannel/ClaimOutbound</c> (api đã lọc notBefore + giãn cách thử lại) → dựng mẫu tin (Presentation) → gửi (giới hạn tốc độ)
    /// → <c>MarkOutbound</c>: Sent (message_id) / Blocked (403 – người dùng chặn bot) / Failed (api tự thử lại 1′, 5′, 15′, 60′, 240′; 429 → tạm dừng bot).
    /// Tin mức THƯỜNG gửi im lặng (disable_notification), KHẨN có chuông.
    /// </summary>
    public sealed class OutboundPump(IBotChannelApi api, IChannelSender sender, SendRateLimiter limiter, IOptionsMonitor<MessagingHostOptions> host,
        IOptionsMonitor<SendingOptions> sending, IOptionsMonitor<TelegramOptions> telegram, ILogger<OutboundPump> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                int sent = 0;
                try
                {
                    sent = await RunOnceAsync(stoppingToken);
                }
                catch (ApiUnavailableException ex)
                {
                    logger.LogWarning("OutboundPump: api không trả lời ({Error})", ex.Message);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "OutboundPump lỗi");
                }
                if (sent == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, host.CurrentValue.OutboundScanSeconds)), stoppingToken);
                }
            }
        }

        public async Task<int> RunOnceAsync(CancellationToken ct)
        {
            int handled = 0;
            foreach (var bot in telegram.CurrentValue.Bots.Where(b => telegram.CurrentValue.Find(b.Key) != null).Select(b => b.Key.ToUpperInvariant()))
            {
                if (limiter.IsPaused(bot))
                {
                    continue;
                }
                var items = await api.ClaimOutboundAsync(bot, Math.Max(1, sending.CurrentValue.BatchSize), ct);
                foreach (var item in items)
                {
                    handled++;
                    if (limiter.IsPaused(bot))
                    {
                        // Bị 429 giữa lô → trả về Failed (api thử lại sau giãn cách) thay vì giữ Sending.
                        await api.MarkOutboundAsync(new BotMarkOutboundRequest { Id = item.Id, Status = "Failed", Error = "Tạm dừng do Telegram giới hạn tốc độ", HttpStatus = 429 }, ct);
                        continue;
                    }
                    await SendOneAsync(bot, item, ct);
                }
            }
            return handled;
        }

        private async Task SendOneAsync(string bot, BotOutboundItemModel item, CancellationToken ct)
        {
            string? chat = item.ExternalConversationId ?? item.RecipientExternalId;
            if (string.IsNullOrWhiteSpace(chat))
            {
                await api.MarkOutboundAsync(new BotMarkOutboundRequest { Id = item.Id, Status = "Failed", Error = "Thiếu người nhận" }, ct);
                return;
            }
            var neutral = MessageTemplates.Render(item.PayloadJson);
            var (html, keyboard) = TelegramFormatter.Render(neutral);
            var wait = limiter.Delay(bot, chat);
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, ct);
            }
            var result = await sender.SendAsync(bot, new OutgoingMessage(chat, html, keyboard, DisableNotification: neutral.Level != "URGENT"), ct);
            limiter.Record(bot, chat);
            var mark = new BotMarkOutboundRequest { Id = item.Id, ProviderMessageId = result.ProviderMessageId, Error = result.Error, HttpStatus = result.HttpStatus };
            switch (result.Status)
            {
                case ChannelSendStatus.Sent:
                    mark.Status = "Sent";
                    // BOT-2.1: Thẻ ca / Thẻ ngày → ghim đầu chat (lỗi ghim không làm hỏng tin).
                    if (MessageTemplates.IsPinned(item.MessageType) && result.ProviderMessageId != null)
                    {
                        await sender.PinAsync(bot, chat, result.ProviderMessageId, ct);
                    }
                    break;
                case ChannelSendStatus.Blocked:
                    mark.Status = "Blocked";
                    break;
                case ChannelSendStatus.RetryAfter:
                    limiter.Pause(bot, result.RetryAfterSeconds ?? 5);
                    mark.Status = "Failed";
                    break;
                default:
                    mark.Status = "Failed";
                    break;
            }
            await api.MarkOutboundAsync(mark, ct);
        }
    }
}
