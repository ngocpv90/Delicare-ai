using System.Net;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Services.Interface;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace AppDelicare.WebApi.Controllers
{
    /// <summary>ACC-4 (D-131 h): Kế toán › Khóa kỳ (#/acc-period) – điều kiện, khóa (Kế toán), xin mở khóa (COO duyệt).</summary>
    public class AccountingPeriodController(IAccountingPeriodService service) : BaseController
    {
        private readonly IAccountingPeriodService _service = service;

        /// <summary>Tổng quan kỳ: month = yyyy-MM (trống = tháng trước).</summary>
        [Security.Permission("acc-period", Services.Interface.PermissionAction.View)]
        [HttpGet("GetOverview")]
        public async Task<IActionResult> GetOverview([FromQuery] string? month)
        {
            try
            {
                return Ok(await _service.GetOverview(month));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetOverview: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>Khóa kỳ (JSON { month, reason }) – đủ 5 điều kiện, kỳ đã hết. Quyền Sửa.</summary>
        [Security.Permission("acc-period", Services.Interface.PermissionAction.Edit)]
        [HttpPost("Lock")]
        public async Task<IActionResult> Lock([FromBody] PeriodActionRequest request)
        {
            try
            {
                return Ok(await _service.Lock(request ?? new PeriodActionRequest()));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Lock: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>Xin mở khóa kỳ (JSON { month, reason } – lý do bắt buộc) → COO duyệt. Quyền Sửa.</summary>
        [Security.Permission("acc-period", Services.Interface.PermissionAction.Edit)]
        [HttpPost("RequestUnlock")]
        public async Task<IActionResult> RequestUnlock([FromBody] PeriodActionRequest request)
        {
            try
            {
                return Ok(await _service.RequestUnlock(request ?? new PeriodActionRequest()));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " RequestUnlock: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }
    }
}
