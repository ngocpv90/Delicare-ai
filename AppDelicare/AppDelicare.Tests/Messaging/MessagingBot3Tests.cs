using System.Text.Json;
using AppDelicare.Messaging.Core;
using AppDelicare.Messaging.Core.Contracts;
using FluentAssertions;

namespace AppDelicare.Tests.Messaging
{
    /// <summary>
    /// BOT-3.1 (UG-1 C-22, anh chọn 2026-11-19): chức năng bot chỉ xem dùng API có sẵn – thiếu hụt, NL của ca, việc tôi đã báo, công tháng, đơn đang chờ,
    /// lịch xe, hộp khay hôm nay, truy xuất lô / mẻ, /chatid (chat riêng + nhóm, chỉ quản trị).
    /// </summary>
    public class MessagingBot3Tests
    {
        private static long _u = 310000;
        private static readonly DateTime Today = VnTime.Today(TimeProvider.System);

        private static string Text(string text, long from = 7801, long? replyTo = null, long? groupChat = null)
        {
            long id = Interlocked.Increment(ref _u);
            string chat = groupChat is { } g ? "{\"id\":" + g + ",\"type\":\"group\",\"title\":\"Bếp\"}" : "{\"id\":" + from + ",\"type\":\"private\"}";
            string reply = replyTo == null ? "" : ",\"reply_to_message\":{\"message_id\":" + replyTo + ",\"date\":1,\"chat\":" + chat + "}";
            return "{\"update_id\":" + id + ",\"message\":{\"message_id\":" + id + ",\"date\":" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ",\"from\":{\"id\":" + from
                + ",\"first_name\":\"An\"},\"chat\":" + chat + ",\"text\":" + JsonSerializer.Serialize(text) + reply + "}}";
        }

        private static MessagingTestHost Host(bool admin = false, params string[] menus)
        {
            var h = new MessagingTestHost();
            h.Api.Identity = new BotIdentityModel { Linked = true, CanUseBot = true, HasWebAccount = true, IsAdmin = admin, UserId = Guid.NewGuid(), FullName = "Người dùng", Status = "Active", Menus = [.. menus] };
            h.Api.Token = new ApiResult<UserTokenModel> { ResponseCode = 200, Data = new UserTokenModel { Token = "jwt-u" } };
            return h;
        }

        private static string D(DateTime d) => d.ToString("yyyy-MM-dd");

        private static long LastSentId(MessagingTestHost h) => 100 + h.Sender.Sent.Count;

        [Fact]
        public async Task Shortage_only_and_today_materials_use_requirement_by_date()
        {
            await using var h = Host(false, "#/scm-requirement");
            object Item(string n, decimal s) => new { ingredientName = n, unitName = "kg", requiredQuantity = 10m, availableQuantity = 10m - s, shortageQuantity = s };
            h.Web.Routes["api/MaterialRequirement/GetByDate"] = (_, _) => (200, null, JsonSerializer.Serialize(new { planNumber = "KH-9", totalMeals = 500, shortageCount = 1, listItem = new[] { Item("Gạo", 0), Item("Thịt bò", 4) } }), null);
            await h.SendAsync(Text("/thieuhut"));
            h.Web.Calls.Last().Path.Should().Contain("productionDate=" + D(Today.AddDays(1)));
            string t = h.Sender.Sent.Last().Message.Text;
            t.Should().Contain("⚠️ Thiếu hụt").And.Contain("Thịt bò").And.NotContain("Gạo:");
            await h.SendAsync(Text("/nlca"));
            h.Web.Calls.Last().Path.Should().Contain("productionDate=" + D(Today));
            h.Sender.Sent.Last().Message.Text.Should().Contain("🥬 NL").And.Contain("Gạo");
            h.Web.Routes["api/MaterialRequirement/GetByDate"] = (_, _) => (200, null, JsonSerializer.Serialize(new { planNumber = "KH-9", listItem = new[] { Item("Gạo", 0) } }), null);
            await h.SendAsync(Text("/thieuhut"));
            h.Sender.Sent.Last().Message.Text.Should().Contain("không thiếu nguyên liệu nào");
        }

