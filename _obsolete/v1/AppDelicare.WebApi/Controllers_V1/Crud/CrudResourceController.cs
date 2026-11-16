using System.Text.Json;
using AppDelicare.Application.Common.Crud;
using AppDelicare.WebApi.Api;
using AppDelicare.WebApi.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace AppDelicare.WebApi.Controllers.V1.Crud
{
    /// <summary>Màn hình (screen-id) và đường dẫn của một resource CRUD chuẩn – khai báo trên controller con.</summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class CrudResourceAttribute(string module, string path) : Attribute
    {
        public string Module { get; } = module;
        public string Path { get; } = path;
    }

    /// <summary>
    /// T45B.1 – CRUD chuẩn (Spec 2.1) cho một màn hình. Controller con chỉ khai báo <c>[Route]</c> + <see cref="CrudResourceAttribute"/>;
    /// quyền theo màn được gắn bởi <see cref="PermissionConvention"/> (List / Get / Summary → View, POST → Add, PUT → Edit, DELETE → Delete).
    /// Dữ liệu vào / ra là JSON camelCase theo tên cột SQL – xem <c>GET /api/v1/meta/schema/{module}/{resource}</c>.
    /// </summary>
    public abstract class CrudResourceController(ICrudService crud) : ApiControllerBase
    {
        protected ResourceDefinition Resource
        {
            get
            {
                var a = (CrudResourceAttribute)Attribute.GetCustomAttribute(GetType(), typeof(CrudResourceAttribute))!;
                return ResourceRegistry.Find(a.Module, a.Path) ?? throw new InvalidOperationException($"Thiếu resource {a.Module}/{a.Path}.");
            }
        }

        private CrudListQuery ListQuery(PageQuery page)
        {
            var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "page", "pageSize", "q", "sort" };
            var filters = Request.Query.Where(kv => !reserved.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value.ToString());
            return new CrudListQuery(page.Page, page.PageSize, page.Q, page.Sort, filters);
        }

        /// <summary>Danh sách: page, pageSize (≤ 200), q (mã / số / tên), sort=field:asc, lọc theo tên cột camelCase (vd. status=Active), cột ngày: xxxFrom / xxxTo.</summary>
        /// <response code="200">Envelope, <c>data = { items, total, page, pageSize }</c>; mỗi item có <c>{field}Label</c> cho cột tham chiếu.</response>
        [HttpGet]
        [ProducesResponseType(typeof(ApiEnvelope<PagedData<Dictionary<string, object>>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> List([FromQuery] PageQuery page, CancellationToken cancellationToken)
        {
            var result = await crud.ListAsync(Resource, ListQuery(page), cancellationToken);
            return Ok(new PagedData<IDictionary<string, object?>> { Items = result.Items, Total = result.Total, Page = result.Page, PageSize = result.PageSize });
        }

        /// <summary>Thẻ KPI / lọc nhanh: <c>{ total, byStatus: [{ status, count }] }</c> (cùng bộ lọc với danh sách).</summary>
        [HttpGet("summary")]
        [ProducesResponseType(typeof(ApiEnvelope<Dictionary<string, object>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Summary([FromQuery] PageQuery page, CancellationToken cancellationToken)
            => Ok(await crud.SummaryAsync(Resource, ListQuery(page), cancellationToken));

        /// <summary>Chi tiết (header ETag dùng cho If-Match khi PUT / DELETE).</summary>
        /// <response code="404">NOT_FOUND.</response>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(ApiEnvelope<Dictionary<string, object>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiEnvelope<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
        {
            var item = await crud.GetAsync(Resource, id, cancellationToken);
            SetETag(item.ETag);
            return Ok(item.Data);
        }

        /// <summary>Tạo mới. Bắt buộc: cột NOT NULL không DEFAULT. Gửi <c>Idempotency-Key</c> để chống bấm trùng.</summary>
        /// <response code="201">Bản ghi vừa tạo.</response>
        /// <response code="400">VALIDATION_FAILED (thiếu / sai kiểu / quá dài / trường lạ hoặc cột cũ).</response>
        /// <response code="409">DUPLICATE.</response>
        /// <response code="422">REF_NOT_FOUND / BUSINESS_RULE.</response>
        [HttpPost]
        [Idempotent]
        [ProducesResponseType(typeof(ApiEnvelope<Dictionary<string, object>>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiEnvelope<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiEnvelope<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Create([FromBody] JsonElement body, CancellationToken cancellationToken)
        {
            var item = await crud.CreateAsync(Resource, body, cancellationToken);
            SetETag(item.ETag);
            return StatusCode(StatusCodes.Status201Created, item.Data);
        }

        /// <summary>Sửa (chỉ các trường gửi lên). Gửi <c>If-Match</c> = ETag đã đọc; lệch → 409 CONCURRENCY_CONFLICT.</summary>
        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(ApiEnvelope<Dictionary<string, object>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiEnvelope<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Update(Guid id, [FromBody] JsonElement body, CancellationToken cancellationToken)
        {
            var item = await crud.UpdateAsync(Resource, id, body, Request.Headers[HeaderNames.IfMatch].ToString(), cancellationToken);
            SetETag(item.ETag);
            return Ok(item.Data);
        }

        /// <summary>Xóa mềm. Đang được tham chiếu → 409 IN_USE.</summary>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ApiEnvelope<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        {
            await crud.DeleteAsync(Resource, id, Request.Headers[HeaderNames.IfMatch].ToString(), cancellationToken);
            return NoContent();
        }
    }
}
