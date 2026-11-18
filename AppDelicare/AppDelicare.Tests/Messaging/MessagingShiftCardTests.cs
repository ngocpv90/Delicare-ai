using System.Text.Json;
using AppDelicare.Messaging.Audiences.Internal;
using AppDelicare.Messaging.Audiences.Internal.Chung;
using AppDelicare.Messaging.Channels;
using AppDelicare.Messaging.Channels.Telegram.Models;
using AppDelicare.Messaging.Core;
using AppDelicare.Messaging.Core.Contracts;
using AppDelicare.Messaging.Experience;
using AppDelicare.Messaging.Options;
using AppDelicare.Messaging.Processing;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AppDelicare.Tests.Messaging
{
    /// <summary>
    /// BOT-2.1 (BOT_TELEGRAM §1.1): Thẻ ca – outbox gửi xong thì GHIM (chỉ thẻ, tin thường không ghim); làm mới thẻ bằng SỬA tin (không gửi tin mới), chỉ khi nội dung đổi
    /// và cách lần sửa trước ≥ 5′ (đồng hồ giả); ⚡ Ca của tôi sửa thẻ ghim + báo; chưa có thẻ → gửi + ghim; nút trên thẻ chạy chức năng thành tin MỚI (không đè thẻ).
    /// </summary>
    public class MessagingShiftCardTests
    {
        private sealed class ManualTime : TimeProvider
        {
            public DateTimeOffset Now { get; set; } = new(2031, 6, 2, 0, 0, 0, TimeSpan.Zero);
            public override DateTimeOffset GetUtcNow() => Now;
        }

        private static string Card(string title, params string[] lines)
            => JsonSerializer.Serialize(new
            {
                template = "SHIFT_CARD", data = new { title, lines }, level = "NORMAL",
                buttons = new object[] { new { text = "🍳 Mẻ cần làm", callback = "sc:run:bep_batches" }, new { text = "🔄 Làm mới", callback = "sc:refresh" } },
            });

        private static long _u = 99500;

        private static string Text(string text, long from = 7601)
        {
            long id = Interlocked.Increment(ref _u);
            return "{\"update_id\":" + id + ",\"message\":{\"message_id\":" + id + ",\"date\":" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ",\"from\":{\"id\":" + from
                + ",\"first_name\":\"Ca\"},\"chat\":{\"id\":" + from + ",\"type\":\"private\"},\"text\":" + JsonSerializer.Serialize(text) + "}}";
        }

        private static string Tap(string data, long messageId, long from = 7601)
        {
            long id = Interlocked.Increment(ref _u);
            return "{\"update_id\":" + id + ",\"callback_query\":{\"id\":\"cb" + id + "\",\"from\":{\"id\":" + from + "},\"data\":" + JsonSerializer.Serialize(data)
                + ",\"message\":{\"message_id\":" + messageId + ",\"date\":1,\"chat\":{\"id\":" + from + ",\"type\":\"private\"}}}}";
        }

        [Fact]
        public async Task Outbound_pump_pins_shift_card_but_not_normal_messages()
        {
            var api = new FakeBotChannelApi();
            var sender = new FakeSender();
            var sending = new StaticOptions<SendingOptions>(new SendingOptions { MaxPerSecond = 100, PerChatPerSecond = 100 });
            var pump = new OutboundPump(api, sender, new SendRateLimiter(sending, TimeProvider.System), new StaticOptions<MessagingHostOptions>(new()), sending,
                new StaticOptions<TelegramOptions>(new TelegramOptions { Bots = { ["VH"] = new() { Token = "t" } } }), NullLogger<OutboundPump>.Instance);
            api.OutboundQueue.AddRange([
                new BotOutboundItemModel { Id = Guid.NewGuid(), BotCode = "VH", RecipientExternalId = "11", MessageType = "SHIFT_CARD", PayloadJson = Card("🗓 Thẻ ca – Ca sáng", "🕐 06:00–14:00") },
                new BotOutboundItemModel { Id = Guid.NewGuid(), BotCode = "VH", RecipientExternalId = "12", MessageType = "MANUAL_TEXT", PayloadJson = """{"template":"MANUAL_TEXT","data":{"text":"Xin chào"}}""" },
            ]);
            sender.Next.Enqueue(ChannelSendResult.Sent("701"));
            sender.Next.Enqueue(ChannelSendResult.Sent("702"));
            (await pump.RunOnceAsync(default)).Should().Be(2);
            sender.Pins.Should().ContainSingle().Which.Should().Be(("VH", "11", "701"));
            var card = sender.Sent.First().Message;
            card.Text.Should().Contain("Thẻ ca – Ca sáng").And.Contain("06:00–14:00");
            ((TgInlineKeyboard)card.ReplyMarkup!).InlineKeyboard.SelectMany(r => r).Select(b => b.CallbackData).Should().Equal("sc:run:bep_batches", "sc:refresh");
        }

        [Fact]
        public async Task Refresh_edits_card_only_when_content_changes_and_at_most_every_five_minutes()
        {
            await using var h = new MessagingTestHost();
            var time = new ManualTime();
            var cards = new ShiftCards(h.Api, h.Sender, h.App.Services.GetRequiredService<BotActionRegistry>(), h.App.Services.GetRequiredService<HelpActions>(),
                new ConfigurationBuilder().Build(), time);
            var item = new ShiftCardItemModel { AssignmentId = Guid.NewGuid(), BotCode = "VH", ChatId = "21", ProviderMessageId = "801", PayloadJson = Card("Thẻ ca", "⚠️ Chưa chấm vào ca") };
            h.Api.ShiftCards.Add(item);

            (await cards.RefreshAsync(default)).Should().Be(1, "lần đầu thấy thẻ → cập nhật");
            h.Sender.Edits.Single().MessageId.Should().Be("801");
            h.Sender.Sent.Should().BeEmpty("cập nhật bằng sửa tin, không gửi tin mới");
            (await cards.RefreshAsync(default)).Should().Be(0, "nội dung không đổi");

            item.PayloadJson = Card("Thẻ ca", "✅ Đã vào ca");
            time.Now = time.Now.AddMinutes(3);
            (await cards.RefreshAsync(default)).Should().Be(0, "chưa đủ 5′ từ lần sửa trước");
            time.Now = time.Now.AddMinutes(3);
            (await cards.RefreshAsync(default)).Should().Be(1);
            h.Sender.Edits.Last().Text.Should().Contain("Đã vào ca");
        }

        [Fact]
        public async Task My_shift_button_edits_pinned_card_or_sends_and_pins_a_new_one()
        {
            await using var h = new MessagingTestHost();
            h.Api.Identity = new BotIdentityModel { Linked = true, CanUseBot = true, EmployeeId = Guid.NewGuid(), Status = "Active" };
            h.Api.MyCard = new ApiResult<ShiftCardItemModel> { ResponseCode = 200, Data = new ShiftCardItemModel { BotCode = "VH", ChatId = "7601", ProviderMessageId = "901", PayloadJson = Card("🗓 Thẻ ca – Ca chiều", "🕐 14:00–22:00") } };
            await h.SendAsync(Text("⚡ Ca của tôi"));
            h.Sender.Edits.Single().MessageId.Should().Be("901");
            h.Sender.Edits.Single().Text.Should().Contain("Ca chiều");
            h.Sender.Sent.Last().Message.Text.Should().Contain("Đã làm mới Thẻ ca");

            h.Api.MyCard.Data!.ProviderMessageId = null;
            await h.SendAsync(Text("⚡ Ca của tôi"));
            h.Sender.Sent.Last().Message.Text.Should().Contain("Thẻ ca – Ca chiều");
            h.Sender.Pins.Should().ContainSingle("chưa có thẻ → gửi thẻ mới và ghim");

            h.Api.MyCard = new ApiResult<ShiftCardItemModel> { ResponseCode = 400, ResponseMessage = "Hôm nay bạn không có ca." };
            int before = h.Sender.Sent.Count;
            await h.SendAsync(Text("⚡ Ca của tôi"));
            h.Sender.Sent.Count.Should().BeGreaterThan(before, "không có thẻ → màn ca cũ (HelpActions)");
        }

        [Fact]
        public async Task Card_buttons_run_functions_as_new_messages_and_refresh_edits_the_card()
        {
            await using var h = new MessagingTestHost();
            h.Api.Identity = new BotIdentityModel { Linked = true, CanUseBot = true, HasWebAccount = true, UserId = Guid.NewGuid(), Status = "Active", Menus = ["#/prod-batch"] };
            h.Api.Token = new ApiResult<UserTokenModel> { ResponseCode = 200, Data = new UserTokenModel { Token = "jwt" } };
            h.Web.Routes["api/ProductionBatch/GetBoard"] = (_, _) => (200, null, "{\"listBatch\":[]}", null);
            await h.SendAsync(Tap("sc:run:bep_batches", messageId: 950));
            h.Sender.Edits.Should().NotContain(e => e.MessageId == "950", "không sửa đè thẻ ghim");
            h.Sender.Sent.Last().Message.Text.Should().Contain("Mẻ cần làm hôm nay");

            await h.SendAsync(Tap("sc:run:hr_absent", messageId: 950));
            h.Sender.Sent.Last().Message.Text.Should().Contain("không còn trong quyền");

            h.Api.MyCard = new ApiResult<ShiftCardItemModel> { ResponseCode = 200, Data = new ShiftCardItemModel { BotCode = "VH", ChatId = "7601", ProviderMessageId = "777", PayloadJson = Card("🗓 Thẻ ca", "✅ Đã vào ca") } };
            await h.SendAsync(Tap("sc:refresh", messageId: 950));
            h.Sender.Edits.Last().MessageId.Should().Be("950", "🔄 trên thẻ → sửa chính tin đó");
        }
    }
}
