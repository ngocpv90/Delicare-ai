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

        /// <summary>UI-6.9 (D-87): bảng kê tháng mọi khách – số liệu, trạng thái Sẵn sàng / Chờ đối soát / Không phát sinh, so tháng trước, đơn chờ theo khách. Quyền Xem.</summary>
        [HttpGet("GetMonthOverview")]
        public async Task<IActionResult> GetMonthOverview([FromQuery] AppDelicare.Model.Model.BillingMonthRequest request)
        {
            try
            {
                return Ok(await _service.GetMonthOverview(request ?? new()));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetMonthOverview: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>UI-6.9: lịch sử xuất / in bảng kê (nhật ký thao tác). Quyền Xem.</summary>
        [HttpGet("GetExportHistory")]
        public async Task<IActionResult> GetExportHistory([FromQuery] AppDelicare.Model.Model.BillingMonthRequest request)
        {
            try
            {
                return Ok(await _service.GetExportHistory(request ?? new()));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetExportHistory: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>UI-6.9: xuất lô Excel – 1 tệp, sheet Tổng hợp + mỗi khách 1 sheet. JSON body. Quyền Xuất.</summary>
        [HttpPost("ExportStatementBatch")]
        public Task<IActionResult> ExportStatementBatch([FromBody] AppDelicare.Model.Model.BillingBatchRequest request) => BatchAsync(request, "xlsx");

        /// <summary>UI-6.9: in lô PDF – tệp nén, mỗi khách 1 PDF. JSON body. Quyền In.</summary>
        [HttpPost("PrintStatementBatch")]
        public Task<IActionResult> PrintStatementBatch([FromBody] AppDelicare.Model.Model.BillingBatchRequest request) => BatchAsync(request, "pdf");

        private async Task<IActionResult> BatchAsync(AppDelicare.Model.Model.BillingBatchRequest request, string format)
        {
            try
            {
                if (null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                request.Format = format;
                var result = await _service.ExportBatch(request);
                if (result.ResponseCode != Convert.ToInt32(HttpStatusCode.OK) || result.Data == null)
                {
                    return Ok(result);
                }
                return File(result.Data.Content, result.Data.ContentType, result.Data.FileName);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ExportBatch: ", ex.ToString()));
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
