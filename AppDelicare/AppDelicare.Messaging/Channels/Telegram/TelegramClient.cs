using System.Net.Http.Json;
using System.Text.Json;
using AppDelicare.Messaging.Channels.Telegram.Models;
using AppDelicare.Messaging.Options;
using Microsoft.Extensions.Options;

namespace AppDelicare.Messaging.Channels.Telegram
{
    /// <summary>BOT-1.3: các lệnh Telegram Bot API dùng tới. Token theo mã bot (cấu hình) – không bao giờ ghi ra log (URL chứa token).</summary>
    public interface ITelegramClient
    {
        Task<TgMessage> SendMessageAsync(string botCode, string chatId, string text, object? replyMarkup = null, bool disableNotification = false, long? replyTo = null, CancellationToken ct = default);
        Task<TgMessage> EditMessageTextAsync(string botCode, string chatId, long messageId, string text, object? replyMarkup = null, CancellationToken ct = default);
        Task EditMessageReplyMarkupAsync(string botCode, string chatId, long messageId, object? replyMarkup, CancellationToken ct = default);
        Task PinChatMessageAsync(string botCode, string chatId, long messageId, CancellationToken ct = default);
        Task AnswerCallbackQueryAsync(string botCode, string callbackId, string? text = null, CancellationToken ct = default);
        Task SendChatActionAsync(string botCode, string chatId, string action = "typing", CancellationToken ct = default);
        Task SetWebhookAsync(string botCode, string url, string secret, IEnumerable<string> allowedUpdates, CancellationToken ct = default);
        Task DeleteWebhookAsync(string botCode, CancellationToken ct = default);
        Task SetMyCommandsAsync(string botCode, IEnumerable<TgBotCommand> commands, object? scope = null, CancellationToken ct = default);
        /// <summary>Trả update THÔ (JsonElement) để lưu nguyên vẹn vào InboundMessage.PayloadJson.</summary>
        Task<List<JsonElement>> GetUpdatesAsync(string botCode, long offset, int timeoutSeconds, IEnumerable<string> allowedUpdates, CancellationToken ct = default);
        Task<TgFile> GetFileAsync(string botCode, string fileId, CancellationToken ct = default);
        /// <summary>BOT-3.3: tải nội dung file đã gửi cho bot (đường dẫn từ getFile) – vd. file Excel máy chấm công.</summary>
        Task<byte[]> DownloadFileAsync(string botCode, string filePath, CancellationToken ct = default) => throw new NotSupportedException();
        /// <summary>BOT-UX-1: deleteMessage (bot chỉ xóa được tin trong 48 giờ ở chat riêng).</summary>
        Task DeleteMessageAsync(string botCode, string chatId, long messageId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    public sealed class TelegramClient(HttpClient http, IOptionsMonitor<TelegramOptions> options) : ITelegramClient
    {
        public static readonly string[] DefaultAllowedUpdates = ["message", "edited_message", "callback_query", "my_chat_member"];

        public Task<TgMessage> SendMessageAsync(string botCode, string chatId, string text, object? replyMarkup = null, bool disableNotification = false, long? replyTo = null, CancellationToken ct = default)
            => CallAsync<TgMessage>(botCode, "sendMessage", new
            {
                chat_id = chatId, text, parse_mode = "HTML", reply_markup = replyMarkup, disable_notification = disableNotification,
                link_preview_options = new { is_disabled = true },
                reply_parameters = replyTo == null ? null : new { message_id = replyTo, allow_sending_without_reply = true },
            }, ct);

        public Task<TgMessage> EditMessageTextAsync(string botCode, string chatId, long messageId, string text, object? replyMarkup = null, CancellationToken ct = default)
            => CallAsync<TgMessage>(botCode, "editMessageText", new { chat_id = chatId, message_id = messageId, text, parse_mode = "HTML", reply_markup = replyMarkup }, ct);

        public Task EditMessageReplyMarkupAsync(string botCode, string chatId, long messageId, object? replyMarkup, CancellationToken ct = default)
            => CallAsync<JsonElement>(botCode, "editMessageReplyMarkup", new { chat_id = chatId, message_id = messageId, reply_markup = replyMarkup }, ct);

        public Task DeleteMessageAsync(string botCode, string chatId, long messageId, CancellationToken ct = default)
            => CallAsync<bool>(botCode, "deleteMessage", new { chat_id = chatId, message_id = messageId }, ct);

        public Task PinChatMessageAsync(string botCode, string chatId, long messageId, CancellationToken ct = default)
            => CallAsync<bool>(botCode, "pinChatMessage", new { chat_id = chatId, message_id = messageId, disable_notification = true }, ct);

        public Task AnswerCallbackQueryAsync(string botCode, string callbackId, string? text = null, CancellationToken ct = default)
            => CallAsync<bool>(botCode, "answerCallbackQuery", new { callback_query_id = callbackId, text }, ct);

        public Task SendChatActionAsync(string botCode, string chatId, string action = "typing", CancellationToken ct = default)
            => CallAsync<bool>(botCode, "sendChatAction", new { chat_id = chatId, action }, ct);

        public Task SetWebhookAsync(string botCode, string url, string secret, IEnumerable<string> allowedUpdates, CancellationToken ct = default)
            => CallAsync<bool>(botCode, "setWebhook", new { url, secret_token = secret, allowed_updates = allowedUpdates, drop_pending_updates = false }, ct);

        public Task DeleteWebhookAsync(string botCode, CancellationToken ct = default)
            => CallAsync<bool>(botCode, "deleteWebhook", new { drop_pending_updates = false }, ct);

        public Task SetMyCommandsAsync(string botCode, IEnumerable<TgBotCommand> commands, object? scope = null, CancellationToken ct = default)
            => CallAsync<bool>(botCode, "setMyCommands", new { commands, scope }, ct);

        public async Task<List<JsonElement>> GetUpdatesAsync(string botCode, long offset, int timeoutSeconds, IEnumerable<string> allowedUpdates, CancellationToken ct = default)
            => await CallAsync<List<JsonElement>>(botCode, "getUpdates", new { offset, timeout = timeoutSeconds, allowed_updates = allowedUpdates }, ct, TimeSpan.FromSeconds(timeoutSeconds + 15)) ?? [];

        public Task<TgFile> GetFileAsync(string botCode, string fileId, CancellationToken ct = default)
            => CallAsync<TgFile>(botCode, "getFile", new { file_id = fileId }, ct);

        public async Task<byte[]> DownloadFileAsync(string botCode, string filePath, CancellationToken ct = default)
        {
            var o = options.CurrentValue;
            var bot = o.Find(botCode) ?? throw new InvalidOperationException($"Bot {botCode} chưa có token (Telegram:Bots:{botCode}:Token)");
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(60));
            return await http.GetByteArrayAsync($"{o.ApiBaseUrl.TrimEnd('/')}/file/bot{bot.Token}/{filePath.TrimStart('/')}", cts.Token);
        }

        private async Task<T> CallAsync<T>(string botCode, string method, object payload, CancellationToken ct, TimeSpan? timeout = null)
        {
            var o = options.CurrentValue;
            var bot = o.Find(botCode) ?? throw new InvalidOperationException($"Bot {botCode} chưa có token (Telegram:Bots:{botCode}:Token)");
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout ?? TimeSpan.FromSeconds(30));
            using var response = await http.PostAsJsonAsync($"{o.ApiBaseUrl.TrimEnd('/')}/bot{bot.Token}/{method}", payload, TgJson.Options, cts.Token);
            var body = await response.Content.ReadFromJsonAsync<TgResponse<T>>(TgJson.Options, cts.Token)
                ?? throw new TelegramApiException((int)response.StatusCode, "Phản hồi rỗng", null);
            if (!body.Ok)
            {
                throw new TelegramApiException(body.ErrorCode ?? (int)response.StatusCode, body.Description, body.Parameters?.RetryAfter);
            }
            return body.Result!;
        }
    }
}
