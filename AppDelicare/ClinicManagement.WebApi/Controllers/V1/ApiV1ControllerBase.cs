using System.Net;
using AppDelicare.Data.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppDelicare.WebApi.Controllers.V1
{
    /// <summary>
    /// T03: nền cho route mới <c>/api/v1/...</c> (CLAUDE.md mục 4):
    /// mã HTTP thật khớp <c>ResponseCode</c>; lỗi trả ProblemDetails; input sai → 400 ValidationProblemDetails (tự động nhờ [ApiController]).
    /// Route cũ <c>api/[controller]</c> KHÔNG dùng lớp này để giữ nguyên hành vi cho Angular.
    /// </summary>
    [ApiController]
    [Authorize]
    public abstract class ApiV1ControllerBase : ControllerBase
    {
        public const string RoutePrefix = "api/v1";

        /// <summary>Trả <see cref="IResult{T}"/>: 2xx → body là result; khác → ProblemDetails cùng mã.</summary>
        protected IActionResult FromResult<T>(IResult<T> result)
            => IsSuccess(result.ResponseCode)
                ? StatusCode(result.ResponseCode, result)
                : ProblemFor(result.ResponseCode, result.ResponseMessage);

        /// <summary>Trả <see cref="IResults{T}"/> (danh sách): 2xx → body là result; khác → ProblemDetails.</summary>
        protected IActionResult FromResults<T>(IResults<T> result)
            => IsSuccess(result.ResponseCode)
                ? StatusCode(result.ResponseCode, result)
                : ProblemFor(result.ResponseCode, result.ResponseMessage);

        /// <summary>ProblemDetails với mã HTTP cho trước (404, 409, 422…).</summary>
        protected ObjectResult ProblemFor(int statusCode, string? detail, string? title = null)
            => Problem(statusCode: statusCode, detail: detail, title: title ?? DefaultTitle(statusCode));

        private static bool IsSuccess(int code) => code is >= 200 and < 300;

        private static string DefaultTitle(int statusCode) => statusCode switch
        {
            (int)HttpStatusCode.BadRequest => "Dữ liệu không hợp lệ.",
            (int)HttpStatusCode.Unauthorized => "Chưa đăng nhập.",
            (int)HttpStatusCode.Forbidden => "Không có quyền.",
            (int)HttpStatusCode.NotFound => "Không tìm thấy.",
            (int)HttpStatusCode.Conflict => "Xung đột dữ liệu hoặc sai trạng thái.",
            (int)HttpStatusCode.UnprocessableEntity => "Vi phạm quy tắc nghiệp vụ.",
            _ => "Lỗi.",
        };
    }
}
