using System.Text.Json;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Services.Implement.Knowledge;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace AppDelicare.Services.Implement.Agent
{
    /// <summary>AI-6 (D-133): danh mục 13 agent (AGENTS.md §1) – mô tả hiển thị, không phải cấu hình (bật / tắt = BotConfiguration.BotStatus).</summary>
    public sealed record AgentInfo(string Code, string Name, string Serves, string Purpose, string Writes, int Wave, string Color);

    public static class AgentCatalog
    {
        public static readonly IReadOnlyList<AgentInfo> All =
        [
            new("VIVI", "Điều phối trung tâm", "Ban GĐ, mọi người", "Bản tin sáng / cuối ngày, KPI, sự cố, việc chờ duyệt; định tuyến câu hỏi cho agent phòng ban", "Không ghi trực tiếp – chuyển cho agent phòng ban", 1, "#7c3aed"),
            new("SALES", "Đơn hàng / Kinh doanh", "KD, TP KD", "Đơn hàng ngày, hợp đồng, danh mục; nhắc đơn chưa chốt", "propose_order_change → TP KD", 2, "#2563eb"),
            new("PLAN", "Kế hoạch sản xuất & thực đơn", "TP vận hành, trưởng bếp", "Kế hoạch ngày, nhu cầu nguyên liệu, thực đơn tuần, giá vốn", "propose_production_plan / menu_change → TP kế hoạch", 3, "#0891b2"),
            new("PROC", "Thu mua", "Mua hàng", "Dự báo thiếu hụt, NCC, giá; đề xuất đơn mua", "propose_purchase_order → TP vận hành / GĐ", 2, "#ca8a04"),
            new("WH", "Kho", "Thủ kho", "Tồn kho, lô cận hạn, truy xuất lô, nhu cầu nguyên liệu", "propose_goods_receipt / issue / stock_count → thủ kho", 1, "#16a34a"),
            new("KITCHEN", "Bếp", "Trưởng ca, tổ trưởng", "Tiến độ bếp theo mẻ / công đoạn, kế hoạch, thực đơn, hao hụt", "propose_batch_event → trưởng ca", 1, "#ea580c"),
            new("QA", "ATTP và Truy xuất", "QA, trưởng bếp", "Truy xuất lô / mẻ, nhiệt độ, phản ánh, lô tạm giữ", "propose_qc_result / hold_lot → QA lead", 1, "#dc2626"),
            new("LOG", "Giao vận", "Điều phối GV, tài xế", "Chuyến giao trong ngày, giao trực tiếp", "propose_route / delivery_event → điều phối GV", 3, "#0d9488"),
            new("PKG", "Hộp / khay và Vệ sinh", "Tổ vệ sinh, KT", "Hộp khay khách giữ, thất thoát, lô vệ sinh", "propose_container_return → giao vận; cleaning_step → tổ trưởng VS / QA", 3, "#64748b"),
            new("HR", "Nhân sự", "NS, trưởng bộ phận", "Chấm công ngày, chưa vào ca, công tháng", "propose_leave / shift_swap / attendance_fix → trưởng bộ phận", 2, "#db2777"),
            new("ACC", "Kế toán", "Kế toán", "Chứng từ kế toán, đối chiếu AMIS, giá vốn & lãi gộp (chỉ đọc – không thay MISA)", "propose_container_loss_charge → người có quyền Sổ hộp khay", 3, "#4f46e5"),
            new("CSKH", "CSKH – Zalo", "Khách hàng", "Đơn và thực đơn của chính khách đang hỏi", "propose_portion_confirm / shortage_report / complaint → KD / điều phối", 4, "#0284c7"),
            new("KNOW", "Thủ thư tri thức", "Mọi agent, mọi người", "Tìm SOP / tiền lệ trong kho tri thức (không bao giờ BOD)", "Ghi nhật ký / bài học nháp 96–98 → chủ thư mục duyệt", 1, "#9333ea"),
        ];

        public static AgentInfo? Find(string? code) => All.FirstOrDefault(a => string.Equals(a.Code, code?.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public sealed class AgentSummaryModel
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Serves { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public string Writes { get; set; } = string.Empty;
        public int Wave { get; set; }
        public string Color { get; set; } = string.Empty;
        /// <summary>Active / Inactive (BotConfiguration.BotStatus); null = chưa seed.</summary>
        public string? Status { get; set; }
        public bool Active { get; set; }
        public bool HasKey { get; set; }
        public List<string> ListTool { get; set; } = [];
        public int CallsToday { get; set; }
        public int OkToday { get; set; }
        public int DeniedToday { get; set; }
        public int FailedToday { get; set; }
        public DateTime? LastCallAt { get; set; }
        public int SchedulesToday { get; set; }
    }

    public sealed class AgentDetailModel
    {
        public AgentSummaryModel Agent { get; set; } = new();
        public List<AgentToolInfoModel> ListTool { get; set; } = [];
        /// <summary>Thư mục kho tri thức được đọc (§7.8) – BOD không bao giờ.</summary>
        public List<string> ListFolder { get; set; } = [];
        public List<AgentScheduleModel> ListSchedule { get; set; } = [];
        public List<AgentActivityModel> ListActivity { get; set; } = [];
    }

    public sealed class AgentToolInfoModel
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? ScreenId { get; set; }
        public bool Write { get; set; }
    }

    public sealed class AgentActivityModel
    {
        public Guid Id { get; set; }
        public DateTime At { get; set; }
        public string? Agent { get; set; }
        public string? Tool { get; set; }
        public string? Status { get; set; }
        public int? HttpStatus { get; set; }
        public long? Ms { get; set; }
        public int? Rows { get; set; }
        public Guid? AskingUserId { get; set; }
        public string? AskingUserName { get; set; }
        public string? OnBehalfOf { get; set; }
        public string? Error { get; set; }
        public string? CorrelationId { get; set; }
    }

    public sealed class AgentActivityRequest
    {
        public string? Agent { get; set; }
        public string? Status { get; set; }
        public string? Tool { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public sealed class AgentActivityPageModel
    {
        public int Total { get; set; }
        public int Ok { get; set; }
        public int Denied { get; set; }
        public int Failed { get; set; }
        public List<AgentActivityModel> ListRow { get; set; } = [];
    }

    public sealed class AgentScheduleModel
    {
        public string Id { get; set; } = string.Empty;
        public string Agent { get; set; } = string.Empty;
        public string? AgentName { get; set; }
        public string? Cron { get; set; }
        public string? At { get; set; }
        public string? Title { get; set; }
        public List<string> ListTool { get; set; } = [];
        public string? CoreJob { get; set; }
        public int Wave { get; set; }
        public bool Enabled { get; set; }
        /// <summary>Agent đang bật (BotStatus) – lịch chỉ chạy khi cả hai bật.</summary>
        public bool AgentActive { get; set; }
        public DateTime? LastRunAt { get; set; }
        public string? LastRunStatus { get; set; }
    }

    /// <summary>
    /// AI-6 (D-133) – Trung tâm agent: danh sách 13 agent (trạng thái, công cụ, lời gọi hôm nay), chi tiết, bật / tắt (= BotStatus – có hiệu lực ngay ở ToolGateway),
    /// nhật ký lời gọi (IntegrationLog kênh HERMES, DirectionCode IN), lịch chạy (đọc <c>app-delicare-agents/schedules.json</c> – một nguồn cho cả cron Hermes).
    /// </summary>
    public interface IAgentAdminService
    {
        Task<IResults<AgentSummaryModel>> GetAll();
        Task<IResult<AgentDetailModel>> GetById(string? code);
        Task<IResult<AgentSummaryModel>> SaveIsActive(string? code, bool active);
        Task<IResult<AgentActivityPageModel>> GetActivity(AgentActivityRequest request);
        Task<IResults<AgentScheduleModel>> GetSchedules();
    }

    public class AgentAdminService(IUnitOfWork unitOfWork, IClock clock, ICurrentActor currentActor, IConfiguration configuration, IHostEnvironment env) : IAgentAdminService
    {
        private static Result<T> Status<T>(int code, string message) => new() { ResponseCode = code, ResponseMessage = message };

        private async Task<Guid?> ChannelAsync() => await unitOfWork.DbContext.IntegrationChannel.AsNoTracking()
            .Where(c => c.ChannelCode == AgentGatewayService.ChannelCode && !c.IsDeleted).Select(c => (Guid?)c.Id).FirstOrDefaultAsync();

        private static string CodeOf(string? errorCode) => errorCode is { } e && e.LastIndexOf("AGENT:", StringComparison.Ordinal) is int i and >= 0 ? e[(i + 6)..] : string.Empty;

        public async Task<IResults<AgentSummaryModel>> GetAll() => Results<AgentSummaryModel>.Success(await SummariesAsync(), "OK");

        private async Task<List<AgentSummaryModel>> SummariesAsync()
        {
            var db = unitOfWork.DbContext;
            Guid? channel = await ChannelAsync();
            var bots = channel == null ? [] : await db.BotConfiguration.AsNoTracking().Where(b => b.IntegrationChannelId == channel && !b.IsDeleted)
                .Select(b => new { b.BotCode, b.BotStatus, b.SecretReference }).ToListAsync();
            DateTime today = clock.Today;
            var logs = channel == null ? [] : await db.IntegrationLog.AsNoTracking()
                .Where(l => l.IntegrationChannelId == channel && l.DirectionCode == "IN" && l.RequestAt >= today && !l.IsDeleted)
                .Select(l => new { l.ErrorCode, l.LogStatus, l.RequestAt }).ToListAsync();
            var schedules = ReadSchedules();
            return AgentCatalog.All.Select(a =>
            {
                var bot = bots.FirstOrDefault(b => string.Equals(b.BotCode, a.Code, StringComparison.OrdinalIgnoreCase));
                var mine = logs.Where(l => CodeOf(l.ErrorCode) == a.Code).ToList();
                return new AgentSummaryModel
                {
                    Code = a.Code, Name = a.Name, Serves = a.Serves, Purpose = a.Purpose, Writes = a.Writes, Wave = a.Wave, Color = a.Color,
                    Status = bot?.BotStatus, Active = bot?.BotStatus == "Active",
                    HasKey = bot?.SecretReference is { Length: > 0 } name && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name) ?? configuration[name]),
                    ListTool = [.. AgentToolRegistry.All.Where(t => t.AllowedFor(a.Code)).Select(t => t.Name)],
                    CallsToday = mine.Count, OkToday = mine.Count(l => l.LogStatus == "Succeeded"), DeniedToday = mine.Count(l => l.LogStatus == "Denied"),
                    FailedToday = mine.Count(l => l.LogStatus == "Failed"), LastCallAt = mine.Count > 0 ? mine.Max(l => l.RequestAt) : null,
                    SchedulesToday = schedules.Count(s => s.Agent == a.Code && s.Enabled),
                };
            }).ToList();
        }

        public async Task<IResult<AgentDetailModel>> GetById(string? code)
        {
            var info = AgentCatalog.Find(code);
            if (info == null)
            {
                return Status<AgentDetailModel>(404, $"Không có agent «{code}»");
            }
            var summary = (await SummariesAsync()).First(s => s.Code == info.Code);
            string[]? scope = KnowledgeVault.AgentScope(info.Code);
            var folders = KnowledgeVault.Folders.Where(f => f.Code != KnowledgeVault.Bod && (scope == null || scope.Contains(f.Code) || f.AgentWritable)).Select(f => f.Dir).ToList();
            var activity = await GetActivity(new AgentActivityRequest { Agent = info.Code, PageSize = 20 });
            var schedules = (await GetSchedules()).ListData.Where(s => s.Agent == info.Code).ToList();
            return Result<AgentDetailModel>.Success(new AgentDetailModel
            {
                Agent = summary,
                ListTool = [.. AgentToolRegistry.All.Where(t => t.AllowedFor(info.Code)).Select(t => new AgentToolInfoModel
                {
                    Name = t.Name, Description = t.Description, ScreenId = t.ScreenId, Write = t.Name.StartsWith("propose_", StringComparison.Ordinal) || t.Name == "ghi_nhat_ky",
                })],
                ListFolder = info.Code == "CSKH" ? ["Chỉ ghi chú công khai của 20 / 30 / 55 / 80"] : folders,
                ListSchedule = schedules,
                ListActivity = activity.Data?.ListRow ?? [],
            }, "OK");
        }

        public async Task<IResult<AgentSummaryModel>> SaveIsActive(string? code, bool active)
        {
            var info = AgentCatalog.Find(code);
            Guid? channel = await ChannelAsync();
            if (info == null || channel == null)
            {
                return Status<AgentSummaryModel>(404, info == null ? $"Không có agent «{code}»" : "Chưa có kênh HERMES (chạy DB/seed/Agent_seed.sql)");
            }
            var db = unitOfWork.DbContext;
            var bot = await db.BotConfiguration.FirstOrDefaultAsync(b => b.IntegrationChannelId == channel && b.BotCode == info.Code && !b.IsDeleted);
            if (bot == null)
            {
                return Status<AgentSummaryModel>(404, $"Agent {info.Code} chưa có cấu hình (chạy DB/seed/Agent_seed.sql)");
            }
            string want = active ? "Active" : "Inactive";
            if (bot.BotStatus != want)
            {
                currentActor.RequireActor();
                bot.BotStatus = want; // nhật ký thay đổi (AuditChange) ghi tự động khi lưu
                await db.SaveChangesAsync();
            }
            return Result<AgentSummaryModel>.Success((await SummariesAsync()).First(s => s.Code == info.Code), active ? $"Đã bật agent {info.Code}" : $"Đã tắt agent {info.Code} – ToolGateway từ chối ngay từ lời gọi sau");
        }

        public async Task<IResult<AgentActivityPageModel>> GetActivity(AgentActivityRequest request)
        {
            var db = unitOfWork.DbContext;
            Guid? channel = await ChannelAsync();
            if (channel == null)
            {
                return Result<AgentActivityPageModel>.Success(new AgentActivityPageModel(), "Chưa có kênh HERMES");
            }
            DateTime from = (request.From ?? clock.Today.AddDays(-6)).Date, to = (request.To ?? clock.Today).Date.AddDays(1);
            var q = db.IntegrationLog.AsNoTracking().Where(l => l.IntegrationChannelId == channel && l.DirectionCode == "IN" && !l.IsDeleted && l.RequestAt >= from && l.RequestAt < to);
            if (!string.IsNullOrWhiteSpace(request.Agent))
            {
                string a = "AGENT:" + request.Agent.Trim().ToUpperInvariant();
                q = q.Where(l => l.ErrorCode != null && l.ErrorCode.EndsWith(a));
            }
            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                q = q.Where(l => l.LogStatus == request.Status);
            }
            if (!string.IsNullOrWhiteSpace(request.Tool))
            {
                q = q.Where(l => l.OperationCode == request.Tool);
            }
            var counts = await q.GroupBy(l => l.LogStatus).Select(g => new { g.Key, N = g.Count() }).ToListAsync();
            int size = Math.Clamp(request.PageSize, 1, 200), page = Math.Max(1, request.Page);
            var rows = await q.OrderByDescending(l => l.RequestAt).Skip((page - 1) * size).Take(size)
                .Select(l => new { l.Id, l.RequestAt, l.ErrorCode, l.OperationCode, l.LogStatus, l.HttpStatus, l.ErrorMessage, l.CorrelationId }).ToListAsync();
            var list = rows.Select(l =>
            {
                var m = new AgentActivityModel { Id = l.Id, At = l.RequestAt, Agent = CodeOf(l.ErrorCode), Tool = l.OperationCode, Status = l.LogStatus, HttpStatus = l.HttpStatus, CorrelationId = l.CorrelationId };
                try
                {
                    using var doc = JsonDocument.Parse(l.ErrorMessage ?? "{}");
                    var r = doc.RootElement;
                    m.Ms = r.TryGetProperty("ms", out var ms) && ms.TryGetInt64(out long v) ? v : null;
                    m.Rows = r.TryGetProperty("rows", out var rw) && rw.TryGetInt32(out int n) ? n : null;
                    m.AskingUserId = r.TryGetProperty("user", out var u) && u.ValueKind == JsonValueKind.String && Guid.TryParse(u.GetString(), out var g) ? g : null;
                    m.OnBehalfOf = r.TryGetProperty("onBehalfOf", out var ob) && ob.ValueKind == JsonValueKind.String ? ob.GetString() : null;
                    m.Error = r.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null;
                }
                catch (JsonException)
                {
                    m.Error = l.ErrorMessage;
                }
                return m;
            }).ToList();
            var users = list.Where(x => x.AskingUserId != null).Select(x => x.AskingUserId!.Value).Distinct().ToList();
            var names = users.Count == 0 ? [] : await db.User.AsNoTracking().Where(u => users.Contains(u.Id)).Select(u => new { u.Id, u.UserName }).ToDictionaryAsync(u => u.Id, u => u.UserName);
            list.ForEach(x => x.AskingUserName = x.AskingUserId is { } id && names.TryGetValue(id, out var n) ? n : null);
            return Result<AgentActivityPageModel>.Success(new AgentActivityPageModel
            {
                Total = counts.Sum(c => c.N), Ok = counts.Where(c => c.Key == "Succeeded").Sum(c => c.N), Denied = counts.Where(c => c.Key == "Denied").Sum(c => c.N),
                Failed = counts.Where(c => c.Key == "Failed").Sum(c => c.N), ListRow = list,
            }, "OK");
        }

        public async Task<IResults<AgentScheduleModel>> GetSchedules()
        {
            var items = ReadSchedules();
            if (items.Count == 0)
            {
                return Results<AgentScheduleModel>.Success([], "Chưa có app-delicare-agents/schedules.json (Agent:SchedulesFile)");
            }
            var summaries = await SummariesAsync();
            var db = unitOfWork.DbContext;
            Guid? channel = await ChannelAsync();
            DateTime since = clock.Today.AddDays(-7);
            var runs = channel == null ? [] : await db.IntegrationLog.AsNoTracking()
                .Where(l => l.IntegrationChannelId == channel && l.DirectionCode == "IN" && l.CorrelationId != null && l.CorrelationId.StartsWith("sched:") && l.RequestAt >= since)
                .Select(l => new { l.CorrelationId, l.RequestAt, l.LogStatus }).ToListAsync();
            foreach (var s in items)
            {
                var a = summaries.FirstOrDefault(x => x.Code == s.Agent);
                s.AgentName = a?.Name;
                s.AgentActive = a?.Active == true;
                var last = runs.Where(r => r.CorrelationId!.StartsWith("sched:" + s.Id + ":", StringComparison.Ordinal)).OrderByDescending(r => r.RequestAt).FirstOrDefault();
                s.LastRunAt = last?.RequestAt;
                s.LastRunStatus = last?.LogStatus;
            }
            return Results<AgentScheduleModel>.Success(items, "OK");
        }

        /// <summary>AI-11: đường dẫn tệp lịch – <c>Agent:SchedulesFile</c> tuyệt đối, hoặc tương đối tìm từ ContentRoot lên tới 5 cấp. Không thấy → null.</summary>
        public static string? SchedulesPath(IConfiguration configuration, string contentRoot)
        {
            string rel = configuration["Agent:SchedulesFile"] is { Length: > 0 } v ? v : "app-delicare-agents/schedules.json";
            if (Path.IsPathRooted(rel))
            {
                return rel;
            }
            var dir = new DirectoryInfo(contentRoot);
            for (int i = 0; i < 6 && dir != null; i++, dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, rel);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            return null;
        }

        /// <summary>Đọc <c>Agent:SchedulesFile</c> (mặc định app-delicare-agents/schedules.json – tìm từ ContentRoot lên tới 5 cấp; Docker: gắn thư mục app-delicare-agents).</summary>
        private List<AgentScheduleModel> ReadSchedules()
        {
            string? file = SchedulesPath(configuration, env.ContentRootPath);
            if (file == null || !File.Exists(file))
            {
                return [];
            }
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                return [.. doc.RootElement.GetProperty("items").EnumerateArray().Select(e => new AgentScheduleModel
                {
                    Id = e.GetProperty("id").GetString() ?? string.Empty, Agent = (e.GetProperty("agent").GetString() ?? string.Empty).ToUpperInvariant(),
                    Cron = e.TryGetProperty("cron", out var c) ? c.GetString() : null, At = e.TryGetProperty("at", out var at) ? at.GetString() : null,
                    Title = e.TryGetProperty("title", out var t) ? t.GetString() : null,
                    ListTool = e.TryGetProperty("tools", out var tl) && tl.ValueKind == JsonValueKind.Array ? [.. tl.EnumerateArray().Select(x => x.GetString() ?? string.Empty)] : [],
                    CoreJob = e.TryGetProperty("coreJob", out var cj) && cj.ValueKind == JsonValueKind.String ? cj.GetString() : null,
                    Wave = e.TryGetProperty("wave", out var w) && w.TryGetInt32(out int wi) ? wi : 1,
                    Enabled = !e.TryGetProperty("enabled", out var en) || en.ValueKind != JsonValueKind.False,
                })];
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
            {
                Serilog.Log.Warning(ex, "[AgentAdmin] app-delicare-agents/schedules.json sai định dạng");
                return [];
            }
        }
    }
}
