using System.ComponentModel.DataAnnotations;
using AppDelicare.Application.Admin;
using AppDelicare.Application.Common;
using AppDelicare.Data.Share;
using AppDelicare.WebApi.Api;
using AppDelicare.WebApi.Security;
using Microsoft.AspNetCore.Mvc;

namespace AppDelicare.WebApi.Controllers.V1.Admin
{
    /// <summary>Tham số danh sách người dùng.</summary>
    public sealed class UserListParams : PageQuery
    {
        /// <summary>Lọc theo trạng thái hoạt động.</summary>
        [FromQuery(Name = "isActive")] public bool? IsActive { get; set; }
        /// <summary>Lọc theo nhóm.</summary>
        [FromQuery(Name = "groupId")] public Guid? GroupId { get; set; }
    }

    /// <summary>Body tạo người dùng (spec 3.2).</summary>
    public sealed class CreateUserBody
    {
        /// <example>bep.truong01</example>
        public string? UserName { get; init; }
        /// <summary>Tối thiểu 8 ký tự.</summary>
        public string? Password { get; init; }
        public PersonDto? Person { get; init; }
        /// <summary>Danh sách UserGroup.Id.</summary>
        public List<Guid>? GroupIds { get; init; }
    }

    public sealed class GroupIdsBody
    {
        [Required] public List<Guid>? GroupIds { get; init; }
    }

    public sealed class ReasonBody
    {
        /// <example>Nghỉ việc từ 15/11</example>
        public string? Reason { get; init; }
    }

    /// <summary>Người dùng – <c>/api/v1/admin/users</c> (spec 3.2). Không bao giờ trả mật khẩu.</summary>
    [Route(RoutePrefix + "/admin/users")]
    public sealed class AdminUsersController(IDispatcher dispatcher, ICurrentActor actor) : ApiControllerBase
    {
        private const string Screen = "adm-user";

