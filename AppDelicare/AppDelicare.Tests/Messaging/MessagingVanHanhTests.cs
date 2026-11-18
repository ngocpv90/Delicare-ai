using System.Text.Json;
using AppDelicare.Messaging.Channels.Telegram.Models;
using AppDelicare.Messaging.Core;
using AppDelicare.Messaging.Core.Contracts;
using FluentAssertions;

namespace AppDelicare.Tests.Messaging
{
    /// <summary>
    /// BOT-1.5a (D-62, BOT_TELEGRAM §3.1, §3.2): phần chung (QR cổng → chấm công, 📅 lịch ca, 🏖 xin nghỉ từng bước bằng nút – web / chỉ EmployeeId),
    /// Bếp (mẻ cần làm → bắt đầu / nhiệt độ lõi nút số + gõ / xong mẻ điền sẵn số kế hoạch / lưu mẫu, 403 báo rõ, ẩn khi không có quyền màn),
    /// ATTP (checklist trong 1 tin – kết quả đã ghi điền sẵn, bấm từng mục, Hoàn tất → Record JSON; mẻ đang giữ). Mọi ghi đều qua api nghiệp vụ.
    /// </summary>
    public class MessagingVanHanhTests
    {
        private static readonly Guid Cooking = Guid.Parse("22222222-0000-4000-8000-00000000000a");
        private static readonly Guid Planned = Guid.Parse("22222222-0000-4000-8000-00000000000b");
        private static readonly Guid Checklist = Guid.Parse("22222222-0000-4000-8000-0000000000c1");
        private static readonly Guid Item1 = Guid.Parse("22222222-0000-4000-8000-0000000000d1");
        private static readonly Guid Item2 = Guid.Parse("22222222-0000-4000-8000-0000000000d2");
        private static long _u = 50000;

        private static string Text(string text, long from = 7001, long? replyTo = null)
        {
            long id = Interlocked.Increment(ref _u);
            string reply = replyTo == null ? "" : ",\"reply_to_message\":{\"message_id\":" + replyTo + ",\"date\":1,\"chat\":{\"id\":" + from + ",\"type\":\"private\"}}";
            return "{\"update_id\":" + id + ",\"message\":{\"message_id\":" + id + ",\"date\":" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ",\"from\":{\"id\":" + from
                + ",\"first_name\":\"An\"},\"chat\":{\"id\":" + from + ",\"type\":\"private\"},\"text\":" + JsonSerializer.Serialize(text) + reply + "}}";
        }

        private static string Tap(string data, long messageId = 50, long from = 7001)
        {
            long id = Interlocked.Increment(ref _u);
            return "{\"update_id\":" + id + ",\"callback_query\":{\"id\":\"cb" + id + "\",\"from\":{\"id\":" + from + "},\"data\":" + JsonSerializer.Serialize(data)
                + ",\"message\":{\"message_id\":" + messageId + ",\"date\":1,\"chat\":{\"id\":" + from + ",\"type\":\"private\"}}}}";
        }

        private static MessagingTestHost WebUserHost(params string[] menus)
        {
            var h = new MessagingTestHost();
            h.Api.Identity = new BotIdentityModel { Linked = true, CanUseBot = true, HasWebAccount = true, UserId = Guid.NewGuid(), FullName = "Tổ trưởng", Status = "Active", Menus = [.. menus] };
            h.Api.Token = new ApiResult<UserTokenModel> { ResponseCode = 200, Data = new UserTokenModel { Token = "jwt-user" } };
            return h;
        }

        private static (string Method, string Path, Dictionary<string, string> Form, string Token) Last(MessagingTestHost h, string path)
            => h.Web.Calls.Last(c => c.Path.Split('?')[0] == path);

        /// <summary>FakeSender đánh số tin gửi từ 101.</summary>
        private static long LastSentId(MessagingTestHost h) => 100 + h.Sender.Sent.Count;

