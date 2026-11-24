using System.Globalization;
using System.Text.Json;
using AppDelicare.Messaging.Audiences.Internal;
using AppDelicare.Messaging.Channels.Telegram.Models;
using AppDelicare.Messaging.Core;
using AppDelicare.Messaging.Core.Contracts;
using AppDelicare.Messaging.Experience;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Messaging
{
    /// <summary>
    /// BOT-UX-2 (anh chốt 2026-11-23): thao tác bot Vận hành tối đa 3 chạm, không gõ tay – có bảng điều khiển (Telegram giả):
    /// tổ trưởng bếp ghi nhiệt độ lõi từ ⭐ = 3 chạm, tài xế giao đủ 1 điểm = 3 chạm, chấm công 1 chạm 📍 và 3 phím vẫn còn, mỗi màn đúng 1 nút Quay lại,
    /// thẻ mẻ › nhiệt độ › ◀ → thẻ mẻ (không ghi gì), mọi nút bấm có chữ trả lời, ghi xong thẻ còn nút bước kế, danh sách 13 mục → 3 trang,
    /// người ≤ 6 chức năng → màn đầu phẳng, không còn nút "·", không chức năng trùng phân hệ.
    /// </summary>
    public class MessagingBotUx2Tests
    {
        private const long Me = 7301;
        private static readonly Guid Cook = Guid.Parse("33333333-0000-4000-8000-000000000001");
        private static readonly Guid Route = Guid.Parse("33333333-0000-4000-8000-000000000010");
        private static readonly Guid Stop1 = Guid.Parse("33333333-0000-4000-8000-000000000011");
        private static long _u = 9000;

        private static (string Json, long Id) Text(string text, long? replyTo = null)
        {
            long id = Interlocked.Increment(ref _u);
            string reply = replyTo == null ? "" : ",\"reply_to_message\":{\"message_id\":" + replyTo + ",\"date\":1,\"chat\":{\"id\":" + Me + ",\"type\":\"private\"}}";
            return ("{\"update_id\":" + id + ",\"message\":{\"message_id\":" + id + ",\"date\":" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ",\"from\":{\"id\":" + Me
                + ",\"first_name\":\"An\"},\"chat\":{\"id\":" + Me + ",\"type\":\"private\"},\"text\":" + JsonSerializer.Serialize(text) + reply + "}}", id);
        }

        private static string Tap(string data, string messageId)
        {
            long id = Interlocked.Increment(ref _u);
            return "{\"update_id\":" + id + ",\"callback_query\":{\"id\":\"cb" + id + "\",\"from\":{\"id\":" + Me + "},\"data\":" + JsonSerializer.Serialize(data)
                + ",\"message\":{\"message_id\":" + messageId + ",\"date\":1,\"chat\":{\"id\":" + Me + ",\"type\":\"private\"}}}}";
        }

        private static string Location()
        {
            long id = Interlocked.Increment(ref _u);
            return "{\"update_id\":" + id + ",\"message\":{\"message_id\":" + id + ",\"date\":" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ",\"from\":{\"id\":" + Me
                + "},\"chat\":{\"id\":" + Me + ",\"type\":\"private\"},\"location\":{\"latitude\":10.77,\"longitude\":106.7}}}";
        }

        private static MessagingTestHost Host(params string[] menus)
        {
            var h = new MessagingTestHost(new Dictionary<string, string?> { ["Messaging:Panel:Enabled"] = "true" });
            h.Api.Identity = new BotIdentityModel { Linked = true, CanUseBot = true, Status = "Active", HasWebAccount = true, UserId = Guid.NewGuid(), FullName = "An", Menus = [.. menus] };
            h.Api.Token = new ApiResult<UserTokenModel> { ResponseCode = 200, Data = new UserTokenModel { Token = "jwt", ExpiresAt = DateTime.UtcNow.AddMinutes(10) } };
            return h;
        }

        private static PanelState State(MessagingTestHost h) => h.App.Services.GetRequiredService<PanelRegistry>().Get("VH", Me.ToString());

        private static string Panel(MessagingTestHost h) => State(h).MessageId!;

        private static string PanelText(MessagingTestHost h)
        {
            string id = Panel(h);
            var edit = h.Sender.Edits.LastOrDefault(e => e.MessageId == id);
            return edit.Text ?? h.Sender.Log.Last(l => l.Id == id).Message.Text;
        }

        private static List<TgInlineButton> PanelButtons(MessagingTestHost h)
        {
            string id = Panel(h);
            var edit = h.Sender.Edits.LastOrDefault(e => e.MessageId == id);
            var markup = (edit.Markup ?? h.Sender.Log.Last(l => l.Id == id).Message.ReplyMarkup) as TgInlineKeyboard;
            return [.. markup!.InlineKeyboard.SelectMany(r => r)];
        }

        private static List<string?> PanelCallbacks(MessagingTestHost h) => [.. PanelButtons(h).Select(b => b.CallbackData)];

        /// <summary>Số nút quay lại trên panel: ◀ của hàng điều hướng + mọi nút "Quay lại" / "⬅️" trong nội dung.</summary>
        private static int Backs(MessagingTestHost h) => PanelButtons(h).Count(b => b.CallbackData == "nav:back" || b.Text.Contains("Quay lại") || b.Text.Contains("⬅️"));

        private static int Posts(MessagingTestHost h) => h.Web.Calls.Count(c => c.Method == "POST");

        private static string Now(int addMinutes = 0) => VnTime.Now(TimeProvider.System).AddMinutes(addMinutes).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

        private static object Batch(Guid id, string no, string status = "Cooking", int minutes = 0)
            => new { id, batchNumber = no, dishName = "Canh chua", status, statusName = status == "Cooking" ? "Đang nấu" : "Kế hoạch", shiftCode = "SANG", plannedStart = Now(minutes), plannedQuantity = 100m, sampleRetained = false };

        private static MessagingTestHost KitchenLead(IEnumerable<object>? batches = null)
        {
            // Tổ trưởng bếp: Bếp (mẻ, tiến độ, NL của ca, ai chưa vào tổ) + Kho / Kế hoạch / Nhân sự / ATTP → > 6 chức năng → màn đầu theo phân hệ.
            var h = Host("#/prod-batch", "#/hr-shift-assign", "#/scm-requirement");
            string board = JsonSerializer.Serialize(new { listBatch = batches ?? [Batch(Cook, "ME-0123")] });
            h.Web.Routes["api/ProductionBatch/GetBoard"] = (_, _) => (200, null, board, null);
            h.Web.Routes["api/ProductionBatch/RecordCoreTemperature"] = (_, form) => (200, "Đã ghi nhiệt độ " + form["TemperatureC"], "{}", null);
            return h;
        }

        [Fact]
        public async Task Kitchen_lead_records_core_temperature_in_three_taps_from_favourites_without_typing()
        {
            await using var h = KitchenLead();
            await h.SendAsync(Text("/me").Json);       // dùng 1 lần → ⭐ Hay dùng
            await h.SendAsync(Text("/menu").Json);
            var fav = PanelButtons(h).Where(b => b.Text.StartsWith('⭐')).ToList();
            fav.Should().ContainSingle().Which.CallbackData.Should().Be("menu:act:bep_batches");
            PanelButtons(h).Should().NotContain(b => b.Text == "·", "không còn ô \"·\" chèn lưới");
            int answered = h.Sender.Answered;

            await h.SendAsync(Tap("menu:act:bep_batches", Panel(h)));            // chạm 1
            PanelText(h).Should().StartWith("<i>Menu › 🍳 Bếp › 🍳 Mẻ cần làm</i>");
            await h.SendAsync(Tap("bat:view:" + Cook.ToString("N"), Panel(h)));  // chạm 2
            PanelText(h).Should().StartWith("<i>Menu › 🍳 Bếp › 🍳 Mẻ cần làm › 🔥 ME-0123</i>", "đường dẫn đủ cấp");
            await h.SendAsync(Tap("bat:tv:" + Cook.ToString("N") + ".78", Panel(h))); // chạm 3

            var call = h.Web.Calls.Last(c => c.Path == "api/ProductionBatch/RecordCoreTemperature");
            call.Form["TemperatureC"].Should().Be("78");
            h.Sender.Log.Should().NotContain(l => l.Message.ReplyMarkup is TgForceReply, "không gõ tay");
            h.Sender.Answered.Should().Be(answered + 3);
            h.Sender.AnswerTexts.Last().Should().Be("✅ Đã ghi 78 °C");
            PanelText(h).Should().Contain("✅ Đã ghi nhiệt độ lõi 78 °C · ");
            PanelCallbacks(h).Should().Contain(["bat:fin:" + Cook.ToString("N"), "bat:smp:" + Cook.ToString("N")], "ghi xong thẻ vẫn còn nút bước kế tiếp (Xong mẻ, Lưu mẫu)");
            PanelCallbacks(h).Should().NotContain("nav:cont", "còn bước → không thành phiếu kết quả");
        }

        [Fact]
        public async Task Back_from_temperature_screen_returns_to_batch_card_and_writes_nothing()
        {
            await using var h = KitchenLead();
            await h.SendAsync(Text("/me").Json);
            await h.SendAsync(Tap("bat:view:" + Cook.ToString("N"), Panel(h)));
            await h.SendAsync(Tap("bat:tn:" + Cook.ToString("N"), Panel(h)));
            PanelText(h).Should().Contain("👉 75 °C", "giá trị điền sẵn hiện trên panel");
            await h.SendAsync(Tap("bat:tn:" + Cook.ToString("N") + ".76", Panel(h)));
            PanelText(h).Should().Contain("👉 76 °C");
            State(h).Stack.Count(e => e.Level == PanelLevel.Screen).Should().Be(2, "bấm +1 trên cùng màn số không đẩy thêm màn");
            int posts = Posts(h);

            await h.SendAsync(Tap("nav:back", Panel(h)));
            PanelText(h).Should().Contain("<b>🔥 ME-0123 – Canh chua</b>", "◀ về thẻ mẻ, không về danh sách");
            PanelText(h).Should().StartWith("<i>Menu › 🍳 Bếp › 🍳 Mẻ cần làm › 🔥 ME-0123</i>");
            Posts(h).Should().Be(posts, "Quay lại = hủy nháp, không bản ghi mới");

            await h.SendAsync(Tap("nav:back", Panel(h)));
            PanelText(h).Should().Contain("Mẻ cần làm hôm nay", "◀ lần nữa → danh sách");
            Posts(h).Should().Be(posts);
        }

        [Fact]
        public async Task Every_screen_has_exactly_one_back_button_and_every_tap_is_answered_with_text()
        {
            await using var h = KitchenLead();
            h.Web.Routes["api/ProductionBatch/CreateExtra"] = (_, _) => (200, "Đã tạo mẻ nấu thêm", "{\"batch\":{\"batchNumber\":\"ME-9\"}}", null);
            await h.SendAsync(Text("/menu").Json);
            Backs(h).Should().Be(0, "màn đầu chỉ có ✖ Đóng");
            string n = Cook.ToString("N");
            string[] taps = ["menu:mod:BEP", "menu:act:bep_batches", "bat:view:" + n, "bat:tn:" + n, "bat:tn:" + n + ".80", "nav:back", "bat:fin:" + n, "nav:back",
                "bat:ex:" + n, "bat:exq:" + n + ".10", "nav:back", "nav:back", "nav:back", "nav:back", "nav:back"];
            int answered = h.Sender.Answered;
            foreach (string t in taps)
            {
                await h.SendAsync(Tap(t, Panel(h)));
                if (State(h).Top?.Level != PanelLevel.Root)
                {
                    Backs(h).Should().Be(1, $"sau khi bấm {t} màn phải có đúng 1 nút Quay lại");
                }
            }
            State(h).Top!.Level.Should().Be(PanelLevel.Root);
            h.Sender.Answered.Should().Be(answered + taps.Length);
            h.Sender.AnswerTexts.TakeLast(taps.Length).Should().OnlyContain(a => !string.IsNullOrWhiteSpace(a), "mọi callback có chữ trả lời");
            h.Web.Calls.Should().NotContain(c => c.Method == "POST", "chỉ mở màn / Quay lại – không ghi gì");
        }

        [Fact]
        public async Task List_of_thirteen_batches_has_three_pages_and_paging_writes_nothing()
        {
            var batches = Enumerable.Range(1, 13).Select(i => Batch(Guid.NewGuid(), $"ME-{i:00}", minutes: i)).ToList();
            await using var h = KitchenLead(batches);
            await h.SendAsync(Text("/me").Json);
            PanelText(h).Should().Contain("trang 1/3");
            PanelCallbacks(h).Count(c => c!.StartsWith("bat:view:")).Should().Be(6);
            PanelCallbacks(h).Should().Contain("pg:bep_batches:1").And.NotContain(c => c!.StartsWith("pg:bep_batches:0"));
            await h.SendAsync(Tap("pg:bep_batches:1", Panel(h)));
            PanelText(h).Should().Contain("trang 2/3").And.Contain("ME-07");
            PanelCallbacks(h).Should().Contain(["pg:bep_batches:0", "pg:bep_batches:2"]);
            h.Sender.AnswerTexts.Last().Should().Be("Trang 2");
            await h.SendAsync(Tap("pg:bep_batches:2", Panel(h)));
            PanelText(h).Should().Contain("trang 3/3").And.Contain("ME-13");
            PanelCallbacks(h).Count(c => c!.StartsWith("bat:view:")).Should().Be(1);
            h.Web.Calls.Should().NotContain(c => c.Method == "POST", "đổi trang chỉ đọc lại danh sách");

            // ◀ từ thẻ mở ở trang 3 → về đúng trang 3.
            await h.SendAsync(Tap(PanelCallbacks(h).First(c => c!.StartsWith("bat:view:"))!, Panel(h)));
            await h.SendAsync(Tap("nav:back", Panel(h)));
            PanelText(h).Should().Contain("trang 3/3");
        }

        private static string RouteDetail() => JsonSerializer.Serialize(new
        {
            route = new { id = Route, routeNo = "CX-01", status = "InTransit", plannedStart = Now(-60) },
            statusName = "Đang giao", licensePlate = "29C-123.45", driverName = "An", loadMeals = 98m, deliveryTempMinC = 60m, departureTemperatureC = 66m,
            allowedActions = Array.Empty<string>(), listBlocker = Array.Empty<string>(),
            listStop = new object[]
            {
                new { id = Stop1, sequenceNo = 1, stopStatus = "Arrived", statusName = "Đã đến", customerName = "Trường A", locationName = "Cổng chính", plannedArrival = Now(),
                    plannedMealQty = 100m, loadedQty = 98m, allowedActions = new[] { "deliver", "fail" } },
            },
        });

        [Fact]
        public async Task Driver_delivers_one_stop_in_full_in_three_taps_with_prefilled_quantity()
        {
            await using var h = Host("#/log-stop", "#/log-route", "#/log-container-return");
            string detail = RouteDetail();
            h.Web.Routes["api/DeliveryRoute/GetMine"] = (_, _) => (200, null, "{\"listRoute\":[" + detail + "]}", null);
            h.Web.Routes["api/DeliveryRoute/GetDetail"] = (_, _) => (200, null, detail, null);
            h.Web.Routes["api/DeliveryStop/GetById"] = (_, _) => (200, null, "{\"id\":\"" + Stop1 + "\",\"deliveryRouteId\":\"" + Route + "\"}", null);
            h.Web.Routes["api/DeliveryStop/Deliver"] = (_, _) => (200, "Đã bàn giao", "{}", null);
            await h.SendAsync(Text("/chuyen").Json);
            await h.SendAsync(Text("/menu").Json);
            PanelCallbacks(h).Should().Contain("menu:act:gv_mine");
            PanelButtons(h).First().Text.Should().StartWith("⭐", "⭐ Hay dùng 1 hàng riêng trên đầu");

            await h.SendAsync(Tap("menu:act:gv_mine", Panel(h)));                 // chạm 1: 1 chuyến → mở thẳng thẻ chuyến
            PanelText(h).Should().Contain("CX-01");
            await h.SendAsync(Tap("dlv:s:" + Stop1.ToString("N"), Panel(h)));      // chạm 2: thẻ điểm
            PanelText(h).Should().Contain("98 suất").And.Contain("66 °C").And.Contain("người nhận: Đại diện Trường A");
            PanelButtons(h).Should().Contain(b => b.Text == "✅ Giao đủ 98 · 66°" && b.CallbackData == "dlv:full:" + Stop1.ToString("N"));
            await h.SendAsync(Tap("dlv:full:" + Stop1.ToString("N"), Panel(h)));   // chạm 3

            var form = h.Web.Calls.Last(c => c.Path == "api/DeliveryStop/Deliver").Form;
            form["DeliveredQuantity"].Should().Be("98");
            form["TemperatureC"].Should().Be("66");
            form["ReceiverName"].Should().Be("Đại diện Trường A");
            h.Sender.Log.Should().NotContain(l => l.Message.ReplyMarkup is TgForceReply, "không gõ tay");
            h.Sender.AnswerTexts.Last().Should().StartWith("✅ Đã giao 98 suất");

            // ◀ từ thẻ điểm → thẻ chuyến (chức năng tự mở), ◀ nữa → phân hệ Giao vận (không lặp lại thẻ chuyến).
            await h.SendAsync(Tap("nav:back", Panel(h)));
            PanelText(h).Should().Contain("CX-01");
            await h.SendAsync(Tap("nav:back", Panel(h)));
            State(h).Top!.Level.Should().Be(PanelLevel.Module);
            h.Web.Calls.Count(c => c.Path == "api/DeliveryStop/Deliver").Should().Be(1, "Quay lại không phát lại thao tác ghi");
        }

        [Fact]
        public async Task Check_in_is_one_tap_on_location_key_and_three_keys_stay()
        {
            var h = new MessagingTestHost(new Dictionary<string, string?> { ["Messaging:Panel:Enabled"] = "true" });
            await using var _ = h;
            h.Api.Identity = new BotIdentityModel { Linked = true, CanUseBot = true, EmployeeId = Guid.NewGuid(), Status = "Active" };
            h.Api.Today = new MyAttendanceModel { EmployeeName = "CN", AllowedActions = ["checkIn"] };
            await h.SendAsync(Text("/chamcong").Json);
            await h.SendAsync(Location());                                         // 1 chạm phím 📍 (Telegram gửi vị trí)
            h.Api.EmployeeCalls.Should().ContainSingle(c => c.Action == "EmployeeCheckIn");
            var keyboards = h.Sender.Log.Select(l => l.Message.ReplyMarkup).Where(m => m is TgReplyKeyboard or TgReplyKeyboardRemove).ToList();
            keyboards.Should().NotBeEmpty().And.OnlyContain(m => m is TgReplyKeyboard, "không bao giờ gỡ bàn phím dưới");
            keyboards.Cast<TgReplyKeyboard>().Should().OnlyContain(k => k.Keyboard.SelectMany(r => r).Select(b => b.Text).SequenceEqual(new[] { KeyboardBuilder.MyShift, KeyboardBuilder.Functions, KeyboardBuilder.CheckIn }),
                "bàn phím dưới luôn đúng 3 phím (không còn bàn phím 1 nút vị trí)");
        }

        [Fact]
        public async Task Main_keyboard_is_resent_on_menu_and_old_keyboard_message_deleted()
        {
            await using var h = KitchenLead();
            await h.SendAsync(Text("/tiendo").Json);
            h.Sender.Log.Count(l => l.Message.ReplyMarkup is TgReplyKeyboard).Should().Be(1, "lần tương tác đầu sau khi container khởi động");
            await h.SendAsync(Text("/tiendo").Json);
            h.Sender.Log.Count(l => l.Message.ReplyMarkup is TgReplyKeyboard).Should().Be(1, "đã có bàn phím → không gửi lại");
            await h.SendAsync(Text("/menu").Json);
            var kbs = h.Sender.Log.Where(l => l.Message.ReplyMarkup is TgReplyKeyboard).ToList();
            kbs.Should().HaveCount(2, "/menu gửi lại 3 phím");
            h.Sender.Deleted.Select(d => d.MessageId).Should().Contain(kbs[0].Id, "tin bàn phím cũ bị xóa – chỉ 1 tin bàn phím trong chat");
            h.Sender.Log.Last().Id.Should().Be(Panel(h), "panel vẫn nằm cuối chat");
        }

        [Fact]
        public async Task User_with_four_functions_gets_flat_first_screen_and_main_role_module_comes_first()
        {
            var h = new MessagingTestHost(new Dictionary<string, string?> { ["Messaging:Panel:Enabled"] = "true" });
            await using var _ = h;
            h.Api.Identity = new BotIdentityModel { Linked = true, CanUseBot = true, EmployeeId = Guid.NewGuid(), Status = "Active" };
            await h.SendAsync(Text("/menu").Json);
            var cbs = PanelCallbacks(h);
            cbs.Where(c => c!.StartsWith("menu:act:")).Should().HaveCount(4).And.Contain(["menu:act:my_schedule", "menu:act:leave", "menu:act:my_requests", "menu:act:help"]);
            cbs.Should().NotContain(c => c!.StartsWith("menu:mod:"), "≤ 6 chức năng → màn đầu phẳng");
            await h.SendAsync(Tap("menu:act:my_schedule", Panel(h)));
            PanelText(h).Should().StartWith("<i>Menu › 📅 Lịch ca</i>", "màn đầu phẳng → không có bước phân hệ");

            // Tài xế + điều phối: phân hệ Giao vận (vai trò chính) đứng trước Cá nhân.
            var registry = h.App.Services.GetRequiredService<BotActionRegistry>();
            var driver = new BotIdentityModel { Linked = true, CanUseBot = true, HasWebAccount = true, Menus = ["#/log-stop", "#/log-route", "#/log-container-return"] };
            registry.ModulesFor("VH", driver).First().Should().Be("GIAOVAN");
        }

        [Fact]
        public async Task No_function_is_duplicated_across_modules_and_keyboard_keys_are_not_repeated_in_menu()
        {
            await using var h = new MessagingTestHost();
            var registry = h.App.Services.GetRequiredService<BotActionRegistry>();
            foreach (string bot in new[] { "VH", "DH" })
            {
                var mine = registry.All.Where(a => a.Bots.Count == 0 || a.Bots.Contains(bot)).ToList();
                mine.GroupBy(a => a.Label).Where(g => g.Count() > 1).Select(g => g.Key).Should().BeEmpty($"bot {bot}: mỗi chức năng 1 phân hệ chính");
            }
            registry.ByCode("qa_cleaning")!.Module.Should().Be("ATTP");
            registry.ByCode("qa_cleaning")!.Command.Should().Be("vesinh");
            registry.ByCode("cs_new")!.Module.Should().Be("CSKH");
            new[] { "bep_cleaning", "dp_complaint", "kd_complaint", "kd_change", "kt_timesheet", "kd_menu", "kho_trace", "bep_trace" }.Should().OnlyContain(c => registry.ByCode(c) == null);
            var admin = new BotIdentityModel { Linked = true, CanUseBot = true, IsAdmin = true, HasWebAccount = true, ApproveScreens = ["hr-leave"] };
            registry.MenuAvailable("VH", admin).Select(a => a.Code).Should().NotContain(["my_shift", "check_in"], "⚡ / 📍 đã có ở bàn phím dưới");
            var root = new MenuNavigator(registry, new FakeSender()).Root("VH", admin);
            root.Buttons.Should().NotContain(b => b.Text == "·");
        }

        [Fact]
        public void Number_widget_steps_quick_row_and_confirm_carry_the_value_in_the_callback()
        {
            var m = new AppDelicare.Messaging.Presentation.NeutralMessage();
            InputWidgets.Number(m, "bat", "tn", "tv", "abc", 78m, "°C", [70m, 78m, 80m], 0, 150);
            m.Lines.Should().Contain("👉 78 °C");
            m.Buttons.Select(b => b.Callback).Should().Equal("bat:tn:abc.68", "bat:tn:abc.77", "bat:tn:abc.79", "bat:tn:abc.88", "bat:tn:abc.70", "bat:tn:abc.80", "bat:tv:abc.78");
            m.Buttons.Select(b => b.Text).Should().StartWith(["−10", "−1", "+1", "+10"]);
            var rows = AppDelicare.Messaging.Channels.Telegram.TelegramFormatter.Rows(m.Buttons);
            rows.Select(r => r.Count).Should().Equal(4, 2, 1);
            Screens.KeyOf("bat:tn:abc.77").Should().Be(Screens.KeyOf("bat:tn:abc.88"));
            Screens.IsView("bat:tv:abc.78").Should().BeFalse("nút ghi không bao giờ được phát lại");
            Screens.IsView("bat:tn:abc.78").Should().BeTrue();
        }
    }
}
