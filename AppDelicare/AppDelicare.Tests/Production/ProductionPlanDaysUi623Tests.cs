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
    /// UI-6.23 (D-104): một kế hoạch / ngày (Generate bỏ qua đơn vị), màn "7 ngày tới" (GetDays – đủ trạng thái), Lập / Tính lại (Recalculate – máy chủ tự quyết),
    /// nguyên liệu thiếu chỉ đọc (GetShortage – không ghi giữ chỗ) và "Vẫn duyệt" khi bị giữ lại.
    /// </summary>
    public class ProductionPlanDaysUi623Tests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public ProductionPlanDaysUi623Tests(InMemoryApiFactory factory) => _factory = factory;

        private static int MenuDay(DateTime d) => d.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)d.DayOfWeek;

        /// <summary>Đơn <paramref name="status"/> <paramref name="meals"/> suất ca trưa ngày <paramref name="day"/> + món (có / không công thức khóa, tồn kho kg).</summary>
        private async Task<(Guid Order, Guid Dish, Guid Ingredient, Guid Unit)> SeedAsync(string code, DateTime day, string status, decimal meals, bool lockedRecipe, decimal stockKg = 0)
        {
            Guid shift = Guid.NewGuid(), dish = Guid.NewGuid(), menu = Guid.NewGuid(), customer = Guid.NewGuid(), order = Guid.NewGuid(), kg = Guid.NewGuid(), ing = Guid.NewGuid();
            await _factory.SeedAsync(async db =>
            {
                db.Shift.Add(new Shift { Id = shift, Code = "T" + code, Name = "Trưa " + code, StartTime = new TimeSpan(10, 30, 0), Status = "Active" });
                db.Dish.Add(new Dish { Id = dish, Code = "M" + code, Name = "Món " + code, Status = "Active" });
                db.Menu.Add(new Model.Entity.Menu { Id = menu, Code = "TD" + code, Name = "TĐ " + code, VersionNo = 1, Status = "Published" });
                db.MenuLine.Add(new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = dish, MealType = "TRUA", DayOfWeek = MenuDay(day), SequenceNo = 1, SortOrder = 1 });
                db.UnitMeasure.Add(new UnitMeasure { Id = kg, Code = "KG" + code, Name = "kg" });
                db.Ingredient.Add(new Ingredient { Id = ing, Code = "NL" + code, Name = "NL " + code, BaseUnitId = kg });
                if (lockedRecipe)
                {
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
                db.Order.Add(new Order { Id = order, OrderNumber = "DH-" + code, CustomerId = customer, OrderDate = day.AddDays(-3), MealDate = day, DeliveryDate = day, Status = status, TotalQuantity = meals });
                db.OrderLine.Add(new OrderLine { Id = Guid.NewGuid(), OrderId = order, MenuId = menu, MealType = "TRUA", ShiftId = shift, Quantity = meals, LineAmount = 0 });
            });
            return (order, dish, ing, kg);
        }

        private async Task<JsonElement> DayAsync(HttpClient c, DateTime day)
        {
            var days = (await c.GetResultAsync($"/api/ProductionPlan/GetDays?from={day:yyyy-MM-dd}&to={day:yyyy-MM-dd}")).Ok();
            return days.GetProperty("listDay").EnumerateArray().Single();
        }

        [Fact]
        public async Task Generate_with_unit_still_keeps_one_plan_per_day()
        {
            var day = new DateTime(2032, 3, 2);
            _factory.Clock.FixedNow = day.AddDays(-1).AddHours(9);
            await SeedAsync("U1", day, "Locked", 100, lockedRecipe: true);
            Guid unit = Guid.NewGuid();
            await _factory.SeedAsync(db => { db.OrganizationUnit.Add(new OrganizationUnit { Id = unit, Code = "CN-U1", Name = "Chi nhánh U1" }); return Task.CompletedTask; });
            using var c = _factory.CreateAuthorizedClient();
            (await c.PostFormAsync("/api/ProductionPlan/Generate", ("ProductionDate", day))).Ok();
            (await c.PostFormAsync("/api/ProductionPlan/Generate", ("ProductionDate", day), ("OrganizationUnitId", unit))).Ok();
            var plans = await _factory.QueryAsync(db => db.ProductionPlan.AsNoTracking().Where(p => p.ProductionDate == day && !p.IsDeleted).ToListAsync());
            plans.Should().ContainSingle("một kế hoạch / ngày – đơn vị gửi lên không tạo bản thứ 2 (nấu gấp đôi)");
            plans[0].OrganizationUnitId.Should().BeNull();
        }

        [Fact]
        public async Task Days_show_every_state_and_recalculate_decides_the_flow()
        {
            DateTime open = new(2032, 4, 5), none = open.AddDays(1), held = open.AddDays(2), mixed = open.AddDays(3);
            _factory.Clock.FixedNow = open.AddDays(-2).AddHours(21);
            await SeedAsync("D1", open, "Confirmed", 80, lockedRecipe: true);
            await SeedAsync("D2", none, "Locked", 100, lockedRecipe: true, stockKg: 30);
            await SeedAsync("D3", held, "Locked", 120, lockedRecipe: false);
            await SeedAsync("D4", mixed, "Locked", 90, lockedRecipe: true, stockKg: 30);
            await SeedAsync("D5", mixed, "Submitted", 40, lockedRecipe: true);
            using var c = _factory.CreateAuthorizedClient();

            var week = (await c.GetResultAsync($"/api/ProductionPlan/GetDays?from={open:yyyy-MM-dd}&to={mixed.AddDays(3):yyyy-MM-dd}")).Ok();
            week.GetProperty("listDay").GetArrayLength().Should().Be(7);
            week.GetProperty("lockAt").GetString().Should().MatchRegex(@"^\d{1,2}:\d{2}$");
            (await c.GetResultAsync($"/api/ProductionPlan/GetDays?from={open:yyyy-MM-dd}&to={open.AddDays(20):yyyy-MM-dd}")).Fail("tối đa 14 ngày");

            var d1 = await DayAsync(c, open);
            (d1.GetProperty("dayStatus").GetString(), d1.GetProperty("action").GetString()).Should().Be(("open", "none"));
            d1.GetProperty("openMeals").GetDecimal().Should().Be(80);
            (await c.PostFormAsync("/api/ProductionPlan/Recalculate", ("ProductionDate", open))).Fail("Đã chốt");

            var d2 = await DayAsync(c, none);
            (d2.GetProperty("dayStatus").GetString(), d2.GetProperty("action").GetString()).Should().Be(("none", "recalc"));
            d2.GetProperty("listShift").EnumerateArray().Single().GetProperty("meals").GetDecimal().Should().Be(100);
            var r2 = (await c.PostFormAsync("/api/ProductionPlan/Recalculate", ("ProductionDate", none))).Ok();
            r2.GetProperty("status").GetString().Should().Be("RELEASED", "mọi đơn đã chốt, đủ công thức → luồng tự động sau chốt");
            r2.GetProperty("message").GetString().Should().Contain("tự duyệt và ra 1 lệnh");
            d2 = await DayAsync(c, none);
            (d2.GetProperty("dayStatus").GetString(), d2.GetProperty("action").GetString()).Should().Be(("released", "view"));
            (d2.GetProperty("shiftReleased").GetInt32(), d2.GetProperty("shiftTotal").GetInt32(), d2.GetProperty("productionOrderCount").GetInt32()).Should().Be((1, 1, 1));

            var r3 = (await c.PostFormAsync("/api/ProductionPlan/Recalculate", ("ProductionDate", held))).Ok();
            r3.GetProperty("status").GetString().Should().Be("HELD");
            var d3 = await DayAsync(c, held);
            (d3.GetProperty("dayStatus").GetString(), d3.GetProperty("action").GetString()).Should().Be(("held", "viewReason"));
            d3.GetProperty("heldReason").GetString().Should().Contain("công thức khóa");
            Guid heldPlan = d3.GetProperty("planId").GetGuid();
            int reservations = await _factory.QueryAsync(db => db.MaterialReservation.CountAsync());
            var shortage = (await c.GetResultAsync($"/api/ProductionPlan/GetShortage?id={heldPlan}")).Ok();
            shortage.GetProperty("listWarning").EnumerateArray().Select(w => w.GetString()).Should().Contain(w => w!.Contains("Món D3"));
            (await _factory.QueryAsync(db => db.MaterialReservation.CountAsync())).Should().Be(reservations, "GetShortage chỉ đọc");
            // Vẫn duyệt (quyền Duyệt prod-plan) → Đã duyệt, việc tiếp theo là Ra lệnh.
            (await c.PostFormAsync("/api/ProductionPlan/Approve", ("Id", heldPlan))).Ok();
            d3 = await DayAsync(c, held);
            (d3.GetProperty("dayStatus").GetString(), d3.GetProperty("action").GetString()).Should().Be(("approved", "release"));

            var r4 = (await c.PostFormAsync("/api/ProductionPlan/Recalculate", ("ProductionDate", mixed))).Ok();
            r4.GetProperty("status").GetString().Should().Be("DRAFT", "còn đơn chưa chốt → chỉ lập Nháp");
            r4.GetProperty("message").GetString().Should().Contain("Còn 40 suất chưa chốt");
            var d4 = await DayAsync(c, mixed);
            (d4.GetProperty("dayStatus").GetString(), d4.GetProperty("action").GetString()).Should().Be(("draft", "recalc"));
            (d4.GetProperty("lockedMeals").GetDecimal(), d4.GetProperty("openMeals").GetDecimal(), d4.GetProperty("lateMeals").GetDecimal()).Should().Be((90m, 40m, 0m));
            // Đơn chốt sau lần lập → "+N suất chốt sau lần lập".
            await _factory.SeedAsync(async db => { var o = await db.Order.SingleAsync(x => x.OrderNumber == "DH-D5"); o.Status = "Locked"; });
            d4 = await DayAsync(c, mixed);
            d4.GetProperty("lateMeals").GetDecimal().Should().Be(40);
        }

        [Fact]
        public async Task Shortage_lists_missing_ingredient_with_open_purchase_order_without_writing()
        {
            var day = new DateTime(2032, 5, 10);
            _factory.Clock.FixedNow = day.AddDays(-1).AddHours(9);
            var (_, _, ing, kg) = await SeedAsync("S1", day, "Locked", 100, lockedRecipe: true, stockKg: 3);
            await SeedAsync("S2", day, "Confirmed", 10, lockedRecipe: true);
            Guid supplier = Guid.NewGuid(), po = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Supplier.Add(new Supplier { Id = supplier, Code = "NCC-S1", Name = "NCC S1", Status = "Active" });
                db.PurchaseOrder.Add(new PurchaseOrder { Id = po, PurchaseOrderNumber = "PO-S1", PONo = "PO-S1", SupplierId = supplier, OrderDate = day.AddDays(-2), ExpectedDeliveryDate = day.AddDays(-1).AddHours(15), Status = "Sent" });
                db.PurchaseOrderLine.Add(new PurchaseOrderLine { Id = Guid.NewGuid(), PurchaseOrderId = po, IngredientId = ing, Quantity = 8, UnitId = kg, UomId = kg, UnitPrice = 1000 });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();
            var r = (await c.PostFormAsync("/api/ProductionPlan/Recalculate", ("ProductionDate", day))).Ok();
            r.GetProperty("status").GetString().Should().Be("DRAFT");
            Guid plan = r.GetProperty("planId").GetGuid();
            int before = await _factory.QueryAsync(db => db.MaterialReservation.CountAsync());
            var s = (await c.GetResultAsync($"/api/ProductionPlan/GetShortage?id={plan}")).Ok();
            var line = s.GetProperty("listLine").EnumerateArray().Single(l => l.GetProperty("ingredientId").GetGuid() == ing);
            (line.GetProperty("required").GetDecimal(), line.GetProperty("reserved").GetDecimal(), line.GetProperty("short").GetDecimal()).Should().Be((10m, 3m, 7m));
            var incoming = line.GetProperty("listIncoming").EnumerateArray().Single();
            (incoming.GetProperty("purchaseOrderNumber").GetString(), incoming.GetProperty("quantity").GetDecimal()).Should().Be(("PO-S1", 8m));
            (await _factory.QueryAsync(db => db.MaterialReservation.CountAsync())).Should().Be(before, "chỉ đọc – không ghi giữ chỗ");
            (await _factory.QueryAsync(db => db.ProductionOrder.CountAsync(o => o.ProductionPlanId == plan))).Should().Be(0);
        }
    }
}
