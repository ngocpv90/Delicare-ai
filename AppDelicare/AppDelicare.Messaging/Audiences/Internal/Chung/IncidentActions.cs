using AppDelicare.Messaging.Callback;
using AppDelicare.Messaging.Channels;
using AppDelicare.Messaging.Channels.Telegram;
using AppDelicare.Messaging.Conversations;
using AppDelicare.Messaging.Core;
using AppDelicare.Messaging.Core.Contracts;
using AppDelicare.Messaging.Presentation;
using Microsoft.Extensions.Configuration;

namespace AppDelicare.Messaging.Audiences.Internal.Chung
{
    /// <summary>
    /// BOT-2.2 (BOT_TELEGRAM §5.2): nút trên tin sự việc – <c>inc:claim</c> 🙋 Nhận xử lý (người bấm trước được ghi; người sau được báo tên người đã nhận),
    /// <c>inc:note</c> 💬 Ghi chú (gõ tay → nối vào thẻ của mọi người), <c>inc:done</c> ✅ Đã xử lý. api tìm sự việc qua CHÍNH tin được bấm (bot + chat + mã tin),
    /// trả các tin cần sửa → sửa đồng loạt (không gửi tin mới cho người khác). Người chỉ có EmployeeId cũng bấm được (api ghi dưới tài khoản hệ thống, tên = nhân viên).
    /// </summary>
    public sealed class IncidentActions(IBotChannelApi api, IDelicareApiClient web, UserTokenCache tokens, IChannelSender sender, ReplyContextStore replies)
        : WebActions(web, tokens, sender, replies)
    {
        public const string Kind = "inc";
        public const string ReplyNote = "inc-note";

        public async Task HandleAsync(BotActionContext ctx, CallbackData data, CancellationToken ct)
        {
            if (ctx.Event.MessageId is not { } mid)
            {
                return;
            }
            if (data.Action == "note")
            {
                await AskAsync(ctx, "💬 Ghi chú cho sự việc (mọi người liên quan sẽ thấy):", ReplyNote, mid.ToString(), ct);
                return;
            }
            if (data.Action is "claim" or "done")
            {
                await ActAsync(ctx, mid.ToString(), data.Action, null, ct);
                return;
            }
            await SayAsync(ctx, "Nút đã hết hạn.", ct);
        }

        public async Task ReplyAsync(BotActionContext ctx, PendingReply pending, CancellationToken ct)
        {
            string text = (ctx.Event.Text ?? string.Empty).Trim();
            if (text.Length < 2)
            {
                await AskAsync(ctx, "Ghi chú quá ngắn – gõ lại:", ReplyNote, pending.Key, ct);
                return;
            }
            await ActAsync(ctx, pending.Key, "note", text, ct);
        }

        private async Task ActAsync(BotActionContext ctx, string providerMessageId, string action, string? text, CancellationToken ct)
        {
            var r = await api.IncidentActionAsync(new IncidentActionRequest
            {
                BotCode = ctx.Bot, ExternalUserId = ctx.Event.FromId, ChatId = ctx.Event.ChatId, ProviderMessageId = providerMessageId, Action = action, Text = text,
            }, ct);
            if (!r.IsOk || r.Data == null)
            {
                await SayAsync(ctx, r.ResponseMessage ?? "Không thực hiện được.", ct);
                return;
            }
            foreach (var m in r.Data.ListEdit.Where(m => m.BotCode != null && m.ChatId != null && m.ProviderMessageId != null))
            {
                var (html, kb) = TelegramFormatter.Render(MessageTemplates.Render(m.PayloadJson));
                await Sender.EditAsync(m.BotCode!, m.ChatId!, m.ProviderMessageId!, html, kb, ct);
            }
            if (r.Data.Note != null)
            {
                await SayAsync(ctx, (r.Data.Changed ? "✅ " : "ℹ️ ") + r.Data.Note, ct);
            }
        }
    }

    /// <summary>BOT-2.3: gọi kiểm tra định kỳ mỗi <c>Messaging:WatchIntervalSeconds</c> giây (mặc định 300; 0 = tắt) – api quyết định việc nào cần báo, chống trùng ở api.</summary>
    public sealed class WatchPump(IBotChannelApi api, IConfiguration configuration, ILogger<WatchPump> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            int seconds = int.TryParse(configuration["Messaging:WatchIntervalSeconds"], out var s) ? s : 300;
            if (seconds <= 0)
            {
                return;
            }
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var r = await api.RunWatchAsync(stoppingToken);
                    if (r.IsOk && r.Data > 0)
                    {
                        logger.LogInformation("Kiểm tra định kỳ: {Count} tin", r.Data);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Kiểm tra định kỳ: lỗi");
                }
                await Task.Delay(TimeSpan.FromSeconds(seconds), stoppingToken);
            }
        }
    }

    /// <summary>BOT-2.2: gọi leo thang mỗi <c>Messaging:Escalation:IntervalSeconds</c> giây (mặc định 60; 0 = tắt) – api quyết định bậc / người nhận, tin đi qua outbox.</summary>
    public sealed class EscalationPump(IBotChannelApi api, IConfiguration configuration, ILogger<EscalationPump> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            int seconds = int.TryParse(configuration["Messaging:EscalationIntervalSeconds"], out var s) ? s : 60;
            if (seconds <= 0)
            {
                return;
            }
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var r = await api.RunEscalationAsync(stoppingToken);
                    if (r.IsOk && r.Data > 0)
                    {
                        logger.LogInformation("Leo thang: {Count} tin", r.Data);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Leo thang: lỗi");
                }
                await Task.Delay(TimeSpan.FromSeconds(seconds), stoppingToken);
            }
        }
    }
}
