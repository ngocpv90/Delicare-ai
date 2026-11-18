using System.Collections.Concurrent;
using AppDelicare.Messaging.Core.Contracts;

namespace AppDelicare.Messaging.Core
{
    /// <summary>
    /// BOT-1.3: JWT 10′ của từng user (theo bot + Telegram id) – xin qua <c>BotChannel/IssueUserToken</c>, dùng lại tới 60″ trước khi hết hạn.
    /// Chỉ trong bộ nhớ (không ghi đĩa, không log). Gỡ / khóa tài khoản → lần xin kế bị từ chối.
    /// </summary>
    public sealed class UserTokenCache(IBotChannelApi api, TimeProvider time)
    {
        private readonly ConcurrentDictionary<string, UserTokenModel> _tokens = new();

        public async Task<(UserTokenModel? Token, string? Error)> GetAsync(string botCode, string externalUserId, CancellationToken ct = default)
        {
            string key = botCode + ":" + externalUserId;
            if (_tokens.TryGetValue(key, out var cached) && cached.ExpiresAt.ToUniversalTime() > time.GetUtcNow().UtcDateTime.AddSeconds(60))
            {
                return (cached, null);
            }
            var issued = await api.IssueUserTokenAsync(new BotIdentityRequest { BotCode = botCode, ExternalUserId = externalUserId }, ct);
            if (!issued.IsOk || issued.Data == null)
            {
                _tokens.TryRemove(key, out _);
                return (null, issued.ResponseMessage);
            }
            // ExpiresAt của api là giờ VN không múi giờ → quy về theo hạn tương đối (10′) để không phụ thuộc múi giờ máy chạy messaging.
            var token = issued.Data;
            token.ExpiresAt = time.GetUtcNow().UtcDateTime.AddMinutes(9);
            _tokens[key] = token;
            return (token, null);
        }

        public void Invalidate(string botCode, string externalUserId) => _tokens.TryRemove(botCode + ":" + externalUserId, out _);
    }
}
