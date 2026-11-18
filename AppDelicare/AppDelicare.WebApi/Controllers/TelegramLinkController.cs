using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Services.Interface;
using AppDelicare.WebApi.Security;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using System.Net;

namespace AppDelicare.WebApi.Controllers
{
    /// <summary>
    /// BOT-1.2 (D-62, BOT_TELEGRAM §2): liên kết Telegram trên web. CreateCode / GetMine / Unlink = chính tài khoản đang đăng nhập (không cần quyền màn);
    /// CreateCodeForEmployee = HR cấp mã cho nhân viên (quyền Sửa màn hồ sơ nhân viên #/adm-employee).
    /// </summary>
    public class TelegramLinkController(ITelegramLinkService service) : BaseController
    {
        private readonly ITelegramLinkService _service = service;

        /// <summary>Mã liên kết cho chính mình (hạn Messaging:UserCodeMinutes = 15′) + link mở bot VH / DH. Người có quyền duyệt dùng mã này để xác nhận sau khi khớp số.</summary>
        [SkipPermission]
        [HttpPost("CreateCode")]
        public Task<IActionResult> CreateCode() => Run(nameof(CreateCode), () => _service.CreateCode());

        /// <summary>HR cấp mã cho nhân viên không khớp được số điện thoại (hạn Messaging:EmployeeCodeHours = 72 giờ).</summary>
        [Permission("adm-employee", PermissionAction.Edit)]
        [HttpPost("CreateCodeForEmployee")]
        public Task<IActionResult> CreateCodeForEmployee([FromForm] TelegramCodeForEmployeeRequest request) => Run(nameof(CreateCodeForEmployee), () => _service.CreateCodeForEmployee(request.EmployeeId));

        /// <summary>Các bot Telegram đã liên kết với tài khoản đang đăng nhập.</summary>
        [SkipPermission]
        [HttpGet("GetMine")]
        public Task<IActionResult> GetMine() => Run(nameof(GetMine), () => _service.GetMine());

        /// <summary>Gỡ 1 liên kết của chính mình.</summary>
        [SkipPermission]
        [HttpPost("Unlink")]
        public Task<IActionResult> Unlink([FromForm] Guid id) => Run(nameof(Unlink), () => _service.UnlinkMine(id));

        private async Task<IActionResult> Run<T>(string action, Func<Task<T>> call)
        {
            try
            {
                return Ok(await call());
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ", action, ": ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }
    }
}
