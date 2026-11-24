using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace AppDelicare.Tests.DetailPages
{
    /// <summary>
    /// UI-6 (D-80): trang chi tiết toàn màn – mẫu test cho mọi …/GetOverview: có dữ liệu, id sai (Fail "không tìm thấy"), dữ liệu rỗng (phiếu không dòng),
    /// quyền theo màn gốc (không có quyền xem scm-stockcount → 403, có quyền xem → 200).
    /// </summary>
    public class StockCountOverviewTests(InMemoryApiFactory factory) : IClassFixture<InMemoryApiFactory>
    {
        private async Task<Guid> UserAsync(params string[] viewLinks)
        {
            Guid userId = Guid.NewGuid(), group = Guid.NewGuid();
            await factory.SeedAsync(async db =>
            {
                db.User.Add(new User { Id = userId, UserName = "dp-" + userId.ToString("N")[..6], IsActive = true });
                db.UserGroup.Add(new UserGroup { Id = group, Name = "DP-" + group.ToString("N")[..6] });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = userId, UserGroupId = group });
                foreach (string link in viewLinks)
                {
                    var menu = await db.ObjectMenu.FirstOrDefaultAsync(m => m.MetaLink == link) ?? db.ObjectMenu.Local.FirstOrDefault(m => m.MetaLink == link);
                    if (menu == null)
                    {
                        menu = new ObjectMenu { Id = Guid.NewGuid(), Name = link, MetaLink = link, IsActive = true, LevelShow = 2 };
                        db.ObjectMenu.Add(menu);
                    }
                    db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = group, ObjectMenuId = menu.Id, AllowAccess = true });
                }
            });
            return userId;
        }

        [Fact]
        public async Task Overview_returns_detail_counts_and_checks_permission_of_the_list_screen()
        {
            Guid wh = Guid.NewGuid(), kg = Guid.NewGuid(), ing = Guid.NewGuid(), sc = Guid.NewGuid(), empty = Guid.NewGuid(), emp = Guid.NewGuid();
            await factory.SeedAsync(db =>
            {
                db.Warehouse.Add(new Warehouse { Id = wh, Code = "KHO-DP1", Name = "Kho DP1" });
                db.UnitMeasure.Add(new UnitMeasure { Id = kg, Code = "KG-DP1", Name = "kg" });
                db.Ingredient.Add(new Ingredient { Id = ing, Code = "NL-DP1", Name = "Thịt DP1", BaseUnitId = kg });
                db.Employee.Add(new Employee { Id = emp, Code = "NV-DP1", FullName = "Thủ kho DP1" });
                db.StockCount.AddRange(
                    new StockCount { Id = sc, CountNumber = "KK-DP1", WarehouseId = wh, CountDate = new DateTime(2026, 11, 20), Status = "Counting", ResponsibleEmployeeId = emp },
                    new StockCount { Id = empty, CountNumber = "KK-DP1-RONG", WarehouseId = wh, CountDate = new DateTime(2026, 11, 20), Status = "Draft" });
                db.StockCountLine.AddRange(
                    new StockCountLine { Id = Guid.NewGuid(), StockCountId = sc, IngredientId = ing, UnitId = kg, SystemQuantity = 100, PhysicalQuantity = 90, VarianceQuantity = -10 },
                    new StockCountLine { Id = Guid.NewGuid(), StockCountId = sc, IngredientId = ing, UnitId = kg, SystemQuantity = 50, PhysicalQuantity = 50, VarianceQuantity = 0 });
                return Task.CompletedTask;
            });
            using var admin = factory.CreateAuthorizedClient();
            var o = (await admin.GetResultAsync($"/api/StockCount/GetOverview?id={sc}")).Ok();
            o.GetProperty("detail").GetProperty("stockCount").GetProperty("countNumber").GetString().Should().Be("KK-DP1");
            o.GetProperty("lineCount").GetInt32().Should().Be(2);
            o.GetProperty("varianceLineCount").GetInt32().Should().Be(1);
            o.GetProperty("responsibleName").GetString().Should().Be("Thủ kho DP1");
            o.GetProperty("approvalRequestId").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null, "chưa gửi duyệt");

            var e = (await admin.GetResultAsync($"/api/StockCount/GetOverview?id={empty}")).Ok();
            e.GetProperty("lineCount").GetInt32().Should().Be(0, "phiếu không dòng vẫn mở được trang");
            (await admin.GetResultAsync($"/api/StockCount/GetOverview?id={Guid.NewGuid()}")).Fail("Không tìm thấy");

            using var viewer = factory.CreateAuthorizedClient(await UserAsync("#/scm-stockcount"));
            (await viewer.GetAsync($"/api/StockCount/GetOverview?id={sc}")).StatusCode.Should().Be(HttpStatusCode.OK);
            using var other = factory.CreateAuthorizedClient(await UserAsync("#/scm-po"));
            (await other.GetAsync($"/api/StockCount/GetOverview?id={sc}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }
}
