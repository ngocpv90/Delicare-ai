using System.Net;
using AppDelicare.Data.Interface;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Implement.Retail;
using AppDelicare.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace AppDelicare.WebApi.Controllers
{
    /// <summary>
    /// RETAIL-1: API CÔNG KHAI của trang khách lẻ (docs/frontend-handoff/RETAIL-1.md mục 5.1) – <c>[AllowAnonymous]</c> cả controller
    /// (khách không có tài khoản; PermissionFilter bỏ qua). Chống lạm dụng: ô bẫy, giới hạn theo SĐT / IP (429), giá + giờ chốt máy chủ tính lại.
    /// Đơn ghi dưới tài khoản system (SystemUsers:JobUserId).
    /// </summary>
    [AllowAnonymous]
    public class RetailPortalController(IRetailPortalService service, IRetailAccountService account, AppDelicare.Services.Implement.IRetailProductImageService image) : BaseController
    {
        private readonly AppDelicare.Services.Implement.IRetailProductImageService _image = image;

        /// <summary>RETAIL-1 §8: ảnh sản phẩm đang bán cho trang khách (công khai, chỉ ảnh đại diện của sản phẩm; cache 1 ngày – đổi ảnh đổi tham số v).</summary>
        [HttpGet("ProductImage")]
        public async Task<IActionResult> ProductImage(Guid id)
        {
            var f = await _image.GetPublicImage(id);
            if (f == null)
            {
                return NotFound();
            }
            Response.Headers.CacheControl = "public, max-age=86400";
            return File(f.Content, f.ContentType);
        }

        private readonly IRetailPortalService _service = service;
        private readonly IRetailAccountService _account = account;

        [HttpGet("GetCatalog")]
        public Task<IActionResult> GetCatalog() => Run(nameof(GetCatalog), () => _service.GetCatalog());

        [HttpGet("GetWeeklyMenu")]
        public Task<IActionResult> GetWeeklyMenu() => Run(nameof(GetWeeklyMenu), () => _service.GetWeeklyMenu());

        /// <summary>[FromBody]: đơn có danh sách dòng món.</summary>
        [HttpPost("SubmitOrder")]
        public Task<IActionResult> SubmitOrder([FromBody] RetailSubmitOrderRequest request)
            => request == null ? Task.FromResult<IActionResult>(BadRequest(ConstantHelper.Message.ModelStateMessage))
                : Run(nameof(SubmitOrder), async () => await _service.SubmitOrder(request, ClientIp(), (await _account.Authenticate(Token()))?.Id));

        [HttpGet("TrackOrder")]
        public Task<IActionResult> TrackOrder(string? orderNumber, string? phoneNumber)
            => Run(nameof(TrackOrder), () => _service.TrackOrder(orderNumber, phoneNumber, ClientIp()));

        // ===== RETAIL-2 (D-136): tài khoản khách lẻ + Đơn của tôi. Phiên ở header X-Retail-Token (KHÔNG dùng JWT nhân viên). =====

        /// <summary>[FromBody]: trang khách gửi JSON (cùng kiểu SubmitOrder).</summary>
        [HttpPost("Register")]
        public Task<IActionResult> Register([FromBody] RetailRegisterRequest request)
            => request == null ? Task.FromResult<IActionResult>(BadRequest(ConstantHelper.Message.ModelStateMessage))
                : Run(nameof(Register), () => _account.Register(request, ClientIp()));

        [HttpPost("Login")]
        public Task<IActionResult> Login([FromBody] RetailLoginRequest request)
            => request == null ? Task.FromResult<IActionResult>(BadRequest(ConstantHelper.Message.ModelStateMessage))
                : Run(nameof(Login), () => _account.Login(request, ClientIp()));

        [HttpGet("GetProfile")]
        public Task<IActionResult> GetProfile() => Run(nameof(GetProfile), () => _account.GetProfile(Token()));

        [HttpPost("UpdateProfile")]
        public Task<IActionResult> UpdateProfile([FromBody] RetailProfileRequest request)
            => request == null ? Task.FromResult<IActionResult>(BadRequest(ConstantHelper.Message.ModelStateMessage))
                : Run(nameof(UpdateProfile), () => _account.UpdateProfile(Token(), request));

        [HttpPost("ChangePassword")]
        public Task<IActionResult> ChangePassword([FromBody] RetailChangePasswordRequest request)
            => request == null ? Task.FromResult<IActionResult>(BadRequest(ConstantHelper.Message.ModelStateMessage))
                : Run(nameof(ChangePassword), () => _account.ChangePassword(Token(), request));

        /// <summary>group: active (đang xử lý / đang giao) · done (đã giao) · cancelled · all.</summary>
        [HttpGet("GetMyOrders")]
        public Task<IActionResult> GetMyOrders(string? group) => Run(nameof(GetMyOrders), () => _account.GetMyOrders(Token(), group));

        [HttpGet("GetMyOrder")]
        public Task<IActionResult> GetMyOrder(string? orderNumber) => Run(nameof(GetMyOrder), () => _account.GetMyOrder(Token(), orderNumber));

        [HttpGet("GetOrderTrace")]
        public Task<IActionResult> GetOrderTrace(string? orderNumber) => Run(nameof(GetOrderTrace), () => _account.GetOrderTrace(Token(), orderNumber));

        [HttpPost("ClaimOrder")]
        public Task<IActionResult> ClaimOrder([FromBody] RetailClaimOrderRequest request)
            => request == null ? Task.FromResult<IActionResult>(BadRequest(ConstantHelper.Message.ModelStateMessage))
                : Run(nameof(ClaimOrder), () => _account.ClaimOrder(Token(), request));

        private string? Token() => Request.Headers.TryGetValue(RetailAccountRules.TokenHeader, out var v) ? v.ToString() : null;

        /// <summary>
        /// IP thật của khách: sau nginx (deploy/docker/nginx) kết nối đến từ mạng nội bộ Docker, IP khách nằm ở header X-Real-IP do nginx đặt.
        /// Chỉ tin header khi kết nối đến từ địa chỉ nội bộ / loopback (khách gọi thẳng API không giả được IP).
        /// </summary>
        private string? ClientIp()
        {
            var remote = HttpContext.Connection.RemoteIpAddress;
            if (remote == null)
            {
                return null;
            }
            if (remote.IsIPv4MappedToIPv6)
            {
                remote = remote.MapToIPv4();
            }
            if (IsInternal(remote) && Request.Headers.TryGetValue("X-Real-IP", out var real) && System.Net.IPAddress.TryParse(real.ToString().Trim(), out var ip))
            {
                return ip.ToString();
            }
            return remote.ToString();
        }

        private static bool IsInternal(System.Net.IPAddress a)
        {
            if (System.Net.IPAddress.IsLoopback(a))
            {
                return true;
            }
            byte[] b = a.GetAddressBytes();
            return b.Length == 4 && (b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168));
        }

        private async Task<IActionResult> Run<T>(string action, Func<Task<IResult<T>>> run)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                var result = await run();
                // Vượt giới hạn → HTTP 429 (trang khách hiện thông báo tiếng Việt trong responseMessage).
                // RETAIL-2: phiên khách hết hạn / sai → HTTP 401 (trang khách xóa phiên, mời đăng nhập lại).
                return result.ResponseCode is RetailPortalService.TooManyRequests or RetailAccountService.Unauthorized ? StatusCode(result.ResponseCode, result) : Ok(result);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ", action, ": ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }
    }
}
