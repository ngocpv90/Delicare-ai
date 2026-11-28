using System.Text.Json;
using AppDelicare.Messaging.Callback;
using AppDelicare.Messaging.Channels;
using AppDelicare.Messaging.Conversations;
using AppDelicare.Messaging.Core;
using AppDelicare.Messaging.Options;
using AppDelicare.Messaging.Presentation;
using Microsoft.Extensions.Options;

namespace AppDelicare.Messaging.Audiences.Internal.Chung
{
    /// <summary>
    /// AI-25.3 (D-143): 📂 Chức năng › 💬 Hỏi nhanh trên bot Vận hành / Điều hành – KHÔNG qua AI. Danh sách câu = <c>Agent/GetQuick?kenh=VH|DH</c> bằng JWT của chính người dùng
    /// (api lọc theo quyền Xem màn của công cụ), nhóm theo agent; bấm câu → <c>Agent/RunQuick</c> (câu có ô nhập: hỏi 1 bước bằng ForceReply) → tin theo mẫu
    /// (tiêu đề + tóm tắt + tối đa <see cref="MaxRows"/> dòng + nút 🌐 Mở trên web khi có <c>Messaging:WebUrl</c>). Câu hỏi tự do vẫn ở bot Trợ lý (Hermes).
    /// Callback <c>qk:ag:&lt;agent&gt;</c> (danh sách câu của agent) · <c>qk:q:&lt;id&gt;</c> (chạy câu) · <c>qk:root</c> (danh sách agent).
    /// </summary>
    public sealed class HoiNhanhActions(IDelicareApiClient web, UserTokenCache tokens, IChannelSender sender, ReplyContextStore replies, IOptionsMonitor<MessagingHostOptions> options)
        : WebActions(web, tokens, sender, replies), IBotActionProvider
    {
        public const string Kind = "qk";
        public const string Code = "hoi_nhanh";
        public const string Module = "HOINHANH";
        public const string ReplyInput = "quick-input";
        public const int MaxRows = 10;

        private sealed record Item(string Id, string Agent, string AgentName, string Hoi, Dictionary<string, string> Nhap);

        public IEnumerable<BotAction> GetActions() =>
        [
            new BotAction
            {
                Code = Code, Module = Module, Label = "💬 Hỏi nhanh", Command = "hoinhanh", CommandDescription = "Hỏi nhanh số liệu (không dùng AI)", Bots = ["VH", "DH"], Order = 1,
                Visible = i => i.HasWebAccount, Handler = (ctx, ct) => ListAsync(ctx, null, ct),
            },
        ];

        private static string Channel(BotActionContext ctx) => string.Equals(ctx.Bot, "DH", StringComparison.OrdinalIgnoreCase) ? "DH" : "VH";

        private async Task<List<Item>?> LoadAsync(BotActionContext ctx, string token, CancellationToken ct)
        {
            var r = await Web.GetAsUserAsync<JsonElement>("api/Agent/GetQuick?kenh=" + Channel(ctx), token, ct);
            if (!r.IsOk)
            {
                await SayAsync(ctx, Problem(r), ct);
                return null;
            }
            return [.. (r.ListData ?? []).Where(x => x.ValueKind == JsonValueKind.Object).Select(x => new Item(
                S(x, "id") ?? string.Empty, S(x, "agent") ?? string.Empty, S(x, "agentName") ?? S(x, "agent") ?? string.Empty, S(x, "hoi") ?? string.Empty,
                x.TryGetProperty("nhap", out var n) && n.ValueKind == JsonValueKind.Object ? n.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString() ?? p.Name) : []))
                .Where(x => x.Id.Length > 0)];
        }

        /// <summary>Danh sách: nhiều agent → chọn agent; 1 agent (hoặc đã chọn) → các câu của agent đó.</summary>
        private async Task ListAsync(BotActionContext ctx, string? agent, CancellationToken ct)
        {
            if (await TokenAsync(ctx, ct) is not { } token || await LoadAsync(ctx, token, ct) is not { } items)
            {
                return;
            }
            if (items.Count == 0)
            {
                await ShowAsync(ctx, NeutralMessage.Text("Chưa có câu hỏi nhanh nào trong quyền của bạn."), ct);
                return;
            }
            var agents = items.Select(x => (x.Agent, x.AgentName)).Distinct().ToList();
            if (agent == null && agents.Count > 1)
            {
                await ShowAsync(ctx, new NeutralMessage
                {
                    Title = "💬 Hỏi nhanh",
                    Lines = ["Số liệu lấy ngay theo mẫu, không dùng AI. Chọn mảng:"],
                    Buttons = [.. agents.Select(a => new NeutralButton($"{a.AgentName} ({items.Count(x => x.Agent == a.Agent)})", CallbackData.Build(Kind, "ag", a.Agent)))],
                }, ct);
                return;
            }
            string code = agent ?? agents[0].Agent;
            var mine = items.Where(x => x.Agent == code).ToList();
            var m = new NeutralMessage
            {
                Title = "💬 Hỏi nhanh · " + (mine.FirstOrDefault()?.AgentName ?? code),
                Lines = mine.Count == 0 ? ["Không có câu nào trong quyền của bạn."] : ["Bấm câu để xem số liệu (không dùng AI):"],
                Buttons = [.. mine.Select(x => new NeutralButton(x.Hoi.Length > 40 ? x.Hoi[..39] + "…" : x.Hoi, CallbackData.Build(Kind, "q", x.Id), Row: "q" + x.Id))],
            };
            if (agents.Count > 1)
            {
                m.Buttons.Add(new NeutralButton("⬅️ Chọn mảng khác", CallbackData.Build(Kind, "root"), Row: "back"));
            }
            await ShowAsync(ctx, m, ct);
        }

