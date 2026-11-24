using System.Globalization;
using System.Text.Json;
using AppDelicare.Messaging.Audiences.Internal;
using AppDelicare.Messaging.Audiences.Internal.VanHanh;
using AppDelicare.Messaging.Callback;
using AppDelicare.Messaging.Channels;
using AppDelicare.Messaging.Core;
using AppDelicare.Messaging.Presentation;

namespace AppDelicare.Messaging.Conversations.Flows
{
    /// <summary>
    /// BOT-1.5c (BOT_TELEGRAM §3.1 "🔁 Đổi ca", DECISIONS p24): chọn ca của tôi (<c>ShiftAssignment/GetMine</c> 14 ngày, ca còn mở) → ca có thể đổi
    /// (<c>ShiftAssignment/GetSwapCandidates</c>): đổi chéo với ca của đồng nghiệp hoặc 🤝 nhường ca cho đồng nghiệp → lý do (ForceReply) → <c>ShiftSwapRequest/Create</c>
    /// (lý do "[Telegram] …"; đơn vào duyệt như web). Tài khoản web (JWT của chính mình); người chỉ có EmployeeId: làm trên web / nhờ quản lý (PROPOSED).
    /// Lựa chọn lưu ở ConversationStore (30′) – callback chỉ mang số thứ tự: <c>swp:m|x|g|p:&lt;số&gt;</c>.
    /// </summary>
    public sealed class ShiftSwapFlow(IDelicareApiClient web, UserTokenCache tokens, IChannelSender sender, ReplyContextStore replies, ConversationStore store, TimeProvider time, Experience.ReasonCatalog reasons)
        : WebActions(web, tokens, sender, replies)
    {
        public const string Kind = "swp";
        public const string Flow = "swap";
        public const string ReplyReason = "swap-reason";
        private static readonly string[] Open = ["Planned", "Confirmed"];

        public sealed class Option
        {
            public Guid? AssignmentId { get; set; }
            public Guid EmployeeId { get; set; }
            public string Label { get; set; } = string.Empty;
        }

        public sealed class State
        {
            public List<Option> Mine { get; set; } = [];
            public Option? Current { get; set; }
            public List<Option> Shifts { get; set; } = [];
            public List<Option> Colleagues { get; set; } = [];
            public Option? Chosen { get; set; }
        }

        public async Task StartAsync(BotActionContext ctx, CancellationToken ct)
        {
            if (await TokenAsync(ctx, ct) is not { } token)
            {
                return;
            }
            var r = await Web.GetAsUserAsync<JsonElement>("api/ShiftAssignment/GetMine?days=14", token, ct);
            if (!r.IsOk)
            {
                await SayAsync(ctx, Problem(r), ct);
                return;
            }
            DateTime today = VnTime.Today(time);
            var mine = (r.ListData ?? []).Where(a => Open.Contains(S(a, "assignmentStatus")) && Guid.TryParse(S(a, "assignmentId"), out _)
                    && DateTime.TryParse(S(a, "workDate"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) && d.Date >= today)
                .Take(10).Select(a => new Option { AssignmentId = Guid.Parse(S(a, "assignmentId")!), Label = ShiftLabel(a, null) }).ToList();
            store.Set(ctx.Bot, ctx.Event.FromId, Flow, new State { Mine = mine });
            await SendAsync(ctx, new NeutralMessage
            {
                Title = "🔁 Đổi ca – chọn ca của bạn",
                Lines = mine.Count == 0 ? ["Bạn không có ca nào còn mở trong 14 ngày tới."] : [],
                Buttons = [.. mine.Select((m, i) => new NeutralButton(m.Label, CallbackData.Build(Kind, "m", i.ToString(CultureInfo.InvariantCulture))))],
            }, ct);
        }

        public async Task HandleAsync(BotActionContext ctx, CallbackData data, CancellationToken ct)
        {
            var s = store.Get<State>(ctx.Bot, ctx.Event.FromId, Flow);
            if (s == null)
            {
                await SayAsync(ctx, "Phiên đổi ca đã hết hạn – gõ /doica để làm lại.", ct);
                return;
            }
            int i = int.TryParse(data.Id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : -1;
            switch (data.Action)
            {
                case "m" when i >= 0 && i < s.Mine.Count:
                    await CandidatesAsync(ctx, s, s.Mine[i], ct);
                    break;
                case "g":
                    await ShowAsync(ctx, new NeutralMessage
                    {
                        Title = "🤝 Nhường ca – chọn người nhận",
                        Lines = [s.Current?.Label ?? string.Empty, .. s.Colleagues.Count == 0 ? new[] { "Không có đồng nghiệp cùng đơn vị." } : []],
                        Buttons = [.. s.Colleagues.Take(12).Select((c, k) => new NeutralButton(c.Label, CallbackData.Build(Kind, "p", k.ToString(CultureInfo.InvariantCulture))))],
                    }, ct);
                    break;
                case "x" when i >= 0 && i < s.Shifts.Count:
                    await AskReasonAsync(ctx, s, s.Shifts[i], $"đổi lấy ca {s.Shifts[i].Label}", ct);
                    break;
                case "p" when i >= 0 && i < s.Colleagues.Count:
                    await AskReasonAsync(ctx, s, s.Colleagues[i], $"nhường ca cho {s.Colleagues[i].Label}", ct);
                    break;
                case "r" when reasons.At(Experience.ReasonCatalog.Swap, data.Id?.Split('.').Last()) is { } reason:
                    await SubmitAsync(ctx, s, reason, ct);
                    break;
                case "rx":
                    await AskAsync(ctx, "Lý do (gõ ngắn gọn):", ReplyReason, "swap", ct);
                    break;
                default:
                    await SayAsync(ctx, "Nút đã hết hạn – gõ /doica để làm lại.", ct);
                    break;
            }
        }

        public async Task ReplyAsync(BotActionContext ctx, PendingReply pending, CancellationToken ct)
        {
            var s = store.Get<State>(ctx.Bot, ctx.Event.FromId, Flow);
            if (s?.Current?.AssignmentId is not { } current || s.Chosen is not { } chosen)
            {
                await SayAsync(ctx, "Phiên đổi ca đã hết hạn – gõ /doica để làm lại.", ct);
                return;
            }
            string reason = (ctx.Event.Text ?? string.Empty).Trim();
            if (reason.Length < 3)
            {
                await AskAsync(ctx, "Lý do quá ngắn (ít nhất 3 ký tự) – gõ lại:", ReplyReason, pending.Key, ct);
                return;
            }
            await SubmitAsync(ctx, s, reason, ct);
        }

        private async Task SubmitAsync(BotActionContext ctx, State s, string reason, CancellationToken ct)
        {
            if (s.Current?.AssignmentId is not { } current || s.Chosen is not { } chosen)
            {
                await SayAsync(ctx, "Phiên đổi ca đã hết hạn – gõ /doica để làm lại.", ct);
                return;
            }
            if (await TokenAsync(ctx, ct) is not { } token)
            {
                return;
            }
            var form = new List<KeyValuePair<string, string>> { new("CurrentShiftAssignmentId", current.ToString()), new("TargetEmployeeId", chosen.EmployeeId.ToString()), new("Reason", "[Telegram] " + reason) };
            if (chosen.AssignmentId is { } proposed)
            {
                form.Add(new("ProposedShiftAssignmentId", proposed.ToString()));
            }
            var r = await Web.PostFormAsUserAsync<JsonElement>("api/ShiftSwapRequest/Create", form, token, ct);
            if (!r.IsOk)
            {
                await SayAsync(ctx, "⚠️ " + Problem(r), ct);
                return;
            }
            store.Clear(ctx.Bot, ctx.Event.FromId, Flow);
            await SendAsync(ctx, new NeutralMessage
            {
                Title = "✅ " + (r.ResponseMessage ?? "Đã gửi đơn đổi ca"),
                Lines = [$"{s.Current.Label} → {chosen.Label}", "Đơn chờ duyệt – kết quả xem ở 📝 Đơn của tôi."],
            }, ct);
        }

        private async Task CandidatesAsync(BotActionContext ctx, State s, Option current, CancellationToken ct)
        {
            if (await TokenAsync(ctx, ct) is not { } token)
            {
                return;
            }
            var r = await Web.GetAsUserAsync<JsonElement>("api/ShiftAssignment/GetSwapCandidates?assignmentId=" + current.AssignmentId, token, ct);
            if (!r.IsOk || r.Data.ValueKind != JsonValueKind.Object)
            {
                await SayAsync(ctx, Problem(r), ct);
                return;
            }
            s.Current = current;
            s.Shifts = [.. GiaoVanActions.Arr(r.Data, "listShift").Where(x => Guid.TryParse(S(x, "employeeId"), out _) && Guid.TryParse(S(x, "assignmentId"), out _)).Take(12)
                .Select(x => new Option { AssignmentId = Guid.Parse(S(x, "assignmentId")!), EmployeeId = Guid.Parse(S(x, "employeeId")!), Label = ShiftLabel(x, S(x, "employeeName")) })];
            s.Colleagues = [.. GiaoVanActions.Arr(r.Data, "listColleague").Where(x => Guid.TryParse(S(x, "employeeId"), out _))
                .Select(x => new Option { EmployeeId = Guid.Parse(S(x, "employeeId")!), Label = S(x, "employeeName") ?? "Đồng nghiệp" })];
            store.Set(ctx.Bot, ctx.Event.FromId, Flow, s);
            await ShowAsync(ctx, new NeutralMessage
            {
                Title = "🔁 Đổi ca: " + current.Label,
                Lines = [s.Shifts.Count == 0 ? "Chưa có ca nào của đồng nghiệp để đổi chéo (± 7 ngày)." : "Chọn ca muốn đổi lấy, hoặc 🤝 nhường ca:"],
                Buttons = [.. s.Shifts.Select((x, k) => new NeutralButton(x.Label, CallbackData.Build(Kind, "x", k.ToString(CultureInfo.InvariantCulture)))),
                    new NeutralButton("🤝 Nhường ca", CallbackData.Build(Kind, "g"))],
            }, ct);
        }

        private async Task AskReasonAsync(BotActionContext ctx, State s, Option chosen, string what, CancellationToken ct)
        {
            s.Chosen = chosen;
            store.Set(ctx.Bot, ctx.Event.FromId, Flow, s);
            // BOT-UX-2 (mục 7): lý do = nút lý do hay gặp (Messaging:Reasons:swap-reason) + [✏️ Khác…] (gõ tay).
            await ShowAsync(ctx, new NeutralMessage { Title = $"Lý do {what}", Buttons = reasons.Buttons(Experience.ReasonCatalog.Swap, Kind, "r", "rx", "k") }, ct);
        }

        private static string ShiftLabel(JsonElement a, string? who)
        {
            string day = DateTime.TryParse(S(a, "workDate"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d.ToString("dd/MM") : "";
            string from = DateTime.TryParse(S(a, "plannedStart"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var f) ? f.ToString("HH:mm") : "";
            return (who != null ? who + " · " : "") + $"{day} {S(a, "shiftName")} {from}".Trim();
        }

        private static string? S(JsonElement e, string k) => BepActions.S(e, k);
    }
}
