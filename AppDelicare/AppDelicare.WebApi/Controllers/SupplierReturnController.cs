using System.Net;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Services.Interface;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace AppDelicare.WebApi.Controllers
{
    /// <summary>ACC-5 (D-131 i): Kho › Trả hàng NCC (#/scm-supplier-return) – Thủ kho lập / ghi sổ, Mua hàng xác nhận NCC.</summary>
    public class SupplierReturnController(ISupplierReturnService service) : BaseController
    {
        private readonly ISupplierReturnService _service = service;

        /// <summary>Danh sách: state = all | open | RETURN_PENDING | RETURN_AGREED | RETURN_POSTED | RETURN_REFUSED | RETURN_CANCELLED; month = yyyy-MM.</summary>
        [Security.Permission("scm-supplier-return", Services.Interface.PermissionAction.View)]
        [HttpGet("GetList")]
        public Task<IActionResult> GetList([FromQuery] string? state, [FromQuery] string? search, [FromQuery] string? month) => Run(nameof(GetList), async () => await _service.GetList(state, search, month));

        [Security.Permission("scm-supplier-return", Services.Interface.PermissionAction.View)]
        [HttpGet("GetDetail")]
        public Task<IActionResult> GetDetail([FromQuery] Guid id) => Run(nameof(GetDetail), async () => await _service.GetDetail(id));

        /// <summary>Lô còn tồn để chọn trả (tìm theo lô / NL / NCC / phiếu nhận).</summary>
        [Security.Permission("scm-supplier-return", Services.Interface.PermissionAction.Add)]
        [HttpGet("GetLots")]
        public Task<IActionResult> GetLots([FromQuery] string? search, [FromQuery] Guid? supplierId) => Run(nameof(GetLots), async () => await _service.GetLots(search, supplierId));

        /// <summary>Thủ kho lập phiếu (JSON { materialLotId, warehouseId?, quantity, reason, note?, linkedIssueId? }) – lô chuyển Tạm giữ.</summary>
        [Security.Permission("scm-supplier-return", Services.Interface.PermissionAction.Add)]
        [HttpPost("Create")]
        public Task<IActionResult> Create([FromBody] SupplierReturnCreateRequest request) => Run(nameof(Create), async () => await _service.Create(request ?? new SupplierReturnCreateRequest()));

        /// <summary>Mua hàng xác nhận NCC (JSON { id, agree, note } – không nhận thì ghi chú bắt buộc).</summary>
        [Security.Permission("scm-supplier-return", Services.Interface.PermissionAction.Edit)]
        [HttpPost("Confirm")]
        public Task<IActionResult> Confirm([FromBody] SupplierReturnActionRequest request) => Run(nameof(Confirm), async () => await _service.Confirm(request ?? new SupplierReturnActionRequest()));

        /// <summary>Thủ kho ghi sổ xuất trả khi NCC nhận hàng (JSON { id, date?, note? }).</summary>
        [Security.Permission("scm-supplier-return", Services.Interface.PermissionAction.Edit)]
        [HttpPost("Post")]
        public Task<IActionResult> Post([FromBody] SupplierReturnActionRequest request) => Run(nameof(Post), async () => await _service.Post(request ?? new SupplierReturnActionRequest()));

        /// <summary>Hủy phiếu chưa ghi sổ (JSON { id, note } – lý do bắt buộc) – lô bỏ tạm giữ.</summary>
        [Security.Permission("scm-supplier-return", Services.Interface.PermissionAction.Edit)]
        [HttpPost("Cancel")]
        public Task<IActionResult> Cancel([FromBody] SupplierReturnActionRequest request) => Run(nameof(Cancel), async () => await _service.Cancel(request ?? new SupplierReturnActionRequest()));

        private async Task<IActionResult> Run(string name, Func<Task<object>> action)
        {
            try
            {
                return Ok(await action());
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "] ", name, ": ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }
    }
}
