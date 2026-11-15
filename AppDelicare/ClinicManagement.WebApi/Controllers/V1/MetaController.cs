using AppDelicare.Application.Common;
using AppDelicare.Application.Common.Lookups;
using AppDelicare.WebApi.Api;
using AppDelicare.WebApi.Security;
using Microsoft.AspNetCore.Mvc;

namespace AppDelicare.WebApi.Controllers.V1
{
    /// <summary>API hỗ trợ giao diện (Spec 2.2) – <c>/api/v1/meta</c>. Controller mỏng: chỉ gửi query tới tầng Application.</summary>
    [Route(RoutePrefix + "/meta")]
    public sealed class MetaController(IDispatcher dispatcher) : ApiControllerBase
    {
        /// <summary>Dropdown tham chiếu cho một bảng.</summary>
        /// <remarks>
        /// Ví dụ: <c>GET /api/v1/meta/lookups/Customer?q=ánh dương&amp;take=20</c>, hoặc lấy nhãn theo Id: <c>?ids=…&amp;ids=…</c>.
        /// <para><c>table</c>: tên bảng thật (vd. <c>Dishe</c>) hoặc tên class (<c>Dish</c>). Nhãn: DisplayName → Name → FullName → số chứng từ → Code.</para>
        /// Bảng có cột Status: chỉ trả bản ghi Active (trừ khi lọc theo <c>ids</c>).
        /// Chỉ cần đăng nhập (dropdown dùng chung nhiều màn hình; chỉ trả id / nhãn / mã / trạng thái) – PROPOSED, GAP-API-02.
        /// </remarks>
        /// <response code="200">Danh sách <c>{ id, label, code, status }</c>.</response>
        /// <response code="404">NOT_FOUND – bảng không có hoặc không cho tra cứu.</response>
        [HttpGet("lookups/{table}")]
        [SkipPermission]
        [ProducesResponseType(typeof(ApiEnvelope<IReadOnlyList<LookupItem>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiEnvelope<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Lookup(string table, [FromQuery] string? q, [FromQuery] int take = 50,
            [FromQuery] Guid[]? ids = null, CancellationToken cancellationToken = default)
            => Ok(await dispatcher.SendAsync(new LookupQuery(table, q, take, ids), cancellationToken));
    }
}
