using AppDelicare.Messaging.Audiences.Internal.Chung;
using AppDelicare.Messaging.Channels;
using AppDelicare.Messaging.Channels.Telegram;
using AppDelicare.Messaging.Channels.Telegram.Models;
using AppDelicare.Messaging.Conversations;
using AppDelicare.Messaging.Core;
using AppDelicare.Messaging.Core.Contracts;
using AppDelicare.Messaging.Presentation;

namespace AppDelicare.Messaging.Audiences.Internal
{
    /// <summary>
    /// BOT-1.5: phần dùng chung của chức năng nghiệp vụ gọi api bằng JWT của CHÍNH user (quyền ObjectMenu như web): lấy token, nói, sửa tin, hỏi gõ tay (ForceReply),
    /// đổi lỗi 401 / 403 thành câu dễ hiểu. Bot không tự kiểm quyền – api quyết định.
    /// </summary>
    public abstract class WebActions(IDelicareApiClient web, UserTokenCache tokens, IChannelSender sender, ReplyContextStore replies)
    {
        protected IDelicareApiClient Web => web;
        protected IChannelSender Sender => sender;

        protected async Task<string?> TokenAsync(BotActionContext ctx, CancellationToken ct)
        {
            if (!ctx.Identity.HasWebAccount)
            {
                await SayAsync(ctx, "Chức năng dành cho tài khoản có quyền trên web.", ct);
                return null;
            }
            var (token, error) = await tokens.GetAsync(ctx.Bot, ctx.Event.FromId, ct);
            if (token == null)
            {
                await SayAsync(ctx, error ?? "Không lấy được quyền truy cập.", ct);
            }
            return token?.Token;
        }

        /// <summary>BOT-2.4: lấy token KHÔNG nhắn lỗi (vị trí trực tiếp – tránh nhắn lỗi mỗi lần cập nhật).</summary>
        protected async Task<string?> QuietTokenAsync(BotActionContext ctx, CancellationToken ct)
        {
            if (!ctx.Identity.HasWebAccount)
            {
                return null;
            }
            var (token, _) = await tokens.GetAsync(ctx.Bot, ctx.Event.FromId, ct);
            return token?.Token;
        }

        protected static string Problem<T>(ApiResult<T> r)
            => r.ResponseCode is 401 or 403 ? "Bạn chưa có quyền làm việc này (quyền trên web áp dụng y hệt cho bot)." : r.ResponseMessage ?? "Không thực hiện được.";

        protected Task SayAsync(BotActionContext ctx, string text, CancellationToken ct) => BotReply.TextAsync(sender, ctx.Bot, ctx.Event.ChatId, text, ct: ct);

        protected Task SendAsync(BotActionContext ctx, NeutralMessage m, CancellationToken ct) => BotReply.SendAsync(sender, ctx.Bot, ctx.Event.ChatId, m, ct: ct);

        /// <summary>Bấm nút → sửa chính tin đó; gõ lệnh → gửi tin mới.</summary>
        protected async Task ShowAsync(BotActionContext ctx, NeutralMessage m, CancellationToken ct)
        {
            var (html, kb) = TelegramFormatter.Render(m);
            if (ctx.Event.Kind == IncomingKind.Callback && ctx.Event.MessageId is { } mid)
            {
                await sender.EditAsync(ctx.Bot, ctx.Event.ChatId, mid.ToString(), html, kb, ct);
            }
            else
            {
                await sender.SendAsync(ctx.Bot, new OutgoingMessage(ctx.Event.ChatId, html, kb), ct);
            }
        }

        protected async Task AskAsync(BotActionContext ctx, string question, string flow, string key, CancellationToken ct)
        {
            var (html, _) = TelegramFormatter.Render(NeutralMessage.Text(question));
            var sent = await sender.SendAsync(ctx.Bot, new OutgoingMessage(ctx.Event.ChatId, html, new TgForceReply()), ct);
            if (sent.ProviderMessageId != null)
            {
                replies.Remember(ctx.Bot, ctx.Event.ChatId, sent.ProviderMessageId, flow, key);
            }
        }
    }
}
