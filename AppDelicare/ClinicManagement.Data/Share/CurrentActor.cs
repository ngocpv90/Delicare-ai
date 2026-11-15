using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace AppDelicare.Data.Share
{
    /// <summary>Loại người thao tác (CLAUDE.md 3.2: mỗi thao tác ghi phải biết ai làm).</summary>
    public enum ActorType
    {
        /// <summary>Người dùng đăng nhập qua HTTP (JWT claim "id").</summary>
        User = 1,
        /// <summary>Job hệ thống (Hangfire…), dùng User dịch vụ cấu hình <c>SystemUsers:JobUserId</c>.</summary>
        System = 2,
        /// <summary>Agent qua ToolGateway (Release 2), dùng tài khoản dịch vụ của agent.</summary>
        Agent = 3,
    }

    /// <summary>
    /// Người thao tác. Mọi loại đều gắn với một <c>User.Id</c> (người thật hoặc tài khoản dịch vụ)
    /// vì các cột CreatedBy / ModifiedBy trong DDL là <c>uniqueidentifier</c>.
    /// </summary>
    public sealed record ActorInfo(ActorType Type, Guid UserId, string? AgentCode = null)
    {
        public static ActorInfo ForUser(Guid userId) => new(ActorType.User, Require(userId));
        public static ActorInfo ForSystem(Guid serviceUserId) => new(ActorType.System, Require(serviceUserId));
        public static ActorInfo ForAgent(Guid serviceUserId, string agentCode)
            => new(ActorType.Agent, Require(serviceUserId),
                string.IsNullOrWhiteSpace(agentCode) ? throw new ArgumentException("AgentCode bắt buộc.", nameof(agentCode)) : agentCode);

        /// <summary>Chuỗi ngắn để ghi log / audit, vd. <c>USER:3f2a…</c>, <c>AGENT:vivi:…</c>.</summary>
        public override string ToString() => Type switch
        {
            ActorType.Agent => $"AGENT:{AgentCode}:{UserId}",
            ActorType.System => $"SYSTEM:{UserId}",
            _ => $"USER:{UserId}",
        };

        private static Guid Require(Guid id)
            => id == Guid.Empty ? throw new ArgumentException("UserId của người thao tác không được rỗng.", nameof(id)) : id;
    }

    /// <summary>Ghi dữ liệu khi không xác định được người thao tác.</summary>
    public sealed class UnauthorizedActorException(string message) : InvalidOperationException(message);

    /// <summary>Người thao tác của luồng hiện tại (T02). Thay cho fallback "user đầu tiên" cũ.</summary>
    public interface ICurrentActor
    {
        /// <summary>Người thao tác hiện tại; <c>null</c> nếu không xác định được (KHÔNG có giá trị mặc định).</summary>
        ActorInfo? Actor { get; }

        /// <summary>Mã tương quan của request / job hiện tại (header <c>X-Correlation-Id</c> hoặc tự sinh).</summary>
        string CorrelationId { get; }

        /// <summary>Trả về người thao tác hoặc ném <see cref="UnauthorizedActorException"/>.</summary>
        ActorInfo RequireActor();

        /// <summary>
        /// Đặt người thao tác cho đoạn code không có HTTP user (job, agent, bước đăng nhập).
        /// Dùng: <c>using (currentActor.BeginScope(ActorInfo.ForSystem(jobUserId))) { ... }</c>
        /// </summary>
        IDisposable BeginScope(ActorInfo actor, string? correlationId = null);
    }

    public sealed class CurrentActor(IHttpContextAccessor httpContextAccessor) : ICurrentActor
    {
        public const string UserIdClaim = "id";
        public const string CorrelationIdItemKey = "CorrelationId";

        private static readonly AsyncLocal<Scope?> CurrentScope = new();
        private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
        private readonly Lazy<string> _fallbackCorrelationId = new(() => Guid.NewGuid().ToString("N"));

        public ActorInfo? Actor
        {
            get
            {
                Scope? scope = CurrentScope.Value;
                if (scope != null)
                {
                    return scope.Actor;
                }
                string? claim = _httpContextAccessor.HttpContext?.User?.FindFirstValue(UserIdClaim);
                return Guid.TryParse(claim, out Guid userId) && userId != Guid.Empty ? ActorInfo.ForUser(userId) : null;
            }
        }

        public string CorrelationId
        {
            get
            {
                if (CurrentScope.Value?.CorrelationId is { } scoped)
                {
                    return scoped;
                }
                if (_httpContextAccessor.HttpContext?.Items[CorrelationIdItemKey] is string fromRequest && fromRequest.Length > 0)
                {
                    return fromRequest;
                }
                return _fallbackCorrelationId.Value;
            }
        }

        public ActorInfo RequireActor()
            => Actor ?? throw new UnauthorizedActorException(
                "Không xác định được người thao tác (user / job / agent). Thao tác ghi bị chặn. "
                + "Request HTTP cần token hợp lệ; job / agent phải chạy trong ICurrentActor.BeginScope(...).");

        public IDisposable BeginScope(ActorInfo actor, string? correlationId = null)
        {
            ArgumentNullException.ThrowIfNull(actor);
            Scope? parent = CurrentScope.Value;
            var scope = new Scope(actor, correlationId ?? parent?.CorrelationId, parent);
            CurrentScope.Value = scope;
            return scope;
        }

        private sealed class Scope(ActorInfo actor, string? correlationId, Scope? parent) : IDisposable
        {
            private bool _disposed;
            public ActorInfo Actor { get; } = actor;
            public string? CorrelationId { get; } = correlationId;

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }
                _disposed = true;
                if (CurrentScope.Value == this)
                {
                    CurrentScope.Value = parent;
                }
            }
        }
    }
}
