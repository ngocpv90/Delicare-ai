using AppDelicare.Data.Share;
using Serilog;

namespace AppDelicare.WebApi.Api
{
    /// <summary>
    /// T03: chỉ cho path <c>/api/v1</c>. Đổi exception → envelope lỗi + mã HTTP (Phụ lục A);
    /// response lỗi không có body (401 từ JWT, 403, 404 route, 405) cũng được bọc envelope.
    /// Route cũ đi qua nguyên vẹn (global handler ProblemDetails xử lý lỗi lọt ra).
    /// </summary>
    public sealed class ExceptionToErrorMiddleware(RequestDelegate next)
    {
        public static readonly PathString V1Prefix = new("/api/v1");

        private readonly RequestDelegate _next = next;

        public async Task InvokeAsync(HttpContext context)
        {
            if (!context.Request.Path.StartsWithSegments(V1Prefix))
            {
                await _next(context);
                return;
            }

            try
            {
                await _next(context);
            }
            catch (ApiException ex)
            {
                await WriteAsync(context, ex.StatusCode, ex.Message, ex.Errors);
                return;
            }
            catch (BusinessRuleException ex)
            {
                await WriteAsync(context, ex.StatusCode, ex.Message, [new ApiError { Code = ex.Code, Field = ex.Field, Message = ex.Message }]);
                return;
            }
            catch (AppendOnlyViolationException ex)
            {
                await WriteAsync(context, StatusCodes.Status422UnprocessableEntity, ex.Message, [new ApiError { Code = ErrorCodes.BusinessRule, Message = ex.Message }]);
                return;
            }
            catch (UnauthorizedActorException)
            {
                await WriteAsync(context, StatusCodes.Status401Unauthorized, EnvelopeResultFilter.DefaultMessage(401),
                    [new ApiError { Code = ErrorCodes.Unauthorized, Message = "Không xác định được người thao tác." }]);
                return;
            }
            catch (Exception ex) when (!context.Response.HasStarted && !context.RequestAborted.IsCancellationRequested)
            {
                Log.Error(ex, "Unhandled exception {Method} {Path} (CorrelationId {CorrelationId})",
                    context.Request.Method, context.Request.Path.Value, EnvelopeResultFilter.TraceIdOf(context));
                string message = EnvelopeResultFilter.DefaultMessage(500);
                await WriteAsync(context, StatusCodes.Status500InternalServerError, message,
                    [new ApiError { Code = ErrorCodes.InternalError, Message = message }]);
                return;
            }

            // Lỗi không có body (JWT challenge 401, Forbid 403, route không tồn tại 404, 405).
            int status = context.Response.StatusCode;
            if (status >= 400 && !context.Response.HasStarted && context.Response.ContentLength is null or 0
                && string.IsNullOrEmpty(context.Response.ContentType))
            {
                string message = EnvelopeResultFilter.DefaultMessage(status);
                await WriteAsync(context, status, message,
                    [new ApiError { Code = EnvelopeResultFilter.ErrorCodeFor(status), Message = message }]);
            }
        }

        private static async Task WriteAsync(HttpContext context, int status, string message, IReadOnlyList<ApiError> errors)
        {
            if (context.Response.HasStarted)
            {
                return;
            }
            context.Response.Clear();
            context.Response.StatusCode = status;
            var envelope = new ApiEnvelope<object?>
            {
                Success = false,
                Message = message,
                Errors = errors,
                TraceId = EnvelopeResultFilter.TraceIdOf(context),
            };
            await context.Response.WriteAsJsonAsync(envelope, ApiJson.Options);
        }
    }
}
