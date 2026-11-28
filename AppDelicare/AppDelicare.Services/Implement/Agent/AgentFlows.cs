using System.Text.Json;
using System.Text.Json.Nodes;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Helper;
using AppDelicare.Services.Implement.Messaging;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AppDelicare.Services.Implement.Agent
{
    // ======================================================================= gửi cảnh báo chủ động

    /// <summary>
    /// AI-11 (D-133): agent nhận sự kiện (AI-4 <c>lay_su_kien</c>) → nhận xét + phương án → <c>gui_canh_bao</c> gửi tin tới ĐÚNG người làm việc đó (người có quyền Thêm
    /// trên màn liên quan) qua bot Vận hành (VIVI: bot Điều hành). Chống trùng: <c>IdempotencyKey = AGENT_ALERT:&lt;agent&gt;:&lt;khóa sự kiện&gt;</c> – mỗi sự kiện một tin.
    /// Màn phải nằm trong quyền Xem của chính agent; nội dung agent soạn là văn bản (không lệnh), cắt độ dài.
    /// </summary>
    public static partial class AgentToolRegistry
    {
        public const string AlertTemplate = NotificationCatalog.AgentAlert;

        private static IEnumerable<AgentToolDef> AlertTools() =>
        [
            new AgentToolDef
            {
                Name = "gui_canh_bao", ScreenId = null, Agents = ["VIVI", "WH", "KITCHEN", "QA", "SALES", "PROC", "LOG", "HR", "PLAN", "PKG", "ACC"],
                Description = "Gửi 1 tin cảnh báo chủ động qua bot tới người phụ trách màn liên quan (người có quyền Thêm trên màn đó): tiêu đề, nhận xét, tối đa 5 phương án. "
                    + "khoa_su_kien = khóa sự kiện (Key của lay_su_kien) – mỗi khóa chỉ gửi 1 lần. Dùng X-Correlation-Id của luồng để màn Luồng liên phòng nối các bước.",
                InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"khoa_su_kien\":{\"type\":\"string\"},\"man\":{\"type\":\"string\",\"description\":\"screen-id liên quan, vd scm-requirement\"},"
                    + "\"tieu_de\":{\"type\":\"string\"},\"nhan_xet\":{\"type\":\"string\"},\"phuong_an\":{\"type\":\"array\",\"items\":{\"type\":\"string\"}},\"muc_do\":{\"type\":\"string\",\"enum\":[\"CAO\",\"VUA\",\"THAP\"]}},"
                    + "\"required\":[\"khoa_su_kien\",\"man\",\"tieu_de\",\"nhan_xet\"]}",
                RunAsync = async (ctx, a) =>
                {
                    string key = Str(a, "khoa_su_kien") ?? throw new AgentToolException("Thiếu khóa sự kiện (khoa_su_kien)");
                    string screen = (Str(a, "man") ?? throw new AgentToolException("Thiếu màn liên quan (man)")).Trim().TrimStart('#', '/');
                    string title = Cut(Str(a, "tieu_de") ?? throw new AgentToolException("Thiếu tiêu đề"), 150);
                    string comment = Cut(Str(a, "nhan_xet") ?? throw new AgentToolException("Thiếu nhận xét"), 800);
                    var perm = Svc<IPermissionService>(ctx);
                    if (!perm.IsAllowed(await perm.GetAsync(ctx.AgentUserId), screen, PermissionAction.View))
                    {
                        throw new AgentToolException($"Agent {ctx.AgentCode} không có quyền xem màn {screen} – không gửi cảnh báo màn này được");
                    }
                    var options = a.ValueKind == JsonValueKind.Object && a.TryGetProperty("phuong_an", out var o) && o.ValueKind == JsonValueKind.Array
                        ? o.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x)).Take(5).Select(x => "• " + Cut(x!, 200)).ToList() : [];
                    string level = (Str(a, "muc_do") ?? "VUA").ToUpperInvariant();
                    string idem = $"AGENT_ALERT:{ctx.AgentCode}:{Cut(key, 150)}";
                    var db = Svc<IUnitOfWork>(ctx).DbContext;
                    if (await db.OutboundMessage.AsNoTracking().AnyAsync(m => !m.IsDeleted && m.IdempotencyKey != null && m.IdempotencyKey.StartsWith(idem)))
                    {
                        return new { daGui = false, lyDo = "Sự kiện này đã được cảnh báo – không gửi lại", khoa = key };
                    }
                    int sent = await BusinessNotifier.ToMenusAsync(Svc<INotificationService>(ctx), db, AlertTemplate, ["#/" + screen], new
                    {
                        title = (level == "CAO" ? "🔴 " : level == "THAP" ? "🟢 " : "🟠 ") + $"[{ctx.AgentCode}] {title}", detail = comment, lines = options,
                    }, idem, correlationId: string.IsNullOrWhiteSpace(ctx.CorrelationId) ? key : ctx.CorrelationId, requireAdd: true,
                        botCode: ctx.AgentCode == "VIVI" ? MessagingCode.BotDieuHanh : MessagingCode.BotVanHanh);
                    return new { daGui = sent > 0, soNguoiNhan = sent, khoa = key, lyDo = sent > 0 ? null : "Không có người nhận (chưa ai có quyền Thêm màn này / chưa liên kết bot)" };
                },
            },
        ];

        private static string Cut(string s, int max) => s.Length <= max ? s.Trim() : s[..max].Trim() + "…";
    }

    // ======================================================================= luồng liên phòng

    public sealed class AgentFlowStepModel
    {
        public DateTime At { get; set; }
        /// <summary>EVENT / CALL / PROPOSAL / DECISION / RESULT / MESSAGE.</summary>
        public string Kind { get; set; } = string.Empty;
        public string? Agent { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Detail { get; set; }
        /// <summary>OK / WAIT / FAIL.</summary>
        public string Status { get; set; } = "OK";
        public string? Link { get; set; }
    }

    public sealed class AgentFlowModel
    {
        public string CorrelationId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public List<string> Agents { get; set; } = [];
        /// <summary>DONE / WAITING / FAILED / INFO.</summary>
        public string Status { get; set; } = "INFO";
        public DateTime StartedAt { get; set; }
        public DateTime LastAt { get; set; }
        public List<AgentFlowStepModel> Steps { get; set; } = [];
    }

    public sealed class AgentFlowBoardModel
    {
        public DateTime Date { get; set; }
        public int Total { get; set; }
        public int Done { get; set; }
        public int Waiting { get; set; }
        public int Failed { get; set; }
        public int Messages { get; set; }
        public List<AgentFlowModel> ListFlow { get; set; } = [];
    }

    /// <summary>
    /// AI-11 (D-133): màn "Luồng liên phòng" (<c>ai-flow</c>) – dựng từ <c>CorrelationId</c> chung của: sự kiện (IntegrationLog agent_event), lời gọi công cụ của agent
    /// (IntegrationLog kênh HERMES), đề xuất ghi (ApprovalRequest AGENT_WRITE – correlationId trong phần đề xuất), quyết định (ApprovalDecision), kết quả
    /// (AuditChange AgentCommand Executed) và tin đã gửi (OutboundMessage). Không bảng mới. Chỉ hiện luồng có sự kiện / đề xuất / tin (bỏ lời gọi đọc lẻ).
    /// </summary>
    public interface IAgentFlowService
    {
        Task<IResult<AgentFlowBoardModel>> GetFlows(DateTime? date);
        Task<IResult<bool>> SaveSchedule(string? id, bool enabled);
    }

    public sealed partial class AgentFlowService(IUnitOfWork unitOfWork, IClock clock, IConfiguration configuration, Microsoft.Extensions.Hosting.IHostEnvironment env) : IAgentFlowService
    {
        private static readonly string[] KindOrder = ["EVENT", "CALL", "PROPOSAL", "DECISION", "RESULT", "MESSAGE"];

        public async Task<IResult<AgentFlowBoardModel>> GetFlows(DateTime? date)
        {
            var db = unitOfWork.DbContext;
            DateTime day = (date ?? clock.Today).Date, next = day.AddDays(1);
            var steps = new List<(string Corr, AgentFlowStepModel Step)>();

            var logs = await db.IntegrationLog.AsNoTracking().Where(l => !l.IsDeleted && l.CorrelationId != null && l.RequestAt >= day && l.RequestAt < next
                    && (l.OperationCode == AgentIncidentService.Operation || l.DirectionCode == "IN"))
                .Select(l => new { l.CorrelationId, l.OperationCode, l.DirectionCode, l.ErrorCode, l.ErrorMessage, l.LogStatus, l.RequestAt }).ToListAsync();
            foreach (var l in logs)
            {
                bool ev = l.OperationCode == AgentIncidentService.Operation;
                string? agent = !ev && l.ErrorCode is { } ec ? ec.Split(':').LastOrDefault() : null;
                steps.Add((l.CorrelationId!, new AgentFlowStepModel
                {
                    At = l.RequestAt, Kind = ev ? "EVENT" : "CALL", Agent = agent, Title = ev ? $"Sự kiện {l.ErrorCode}" : $"{agent} gọi {l.OperationCode}",
                    Status = l.LogStatus == "Succeeded" || ev ? "OK" : "FAIL", Detail = ev ? null : l.LogStatus == "Succeeded" ? null : l.ErrorMessage,
                }));
            }

            var reqs = await db.ApprovalRequest.AsNoTracking().Where(r => !r.IsDeleted && r.BusinessType == AgentCommandService.BusinessType && r.RequestedAt >= day && r.RequestedAt < next)
                .Select(r => new { r.Id, r.BusinessId, r.RequestNo, r.RequestStatus, r.RequestedAt, r.PayloadJson }).ToListAsync();
            var reqIds = reqs.Select(r => r.Id).ToList();
            var bizIds = reqs.Select(r => r.BusinessId).ToList();
            var decisions = await db.ApprovalDecision.AsNoTracking().Where(d => reqIds.Contains(d.ApprovalRequestId) && !d.IsDeleted)
                .Select(d => new { d.ApprovalRequestId, d.DecisionCode, d.DecidedAt, d.DecidedBy, d.Comment }).ToListAsync();
            var executed = await db.AuditChange.AsNoTracking().Where(a => a.EntityName == "AgentCommand" && a.Action == "Executed" && a.IdChange != null && bizIds.Contains(a.IdChange) && !a.IsDeleted)
                .Select(a => new { a.IdChange, a.CreatedDate, a.Changes }).ToListAsync();
            foreach (var r in reqs)
            {
                var p = AgentWriteApprovalHandler.ProposalOf(r.PayloadJson);
                string corr = p?["correlationId"]?.ToString() is { Length: > 0 } c ? c : "REQ:" + r.RequestNo;
                string? agent = p?["agent"]?.ToString();
                string summary = p?["preview"]?["Summary"]?.ToString() ?? p?["preview"]?["summary"]?.ToString() ?? p?["title"]?.ToString() ?? p?["command"]?.ToString() ?? r.RequestNo ?? string.Empty;
                steps.Add((corr, new AgentFlowStepModel
                {
                    At = r.RequestedAt, Kind = "PROPOSAL", Agent = agent, Title = $"Đề xuất {r.RequestNo}: {summary}", Detail = p?["reason"]?.ToString(),
                    Status = r.RequestStatus is "Pending" ? "WAIT" : r.RequestStatus is "Approved" ? "OK" : "FAIL", Link = "#/wf-inbox?type=ai-agent-cmd",
                }));
                foreach (var d in decisions.Where(x => x.ApprovalRequestId == r.Id))
                {
                    steps.Add((corr, new AgentFlowStepModel
                    {
                        At = d.DecidedAt, Kind = "DECISION", Title = d.DecisionCode switch { "APPROVE" => "Người duyệt xác nhận", "REJECT" => "Người duyệt từ chối", "CANCEL" => "Hết hạn / hủy", _ => d.DecisionCode ?? "Quyết định" },
                        Detail = d.Comment, Status = d.DecisionCode == "APPROVE" ? "OK" : "FAIL",
                    }));
                }
                foreach (var e in executed.Where(x => x.IdChange == r.BusinessId))
                {
                    string? result = null;
                    try { result = JsonNode.Parse(e.Changes ?? "{}")?["result"]?.ToString(); } catch (JsonException) { }
                    steps.Add((corr, new AgentFlowStepModel { At = e.CreatedDate, Kind = "RESULT", Agent = agent, Title = result ?? "Đã ghi nghiệp vụ", Status = "OK" }));
                }
            }

            var corrs = steps.Select(s => s.Corr).Distinct().ToList();
            var msgs = await db.OutboundMessage.AsNoTracking().Where(m => !m.IsDeleted && m.CorrelationId != null && m.QueuedAt >= day && m.QueuedAt < next
                    && (corrs.Contains(m.CorrelationId) || (m.IdempotencyKey != null && m.IdempotencyKey.StartsWith("AGENT_ALERT:"))))
                .Select(m => new { m.CorrelationId, m.MessageType, m.MessageStatus, m.QueuedAt, m.IdempotencyKey, m.LastError }).ToListAsync();
            foreach (var g in msgs.GroupBy(m => (m.CorrelationId!, m.IdempotencyKey?.Split(':').Take(3).Aggregate((x, y) => x + ":" + y) ?? m.MessageType)))
            {
                var f = g.First();
                bool alert = f.IdempotencyKey?.StartsWith("AGENT_ALERT:", StringComparison.Ordinal) == true;
                steps.Add((g.Key.Item1, new AgentFlowStepModel
                {
                    At = g.Min(x => x.QueuedAt), Kind = "MESSAGE", Agent = alert ? f.IdempotencyKey!.Split(':')[1] : null,
                    Title = (alert ? "Cảnh báo chủ động" : "Tin bot " + f.MessageType) + $" → {g.Count()} người",
                    Status = g.All(x => x.MessageStatus is "Failed" or "Dead") ? "FAIL" : "OK", Detail = g.Select(x => x.LastError).FirstOrDefault(x => x != null),
                }));
            }

            var flows = steps.GroupBy(s => s.Corr).Select(g =>
            {
                // Kết quả ghi nghiệp vụ chạy TRONG lúc duyệt (cùng giây, ghi trước quyết định) → xếp sau bước duyệt.
                var raw = g.Select(x => x.Step).ToList();
                foreach (var res in raw.Where(x => x.Kind == "RESULT"))
                {
                    var dec = raw.Where(x => x.Kind == "DECISION" && x.At >= res.At.AddSeconds(-5)).Select(x => (DateTime?)x.At).Max();
                    if (dec is { } d && d >= res.At)
                    {
                        res.At = d.AddMilliseconds(1);
                    }
                }
                var list = raw.OrderBy(s => s.At).ThenBy(s => Array.IndexOf(KindOrder, s.Kind)).ToList();
                var lead = list.FirstOrDefault(s => s.Kind is "EVENT" or "PROPOSAL" or "MESSAGE") ?? list[0];
                return new AgentFlowModel
                {
                    CorrelationId = g.Key, Title = lead.Title, StartedAt = list[0].At, LastAt = list[^1].At, Steps = list,
                    Agents = [.. list.Select(s => s.Agent).Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a!).Distinct()],
                    Status = list.Any(s => s.Status == "FAIL" && s.Kind is "DECISION" or "RESULT" or "CALL" or "PROPOSAL") ? "FAILED"
                        : list.Any(s => s.Status == "WAIT") ? "WAITING" : list.Any(s => s.Kind is "RESULT" or "MESSAGE") ? "DONE" : "INFO",
                };
            })
                // Bỏ lời gọi đọc lẻ (không sự kiện / đề xuất / tin) – không phải luồng.
                .Where(f => f.Steps.Any(s => s.Kind is "EVENT" or "PROPOSAL" or "MESSAGE"))
                .OrderByDescending(f => f.LastAt).Take(200).ToList();
            var board = new AgentFlowBoardModel
            {
                Date = day, Total = flows.Count, Done = flows.Count(f => f.Status == "DONE"), Waiting = flows.Count(f => f.Status == "WAITING"), Failed = flows.Count(f => f.Status == "FAILED"),
                Messages = flows.Sum(f => f.Steps.Count(s => s.Kind == "MESSAGE")), ListFlow = flows,
            };
            return Result<AgentFlowBoardModel>.Success(board, ConstantHelper.Message.GetAllSuccessfully);
        }

        /// <summary>
        /// AI-11: bật / tắt 1 việc theo lịch – ghi lại <c>Agent:SchedulesFile</c> (agents/schedules.json, Docker: volume dùng chung với Hermes; Hermes đọc lại khi sinh cron).
        /// Chỉ đổi cờ <c>enabled</c>, giữ nguyên phần còn lại của tệp.
        /// </summary>
        public async Task<IResult<bool>> SaveSchedule(string? id, bool enabled)
        {
            string? path = AgentAdminService.SchedulesPath(configuration, env.ContentRootPath);
            if (path == null || !File.Exists(path))
            {
                return Result<bool>.Fail("Chưa có agents/schedules.json (Agent:SchedulesFile)", false);
            }
            var root = JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();
            var item = (root["items"] as JsonArray ?? []).OfType<JsonObject>().FirstOrDefault(x => string.Equals(x["id"]?.ToString(), id, StringComparison.OrdinalIgnoreCase));
            if (item == null)
            {
                return Result<bool>.Fail($"Không có việc «{id}» trong lịch", false);
            }
            if (enabled && item["tools"] is JsonArray { Count: 0 })
            {
                return Result<bool>.Fail("Việc này chưa có công cụ (agent chưa bật) – chưa bật được", false);
            }
            item["enabled"] = enabled;
            await File.WriteAllTextAsync(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
            return Result<bool>.Success(true, enabled ? $"Đã bật «{item["title"]}»" : $"Đã tắt «{item["title"]}» – Hermes dừng việc này từ lần đọc lịch sau");
        }
    }
}
