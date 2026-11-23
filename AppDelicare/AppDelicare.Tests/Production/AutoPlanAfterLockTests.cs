using System.Text.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Services.Implement;
using AppDelicare.Services.Interface;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Production
{
    /// <summary>
    /// BIZ-2.4 (D-77 – B5, C3): chốt đơn theo ngày → hệ thống tự lập kế hoạch SX, tự duyệt + phát hành. Món thiếu công thức khóa / kế hoạch đã sửa tay →
    /// giữ Nháp (HELD) kèm lý do. (Thiếu NL / không kịp giờ chỉ kiểm được trên SQL Server – phát hành thử trong transaction.)
    /// </summary>
    public class AutoPlanAfterLockTests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public AutoPlanAfterLockTests(InMemoryApiFactory factory) => _factory = factory;

        private static int MenuDay(DateTime d) => d.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)d.DayOfWeek;

        /// <summary>Khách + đơn Đã xác nhận 100 suất TRUA ngày <paramref name="day"/> + món (có / không công thức khóa).</summary>
        private async Task<(Guid Order, Guid Dish)> SeedAsync(string code, DateTime day, bool lockedRecipe, decimal stockKg = 0)
        {
            Guid shift = Guid.NewGuid(), dish = Guid.NewGuid(), menu = Guid.NewGuid(), customer = Guid.NewGuid(), order = Guid.NewGuid(), kg = Guid.NewGuid(), ing = Guid.NewGuid();
            await _factory.SeedAsync(async db =>
            {
                db.Shift.Add(new Shift { Id = shift, Code = "T" + code, Name = "Trưa " + code, StartTime = new TimeSpan(10, 30, 0), Status = "Active" });
                db.Dish.Add(new Dish { Id = dish, Code = "M" + code, Name = "Món " + code, Status = "Active" });
                db.Menu.Add(new Model.Entity.Menu { Id = menu, Code = "TD" + code, Name = "TĐ " + code, VersionNo = 1, Status = "Published" });
                db.MenuLine.Add(new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = dish, MealType = "TRUA", DayOfWeek = MenuDay(day), SequenceNo = 1, SortOrder = 1 });
                if (lockedRecipe)
                {
                    db.UnitMeasure.Add(new UnitMeasure { Id = kg, Code = "KG" + code, Name = "kg" });
                    db.Ingredient.Add(new Ingredient { Id = ing, Code = "NL" + code, Name = "NL " + code, BaseUnitId = kg });
                    db.Recipe.Add(new Recipe { Id = Guid.NewGuid(), DishId = dish, VersionNo = 1, Status = "Locked", IsActive = true, StandardOutputQty = 100, EffectiveFrom = day.AddDays(-30),
                        LockedAt = day.AddDays(-30), SnapshotJson = JsonSerializer.Serialize(new[] { new { IngredientId = ing, Quantity = 10m, UnitId = kg, SequenceNo = 1 } }) });
                    if (stockKg > 0)
                    {
                        Guid warehouse = Guid.NewGuid(), lot = Guid.NewGuid();
                        db.Warehouse.Add(new Warehouse { Id = warehouse, Code = "W" + code, Name = "Kho " + code, Status = "Active" });
                        db.MaterialLot.Add(new MaterialLot { Id = lot, IngredientId = ing, LotNumber = "L" + code, LotNo = "L" + code, ExpiryDate = day.AddDays(30), ReceivedDate = day.AddDays(-2), QcStatus = "DAT", QualityStatus = "DAT", Status = "Available" });
                        await new InventoryLedgerService(db).PostAsync(new InventoryPostRequest { TransactionType = "RECEIPT", TransactionNumber = "NK" + code, WarehouseId = warehouse, IngredientId = ing, MaterialLotId = lot, UnitId = kg, QuantityIn = stockKg });
                    }
                }
                db.Customer.Add(new Customer { Id = customer, Code = "K" + code, Name = "Khách " + code, DisplayName = "Khách " + code, CIF = "K" + code, CustomerType = "FACTORY", Status = "Active" });
                db.Order.Add(new Order { Id = order, OrderNumber = "DH-" + code, CustomerId = customer, OrderDate = day.AddDays(-3), MealDate = day, DeliveryDate = day, Status = "Confirmed", TotalQuantity = 100 });
                db.OrderLine.Add(new OrderLine { Id = Guid.NewGuid(), OrderId = order, MenuId = menu, MealType = "TRUA", ShiftId = shift, Quantity = 100, LineAmount = 0 });
            });
            return (order, dish);
        }

        private Task<ProductionPlan> PlanAsync(DateTime day)
            => _factory.QueryAsync(db => db.ProductionPlan.AsNoTracking().SingleAsync(p => p.ProductionDate == day && !p.IsDeleted));

        [Fact]
        public async Task Lock_generates_approves_and_releases_the_plan()
        {
            var day = new DateTime(2027, 6, 14);
            _factory.Clock.FixedNow = day.AddDays(-1).AddHours(20).AddMinutes(5);
            var (order, _) = await SeedAsync("AP1", day, lockedRecipe: true, stockKg: 30);
            using var c = _factory.CreateAuthorizedClient();
            var summary = (await c.PostFormAsync("/api/Order/LockByMealDate", ("MealDate", day))).Ok();
            summary.GetProperty("lockedCount").GetInt32().Should().Be(1);
            var auto = summary.GetProperty("autoPlan");
            auto.GetProperty("status").GetString().Should().Be("RELEASED", string.Join("; ", auto.GetProperty("listReason").EnumerateArray().Select(x => x.GetString())));
            auto.GetProperty("productionOrderCount").GetInt32().Should().Be(1);
            var plan = await PlanAsync(day);
            plan.Status.Should().Be("Released");
            plan.Note.Should().Contain("Hệ thống tự duyệt + phát hành");
            (await _factory.QueryAsync(db => db.Order.AsNoTracking().SingleAsync(o => o.Id == order))).Status.Should().Be("InProduction");

            // BIZ-2.5: phiếu xuất Nháp theo ca (số = giữ chỗ 10 kg) → Ghi sổ cả ca.
            var group = (await c.GetResultAsync("/api/MaterialIssue/GetShiftGroups?fromDate=2027-06-01")).ListData.EnumerateArray().Single(g => g.GetProperty("shiftCode").GetString() == "TAP1");
            group.GetProperty("draftCount").GetInt32().Should().Be(1);
            string tag = group.GetProperty("tag").GetString()!;
            var issueLine = await _factory.QueryAsync(db => db.MaterialIssueLine.AsNoTracking().SingleAsync(l => db.MaterialIssue.Any(i => i.Id == l.MaterialIssueId && i.Note!.StartsWith(tag))));
            issueLine.Quantity.Should().Be(10);
            issueLine.MaterialLotId.Should().NotBeNull("lô FEFO phân bổ sẵn");
            (await c.PostFormAsync("/api/MaterialIssue/PostShift", ("Tag", tag))).Ok().GetInt32().Should().Be(1);
            (await c.PostFormAsync("/api/MaterialIssue/PostShift", ("Tag", tag))).Fail("Không có phiếu xuất Nháp");
        }

        [Fact]
        public async Task Dish_without_locked_recipe_keeps_plan_draft_for_coo()
        {
            var day = new DateTime(2027, 6, 21);
            _factory.Clock.FixedNow = day.AddDays(-1).AddHours(20).AddMinutes(5);
            await SeedAsync("AP2", day, lockedRecipe: false);
            using var c = _factory.CreateAuthorizedClient();
            var auto = (await c.PostFormAsync("/api/Order/LockByMealDate", ("MealDate", day))).Ok().GetProperty("autoPlan");
            auto.GetProperty("status").GetString().Should().Be("HELD");
            auto.GetProperty("listReason").EnumerateArray().Select(x => x.GetString()).Should().Contain(r => r!.Contains("Món chưa có công thức khóa: Món AP2"));
            var plan = await PlanAsync(day);
            plan.Status.Should().Be("Draft");
            plan.Note.Should().Contain("KHÔNG tự phát hành");
        }

        [Fact]
        public async Task Hand_edited_draft_plan_is_kept_and_not_released()
        {
            var day = new DateTime(2027, 6, 28);
            _factory.Clock.FixedNow = day.AddDays(-1).AddHours(20).AddMinutes(5);
            var (_, dish) = await SeedAsync("AP3", day, lockedRecipe: true);
            Guid planId = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.ProductionPlan.Add(new ProductionPlan { Id = planId, PlanNumber = "KS-AP3", PlanNo = "KS-AP3", ProductionDate = day, PlanDate = day.AddDays(-1), Status = "Draft", Note = "Kế hoạch lập tay" });
                db.ProductionPlanLine.Add(new ProductionPlanLine { Id = Guid.NewGuid(), ProductionPlanId = planId, DishId = dish, PlannedQuantity = 120, PlannedQty = 120, Status = "Draft" });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();
            var auto = (await c.PostFormAsync("/api/Order/LockByMealDate", ("MealDate", day))).Ok().GetProperty("autoPlan");
            auto.GetProperty("status").GetString().Should().Be("HELD");
            auto.GetProperty("listReason").EnumerateArray().Select(x => x.GetString()).Should().Contain(r => r!.Contains("đã được sửa tay"));
            (await _factory.QueryAsync(db => db.ProductionPlanLine.AsNoTracking().SingleAsync(l => l.ProductionPlanId == planId))).PlannedQuantity.Should().Be(120, "giữ bản sửa tay");
        }
    }
}
