using System.Net;
using AppDelicare.Model.Helper;
using AppDelicare.Services.Implement;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace AppDelicare.WebApi.Controllers
{
    /// <summary>DEPLOY-SYNC-1.5 (D-134): tự kiểm menu / quyền / script chuyển đổi so bản chuẩn – chỉ admin (cảnh báo trên sidebar khi lệch).</summary>
    [Security.AdminOnly]
    public class ConfigCheckController(IConfigCheckService service) : BaseController
    {
        private readonly IConfigCheckService _service = service;

        /// <summary>Kết quả CHECK (nhớ 10 phút); <c>refresh=true</c> chạy lại ngay.</summary>
        [HttpGet("Get")]
        public async Task<IActionResult> Get([FromQuery] bool refresh = false)
        {
            try
            {
                return Ok(await _service.Run(refresh));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "] Get: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }
    }
}