        [Fact]
        public async Task Qr_gate_when_linked_opens_check_in_and_schedule_uses_my_shifts()
        {
            await using var h = WebUserHost();
            await h.SendAsync(Text("/start CHAMCONG"));
            h.Sender.Sent.Last().Message.ReplyMarkup.Should().BeOfType<TgReplyKeyboard>().Which.Keyboard[0][0].RequestLocation.Should().BeTrue();
            h.Web.Routes["api/ShiftAssignment/GetMine"] = (_, _) => (200, null, null, "[{\"workDate\":\"2031-06-03T00:00:00\",\"shiftName\":\"Ca sáng\",\"plannedStart\":\"2031-06-03T06:00:00\",\"plannedEnd\":\"2031-06-03T14:00:00\"}]");
            await h.SendAsync(Text("/lichca"));
            Last(h, "api/ShiftAssignment/GetMine").Token.Should().Be("jwt-user");
            h.Sender.Sent.Last().Message.Text.Should().Contain("Ca sáng: 06:00–14:00");
        }

        [Fact]
        public async Task Leave_request_step_by_step_with_buttons_then_reason_creates_request()
        {
            await using var h = WebUserHost();
            h.Web.Routes["api/LeaveRequest/Create"] = (_, _) => (200, "Đã gửi", "{\"request\":{\"requestNo\":\"DN-261118-0001\"}}", null);
            await h.SendAsync(Text("🏖 Xin nghỉ"));
            var types = (TgInlineKeyboard)h.Sender.Sent.Last().Message.ReplyMarkup!;
            types.InlineKeyboard.SelectMany(r => r).Select(b => b.CallbackData).Should().Contain("lv:t:ANNUAL");
            await h.SendAsync(Tap("lv:t:ANNUAL"));
            h.Sender.Edits.Last().Text.Should().Contain("Nghỉ từ ngày nào");
            await h.SendAsync(Tap("lv:d:1"));
            await h.SendAsync(Tap("lv:n:2"));
            h.Sender.Sent.Last().Message.ReplyMarkup.Should().BeOfType<TgForceReply>();
            h.Web.Calls.Should().NotContain(c => c.Path == "api/LeaveRequest/Create", "chưa có lý do");
            await h.SendAsync(Text("Đi khám bệnh", replyTo: LastSentId(h)));
            var call = Last(h, "api/LeaveRequest/Create");
            DateTime tomorrow = VnTime.Today(TimeProvider.System).AddDays(1);
            call.Form["FromDate"].Should().Be(tomorrow.ToString("yyyy-MM-dd"));
            call.Form["ToDate"].Should().Be(tomorrow.AddDays(1).ToString("yyyy-MM-dd"));
            call.Form["LeaveTypeCode"].Should().Be("ANNUAL");
            call.Form["Reason"].Should().Be("[Telegram] Đi khám bệnh");
            h.Sender.Sent.Last().Message.Text.Should().Contain("DN-261118-0001");
        }

        [Fact]
        public async Task Leave_for_employee_only_goes_through_bot_channel_with_typed_date()
        {
            await using var h = new MessagingTestHost();
            h.Api.Identity = new BotIdentityModel { Linked = true, CanUseBot = true, EmployeeId = Guid.NewGuid(), Status = "Active" };
            await h.SendAsync(Text("/nghi"));
            await h.SendAsync(Tap("lv:t:SICK"));
            await h.SendAsync(Tap("lv:d:x"));
            await h.SendAsync(Text("31/13", replyTo: LastSentId(h)));
            h.Sender.Sent.Last().Message.Text.Should().Contain("Ngày chưa đúng");
            await h.SendAsync(Text("25/12", replyTo: LastSentId(h)));
            await h.SendAsync(Tap("lv:n:1"));
            await h.SendAsync(Text("Sốt", replyTo: LastSentId(h)));
            var call = h.Api.EmployeeCalls.Should().ContainSingle(c => c.Action == "EmployeeCreateLeave").Subject;
            using var doc = JsonDocument.Parse(call.Json);
            doc.RootElement.GetProperty("fromDate").GetString().Should().EndWith("-12-25");
            doc.RootElement.GetProperty("leaveTypeCode").GetString().Should().Be("SICK");
            doc.RootElement.GetProperty("reason").GetString().Should().Be("Sốt");
        }

