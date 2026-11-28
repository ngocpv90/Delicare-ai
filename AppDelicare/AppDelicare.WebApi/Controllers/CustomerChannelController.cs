using AppDelicare.Model.Helper;
using AppDelicare.Services.Implement.CustomerChannel;
using AppDelicare.Services.Interface;
using AppDelicare.WebApi.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppDelicare.WebApi.Controllers
{
    /// <summary>
    /// AI-19 (D-06): API NỘI BỘ cho container <c>messaging</c> – bot Zalo khách (khóa client <c>X-Client-Key</c>, không JWT). Khách được xác định từ định danh Zalo ở máy chủ,
    /// không nhận mã khách từ body. nginx chặn <c>/api/CustomerChannel/</c> từ ngoài như <c>/api/BotChannel/</c>.
    /// </summary>
    [AllowAnonymous]
    [SkipPermission]
    [ClientKey(MessagingCode.ClientMessaging)]
    public class CustomerChannelController(ICustomerChannelService service) : BaseController
    {
        private async Task<IActionResult> Run<T>(string name, Func<Task<T>> call)
        {
            try
            {
                return Ok(await call());
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(string.Concat("[", GetType().Name, "] ", name, ": ", ex.ToString()));
                return StatusCode(500, ConstantHelper.Message.InternalServer);
            }
        }

        /// <summary>1 tin khách gửi OA → các câu trả lời (messaging gửi lại qua Zalo). Cùng EventId gửi lại → <c>duplicate</c>, không xử lý lần 2.</summary>
        [HttpPost("Handle")]
        public Task<IActionResult> Handle([FromBody] CustomerChannelRequest request) => Run(nameof(Handle), () => service.Handle(request ?? new CustomerChannelRequest()));

        /// <summary>Token truy cập Zalo OA còn hạn (tự làm mới, lưu mã hóa) – messaging dùng để gửi tin.</summary>
        [HttpPost("GetAccessToken")]
        public Task<IActionResult> GetAccessToken() => Run(nameof(GetAccessToken), service.GetAccessToken);
    }

    /// <summary>AI-19 (D-06): KD cấp mã liên kết Zalo cho 1 người liên hệ của khách (Khách 360 › Người liên hệ). Quyền Sửa Khách hàng.</summary>
    public class CustomerLinkController(ICustomerChannelService service) : BaseController
    {
        private async Task<IActionResult> Run<T>(string name, Func<Task<T>> call)
        {
            try
            {
                return Ok(await call());
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(string.Concat("[", GetType().Name, "] ", name, ": ", ex.ToString()));
                return StatusCode(500, ConstantHelper.Message.InternalServer);
            }
        }

        [HttpPost("CreateCode")]
        [Permission("crm-customer", PermissionAction.Edit)]
        public Task<IActionResult> CreateCode([FromForm] CustomerLinkCodeRequest request) => Run(nameof(CreateCode), () => service.CreateLinkCode(request));
    }
}
