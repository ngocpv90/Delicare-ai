using System.Text.Json;
using AppDelicare.Messaging.Channels.Telegram.Models;
using AppDelicare.Messaging.Core;
using AppDelicare.Messaging.Core.Contracts;
using FluentAssertions;

namespace AppDelicare.Tests.Messaging
{
    /// <summary>
    /// BOT-1.5c: CSKH / điểm phát – 📋 phản ánh đang mở (sắp hạn trước) → thẻ → 🙋 nhận xử lý / ✅ đã xử lý (cách xử lý gõ tay) theo AllowedActions;
    /// 📣 ghi phản ánh: gõ tên khách → chọn → mức độ → mô tả → CustomerComplaint/Create. Nhân sự – 👥 ai chưa vào ca (quá 10′, không tính nghỉ phép / đã chấm)
    /// → ✍️ chấm hộ (chỉ khi có quyền màn chấm công, lý do bắt buộc, SourceChannel PROXY).
    /// </summary>
    public class MessagingPhanAnhNhanSuTests
    {
        private static readonly Guid Complaint = Guid.Parse("55555555-0000-4000-8000-0000000000a1");
        private static readonly Guid Customer = Guid.Parse("55555555-0000-4000-8000-0000000000b1");
        private static readonly Guid Late = Guid.Parse("55555555-0000-4000-8000-0000000000c1");
        private static long _u = 95000;

        private static string Text(string text, long from = 7301, long? replyTo = null)
        {
            long id = Interlocked.Increment(ref _u);
            string reply = replyTo == null ? "" : ",\"reply_to_message\":{\"message_id\":" + replyTo + ",\"date\":1,\"chat\":{\"id\":" + from + ",\"type\":\"private\"}}";
            return "{\"update_id\":" + id + ",\"message\":{\"message_id\":" + id + ",\"date\":" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ",\"from\":{\"id\":" + from
                + ",\"first_name\":\"Cs\"},\"chat\":{\"id\":" + from + ",\"type\":\"private\"},\"text\":" + JsonSerializer.Serialize(text) + reply + "}}";
        }

        private static string Tap(string data, long from = 7301)
        {
            long id = Interlocked.Increment(ref _u);
            return "{\"update_id\":" + id + ",\"callback_query\":{\"id\":\"cb" + id + "\",\"from\":{\"id\":" + from + "},\"data\":" + JsonSerializer.Serialize(data)
                + ",\"message\":{\"message_id\":80,\"date\":1,\"chat\":{\"id\":" + from + ",\"type\":\"private\"}}}}";
        }

        private static long LastSentId(MessagingTestHost h) => 100 + h.Sender.Sent.Count;

        private static List<string> Buttons(object? markup) => [.. ((TgInlineKeyboard)markup!).InlineKeyboard.SelectMany(r => r).Select(b => b.CallbackData!)];

        private static MessagingTestHost Host(params string[] menus)
        {
            var h = new MessagingTestHost();
            h.Api.Identity = new BotIdentityModel { Linked = true, CanUseBot = true, HasWebAccount = true, UserId = Guid.NewGuid(), FullName = "CSKH", Status = "Active", Menus = [.. menus] };
            h.Api.Token = new ApiResult<UserTokenModel> { ResponseCode = 200, Data = new UserTokenModel { Token = "jwt-cs" } };
            return h;
        }

        private static string Item(Guid id, string no, string status, decimal hours, string severity = "High")
            => JsonSerializer.Serialize(new { complaint = new { id, complaintNo = no, complaintStatus = status, severity }, customerName = "Trường A", severityName = "Cao", statusName = status, hoursToDue = hours });

