using System.Text.Json;
using AppDelicare.Messaging.Channels.Telegram.Models;
using AppDelicare.Messaging.Core;
using AppDelicare.Messaging.Core.Contracts;
using FluentAssertions;

namespace AppDelicare.Tests.Messaging
{
    /// <summary>
    /// BOT-1.5c (DECISIONS p24): 🔁 Đổi ca – chọn ca còn mở của tôi → ca có thể đổi (GetSwapCandidates) → đổi chéo / 🤝 nhường ca → lý do → ShiftSwapRequest/Create
    /// bằng JWT của chính mình. Người chỉ có EmployeeId không thấy chức năng; phiên hết hạn báo rõ.
    /// </summary>
    public class MessagingSwapTests
    {
        private static readonly Guid MyShift = Guid.Parse("44444444-0000-4000-8000-0000000000a1");
        private static readonly Guid TheirShift = Guid.Parse("44444444-0000-4000-8000-0000000000b1");
        private static readonly Guid Colleague = Guid.Parse("44444444-0000-4000-8000-0000000000c1");
        private static readonly Guid Colleague2 = Guid.Parse("44444444-0000-4000-8000-0000000000c2");
        private static long _u = 90000;

        private static string Text(string text, long from = 7201, long? replyTo = null)
        {
            long id = Interlocked.Increment(ref _u);
            string reply = replyTo == null ? "" : ",\"reply_to_message\":{\"message_id\":" + replyTo + ",\"date\":1,\"chat\":{\"id\":" + from + ",\"type\":\"private\"}}";
            return "{\"update_id\":" + id + ",\"message\":{\"message_id\":" + id + ",\"date\":" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ",\"from\":{\"id\":" + from
                + ",\"first_name\":\"Hoa\"},\"chat\":{\"id\":" + from + ",\"type\":\"private\"},\"text\":" + JsonSerializer.Serialize(text) + reply + "}}";
        }

        private static string Tap(string data, long from = 7201)
        {
            long id = Interlocked.Increment(ref _u);
            return "{\"update_id\":" + id + ",\"callback_query\":{\"id\":\"cb" + id + "\",\"from\":{\"id\":" + from + "},\"data\":" + JsonSerializer.Serialize(data)
                + ",\"message\":{\"message_id\":70,\"date\":1,\"chat\":{\"id\":" + from + ",\"type\":\"private\"}}}}";
        }

        private static long LastSentId(MessagingTestHost h) => 100 + h.Sender.Sent.Count;

        private static List<string> Buttons(object? markup) => [.. ((TgInlineKeyboard)markup!).InlineKeyboard.SelectMany(r => r).Select(b => b.CallbackData!)];

        private static MessagingTestHost Host()
        {
            var h = new MessagingTestHost();
            h.Api.Identity = new BotIdentityModel { Linked = true, CanUseBot = true, HasWebAccount = true, UserId = Guid.NewGuid(), FullName = "Hoa", Status = "Active" };
            h.Api.Token = new ApiResult<UserTokenModel> { ResponseCode = 200, Data = new UserTokenModel { Token = "jwt-hoa" } };
            string day = VnTime.Today(TimeProvider.System).AddDays(2).ToString("yyyy-MM-dd");
            string past = VnTime.Today(TimeProvider.System).AddDays(-1).ToString("yyyy-MM-dd");
            h.Web.Routes["api/ShiftAssignment/GetMine"] = (_, _) => (200, null, null, JsonSerializer.Serialize(new object[]
            {
                new { assignmentId = Guid.NewGuid(), workDate = past + "T00:00:00", shiftName = "Ca sáng", plannedStart = past + "T06:00:00", assignmentStatus = "Planned" },
                new { assignmentId = MyShift, workDate = day + "T00:00:00", shiftName = "Ca sáng", plannedStart = day + "T06:00:00", assignmentStatus = "Planned" },
                new { assignmentId = Guid.NewGuid(), workDate = day + "T00:00:00", shiftName = "Ca chiều", plannedStart = day + "T14:00:00", assignmentStatus = "Completed" },
            }));
            h.Web.Routes["api/ShiftAssignment/GetSwapCandidates"] = (_, _) => (200, null, JsonSerializer.Serialize(new
            {
                current = new { assignmentId = MyShift },
                listShift = new[] { new { employeeId = Colleague, employeeName = "Lan", assignmentId = TheirShift, workDate = day + "T00:00:00", shiftName = "Ca chiều", plannedStart = day + "T14:00:00" } },
                listColleague = new[] { new { employeeId = Colleague, employeeName = "Lan" }, new { employeeId = Colleague2, employeeName = "Mai" } },
            }), null);
            h.Web.Routes["api/ShiftSwapRequest/Create"] = (_, _) => (200, "Đã gửi đơn đổi ca", "{}", null);
            return h;
        }

