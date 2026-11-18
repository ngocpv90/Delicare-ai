using AppDelicare.Messaging.Options;
using AppDelicare.Messaging.Processing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AppDelicare.Messaging.Channels.Telegram
{
    /// <summary>
    /// BOT-1.3: <c>POST /tg/{botCode}</c> (nginx <c>/tg/</c> → messaging). Bot chưa cấu hình → 404; secret sai / thiếu → 401 và KHÔNG ghi gì;
    /// lưu update qua api → 200; api không trả lời → 503 (Telegram tự gửi lại). Update không đọc được → 200 (không cho Telegram gửi lại mãi).
    /// </summary>
    public static class TelegramWebhookEndpoint
    {
        public const int MaxBodyBytes = 64 * 1024;

        public static async Task<IResult> HandleAsync(string botCode, HttpRequest request, IOptionsMonitor<TelegramOptions> options, UpdateIngestor ingestor,
            ILoggerFactory loggers, CancellationToken ct)
        {
            var logger = loggers.CreateLogger("TelegramWebhook");
            var bot = options.CurrentValue.Find(botCode);
            if (bot == null || string.IsNullOrWhiteSpace(bot.WebhookSecret))
            {
                return Results.NotFound();
            }
            if (!TelegramSecretValidator.IsValid(bot.WebhookSecret, request.Headers[TelegramSecretValidator.HeaderName].ToString()))
            {
                logger.LogWarning("Webhook {Bot}: secret sai / thiếu từ {Ip}", botCode, request.HttpContext.Connection.RemoteIpAddress);
                return Results.Unauthorized();
            }
            if (request.ContentLength > MaxBodyBytes)
            {
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }
            using var reader = new StreamReader(request.Body);
            string json = await reader.ReadToEndAsync(ct);
            return await ingestor.IngestAsync(botCode.ToUpperInvariant(), json, ct) switch
            {
                IngestResult.Unavailable => Results.StatusCode(StatusCodes.Status503ServiceUnavailable),
                _ => Results.Ok(),
            };
        }
    }
}
