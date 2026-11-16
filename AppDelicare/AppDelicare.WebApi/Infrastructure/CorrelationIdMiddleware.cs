using System.Text.RegularExpressions;
using AppDelicare.Data.Share;
using Serilog.Context;

namespace AppDelicare.WebApi.Infrastructure
{
    /// <summary>
    /// T02: gắn CorrelationId cho mỗi request. Lấy từ header <c>X-Correlation-Id</c> nếu hợp lệ, không thì tự sinh.
    /// Trả lại trong response header và đưa vào Serilog LogContext (thuộc tính <c>CorrelationId</c>).
    /// </summary>
    public sealed partial class CorrelationIdMiddleware(RequestDelegate next)
    {
        public const string HeaderName = "X-Correlation-Id";
        public const string LogProperty = "CorrelationId";

        private readonly RequestDelegate _next = next;

        public async Task InvokeAsync(HttpContext context)
        {
            string correlationId = context.Request.Headers.TryGetValue(HeaderName, out var values) && IsValid(values.ToString())
                ? values.ToString()
                : Guid.NewGuid().ToString("N");

            context.Items[CurrentActor.CorrelationIdItemKey] = correlationId;
            context.TraceIdentifier = correlationId;
            context.Response.OnStarting(() =>
            {
                context.Response.Headers[HeaderName] = correlationId;
                return Task.CompletedTask;
            });

            using (LogContext.PushProperty(LogProperty, correlationId))
            {
                await _next(context);
            }
        }

        /// <summary>Chỉ nhận chuỗi ngắn, an toàn (chống chèn ký tự lạ vào log / header).</summary>
        public static bool IsValid(string? value) => !string.IsNullOrEmpty(value) && SafeId().IsMatch(value);

        [GeneratedRegex("^[A-Za-z0-9._-]{1,64}$")]
        private static partial Regex SafeId();
    }
}
