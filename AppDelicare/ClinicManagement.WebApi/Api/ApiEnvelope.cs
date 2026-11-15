using System.Text.Json.Serialization;

namespace AppDelicare.WebApi.Api
{
    /// <summary>
    /// Envelope chuẩn cho mọi response <c>/api/v1</c> (Spec 1.2, D-28), kể cả lỗi.
    /// </summary>
    /// <typeparam name="T">Kiểu dữ liệu trong <c>data</c>.</typeparam>
    public sealed class ApiEnvelope<T>
    {
        /// <summary>true nếu HTTP 2xx.</summary>
        /// <example>true</example>
        public bool Success { get; init; }

        /// <summary>Dữ liệu trả về; null khi lỗi.</summary>
        public T? Data { get; init; }

        /// <summary>Thông báo ngắn cho người dùng (tiếng Việt).</summary>
        /// <example>null</example>
        public string? Message { get; init; }

        /// <summary>Danh sách lỗi (rỗng khi thành công).</summary>
        public IReadOnlyList<ApiError> Errors { get; init; } = [];

        /// <summary>Mã tra cứu = header <c>X-Correlation-Id</c>.</summary>
        /// <example>9f1c2b7e4d0a4c1e8b3f5a6d7e8f9a0b</example>
        public string TraceId { get; init; } = string.Empty;
    }

    /// <summary>Một lỗi trong envelope. <c>code</c> theo Phụ lục A của spec.</summary>
    public sealed class ApiError
    {
        /// <summary>Mã lỗi, vd. <c>VALIDATION_FAILED</c>, <c>REF_NOT_FOUND</c>.</summary>
        /// <example>VALIDATION_FAILED</example>
        public string Code { get; init; } = ErrorCodes.BusinessRule;

        /// <summary>Tên trường (camelCase) nếu lỗi gắn với một trường.</summary>
        /// <example>name</example>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Field { get; init; }

        /// <summary>Mô tả lỗi (tiếng Việt).</summary>
        /// <example>Tên ca là bắt buộc.</example>
        public string Message { get; init; } = string.Empty;
    }

    /// <summary>Dữ liệu danh sách có phân trang (Spec 1.2).</summary>
    public sealed class PagedData<T>
    {
        /// <summary>Các dòng của trang hiện tại.</summary>
        public IReadOnlyList<T> Items { get; init; } = [];

        /// <summary>Tổng số dòng thỏa bộ lọc.</summary>
        /// <example>4</example>
        public int Total { get; init; }

        /// <summary>Trang hiện tại (bắt đầu từ 1).</summary>
        /// <example>1</example>
        public int Page { get; init; }

        /// <summary>Số dòng mỗi trang (≤ 200).</summary>
        /// <example>20</example>
        public int PageSize { get; init; }
    }

    /// <summary>Mã lỗi nghiệp vụ – Phụ lục A của DELICARE_API_Specification.md.</summary>
    public static class ErrorCodes
    {
        public const string ValidationFailed = "VALIDATION_FAILED";
        public const string Unauthorized = "UNAUTHORIZED";
        public const string PermissionDenied = "PERMISSION_DENIED";
        public const string NotApprover = "NOT_APPROVER";
        public const string NotFound = "NOT_FOUND";
        public const string MethodNotAllowed = "METHOD_NOT_ALLOWED";
        public const string InvalidTransition = "INVALID_TRANSITION";
        public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";
        public const string Duplicate = "DUPLICATE";
        public const string InUse = "IN_USE";
        public const string QcNotConcluded = "QC_NOT_CONCLUDED";
        public const string RefNotFound = "REF_NOT_FOUND";
        public const string NoActiveContract = "NO_ACTIVE_CONTRACT";
        public const string NoActiveRecipe = "NO_ACTIVE_RECIPE";
        public const string InsufficientStock = "INSUFFICIENT_STOCK";
        public const string QcRejected = "QC_REJECTED";
        public const string CoreTempOutOfRange = "CORE_TEMP_OUT_OF_RANGE";
        public const string UomConversionMissing = "UOM_CONVERSION_MISSING";
        public const string BusinessRule = "BUSINESS_RULE";
        /// <summary>Không thuộc Phụ lục A: lỗi hệ thống.</summary>
        public const string InternalError = "INTERNAL_ERROR";
    }
}
