using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using AppDelicare.WebApi.Api;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Admin
{
    /// <summary>T45B.2 – API quản trị riêng của P0 (spec 3.1–3.6, 2.2).</summary>
    public class AdminApiTests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public AdminApiTests(InMemoryApiFactory factory) => _factory = factory;

        private static async Task<JsonElement> Body(HttpResponseMessage r) => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.Clone();
        private static JsonElement Data(JsonElement b) => b.GetProperty("data");

        private async Task<(Guid GroupId, Guid MenuId)> GroupWithMenuAsync(string groupName, string metaLink, bool access)
        {
            Guid group = Guid.NewGuid(), menu = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.UserGroup.Add(new UserGroup { Id = group, Name = groupName });
                if (!db.ObjectMenu.Any(m => m.MetaLink == metaLink))
                {
                    db.ObjectMenu.Add(new ObjectMenu { Id = menu, Name = metaLink, MetaLink = metaLink, IsActive = true, LevelShow = 2 });
                }
                else
                {
                    menu = db.ObjectMenu.First(m => m.MetaLink == metaLink).Id;
                }
                if (access)
                {
                    db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = group, ObjectMenuId = menu, AllowAccess = true });
                }
                return Task.CompletedTask;
            });
            return (group, menu);
        }

        private async Task<Guid> CreateUserAsync(HttpClient admin, string userName, params Guid[] groups)
        {
            var r = await admin.PostAsJsonAsync("/api/v1/admin/users", new { userName, password = "Matkhau@123", person = new { fullName = "Nguyễn " + userName, phoneNumber = "0900" }, groupIds = groups });
            r.StatusCode.Should().Be(HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
            return Data(await Body(r)).GetProperty("id").GetGuid();
        }

        [Fact]
        public async Task Create_user_with_person_and_groups_never_returns_password_and_validates()
        {
            using var admin = _factory.CreateAuthorizedClient();
            var (g, _) = await GroupWithMenuAsync("T452-SALES", "#/crm-customer", true);

            var created = await admin.PostAsJsonAsync("/api/v1/admin/users", new { userName = "T452.User1", password = "Matkhau@123", person = new { fullName = "Trần Văn A" }, groupIds = new[] { g } });
            var raw = await created.Content.ReadAsStringAsync();
            var dup = await admin.PostAsJsonAsync("/api/v1/admin/users", new { userName = "t452.user1", password = "Matkhau@123" });
            var weak = await admin.PostAsJsonAsync("/api/v1/admin/users", new { userName = "t452.weak", password = "123" });
            var badGroup = await admin.PostAsJsonAsync("/api/v1/admin/users", new { userName = "t452.badgroup", password = "Matkhau@123", groupIds = new[] { Guid.NewGuid() } });

            created.StatusCode.Should().Be(HttpStatusCode.Created);
            raw.Should().NotContainEquivalentOf("password");
            var data = Data(JsonDocument.Parse(raw).RootElement);
            data.GetProperty("userName").GetString().Should().Be("t452.user1", "tên đăng nhập chuẩn hóa chữ thường");
            data.GetProperty("person").GetProperty("fullName").GetString().Should().Be("Trần Văn A");
            data.GetProperty("groups")[0].GetProperty("name").GetString().Should().Be("T452-SALES");
            dup.StatusCode.Should().Be(HttpStatusCode.Conflict);
            weak.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            badGroup.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

            var list = await Body(await admin.GetAsync($"/api/v1/admin/users?q=Trần&groupId={g}"));
            Data(list).GetProperty("total").GetInt32().Should().Be(1);
        }

        [Fact]
        public async Task Granting_permissions_takes_effect_immediately_and_flags_are_normalized()
        {
            using var admin = _factory.CreateAuthorizedClient();
            var (g, menu) = await GroupWithMenuAsync("T452-SCM", "#/scm-supplier", access: false);
            var userId = await CreateUserAsync(admin, "t452.scm", g);
            using var user = _factory.CreateAuthorizedClient(userId);

            (await user.GetAsync("/api/v1/scm/suppliers")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

            var grant = await admin.PutAsJsonAsync($"/api/v1/admin/user-groups/{g}/permissions",
                new[] { new { menuId = menu, allowAccess = false, allowAdd = true, allowEdit = false, allowDelete = false, allowPrint = false, allowExport = false, allowImport = false } });
            grant.StatusCode.Should().Be(HttpStatusCode.OK);
            var node = Data(await Body(grant)).EnumerateArray().First(n => n.GetProperty("menuId").GetGuid() == menu);
            node.GetProperty("allowAccess").GetBoolean().Should().BeTrue("bật cờ bất kỳ → tự bật AllowAccess");

            (await user.GetAsync("/api/v1/scm/suppliers")).StatusCode.Should().Be(HttpStatusCode.OK, "cache quyền được xóa ngay khi cấp quyền");

            var effective = Data(await Body(await admin.GetAsync($"/api/v1/admin/users/{userId}/effective-permissions")));
            effective.EnumerateArray().Single(n => n.GetProperty("metaLink").GetString() == "#/scm-supplier")
                .GetProperty("grantedBy")[0].GetString().Should().Be("T452-SCM");

            await admin.PutAsJsonAsync($"/api/v1/admin/user-groups/{g}/permissions",
                new[] { new { menuId = menu, allowAccess = false, allowAdd = false, allowEdit = false, allowDelete = false, allowPrint = false, allowExport = false, allowImport = false } });
            (await user.GetAsync("/api/v1/scm/suppliers")).StatusCode.Should().Be(HttpStatusCode.Forbidden, "thu hồi cũng có hiệu lực ngay");
        }

        [Fact]
        public async Task Lock_unlock_reset_and_change_password()
        {
            using var admin = _factory.CreateAuthorizedClient();
            var (g, _) = await GroupWithMenuAsync("T452-LOCK", "#/crm-contact", true);
            var userId = await CreateUserAsync(admin, "t452.lock", g);
            using var user = _factory.CreateAuthorizedClient(userId);
            (await user.GetAsync("/api/v1/sales/customer-contacts")).StatusCode.Should().Be(HttpStatusCode.OK);

            (await admin.PostAsJsonAsync($"/api/v1/admin/users/{userId}/lock", new { reason = "" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await admin.PostAsJsonAsync($"/api/v1/admin/users/{InMemoryApiFactory.UserId}/lock", new { reason = "x" })).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "không tự khóa mình");
            (await admin.PostAsJsonAsync($"/api/v1/admin/users/{userId}/lock", new { reason = "Nghỉ việc" })).StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await user.GetAsync("/api/v1/sales/customer-contacts")).StatusCode.Should().Be(HttpStatusCode.Forbidden, "tài khoản bị khóa mất quyền ngay");
            (await admin.PostAsJsonAsync($"/api/v1/admin/users/{userId}/unlock", new { })).StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await user.GetAsync("/api/v1/sales/customer-contacts")).StatusCode.Should().Be(HttpStatusCode.OK);

            (await user.PostAsync($"/api/v1/admin/users/{userId}/reset-password", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden, "chỉ admin");
            var reset = await admin.PostAsync($"/api/v1/admin/users/{userId}/reset-password", null);
            string temp = Data(await Body(reset)).GetProperty("temporaryPassword").GetString()!;
            temp.Should().HaveLength(12);

            (await user.PostAsJsonAsync("/api/v1/auth/change-password", new { oldPassword = "sai", newPassword = "Moi@123456" })).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            (await user.PostAsJsonAsync("/api/v1/auth/change-password", new { oldPassword = temp, newPassword = "Moi@123456" })).StatusCode.Should().Be(HttpStatusCode.NoContent);

            var audits = await _factory.QueryAsync(db => db.AuditChange.AsNoTracking().Where(a => a.IdChange == userId).Select(a => a.Changes).ToListAsync());
            var stored = await _factory.QueryAsync(db => db.User.AsNoTracking().Where(u => u.Id == userId).Select(u => u.PassWord).SingleAsync());
            audits.Should().NotContain(a => a!.Contains(stored!), "nhật ký không chứa hash mật khẩu");
            audits.Should().Contain(a => a!.Contains("không lưu giá trị"));
        }

        [Fact]
        public async Task Menu_tree_reorder_and_cycle_guard()
        {
            using var admin = _factory.CreateAuthorizedClient();
            Guid parent = Guid.NewGuid(), child = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.ObjectMenu.Add(new ObjectMenu { Id = parent, Name = "T452 Nhóm", MetaLink = "#/group/t452", IsActive = true, LevelShow = 1, OrderShow = 900 });
                db.ObjectMenu.Add(new ObjectMenu { Id = child, Name = "T452 Màn", MetaLink = "#/t452-screen", IsActive = true, LevelShow = 1, OrderShow = 901 });
                return Task.CompletedTask;
            });

            var ok = await admin.PutAsJsonAsync("/api/v1/admin/object-menus/reorder", new[] { new { id = child, parentId = (Guid?)parent, orderShow = 1 } });
            ok.StatusCode.Should().Be(HttpStatusCode.OK);
            var tree = Data(await Body(ok));
            tree.EnumerateArray().Single(n => n.GetProperty("id").GetGuid() == parent).GetProperty("children")[0].GetProperty("id").GetGuid().Should().Be(child);

            var cycle = await admin.PutAsJsonAsync("/api/v1/admin/object-menus/reorder", new[] { new { id = parent, parentId = (Guid?)child, orderShow = 1 } });
            cycle.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        }

        [Fact]
        public async Task Org_tree_branches_and_merge_conflicts()
        {
            using var admin = _factory.CreateAuthorizedClient();
            Guid company = Guid.NewGuid(), root = Guid.NewGuid(), branch = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.OrganizationUnit.AddRange(
                    new OrganizationUnit { Id = root, CompanyId = company, Code = "T452-ROOT", Name = "Gốc", UnitType = "COMPANY" },
                    new OrganizationUnit { Id = branch, CompanyId = company, Code = "T452-BR", Name = "Chi nhánh", UnitType = "BRANCH", ParentId = root });
                db.Employee.Add(new Employee { Id = Guid.NewGuid(), OrganizationUnitId = branch, FullName = "NV 1" });
                db.CoreDataMergeConflict.Add(new CoreDataMergeConflict { Id = 4521, MigrationCode = "M1", EntityName = "Customer", FieldName = "Name", DetectedAt = DateTime.Now });
                return Task.CompletedTask;
            });

            var tree = Data(await Body(await admin.GetAsync($"/api/v1/admin/org-units/tree?companyId={company}")));
            tree.GetArrayLength().Should().Be(1);
            var br = tree[0].GetProperty("children")[0];
            br.GetProperty("code").GetString().Should().Be("T452-BR");
            br.GetProperty("employeeCount").GetInt32().Should().Be(1);
            Data(await Body(await admin.GetAsync("/api/v1/admin/branches"))).EnumerateArray().Should().Contain(b => b.GetProperty("code").GetString() == "T452-BR");

            var open = Data(await Body(await admin.GetAsync("/api/v1/admin/merge-conflicts?resolved=false")));
            open.GetProperty("items").EnumerateArray().Should().Contain(x => x.GetProperty("id").GetInt64() == 4521);
            (await admin.PostAsJsonAsync("/api/v1/admin/merge-conflicts/4521/resolve", new { resolutionNote = "" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await admin.PostAsJsonAsync("/api/v1/admin/merge-conflicts/4521/resolve", new { resolutionNote = "Đã đối chiếu" })).StatusCode.Should().Be(HttpStatusCode.OK);
            (await admin.PostAsJsonAsync("/api/v1/admin/merge-conflicts/4521/resolve", new { resolutionNote = "lần 2" })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task Meta_me_and_menu_reflect_current_user()
        {
            using var admin = _factory.CreateAuthorizedClient();
            var (g, _) = await GroupWithMenuAsync("T452-ME", "#/scm-warehouse", true);
            var userId = await CreateUserAsync(admin, "t452.me", g);
            using var user = _factory.CreateAuthorizedClient(userId);

            var me = Data(await Body(await user.GetAsync("/api/v1/meta/me")));
            var menu = Data(await Body(await user.GetAsync("/api/v1/meta/menu")));

            me.GetProperty("user").GetProperty("userName").GetString().Should().Be("t452.me");
            me.GetProperty("permissions").EnumerateArray().Select(p => p.GetProperty("metaLink").GetString()).Should().Contain("#/scm-warehouse");
            menu.EnumerateArray().Should().Contain(n => n.GetProperty("metaLink").GetString() == "#/scm-warehouse");
            menu.EnumerateArray().Should().NotContain(n => n.GetProperty("metaLink").GetString() == "#/scm-supplier");
        }
    }

    /// <summary>T45B.2 – đăng nhập v1 trên DB dev (LoginService dùng SP lấy menu) – không có DB thì Skip.</summary>
    [Collection(ApiCollection.Name)]
    public class AuthV1Tests(ApiFactory factory)
    {
        [SkippableFact]
        public async Task Login_v1_returns_token_and_wrong_password_is_401_envelope()
        {
            Skip.If(factory.SkipReason != null, factory.SkipReason);
            using var client = factory.CreateClient();

            var ok = await client.PostAsJsonAsync("/api/v1/auth/login", new { userName = Environment.GetEnvironmentVariable("DELICARE_TEST_USER") ?? "admin", password = Environment.GetEnvironmentVariable("DELICARE_TEST_PASSWORD") ?? "admin" });
            var bad = await client.PostAsJsonAsync("/api/v1/auth/login", new { userName = "admin", password = "sai-mat-khau" });

            ok.StatusCode.Should().Be(HttpStatusCode.OK);
            using (var b = JsonDocument.Parse(await ok.Content.ReadAsStringAsync()))
            {
                b.RootElement.GetProperty("data").GetProperty("accessToken").GetString().Should().StartWith("eyJ");
                b.RootElement.GetProperty("data").GetProperty("tokenType").GetString().Should().Be("Bearer");
            }
            bad.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            using (var b = JsonDocument.Parse(await bad.Content.ReadAsStringAsync()))
            {
                b.RootElement.GetProperty("errors")[0].GetProperty("code").GetString().Should().Be(ErrorCodes.Unauthorized);
            }
        }
    }
}
