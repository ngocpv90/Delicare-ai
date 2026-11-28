using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AppDelicare.Messaging.Core;
using AppDelicare.Messaging.Presentation;
using AppDelicare.Messaging.Core.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AppDelicare.Messaging.Channels.Zalo
{
    /// <summary>
    /// AI-19 (D-133 đợt 4, D-06): Zalo OA – bot KHÁCH HÀNG. Mục cấu hình <c>Zalo</c> của messaging. Bí mật chỉ ở biến môi trường (<c>Zalo__OaSecretKey</c>).
    /// Mặc định tắt (<see cref="Enabled"/> = false) – bật khi đã có OA thật (UAT AI-19).
    /// </summary>
    public sealed class ZaloOptions
    {
        public const string Section = "Zalo";
        public bool Enabled { get; set; }
        public string BotCode { get; set; } = "ZALO_KH";
        public string? AppId { get; set; }
        /// <summary>OA secret key – kiểm chữ ký webhook (biến môi trường Zalo__OaSecretKey).</summary>
        public string? OaSecretKey { get; set; }
        public string SendUrl { get; set; } = "https://openapi.zalo.me/v3.0/oa/message/cs";
        public int PollSeconds { get; set; } = 10;
        /// <summary>Bỏ sự kiện có timestamp lệch quá số phút này (chống phát lại).</summary>
        public int MaxSkewMinutes { get; set; } = 30;
    }

    /// <summary>
    /// Chữ ký webhook Zalo OA: header <c>X-ZEvent-Signature: mac=&lt;hex&gt;</c>, mac = SHA-256(app_id + nội dung body + timestamp + OA secret key).
    /// So sánh hằng thời gian; thiếu khóa / sai → từ chối.
    /// </summary>
    public static class ZaloSignature
    {
        public static string Mac(string appId, string body, string timestamp, string secret)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(appId + body + timestamp + secret))).ToLowerInvariant();

        public static bool Verify(string? header, string? appId, string body, string? timestamp, string? secret)
        {
            if (string.IsNullOrEmpty(header) || string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(timestamp))
            {
                return false;
            }
            string given = header.Trim();
            if (given.StartsWith("mac=", StringComparison.OrdinalIgnoreCase))
            {
                given = given[4..];
            }
            byte[] a = Encoding.ASCII.GetBytes(Mac(appId, body, timestamp, secret)), b = Encoding.ASCII.GetBytes(given.ToLowerInvariant());
            return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
        }
    }

    /// <summary>Sự kiện Zalo đã đọc: chỉ tin nhắn chữ (user_send_text) và quan tâm OA (follow – gửi hướng dẫn); còn lại bỏ qua.</summary>
    public sealed record ZaloEvent(string EventName, string UserId, string? Text, string? MessageId, string Timestamp, string? AppId);

    public static class ZaloEventParser
    {
        public static ZaloEvent? Parse(string body)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                var r = doc.RootElement;
                string ev = r.TryGetProperty("event_name", out var e) ? e.GetString() ?? string.Empty : string.Empty;
                string ts = r.TryGetProperty("timestamp", out var t) ? t.ValueKind == JsonValueKind.Number ? t.GetRawText() : t.GetString() ?? string.Empty : string.Empty;
                string? app = r.TryGetProperty("app_id", out var a) ? a.ValueKind == JsonValueKind.String ? a.GetString() : a.GetRawText() : null;
                string? user = ev == "follow" && r.TryGetProperty("follower", out var f) ? f.TryGetProperty("id", out var fid) ? fid.GetString() : null
                    : r.TryGetProperty("sender", out var s) && s.TryGetProperty("id", out var sid) ? sid.GetString() : null;
                if (string.IsNullOrEmpty(user) || ev is not ("user_send_text" or "follow"))
                {
                    return null;
                }
                string? text = null, msgId = null;
                if (r.TryGetProperty("message", out var m))
                {
                    text = m.TryGetProperty("text", out var tx) ? tx.GetString() : null;
                    msgId = m.TryGetProperty("msg_id", out var mid) ? mid.GetString() : null;
                }
                return new ZaloEvent(ev, user!, ev == "follow" ? string.Empty : text, msgId ?? $"{ev}:{user}:{ts}", ts, app);
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }

    public sealed record ZaloSendResult(bool Ok, string? MessageId, string? Error, bool Blocked);

    public interface IZaloClient
    {
        Task<ZaloSendResult> SendTextAsync(string accessToken, string userId, string text, CancellationToken ct = default);
    }

    /// <summary>Gửi tin tư vấn (CS) – trả lời trong khung 7 ngày kể từ tin gần nhất của khách (chính sách Zalo OA).</summary>
    public sealed class ZaloClient(HttpClient http, IOptionsMonitor<ZaloOptions> options) : IZaloClient
    {
        public async Task<ZaloSendResult> SendTextAsync(string accessToken, string userId, string text, CancellationToken ct = default)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, options.CurrentValue.SendUrl)
            {
                Content = JsonContent.Create(new { recipient = new { user_id = userId }, message = new { text } }),
            };
            req.Headers.Add("access_token", accessToken);
            try
            {
                using var res = await http.SendAsync(req, ct);
                using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
                int code = doc.RootElement.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.Number ? e.GetInt32() : (int)res.StatusCode == 200 ? 0 : -1;
                if (code == 0)
                {
                    string? id = doc.RootElement.TryGetProperty("data", out var d) && d.TryGetProperty("message_id", out var mid) ? mid.GetString() : null;
                    return new ZaloSendResult(true, id, null, false);
                }
                string msg = doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() ?? string.Empty : string.Empty;
                // -213: người dùng chưa quan tâm / đã bỏ quan tâm OA; -230: quá khung tương tác – không thử lại vô hạn.
                return new ZaloSendResult(false, null, $"Zalo {code}: {msg}", code is -213 or -230);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                return new ZaloSendResult(false, null, ex.Message, false);
            }
        }
    }

    /// <summary>Gọi API nội bộ CustomerChannel/* (khóa client như BotChannel).</summary>
    public interface ICustomerChannelApi
    {
        Task<ApiResult<CustomerReplyDto>> HandleAsync(object request, CancellationToken ct = default);
        Task<string?> AccessTokenAsync(CancellationToken ct = default);
    }

    public sealed class CustomerReplyDto
    {
        public List<string> Replies { get; set; } = [];
        public bool Duplicate { get; set; }
    }

    public sealed class TokenDto
    {
        public string AccessToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }

    public sealed class CustomerChannelApi(IDelicareApiClient api, TimeProvider time) : ICustomerChannelApi
    {
        private string? _token;
        private DateTime _until;

        public Task<ApiResult<CustomerReplyDto>> HandleAsync(object request, CancellationToken ct = default) => api.PostInternalToAsync<CustomerReplyDto>("api/CustomerChannel/Handle", request, ct);

        public async Task<string?> AccessTokenAsync(CancellationToken ct = default)
        {
            DateTime now = time.GetUtcNow().UtcDateTime;
            if (_token != null && _until > now.AddMinutes(2))
            {
                return _token;
            }
            var r = await api.PostInternalToAsync<TokenDto>("api/CustomerChannel/GetAccessToken", new { }, ct);
            if (!r.IsOk || r.Data == null)
            {
                return null;
            }
            (_token, _until) = (r.Data.AccessToken, r.Data.ExpiresAt);
            return _token;
        }
    }

    /// <summary>
    /// Webhook <c>POST /zalo/{botCode}</c>: kiểm chữ ký → trả 200 ngay (Zalo cần phản hồi nhanh) → xử lý nền: API CustomerChannel/Handle (chống trùng theo msg_id) → gửi các câu trả lời.
    /// Bot tắt / khác mã → 404; sai chữ ký → 401.
    /// </summary>
    public static class ZaloWebhookEndpoint
    {
        public static async Task<IResult> HandleAsync(string botCode, HttpContext http, IOptionsMonitor<ZaloOptions> options, IServiceScopeFactory scopes, ILoggerFactory logs, TimeProvider time)
        {
            var o = options.CurrentValue;
            if (!o.Enabled || !string.Equals(botCode, o.BotCode, StringComparison.OrdinalIgnoreCase))
            {
                return Results.NotFound();
            }
            using var reader = new StreamReader(http.Request.Body, Encoding.UTF8);
            string body = await reader.ReadToEndAsync();
            var ev = ZaloEventParser.Parse(body);
            string? ts = ev?.Timestamp;
            if (ts == null)
            {
                using var doc0 = JsonDocument.Parse(body.Length == 0 ? "{}" : body);
                ts = doc0.RootElement.TryGetProperty("timestamp", out var t) ? t.ValueKind == JsonValueKind.Number ? t.GetRawText() : t.GetString() : null;
            }
            if (!ZaloSignature.Verify(http.Request.Headers["X-ZEvent-Signature"].ToString(), o.AppId, body, ts, o.OaSecretKey))
            {
                return Results.Unauthorized();
            }
            if (ev == null)
            {
                return Results.Ok();
            }
            if (long.TryParse(ev.Timestamp, out long ms) && Math.Abs((time.GetUtcNow() - DateTimeOffset.FromUnixTimeMilliseconds(ms)).TotalMinutes) > Math.Max(1, o.MaxSkewMinutes))
            {
                return Results.Ok(); // sự kiện quá cũ – bỏ (chống phát lại)
            }
            var log = logs.CreateLogger("ZaloWebhook");
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    var api = scope.ServiceProvider.GetRequiredService<ICustomerChannelApi>();
                    var client = scope.ServiceProvider.GetRequiredService<IZaloClient>();
                    var r = await api.HandleAsync(new { botCode = o.BotCode, externalUserId = ev.UserId, eventId = ev.MessageId, text = ev.Text });
                    if (!r.IsOk || r.Data == null || r.Data.Duplicate || r.Data.Replies.Count == 0)
                    {
                        return;
                    }
                    string? token = await api.AccessTokenAsync();
                    if (token == null)
                    {
                        log.LogWarning("Zalo: chưa có token OA – không gửi được trả lời");
                        return;
                    }
                    foreach (string reply in r.Data.Replies)
                    {
                        var s = await client.SendTextAsync(token, ev.UserId, reply);
                        if (!s.Ok)
                        {
                            log.LogWarning("Zalo: gửi trả lời lỗi {Error}", s.Error);
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.LogError(ex, "Zalo: xử lý sự kiện lỗi");
                }
            });
            return Results.Ok();
        }
    }

    /// <summary>Gửi tin chờ của kênh ZALO (kết quả xác nhận yêu cầu của khách – CUSTOMER_NOTICE, hoặc mẫu khác chuyển sang chữ thường).</summary>
    public sealed class ZaloOutboundPump(IBotChannelApi bot, ICustomerChannelApi api, IZaloClient client, IOptionsMonitor<ZaloOptions> options, ILogger<ZaloOutboundPump> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (options.CurrentValue.Enabled)
                    {
                        await RunOnceAsync(stoppingToken);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "ZaloOutboundPump lỗi");
                }
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(3, options.CurrentValue.PollSeconds)), stoppingToken);
            }
        }

        public static string TextOf(string? payloadJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(payloadJson ?? "{}");
                if (doc.RootElement.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Object && d.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String)
                {
                    return t.GetString() ?? string.Empty;
                }
            }
            catch (JsonException)
            {
                // mẫu thường bên dưới
            }
            var n = MessageTemplates.Render(payloadJson);
            return string.Join("\n", new[] { n.Title }.Concat(n.Lines).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => System.Text.RegularExpressions.Regex.Replace(x!, "<[^>]+>", string.Empty)));
        }

        public async Task<int> RunOnceAsync(CancellationToken ct)
        {
            var items = await bot.ClaimOutboundAsync(options.CurrentValue.BotCode, 20, ct);
            if (items.Count == 0)
            {
                return 0;
            }
            string? token = await api.AccessTokenAsync(ct);
            foreach (var item in items)
            {
                string? to = item.RecipientExternalId ?? item.ExternalConversationId;
                if (token == null || string.IsNullOrWhiteSpace(to))
                {
                    await bot.MarkOutboundAsync(new BotMarkOutboundRequest { Id = item.Id, Status = "Failed", Error = token == null ? "Chưa có token Zalo OA" : "Thiếu người nhận" }, ct);
                    continue;
                }
                var r = await client.SendTextAsync(token, to, TextOf(item.PayloadJson), ct);
                await bot.MarkOutboundAsync(new BotMarkOutboundRequest { Id = item.Id, Status = r.Ok ? "Sent" : r.Blocked ? "Blocked" : "Failed", ProviderMessageId = r.MessageId, Error = r.Error }, ct);
            }
            return items.Count;
        }
    }
}
