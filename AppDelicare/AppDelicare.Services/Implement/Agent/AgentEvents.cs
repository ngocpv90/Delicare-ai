using System.Net.Http.Json;
using System.Text.Json;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Services.Implement.Messaging;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Serilog;

namespace AppDelicare.Services.Implement.Agent
{
    /// <summary>AI-4: 1 sự kiện cho agent. <see cref="Key"/> ổn định – Hermes dùng để không xử lý trùng.</summary>
    public sealed class AgentEventModel
    {
        public string Key { get; set; } = string.Empty;
        /// <summary>INCIDENT / ALERT / APPROVAL / JOB / COMPLAINT.</summary>
        public string Type { get; set; } = string.Empty;
        /// <summary>HIGH / MEDIUM / LOW.</summary>
        public string Severity { get; set; } = "LOW";
        /// <summary>Agent nên nhận (VIVI nhận mọi sự kiện để điều phối).</summary>
        public string[] Agents { get; set; } = [];
        public string Title { get; set; } = string.Empty;
        public string? Detail { get; set; }
        /// <summary>Mã chứng từ / mã việc (số phản ánh, số yêu cầu duyệt, mã job…).</summary>
        public string? Number { get; set; }
        /// <summary>Đường dẫn màn web (vd <c>#/qc-temperature</c>).</summary>
        public string? Route { get; set; }
        public DateTime OccurredAt { get; set; }
    }

    public sealed class AgentEventFeedModel
    {
        public DateTime Since { get; set; }
        /// <summary>Mốc cho lần gọi sau (<c>since = next</c>) – không trả trùng.</summary>
        public DateTime Next { get; set; }
        public List<AgentEventModel> Items { get; set; } = [];
    }

    /// <summary>AI-4: nơi định tuyến sự kiện → agent (một chỗ, dùng cho nguồn sự kiện + webhook).</summary>
    public static class AgentEventRouting
    {
        public const string Coordinator = "VIVI";

        public static string[] ForAlert(string code) => code switch
        {
            "STOCK_BELOW_MIN" or "LOT_EXPIRING" => ["WH"],
            "LOT_ON_HOLD" or "QC_INCOMING_PENDING" or "TEMPERATURE_OUT_OF_RANGE" => ["QA"],
            "COMPLAINT_OVERDUE" or "COMPLAINT_OPEN" => ["CSKH"],
            "ACCOUNT_RESIGNED_OPEN" or "SHIFT_SHORT" => ["HR"],
            "CLEAN_TRAYS_SHORT" or "ROUTE_NOT_READY" => ["LOG"],
            _ => [Coordinator],
        };

        public static string[] ForIncident(string template) => template switch
        {
            NotificationCatalog.FoodSafetyCritical or NotificationCatalog.CoreTempLow or NotificationCatalog.IotOutOfRange
                or NotificationCatalog.QcIncomingFail or NotificationCatalog.DeliveryTempLow => ["QA"],
            NotificationCatalog.StopLate or NotificationCatalog.TripNotDeparted or NotificationCatalog.ShortageReported => ["LOG"],
            _ => [Coordinator],
        };

        public static string[] ForJob(string? code) => code switch
        {
            "LotExpiry" => ["WH"],
            "Mrp" or "LockOrders" or "OrderMissingAtCutoff" => ["PLAN"],
            "MenuAutoDraft" or "MenuDraftNotice" or "MealCostRecalc" => ["KITCHEN"],
            "MarkAbsent" or "AccountLockResigned" or "LeaveHandover" => ["HR"],
            _ => [Coordinator],
        };

        public static string RouteForIncident(string template) => template switch
        {
            NotificationCatalog.StopLate or NotificationCatalog.TripNotDeparted or NotificationCatalog.ShortageReported => "#/log-execution",
            NotificationCatalog.QcIncomingFail => "#/qc-inspection",
            _ => "#/qc-temperature",
        };

        public static bool Visible(AgentEventModel e, string agent) => agent == Coordinator || e.Agents.Contains(agent);
    }