        [Fact]
        public async Task Open_complaints_sorted_by_due_then_assign_and_resolve_with_typed_resolution()
        {
            await using var h = Host("#/log-complaint");
            h.Web.Routes["api/CustomerComplaint/GetList"] = (_, _) => (200, null, "{\"listItem\":[" + Item(Guid.NewGuid(), "PA-2", "InProgress", 20) + "," + Item(Complaint, "PA-1", "Open", 2)
                + "," + Item(Guid.NewGuid(), "PA-0", "Closed", 1) + "]}", null);
            string detail = JsonSerializer.Serialize(new
            {
                complaint = new { id = Complaint, complaintNo = "PA-1", complaintStatus = "Open", severity = "High", description = "Cơm nguội", slaDueAt = "2031-06-02T12:00:00" },
                statusName = "Mới", severityName = "Cao", customerName = "Trường A", allowedActions = new[] { "assign", "resolve", "addComment" },
            });
            h.Web.Routes["api/CustomerComplaint/GetDetail"] = (_, _) => (200, null, detail, null);
            h.Web.Routes["api/CustomerComplaint/Assign"] = (_, _) => (200, "Đã nhận xử lý", "{}", null);
            h.Web.Routes["api/CustomerComplaint/Resolve"] = (_, _) => (200, "Đã xử lý", "{}", null);
            await h.SendAsync(Text("/phananh"));
            var list = h.Sender.Sent.Last().Message;
            list.Text.Should().Contain("(2)").And.NotContain("PA-0");
            list.Text.IndexOf("PA-1", StringComparison.Ordinal).Should().BeLessThan(list.Text.IndexOf("PA-2", StringComparison.Ordinal), "sắp hạn trước");
            string c = Complaint.ToString("N");
            await h.SendAsync(Tap("cmp:v:" + c));
            h.Sender.Edits.Last().Text.Should().Contain("Cơm nguội");
            Buttons(h.Sender.Edits.Last().Markup).Should().Contain(["cmp:as:" + c, "cmp:rs:" + c, "cmp:nt:" + c]);
            await h.SendAsync(Tap("cmp:as:" + c));
            h.Web.Calls.Last(x => x.Path == "api/CustomerComplaint/Assign").Form["Id"].Should().Be(Complaint.ToString());
            await h.SendAsync(Tap("cmp:rs:" + c));
            await h.SendAsync(Text("Đã đổi suất nóng", replyTo: LastSentId(h)));
            var resolve = h.Web.Calls.Last(x => x.Path == "api/CustomerComplaint/Resolve");
            resolve.Form["Comment"].Should().Be("Đã đổi suất nóng");
            resolve.Token.Should().Be("jwt-cs");
        }

        [Fact]
        public async Task New_complaint_search_customer_choose_severity_then_description()
        {
            await using var h = Host("#/log-complaint");
            h.Web.Routes["api/Customer/GetAll"] = (_, _) => (200, null, null, JsonSerializer.Serialize(new[] { new { id = Customer, name = "Trường A", code = "KH01" } }));
            h.Web.Routes["api/CustomerComplaint/Create"] = (_, _) => (200, "Đã tạo", JsonSerializer.Serialize(new { complaint = new { complaintNo = "PA-261118-0001", slaDueAt = "2031-06-02T12:00:00" }, severityName = "Cao" }), null);
            await h.SendAsync(Text("📣 Ghi phản ánh"));
            h.Sender.Sent.Last().Message.ReplyMarkup.Should().BeOfType<TgForceReply>();
            await h.SendAsync(Text("truong a", replyTo: LastSentId(h)));
            h.Web.Calls.Last(x => x.Path.StartsWith("api/Customer/GetAll")).Path.Should().Contain("searchText=truong%20a");
            Buttons(h.Sender.Sent.Last().Message.ReplyMarkup).Should().Equal("cmp:c:0");
            await h.SendAsync(Tap("cmp:c:0"));
            Buttons(h.Sender.Edits.Last().Markup).Should().Equal("cmp:sv:0", "cmp:sv:1", "cmp:sv:2");
            await h.SendAsync(Tap("cmp:sv:0"));
            await h.SendAsync(Text("Thiếu 5 suất", replyTo: LastSentId(h)));
            var call = h.Web.Calls.Last(x => x.Path == "api/CustomerComplaint/Create");
            call.Form["CustomerId"].Should().Be(Customer.ToString());
            call.Form["Severity"].Should().Be("High");
            call.Form["SourceChannel"].Should().Be("TELEGRAM");
            call.Form["Description"].Should().Be("Thiếu 5 suất");
            h.Sender.Sent.Last().Message.Text.Should().Contain("PA-261118-0001");
        }

