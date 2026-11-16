using System.Text.Json;
using AppDelicare.Data.Share;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Options;

namespace AppDelicare.WebApi.Api
{
    /// <summary>
    /// Bọc kết quả của controller v1 (<see cref="ApiControllerBase"/>) vào <see cref="ApiEnvelope{T}"/>.
    /// Không đụng route cũ. <c>NoContent()</c> (204) giữ nguyên, không có body.
    /// </summary>
    public sealed class EnvelopeResultFilter : IAsyncAlwaysRunResultFilter
    {
        public Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
        {
            if (!IsV1(context.ActionDescriptor))
            {
                return next();
            }

            switch (context.Result)
            {
                case ObjectResult obj when obj.Value is not null && IsEnvelope(obj.Value.GetType()):
                    break;
                case ObjectResult obj:
                    int status = obj.StatusCode ?? StatusCodes.Status200OK;
                    obj.Value = Wrap(context.HttpContext, status, obj.Value);
                    obj.DeclaredType = obj.Value.GetType();
                    obj.StatusCode = status;
                    break;
                case StatusCodeResult sc when sc.StatusCode != StatusCodes.Status204NoContent:
                    context.Result = new ObjectResult(Wrap(context.HttpContext, sc.StatusCode, null)) { StatusCode = sc.StatusCode };
                    break;
            }
            return next();
        }

        public static bool IsV1(Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor descriptor)
            => descriptor is ControllerActionDescriptor cad && typeof(ApiControllerBase).IsAssignableFrom(cad.ControllerTypeInfo);

        private static bool IsEnvelope(Type t) => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ApiEnvelope<>);

        private static object Wrap(HttpContext http, int status, object? value)
        {
            string traceId = TraceIdOf(http);
            bool success = status is >= 200 and < 300;
            if (success)
            {
                return new ApiEnvelope<object?> { Success = true, Data = value, TraceId = traceId };
            }
            // Lỗi do action trả thẳng (vd. NotFound()) – đổi sang envelope lỗi.
            string code = ErrorCodeFor(status);
            string message = value as string ?? DefaultMessage(status);
            return new ApiEnvelope<object?>
            {
                Success = false,
                Message = message,
                Errors = [new ApiError { Code = code, Message = message }],
                TraceId = traceId,
            };
        }

        internal static string TraceIdOf(HttpContext http)
            => http.Items[CurrentActor.CorrelationIdItemKey] as string ?? http.TraceIdentifier;

        internal static string ErrorCodeFor(int status) => status switch
        {
            400 => ErrorCodes.ValidationFailed,
            401 => ErrorCodes.Unauthorized,
            403 => ErrorCodes.PermissionDenied,
            404 => ErrorCodes.NotFound,
            405 => ErrorCodes.MethodNotAllowed,
            409 => ErrorCodes.Duplicate,
            422 => ErrorCodes.BusinessRule,
            _ => ErrorCodes.InternalError,
        };

        internal static string DefaultMessage(int status) => status switch
        {
            400 => "Dữ liệu không hợp lệ.",
            401 => "Chưa đăng nhập hoặc phiên đăng nhập đã hết hạn.",
            403 => "Không có quyền thực hiện thao tác này.",
            404 => "Không tìm thấy.",
            405 => "Phương thức không được hỗ trợ.",
            409 => "Xung đột dữ liệu.",
            422 => "Vi phạm quy tắc nghiệp vụ.",
            _ => "Lỗi hệ thống. Vui lòng thử lại hoặc báo quản trị kèm mã tra cứu.",
        };

        /// <summary>400 cho lỗi model binding / DataAnnotations của route v1 (route cũ giữ mặc định).</summary>
        public static IActionResult InvalidModelState(ActionContext context, Func<ActionContext, IActionResult> legacyFactory)
        {
            if (!IsV1(context.ActionDescriptor))
            {
                return legacyFactory(context);
            }
            var naming = context.HttpContext.RequestServices.GetRequiredService<IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>()
                .Value.JsonSerializerOptions.PropertyNamingPolicy ?? JsonNamingPolicy.CamelCase;
            var errors = context.ModelState
                .Where(kv => kv.Value is { Errors.Count: > 0 })
                .SelectMany(kv => kv.Value!.Errors.Select(e => new ApiError
                {
                    Code = ErrorCodes.ValidationFailed,
                    Field = ToFieldName(kv.Key, naming),
                    Message = string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Giá trị không hợp lệ." : e.ErrorMessage,
                }))
                .ToList();
            var envelope = new ApiEnvelope<object?>
            {
                Success = false,
                Message = "Dữ liệu không hợp lệ.",
                Errors = errors,
                TraceId = TraceIdOf(context.HttpContext),
            };
            return new BadRequestObjectResult(envelope);
        }

        private static string? ToFieldName(string key, JsonNamingPolicy naming)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }
            // "$.startTime" (lỗi JSON) hoặc "request.Name" / "Name" (DataAnnotations).
            string trimmed = key.StartsWith("$.", StringComparison.Ordinal) ? key[2..] : key;
            string last = trimmed.Contains('.') ? trimmed[(trimmed.LastIndexOf('.') + 1)..] : trimmed;
            return naming.ConvertName(last);
        }
    }

    /// <summary>Đăng ký factory 400 cho v1.</summary>
    public sealed class ConfigureV1ApiBehavior : IPostConfigureOptions<ApiBehaviorOptions>
    {
        public void PostConfigure(string? name, ApiBehaviorOptions options)
        {
            var legacy = options.InvalidModelStateResponseFactory;
            options.InvalidModelStateResponseFactory = ctx => EnvelopeResultFilter.InvalidModelState(ctx, legacy);
        }
    }
}
