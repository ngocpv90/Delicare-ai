using AppDelicare.Messaging.Channels;
using AppDelicare.Messaging.Channels.Telegram;
using AppDelicare.Messaging.Presentation;

namespace AppDelicare.Messaging.Audiences.Internal.Chung
{
    /// <summary>Trả lời ngay trong cuộc chat (không qua outbox): nội dung trung tính → HTML Telegram; bàn phím: tham số ưu tiên, không có thì nút trong tin.</summary>
    public static class BotReply
    {
        public static Task<ChannelSendResult> SendAsync(IChannelSender sender, string botCode, string chatId, NeutralMessage message, object? replyMarkup = null, CancellationToken ct = default)
        {
            var (html, inline) = TelegramFormatter.Render(message);
            return sender.SendAsync(botCode, new OutgoingMessage(chatId, html, replyMarkup ?? inline), ct);
        }

        public static Task<ChannelSendResult> TextAsync(IChannelSender sender, string botCode, string chatId, string text, object? replyMarkup = null, CancellationToken ct = default)
            => SendAsync(sender, botCode, chatId, NeutralMessage.Text(text), replyMarkup, ct);
    }
}
