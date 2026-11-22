using System.Text.Json;
using AppDelicare.Messaging.Channels;
using AppDelicare.Messaging.Channels.Telegram.Models;
using AppDelicare.Messaging.Core.Contracts;
using AppDelicare.Messaging.Experience;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Messaging
{
    /// <summary>
    /// BOT-UX-1 (anh duyệt 2026-11-20): bảng điều khiển duy nhất ở chat riêng – điều hướng = sửa panel, [◀ Quay lại] [🏠 Menu] [✖ Đóng], /menu xóa tin lệnh + panel cũ,
    /// nút trên panel cũ → "Menu này đã cũ", Quay lại khi đang nhập dở → không ghi gì, tin hệ thống không bị đụng, 4 lệnh "/", bàn phím dưới is_persistent.
    /// </summary>
    public class MessagingPanelTests
    {
        private const long Me = 7101;
        private static readonly Guid L1 = Guid.Parse("22222222-0000-4000-8000-00000000000a");
        private static long _u = 5000;

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

        private static MessagingTestHost Host()
        {
            var h = new MessagingTestHost(new Dictionary<string, string?> { ["Messaging:Panel:Enabled"] = "true" });
            h.Api.Identity = new BotIdentityModel
            {
                Linked = true, CanUseBot = true, Status = "Active", HasWebAccount = true, UserId = Guid.NewGuid(), FullName = "An",
                ApproveScreens = ["hr-leave"], Menus = ["#/hr-leave"],
            };
            h.Api.Token = new ApiResult<UserTokenModel> { ResponseCode = 200, Data = new UserTokenModel { Token = "jwt-user", ExpiresAt = DateTime.UtcNow.AddMinutes(10) } };
            h.Web.Routes["api/ApprovalRequest/GetMyPending"] = (_, _) => (200, null, null, JsonSerializer.Serialize(new[]
            {
                new { id = L1, requestNo = "XN-261120-001", businessType = "LEAVE_REQUEST", title = "Nghỉ phép 2 ngày" },
            }));
            h.Web.Routes["api/ApprovalRequest/GetDetail"] = (_, _) => (200, null, JsonSerializer.Serialize(new
            {
                request = new { requestNo = "XN-261120-001", businessType = "LEAVE_REQUEST" }, title = "Nghỉ phép 2 ngày", canDecide = true, currentStepNo = 1, stepCount = 1,
            }), null);
            h.Web.Routes["api/ApprovalRequest/Decide"] = (_, _) => (200, "Đã xử lý", "true", null);
            return h;
        }

        private static string Panel(MessagingTestHost h) => h.App.Services.GetRequiredService<PanelRegistry>().Get("VH", Me.ToString()).MessageId!;

        private static PanelState State(MessagingTestHost h) => h.App.Services.GetRequiredService<PanelRegistry>().Get("VH", Me.ToString());

        /// <summary>Tin của bot còn trong chat (đã gửi, chưa xóa) có nút inline.</summary>
        private static List<string> AliveInline(MessagingTestHost h)
        {
            var deleted = h.Sender.Deleted.Select(d => d.MessageId).ToHashSet();
            return [.. h.Sender.Log.Where(l => l.Message.ReplyMarkup is TgInlineKeyboard { InlineKeyboard.Count: > 0 } && !deleted.Contains(l.Id)).Select(l => l.Id)];
        }

        private static string LastPanelText(MessagingTestHost h)
        {
            string id = Panel(h);
            var edit = h.Sender.Edits.LastOrDefault(e => e.MessageId == id);
            var sent = h.Sender.Log.Last(l => l.Id == id);
            return edit.Text ?? sent.Message.Text;
        }

        private static List<string?> PanelCallbacks(MessagingTestHost h)
        {
            string id = Panel(h);
            var edit = h.Sender.Edits.LastOrDefault(e => e.MessageId == id);
            var markup = (edit.Markup ?? h.Sender.Log.Last(l => l.Id == id).Message.ReplyMarkup) as TgInlineKeyboard;
            return [.. markup!.InlineKeyboard.SelectMany(r => r).Select(b => b.CallbackData)];
        }

        [Fact]
        public async Task Five_navigations_leave_exactly_one_bot_message_with_buttons()
        {
            await using var h = Host();
            await h.SendAsync(Text("/menu").Json);
            await h.SendAsync(Tap("menu:mod:DUYET", Panel(h)));
            await h.SendAsync(Tap("menu:act:approvals", Panel(h)));
            await h.SendAsync(Tap("nav:back", Panel(h)));
            await h.SendAsync(Tap("nav:back", Panel(h)));

            AliveInline(h).Should().ContainSingle().Which.Should().Be(Panel(h));
            h.Sender.Log.Count(l => l.Message.ReplyMarkup is TgInlineKeyboard).Should().Be(1, "chỉ /menu gửi tin mới; 4 lần bấm sau đều sửa panel");
            LastPanelText(h).Should().StartWith("<i>Menu</i>");
        }

        [Fact]
        public async Task Back_from_level_three_goes_to_level_two_then_one_and_breadcrumb_follows()
        {
            await using var h = Host();
            await h.SendAsync(Text("/menu").Json);
            await h.SendAsync(Tap("menu:mod:DUYET", Panel(h)));
            await h.SendAsync(Tap("menu:act:approvals", Panel(h)));
            LastPanelText(h).Should().StartWith("<i>Menu › ⏳ Phê duyệt › ⏳ Chờ tôi duyệt</i>");
            PanelCallbacks(h).Should().Contain(["nav:back", "nav:home", "nav:close"], "hàng nút cuối mọi màn");

            await h.SendAsync(Tap("nav:back", Panel(h)));
            State(h).Top!.Level.Should().Be(PanelLevel.Module);
            PanelCallbacks(h).Should().Contain("menu:act:approvals", "cấp 2 = danh sách chức năng của phân hệ");

            await h.SendAsync(Tap("nav:back", Panel(h)));
            State(h).Top!.Level.Should().Be(PanelLevel.Root);
            PanelCallbacks(h).Should().Contain("menu:mod:DUYET").And.NotContain("nav:back", "màn đầu chỉ có ✖ Đóng");
        }

        [Fact]
        public async Task Back_while_typing_cancels_draft_and_writes_nothing()
        {
            await using var h = Host();
            await h.SendAsync(Text("/cho").Json);
            await h.SendAsync(Tap("apv:no:" + L1.ToString("N"), Panel(h)));
            var ask = h.Sender.Log.Last();
            ask.Message.ReplyMarkup.Should().BeOfType<TgForceReply>("lý do từ chối – bắt buộc gõ → ForceReply");

            await h.SendAsync(Tap("nav:back", Panel(h)));
            h.Sender.Deleted.Select(d => d.MessageId).Should().Contain(ask.Id, "hủy nháp → xóa câu hỏi");
            await h.SendAsync(Text("Trùng lịch", replyTo: long.Parse(ask.Id)).Json);
            h.Web.Calls.Should().NotContain(c => c.Path == "api/ApprovalRequest/Decide", "đã Quay lại → không có bản ghi mới");
            AliveInline(h).Should().ContainSingle();
        }

        [Fact]
        public async Task Answer_by_reply_deletes_question_and_answer_and_updates_panel()
        {
            await using var h = Host();
            await h.SendAsync(Text("/cho").Json);
            string panel = Panel(h);
            await h.SendAsync(Tap("apv:no:" + L1.ToString("N"), panel));
            var ask = h.Sender.Log.Last();
            var answer = Text("Trùng lịch kiểm kê", replyTo: long.Parse(ask.Id));
            await h.SendAsync(answer.Json);

            h.Web.Calls.Should().Contain(c => c.Path == "api/ApprovalRequest/Decide" && c.Form["Note"] == "Trùng lịch kiểm kê");
            h.Sender.Deleted.Select(d => d.MessageId).Should().Contain([ask.Id, answer.Id.ToString()]);
            Panel(h).Should().Be(panel, "kết quả sửa vào panel đang có");
            AliveInline(h).Should().ContainSingle();
        }

        [Fact]
        public async Task Menu_three_times_keeps_one_panel_at_bottom_and_deletes_command_messages()
        {
            await using var h = Host();
            var cmds = new[] { Text("/menu"), Text("/menu"), Text("/menu") };
            foreach (var c in cmds)
            {
                await h.SendAsync(c.Json);
            }
            AliveInline(h).Should().ContainSingle().Which.Should().Be(Panel(h));
            h.Sender.Log.Last().Id.Should().Be(Panel(h), "panel nằm cuối chat");
            h.Sender.Deleted.Select(d => d.MessageId).Should().Contain(cmds.Select(c => c.Id.ToString()), "tin lệnh /menu của người dùng bị xóa");
        }

        [Fact]
        public async Task Tap_on_old_panel_says_old_and_opens_new_panel()
        {
            await using var h = Host();
            await h.SendAsync(Text("/menu").Json);
            string old = Panel(h);
            await h.SendAsync(Text("/menu").Json);
            string current = Panel(h);
            current.Should().NotBe(old);

            await h.SendAsync(Tap("menu:mod:DUYET", old));
            h.Sender.AnswerTexts.Should().Contain("Menu này đã cũ");
            h.Sender.Deleted.Select(d => d.MessageId).Should().Contain(old);
            Panel(h).Should().NotBe(current, "mở panel mới ở màn đầu");
            State(h).Top!.Level.Should().Be(PanelLevel.Root);
            AliveInline(h).Should().ContainSingle();
        }

        [Fact]
        public async Task System_cards_sent_in_between_are_never_deleted_or_edited_by_the_panel()
        {
            await using var h = Host();
            await h.SendAsync(Text("/menu").Json);
            // Thẻ việc qua OutboundPump: gửi ngoài phiên panel.
            var sender = h.App.Services.GetRequiredService<IChannelSender>();
            var card = await sender.SendAsync("VH", new OutgoingMessage(Me.ToString(), "📋 Việc mới", new TgInlineKeyboard
            {
                InlineKeyboard = [[new TgInlineButton { Text = "Duyệt", CallbackData = "apv:ok:" + L1.ToString("N") }]],
            }));
            string cardId = card.ProviderMessageId!;

            await h.SendAsync(Text("/menu").Json);
            await h.SendAsync(Tap("menu:mod:DUYET", Panel(h)));
            await h.SendAsync(Tap("nav:home", Panel(h)));
            await h.SendAsync(Tap("nav:close", Panel(h)));
            await h.SendAsync(Text("/cho").Json);

            h.Sender.Deleted.Select(d => d.MessageId).Should().NotContain(cardId);
            h.Sender.Edits.Select(e => e.MessageId).Should().NotContain(cardId);
            await h.SendAsync(Tap("apv:ok:" + L1.ToString("N"), cardId));
            h.Sender.AnswerTexts.Should().NotContain("Menu này đã cũ", "nút trên thẻ việc vẫn chạy như cũ");
            h.Sender.Deleted.Select(d => d.MessageId).Should().NotContain(cardId);
        }

        [Fact]
        public async Task Four_commands_and_persistent_three_key_keyboard()
        {
            await using var h = Host();
            await h.SendAsync(Text("/start").Json);
            h.Telegram.CommandCalls.Last().Commands.Select(c => c.Command).Should().Equal("menu", "ca", "chamcong", "help");
            var kb = h.Sender.Log.Select(l => l.Message.ReplyMarkup).OfType<TgReplyKeyboard>().Last();
            kb.IsPersistent.Should().BeTrue();
            kb.Keyboard.SelectMany(r => r).Select(b => b.Text).Should().Equal(KeyboardBuilder.MyShift, KeyboardBuilder.Functions, KeyboardBuilder.CheckIn);

            await h.SendAsync(Text("/chamcong").Json);
            LastPanelText(h).Should().Contain("📍").And.Contain("bàn phím dưới");
        }

        [Fact]
        public async Task Finished_action_turns_panel_into_result_with_continue()
        {
            await using var h = Host();
            await h.SendAsync(Text("/cho").Json);
            var reg = h.App.Services.GetRequiredService<PanelRegistry>();
            var sender = h.App.Services.GetRequiredService<IChannelSender>();
            var s = PanelSession.Begin("VH", Me.ToString(), reg.Get("VH", Me.ToString()), PanelMode.EditCurrent);
            try
            {
                await sender.SendAsync("VH", new OutgoingMessage(Me.ToString(), "✅ Đã từ chối XN-261120-001"));
            }
            finally
            {
                s.End();
            }
            LastPanelText(h).Should().Contain("✅ Đã từ chối XN-261120-001").And.Contain("🕒");
            PanelCallbacks(h).Should().Equal("nav:cont", "nav:home");

            await h.SendAsync(Tap("nav:cont", Panel(h)));
            LastPanelText(h).Should().Contain("› ⏳ Chờ tôi duyệt</i>", "Tiếp tục → về màn chức năng vừa dùng");
        }

        [Fact]
        public async Task Group_chat_keeps_old_behaviour()
        {
            await using var h = Host();
            long id = Interlocked.Increment(ref _u);
            await h.SendAsync("{\"update_id\":" + id + ",\"message\":{\"message_id\":" + id + ",\"date\":1,\"from\":{\"id\":" + Me + "},\"chat\":{\"id\":-100500,\"type\":\"group\"},\"text\":\"/menu\"}}");
            h.Sender.Deleted.Should().BeEmpty();
            h.App.Services.GetRequiredService<PanelRegistry>().TryGet("VH", "-100500", out _).Should().BeFalse();
        }
    }
}
