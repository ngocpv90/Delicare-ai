using System.Text.Json;
using AppDelicare.Messaging.Channels.Telegram.Models;
using AppDelicare.Messaging.Core.Contracts;
using FluentAssertions;

namespace AppDelicare.Tests.Messaging
{
    /// <summary>
    /// BOT-2.2: nút sự việc trên bot – gửi api đúng tin được bấm (bot + chat + mã tin), sửa tin của mọi người theo danh sách api trả, báo kết quả cho người bấm;
    /// 💬 Ghi chú hỏi gõ tay rồi gửi kèm mã tin; người đến sau được báo tên người đã nhận (không sửa tin).
    /// </summary>
    public class MessagingIncidentTests
    {
        private static long _u = 99800;

        private static string Tap(string data, long messageId, long from = 7701)
        {
            long id = Interlocked.Increment(ref _u);
            return "{\"update_id\":" + id + ",\"callback_query\":{\"id\":\"cb" + id + "\",\"from\":{\"id\":" + from + "},\"data\":" + JsonSerializer.Serialize(data)
                + ",\"message\":{\"message_id\":" + messageId + ",\"date\":1,\"chat\":{\"id\":" + from + ",\"type\":\"private\"}}}}";
        }

        private static string Text(string text, long replyTo, long from = 7701)
        {
            long id = Interlocked.Increment(ref _u);
            return "{\"update_id\":" + id + ",\"message\":{\"message_id\":" + id + ",\"date\":" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ",\"from\":{\"id\":" + from
                + ",\"first_name\":\"Qa\"},\"chat\":{\"id\":" + from + ",\"type\":\"private\"},\"text\":" + JsonSerializer.Serialize(text)
                + ",\"reply_to_message\":{\"message_id\":" + replyTo + ",\"date\":1,\"chat\":{\"id\":" + from + ",\"type\":\"private\"}}}}";
        }

        private static string Payload(string? claimedBy = null)
            => JsonSerializer.Serialize(new
            {
                template = "FOODSAFETY_CRITICAL", level = "URGENT", data = new { title = "Checklist bếp nóng" },
                buttons = new object[] { new { text = "🙋 Nhận xử lý", callback = "inc:claim" }, new { text = "💬 Ghi chú", callback = "inc:note" }, new { text = "✅ Đã xử lý", callback = "inc:done" } },
                incident = claimedBy == null ? null : new { claimedBy, claimedAt = "09:00" },
            });

        [Fact]
        public async Task Claim_edits_every_related_message_and_late_claimer_sees_the_name()
        {
            await using var h = new MessagingTestHost();
            h.Api.Identity = new BotIdentityModel { Linked = true, CanUseBot = true, EmployeeId = Guid.NewGuid(), Status = "Active" };
            h.Api.IncidentResult = req => new ApiResult<IncidentResultModel>
            {
                ResponseCode = 200,
                Data = new IncidentResultModel
                {
                    Changed = true, Note = "Bạn đã nhận xử lý – mọi người liên quan đã thấy tên bạn.",
                    ListEdit = [new() { BotCode = "VH", ChatId = "7701", ProviderMessageId = "601", PayloadJson = Payload("Chị Hoa") }, new() { BotCode = "DH", ChatId = "7702", ProviderMessageId = "905", PayloadJson = Payload("Chị Hoa") }],
                },
            };
            await h.SendAsync(Tap("inc:claim", messageId: 601));
            var call = h.Api.IncidentCalls.Single();
            (call.Action, call.ProviderMessageId, call.ChatId, call.BotCode).Should().Be(("claim", "601", "7701", "VH"));
            h.Sender.Edits.Select(e => (e.Bot, e.Chat, e.MessageId)).Should().BeEquivalentTo([("VH", "7701", "601"), ("DH", "7702", "905")]);
            h.Sender.Edits.First().Text.Should().Contain("Chị Hoa đã nhận xử lý");
            ((TgInlineKeyboard)h.Sender.Edits.First().Markup!).InlineKeyboard.SelectMany(r => r).Select(b => b.CallbackData).Should().Equal("inc:note", "inc:done");
            h.Sender.Sent.Last().Message.Text.Should().Contain("Bạn đã nhận xử lý");

            h.Api.IncidentResult = _ => new ApiResult<IncidentResultModel> { ResponseCode = 200, Data = new IncidentResultModel { Changed = false, Note = "Chị Hoa đã nhận xử lý lúc 09:00." } };
            int edits = h.Sender.Edits.Count;
            await h.SendAsync(Tap("inc:claim", messageId: 601));
            h.Sender.Edits.Count.Should().Be(edits);
            h.Sender.Sent.Last().Message.Text.Should().Contain("ℹ️ Chị Hoa đã nhận xử lý lúc 09:00.");
        }

        [Fact]
        public async Task Note_asks_for_text_then_sends_it_with_the_tapped_message()
        {
            await using var h = new MessagingTestHost();
            h.Api.Identity = new BotIdentityModel { Linked = true, CanUseBot = true, EmployeeId = Guid.NewGuid(), Status = "Active" };
            h.Api.IncidentResult = _ => new ApiResult<IncidentResultModel> { ResponseCode = 200, Data = new IncidentResultModel { Changed = true, Note = "Đã thêm ghi chú vào thẻ của mọi người." } };
            await h.SendAsync(Tap("inc:note", messageId: 777));
            h.Sender.Sent.Last().Message.ReplyMarkup.Should().BeOfType<TgForceReply>();
            h.Api.IncidentCalls.Should().BeEmpty();
            await h.SendAsync(Text("Đã chuyển sang tủ 2", replyTo: 100 + h.Sender.Sent.Count));
            var call = h.Api.IncidentCalls.Single();
            (call.Action, call.ProviderMessageId, call.Text).Should().Be(("note", "777", "Đã chuyển sang tủ 2"));
            h.Sender.Sent.Last().Message.Text.Should().Contain("Đã thêm ghi chú");
        }
    }
}
