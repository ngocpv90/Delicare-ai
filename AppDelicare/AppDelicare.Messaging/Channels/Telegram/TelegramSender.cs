using Microsoft.Extensions.Logging;

namespace AppDelicare.Messaging.Channels.Telegram
{
    /// <summary>BOT-1.3: <see cref="IChannelSender"/> cho Telegram – đổi lỗi Telegram thành kết quả: 429 → RetryAfter, 403 → Blocked, khác → Failed.</summary>
    public sealed class TelegramSender(ITelegramClient client, ILogger<TelegramSender> logger) : IChannelSender
    {
        public async Task<ChannelSendResult> SendAsync(string botCode, OutgoingMessage message, CancellationToken ct = default)
            => await Guard(async () =>
            {
                var sent = await client.SendMessageAsync(botCode, message.ChatId, message.Text, message.ReplyMarkup, message.DisableNotification, message.ReplyToMessageId, ct);
                return ChannelSendResult.Sent(sent.MessageId.ToString());
            });

        public async Task<ChannelSendResult> EditAsync(string botCode, string chatId, string providerMessageId, string text, object? replyMarkup, CancellationToken ct = default)
            => await Guard(async () =>
            {
                var edited = await client.EditMessageTextAsync(botCode, chatId, long.Parse(providerMessageId), text, replyMarkup, ct);
                return ChannelSendResult.Sent(edited.MessageId.ToString());
            });

        public async Task<ChannelSendResult> PinAsync(string botCode, string chatId, string providerMessageId, CancellationToken ct = default)
            => await Guard(async () =>
            {
                await client.PinChatMessageAsync(botCode, chatId, long.Parse(providerMessageId), ct);
                return ChannelSendResult.Sent(providerMessageId);
            });

        public async Task AnswerCallbackAsync(string botCode, string callbackId, string? text = null, CancellationToken ct = default)
            => await Guard(async () =>
            {
                await client.AnswerCallbackQueryAsync(botCode, callbackId, text, ct);
                return ChannelSendResult.Sent(callbackId);
            });

        public async Task<byte[]?> DownloadFileAsync(string botCode, string fileId, CancellationToken ct = default)
        {
            try
            {
                var file = await client.GetFileAsync(botCode, fileId, ct);
                return string.IsNullOrWhiteSpace(file.FilePath) ? null : await client.DownloadFileAsync(botCode, file.FilePath, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning("Tải file Telegram {FileId} lỗi: {Error}", fileId, ex.Message);
                return null;
            }
        }

        private async Task<ChannelSendResult> Guard(Func<Task<ChannelSendResult>> call)
        {
            try
            {
                return await call();
            }
            catch (TelegramApiException ex) when (ex.ErrorCode == 429)
            {
                return ChannelSendResult.Retry(Math.Max(1, ex.RetryAfter ?? 5), ex.Description);
            }
            catch (TelegramApiException ex) when (ex.ErrorCode == 403)
            {
                return ChannelSendResult.Blocked(ex.Description);
            }
            catch (TelegramApiException ex)
            {
                logger.LogWarning("Telegram {Code}: {Description}", ex.ErrorCode, ex.Description);
                return ChannelSendResult.Failed(ex.Description, ex.ErrorCode);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                logger.LogWarning("Không gọi được Telegram: {Error}", ex.GetType().Name);
                return ChannelSendResult.Failed("Không gọi được Telegram: " + ex.GetType().Name);
            }
        }
    }
}
