using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using AppDelicare.Messaging.Audiences.Internal;
using AppDelicare.Messaging.Audiences.Internal.Chung;
using AppDelicare.Messaging.Callback;
using AppDelicare.Messaging.Channels;
using AppDelicare.Messaging.Channels.Telegram;
using AppDelicare.Messaging.Channels.Telegram.Models;
using AppDelicare.Messaging.Core;
using AppDelicare.Messaging.Core.Contracts;
using AppDelicare.Messaging.Presentation;
using Microsoft.Extensions.Configuration;

namespace AppDelicare.Messaging.Experience
{
    /// <summary>
    /// BOT-2.1 (BOT_TELEGRAM §1.1): Thẻ ca. Thẻ mới do api xếp qua outbox (OutboundPump gửi + ghim); ở đây: làm mới thẻ đã gửi (sửa tin, chỉ khi nội dung đổi và
    /// cách lần sửa trước ≥ <c>Messaging:ShiftCard:MinEditMinutes</c> = 5′), ⚡ Ca của tôi (sửa thẻ ghim / gửi thẻ mới), nút trên thẻ <c>sc:run:&lt;mã chức năng&gt;</c>
    /// (chạy như gõ lệnh → kết quả là tin mới, không đè thẻ ghim; kiểm lại quyền theo danh mục) và <c>sc:refresh</c>.
    /// </summary>
    public sealed class ShiftCards(IBotChannelApi api, IChannelSender sender, BotActionRegistry registry, HelpActions help, IConfiguration configuration, TimeProvider time)
    {
        public const string Kind = "sc";
        private readonly ConcurrentDictionary<string, (string Hash, DateTimeOffset At)> _edited = new();

        private TimeSpan MinEdit => TimeSpan.FromMinutes(int.TryParse(configuration["Messaging:ShiftCard:MinEditMinutes"], out var m) && m >= 0 ? m : 5);

        /// <summary>1 vòng làm mới (ShiftCardPump gọi mỗi phút). Trả số thẻ đã sửa.</summary>
        public async Task<int> RefreshAsync(CancellationToken ct)
        {
            int edited = 0;
            foreach (var card in await api.SyncShiftCardsAsync(ct))
            {
                if (card.ProviderMessageId == null || card.ChatId == null || card.BotCode == null)
                {
                    continue;
                }
                string key = $"{card.BotCode}:{card.ChatId}:{card.ProviderMessageId}";
                string hash = Hash(card.PayloadJson);
                var now = time.GetUtcNow();
                if (_edited.TryGetValue(key, out var last) && (last.Hash == hash || now - last.At < MinEdit))
                {
                    continue;
                }
                var (html, kb) = TelegramFormatter.Render(MessageTemplates.Render(card.PayloadJson));
                await sender.EditAsync(card.BotCode, card.ChatId, card.ProviderMessageId, html, kb, ct);
                _edited[key] = (hash, now);
                edited++;
            }
            return edited;
        }

        /// <summary>⚡ Ca của tôi: có thẻ ca → sửa thẻ đã ghim (hoặc tin vừa bấm 🔄) / gửi + ghim thẻ mới; không có ca / chưa gắn nhân viên → màn ca cũ (HelpActions).</summary>
        public async Task MyCardAsync(BotActionContext ctx, CancellationToken ct)
        {
            var r = await api.MyShiftCardAsync(new BotIdentityRequest { BotCode = ctx.Bot, ExternalUserId = ctx.Event.FromId }, ct);
            if (!r.IsOk || r.Data == null)
            {
                await help.MyShiftAsync(ctx.Bot, ctx.Event, ctx.Identity, ct);
                return;
            }
            var (html, kb) = TelegramFormatter.Render(MessageTemplates.Render(r.Data.PayloadJson));
            string? target = ctx.Event.Kind == IncomingKind.Callback && ctx.Event.MessageId is { } mid ? mid.ToString() : r.Data.ProviderMessageId;
            if (target != null)
            {
                var edit = await sender.EditAsync(ctx.Bot, ctx.Event.ChatId, target, html, kb, ct);
                if (edit.Status == ChannelSendStatus.Sent || (edit.Error ?? string.Empty).Contains("not modified", StringComparison.OrdinalIgnoreCase))
                {
                    _edited[$"{ctx.Bot}:{ctx.Event.ChatId}:{target}"] = (Hash(r.Data.PayloadJson), time.GetUtcNow());
                    if (ctx.Event.Kind != IncomingKind.Callback)
                    {
                        await BotReply.TextAsync(sender, ctx.Bot, ctx.Event.ChatId, "🔄 Đã làm mới Thẻ ca (tin ghim đầu chat).", ct: ct);
                    }
                    return;
                }
            }
            // BOT-UX-1 (mục 7): Thẻ ca là tin ghim riêng – không bao giờ thành / bị thay bởi panel.
            using (PanelSession.Bypass())
            {
                var sent = await sender.SendAsync(ctx.Bot, new OutgoingMessage(ctx.Event.ChatId, html, kb), ct);
                if (sent.ProviderMessageId != null)
                {
                    await sender.PinAsync(ctx.Bot, ctx.Event.ChatId, sent.ProviderMessageId, ct);
                }
            }
        }

        public async Task HandleAsync(BotActionContext ctx, CallbackData data, CancellationToken ct)
        {
            if (data.Action == "refresh")
            {
                await MyCardAsync(ctx, ct);
                return;
            }
            var action = data.Action == "run" ? registry.ByCode(data.Id) : null;
            if (action == null || !action.IsAllowed(ctx.Bot, ctx.Identity))
            {
                await BotReply.TextAsync(sender, ctx.Bot, ctx.Event.ChatId, "Chức năng không còn trong quyền của bạn.", ct: ct);
                return;
            }
            // Chạy như gõ lệnh: kết quả là tin mới, không sửa đè thẻ ghim.
            await action.Handler(ctx with { Event = ctx.Event with { Kind = IncomingKind.Command, MessageId = null, CallbackData = null } }, ct);
        }

        private static string Hash(string payload) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)))[..16];
    }

    /// <summary>BOT-2.1: gọi <see cref="ShiftCards.RefreshAsync"/> mỗi <c>Messaging:ShiftCard:IntervalSeconds</c> (mặc định 60; 0 = tắt).</summary>
    public sealed class ShiftCardPump(ShiftCards cards, IConfiguration configuration, ILogger<ShiftCardPump> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            int seconds = int.TryParse(configuration["Messaging:ShiftCard:IntervalSeconds"], out var s) ? s : 60;
            if (seconds <= 0)
            {
                return;
            }
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    int edited = await cards.RefreshAsync(stoppingToken);
                    if (edited > 0)
                    {
                        logger.LogInformation("Thẻ ca: đã cập nhật {Count} thẻ", edited);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Thẻ ca: lỗi khi làm mới");
                }
                await Task.Delay(TimeSpan.FromSeconds(seconds), stoppingToken);
            }
        }
    }
}
