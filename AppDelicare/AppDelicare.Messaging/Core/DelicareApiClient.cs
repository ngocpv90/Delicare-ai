using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AppDelicare.Messaging.Core.Contracts;
using AppDelicare.Messaging.Options;
using Microsoft.Extensions.Options;

namespace AppDelicare.Messaging.Core
{
    /// <summary>Api không trả lời được (tắt / lỗi 5xx / mất mạng) – webhook trả 503 để Telegram gửi lại, pump chờ rồi thử lại.</summary>
    public sealed class ApiUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

    /// <summary>
    /// BOT-1.3 (D-62): gọi container api theo khung cũ D-30 – <c>api/&lt;Controller&gt;/&lt;Action&gt;</c>, đọc <c>Result&lt;T&gt;</c> / <c>Results&lt;T&gt;</c>.
    /// API nội bộ <c>BotChannel/*</c>: JSON + header <c>X-Client-Key</c>. API nghiệp vụ: form / query + JWT của chính user (UserTokenCache).
    /// </summary>
    public interface IDelicareApiClient
    {
        Task<ApiResult<T>> PostInternalAsync<T>(string action, object body, CancellationToken ct = default);
        Task<ApiResult<T>> GetAsUserAsync<T>(string path, string bearerToken, CancellationToken ct = default);
        Task<ApiResult<T>> PostFormAsUserAsync<T>(string path, IEnumerable<KeyValuePair<string, string>> form, string bearerToken, CancellationToken ct = default);
    }

    public sealed class DelicareApiClient(HttpClient http, IOptionsMonitor<MessagingHostOptions> options) : IDelicareApiClient
    {
        public const string ClientKeyHeader = "X-Client-Key";
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        public async Task<ApiResult<T>> PostInternalAsync<T>(string action, object body, CancellationToken ct = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, Url("api/BotChannel/" + action)) { Content = JsonContent.Create(body, options: Json) };
            request.Headers.Add(ClientKeyHeader, options.CurrentValue.ClientKey ?? string.Empty);
            return await SendAsync<T>(request, ct);
        }

        public async Task<ApiResult<T>> GetAsUserAsync<T>(string path, string bearerToken, CancellationToken ct = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, Url(path));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            return await SendAsync<T>(request, ct);
        }

        public async Task<ApiResult<T>> PostFormAsUserAsync<T>(string path, IEnumerable<KeyValuePair<string, string>> form, string bearerToken, CancellationToken ct = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, Url(path)) { Content = new FormUrlEncodedContent(form) };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            return await SendAsync<T>(request, ct);
        }

        private string Url(string path) => options.CurrentValue.ApiBaseUrl.TrimEnd('/') + "/" + path.TrimStart('/');

        private async Task<ApiResult<T>> SendAsync<T>(HttpRequestMessage request, CancellationToken ct)
        {
            HttpResponseMessage response;
            try
            {
                response = await http.SendAsync(request, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                throw new ApiUnavailableException("Không gọi được api: " + ex.GetType().Name, ex);
            }
            using (response)
            {
                if ((int)response.StatusCode >= 500)
                {
                    throw new ApiUnavailableException($"api trả HTTP {(int)response.StatusCode}");
                }
                if (!response.IsSuccessStatusCode)
                {
                    // 401 (khóa client / token sai) / 403 (thiếu quyền) / 404 – lỗi rõ ràng, không thử lại.
                    return new ApiResult<T> { ResponseCode = (int)response.StatusCode, ResponseMessage = $"HTTP {(int)response.StatusCode}" };
                }
                return await response.Content.ReadFromJsonAsync<ApiResult<T>>(Json, ct) ?? new ApiResult<T> { ResponseCode = 500, ResponseMessage = "Phản hồi rỗng" };
            }
        }
    }
}
