using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Implement.Agent
{
    public sealed class QuickItemModel
    {
        public string Id { get; set; } = string.Empty;
        public string Agent { get; set; } = string.Empty;
        /// <summary>AI-25.3: tên ngắn của agent (nhóm câu trên bot).</summary>
        public string AgentName { get; set; } = string.Empty;
        public string Hoi { get; set; } = string.Empty;
        public List<string> Kenh { get; set; } = [];
        /// <summary>Ô nhập trước khi chạy: tham số → nhãn.</summary>
        public Dictionary<string, string> Nhap { get; set; } = [];
        public bool CoPhanTich { get; set; }
    }

    public sealed class QuickResultModel
    {
        public string Id { get; set; } = string.Empty;
        public string Agent { get; set; } = string.Empty;
        public string Hoi { get; set; } = string.Empty;
        public string Kieu { get; set; } = "bang";
        public string TieuDe { get; set; } = string.Empty;
        public string? TomTat { get; set; }
        /// <summary>Không có dòng nào → câu "rong" (tin tốt).</summary>
        public string? Rong { get; set; }
        public List<string> Cot { get; set; } = [];
        public List<List<string>> Dong { get; set; } = [];
        public int TongDong { get; set; }
        public string? MoMan { get; set; }
        public string? PhanTich { get; set; }
        public long Ms { get; set; }
    }

    public sealed class QuickRunRequest
    {
        public string? Id { get; set; }
        public Dictionary<string, string>? Nhap { get; set; }
    }

    /// <summary>
    /// AI-25.1 (D-143): câu hỏi nhanh – máy chủ chạy thẳng công cụ trong <see cref="AgentToolRegistry"/> theo <c>quick.yaml</c>, <b>KHÔNG gọi Hermes / mô hình AI (0 token)</b>.
    /// Quyền: câu hiện / chạy khi NGƯỜI HỎI có quyền Xem màn của công cụ (ScreenId); công cụ chạy dưới danh nghĩa người hỏi (không dùng quyền agent).
    /// Kết quả qua <see cref="AgentMask"/> (che dữ liệu cá nhân, cắt Agent:MaxRows) → lọc / tóm tắt / cột theo <see cref="QuickSpecs"/>. Mỗi lần chạy 1 dòng IntegrationLog
    /// <c>AGENT_QUICK:&lt;agent&gt;</c> (token 0) – tab Sử dụng &amp; chi phí đếm riêng Hỏi nhanh.
    /// </summary>
    public interface IAgentQuickService
    {
        Task<IResults<QuickItemModel>> GetQuick(string? agent, string? channel);
        Task<IResult<QuickResultModel>> RunQuick(QuickRunRequest request, string? channel = "web");
        /// <summary>AI-25.4: chạy 1 câu cho người dùng chỉ định (bản tin) – kiểm quyền người đó.</summary>
        Task<IResult<QuickResultModel>> RunQuickFor(Guid userId, string id, string channel);
        IReadOnlyList<QuickQuestion> All();
        IReadOnlyList<QuickBrief> Briefs();
    }

    public sealed class AgentQuickService(IUnitOfWork unitOfWork, ICurrentActor currentActor, IPermissionService permissions, IClock clock, IServiceProvider services,
        Microsoft.Extensions.Options.IOptionsMonitor<AppDelicare.Model.Option.AgentOptions> options) : IAgentQuickService
    {
        public const string LogPrefix = "AGENT_QUICK:";

        public IReadOnlyList<QuickQuestion> All() => QuickCatalog.Load(QuickCatalog.Root()).Questions;

        public IReadOnlyList<QuickBrief> Briefs() => QuickCatalog.Load(QuickCatalog.Root()).Briefs;

        private async Task<bool> CanAsync(Guid userId, QuickQuestion q)
        {
            var tool = AgentToolRegistry.Find(q.Tool);
            if (tool == null)
            {
                return false;
            }
            return tool.ScreenId == null || permissions.IsAllowed(await permissions.GetAsync(userId), tool.ScreenId, PermissionAction.View);
        }

        public async Task<IResults<QuickItemModel>> GetQuick(string? agent, string? channel)
        {
            var actor = currentActor.RequireActor();
            string ch = string.IsNullOrWhiteSpace(channel) ? "web" : channel.Trim();
            var list = new List<QuickItemModel>();
            foreach (var q in All().Where(q => (agent == null || string.Equals(q.Agent, agent, StringComparison.OrdinalIgnoreCase)) && q.Kenh.Contains(ch, StringComparer.OrdinalIgnoreCase)))
            {
                if (await CanAsync(actor.UserId, q))
                {
                    list.Add(new QuickItemModel { Id = q.Id, Agent = q.Agent, AgentName = AgentCenterService.ShortName.TryGetValue(q.Agent, out var n) ? n : q.Agent, Hoi = q.Hoi, Kenh = q.Kenh, Nhap = q.Nhap, CoPhanTich = !string.IsNullOrWhiteSpace(q.PhanTich) });
                }
            }
            return Results<QuickItemModel>.Success(list, "OK");
        }

        public async Task<IResult<QuickResultModel>> RunQuick(QuickRunRequest request, string? channel = "web")
        {
            var actor = currentActor.RequireActor();
            return await RunCore(actor.UserId, request.Id, request.Nhap ?? [], channel ?? "web");
        }

        public async Task<IResult<QuickResultModel>> RunQuickFor(Guid userId, string id, string channel)
        {
            using var scope = currentActor.BeginScope(ActorInfo.ForUser(userId));
            return await RunCore(userId, id, [], channel);
        }

        private async Task<IResult<QuickResultModel>> RunCore(Guid userId, string? id, Dictionary<string, string> input, string channel)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var q = All().FirstOrDefault(x => string.Equals(x.Id, id?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (q == null)
            {
                return Result<QuickResultModel>.Fail("Không có câu hỏi nhanh này");
            }
            var tool = AgentToolRegistry.Find(q.Tool);
            if (tool == null || !await CanAsync(userId, q))
            {
                return new Result<QuickResultModel> { ResponseCode = 403, ResponseMessage = tool == null ? "Công cụ của câu hỏi không còn" : "Anh/chị chưa có quyền xem màn của câu hỏi này" };
            }
            foreach (var k in q.Nhap.Keys.Where(k => string.IsNullOrWhiteSpace(input.GetValueOrDefault(k))))
            {
                return Result<QuickResultModel>.Fail($"Nhập «{q.Nhap[k]}»");
            }
            var args = BuildArgs(q, input, clock.Today);
            Guid agentUser = await AgentUserAsync(q.Agent) ?? userId;
            JsonNode? root;
            try
            {
                var ctx = new AgentToolContext { AgentCode = q.Agent, AgentUserId = agentUser, AskingUserId = userId, CorrelationId = "quick:" + q.Id, Services = services };
                var data = await tool.RunAsync(ctx, args);
                root = AgentMask.Apply(data, AgentToolRegistry.MaxRows(ctx), AgentToolRegistry.WebJson).Node;
            }
            catch (AgentToolException ex)
            {
                await LogAsync(q, userId, 400, 0, watch.ElapsedMilliseconds, channel);
                return Result<QuickResultModel>.Fail(ex.Message);
            }
            var res = QuickEngine.Render(q, root, args, clock.Today);
            res.Ms = watch.ElapsedMilliseconds;
            await LogAsync(q, userId, 200, res.TongDong, res.Ms, channel);
            return Result<QuickResultModel>.Success(res, "OK");
        }

        public static JsonElement BuildArgs(QuickQuestion q, Dictionary<string, string> input, DateTime today)
        {
            var o = new JsonObject();
            foreach (var (k, v) in q.Args.Concat(q.Nhap.Keys.Select(k => new KeyValuePair<string, string>(k, input.GetValueOrDefault(k) ?? string.Empty))))
            {
                string s = Regex.Replace(v.Trim(), @"^\{ngay\+(\d+)\}$", m => today.AddDays(int.Parse(m.Groups[1].Value)).ToString("yyyy-MM-dd")) switch
                {
                    "hom_nay" => today.ToString("yyyy-MM-dd"), "ngay_mai" => today.AddDays(1).ToString("yyyy-MM-dd"), "hom_qua" => today.AddDays(-1).ToString("yyyy-MM-dd"), var x => x,
                };
                o[k] = s is "true" or "false" ? JsonValue.Create(s == "true") : decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) && !s.StartsWith('0') ? JsonValue.Create(d) : JsonValue.Create(s);
            }
            return JsonSerializer.SerializeToElement(o);
        }

        private async Task<Guid?> AgentUserAsync(string code)
        {
            var db = unitOfWork.DbContext;
            return await (from x in db.UserExternalIdentity.AsNoTracking()
                          join c in db.IntegrationChannel.AsNoTracking() on x.IntegrationChannelId equals c.Id
                          where !x.IsDeleted && x.ExternalUserId == code && c.ProviderCode == "HERMES"
                          select x.UserId).FirstOrDefaultAsync();
        }

        private async Task LogAsync(QuickQuestion q, Guid userId, int status, int rows, long ms, string channel)
        {
            try
            {
                var db = unitOfWork.DbContext;
                Guid? hermes = await db.IntegrationChannel.AsNoTracking().Where(c => c.ProviderCode == "HERMES" && !c.IsDeleted).Select(c => (Guid?)c.Id).FirstOrDefaultAsync();
                db.IntegrationLog.Add(new IntegrationLog
                {
                    Id = Guid.NewGuid(), IntegrationChannelId = hermes, DirectionCode = "IN", OperationCode = q.Tool, CorrelationId = "quick:" + q.Id, RequestAt = clock.Now, ResponseAt = clock.Now, HttpStatus = status,
                    LogStatus = status == 200 ? "Succeeded" : "Failed", ErrorCode = LogPrefix + q.Agent,
                    ErrorMessage = JsonSerializer.Serialize(new { id = q.Id, user = userId, rows, ms, tokens = 0, kenh = channel }),
                    CreatedDate = clock.Now, CreatedBy = userId, IsDeleted = false,
                });
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "[AgentQuick] không ghi được IntegrationLog");
            }
        }
    }

    /// <summary>AI-25.1: dựng kết quả câu nhanh từ JSON công cụ theo <see cref="QuickSpecs"/> – lọc, sắp xếp, chỗ trống tóm tắt, cột, định dạng ô.</summary>
    public static class QuickEngine
    {
        private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");
        private static readonly Regex Ph = new(@"\{([a-z_]+)(?:\.([a-z_]+))?(?:=([^}]+))?\}", RegexOptions.Compiled);

        public static QuickResultModel Render(QuickQuestion q, JsonNode? root, JsonElement args, DateTime today)
        {
            var spec = QuickSpecs.Tools.GetValueOrDefault(q.Tool) ?? new QuickSpecs.Spec();
            var rows = spec.Rows(root, q.Id);
            if (QuickSpecs.Filters.TryGetValue(q.Id, out var f))
            {
                rows = [.. rows.Where(f)];
            }
            if (q.Id == "kit-hom-nay" && spec.Fields.TryGetValue("ngay", out var nd))
            {
                rows = [.. rows.Where(r => nd(r, root)?.ToString() == today.ToString("yyyy-MM-dd"))];
            }
            var cols = q.View.Cot.Where(c => spec.Cols.ContainsKey(c)).ToList();
            // sắp xếp "<nhãn cột> tăng / giảm dần"
            if (q.View.SapXep is { } sx && Regex.Match(sx, @"^(.+?)\s+(tăng|giảm) dần$") is { Success: true } sm && spec.Cols.TryGetValue(sm.Groups[1].Value.Trim(), out var key))
            {
                IComparable K(JsonObject r) => key(r, root) is var v && QuickSpecs.Num(v) is { } n ? n : (IComparable)(v?.ToString() ?? string.Empty);
                rows = sm.Groups[2].Value == "tăng" ? [.. rows.OrderBy(K)] : [.. rows.OrderByDescending(K)];
            }
            var argObj = JsonNode.Parse(args.GetRawText()) as JsonObject;
            string Fill(string? t) => t == null ? string.Empty : Ph.Replace(t, m => Value(m, spec, rows, root, argObj, today));
            return new QuickResultModel
            {
                Id = q.Id, Agent = q.Agent, Hoi = q.Hoi, Kieu = q.View.Kieu, TieuDe = Fill(q.View.TieuDe), TomTat = rows.Count == 0 && q.View.Rong != null ? null : Fill(q.View.TomTat),
                Rong = rows.Count == 0 ? Fill(q.View.Rong ?? "Không có dữ liệu.") : null, Cot = cols, TongDong = rows.Count,
                Dong = [.. rows.Select(r => cols.Select(c => Cell(spec.Cols[c](r, root))).ToList())], MoMan = q.MoMan, PhanTich = q.PhanTich,
            };
        }

        private static string Value(Match m, QuickSpecs.Spec spec, List<JsonObject> rows, JsonNode? root, JsonObject? args, DateTime today)
        {
            string a = m.Groups[1].Value, b = m.Groups[2].Value, eq = m.Groups[3].Value;
            if (a == "so_dong") return rows.Count.ToString("N0", Vi);
            if (a == "ngay") return today.ToString("dd/MM/yyyy");
            if (a == "args") return Cell(args?[b]);
            if (b.Length == 0)
            {
                return spec.Root.TryGetValue(a, out var rf) ? Cell(rf(root, rows)) : m.Value;
            }
            if (a == "dem")
            {
                if (!spec.Fields.TryGetValue(b, out var fd)) return m.Value;
                return rows.Count(r => Match(fd(r, root), eq)).ToString("N0", Vi);
            }
            if (spec.Root.TryGetValue(b, out var rootFn))
            {
                return Cell(rootFn(root, rows));
            }
            if (!spec.Fields.TryGetValue(b, out var field) && !spec.Cols.TryGetValue(b, out field))
            {
                return m.Value;
            }
            var vals = rows.Select(r => field(r, root)).ToList();
            var nums = vals.Select(QuickSpecs.Num).Where(x => x != null).Select(x => x!.Value).ToList();
            return a switch
            {
                "tong" => Cell(nums.Sum()),
                "tb" => nums.Count == 0 ? "–" : Cell(Math.Round(nums.Average(), 1)),
                "min" => vals.Where(v => v != null).Select(v => v!.ToString() ?? string.Empty).Where(s => s.Length > 0).OrderBy(s => s, StringComparer.Ordinal).Select(s => Cell(JsonValue.Create(s))).FirstOrDefault() ?? "–",
                "pct" => nums.Count == 0 ? "–" : Cell(Math.Round(nums.Average(), 0)),
                _ => m.Value,
            };
        }

        private static bool Match(object? v, string eq)
        {
            if (eq is "true" or "có") return v switch { bool b => b, JsonValue j when j.TryGetValue(out bool jb) => jb, JsonValue j => QuickSpecs.Num(j) is { } n && n != 0, _ => false };
            return QuickSpecs.Fold(v?.ToString()) == QuickSpecs.Fold(eq);
        }

        /// <summary>Ô hiển thị: số vi-VN, ngày dd/MM/yyyy (giờ HH:mm nếu có), đúng / sai = có / –.</summary>
        public static string Cell(object? v)
        {
            switch (v)
            {
                case null: return "–";
                case bool b: return b ? "có" : "–";
                case decimal d: return d == Math.Round(d) ? d.ToString("N0", Vi) : d.ToString("#,0.##", Vi);
                case int i: return i.ToString("N0", Vi);
                case string s: return Text(s);
                case JsonValue j:
                    if (j.TryGetValue(out bool jb)) return jb ? "có" : "–";
                    if (j.TryGetValue(out decimal jd)) return Cell(jd);
                    return j.TryGetValue(out string? js) ? Text(js) : j.ToJsonString();
                case JsonArray arr: return string.Join(", ", arr.Select(x => Cell(x)));
                default: return v.ToString() ?? "–";
            }
        }

        private static string Text(string s)
        {
            if (s.Length >= 10 && DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) && Regex.IsMatch(s, @"^\d{4}-\d{2}-\d{2}"))
            {
                return dt.TimeOfDay == TimeSpan.Zero ? dt.ToString("dd/MM/yyyy") : dt.ToString("dd/MM HH:mm");
            }
            return s.Length == 0 ? "–" : s;
        }
    }
}
