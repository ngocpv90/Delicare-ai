using System.Security.Cryptography;
using System.Text;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Option;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Serilog;

namespace AppDelicare.WebApi.Security
{
    /// <summary>
    /// BE3.2 (PROPOSED D-57 f): xác thực cảm biến IoT bằng header <c>X-Api-Key</c> (không dùng JWT người dùng). Khóa hợp lệ = <c>Iot:ApiKeys</c>
    /// (bí mật – biến môi trường / user-secrets). So sánh băm SHA-256 thời gian hằng; danh sách trống = tắt API. Giới hạn <c>Iot:RateLimitPerMinute</c>
    /// request / phút / khóa (quá → 429). Không ghi khóa ra log (chỉ 8 ký tự đầu của băm).
    /// </summary>
    public sealed class IotApiKeyAttribute() : TypeFilterAttribute(typeof(IotApiKeyFilter));

    public sealed class IotApiKeyFilter(IOptionsMonitor<IotOptions> options, IMemoryCache cache) : IAsyncAuthorizationFilter
    {
        public const string HeaderName = "X-Api-Key";

        public Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var iot = options.CurrentValue;
            string presented = context.HttpContext.Request.Headers[HeaderName].ToString();
            byte[] presentedHash = SHA256.HashData(Encoding.UTF8.GetBytes(presented));
            bool valid = presented.Length > 0 && iot.ApiKeys
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Select(k => SHA256.HashData(Encoding.UTF8.GetBytes(k.Trim())))
                .Aggregate(false, (found, keyHash) => CryptographicOperations.FixedTimeEquals(keyHash, presentedHash) | found);
            string keyId = Convert.ToHexString(presentedHash)[..8];
            if (!valid)
            {
                Log.Warning("[IoT] khóa API sai / thiếu từ {Ip} (băm {KeyId})", context.HttpContext.Connection.RemoteIpAddress, presented.Length > 0 ? keyId : "-");
                context.Result = Fail(StatusCodes.Status401Unauthorized, ConstantHelper.Message.IotApiKeyInvalid);
                return Task.CompletedTask;
            }
            // Đếm theo khóa + phút hiện tại (cửa sổ cố định 1 phút).
            string bucket = $"iot-rate:{keyId}:{DateTime.UtcNow:yyyyMMddHHmm}";
            int count = cache.GetOrCreate(bucket, e => { e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2); return new Counter(); })!.Increment();
            if (count > Math.Max(1, iot.RateLimitPerMinute))
            {
                Log.Warning("[IoT] khóa {KeyId} gửi quá {Limit} lần / phút", keyId, iot.RateLimitPerMinute);
                context.Result = Fail(StatusCodes.Status429TooManyRequests, ConstantHelper.Message.IotRateLimited);
            }
            return Task.CompletedTask;
        }

        private static ObjectResult Fail(int status, string message)
            => new(new Result<bool> { ResponseCode = status, ResponseMessage = message, Data = false }) { StatusCode = status };

        private sealed class Counter
        {
            private int _value;
            public int Increment() => Interlocked.Increment(ref _value);
        }
    }
}
