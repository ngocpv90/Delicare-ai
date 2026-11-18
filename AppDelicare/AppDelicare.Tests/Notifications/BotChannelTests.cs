using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Tests.Integration;
using AppDelicare.WebApi.Security;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Notifications
{
    /// <summary>
    /// BOT-1.1 (D-62): API nội bộ BotChannel (khóa client X-Client-Key, chống trùng update, claim / complete, outbox claim / mark, thử lại có giãn cách,
    /// notBefore, người dùng chặn bot) + API web Reprocess / Enqueue / Retry / GetConversation + che bí mật ConfigJson. DB InMemory (song song: SQL Server test).
    /// </summary>
    public class BotChannelTests : IClassFixture<InMemoryApiFactory>
    {
        public const string ClientKey = "test-messaging-client-key-0123456789";
        private static readonly Guid VhChannel = Guid.Parse("b0710000-0000-4000-8000-000000000001");
        private static readonly Guid DhChannel = Guid.Parse("b0710000-0000-4000-8000-000000000002");
        private static readonly Guid LinkedUser = Guid.Parse("b0710000-0000-4000-8000-0000000000a1");
        private static readonly Guid LinkedEmployee = Guid.Parse("b0710000-0000-4000-8000-0000000000e1");
        private readonly InMemoryApiFactory _factory;

        public BotChannelTests(InMemoryApiFactory factory)
        {
            _factory = factory;
            _factory.Clock.FixedNow = new DateTime(2031, 5, 10, 9, 0, 0);
            _factory.Settings["Clients:MESSAGING:KeyHash"] = ClientKeyFilter.HashKey(ClientKey);
        }

        private async Task SeedAsync()
        {
            await _factory.SeedAsync(async db =>
            {
                if (await db.IntegrationChannel.AnyAsync(c => c.Id == VhChannel))
                {
                    return;
                }
                db.IntegrationChannel.AddRange(
                    new IntegrationChannel { Id = VhChannel, ChannelCode = "TELEGRAM_VH", ChannelName = "VH", ProviderCode = "TELEGRAM", EnvironmentCode = "Development",
                        ChannelStatus = "Active", ConfigJson = """{"groupChatId":"-100123","webhookSecret":"s3cr3t","nested":{"apiKey":"k-1","radius":150}}""" },
                    new IntegrationChannel { Id = DhChannel, ChannelCode = "TELEGRAM_DH", ChannelName = "DH", ProviderCode = "TELEGRAM", EnvironmentCode = "Development",
                        ChannelStatus = "Active", ConfigJson = "{}" });
                db.BotConfiguration.AddRange(
                    new BotConfiguration { Id = Guid.NewGuid(), IntegrationChannelId = VhChannel, BotCode = "VH", BotName = "Vận hành", BotStatus = "Active" },
                    new BotConfiguration { Id = Guid.NewGuid(), IntegrationChannelId = DhChannel, BotCode = "DH", BotName = "Điều hành", BotStatus = "Active" });
                db.UserExternalIdentity.AddRange(
                    new UserExternalIdentity { Id = Guid.NewGuid(), UserId = LinkedUser, IntegrationChannelId = VhChannel, ExternalUserId = "7001", LinkedAt = new DateTime(2031, 5, 1), IdentityStatus = "Active" },
                    new UserExternalIdentity { Id = Guid.NewGuid(), EmployeeId = LinkedEmployee, IntegrationChannelId = VhChannel, ExternalUserId = "7002", LinkedAt = new DateTime(2031, 5, 1), IdentityStatus = "Active" });
            });
        }

        private HttpClient Bot(string? key = ClientKey)
        {
            var client = _factory.CreateClient();
            if (key != null)
            {
                client.DefaultRequestHeaders.Add(ClientKeyFilter.HeaderName, key);
            }
            return client;
        }

        private static async Task<LegacyResult> Post(HttpClient client, string action, object body)
            => await LegacyResult.ReadAsync(await client.PostAsJsonAsync("/api/BotChannel/" + action, body));

        [Fact]
        public async Task Internal_api_requires_client_key_not_user_token()
        {
            await SeedAsync();
            object body = new { botCode = "VH", max = 1 };
            using (var none = Bot(null))
            {
                (await none.PostAsJsonAsync("/api/BotChannel/ClaimInbound", body)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            }
            using (var wrong = Bot("sai-khoa"))
            {
                (await wrong.PostAsJsonAsync("/api/BotChannel/ClaimInbound", body)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            }
            using (var user = _factory.CreateAuthorizedClient())
            {
                (await user.PostAsJsonAsync("/api/BotChannel/ClaimInbound", body)).StatusCode.Should().Be(HttpStatusCode.Unauthorized, "JWT admin web không thay được khóa client");
            }
            using var ok = Bot();
            (await ok.PostAsJsonAsync("/api/BotChannel/ClaimInbound", body)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Same_update_twice_creates_one_row_and_claim_complete_cycle()
        {
            await SeedAsync();
            using var bot = Bot();
            object update = new { botCode = "vh", updateId = 900001L, externalUserId = "7001", externalConversationId = "7001", messageType = "message",
                payloadJson = """{"update_id":900001,"message":{"text":"/start"}}""" };
            var first = await Post(bot, "ReceiveUpdate", update);
            var firstData = first.Ok();
            firstData.GetProperty("duplicate").GetBoolean().Should().BeFalse();
            var second = (await Post(bot, "ReceiveUpdate", update)).Ok();
            second.GetProperty("duplicate").GetBoolean().Should().BeTrue();
            second.GetProperty("id").GetGuid().Should().Be(firstData.GetProperty("id").GetGuid());
            (await _factory.QueryAsync(db => db.InboundMessage.CountAsync(m => m.IdempotencyKey == "TG:VH:900001"))).Should().Be(1);
            (await _factory.QueryAsync(db => db.IntegrationLog.CountAsync(l => l.IdempotencyKey == "TG:VH:900001" && l.DirectionCode == "IN"))).Should().Be(2);

            (await Post(bot, "ReceiveUpdate", new { botCode = "XX", updateId = 1L })).Fail("Bot chưa khai báo");
            (await Post(bot, "ReceiveUpdate", new { botCode = "VH" })).Fail("update_id");

            var claimed = await Post(bot, "ClaimInbound", new { botCode = "VH", max = 10 });
            claimed.Ok();
            var item = claimed.ListData.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == firstData.GetProperty("id").GetGuid());
            item.GetProperty("botCode").GetString().Should().Be("VH");
            item.GetProperty("payloadJson").GetString().Should().Contain("/start");
            (await Post(bot, "ClaimInbound", new { botCode = "VH", max = 10 })).ListData.EnumerateArray()
                .Should().NotContain(x => x.GetProperty("id").GetGuid() == firstData.GetProperty("id").GetGuid(), "đang Processing – không trả lần 2");

            Guid id = firstData.GetProperty("id").GetGuid();
            (await Post(bot, "CompleteInbound", new { id, status = "Done" })).Fail("không hợp lệ");
            (await Post(bot, "CompleteInbound", new { id, status = "Failed", error = "lỗi thử" })).Ok();
            (await Post(bot, "CompleteInbound", new { id, status = "Processed" })).Fail("đang xử lý");
            var stored = await _factory.QueryAsync(db => db.InboundMessage.AsNoTracking().SingleAsync(m => m.Id == id));
            stored.ProcessingStatus.Should().Be("Failed");
            stored.ErrorMessage.Should().Be("lỗi thử");

            // Web: xử lý lại → Received → lấy lại được.
            using var web = _factory.CreateAuthorizedClient();
            (await web.PostFormAsync("/api/InboundMessage/Reprocess", ("id", id))).Ok();
            (await web.PostFormAsync("/api/InboundMessage/Reprocess", ("id", id))).Fail("Lỗi / Bỏ qua");
            (await Post(bot, "ClaimInbound", new { botCode = "VH" })).ListData.EnumerateArray().Should().Contain(x => x.GetProperty("id").GetGuid() == id);
        }

        [Fact]
        public async Task Outbox_manual_enqueue_claim_retry_backoff_and_blocked()
        {
            await SeedAsync();
            using var web = _factory.CreateAuthorizedClient();
            using var bot = Bot();
            (await web.PostFormAsync("/api/OutboundMessage/Enqueue", ("UserId", LinkedUser), ("Text", ""))).Fail("Nhập nội dung");
            (await web.PostFormAsync("/api/OutboundMessage/Enqueue", ("Text", "Xin chào"))).Fail("Chọn người nhận");
            (await web.PostFormAsync("/api/OutboundMessage/Enqueue", ("UserId", Guid.NewGuid()), ("Text", "Xin chào"))).Fail("chưa liên kết");
            var queued = (await web.PostFormAsync("/api/OutboundMessage/Enqueue", ("UserId", LinkedUser), ("Text", "Ca sáng đổi giờ vào 6:15"))).Ok();
            Guid msgId = queued.GetProperty("messageIds")[0].GetGuid();
            var msg = await _factory.QueryAsync(db => db.OutboundMessage.AsNoTracking().SingleAsync(m => m.Id == msgId));
            msg.MessageStatus.Should().Be("Queued");
            msg.RecipientExternalId.Should().Be("7001");
            using (var doc = JsonDocument.Parse(msg.PayloadJson))
            {
                doc.RootElement.GetProperty("template").GetString().Should().Be("MANUAL_TEXT");
                doc.RootElement.GetProperty("data").GetProperty("text").GetString().Should().Be("Ca sáng đổi giờ vào 6:15");
                doc.RootElement.GetProperty("level").GetString().Should().Be("NORMAL");
            }

            JsonElement[] Claim() => Post(bot, "ClaimOutbound", new { botCode = "VH", max = 20 }).GetAwaiter().GetResult().ListData.EnumerateArray().ToArray();
            Claim().Should().Contain(x => x.GetProperty("id").GetGuid() == msgId);
            Claim().Should().NotContain(x => x.GetProperty("id").GetGuid() == msgId, "đang Sending");

            // Lỗi lần 1 → chờ 1 phút mới thử lại.
            (await Post(bot, "MarkOutbound", new { id = msgId, status = "Failed", error = "timeout", httpStatus = 502 })).Ok();
            (await Post(bot, "MarkOutbound", new { id = msgId, status = "Sent" })).Fail("đang gửi");
            Claim().Should().NotContain(x => x.GetProperty("id").GetGuid() == msgId, "chưa tới giờ thử lại");
            _factory.Clock.FixedNow = _factory.Clock.FixedNow!.Value.AddMinutes(2);
            var retried = Claim().Single(x => x.GetProperty("id").GetGuid() == msgId);
            retried.GetProperty("retryCount").GetInt32().Should().Be(1);
            (await Post(bot, "MarkOutbound", new { id = msgId, status = "Sent", providerMessageId = "555" })).Ok();
            msg = await _factory.QueryAsync(db => db.OutboundMessage.AsNoTracking().SingleAsync(m => m.Id == msgId));
            msg.MessageStatus.Should().Be("Sent");
            msg.ProviderMessageId.Should().Be("555");
            msg.SentAt.Should().Be(_factory.Clock.FixedNow);

            // Nhân viên chặn bot → Blocked, định danh Blocked, không gửi lại.
            var toEmployee = (await web.PostFormAsync("/api/OutboundMessage/Enqueue", ("EmployeeId", LinkedEmployee), ("Text", "Nhắc chấm công"))).Ok();
            Guid blockedId = toEmployee.GetProperty("messageIds")[0].GetGuid();
            Claim().Should().Contain(x => x.GetProperty("id").GetGuid() == blockedId);
            (await Post(bot, "MarkOutbound", new { id = blockedId, status = "Blocked", error = "Forbidden: bot was blocked by the user", httpStatus = 403 })).Ok();
            (await _factory.QueryAsync(db => db.UserExternalIdentity.AsNoTracking().SingleAsync(i => i.EmployeeId == LinkedEmployee))).IdentityStatus.Should().Be("Blocked");
            (await web.PostFormAsync("/api/OutboundMessage/Retry", ("id", blockedId))).Fail("Lỗi / Hủy");
            (await web.PostFormAsync("/api/OutboundMessage/Enqueue", ("EmployeeId", LinkedEmployee), ("Text", "x"))).Fail("chưa liên kết");

            // Hội thoại của user: tin ra đã gửi.
            var conv = (await web.GetResultAsync($"/api/OutboundMessage/GetConversation?UserId={LinkedUser}&BotCode=VH")).Ok();
            conv.GetProperty("items").EnumerateArray().Should().Contain(x => x.GetProperty("id").GetGuid() == msgId && x.GetProperty("direction").GetString() == "OUT");
            (await web.GetResultAsync("/api/OutboundMessage/GetConversation")).Fail("Chọn định danh");
        }

        [Fact]
        public async Task Retry_exhausted_stops_and_not_before_is_respected()
        {
            await SeedAsync();
            using var bot = Bot();
            DateTime now = _factory.Clock.FixedNow!.Value;
            Guid exhausted = Guid.NewGuid(), later = Guid.NewGuid(), manualRetry = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.OutboundMessage.AddRange(
                    new OutboundMessage { Id = exhausted, IntegrationChannelId = DhChannel, RecipientExternalId = "8001", PayloadJson = "{}", QueuedAt = now.AddHours(-10),
                        MessageStatus = "Failed", RetryCount = 6, ModifiedDate = now.AddHours(-9) },
                    new OutboundMessage { Id = later, IntegrationChannelId = DhChannel, RecipientExternalId = "8001", QueuedAt = now,
                        PayloadJson = $$"""{"template":"SYSTEM_TEST","notBefore":"{{now.AddMinutes(30):yyyy-MM-ddTHH:mm:ss}}"}""", MessageStatus = "Queued" },
                    new OutboundMessage { Id = manualRetry, IntegrationChannelId = DhChannel, RecipientExternalId = "8001", PayloadJson = "{}", QueuedAt = now.AddHours(-10),
                        MessageStatus = "Failed", RetryCount = 6, ModifiedDate = now.AddHours(-9) });
                return Task.CompletedTask;
            });
            var ids = (await Post(bot, "ClaimOutbound", new { botCode = "DH" })).ListData.EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToList();
            ids.Should().NotContain([exhausted, later, manualRetry]);
            _factory.Clock.FixedNow = now.AddMinutes(31);
            using var web = _factory.CreateAuthorizedClient();
            (await web.PostFormAsync("/api/OutboundMessage/Retry", ("id", manualRetry))).Ok();
            ids = (await Post(bot, "ClaimOutbound", new { botCode = "DH" })).ListData.EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToList();
            ids.Should().Contain([later, manualRetry]).And.NotContain(exhausted);
            _factory.Clock.FixedNow = now;
        }

        [Fact]
        public async Task Channel_config_secrets_are_masked_and_kept_on_update()
        {
            await SeedAsync();
            using var web = _factory.CreateAuthorizedClient();
            var one = (await web.GetResultAsync($"/api/IntegrationChannel/GetById?id={VhChannel}")).Ok();
            string masked = one.GetProperty("configJson").GetString()!;
            masked.Should().NotContain("s3cr3t").And.NotContain("k-1").And.Contain("-100123").And.Contain("150");
            var all = await web.GetResultAsync("/api/IntegrationChannel/GetAllData");
            all.Ok();
            all.ListData.GetRawText().Should().NotContain("s3cr3t").And.NotContain("k-1");
            (await _factory.QueryAsync(db => db.IntegrationChannel.AsNoTracking().SingleAsync(c => c.Id == VhChannel))).ConfigJson.Should().Contain("s3cr3t", "đọc không ghi ngược bản đã che");

            // Form gửi lại bản đã che + đổi bán kính → bí mật giữ nguyên.
            string edited = masked.Replace("150", "200");
            (await web.PutFormAsync("/api/IntegrationChannel/Update", ("Id", VhChannel), ("ChannelCode", "TELEGRAM_VH"), ("ChannelName", "VH"), ("ProviderCode", "TELEGRAM"),
                ("EnvironmentCode", "Development"), ("ChannelStatus", "Active"), ("ConfigJson", edited))).Ok();
            string saved = (await _factory.QueryAsync(db => db.IntegrationChannel.AsNoTracking().SingleAsync(c => c.Id == VhChannel))).ConfigJson!;
            saved.Should().Contain("s3cr3t").And.Contain("k-1").And.Contain("200").And.NotContain(MessagingRulesMasked);
        }

        private const string MessagingRulesMasked = "***";

        [Fact]
        public async Task Integration_log_is_read_only()
        {
            using var web = _factory.CreateAuthorizedClient();
            var response = await web.PostAsync("/api/IntegrationLog/Insert", LegacyHttp.Form(("DirectionCode", "IN"), ("OperationCode", "X")));
            response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
        }

        [Fact]
        public void Mask_json_hides_secret_keys_at_any_level()
        {
            AppDelicare.Services.Implement.Messaging.MessagingRules.MaskJson("""{"token":"a","list":[{"secretValue":"b"}],"name":"x"}""")
                .Should().Be("""{"token":"***","list":[{"secretValue":"***"}],"name":"x"}""");
            AppDelicare.Services.Implement.Messaging.MessagingRules.MaskJson("không phải json").Should().Be("***");
            MessagingCode.Outbound.Blocked.Should().Be("Blocked");
        }
    }
}
