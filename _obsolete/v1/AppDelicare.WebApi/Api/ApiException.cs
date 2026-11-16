namespace AppDelicare.WebApi.Api
{
    /// <summary>
    /// Lỗi có chủ đích của route v1: <see cref="ExceptionToErrorMiddleware"/> đổi thành envelope + mã HTTP.
    /// Dùng các factory: <c>ApiException.NotFound(...)</c>, <c>.Conflict(...)</c>, <c>.Unprocessable(...)</c>…
    /// </summary>
    public sealed class ApiException : Exception
    {
        public int StatusCode { get; }
        public IReadOnlyList<ApiError> Errors { get; }

        public ApiException(int statusCode, string code, string message, string? field = null)
            : this(statusCode, message, [new ApiError { Code = code, Field = field, Message = message }])
        {
        }

        public ApiException(int statusCode, string message, IReadOnlyList<ApiError> errors) : base(message)
        {
            StatusCode = statusCode;
            Errors = errors;
        }

        public static ApiException Validation(string field, string message)
            => new(StatusCodes.Status400BadRequest, ErrorCodes.ValidationFailed, message, field);

        public static ApiException NotFound(string message = "Không tìm thấy hoặc đã bị xóa.")
            => new(StatusCodes.Status404NotFound, ErrorCodes.NotFound, message);

        public static ApiException Forbidden(string message = "Không có quyền thực hiện thao tác này.", string code = ErrorCodes.PermissionDenied)
            => new(StatusCodes.Status403Forbidden, code, message);

        public static ApiException Conflict(string code, string message, string? field = null)
            => new(StatusCodes.Status409Conflict, code, message, field);

        public static ApiException Unprocessable(string code, string message, string? field = null)
            => new(StatusCodes.Status422UnprocessableEntity, code, message, field);
    }
}