        [Fact]
        public async Task My_complaints_timesheet_and_pending_requests()
        {
            await using var h = Host(false, "#/log-complaint", "#/hr-attendance", "#/hr-leave", "#/hr-swap");
            h.Web.Routes["api/CustomerComplaint/GetList"] = (_, _) => (200, null, JsonSerializer.Serialize(new
            {
                listItem = new[] { new { complaint = new { complaintNo = "PA-1", receivedAt = D(Today) + "T08:00:00" }, customerName = "Trường A", statusName = "Mới", isOverdue = true, assignedToName = "CSKH Lan" } },
            }), null);
            await h.SendAsync(Text("/viecdabao"));
            h.Web.Calls.Last().Path.Should().Contain("mine=true");
            h.Sender.Sent.Last().Message.Text.Should().Contain("🔴 PA-1 Trường A").And.Contain("CSKH Lan");

            h.Web.Routes["api/AttendanceEvent/GetTimesheet"] = (_, _) => (200, null, null, JsonSerializer.Serialize(new object[]
            {
                new { fullName = "Bình", assignedShifts = 20, present = 18, absent = 2, late = 1, leaveDays = 0, workedHours = 150m },
                new { fullName = "Chi", assignedShifts = 20, present = 20, absent = 0, late = 0, leaveDays = 1, workedHours = 160m },
            }));
            await h.SendAsync(Text("/congthang"));
            h.Web.Calls.Last().Path.Should().Contain("FromDate=" + D(new DateTime(Today.Year, Today.Month, 1))).And.Contain("ToDate=" + D(Today));
            h.Sender.Sent.Last().Message.Text.Should().Contain("2 người · 40 ca · có mặt 38 · vắng 2 · muộn 1").And.Contain("• Bình · vắng 2").And.NotContain("• Chi");

            h.Web.Routes["api/LeaveRequest/GetAll"] = (_, _) => (200, null, null, JsonSerializer.Serialize(new[] { new { requestNo = "NP-1", employeeName = "Bình", fromDate = D(Today.AddDays(2)), toDate = D(Today.AddDays(3)), leaveTypeCode = "ANNUAL" } }));
            h.Web.Routes["api/ShiftSwapRequest/GetAll"] = (_, _) => (200, null, null, JsonSerializer.Serialize(new[] { new { requestNo = "DC-1", submittedAt = D(Today) } }));
            await h.SendAsync(Text("/donchoduyet"));
            h.Web.Calls.Should().Contain(c => c.Path.StartsWith("api/LeaveRequest/GetAll") && c.Path.Contains("RequestStatus=Pending"));
            h.Sender.Sent.Last().Message.Text.Should().Contain("NP-1 Bình").And.Contain("DC-1 đổi ca");
        }

        [Fact]
        public async Task Vehicle_schedule_tomorrow_and_boxes_today()
        {
            await using var h = Host(false, "#/log-route", "#/log-container-return");
            h.Web.Routes["api/DeliveryRoute/GetBoard"] = (_, _) => (200, null, JsonSerializer.Serialize(new
            {
                listRoute = new[] { new { route = new { id = Guid.NewGuid(), routeNo = "CX-1", status = "Planned" }, statusName = "Kế hoạch", licensePlate = "51C-123", driverName = "Tài xế Hùng", listStop = Array.Empty<object>() } },
            }), null);
            await h.SendAsync(Text("/lichxe"));
            h.Web.Calls.Last().Path.Should().Contain("DeliveryDate=" + D(Today.AddDays(1)));
            h.Sender.Sent.Last().Message.Text.Should().Contain("🧭 Lịch xe").And.Contain("51C-123").And.Contain("Tài xế Hùng");

            h.Web.Routes["api/ContainerReturn/GetList"] = (_, _) => (200, null, JsonSerializer.Serialize(new
            {
                listItem = new[]
                {
                    new { @return = new { expectedQuantity = 100m, returnedQuantity = 90m, damagedQuantity = 2m }, customerName = "Trường A", packagingType = "Khay", statusName = "Đã thu hồi" },
                    new { @return = new { expectedQuantity = 50m, returnedQuantity = 0m, damagedQuantity = 0m }, customerName = "Công ty B", packagingType = "Hộp", statusName = "Chờ thu hồi" },
                },
            }), null);
            await h.SendAsync(Text("/hopkhay"));
            h.Web.Calls.Last().Path.Should().Contain("from=" + D(Today)).And.Contain("to=" + D(Today));
            h.Sender.Sent.Last().Message.Text.Should().Contain("Phải thu 150 · đã thu 90 · hỏng 2 · còn thiếu 60").And.Contain("Trường A · Khay 90/100");
        }

