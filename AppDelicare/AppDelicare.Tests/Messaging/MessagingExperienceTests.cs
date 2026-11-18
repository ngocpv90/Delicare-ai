using System.Text.Json;
using AppDelicare.Messaging.Audiences.Internal;
using AppDelicare.Messaging.Callback;
using AppDelicare.Messaging.Channels.Telegram.Models;
using AppDelicare.Messaging.Core.Contracts;
using AppDelicare.Messaging.Experience;
using FluentAssertions;

namespace AppDelicare.Tests.Messaging
{
    /// <summary>
    /// BOT-1.4 (D-62, BOT_TELEGRAM §1.4, §4, §6): CallbackData ≤ 64 byte, bàn phím (≤ 6 phẳng / admin 3 nút + ≤ 2 lần bấm), lệnh "/" theo chat, menu 2 tầng,
    /// duyệt theo mức rủi ro (thấp 1 chạm + hoàn tác, bấm 2 lần = 1 quyết định, cao 2 bước, từ chối + lý do ForceReply), thẻ cùng sự việc đổi trạng thái,
    /// thiếu quyền → báo rõ, chấm công vị trí (chặn chuyển tiếp / cũ), gõ mã → tra cứu.
    /// </summary>
    public class MessagingExperienceTests
    {
        private static readonly Guid L1 = Guid.Parse("11111111-0000-4000-8000-00000000000a");
        private static readonly Guid L2 = Guid.Parse("11111111-0000-4000-8000-00000000000b");
        private static readonly Guid H1 = Guid.Parse("11111111-0000-4000-8000-00000000000c");
        private static long _u = 1000;

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

        private static string Location(long ageSeconds = 0, bool forwarded = false, long from = 7001)
        {
            long id = Interlocked.Increment(ref _u);
            string fwd = forwarded ? ",\"forward_origin\":{\"type\":\"user\",\"date\":1}" : "";
            return "{\"update_id\":" + id + ",\"message\":{\"message_id\":" + id + ",\"date\":" + (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - ageSeconds) + ",\"from\":{\"id\":" + from
                + "},\"chat\":{\"id\":" + from + ",\"type\":\"private\"},\"location\":{\"latitude\":10.77,\"longitude\":106.7}" + fwd + "}}";
        }

        private static BotIdentityModel Approver(params string[] screens) => new()
        {
            Linked = true, CanUseBot = true, HasWebAccount = true, UserId = Guid.NewGuid(), FullName = "Trưởng ca", ApproveScreens = [.. screens], Status = "Active",
        };

        private static MessagingTestHost ApprovalHost(int decideCode = 200)
        {
            var h = new MessagingTestHost();
            h.Api.Identity = Approver("hr-leave", "scm-po");
            h.Api.Token = new ApiResult<UserTokenModel> { ResponseCode = 200, Data = new UserTokenModel { Token = "jwt-user", ExpiresAt = DateTime.UtcNow.AddMinutes(10) } };
            h.Web.Routes["api/ApprovalRequest/GetMyPending"] = (_, _) => (200, null, null, JsonSerializer.Serialize(new[]
            {
                new { id = L1, requestNo = "XN-261118-001", businessType = "LEAVE_REQUEST", title = "Nghỉ phép 2 ngày" },
                new { id = L2, requestNo = "XN-261118-002", businessType = "SHIFT_SWAP", title = "Đổi ca" },
                new { id = H1, requestNo = "XN-261118-003", businessType = "PURCHASE_ORDER", title = "PO-261118-0001 62,4 triệu" },
            }));
            h.Web.Routes["api/ApprovalRequest/GetDetail"] = (path, _) =>
            {
                Guid id = Guid.Parse(path.Split("id=")[1]);
                string type = id == H1 ? "PURCHASE_ORDER" : "LEAVE_REQUEST";
                return (200, null, JsonSerializer.Serialize(new { request = new { requestNo = "XN-" + id.ToString("N")[^3..], businessType = type }, title = "Yêu cầu " + type, canDecide = true, currentStepNo = 1, stepCount = 1 }), null);
            };
            h.Web.Routes["api/ApprovalRequest/Decide"] = (_, _) => (decideCode, decideCode == 200 ? "Đã duyệt" : "HTTP 403", "true", null);
            return h;
        }

        private static List<Dictionary<string, string>> Decisions(MessagingTestHost h)
            => [.. h.Web.Calls.Where(c => c.Path == "api/ApprovalRequest/Decide").Select(c => c.Form)];

