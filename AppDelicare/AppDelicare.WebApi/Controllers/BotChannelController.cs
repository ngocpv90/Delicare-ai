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
    /// BOT-1.1 (D-62): API NỘI BỘ cho container <c>messaging</c> – không JWT người dùng, chỉ khóa client (<c>X-Client-Key</c>, <see cref="ClientKeyAttribute"/>).
    /// nginx chặn <c>/api/BotChannel/</c> từ ngoài (404) – chỉ gọi trong mạng Docker (<c>http://api:8080</c>). Body JSON (như IoT – khách máy, không phải form web).
    /// Các action liên kết tài khoản / cấp JWT thay mặt user (ResolveIdentity, LinkByCode, LinkByPhone, IssueUserToken…) làm ở BOT-1.2.
    /// </summary>
    [AllowAnonymous]
    [SkipPermission]
    [ClientKey(MessagingCode.ClientMessaging)]
    public class BotChannelController(IBotChannelService service) : BaseController
    {
        private readonly IBotChannelService _service = service;

        /// <summary>Lưu 1 update (chống trùng theo bot + update_id). Gửi lại cùng update → <c>duplicate = true</c>, không tạo dòng mới.</summary>
        [HttpPost("ReceiveUpdate")]
        public Task<IActionResult> ReceiveUpdate([FromBody] BotUpdateRequest request) => Run(nameof(ReceiveUpdate), () => _service.ReceiveUpdate(request));

        /// <summary>Lấy tối đa <c>max</c> update chờ xử lý (Received → Processing). 2 lần gọi song song không nhận trùng dòng.</summary>
        [HttpPost("ClaimInbound")]
        public Task<IActionResult> ClaimInbound([FromBody] BotClaimRequest request) => Run(nameof(ClaimInbound), () => _service.ClaimInbound(request));

        /// <summary>Kết thúc xử lý update: Processed / Failed / Ignored.</summary>
        [HttpPost("CompleteInbound")]
        public Task<IActionResult> CompleteInbound([FromBody] BotCompleteInboundRequest request) => Run(nameof(CompleteInbound), () => _service.CompleteInbound(request));

        /// <summary>Lấy tối đa <c>max</c> tin cần gửi (Queued / Failed đến hạn thử lại → Sending), bỏ tin có <c>notBefore</c> chưa tới.</summary>
        [HttpPost("ClaimOutbound")]
        public Task<IActionResult> ClaimOutbound([FromBody] BotClaimRequest request) => Run(nameof(ClaimOutbound), () => _service.ClaimOutbound(request));

        /// <summary>Kết quả gửi: Sent (kèm providerMessageId) / Failed (thử lại theo Messaging:RetryDelaysMinutes) / Blocked (người dùng chặn bot).</summary>
        [HttpPost("MarkOutbound")]
        public Task<IActionResult> MarkOutbound([FromBody] BotMarkOutboundRequest request) => Run(nameof(MarkOutbound), () => _service.MarkOutbound(request));

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