        public async Task HandleAsync(BotActionContext ctx, CallbackData data, CancellationToken ct)
        {
            switch (data.Action)
            {
                case "root":
                    await ListAsync(ctx, null, ct);
                    return;
                case "ag":
                    await ListAsync(ctx, data.Id, ct);
                    return;
                case "q" when data.Id != null:
                    if (await TokenAsync(ctx, ct) is not { } token || await LoadAsync(ctx, token, ct) is not { } items)
                    {
                        return;
                    }
                    var q = items.FirstOrDefault(x => x.Id == data.Id);
                    if (q == null)
                    {
                        await SayAsync(ctx, "Câu hỏi không còn trong quyền của bạn.", ct);
                        return;
                    }
                    if (q.Nhap.Count > 0)
                    {
                        var (key, label) = q.Nhap.First();
                        await AskAsync(ctx, $"💬 {q.Hoi}\nGõ {label}:", ReplyInput, q.Id + "|" + key, ct);
                        return;
                    }
                    await RunAsync(ctx, token, q.Id, [], ct);
                    return;
                default:
                    await SayAsync(ctx, "Nút đã hết hạn.", ct);
                    return;
            }
        }

        /// <summary>Trả lời ô nhập (số lô, từ khóa…) → chạy câu.</summary>
        public async Task ReplyAsync(BotActionContext ctx, PendingReply pending, CancellationToken ct)
        {
            string text = (ctx.Event.Text ?? string.Empty).Trim();
            string[] parts = pending.Key.Split('|', 2);
            if (text.Length == 0 || parts.Length < 2)
            {
                await SayAsync(ctx, "Chưa nhận được nội dung – bấm lại câu hỏi.", ct);
                return;
            }
            if (await TokenAsync(ctx, ct) is not { } token)
            {
                return;
            }
            await RunAsync(ctx, token, parts[0], new Dictionary<string, string> { [parts[1]] = text.Length > 100 ? text[..100] : text }, ct);
        }

        private async Task RunAsync(BotActionContext ctx, string token, string id, Dictionary<string, string> nhap, CancellationToken ct)
        {
            var r = await Web.PostJsonAsUserAsync<JsonElement>("api/Agent/RunQuick?kenh=" + Channel(ctx), new { id, nhap }, token, ct);
            if (!r.IsOk || r.Data.ValueKind != JsonValueKind.Object)
            {
                await SayAsync(ctx, Problem(r), ct);
                return;
            }
            await SendAsync(ctx, Card(r.Data, options.CurrentValue.WebUrl), ct);
        }

        /// <summary>Tin kết quả: tiêu đề · tóm tắt · ≤ 10 dòng "cột: giá trị" gọn · "… còn N dòng" · 🌐 Mở trên web · ⬅️ Câu khác.</summary>
        public static NeutralMessage Card(JsonElement d, string? webUrl)
        {
            var cot = Arr(d, "cot").Select(x => x.GetString() ?? string.Empty).ToList();
            var dong = Arr(d, "dong").Select(r => r.ValueKind == JsonValueKind.Array ? r.EnumerateArray().Select(c => c.GetString() ?? string.Empty).ToList() : []).ToList();
            int total = d.TryGetProperty("tongDong", out var t) && t.TryGetInt32(out int n) ? n : dong.Count;
            var lines = new List<string>();
            if (S(d, "tomTat") is { Length: > 0 } sum)
            {
                lines.Add(sum);
            }
            if (dong.Count == 0)
            {
                lines.Add(S(d, "rong") ?? "Không có dữ liệu.");
            }
            else if (S(d, "kieu") != "tom_tat")
            {
                foreach (var row in dong.Take(MaxRows))
                {
                    lines.Add("• " + string.Join(" · ", row.Where(c => c.Length > 0 && c != "–")));
                }
                if (total > Math.Min(MaxRows, dong.Count))
                {
                    lines.Add($"… còn {total - Math.Min(MaxRows, dong.Count)} dòng – xem đủ trên web.");
                }
            }
            var m = new NeutralMessage { Title = "💬 " + (S(d, "tieuDe") ?? S(d, "hoi") ?? "Hỏi nhanh"), Lines = lines };
            m.Lines.Add("— Hỏi nhanh · không dùng AI");
            if (!string.IsNullOrWhiteSpace(webUrl) && S(d, "moMan") is { Length: > 0 } link && Uri.TryCreate(webUrl.TrimEnd('/') + "/" + link, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps)
            {
                m.Buttons.Add(new NeutralButton("🌐 Mở trên web", Url: u.ToString(), Row: "web"));
            }
            if (S(d, "agent") is { Length: > 0 } agent)
            {
                m.Buttons.Add(new NeutralButton("⬅️ Câu khác", CallbackData.Build(Kind, "ag", agent), Row: "web"));
            }
            return m;
        }

        private static List<JsonElement> Arr(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Array ? [.. v.EnumerateArray()] : [];

        private static string? S(JsonElement e, string k) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }
}
