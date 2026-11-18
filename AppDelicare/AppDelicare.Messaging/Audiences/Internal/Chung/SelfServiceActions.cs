using System.Globalization;
using System.Text.Json;
using AppDelicare.Messaging.Channels;
using AppDelicare.Messaging.Core;
using AppDelicare.Messaging.Core.Contracts;
using AppDelicare.Messaging.Experience;
using AppDelicare.Messaging.Presentation;

namespace AppDelicare.Messaging.Audiences.Internal.Chung
{
    /// <summary>
    /// BOT-1.4 (BOT_TELEGRAM §3.1, §6): 📍 Chấm công bằng vị trí (nút cố định xin vị trí) – chặn vị trí CHUYỂN TIẾP và vị trí cũ hơn 120″; vào / ra ca theo việc api cho phép
    /// (GetMine – AllowedActions). Người có tài khoản web: <c>AttendanceEvent/CheckIn|CheckOut</c> bằng JWT của chính mình; chỉ có EmployeeId:
    /// <c>BotChannel/EmployeeCheckIn|CheckOut</c>. Quy tắc ca / bán kính / chống trùng (ExternalEventId = update) nằm ở api. 📝 Đơn của tôi: danh sách đơn nghỉ / đổi ca.
    /// Lịch ca 7 ngày, xin nghỉ / đổi ca từng bước: BOT-1.5.
    /// </summary>
    public sealed class SelfServiceActions(IBotChannelApi api, IDelicareApiClient web, UserTokenCache tokens, IChannelSender sender, TimeProvider time)
    {
        public const int MaxLocationAgeSeconds = 120;

        public Task PromptLocationAsync(BotActionContext ctx, CancellationToken ct)
            => BotReply.TextAsync(sender, ctx.Bot, ctx.Event.ChatId, "Bấm nút \"" + KeyboardBuilder.CheckIn + "\" để gửi vị trí hiện tại.", KeyboardBuilder.Location(), ct);

        public async Task LocationAsync(BotActionContext ctx, CancellationToken ct)
        {
            var e = ctx.Event;
            if (e.Forwarded)
            {
                await SayAsync(ctx, "Không nhận vị trí chuyển tiếp – bấm \"" + KeyboardBuilder.CheckIn + "\" để gửi vị trí của bạn.", ct);
                return;
            }
            if (e.Location == null || (e.Date > 0 && time.GetUtcNow().ToUnixTimeSeconds() - e.Date > MaxLocationAgeSeconds))
            {
                await SayAsync(ctx, "Vị trí đã cũ – vui lòng gửi lại vị trí hiện tại.", ct);
                return;
            }
            string lat = e.Location.Latitude.ToString(CultureInfo.InvariantCulture), lng = e.Location.Longitude.ToString(CultureInfo.InvariantCulture);
            string eventId = $"TG:{ctx.Bot}:{e.UpdateId}";
            ApiResult<JsonElement> result;
            List<string> allowed;
            if (ctx.Identity.HasWebAccount)
            {
                var (token, error) = await tokens.GetAsync(ctx.Bot, e.FromId, ct);
                if (token == null)
                {
                    await SayAsync(ctx, error ?? "Không lấy được quyền truy cập.", ct);
                    return;
                }
                var mine = await web.GetAsUserAsync<MyAttendanceModel>("api/AttendanceEvent/GetMine", token.Token, ct);
                if (!mine.IsOk || mine.Data == null)
                {
                    await SayAsync(ctx, mine.ResponseMessage ?? "Không xem được ca.", ct);
                    return;
                }
                allowed = mine.Data.AllowedActions;
                string? path = Path(allowed, "api/AttendanceEvent/CheckIn", "api/AttendanceEvent/CheckOut");
                if (path == null)
                {
                    await SayAsync(ctx, "Hiện không có ca để vào / ra.", ct);
                    return;
                }
                result = await web.PostFormAsUserAsync<JsonElement>(path, [new("Latitude", lat), new("Longitude", lng), new("SourceChannel", "GPS"), new("ExternalEventId", eventId)], token.Token, ct);
            }
            else
            {
                var who = new BotIdentityRequest { BotCode = ctx.Bot, ExternalUserId = e.FromId };
                var today = await api.EmployeeGetTodayAsync(who, ct);
                if (!today.IsOk || today.Data == null)
                {
                    await SayAsync(ctx, today.ResponseMessage ?? "Không xem được ca.", ct);
                    return;
                }
                allowed = today.Data.AllowedActions;
                string? action = Path(allowed, "EmployeeCheckIn", "EmployeeCheckOut");
                if (action == null)
                {
                    await SayAsync(ctx, "Hiện không có ca để vào / ra.", ct);
                    return;
                }
                result = await api.EmployeeCallAsync(action, new
                {
                    botCode = ctx.Bot, externalUserId = e.FromId, displayName = e.DisplayName, latitude = e.Location.Latitude, longitude = e.Location.Longitude,
                    sourceChannel = "GPS", externalEventId = eventId,
                }, ct);
            }
            string verb = allowed.Contains("checkOut") ? "Ra ca" : "Vào ca";
            await BotReply.SendAsync(sender, ctx.Bot, e.ChatId, new NeutralMessage
            {
                Title = result.IsOk ? "✅ " + verb : "⚠️ " + verb + " chưa được",
                Lines = [result.ResponseMessage ?? ""],
            }, ct: ct);
        }

