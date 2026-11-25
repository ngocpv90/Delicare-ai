using System.Net.Http.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Admin
{
    /// <summary>
    /// UI-6.41 (D-122): Move tạo vòng lặp → lỗi; chuyển nhánh sang công ty khác → cả nhánh đổi CompanyId; ngừng hoạt động đơn vị còn người → lỗi nêu số người;
    /// SetManager nhân viên đã nghỉ → lỗi; xóa công ty → lỗi; GetOrgTree gốc là công ty; Insert lấy công ty theo cha + loại theo danh mục.
    /// </summary>
    public class OrgUnitUi641Tests(InMemoryApiFactory factory) : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory = factory;

        private async Task<(Guid C1, Guid C2, Guid Root, Guid Child, Guid Grand, Guid Other)> SeedAsync(string k)
        {
            Guid c1 = Guid.NewGuid(), c2 = Guid.NewGuid(), root = Guid.NewGuid(), child = Guid.NewGuid(), grand = Guid.NewGuid(), other = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Company.Add(new Company { Id = c1, Code = "C1" + k, Name = "Công ty 1 " + k, Status = "Active" });
                db.Company.Add(new Company { Id = c2, Code = "C2" + k, Name = "Công ty 2 " + k, Status = "Active" });
                db.OrganizationUnit.Add(new OrganizationUnit { Id = root, CompanyId = c1, Code = "R" + k, Name = "Bếp " + k, UnitType = "BRANCH", Status = "Active" });
                db.OrganizationUnit.Add(new OrganizationUnit { Id = child, CompanyId = c1, ParentId = root, Code = "CH" + k, Name = "Tổ " + k, UnitType = "TEAM", Status = "Active" });
                db.OrganizationUnit.Add(new OrganizationUnit { Id = grand, CompanyId = c1, ParentId = child, Code = "G" + k, Name = "Nhóm " + k, UnitType = "TEAM", Status = "Active" });
                db.OrganizationUnit.Add(new OrganizationUnit { Id = other, CompanyId = c2, Code = "O" + k, Name = "Bếp khác " + k, UnitType = "BRANCH", Status = "Active" });
                db.Employee.Add(new Employee { Id = Guid.NewGuid(), Code = "E" + k, FullName = "NV " + k, OrganizationUnitId = grand, Status = "Active" });
                return Task.CompletedTask;
            });
            return (c1, c2, root, child, grand, other);
        }

        private static async Task<string> Post(HttpClient c, string url, object body) => await (await c.PostAsJsonAsync(url, body)).Content.ReadAsStringAsync();

        [Fact]
        public async Task Move_checks_cycle_and_changes_company_for_whole_branch()
        {
            var w = await SeedAsync("641A");
            using var c = _factory.CreateAuthorizedClient();
            (await Post(c, "/api/OrganizationUnit/Move", new { id = w.Root, newParentId = w.Grand })).Should().Contain("đơn vị con của nó");
            (await Post(c, "/api/OrganizationUnit/Move", new { id = w.Child, newParentId = w.Other })).Should().Contain("\"responseCode\":200");
            var companies = await _factory.QueryAsync(db => db.OrganizationUnit.AsNoTracking().Where(o => o.Id == w.Child || o.Id == w.Grand).Select(o => o.CompanyId).ToListAsync());
            companies.Should().OnlyContain(x => x == w.C2, "cả nhánh đổi công ty");
            (await _factory.QueryAsync(db => db.OrganizationUnit.AsNoTracking().Where(o => o.Id == w.Child).Select(o => o.ParentId).SingleAsync())).Should().Be(w.Other);
        }

        [Fact]
        public async Task Deactivate_blocks_when_people_and_manager_must_be_working_and_company_cannot_be_deleted()
        {
            var w = await SeedAsync("641B");
            Guid left = Guid.NewGuid();
            await _factory.SeedAsync(db => { db.Employee.Add(new Employee { Id = left, Code = "L641B", FullName = "Đã nghỉ", Status = "Resigned" }); return Task.CompletedTask; });
            using var c = _factory.CreateAuthorizedClient();
            (await Post(c, "/api/OrganizationUnit/SetStatus", new { id = w.Grand, status = "Inactive" })).Should().Contain("còn 1 nhân viên");
            (await Post(c, "/api/OrganizationUnit/SetStatus", new { id = w.Child, status = "Inactive" })).Should().Contain("1 đơn vị con");
            (await Post(c, "/api/OrganizationUnit/SetManager", new { id = w.Root, employeeId = left })).Should().Contain("đang làm việc");
            (await c.DeleteResultAsync("/api/Company/Delete?id=" + w.C2)).Fail("Không xóa công ty");
            (await c.PostFormAsync("/api/OrganizationUnit/Insert", ("Code", "N641B"), ("Name", "Tổ mới"), ("UnitType", "XYZ"), ("ParentId", w.Other))).Fail("Loại đơn vị");
            (await c.PostFormAsync("/api/OrganizationUnit/Insert", ("Code", "N641B"), ("Name", "Tổ mới"), ("UnitType", "TEAM"), ("ParentId", w.Other), ("CompanyId", w.C1))).Ok();
            (await _factory.QueryAsync(db => db.OrganizationUnit.AsNoTracking().Where(o => o.Code == "N641B").Select(o => o.CompanyId).SingleAsync())).Should().Be(w.C2, "công ty lấy theo đơn vị cha");
        }

        [Fact]
        public async Task Org_tree_has_company_roots_with_totals_and_no_manager_flag()
        {
            var w = await SeedAsync("641C");
            using var c = _factory.CreateAuthorizedClient();
            var roots = (await c.GetResultAsync("/api/OrganizationUnit/GetOrgTree?companyId=" + w.C1)).Ok().EnumerateArray().ToList();
            roots.Should().ContainSingle();
            roots[0].GetProperty("isCompany").GetBoolean().Should().BeTrue();
            var branch = roots[0].GetProperty("children")[0];
            branch.GetProperty("unitTypeName").GetString().Should().Be("Chi nhánh");
            branch.GetProperty("totalEmployees").GetInt32().Should().Be(1, "gồm nhân viên đơn vị cháu");
            branch.GetProperty("noManager").GetBoolean().Should().BeTrue();
            (await c.GetResultAsync("/api/OrganizationUnit/GetSummary")).Ok().GetProperty("noManager").GetInt32().Should().BeGreaterThan(0);
        }
    }
}
