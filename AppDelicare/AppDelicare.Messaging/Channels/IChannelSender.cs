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
        /// <summary>BOT-3.3: tải file người dùng gửi cho bot (Telegram file_id) – null khi kênh không hỗ trợ / lỗi. File ≤ 20 MB (giới hạn Bot API).</summary>
        Task<byte[]?> DownloadFileAsync(string botCode, string fileId, CancellationToken ct = default) => Task.FromResult<byte[]?>(null);
        /// <summary>BOT-UX-1: xóa 1 tin (panel cũ, tin lệnh của người dùng, câu hỏi / trả lời đã xong). Kênh không hỗ trợ → Failed.</summary>
        Task<ChannelSendResult> DeleteAsync(string botCode, string chatId, string providerMessageId, CancellationToken ct = default) => Task.FromResult(ChannelSendResult.Failed("Kênh không hỗ trợ xóa tin"));
    }
}
