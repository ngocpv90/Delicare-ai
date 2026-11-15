using AppDelicare.Application.Admin;
using AppDelicare.Application.Common;
using AppDelicare.Data.Share;
using AppDelicare.Model.Request;
using AppDelicare.Services.Interface;
using AppDelicare.WebApi.Api;
using AppDelicare.WebApi.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppDelicare.WebApi.Controllers.V1
{
    public sealed class LoginBody
    {
        /// <example>admin</example>
        public string? UserName { get; init; }
        public string? Password { get; init; }
    }

    /// <summary>Kết quả đăng nhập v1.</summary>
    public sealed record LoginResult(string AccessToken, int ExpiresIn, string TokenType, Guid UserId, string? UserName, string? FullName, bool IsAdmin);

    public sealed class ChangePasswordBody
    {
        public string? OldPassword { get; init; }
        public string? NewPassword { get; init; }
    }

    /// <summary>
    /// Xác thực v1 – <c>/api/v1/auth</c> (spec 3.1). Đăng nhập dùng lại LoginService (ghi LocationTracking, IP client).
    /// Refresh token: chưa làm – chưa có nơi lưu (không có bảng; cần cache dùng chung khi chạy nhiều instance) → GAP-AUTH-01.
    /// </summary>
    [Route(RoutePrefix + "/auth")]
    public sealed class AuthController(ILoginService login, IDispatcher dispatcher, ICurrentActor actor) : ApiControllerBase
    {
        /// <summary>Đăng nhập → <c>accessToken</c> (JWT, dùng header Authorization: Bearer …).</summary>
        /// <response code="200">Token + thông tin user.</response>
        /// <response code="401">Sai tài khoản / mật khẩu hoặc tài khoản bị khóa.</response>
        [HttpPost("login"), AllowAnonymous]
        [ProducesResponseType(typeof(ApiEnvelope<LoginResult>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiEnvelope<object>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginBody body)
        {
            if (string.IsNullOrWhiteSpace(body.UserName) || string.IsNullOrEmpty(body.Password))
            {
                throw ApiException.Validation("userName", "Tên đăng nhập và mật khẩu là bắt buộc.");
            }
            var r = await login.Authenticate(new LoginRequest { UserName = body.UserName, PassWord = body.Password });
            if (r.Code != StatusCodes.Status200OK || r.Data?.Token == null || !Guid.TryParse(r.Data.UserId, out var userId))
            {
                // Không phân biệt "sai mật khẩu" / "không tồn tại" để tránh dò tài khoản.
                throw new ApiException(StatusCodes.Status401Unauthorized, ErrorCodes.Unauthorized, r.Message ?? "Tên đăng nhập hoặc mật khẩu không đúng.");
            }
            return Ok(new LoginResult(r.Data.Token, r.Data.ExpireTime, "Bearer", userId, r.Data.UserName, r.Data.FullName, r.Data.IsAdmin));
        }

        /// <summary>Đổi mật khẩu của chính mình. Sai mật khẩu cũ → 422.</summary>
        [HttpPost("change-password"), SkipPermission]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordBody body, CancellationToken ct)
        {
            await dispatcher.SendAsync(new ChangePasswordCommand(actor.RequireActor().UserId, body.OldPassword, body.NewPassword), ct);
            return NoContent();
        }
    }

    /// <summary>Thông tin của user đang đăng nhập (spec 2.2).</summary>
    [Route(RoutePrefix + "/meta")]
    public sealed class MeController(IDispatcher dispatcher, ICurrentActor actor) : ApiControllerBase
    {
        /// <summary>Cây menu user được truy cập kèm cờ quyền (dựng sidebar, ẩn / hiện nút).</summary>
        [HttpGet("menu"), SkipPermission]
        [ProducesResponseType(typeof(ApiEnvelope<IReadOnlyList<MenuPermissionNode>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Menu(CancellationToken ct) => Ok(await dispatcher.SendAsync(new MyMenuQuery(actor.RequireActor().UserId), ct));

        /// <summary><c>{ user, branches, permissions }</c> – gọi một lần sau đăng nhập.</summary>
        [HttpGet("me"), SkipPermission]
        [ProducesResponseType(typeof(ApiEnvelope<MeDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Me(CancellationToken ct) => Ok(await dispatcher.SendAsync(new MeQuery(actor.RequireActor().UserId), ct));
    }
}

namespace AppDelicare.WebApi.Security
{
    /// <summary>Xóa cache quyền ngay khi quyền / nhóm của user thay đổi (thay vì chờ hết 5 phút).</summary>
    public sealed class PermissionCacheInvalidator(IPermissionService permissions) : IDomainEventHandler<PermissionsChanged>
    {
        public Task HandleAsync(PermissionsChanged domainEvent, CancellationToken cancellationToken)
        {
            foreach (var userId in domainEvent.UserIds)
            {
                permissions.Invalidate(userId);
            }
            return Task.CompletedTask;
        }
    }
}
