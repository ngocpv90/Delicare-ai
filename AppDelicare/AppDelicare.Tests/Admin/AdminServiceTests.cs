using System.Net;
using System.Text;
using System.Text.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Admin
{
    /// <summary>
    /// T45R (chuyển từ T45B.2 v1) – quản trị P0 trên service / controller cũ: ma trận quyền (UserRole), nhóm mới không có quyền,
    /// menu (cây, sắp xếp, MetaLink duy nhất), cây đơn vị, xung đột dữ liệu, thông tin người đang đăng nhập.
    /// </summary>
    public class AdminServiceTests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public AdminServiceTests(InMemoryApiFactory factory) => _factory = factory;

        private async Task<(Guid Group, Guid Parent, Guid MenuA, Guid MenuB)> SeedMenusAsync(string tag)
        {
            Guid group = Guid.NewGuid(), parent = Guid.NewGuid(), a = Guid.NewGuid(), b = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.UserGroup.Add(new UserGroup { Id = group, Name = "NHOM-" + tag });
                db.ObjectMenu.AddRange(
                    new ObjectMenu { Id = parent, Name = "Nhóm " + tag, MetaLink = $"#/group/{tag}", LevelShow = 1, OrderShow = 1, IsActive = true },
                    new ObjectMenu { Id = a, Name = "Màn A " + tag, MetaLink = $"#/{tag}-a", ParentId = parent, LevelShow = 2, OrderShow = 1, IsActive = true },
                    new ObjectMenu { Id = b, Name = "Màn B " + tag, MetaLink = $"#/{tag}-b", ParentId = parent, LevelShow = 2, OrderShow = 2, IsActive = true });
                // Chỉ menu A có dòng UserRole (menu B là menu seed sau, chưa có dòng).
                db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = group, ObjectMenuId = a, AllowAccess = true });
                return Task.CompletedTask;
            });
            return (group, parent, a, b);
        }

        private static JsonElement FindMenu(JsonElement tree, Guid objectMenuId)
        {
            foreach (var node in tree.EnumerateArray())
            {
                if (node.GetProperty("objectMenuId").GetGuid() == objectMenuId)
                {
                    return node;
                }
                if (node.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
                {
                    var found = FindMenu(children, objectMenuId);
                    if (found.ValueKind != JsonValueKind.Undefined)
                    {
                        return found;
                    }
                }
            }
            return default;
        }

        [Fact]
        public async Task Matrix_shows_menus_without_rows_and_update_creates_them_with_normalized_flags()
        {
            var (group, _, menuA, menuB) = await SeedMenusAsync("mtx");
            using var c = _factory.CreateAuthorizedClient();

            var matrix = (await c.GetResultAsync($"/api/UserRole/GetMatrix?userGroupId={group}")).Ok().GetProperty("listMenu");
            var b = FindMenu(matrix, menuB);
            b.GetProperty("id").GetGuid().Should().Be(Guid.Empty, "menu chưa có dòng UserRole");
            b.GetProperty("allowAccess").GetBoolean().Should().BeFalse();
            FindMenu(matrix, menuA).GetProperty("allowAccess").GetBoolean().Should().BeTrue();

            // Bật AllowEdit mà quên AllowAccess → tự bật AllowAccess (spec 3.3).
            string json = JsonSerializer.Serialize(new[] { new { Id = Guid.Empty, UserGroupId = group, ObjectMenuId = menuB, AllowEdit = true } });
            (await c.PutFormAsync("/api/UserRole/Update", ("ListPermission", json))).Ok();

            var row = await _factory.QueryAsync(db => db.UserRole.AsNoTracking().SingleAsync(r => r.UserGroupId == group && r.ObjectMenuId == menuB && !r.IsDeleted));
            row.AllowAccess.Should().BeTrue();
            row.AllowEdit.Should().BeTrue();
        }

        [Fact]
        public async Task Access_all_copy_preview_and_effective_permissions()
        {
            var (group, _, menuA, menuB) = await SeedMenusAsync("bulk");
            Guid target = Guid.NewGuid(), userId = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.UserGroup.Add(new UserGroup { Id = target, Name = "NHOM-bulk-dich" });
                db.User.Add(new User { Id = userId, UserName = "bulk-user", IsActive = true });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = userId, UserGroupId = target });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();

            (await c.PostFormAsync("/api/UserRole/SaveAccessAll", ("UserGroupId", group), ("AllowAccess", true))).Ok().GetInt32().Should().BeGreaterThan(0);
            (await _factory.QueryAsync(db => db.UserRole.AnyAsync(r => r.UserGroupId == group && r.ObjectMenuId == menuB && r.AllowAccess && !r.IsDeleted))).Should().BeTrue();

            (await c.PostFormAsync("/api/UserRole/CopyFromUserGroup", ("SourceUserGroupId", group), ("TargetUserGroupId", target))).Ok();
            var preview = (await c.GetResultAsync($"/api/UserRole/GetPreview?userGroupId={target}")).Ok();
            preview.GetProperty("screenCount").GetInt32().Should().BeGreaterThanOrEqualTo(2);
            preview.GetProperty("listUser")[0].GetProperty("userName").GetString().Should().Be("bulk-user");

            var effective = (await c.GetResultAsync($"/api/UserRole/GetByUserId?userId={userId}")).Ok().GetProperty("listMenu");
            FindMenu(effective, menuA).ValueKind.Should().NotBe(JsonValueKind.Undefined);

            (await c.PostFormAsync("/api/UserRole/SaveAccessAll", ("UserGroupId", group), ("AllowAccess", false))).Ok();
            (await _factory.QueryAsync(db => db.UserRole.AnyAsync(r => r.UserGroupId == group && r.AllowAccess && !r.IsDeleted))).Should().BeFalse();
        }

        [Fact]
        public async Task New_user_group_starts_without_any_rights()
        {
            await SeedMenusAsync("newgrp");
            using var c = _factory.CreateAuthorizedClient();

            (await c.PostFormAsync("/api/UserGroup/Insert", ("Name", "NHOM-MOI-T45R"))).Ok();

            var groupId = await _factory.QueryAsync(db => db.UserGroup.Where(g => g.Name == "NHOM-MOI-T45R").Select(g => g.Id).SingleAsync());
            var rows = await _factory.QueryAsync(db => db.UserRole.AsNoTracking().Where(r => r.UserGroupId == groupId && !r.IsDeleted).ToListAsync());
            rows.Should().NotBeEmpty();
            rows.Should().OnlyContain(r => !r.AllowAccess && !r.AllowAdd && !r.AllowEdit && !r.AllowDelete && !r.AllowPrint && !r.AllowExport && !r.AllowImport);
        }

        [Fact]
        public async Task Object_menu_tree_reorder_blocks_cycles_and_meta_link_is_unique()
        {
            var (_, parent, menuA, menuB) = await SeedMenusAsync("tree");
            using var c = _factory.CreateAuthorizedClient();

            var tree = (await c.GetResultAsync("/api/ObjectMenu/GetTree?includeInactive=false")).Ok();
            tree.EnumerateArray().Single(n => n.GetProperty("id").GetGuid() == parent).GetProperty("children").GetArrayLength().Should().Be(2);

            string Order(params object[] items) => JsonSerializer.Serialize(items);
            var cycle = await c.PutAsync("/api/ObjectMenu/UpdateOrder", new StringContent(Order(new { id = parent, parentId = menuA, orderShow = 1 }), Encoding.UTF8, "application/json"));
            (await LegacyResult.ReadAsync(cycle)).Fail("vòng lặp");
            var ok = await c.PutAsync("/api/ObjectMenu/UpdateOrder", new StringContent(Order(new { id = menuB, parentId = (Guid?)null, orderShow = 9 }), Encoding.UTF8, "application/json"));
            (await LegacyResult.ReadAsync(ok)).Ok();
            var moved = await _factory.QueryAsync(db => db.ObjectMenu.AsNoTracking().SingleAsync(m => m.Id == menuB));
            moved.ParentId.Should().BeNull();
            moved.LevelShow.Should().Be(1);

            (await c.PostFormAsync("/api/ObjectMenu/Insert", ("Name", "Trùng"), ("MetaLink", "#/tree-a"), ("LevelShow", 2))).Fail("#/tree-a");
        }

        [Fact]
        public async Task Org_tree_branches_and_parent_cycle()
        {
            Guid company = Guid.NewGuid(), root = Guid.NewGuid(), branch = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.OrganizationUnit.AddRange(
                    new OrganizationUnit { Id = root, CompanyId = company, Code = "T45R-DLC", Name = "Công ty", UnitType = "COMPANY" },
                    new OrganizationUnit { Id = branch, CompanyId = company, Code = "T45R-BR1", Name = "Bếp 1", UnitType = "BRANCH", ParentId = root });
                db.Employee.Add(new Employee { Id = Guid.NewGuid(), OrganizationUnitId = branch, FullName = "NV 1" });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();

            var tree = (await c.GetResultAsync($"/api/OrganizationUnit/GetTree?companyId={company}")).Ok();
            var child = tree[0].GetProperty("children")[0];
            child.GetProperty("code").GetString().Should().Be("T45R-BR1");
            child.GetProperty("employeeCount").GetInt32().Should().Be(1);
            (await c.GetResultAsync("/api/OrganizationUnit/GetBranches")).ListData.EnumerateArray()
                .Should().Contain(b => b.GetProperty("code").GetString() == "T45R-BR1");

            (await c.PutFormAsync("/api/OrganizationUnit/Update", ("Id", root), ("CompanyId", company), ("Code", "T45R-DLC"), ("Name", "Công ty"), ("ParentId", branch)))
                .Fail("Đơn vị cha");
        }

        [Fact]
        public async Task Merge_conflict_resolve_requires_note_and_only_once()
        {
            long id = 0;
            await _factory.SeedAsync(async db =>
            {
                var x = new CoreDataMergeConflict { Id = 4242, MigrationCode = "MIG-1", EntityName = "Customer", FieldName = "Name", LegacyValue = "A", CanonicalValue = "B", DetectedAt = DateTime.Now };
                db.CoreDataMergeConflict.Add(x);
                await Task.CompletedTask;
                id = x.Id;
            });
            using var c = _factory.CreateAuthorizedClient();

            (await c.PostFormAsync("/api/CoreDataMergeConflict/Resolve", ("Id", id), ("ResolutionNote", " "))).Fail("Ghi chú");
            (await c.PostFormAsync("/api/CoreDataMergeConflict/Resolve", ("Id", id), ("ResolutionNote", "Giữ giá trị chuẩn"))).Ok();
            (await c.PostFormAsync("/api/CoreDataMergeConflict/Resolve", ("Id", id), ("ResolutionNote", "Lần 2"))).Fail("đã được xử lý");
            (await c.GetResultAsync($"/api/CoreDataMergeConflict/GetById?id={id}")).Ok().GetProperty("resolvedAt").ValueKind.Should().NotBe(JsonValueKind.Null);
        }

        [Fact]
        public async Task Current_user_needs_only_login_and_returns_menu()
        {
            var (group, _, menuA, _) = await SeedMenusAsync("me");
            Guid userId = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.User.Add(new User { Id = userId, UserName = "me-user", IsActive = true });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = userId, UserGroupId = group });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient(userId);

            var response = await c.GetAsync("/api/User/GetCurrent");

            response.StatusCode.Should().Be(HttpStatusCode.OK, "GetCurrent chỉ cần đăng nhập");
            var me = (await LegacyResult.ReadAsync(response)).Ok();
            me.GetProperty("user").GetProperty("userName").GetString().Should().Be("me-user");
            me.GetProperty("user").TryGetProperty("passWord", out _).Should().BeFalse("không bao giờ trả mật khẩu");
            FindMenu(me.GetProperty("listMenu"), menuA).ValueKind.Should().NotBe(JsonValueKind.Undefined);
        }
    }
}