        /// <summary>BOT-1.5a: 📅 Lịch ca 7 ngày – web: ShiftAssignment/GetMine; chỉ EmployeeId: BotChannel/EmployeeGetMyShifts.</summary>
        public async Task MyShiftsAsync(BotActionContext ctx, CancellationToken ct)
        {
            ApiResult<JsonElement> r;
            if (ctx.Identity.HasWebAccount)
            {
                var (token, error) = await tokens.GetAsync(ctx.Bot, ctx.Event.FromId, ct);
                if (token == null)
                {
                    await SayAsync(ctx, error ?? "Không lấy được quyền truy cập.", ct);
                    return;
                }
                r = await web.GetAsUserAsync<JsonElement>("api/ShiftAssignment/GetMine?days=7", token.Token, ct);
            }
            else
            {
                r = await api.EmployeeCallAsync("EmployeeGetMyShifts", new BotIdentityRequest { BotCode = ctx.Bot, ExternalUserId = ctx.Event.FromId }, ct);
            }
            if (!r.IsOk)
            {
                await SayAsync(ctx, r.ResponseMessage ?? "Không xem được lịch ca.", ct);
                return;
            }
            var rows = (r.ListData ?? []).ToList();
            await BotReply.SendAsync(sender, ctx.Bot, ctx.Event.ChatId, new NeutralMessage
            {
                Title = "📅 Lịch ca 7 ngày",
                Lines = rows.Count == 0 ? ["Chưa có ca trong 7 ngày tới."]
                    : [.. rows.Select(x => $"{Date(x, null, "workDate")} {Str(x, null, "shiftName") ?? Str(x, null, "shiftCode")}: {Time(x, "plannedStart")}–{Time(x, "plannedEnd")}")],
            }, ct: ct);
        }

        private static string Time(JsonElement e, string key)
            => DateTime.TryParse(Str(e, null, key), CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t.ToString("HH:mm") : "?";

        public async Task MyRequestsAsync(BotActionContext ctx, CancellationToken ct)
        {
            var lines = new List<string>();
            if (ctx.Identity.HasWebAccount)
            {
                var (token, error) = await tokens.GetAsync(ctx.Bot, ctx.Event.FromId, ct);
                if (token == null)
                {
                    await SayAsync(ctx, error ?? "Không lấy được quyền truy cập.", ct);
                    return;
                }
                var leaves = await web.GetAsUserAsync<JsonElement>("api/LeaveRequest/GetMine", token.Token, ct);
                var swaps = await web.GetAsUserAsync<JsonElement>("api/ShiftSwapRequest/GetMine", token.Token, ct);
                lines.AddRange((leaves.ListData ?? []).Take(10).Select(l => $"🏖 {Str(l, "request", "requestNo")} {Date(l, "request", "fromDate")}–{Date(l, "request", "toDate")} · {Str(l, null, "statusName")}"));
                lines.AddRange((swaps.ListData ?? []).Take(10).Select(s => $"🔁 {Str(s, "request", "requestNo")} · {Str(s, null, "statusName")}"));
            }
            else
            {
                var r = await api.EmployeeCallAsync("EmployeeGetMyRequests", new BotIdentityRequest { BotCode = ctx.Bot, ExternalUserId = ctx.Event.FromId }, ct);
                lines.AddRange((r.ListData ?? []).Take(20).Select(x => $"{(Str(x, null, "kind") == "SWAP" ? "🔁" : "🏖")} {Date(x, null, "fromDate")} · {Str(x, null, "status")}"));
            }
            await BotReply.SendAsync(sender, ctx.Bot, ctx.Event.ChatId, new NeutralMessage
            {
                Title = "📝 Đơn của tôi",
                Lines = lines.Count == 0 ? ["Bạn chưa có đơn nào (60 ngày gần đây)."] : lines,
            }, ct: ct);
        }

        private static string? Path(List<string> allowed, string checkIn, string checkOut)
            => allowed.Contains("checkOut") ? checkOut : allowed.Contains("checkIn") ? checkIn : null;

        private Task SayAsync(BotActionContext ctx, string text, CancellationToken ct) => BotReply.TextAsync(sender, ctx.Bot, ctx.Event.ChatId, text, ct: ct);

        private static string? Str(JsonElement e, string? parent, string key)
        {
            if (parent != null)
            {
                if (!e.TryGetProperty(parent, out e) || e.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }
            }
            return e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        }

        private static string Date(JsonElement e, string? parent, string key)
            => DateTime.TryParse(Str(e, parent, key), CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d.ToString("dd/MM") : "";
    }
}
