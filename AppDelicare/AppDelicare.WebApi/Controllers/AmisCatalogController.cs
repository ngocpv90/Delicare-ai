using System.Net;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Services.Interface;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace AppDelicare.WebApi.Controllers
{
    /// <summary>ACC-1 (D-131 c): Kế toán › Khớp mã AMIS (#/acc-amis-code) – nhận danh mục AMIS, so mã, ghép mã, xuất mã cần tạo trên AMIS.</summary>
    public class AmisCatalogController(IAmisCatalogService service) : BaseController
    {
        private readonly IAmisCatalogService _service = service;

        /// <summary>So danh mục Delicare với bản chụp AMIS mới nhất. kind: material · product · unit · customer · supplier · warehouse.</summary>
        [Security.Permission("acc-amis-code", Services.Interface.PermissionAction.View)]
        [HttpGet("GetMatch")]
        public async Task<IActionResult> GetMatch([FromQuery] string? kind)
        {
            try
            {
                return Ok(await _service.GetMatch(kind));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetMatch: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>Nhận danh mục từ tệp Excel xuất từ AMIS (form: file, kind tùy chọn – trống = tự nhận). Quyền Nhập.</summary>
        [Security.Permission("acc-amis-code", Services.Interface.PermissionAction.Import)]
        [HttpPost("ImportExcel")]
        public async Task<IActionResult> ImportExcel(IFormFile? file, [FromForm] string? kind)
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
                return Ok(await _service.ImportExcel(copy, file.FileName, kind));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ImportExcel: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>Ghép mã: đổi mã Delicare (nguyên liệu / khách / NCC) thành mã AMIS. JSON { kind, id, amisCode }. Quyền Sửa.</summary>
        [Security.Permission("acc-amis-code", Services.Interface.PermissionAction.Edit)]
        [HttpPost("Remap")]
        public async Task<IActionResult> Remap([FromBody] AmisRemapRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.Remap(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Remap: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>Excel danh sách mã cần tạo trên AMIS. Quyền Xuất.</summary>
        [Security.Permission("acc-amis-code", Services.Interface.PermissionAction.Export)]
        [HttpGet("ExportMissing")]
        public async Task<IActionResult> ExportMissing()
        {
            try
            {
                var result = await _service.ExportMissing();
                if (result.ResponseCode != 200 || result.Data == null)
                {
                    return Ok(result);
                }
                return File(result.Data.Content, result.Data.ContentType, result.Data.FileName);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ExportMissing: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }
    }
}
