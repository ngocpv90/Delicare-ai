using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Services.Interface;
using AppDelicare.WebApi.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using System.Net;

namespace AppDelicare.WebApi.Controllers
{
    /// <summary>
    /// BE3.2 (Catalog 166, spec 9.3 integration/iot/temperature): cổng nhận nhiệt độ từ cảm biến. KHÔNG dùng đăng nhập người dùng:
    /// <c>[AllowAnonymous]</c> + khóa API riêng (<see cref="IotApiKeyAttribute"/>, header <c>X-Api-Key</c>). Ghi dưới tài khoản <c>system</c>.
    /// </summary>
    [AllowAnonymous]
    [SkipPermission]
    [IotApiKey]
    public class IotTemperatureController(IIotTemperatureService service) : BaseController
    {
        private readonly IIotTemperatureService _service = service;

        /// <summary>
        /// Nhận tối đa 500 lần đo (JSON mảng <c>[{ deviceCode, measuredAt, temperatureC }]</c>). Trả <c>{ received, accepted, duplicate, outOfRange, rejected[] }</c>.
        /// Lần đo lỗi nằm trong <c>rejected</c> (index, lý do) – phần còn lại vẫn ghi; gửi lại cùng (deviceCode, measuredAt) → tính là trùng, không ghi lại.
        /// </summary>
        [HttpPost("Receive")]
        public async Task<IActionResult> Receive([FromBody] List<IotTemperatureItem>? request)
        {
            try
            {
                return Ok(await _service.Receive(request));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Receive: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }
    }
}
