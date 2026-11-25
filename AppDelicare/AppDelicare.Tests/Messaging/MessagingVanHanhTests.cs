using System.Text.Json;
using AppDelicare.Messaging.Channels.Telegram.Models;
using AppDelicare.Messaging.Core;
using AppDelicare.Messaging.Core.Contracts;
using AppDelicare.Messaging.Experience;
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
            // BOT-UX-2 (mục 5): vẫn đúng 3 phím, phím 📍 Chấm công xin vị trí (không đổi thành bàn phím 1 nút).
            var kb = h.Sender.Sent.Last().Message.ReplyMarkup.Should().BeOfType<TgReplyKeyboard>().Subject;
            kb.Keyboard.SelectMany(r => r).Select(b => b.Text).Should().Equal(KeyboardBuilder.MyShift, KeyboardBuilder.Functions, KeyboardBuilder.CheckIn);
            kb.Keyboard[0][2].RequestLocation.Should().BeTrue();
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
            // BOT-UX-2 (mục 6): "Khác…" số ngày = màn số (không gõ); chọn 2 ngày.
            await h.SendAsync(Tap("lv:nn:5"));
            ((TgInlineKeyboard)h.Sender.Edits.Last().Markup!).InlineKeyboard.SelectMany(r => r).Select(b => b.CallbackData).Should().Contain(["lv:nn:4", "lv:nn:6", "lv:n:5"]);
            await h.SendAsync(Tap("lv:n:2"));
            // BOT-UX-2 (mục 7): lý do = nút lý do hay gặp + [✏️ Khác…].
            ((TgInlineKeyboard)h.Sender.Edits.Last().Markup!).InlineKeyboard.SelectMany(r => r).Select(b => b.CallbackData).Should().Contain(["lv:r:k.1", "lv:rx:k"]);
            h.Web.Calls.Should().NotContain(c => c.Path == "api/LeaveRequest/Create", "chưa có lý do");
            await h.SendAsync(Tap("lv:rx:k"));
            h.Sender.Sent.Last().Message.ReplyMarkup.Should().BeOfType<TgForceReply>();
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
            // UI-6.49 (1): trước khi chọn lý do – phép năm còn / ca bị bỏ (BotChannel/EmployeePreviewLeave).
            h.Api.EmployeeData["EmployeePreviewLeave"] = JsonSerializer.Serialize(new { isAnnual = true, annualTotal = 12, annualRemaining = 9, annualAfter = 8, listShiftDrop = new[] { new { shiftCode = "SANG", workDate = "2026-12-25T00:00:00" } } });
            await h.SendAsync(Tap("lv:n:1"));
            string preview = string.Join(" | ", h.Sender.Edits.Select(e => e.Text)) + string.Join(" | ", h.Sender.Sent.Select(x => x.Message.Text));
            preview.Should().Contain("Phép năm còn 9/12 ngày → sau đơn còn 8").And.Contain("bỏ 1 ca: SANG 25/12");
            await h.SendAsync(Tap("lv:r:k.1"));
            var call = h.Api.EmployeeCalls.Should().ContainSingle(c => c.Action == "EmployeeCreateLeave").Subject;
            using var doc = JsonDocument.Parse(call.Json);
            doc.RootElement.GetProperty("fromDate").GetString().Should().EndWith("-12-25");
            doc.RootElement.GetProperty("leaveTypeCode").GetString().Should().Be("SICK");
            doc.RootElement.GetProperty("reason").GetString().Should().Be("Ốm / khám bệnh", "bấm lý do có sẵn");
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
            // BOT-UX-2 (mục 11): mặc định ca hiện tại (mẻ đang nấu), [📋 Xem tất cả (2)] mở cả mẻ kế hoạch ngày khác giờ.
            list.Text.Should().Contain("Mẻ cần làm hôm nay (1 · ca hiện tại)");
            string cook = Cooking.ToString("N"), plan = Planned.ToString("N");
            ((TgInlineKeyboard)list.ReplyMarkup!).InlineKeyboard.SelectMany(r => r).Should().Contain(b => b.Text == "📋 Xem tất cả (2)" && b.CallbackData == "pg:bep_batches:0.all");
            await h.SendAsync(Tap("pg:bep_batches:0.all"));
            h.Sender.Edits.Last().Text.Should().Contain("Mẻ cần làm hôm nay (2)");
            await h.SendAsync(Tap("bat:view:" + cook));
            var card = h.Sender.Edits.Last();
            // BOT-UX-2: nhiệt độ lõi bấm 1 chạm ngay trên thẻ mẻ + "🌡 Nhiệt độ khác" (màn số).
            ((TgInlineKeyboard)card.Markup!).InlineKeyboard.SelectMany(r => r).Select(b => b.CallbackData).Should().Contain(["bat:tv:" + cook + ".72", "bat:tn:" + cook, "bat:fin:" + cook, "bat:smp:" + cook]);
            await h.SendAsync(Tap("bat:tv:" + cook + ".72"));
            Last(h, "api/ProductionBatch/RecordCoreTemperature").Form.Should().Contain(new KeyValuePair<string, string>("TemperatureC", "72")).And.Contain(new KeyValuePair<string, string>("Id", Cooking.ToString()));
            h.Sender.Edits.Last().Text.Should().StartWith("✅ Đã ghi nhiệt độ lõi 72 °C ·", "dòng kết quả ở đầu thẻ");
            ((TgInlineKeyboard)h.Sender.Edits.Last().Markup!).InlineKeyboard.SelectMany(r => r).Select(b => b.CallbackData).Should().Contain("bat:fin:" + cook, "ghi xong vẫn giữ nút bước kế tiếp");
            // Màn số: điền sẵn 75 (chưa đo), −1 → 74, ✅ Ghi 74 °C.
            await h.SendAsync(Tap("bat:tn:" + cook));
            ((TgInlineKeyboard)h.Sender.Edits.Last().Markup!).InlineKeyboard.SelectMany(r => r).Select(b => b.CallbackData).Should().Contain(["bat:tn:" + cook + ".74", "bat:tv:" + cook + ".75"]);
            int writes = h.Web.Calls.Count(c => c.Method == "POST");
            await h.SendAsync(Tap("bat:tn:" + cook + ".74"));
            h.Web.Calls.Count(c => c.Method == "POST").Should().Be(writes, "chỉnh số trên màn số không ghi gì");
            await h.SendAsync(Tap("bat:tv:" + cook + ".74"));
            Last(h, "api/ProductionBatch/RecordCoreTemperature").Form["TemperatureC"].Should().Be("74");
            // Nút cũ (gõ tay) vẫn nhận.
            await h.SendAsync(Tap("bat:tx:" + cook));
            await h.SendAsync(Text("73,5", replyTo: LastSentId(h)));
            Last(h, "api/ProductionBatch/RecordCoreTemperature").Form["TemperatureC"].Should().Be("73.5");

            // UG-1 C-09 / BOT-UX-2 (mục 7): dưới ngưỡng → máy chủ đòi hành động khắc phục → bấm hành động hay gặp (không gõ) → ghi lại kèm CorrectiveAction.
            h.Web.Routes["api/ProductionBatch/RecordCoreTemperature"] = (_, form) => form.ContainsKey("CorrectiveAction")
                ? (200, "Đã ghi nhiệt độ (dưới ngưỡng)", "{}", null)
                : (400, "Nhiệt độ lõi 65 °C dưới ngưỡng 70 °C – phải ghi hành động khắc phục", null, null);
            await h.SendAsync(Tap("bat:tv:" + cook + ".65"));
            h.Sender.Edits.Last().Text.Should().Contain("hành động khắc phục");
            ((TgInlineKeyboard)h.Sender.Edits.Last().Markup!).InlineKeyboard.SelectMany(r => r).Select(b => b.CallbackData).Should().Contain(["bat:fixr:" + cook + ".65.0", "bat:fixx:" + cook + ".65"]);
            await h.SendAsync(Tap("bat:fixr:" + cook + ".65.0"));
            Last(h, "api/ProductionBatch/RecordCoreTemperature").Form.Should().Contain(new KeyValuePair<string, string>("CorrectiveAction", "Gia nhiệt lại 10 phút, đo lại"))
                .And.Contain(new KeyValuePair<string, string>("TemperatureC", "65"));
            // [✏️ Khác…] → gõ tay.
            await h.SendAsync(Tap("bat:fixx:" + cook + ".65"));
            await h.SendAsync(Text("Gia nhiệt lại 15 phút", replyTo: LastSentId(h)));
            Last(h, "api/ProductionBatch/RecordCoreTemperature").Form["CorrectiveAction"].Should().Be("Gia nhiệt lại 15 phút");

            // UI-6.49 (1): ✅ Xong mẻ = ProductionBatch/Complete như web – chưa đo nhiệt độ lõi thì hỏi ngay (bước 1/2), rồi số suất; dưới ngưỡng → chọn hành động khắc phục.
            h.Web.Routes["api/ProductionBatch/Complete"] = (_, form) => form["__json"].Contains("\"correctiveAction\":\"")
                || form["__json"].Contains("\"coreTemperatureC\":78") ? (200, "Đã xong mẻ – tự Đạt", "{}", null)
                : (400, "Nhiệt độ lõi 65 °C dưới ngưỡng 70 °C – phải ghi hành động khắc phục (nấu tiếp / hâm lại rồi đo lại)", null, null);
            await h.SendAsync(Tap("bat:fin:" + cook));
            h.Sender.Edits.Last().Text.Should().Contain("bước 1/2: nhiệt độ lõi");
            await h.SendAsync(Tap("bat:ftv:" + cook + ".78"));
            ((TgInlineKeyboard)h.Sender.Edits.Last().Markup!).InlineKeyboard.SelectMany(r => r).Should().Contain(b => b.Text == "✅ Xác nhận 100 suất" && b.CallbackData == "bat:fq:" + cook + ".100", "điền sẵn số kế hoạch");
            await h.SendAsync(Tap("bat:fq:" + cook + ".100"));
            string done = Last(h, "api/ProductionBatch/Complete").Form["__json"];
            done.Should().Contain("\"coreTemperatureC\":78").And.Contain("\"producedQuantity\":100").And.Contain(Cooking.ToString());
            h.Web.Calls.Should().NotContain(c => c.Path == "api/ProductionBatch/Finish" || c.Path == "api/ProductionBatch/RecordOutput", "không còn 2 lệnh lẻ");
            // Dưới ngưỡng → nút hành động khắc phục → Complete kèm correctiveAction.
            await h.SendAsync(Tap("bat:fin:" + cook));
            await h.SendAsync(Tap("bat:ftv:" + cook + ".65"));
            await h.SendAsync(Tap("bat:fq:" + cook + ".100"));
            h.Sender.Edits.Last().Text.Should().Contain("dưới ngưỡng");
            await h.SendAsync(Tap("bat:fcr:" + cook + ".100.0"));
            using (var cj = JsonDocument.Parse(Last(h, "api/ProductionBatch/Complete").Form["__json"])) { cj.RootElement.GetProperty("correctiveAction").GetString().Should().Be("Gia nhiệt lại 10 phút, đo lại"); cj.RootElement.GetProperty("coreTemperatureC").GetDecimal().Should().Be(65); }
            await h.SendAsync(Tap("bat:smp:" + cook));
            Last(h, "api/ProductionBatch/RecordSampleRetained").Form["Id"].Should().Be(Cooking.ToString());
            await h.SendAsync(Tap("bat:start:" + plan));
            Last(h, "api/ProductionBatch/Start").Form["Id"].Should().Be(Planned.ToString());
            await h.SendAsync(Text("/tiendo"));
            h.Sender.Sent.Last().Message.Text.Should().Contain("0 / 300").And.Contain("Đang nấu: 1");
        }

        [Fact]
        public async Task Kitchen_batch_card_shows_current_stage_button_start_then_finish()
        {
            // UG-1 C-15: mẻ Đang nấu có công đoạn → nút Bắt đầu công đoạn kế tiếp / Xong công đoạn đang làm.
            await using var h = KitchenHost();
            string cook = Cooking.ToString("N"), state = "NotStarted";
            h.Web.Routes["api/ProductionBatch/GetDetail"] = (_, _) => (200, null, JsonSerializer.Serialize(new
            {
                id = Cooking,
                listStage = new object[]
                {
                    new { stageCode = "PREP", stageName = "Sơ chế", status = "Done" },
                    new { stageCode = "COOK", stageName = "Nấu", status = state },
                    new { stageCode = "PORTION", stageName = "Chia suất", status = "NotStarted" },
                },
            }), null);
            h.Web.Routes["api/ProductionBatch/StartStage"] = (_, _) => { state = "InProgress"; return (200, "Đã bắt đầu công đoạn", "{}", null); };
            h.Web.Routes["api/ProductionBatch/FinishStage"] = (_, _) => { state = "Done"; return (200, "Đã xong công đoạn", "{}", null); };

            await h.SendAsync(Tap("bat:view:" + cook));
            var card = h.Sender.Edits.Last();
            card.Text.Should().Contain("Công đoạn: Sơ chế ✅ → Nấu → Chia suất");
            var buttons = ((TgInlineKeyboard)card.Markup!).InlineKeyboard.SelectMany(r => r).ToList();
            buttons.Should().Contain(b => b.Text == "▶️ Bắt đầu Nấu" && b.CallbackData == "bat:ss:" + cook + ".COOK");

            await h.SendAsync(Tap("bat:ss:" + cook + ".COOK"));
            Last(h, "api/ProductionBatch/StartStage").Form.Should().Contain(new KeyValuePair<string, string>("StageCode", "COOK")).And.Contain(new KeyValuePair<string, string>("Id", Cooking.ToString()));
            card = h.Sender.Edits.Last();
            card.Text.Should().Contain("Nấu ⏳");
            ((TgInlineKeyboard)card.Markup!).InlineKeyboard.SelectMany(r => r).Should().Contain(b => b.Text == "⏹ Xong Nấu" && b.CallbackData == "bat:sf:" + cook + ".COOK");

            await h.SendAsync(Tap("bat:sf:" + cook + ".COOK"));
            Last(h, "api/ProductionBatch/FinishStage").Form["StageCode"].Should().Be("COOK");
            ((TgInlineKeyboard)h.Sender.Edits.Last().Markup!).InlineKeyboard.SelectMany(r => r).Should().Contain(b => b.Text == "▶️ Bắt đầu Chia suất");
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
        public async Task Ui649_temperature_round_records_manual_zones_with_corrective_for_out_of_range()
        {
            await using var h = WebUserHost("#/qc-temperature");
            Guid cold = Guid.NewGuid(), hot = Guid.NewGuid(), sensor = Guid.NewGuid();
            h.Web.Routes["api/TemperatureLog/GetDayBoard"] = (_, _) => (200, null, JsonSerializer.Serialize(new
            {
                listZone = new object[]
                {
                    new { storageZoneId = cold, zoneName = "Kho lạnh", temperatureMin = 0m, temperatureMax = 5m, hasSensor = false, status = "DUE" },
                    new { storageZoneId = hot, zoneName = "Tủ giữ nóng", temperatureMin = 65m, temperatureMax = 90m, hasSensor = true, status = "SILENT" },
                    new { storageZoneId = sensor, zoneName = "Kho mát", temperatureMin = 0m, temperatureMax = 8m, hasSensor = true, status = "OK" },
                },
            }), null);
            h.Web.Routes["api/TemperatureLog/RecordRound"] = (_, _) => (200, "Đã ghi 2 khu", "{}", null);
            await h.SendAsync(Text("/nhietdo"));
            var list = h.Sender.Sent.Last().Message;
            list.Text.Should().Contain("Kho lạnh").And.Contain("Tủ giữ nóng").And.NotContain("Kho mát", "khu có cảm biến đang gửi số không cần ghi tay");
            await h.SendAsync(Tap("chk:tzv:0.4"));
            await h.SendAsync(Tap("chk:tzv:1.60"));
            h.Sender.Edits.Last().Text.Should().Contain("ngoài ngưỡng");
            await h.SendAsync(Tap("chk:ts"));
            h.Sender.Sent.Last().Message.Text.Should().Contain("chưa chọn hành động khắc phục");
            await h.SendAsync(Tap("chk:tf:1.0"));
            await h.SendAsync(Tap("chk:ts"));
            using var doc = JsonDocument.Parse(Last(h, "api/TemperatureLog/RecordRound").Form["__json"]);
            var items = doc.RootElement.GetProperty("listItem").EnumerateArray().ToList();
            items.Should().HaveCount(2);
            items[0].GetProperty("temperatureC").GetDecimal().Should().Be(4);
            items[1].GetProperty("correctiveAction").GetString().Should().Be("Chỉnh nhiệt độ thiết bị, đo lại sau 30 phút");
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
            buttons.Should().HaveCount(3, "chưa chọn hết → chưa có Hoàn tất, có Đạt tất cả (UI-6.45)");
            buttons.Last().CallbackData.Should().Be("chk:all");
            await h.SendAsync(Tap("chk:all", messageId: 90));
            ((TgInlineKeyboard)h.Sender.Edits.Last().Markup!).InlineKeyboard.SelectMany(r => r).Take(2).Should().OnlyContain(b => b.Text.StartsWith("✅"));
            await h.SendAsync(Tap("chk:1:0", messageId: 90));
            var after = ((TgInlineKeyboard)h.Sender.Edits.Last().Markup!).InlineKeyboard.SelectMany(r => r).ToList();
            after[1].Text.Should().StartWith("❌");
            after.Last().CallbackData.Should().Be("chk:done");
            await h.SendAsync(Tap("chk:done", messageId: 90));
            // UI-6.45 (UI-6.30): tiêu chí trọng yếu Không đạt → hỏi mẻ bị ảnh hưởng trước khi ghi.
            h.Web.Calls.Should().NotContain(c => c.Path.StartsWith("api/FoodSafetyChecklistResult/Record"));
            h.Sender.Edits.Last().Text.Should().Contain("mẻ nào bị ảnh hưởng");
            await h.SendAsync(Tap("chk:nb", messageId: 90));
            string json = Last(h, "api/FoodSafetyChecklistResult/Record").Form["__json"];
            json.Should().Contain(Checklist.ToString()).And.Contain("\"resultCode\":\"PASS\"").And.Contain("\"resultCode\":\"FAIL\"").And.Contain("noBatchReason");
            h.Sender.Edits.Last().Text.Should().Contain("1 đạt, 1 không đạt");

            await h.SendAsync(Text("⛔ Mẻ đang giữ"));
            h.Sender.Sent.Last().Message.Text.Should().Contain("(1)").And.Contain("ME-261118-S1-02");
        }
    }
}
