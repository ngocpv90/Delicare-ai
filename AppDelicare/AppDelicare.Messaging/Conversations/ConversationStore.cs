using System.Collections.Concurrent;

namespace AppDelicare.Messaging.Conversations
{
    /// <summary>
    /// BOT-1.5a: trạng thái hội thoại nhiều bước (xin nghỉ: loại → ngày → số ngày → lý do; checklist đang bấm trong 1 tin…) theo (bot, người, luồng).
    /// Chỉ trong bộ nhớ, hết hạn 30′ – container khởi động lại giữa chừng thì người dùng làm lại từ đầu (chưa ghi gì vào api cho tới bước cuối).
    /// </summary>
    public sealed class ConversationStore(TimeProvider time)
    {
        public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);
        private readonly ConcurrentDictionary<string, (object State, DateTime ExpiresUtc)> _items = new();

        private static string K(string bot, string user, string flow) => $"{bot}:{user}:{flow}";

        public void Set<T>(string bot, string user, string flow, T state) where T : class
            => _items[K(bot, user, flow)] = (state, time.GetUtcNow().UtcDateTime.Add(Lifetime));

        public T? Get<T>(string bot, string user, string flow) where T : class
            => _items.TryGetValue(K(bot, user, flow), out var v) && v.ExpiresUtc > time.GetUtcNow().UtcDateTime ? v.State as T : null;

        public void Clear(string bot, string user, string flow) => _items.TryRemove(K(bot, user, flow), out _);
    }
}
