using System.Text.Json;
using AppDelicare.Messaging.Audiences.Internal.Chung;
using AppDelicare.Messaging.Core.Contracts;
using FluentAssertions;

namespace AppDelicare.Tests.Messaging
{
    /// <summary>
    /// BOT-1.7d: QR <c>DOC_</c> trên phiếu in mở đúng phiếu (Common/GetPrintDocument bằng JWT người quét); nút trên tin trong NHÓM chat:
    /// 🙋 Nhận xử lý ghi ngay (tìm sự việc theo chat nhóm + mã tin), kết quả hiện dạng thông báo nhỏ, không nhắn vào nhóm; nút khác mở trong chat riêng;
    /// người chưa liên kết bấm → được nhắc liên kết; tin thường trong nhóm bị bỏ qua.
    /// </summary>
    public class MessagingDocGroupTests
    {
        private static readonly Guid Doc = Guid.Parse("55555555-0000-4000-8000-0000000000a1");
        private static readonly Guid Route = Guid.Parse("55555555-0000-4000-8000-0000000000b1");
        private const long Group = -1001234;
        private static long _u = 93000;

        private static string Text(string text, long from = 7501, long chat = 7501, string type = "private")
        {
            long id = Interlocked.Increment(ref _u);
            return "{\"update_id\":" + id + ",\"message\":{\"message_id\":" + id + ",\"date\":" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ",\"from\":{\"id\":" + from
                + ",\"first_name\":\"Kho\"},\"chat\":{\"id\":" + chat + ",\"type\":\"" + type + "\"},\"text\":" + JsonSerializer.Serialize(text) + "}}";
        }

        private static string GroupTap(string data, long messageId, long from = 7501)
        {
            long id = Interlocked.Increment(ref _u);
            return "{\"update_id\":" + id + ",\"callback_query\":{\"id\":\"cb" + id + "\",\"from\":{\"id\":" + from + "},\"data\":" + JsonSerializer.Serialize(data)
                + ",\"message\":{\"message_id\":" + messageId + ",\"date\":1,\"chat\":{\"id\":" + Group + ",\"type\":\"supergroup\"}}}}";
        }

        private static MessagingTestHost Host(bool linked = true)
        {
            var h = new MessagingTestHost();
            h.Api.Identity = linked
                ? new BotIdentityModel { Linked = true, CanUseBot = true, HasWebAccount = true, UserId = Guid.NewGuid(), FullName = "Thủ kho", Status = "Active", Menus = ["#/scm-gr", "#/log-route"] }
                : new BotIdentityModel { Linked = false };
            h.Api.Token = new ApiResult<UserTokenModel> { ResponseCode = 200, Data = new UserTokenModel { Token = "jwt-doc" } };
            return h;
        }

        [Fact]
        public void Doc_qr_parameter_needs_known_type_and_guid()
        {
            QrActions.Parse("DOC_GR_" + Doc.ToString("N")).Should().Be(("DOC_", "GR_" + Doc.ToString("N")));
            QrActions.DocOf("POD_" + Doc.ToString("N")).Should().Be(("POD", Doc));
            QrActions.Parse("DOC_XX_" + Doc.ToString("N")).Should().BeNull("loại phiếu lạ");
            QrActions.Parse("DOC_GR_123").Should().BeNull("Id không phải Guid");
        }

        [Fact]
        public async Task Doc_qr_opens_the_printed_document_with_the_scanners_token()
        {
            await using var h = Host();
            h.Web.Routes["api/Common/GetPrintDocument"] = (_, _) => (200, null, JsonSerializer.Serialize(new
            {
                title = "PHIẾU NHẬP KHO", documentNo = "PN-001", documentDate = "2031-06-02T00:00:00",
                listInfo = new[] { new { label = "Nhà cung cấp", value = "NCC A" } },
                listRow = new[] { new[] { "1", "Thịt gà", "kg", "20" } },
            }), null);
            await h.SendAsync(Text("/start DOC_GR_" + Doc.ToString("N")));
            var call = h.Web.Calls.Single(c => c.Path.StartsWith("api/Common/GetPrintDocument"));
            call.Path.Should().Be($"api/Common/GetPrintDocument?type=GR&id={Doc}");
            call.Token.Should().Be("jwt-doc");
            h.Sender.Sent.Last().Message.Text.Should().Contain("PHIẾU NHẬP KHO").And.Contain("PN-001").And.Contain("Nhà cung cấp: NCC A").And.Contain("Thịt gà · kg · 20");
        }

        [Fact]
        public async Task Group_claim_is_recorded_against_the_group_message_and_answered_privately()
        {
            await using var h = Host();
            h.Api.IncidentResult = _ => new ApiResult<IncidentResultModel>
            {
                ResponseCode = 200,
                Data = new IncidentResultModel { Changed = true, Note = "Bạn đã nhận xử lý", ListEdit = [new() { BotCode = "VH", ChatId = Group.ToString(), ProviderMessageId = "301", PayloadJson = "{\"template\":\"CORE_TEMP_LOW\"}" }] },
            };
            int sent = h.Sender.Sent.Count;
            await h.SendAsync(GroupTap("inc:claim", 301));
            var call = h.Api.IncidentCalls.Single();
            (call.ChatId, call.ProviderMessageId, call.ExternalUserId, call.Action).Should().Be((Group.ToString(), "301", "7501", "claim"));
            h.Sender.Edits.Should().ContainSingle(e => e.Chat == Group.ToString() && e.MessageId == "301", "sửa tin trong nhóm");
            h.Sender.AnswerTexts.Should().Contain("Bạn đã nhận xử lý");
            h.Sender.Sent.Count.Should().Be(sent, "không nhắn thêm vào nhóm");
        }

        [Fact]
        public async Task Other_group_buttons_open_in_private_chat_and_unlinked_users_are_told_to_link()
        {
            await using var h = Host();
            h.Web.Routes["api/DeliveryRoute/GetDetail"] = (_, _) => (200, null, JsonSerializer.Serialize(new
            {
                route = new { id = Route, routeNo = "CX-17D", status = "InTransit" }, statusName = "Đang giao", allowedActions = Array.Empty<string>(), listStop = Array.Empty<object>(),
            }), null);
            await h.SendAsync(GroupTap("dlv:r:" + Route.ToString("N"), 302));
            var card = h.Sender.Sent.Last();
            card.Message.ChatId.Should().Be("7501", "mở trong chat riêng của người bấm");
            card.Message.Text.Should().Contain("CX-17D");
            h.Sender.AnswerTexts.Should().Contain("Đã mở trong chat riêng với bot.");

            int before = h.Sender.Sent.Count;
            await h.SendAsync(Text("chào cả nhóm", chat: Group, type: "supergroup"));
            h.Sender.Sent.Count.Should().Be(before, "tin thường trong nhóm bị bỏ qua");

            await using var stranger = Host(linked: false);
            await stranger.SendAsync(GroupTap("inc:claim", 303, from: 7599));
            stranger.Api.IncidentCalls.Should().BeEmpty();
            stranger.Sender.AnswerTexts.Should().Contain(t => t!.Contains("chưa liên kết"));
        }
    }
}
