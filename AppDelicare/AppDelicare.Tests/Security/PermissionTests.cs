using System.Net;
using System.Text.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Services.Interface;
using AppDelicare.Tests.Integration;
using AppDelicare.WebApi.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Security
{
    /// <summary>
    /// T07 / T45R – phân quyền theo cơ chế cũ (D-31): UserRole × ObjectMenu, admin bỏ qua, quyền duyệt = mục menu "Duyệt …",
    /// xóa cache quyền ngay khi đổi, ReferenceGuard khi xóa. Gọi route cũ <c>api/&lt;Controller&gt;/&lt;Action&gt;</c>.
    /// </summary>
    public class PermissionTests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public PermissionTests(InMemoryApiFactory factory) => _factory = factory;

        /// <summary>Tạo user thường thuộc 1 nhóm mới, có quyền trên <paramref name="metaLink"/> theo cờ cho trước. Trả (userId, groupId, userRoleId).</summary>
        private async Task<(Guid UserId, Guid GroupId, Guid RoleId)> CreateUserAsync(bool access, bool add, bool edit = false, bool delete = false,
            bool active = true, string metaLink = "#/hr-shift")
        {
            var userId = Guid.NewGuid();
            var groupId = Guid.NewGuid();
            var roleId = Guid.NewGuid();
            await _factory.SeedAsync(async db =>
            {
                var menu = await db.ObjectMenu.FirstOrDefaultAsync(m => m.MetaLink == metaLink);
                if (menu == null)
                {
                    menu = new ObjectMenu { Id = Guid.NewGuid(), Name = metaLink, MetaLink = metaLink, IsActive = true, LevelShow = 2 };
                    db.ObjectMenu.Add(menu);
                }
                db.User.Add(new User { Id = userId, UserName = "u-" + userId.ToString("N")[..8], IsActive = active, IsAdmin = false });
                db.UserGroup.Add(new UserGroup { Id = groupId, Name = "G-" + groupId.ToString("N")[..6] });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = userId, UserGroupId = groupId });
                db.UserRole.Add(new UserRole
                {
                    Id = roleId, UserGroupId = groupId, ObjectMenuId = menu.Id,
                    AllowAccess = access, AllowAdd = add, AllowEdit = edit, AllowDelete = delete,
                });
            });
            return (userId, groupId, roleId);
        }

        private static FormUrlEncodedContent ShiftForm(string code) => new(new Dictionary<string, string>
        {
            ["Code"] = code, ["Name"] = "Ca " + code, ["StartTime"] = "06:00:00", ["EndTime"] = "10:00:00",
        });

        private static async Task<int> ResponseCodeAsync(HttpResponseMessage response)
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return doc.RootElement.GetProperty("responseCode").GetInt32();
        }

        [Fact]
        public async Task User_without_AllowAdd_gets_403_on_insert_but_can_view()
        {
            var (userId, _, _) = await CreateUserAsync(access: true, add: false);
            using var client = _factory.CreateAuthorizedClient(userId);

            (await client.PostAsync("/api/Shift/Insert", ShiftForm("T07-NOADD"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await client.GetAsync("/api/Shift/GetAllData")).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task User_with_AllowAdd_can_insert_and_admin_can_always_insert()
        {
            var (userId, _, _) = await CreateUserAsync(access: true, add: true);
            using var user = _factory.CreateAuthorizedClient(userId);
            using var admin = _factory.CreateAuthorizedClient();

            var byUser = await user.PostAsync("/api/Shift/Insert", ShiftForm("T07-ADD"));
            byUser.StatusCode.Should().Be(HttpStatusCode.OK);
            (await ResponseCodeAsync(byUser)).Should().Be(200);
            (await ResponseCodeAsync(await admin.PostAsync("/api/Shift/Insert", ShiftForm("T07-ADMIN")))).Should().Be(200);
            (await _factory.QueryAsync(db => db.Shift.CountAsync(s => s.Code == "T07-ADD" || s.Code == "T07-ADMIN"))).Should().Be(2);
        }

        [Fact]
        public async Task Shift_rules_are_enforced_by_the_legacy_service()
        {
            using var admin = _factory.CreateAuthorizedClient();
            var sameTime = new FormUrlEncodedContent(new Dictionary<string, string> { ["Code"] = "T45R-SAME", ["Name"] = "x", ["StartTime"] = "06:00:00", ["EndTime"] = "06:00:00" });

            var response = await admin.PostAsync("/api/Shift/Insert", sameTime);

            (await ResponseCodeAsync(response)).Should().Be(400);
            (await _factory.QueryAsync(db => db.Shift.AnyAsync(s => s.Code == "T45R-SAME"))).Should().BeFalse();
        }

        [Fact]
        public async Task User_without_menu_access_gets_403_even_on_view()
        {
            var (userId, _, _) = await CreateUserAsync(access: false, add: false);
            using var client = _factory.CreateAuthorizedClient(userId);

            (await client.GetAsync("/api/Shift/GetAllData")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task Inactive_user_gets_401_account_locked()
        {
            var (userId, _, _) = await CreateUserAsync(access: true, add: true, active: false);
            using var client = _factory.CreateAuthorizedClient(userId);

            // HR-ACC-1.7 (D-82 h): tài khoản khóa → 401 "Tài khoản đã bị khóa" ở MỌI API đã đăng nhập (trước: 403 chỉ ở API có quyền).
            (await client.GetAsync("/api/Shift/GetAllData")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await client.GetAsync("/api/User/GetCurrent")).StatusCode.Should().Be(HttpStatusCode.Unauthorized, "kể cả API tự phục vụ");
        }

        [Fact]
        public async Task Admin_only_actions_are_denied_for_non_admin_with_full_rights()
        {
            var (userId, groupId, _) = await CreateUserAsync(access: true, add: true, edit: true, delete: true, metaLink: "#/adm-user");
            using var client = _factory.CreateAuthorizedClient(userId);

            (await client.GetAsync("/api/AuditChange/DeleteData")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await client.GetAsync($"/api/User/SaveIsAdmin?id={userId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await client.GetAsync($"/api/User/ResetPassword?id={userId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await client.GetAsync($"/api/UserRole/GetPreview?userGroupId={groupId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task Changing_rights_via_UserRole_Update_applies_on_the_next_call()
        {
            var (userId, groupId, roleId) = await CreateUserAsync(access: true, add: false);
            using var user = _factory.CreateAuthorizedClient(userId);
            using var admin = _factory.CreateAuthorizedClient();
            (await user.PostAsync("/api/Shift/Insert", ShiftForm("T45R-CACHE-1"))).StatusCode.Should().Be(HttpStatusCode.Forbidden); // quyền đã vào cache

            string json = JsonSerializer.Serialize(new[] { new { Id = roleId, UserGroupId = groupId, AllowAccess = true, AllowAdd = true } });
            var update = await admin.PutAsync("/api/UserRole/Update", new FormUrlEncodedContent(new Dictionary<string, string> { ["ListPermission"] = json }));
            (await ResponseCodeAsync(update)).Should().Be(200);

            (await user.PostAsync("/api/Shift/Insert", ShiftForm("T45R-CACHE-2"))).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Locking_a_user_with_SaveIsActive_takes_effect_immediately()
        {
            var (userId, _, _) = await CreateUserAsync(access: true, add: true);
            using var user = _factory.CreateAuthorizedClient(userId);
            using var admin = _factory.CreateAuthorizedClient();
            (await user.GetAsync("/api/Shift/GetAllData")).StatusCode.Should().Be(HttpStatusCode.OK);

            (await ResponseCodeAsync(await admin.GetAsync($"/api/User/SaveIsActive?id={userId}"))).Should().Be(200);

            (await user.GetAsync("/api/Shift/GetAllData")).StatusCode.Should().Be(HttpStatusCode.Unauthorized, "HR-ACC-1.7: token đang có bị chặn ngay (401)");
        }

        [Fact]
        public async Task Admin_cannot_lock_their_own_account()
        {
            using var admin = _factory.CreateAuthorizedClient();

            var response = await admin.GetAsync($"/api/User/SaveIsActive?id={InMemoryApiFactory.UserId}");

            (await ResponseCodeAsync(response)).Should().Be(400);
            (await _factory.QueryAsync(db => db.User.SingleAsync(u => u.Id == InMemoryApiFactory.UserId))).IsActive.Should().BeTrue();
        }

        [Fact]
        public async Task Deleting_a_referenced_shift_is_refused_and_unreferenced_delete_works()
        {
            using var admin = _factory.CreateAuthorizedClient();
            await admin.PostAsync("/api/Shift/Insert", ShiftForm("T07-REF"));
            await admin.PostAsync("/api/Shift/Insert", ShiftForm("T07-FREE"));
            var shiftId = await _factory.QueryAsync(db => db.Shift.Where(s => s.Code == "T07-REF").Select(s => s.Id).SingleAsync());
            var freeId = await _factory.QueryAsync(db => db.Shift.Where(s => s.Code == "T07-FREE").Select(s => s.Id).SingleAsync());
            await _factory.SeedAsync(db =>
            {
                db.ShiftAssignment.Add(new ShiftAssignment { Id = Guid.NewGuid(), ShiftId = shiftId, EmployeeId = Guid.NewGuid(), WorkDate = DateTime.Today, AssignmentStatus = "Planned" });
                return Task.CompletedTask;
            });

            (await ResponseCodeAsync(await admin.DeleteAsync($"/api/Shift/Delete?id={shiftId}"))).Should().Be(400);
            (await ResponseCodeAsync(await admin.DeleteAsync($"/api/Shift/Delete?id={freeId}"))).Should().Be(200);
            (await _factory.QueryAsync(db => db.Shift.SingleAsync(s => s.Id == shiftId))).IsDeleted.Should().BeFalse();
        }

        [Fact]
        public async Task Soft_deleted_reference_does_not_block_delete()
        {
            using var admin = _factory.CreateAuthorizedClient();
            await admin.PostAsync("/api/Shift/Insert", ShiftForm("T07-SOFT"));
            var shiftId = await _factory.QueryAsync(db => db.Shift.Where(s => s.Code == "T07-SOFT").Select(s => s.Id).SingleAsync());
            await _factory.SeedAsync(db =>
            {
                db.ShiftAssignment.Add(new ShiftAssignment { Id = Guid.NewGuid(), ShiftId = shiftId, EmployeeId = Guid.NewGuid(), WorkDate = DateTime.Today, AssignmentStatus = "Planned", IsDeleted = true });
                return Task.CompletedTask;
            });

            (await ResponseCodeAsync(await admin.DeleteAsync($"/api/Shift/Delete?id={shiftId}"))).Should().Be(200);
        }

        [Fact]
        public void Approve_right_comes_from_the_approval_menu_not_from_group_names()
        {
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IPermissionService>();
            var full = new MenuRights(true, true, true, true, true, true, true);
            var screenOnly = new Dictionary<string, MenuRights>(StringComparer.OrdinalIgnoreCase) { ["#/prod-plan"] = full };
            var withApproval = new Dictionary<string, MenuRights>(screenOnly, StringComparer.OrdinalIgnoreCase)
            {
                ["#/wf-inbox?type=prod-plan"] = new MenuRights(true, false, true, false, false, false, false),
            };

            // Tên nhóm (kể cả "COO") không cấp quyền duyệt; chỉ dòng UserRole trên mục "Duyệt …" mới cấp.
            service.IsAllowed(new UserPermissions(false, new HashSet<string> { "COO" }, screenOnly), "prod-plan", PermissionAction.Approve).Should().BeFalse();
            service.IsAllowed(new UserPermissions(false, new HashSet<string> { "OPS-SCM" }, screenOnly), "prod-plan", PermissionAction.View).Should().BeTrue();
            service.IsAllowed(new UserPermissions(false, new HashSet<string> { "đổi-tên-bất-kỳ" }, withApproval), "prod-plan", PermissionAction.Approve).Should().BeTrue();
            service.IsAllowed(new UserPermissions(true, new HashSet<string>(), new Dictionary<string, MenuRights>()), "prod-plan", PermissionAction.Approve).Should().BeTrue();
        }

        [Fact]
        public async Task User_in_two_groups_gets_the_union_of_rights()
        {
            var (userId, _, _) = await CreateUserAsync(access: true, add: false);
            await _factory.SeedAsync(async db =>
            {
                var menu = await db.ObjectMenu.FirstAsync(m => m.MetaLink == "#/hr-shift");
                var second = Guid.NewGuid();
                db.UserGroup.Add(new UserGroup { Id = second, Name = "G2-" + second.ToString("N")[..6] });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = userId, UserGroupId = second });
                db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = second, ObjectMenuId = menu.Id, AllowAccess = true, AllowAdd = true });
            });
            using var client = _factory.CreateAuthorizedClient(userId);

            (await client.PostAsync("/api/Shift/Insert", ShiftForm("T45R-UNION"))).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public void Every_authorized_endpoint_declares_a_permission_and_no_v1_route_remains()
        {
            var endpoints = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints;
            var missing = endpoints
                .Where(e => e.Metadata.GetMetadata<IAuthorizeData>() != null && e.Metadata.GetMetadata<IAllowAnonymous>() == null)
                .Where(e => e.Metadata.GetMetadata<PermissionAttribute>() == null && e.Metadata.GetMetadata<SkipPermissionAttribute>() == null)
                .Select(e => e.DisplayName)
                .ToList();

            missing.Should().BeEmpty();
            endpoints.Count(e => e.Metadata.GetMetadata<PermissionAttribute>() != null).Should().BeGreaterThan(500);
            endpoints.OfType<RouteEndpoint>().Where(e => e.RoutePattern.RawText?.StartsWith("api/v1", StringComparison.OrdinalIgnoreCase) == true)
                .Should().BeEmpty("D-30: không còn route /api/v1");
        }
    }
}
