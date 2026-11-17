using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Request;
using AppDelicare.Model.Response;
using AppDelicare.Services.Interface;
using Serilog;
using System.Net;

namespace AppDelicare.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthenticationController(ILoginService service, IMemoryCache cache) : ControllerBase
    {
        private readonly ILoginService _service = service;

        /// <summary>
        /// Rà soát BE (2026-11-17): chống dò mật khẩu – sai từ <see cref="MaxFailuresPerUser"/> lần / tên đăng nhập hoặc
        /// <see cref="MaxFailuresPerIp"/> lần / địa chỉ IP trong <see cref="Window"/> → từ chối (429) tới hết cửa sổ. Đăng nhập đúng xóa bộ đếm của tên đó.
        /// Đếm trong bộ nhớ tiến trình api (1 bản – D-60); không thêm bảng / gói.
        /// </summary>
        public const int MaxFailuresPerUser = 10;
        public const int MaxFailuresPerIp = 50;
        public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

        [HttpPost("Login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ConstantHelper.Message.ModelStateMessage);
                }
                string userKey = "login-fail:u:" + (request.UserName ?? string.Empty).Trim().ToLowerInvariant();
                string ipKey = "login-fail:ip:" + (HttpContext.Connection.RemoteIpAddress?.ToString() ?? "-");
                if (Count(userKey) >= MaxFailuresPerUser || Count(ipKey) >= MaxFailuresPerIp)
                {
                    Log.Warning("Đăng nhập bị tạm khóa do sai nhiều lần: {UserName} từ {Ip}", request.UserName, HttpContext.Connection.RemoteIpAddress);
                    return StatusCode(StatusCodes.Status429TooManyRequests, new Response<AuthenticateResponse>
                    {
                        Code = StatusCodes.Status429TooManyRequests,
                        Message = $"Đăng nhập sai quá nhiều lần – vui lòng thử lại sau {Window.TotalMinutes:0} phút.",
                    });
                }
                var result = await _service.Authenticate(request);
                if (result.Code == StatusCodes.Status200OK && result.Data != null)
                {
                    cache.Remove(userKey);
                }
                else
                {
                    Increment(userKey);
                    Increment(ipKey);
                }
                return Ok(result);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Login: ", ex.ToString()));
                return StatusCode(Convert.ToInt32(HttpStatusCode.InternalServerError), ConstantHelper.Message.InternalServer);
            }
        }

        private int Count(string key) => cache.TryGetValue(key, out Counter? c) && c != null ? c.Value : 0;

        private void Increment(string key)
            => cache.GetOrCreate(key, e => { e.AbsoluteExpirationRelativeToNow = Window; return new Counter(); })!.Increment();

        private sealed class Counter
        {
            private int _value;
            public int Value => Volatile.Read(ref _value);
            public void Increment() => Interlocked.Increment(ref _value);
        }
    }
}
