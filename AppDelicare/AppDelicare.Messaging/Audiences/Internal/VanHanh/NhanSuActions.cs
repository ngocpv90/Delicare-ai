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
    /// BOT-1.5c (BOT_TELEGRAM §3.2 – nhân sự FIN-HR, tổ trưởng có quyền màn phân ca): 👥 Ai chưa vào ca (<c>ShiftAssignment/GetGrid</c> hôm nay, mục <c>#/hr-shift-assign</c>:
    /// ca còn mở, giờ bắt đầu đã qua ≥ 10′, chưa chấm, không nghỉ phép) → [✍️ Chấm hộ] (chỉ khi có mục <c>#/hr-attendance</c>; lý do gõ tay bắt buộc –
    /// <c>AttendanceEvent/CheckIn</c> EmployeeId + SourceChannel PROXY, api kiểm quyền Thêm). Callback <c>hr:px:&lt;EmployeeId N&gt;</c>.
    /// </summary>
    public sealed class NhanSuActions(IDelicareApiClient web, UserTokenCache tokens, IChannelSender sender, ReplyContextStore replies, TimeProvider time)
        : WebActions(web, tokens, sender, replies), IBotActionProvider
    {
        public const string Kind = CallbackData.Kinds.Hr;
        public const string ReplyProxy = "hr-proxy";
        public const string AttendanceMenu = "#/hr-attendance";
        public const int GraceMinutes = 10;

        public IEnumerable<BotAction> GetActions() =>
        [
            new BotAction { Code = "hr_absent", Module = "NHANSU", Label = "👥 Ai chưa vào ca", Command = "chuavao", CommandDescription = "Ai chưa vào ca hôm nay", Bots = ["VH"], MenuMetaLink = "#/hr-shift-assign", Order = 1, Handler = AbsentAsync },
        ];

        public async Task AbsentAsync(BotActionContext ctx, CancellationToken ct)
        {
            if (await TokenAsync(ctx, ct) is not { } token)
            {
                return;
            }
            DateTime now = VnTime.Now(time);
            string key = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var r = await Web.GetAsUserAsync<JsonElement>($"api/ShiftAssignment/GetGrid?fromDate={key}&days=1", token, ct);
            if (!r.IsOk || r.Data.ValueKind != JsonValueKind.Object)
            {
                await SayAsync(ctx, Problem(r), ct);
                return;
            }
            var absent = new List<(Guid Id, string Name, string Line)>();
            foreach (var e in GiaoVanActions.Arr(r.Data, "employees"))
            {
                if (!e.TryGetProperty("cells", out var cells) || cells.ValueKind != JsonValueKind.Object || !cells.TryGetProperty(key, out var c) || c.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }
                bool open = S(c, "status") is "Planned" or "Confirmed";
                bool started = DateTime.TryParse(S(c, "plannedStart"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var start) && start.AddMinutes(GraceMinutes) <= now;
                if (open && started && !Flag(c, "hasAttendance") && !Flag(c, "onLeave") && Guid.TryParse(S(e, "id"), out var id))
                {
                    string name = S(e, "fullName") ?? S(e, "code") ?? "Nhân viên";
                    absent.Add((id, name, $"• {name} · {S(c, "shiftName")} {start:HH:mm} · trễ {(int)(now - start).TotalMinutes}′{(S(e, "organizationUnitName") is { } u ? " · " + u : "")}"));
                }
            }
            bool canProxy = ctx.Identity.IsAdmin || ctx.Identity.Menus.Contains(AttendanceMenu, StringComparer.OrdinalIgnoreCase);
            await SendAsync(ctx, new NeutralMessage
            {
                Title = $"👥 Chưa vào ca {now:HH:mm} ({absent.Count})",
                Lines = absent.Count == 0 ? ["Mọi người có ca đã vào ca đủ."] : [.. absent.Take(25).Select(a => a.Line)],
                Buttons = canProxy ? [.. absent.Take(10).Select(a => new NeutralButton("✍️ " + a.Name, CallbackData.Build(Kind, "px", a.Id)))] : [],
            }, ct);
        }

        public async Task HandleAsync(BotActionContext ctx, CallbackData data, CancellationToken ct)
        {
            if (data.Action == "px" && Guid.TryParseExact(data.Id ?? string.Empty, "N", out Guid id))
            {
                await AskAsync(ctx, "✍️ Chấm hộ vào ca – lý do (bắt buộc, vd. quên điện thoại):", ReplyProxy, id.ToString("N"), ct);
                return;
            }
            await SayAsync(ctx, "Nút đã hết hạn.", ct);
        }

        public async Task ReplyAsync(BotActionContext ctx, PendingReply pending, CancellationToken ct)
        {
            if (!Guid.TryParseExact(pending.Key, "N", out Guid id) || await TokenAsync(ctx, ct) is not { } token)
            {
                return;
            }
            string reason = (ctx.Event.Text ?? string.Empty).Trim();
            if (reason.Length < 3)
            {
                await AskAsync(ctx, "Lý do quá ngắn – gõ lại:", ReplyProxy, pending.Key, ct);
                return;
            }
            var r = await Web.PostFormAsUserAsync<JsonElement>("api/AttendanceEvent/CheckIn",
                [new("EmployeeId", id.ToString()), new("SourceChannel", "PROXY"), new("Note", "Chấm hộ qua Telegram: " + reason)], token, ct);
            if (!r.IsOk)
            {
                await SayAsync(ctx, "⚠️ " + Problem(r), ct);
                return;
            }
            var lines = new List<string> { $"{S(r.Data, "employeeName")} · {S(r.Data, "eventTypeName")} {Time(S(r.Data, "eventAt"))}" };
            lines.AddRange(GiaoVanActions.Arr(r.Data, "listWarning").Select(w => "⚠️ " + w));
            await SendAsync(ctx, new NeutralMessage { Title = "✅ " + (r.ResponseMessage ?? "Đã chấm hộ"), Lines = lines }, ct);
        }

        private static bool Flag(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.True;

        private static string? S(JsonElement e, string k) => BepActions.S(e, k);

        private static string Time(string? s) => DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t.ToString("HH:mm") : "";
    }
}