        /// <summary>Danh sách: lọc isActive, groupId, q (tên đăng nhập / họ tên / SĐT); kèm person và groups.</summary>
        [HttpGet, Permission(Screen, PermissionAction.View)]
        [ProducesResponseType(typeof(ApiEnvelope<PagedData<UserDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> List([FromQuery] UserListParams p, CancellationToken ct)
        {
            var r = await dispatcher.SendAsync(new UserListQuery(p.Page, p.PageSize, p.Q, p.IsActive, p.GroupId), ct);
            return Ok(new PagedData<UserDto> { Items = r.Items, Total = r.Total, Page = r.Page, PageSize = r.PageSize });
        }

        /// <summary>Chi tiết người dùng.</summary>
        [HttpGet("{id:guid}"), Permission(Screen, PermissionAction.View)]
        [ProducesResponseType(typeof(ApiEnvelope<UserDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await dispatcher.SendAsync(new UserGetQuery(id), ct));

        /// <summary>Tạo người dùng: Person → User (IsActive) → UserClaim từng nhóm, trong 1 transaction. Trùng tên đăng nhập → 409.</summary>
        [HttpPost, Idempotent, Permission(Screen, PermissionAction.Add)]
        [ProducesResponseType(typeof(ApiEnvelope<UserDto>), StatusCodes.Status201Created)]
        public async Task<IActionResult> Create([FromBody] CreateUserBody b, CancellationToken ct)
            => StatusCode(StatusCodes.Status201Created, await dispatcher.SendAsync(new CreateUserCommand(b.UserName, b.Password, b.Person, b.GroupIds), ct));

        /// <summary>Sửa hồ sơ (Person) của người dùng.</summary>
        [HttpPut("{id:guid}"), Permission(Screen, PermissionAction.Edit)]
        [ProducesResponseType(typeof(ApiEnvelope<UserDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Update(Guid id, [FromBody] PersonDto person, CancellationToken ct)
            => Ok(await dispatcher.SendAsync(new UpdateUserCommand(id, person), ct));

        /// <summary>Gán nhóm: thêm mới, xóa mềm nhóm bị bỏ. Cache quyền của user được xóa ngay.</summary>
        [HttpPut("{id:guid}/groups"), Permission(Screen, PermissionAction.Edit)]
        [ProducesResponseType(typeof(ApiEnvelope<IReadOnlyList<GroupBrief>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> SetGroups(Guid id, [FromBody] GroupIdsBody b, CancellationToken ct)
            => Ok(await dispatcher.SendAsync(new SetUserGroupsCommand(id, b.GroupIds!), ct));

        /// <summary>Khóa tài khoản (bắt buộc lý do; không tự khóa chính mình).</summary>
        [HttpPost("{id:guid}/lock"), Permission(Screen, PermissionAction.Edit)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> Lock(Guid id, [FromBody] ReasonBody b, CancellationToken ct)
        {
            await dispatcher.SendAsync(new SetUserActiveCommand(id, false, b.Reason, actor.RequireActor().UserId), ct);
            return NoContent();
        }

        /// <summary>Mở khóa tài khoản.</summary>
        [HttpPost("{id:guid}/unlock"), Permission(Screen, PermissionAction.Edit)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> Unlock(Guid id, [FromBody] ReasonBody? b, CancellationToken ct)
        {
            await dispatcher.SendAsync(new SetUserActiveCommand(id, true, b?.Reason, actor.RequireActor().UserId), ct);
            return NoContent();
        }

        /// <summary>Đặt lại mật khẩu → trả mật khẩu tạm MỘT lần. Chỉ admin (spec: nhóm Quản trị hệ thống).</summary>
        [HttpPost("{id:guid}/reset-password"), AdminOnly]
        [ProducesResponseType(typeof(ApiEnvelope<ResetPasswordResult>), StatusCodes.Status200OK)]
        public async Task<IActionResult> ResetPassword(Guid id, CancellationToken ct) => Ok(await dispatcher.SendAsync(new ResetPasswordCommand(id), ct));

        /// <summary>Quyền hiệu lực = OR cờ trên mọi nhóm; <c>grantedBy</c> = nhóm cấp quyền.</summary>
        [HttpGet("{id:guid}/effective-permissions"), Permission(Screen, PermissionAction.View)]
        [ProducesResponseType(typeof(ApiEnvelope<IReadOnlyList<MenuPermissionNode>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> EffectivePermissions(Guid id, CancellationToken ct) => Ok(await dispatcher.SendAsync(new EffectivePermissionsQuery(id), ct));
    }

    public sealed class BulkAccessBody
    {
        public bool AllowAccess { get; init; }
    }

    /// <summary>Ma trận quyền của nhóm – <c>/api/v1/admin/user-groups/{id}/…</c> (spec 3.3). CRUD nhóm ở <c>/admin/user-groups</c>.</summary>
    [Route(RoutePrefix + "/admin/user-groups/{id:guid}")]
    public sealed class AdminUserGroupPermissionsController(IDispatcher dispatcher) : ApiControllerBase
    {
        private const string Screen = "adm-permission";

        /// <summary>Cây ObjectMenu kèm cờ quyền của nhóm (menu chưa có dòng → mọi cờ false).</summary>
        [HttpGet("permissions"), Permission(Screen, PermissionAction.View)]
        [ProducesResponseType(typeof(ApiEnvelope<IReadOnlyList<MenuPermissionNode>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await dispatcher.SendAsync(new GroupPermissionsQuery(id), ct));

        /// <summary>Cập nhật các dòng thay đổi. Bật cờ bất kỳ → tự bật AllowAccess; tắt AllowAccess → tắt hết. Ghi audit, xóa cache quyền.</summary>
        [HttpPut("permissions"), Permission(Screen, PermissionAction.Edit)]
        [ProducesResponseType(typeof(ApiEnvelope<IReadOnlyList<MenuPermissionNode>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Update(Guid id, [FromBody] List<PermissionChange> changes, CancellationToken ct)
            => Ok(await dispatcher.SendAsync(new UpdateGroupPermissionsCommand(id, changes), ct));

        /// <summary>Cấp / thu hồi truy cập toàn bộ menu cấp 2.</summary>
        [HttpPost("permissions/bulk"), Permission(Screen, PermissionAction.Edit)]
        [ProducesResponseType(typeof(ApiEnvelope<object>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Bulk(Guid id, [FromBody] BulkAccessBody b, CancellationToken ct)
            => Ok(new { changed = await dispatcher.SendAsync(new BulkGroupAccessCommand(id, b.AllowAccess), ct) });

        /// <summary>Xem trước menu + cờ quyền nhóm nhận được, danh sách user của nhóm. Chỉ admin.</summary>
        [HttpGet("preview"), AdminOnly]
        [ProducesResponseType(typeof(ApiEnvelope<GroupPreview>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Preview(Guid id, CancellationToken ct) => Ok(await dispatcher.SendAsync(new GroupPreviewQuery(id), ct));
    }

    /// <summary>Menu chức năng – cây và sắp xếp (spec 3.4). CRUD ở <c>/admin/object-menus</c>.</summary>
    [Route(RoutePrefix + "/admin/object-menus")]
    public sealed class AdminObjectMenuTreeController(IDispatcher dispatcher) : ApiControllerBase
    {
        /// <summary>Cây theo ParentId / LevelShow / OrderShow.</summary>
        [HttpGet("tree"), Permission("adm-objectmenu", PermissionAction.View)]
        [ProducesResponseType(typeof(ApiEnvelope<IReadOnlyList<MenuNode>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Tree([FromQuery] bool includeInactive, CancellationToken ct) => Ok(await dispatcher.SendAsync(new MenuTreeQuery(includeInactive), ct));

        /// <summary>Đổi cha / thứ tự: <c>[{ id, parentId, orderShow }]</c>. Chặn vòng lặp.</summary>
        [HttpPut("reorder"), Permission("adm-objectmenu", PermissionAction.Edit)]
        [ProducesResponseType(typeof(ApiEnvelope<IReadOnlyList<MenuNode>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Reorder([FromBody] List<MenuOrder> items, CancellationToken ct) => Ok(await dispatcher.SendAsync(new ReorderMenusCommand(items), ct));
    }

    /// <summary>Cây đơn vị và chi nhánh (spec 3.5). CRUD ở <c>/admin/org-units</c>.</summary>
    [Route(RoutePrefix + "/admin")]
    public sealed class AdminOrgController(IDispatcher dispatcher) : ApiControllerBase
    {
        /// <summary>Cây đơn vị kèm số nhân viên, số kho, tên quản lý.</summary>
        [HttpGet("org-units/tree"), Permission("adm-orgunit", PermissionAction.View)]
        [ProducesResponseType(typeof(ApiEnvelope<IReadOnlyList<OrgNode>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Tree([FromQuery] Guid? companyId, CancellationToken ct) => Ok(await dispatcher.SendAsync(new OrgTreeQuery(companyId), ct));

        /// <summary>Đơn vị UnitType = BRANCH cho bộ chọn đơn vị trên header (mọi user đăng nhập).</summary>
        [HttpGet("branches"), SkipPermission]
        [ProducesResponseType(typeof(ApiEnvelope<IReadOnlyList<BranchItem>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Branches(CancellationToken ct) => Ok(await dispatcher.SendAsync(new BranchesQuery(), ct));
    }

    public sealed class ResolveBody
    {
        /// <example>Giữ giá trị chuẩn sau khi đối chiếu file gốc</example>
        public string? ResolutionNote { get; init; }
    }

    /// <summary>Xung đột dữ liệu khi chuyển đổi – <c>CoreDataMergeConflict</c>, Id bigint (spec 3.6).</summary>
    [Route(RoutePrefix + "/admin/merge-conflicts")]
    public sealed class AdminMergeConflictsController(IDispatcher dispatcher) : ApiControllerBase
    {
        private const string Screen = "adm-merge-conflict";

        /// <summary>Danh sách, lọc resolved=true|false.</summary>
        [HttpGet, Permission(Screen, PermissionAction.View)]
        [ProducesResponseType(typeof(ApiEnvelope<PagedData<MergeConflictDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> List([FromQuery] PageQuery p, [FromQuery] bool? resolved, CancellationToken ct)
        {
            var (items, total) = await dispatcher.SendAsync(new MergeConflictListQuery(resolved, p.Page, p.PageSize), ct);
            return Ok(new PagedData<MergeConflictDto> { Items = items, Total = total, Page = p.Page, PageSize = p.PageSize });
        }

        /// <summary>Đánh dấu đã xử lý (ResolvedAt = now, ResolutionNote).</summary>
        [HttpPost("{id:long}/resolve"), Permission(Screen, PermissionAction.Edit)]
        [ProducesResponseType(typeof(ApiEnvelope<MergeConflictDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Resolve(long id, [FromBody] ResolveBody b, CancellationToken ct)
            => Ok(await dispatcher.SendAsync(new ResolveMergeConflictCommand(id, b.ResolutionNote), ct));
    }
}