        [Fact]
        public void Callback_data_fits_64_bytes_and_round_trips()
        {
            string s = CallbackData.Build(CallbackData.Kinds.Approval, "ok", L1);
            s.Should().Be("apv:ok:" + L1.ToString("N"));
            System.Text.Encoding.UTF8.GetByteCount(s).Should().BeLessThanOrEqualTo(64);
            var p = CallbackData.Parse(s)!;
            (p.Kind, p.Action, p.Guid).Should().Be(("apv", "ok", L1));
            CallbackData.Parse("menu:root")!.Id.Should().BeNull();
            CallbackData.Parse("rác").Should().BeNull();
            FluentActions.Invoking(() => CallbackData.Build("apv", "ok", new string('x', 80))).Should().Throw<ArgumentException>();
        }

        private sealed class ManyActions : IBotActionProvider
        {
            public IEnumerable<BotAction> GetActions()
            {
                string[] modules = ["BEP", "KHO", "GIAOVAN"];
                for (int i = 0; i < 9; i++)
                {
                    yield return new BotAction { Code = "x" + i, Module = modules[i % 3], Label = "Việc " + i, Command = "viec" + i, MenuMetaLink = "#/m" + i, Order = i, Handler = (_, _) => Task.CompletedTask };
                }
            }
        }

        [Fact]
        public void Admin_gets_three_fixed_buttons_and_every_function_in_two_taps_small_user_gets_flat_keyboard()
        {
            var registry = new BotActionRegistry([new ManyActions()]);
            var admin = new BotIdentityModel { Linked = true, CanUseBot = true, IsAdmin = true };
            var all = registry.Available("VH", admin);
            all.Should().HaveCount(9);
            KeyboardBuilder.ForActions(all).Keyboard.Single().Select(b => b.Text).Should().Equal(KeyboardBuilder.MyShift, KeyboardBuilder.Functions, KeyboardBuilder.CheckIn);
            var nav = new MenuNavigator(registry, new FakeSender());
            var root = nav.Root("VH", admin);
            root.Buttons.Should().HaveCount(3).And.OnlyContain(b => b.Callback!.StartsWith("menu:mod:"));
            // Bấm 1: phân hệ → bấm 2: chức năng. Mọi chức năng xuất hiện đúng 1 lần.
            var reached = root.Buttons.SelectMany(b => nav.Module("VH", admin, CallbackData.Parse(b.Callback)!.Id!).Buttons)
                .Where(b => b.Callback!.StartsWith("menu:act:")).Select(b => CallbackData.Parse(b.Callback)!.Id).ToList();
            reached.Should().BeEquivalentTo(all.Select(a => a.Code)).And.OnlyHaveUniqueItems();
            nav.Module("VH", admin, "BEP").Buttons.Last().Callback.Should().Be("menu:root", "có ⬅️ Quay lại");

            var small = new BotIdentityModel { Linked = true, CanUseBot = true, Menus = ["#/m0", "#/m1", "#/m2"] };
            var mine = registry.Available("VH", small);
            mine.Select(a => a.Code).Should().BeEquivalentTo(["x0", "x1", "x2"]);
            KeyboardBuilder.ForActions(mine).Keyboard.SelectMany(r => r).Select(b => b.Text).Should().BeEquivalentTo(["Việc 0", "Việc 1", "Việc 2"], "≤ 6 chức năng → hiện thẳng, không tầng");
        }

        [Fact]
        public async Task Slash_commands_per_chat_only_allowed_and_synced_once()
        {
            await using var h = ApprovalHost();
            await h.SendAsync(Text("/start"));
            await h.SendAsync(Text("/start"));
            var call = h.Telegram.CommandCalls.Should().ContainSingle().Subject;
            call.Scope.Should().Contain("\"chat\"").And.Contain("7001");
            call.Commands.Select(c => c.Command).Should().Contain(["start", "menu", "chamcong", "donct", "cho", "help", "huylienket"]);

            h.Api.Identity = new BotIdentityModel { Linked = true, CanUseBot = true, Status = "Active", EmployeeId = Guid.NewGuid() };
            await h.SendAsync(Text("/start", from: 7002));
            h.Telegram.CommandCalls.Last().Commands.Select(c => c.Command).Should().NotContain("cho", "không có quyền duyệt → không có lệnh /cho");
            h.Sender.Sent.Last().Message.ReplyMarkup.Should().BeOfType<TgReplyKeyboard>().Which.Keyboard.SelectMany(r => r).Select(b => b.Text)
                .Should().NotContain("⏳ Chờ tôi duyệt");
        }

