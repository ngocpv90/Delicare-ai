using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace AppDelicare.WebApi.Api
{
    /// <summary>
    /// Nền cho mọi controller <c>/api/v1/{module}/{resource}</c> (Spec 1.2, CLAUDE.md mục 4).
    /// Action trả dữ liệu thô (<c>Ok(dto)</c>, <c>Created(...)</c>); <see cref="EnvelopeResultFilter"/> bọc envelope.
    /// Lỗi: ném <see cref="ApiException"/>; input sai tự động → 400 <c>VALIDATION_FAILED</c>.
    /// </summary>
    [ApiController]
    [Authorize]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ApiEnvelope<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiEnvelope<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiEnvelope<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiEnvelope<object>), StatusCodes.Status500InternalServerError)]
    public abstract class ApiControllerBase : ControllerBase
    {
        public const string RoutePrefix = "api/v1";

        /// <summary>
        /// ETag theo <c>ModifiedDate</c> (không có rowversion – Spec 1.2). Bản ghi chưa sửa lần nào dùng <c>CreatedDate</c>.
        /// </summary>
        public static string ComputeETag(DateTime? modifiedDate, DateTime createdDate)
            => "\"" + (modifiedDate ?? createdDate).Ticks.ToString(CultureInfo.InvariantCulture) + "\"";

        /// <summary>Gắn header ETag cho response.</summary>
        protected void SetETag(string etag) => Response.Headers[HeaderNames.ETag] = etag;

        /// <summary>
        /// Kiểm tra <c>If-Match</c>: có gửi mà lệch → 409 <c>CONCURRENCY_CONFLICT</c>.
        /// Không gửi thì cho qua (PROPOSED – BA/PO CONFIRM: có bắt buộc If-Match cho PUT không).
        /// </summary>
        protected void EnsureIfMatch(string currentETag)
        {
            string ifMatch = Request.Headers[HeaderNames.IfMatch].ToString();
            if (string.IsNullOrWhiteSpace(ifMatch) || ifMatch.Trim() == "*")
            {
                return;
            }
            bool matched = ifMatch.Split(',').Select(v => v.Trim().Replace("W/", string.Empty, StringComparison.Ordinal)).Contains(currentETag);
            if (!matched)
            {
                throw ApiException.Conflict(ErrorCodes.ConcurrencyConflict,
                    "Bản ghi đã được người khác sửa. Tải lại dữ liệu rồi thử lại.");
            }
        }
    }
}
