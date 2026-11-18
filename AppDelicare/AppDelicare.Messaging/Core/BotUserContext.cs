using System.Collections.Concurrent;
using AppDelicare.Messaging.Core.Contracts;
using AppDelicare.Messaging.Options;
using Microsoft.Extensions.Options;

namespace AppDelicare.Messaging.Core
{
    /// <summary>
    /// BOT-1.3: người đang thao tác trên bot – định danh (đã liên kết? tài khoản / nhân viên, PendingCode, quyền vào bot / bot DH) lấy qua
    /// <c>BotChannel/ResolveIdentity</c>, cache ngắn <c>Messaging:IdentityCacheSeconds</c>. Bot KHÔNG tự kiểm quyền nghiệp vụ – api quyết định.
    /// </summary>
    public sealed class BotUserContext(IBotChannelApi api, IOptionsMonitor<MessagingHostOptions> options, TimeProvider time)
    {
        private readonly ConcurrentDictionary<string, (BotIdentityModel Identity, DateTime At)> _cache = new();

        public async Task<BotIdentityModel> GetAsync(string botCode, string externalUserId, CancellationToken ct = default)
        {
            string key = botCode + ":" + externalUserId;
            DateTime now = time.GetUtcNow().UtcDateTime;
            if (_cache.TryGetValue(key, out var hit) && (now - hit.At).TotalSeconds < Math.Max(0, options.CurrentValue.IdentityCacheSeconds))
            {
                return hit.Identity;
            }
            var r = await api.ResolveIdentityAsync(new BotIdentityRequest { BotCode = botCode, ExternalUserId = externalUserId }, ct);
            var identity = r.Data ?? new BotIdentityModel();
            _cache[key] = (identity, now);
            return identity;
        }

        public void Set(string botCode, string externalUserId, BotIdentityModel identity)
            => _cache[botCode + ":" + externalUserId] = (identity, time.GetUtcNow().UtcDateTime);

        public void Invalidate(string botCode, string externalUserId) => _cache.TryRemove(botCode + ":" + externalUserId, out _);
    }
}
