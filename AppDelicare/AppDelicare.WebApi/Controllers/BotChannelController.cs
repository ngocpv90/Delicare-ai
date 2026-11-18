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
    /// BOT-1.2: định danh (ResolveIdentity, LinkByPhone, LinkByCode, TouchIdentity), JWT 10′ thay mặt user (IssueUserToken) và tự phục vụ nhân sự cho người chỉ có
    /// EmployeeId (Employee*: EmployeeId luôn lấy từ định danh, không nhận từ body).
    /// </summary>
    [AllowAnonymous]
    [SkipPermission]
    [ClientKey(MessagingCode.ClientMessaging)]
    public class BotChannelController(IBotChannelService service, ITelegramLinkService link, IUserTokenIssuer tokens, IBotEmployeeService employee) : BaseController
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


        /// <summary>BOT-1.2: Telegram user id trên bot → đã liên kết chưa, ai, trạng thái (PendingCode = cần nhập mã web), có tài khoản web, quyền vào bot / bot DH.</summary>
        [HttpPost("ResolveIdentity")]
        public Task<IActionResult> ResolveIdentity([FromBody] BotIdentityRequest request) => Run(nameof(ResolveIdentity), () => link.Resolve(request));

        /// <summary>BOT-1.2: liên kết bằng số điện thoại chia sẻ (contactUserId phải = externalUserId); khớp đúng 1 hồ sơ, 0 / nhiều → 400 gợi ý liên hệ Nhân sự.</summary>
        [HttpPost("LinkByPhone")]
        public Task<IActionResult> LinkByPhone([FromBody] BotLinkByPhoneRequest request) => Run(nameof(LinkByPhone), () => link.LinkByPhone(request));

        /// <summary>BOT-1.2: liên kết / xác nhận bằng mã web (/start &lt;mã&gt;) – sai chữ ký / hết hạn / đã dùng → 400.</summary>
        [HttpPost("LinkByCode")]
        public Task<IActionResult> LinkByCode([FromBody] BotLinkByCodeRequest request) => Run(nameof(LinkByCode), () => link.LinkByCode(request));

        /// <summary>BOT-1.2: cập nhật lần thấy cuối, tên; blocked = true khi người dùng chặn bot, false khi /start lại.</summary>
        [HttpPost("TouchIdentity")]
        public Task<IActionResult> TouchIdentity([FromBody] BotTouchRequest request) => Run(nameof(TouchIdentity), () => link.Touch(request));

        /// <summary>BOT-1.2: JWT 10′ của chính user đã liên kết (User hoạt động + quyền vào bot); PendingCode → token hạn chế (không duyệt / quản trị). Ghi IntegrationLog TOKEN.</summary>
        [HttpPost("IssueUserToken")]
        public Task<IActionResult> IssueUserToken([FromBody] BotIdentityRequest request) => Run(nameof(IssueUserToken), () => tokens.Issue(request));

        /// <summary>BOT-1.2: lịch ca 7 ngày tới của nhân viên (người chỉ có EmployeeId).</summary>
        [HttpPost("EmployeeGetMyShifts")]
        public Task<IActionResult> EmployeeGetMyShifts([FromBody] BotIdentityRequest request) => Run(nameof(EmployeeGetMyShifts), () => employee.GetMyShifts(request));

        /// <summary>BOT-1.2: ca hôm nay + hành động làm được (vào / ra ca).</summary>
        [HttpPost("EmployeeGetToday")]
        public Task<IActionResult> EmployeeGetToday([FromBody] BotIdentityRequest request) => Run(nameof(EmployeeGetToday), () => employee.GetToday(request));

        /// <summary>BOT-1.2: vào ca qua bot (GPS / QR – quy tắc tự chấm), ghi dưới system + nguồn Telegram.</summary>
        [HttpPost("EmployeeCheckIn")]
        public Task<IActionResult> EmployeeCheckIn([FromBody] BotEmployeeCheckRequest request) => Run(nameof(EmployeeCheckIn), () => employee.CheckIn(request));

        /// <summary>BOT-1.2: ra ca qua bot.</summary>
        [HttpPost("EmployeeCheckOut")]
        public Task<IActionResult> EmployeeCheckOut([FromBody] BotEmployeeCheckRequest request) => Run(nameof(EmployeeCheckOut), () => employee.CheckOut(request));

        /// <summary>BOT-1.2: đơn nghỉ / đổi ca 60 ngày của chính nhân viên.</summary>
        [HttpPost("EmployeeGetMyRequests")]
        public Task<IActionResult> EmployeeGetMyRequests([FromBody] BotIdentityRequest request) => Run(nameof(EmployeeGetMyRequests), () => employee.GetMyRequests(request));

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
