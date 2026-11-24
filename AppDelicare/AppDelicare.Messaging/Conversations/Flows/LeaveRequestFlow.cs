using System.Globalization;
using System.Text.Json;
using AppDelicare.Messaging.Audiences.Internal;
using AppDelicare.Messaging.Audiences.Internal.Chung;
using AppDelicare.Messaging.Callback;
using AppDelicare.Messaging.Channels;
using AppDelicare.Messaging.Channels.Telegram;
using AppDelicare.Messaging.Channels.Telegram.Models;
using AppDelicare.Messaging.Core;
using AppDelicare.Messaging.Core.Contracts;
using AppDelicare.Messaging.Experience;
using AppDelicare.Messaging.Presentation;

namespace AppDelicare.Messaging.Conversations.Flows
{
    /// <summary>
    /// BOT-1.5a (BOT_TELEGRAM §3.1 "🏖 Xin nghỉ – hỏi từng bước bằng nút"): loại nghỉ (ReferenceLists:LeaveType) → từ ngày (hôm nay / mai / ngày kia / gõ dd/MM)
    /// → số ngày (1 / 2 / 3 / gõ số) → lý do (ForceReply) → tạo đơn. Tài khoản web: <c>LeaveRequest/Create</c> bằng JWT của chính mình; chỉ EmployeeId:
    /// <c>BotChannel/EmployeeCreateLeave</c>. Mọi quy tắc (số ngày tối đa, lập lùi, chồng đơn, ngày đã chấm công) ở api – bot chỉ hỏi và chuyển câu báo.
    /// Callback <c>lv:t:&lt;mã&gt;</c>, <c>lv:d:&lt;0|1|2|x&gt;</c>, <c>lv:n:&lt;1|2|3|x&gt;</c>.
    /// </summary>
    public sealed class LeaveRequestFlow(IBotChannelApi api, IDelicareApiClient web, UserTokenCache tokens, IChannelSender sender, ConversationStore store,
        ReplyContextStore replies, TimeProvider time, ReasonCatalog reasons)
    {
        public const string Kind = "lv";
        public const string Flow = "leave";
        public const string ReplyDate = "leave-date";
        public const string ReplyDays = "leave-days";
        public const string ReplyReason = "leave-reason";

        public sealed class State
        {
            public string? Type { get; set; }
            public string? TypeLabel { get; set; }
            public DateTime? From { get; set; }
            public int? Days { get; set; }
        }

        public async Task StartAsync(BotActionContext ctx, CancellationToken ct)
        {
            var types = await api.GetLeaveTypesAsync(ct);
            store.Set(ctx.Bot, ctx.Event.FromId, Flow, new State());
            await BotReply.SendAsync(sender, ctx.Bot, ctx.Event.ChatId, new NeutralMessage
            {
                Title = "🏖 Xin nghỉ – chọn loại nghỉ",
                Buttons = [.. types.Take(10).Select(t => new NeutralButton(t.Label, CallbackData.Build(Kind, "t", t.Code)))],
            }, ct: ct);
        }

        public async Task HandleAsync(BotActionContext ctx, CallbackData data, CancellationToken ct)
        {
            var s = store.Get<State>(ctx.Bot, ctx.Event.FromId, Flow);
            if (s == null)
            {
                await SayAsync(ctx, "Phiên xin nghỉ đã hết hạn – gõ /nghi để làm lại.", ct);
                return;
            }
            DateTime today = VnTime.Today(time);
            switch (data.Action)
            {
                case "t":
                    s.Type = data.Id;
                    s.TypeLabel = (await api.GetLeaveTypesAsync(ct)).FirstOrDefault(t => t.Code == data.Id)?.Label ?? data.Id;
                    await EditAsync(ctx, new NeutralMessage
                    {
                        Title = "Nghỉ từ ngày nào? (" + s.TypeLabel + ")",
                        // BOT-UX-2 (mục 6): 10 ngày tới bấm 1 chạm (hàng ngày dd/MM), "Ngày khác…" chỉ cho ngày xa hơn.
                        Buttons =
                        [
                            new("Hôm nay " + today.ToString("dd/MM"), CallbackData.Build(Kind, "d", "0")),
                            new("Ngày mai " + today.AddDays(1).ToString("dd/MM"), CallbackData.Build(Kind, "d", "1")),
                            .. Enumerable.Range(2, 8).Select(p => new NeutralButton(today.AddDays(p).ToString("dd/MM"), CallbackData.Build(Kind, "d", p.ToString(CultureInfo.InvariantCulture)), Row: "lv-day")),
                            new("Ngày khác…", CallbackData.Build(Kind, "d", "x")),
                        ],
                    }, ct);
                    break;
                case "d" when data.Id == "x":
                    await AskAsync(ctx, "Nhập ngày bắt đầu nghỉ (dd/MM):", ReplyDate, ct);
                    break;
                case "d" when int.TryParse(data.Id, out int plus):
                    s.From = today.AddDays(plus);
                    await AskDaysAsync(ctx, s, ct);
                    break;
                case "n" when data.Id == "x":
                    await AskAsync(ctx, "Nghỉ bao nhiêu ngày? (gõ số)", ReplyDays, ct);
                    break;
                case "nn":
                    await AskDaysAsync(ctx, s, ct, InputWidgets.Value(data.Id));
                    break;
                case "n" when int.TryParse(data.Id, out int days) && days is >= 1 and <= 60:
                    s.Days = days;
                    await ReasonsAsync(ctx, s, ct);
                    break;
                case "r" when reasons.At(ReasonCatalog.Leave, data.Id?.Split('.').Last()) is { } reason:
                    await CreateAsync(ctx, s, reason, ct);
                    break;
                case "rx":
                    await AskAsync(ctx, $"Lý do nghỉ ({s.TypeLabel}, {s.From:dd/MM}, {s.Days} ngày):", ReplyReason, ct);
                    break;
            }
        }

        /// <summary>Trả lời các câu hỏi gõ tay (ngày khác / số ngày khác / lý do).</summary>
        public async Task ReplyAsync(BotActionContext ctx, PendingReply pending, CancellationToken ct)
        {
            var s = store.Get<State>(ctx.Bot, ctx.Event.FromId, Flow);
            string text = ctx.Event.Text?.Trim() ?? string.Empty;
            if (s == null)
            {
                await SayAsync(ctx, "Phiên xin nghỉ đã hết hạn – gõ /nghi để làm lại.", ct);
                return;
            }
            switch (pending.Flow)
            {
                case ReplyDate:
                    DateTime today = VnTime.Today(time);
                    if (!DateTime.TryParseExact(text, ["d/M", "dd/MM", "d/M/yyyy", "dd/MM/yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                    {
                        await AskAsync(ctx, "Ngày chưa đúng – nhập dạng dd/MM (vd. 25/11):", ReplyDate, ct);
                        return;
                    }
                    if (text.Count(c => c == '/') == 1)
                    {
                        d = new DateTime(today.Year, d.Month, d.Day);
                        if (d < today.AddDays(-31))
                        {
                            d = d.AddYears(1);
                        }
                    }
                    s.From = d.Date;
                    await AskDaysAsync(ctx, s, ct);
                    break;
                case ReplyDays:
                    if (!int.TryParse(text, out int days) || days < 1 || days > 60)
                    {
                        await AskAsync(ctx, "Số ngày chưa đúng – gõ số (1–60):", ReplyDays, ct);
                        return;
                    }
                    s.Days = days;
                    await ReasonsAsync(ctx, s, ct);
                    break;
                case ReplyReason:
                    await CreateAsync(ctx, s, text, ct);
                    break;
            }
        }

        private async Task CreateAsync(BotActionContext ctx, State s, string reason, CancellationToken ct)
        {
            if (s.Type == null || s.From == null || s.Days == null)
            {
                await SayAsync(ctx, "Thiếu thông tin – gõ /nghi để làm lại.", ct);
                return;
            }
            DateTime from = s.From.Value, to = from.AddDays(s.Days.Value - 1);
            ApiResult<JsonElement> r;
            if (ctx.Identity.HasWebAccount)
            {
                var (token, error) = await tokens.GetAsync(ctx.Bot, ctx.Event.FromId, ct);
                if (token == null)
                {
                    await SayAsync(ctx, error ?? "Không lấy được quyền truy cập.", ct);
                    return;
                }
                r = await web.PostFormAsUserAsync<JsonElement>("api/LeaveRequest/Create",
                [
                    new("FromDate", from.ToString("yyyy-MM-dd")), new("ToDate", to.ToString("yyyy-MM-dd")), new("LeaveTypeCode", s.Type), new("Reason", "[Telegram] " + reason),
                ], token.Token, ct);
            }
            else
            {
                r = await api.EmployeeCallAsync("EmployeeCreateLeave", new
                {
                    botCode = ctx.Bot, externalUserId = ctx.Event.FromId, fromDate = from.ToString("yyyy-MM-dd"), toDate = to.ToString("yyyy-MM-dd"),
                    leaveTypeCode = s.Type, reason,
                }, ct);
            }
            if (r.IsOk)
            {
                store.Clear(ctx.Bot, ctx.Event.FromId, Flow);
            }
            string? no = r.Data.ValueKind == JsonValueKind.Object && r.Data.TryGetProperty("request", out var req) && req.TryGetProperty("requestNo", out var rn) ? rn.GetString() : null;
            await BotReply.SendAsync(sender, ctx.Bot, ctx.Event.ChatId, new NeutralMessage
            {
                Title = r.IsOk ? "✅ Đã gửi đơn nghỉ " + no : "⚠️ Chưa gửi được đơn nghỉ",
                Lines = r.IsOk ? [$"{s.TypeLabel}: {from:dd/MM} – {to:dd/MM} ({s.Days} ngày). Chờ duyệt."] : [r.ResponseMessage ?? "Lỗi không xác định."],
            }, ct: ct);
        }

        /// <summary>
        /// Số ngày nghỉ: [1 ngày] [2 ngày] [3 ngày] bấm 1 chạm; BOT-UX-2 (mục 6): "Khác…" = màn số (−10 −1 +1 +10, 1–60 ngày, giá trị nằm trên panel) thay cho gõ tay.
        /// <paramref name="value"/> = đang chỉnh trên màn số (null = màn chọn nhanh).
        /// </summary>
        private Task AskDaysAsync(BotActionContext ctx, State s, CancellationToken ct, decimal? value = null)
        {
            var m = new NeutralMessage { Title = $"Nghỉ bao nhiêu ngày? (từ {s.From:dd/MM})" };
            if (value == null)
            {
                m.Buttons = [new("1 ngày", CallbackData.Build(Kind, "n", "1")), new("2 ngày", CallbackData.Build(Kind, "n", "2")), new("3 ngày", CallbackData.Build(Kind, "n", "3")),
                    new("Khác…", CallbackData.Build(Kind, "nn", "5"))];
            }
            else
            {
                InputWidgets.Number(m, Kind, "nn", "n", string.Empty, value.Value, "ngày", [4m, 5m, 7m, 10m, 14m], 1, 60);
            }
            return EditOrSendAsync(ctx, m, ct);
        }

        /// <summary>BOT-UX-2 (mục 7): lý do nghỉ = nút lý do hay gặp (<c>Messaging:Reasons:leave-reason</c>) + [✏️ Khác…] (gõ tay).</summary>
        private Task ReasonsAsync(BotActionContext ctx, State s, CancellationToken ct)
            => EditOrSendAsync(ctx, new NeutralMessage
            {
                Title = $"Lý do nghỉ ({s.TypeLabel}, {s.From:dd/MM}, {s.Days} ngày)",
                Buttons = reasons.Buttons(ReasonCatalog.Leave, Kind, "r", "rx", "k"),
            }, ct);

        private async Task AskAsync(BotActionContext ctx, string question, string flow, CancellationToken ct)
        {
            var (html, _) = TelegramFormatter.Render(NeutralMessage.Text(question));
            var sent = await sender.SendAsync(ctx.Bot, new OutgoingMessage(ctx.Event.ChatId, html, new TgForceReply()), ct);
            if (sent.ProviderMessageId != null)
            {
                replies.Remember(ctx.Bot, ctx.Event.ChatId, sent.ProviderMessageId, flow, Flow);
            }
        }

        private Task EditOrSendAsync(BotActionContext ctx, NeutralMessage m, CancellationToken ct)
            => ctx.Event.Kind == IncomingKind.Callback ? EditAsync(ctx, m, ct) : BotReply.SendAsync(sender, ctx.Bot, ctx.Event.ChatId, m, ct: ct);

        private async Task EditAsync(BotActionContext ctx, NeutralMessage m, CancellationToken ct)
        {
            var (html, kb) = TelegramFormatter.Render(m);
            if (ctx.Event.MessageId is { } mid)
            {
                await sender.EditAsync(ctx.Bot, ctx.Event.ChatId, mid.ToString(), html, kb, ct);
            }
            else
            {
                await sender.SendAsync(ctx.Bot, new OutgoingMessage(ctx.Event.ChatId, html, kb), ct);
            }
        }

        private Task SayAsync(BotActionContext ctx, string text, CancellationToken ct) => BotReply.TextAsync(sender, ctx.Bot, ctx.Event.ChatId, text, ct: ct);
    }
}
