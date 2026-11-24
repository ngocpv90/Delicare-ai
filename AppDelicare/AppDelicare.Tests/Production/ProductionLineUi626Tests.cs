using System.Net.Http.Json;
using System.Text.Json;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Services.Implement;
using AppDelicare.Services.Interface;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Production
{
    /// <summary>
    /// UI-6.26 (D-107, GAP-PRD-10): dây chuyền theo nhóm món – ra lệnh (tự lập sau chốt → Release) chia mẻ đúng dây chuyền (số mẻ = ceil(suất ÷ công suất), giờ theo phút / mẻ),
    /// nhóm con kế thừa nhóm cha, nhóm chưa gán / dây chuyền ngừng → 1 mẻ + cảnh báo; bổ sung đơn chốt muộn cùng luật; Gán nhanh, form dây chuyền kèm nhóm, đổi tên nhóm giữ dây chuyền.
    /// </summary>
    public class ProductionLineUi626Tests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public ProductionLineUi626Tests(InMemoryApiFactory factory) => _factory = factory;

        private static int MenuDay(DateTime d) => d.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)d.DayOfWeek;

        private sealed record Seed(Guid Order, Guid Menu, Guid Shift, Guid Customer, Guid LineA, Guid LineB, Guid LineOff, Guid DishA, Guid DishB, Guid DishC, Guid DishD, Guid CatA, Guid CatParent, Guid CatChild, Guid CatNone);

        private async Task<Seed> SeedAsync(string code, DateTime day)
        {
            Guid shift = Guid.NewGuid(), menu = Guid.NewGuid(), customer = Guid.NewGuid(), order = Guid.NewGuid(), kg = Guid.NewGuid(), ing = Guid.NewGuid();
            Guid lineA = Guid.NewGuid(), lineB = Guid.NewGuid(), lineOff = Guid.NewGuid();
            Guid catA = Guid.NewGuid(), catParent = Guid.NewGuid(), catChild = Guid.NewGuid(), catNone = Guid.NewGuid(), catOff = Guid.NewGuid();
            Guid dishA = Guid.NewGuid(), dishB = Guid.NewGuid(), dishC = Guid.NewGuid(), dishD = Guid.NewGuid();
            await _factory.SeedAsync(async db =>
            {
                db.Shift.Add(new Shift { Id = shift, Code = "T" + code, Name = "Trưa " + code, StartTime = new TimeSpan(10, 30, 0), Status = "Active" });
                db.ProductionLine.AddRange(new ProductionLine { Id = lineA, Code = "LA" + code, Name = "Bếp mặn " + code, CapacityMealPerBatch = 40, MinutesPerBatch = 30, Status = "Active" },
                    new ProductionLine { Id = lineB, Code = "LB" + code, Name = "Rau canh " + code, CapacityMealPerBatch = 100, MinutesPerBatch = 45, Status = "Active" },
                    new ProductionLine { Id = lineOff, Code = "LO" + code, Name = "Bếp cũ " + code, CapacityMealPerBatch = 10, MinutesPerBatch = 20, Status = "Inactive" });
                db.DishCategory.AddRange(new DishCategory { Id = catA, Code = "CA" + code, Name = "Mặn " + code, ProductionLineId = lineA },
                    new DishCategory { Id = catParent, Code = "CP" + code, Name = "Rau cha " + code, ProductionLineId = lineB },
                    new DishCategory { Id = catChild, Code = "CC" + code, Name = "Rau con " + code, ParentId = catParent },
                    new DishCategory { Id = catNone, Code = "CN" + code, Name = "Tráng miệng " + code },
                    new DishCategory { Id = catOff, Code = "CO" + code, Name = "Cũ " + code, ProductionLineId = lineOff });
                db.Dish.AddRange(new Dish { Id = dishA, Code = "MA" + code, Name = "Thịt " + code, Status = "Active", DishCategoryId = catA },
                    new Dish { Id = dishB, Code = "MB" + code, Name = "Rau " + code, Status = "Active", DishCategoryId = catChild },
                    new Dish { Id = dishC, Code = "MC" + code, Name = "Chè " + code, Status = "Active", DishCategoryId = catNone },
                    new Dish { Id = dishD, Code = "MD" + code, Name = "Món cũ " + code, Status = "Active", DishCategoryId = catOff });
                db.Menu.Add(new Model.Entity.Menu { Id = menu, Code = "TD" + code, Name = "TĐ " + code, VersionNo = 1, Status = "Published" });
                int seq = 0;
                foreach (var d in new[] { dishA, dishB, dishC, dishD })
                {
                    db.MenuLine.Add(new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = d, MealType = "TRUA", DayOfWeek = MenuDay(day), SequenceNo = 1, SortOrder = ++seq });
                }
                db.UnitMeasure.Add(new UnitMeasure { Id = kg, Code = "KG" + code, Name = "kg" });
                db.Ingredient.Add(new Ingredient { Id = ing, Code = "NL" + code, Name = "NL " + code, BaseUnitId = kg });
                foreach (var d in new[] { dishA, dishB, dishC, dishD })
                {
                    db.Recipe.Add(new Recipe { Id = Guid.NewGuid(), DishId = d, VersionNo = 1, Status = "Locked", IsActive = true, StandardOutputQty = 100, EffectiveFrom = day.AddDays(-30),
                        LockedAt = day.AddDays(-30), SnapshotJson = JsonSerializer.Serialize(new[] { new { IngredientId = ing, Quantity = 1m, UnitId = kg, SequenceNo = 1 } }) });
                }
                Guid warehouse = Guid.NewGuid(), lot = Guid.NewGuid();
                db.Warehouse.Add(new Warehouse { Id = warehouse, Code = "W" + code, Name = "Kho " + code, Status = "Active" });
                db.MaterialLot.Add(new MaterialLot { Id = lot, IngredientId = ing, LotNumber = "L" + code, LotNo = "L" + code, ExpiryDate = day.AddDays(30), ReceivedDate = day.AddDays(-2), QcStatus = "DAT", QualityStatus = "DAT", Status = "Available" });
                await new InventoryLedgerService(db).PostAsync(new InventoryPostRequest { TransactionType = "RECEIPT", TransactionNumber = "NK" + code, WarehouseId = warehouse, IngredientId = ing, MaterialLotId = lot, UnitId = kg, QuantityIn = 100 });
                db.Customer.Add(new Customer { Id = customer, Code = "K" + code, Name = "Khách " + code, DisplayName = "Khách " + code, CIF = "K" + code, CustomerType = "FACTORY", Status = "Active" });
                db.Order.Add(new Order { Id = order, OrderNumber = "DH-" + code, CustomerId = customer, OrderDate = day.AddDays(-3), MealDate = day, DeliveryDate = day, Status = "Confirmed", TotalQuantity = 100 });
                db.OrderLine.Add(new OrderLine { Id = Guid.NewGuid(), OrderId = order, MenuId = menu, MealType = "TRUA", ShiftId = shift, Quantity = 100, LineAmount = 0 });
            });
            return new Seed(order, menu, shift, customer, lineA, lineB, lineOff, dishA, dishB, dishC, dishD, catA, catParent, catChild, catNone);
        }

        [Fact]
        public async Task Release_splits_batches_by_dish_category_line_and_supplement_follows()
        {
            var day = new DateTime(2032, 9, 7);
            _factory.Clock.FixedNow = day.AddDays(-1).AddHours(20).AddMinutes(5);
            var s = await SeedAsync("L1", day);
            using var c = _factory.CreateAuthorizedClient();
            var auto = (await c.PostFormAsync("/api/Order/LockByMealDate", ("MealDate", day))).Ok().GetProperty("autoPlan");
            auto.GetProperty("status").GetString().Should().Be("RELEASED", string.Join("; ", auto.GetProperty("listReason").EnumerateArray().Select(x => x.GetString())));
            var warnings = auto.GetProperty("listWarning").EnumerateArray().Select(x => x.GetString()).ToList();
            warnings.Should().Contain(w => w!.Contains("Chè L1") && w.Contains("chưa gán dây chuyền – nấu 1 mẻ"));
            warnings.Should().Contain(w => w!.Contains("đã ngừng hoạt động") && w.Contains("Món cũ L1"));

            var orders = await _factory.QueryAsync(db => db.ProductionOrder.AsNoTracking().Where(o => db.ProductionPlan.Any(p => p.Id == o.ProductionPlanId && p.ProductionDate == day)).ToListAsync());
            var batches = await _factory.QueryAsync(db => db.ProductionBatch.AsNoTracking().Where(b => orders.Select(o => o.Id).Contains(b.ProductionOrderId)).ToListAsync());
            var a = orders.Single(o => o.DishId == s.DishA);
            a.ProductionLineId.Should().Be(s.LineA);
            var aBatches = batches.Where(b => b.ProductionOrderId == a.Id).ToList();
            aBatches.Should().HaveCount(3, "100 suất ÷ 40 suất / mẻ");
            aBatches.Should().OnlyContain(b => (b.EndDate!.Value - b.StartDate).TotalMinutes == 30);
            var bOrder = orders.Single(o => o.DishId == s.DishB);
            bOrder.ProductionLineId.Should().Be(s.LineB, "nhóm con kế thừa dây chuyền của nhóm cha");
            batches.Where(b => b.ProductionOrderId == bOrder.Id).Should().ContainSingle().Which.Should().Match<ProductionBatch>(b => (b.EndDate!.Value - b.StartDate).TotalMinutes == 45);
            foreach (var d in new[] { s.DishC, s.DishD })
            {
                var o = orders.Single(x => x.DishId == d);
                o.ProductionLineId.Should().BeNull();
                batches.Where(b => b.ProductionOrderId == o.Id).Should().ContainSingle("chưa gán / dây chuyền ngừng → 1 mẻ");
            }

            // Đơn chốt muộn → lệnh bổ sung cũng theo dây chuyền của nhóm món.
            Guid late = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Order.Add(new Order { Id = late, OrderNumber = "DH-L1-LATE", CustomerId = s.Customer, OrderDate = day.AddDays(-1), MealDate = day, DeliveryDate = day, Status = "Locked", TotalQuantity = 50 });
                db.OrderLine.Add(new OrderLine { Id = Guid.NewGuid(), OrderId = late, MenuId = s.Menu, MealType = "TRUA", ShiftId = s.Shift, Quantity = 50, LineAmount = 0 });
                return Task.CompletedTask;
            });
            using (var scope = _factory.Services.CreateScope())
            using (scope.ServiceProvider.GetRequiredService<ICurrentActor>().BeginScope(ActorInfo.ForSystem(InMemoryApiFactory.UserId)))
            {
                var sup = await scope.ServiceProvider.GetRequiredService<IProductionPlanService>().SupplementForOrderAsync(late, "test");
                sup.Status.Should().Be("RELEASED", string.Join("; ", sup.ListReason));
            }
            var supA = await _factory.QueryAsync(db => db.ProductionOrder.AsNoTracking().SingleAsync(o => o.DishId == s.DishA && o.Note!.Contains("DH-L1-LATE")));
            supA.ProductionLineId.Should().Be(s.LineA);
            (await _factory.QueryAsync(db => db.ProductionBatch.CountAsync(b => b.ProductionOrderId == supA.Id))).Should().Be(2, "50 suất ÷ 40 suất / mẻ");
        }

        [Fact]
        public async Task Line_setup_assigns_categories_and_rename_keeps_the_line()
        {
            var day = new DateTime(2032, 9, 14);
            _factory.Clock.FixedNow = day.AddHours(9);
            var s = await SeedAsync("L2", day);
            using var c = _factory.CreateAuthorizedClient();

            // Gán nhanh: dây chuyền ngừng → chặn; gán + bỏ gán trong một giao dịch.
            (await LegacyResult.ReadAsync(await c.PostAsJsonAsync("/api/DishCategory/AssignProductionLine", new { listItem = new[] { new { dishCategoryId = s.CatNone, productionLineId = (Guid?)s.LineOff } } }))).Fail("ngừng hoạt động");
            var assigned = (await LegacyResult.ReadAsync(await c.PostAsJsonAsync("/api/DishCategory/AssignProductionLine", new { listItem = new object[] {
                new { dishCategoryId = s.CatNone, productionLineId = (Guid?)s.LineB }, new { dishCategoryId = s.CatA, productionLineId = (Guid?)null } } }))).Ok();
            assigned.GetInt32().Should().Be(2);
            (await _factory.QueryAsync(db => db.DishCategory.AsNoTracking().SingleAsync(x => x.Id == s.CatNone))).ProductionLineId.Should().Be(s.LineB);
            (await _factory.QueryAsync(db => db.DishCategory.AsNoTracking().SingleAsync(x => x.Id == s.CatA))).ProductionLineId.Should().BeNull();

            // Form dây chuyền mới kèm nhóm món: mã tự sinh DC-NNN, nhóm được gán; bỏ nhóm khỏi danh sách → bỏ gán.
            var saved = (await LegacyResult.ReadAsync(await c.PostAsJsonAsync("/api/ProductionLine/SaveWithCategories", new { name = "Bếp mặn mới L2", capacityMealPerBatch = 60, minutesPerBatch = 40, listDishCategoryId = new[] { s.CatA } }))).Ok();
            saved.GetProperty("code").GetString().Should().MatchRegex(@"^DC-\d{3,}$");
            Guid newLine = saved.GetProperty("id").GetGuid();
            saved.GetProperty("listCategory").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).Should().Equal(s.CatA);
            (await LegacyResult.ReadAsync(await c.PostAsJsonAsync("/api/ProductionLine/SaveWithCategories", new { name = "x", capacityMealPerBatch = 0, minutesPerBatch = 40 }))).Fail("lớn hơn 0");
            (await LegacyResult.ReadAsync(await c.PostAsJsonAsync("/api/ProductionLine/SaveWithCategories", new { id = newLine, name = "Bếp mặn mới L2", capacityMealPerBatch = 60, minutesPerBatch = 40, listDishCategoryId = Array.Empty<Guid>() }))).Ok();
            (await _factory.QueryAsync(db => db.DishCategory.AsNoTracking().SingleAsync(x => x.Id == s.CatA))).ProductionLineId.Should().BeNull();

            // Đổi tên nhóm ở cột nhóm (không gửi dây chuyền) → giữ dây chuyền.
            (await c.PutFormAsync("/api/DishCategory/Update", ("Id", s.CatNone), ("Name", "Tráng miệng đổi tên L2"), ("Status", "Active"))).Ok();
            (await _factory.QueryAsync(db => db.DishCategory.AsNoTracking().SingleAsync(x => x.Id == s.CatNone))).ProductionLineId.Should().Be(s.LineB);

            var ov = (await c.GetResultAsync("/api/ProductionLine/GetOverview")).Ok();
            var lb = ov.GetProperty("listLine").EnumerateArray().Single(l => l.GetProperty("id").GetGuid() == s.LineB);
            lb.GetProperty("listCategory").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).Should().Contain([s.CatNone, s.CatParent]);
            ov.GetProperty("listCategory").EnumerateArray().Single(x => x.GetProperty("name").GetString() == "Cũ L2").GetProperty("lineInactive").GetBoolean().Should().BeTrue();
            ov.GetProperty("unassignedCategoryCount").GetInt32().Should().BeGreaterThan(0);
        }
    }
}
