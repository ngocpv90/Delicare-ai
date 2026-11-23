using System.Collections.Concurrent;

namespace AppDelicare.Messaging.Conversations
{
    /// <summary>Đang chờ người dùng trả lời 1 câu hỏi ForceReply (vd. lý do từ chối duyệt).</summary>
    public sealed record PendingReply(string Flow, string Key, DateTime ExpiresAtUtc, string? Extra = null);

    /// <summary>
    /// BOT-1.4: ngữ cảnh ForceReply – bot hỏi (vd. "Nhập lý do từ chối XN-…") → người dùng TRẢ LỜI vào đúng tin đó (reply_to_message) → lấy lại ngữ cảnh.
    /// Tin hỏi gửi trực tiếp (không qua outbox) nên ngữ cảnh giữ trong bộ nhớ, hết hạn 30′; container khởi động lại → người dùng bấm lại nút
    /// (an toàn: chưa gọi api gì). Tin chủ động qua outbox (BOT-1.7) giữ ngữ cảnh trong PayloadJson.replyContext.
    /// </summary>
    public sealed class ReplyContextStore(TimeProvider time)
    {
        public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);
        private readonly ConcurrentDictionary<string, PendingReply> _pending = new();

        private static string K(string bot, string chat, string messageId) => $"{bot}:{chat}:{messageId}";

        public void Remember(string bot, string chat, string messageId, string flow, string key, string? extra = null)
        {
            DateTime now = time.GetUtcNow().UtcDateTime;
            foreach (var old in _pending.Where(p => p.Value.ExpiresAtUtc < now).Select(p => p.Key).ToList())
            {
                _pending.TryRemove(old, out _);
            }
            _pending[K(bot, chat, messageId)] = new PendingReply(flow, key, now.Add(Lifetime), extra);
        }

        /// <summary>BOT-UX-1: hủy mọi câu hỏi đang chờ của chat (Quay lại / Menu / Đóng khi đang nhập dở) → trả mã tin hỏi để xóa.</summary>
        public List<string> Forget(string bot, string chat)
        {
            string prefix = bot + ":" + chat + ":";
            var keys = _pending.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList();
            foreach (var k in keys)
            {
                _pending.TryRemove(k, out _);
            }
            return [.. keys.Select(k => k[prefix.Length..])];
        }

        /// <summary>Lấy và xóa (1 câu trả lời / câu hỏi).</summary>
        public PendingReply? Take(string bot, string chat, string? replyToMessageId)
        {
            if (replyToMessageId == null || !_pending.TryRemove(K(bot, chat, replyToMessageId), out var p))
            {
                return null;
            }
            return p.ExpiresAtUtc < time.GetUtcNow().UtcDateTime ? null : p;
        }
    }
}
