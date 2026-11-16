using AppDelicare.Data.Share;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace AppDelicare.WebApi.Infrastructure
{
    /// <summary>
    /// T03: lỗi chưa bắt → log đầy đủ (kèm CorrelationId) + trả ProblemDetails, KHÔNG lộ stack trace / message nội bộ.
    /// Route cũ vẫn tự bắt lỗi trong controller như trước; handler này chỉ nhận lỗi lọt ra ngoài.
    /// </summary>
    public static class ProblemDetailsExceptionHandler
    {
        public static async Task HandleAsync(HttpContext context)
        {
            Exception? exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
            string correlationId = context.Items[CurrentActor.CorrelationIdItemKey] as string ?? context.TraceIdentifier;

            (int status, string title) = exception switch
            {
                UnauthorizedActorException => (StatusCodes.Status401Unauthorized, "Không xác định được người thao tác."),
                OperationCanceledException when context.RequestAborted.IsCancellationRequested => (499, "Client đã hủy yêu cầu."),
                _ => (StatusCodes.Status500InternalServerError, "Lỗi hệ thống. Vui lòng thử lại hoặc báo quản trị kèm mã tra cứu."),
            };

            if (status >= 500)
            {
                Log.Error(exception, "Unhandled exception {Method} {Path} (CorrelationId {CorrelationId})",
                    context.Request.Method, context.Request.Path.Value, correlationId);
            }
            else
            {
                Log.Warning("{Title} {Method} {Path} (CorrelationId {CorrelationId})",
                    title, context.Request.Method, context.Request.Path.Value, correlationId);
            }

            var problem = new ProblemDetails
            {
                Status = status,
                Title = title,
                Instance = context.Request.Path,
            };
            problem.Extensions["traceId"] = correlationId;

            context.Response.StatusCode = status;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json");
        }
    }
}
