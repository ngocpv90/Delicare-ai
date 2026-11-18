using System.Text.Json;
using AppDelicare.Messaging.Channels;
using AppDelicare.Messaging.Channels.Telegram;
using AppDelicare.Messaging.Core;
using AppDelicare.Messaging.Core.Contracts;
using AppDelicare.Messaging.Experience;
using AppDelicare.Messaging.Presentation;

namespace AppDelicare.Messaging.Audiences.Internal.Chung
{
    /// <summary>
    /// BOT-1.3 (khung): <c>/help</c>, <c>/menu</c>, 3 nút cố định. "⚡ Ca của tôi" gọi API nghiệp vụ bằng JWT của chính user (AttendanceEvent/GetMine) –
    /// người chỉ có EmployeeId dùng BotChannel/EmployeeGetToday. Menu chức năng theo quyền làm ở BOT-1.4.
    /// </summary>
    public sealed class HelpActions(IBotChannelApi api, IDelicareApiClient web, UserTokenCache tokens, IChannelSender sender)
    {
        public Task HelpAsync(string bot, IncomingEvent e, CancellationToken ct)
            => BotReply.SendAsync(sender, bot, e.ChatId, new NeutralMessage
            {
                Title = "Trợ giúp",
                Lines =
                [
                    KeyboardBuilder.MyShift + " – ca hôm nay của bạn",
                    KeyboardBuilder.Functions + " – chức năng theo quyền",
                    "/start – liên kết / mở lại bot",
                    "/huylienket – hủy liên kết tài khoản",
                ],
            }, KeyboardBuilder.MainMenu(), ct);

        public Task FunctionsAsync(string bot, IncomingEvent e, CancellationToken ct)
            => BotReply.TextAsync(sender, bot, e.ChatId, "Danh sách chức năng theo quyền đang được hoàn thiện.", KeyboardBuilder.MainMenu(), ct);

        public async Task MyShiftAsync(string bot, IncomingEvent e, BotIdentityModel identity, CancellationToken ct)
        {
            ApiResult<MyAttendanceModel> r;
            if (identity.HasWebAccount)
            {
                var (token, error) = await tokens.GetAsync(bot, e.FromId, ct);
                if (token == null)
                {
                    await BotReply.TextAsync(sender, bot, e.ChatId, error ?? "Không lấy được quyền truy cập.", ct: ct);
                    return;
                }
                r = await web.GetAsUserAsync<MyAttendanceModel>("api/AttendanceEvent/GetMine", token.Token, ct);
            }
            else
            {
                r = await api.EmployeeGetTodayAsync(new BotIdentityRequest { BotCode = bot, ExternalUserId = e.FromId }, ct);
            }
            if (!r.IsOk || r.Data == null)
            {
                await BotReply.TextAsync(sender, bot, e.ChatId, r.ResponseMessage ?? "Không xem được ca.", KeyboardBuilder.MainMenu(), ct);
                return;
            }
            var lines = r.Data.ListAssignment.Select(a =>
                $"{Str(a, "shiftName") ?? "Ca"}: {Time(a, "plannedStart")}–{Time(a, "plannedEnd")} ({Str(a, "statusName") ?? Str(a, "assignmentStatus") ?? ""})").ToList();
            if (lines.Count == 0)
            {
                lines.Add("Hôm nay bạn không có ca.");
            }
            if (r.Data.AllowedActions.Contains("checkIn"))
            {
                lines.Add("Có thể vào ca (chấm công bằng vị trí – bản tới).");
            }
            else if (r.Data.AllowedActions.Contains("checkOut"))
            {
                lines.Add("Đang trong ca – có thể ra ca.");
            }
            await BotReply.SendAsync(sender, bot, e.ChatId, new NeutralMessage { Title = "Ca của tôi – " + (r.Data.EmployeeName ?? ""), Lines = lines }, KeyboardBuilder.MainMenu(), ct);
        }

        private static string? Str(JsonElement e, string key)
            => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        private static string Time(JsonElement e, string key)
            => DateTime.TryParse(Str(e, key), out var t) ? t.ToString("HH:mm") : "?";
    }
}