        private static MessagingTestHost KitchenHost(int writeCode = 200)
        {
            var h = WebUserHost("#/prod-batch", "#/qc-checklist-result");
            string board = JsonSerializer.Serialize(new
            {
                plannedPortions = 300, passedPortions = 0, statusCounts = new { Planned = 1, Cooking = 1 },
                listBatch = new object[]
                {
                    new { id = Cooking, batchNumber = "ME-261118-S1-01", dishName = "Canh chua", status = "Cooking", statusName = "Đang nấu", shiftCode = "SANG", plannedStart = "2031-06-02T07:00:00", plannedQuantity = 100m, sampleRetained = false },
                    new { id = Planned, batchNumber = "ME-261118-S1-02", dishName = "Cá kho", status = "Planned", statusName = "Kế hoạch", shiftCode = "SANG", plannedStart = "2031-06-02T08:00:00", plannedQuantity = 200m, sampleRetained = false, qcStatus = "HOLD" },
                },
            });
            h.Web.Routes["api/ProductionBatch/GetBoard"] = (_, _) => (200, null, board, null);
            foreach (var a in new[] { "Start", "Finish", "RecordCoreTemperature", "RecordSampleRetained" })
            {
                h.Web.Routes["api/ProductionBatch/" + a] = (_, _) => (writeCode, writeCode == 200 ? "Đã ghi " + a : "HTTP 403", "{}", null);
            }
            return h;
        }

        [Fact]
        public async Task Kitchen_batch_card_start_core_temperature_finish_with_prefilled_quantity()
        {
            await using var h = KitchenHost();
            await h.SendAsync(Text("🍳 Mẻ cần làm"));
            var list = h.Sender.Sent.Last().Message;
            list.Text.Should().Contain("Mẻ cần làm hôm nay (2)");
            string cook = Cooking.ToString("N"), plan = Planned.ToString("N");
            await h.SendAsync(Tap("bat:view:" + cook));
            var card = h.Sender.Edits.Last();
            ((TgInlineKeyboard)card.Markup!).InlineKeyboard.SelectMany(r => r).Select(b => b.CallbackData).Should().Contain(["bat:t:" + cook, "bat:fin:" + cook, "bat:smp:" + cook]);
            await h.SendAsync(Tap("bat:t:" + cook));
            ((TgInlineKeyboard)h.Sender.Edits.Last().Markup!).InlineKeyboard.SelectMany(r => r).Select(b => b.CallbackData).Should().Contain("bat:tv:" + cook + ".72");
            await h.SendAsync(Tap("bat:tv:" + cook + ".72"));
            Last(h, "api/ProductionBatch/RecordCoreTemperature").Form.Should().Contain(new KeyValuePair<string, string>("TemperatureC", "72")).And.Contain(new KeyValuePair<string, string>("Id", Cooking.ToString()));
            h.Sender.Edits.Last().Text.Should().Contain("Đã ghi RecordCoreTemperature");
            await h.SendAsync(Tap("bat:tx:" + cook));
            await h.SendAsync(Text("73,5", replyTo: LastSentId(h)));
            Last(h, "api/ProductionBatch/RecordCoreTemperature").Form["TemperatureC"].Should().Be("73.5");
            await h.SendAsync(Tap("bat:fin:" + cook));
            ((TgInlineKeyboard)h.Sender.Edits.Last().Markup!).InlineKeyboard.SelectMany(r => r).First().Text.Should().Be("✅ Đúng 100", "điền sẵn số kế hoạch");
            await h.SendAsync(Tap("bat:fq:" + cook + ".100"));
            Last(h, "api/ProductionBatch/Finish").Form["ActualQuantity"].Should().Be("100");
            await h.SendAsync(Tap("bat:smp:" + cook));
            Last(h, "api/ProductionBatch/RecordSampleRetained").Form["Id"].Should().Be(Cooking.ToString());
            await h.SendAsync(Tap("bat:start:" + plan));
            Last(h, "api/ProductionBatch/Start").Form["Id"].Should().Be(Planned.ToString());
            await h.SendAsync(Text("/tiendo"));
            h.Sender.Sent.Last().Message.Text.Should().Contain("0 / 300").And.Contain("Đang nấu: 1");
        }

