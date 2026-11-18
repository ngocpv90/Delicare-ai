namespace AppDelicare.Messaging.Channels
{
    /// <summary>1 tin gửi ra kênh: nội dung đã định dạng cho kênh + bàn phím (đối tượng JSON của kênh).</summary>
    public sealed record OutgoingMessage(string ChatId, string Text, object? ReplyMarkup = null, bool DisableNotification = false, long? ReplyToMessageId = null);

    /// <summary>
    /// BOT-1.3 (D-62): gửi tin qua 1 nhà cung cấp (Telegram; sau này Zalo / Facebook cùng giao diện). Không ném lỗi nhà cung cấp – trả <see cref="ChannelSendResult"/>.
    /// </summary>
    public interface IChannelSender
    {
        Task<ChannelSendResult> SendAsync(string botCode, OutgoingMessage message, CancellationToken ct = default);

        Task<ChannelSendResult> EditAsync(string botCode, string chatId, string providerMessageId, string text, object? replyMarkup, CancellationToken ct = default);

        Task<ChannelSendResult> PinAsync(string botCode, string chatId, string providerMessageId, CancellationToken ct = default);

        Task AnswerCallbackAsync(string botCode, string callbackId, string? text = null, CancellationToken ct = default);
    }
}
