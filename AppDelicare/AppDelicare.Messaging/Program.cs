using AppDelicare.Messaging.Audiences.Internal;
using AppDelicare.Messaging.Audiences.Internal.Chung;
using AppDelicare.Messaging.Conversations;
using AppDelicare.Messaging.Experience;
using AppDelicare.Messaging.Channels;
using AppDelicare.Messaging.Channels.Telegram;
using AppDelicare.Messaging.Core;
using AppDelicare.Messaging.Options;
using AppDelicare.Messaging.Processing;
using Microsoft.Extensions.Options;

namespace AppDelicare.Messaging
{
    /// <summary>
    /// BOT-1.3 (D-62): container <c>messaging</c> – tất cả bot. Kestrel: <c>POST /tg/{botCode}</c> (webhook Telegram), <c>GET /health</c>;
    /// <c>dotnet AppDelicare.Messaging.dll --healthcheck</c> = gọi /health của chính nó (HEALTHCHECK Docker). Không chạm DB – mọi thứ qua api.
    /// </summary>
    public static class MessagingProgram
    {
        public static async Task<int> Main(string[] args)
        {
            if (args.Contains("--healthcheck"))
            {
                return await HealthCheckAsync();
            }
            var app = Build(args);
            await app.RunAsync();
            return 0;
        }

