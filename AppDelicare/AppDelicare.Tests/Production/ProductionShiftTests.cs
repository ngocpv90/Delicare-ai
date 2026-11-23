using System.Net.Http.Json;
using System.Text.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Production
{
    /// <summary>
    /// T17.1 (anh chốt 2026-11-16) – Pilot chạy 4 ca / ngày: kế hoạch MRP vẫn 1 KH- / ngày, phát hành tách theo ca của đơn Đã chốt.
    /// Đồng hồ test = 20/12/2026 10:00, ngày sản xuất 21/12/2026 (thứ 2).
    /// <para>Bộ số tính tay: đơn A (TRUA, điểm giao 11:30) 250 suất, đơn B (TOI, điểm giao 17:30) 100 suất. Thực đơn: TRUA = Thịt + Canh, TOI = Thịt.
    /// Kế hoạch: Thịt 350 (= TRUA 250 + TOI 100), Canh 80 (chỉ TRUA). Dây chuyền 100 phần / 60′.
    /// TRUA: xuất bếp 11:30 − 60′ = 10:30, xong nấu 10:00 → Thịt 250 → 84 / 83 / 83 (07–08, 08–09, 09–10), Canh 80 (09–10) → ME-261221-TRUAS1-01…04.
    /// TOI: xuất bếp 17:30 − 60′ = 16:30, xong nấu 16:00 → Thịt 100 → 1 mẻ 15:00–16:00 → ME-261221-TOIS1-01.</para>
    /// </summary>
    public class ProductionShiftTests : IClassFixture<InMemoryApiFactory>
    {
        private static readonly DateTime Today = new(2026, 12, 20);
        private static readonly DateTime Day = Today.AddDays(1);
        private readonly InMemoryApiFactory _factory;

        public ProductionShiftTests(InMemoryApiFactory factory)
        {
            _factory = factory;
            _factory.Clock.FixedNow = Today.AddHours(10);
            _factory.Settings["BusinessRules:BatchAutoPass"] = "false"; // kiểm luồng QC / đóng gói tay (BIZ-2.7 / 2.8 tự Đạt + tự lệnh đóng gói: kiểm ở ProductionSqlServerTests / luồng SQL)
            _factory.Settings["BusinessRules:PackingAutoGenerateOnPass"] = "false";
        }

        private sealed record Scenario(Guid Noon, Guid Evening, Guid Line, Guid DishMeat, Guid DishSoup, Guid Plan, Guid OrderNoon, Guid OrderEvening, Guid Vehicle, Guid Driver);

        private async Task<Scenario> SeedAsync(string code)
        {
            Guid noon = Guid.NewGuid(), evening = Guid.NewGuid(), line = Guid.NewGuid(), kg = Guid.NewGuid(), meat = Guid.NewGuid(), veg = Guid.NewGuid();
            Guid dishMeat = Guid.NewGuid(), dishSoup = Guid.NewGuid(), plan = Guid.NewGuid(), menu = Guid.NewGuid(), customer = Guid.NewGuid();
            Guid locNoon = Guid.NewGuid(), locEvening = Guid.NewGuid(), orderNoon = Guid.NewGuid(), orderEvening = Guid.NewGuid(), vehicle = Guid.NewGuid(), driver = Guid.NewGuid();
            await _factory.SeedAsync(async db =>
            {
                db.Shift.AddRange(
                    new Shift { Id = noon, Code = "TRUA" + code, Name = "Trưa", StartTime = new TimeSpan(10, 30, 0), EndTime = new TimeSpan(13, 30, 0), Status = "Active" },
                    new Shift { Id = evening, Code = "TOI" + code, Name = "Tối", StartTime = new TimeSpan(16, 30, 0), EndTime = new TimeSpan(19, 30, 0), Status = "Active" });
                db.ProductionLine.Add(new ProductionLine { Id = line, Code = "DC-" + code, Name = "Bếp nóng " + code, CapacityMealPerBatch = 100, MinutesPerBatch = 60, Status = "Active" });
                db.UnitMeasure.Add(new UnitMeasure { Id = kg, Code = "KG-" + code, Name = "kg" });
                db.Ingredient.AddRange(
                    new Ingredient { Id = meat, Code = "THIT-" + code, Name = "Thịt " + code, BaseUnitId = kg },
                    new Ingredient { Id = veg, Code = "RAU-" + code, Name = "Rau " + code, BaseUnitId = kg });
                db.Dish.AddRange(
                    new Dish { Id = dishMeat, Code = "TK-" + code, Name = "Thịt kho " + code, Status = "Active" },
                    new Dish { Id = dishSoup, Code = "CA-" + code, Name = "Canh rau " + code, Status = "Active" });
                db.Recipe.AddRange(
                    new Recipe { Id = Guid.NewGuid(), DishId = dishMeat, VersionNo = 1, VersionCode = "v1", Status = "Locked", LockedAt = Today.AddDays(-5), IsActive = true, StandardOutputQty = 100,
                        SnapshotJson = $"[{{\"IngredientId\":\"{meat}\",\"Quantity\":12,\"UnitId\":\"{kg}\",\"WastePercent\":10,\"SequenceNo\":1}}]" },
                    new Recipe { Id = Guid.NewGuid(), DishId = dishSoup, VersionNo = 1, VersionCode = "v1", Status = "Locked", LockedAt = Today.AddDays(-5), IsActive = true, StandardOutputQty = 100,
                        SnapshotJson = $"[{{\"IngredientId\":\"{veg}\",\"Quantity\":5,\"UnitId\":\"{kg}\",\"WastePercent\":0,\"SequenceNo\":1}}]" });
                db.Menu.Add(new Model.Entity.Menu { Id = menu, Code = "TD-" + code, Name = "Thực đơn " + code, VersionNo = 1, Status = "Published", EffectiveFrom = Today.AddDays(-30) });
                db.MenuLine.AddRange(
                    new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = dishMeat, MealType = "TRUA", DayOfWeek = 1, SequenceNo = 1 },
                    new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = dishSoup, MealType = "TRUA", DayOfWeek = 1, SequenceNo = 1 },
                    new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = dishMeat, MealType = "TOI", DayOfWeek = 1, SequenceNo = 1 });
                db.ProductionPlan.Add(new ProductionPlan { Id = plan, PlanNumber = "KH-" + code, PlanNo = "KH-" + code, ProductionDate = Day, PlanDate = Today, Status = "Draft" });
                db.ProductionPlanLine.AddRange(
                    new ProductionPlanLine { Id = Guid.NewGuid(), ProductionPlanId = plan, DishId = dishMeat, PlannedQuantity = 350, PlannedQty = 350, Status = "Draft" },
                    new ProductionPlanLine { Id = Guid.NewGuid(), ProductionPlanId = plan, DishId = dishSoup, PlannedQuantity = 80, PlannedQty = 80, Status = "Draft" });
                db.Customer.Add(new Customer { Id = customer, Code = "KH-" + code, Name = "[DEMO] Khách " + code, CIF = "C" + code, CustomerType = "FACTORY", DisplayName = "Khách " + code, Status = "Active" });
                db.CustomerDeliveryLocation.AddRange(
                    new CustomerDeliveryLocation { Id = locNoon, CustomerId = customer, Code = "DGT-" + code, Name = "Căng tin trưa " + code, DeliveryWindowFrom = new TimeSpan(11, 30, 0) },
                    new CustomerDeliveryLocation { Id = locEvening, CustomerId = customer, Code = "DGC-" + code, Name = "Căng tin tối " + code, DeliveryWindowFrom = new TimeSpan(17, 30, 0) });
                db.Order.AddRange(
                    new Order { Id = orderNoon, OrderNumber = "PH-T" + code, OrderNo = "PH-T" + code, CustomerId = customer, DeliveryLocationId = locNoon, OrderDate = Today, MealDate = Day, DeliveryDate = Day, Status = "Locked", TotalQuantity = 250 },
                    new Order { Id = orderEvening, OrderNumber = "PH-C" + code, OrderNo = "PH-C" + code, CustomerId = customer, DeliveryLocationId = locEvening, OrderDate = Today, MealDate = Day, DeliveryDate = Day, Status = "Locked", TotalQuantity = 100 });
                db.OrderLine.AddRange(
                    new OrderLine { Id = Guid.NewGuid(), OrderId = orderNoon, MenuId = menu, MealType = "TRUA", ShiftId = noon, Quantity = 250, UnitPrice = 30_000, LineAmount = 7_500_000 },
                    new OrderLine { Id = Guid.NewGuid(), OrderId = orderEvening, MenuId = menu, MealType = "TOI", ShiftId = evening, Quantity = 100, UnitPrice = 30_000, LineAmount = 3_000_000 });
                db.Vehicle.Add(new Vehicle { Id = vehicle, Code = "XE" + code, LicensePlate = "51C-" + code, Capacity = 200, Status = "Active" });
                db.Employee.Add(new Employee { Id = driver, Code = "TX" + code, FullName = "Tài xế " + code, Status = "Active" });
                if (!await db.FoodSafetyChecklistItem.AnyAsync(i => i.ItemCode == "SAMPLE_RETAINED"))
                {
                    Guid checklist = Guid.NewGuid();
                    db.FoodSafetyChecklist.Add(new FoodSafetyChecklist { Id = checklist, Code = "PRD_BATCH", Name = "Kiểm soát mẻ nấu", VersionNo = 1, Status = "Active" });
                    db.FoodSafetyChecklistItem.Add(new FoodSafetyChecklistItem { Id = Guid.NewGuid(), ChecklistId = checklist, ItemCode = "SAMPLE_RETAINED", ItemName = "Lưu mẫu", SortOrder = 1, IsCritical = true, IsRequired = true });
                }
            });
            return new Scenario(noon, evening, line, dishMeat, dishSoup, plan, orderNoon, orderEvening, vehicle, driver);
        }

        private static List<(string? Number, Guid Dish, decimal Qty, string Start, string End)> Batches(JsonElement detail, string shiftCode)
            => [.. detail.GetProperty("listBatch").EnumerateArray()
                .Where(b => b.GetProperty("shiftCode").GetString() == shiftCode)
                .Select(b => (b.GetProperty("batchNumber").GetString(), b.GetProperty("dishId").GetGuid(), b.GetProperty("plannedQuantity").GetDecimal(),
                    b.GetProperty("plannedStart").GetDateTime().ToString("HH:mm"), b.GetProperty("plannedEnd").GetDateTime().ToString("HH:mm")))
                .OrderBy(b => b.Item1, StringComparer.Ordinal)];

        private Task<string?> OrderStatusAsync(Guid id) => _factory.QueryAsync(db => db.Order.AsNoTracking().Where(o => o.Id == id).Select(o => o.Status).SingleAsync());

        [Fact]
        public async Task Release_splits_the_daily_plan_by_shift_with_own_schedule_and_me_codes_and_can_release_one_shift_at_a_time()
        {
            var s = await SeedAsync("S1");
            using var c = _factory.CreateAuthorizedClient();
            (await c.PostFormAsync("/api/ProductionPlan/Approve", ("Id", s.Plan))).Ok();

            // Trước khi phát hành: phần theo ca (TRUA 250 + 80, TOI 100), thứ tự theo giờ bắt đầu ca.
            var before = (await c.GetResultAsync($"/api/ProductionPlan/GetDetail?id={s.Plan}")).Ok();
            before.GetProperty("listShift").EnumerateArray().Select(x => (x.GetProperty("shiftCode").GetString(), x.GetProperty("portions").GetDecimal(), x.GetProperty("released").GetBoolean()))
                .Should().Equal(("TRUAS1", 330m, false), ("TOIS1", 100m, false));
            var meatLine = before.GetProperty("listLine").EnumerateArray().Single(l => l.GetProperty("dishId").GetGuid() == s.DishMeat);
            meatLine.GetProperty("listShift").EnumerateArray().Select(x => (x.GetProperty("shiftCode").GetString(), x.GetProperty("quantity").GetDecimal()))
                .Should().BeEquivalentTo([("TRUAS1", 250m), ("TOIS1", 100m)]);
            // Giờ xuất bếp nhập tay khi còn 2 ca chưa phát hành → phải chọn ca.
            (await c.PostFormAsync("/api/ProductionPlan/Release", ("Id", s.Plan), ("ProductionLineId", s.Line), ("DispatchTime", "10:00"))).Fail("chỉ dùng khi phát hành 1 ca");

            // 1) Phát hành riêng ca TỐI: 1 lệnh SX Thịt 100, 1 mẻ 15:00–16:00 (xuất bếp 17:30 − 60′ = 16:30, đóng gói 30′); kế hoạch vẫn Đã duyệt.
            var evening = (await c.PostFormAsync("/api/ProductionPlan/Release", ("Id", s.Plan), ("ShiftId", s.Evening), ("ProductionLineId", s.Line))).Ok();
            evening.GetProperty("productionPlan").GetProperty("status").GetString().Should().Be("Approved");
            Batches(evening, "TOIS1").Should().Equal(("ME-261221-TOIS1-01", s.DishMeat, 100m, "15:00", "16:00"));
            Batches(evening, "TRUAS1").Should().BeEmpty();
            evening.GetProperty("allowedActions").EnumerateArray().Select(a => a.GetString()).Should().Equal("release", "exportProductionSheet");
            var shifts = evening.GetProperty("listShift").EnumerateArray().ToList();
            shifts.Select(x => (x.GetProperty("shiftCode").GetString(), x.GetProperty("released").GetBoolean())).Should().Equal(("TRUAS1", false), ("TOIS1", true));
            shifts[1].GetProperty("dispatchAt").GetDateTime().Should().Be(Day.AddHours(16).AddMinutes(30));
            (await OrderStatusAsync(s.OrderEvening)).Should().Be("InProduction");
            (await OrderStatusAsync(s.OrderNoon)).Should().Be("Locked", "ca TRƯA chưa phát hành");
            // Không phát hành lại ca đã phát hành.
            (await c.PostFormAsync("/api/ProductionPlan/Release", ("Id", s.Plan), ("ShiftId", s.Evening))).Fail("đã phát hành");

            // 2) Phát hành phần còn lại (bỏ trống ca = mọi ca chưa phát hành) → ca TRƯA: lịch riêng từ 10:30, mẻ chia đều 84 / 83 / 83.
            var all = (await c.PostFormAsync("/api/ProductionPlan/Release", ("Id", s.Plan), ("ProductionLineId", s.Line))).Ok();
            all.GetProperty("productionPlan").GetProperty("status").GetString().Should().Be("Released");
            Batches(all, "TRUAS1").Should().Equal(
                ("ME-261221-TRUAS1-01", s.DishMeat, 84m, "07:00", "08:00"),
                ("ME-261221-TRUAS1-02", s.DishMeat, 83m, "08:00", "09:00"),
                ("ME-261221-TRUAS1-03", s.DishSoup, 80m, "09:00", "10:00"),
                ("ME-261221-TRUAS1-04", s.DishMeat, 83m, "09:00", "10:00"));
            Batches(all, "TOIS1").Should().ContainSingle();
            all.GetProperty("allowedActions").EnumerateArray().Select(a => a.GetString()).Should().Equal("exportProductionSheet");
            (await OrderStatusAsync(s.OrderNoon)).Should().Be("InProduction");
            (await c.PostFormAsync("/api/ProductionPlan/Release", ("Id", s.Plan))).Fail("Đã duyệt");

            var orders = await _factory.QueryAsync(db => db.ProductionOrder.AsNoTracking().Where(o => o.ProductionPlanId == s.Plan).ToListAsync());
            orders.Select(o => (o.ShiftId, o.DishId, o.PlannedQty)).Should().BeEquivalentTo(
                [(s.Evening, s.DishMeat, 100m), (s.Noon, s.DishMeat, 250m), (s.Noon, s.DishSoup, 80m)]);
            orders.Select(o => o.ProductionOrderNumber).Should().OnlyHaveUniqueItems().And.OnlyContain(n => n!.StartsWith("LS-261220-"));
            orders.Single(o => o.ShiftId == s.Noon && o.DishId == s.DishMeat).EndTime.Should().Be(Day.AddHours(10));
            var planLines = await _factory.QueryAsync(db => db.ProductionPlanLine.AsNoTracking().Where(l => l.ProductionPlanId == s.Plan).ToListAsync());
            planLines.Should().OnlyContain(l => l.Status == "Released");
            planLines.Single(l => l.DishId == s.DishMeat).ShiftId.Should().BeNull("món thuộc 2 ca");
            planLines.Single(l => l.DishId == s.DishSoup).ShiftId.Should().Be(s.Noon);
        }

        [Fact]
        public async Task Dispatch_of_an_evening_route_checks_only_evening_batches_and_a_packed_batch_without_sample_can_still_record_it()
        {
            var s = await SeedAsync("S2");
            using var c = _factory.CreateAuthorizedClient();
            (await c.PostFormAsync("/api/ProductionPlan/Approve", ("Id", s.Plan))).Ok();
            var detail = (await c.PostFormAsync("/api/ProductionPlan/Release", ("Id", s.Plan), ("ProductionLineId", s.Line))).Ok();
            detail.GetProperty("productionPlan").GetProperty("status").GetString().Should().Be("Released", "phát hành mọi ca trong 1 lần");
            var (eveningNo, _, _, _, _) = Batches(detail, "TOIS2").Single();
            Guid eveningBatch = detail.GetProperty("listBatch").EnumerateArray().Single(b => b.GetProperty("batchNumber").GetString() == eveningNo).GetProperty("id").GetGuid();

            // Mẻ TỐI: nấu → 75 °C → xong → sản lượng → đạt → đóng gói, chưa lưu mẫu.
            (await c.PostFormAsync("/api/ProductionBatch/Start", ("Id", eveningBatch))).Ok();
            (await c.PostFormAsync("/api/ProductionBatch/RecordCoreTemperature", ("Id", eveningBatch), ("TemperatureC", 75m))).Ok();
            (await c.PostFormAsync("/api/ProductionBatch/Finish", ("Id", eveningBatch), ("ActualQuantity", 100m))).Ok();
            (await c.PostFormAsync("/api/ProductionBatch/RecordOutput", ("Id", eveningBatch), ("ProducedQuantity", 100m))).Ok();
            (await c.PostFormAsync("/api/ProductionBatch/Release", ("Id", eveningBatch))).Ok();
            var packed = (await c.PostFormAsync("/api/ProductionBatch/Pack", ("Id", eveningBatch))).Ok();
            packed.GetProperty("batch").GetProperty("status").GetString().Should().Be("Packed");
            // Đã đóng gói chưa lưu mẫu → vẫn có thao tác ghi lưu mẫu (D-42).
            packed.GetProperty("allowedActions").EnumerateArray().Select(a => a.GetString()).Should().Contain("recordSampleRetained");

            // Chuyến TỐI chỉ gồm đơn B; mẻ TRƯA còn Kế hoạch không chặn chuyến TỐI.
            var route = (await LegacyResult.ReadAsync(await c.PostAsJsonAsync("/api/DeliveryRoute/Create",
                new { deliveryDate = Day, shiftId = s.Evening, vehicleId = s.Vehicle, driverId = s.Driver, listOrderId = new[] { s.OrderEvening } }))).Ok();
            Guid routeId = route.GetProperty("route").GetProperty("id").GetGuid();
            route.GetProperty("listBatch").EnumerateArray().Select(b => b.GetProperty("batchNumber").GetString()).Should().Equal(eveningNo);
            var blocked = await c.PostFormAsync("/api/DeliveryRoute/Dispatch", ("Id", routeId), ("TemperatureC", 65m));
            blocked.Fail("Mẻ chưa lưu mẫu thức ăn (bắt buộc trước khi giao): " + eveningNo);
            blocked.Message.Should().NotContain("TRUAS2");

            var sampled = (await c.PostFormAsync("/api/ProductionBatch/RecordSampleRetained", ("Id", eveningBatch))).Ok();
            sampled.GetProperty("allowedActions").EnumerateArray().Select(a => a.GetString()).Should().NotContain("recordSampleRetained");
            // BE2.1: xếp hàng lên xe trước khi xuất phát (DeliveryRequireLoading).
            (await LegacyResult.ReadAsync(await c.PostAsJsonAsync("/api/DeliveryRoute/Load", new { id = routeId }))).Ok();
            var dispatched = (await c.PostFormAsync("/api/DeliveryRoute/Dispatch", ("Id", routeId))).Ok();
            dispatched.GetProperty("route").GetProperty("status").GetString().Should().Be("InTransit");
            (await _factory.QueryAsync(db => db.ProductionBatch.AsNoTracking().CountAsync(b => b.BatchNumber!.StartsWith("ME-261221-TRUAS2-") && b.Status == "Planned")))
                .Should().Be(4, "mẻ ca TRƯA chưa nấu không ảnh hưởng chuyến TỐI");
        }
    }
}
