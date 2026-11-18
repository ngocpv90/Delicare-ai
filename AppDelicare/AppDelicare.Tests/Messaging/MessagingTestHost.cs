using System.Collections.Concurrent;
using System.Text.Json;
using AppDelicare.Messaging;
using AppDelicare.Messaging.Channels;
using AppDelicare.Messaging.Channels.Telegram;
using AppDelicare.Messaging.Channels.Telegram.Models;
using AppDelicare.Messaging.Core;
using AppDelicare.Messaging.Core.Contracts;
using AppDelicare.Messaging.Options;
using AppDelicare.Messaging.Processing;
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
    /// <summary>api nghiệp vụ giả (JWT user): GET / POST form theo đường dẫn → trả lời cấu hình được; ghi lại mọi lệnh gọi.</summary>
    public sealed class FakeWebApi : IDelicareApiClient
    {
        public ConcurrentQueue<(string Method, string Path, Dictionary<string, string> Form, string Token)> Calls { get; } = new();
        public Dictionary<string, Func<string, Dictionary<string, string>, (int Code, string? Message, string? DataJson, string? ListJson)>> Routes { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<ApiResult<T>> PostInternalAsync<T>(string action, object body, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<ApiResult<T>> GetAsUserAsync<T>(string path, string bearerToken, CancellationToken ct = default) => Answer<T>("GET", path, [], bearerToken);

        public Task<ApiResult<T>> PostFormAsUserAsync<T>(string path, IEnumerable<KeyValuePair<string, string>> form, string bearerToken, CancellationToken ct = default)
            => Answer<T>("POST", path, form.ToDictionary(k => k.Key, k => k.Value), bearerToken);

        private Task<ApiResult<T>> Answer<T>(string method, string path, Dictionary<string, string> form, string token)
        {
            Calls.Enqueue((method, path, form, token));
            string route = path.Split('?')[0];
            if (!Routes.TryGetValue(route, out var f))
            {
                return Task.FromResult(new ApiResult<T> { ResponseCode = 404, ResponseMessage = "HTTP 404 (giả)" });
            }
            var (code, message, data, list) = f(path, form);
            var o = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            return Task.FromResult(new ApiResult<T>
            {
                ResponseCode = code, ResponseMessage = message,
                Data = data == null ? default : JsonSerializer.Deserialize<T>(data, o),
                ListData = list == null ? null : JsonSerializer.Deserialize<List<T>>(list, o),
            });
        }
    }

    /// <summary>Telegram giả cho CommandScopeSync (chỉ setMyCommands; còn lại không dùng vì kênh gửi là FakeSender).</summary>
    public sealed class FakeTelegramClient : ITelegramClient
    {
        public ConcurrentQueue<(string Bot, List<TgBotCommand> Commands, string? Scope)> CommandCalls { get; } = new();

        public Task SetMyCommandsAsync(string botCode, IEnumerable<TgBotCommand> commands, object? scope = null, CancellationToken ct = default)
        {
            CommandCalls.Enqueue((botCode, commands.ToList(), scope == null ? null : JsonSerializer.Serialize(scope)));
            return Task.CompletedTask;
        }

        public Task<TgMessage> SendMessageAsync(string botCode, string chatId, string text, object? replyMarkup = null, bool disableNotification = false, long? replyTo = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<TgMessage> EditMessageTextAsync(string botCode, string chatId, long messageId, string text, object? replyMarkup = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task EditMessageReplyMarkupAsync(string botCode, string chatId, long messageId, object? replyMarkup, CancellationToken ct = default) => throw new NotSupportedException();
        public Task PinChatMessageAsync(string botCode, string chatId, long messageId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AnswerCallbackQueryAsync(string botCode, string callbackId, string? text = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SendChatActionAsync(string botCode, string chatId, string action = "typing", CancellationToken ct = default) => throw new NotSupportedException();
        public Task SetWebhookAsync(string botCode, string url, string secret, IEnumerable<string> allowedUpdates, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteWebhookAsync(string botCode, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<JsonElement>> GetUpdatesAsync(string botCode, long offset, int timeoutSeconds, IEnumerable<string> allowedUpdates, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<TgFile> GetFileAsync(string botCode, string fileId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    /// <summary>Container messaging thật (DI của MessagingProgram) với api / kênh / Telegram / web giả – không chạy dịch vụ nền.</summary>
    public sealed class MessagingTestHost : IAsyncDisposable
    {
        public FakeBotChannelApi Api { get; } = new();
        public FakeSender Sender { get; } = new();
        public FakeWebApi Web { get; } = new();
        public FakeTelegramClient Telegram { get; } = new();
        public WebApplication App { get; }
        public UpdateDispatcher Dispatcher => App.Services.GetRequiredService<UpdateDispatcher>();
        public InboundPump Inbound { get; }

        public MessagingTestHost(Dictionary<string, string?>? settings = null)
        {
            App = MessagingProgram.Build([], b =>
            {
                b.WebHost.UseTestServer();
                var cfg = new Dictionary<string, string?>
                {
                    ["Telegram:Bots:VH:Token"] = "t-vh",
                    ["Telegram:Bots:DH:Token"] = "t-dh",
                    ["Telegram:Bots:DH:Username"] = "delicare_dh_bot",
                    ["Messaging:IdentityCacheSeconds"] = "0",
                    ["Sending:UndoSeconds"] = "1",
                };
                foreach (var kv in settings ?? [])
                {
                    cfg[kv.Key] = kv.Value;
                }
                b.Configuration.AddInMemoryCollection(cfg);
                b.Services.RemoveAll<IHostedService>();
                b.Services.RemoveAll<IBotChannelApi>();
                b.Services.AddSingleton<IBotChannelApi>(Api);
                b.Services.RemoveAll<IChannelSender>();
                b.Services.AddSingleton<IChannelSender>(Sender);
                b.Services.RemoveAll<IDelicareApiClient>();
                b.Services.AddSingleton<IDelicareApiClient>(Web);
                b.Services.RemoveAll<ITelegramClient>();
                b.Services.AddSingleton<ITelegramClient>(Telegram);
            });
            Inbound = new InboundPump(Api, Dispatcher, new InboundSignal(), App.Services.GetRequiredService<IOptionsMonitor<MessagingHostOptions>>(), NullLogger<InboundPump>.Instance);
        }

        public BotInboundItemModel Item(string json, string bot = "VH") => new() { Id = Guid.NewGuid(), BotCode = bot, PayloadJson = json };

        public Task<DispatchResult> SendAsync(string json, string bot = "VH") => Dispatcher.HandleAsync(Item(json, bot), default);

        public async ValueTask DisposeAsync() => await App.DisposeAsync();
    }
}
