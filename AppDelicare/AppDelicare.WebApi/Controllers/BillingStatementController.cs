using Microsoft.AspNetCore.Mvc;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Interface;
using Serilog;
using System.Net;

namespace AppDelicare.WebApi.Controllers
{
    /// <summary>
    /// BIZ-3.2 (D-77 – A7): bảng kê thanh toán tháng – màn <c>#/crm-billing</c> (PermissionConvention.ControllerScreens). Chỉ đọc: Xem / Xuất Excel / In PDF.
    /// </summary>
    public class BillingStatementController(IBillingStatementService service) : BaseController
    {
        private readonly IBillingStatementService _service = service;

        [HttpGet("GetStatement")]
        public async Task<IActionResult> GetStatement([FromQuery] BillingStatementRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.GetStatement(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetStatement: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>Xuất Excel (mẫu biên bản xác nhận khối lượng). Quyền Xuất.</summary>
        [HttpGet("ExportStatement")]
        public Task<IActionResult> ExportStatement([FromQuery] BillingStatementRequest request) => FileAsync(request, "xlsx");

        /// <summary>In PDF. Quyền In.</summary>
        [HttpGet("PrintStatement")]
        public Task<IActionResult> PrintStatement([FromQuery] BillingStatementRequest request) => FileAsync(request, "pdf");

        private async Task<IActionResult> FileAsync(BillingStatementRequest request, string format)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                request.Format = format;
                var result = await _service.Export(request);
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
    }
}
