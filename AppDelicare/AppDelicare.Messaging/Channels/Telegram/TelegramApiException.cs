namespace AppDelicare.Messaging.Channels.Telegram
{
    /// <summary>Telegram trả <c>ok = false</c>: mã lỗi (400 / 403 / 429…), mô tả, retry_after khi 429. Không chứa token.</summary>
    public sealed class TelegramApiException(int errorCode, string? description, int? retryAfter) : Exception($"Telegram {errorCode}: {description}")
    {
        public int ErrorCode { get; } = errorCode;
        public string? Description { get; } = description;
        public int? RetryAfter { get; } = retryAfter;
    }
}
