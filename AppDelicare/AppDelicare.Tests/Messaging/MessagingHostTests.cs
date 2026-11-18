using System.Net;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using AppDelicare.Messaging;
using AppDelicare.Messaging.Audiences.Internal.Chung;
using AppDelicare.Messaging.Channels;
using AppDelicare.Messaging.Channels.Telegram;
using AppDelicare.Messaging.Channels.Telegram.Models;
using AppDelicare.Messaging.Core;
using AppDelicare.Messaging.Core.Contracts;
using AppDelicare.Messaging.Options;
using AppDelicare.Messaging.Presentation;
using AppDelicare.Messaging.Processing;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AppDelicare.Tests.Messaging
{
    /// <summary>
    /// BOT-1.3 (D-62): container messaging – không ProjectReference / chuỗi DB, DTO khớp api, webhook (secret sai 401 không ghi gì, api tắt 503),
    /// parser / formatter, đăng ký (QR cổng → nút số điện thoại, số của người khác, /start mã, /huylienket), InboundPump quét lại (khởi động lại không mất tin),
    /// OutboundPump (Sent / 403 Blocked / 429 tạm dừng bot, tin THƯỜNG im lặng).
    /// </summary>
    public class MessagingHostTests
    {
        private static readonly string MessagingDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AppDelicare.Messaging"));

        private static string Update(long id, string text, long from = 7001, string? contactJson = null)
        {
            string body = contactJson == null ? ",\"text\":" + JsonSerializer.Serialize(text) : ",\"contact\":" + contactJson;
            return "{\"update_id\":" + id + ",\"message\":{\"message_id\":" + id + ",\"date\":1,\"from\":{\"id\":" + from
                + ",\"is_bot\":false,\"first_name\":\"An\",\"username\":\"an_nv\"},\"chat\":{\"id\":" + from + ",\"type\":\"private\"}" + body + "}}";
        }

        [Fact]
        public void Project_has_no_project_reference_package_or_db_connection()
        {
            var csproj = XDocument.Load(Path.Combine(MessagingDir, "AppDelicare.Messaging.csproj"));
            csproj.Descendants("ProjectReference").Should().BeEmpty("messaging không tham chiếu Services / Data / Model (D-62)");
            csproj.Descendants("PackageReference").Should().BeEmpty("không thêm gói NuGet");
            File.ReadAllText(Path.Combine(MessagingDir, "appsettings.json")).Should().NotContainEquivalentOf("ConnectionStrings");
            Directory.EnumerateFiles(MessagingDir, "*.cs", SearchOption.AllDirectories).Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                .Select(File.ReadAllText).Should().NotContain(s => s.Contains("SqlConnection") || s.Contains("ConnectionStrings"));
        }

        [Theory]
        [InlineData(typeof(BotUpdateRequest), typeof(AppDelicare.Model.Model.BotUpdateRequest))]
        [InlineData(typeof(BotUpdateResultModel), typeof(AppDelicare.Model.Model.BotUpdateResultModel))]
        [InlineData(typeof(BotClaimRequest), typeof(AppDelicare.Model.Model.BotClaimRequest))]
        [InlineData(typeof(BotInboundItemModel), typeof(AppDelicare.Model.Model.BotInboundItemModel))]
        [InlineData(typeof(BotCompleteInboundRequest), typeof(AppDelicare.Model.Model.BotCompleteInboundRequest))]
        [InlineData(typeof(BotOutboundItemModel), typeof(AppDelicare.Model.Model.BotOutboundItemModel))]
        [InlineData(typeof(BotMarkOutboundRequest), typeof(AppDelicare.Model.Model.BotMarkOutboundRequest))]
        [InlineData(typeof(BotIdentityRequest), typeof(AppDelicare.Model.Model.BotIdentityRequest))]
        [InlineData(typeof(BotLinkByPhoneRequest), typeof(AppDelicare.Model.Model.BotLinkByPhoneRequest))]
        [InlineData(typeof(BotLinkByCodeRequest), typeof(AppDelicare.Model.Model.BotLinkByCodeRequest))]
        [InlineData(typeof(BotTouchRequest), typeof(AppDelicare.Model.Model.BotTouchRequest))]
        [InlineData(typeof(BotIdentityModel), typeof(AppDelicare.Model.Model.BotIdentityModel))]
        [InlineData(typeof(UserTokenModel), typeof(AppDelicare.Model.Model.UserTokenModel))]
        [InlineData(typeof(MyAttendanceModel), typeof(AppDelicare.Model.Model.MyAttendanceModel))]
        public void Contract_fields_match_api_model(Type messaging, Type model)
        {
            var api = model.GetProperties().ToDictionary(p => p.Name, p => p.PropertyType);
            foreach (var p in messaging.GetProperties().Where(p => p.Name != "IsOk"))
            {
                api.Should().ContainKey(p.Name, $"{messaging.Name}.{p.Name} phải có ở {model.Name}");
                if (!p.PropertyType.IsGenericType)
                {
                    (Nullable.GetUnderlyingType(api[p.Name]) ?? api[p.Name]).Should().Be(Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType, $"{messaging.Name}.{p.Name} cùng kiểu");
                }
            }
        }

        [Fact]
        public void Parser_reads_command_contact_callback_and_member()
        {
            var cmd = TelegramUpdateParser.Parse(TelegramUpdateParser.Deserialize(Update(5, "/start@delicare_vh_bot  CHAMCONG")))!;
            cmd.Kind.Should().Be(IncomingKind.Command);
            cmd.Command.Should().Be("start");
            cmd.Argument.Should().Be("CHAMCONG");
            cmd.FromId.Should().Be("7001");
            var contact = TelegramUpdateParser.Parse(TelegramUpdateParser.Deserialize(Update(6, "", contactJson: """{"phone_number":"+84901234567","user_id":7001}""")))!;
            contact.Kind.Should().Be(IncomingKind.Contact);
            contact.Contact!.UserId.Should().Be(7001);
            var cb = TelegramUpdateParser.Parse(TelegramUpdateParser.Deserialize("""{"update_id":7,"callback_query":{"id":"cb1","from":{"id":9},"data":"apr:ok:1","message":{"message_id":3,"date":1,"chat":{"id":9,"type":"private"}}}}"""))!;
            cb.Kind.Should().Be(IncomingKind.Callback);
            cb.CallbackData.Should().Be("apr:ok:1");
            var member = TelegramUpdateParser.Parse(TelegramUpdateParser.Deserialize("""{"update_id":8,"my_chat_member":{"chat":{"id":9,"type":"private"},"from":{"id":9},"new_chat_member":{"status":"kicked"}}}"""))!;
            member.Kind.Should().Be(IncomingKind.ChatMember);
            member.MemberStatus.Should().Be("kicked");
            TelegramUpdateParser.Peek("không phải json").Should().BeNull();
        }

        [Fact]
        public void Formatter_escapes_html_cuts_long_text_and_templates_render()
        {
            var (html, kb) = TelegramFormatter.Render(new NeutralMessage { Title = "<Đơn> & suất", Lines = ["a<b"], Buttons = [new("Mở", Url: "https://t.me/x")] });
            html.Should().Be("<b>&lt;Đơn&gt; &amp; suất</b>\na&lt;b");
            kb!.InlineKeyboard[0][0].Url.Should().Be("https://t.me/x");
            TelegramFormatter.Render(NeutralMessage.Text(string.Join('\n', Enumerable.Repeat(new string('x', 99), 60)))).Html.Length.Should().BeLessThanOrEqualTo(TelegramFormatter.MaxLength + 2);
            var manual = MessageTemplates.Render("""{"template":"MANUAL_TEXT","data":{"text":"Ca sáng đổi giờ"},"level":"NORMAL"}""");
            manual.Lines.Should().Equal("Ca sáng đổi giờ");
            manual.Level.Should().Be("NORMAL");
            MessageTemplates.Render("""{"template":"KHONG_CO","data":{}}""").Title.Should().Be("Có thông báo mới");
            MessageTemplates.Render("hỏng").Title.Should().Be("Có thông báo mới");
        }

        private static (WebApplication App, FakeBotChannelApi Api) Host(Action<FakeBotChannelApi>? setup = null)
        {
            var api = new FakeBotChannelApi();
            setup?.Invoke(api);
            var app = MessagingProgram.Build([], b =>
            {
                b.WebHost.UseTestServer();
                b.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Telegram:Bots:VH:Token"] = "123:test-token",
                    ["Telegram:Bots:VH:WebhookSecret"] = "dung-secret",
                    ["Messaging:Mode"] = "Webhook",
                });
                b.Services.RemoveAll<IHostedService>();
                b.Services.RemoveAll<IBotChannelApi>();
                b.Services.AddSingleton<IBotChannelApi>(api);
            });
            return (app, api);
        }

        [Fact]
        public async Task Webhook_checks_secret_stores_update_and_returns_503_when_api_down()
        {
            var (app, api) = Host();
            await app.StartAsync();
            try
            {
                var client = app.GetTestClient();
                HttpRequestMessage Req(string? secret, string bot = "VH")
                {
                    var r = new HttpRequestMessage(HttpMethod.Post, "/tg/" + bot) { Content = new StringContent(Update(900, "/start"), Encoding.UTF8, "application/json") };
                    if (secret != null)
                    {
                        r.Headers.Add(TelegramSecretValidator.HeaderName, secret);
                    }
                    return r;
                }
                (await client.SendAsync(Req(null))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
                (await client.SendAsync(Req("sai"))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
                api.Received.Should().BeEmpty("secret sai → không ghi gì");
                (await client.SendAsync(Req("dung-secret", "DH"))).StatusCode.Should().Be(HttpStatusCode.NotFound, "bot chưa cấu hình");
                (await client.SendAsync(Req("dung-secret"))).StatusCode.Should().Be(HttpStatusCode.OK);
                api.Received.Should().ContainSingle().Which.Should().Match<BotUpdateRequest>(r => r.BotCode == "VH" && r.UpdateId == 900 && r.ExternalUserId == "7001" && r.MessageType == "message");
                api.Down = true;
                (await client.SendAsync(Req("dung-secret"))).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable, "api tắt → Telegram gửi lại");
                var health = await client.GetAsync("/health");
                health.StatusCode.Should().Be(HttpStatusCode.OK);
                (await health.Content.ReadAsStringAsync()).Should().Contain("\"VH\"").And.NotContain("test-token").And.NotContain("dung-secret");
            }
            finally
            {
                await app.StopAsync();
                await app.DisposeAsync();
            }
        }

        private sealed class Fixture
        {
            public FakeBotChannelApi Api { get; } = new();
            public FakeSender Sender { get; } = new();
            public UpdateDispatcher Dispatcher { get; }
            public InboundPump Inbound { get; }

            public Fixture()
            {
                var host = new StaticOptions<MessagingHostOptions>(new MessagingHostOptions { IdentityCacheSeconds = 0 });
                var tg = new StaticOptions<TelegramOptions>(new TelegramOptions { Bots = { ["VH"] = new() { Token = "t" }, ["DH"] = new() { Token = "t2", Username = "delicare_dh_bot" } } });
                var users = new BotUserContext(Api, host, TimeProvider.System);
                var tokens = new UserTokenCache(Api, TimeProvider.System);
                var onboarding = new OnboardingActions(Api, users, tokens, Sender, tg);
                var help = new HelpActions(Api, new NoWebApi(), tokens, Sender);
                Dispatcher = new UpdateDispatcher(onboarding, help, users, Api, Sender);
                Inbound = new InboundPump(Api, Dispatcher, new InboundSignal(), host, NullLogger<InboundPump>.Instance);
            }

            public BotInboundItemModel Item(string json) => new() { Id = Guid.NewGuid(), BotCode = "VH", PayloadJson = json };
        }

        [Fact]
        public async Task Onboarding_phone_share_full_flow_and_code_and_unlink()
        {
            var f = new Fixture();
            // QR cổng: /start CHAMCONG, chưa liên kết → nút chia sẻ số điện thoại (request_contact).
            (await f.Dispatcher.HandleAsync(f.Item(Update(1, "/start CHAMCONG")), default)).Status.Should().Be("Processed");
            f.Sender.Sent.Last().Message.ReplyMarkup.Should().BeOfType<TgReplyKeyboard>().Which.Keyboard[0][0].RequestContact.Should().BeTrue();
            // Chia sẻ số của người khác → từ chối, không gọi LinkByPhone.
            await f.Dispatcher.HandleAsync(f.Item(Update(2, "", contactJson: """{"phone_number":"0901","user_id":5555}""")), default);
            f.Api.Calls.OfType<BotLinkByPhoneRequest>().Should().BeEmpty();
            f.Sender.Sent.Last().Message.Text.Should().Contain("của chính bạn");
            // Số của chính mình → LinkByPhone → chào + 3 nút + nút mở bot DH (có quyền DH).
            f.Api.LinkResult = _ => new ApiResult<BotIdentityModel>
            {
                ResponseCode = 200, Data = new BotIdentityModel { Linked = true, CanUseBot = true, CanUseDieuHanh = true, NeedsCode = true, FullName = "Nguyễn An", HasWebAccount = true },
            };
            await f.Dispatcher.HandleAsync(f.Item(Update(3, "", contactJson: """{"phone_number":"+84901234567","user_id":7001}""")), default);
            var link = f.Api.Calls.OfType<BotLinkByPhoneRequest>().Single();
            link.ContactUserId.Should().Be("7001");
            link.Phone.Should().Be("+84901234567");
            var sent = f.Sender.Sent.ToArray();
            sent[^2].Message.Text.Should().Contain("Xin chào Nguyễn An").And.Contain("quyền duyệt");
            sent[^2].Message.ReplyMarkup.Should().BeOfType<TgReplyKeyboard>().Which.Keyboard[0].Should().HaveCount(3);
            sent[^1].Message.ReplyMarkup.Should().BeOfType<TgInlineKeyboard>().Which.InlineKeyboard[0][0].Url.Should().StartWith("https://t.me/delicare_dh_bot");
            // Không khớp số → câu báo của api.
            f.Api.LinkResult = _ => new ApiResult<BotIdentityModel> { ResponseCode = 400, ResponseMessage = "Không tìm thấy … liên hệ phòng Nhân sự" };
            await f.Dispatcher.HandleAsync(f.Item(Update(4, "/start AbCdEfGhIjKlMnOpQrStUvWxYz0123456789_-abcdef")), default);
            f.Api.Calls.OfType<BotLinkByCodeRequest>().Single().Code.Should().Be("AbCdEfGhIjKlMnOpQrStUvWxYz0123456789_-abcdef");
            f.Sender.Sent.Last().Message.Text.Should().Contain("Nhân sự");
            // /huylienket.
            await f.Dispatcher.HandleAsync(f.Item(Update(5, "/huylienket")), default);
            f.Api.Calls.OfType<BotIdentityRequest>().Should().Contain(r => r.GetType() == typeof(BotIdentityRequest) && r.ExternalUserId == "7001");
            f.Sender.Sent.Last().Message.Text.Should().Contain("Đã hủy liên kết");
        }

        [Fact]
        public async Task Inbound_pump_scans_claimed_updates_after_restart_and_marks_results()
        {
            var f = new Fixture();
            f.Api.Identity = new BotIdentityModel { Linked = true, CanUseBot = true, EmployeeId = Guid.NewGuid() };
            var ok = f.Item(Update(10, "❓ Trợ giúp"));
            var broken = f.Item("{không phải json");
            var group = f.Item("""{"update_id":11,"message":{"message_id":1,"date":1,"from":{"id":1},"chat":{"id":-100,"type":"group"},"text":"hi"}}""");
            var blocked = f.Item("""{"update_id":12,"my_chat_member":{"chat":{"id":7001,"type":"private"},"from":{"id":7001},"new_chat_member":{"status":"kicked"}}}""");
            f.Api.InboundQueue.AddRange([ok, broken, group, blocked]);
            // Không có tín hiệu webhook (giả lập vừa khởi động lại) – 1 lượt quét vẫn lấy hết.
            (await f.Inbound.RunOnceAsync(default)).Should().Be(4);
            var done = f.Api.Completed.ToDictionary(c => c.Id, c => c.Status);
            done[ok.Id].Should().Be("Processed");
            done[broken.Id].Should().Be("Ignored");
            done[group.Id].Should().Be("Ignored");
            done[blocked.Id].Should().Be("Processed");
            f.Api.Calls.OfType<BotTouchRequest>().Should().Contain(t => t.Blocked == true);
            f.Sender.Sent.Should().ContainSingle(s => s.Message.Text.Contains("Trợ giúp"));
            // api tắt giữa chừng → không báo Complete (dòng giữ Processing, hết hạn giữ thì được lấy lại).
            f.Api.InboundQueue.Add(f.Item(Update(13, "/help")));
            f.Api.Down = true;
            await FluentActions.Awaiting(() => f.Inbound.RunOnceAsync(default)).Should().ThrowAsync<ApiUnavailableException>();
        }

        [Fact]
        public async Task Outbound_pump_sends_marks_blocked_and_pauses_bot_on_429()
        {
            var api = new FakeBotChannelApi();
            var sender = new FakeSender();
            var sending = new StaticOptions<SendingOptions>(new SendingOptions { MaxPerSecond = 100, PerChatPerSecond = 100 });
            var limiter = new SendRateLimiter(sending, TimeProvider.System);
            var pump = new OutboundPump(api, sender, limiter, new StaticOptions<MessagingHostOptions>(new()), sending,
                new StaticOptions<TelegramOptions>(new TelegramOptions { Bots = { ["VH"] = new() { Token = "t" } } }), NullLogger<OutboundPump>.Instance);
            BotOutboundItemModel Out(string chat, string level) => new()
            {
                Id = Guid.NewGuid(), BotCode = "VH", RecipientExternalId = chat, PayloadJson = $$"""{"template":"MANUAL_TEXT","data":{"text":"Xin chào"},"level":"{{level}}"}""",
            };
            var normal = Out("1", "NORMAL");
            var urgent = Out("2", "URGENT");
            var blocked = Out("3", "NORMAL");
            var limited = Out("4", "NORMAL");
            var afterLimit = Out("5", "NORMAL");
            api.OutboundQueue.AddRange([normal, urgent, blocked, limited, afterLimit]);
            sender.Next.Enqueue(ChannelSendResult.Sent("501"));
            sender.Next.Enqueue(ChannelSendResult.Sent("502"));
            sender.Next.Enqueue(ChannelSendResult.Blocked("Forbidden: bot was blocked by the user"));
            sender.Next.Enqueue(ChannelSendResult.Retry(30, "Too Many Requests"));
            (await pump.RunOnceAsync(default)).Should().Be(5);
            var marks = api.Marked.ToDictionary(m => m.Id!.Value);
            marks[normal.Id].Status.Should().Be("Sent");
            marks[normal.Id].ProviderMessageId.Should().Be("501");
            marks[blocked.Id].Status.Should().Be("Blocked");
            marks[limited.Id].Status.Should().Be("Failed");
            marks[afterLimit.Id].Status.Should().Be("Failed", "bot đang tạm dừng (429) → không gửi, api thử lại sau");
            sender.Sent.Should().HaveCount(4);
            sender.Sent.Single(s => s.Message.ChatId == "1").Message.DisableNotification.Should().BeTrue("tin THƯỜNG gửi im lặng");
            sender.Sent.Single(s => s.Message.ChatId == "2").Message.DisableNotification.Should().BeFalse("tin KHẨN có chuông");
            limiter.IsPaused("VH").Should().BeTrue();
            (await pump.RunOnceAsync(default)).Should().Be(0, "bot tạm dừng – không lấy thêm tin");
        }

        [Fact]
        public void Rate_limiter_enforces_one_message_per_second_per_chat()
        {
            var limiter = new SendRateLimiter(new StaticOptions<SendingOptions>(new SendingOptions { MaxPerSecond = 25, PerChatPerSecond = 1 }), TimeProvider.System);
            limiter.Delay("VH", "1").Should().Be(TimeSpan.Zero);
            limiter.Record("VH", "1");
            limiter.Delay("VH", "1").Should().BeGreaterThan(TimeSpan.FromMilliseconds(500));
            limiter.Delay("VH", "2").Should().Be(TimeSpan.Zero, "chat khác không phải chờ");
        }

        private sealed class NoWebApi : IDelicareApiClient
        {
            public Task<ApiResult<T>> PostInternalAsync<T>(string action, object body, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<ApiResult<T>> GetAsUserAsync<T>(string path, string bearerToken, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<ApiResult<T>> PostFormAsUserAsync<T>(string path, IEnumerable<KeyValuePair<string, string>> form, string bearerToken, CancellationToken ct = default) => throw new NotSupportedException();
        }
    }

    internal sealed class StaticOptions<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;
        public T Get(string? name) => value;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