        /// <summary>Dựng host (test truyền <paramref name="configure"/> để thay api / Telegram giả, dùng TestServer).</summary>
        public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Logging.ClearProviders();
            builder.Logging.AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "yyyy-MM-dd HH:mm:ss "; });
            builder.Services.Configure<MessagingHostOptions>(builder.Configuration.GetSection(MessagingHostOptions.Section));
            builder.Services.Configure<TelegramOptions>(builder.Configuration.GetSection(TelegramOptions.Section));
            builder.Services.Configure<SendingOptions>(builder.Configuration.GetSection(SendingOptions.Section));
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddHttpClient<IDelicareApiClient, DelicareApiClient>(c => c.Timeout = TimeSpan.FromSeconds(30));
            builder.Services.AddHttpClient<ITelegramClient, TelegramClient>(c => c.Timeout = TimeSpan.FromSeconds(70));
            builder.Services.AddSingleton<IBotChannelApi, BotChannelApi>();
            builder.Services.AddSingleton<UserTokenCache>();
            builder.Services.AddSingleton<BotUserContext>();
            builder.Services.AddSingleton<IChannelSender, TelegramSender>();
            builder.Services.AddSingleton<InboundSignal>();
            builder.Services.AddSingleton<UpdateIngestor>();
            builder.Services.AddSingleton<SendRateLimiter>();
            builder.Services.AddSingleton<OnboardingActions>();
            // BOT-2.4: QR vật thể.
            builder.Services.AddSingleton<AppDelicare.Messaging.Audiences.Internal.Chung.QrActions>();
            builder.Services.AddSingleton<HelpActions>();
            // BOT-1.4: khung trải nghiệm + duyệt.
            builder.Services.AddSingleton<ReplyContextStore>();
            builder.Services.AddSingleton<UndoWindow>();
            builder.Services.AddSingleton<TaskCard>();
            builder.Services.AddSingleton<SelfServiceActions>();
            builder.Services.AddSingleton<ApprovalActions>();
            builder.Services.AddSingleton<IBotActionProvider, ChungActionProvider>();
            // BOT-1.5a: luồng nhiều bước + phân hệ Bếp, ATTP (mỗi lớp vừa là provider chức năng vừa xử lý nút của mình – 1 instance).
            builder.Services.AddSingleton<ConversationStore>();
            builder.Services.AddSingleton<AppDelicare.Messaging.Conversations.Flows.LeaveRequestFlow>();
            builder.Services.AddSingleton<AppDelicare.Messaging.Conversations.Flows.ShiftSwapFlow>();
            builder.Services.AddSingleton<AppDelicare.Messaging.Audiences.Internal.VanHanh.BepActions>();
            builder.Services.AddSingleton<IBotActionProvider>(sp => sp.GetRequiredService<AppDelicare.Messaging.Audiences.Internal.VanHanh.BepActions>());
            builder.Services.AddSingleton<AppDelicare.Messaging.Audiences.Internal.VanHanh.AttpActions>();
            builder.Services.AddSingleton<IBotActionProvider>(sp => sp.GetRequiredService<AppDelicare.Messaging.Audiences.Internal.VanHanh.AttpActions>());
            // BOT-1.5b: Giao vận (điều phối + tài xế) + Điểm phát.
            builder.Services.AddSingleton<AppDelicare.Messaging.Audiences.Internal.VanHanh.GiaoVanActions>();
            builder.Services.AddSingleton<IBotActionProvider>(sp => sp.GetRequiredService<AppDelicare.Messaging.Audiences.Internal.VanHanh.GiaoVanActions>());
            builder.Services.AddSingleton<AppDelicare.Messaging.Audiences.Internal.VanHanh.DiemPhatActions>();
            builder.Services.AddSingleton<IBotActionProvider>(sp => sp.GetRequiredService<AppDelicare.Messaging.Audiences.Internal.VanHanh.DiemPhatActions>());
            // BOT-1.5c: phản ánh (CSKH + điểm phát), nhân sự (ai chưa vào ca, chấm hộ).
            builder.Services.AddSingleton<AppDelicare.Messaging.Audiences.Internal.VanHanh.PhanAnhActions>();
            builder.Services.AddSingleton<IBotActionProvider>(sp => sp.GetRequiredService<AppDelicare.Messaging.Audiences.Internal.VanHanh.PhanAnhActions>());
            builder.Services.AddSingleton<AppDelicare.Messaging.Audiences.Internal.VanHanh.NhanSuActions>();
            builder.Services.AddSingleton<IBotActionProvider>(sp => sp.GetRequiredService<AppDelicare.Messaging.Audiences.Internal.VanHanh.NhanSuActions>());
            // BOT-1.5c-2: chức năng chỉ xem (Kho, Kế hoạch, Kinh doanh, Kế toán, QA, Quản trị).
            builder.Services.AddSingleton<AppDelicare.Messaging.Audiences.Internal.VanHanh.XemNhanhActions>();
            builder.Services.AddSingleton<IBotActionProvider>(sp => sp.GetRequiredService<AppDelicare.Messaging.Audiences.Internal.VanHanh.XemNhanhActions>());
            // BOT-3.1: 🔍 Truy xuất lô / mẻ (Bếp, QA, Kho).
            builder.Services.AddSingleton<AppDelicare.Messaging.Audiences.Internal.VanHanh.TraCuuActions>();
            builder.Services.AddSingleton<IBotActionProvider>(sp => sp.GetRequiredService<AppDelicare.Messaging.Audiences.Internal.VanHanh.TraCuuActions>());
            // BOT-1.6: bot Điều hành (DH).
            builder.Services.AddSingleton<IBotActionProvider, AppDelicare.Messaging.Audiences.Internal.DieuHanh.DieuHanhActions>();
            builder.Services.AddSingleton<BotActionRegistry>();
            // BOT-2.1: Thẻ ca (làm mới mỗi phút).
            builder.Services.AddSingleton<ShiftCards>();
            // BOT-2.2: sự việc liên phòng.
            builder.Services.AddSingleton<AppDelicare.Messaging.Audiences.Internal.Chung.IncidentActions>();
            builder.Services.AddSingleton(sp => new Lazy<ShiftCards>(() => sp.GetRequiredService<ShiftCards>()));
            builder.Services.AddSingleton<MenuNavigator>();
            builder.Services.AddSingleton<CodeSearch>();
            builder.Services.AddSingleton<CommandScopeSync>();
            builder.Services.AddSingleton<UpdateDispatcher>();
            builder.Services.AddHostedService<InboundPump>();
            builder.Services.AddHostedService<OutboundPump>();
            builder.Services.AddHostedService<ShiftCardPump>();
            builder.Services.AddHostedService<AppDelicare.Messaging.Audiences.Internal.Chung.EscalationPump>();
            builder.Services.AddHostedService<AppDelicare.Messaging.Audiences.Internal.Chung.WatchPump>();
            builder.Services.AddHostedService<TelegramPollingService>();
            builder.Services.AddHostedService<TelegramStartupService>();
            configure?.Invoke(builder);

            var app = builder.Build();
            app.MapPost("/tg/{botCode}", TelegramWebhookEndpoint.HandleAsync);
            app.MapGet("/health", (IOptionsMonitor<MessagingHostOptions> host, IOptionsMonitor<TelegramOptions> tg) => Results.Ok(new
            {
                status = "Healthy",
                mode = host.CurrentValue.Mode.ToString(),
                bots = tg.CurrentValue.Bots.Keys.Where(k => tg.CurrentValue.Find(k) != null).Select(k => k.ToUpperInvariant()),
                clientKeyConfigured = !string.IsNullOrWhiteSpace(host.CurrentValue.ClientKey),
            }));
            return app;
        }

        private static async Task<int> HealthCheckAsync()
        {
            string url = (Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS") is { Length: > 0 } port ? $"http://localhost:{port.Split(';')[0]}" : "http://localhost:8080") + "/health";
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                return (await http.GetAsync(url)).IsSuccessStatusCode ? 0 : 1;
            }
            catch (Exception)
            {
                return 1;
            }
        }
    }
}
