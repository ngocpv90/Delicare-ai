using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Option;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Serilog;

namespace AppDelicare.Services.Implement.Agent
{
    public sealed class AgentChatTurn
    {
        /// <summary>user / assistant.</summary>
        public string? Role { get; set; }
        public string? Content { get; set; }
    }

    public sealed class AgentChatRequest
    {
        public string? Agent { get; set; }
        public string? Message { get; set; }
        public List<AgentChatTurn>? History { get; set; }
    }

    public sealed class AgentChatReplyModel
    {
        public string Agent { get; set; } = string.Empty;
        public string Reply { get; set; } = string.Empty;
        public string CorrelationId { get; set; } = string.Empty;
    }

    /// <summary>
    /// AI-14 / AI-15 (D-133): chat với agent trên web. API gửi câu hỏi tới API server Hermes kiểu OpenAI (<c>POST {Agent:HermesApiUrl}/chat/completions</c>; agent ở container khác →
    /// <c>Agent:HermesApiUrlByAgent:&lt;MÃ&gt;</c>; khóa Bearer ở biến môi trường <c>Agent:HermesApiKeyEnv</c>) kèm header <b>vé người hỏi</b> <c>X-On-Behalf-Of: web:&lt;userId&gt;:&lt;hạn unix&gt;:&lt;HMAC&gt;</c>
    /// (ký bằng khóa JWT của hệ thống, hạn 10 phút) – Hermes chuyển vé vào <c>X-On-Behalf-Of</c> khi gọi ToolGateway, cổng xác minh chữ ký + hạn rồi áp quyền agent ∩ người hỏi như bot.
    /// <c>user = user:&lt;id&gt;</c> để Hermes giữ lịch sử theo người dùng.
    /// Hermes tắt / chưa cấu hình → thông báo tiếng Việt, không lỗi. Mỗi câu ghi 1 dòng IntegrationLog (OUT, agent_chat – không lưu nội dung câu hỏi).
    /// </summary>
    public interface IAgentChatService
    {
        Task<IResult<AgentChatReplyModel>> Chat(AgentChatRequest request);
    }

    public static class AgentWebTicket
    {
        public const string Prefix = "web:";

        private static byte[] Key(IConfiguration configuration) => Encoding.UTF8.GetBytes("agent-web-ticket:" + (configuration["Jwt:SigningKey"] ?? throw new InvalidOperationException("Thiếu Jwt:SigningKey")));

        public static string Issue(IConfiguration configuration, Guid userId, DateTime nowUtc, int minutes = 10)
        {
            long exp = new DateTimeOffset(nowUtc.AddMinutes(minutes), TimeSpan.Zero).ToUnixTimeSeconds();
            string body = $"{userId:N}:{exp}";
            return Prefix + body + ":" + Convert.ToHexString(HMACSHA256.HashData(Key(configuration), Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
        }

        /// <summary>Vé hợp lệ + còn hạn → userId; còn lại null.</summary>
        public static Guid? Verify(IConfiguration configuration, string? ticket, DateTime nowUtc)
        {
            if (ticket == null || !ticket.StartsWith(Prefix, StringComparison.Ordinal))
            {
                return null;
            }
            var parts = ticket[Prefix.Length..].Split(':');
            if (parts.Length != 3 || !Guid.TryParseExact(parts[0], "N", out var user) || !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out long exp))
            {
                return null;
            }
            byte[] expected = HMACSHA256.HashData(Key(configuration), Encoding.UTF8.GetBytes(parts[0] + ":" + parts[1]));
            byte[] given;
            try
            {
                given = Convert.FromHexString(parts[2]);
            }
            catch (FormatException)
            {
                return null;
            }
            return CryptographicOperations.FixedTimeEquals(expected, given) && DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime > nowUtc ? user : null;
        }
    }

    public sealed partial class AgentChatService(IUnitOfWork unitOfWork, IClock clock, ICurrentActor currentActor, IOptionsMonitor<AgentOptions> options, IConfiguration configuration) : IAgentChatService
    {
        private static readonly HttpClient Http = new();

        /// <summary>AI-15: URL API server Hermes của container chứa agent (Agent:HermesApiUrlByAgent:&lt;MÃ&gt;), không có → Agent:HermesApiUrl (hermes-vivi).</summary>
        public static string? ChatUrl(AgentOptions o, string agent)
            => o.HermesApiUrlByAgent.TryGetValue(agent, out var u) && !string.IsNullOrWhiteSpace(u) ? u : o.HermesApiUrl;

        /// <summary>choices[0].message.content (chuẩn OpenAI); content dạng mảng phần → nối các phần text.</summary>
        public static string ReadReply(JsonElement root)
        {
            if (!root.TryGetProperty("choices", out var ch) || ch.ValueKind != JsonValueKind.Array || ch.GetArrayLength() == 0
                || !ch[0].TryGetProperty("message", out var m) || !m.TryGetProperty("content", out var c))
            {
                return string.Empty;
            }
            return c.ValueKind switch
            {
                JsonValueKind.String => c.GetString() ?? string.Empty,
                JsonValueKind.Array => string.Concat(c.EnumerateArray().Where(p => p.TryGetProperty("text", out _)).Select(p => p.GetProperty("text").GetString())),
                _ => string.Empty,
            };
        }

