using AppDelicare.Application.Common;
using AppDelicare.Application.Common.Crud;
using AppDelicare.Application.Common.Lookups;
using AppDelicare.WebApi.Api;
using AppDelicare.WebApi.Security;
using Microsoft.AspNetCore.Mvc;

namespace AppDelicare.WebApi.Controllers.V1
{
    /// <summary>API hỗ trợ giao diện (Spec 2.2) – <c>/api/v1/meta</c>. Controller mỏng: chỉ gửi query tới tầng Application.</summary>
    [Route(RoutePrefix + "/meta")]
    public sealed class MetaController(IDispatcher dispatcher, ICrudService crud) : ApiControllerBase
    {
        /// <summary>Danh sách resource CRUD chuẩn (màn hình → route → quyền thao tác) – dùng cho frontend.</summary>
        [HttpGet("resources")]
        [SkipPermission]
        [ProducesResponseType(typeof(ApiEnvelope<IReadOnlyList<object>>), StatusCodes.Status200OK)]
        public IActionResult Resources()
            => Ok(ResourceRegistry.All.Select(r => new { r.Screen, route = "/" + r.Route, r.Table, r.CanCreate, r.CanUpdate, r.CanDelete }).ToList());

        /// <summary>Mô tả trường của một resource (kiểu, bắt buộc, độ dài, tham chiếu, nhãn) để dựng form / bảng.</summary>
        /// <response code="404">NOT_FOUND – không có resource.</response>
        [HttpGet("schema/{module}/{resource}")]
        [SkipPermission]
        [ProducesResponseType(typeof(ApiEnvelope<ResourceSchema>), StatusCodes.Status200OK)]
        public IActionResult Schema(string module, string resource)
        {
            var r = ResourceRegistry.Find(module, resource);
            return r == null ? throw ApiException.NotFound($"Không có resource {module}/{resource}.") : Ok(crud.Schema(r));
        }

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
