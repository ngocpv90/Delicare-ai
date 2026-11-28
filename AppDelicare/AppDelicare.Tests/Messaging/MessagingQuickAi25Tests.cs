using System.Text.Json;
using AppDelicare.Messaging.Channels.Telegram.Models;
using AppDelicare.Messaging.Core;
using AppDelicare.Messaging.Core.Contracts;
using FluentAssertions;

namespace AppDelicare.Tests.Messaging
{
    /// <summary>
    /// AI-25.3 (D-143): 📂 Chức năng › 💬 Hỏi nhanh trên bot VH / DH – danh sách câu từ Agent/GetQuick (JWT người dùng, kênh theo bot), nhóm theo agent,
    /// bấm câu → Agent/RunQuick (không AI) → tin theo mẫu ≤ 10 dòng + 🌐 Mở trên web; câu có ô nhập hỏi 1 bước (ForceReply).
    /// </summary>
    public class MessagingQuickAi25Tests
    {
        private static long _u = 91000;

        private static string Text(string text, long from = 7301, long? replyTo = null)
        {
            long id = Interlocked.Increment(ref _u);
            string reply = replyTo == null ? "" : ",\"reply_to_message\":{\"message_id\":" + replyTo + ",\"date\":1,\"chat\":{\"id\":" + from + ",\"type\":\"private\"}}";
            return "{\"update_id\":" + id + ",\"message\":{\"message_id\":" + id + ",\"date\":" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ",\"from\":{\"id\":" + from
                + ",\"first_name\":\"An\"},\"chat\":{\"id\":" + from + ",\"type\":\"private\"},\"text\":" + JsonSerializer.Serialize(text) + reply + "}}";
        }

        private static string Tap(string data, long messageId = 50, long from = 7301)
        {
            long id = Interlocked.Increment(ref _u);
            return "{\"update_id\":" + id + ",\"callback_query\":{\"id\":\"cb" + id + "\",\"from\":{\"id\":" + from + "},\"data\":" + JsonSerializer.Serialize(data)
                + ",\"message\":{\"message_id\":" + messageId + ",\"date\":1,\"chat\":{\"id\":" + from + ",\"type\":\"private\"}}}}";
        }

        private static MessagingTestHost Host(Dictionary<string, string?>? settings = null)
        {
            var h = new MessagingTestHost(settings);
            h.Api.Identity = new BotIdentityModel { Linked = true, CanUseBot = true, HasWebAccount = true, UserId = Guid.NewGuid(), FullName = "Thủ kho", Status = "Active", Menus = ["#/scm-inv-balance"] };
            h.Api.Token = new ApiResult<UserTokenModel> { ResponseCode = 200, Data = new UserTokenModel { Token = "jwt-user" } };
            h.Web.Routes["api/Agent/GetQuick"] = (_, _) => (200, null, null, JsonSerializer.Serialize(new object[]
            {
                new { id = "wh-can-han-7", agent = "WH", agentName = "Kho", hoi = "Lô nào sắp hết hạn trong 7 ngày?", nhap = new { } },
                new { id = "wh-truy-xuat", agent = "WH", agentName = "Kho", hoi = "Truy xuất lô", nhap = new { tu_khoa = "Số lô" } },
                new { id = "vivi-kpi-7", agent = "VIVI", agentName = "Vivi – Điều hành", hoi = "KPI 7 ngày qua", nhap = new { } },
            }));
            h.Web.Routes["api/Agent/RunQuick"] = (_, _) => (200, "OK", JsonSerializer.Serialize(new
            {
                id = "wh-can-han-7", agent = "WH", tieuDe = "Lô hết hạn trong 7 ngày", tomTat = "12 lô", kieu = "bang", cot = new[] { "Nguyên liệu", "Số lô" },
                dong = Enumerable.Range(1, 12).Select(i => new[] { "Thịt bò " + i, "LO-" + i }).ToArray(), tongDong = 12, moMan = "#/scm-inv-balance?tab=lot",
            }), null);
            return h;
        }

