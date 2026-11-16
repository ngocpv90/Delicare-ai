using System.Text.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Services.Implement;
using AppDelicare.Services.Interface;
using AppDelicare.Tests.Integration;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Production
{
    /// <summary>
    /// T17 (Pilot P7) – sản xuất theo mẻ: kế hoạch KH- → duyệt → phát hành (lệnh SX PH- + mẻ ME-, lịch ngược từ giờ xuất bếp) → xuất NL cho mẻ (FEFO, chặn lô hết hạn /
    /// KHONG_DAT) → bắt đầu → nhiệt độ lõi (≥ 70 °C) → lưu mẫu → xong → sản lượng → đạt QC → đóng gói.
    /// Hôm nay (đồng hồ test) = 20/12/2026 10:00, ngày sản xuất 21/12/2026 (thứ 2).
    /// <para>Bộ số tính tay: điểm giao nhận lúc 11:30 → giờ xuất bếp 11:30 − 60′ = 10:30 → mẻ cuối xong 10:30 − 30′ (chia suất / đóng gói) = 10:00.
    /// Dây chuyền 100 phần / mẻ, 60 phút / mẻ. Thịt kho 250 phần → 3 mẻ 100 / 100 / 50: 07:00–08:00, 08:00–09:00, 09:00–10:00; Canh 80 phần → 1 mẻ 09:00–10:00.
    /// Số mẻ theo giờ bắt đầu rồi tên món: -01 Thịt#1, -02 Thịt#2, -03 Canh, -04 Thịt#3.</para>
    /// <para>Định lượng: Thịt 12 kg / 100 suất, hao hụt 10% → mẻ 100 phần cần 13,2 kg; mẻ 50 phần 6,6 kg. Rau 5 kg / 100 suất → mẻ 80 phần 4 kg.
    /// Lô thịt: L1 HSD 22/12 10 kg, L2 HSD 30/12 20 kg, LX hết hạn 19/12 50 kg, LK KHONG_DAT 50 kg → mẻ -01 lấy L1 10 + L2 3,2 (FEFO).</para>
    /// </summary>
    public class ProductionTests : IClassFixture<InMemoryApiFactory>
    {
        private static readonly DateTime Today = new(2026, 12, 20);
        private static readonly DateTime Day = Today.AddDays(1);
        private readonly InMemoryApiFactory _factory;

        public ProductionTests(InMemoryApiFactory factory)
        {
            _factory = factory;
            _factory.Clock.FixedNow = Today.AddHours(10);
        }

        private sealed record Scenario(string Shift, Guid ShiftId, Guid Line, Guid Warehouse, Guid Kg, Guid Meat, Guid Veg, Guid DishMeat, Guid DishSoup,
            Guid Plan, Guid Order, Dictionary<string, Guid> Lots);

        private async Task<Scenario> SeedAsync(string code)
        {
            Guid shift = Guid.NewGuid(), line = Guid.NewGuid(), warehouse = Guid.NewGuid(), kg = Guid.NewGuid(), meat = Guid.NewGuid(), veg = Guid.NewGuid();
            Guid dishMeat = Guid.NewGuid(), dishSoup = Guid.NewGuid(), plan = Guid.NewGuid(), customer = Guid.NewGuid(), location = Guid.NewGuid(), order = Guid.NewGuid();
            string shiftCode = "TR" + code;
            var lots = new Dictionary<string, Guid> { ["L1"] = Guid.NewGuid(), ["L2"] = Guid.NewGuid(), ["LX"] = Guid.NewGuid(), ["LK"] = Guid.NewGuid(), ["R1"] = Guid.NewGuid() };
            await _factory.SeedAsync(async db =>
            {
                db.Shift.Add(new Shift { Id = shift, Code = shiftCode, Name = "Trưa " + code, StartTime = new TimeSpan(10, 30, 0), EndTime = new TimeSpan(13, 30, 0), Status = "Active" });
                db.ProductionLine.Add(new ProductionLine { Id = line, Code = "DC-" + code, Name = "Bếp nóng " + code, CapacityMealPerBatch = 100, MinutesPerBatch = 60, Status = "Active" });
                db.Warehouse.Add(new Warehouse { Id = warehouse, Code = code, Name = "Kho " + code, Status = "Active" });
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
                db.ProductionPlan.Add(new ProductionPlan { Id = plan, PlanNumber = "KH-" + code, PlanNo = "KH-" + code, ProductionDate = Day, PlanDate = Today, Status = "Draft" });
                db.ProductionPlanLine.AddRange(
                    new ProductionPlanLine { Id = Guid.NewGuid(), ProductionPlanId = plan, DishId = dishMeat, PlannedQuantity = 250, PlannedQty = 250, Status = "Draft" },
                    new ProductionPlanLine { Id = Guid.NewGuid(), ProductionPlanId = plan, DishId = dishSoup, PlannedQuantity = 80, PlannedQty = 80, Status = "Draft" });
                db.Customer.Add(new Customer { Id = customer, Code = "KH-" + code, Name = "[DEMO] Khách " + code, CIF = "C" + code, CustomerType = "FACTORY", DisplayName = "Khách " + code, Status = "Active" });
                db.CustomerDeliveryLocation.Add(new CustomerDeliveryLocation { Id = location, CustomerId = customer, Code = "DG-" + code, Name = "Điểm giao " + code, DeliveryWindowFrom = new TimeSpan(11, 30, 0) });
                db.Order.Add(new Order { Id = order, OrderNumber = "PH-" + code, OrderNo = "PH-" + code, CustomerId = customer, DeliveryLocationId = location, OrderDate = Today, MealDate = Day, DeliveryDate = Day, Status = "Locked" });
                db.OrderLine.Add(new OrderLine { Id = Guid.NewGuid(), OrderId = order, MealType = "TRUA", ShiftId = shift, Quantity = 330 });

                var ledger = new InventoryLedgerService(db);
                foreach (var (name, ingredient, expiry, qc, qty) in new[]
                {
                    ("L1", meat, Day.AddDays(1), "DAT", 10m), ("L2", meat, Day.AddDays(9), "DAT", 20m), ("LX", meat, Today.AddDays(-1), "DAT", 50m),
                    ("LK", meat, Day.AddDays(10), "KHONG_DAT", 50m), ("R1", veg, Day.AddDays(2), "DAT", 10m),
                })
                {
                    db.MaterialLot.Add(new MaterialLot
                    {
                        Id = lots[name], IngredientId = ingredient, LotNumber = name + "-" + code, LotNo = name + "-" + code, ExpiryDate = expiry,
                        ReceivedDate = Today.AddDays(-3), QcStatus = qc, QualityStatus = qc, Status = "Available", UnitCost = 1000,
                    });
                    await ledger.PostAsync(new InventoryPostRequest
                    {
                        TransactionType = "RECEIPT", TransactionNumber = "NK-" + name + code, WarehouseId = warehouse, IngredientId = ingredient,
                        MaterialLotId = lots[name], UnitId = kg, QuantityIn = qty, UnitCost = 1000,
                    });
                }
                if (!await db.FoodSafetyChecklistItem.AnyAsync(i => i.ItemCode == "SAMPLE_RETAINED"))
                {
                    Guid checklist = Guid.NewGuid();
                    db.FoodSafetyChecklist.Add(new FoodSafetyChecklist { Id = checklist, Code = "PRD_BATCH", Name = "Kiểm soát mẻ nấu", VersionNo = 1, Status = "Active" });
                    db.FoodSafetyChecklistItem.Add(new FoodSafetyChecklistItem { Id = Guid.NewGuid(), ChecklistId = checklist, ItemCode = "SAMPLE_RETAINED", ItemName = "Lưu mẫu", SortOrder = 1, IsCritical = true, IsRequired = true });
                }
            });
            return new Scenario(shiftCode, shift, line, warehouse, kg, meat, veg, dishMeat, dishSoup, plan, order, lots);
        }

        /// <summary>Duyệt + phát hành kế hoạch trên dây chuyền 100 phần / 60′ của kịch bản → danh sách mẻ (theo giờ bắt đầu).</summary>
        private async Task<List<JsonElement>> ReleaseAsync(HttpClient c, Scenario s)
        {
            (await c.PostFormAsync("/api/ProductionPlan/Approve", ("Id", s.Plan))).Ok();
            var detail = (await c.PostFormAsync("/api/ProductionPlan/Release", ("Id", s.Plan), ("ShiftId", s.ShiftId), ("ProductionLineId", s.Line))).Ok();
            return [.. detail.GetProperty("listBatch").EnumerateArray()];
        }

        private static Guid Id(JsonElement e) => e.GetProperty("id").GetGuid();

        private Task<ProductionBatch> BatchAsync(Guid id) => _factory.QueryAsync(db => db.ProductionBatch.AsNoTracking().SingleAsync(b => b.Id == id));

        [Fact]
        public void Schedule_is_backward_from_cook_end_with_full_batches_first()
        {
            var end = Day.AddHours(10);
            var batches = ProductionRules.Schedule(250, 100, 60, end);
            batches.Select(b => (b.Quantity, b.Start.ToString("HH:mm"), b.End.ToString("HH:mm"))).Should().Equal(
                (100m, "07:00", "08:00"), (100m, "08:00", "09:00"), (50m, "09:00", "10:00"));
            ProductionRules.Schedule(80, null, 45, end).Should().ContainSingle()
                .Which.Should().Be(new ProductionRules.ScheduledBatch(1, 80, Day.AddHours(9).AddMinutes(15), end));
            ProductionRules.Increment("ME-261221-TRUA-09", 3).Should().Be("ME-261221-TRUA-12");
        }

        [Fact]
        public async Task Release_plans_backward_from_dispatch_time_with_me_numbers_and_puts_orders_in_production()
        {
            var s = await SeedAsync("P1");
            using var c = _factory.CreateAuthorizedClient();
            // Nháp → chưa phát hành được; dòng kế hoạch sửa được khi Nháp.
            (await c.PostFormAsync("/api/ProductionPlan/Release", ("Id", s.Plan))).Fail("Đã duyệt");
            var batches = await ReleaseAsync(c, s);

            batches.Should().HaveCount(4);
            string prefix = $"ME-261221-{s.Shift}-";
            batches.Select(b => (b.GetProperty("batchNumber").GetString(), b.GetProperty("dishId").GetGuid(), b.GetProperty("plannedQuantity").GetDecimal(),
                    b.GetProperty("plannedStart").GetDateTime().ToString("HH:mm"), b.GetProperty("plannedEnd").GetDateTime().ToString("HH:mm")))
                .Should().Equal(
                    (prefix + "01", s.DishMeat, 100m, "07:00", "08:00"),
                    (prefix + "02", s.DishMeat, 100m, "08:00", "09:00"),
                    (prefix + "03", s.DishSoup, 80m, "09:00", "10:00"),
                    (prefix + "04", s.DishMeat, 50m, "09:00", "10:00"));
            batches.Should().OnlyContain(b => b.GetProperty("status").GetString() == "Planned");

            var detail = (await c.GetResultAsync($"/api/ProductionPlan/GetDetail?id={s.Plan}")).Ok();
            detail.GetProperty("productionPlan").GetProperty("status").GetString().Should().Be("Released");
            detail.GetProperty("dispatchAt").GetDateTime().Should().Be(Day.AddHours(10).AddMinutes(30), "11:30 − 60′ vận chuyển");
            var orders = detail.GetProperty("listOrder").EnumerateArray().ToList();
            orders.Should().HaveCount(2);
            orders.Should().OnlyContain(o => o.GetProperty("productionOrderNumber").GetString()!.StartsWith("PH-261220-") && o.GetProperty("status").GetString() == "Released");
            var meatOrder = await _factory.QueryAsync(db => db.ProductionOrder.AsNoTracking().SingleAsync(o => o.ProductionPlanId == s.Plan && o.DishId == s.DishMeat));
            meatOrder.PlannedQty.Should().Be(250);
            meatOrder.PlannedStartDate.Should().Be(Day.AddHours(7));
            meatOrder.EndTime.Should().Be(Day.AddHours(10));
            meatOrder.ShiftId.Should().Be(s.ShiftId);
            meatOrder.ProductionLineId.Should().Be(s.Line);
            meatOrder.RecipeSnapshotJson.Should().Contain(s.Meat.ToString());
            meatOrder.ProductionOrderNumber.Should().Be(meatOrder.ProductionOrderNo);
            var batch = await BatchAsync(Id(batches[0]));
            batch.BatchNo.Should().Be(batch.BatchNumber);
            batch.PlannedQty.Should().Be(100);
            (await _factory.QueryAsync(db => db.Order.AsNoTracking().SingleAsync(o => o.Id == s.Order))).Status.Should().Be("InProduction");
            (await _factory.QueryAsync(db => db.ProductionPlanLine.AsNoTracking().Where(l => l.ProductionPlanId == s.Plan).ToListAsync()))
                .Should().OnlyContain(l => l.ShiftId == s.ShiftId && l.Status == "Released");

            // Không phát hành lại; dòng kế hoạch đã phát hành không sửa; CRUD không phá máy trạng thái.
            (await c.PostFormAsync("/api/ProductionPlan/Release", ("Id", s.Plan))).Fail("Đã duyệt");
            var planLine = await _factory.QueryAsync(db => db.ProductionPlanLine.AsNoTracking().FirstAsync(l => l.ProductionPlanId == s.Plan));
            (await c.PutFormAsync("/api/ProductionPlanLine/Update", ("Id", planLine.Id), ("ProductionPlanId", s.Plan), ("DishId", planLine.DishId), ("PlannedQuantity", 999m))).Fail("Nháp");
            (await c.PutFormAsync("/api/ProductionPlan/Update", ("Id", s.Plan), ("ProductionDate", Day), ("PlanDate", Day), ("Status", "Draft"))).Fail("Approve / Release");
            (await c.PostFormAsync("/api/ProductionPlan/Insert", ("ProductionDate", Day), ("PlanDate", Day))).Fail("MaterialRequirement/Calculate");
            (await c.PostFormAsync("/api/ProductionBatch/Insert", ("ProductionOrderId", meatOrder.Id), ("StartDate", Day))).Fail("ProductionPlan/Release");
            (await c.PutFormAsync("/api/ProductionBatch/Update", ("Id", batch.Id), ("ProductionOrderId", meatOrder.Id), ("StartDate", Day), ("Status", "Packed"))).Ok();
            (await BatchAsync(batch.Id)).Status.Should().Be("Planned", "CRUD Update không đổi trạng thái mẻ");
            (await c.DeleteResultAsync($"/api/ProductionBatch/Delete?id={batch.Id}")).Fail();
            (await c.PostFormAsync("/api/ProductionOrder/Insert", ("ProductionPlanId", s.Plan), ("PlannedStartDate", Day))).Fail("ProductionPlan/Release");
            (await c.PostFormAsync("/api/ProductionOutput/Insert", ("ProductionBatchId", batch.Id), ("ProducedQuantity", 1m))).Fail("RecordOutput");
            (await c.PostFormAsync("/api/MealWasteRecord/Insert", ("ProductionBatchId", batch.Id), ("WasteType", "SPOILAGE"), ("QuantityMeals", 1m))).Fail("RecordWaste");

            // In Phiếu sản xuất (kế hoạch) mở được, đúng tiêu đề.
            var file = await c.GetAsync($"/api/ProductionPlan/ExportProductionSheet?id={s.Plan}");
            file.Content.Headers.ContentType!.MediaType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            using var wb = new XLWorkbook(await file.Content.ReadAsStreamAsync());
            var ws = wb.Worksheet(1);
            ws.Cell(3, 1).GetString().Should().Be("PHIẾU SẢN XUẤT");
            ws.Cell(1, 13).GetValue<decimal>().Should().Be(330, "số suất order = Σ phần kế hoạch của các mẻ");
            ws.Cell(7, 6).GetString().Should().Be("SL cần");
            ws.Cell(8, 3).GetString().Should().Be("Thịt P1");
            ws.Cell(8, 6).GetValue<decimal>().Should().Be(13.2m);
        }

        [Fact]
        public async Task Batch_material_issue_uses_fefo_and_never_takes_expired_or_khong_dat_lots()
        {
            var s = await SeedAsync("P2");
            using var c = _factory.CreateAuthorizedClient();
            var batches = await ReleaseAsync(c, s);
            Guid first = Id(batches[0]), second = Id(batches[1]), last = Id(batches[3]);

            var detail = (await c.GetResultAsync($"/api/ProductionBatch/GetDetail?id={first}")).Ok();
            var material = detail.GetProperty("listMaterial").EnumerateArray().Single();
            material.GetProperty("requiredQuantity").GetDecimal().Should().Be(13.2m, "12 kg / 100 suất × 100 phần × 1,1");
            material.GetProperty("quantityPerServing").GetDecimal().Should().Be(0.12m);

            var issue = (await c.PostFormAsync("/api/ProductionBatch/CreateMaterialIssue", ("Id", first), ("WarehouseId", s.Warehouse), ("Post", true))).Ok();
            issue.GetProperty("materialIssue").GetProperty("status").GetString().Should().Be("Issued");
            issue.GetProperty("materialIssue").GetProperty("issueNumber").GetString().Should().MatchRegex(@"^PH-261220-\d{4}$");
            var lines = issue.GetProperty("listLine").EnumerateArray()
                .Select(l => (l.GetProperty("materialLotId").GetGuid(), l.GetProperty("quantity").GetDecimal())).ToList();
            lines.Should().BeEquivalentTo([(s.Lots["L1"], 10m), (s.Lots["L2"], 3.2m)], "FEFO: lô hạn gần trước, bỏ lô hết hạn / KHONG_DAT");
            var saved = await _factory.QueryAsync(db => db.MaterialIssue.AsNoTracking().SingleAsync(i => i.Id == issue.GetProperty("materialIssue").GetProperty("id").GetGuid()));
            saved.ProductionOrderId.Should().Be((await BatchAsync(first)).ProductionOrderId, "phiếu xuất gắn lệnh SX thật (GAP-INV-02 đã đóng)");
            saved.Note.Should().Contain(batches[0].GetProperty("batchNumber").GetString());
            (await c.GetResultAsync($"/api/ProductionBatch/GetDetail?id={first}")).Ok()
                .GetProperty("batch").GetProperty("materialIssued").GetBoolean().Should().BeTrue();

            // 1 phiếu tự động / mẻ.
            (await c.PostFormAsync("/api/ProductionBatch/CreateMaterialIssue", ("Id", first), ("WarehouseId", s.Warehouse), ("Post", true))).Fail("đã có phiếu xuất");
            // Mẻ -02 lấy tiếp L2 (13,2 / 16,8 còn lại); mẻ 50 phần cần 6,6 nhưng chỉ còn 3,6 kg xuất được – 100 kg hết hạn / KHONG_DAT không được dùng.
            (await c.PostFormAsync("/api/ProductionBatch/CreateMaterialIssue", ("Id", second), ("WarehouseId", s.Warehouse), ("Post", true))).Ok();
            (await c.PostFormAsync("/api/ProductionBatch/CreateMaterialIssue", ("Id", last), ("WarehouseId", s.Warehouse), ("Post", true))).Fail("Không đủ tồn khả dụng");
            (await _factory.QueryAsync(db => db.MaterialIssue.AsNoTracking().CountAsync(i => i.Note != null && i.Note.Contains(batches[3].GetProperty("batchNumber").GetString()!))))
                .Should().Be(0, "lỗi → không ghi phiếu");
            (await _factory.QueryAsync(db => db.InventoryTransaction.AsNoTracking()
                    .CountAsync(t => (t.MaterialLotId == s.Lots["LX"] || t.MaterialLotId == s.Lots["LK"]) && t.QuantityOut > 0)))
                .Should().Be(0);
        }

        [Fact]
        public async Task Core_temperature_below_70_blocks_release_and_packing_and_sample_is_required()
        {
            var s = await SeedAsync("P3");
            using var c = _factory.CreateAuthorizedClient();
            var batches = await ReleaseAsync(c, s);
            Guid soup = Id(batches[2]);

            (await c.PostFormAsync("/api/ProductionBatch/RecordCoreTemperature", ("Id", soup), ("TemperatureC", 75m))).Fail("Đang nấu");
            var started = (await c.PostFormAsync("/api/ProductionBatch/Start", ("Id", soup))).Ok();
            started.GetProperty("listWarning").EnumerateArray().Select(w => w.GetString()).Should().Contain(w => w!.Contains("chưa có phiếu xuất"));
            started.GetProperty("batch").GetProperty("status").GetString().Should().Be("Cooking");
            (await _factory.QueryAsync(db => db.ProductionOrder.AsNoTracking().SingleAsync(o => o.Id == (started.GetProperty("batch").GetProperty("productionOrderId").GetGuid()))))
                .Status.Should().Be("InProgress");

            // < 70 °C: bắt buộc hành động khắc phục; ghi được kèm cảnh báo.
            (await c.PostFormAsync("/api/ProductionBatch/RecordCoreTemperature", ("Id", soup), ("TemperatureC", 65m))).Fail("hành động khắc phục");
            var low = (await c.PostFormAsync("/api/ProductionBatch/RecordCoreTemperature", ("Id", soup), ("TemperatureC", 65m), ("CorrectiveAction", "Nấu tiếp 10 phút"))).Ok();
            low.GetProperty("listWarning").EnumerateArray().Select(w => w.GetString()).Should().Contain(w => w!.Contains("dưới ngưỡng"));
            (await c.PostFormAsync("/api/ProductionBatch/Finish", ("Id", soup), ("ActualQuantity", 80m))).Ok();

            // Chờ QC: nhiệt độ 65 °C, chưa lưu mẫu, chưa ghi sản lượng → không cho đạt.
            var blocked = await c.PostFormAsync("/api/ProductionBatch/Release", ("Id", soup));
            blocked.Fail("65 °C dưới ngưỡng 70 °C");
            blocked.Message.Should().Contain("Chưa lưu mẫu").And.Contain("Chưa ghi sản lượng");
            (await c.PostFormAsync("/api/ProductionBatch/RecordCoreTemperature", ("Id", soup), ("TemperatureC", 75.5m), ("DeviceCode", "NK-01"))).Ok();
            (await c.PostFormAsync("/api/ProductionBatch/RecordOutput", ("Id", soup), ("ProducedQuantity", 80m), ("RejectedQuantity", 2m))).Ok();
            var noSample = await c.PostFormAsync("/api/ProductionBatch/Release", ("Id", soup));
            noSample.Fail("Chưa lưu mẫu");
            noSample.Message.Should().NotContain("nhiệt độ", "đã đo lại đạt 75,5 °C");

            (await c.PostFormAsync("/api/ProductionBatch/RecordSampleRetained", ("Id", soup), ("ActualValue", "Tủ mẫu số 2 – 150 g"))).Ok();
            (await c.PostFormAsync("/api/ProductionBatch/RecordSampleRetained", ("Id", soup))).Fail("đã ghi lưu mẫu");
            var passed = (await c.PostFormAsync("/api/ProductionBatch/Release", ("Id", soup))).Ok();
            passed.GetProperty("batch").GetProperty("status").GetString().Should().Be("Passed");
            passed.GetProperty("batch").GetProperty("qcStatus").GetString().Should().Be("PASS");
            passed.GetProperty("batch").GetProperty("sampleRetained").GetBoolean().Should().BeTrue();

            // Đo lại trước khi đóng gói: 68 °C → chặn đóng gói; hâm lại 72 °C → đóng gói được.
            (await c.PostFormAsync("/api/ProductionBatch/RecordCoreTemperature", ("Id", soup), ("TemperatureC", 68m), ("CorrectiveAction", "Hâm lại"))).Ok();
            (await c.PostFormAsync("/api/ProductionBatch/Pack", ("Id", soup))).Fail("68 °C dưới ngưỡng");
            (await BatchAsync(soup)).Status.Should().Be("Passed");
            (await c.PostFormAsync("/api/ProductionBatch/RecordCoreTemperature", ("Id", soup), ("TemperatureC", 72m))).Ok();
            (await c.PostFormAsync("/api/ProductionBatch/RecordWaste", ("Id", soup), ("QuantityMeals", 3m), ("Reason", "Nấu dư"))).Ok();
            var packed = (await c.PostFormAsync("/api/ProductionBatch/Pack", ("Id", soup))).Ok();
            packed.GetProperty("batch").GetProperty("status").GetString().Should().Be("Packed");

            var batch = await BatchAsync(soup);
            batch.ActualQuantity.Should().Be(80);
            batch.AcceptedQty.Should().Be(78);
            batch.RejectedQty.Should().Be(2);
            batch.WasteQty.Should().Be(3);
            batch.StartTime.Should().NotBeNull();
            batch.EndTime.Should().NotBeNull();
            var logs = await _factory.QueryAsync(db => db.TemperatureLog.AsNoTracking().Where(t => t.ProductionBatchId == soup).OrderBy(t => t.CreatedDate).ToListAsync());
            logs.Select(t => (t.MeasurementType, t.TemperatureC, t.IsWithinRange, t.MinAllowedC)).Should().Equal(
                ("CORE", 65m, false, 70m), ("CORE", 75.5m, true, 70m), ("CORE", 68m, false, 70m), ("CORE", 72m, true, 70m));
            logs[0].CorrectiveAction.Should().Be("Nấu tiếp 10 phút");
            logs.Should().OnlyContain(t => t.RecordedBy == InMemoryApiFactory.UserId);
            var sample = await _factory.QueryAsync(db => db.FoodSafetyChecklistResult.AsNoTracking().SingleAsync(r => r.ProductionBatchId == soup));
            sample.ResultCode.Should().Be("PASS");
            sample.ShiftId.Should().Be(s.ShiftId);
            sample.CheckedBy.Should().Be(InMemoryApiFactory.UserId);
            var output = await _factory.QueryAsync(db => db.ProductionOutput.AsNoTracking().SingleAsync(o => o.ProductionBatchId == soup));
            (output.ProducedQuantity, output.AcceptedQuantity, output.RejectedQuantity, output.QualityStatus, output.OutputQty).Should().Be((80m, 78m, 2m, "PASS", 80m));
            (await _factory.QueryAsync(db => db.MealWasteRecord.AsNoTracking().SingleAsync(w => w.ProductionBatchId == soup))).WasteType.Should().Be("OVERPRODUCTION");

            // Lệnh Canh (1 mẻ) hoàn thành; kế hoạch chưa (còn mẻ Thịt).
            var soupOrder = await _factory.QueryAsync(db => db.ProductionOrder.AsNoTracking().SingleAsync(o => o.Id == batch.ProductionOrderId));
            soupOrder.Status.Should().Be("Completed");
            soupOrder.ActualQty.Should().Be(78);
            (await _factory.QueryAsync(db => db.ProductionPlan.AsNoTracking().SingleAsync(p => p.Id == s.Plan))).Status.Should().Be("Released");

            // Mẻ thịt bị loại → Không đạt + hao hụt QC_REJECT; đủ mẻ đóng / loại → kế hoạch hoàn thành.
            foreach (var other in new[] { batches[0], batches[1], batches[3] })
            {
                Guid id = Id(other);
                (await c.PostFormAsync("/api/ProductionBatch/Start", ("Id", id))).Ok();
                (await c.PostFormAsync("/api/ProductionBatch/Finish", ("Id", id))).Ok();
                (await c.PostFormAsync("/api/ProductionBatch/Reject", ("Id", id))).Fail("lý do");
                (await c.PostFormAsync("/api/ProductionBatch/Reject", ("Id", id), ("Note", "[DEMO] cháy đáy"))).Ok()
                    .GetProperty("batch").GetProperty("status").GetString().Should().Be("Failed");
            }
            (await _factory.QueryAsync(db => db.MealWasteRecord.AsNoTracking().CountAsync(w => w.WasteType == "QC_REJECT" && w.ProductionOrderId != batch.ProductionOrderId && w.DishId == s.DishMeat)))
                .Should().Be(3);
            (await _factory.QueryAsync(db => db.ProductionPlan.AsNoTracking().SingleAsync(p => p.Id == s.Plan))).Status.Should().Be("Completed");

            // Bảng mẻ của ngày + in phiếu của mẻ.
            var board = (await c.GetResultAsync($"/api/ProductionBatch/GetBoard?ProductionDate=2026-12-21&ShiftId={s.ShiftId}")).Ok();
            board.GetProperty("listBatch").GetArrayLength().Should().Be(4);
            board.GetProperty("statusCounts").GetProperty("Failed").GetInt32().Should().Be(3);
            board.GetProperty("passedPortions").GetDecimal().Should().Be(78);
            var file = await c.GetAsync($"/api/ProductionBatch/ExportProductionSheet?id={soup}");
            using var wb = new XLWorkbook(await file.Content.ReadAsStreamAsync());
            wb.Worksheet(1).Cell(3, 1).GetString().Should().Be("PHIẾU SẢN XUẤT");
            wb.Worksheet(1).Cell(8, 6).GetValue<decimal>().Should().Be(4m, "Rau 5 kg / 100 suất × 80 phần");
        }
    }
}