        [Fact]
        public async Task Trace_lot_by_code_shows_forward_chain_and_unknown_code_is_reported()
        {
            await using var h = Host(false, "#/scm-lot");
            Guid lot = Guid.NewGuid();
            h.Web.Routes["api/Common/GlobalSearch"] = (path, _) => (200, null, JsonSerializer.Serialize(new
            {
                records = path.Contains("LO-261119")
                    ? new object[] { new { type = "Customer", id = Guid.NewGuid(), code = "KHB-1" }, new { type = "MaterialLot", id = lot, code = "LO-261119-00001" } }
                    : Array.Empty<object>(),
            }), null);
            h.Web.Routes["api/MaterialLot/Trace"] = (_, _) => (200, null, JsonSerializer.Serialize(new
            {
                listSupplier = new[] { new { number = "NC-00001", name = "NCC Thịt" } },
                listBatch = new[] { new { number = "ME-261119-TRUA-01" }, new { number = "ME-261119-TRUA-02" } },
                listCustomer = new[] { new { name = "Trường A" } },
                listWarning = new[] { "Lô đang tạm giữ" },
            }), null);
            await h.SendAsync(Text("/truyxuatlo"));
            h.Sender.Sent.Last().Message.Text.Should().Contain("Gõ số lô");
            await h.SendAsync(Text("LO-261119-00001", replyTo: LastSentId(h)));
            h.Web.Calls.Should().Contain(c => c.Path.StartsWith("api/MaterialLot/Trace") && c.Path.Contains(lot.ToString()));
            string t = h.Sender.Sent.Last().Message.Text;
            t.Should().Contain("🔍 Truy xuất LO-261119-00001").And.Contain("Truy xuôi").And.Contain("🍳 Mẻ (2): ME-261119-TRUA-01, ME-261119-TRUA-02").And.Contain("⚠️ Lô đang tạm giữ");
            await h.SendAsync(Text("/truyxuatlo"));
            await h.SendAsync(Text("XYZ-404", replyTo: LastSentId(h)));
            h.Sender.Sent.Last().Message.Text.Should().Contain("Không tìm thấy lô / mẻ");
        }

        [Fact]
        public async Task Chat_id_private_for_admin_and_in_group_only_admin_gets_reply()
        {
            await using var admin = Host(true);
            await admin.SendAsync(Text("/chatid", from: 7802));
            admin.Sender.Sent.Last().Message.Text.Should().Contain("Mã chat này: 7802");
            await admin.SendAsync(Text("/chatid@DelicareBot", from: 7802, groupChat: -100555));
            admin.Sender.Sent.Last().Message.ChatId.Should().Be("-100555");
            admin.Sender.Sent.Last().Message.Text.Should().Contain("Mã nhóm này: -100555");

            await using var user = Host(false);
            await user.SendAsync(Text("/chatid", from: 7803, groupChat: -100777));
            user.Sender.Sent.Should().NotContain(s => s.Message.Text.Contains("-100777"), "người không phải quản trị gõ trong nhóm → bot im lặng");
        }
    }
}