        private static List<TgInlineButton> Buttons(object? markup) => markup is TgInlineKeyboard k ? [.. k.InlineKeyboard.SelectMany(r => r)] : [];

        [Fact]
        public async Task Quick_menu_groups_by_agent_runs_without_ai_and_shows_ten_rows_with_web_link()
        {
            await using var h = Host(new() { ["Messaging:WebUrl"] = "https://erp.example.vn" });
            await h.SendAsync(Text("/hoinhanh"), "DH");
            h.Web.Calls.Last(c => c.Path.StartsWith("api/Agent/GetQuick")).Path.Should().Contain("kenh=DH", "kênh theo bot");
            var first = h.Sender.Sent.Last().Message;
            first.Text.Should().Contain("Hỏi nhanh");
            Buttons(first.ReplyMarkup).Select(b => b.CallbackData).Should().Equal("qk:ag:WH", "qk:ag:VIVI");

            await h.SendAsync(Tap("qk:ag:WH"), "DH");
            var list = h.Sender.Edits.Last();
            Buttons(list.Markup).Select(b => b.CallbackData).Should().Contain(["qk:q:wh-can-han-7", "qk:q:wh-truy-xuat", "qk:root"]);

            await h.SendAsync(Tap("qk:q:wh-can-han-7"), "DH");
            var run = h.Web.Calls.Last(c => c.Path.StartsWith("api/Agent/RunQuick"));
            run.Token.Should().Be("jwt-user", "chạy bằng JWT của chính người dùng");
            run.Path.Should().Contain("kenh=DH");
            var card = h.Sender.Sent.Last().Message;
            card.Text.Should().Contain("Lô hết hạn trong 7 ngày").And.Contain("12 lô").And.Contain("Thịt bò 10").And.NotContain("Thịt bò 11").And.Contain("còn 2 dòng").And.Contain("không dùng AI");
            var web = Buttons(card.ReplyMarkup).First(b => b.Url != null);
            web.Url.Should().Be("https://erp.example.vn/#/scm-inv-balance?tab=lot");
            Buttons(card.ReplyMarkup).Should().Contain(b => b.CallbackData == "qk:ag:WH");
        }

        [Fact]
        public async Task Quick_question_with_input_asks_one_step_then_runs_with_typed_value()
        {
            await using var h = Host();
            await h.SendAsync(Tap("qk:q:wh-truy-xuat"));
            var ask = h.Sender.Sent.Last().Message;
            ask.Text.Should().Contain("Gõ Số lô");
            ask.ReplyMarkup.Should().BeOfType<TgForceReply>();
            await h.SendAsync(Text("LO-0007", replyTo: 100 + h.Sender.Sent.Count));
            var run = h.Web.Calls.Last(c => c.Path.StartsWith("api/Agent/RunQuick"));
            run.Path.Should().Contain("kenh=VH");
            run.Form["__json"].Should().Contain("\"id\":\"wh-truy-xuat\"").And.Contain("\"tu_khoa\":\"LO-0007\"");
            // Không đặt Messaging:WebUrl → không có nút mở web.
            Buttons(h.Sender.Sent.Last().Message.ReplyMarkup).Should().NotContain(b => b.Url != null);
        }

        [Fact]
        public async Task Quick_menu_hidden_without_web_account_and_api_403_is_explained()
        {
            await using var h = Host();
            h.Web.Routes["api/Agent/RunQuick"] = (_, _) => (403, "Anh/chị chưa có quyền xem màn của câu hỏi này", null, null);
            await h.SendAsync(Tap("qk:q:wh-can-han-7"));
            h.Sender.Sent.Last().Message.Text.Should().Contain("chưa có quyền");
            h.Api.Identity = new BotIdentityModel { Linked = true, CanUseBot = true, HasWebAccount = false, EmployeeId = Guid.NewGuid(), Status = "Active" };
            await h.SendAsync(Text("/menu"));
            h.Sender.Sent.SelectMany(s => Buttons(s.Message.ReplyMarkup)).Should().NotContain(b => b.Text.Contains("Hỏi nhanh") && b.CallbackData != null && b.CallbackData.StartsWith("menu:"));
        }
    }
}