        [Fact]
        public async Task Menu_navigates_by_editing_same_message_and_rechecks_rights()
        {
            await using var h = ApprovalHost();
            await h.SendAsync(Text("/menu"));
            var root = h.Sender.Sent.Last().Message.ReplyMarkup.Should().BeOfType<TgInlineKeyboard>().Subject;
            root.InlineKeyboard.SelectMany(r => r).Select(b => b.CallbackData).Should().Contain(["menu:mod:CHUNG", "menu:mod:DUYET"]);
            await h.SendAsync(Tap("menu:mod:CHUNG", messageId: 77));
            var edit = h.Sender.Edits.Last();
            edit.MessageId.Should().Be("77");
            edit.Text.Should().Contain("Cá nhân");
            await h.SendAsync(Tap("menu:act:help"));
            h.Sender.Sent.Last().Message.Text.Should().Contain("Trợ giúp");
            h.Api.Identity = new BotIdentityModel { Linked = true, CanUseBot = true, HasWebAccount = true, Status = "Active" };
            await h.SendAsync(Tap("menu:act:approvals"));
            h.Sender.Sent.Last().Message.Text.Should().Contain("không còn trong quyền");
        }

        [Fact]
        public async Task Low_risk_one_tap_with_undo_double_tap_is_one_decision_and_cards_sync()
        {
            await using var h = ApprovalHost();
            h.Api.Correlated.AddRange([
                new BotCorrelatedMessageModel { Id = Guid.NewGuid(), BotCode = "DH", ExternalConversationId = "8001", ProviderMessageId = "901" },
                new BotCorrelatedMessageModel { Id = Guid.NewGuid(), BotCode = "DH", ExternalConversationId = "8002", ProviderMessageId = "902" },
            ]);
            await h.SendAsync(Text("/cho"));
            var list = h.Sender.Sent.Last().Message;
            list.Text.Should().Contain("Chờ tôi duyệt (3)");
            ((TgInlineKeyboard)list.ReplyMarkup!).InlineKeyboard.SelectMany(r => r).Select(b => b.Text).Should().Contain(b => b.Contains("Duyệt nhanh 2"));
            // Bấm duyệt 2 lần liên tiếp → chỉ 1 quyết định sau khi hết thời gian hoàn tác.
            await h.SendAsync(Tap("apv:ok:" + L1.ToString("N"), messageId: 60));
            await h.SendAsync(Tap("apv:ok:" + L1.ToString("N"), messageId: 60));
            h.Sender.Edits.Last().Text.Should().Contain("Đang duyệt");
            Decisions(h).Should().BeEmpty("chưa hết 1″ hoàn tác");
            await Task.Delay(1800);
            Decisions(h).Should().ContainSingle().Which.Should().Contain(new KeyValuePair<string, string>("Id", L1.ToString())).And.Contain(new KeyValuePair<string, string>("Approve", "true"));
            h.Sender.Edits.Should().Contain(x => x.MessageId == "60" && x.Text.Contains("Đã duyệt bởi Trưởng ca"));
            h.Api.Calls.OfType<string>().Should().Contain("corr:APR:" + L1.ToString("N"));
            h.Sender.Edits.Where(x => x.MessageId is "901" or "902").Should().HaveCount(2, "thẻ ở mọi người cùng cấp đổi trạng thái");

            // Hoàn tác trong thời gian chờ → KHÔNG gọi api.
            await h.SendAsync(Tap("apv:ok:" + L2.ToString("N"), messageId: 61));
            await h.SendAsync(Tap("undo:apv:" + L2.ToString("N"), messageId: 61));
            await Task.Delay(1800);
            Decisions(h).Should().ContainSingle("L2 đã hoàn tác");
            h.Sender.Edits.Should().Contain(x => x.MessageId == "61" && x.Text.Contains("Đã hoàn tác"));
        }

        [Fact]
        public async Task High_risk_needs_confirm_and_reject_needs_reason_by_reply()
        {
            await using var h = ApprovalHost();
            await h.SendAsync(Tap("apv:ok:" + H1.ToString("N"), messageId: 70));
            h.Sender.Edits.Last().Text.Should().Contain("Xác nhận duyệt");
            Decisions(h).Should().BeEmpty("rủi ro cao: bước 1 chỉ hỏi xác nhận");
            await h.SendAsync(Tap("apv:cf:" + H1.ToString("N"), messageId: 70));
            Decisions(h).Should().ContainSingle().Which["Approve"].Should().Be("true");

            await h.SendAsync(Tap("apv:no:" + L1.ToString("N"), messageId: 71));
            var ask = h.Sender.Sent.Last();
            ask.Message.ReplyMarkup.Should().BeOfType<TgForceReply>();
            Decisions(h).Should().HaveCount(1, "chưa có lý do → chưa từ chối");
            long askId = long.Parse(h.Sender.Sent.Count.ToString()) + 100; // FakeSender đánh số tin từ 101
            await h.SendAsync(Text("Trùng lịch kiểm kê", replyTo: askId));
            var reject = Decisions(h).Last();
            reject["Approve"].Should().Be("false");
            reject["Note"].Should().Be("Trùng lịch kiểm kê");
        }

