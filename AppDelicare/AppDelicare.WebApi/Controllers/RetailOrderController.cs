using AppDelicare.Model.Helper;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Interface;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using System.Net;

namespace AppDelicare.WebApi.Controllers
{
    /// <summary>
    /// RETAIL-1: đơn khách lẻ – màn "Bán lẻ › Đơn khách lẻ" (#/sal-retail-order, ScreenMap RetailOrder). Quyền theo tên action (PermissionConvention):
    /// Get* = Xem, Create = Thêm, UpdateLines / Transition / AssignRoute = Sửa, Export = Xuất. Không có Insert / Update / Delete chung (đơn đi theo máy trạng thái).
    /// </summary>
    public class RetailOrderController(IRetailOrderService service) : BaseController
    {
        private readonly IRetailOrderService _service = service;

        [HttpGet("GetList")]
        public Task<IActionResult> GetList([FromQuery] RetailOrderListRequest request) => Run(nameof(GetList), () => _service.GetList(request ?? new()));

        [HttpGet("GetDetail")]
        public Task<IActionResult> GetDetail(Guid id) => Run(nameof(GetDetail), () => _service.GetDetail(id));

        [HttpGet("GetPrepSummary")]
        public Task<IActionResult> GetPrepSummary(string? date) => Run(nameof(GetPrepSummary), () => _service.GetPrepSummary(date));

        /// <summary>[FromBody]: đơn có danh sách dòng món.</summary>
        [HttpPost("Create")]
        public Task<IActionResult> Create([FromBody] RetailOrderCreateRequest request)
            => request == null ? Task.FromResult<IActionResult>(BadRequest(ConstantHelper.Message.ModelStateMessage)) : Run(nameof(Create), () => _service.Create(request));

        /// <summary>[FromBody]: đơn có danh sách dòng món.</summary>
        [HttpPut("UpdateLines")]
        public Task<IActionResult> UpdateLines([FromBody] RetailOrderUpdateLinesRequest request)
            => request == null ? Task.FromResult<IActionResult>(BadRequest(ConstantHelper.Message.ModelStateMessage)) : Run(nameof(UpdateLines), () => _service.UpdateLines(request));

        [HttpPost("Transition")]
        public Task<IActionResult> Transition([FromForm] RetailOrderTransitionRequest request)
            => request == null ? Task.FromResult<IActionResult>(BadRequest(ConstantHelper.Message.ModelStateMessage)) : Run(nameof(Transition), () => _service.Transition(request));

        /// <summary>RETAIL-2: gán / bỏ chuyến giao (quyền Sửa).</summary>
        [HttpPost("AssignRoute")]
        public Task<IActionResult> AssignRoute([FromForm] RetailOrderAssignRouteRequest request)
            => request == null ? Task.FromResult<IActionResult>(BadRequest(ConstantHelper.Message.ModelStateMessage)) : Run(nameof(AssignRoute), () => _service.AssignRoute(request));

        [HttpGet("Export")]
        public async Task<IActionResult> Export([FromQuery] RetailOrderListRequest request)
        {
            try
            {
                var result = await _service.Export(request ?? new());
                if (result.ResponseCode != Convert.ToInt32(HttpStatusCode.OK) || result.Data == null)
                {
                    return Ok(result);
                }
                return File(result.Data.Content, result.Data.ContentType, result.Data.FileName);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Export: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        private async Task<IActionResult> Run<T>(string action, Func<Task<T>> run)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await run());
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ", action, ": ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }
    }
}