        [Fact]
        public async Task Kitchen_write_denied_is_reported_and_functions_hidden_without_screen_right()
        {
            await using var h = KitchenHost(writeCode: 403);
            await h.SendAsync(Tap("bat:start:" + Planned.ToString("N")));
            h.Sender.Edits.Last().Text.Should().Contain("chưa có quyền");
            await using var noRight = WebUserHost();
            await noRight.SendAsync(Text("🍳 Mẻ cần làm"));
            noRight.Web.Calls.Should().NotContain(c => c.Path.StartsWith("api/ProductionBatch"), "không có quyền #/prod-batch → không có chức năng Bếp (chữ gõ chỉ đi tra cứu)");
        }

        [Fact]
        public async Task Food_safety_checklist_in_one_message_records_pass_fail_and_hold_list()
        {
            await using var h = KitchenHost();
            h.Web.Routes["api/FoodSafetyChecklist/GetAllData"] = (_, _) => (200, null, null, JsonSerializer.Serialize(new[] { new { id = Checklist, code = "PREP", name = "Sơ chế", status = "Active" } }));
            h.Web.Routes["api/FoodSafetyChecklistResult/GetRun"] = (_, _) => (200, null, JsonSerializer.Serialize(new
            {
                checklist = new { name = "Sơ chế" },
                listItem = new object[]
                {
                    new { item = new { id = Item1, itemName = "Rửa rau 3 lần", isCritical = false }, result = new { resultCode = "PASS" } },
                    new { item = new { id = Item2, itemName = "Khử khuẩn dao thớt", isCritical = true }, result = (object?)null },
                },
            }), null);
            h.Web.Routes["api/FoodSafetyChecklistResult/Record"] = (_, _) => (200, "Đã ghi 2 tiêu chí", "{}", null);
            await h.SendAsync(Text("/checklist"));
            await h.SendAsync(Tap("chk:open:" + Checklist.ToString("N"), messageId: 90));
            var first = h.Sender.Edits.Last();
            first.Text.Should().Contain("Sơ chế");
            var buttons = ((TgInlineKeyboard)first.Markup!).InlineKeyboard.SelectMany(r => r).ToList();
            buttons[0].Text.Should().StartWith("✅").And.Contain("Rửa rau", "kết quả đã ghi điền sẵn");
            buttons[1].Text.Should().StartWith("⬜").And.Contain("❗Khử khuẩn");
            buttons.Should().HaveCount(2, "chưa chọn hết → chưa có Hoàn tất");
            await h.SendAsync(Tap("chk:1:0", messageId: 90));
            var after = ((TgInlineKeyboard)h.Sender.Edits.Last().Markup!).InlineKeyboard.SelectMany(r => r).ToList();
            after[1].Text.Should().StartWith("❌");
            after.Last().CallbackData.Should().Be("chk:done");
            await h.SendAsync(Tap("chk:done", messageId: 90));
            string json = Last(h, "api/FoodSafetyChecklistResult/Record").Form["__json"];
            json.Should().Contain(Checklist.ToString()).And.Contain("\"resultCode\":\"PASS\"").And.Contain("\"resultCode\":\"FAIL\"");
            h.Sender.Edits.Last().Text.Should().Contain("1 đạt, 1 không đạt");

            await h.SendAsync(Text("⛔ Mẻ đang giữ"));
            h.Sender.Sent.Last().Message.Text.Should().Contain("(1)").And.Contain("ME-261118-S1-02");
        }
    }
}
