using System.Globalization;
using System.Text.Json;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Option;
using AppDelicare.Services.Implement.Messaging;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AppDelicare.Services.Implement.Agent
{
    // ======================================================================= Hermes báo chi phí

    /// <summary>
    /// AI-13 (D-133): Hermes báo token / chi phí MỖI LƯỢT bằng công cụ <c>bao_chi_phi</c> → 1 dòng IntegrationLog (DirectionCode <c>COST</c>, OperationCode <c>agent_cost</c>,
    /// IdempotencyKey <c>COST:&lt;agent&gt;:&lt;lượt&gt;</c> – báo lại cùng lượt không cộng 2 lần; chi phí quy VND theo <c>Agent:UsdToVnd</c>). Không bảng mới (GAP-AI-LOG tạm).
    /// Sau mỗi lần ghi: tổng tháng ≥ <c>Agent:CostAlertPct</c> % (và 100 %) ngân sách <c>Agent:MonthlyBudget</c> → báo CEO / FIN-IT (người có quyền Sửa Trung tâm agent) 1 lần / tháng / ngưỡng.
    /// </summary>
    public static partial class AgentToolRegistry
    {
        private static IEnumerable<AgentToolDef> CostTools() =>
        [
            new AgentToolDef
            {
                Name = "bao_chi_phi", ScreenId = null, Agents = ["VIVI", "SALES", "PLAN", "PROC", "WH", "KITCHEN", "QA", "LOG", "PKG", "HR", "ACC", "CSKH", "KNOW"],
                Description = "Hermes gọi SAU MỖI LƯỢT trả lời để báo chi phí mô hình: mo_hinh, token_vao, token_ra, chi_phi (số), don_vi (USD / VND, mặc định USD), luot (mã lượt – báo lại cùng lượt không cộng 2 lần).",
                InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"mo_hinh\":{\"type\":\"string\"},\"token_vao\":{\"type\":\"integer\"},\"token_ra\":{\"type\":\"integer\"},\"chi_phi\":{\"type\":\"number\"},\"don_vi\":{\"type\":\"string\",\"enum\":[\"USD\",\"VND\"]},\"luot\":{\"type\":\"string\"}},\"required\":[\"chi_phi\"]}",
                RunAsync = async (ctx, a) => await Svc<IAgentCostService>(ctx).RecordAsync(ctx.AgentCode, ctx.CorrelationId, a),
            },
        ];
    }

    public sealed class AgentCostAgentModel
    {
        public string Agent { get; set; } = string.Empty;
        public decimal CostVnd { get; set; }
        public long TokensIn { get; set; }
        public long TokensOut { get; set; }
        public int Turns { get; set; }
    }

    public sealed class AgentCostDayModel
    {
        public DateTime Date { get; set; }
        public decimal CostVnd { get; set; }
    }

    public sealed class AgentCostModel
    {
        public string Month { get; set; } = string.Empty;
        public decimal? Budget { get; set; }
        public decimal TotalVnd { get; set; }
        public decimal? Pct { get; set; }
        public decimal AlertPct { get; set; }
        public long TokensIn { get; set; }
        public long TokensOut { get; set; }
        public int Turns { get; set; }
        public decimal TodayVnd { get; set; }
        /// <summary>Ước tính cả tháng theo bình quân ngày đã qua.</summary>
        public decimal ForecastVnd { get; set; }
        /// <summary>AI-21 (D-138 g): tháng trước (yyyy-MM), tổng tháng trước, bình quân / lượt, số ngày của tháng, ngày đang tính (mốc thời gian trên thanh ngân sách).</summary>
        public string PrevMonth { get; set; } = string.Empty;
        public decimal PrevTotalVnd { get; set; }
        public decimal? PerTurnVnd { get; set; }
        public int DaysInMonth { get; set; }
        public int DaysPassed { get; set; }
        /// <summary>AI-25.2 (D-143): lượt theo nguồn trong tháng – Hỏi nhanh (AGENT_QUICK, 0 token) · Chat AI · Theo lịch (CorrelationId <c>sched:</c>).</summary>
        public int QuickRuns { get; set; }
        public int ChatTurns { get; set; }
        public decimal ChatVnd { get; set; }
        public int ScheduleTurns { get; set; }
        public decimal ScheduleVnd { get; set; }
        /// <summary>Ước tiết kiệm = số lượt Hỏi nhanh × chi phí trung bình 1 lượt Chat AI (chưa có lượt Chat AI → trung bình mọi lượt; chưa có gì → null).</summary>
        public decimal? SavedVnd { get; set; }
        public List<AgentCostAgentModel> ListAgent { get; set; } = [];
        public List<AgentCostDayModel> ListDay { get; set; } = [];
    }

    public interface IAgentCostService
    {
        Task<object> RecordAsync(string agent, string? correlationId, JsonElement args);
        Task<IResult<AgentCostModel>> GetCost(string? month);
    }

    public sealed class AgentCostService(IUnitOfWork unitOfWork, IClock clock, IOptionsMonitor<AgentOptions> options, INotificationService notifications) : IAgentCostService
    {
        public const string Operation = "agent_cost";
        public const string Direction = "COST";

        private sealed record CostRow(string Agent, string? Model, long In, long Out, decimal Vnd)
        {
            public bool Sched { get; init; }
        }

        public async Task<object> RecordAsync(string agent, string? correlationId, JsonElement a)
        {
            var o = options.CurrentValue;
            decimal raw = a.ValueKind == JsonValueKind.Object && a.TryGetProperty("chi_phi", out var c) && c.TryGetDecimal(out var cv) && cv >= 0 ? cv : throw new AgentToolException("chi_phi phải là số ≥ 0");
            string unit = (AgentToolRegistry.Str(a, "don_vi") ?? "USD").Trim().ToUpperInvariant();
            if (unit is not ("USD" or "VND"))
            {
                throw new AgentToolException("don_vi phải là USD hoặc VND");
            }
            decimal vnd = Math.Round(unit == "USD" ? raw * o.UsdToVnd : raw, 0);
            long tin = AgentToolRegistry.Int(a, "token_vao") ?? 0, tout = AgentToolRegistry.Int(a, "token_ra") ?? 0;
            string turn = AgentToolRegistry.Str(a, "luot") ?? correlationId ?? Guid.NewGuid().ToString("N");
            string key = $"COST:{agent}:{turn}";
            var db = unitOfWork.DbContext;
            if (key.Length <= 100 && await db.IntegrationLog.AsNoTracking().AnyAsync(l => l.IdempotencyKey == key && l.OperationCode == Operation && !l.IsDeleted))
            {
                return new { daGhi = false, lyDo = "Lượt này đã báo chi phí" };
            }
            DateTime now = clock.Now;
            db.IntegrationLog.Add(new IntegrationLog
            {
                Id = Guid.NewGuid(), DirectionCode = Direction, OperationCode = Operation, CorrelationId = Cut(correlationId, 100), IdempotencyKey = Cut(key, 100),
                RequestAt = now, ResponseAt = now, HttpStatus = 200, LogStatus = "Succeeded", ErrorCode = Cut("COST:" + agent, 100),
                ErrorMessage = JsonSerializer.Serialize(new CostRow(agent, Cut(AgentToolRegistry.Str(a, "mo_hinh"), 60), tin, tout, vnd)), CreatedDate = now, IsDeleted = false,
            });
            await db.SaveChangesAsync();
            await AlertAsync(now);
            return new { daGhi = true, chiPhiVnd = vnd };
        }

        private static string? Cut(string? s, int n) => s == null ? null : s.Length <= n ? s : s[..n];

        private async Task<List<(DateTime At, CostRow Row)>> RowsAsync(DateTime from, DateTime to)
        {
            var rows = await unitOfWork.DbContext.IntegrationLog.AsNoTracking().Where(l => !l.IsDeleted && l.OperationCode == Operation && l.RequestAt >= from && l.RequestAt < to)
                .Select(l => new { l.RequestAt, l.ErrorMessage, l.CorrelationId }).ToListAsync();
            var list = new List<(DateTime, CostRow)>();
            foreach (var r in rows)
            {
                try
                {
                    if (JsonSerializer.Deserialize<CostRow>(r.ErrorMessage ?? "{}") is { } c)
                    {
                        list.Add((r.RequestAt, c with { Sched = r.CorrelationId != null && r.CorrelationId.StartsWith("sched:", StringComparison.Ordinal) }));
                    }
                }
                catch (JsonException)
                {
                    // dòng hỏng – bỏ qua
                }
            }
            return list;
        }

        public async Task<IResult<AgentCostModel>> GetCost(string? month)
        {
            var o = options.CurrentValue;
            DateTime m0 = DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var mm) ? mm : new DateTime(clock.Today.Year, clock.Today.Month, 1);
            DateTime m1 = m0.AddMonths(1);
            var rows = await RowsAsync(m0, m1);
            decimal total = rows.Sum(r => r.Row.Vnd);
            int daysPassed = clock.Today >= m1 ? (m1 - m0).Days : Math.Max(1, (clock.Today - m0).Days + 1);
            var m = new AgentCostModel
            {
                Month = m0.ToString("yyyy-MM"), Budget = o.MonthlyBudget is > 0 ? o.MonthlyBudget : null, TotalVnd = total, AlertPct = o.CostAlertPct,
                TokensIn = rows.Sum(r => r.Row.In), TokensOut = rows.Sum(r => r.Row.Out), Turns = rows.Count, TodayVnd = rows.Where(r => r.At.Date == clock.Today).Sum(r => r.Row.Vnd),
                ForecastVnd = clock.Today >= m1 ? total : Math.Round(total / daysPassed * (m1 - m0).Days, 0),
                ListAgent = [.. rows.GroupBy(r => r.Row.Agent).Select(g => new AgentCostAgentModel { Agent = g.Key, CostVnd = g.Sum(x => x.Row.Vnd), TokensIn = g.Sum(x => x.Row.In), TokensOut = g.Sum(x => x.Row.Out), Turns = g.Count() })
                    .OrderByDescending(x => x.CostVnd)],
                ListDay = [.. Enumerable.Range(0, (m1 - m0).Days).Select(i => m0.AddDays(i)).Select(d => new AgentCostDayModel { Date = d, CostVnd = rows.Where(r => r.At.Date == d).Sum(r => r.Row.Vnd) })],
            };
            m.Pct = m.Budget is { } b ? Math.Round(total / b * 100m, 1) : null;
            var prev = await RowsAsync(m0.AddMonths(-1), m0);
            m.PrevMonth = m0.AddMonths(-1).ToString("yyyy-MM");
            m.PrevTotalVnd = prev.Sum(r => r.Row.Vnd);
            m.PerTurnVnd = rows.Count > 0 ? Math.Round(total / rows.Count, 0) : null;
            m.DaysInMonth = (m1 - m0).Days;
            m.DaysPassed = clock.Today < m0 ? 0 : daysPassed;
            m.QuickRuns = await unitOfWork.DbContext.IntegrationLog.AsNoTracking().CountAsync(l => !l.IsDeleted && l.ErrorCode != null && l.ErrorCode.StartsWith(AgentQuickService.LogPrefix) && l.RequestAt >= m0 && l.RequestAt < m1);
            var chat = rows.Where(r => !r.Row.Sched).ToList();
            m.ChatTurns = chat.Count;
            m.ChatVnd = chat.Sum(r => r.Row.Vnd);
            m.ScheduleTurns = rows.Count - chat.Count;
            m.ScheduleVnd = total - m.ChatVnd;
            decimal? per = chat.Count > 0 ? m.ChatVnd / chat.Count : m.PerTurnVnd;
            m.SavedVnd = per is { } pv ? Math.Round(pv * m.QuickRuns, 0) : null;
            return Result<AgentCostModel>.Success(m, ConstantHelper.Message.GetAllSuccessfully);
        }

        /// <summary>Ngưỡng CostAlertPct và 100 % ngân sách tháng → 1 tin / ngưỡng / tháng cho người có quyền Sửa Trung tâm agent (CEO, COO, FIN-IT).</summary>
        private async Task AlertAsync(DateTime now)
        {
            var o = options.CurrentValue;
            if (o.MonthlyBudget is not > 0)
            {
                return;
            }
            var cost = (await GetCost(now.ToString("yyyy-MM"))).Data!;
            foreach (decimal level in new[] { o.CostAlertPct, 100m }.Distinct().OrderBy(x => x))
            {
                if (cost.Pct < level)
                {
                    continue;
                }
                await BusinessNotifier.ToMenusAsync(notifications, unitOfWork.DbContext, NotificationCatalog.AgentCostAlert, ["#/ai-agents"], new
                {
                    title = $"Chi phí Trợ lý AI tháng {now:MM/yyyy} đã {cost.Pct:0.#}% ngân sách",
                    detail = $"{cost.TotalVnd:#,0} / {cost.Budget:#,0} đ · ước cả tháng {cost.ForecastVnd:#,0} đ",
                    lines = cost.ListAgent.Take(5).Select(x => $"🤖 {x.Agent}: {x.CostVnd:#,0} đ ({x.Turns} lượt)").ToList(),
                }, $"{NotificationCatalog.AgentCostAlert}:{now:yyyyMM}:{level:0}", requireAdd: true, botCode: MessagingCode.BotDieuHanh);
            }
        }
    }
}
