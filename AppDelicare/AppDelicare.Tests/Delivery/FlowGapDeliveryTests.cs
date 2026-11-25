using System.Net.Http.Json;
using System.Text.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Delivery
{
    /// <summary>
    /// BE2.1 – đóng gói từ mẻ (171–175), đơn giao tay / tách / gán – chuyển chuyến / hủy (184–187), lập chuyến nhiều xe theo khu vực (177–179),
    /// xếp hàng lên xe (180), thực hiện giao (188). Mỗi test 1 ngày riêng (các truy vấn "đơn chờ xếp chuyến" theo ngày).
    /// </summary>
    public class FlowGapDeliveryTests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public FlowGapDeliveryTests(InMemoryApiFactory factory)
        {
            factory.Settings["BusinessRules:PackingManualCreate"] = "true"; // UI-6.34: thử đường lập tay cũ (mặc định tắt)
            _factory = factory;
        }

        private static async Task<LegacyResult> PostJson(HttpClient c, string url, object body) => await LegacyResult.ReadAsync(await c.PostAsJsonAsync(url, body));

        private static async Task<LegacyResult> PutJson(HttpClient c, string url, object body) => await LegacyResult.ReadAsync(await c.PutAsJsonAsync(url, body));

        private static int MenuDay(DateTime d) => d.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)d.DayOfWeek;

        private sealed record Kitchen(Guid Shift, Guid Dish, Guid Batch, string BatchNumber, Guid OrderA, Guid OrderB, Guid CustomerA, Guid CustomerB, Guid LocationA, Guid LocationB2);

        /// <summary>
        /// 2 đơn (A 60 suất – Đang SX, B 40 suất – Đã chốt) cùng ca, thực đơn có món của mẻ; mẻ Đạt QC 100 phần đạt, nhiệt độ lõi 72 °C, đã ghi sản lượng.
        /// Khách A có 2 điểm giao (KCN Tân Bình / Quận 7), khách B ở Thủ Đức.
        /// </summary>
        private async Task<Kitchen> SeedKitchenAsync(string code, DateTime day, string batchStatus = "Passed", decimal coreTemp = 72m)
        {
            Guid shift = Guid.NewGuid(), dish = Guid.NewGuid(), menu = Guid.NewGuid(), plan = Guid.NewGuid(), productionOrder = Guid.NewGuid(), batch = Guid.NewGuid();
            Guid customerA = Guid.NewGuid(), customerB = Guid.NewGuid(), locationA = Guid.NewGuid(), locationA2 = Guid.NewGuid(), locationB = Guid.NewGuid();
            Guid orderA = Guid.NewGuid(), orderB = Guid.NewGuid();
            string batchNumber = $"ME-{day:yyMMdd}-{code}-01";
            await _factory.SeedAsync(db =>
            {
                db.Shift.Add(new Shift { Id = shift, Code = "CA" + code, Name = "Trưa " + code, StartTime = new TimeSpan(10, 30, 0), Status = "Active" });
                db.Dish.Add(new Dish { Id = dish, Code = "MON" + code, Name = "Cơm gà " + code, Status = "Active" });
                db.Menu.Add(new AppDelicare.Model.Entity.Menu { Id = menu, Code = "TD" + code, Name = "Thực đơn " + code, VersionNo = 1, Status = "Published", EffectiveFrom = day.AddDays(-7) });
                db.MenuLine.Add(new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = dish, MealType = "TRUA", DayOfWeek = MenuDay(day), SequenceNo = 1, SortOrder = 1 });
                db.Customer.AddRange(
                    new Customer { Id = customerA, Code = "A" + code, Name = "[DEMO] Khách A " + code, CIF = "A" + code, CustomerType = "FACTORY", DisplayName = "Khách A " + code, Status = "Active" },
                    new Customer { Id = customerB, Code = "B" + code, Name = "[DEMO] Khách B " + code, CIF = "B" + code, CustomerType = "FACTORY", DisplayName = "Khách B " + code, Status = "Active" });
                db.CustomerDeliveryLocation.AddRange(
                    new CustomerDeliveryLocation { Id = locationA, CustomerId = customerA, Code = "LA" + code, Name = "Căng tin A", Address = "12 Cộng Hòa, Quận Tân Bình, TP. HCM", DeliveryWindowFrom = new TimeSpan(11, 0, 0) },
                    new CustomerDeliveryLocation { Id = locationA2, CustomerId = customerA, Code = "LA2" + code, Name = "Xưởng A2", Address = "5 Nguyễn Văn Linh, Quận 7, TP. HCM", DeliveryWindowFrom = new TimeSpan(11, 30, 0) },
                    new CustomerDeliveryLocation { Id = locationB, CustomerId = customerB, Code = "LB" + code, Name = "Căng tin B", Address = "KCN Linh Trung, TP. Thủ Đức", DeliveryWindowFrom = new TimeSpan(11, 15, 0) });
                db.Order.AddRange(
                    new Order { Id = orderA, OrderNumber = "PH-A" + code, OrderNo = "PH-A" + code, CustomerId = customerA, DeliveryLocationId = locationA, OrderDate = day.AddDays(-2), MealDate = day, DeliveryDate = day, Status = "InProduction", TotalQuantity = 60, TotalAmount = 1_800_000 },
                    new Order { Id = orderB, OrderNumber = "PH-B" + code, OrderNo = "PH-B" + code, CustomerId = customerB, DeliveryLocationId = locationB, OrderDate = day.AddDays(-2), MealDate = day, DeliveryDate = day, Status = "Locked", TotalQuantity = 40, TotalAmount = 1_200_000 });
                db.OrderLine.AddRange(
                    new OrderLine { Id = Guid.NewGuid(), OrderId = orderA, MenuId = menu, MealType = "TRUA", ShiftId = shift, Quantity = 60, UnitPrice = 30_000, LineAmount = 1_800_000 },
                    new OrderLine { Id = Guid.NewGuid(), OrderId = orderB, MenuId = menu, MealType = "TRUA", ShiftId = shift, Quantity = 40, UnitPrice = 30_000, LineAmount = 1_200_000 });
                db.ProductionPlan.Add(new ProductionPlan { Id = plan, PlanNumber = "KH-" + code, PlanNo = "KH-" + code, ProductionDate = day, PlanDate = day.AddDays(-1), Status = "Released" });
                db.ProductionOrder.Add(new ProductionOrder { Id = productionOrder, ProductionPlanId = plan, DishId = dish, RecipeId = Guid.NewGuid(), ShiftId = shift, PlannedQty = 100, Status = "InProgress", ProductionOrderNumber = "PH-SX" + code, PlannedStartDate = day.AddHours(7) });
                db.ProductionBatch.Add(new ProductionBatch { Id = batch, ProductionOrderId = productionOrder, DishId = dish, BatchNumber = batchNumber, BatchNo = batchNumber, StartDate = day.AddHours(7),
                    PlannedQuantity = 100, ActualQuantity = 100, AcceptedQty = 100, Status = batchStatus, QcStatus = "PASS" });
                db.ProductionOutput.Add(new ProductionOutput { Id = Guid.NewGuid(), ProductionBatchId = batch, DishId = dish, ProducedQuantity = 100, AcceptedQuantity = 100, QualityStatus = "PASS" });
                db.TemperatureLog.Add(new TemperatureLog { Id = Guid.NewGuid(), ProductionBatchId = batch, MeasurementType = "CORE", TemperatureC = coreTemp, MinAllowedC = 70, IsWithinRange = coreTemp >= 70, MeasuredAt = day.AddHours(8) });
                return Task.CompletedTask;
            });
            return new Kitchen(shift, dish, batch, batchNumber, orderA, orderB, customerA, customerB, locationA, locationA2);
        }

        private static List<JsonElement> Packings(JsonElement batchModel) => [.. batchModel.GetProperty("listPackingOrder").EnumerateArray()];

        [Fact]
        public async Task Packing_orders_are_generated_from_a_passed_batch_and_the_batch_is_packed_when_fully_allocated_and_packed()
        {
            var day = new DateTime(2026, 12, 14);
            _factory.Clock.FixedNow = day.AddHours(9);
            var k = await SeedKitchenAsync("P1", day);
            using var c = _factory.CreateAuthorizedClient();

            // Sinh từ mẻ: 100 phần đạt chia theo suất món của đơn A (60) / B (40).
            var generated = (await c.PostFormAsync("/api/PackingOrder/GenerateFromBatch", ("ProductionBatchId", k.Batch))).Ok();
            generated.GetProperty("acceptedQuantity").GetDecimal().Should().Be(100);
            generated.GetProperty("allocatedQuantity").GetDecimal().Should().Be(100);
            generated.GetProperty("remainingQuantity").GetDecimal().Should().Be(0);
            var list = Packings(generated);
            list.Select(p => (p.GetProperty("orderNumber").GetString(), p.GetProperty("packingOrder").GetProperty("plannedQty").GetDecimal()))
                .Should().BeEquivalentTo([("PH-AP1", 60m), ("PH-BP1", 40m)]);
            list.Should().OnlyContain(p => p.GetProperty("packingOrder").GetProperty("packingOrderNumber").GetString()!.StartsWith("DG-261214-"));
            Guid poA = list.Single(p => p.GetProperty("orderNumber").GetString() == "PH-AP1").GetProperty("packingOrder").GetProperty("id").GetGuid();
            Guid poB = list.Single(p => p.GetProperty("orderNumber").GetString() == "PH-BP1").GetProperty("packingOrder").GetProperty("id").GetGuid();

            // Đã chia hết → sinh lại / lập tay vượt số đạt bị chặn; đóng gói trực tiếp mẻ bị chặn khi còn lệnh chưa xong.
            (await c.PostFormAsync("/api/PackingOrder/GenerateFromBatch", ("ProductionBatchId", k.Batch))).Fail("chia hết");
            (await c.PostFormAsync("/api/PackingOrder/Create", ("ProductionBatchId", k.Batch), ("OrderId", k.OrderA), ("PlannedQty", 1m))).Fail("vượt số phần đạt");
            (await c.PostFormAsync("/api/ProductionBatch/Pack", ("Id", k.Batch))).Fail("PackingOrder/RecordResult");

            // Hủy lệnh B (Kế hoạch) → trả 40 phần về mẻ; lập tay lại 40 cho B.
            (await c.PostFormAsync("/api/PackingOrder/Cancel", ("Id", poB))).Fail("lý do");
            var cancelled = (await c.PostFormAsync("/api/PackingOrder/Cancel", ("Id", poB), ("Reason", "Đổi lệnh"))).Ok();
            cancelled.GetProperty("remainingQuantity").GetDecimal().Should().Be(40);
            var manual = (await c.PostFormAsync("/api/PackingOrder/Create", ("ProductionBatchId", k.Batch), ("OrderId", k.OrderB), ("PlannedQty", 40m))).Ok();
            Guid poB2 = Packings(manual).Single(p => p.GetProperty("packingOrder").GetProperty("status").GetString() == "Planned" && p.GetProperty("orderNumber").GetString() == "PH-BP1")
                .GetProperty("packingOrder").GetProperty("id").GetGuid();

            // A: bắt đầu → kết quả lệch tổng bị chặn → 58 + 1 hỏng + 1 loại = 60 → hao hụt PACKING_DAMAGE 2 suất; mẻ chưa đóng gói (B chưa xong).
            (await c.PostFormAsync("/api/PackingOrder/Start", ("Id", poA))).Ok();
            (await c.PostFormAsync("/api/PackingOrder/Cancel", ("Id", poA), ("Reason", "x"))).Fail("Kế hoạch");
            (await c.PostFormAsync("/api/PackingOrder/RecordResult", ("Id", poA), ("PackedQuantity", 58m))).Fail("phải bằng số phần kế hoạch");
            var resultA = (await c.PostFormAsync("/api/PackingOrder/RecordResult", ("Id", poA), ("PackedQuantity", 58m), ("DamagedQty", 1m), ("RejectedQuantity", 1m),
                ("PackagingType", "HOP_GIU_NHIET"))).Ok();
            resultA.GetProperty("batchStatus").GetString().Should().Be("Passed");
            var packedA = Packings(resultA).Single(p => p.GetProperty("packingOrder").GetProperty("id").GetGuid() == poA);
            packedA.GetProperty("packingOrder").GetProperty("status").GetString().Should().Be("Packed");
            packedA.GetProperty("listResult")[0].GetProperty("acceptedQty").GetDecimal().Should().Be(58);
            var waste = await _factory.QueryAsync(db => db.MealWasteRecord.AsNoTracking().SingleAsync(w => w.ProductionBatchId == k.Batch));
            waste.WasteType.Should().Be("PACKING_DAMAGE");
            waste.QuantityMeals.Should().Be(2);
            waste.RecordedBy.Should().Be(InMemoryApiFactory.UserId);

            // B: kết quả đủ 40 → mẻ chia hết + mọi lệnh đã đóng gói → mẻ Đã đóng gói, lệnh SX hoàn thành.
            var resultB = (await c.PostFormAsync("/api/PackingOrder/RecordResult", ("Id", poB2), ("PackedQuantity", 40m))).Ok();
            resultB.GetProperty("batchStatus").GetString().Should().Be("Packed");
            (await _factory.QueryAsync(db => db.ProductionOrder.AsNoTracking().SingleAsync(o => o.Id == db.ProductionBatch.Single(b => b.Id == k.Batch).ProductionOrderId))).Status.Should().Be("Closed", "UI-6.24: lệnh Hoàn thành tự Đóng");
            resultB.GetProperty("listWarning").EnumerateArray().Select(w => w.GetString()).Should().Contain(w => w!.Contains("tự đóng"));
            (await c.PostFormAsync("/api/PackingOrder/RecordResult", ("Id", poB2), ("PackedQuantity", 40m))).Fail("Đã đóng gói");

            // Danh sách theo ngày đóng gói / đơn; CRUD cũ không ghi được.
            var byOrder = await c.GetResultAsync($"/api/PackingOrder/GetList?OrderId={k.OrderB}");
            byOrder.ListData.EnumerateArray().Select(p => p.GetProperty("packingOrder").GetProperty("status").GetString()).Should().BeEquivalentTo(["Cancelled", "Packed"]);
            (await c.PostFormAsync("/api/PackingOrder/Insert", ("ProductionBatchId", k.Batch), ("PackingDate", day))).Fail("PackingOrder/GenerateFromBatch");
            (await c.PostFormAsync("/api/PackingResult/Insert", ("PackingOrderId", poA))).Fail("PackingOrder/RecordResult");
        }

        /// <summary>UI-6.34 (D-115): ô chờ khi món chưa đủ mẻ đạt; đủ → ghi hàng loạt mọi lệnh theo mẻ, ghi lại không nhân đôi; mẻ nấu thêm → món quay lại chờ.</summary>
        [Fact]
        public async Task Packing_board_cells_wait_for_all_batches_and_record_many_is_idempotent()
        {
            var day = new DateTime(2026, 12, 28);
            _factory.Clock.FixedNow = day.AddHours(9);
            var k = await SeedKitchenAsync("U34", day, batchStatus: "QCPending");
            using var c = _factory.CreateAuthorizedClient();
            object Cell(Guid order, decimal? damaged = null) => new { orderId = order, dishId = k.Dish, damaged, reason = damaged > 0 ? "Đổ khi chia" : null };
            string Status(System.Text.Json.JsonElement b, Guid order) => b.GetProperty("listRow").EnumerateArray().Single(r => r.GetProperty("orderId").GetGuid() == order).GetProperty("listCell")[0].GetProperty("status").GetString()!;

            var board = (await c.GetResultAsync($"/api/PackingOrder/GetBoard?date={day:yyyy-MM-dd}&shiftId={k.Shift}")).Ok();
            (Status(board, k.OrderA), board.GetProperty("listDish")[0].GetProperty("ready").GetBoolean()).Should().Be(("WAITING", false));
            (await PostJson(c, "/api/PackingOrder/RecordResultMany", new { date = day, shiftId = k.Shift, listCell = new[] { Cell(k.OrderA) } })).Fail("chưa đủ mẻ");

            await _factory.SeedAsync(async db => { (await db.ProductionBatch.SingleAsync(b => b.Id == k.Batch)).Status = "Passed"; });
            (await c.PostFormAsync("/api/PackingOrder/GenerateFromBatch", ("ProductionBatchId", k.Batch))).Ok();
            board = (await c.GetResultAsync($"/api/PackingOrder/GetBoard?date={day:yyyy-MM-dd}&shiftId={k.Shift}")).Ok();
            (Status(board, k.OrderA), Status(board, k.OrderB)).Should().Be(("READY", "READY"));

            var many = (await PostJson(c, "/api/PackingOrder/RecordResultMany", new { date = day, shiftId = k.Shift, listCell = new[] { Cell(k.OrderA, 2m) } })).Ok();
            (many.GetProperty("cells").GetInt32(), many.GetProperty("orders").GetInt32()).Should().Be((1, 1));
            (await PostJson(c, "/api/PackingOrder/RecordResultMany", new { date = day, shiftId = k.Shift, listCell = new[] { Cell(k.OrderA) } })).Ok().GetProperty("cells").GetInt32().Should().Be(0, "ô đã chia – không nhân đôi");
            (await _factory.QueryAsync(db => (from r in db.PackingResult join p in db.PackingOrder on r.PackingOrderId equals p.Id where p.OrderId == k.OrderA select r).CountAsync())).Should().Be(1);
            board = (await c.GetResultAsync($"/api/PackingOrder/GetBoard?date={day:yyyy-MM-dd}&shiftId={k.Shift}")).Ok();
            Status(board, k.OrderA).Should().Be("DAMAGED");
            board.GetProperty("listRow").EnumerateArray().Single(r => r.GetProperty("orderId").GetGuid() == k.OrderA).GetProperty("complete").GetBoolean().Should().BeTrue();
            board.GetProperty("damagedTotal").GetDecimal().Should().Be(2m);

            // Mẻ nấu thêm cùng món × ca chưa đạt → món quay lại chờ (ô chưa chia của đơn B chờ; ô đã chia giữ).
            await _factory.SeedAsync(async db =>
            {
                var b0 = await db.ProductionBatch.AsNoTracking().SingleAsync(b => b.Id == k.Batch);
                db.ProductionBatch.Add(new ProductionBatch { Id = Guid.NewGuid(), ProductionOrderId = b0.ProductionOrderId, DishId = k.Dish, BatchNumber = "ME-U34-02", BatchNo = "ME-U34-02", StartDate = day.AddHours(10), PlannedQuantity = 10, Status = "Planned" });
            });
            board = (await c.GetResultAsync($"/api/PackingOrder/GetBoard?date={day:yyyy-MM-dd}&shiftId={k.Shift}")).Ok();
            (Status(board, k.OrderA), Status(board, k.OrderB)).Should().Be(("DAMAGED", "WAITING"));
        }

        [Fact]
        public async Task Packing_is_blocked_for_a_batch_not_passed_or_below_core_temperature()
        {
            var day = new DateTime(2026, 12, 15);
            _factory.Clock.FixedNow = day.AddHours(9);
            var cold = await SeedKitchenAsync("P2", day, coreTemp: 65m);
            var pending = await SeedKitchenAsync("P3", day, batchStatus: "QCPending");
            using var c = _factory.CreateAuthorizedClient();
            (await c.PostFormAsync("/api/PackingOrder/GenerateFromBatch", ("ProductionBatchId", cold.Batch))).Fail("65 °C");
            (await c.PostFormAsync("/api/PackingOrder/GenerateFromBatch", ("ProductionBatchId", pending.Batch))).Fail("Chỉ lập lệnh đóng gói từ mẻ Đạt QC");
            (await c.PostFormAsync("/api/PackingOrder/Create", ("ProductionBatchId", pending.Batch), ("OrderId", pending.OrderA), ("PlannedQty", 10m))).Fail("Đạt QC");
        }

        private async Task<(Guid VehicleA, Guid VehicleB, Guid Driver)> SeedVehiclesAsync(string code, decimal capacityA, decimal capacityB)
        {
            Guid va = Guid.NewGuid(), vb = Guid.NewGuid(), driver = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Vehicle.AddRange(
                    new Vehicle { Id = va, Code = "XA" + code, LicensePlate = "51C-A" + code, Capacity = capacityA, Status = "Active" },
                    new Vehicle { Id = vb, Code = "XB" + code, LicensePlate = "51C-B" + code, Capacity = capacityB, Status = "Active" });
                db.Employee.Add(new Employee { Id = driver, Code = "TX" + code, FullName = "Tài xế " + code, Status = "Active" });
                return Task.CompletedTask;
            });
            return (va, vb, driver);
        }

        [Fact]
        public async Task Manual_delivery_orders_split_assign_move_cancel_and_load_dispatch_with_executions()
        {
            var day = new DateTime(2026, 12, 16);
            _factory.Clock.FixedNow = day.AddHours(9);
            var k = await SeedKitchenAsync("D1", day, batchStatus: "Packed");
            var (va, vb, driver) = await SeedVehiclesAsync("D1", 200, 200);
            await _factory.SeedAsync(db =>
            {
                var item = db.FoodSafetyChecklistItem.FirstOrDefault(i => i.ItemCode == "SAMPLE_RETAINED");
                if (item == null)
                {
                    item = new FoodSafetyChecklistItem { Id = Guid.NewGuid(), ChecklistId = Guid.NewGuid(), ItemCode = "SAMPLE_RETAINED", ItemName = "Lưu mẫu", SortOrder = 1, IsCritical = true, IsRequired = true };
                    db.FoodSafetyChecklistItem.Add(item);
                }
                db.FoodSafetyChecklistResult.Add(new FoodSafetyChecklistResult { Id = Guid.NewGuid(), ChecklistId = item.ChecklistId, ChecklistItemId = item.Id, CheckDate = day, ProductionBatchId = k.Batch, ResultCode = "PASS", CheckedAt = day.AddHours(8) });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();

            // Đơn A 60 suất: lập tay 25 suất tới điểm A2 (Quận 7); vượt số còn lại → chặn; điểm giao của khách khác → chặn; đơn Đã chốt (B) lập được.
            var created = (await PostJson(c, "/api/DeliveryOrder/Create", new { orderId = k.OrderA, customerDeliveryLocationId = k.LocationB2, mealQuantity = 25m })).Ok();
            var do1 = created.GetProperty("listDeliveryOrder")[0];
            Guid do1Id = do1.GetProperty("deliveryOrder").GetProperty("id").GetGuid();
            do1.GetProperty("mealQuantity").GetDecimal().Should().Be(25);
            do1.GetProperty("locationName").GetString().Should().Be("Xưởng A2");
            do1.GetProperty("area").GetString().Should().Be("Quận 7");
            do1.GetProperty("deliveryOrder").GetProperty("deliveryNumber").GetString().Should().MatchRegex(@"^GH-261216-\d{4}$");
            do1.GetProperty("allowedActions").EnumerateArray().Select(a => a.GetString()).Should().BeEquivalentTo(["split", "assignRoute", "cancel"]);
            created.GetProperty("listWarning")[0].GetString().Should().Contain("còn 35 suất");
            (await PostJson(c, "/api/DeliveryOrder/Create", new { orderId = k.OrderA, mealQuantity = 36m })).Fail("còn 35 suất");
            (await PostJson(c, "/api/DeliveryOrder/Create", new { orderId = k.OrderA, customerDeliveryLocationId = Guid.NewGuid(), mealQuantity = 5m })).Fail("Điểm giao");

            // Tách 25 → 15 + 10 (phần sau là đơn giao mới chưa gán chuyến); tổng sai → chặn.
            (await PostJson(c, "/api/DeliveryOrder/Split", new { id = do1Id, listPart = new[] { new { mealQuantity = 20m }, new { mealQuantity = 10m } } })).Fail("phải bằng số suất");
            var split = (await PostJson(c, "/api/DeliveryOrder/Split", new { id = do1Id, listPart = new[] { new { mealQuantity = 15m }, new { mealQuantity = 10m } } })).Ok();
            var parts = split.GetProperty("listDeliveryOrder").EnumerateArray().ToList();
            parts.Select(p => p.GetProperty("mealQuantity").GetDecimal()).Should().BeEquivalentTo([15m, 10m]);
            Guid do2Id = parts.Single(p => p.GetProperty("mealQuantity").GetDecimal() == 10).GetProperty("deliveryOrder").GetProperty("id").GetGuid();

            // Chuyến xe A: phần còn lại của đơn A (35) + đơn giao do1 (15); điểm theo giờ giao: A 11:00 (#1), A2 11:30 (#2).
            var route = (await PostJson(c, "/api/DeliveryRoute/Create", new { deliveryDate = day, vehicleId = va, driverId = driver, listOrderId = new[] { k.OrderA }, listDeliveryOrderId = new[] { do1Id } })).Ok();
            Guid routeId = route.GetProperty("route").GetProperty("id").GetGuid();
            route.GetProperty("listStop").EnumerateArray().Select(s => (s.GetProperty("sequenceNo").GetInt32(), s.GetProperty("plannedMealQty").GetDecimal()))
                .Should().Equal((1, 35m), (2, 15m));
            route.GetProperty("allowedActions").EnumerateArray().Select(a => a.GetString()).Should().Contain(["load", "updateStops"]).And.NotContain("dispatch");
            // Đơn A hết suất chờ xếp; đơn giao do2 vẫn chưa gán chuyến.
            (await c.GetResultAsync($"/api/DeliveryRoute/GetReadyOrders?DeliveryDate={day:yyyy-MM-dd}")).ListData.EnumerateArray()
                .Select(o => o.GetProperty("orderId").GetGuid()).Should().NotContain(k.OrderA);
            var unassigned = await c.GetResultAsync($"/api/DeliveryOrder/GetList?DeliveryDate={day:yyyy-MM-dd}&Unassigned=true");
            unassigned.ListData.EnumerateArray().Select(d => d.GetProperty("deliveryOrder").GetProperty("id").GetGuid()).Should().Equal(do2Id);

            // Gán do2 vào đầu chuyến A (thứ tự 1) → các điểm cũ dời xuống; đổi thứ tự; chuyến B rồi chuyển do2 sang chuyến B.
            var assigned = (await c.PostFormAsync("/api/DeliveryOrder/AssignRoute", ("Id", do2Id), ("DeliveryRouteId", routeId), ("SequenceNo", 1))).Ok();
            assigned.GetProperty("listRoute")[0].GetProperty("listStop").EnumerateArray().Select(s => (s.GetProperty("sequenceNo").GetInt32(), s.GetProperty("plannedMealQty").GetDecimal()))
                .Should().Equal((1, 10m), (2, 35m), (3, 15m));
            var reordered = (await PutJson(c, "/api/DeliveryRoute/UpdateStops", new { id = routeId, listStop = new[] { new { deliveryOrderId = do2Id, sequenceNo = 3 }, new { deliveryOrderId = do1Id, sequenceNo = 1 } } })).Ok();
            reordered.GetProperty("listStop").EnumerateArray().Select(s => (s.GetProperty("sequenceNo").GetInt32(), s.GetProperty("plannedMealQty").GetDecimal()))
                .Should().Equal((1, 15m), (2, 35m), (3, 10m));
            (await PutJson(c, "/api/DeliveryRoute/UpdateStops", new { id = routeId, listStop = new[] { new { deliveryOrderId = do2Id, sequenceNo = 2 } } })).Fail("không trùng");

            // Đơn B (Đã chốt) lập đơn giao toàn bộ 40 suất → chuyến xe B; chuyển do2 từ chuyến A sang chuyến B (chuyến A đánh số lại).
            var doB = (await PostJson(c, "/api/DeliveryOrder/Create", new { orderId = k.OrderB })).Ok().GetProperty("listDeliveryOrder")[0];
            doB.GetProperty("mealQuantity").GetDecimal().Should().Be(40);
            Guid doBId = doB.GetProperty("deliveryOrder").GetProperty("id").GetGuid();
            var routeB = (await PostJson(c, "/api/DeliveryRoute/Create", new { deliveryDate = day, vehicleId = vb, listDeliveryOrderId = new[] { doBId } })).Ok();
            Guid routeBId = routeB.GetProperty("route").GetProperty("id").GetGuid();
            var moved = (await c.PostFormAsync("/api/DeliveryOrder/AssignRoute", ("Id", do2Id), ("DeliveryRouteId", routeBId))).Ok();
            var movedRoutes = moved.GetProperty("listRoute").EnumerateArray().ToDictionary(r => r.GetProperty("route").GetProperty("id").GetGuid());
            movedRoutes[routeId].GetProperty("listStop").EnumerateArray().Select(s => (s.GetProperty("sequenceNo").GetInt32(), s.GetProperty("plannedMealQty").GetDecimal()))
                .Should().Equal((1, 15m), (2, 35m));
            movedRoutes[routeBId].GetProperty("listStop").EnumerateArray().Select(s => (s.GetProperty("sequenceNo").GetInt32(), s.GetProperty("plannedMealQty").GetDecimal()))
                .Should().Equal((1, 40m), (2, 10m));
            (await _factory.QueryAsync(db => db.DeliveryStop.CountAsync(s => s.DeliveryOrderId == do2Id && s.IsDeleted))).Should().Be(1, "điểm cũ trên chuyến A xóa mềm");

            // Hủy đơn giao B (lý do bắt buộc) → gỡ khỏi chuyến B, suất trả lại đơn B.
            (await c.PostFormAsync("/api/DeliveryOrder/Cancel", ("Id", doBId))).Fail("lý do");
            var cancelled = (await c.PostFormAsync("/api/DeliveryOrder/Cancel", ("Id", doBId), ("Reason", "Khách đổi giờ"))).Ok();
            cancelled.GetProperty("listDeliveryOrder")[0].GetProperty("deliveryOrder").GetProperty("status").GetString().Should().Be("Cancelled");
            cancelled.GetProperty("listRoute")[0].GetProperty("listStop").EnumerateArray().Select(s => s.GetProperty("sequenceNo").GetInt32()).Should().Equal(1);
            (await PostJson(c, "/api/DeliveryOrder/Create", new { orderId = k.OrderB, mealQuantity = 40m })).Ok();

            // Chuyến A: chưa xếp hàng → xuất phát bị chặn; xếp hàng (đơn A xếp 34 / 35 → cảnh báo) → Đang xếp, thực hiện giao LOADED.
            (await c.PostFormAsync("/api/DeliveryRoute/RecordTemperature", ("Id", routeId), ("TemperatureC", 66m))).Ok();
            (await c.PostFormAsync("/api/DeliveryRoute/Dispatch", ("Id", routeId))).Fail("Chưa xếp hàng lên xe");
            Guid doA = route.GetProperty("listStop")[0].GetProperty("deliveryOrderId").GetGuid();
            // UI-6.34 (D-115): chưa chia đủ thực đơn → chặn; xếp có điều kiện cần lý do (quyền Duyệt Điều phối).
            (await PostJson(c, "/api/DeliveryRoute/Load", new { id = routeId, employeeId = driver, listItem = new[] { new { deliveryOrderId = doA, loadedQty = 34m } } })).Fail("Chưa đủ thực đơn");
            var loaded = (await PostJson(c, "/api/DeliveryRoute/Load", new { id = routeId, employeeId = driver, overrideReason = "Thử luồng giao tay", listItem = new[] { new { deliveryOrderId = doA, loadedQty = 34m } } })).Ok();
            loaded.GetProperty("route").GetProperty("route").GetProperty("status").GetString().Should().Be("Loading");
            loaded.GetProperty("route").GetProperty("listWarning").EnumerateArray().Select(w => w.GetString()).Should().Contain(w => w!.Contains("xếp 34 / 35"));
            loaded.GetProperty("listLoading").EnumerateArray().Single().GetProperty("vehicleCode").GetString().Should().Be("XAD1");
            (await c.PostFormAsync("/api/DeliveryRoute/Dispatch", ("Id", routeId))).Fail("Chưa xếp hàng lên xe (DeliveryRoute/Load): " + route.GetProperty("listStop")[1].GetProperty("deliveryNumber").GetString());
            (await c.PostFormAsync("/api/DeliveryOrder/AssignRoute", ("Id", do2Id), ("DeliveryRouteId", routeId))).Fail("chỉ gán");
            (await PostJson(c, "/api/DeliveryRoute/Load", new { id = routeId, overrideReason = "Thử luồng giao tay" })).Ok();
            (await c.PostFormAsync("/api/DeliveryOrder/Cancel", ("Id", do1Id), ("Reason", "x"))).Fail("đã xếp lên xe");

            // Xuất phát → thực hiện giao DISPATCHED; bảng thực hiện giao của ngày.
            (await c.PostFormAsync("/api/DeliveryRoute/Dispatch", ("Id", routeId))).Ok().GetProperty("route").GetProperty("status").GetString().Should().Be("InTransit");
            var board = (await c.GetResultAsync($"/api/DeliveryExecution/GetBoard?DeliveryDate={day:yyyy-MM-dd}")).Ok();
            var items = board.GetProperty("listItem").EnumerateArray().ToList();
            items.Where(i => i.GetProperty("deliveryRouteId").ValueKind != JsonValueKind.Null && i.GetProperty("deliveryRouteId").GetGuid() == routeId)
                .Select(i => i.GetProperty("status").GetString()).Should().OnlyContain(s => s == "DISPATCHED");
            items.Single(i => i.GetProperty("deliveryOrderId").GetGuid() == do2Id).GetProperty("status").GetString().Should().Be("READY");
            items.Single(i => i.GetProperty("deliveryOrderId").GetGuid() == doA).GetProperty("loadedQty").GetDecimal().Should().Be(34);
            board.GetProperty("statusCounts").GetProperty("DISPATCHED").GetInt32().Should().Be(2);
            board.GetProperty("statusCounts").GetProperty("READY").GetInt32().Should().Be(2, "do2 trên chuyến B + đơn giao mới của đơn B");
            items.Should().NotContain(i => i.GetProperty("deliveryOrderId").GetGuid() == doBId, "đơn giao Hủy không hiện");

            // Đến điểm #1 → ARRIVED.
            Guid stop1 = route.GetProperty("listStop")[0].GetProperty("id").GetGuid();
            (await c.PostFormAsync("/api/DeliveryStop/Arrive", ("Id", stop1))).Ok();
            (await _factory.QueryAsync(db => db.DeliveryExecution.AsNoTracking().SingleAsync(e => e.DeliveryOrderId == doA))).Status.Should().Be("ARRIVED");
            // CRUD cũ của xếp hàng / thực hiện giao / đơn giao không ghi được.
            (await c.PostFormAsync("/api/DeliveryLoading/Insert", ("DeliveryRouteId", routeId), ("DeliveryOrderId", doA))).Fail("DeliveryRoute/Load");
            (await c.PostFormAsync("/api/DeliveryExecution/Insert", ("DeliveryOrderId", doA))).Fail("Thực hiện giao");
            (await c.PostFormAsync("/api/DeliveryOrder/Insert", ("OrderId", k.OrderA))).Fail("DeliveryOrder/Create");
        }

        [Fact]
        public async Task Plan_creates_one_route_per_vehicle_grouping_stops_by_area_and_respecting_capacity()
        {
            var day = new DateTime(2026, 12, 17);
            _factory.Clock.FixedNow = day.AddHours(8);
            var k = await SeedKitchenAsync("R1", day);
            // Đơn B → Đang SX để vào lập chuyến; thêm đơn C (Quận Tân Bình, cùng khu với A) 30 suất.
            Guid orderC = Guid.NewGuid(), customerC = Guid.NewGuid(), locationC = Guid.NewGuid();
            await _factory.SeedAsync(async db =>
            {
                var b = await db.Order.SingleAsync(o => o.Id == k.OrderB);
                b.Status = "InProduction";
                db.Customer.Add(new Customer { Id = customerC, Code = "CR1", Name = "[DEMO] Khách C", CIF = "CR1", CustomerType = "FACTORY", DisplayName = "Khách C", Status = "Active" });
                db.CustomerDeliveryLocation.Add(new CustomerDeliveryLocation { Id = locationC, CustomerId = customerC, Code = "LCR1", Name = "Căng tin C", Address = "99 Hoàng Văn Thụ, Quận Tân Bình, TP. HCM", DeliveryWindowFrom = new TimeSpan(10, 45, 0) });
                db.Order.Add(new Order { Id = orderC, OrderNumber = "PH-CR1", OrderNo = "PH-CR1", CustomerId = customerC, DeliveryLocationId = locationC, OrderDate = day.AddDays(-2), MealDate = day, DeliveryDate = day, Status = "InProduction", TotalQuantity = 30 });
                db.OrderLine.Add(new OrderLine { Id = Guid.NewGuid(), OrderId = orderC, MealType = "TRUA", ShiftId = k.Shift, Quantity = 30, LineAmount = 0 });
            });
            var (va, vb, driver) = await SeedVehiclesAsync("R1", 100, 50);
            using var c = _factory.CreateAuthorizedClient();

            (await PostJson(c, "/api/DeliveryRoute/Plan", new { deliveryDate = day, listVehicle = Array.Empty<object>() })).Fail("ít nhất 1 xe");
            var plan = (await PostJson(c, "/api/DeliveryRoute/Plan", new { deliveryDate = day, listVehicle = new object[] { new { vehicleId = va, driverId = driver }, new { vehicleId = vb } } })).Ok();
            // Khu Quận Tân Bình (A 60 + C 30 = 90) → xe A (100); khu Thủ Đức (B 40) → xe B (50).
            var areas = plan.GetProperty("listArea").EnumerateArray().ToDictionary(a => a.GetProperty("area").GetString()!, a => a.GetProperty("listVehicleCode").EnumerateArray().Select(x => x.GetString()).ToList());
            areas["Quận Tân Bình"].Should().Equal("XAR1");
            areas["TP. Thủ Đức"].Should().Equal("XBR1");
            var routes = plan.GetProperty("listRoute").EnumerateArray().ToDictionary(r => r.GetProperty("vehicleCode").GetString()!);
            routes["XAR1"].GetProperty("listStop").EnumerateArray().Select(s => (s.GetProperty("orderNumber").GetString(), s.GetProperty("sequenceNo").GetInt32()))
                .Should().Equal(("PH-CR1", 1), ("PH-AR1", 2));
            routes["XAR1"].GetProperty("route").GetProperty("routeNo").GetString().Should().Be("CX-261217-XAR1-01");
            routes["XAR1"].GetProperty("driverName").GetString().Should().Be("Tài xế R1");
            routes["XBR1"].GetProperty("loadMeals").GetDecimal().Should().Be(40);
            // Hết hàng chờ xếp → lập lại bị chặn.
            (await PostJson(c, "/api/DeliveryRoute/Plan", new { deliveryDate = day, listVehicle = new object[] { new { vehicleId = va } } })).Fail("cần xếp chuyến");
        }
    }
}
