using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using AppDelicare.WebApi.Api;
using AppDelicare.WebApi.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AppDelicare.Tests.Security
{
    /// <summary>T07 – UserRole × ObjectMenu, admin bỏ qua, ReferenceGuard (409 IN_USE), duyệt theo nhóm.</summary>
    public class PermissionTests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public PermissionTests(InMemoryApiFactory factory) => _factory = factory;

        /// <summary>Tạo user thường thuộc 1 nhóm, có quyền trên #/hr-shift theo cờ cho trước.</summary>
        private async Task<Guid> CreateUserAsync(bool access, bool add, bool edit = false, bool delete = false, bool active = true, string group = "T07-GROUP")
        {
            var userId = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                var groupId = Guid.NewGuid();
                var menuId = Guid.NewGuid();
                db.User.Add(new User { Id = userId, UserName = "u-" + userId.ToString("N")[..8], IsActive = active, IsAdmin = false });
                db.UserGroup.Add(new UserGroup { Id = groupId, Name = group });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = userId, UserGroupId = groupId });
                db.ObjectMenu.Add(new ObjectMenu { Id = menuId, Name = "Ca", MetaLink = "#/hr-shift", IsActive = true, LevelShow = 2 });
                db.UserRole.Add(new UserRole
                {
                    Id = Guid.NewGuid(), UserGroupId = groupId, ObjectMenuId = menuId,
                    AllowAccess = access, AllowAdd = add, AllowEdit = edit, AllowDelete = delete,
                });
                return Task.CompletedTask;
            });
            return userId;
        }

        private static object NewShift(string code) => new { code, name = "Ca " + code, startTime = "06:00:00", endTime = "10:00:00" };

        [Fact]
        public async Task User_without_AllowAdd_gets_403_on_insert_but_can_view()
        {
            var userId = await CreateUserAsync(access: true, add: false);
            using var client = _factory.CreateAuthorizedClient(userId);

            var post = await client.PostAsJsonAsync("/api/v1/hr/shifts", NewShift("T07-NOADD"));
            var list = await client.GetAsync("/api/v1/hr/shifts");

            post.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            using var body = JsonDocument.Parse(await post.Content.ReadAsStringAsync());
            body.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
            body.RootElement.GetProperty("errors")[0].GetProperty("code").GetString().Should().Be(ErrorCodes.PermissionDenied);
            list.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Legacy_insert_route_is_also_protected()
        {
            var userId = await CreateUserAsync(access: true, add: false);
            using var client = _factory.CreateAuthorizedClient(userId);

            var response = await client.PostAsync("/api/Shift/Insert", new FormUrlEncodedContent(new Dictionary<string, string> { ["Code"] = "X", ["Name"] = "X" }));

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task User_with_AllowAdd_can_insert_and_admin_can_always_insert()
        {
            var userId = await CreateUserAsync(access: true, add: true);
            using var user = _factory.CreateAuthorizedClient(userId);
            using var admin = _factory.CreateAuthorizedClient();

            (await user.PostAsJsonAsync("/api/v1/hr/shifts", NewShift("T07-ADD"))).StatusCode.Should().Be(HttpStatusCode.Created);
            (await admin.PostAsJsonAsync("/api/v1/hr/shifts", NewShift("T07-ADMIN"))).StatusCode.Should().Be(HttpStatusCode.Created);
        }

        [Fact]
        public async Task User_without_menu_access_gets_403_even_on_view()
        {
            var userId = await CreateUserAsync(access: false, add: true);
            using var client = _factory.CreateAuthorizedClient(userId);

            (await client.GetAsync("/api/v1/hr/shifts")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task Inactive_user_gets_403()
        {
            var userId = await CreateUserAsync(access: true, add: true, active: false);
            using var client = _factory.CreateAuthorizedClient(userId);

            (await client.GetAsync("/api/v1/hr/shifts")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task Admin_only_actions_are_denied_for_non_admin_with_full_rights()
        {
            var userId = await CreateUserAsync(access: true, add: true, edit: true, delete: true);
            using var client = _factory.CreateAuthorizedClient(userId);

            (await client.GetAsync("/api/AuditChange/DeleteData")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await client.GetAsync($"/api/User/SaveIsAdmin?id={userId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task Deleting_a_referenced_shift_returns_409_in_use_and_unreferenced_delete_works()
        {
            using var admin = _factory.CreateAuthorizedClient();
            var created = await admin.PostAsJsonAsync("/api/v1/hr/shifts", NewShift("T07-REF"));
            var shiftId = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.GetProperty("data").GetProperty("id").GetGuid();
            await _factory.SeedAsync(db =>
            {
                db.ShiftAssignment.Add(new ShiftAssignment { Id = Guid.NewGuid(), ShiftId = shiftId, EmployeeId = Guid.NewGuid(), WorkDate = DateTime.Today, AssignmentStatus = "Planned" });
                return Task.CompletedTask;
            });

            var inUse = await admin.DeleteAsync($"/api/v1/hr/shifts/{shiftId}");

            inUse.StatusCode.Should().Be(HttpStatusCode.Conflict);
            using var body = JsonDocument.Parse(await inUse.Content.ReadAsStringAsync());
            body.RootElement.GetProperty("errors")[0].GetProperty("code").GetString().Should().Be(ErrorCodes.InUse);
            body.RootElement.GetProperty("message").GetString().Should().Contain("ShiftAssignment");

            var free = await admin.PostAsJsonAsync("/api/v1/hr/shifts", NewShift("T07-FREE"));
            var freeId = JsonDocument.Parse(await free.Content.ReadAsStringAsync()).RootElement.GetProperty("data").GetProperty("id").GetGuid();
            (await admin.DeleteAsync($"/api/v1/hr/shifts/{freeId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        [Fact]
        public async Task Soft_deleted_reference_does_not_block_delete()
        {
            using var admin = _factory.CreateAuthorizedClient();
            var created = await admin.PostAsJsonAsync("/api/v1/hr/shifts", NewShift("T07-SOFT"));
            var shiftId = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.GetProperty("data").GetProperty("id").GetGuid();
            await _factory.SeedAsync(db =>
            {
                db.ShiftAssignment.Add(new ShiftAssignment { Id = Guid.NewGuid(), ShiftId = shiftId, EmployeeId = Guid.NewGuid(), WorkDate = DateTime.Today, AssignmentStatus = "Planned", IsDeleted = true });
                return Task.CompletedTask;
            });

            (await admin.DeleteAsync($"/api/v1/hr/shifts/{shiftId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        [Fact]
        public void Ops_scm_cannot_approve_production_plan_but_coo_can()
        {
            // Nhóm duyệt đọc từ appsettings.json thật (cột "A" của ma trận ORG_STRUCTURE).
            var options = _factory.Services.GetRequiredService<IOptions<PermissionOptions>>();
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IPermissionService>();
            var view = new MenuRights(true, true, true, true, true, true, true);
            var menus = new Dictionary<string, MenuRights> { ["#/prod-plan"] = view };
            UserPermissions Of(string group) => new(false, new HashSet<string> { group }, menus);

            service.IsAllowed(Of("OPS-SCM"), "prod-plan", PermissionAction.Approve).Should().BeFalse();
            service.IsAllowed(Of("OPS-SCM"), "prod-plan", PermissionAction.View).Should().BeTrue();
            service.IsAllowed(Of("COO"), "prod-plan", PermissionAction.Approve).Should().BeTrue();
            service.IsAllowed(Of("CEO"), "prod-plan", PermissionAction.Approve).Should().BeTrue();
            options.Value.ApproverGroups["production"].Should().BeEquivalentTo(["CEO", "COO"]);
        }

        [Fact]
        public void Every_authorized_endpoint_declares_a_permission()
        {
            var endpoints = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints;
            var missing = endpoints
                .Where(e => e.Metadata.GetMetadata<IAuthorizeData>() != null && e.Metadata.GetMetadata<IAllowAnonymous>() == null)
                .Where(e => e.Metadata.GetMetadata<PermissionAttribute>() == null && e.Metadata.GetMetadata<SkipPermissionAttribute>() == null)
                .Select(e => e.DisplayName)
                .ToList();

            missing.Should().BeEmpty();
            endpoints.Count(e => e.Metadata.GetMetadata<PermissionAttribute>() != null).Should().BeGreaterThan(600);
        }
    }
}