        private static string Grid(DateTime now)
        {
            string key = now.ToString("yyyy-MM-dd");
            object Cell(DateTime start, string status, bool att = false, bool leave = false) => new { shiftName = "Ca sáng", plannedStart = start.ToString("yyyy-MM-ddTHH:mm:ss"), status, hasAttendance = att, onLeave = leave };
            var employees = new object[]
            {
                new { id = Late, fullName = "Nguyễn Trễ", cells = new Dictionary<string, object> { [key] = Cell(now.AddMinutes(-30), "Planned") } },
                new { id = Guid.NewGuid(), fullName = "Vừa tới giờ", cells = new Dictionary<string, object> { [key] = Cell(now.AddMinutes(-5), "Planned") } },
                new { id = Guid.NewGuid(), fullName = "Đã vào", cells = new Dictionary<string, object> { [key] = Cell(now.AddMinutes(-60), "CheckedIn", att: true) } },
                new { id = Guid.NewGuid(), fullName = "Nghỉ phép", cells = new Dictionary<string, object> { [key] = Cell(now.AddMinutes(-60), "Planned", leave: true) } },
            };
            return JsonSerializer.Serialize(new { employees });
        }

        [Fact]
        public async Task Absent_list_and_proxy_check_in_requires_attendance_menu_and_reason()
        {
            await using var h = Host("#/hr-shift-assign", "#/hr-attendance");
            h.Web.Routes["api/ShiftAssignment/GetGrid"] = (_, _) => (200, null, Grid(VnTime.Now(TimeProvider.System)), null);
            h.Web.Routes["api/AttendanceEvent/CheckIn"] = (_, _) => (200, "Đã chấm vào ca", "{\"employeeName\":\"Nguyễn Trễ\",\"eventTypeName\":\"Vào ca\",\"eventAt\":\"2031-06-02T07:30:00\",\"listWarning\":[\"Đi muộn 30 phút\"]}", null);
            await h.SendAsync(Text("/chuavao"));
            var list = h.Sender.Sent.Last().Message;
            list.Text.Should().Contain("(1)").And.Contain("Nguyễn Trễ").And.NotContain("Vừa tới giờ").And.NotContain("Nghỉ phép");
            string id = Late.ToString("N");
            Buttons(list.ReplyMarkup).Should().Equal("hr:px:" + id);
            await h.SendAsync(Tap("hr:px:" + id));
            await h.SendAsync(Text("Quên điện thoại", replyTo: LastSentId(h)));
            var call = h.Web.Calls.Last(x => x.Path == "api/AttendanceEvent/CheckIn");
            call.Form["EmployeeId"].Should().Be(Late.ToString());
            call.Form["SourceChannel"].Should().Be("PROXY");
            call.Form["Note"].Should().Be("Chấm hộ qua Telegram: Quên điện thoại");
            h.Sender.Sent.Last().Message.Text.Should().Contain("Đi muộn 30 phút");

            await using var viewer = Host("#/hr-shift-assign");
            viewer.Web.Routes["api/ShiftAssignment/GetGrid"] = (_, _) => (200, null, Grid(VnTime.Now(TimeProvider.System)), null);
            await viewer.SendAsync(Text("/chuavao"));
            viewer.Sender.Sent.Last().Message.ReplyMarkup.Should().BeNull("không có quyền màn chấm công → không có nút chấm hộ");
        }
    }
}
