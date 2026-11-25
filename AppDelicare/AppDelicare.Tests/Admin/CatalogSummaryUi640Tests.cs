using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Admin
{
    /// <summary>
    /// UI-6.40 (D-121): Common/GetCatalogSummary – quản trị thấy đủ 10 danh mục đúng thứ tự; người chỉ có quyền Xem 1 danh mục thấy đúng 1 thẻ;
    /// số bản ghi / cảnh báo khớp dữ liệu (đơn vị chưa có trưởng, xe chưa có tải trọng, nhóm món chưa gán dây chuyền).
    /// </summary>
    public class CatalogSummaryUi640Tests(InMemoryApiFactory factory) : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory = factory;

        private async Task<Guid> UserWithAsync(string name, string link)
        {
            Guid user = Guid.NewGuid(), group = Guid.NewGuid();
            await _factory.SeedAsync(async db =>
            {
                db.User.Add(new User { Id = user, UserName = name, IsActive = true });
                db.UserGroup.Add(new UserGroup { Id = group, Name = "G-" + name });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = user, UserGroupId = group });
                var menu = await db.ObjectMenu.FirstOrDefaultAsync(m => m.MetaLink == link);
                if (menu == null)
                {
                    menu = new ObjectMenu { Id = Guid.NewGuid(), Name = link, MetaLink = link, IsActive = true, LevelShow = 2 };
                    db.ObjectMenu.Add(menu);
                }
                db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = group, ObjectMenuId = menu.Id, AllowAccess = true });
            });
            return user;
        }

        /// <summary>UI-6.49 (9) (D-127 g): Excel "Danh sách cần bổ sung" – sheet Tổng hợp + sheet theo quyền xem (không sửa dữ liệu).</summary>
        [Fact]
        public async Task Ui649_data_gaps_excel_lists_missing_data_by_view_right()
        {
            await _factory.SeedAsync(db =>
            {
                db.Employee.Add(new Employee { Id = Guid.NewGuid(), Code = "GAP649", EmployeeCode = "GAP649", FullName = "NV thiếu đơn vị 649", Status = "Active" });
                db.UserGroup.Add(new UserGroup { Id = Guid.NewGuid(), Name = "Nhóm lạ 649", Code = "LA-649" });
                return Task.CompletedTask;
            });
            using var admin = _factory.CreateAuthorizedClient();
            var res = await admin.GetAsync("/api/Common/ExportDataGaps");
            res.Content.Headers.ContentType!.MediaType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            using (var wb = new ClosedXML.Excel.XLWorkbook(await res.Content.ReadAsStreamAsync()))
            {
                wb.Worksheets.First().Name.Should().Be("Tổng hợp");
                wb.Worksheet("Nhân viên chưa gắn đơn vị").RangeUsed()!.RowsUsed().Select(r => r.Cell(1).GetString()).Should().Contain("GAP649");
                wb.Worksheet("Nhóm quyền ngoài chuẩn").RangeUsed()!.RowsUsed().Select(r => r.Cell(1).GetString()).Should().Contain("LA-649");
            }
            // Người chỉ xem Nhân viên: không có sheet nhóm quyền.
            using var hr = _factory.CreateAuthorizedClient(await UserWithAsync("gap649.hr", "#/adm-employee"));
            using (var wb = new ClosedXML.Excel.XLWorkbook(await (await hr.GetAsync("/api/Common/ExportDataGaps")).Content.ReadAsStreamAsync()))
            {
                wb.Worksheets.Select(w => w.Name).Should().Contain("Nhân viên chưa gắn đơn vị").And.NotContain("Nhóm quyền ngoài chuẩn");
            }
            (await _factory.QueryAsync(db => db.Employee.CountAsync(e => e.Code == "GAP649" && e.OrganizationUnitId == null))).Should().Be(1, "không sửa hộ dữ liệu");
        }

        [Fact]
        public async Task Summary_is_filtered_by_view_right_and_counts_match_data()
        {
            await _factory.SeedAsync(db =>
            {
                Guid co = Guid.NewGuid();
                db.Company.Add(new Company { Id = co, Code = "C640", Name = "Công ty 640", Status = "Active" });
                db.OrganizationUnit.Add(new OrganizationUnit { Id = Guid.NewGuid(), CompanyId = co, Code = "U640A", Name = "Bếp 640", UnitType = "BRANCH", Status = "Active" });
                db.Vehicle.Add(new Vehicle { Id = Guid.NewGuid(), Code = "XE640", LicensePlate = "51C-640", Status = "Active" });
                db.DishCategory.Add(new DishCategory { Id = Guid.NewGuid(), Code = "NM640", Name = "Nhóm món 640" });
                return Task.CompletedTask;
            });
            using var admin = _factory.CreateAuthorizedClient();
            var cards = (await admin.GetResultAsync("/api/Common/GetCatalogSummary")).ListData.EnumerateArray().ToList();
            cards.Select(c => c.GetProperty("screen").GetString()).Should().Equal("adm-orgunit", "adm-position", "hr-shift", "scm-uom", "scm-warehouse", "prod-line", "log-vehicle", "adm-reference", "adm-admin-unit", "kpi-definition");
            int noMgr = await _factory.QueryAsync(db => db.OrganizationUnit.CountAsync(o => !o.IsDeleted && o.Status != "Inactive" && o.ManagerId == null));
            cards[0].GetProperty("listWarning").EnumerateArray().First(w => w.GetProperty("code").GetString() == "NO_MANAGER").GetProperty("count").GetInt32().Should().Be(noMgr);
            cards.First(c => c.GetProperty("screen").GetString() == "log-vehicle").GetProperty("listWarning").EnumerateArray().Should().Contain(w => w.GetProperty("code").GetString() == "NO_CAPACITY");
            cards.First(c => c.GetProperty("screen").GetString() == "prod-line").GetProperty("listWarning").EnumerateArray().Should().Contain(w => w.GetProperty("code").GetString() == "CAT_NO_LINE");
            int vehicles = await _factory.QueryAsync(db => db.Vehicle.CountAsync(v => !v.IsDeleted && (v.Status == null || v.Status == "Active")));
            cards.First(c => c.GetProperty("screen").GetString() == "log-vehicle").GetProperty("count").GetInt32().Should().Be(vehicles);

            using var one = _factory.CreateAuthorizedClient(await UserWithAsync("u640-veh", "#/log-vehicle"));
            var mine = (await one.GetResultAsync("/api/Common/GetCatalogSummary")).ListData.EnumerateArray().Select(c => c.GetProperty("screen").GetString()).ToList();
            mine.Should().Equal("log-vehicle");
        }
    }
}
