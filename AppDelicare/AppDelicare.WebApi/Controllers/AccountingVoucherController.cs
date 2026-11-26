using System.Net;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Services.Interface;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace AppDelicare.WebApi.Controllers
{
    /// <summary>ACC-2 (D-131 d, e): Kế toán › Chứng từ kế toán (#/acc-voucher) – chứng từ gửi MISA AMIS theo kỳ tháng, tải Excel mẫu AMIS, đánh dấu đã lập.</summary>
    public class AccountingVoucherController(IAccountingVoucherService service) : BaseController
    {
        private readonly IAccountingVoucherService _service = service;

        /// <summary>Chứng từ của kỳ: month = yyyy-MM (trống = tháng này), tab = need · sale · purchase · stock · timesheet · all.</summary>
        [Security.Permission("acc-voucher", Services.Interface.PermissionAction.View)]
        [HttpGet("GetMonth")]
        public async Task<IActionResult> GetMonth([FromQuery] string? month, [FromQuery] string? tab)
        {
            try
            {
                return Ok(await _service.GetMonth(month, tab));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetMonth: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>Tải Excel mẫu AMIS cho chứng từ chọn (JSON { month, listKey }). Quyền Xuất.</summary>
        [Security.Permission("acc-voucher", Services.Interface.PermissionAction.Export)]
        [HttpPost("ExportExcel")]
        public async Task<IActionResult> ExportExcel([FromBody] VoucherActionRequest request)
        {
            try
            {
                var result = await _service.ExportExcel(request ?? new VoucherActionRequest());
                if (result.ResponseCode != 200 || result.Data == null)
                {
                    return Ok(result);
                }
                return File(result.Data.Content, result.Data.ContentType, result.Data.FileName);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ExportExcel: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>Đánh dấu đã lập trên AMIS (JSON { month, listKey }). Quyền Sửa.</summary>
        [Security.Permission("acc-voucher", Services.Interface.PermissionAction.Edit)]
        [HttpPost("MarkCreated")]
        public async Task<IActionResult> MarkCreated([FromBody] VoucherActionRequest request)
        {
            try
            {
                return Ok(await _service.MarkCreated(request ?? new VoucherActionRequest()));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " MarkCreated: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }
    }
}
