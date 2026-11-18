using System.Security.Cryptography;
using System.Text;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Helper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Serilog;

namespace AppDelicare.WebApi.Security
{
    /// <summary>
    /// BOT-1.1 (D-62): khách nội bộ (container <c>messaging</c>; sau này HERMES, AGENT_&lt;CODE&gt;) gọi API bằng header <c>X-Client-Key</c>, không JWT người dùng.
    /// Khóa hợp lệ khi SHA-256 (hex) của khóa = <c>Clients:&lt;code&gt;:KeyHash</c> (BÍ MẬT – env <c>Clients__MESSAGING__KeyHash</c>); <c>Clients:&lt;code&gt;:Enabled = false</c>
    /// hoặc chưa đặt hash = tắt (mọi request → 401). So sánh thời gian hằng, không ghi khóa ra log. Từ ngoài internet nginx còn chặn <c>/api/BotChannel/</c> (404).
    /// </summary>
    public sealed class ClientKeyAttribute(string clientCode) : TypeFilterAttribute(typeof(ClientKeyFilter))
    {
        public string ClientCode { get; } = clientCode;

    }

    public sealed class ClientKeyFilter(IConfiguration configuration) : IAsyncAuthorizationFilter
    {
        public const string HeaderName = "X-Client-Key";
        public const string ItemClientCode = "Delicare.ClientCode";

        public Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            string code = context.ActionDescriptor.EndpointMetadata.OfType<ClientKeyAttribute>().LastOrDefault()?.ClientCode ?? MessagingCode.ClientMessaging;
            if (!IsValid(configuration, code, context.HttpContext.Request.Headers[HeaderName].ToString()))
            {
                Log.Warning("[ClientKey] khóa client {Client} sai / thiếu từ {Ip} ({Path})", code, context.HttpContext.Connection.RemoteIpAddress,
                    context.HttpContext.Request.Path.Value);
                context.Result = new ObjectResult(new Result<bool>
                {
                    ResponseCode = StatusCodes.Status401Unauthorized,
                    ResponseMessage = ConstantHelper.Message.ClientKeyInvalid,
                    Data = false,
                }) { StatusCode = StatusCodes.Status401Unauthorized };
                return Task.CompletedTask;
            }
            context.HttpContext.Items[ItemClientCode] = code;
            return Task.CompletedTask;
        }

        public static bool IsValid(IConfiguration configuration, string clientCode, string? presented)
        {
            string section = $"Clients:{clientCode}";
            string? expectedHex = configuration[$"{section}:KeyHash"]?.Trim();
            if (string.IsNullOrEmpty(presented) || string.IsNullOrEmpty(expectedHex)
                || string.Equals(configuration[$"{section}:Enabled"], "false", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            byte[] expected;
            try
            {
                expected = Convert.FromHexString(expectedHex);
            }
            catch (FormatException)
            {
                Log.Error("[ClientKey] Clients:{Client}:KeyHash không phải chuỗi hex SHA-256", clientCode);
                return false;
            }
            byte[] actual = SHA256.HashData(Encoding.UTF8.GetBytes(presented));
            return expected.Length == actual.Length && CryptographicOperations.FixedTimeEquals(expected, actual);
        }

        /// <summary>Băm khóa để đặt vào Clients:&lt;code&gt;:KeyHash (dùng trong tài liệu / test).</summary>
        public static string HashKey(string key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
    }
}