        [Fact]
        public async Task Swap_exchange_with_colleague_shift_then_reason_creates_request()
        {
            await using var h = Host();
            await h.SendAsync(Text("/doica"));
            var mine = h.Sender.Sent.Last().Message;
            Buttons(mine.ReplyMarkup).Should().Equal("swp:m:0"); // chỉ ca còn mở, từ hôm nay
            await h.SendAsync(Tap("swp:m:0"));
            h.Web.Calls.Last(c => c.Path.StartsWith("api/ShiftAssignment/GetSwapCandidates")).Path.Should().Contain("assignmentId=" + MyShift);
            var cands = h.Sender.Edits.Last();
            ((TgInlineKeyboard)cands.Markup!).InlineKeyboard.SelectMany(r => r).First().Text.Should().Contain("Lan").And.Contain("Ca chiều 14:00");
            Buttons(cands.Markup).Should().Equal("swp:x:0", "swp:g");
            await h.SendAsync(Tap("swp:x:0"));
            h.Sender.Sent.Last().Message.ReplyMarkup.Should().BeOfType<TgForceReply>();
            await h.SendAsync(Text("ok", replyTo: LastSentId(h)));
            h.Sender.Sent.Last().Message.Text.Should().Contain("Lý do quá ngắn");
            await h.SendAsync(Text("Bận việc gia đình", replyTo: LastSentId(h)));
            var call = h.Web.Calls.Last(c => c.Path == "api/ShiftSwapRequest/Create");
            call.Token.Should().Be("jwt-hoa");
            call.Form["CurrentShiftAssignmentId"].Should().Be(MyShift.ToString());
            call.Form["ProposedShiftAssignmentId"].Should().Be(TheirShift.ToString());
            call.Form["TargetEmployeeId"].Should().Be(Colleague.ToString());
            call.Form["Reason"].Should().Be("[Telegram] Bận việc gia đình");
            h.Sender.Sent.Last().Message.Text.Should().Contain("Đã gửi đơn đổi ca");
        }

        [Fact]
        public async Task Give_away_shift_to_colleague_without_proposed_shift()
        {
            await using var h = Host();
            await h.SendAsync(Text("🔁 Đổi ca"));
            await h.SendAsync(Tap("swp:m:0"));
            await h.SendAsync(Tap("swp:g"));
            Buttons(h.Sender.Edits.Last().Markup).Should().Equal("swp:p:0", "swp:p:1");
            await h.SendAsync(Tap("swp:p:1"));
            await h.SendAsync(Text("Nhường cho Mai", replyTo: LastSentId(h)));
            var call = h.Web.Calls.Last(c => c.Path == "api/ShiftSwapRequest/Create");
            call.Form["TargetEmployeeId"].Should().Be(Colleague2.ToString());
            call.Form.Should().NotContainKey("ProposedShiftAssignmentId");
        }

        [Fact]
        public async Task Employee_only_does_not_see_swap_and_stale_button_is_reported()
        {
            await using var h = new MessagingTestHost();
            h.Api.Identity = new BotIdentityModel { Linked = true, CanUseBot = true, EmployeeId = Guid.NewGuid(), Status = "Active" };
            await h.SendAsync(Text("/doica"));
            h.Web.Calls.Should().NotContain(c => c.Path.StartsWith("api/ShiftAssignment"));
            await h.SendAsync(Tap("swp:m:0"));
            h.Sender.Sent.Last().Message.Text.Should().Contain("Phiên đổi ca đã hết hạn");
        }
    }
}
