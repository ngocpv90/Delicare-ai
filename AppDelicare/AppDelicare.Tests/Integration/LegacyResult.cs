using System.Globalization;
using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace AppDelicare.Tests.Integration
{
    /// <summary>T45R: đọc kết quả khung cũ <c>Result&lt;T&gt;</c> { responseCode, responseMessage, data } / <c>Results&lt;T&gt;</c> { listData, totalRecords }.</summary>
    public sealed record LegacyResult(int Code, string? Message, JsonElement Data, JsonElement ListData)
    {
        public static async Task<LegacyResult> ReadAsync(HttpResponseMessage response)
        {
            string raw = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.OK, raw);
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            return new LegacyResult(
                root.GetProperty("responseCode").GetInt32(),
                root.TryGetProperty("responseMessage", out var m) ? m.GetString() : null,
                root.TryGetProperty("data", out var d) ? d.Clone() : default,
                root.TryGetProperty("listData", out var l) ? l.Clone() : default);
        }

        /// <summary>ResponseCode = 200; trả Data.</summary>
        public JsonElement Ok()
        {
            Code.Should().Be(200, Message);
            return Data;
        }

        /// <summary>ResponseCode = 400 (lỗi nghiệp vụ); thông báo chứa <paramref name="contains"/> nếu có.</summary>
        public void Fail(string? contains = null)
        {
            Code.Should().Be(400, "kỳ vọng lỗi nghiệp vụ");
            if (contains != null)
            {
                Message.Should().Contain(contains);
            }
        }
    }

    public static class LegacyHttp
    {
        public static FormUrlEncodedContent Form(params (string Key, object? Value)[] fields)
            => new(fields.Where(f => f.Value != null).Select(f => new KeyValuePair<string, string>(f.Key,
                f.Value switch
                {
                    DateTime d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    decimal m => m.ToString(CultureInfo.InvariantCulture),
                    bool b => b ? "true" : "false",
                    _ => Convert.ToString(f.Value, CultureInfo.InvariantCulture)!,
                })));

        public static async Task<LegacyResult> PostFormAsync(this HttpClient client, string url, params (string Key, object? Value)[] fields)
            => await LegacyResult.ReadAsync(await client.PostAsync(url, Form(fields)));

        public static async Task<LegacyResult> PutFormAsync(this HttpClient client, string url, params (string Key, object? Value)[] fields)
            => await LegacyResult.ReadAsync(await client.PutAsync(url, Form(fields)));

        public static async Task<LegacyResult> GetResultAsync(this HttpClient client, string url)
            => await LegacyResult.ReadAsync(await client.GetAsync(url));

        public static async Task<LegacyResult> DeleteResultAsync(this HttpClient client, string url)
            => await LegacyResult.ReadAsync(await client.DeleteAsync(url));
    }
}
