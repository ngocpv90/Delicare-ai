namespace AppDelicare.Messaging.Options
{
    /// <summary>BOT-1.3: section <c>Sending</c> – giới hạn tốc độ Telegram.</summary>
    public sealed class SendingOptions
    {
        public const string Section = "Sending";

        /// <summary>Tối đa N tin / giây / bot (Telegram ~30).</summary>
        public int MaxPerSecond { get; set; } = 25;

        /// <summary>Tối đa N tin / giây / chat (Telegram 1).</summary>
        public int PerChatPerSecond { get; set; } = 1;

        /// <summary>Số tin mỗi lần ClaimOutbound.</summary>
        public int BatchSize { get; set; } = 20;

        /// <summary>Giữ quyết định rủi ro thấp N giây cho phép hoàn tác (BOT-1.4).</summary>
        public int UndoSeconds { get; set; } = 10;
    }
}
