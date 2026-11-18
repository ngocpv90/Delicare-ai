namespace AppDelicare.Messaging.Channels
{
    public enum ChannelSendStatus
    {
        Sent,
        /// <summary>Nhà cung cấp giới hạn tốc độ (Telegram 429) – chờ <see cref="ChannelSendResult.RetryAfterSeconds"/>.</summary>
        RetryAfter,
        /// <summary>Người dùng chặn bot / không còn chat (Telegram 403) – không thử lại.</summary>
        Blocked,
        /// <summary>Ngoài khung được nhắn (Zalo 48 giờ / 7 ngày – sau này).</summary>
        OutOfWindow,
        Failed,
    }

    public sealed record ChannelSendResult(ChannelSendStatus Status, string? ProviderMessageId = null, int? RetryAfterSeconds = null, string? Error = null, int? HttpStatus = null)
    {
        public static ChannelSendResult Sent(string providerMessageId) => new(ChannelSendStatus.Sent, providerMessageId, HttpStatus: 200);
        public static ChannelSendResult Retry(int seconds, string? error) => new(ChannelSendStatus.RetryAfter, RetryAfterSeconds: seconds, Error: error, HttpStatus: 429);
        public static ChannelSendResult Blocked(string? error) => new(ChannelSendStatus.Blocked, Error: error, HttpStatus: 403);
        public static ChannelSendResult Failed(string? error, int? httpStatus = null) => new(ChannelSendStatus.Failed, Error: error, HttpStatus: httpStatus);
    }
}
