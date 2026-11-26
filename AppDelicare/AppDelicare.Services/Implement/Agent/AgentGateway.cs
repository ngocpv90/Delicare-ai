using System.Text.Json;
using AppDelicare.Data.Share;

namespace AppDelicare.Services.Implement.Agent
{
    /// <summary>Ngữ cảnh 1 lời gọi công cụ của agent (AI-1 / AI-2, D-133).</summary>
    public sealed class AgentToolContext
    {
        public string AgentCode { get; init; } = string.Empty;
        public Guid AgentUserId { get; init; }
        /// <summary>Người đang hỏi (tra từ <c>UserExternalIdentity</c>); null = việc theo lịch, chỉ quyền agent.</summary>
        public Guid? AskingUserId { get; init; }
        public string CorrelationId { get; init; } = string.Empty;
        public IServiceProvider Services { get; init; } = null!;
    }

    /// <summary>
    /// 1 công cụ của ToolGateway: tên (giữ tên hàm cũ LEGACY_GATEWAY.md nếu có, mới thì snake_case), mô tả tiếng Việt cho mô hình, schema tham số (JSON Schema),
    /// màn dùng kiểm quyền Xem (agent ∩ người hỏi), agent được dùng (rỗng = mọi agent), và hàm chạy – CHỈ gọi method Service sẵn có (không SQL riêng).
    /// </summary>
    public sealed class AgentToolDef
    {
        public string Name { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string InputSchemaJson { get; init; } = "{\"type\":\"object\",\"properties\":{}}";
        public string? ScreenId { get; init; }
        public IReadOnlyList<string> Agents { get; init; } = [];
        public Func<AgentToolContext, JsonElement, Task<object?>> RunAsync { get; init; } = (_, _) => Task.FromResult<object?>(null);

        public bool AllowedFor(string agentCode) => Agents.Count == 0 || Agents.Contains(agentCode, StringComparer.OrdinalIgnoreCase);
    }

    public sealed class AgentCallRequest
    {
        public string? AgentCode { get; init; }
        public string? AgentKey { get; init; }
        /// <summary>Mã định danh ngoài của người hỏi (vd Telegram user id) – header <c>X-On-Behalf-Of</c>.</summary>
        public string? OnBehalfOf { get; init; }
        public string Tool { get; init; } = string.Empty;
        public JsonElement Arguments { get; init; }
        public string? CorrelationId { get; init; }
    }

    /// <summary>Kết quả gọi công cụ – <c>Status</c> theo HTTP: 200 / 400 / 401 / 403 / 404 / 429 / 500 / 503.</summary>
    public sealed class AgentCallResult
    {
        public int Status { get; init; } = 200;
        public string? Message { get; init; }
        public object? Data { get; init; }
        public static AgentCallResult Ok(object? data) => new() { Status = 200, Data = data };
        public static AgentCallResult Fail(int status, string message) => new() { Status = status, Message = message };
    }

    /// <summary>Kết quả kiểm danh tính agent (AI-2): agent hợp lệ hay mã lỗi + câu báo.</summary>
    public sealed record AgentIdentity(string AgentCode, Guid AgentUserId, Guid? AskingUserId);

    /// <summary>
    /// AI-1 (D-133): lõi ToolGateway – danh sách công cụ + gọi công cụ. ToolGateway (MCP <c>/mcp</c> + REST <c>/tools/{name}</c>), web (Trung tâm agent) và test
    /// đều đi qua đây. Mỗi lời gọi chạy trong <c>ICurrentActor.BeginScope(ActorInfo.ForAgent(...))</c> nên CreatedBy / nhật ký ghi đúng agent.
    /// </summary>
    public interface IAgentGatewayService
    {
        Task<(AgentCallResult Error, IReadOnlyList<AgentToolDef> Tools)> ListToolsAsync(AgentCallRequest request);
        Task<AgentCallResult> CallAsync(AgentCallRequest request);
    }

    public static class AgentToolRegistry
    {
        public const string Version = "ai-1";
        private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

        /// <summary>Danh bạ công cụ (AI-1: ping; AI-3 thêm công cụ đọc).</summary>
        public static IReadOnlyList<AgentToolDef> All { get; } = Build();

        public static AgentToolDef? Find(string? name) => All.FirstOrDefault(t => string.Equals(t.Name, name?.Trim(), StringComparison.OrdinalIgnoreCase));

        public static string Serialize(object? data) => JsonSerializer.Serialize(data, Web);

        private static List<AgentToolDef> Build() =>
        [
            new AgentToolDef
            {
                Name = "ping",
                Description = "Kiểm tra kết nối ToolGateway: trả giờ máy chủ (giờ Việt Nam), phiên bản danh bạ công cụ và agent đang gọi.",
                RunAsync = (ctx, _) => Task.FromResult<object?>(new
                {
                    ok = true, version = Version, agent = ctx.AgentCode, correlationId = ctx.CorrelationId,
                    time = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh").ToString("yyyy-MM-dd HH:mm:ss"),
                }),
            },
        ];
    }

    /// <summary>AI-1: gọi công cụ (chưa xác thực – AI-2 thêm khóa agent, công tắc, giới hạn, quyền giao nhau, nhật ký).</summary>
    public class AgentGatewayService(IServiceProvider services, ICurrentActor currentActor) : IAgentGatewayService
    {
        private readonly IServiceProvider _services = services;
        private readonly ICurrentActor _currentActor = currentActor;

        public Task<(AgentCallResult Error, IReadOnlyList<AgentToolDef> Tools)> ListToolsAsync(AgentCallRequest request)
        {
            string agent = (request.AgentCode ?? string.Empty).Trim().ToUpperInvariant();
            IReadOnlyList<AgentToolDef> tools = [.. AgentToolRegistry.All.Where(t => agent.Length == 0 || t.AllowedFor(agent))];
            return Task.FromResult((AgentCallResult.Ok(null), tools));
        }

        public async Task<AgentCallResult> CallAsync(AgentCallRequest request)
        {
            var tool = AgentToolRegistry.Find(request.Tool);
            if (tool == null)
            {
                return AgentCallResult.Fail(404, $"Không có công cụ «{request.Tool}»");
            }
            string agent = (request.AgentCode ?? string.Empty).Trim().ToUpperInvariant();
            string correlation = string.IsNullOrWhiteSpace(request.CorrelationId) ? Guid.NewGuid().ToString("N") : request.CorrelationId!;
            var ctx = new AgentToolContext { AgentCode = agent, CorrelationId = correlation, Services = _services };
            var data = await tool.RunAsync(ctx, request.Arguments);
            return AgentCallResult.Ok(data);
        }
    }
}
