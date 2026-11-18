using System.Globalization;
using System.Text.Json;
using AppDelicare.Messaging.Callback;
using AppDelicare.Messaging.Channels;
using AppDelicare.Messaging.Conversations;
using AppDelicare.Messaging.Core;
using AppDelicare.Messaging.Presentation;

namespace AppDelicare.Messaging.Audiences.Internal.VanHanh
{
    /// <summary>
    /// BOT-1.5c (BOT_TELEGRAM §3.2 – CSKH SAL-CS, điểm phát OPS-POS "📣 Ghi phản ánh tại điểm"): mục <c>#/log-complaint</c>.
    /// 📋 Phản ánh đang mở (<c>CustomerComplaint/GetList</c> – Mới / Đang xử lý, sắp hạn trước) · ⏱ Sắp quá hạn (<c>overdue=true</c> + còn ≤ 4 giờ)
    /// → thẻ (<c>GetDetail</c>): [🙋 Nhận xử lý] (<c>Assign</c> – về mình), [✅ Đã xử lý] (cách xử lý gõ tay – <c>Resolve</c>), [💬 Ghi chú] (<c>AddComment</c>) theo AllowedActions;
    /// 📣 Ghi phản ánh: gõ tên khách → chọn (<c>Customer/GetAll</c>) → mức độ (Cao / Trung bình / Thấp) → mô tả gõ tay → <c>Create</c> (SourceChannel TELEGRAM).
    /// Callback <c>cmp:&lt;hành động&gt;:&lt;Id N | số&gt;</c>; lựa chọn đang tạo lưu ở ConversationStore.
    /// </summary>
    public sealed class PhanAnhActions(IDelicareApiClient web, UserTokenCache tokens, IChannelSender sender, ReplyContextStore replies, ConversationStore store)
        : WebActions(web, tokens, sender, replies), IBotActionProvider
    {
        public const string Kind = CallbackData.Kinds.Complaint;
        public const string Menu = "#/log-complaint";
        public const string Flow = "complaint-new";
        public const string ReplyCustomer = "cmp-cust";
        public const string ReplyDescription = "cmp-desc";
        public const string ReplyResolve = "cmp-resolve";
        public const string ReplyComment = "cmp-note";
        private static readonly (string Code, string Label)[] Severities = [("High", "🔴 Cao"), ("Medium", "🟡 Trung bình"), ("Low", "⚪ Thấp")];

        public sealed class Draft
        {
            public List<(Guid Id, string Name)> Customers { get; set; } = [];
            public Guid? CustomerId { get; set; }
            public string? CustomerName { get; set; }
            public string? Severity { get; set; }
        }

        public IEnumerable<BotAction> GetActions() =>
        [
            new BotAction { Code = "cs_open", Module = "CSKH", Label = "📋 Phản ánh đang mở", Command = "phananh", CommandDescription = "Phản ánh đang mở", Bots = ["VH"], MenuMetaLink = Menu, Order = 1, Handler = (c, ct) => ListAsync(c, false, ct) },
            new BotAction { Code = "cs_due", Module = "CSKH", Label = "⏱ Sắp quá hạn", Command = "saphan", CommandDescription = "Phản ánh sắp / đã quá hạn", Bots = ["VH"], MenuMetaLink = Menu, Order = 2, Handler = (c, ct) => ListAsync(c, true, ct) },
            new BotAction { Code = "cs_new", Module = "CSKH", Label = "📣 Ghi phản ánh", Command = "ghiphananh", CommandDescription = "Ghi phản ánh của khách", Bots = ["VH"], MenuMetaLink = Menu, Order = 3, Handler = NewAsync },
            new BotAction { Code = "dp_complaint", Module = "DIEMPHAT", Label = "📣 Phản ánh tại điểm", Bots = ["VH"], MenuMetaLink = Menu, Order = 3, Handler = NewAsync },
        ];

        public async Task ListAsync(BotActionContext ctx, bool dueOnly, CancellationToken ct)
        {
            if (await TokenAsync(ctx, ct) is not { } token)
            {
                return;
            }
            var r = await Web.GetAsUserAsync<JsonElement>("api/CustomerComplaint/GetList?page=1&pageSize=100" + (dueOnly ? "&overdue=true" : ""), token, ct);
            if (!r.IsOk)
            {
                await SayAsync(ctx, Problem(r), ct);
                return;
            }
            var items = GiaoVanActions.Arr(r.Data, "listItem").Where(i => S(C(i), "status") is "Open" or "InProgress").ToList();
            if (dueOnly)
            {
                // Đã quá hạn (overdue=true trả cả quá hạn) + còn ≤ 4 giờ.
                var soon = await Web.GetAsUserAsync<JsonElement>("api/CustomerComplaint/GetList?page=1&pageSize=100", token, ct);
                items = [.. items, .. GiaoVanActions.Arr(soon.Data, "listItem").Where(i => S(C(i), "status") is "Open" or "InProgress" && Dec(i, "hoursToDue") is >= 0 and <= 4
                    && !items.Any(x => S(C(x), "id") == S(C(i), "id")))];
            }
            items = [.. items.OrderBy(i => Dec(i, "hoursToDue") ?? decimal.MaxValue)];
            await SendAsync(ctx, new NeutralMessage
            {
                Title = (dueOnly ? "⏱ Phản ánh sắp / đã quá hạn" : "📋 Phản ánh đang mở") + $" ({items.Count})",
                Lines = items.Count == 0 ? ["Không có phản ánh nào."] : [.. items.Take(12).Select(Line)],
                Buttons = [.. items.Take(10).Where(i => Guid.TryParse(S(C(i), "id"), out _)).Select(i => new NeutralButton($"{Icon(i)} {S(C(i), "complaintNo")}", CallbackData.Build(Kind, "v", Guid.Parse(S(C(i), "id")!))))],
            }, ct);
        }

        public async Task NewAsync(BotActionContext ctx, CancellationToken ct)
        {
            if (await TokenAsync(ctx, ct) is null)
            {
                return;
            }
            store.Set(ctx.Bot, ctx.Event.FromId, Flow, new Draft());
            await AskAsync(ctx, "📣 Ghi phản ánh – gõ tên (hoặc mã) khách hàng:", ReplyCustomer, "new", ct);
        }

        public async Task HandleAsync(BotActionContext ctx, CallbackData data, CancellationToken ct)
        {
            if (await TokenAsync(ctx, ct) is not { } token)
            {
                return;
            }
            Guid.TryParseExact(data.Id ?? string.Empty, "N", out Guid id);
            switch (data.Action)
            {
                case "v":
                    await CardAsync(ctx, id, token, null, ct);
                    break;
                case "as":
                    await PostAsync(ctx, id, token, "api/CustomerComplaint/Assign", [new("Id", id.ToString()), new("Comment", "Nhận xử lý (Telegram)")], ct);
                    break;
                case "rs":
                    await AskAsync(ctx, "Cách đã xử lý (gõ ngắn gọn):", ReplyResolve, id.ToString("N"), ct);
                    break;
                case "nt":
                    await AskAsync(ctx, "Ghi chú thêm:", ReplyComment, id.ToString("N"), ct);
                    break;
                case "c" or "sv":
                    await DraftStepAsync(ctx, data, ct);
                    break;
                default:
                    await SayAsync(ctx, "Nút đã hết hạn.", ct);
                    break;
            }
        }

        public async Task ReplyAsync(BotActionContext ctx, PendingReply pending, CancellationToken ct)
        {
            if (await TokenAsync(ctx, ct) is not { } token)
            {
                return;
            }
            string text = (ctx.Event.Text ?? string.Empty).Trim();
            if (text.Length < 2)
            {
                await AskAsync(ctx, "Quá ngắn – gõ lại:", pending.Flow, pending.Key, ct);
                return;
            }
            switch (pending.Flow)
            {
                case ReplyResolve when Guid.TryParseExact(pending.Key, "N", out var id):
                    await PostAsync(ctx, id, token, "api/CustomerComplaint/Resolve", [new("Id", id.ToString()), new("Comment", text)], ct);
                    break;
                case ReplyComment when Guid.TryParseExact(pending.Key, "N", out var id):
                    await PostAsync(ctx, id, token, "api/CustomerComplaint/AddComment", [new("Id", id.ToString()), new("Comment", text)], ct);
                    break;
                case ReplyCustomer:
                    await CustomerSearchAsync(ctx, token, text, ct);
                    break;
                case ReplyDescription:
                    await CreateAsync(ctx, token, text, ct);
                    break;
            }
        }

        private async Task CustomerSearchAsync(BotActionContext ctx, string token, string text, CancellationToken ct)
        {
            var d = store.Get<Draft>(ctx.Bot, ctx.Event.FromId, Flow);
            if (d == null)
            {
                await SayAsync(ctx, "Phiên đã hết hạn – gõ /ghiphananh để làm lại.", ct);
                return;
            }
            var r = await Web.GetAsUserAsync<JsonElement>("api/Customer/GetAll?page=1&pageSize=8&searchText=" + Uri.EscapeDataString(text), token, ct);
            d.Customers = [.. (r.ListData ?? []).Where(c => Guid.TryParse(S(c, "id"), out _)).Select(c => (Guid.Parse(S(c, "id")!), S(c, "name") ?? S(c, "code") ?? "Khách"))];
            store.Set(ctx.Bot, ctx.Event.FromId, Flow, d);
            if (d.Customers.Count == 0)
            {
                await AskAsync(ctx, r.IsOk ? "Không tìm thấy khách – gõ lại tên / mã khách:" : Problem(r), ReplyCustomer, "new", ct);
                return;
            }
            await SendAsync(ctx, new NeutralMessage
            {
                Title = "📣 Chọn khách hàng",
                Buttons = [.. d.Customers.Select((c, i) => new NeutralButton(c.Name, CallbackData.Build(Kind, "c", i.ToString(CultureInfo.InvariantCulture))))],
            }, ct);
        }

        private async Task DraftStepAsync(BotActionContext ctx, CallbackData data, CancellationToken ct)
        {
            var d = store.Get<Draft>(ctx.Bot, ctx.Event.FromId, Flow);
            int i = int.TryParse(data.Id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : -1;
            if (d == null || i < 0)
            {
                await SayAsync(ctx, "Phiên đã hết hạn – gõ /ghiphananh để làm lại.", ct);
                return;
            }
            if (data.Action == "c" && i < d.Customers.Count)
            {
                (d.CustomerId, d.CustomerName) = d.Customers[i];
                store.Set(ctx.Bot, ctx.Event.FromId, Flow, d);
                await ShowAsync(ctx, new NeutralMessage
                {
                    Title = "📣 " + d.CustomerName + " – mức độ",
                    Buttons = [.. Severities.Select((s, k) => new NeutralButton(s.Label, CallbackData.Build(Kind, "sv", k.ToString(CultureInfo.InvariantCulture))))],
                }, ct);
                return;
            }
            if (data.Action == "sv" && i < Severities.Length && d.CustomerId != null)
            {
                d.Severity = Severities[i].Code;
                store.Set(ctx.Bot, ctx.Event.FromId, Flow, d);
                await AskAsync(ctx, $"Nội dung phản ánh của {d.CustomerName} (gõ mô tả):", ReplyDescription, "new", ct);
                return;
            }
            await SayAsync(ctx, "Nút đã hết hạn.", ct);
        }

        private async Task CreateAsync(BotActionContext ctx, string token, string description, CancellationToken ct)
        {
            var d = store.Get<Draft>(ctx.Bot, ctx.Event.FromId, Flow);
            if (d?.CustomerId is not { } customer)
            {
                await SayAsync(ctx, "Phiên đã hết hạn – gõ /ghiphananh để làm lại.", ct);
                return;
            }
            var r = await Web.PostFormAsUserAsync<JsonElement>("api/CustomerComplaint/Create",
                [new("CustomerId", customer.ToString()), new("Severity", d.Severity ?? "Medium"), new("SourceChannel", "TELEGRAM"), new("Description", description)], token, ct);
            if (!r.IsOk)
            {
                await SayAsync(ctx, "⚠️ " + Problem(r), ct);
                return;
            }
            store.Clear(ctx.Bot, ctx.Event.FromId, Flow);
            await SendAsync(ctx, new NeutralMessage
            {
                Title = "✅ Đã ghi phản ánh " + S(C(r.Data), "complaintNo"),
                Lines = [$"{d.CustomerName} · {S(r.Data, "severityName")} · hạn xử lý {Time(S(C(r.Data), "slaDueAt"))}"],
            }, ct);
        }

        private async Task PostAsync(BotActionContext ctx, Guid id, string token, string path, List<KeyValuePair<string, string>> form, CancellationToken ct)
        {
            var r = await Web.PostFormAsUserAsync<JsonElement>(path, form, token, ct);
            await CardAsync(ctx, id, token, r.IsOk ? "✅ " + (r.ResponseMessage ?? "Đã ghi") : "⚠️ " + Problem(r), ct);
        }

        private async Task CardAsync(BotActionContext ctx, Guid id, string token, string? note, CancellationToken ct)
        {
            var r = await Web.GetAsUserAsync<JsonElement>("api/CustomerComplaint/GetDetail?id=" + id, token, ct);
            if (!r.IsOk || r.Data.ValueKind != JsonValueKind.Object)
            {
                await SayAsync(ctx, note ?? Problem(r), ct);
                return;
            }
            var d = r.Data;
            var c = C(d);
            var allowed = GiaoVanActions.Arr(d, "allowedActions").Select(x => x.ToString()).ToList();
            var m = new NeutralMessage
            {
                Title = $"{Icon(d)} {S(c, "complaintNo")} – {S(d, "statusName")}",
                Lines =
                [
                    $"{S(d, "customerName")} · {S(d, "severityName")}{(S(d, "orderNumber") is { } o ? " · đơn " + o : "")}",
                    S(c, "description") ?? string.Empty,
                    $"Phụ trách: {S(d, "assignedToName") ?? "chưa có"} · hạn {Time(S(c, "slaDueAt"))}{(d.TryGetProperty("isOverdue", out var ov) && ov.ValueKind == JsonValueKind.True ? " ⚠️ quá hạn" : "")}",
                ],
            };
            if (note != null)
            {
                m.Lines.Insert(0, note);
            }
            string n = id.ToString("N");
            if (allowed.Contains("assign"))
            {
                m.Buttons.Add(new NeutralButton("🙋 Nhận xử lý", CallbackData.Build(Kind, "as", n)));
            }
            if (allowed.Contains("resolve"))
            {
                m.Buttons.Add(new NeutralButton("✅ Đã xử lý", CallbackData.Build(Kind, "rs", n)));
            }
            if (allowed.Contains("addComment"))
            {
                m.Buttons.Add(new NeutralButton("💬 Ghi chú", CallbackData.Build(Kind, "nt", n)));
            }
            m.Buttons.Add(new NeutralButton("🔄 Làm mới", CallbackData.Build(Kind, "v", n)));
            await ShowAsync(ctx, m, ct);
        }

        private static string Line(JsonElement i)
        {
            decimal? h = Dec(i, "hoursToDue");
            string due = h is null ? "" : h < 0 ? " · ⚠️ quá hạn" : $" · còn {h:0.#} giờ";
            return $"{Icon(i)} {S(C(i), "complaintNo")} {S(i, "customerName")} · {S(i, "severityName")} · {S(i, "statusName")}{due}";
        }

        private static string Icon(JsonElement i) => S(C(i), "severity") switch { "High" => "🔴", "Low" => "⚪", _ => "🟡" };

        private static JsonElement C(JsonElement i) => i.ValueKind == JsonValueKind.Object && i.TryGetProperty("complaint", out var c) ? c : default;

        private static string? S(JsonElement e, string k) => BepActions.S(e, k);

        private static decimal? Dec(JsonElement e, string k) => decimal.TryParse(S(e, k), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;

        private static string Time(string? s) => DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t.ToString("HH:mm dd/MM") : "-";
    }
}