    /// <summary>
    /// AI-4: ghi nhận sự cố gấp (mẫu thông báo <see cref="NotificationCatalog.IsIncident"/>) cho agent – 1 dòng <c>IntegrationLog</c> (DirectionCode OUT,
    /// OperationCode <c>agent_event</c>, IdempotencyKey = <c>INC:&lt;khóa sự việc&gt;</c> → mỗi sự việc 1 lần) + đẩy ngay sang Hermes nếu có
    /// <c>Agent:HermesWebhookUrl</c>. Lỗi / Hermes tắt KHÔNG ảnh hưởng nghiệp vụ (nuốt lỗi, timeout ngắn). Không bảng mới (GAP-AI-LOG tạm).
    /// </summary>
    public interface IAgentIncidentService
    {
        Task RecordAsync(NotificationRequest request, CancellationToken ct = default);
    }

    public class AgentIncidentService(IUnitOfWork unitOfWork, IClock clock, ICurrentActor currentActor, IOptionsMonitor<AgentOptions> options) : IAgentIncidentService
    {
        public const string Operation = "agent_event";
        private static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan }; // thời hạn theo Agent:WebhookTimeoutSeconds (mỗi lần gửi)
        private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web) { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        public static string KeyFor(NotificationRequest r) => "INC:" + (string.IsNullOrWhiteSpace(r.IdempotencyKey) ? r.Template + ":" + Guid.NewGuid().ToString("N") : r.IdempotencyKey.Trim());

        public async Task RecordAsync(NotificationRequest request, CancellationToken ct = default)
        {
            if (!NotificationCatalog.IsIncident(request.Template))
            {
                return;
            }
            try
            {
                string key = Trim(KeyFor(request), 100)!;
                var db = unitOfWork.DbContext;
                if (db.IntegrationLog.Local.Any(l => l.IdempotencyKey == key && l.OperationCode == Operation)
                    || await db.IntegrationLog.AsNoTracking().AnyAsync(l => l.IdempotencyKey == key && l.OperationCode == Operation && !l.IsDeleted, ct))
                {
                    return; // 1 sự việc = 1 lần
                }
                DateTime now = clock.Now;
                var (data, _) = AgentMask.Apply(request.Data, 50, Web);
                var evt = new
                {
                    key, type = "INCIDENT", template = request.Template, severity = "HIGH", agents = AgentEventRouting.ForIncident(request.Template),
                    route = AgentEventRouting.RouteForIncident(request.Template), occurredAt = now, correlationId = request.CorrelationId, data,
                };
                string json = JsonSerializer.Serialize(evt, Web);
                var o = options.CurrentValue;
                int? status = null;
                string logStatus = "Recorded";
                if (o.Enabled && !string.IsNullOrWhiteSpace(o.HermesWebhookUrl))
                {
                    try
                    {
                        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        cts.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(o.WebhookTimeoutSeconds, 1, 60)));
                        using var res = await Http.PostAsync(o.HermesWebhookUrl, new StringContent(json, System.Text.Encoding.UTF8, "application/json"), cts.Token);
                        status = (int)res.StatusCode;
                        logStatus = res.IsSuccessStatusCode ? "Sent" : "Failed";
                    }
                    catch (Exception ex)
                    {
                        logStatus = "Failed";
                        Log.Warning("[AgentEvent] webhook Hermes lỗi ({Key}): {Error} – nghiệp vụ vẫn tiếp tục", key, ex.Message);
                    }
                }
                Guid? channel = await db.IntegrationChannel.AsNoTracking().Where(c => c.ChannelCode == AgentGatewayService.ChannelCode && !c.IsDeleted).Select(c => (Guid?)c.Id).FirstOrDefaultAsync(ct);
                db.IntegrationLog.Add(new IntegrationLog
                {
                    Id = Guid.NewGuid(), IntegrationChannelId = channel, DirectionCode = "OUT", OperationCode = Operation, IdempotencyKey = key,
                    CorrelationId = Trim(request.CorrelationId ?? key, 100), RequestAt = now, ResponseAt = clock.Now, HttpStatus = status, LogStatus = logStatus,
                    ErrorCode = Trim(request.Template, 100), ErrorMessage = Trim(json, 2000),
                    CreatedDate = now, CreatedBy = currentActor.Actor?.UserId, IsDeleted = false,
                });
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[AgentEvent] không ghi được sự cố {Template} – nghiệp vụ vẫn tiếp tục", request.Template);
            }
        }

        private static string? Trim(string? s, int max) => s == null ? null : s.Length <= max ? s : s[..max];
    }

    /// <summary>
    /// AI-4 (D-133): nguồn sự kiện <c>lay_su_kien(since)</c> – gom từ dữ liệu có sẵn (không bảng mới): sự cố gấp (<see cref="AgentIncidentService"/>),
    /// duyệt mới / quá hạn, lần chạy job (<c>AnalyticsRefreshLog</c>), phản ánh mới, cảnh báo đang mở (<c>CommonService.GetAlerts</c> theo quyền agent).
    /// Sự kiện có thời điểm lọc <c>since &lt; t ≤ next</c>; cảnh báo không có thời điểm → khóa <c>ALERT:&lt;mã&gt;:&lt;ngày&gt;:&lt;số&gt;</c>, mỗi agent nhận 1 lần
    /// (nhớ trong bộ nhớ 26 giờ). Chạy dưới actor agent (gọi qua ToolGateway).
    /// </summary>
    public interface IAgentEventService
    {
        Task<AgentEventFeedModel> GetEvents(DateTime? since, string agentCode);
    }

    public class AgentEventService(IUnitOfWork unitOfWork, IClock clock, ICommonService common, IMemoryCache cache) : IAgentEventService
    {
        public async Task<AgentEventFeedModel> GetEvents(DateTime? since, string agentCode)
        {
            DateTime now = clock.Now;
            now = new DateTime(now.Ticks - now.Ticks % TimeSpan.TicksPerSecond, now.Kind); // mốc next tròn giây
            DateTime from = since is { } s && s > now.AddDays(-7) ? (s > now ? now : s) : now.AddDays(-1);
            string agent = (agentCode ?? string.Empty).Trim().ToUpperInvariant();
            var db = unitOfWork.DbContext;
            var items = new List<AgentEventModel>();

            // 1. Sự cố gấp đã ghi nhận
            var incidents = await db.IntegrationLog.AsNoTracking()
                .Where(l => !l.IsDeleted && l.OperationCode == AgentIncidentService.Operation && l.RequestAt > from && l.RequestAt <= now)
                .Select(l => new { l.IdempotencyKey, l.ErrorCode, l.ErrorMessage, l.RequestAt }).ToListAsync();
            foreach (var i in incidents)
            {
                string template = i.ErrorCode ?? string.Empty;
                items.Add(new AgentEventModel
                {
                    Key = i.IdempotencyKey ?? template, Type = "INCIDENT", Severity = "HIGH", Agents = AgentEventRouting.ForIncident(template), Title = IncidentTitle(template),
                    Detail = DataOf(i.ErrorMessage), Number = template, Route = AgentEventRouting.RouteForIncident(template), OccurredAt = i.RequestAt,
                });
            }

            // 2. Duyệt mới gửi / vừa quá hạn
            var approvals = await db.ApprovalRequest.AsNoTracking()
                .Where(a => !a.IsDeleted && a.RequestStatus == "Pending" && ((a.RequestedAt > from && a.RequestedAt <= now) || (a.DueAt > from && a.DueAt <= now)))
                .Select(a => new { a.Id, a.RequestNo, a.BusinessType, a.RequestedAt, a.DueAt, a.PriorityCode }).ToListAsync();
            foreach (var a in approvals)
            {
                if (a.RequestedAt > from && a.RequestedAt <= now)
                {
                    items.Add(new AgentEventModel
                    {
                        Key = $"APPROVAL:{a.Id:N}:NEW", Type = "APPROVAL", Severity = a.PriorityCode is "HIGH" or "URGENT" ? "MEDIUM" : "LOW", Agents = [AgentEventRouting.Coordinator],
                        Title = $"Yêu cầu duyệt mới {a.BusinessType}", Number = a.RequestNo, Route = "#/wf-inbox", OccurredAt = a.RequestedAt,
                    });
                }
                if (a.DueAt is { } due && due > from && due <= now)
                {
                    items.Add(new AgentEventModel
                    {
                        Key = $"APPROVAL:{a.Id:N}:OVERDUE", Type = "APPROVAL", Severity = "MEDIUM", Agents = [AgentEventRouting.Coordinator],
                        Title = $"Yêu cầu duyệt quá hạn {a.BusinessType}", Number = a.RequestNo, Route = "#/wf-inbox", OccurredAt = due,
                    });
                }
            }

            // 3. Lần chạy job xong (thành công / lỗi)
            var runs = await db.AnalyticsRefreshLog.AsNoTracking()
                .Where(r => r.CompletedAt > from && r.CompletedAt <= now && (r.RunStatus == "Succeeded" || r.RunStatus == "Failed"))
                .Select(r => new { r.RunId, r.JobCode, r.RunStatus, r.CompletedAt, r.RowsProcessed, r.ErrorMessage }).ToListAsync();
            foreach (var r in runs)
            {
                bool failed = r.RunStatus == "Failed";
                items.Add(new AgentEventModel
                {
                    Key = $"JOB:{r.RunId:N}", Type = "JOB", Severity = failed ? "HIGH" : "LOW",
                    Agents = failed ? [AgentEventRouting.Coordinator, .. AgentEventRouting.ForJob(r.JobCode)] : AgentEventRouting.ForJob(r.JobCode),
                    Title = failed ? $"Job {r.JobCode} lỗi" : $"Job {r.JobCode} đã chạy xong", Detail = failed ? Trim(r.ErrorMessage, 300) : $"{r.RowsProcessed ?? 0} dòng",
                    Number = r.JobCode, Route = "#/adm-job", OccurredAt = r.CompletedAt!.Value,
                });
            }

            // 4. Phản ánh mới (theo thời điểm nhập)
            var complaints = await db.CustomerComplaint.AsNoTracking()
                .Where(c => !c.IsDeleted && c.CreatedDate > from && c.CreatedDate <= now)
                .Select(c => new { c.Id, c.ComplaintNo, c.Severity, c.CategoryCode, c.CreatedDate }).ToListAsync();
            foreach (var c in complaints)
            {
                bool high = string.Equals(c.Severity, "High", StringComparison.OrdinalIgnoreCase) || string.Equals(c.Severity, "Critical", StringComparison.OrdinalIgnoreCase);
                items.Add(new AgentEventModel
                {
                    Key = $"COMPLAINT:{c.Id:N}:NEW", Type = "COMPLAINT", Severity = high ? "HIGH" : "MEDIUM", Agents = high ? ["CSKH", "QA"] : ["CSKH"],
                    Title = "Phản ánh mới của khách" + (c.CategoryCode is { Length: > 0 } cat ? $" ({cat})" : string.Empty), Number = c.ComplaintNo, Route = "#/log-complaint", OccurredAt = c.CreatedDate,
                });
            }

            // 5. Cảnh báo đang mở – theo quyền agent; mỗi khóa 1 lần / agent
            var alerts = (await common.GetAlerts(null)).ListData ?? [];
            foreach (var a in alerts)
            {
                string key = $"ALERT:{a.Code}:{now:yyyyMMdd}:{a.Count}";
                var e = new AgentEventModel
                {
                    Key = key, Type = "ALERT", Severity = a.Severity, Agents = AgentEventRouting.ForAlert(a.Code), Title = a.Title, Detail = a.Detail, Number = a.Code, Route = a.Route, OccurredAt = now,
                };
                if (!AgentEventRouting.Visible(e, agent))
                {
                    continue;
                }
                string seen = $"agent-evt-seen:{agent}:{key}";
                if (cache.TryGetValue(seen, out _))
                {
                    continue;
                }
                cache.Set(seen, true, TimeSpan.FromHours(26));
                items.Add(e);
            }

            return new AgentEventFeedModel
            {
                Since = from, Next = now,
                Items = [.. items.Where(e => AgentEventRouting.Visible(e, agent)).OrderBy(e => e.Severity == "HIGH" ? 0 : e.Severity == "MEDIUM" ? 1 : 2).ThenBy(e => e.OccurredAt)],
            };
        }

        private static string IncidentTitle(string template) => NotificationCatalog.Find(template)?.Description is { Length: > 0 } n ? n : template;

        private static string? DataOf(string? json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }
            try
            {
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.TryGetProperty("data", out var d) ? Trim(d.GetRawText(), 500) : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string? Trim(string? s, int max) => s == null ? null : s.Length <= max ? s : s[..max];
    }
}