        public async Task<IResult<AgentChatReplyModel>> Chat(AgentChatRequest request)
        {
            var o = options.CurrentValue;
            var actor = currentActor.RequireActor();
            string agent = (request.Agent ?? "VIVI").Trim().ToUpperInvariant();
            string message = (request.Message ?? string.Empty).Trim();
            if (message.Length == 0)
            {
                return Result<AgentChatReplyModel>.Fail("Nhập câu hỏi");
            }
            if (message.Length > 2000)
            {
                return Result<AgentChatReplyModel>.Fail("Câu hỏi tối đa 2.000 ký tự");
            }
            if (!o.Enabled)
            {
                return Result<AgentChatReplyModel>.Fail("Trợ lý AI đang tắt (Agent:Enabled = false)");
            }
            var db = unitOfWork.DbContext;
            var bot = await (from b in db.BotConfiguration.AsNoTracking() join c in db.IntegrationChannel.AsNoTracking() on b.IntegrationChannelId equals c.Id
                             where c.ChannelCode == "HERMES" && !b.IsDeleted && b.BotCode == agent select new { b.BotStatus, b.BotName, ChannelId = c.Id }).FirstOrDefaultAsync();
            if (bot == null || bot.BotStatus != "Active")
            {
                return Result<AgentChatReplyModel>.Fail($"Agent {agent} chưa bật – chọn agent khác hoặc nhờ IT bật ở Trung tâm agent");
            }
            string? baseUrl = ChatUrl(o, agent);
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return Result<AgentChatReplyModel>.Fail("Trợ lý AI chưa kết nối máy chủ Hermes (Agent:HermesApiUrl) – vẫn hỏi được qua bot Trợ lý; liên hệ IT");
            }
            string correlation = "chat:" + Guid.NewGuid().ToString("N")[..16];
            var history = (request.History ?? []).Where(h => h.Role is "user" or "assistant" && !string.IsNullOrWhiteSpace(h.Content)).TakeLast(20)
                .Select(h => new { role = h.Role, content = h.Content!.Length > 4000 ? h.Content[..4000] : h.Content }).ToList();
            DateTime started = clock.Now;
            int status = 0;
            string? error = null;
            string reply = string.Empty;
            try
            {
                // API server Hermes kiểu OpenAI: POST {url}/chat/completions, model = kỹ năng delicare-<mã>, user = khóa lịch sử theo người dùng.
                // Vé người hỏi đi ở header X-On-Behalf-Of (Hermes chuyển tiếp vào lời gọi MCP) – KHÔNG đưa vào nội dung hội thoại để mô hình không thấy / không bỏ được.
                string ticket = AgentWebTicket.Issue(configuration, actor.UserId, DateTime.UtcNow);
                var messages = history.Select(h => new { h.role, h.content }).Append(new { role = (string?)"user", content = message }).ToList();
                using var msg = new HttpRequestMessage(HttpMethod.Post, baseUrl!.TrimEnd('/') + "/chat/completions")
                {
                    Content = new StringContent(JsonSerializer.Serialize(new { model = "delicare-" + agent.ToLowerInvariant(), messages, user = "user:" + actor.UserId.ToString("N"), stream = false }), Encoding.UTF8, "application/json"),
                };
                msg.Headers.Add("X-On-Behalf-Of", ticket);
                msg.Headers.Add("X-Agent-Code", agent);
                if (Environment.GetEnvironmentVariable(o.HermesApiKeyEnv) is { Length: > 0 } key)
                {
                    msg.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
                }
                msg.Headers.Add("X-Correlation-Id", correlation);
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Clamp(o.ChatTimeoutSeconds, 5, 300)));
                using var res = await Http.SendAsync(msg, cts.Token);
                status = (int)res.StatusCode;
                string text = await res.Content.ReadAsStringAsync(cts.Token);
                if (!res.IsSuccessStatusCode)
                {
                    error = $"Hermes trả {status}";
                }
                else
                {
                    using var doc = JsonDocument.Parse(text);
                    reply = ReadReply(doc.RootElement);
                    if (reply.Length == 0)
                    {
                        error = "Hermes không trả câu trả lời";
                    }
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or OperationCanceledException)
            {
                error = ex is TaskCanceledException or OperationCanceledException ? "Hermes không phản hồi kịp" : "Không kết nối được Hermes";
                Log.Warning(ex, "[AgentChat] {Agent}", agent);
            }
            db.IntegrationLog.Add(new IntegrationLog
            {
                Id = Guid.NewGuid(), IntegrationChannelId = bot.ChannelId, DirectionCode = "OUT", OperationCode = "agent_chat", CorrelationId = correlation, RequestAt = started, ResponseAt = clock.Now,
                HttpStatus = status == 0 ? null : status, LogStatus = error == null ? "Succeeded" : "Failed", ErrorCode = "CHAT:" + agent,
                ErrorMessage = JsonSerializer.Serialize(new { user = actor.UserId, chars = message.Length, error }), CreatedDate = clock.Now, CreatedBy = actor.UserId, IsDeleted = false,
            });
            await db.SaveChangesAsync();
            return error == null
                ? Result<AgentChatReplyModel>.Success(new AgentChatReplyModel { Agent = agent, Reply = reply, CorrelationId = correlation }, "OK")
                : Result<AgentChatReplyModel>.Fail($"Trợ lý {agent} đang bận / không phản hồi ({error}) – thử lại sau ít phút");
        }
    }
}
