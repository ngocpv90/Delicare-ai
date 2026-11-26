using System.Net;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Services.Interface;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace AppDelicare.WebApi.Controllers
{
    /// <summary>ACC-8 (D-131 l): Kế toán › Đối chiếu với AMIS (#/acc-amis-recon) – 8 chỉ tiêu, số AMIS nhập Excel mẫu / nhập tay (API: ACC-7).</summary>
    public class AmisReconController(IAmisReconService service) : BaseController
    {
        private readonly IAmisReconService _service = service;

        /// <summary>Đối chiếu kỳ: month = yyyy-MM (trống = tháng trước).</summary>
        [Security.Permission("acc-amis-recon", Services.Interface.PermissionAction.View)]
        [HttpGet("GetMonth")]
        public async Task<IActionResult> GetMonth([FromQuery] string? month)
        {
            try
            {
                return Ok(await _service.GetMonth(month));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetMonth: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>Nhập tay số AMIS (JSON { month, listFigure: [{ code, value }] }). Quyền Sửa.</summary>
        [Security.Permission("acc-amis-recon", Services.Interface.PermissionAction.Edit)]
        [HttpPost("SaveFigures")]
        public async Task<IActionResult> SaveFigures([FromBody] AmisReconSaveRequest request)
        {
            try
            {
                return Ok(await _service.SaveFigures(request ?? new AmisReconSaveRequest()));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " SaveFigures: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>Tải Excel mẫu số AMIS của kỳ (điền sẵn số đã nhập). Quyền Xuất.</summary>
        [Security.Permission("acc-amis-recon", Services.Interface.PermissionAction.Export)]
        [HttpGet("Template")]
        public async Task<IActionResult> Template([FromQuery] string? month)
        {
            try
            {
                var result = await _service.Template(month);
                if (result.ResponseCode != 200 || result.Data == null)
                {
                    return Ok(result);
                }
                return File(result.Data.Content, result.Data.ContentType, result.Data.FileName);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Template: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>Nhập Excel số AMIS (form: file .xlsx theo mẫu, month). Quyền Nhập.</summary>
        [Security.Permission("acc-amis-recon", Services.Interface.PermissionAction.Import)]
        [HttpPost("ImportExcel")]
        public async Task<IActionResult> ImportExcel(IFormFile? file, [FromForm] string? month)
        {
            try
            {
                if (file == null || !Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(ConstantHelper.Message.FileCorrectFormat);
                }
                await using var stream = file.OpenReadStream();
                using var copy = new MemoryStream();
                await stream.CopyToAsync(copy);
                copy.Position = 0;
                return Ok(await _service.ImportExcel(month, copy, file.FileName));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ImportExcel: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }
    }
}
