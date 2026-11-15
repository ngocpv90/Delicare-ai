using Microsoft.AspNetCore.Mvc;
using AppDelicare.Data.Share;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Request;
using AppDelicare.Services.Interface;
using Serilog;
using System.Net;

namespace AppDelicare.WebApi.Controllers
{
    /// <summary>
    /// Nhập Excel. <c>Save</c>: luồng cũ (TestDemo). T49: <c>GetTemplateList</c>, <c>GetTemplate</c>, <c>Import</c> – nạp danh mục ban đầu.
    /// Quyền T49 = cờ Import trên màn hình của danh mục đích (vd. Customer → #/crm-customer), kiểm theo UserRole như mọi action (D-31).
    /// </summary>
    public class ImportExcelController(IImportService service, IMasterDataImportService masterDataImportService,
        IPermissionService permissionService, ICurrentActor currentActor) : BaseController
    {
        private readonly IImportService _service = service;
        private readonly IMasterDataImportService _masterDataImportService = masterDataImportService;
        private readonly IPermissionService _permissionService = permissionService;
        private readonly ICurrentActor _currentActor = currentActor;

        [HttpPost("Save")]
        public async Task<IActionResult> Save([FromForm] ImportExcelRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                return Ok(await _service.Save(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Save: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>T49: các loại file nhập được (tableName, cột, màn hình cần quyền Import; file mẫu hay file vận hành thật). Chỉ cần đăng nhập.</summary>
        [HttpGet("GetTemplateList"), Security.SkipPermission]
        public IActionResult GetTemplateList()
        {
            try
            {
                return Ok(_masterDataImportService.GetTemplateList());
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetTemplateList: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>T49: tải file mẫu .xlsx của 1 danh mục (tiêu đề tiếng Việt, cột * bắt buộc, 1 dòng ví dụ [DEMO]).</summary>
        [HttpGet("GetTemplate"), Security.SkipPermission]
        public async Task<IActionResult> GetTemplate(string tableName)
        {
            try
            {
                if (!await CanImportAsync(tableName))
                {
                    return StatusCode(Convert.ToInt32(HttpStatusCode.Forbidden));
                }
                var bytes = _masterDataImportService.BuildTemplate(tableName);
                if (bytes == null)
                {
                    return BadRequest(string.Format(ConstantHelper.Message.NotExist, "Mẫu " + tableName));
                }
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Mau_nhap_{tableName}.xlsx");
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetTemplate: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>
        /// T49: nạp file (form: TableName, File .xlsx / .xlsm). Idempotent theo mã; dòng lỗi bỏ qua và trả trong <c>data.listError</c>
        /// (số dòng + lý do); dòng hợp lệ ghi trong 1 transaction. File không lưu trên server.
        /// </summary>
        [HttpPost("Import"), Security.SkipPermission]
        public async Task<IActionResult> Import([FromForm] ImportExcelRequest request)
        {
            try
            {
                if (!ModelState.IsValid || null == request || request.File == null)
                {
                    return BadRequest(ConstantHelper.Message.FileRequired);
                }
                if (!await CanImportAsync(request.TableName))
                {
                    return StatusCode(Convert.ToInt32(HttpStatusCode.Forbidden));
                }
                string extension = Path.GetExtension(request.File.FileName).ToLowerInvariant();
                if (extension is not (".xlsx" or ".xlsm"))
                {
                    return BadRequest(ConstantHelper.Message.FileCorrectFormat);
                }
                await using var stream = request.File.OpenReadStream();
                return Ok(await _masterDataImportService.Import(request.TableName, stream));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Import: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>Quyền Import trên màn hình của danh mục đích. Loại file không tồn tại → cho qua để service trả thông báo rõ.</summary>
        private async Task<bool> CanImportAsync(string? tableName)
        {
            string? screen = _masterDataImportService.ScreenOf(tableName);
            if (screen == null)
            {
                return true;
            }
            var permissions = await _permissionService.GetAsync(_currentActor.RequireActor().UserId, HttpContext.RequestAborted);
            return _permissionService.IsAllowed(permissions, screen, PermissionAction.Import);
        }
    }
}