        [Fact]
        public async Task Forbidden_decision_is_reported_clearly()
        {
            await using var h = ApprovalHost(decideCode: 403);
            await h.SendAsync(Tap("apv:cf:" + H1.ToString("N")));
            h.Sender.Sent.Last().Message.Text.Should().Contain("chưa có quyền duyệt").And.Contain("/start");
        }

        [Fact]
        public async Task Check_in_rejects_forwarded_and_old_location_then_checks_in()
        {
            await using var h = new MessagingTestHost();
            h.Api.Identity = new BotIdentityModel { Linked = true, CanUseBot = true, EmployeeId = Guid.NewGuid(), Status = "Active" };
            h.Api.Today = new MyAttendanceModel { EmployeeName = "CN", AllowedActions = ["checkIn"] };
            await h.SendAsync(Location(forwarded: true));
            h.Sender.Sent.Last().Message.Text.Should().Contain("chuyển tiếp");
            await h.SendAsync(Location(ageSeconds: 300));
            h.Sender.Sent.Last().Message.Text.Should().Contain("đã cũ");
            h.Api.EmployeeCalls.Should().BeEmpty();
            await h.SendAsync(Location());
            var call = h.Api.EmployeeCalls.Should().ContainSingle().Subject;
            call.Action.Should().Be("EmployeeCheckIn");
            call.Json.Should().Contain("10.77").And.Contain("TG:VH:");

            // Tài khoản web: AttendanceEvent/GetMine → ra ca bằng JWT của chính mình.
            h.Api.Identity = Approver();
            h.Api.Token = new ApiResult<UserTokenModel> { ResponseCode = 200, Data = new UserTokenModel { Token = "jwt-user" } };
            h.Web.Routes["api/AttendanceEvent/GetMine"] = (_, _) => (200, null, "{\"allowedActions\":[\"checkOut\"],\"listAssignment\":[]}", null);
            h.Web.Routes["api/AttendanceEvent/CheckOut"] = (_, _) => (200, "Ra ca lúc 17:02", "{}", null);
            await h.SendAsync(Location(from: 7003));
            var post = h.Web.Calls.Last();
            (post.Method, post.Path, post.Token).Should().Be(("POST", "api/AttendanceEvent/CheckOut", "jwt-user"));
            post.Form["SourceChannel"].Should().Be("GPS");
            h.Sender.Sent.Last().Message.Text.Should().Contain("Ra ca");
        }

        [Fact]
        public async Task Typing_a_document_code_searches_with_user_token()
        {
            await using var h = ApprovalHost();
            h.Web.Routes["api/Common/GlobalSearch"] = (_, _) => (200, null,
                "{\"records\":[{\"typeTitle\":\"Mẻ nấu\",\"code\":\"ME-261118-07\",\"label\":\"Canh chua\",\"statusName\":\"Đang nấu\"}]}", null);
            await h.SendAsync(Text("ME-261118-07"));
            h.Web.Calls.Last().Path.Should().Be("api/Common/GlobalSearch?q=ME-261118-07");
            h.Sender.Sent.Last().Message.Text.Should().Contain("ME-261118-07").And.Contain("Canh chua");
        }

        [Fact]
        public void Input_widgets_number_pad_stepper_and_checklist()
        {
            var pad = InputWidgets.NumberPad("core", [70m, 75m, 80m]);
            pad.Select(b => b.Callback).Should().Equal("num:core:70", "num:core:75", "num:core:80", "num:core:other");
            var step = InputWidgets.Stepper("qty", 120m);
            step.Last().Callback.Should().Be("num:qty:ok120");
            InputWidgets.ParseNumber(CallbackData.Parse("num:qty:ok120")!, out bool ok).Should().Be(120m);
            ok.Should().BeTrue();
            InputWidgets.ParseNumber(CallbackData.Parse("num:core:other")!, out _).Should().BeNull();
            var half = InputWidgets.Checklist("Vệ sinh", [("Rửa", true), ("Khử khuẩn", null)]);
            half.Buttons.Should().HaveCount(2, "chưa chọn hết → chưa có Hoàn tất");
            InputWidgets.Checklist("Vệ sinh", [("Rửa", true), ("Khử khuẩn", false)]).Buttons.Last().Callback.Should().Be("chk:done");
            ContextKeyboard.Build([new("📍 Đến điểm", true), new("✅ Xong điểm")]).Keyboard.Last().Single().Text.Should().Be(ContextKeyboard.Exit);
        }
    }
}
