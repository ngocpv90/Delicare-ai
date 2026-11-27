using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Option;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Serilog;

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

    public static partial class AgentToolRegistry
    {
        public const string Version = "ai-3";
        private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
        public static JsonSerializerOptions WebJson => Web;

        /// <summary>Danh bạ công cụ (AI-1: ping; AI-3 thêm công cụ đọc).</summary>
        public static IReadOnlyList<AgentToolDef> All { get; } = [.. Build(), .. ReadTools(), .. KnowledgeTools()];

        // ---- tham số công cụ (JSON Schema đơn giản) ----
        public static string? Str(JsonElement a, string k) => a.ValueKind == JsonValueKind.Object && a.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } s ? s.Trim() : null;
        public static bool? Bool(JsonElement a, string k) => a.ValueKind == JsonValueKind.Object && a.TryGetProperty(k, out var v) ? v.ValueKind == JsonValueKind.True ? true : v.ValueKind == JsonValueKind.False ? false : null : null;
        public static int MaxRows(AgentToolContext ctx) => Math.Clamp(Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<IOptions<AgentOptions>>(ctx.Services)?.Value.MaxRows ?? 50, 1, 500);

        public static AgentToolDef? Find(string? name) => All.FirstOrDefault(t => string.Equals(t.Name, name?.Trim(), StringComparison.OrdinalIgnoreCase));

        public static string Serialize(object? data) => JsonSerializer.Serialize(data, Web);

        /// <summary>Số dòng của kết quả (đếm mảng "items" / "rows" nếu có) – ghi nhật ký.</summary>
        public static int RowsOf(object? data)
        {
            if (data == null) return 0;
            var e = JsonSerializer.SerializeToElement(data, Web);
            foreach (var k in new[] { "items", "rows", "list" })
            {
                if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty(k, out var a) && a.ValueKind == JsonValueKind.Array) return a.GetArrayLength();
            }
            return e.ValueKind == JsonValueKind.Array ? e.GetArrayLength() : 1;
        }

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
            new AgentToolDef
            {
                Name = "get_ton_kho",
                Description = "Tồn kho nguyên liệu theo kho: tồn thực, đã giữ, khả dụng, dưới tồn tối thiểu. Lọc theo từ khóa (mã / tên nguyên liệu) hoặc chỉ dòng dưới tồn tối thiểu.",
                InputSchemaJson = """{"type":"object","properties":{"tu_khoa":{"type":"string","description":"Mã hoặc tên nguyên liệu"},"duoi_toi_thieu":{"type":"boolean","description":"Chỉ nguyên liệu dưới tồn tối thiểu"}}}""",
                ScreenId = "scm-inv-balance",
                Agents = ["VIVI", "WH", "KITCHEN", "PROC", "PLAN"],
                RunAsync = async (ctx, args) =>
                {
                    var svc = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IInventoryBalanceService>(ctx.Services);
                    var r = await svc.GetSummary(new AppDelicare.Model.Request.Custom.InventorySummaryRequest { SearchText = Str(args, "tu_khoa"), BelowMin = Bool(args, "duoi_toi_thieu") });
                    int max = MaxRows(ctx);
                    var list = (r.ListData ?? []).ToList();
                    return new
                    {
                        total = list.Count, truncated = list.Count > max,
                        items = list.Take(max).Select(x => new { kho = x.WarehouseName, ma = x.IngredientCode, ten = x.IngredientName, dvt = x.UnitName, ton = x.OnHandQuantity, da_giu = x.ReservedQuantity, kha_dung = x.AvailableQuantity }),
                    };
                },
            },
        ];
    }

    /// <summary>
    /// AI-2 (D-133): gọi công cụ có xác thực. Thứ tự kiểm: <c>Agent:Enabled</c> (503) → mã + khóa agent (401; khóa = biến môi trường tên ở
    /// <c>BotConfiguration.SecretReference</c>, so SHA-256 thời gian cố định) → <c>BotStatus</c> Active (403) → giới hạn / phút (429) → công cụ có (404) và thuộc
    /// danh sách agent (403) → <c>Agent:ReadOnly</c> chặn <c>propose_*</c> (403) → quyền Xem màn của công cụ: agent ∩ người hỏi (403). Người hỏi = <c>X-On-Behalf-Of</c>
    /// tra <c>UserExternalIdentity</c> kênh Telegram; không tra được → chỉ quyền agent + cảnh báo log. Chạy trong <c>BeginScope(ActorInfo.ForAgent)</c>.
    /// Mọi lời gọi (kể cả bị từ chối) ghi 1 dòng <c>IntegrationLog</c> (kênh HERMES, OperationCode = công cụ, ErrorCode = AGENT:&lt;mã&gt; / mã lỗi,
    /// ErrorMessage = JSON {ms, user, rows} – GAP-AI-LOG tạm, PayloadHash = SHA-256 tham số).
    /// </summary>
    public class AgentGatewayService(IServiceProvider services, ICurrentActor currentActor, IUnitOfWork unitOfWork, IPermissionService permissions,
        IOptionsMonitor<AgentOptions> options, IConfiguration configuration, IMemoryCache cache, IClock clock) : IAgentGatewayService
    {
        public const string ChannelCode = "HERMES";
        private readonly IServiceProvider _services = services;
        private readonly ICurrentActor _currentActor = currentActor;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IPermissionService _permissions = permissions;
        private readonly IOptionsMonitor<AgentOptions> _options = options;
        private readonly IConfiguration _configuration = configuration;
        private readonly IMemoryCache _cache = cache;

        private sealed record Auth(AgentCallResult? Error, AgentIdentity? Identity, Guid? ChannelId);

        /// <summary>Kiểm danh tính agent + người hỏi (dùng cho liệt kê và gọi công cụ).</summary>
        private async Task<Auth> AuthenticateAsync(AgentCallRequest request, bool countRate)
        {
            var o = _options.CurrentValue;
            var db = _unitOfWork.DbContext;
            var channel = await db.IntegrationChannel.AsNoTracking().Where(c => !c.IsDeleted && c.ChannelCode == ChannelCode).Select(c => new { c.Id, c.ChannelStatus }).FirstOrDefaultAsync();
            if (!o.Enabled || channel == null || channel.ChannelStatus != "Active")
            {
                return new Auth(AgentCallResult.Fail(503, "Trợ lý AI đang tắt"), null, channel?.Id);
            }
            string code = (request.AgentCode ?? string.Empty).Trim().ToUpperInvariant();
            if (code.Length == 0 || string.IsNullOrEmpty(request.AgentKey))
            {
                return new Auth(AgentCallResult.Fail(401, "Thiếu mã hoặc khóa agent"), null, channel.Id);
            }
            var bot = await db.BotConfiguration.AsNoTracking().Where(b => !b.IsDeleted && b.IntegrationChannelId == channel.Id && b.BotCode == code)
                .Select(b => new { b.BotStatus, b.SecretReference }).FirstOrDefaultAsync();
            string? secret = bot?.SecretReference is { Length: > 0 } name ? Environment.GetEnvironmentVariable(name) ?? _configuration[name] : null;
            if (bot == null || string.IsNullOrEmpty(secret) || !SameKey(secret, request.AgentKey))
            {
                return new Auth(AgentCallResult.Fail(401, "Khóa agent không đúng"), null, channel.Id);
            }
            if (bot.BotStatus != "Active")
            {
                return new Auth(AgentCallResult.Fail(403, $"Agent {code} đang tắt"), null, channel.Id);
            }
            if (countRate && o.RateLimitPerMinute > 0)
            {
                string key = $"agent-rate:{code}:{DateTime.UtcNow:yyyyMMddHHmm}";
                var counter = _cache.GetOrCreate(key, e => { e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2); return new int[1]; })!;
                int n = Interlocked.Increment(ref counter[0]);
                if (n > o.RateLimitPerMinute)
                {
                    return new Auth(AgentCallResult.Fail(429, $"Agent {code} gọi quá {o.RateLimitPerMinute} lần / phút"), null, channel.Id);
                }
            }
            var agentUser = await db.UserExternalIdentity.AsNoTracking().Where(x => !x.IsDeleted && x.IntegrationChannelId == channel.Id && x.ExternalUserId == code && x.UserId != null)
                .Select(x => x.UserId).FirstOrDefaultAsync();
            if (agentUser == null || !await db.User.AsNoTracking().AnyAsync(u => u.Id == agentUser && !u.IsDeleted && u.IsActive))
            {
                return new Auth(AgentCallResult.Fail(403, $"Agent {code} chưa có tài khoản dịch vụ"), null, channel.Id);
            }
            Guid? asking = null;
            if (!string.IsNullOrWhiteSpace(request.OnBehalfOf))
            {
                string ext = request.OnBehalfOf.Trim();
                string? botCode = string.IsNullOrWhiteSpace(o.AskingBotCode) ? null : o.AskingBotCode.Trim();
                asking = await (from x in db.UserExternalIdentity.AsNoTracking()
                                join c in db.IntegrationChannel.AsNoTracking() on x.IntegrationChannelId equals c.Id
                                where !x.IsDeleted && x.ExternalUserId == ext && x.UserId != null && x.IdentityStatus == "Active" && c.ProviderCode == "TELEGRAM"
                                      && (botCode == null || db.BotConfiguration.Any(b => b.IntegrationChannelId == c.Id && b.BotCode == botCode && !b.IsDeleted))
                                select x.UserId).FirstOrDefaultAsync();
                if (asking == null)
                {
                    Log.Warning("[AgentGateway] {Agent}: không tra được người hỏi {External} – chỉ dùng quyền agent", code, ext);
                }
            }
            return new Auth(null, new AgentIdentity(code, agentUser.Value, asking), channel.Id);
        }

        private static bool SameKey(string expected, string given)
            => CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(expected)), SHA256.HashData(Encoding.UTF8.GetBytes(given)));

        /// <summary>Từ chối khi agent HOẶC người hỏi không có quyền Xem màn (người hỏi bị giới hạn bot – Pending – không tính quyền Duyệt).</summary>
        public static async Task<string?> DenyAsync(IPermissionService permissions, Guid agentUserId, Guid? askingUserId, string screenId, PermissionAction action)
        {
            var agent = await permissions.GetAsync(agentUserId);
            if (!agent.IsAdmin && !permissions.IsAllowed(agent, screenId, action))
            {
                return $"Agent không có quyền {ActionVi(action)} màn {screenId}";
            }
            if (askingUserId is { } u && !permissions.IsAllowed(await permissions.GetAsync(u), screenId, action))
            {
                return $"Người hỏi không có quyền {ActionVi(action)} màn {screenId}";
            }
            return null;
        }

        private static string ActionVi(PermissionAction a) => a switch
        {
            PermissionAction.View => "Xem", PermissionAction.Add => "Thêm", PermissionAction.Edit => "Sửa", PermissionAction.Delete => "Xóa",
            PermissionAction.Approve => "Duyệt", PermissionAction.Export => "Xuất", PermissionAction.Print => "In", PermissionAction.Import => "Nhập", _ => a.ToString(),
        };

        public async Task<(AgentCallResult Error, IReadOnlyList<AgentToolDef> Tools)> ListToolsAsync(AgentCallRequest request)
        {
            var auth = await AuthenticateAsync(request, countRate: false);
            if (auth.Error != null)
            {
                return (auth.Error, []);
            }
            var id = auth.Identity!;
            var list = new List<AgentToolDef>();
            foreach (var t in AgentToolRegistry.All.Where(t => t.AllowedFor(id.AgentCode)))
            {
                if (_options.CurrentValue.ReadOnly && t.Name.StartsWith("propose_", StringComparison.Ordinal))
                {
                    continue;
                }
                if (t.ScreenId == null || await DenyAsync(_permissions, id.AgentUserId, id.AskingUserId, t.ScreenId, PermissionAction.View) == null)
                {
                    list.Add(t);
                }
            }
            return (AgentCallResult.Ok(null), list);
        }

        public async Task<AgentCallResult> CallAsync(AgentCallRequest request)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            DateTime started = clock.Now; // giờ VN (IClock) – như mọi bản ghi khác
            string correlation = string.IsNullOrWhiteSpace(request.CorrelationId) ? Guid.NewGuid().ToString("N") : request.CorrelationId!;
            var auth = await AuthenticateAsync(request, countRate: true);
            AgentCallResult result;
            int rows = 0;
            if (auth.Error != null)
            {
                result = auth.Error;
            }
            else
            {
                var id = auth.Identity!;
                var tool = AgentToolRegistry.Find(request.Tool);
                string? deny = tool == null ? null
                    : !tool.AllowedFor(id.AgentCode) ? $"Công cụ «{tool.Name}» không thuộc agent {id.AgentCode}"
                    : _options.CurrentValue.ReadOnly && tool.Name.StartsWith("propose_", StringComparison.Ordinal) ? "Trợ lý AI đang ở chế độ chỉ đọc"
                    : tool.ScreenId != null ? await DenyAsync(_permissions, id.AgentUserId, id.AskingUserId, tool.ScreenId, PermissionAction.View)
                    : null;
                if (tool == null)
                {
                    result = AgentCallResult.Fail(404, $"Không có công cụ «{request.Tool}»");
                }
                else if (deny != null)
                {
                    result = AgentCallResult.Fail(403, deny);
                }
                else
                {
                    try
                    {
                        using var scope = _currentActor.BeginScope(ActorInfo.ForAgent(id.AgentUserId, id.AgentCode), correlation);
                        var ctx = new AgentToolContext { AgentCode = id.AgentCode, AgentUserId = id.AgentUserId, AskingUserId = id.AskingUserId, CorrelationId = correlation, Services = _services };
                        var data = await tool.RunAsync(ctx, request.Arguments);
                        // AI-3: che dữ liệu cá nhân + cắt Agent:MaxRows TẬP TRUNG – mọi công cụ (kể cả thêm sau) đều đi qua.
                        var (masked, n) = AgentMask.Apply(data, AgentToolRegistry.MaxRows(ctx), AgentToolRegistry.WebJson);
                        rows = n > 0 ? n : masked == null ? 0 : 1;
                        result = AgentCallResult.Ok(masked);
                    }
                    catch (AgentToolException ex)
                    {
                        result = AgentCallResult.Fail(400, ex.Message);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "[AgentGateway] {Agent} {Tool}", id.AgentCode, tool.Name);
                        result = AgentCallResult.Fail(500, "Lỗi khi chạy công cụ – xem log máy chủ");
                    }
                }
            }
            await LogAsync(auth, request, correlation, result, started, watch.ElapsedMilliseconds, rows);
            return result;
        }

        /// <summary>1 dòng IntegrationLog / lời gọi (chạy dưới actor agent, hoặc system khi chưa xác thực được).</summary>
        private async Task LogAsync(Auth auth, AgentCallRequest request, string correlation, AgentCallResult result, DateTime started, long ms, int rows)
        {
            try
            {
                Guid? actorId = auth.Identity?.AgentUserId ?? (Guid.TryParse(_configuration[$"{SystemUsersOptions.Section}:{nameof(SystemUsersOptions.JobUserId)}"], out var sys) ? sys : null);
                if (actorId == null)
                {
                    return;
                }
                using var scope = _currentActor.BeginScope(auth.Identity != null ? ActorInfo.ForAgent(auth.Identity.AgentUserId, auth.Identity.AgentCode) : ActorInfo.ForSystem(actorId.Value), correlation);
                string args = request.Arguments.ValueKind == JsonValueKind.Undefined ? "{}" : request.Arguments.GetRawText();
                string code = (request.AgentCode ?? string.Empty).Trim().ToUpperInvariant();
                var db = _unitOfWork.DbContext;
                db.IntegrationLog.Add(new IntegrationLog
                {
                    Id = Guid.NewGuid(), IntegrationChannelId = auth.ChannelId, DirectionCode = "IN", OperationCode = Trim(request.Tool, 100), CorrelationId = Trim(correlation, 100),
                    RequestAt = started, ResponseAt = clock.Now, HttpStatus = result.Status,
                    LogStatus = result.Status == 200 ? "Succeeded" : result.Status is 401 or 403 or 429 or 503 ? "Denied" : "Failed",
                    ErrorCode = Trim(result.Status == 200 ? "AGENT:" + code : $"{result.Status}:AGENT:{code}", 100),
                    ErrorMessage = Trim(JsonSerializer.Serialize(new { ms, rows, user = auth.Identity?.AskingUserId, onBehalfOf = request.OnBehalfOf, error = result.Message }, ReadableJson), 2000),
                    PayloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(args))),
                    CreatedDate = clock.Now, CreatedBy = actorId, IsDeleted = false,
                });
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[AgentGateway] không ghi được IntegrationLog");
            }
        }

        private static readonly JsonSerializerOptions ReadableJson = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        private static string? Trim(string? s, int max) => s == null ? null : s.Length <= max ? s : s[..max];
    }

    /// <summary>Lỗi tham số / nghiệp vụ của công cụ – trả 400 kèm câu báo tiếng Việt.</summary>
    public sealed class AgentToolException(string message) : Exception(message);

}

namespace AppDelicare.Services.Implement.Agent
{
    /// <summary>AI-2 (D-133): tài khoản dịch vụ của agent = User có liên kết <c>UserExternalIdentity</c> trên kênh HERMES (không theo tên nhóm cứng).</summary>
    public static class AgentAccounts
    {
        public const string LoginDenied = "Tài khoản agent (Trợ lý AI) không đăng nhập web / bot được";

        public static Task<bool> IsAgentAsync(AppDelicare.Data.Connection.DatabaseContext db, Guid userId)
            => (from x in db.UserExternalIdentity.AsNoTracking()
                join c in db.IntegrationChannel.AsNoTracking() on x.IntegrationChannelId equals c.Id
                where x.UserId == userId && !x.IsDeleted && c.ProviderCode == AgentGatewayService.ChannelCode && !c.IsDeleted
                select x.Id).AnyAsync();
    }
}
