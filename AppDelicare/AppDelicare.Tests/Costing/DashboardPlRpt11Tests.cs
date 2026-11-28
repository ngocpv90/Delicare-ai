using System.Net;
using System.Text.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;

namespace AppDelicare.Tests.Costing
{
    /// <summary>
    /// RPT-1.1 (D-140 a, c): tab "Kết quả kinh doanh" – Dashboard/GetPl chỉ cho mục quyền dash-pl (người chỉ có Bảng điều hành → 403); DT / NVL / %NVL khớp
    /// MealCost/GetMargin (tab Giá vốn); bao bì = phiếu xuất bao bì [XUAT_BB:; NVL theo nhóm nguyên liệu = xuất kho × giá lô (không gồm bao bì);
    /// DT theo khách × bữa + cờ chưa xác nhận; thẻ AMIS chưa có số = null.
    /// </summary>
    public class DashboardPlRpt11Tests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public DashboardPlRpt11Tests(InMemoryApiFactory factory) => _factory = factory;

        private async Task<Guid> UserAsync(string code, string screen)
        {
            Guid u = Guid.NewGuid(), g = Guid.NewGuid(), m = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.User.Add(new User { Id = u, UserName = "uat." + code.ToLowerInvariant(), IsActive = true, AccountType = "INTERNAL" });
                db.UserGroup.Add(new UserGroup { Id = g, Name = code, Code = code });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = u, UserGroupId = g });
                var menu = db.ObjectMenu.FirstOrDefault(x => x.MetaLink == "#/" + screen);
                if (menu == null)
                {
                    menu = new ObjectMenu { Id = m, Name = screen, MetaLink = "#/" + screen, LevelShow = 2, OrderShow = 1, IsActive = true };
                    db.ObjectMenu.Add(menu);
                }
                db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = g, ObjectMenuId = menu.Id, AllowAccess = true, AllowPrint = true, AllowExport = true });
                return Task.CompletedTask;
            });
            return u;
        }

        [Fact]
        public async Task Pl_tab_matches_cost_tab_groups_materials_and_is_limited_to_dash_pl()
        {
            var day = new DateTime(2035, 6, 10);
            _factory.Clock.FixedNow = new DateTime(2035, 6, 20, 9, 0, 0);
            Guid cus = Guid.NewGuid(), o = Guid.NewGuid(), l1 = Guid.NewGuid(), l2 = Guid.NewGuid(), dish = Guid.NewGuid();
            Guid catVeg = Guid.NewGuid(), catMeat = Guid.NewGuid(), veg = Guid.NewGuid(), meat = Guid.NewGuid(), box = Guid.NewGuid(), wh = Guid.NewGuid();
            Guid issue = Guid.NewGuid(), issuePrev = Guid.NewGuid(), issueBox = Guid.NewGuid(), uom = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Customer.Add(new Customer { Id = cus, Code = "K-PL", Name = "Khách PL", DisplayName = "Khách PL", CIF = "K-PL", CustomerType = "FACTORY", Status = "Active" });
                db.Order.Add(new Order { Id = o, OrderNumber = "DH-PL", CustomerId = cus, OrderDate = day.AddDays(-1), MealDate = day, DeliveryDate = day, Status = "Reconciled", TotalQuantity = 150, TotalAmount = 4_500_000 });
                db.OrderLine.AddRange(new OrderLine { Id = l1, OrderId = o, MealType = "TRUA", Quantity = 100, LineAmount = 3_000_000 }, new OrderLine { Id = l2, OrderId = o, MealType = "TOI", Quantity = 50, LineAmount = 1_500_000 });
                db.MealCost.AddRange(new MealCost { Id = Guid.NewGuid(), OrderId = o, OrderLineId = l1, DishId = dish, MealQty = 100, AcceptedMealQuantity = 100, TotalActualCost = 1_200_000, Revenue = 3_000_000, CalculatedAt = day },
                    new MealCost { Id = Guid.NewGuid(), OrderId = o, OrderLineId = l2, DishId = dish, MealQty = 50, AcceptedMealQuantity = 50, TotalActualCost = 600_000, Revenue = 1_500_000, CalculatedAt = day });
                db.IngredientCategory.AddRange(new IngredientCategory { Id = catVeg, Code = "RAU-PL", Name = "Rau củ PL" }, new IngredientCategory { Id = catMeat, Code = "THIT-PL", Name = "Thịt PL" });
                db.Ingredient.AddRange(new Ingredient { Id = veg, Code = "NL-PL-RAU", Name = "Rau", IngredientCategoryId = catVeg }, new Ingredient { Id = meat, Code = "NL-PL-THIT", Name = "Thịt", IngredientCategoryId = catMeat },
                    new Ingredient { Id = box, Code = "CC-PL-HOP", Name = "Hộp", IngredientCategoryId = catVeg });
                db.MaterialIssue.AddRange(new MaterialIssue { Id = issue, IssueNumber = "PX-PL-1", WarehouseId = wh, IssueDate = day, Status = "Issued" },
                    new MaterialIssue { Id = issuePrev, IssueNumber = "PX-PL-0", WarehouseId = wh, IssueDate = day.AddMonths(-1), Status = "Issued" },
                    new MaterialIssue { Id = issueBox, IssueNumber = "PX-PL-BB", WarehouseId = wh, IssueDate = day, Status = "Issued", Note = "[XUAT_BB:20350610:CA1:TRUA:100] bao bì" });
                InventoryTransaction Tx(Guid ing, Guid refId, DateTime at, decimal qty, decimal cost) => new()
                {
                    Id = Guid.NewGuid(), TransactionType = "ISSUE", WarehouseId = wh, IngredientId = ing, ReferenceType = "MaterialIssue", ReferenceId = refId, TransactionDate = at,
                    QuantityOut = qty, UnitCost = cost, UomId = uom, UnitId = uom,
                };
                db.InventoryTransaction.AddRange(Tx(veg, issue, day, 10, 50_000), Tx(meat, issue, day, 4, 200_000), Tx(meat, issuePrev, day.AddMonths(-1), 2, 200_000), Tx(box, issueBox, day, 100, 2_000));
                return Task.CompletedTask;
            });
            using var admin = _factory.CreateAuthorizedClient();
            var pl = (await admin.GetResultAsync("/api/Dashboard/GetPl?month=2035-06")).Ok();
            var cost = (await admin.GetResultAsync("/api/MealCost/GetMargin?Month=2035-06&GroupBy=customer")).Ok().GetProperty("summary");
            JsonElement Card(string code) => pl.GetProperty("listCard").EnumerateArray().Single(c => c.GetProperty("code").GetString() == code);
            Card("PL_EST_REVENUE").GetProperty("value").GetDecimal().Should().Be(cost.GetProperty("revenue").GetDecimal()).And.Be(4_500_000m);
            Card("PL_EST_FOOD_COST").GetProperty("value").GetDecimal().Should().Be(cost.GetProperty("cost").GetDecimal(), "NVL khớp tab Giá vốn");
            Card("PL_EST_FOOD_PCT").GetProperty("value").GetDecimal().Should().Be(cost.GetProperty("ratioPct").GetDecimal()).And.Be(40m);
            Card("PL_EST_PACKAGING").GetProperty("value").GetDecimal().Should().Be(200_000m, "chỉ phiếu xuất bao bì");
            Card("PL_AMIS_PROFIT_BEFORE_TAX").GetProperty("value").ValueKind.Should().Be(JsonValueKind.Null, "chưa nhập sổ AMIS");
            pl.GetProperty("hasAmis").GetBoolean().Should().BeFalse();

            var groups = pl.GetProperty("listGroup").EnumerateArray().ToDictionary(g => g.GetProperty("group").GetString()!, g => (g.GetProperty("value").GetDecimal(), g.GetProperty("prev").GetDecimal()));
            groups["Thịt PL"].Should().Be((800_000m, 400_000m));
            groups["Rau củ PL"].Should().Be((500_000m, 0m), "hộp bao bì không tính vào NVL dù cùng nhóm");
            pl.GetProperty("listGroup")[0].GetProperty("group").GetString().Should().Be("Thịt PL", "Pareto: lớn nhất trước");
            pl.GetProperty("listGroupMonth").EnumerateArray().Single(g => g.GetProperty("group").GetString() == "Thịt PL").GetProperty("values").EnumerateArray().TakeLast(2).Select(v => v.GetDecimal())
                .Should().Equal(400_000m, 800_000m);

            var row = pl.GetProperty("listRevenue").EnumerateArray().Single();
            row.GetProperty("byMeal").GetProperty("TRUA").GetDecimal().Should().Be(3_000_000m);
            row.GetProperty("byMeal").GetProperty("TOI").GetDecimal().Should().Be(1_500_000m);
            row.GetProperty("unconfirmed").GetBoolean().Should().BeTrue("bảng kê tháng chưa được khách xác nhận");
            pl.GetProperty("meals").EnumerateArray().Select(x => x.GetString()).Should().Equal("TRUA", "TOI");

            Guid acc = await UserAsync("FINACC-PL", "dash-pl"), exec = await UserAsync("EXEC-PL", "dash-exec");
            using (var c = _factory.CreateAuthorizedClient(acc))
            {
                (await c.GetAsync("/api/Dashboard/GetPl?month=2035-06")).StatusCode.Should().Be(HttpStatusCode.OK);
            }
            using (var c = _factory.CreateAuthorizedClient(exec))
            {
                (await c.GetAsync("/api/Dashboard/GetPl?month=2035-06")).StatusCode.Should().Be(HttpStatusCode.Forbidden, "Bảng điều hành không kèm Kết quả kinh doanh");
            }
        }
    }
}
