using System.Net.Http.Json;
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
    /// BE2.4 – kho & sản xuất mở rộng: giữ chỗ nguyên liệu khi phát hành / xuất (126), hàng sắp về từ PO cho MRP, hủy / đóng lệnh SX (144, 145),
    /// chênh lệch tiêu hao (135), giá thành thực tế + giá vốn suất (152–154), công đoạn theo tổ, xử lý lô hết hạn. Hôm nay (đồng hồ test) = 20/12/2026 10:00.
    /// <para>Bộ số tính tay (giống ProductionTests): Thịt 12 kg / 100 suất, hao hụt 10% → lệnh 250 phần cần 33 kg; Rau 5 kg / 100 suất → lệnh Canh 80 phần cần 4 kg.
    /// Lô thịt L1 HSD ngày SX+1 10 kg, L2 HSD +9 20 kg, L3 HSD +20 20 kg (FEFO L1 → L2 → L3); rau R1 10 kg; đơn giá lô 1 000 đ / kg.</para>
    /// </summary>
    public class InventoryProductionTests : IClassFixture<InMemoryApiFactory>
    {
        private static readonly DateTime Today = new(2026, 12, 20);
        private readonly InMemoryApiFactory _factory;

        public InventoryProductionTests(InMemoryApiFactory factory)
        {
            _factory = factory;
            _factory.Clock.FixedNow = Today.AddHours(10);
        }

        private sealed record Scenario(DateTime Day, string Shift, Guid ShiftId, Guid Line, Guid Warehouse, Guid Kg, Guid Meat, Guid Veg, Guid DishMeat, Guid DishSoup,
            Guid Plan, Guid Order, Guid OrderLine, Dictionary<string, Guid> Lots);

        private async Task<Scenario> SeedAsync(string code, int dayOffset)
        {
            DateTime day = Today.AddDays(dayOffset);
            Guid shift = Guid.NewGuid(), line = Guid.NewGuid(), warehouse = Guid.NewGuid(), kg = Guid.NewGuid(), meat = Guid.NewGuid(), veg = Guid.NewGuid();
            Guid dishMeat = Guid.NewGuid(), dishSoup = Guid.NewGuid(), plan = Guid.NewGuid(), customer = Guid.NewGuid(), location = Guid.NewGuid(), order = Guid.NewGuid();
            Guid orderLine = Guid.NewGuid();
            string shiftCode = "B4" + code;
            var lots = new Dictionary<string, Guid> { ["L1"] = Guid.NewGuid(), ["L2"] = Guid.NewGuid(), ["L3"] = Guid.NewGuid(), ["R1"] = Guid.NewGuid() };
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
                db.ProductionPlan.Add(new ProductionPlan { Id = plan, PlanNumber = "KH-" + code, PlanNo = "KH-" + code, ProductionDate = day, PlanDate = Today, Status = "Draft" });
                db.ProductionPlanLine.AddRange(
                    new ProductionPlanLine { Id = Guid.NewGuid(), ProductionPlanId = plan, DishId = dishMeat, PlannedQuantity = 250, PlannedQty = 250, Status = "Draft" },
                    new ProductionPlanLine { Id = Guid.NewGuid(), ProductionPlanId = plan, DishId = dishSoup, PlannedQuantity = 80, PlannedQty = 80, Status = "Draft" });
                db.Customer.Add(new Customer { Id = customer, Code = "KH-" + code, Name = "[DEMO] Khách " + code, CIF = "C" + code, CustomerType = "FACTORY", DisplayName = "Khách " + code, Status = "Active" });
                db.CustomerDeliveryLocation.Add(new CustomerDeliveryLocation { Id = location, CustomerId = customer, Code = "DG-" + code, Name = "Điểm giao " + code, DeliveryWindowFrom = new TimeSpan(11, 30, 0) });
                db.Order.Add(new Order { Id = order, OrderNumber = "PH-" + code, OrderNo = "PH-" + code, CustomerId = customer, DeliveryLocationId = location, OrderDate = Today, MealDate = day, DeliveryDate = day, Status = "Locked" });
                Guid menu = Guid.NewGuid();
                db.Menu.Add(new Model.Entity.Menu { Id = menu, Code = "TD-" + code, Name = "Thực đơn " + code, VersionNo = 1, Status = "Published", EffectiveFrom = Today.AddDays(-30) });
                int dow = MenuService.MenuDayOf(day);
                db.MenuLine.AddRange(
                    new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = dishMeat, MealType = "TRUA", DayOfWeek = dow, SequenceNo = 1 },
                    new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = dishSoup, MealType = "TRUA", DayOfWeek = dow, SequenceNo = 1 });
                db.OrderLine.Add(new OrderLine { Id = orderLine, OrderId = order, MenuId = menu, MealType = "TRUA", ShiftId = shift, Quantity = 330, UnitPrice = 30000, LineAmount = 9_900_000 });

                var ledger = new InventoryLedgerService(db);
                foreach (var (name, ingredient, expiry, qty) in new[]
                {
                    ("L1", meat, day.AddDays(1), 10m), ("L2", meat, day.AddDays(9), 20m), ("L3", meat, day.AddDays(20), 20m), ("R1", veg, day.AddDays(2), 10m),
                })
                {
                    db.MaterialLot.Add(new MaterialLot
                    {
                        Id = lots[name], IngredientId = ingredient, LotNumber = name + "-" + code, LotNo = name + "-" + code, ExpiryDate = expiry,
                        ReceivedDate = Today.AddDays(-3), QcStatus = "DAT", QualityStatus = "DAT", Status = "Available", UnitCost = 1000,
                    });
                    await ledger.PostAsync(new InventoryPostRequest
                    {
                        TransactionType = "RECEIPT", TransactionNumber = "NK-" + name + code, WarehouseId = warehouse, IngredientId = ingredient,
                        MaterialLotId = lots[name], UnitId = kg, QuantityIn = qty, UnitCost = 1000,
                    });
                }
                await EnsureChecklistsAsync(db);
            });
            return new Scenario(day, shiftCode, shift, line, warehouse, kg, meat, veg, dishMeat, dishSoup, plan, order, orderLine, lots);
        }

        /// <summary>Checklist lưu mẫu (T17) + công đoạn PRD_STAGE (seed BE2.4) + 4 tổ bếp (ORG_STRUCTURE) – 1 lần cho DB InMemory của lớp.</summary>
        private static async Task EnsureChecklistsAsync(Data.Connection.DatabaseContext db)
        {
            if (!await db.FoodSafetyChecklistItem.AnyAsync(i => i.ItemCode == "SAMPLE_RETAINED"))
            {
                Guid checklist = Guid.NewGuid();
                db.FoodSafetyChecklist.Add(new FoodSafetyChecklist { Id = checklist, Code = "PRD_BATCH", Name = "Kiểm soát mẻ nấu", VersionNo = 1, Status = "Active" });
                db.FoodSafetyChecklistItem.Add(new FoodSafetyChecklistItem { Id = Guid.NewGuid(), ChecklistId = checklist, ItemCode = "SAMPLE_RETAINED", ItemName = "Lưu mẫu", SortOrder = 1, IsCritical = true, IsRequired = true });
            }
            if (!await db.FoodSafetyChecklist.AnyAsync(c => c.Code == "PRD_STAGE"))
            {
                Guid checklist = Guid.NewGuid();
                db.FoodSafetyChecklist.Add(new FoodSafetyChecklist { Id = checklist, Code = "PRD_STAGE", Name = "Công đoạn sản xuất theo tổ", VersionNo = 1, Status = "Active" });
                db.FoodSafetyChecklistItem.AddRange(
                    new FoodSafetyChecklistItem { Id = Guid.NewGuid(), ChecklistId = checklist, ItemCode = "SO_CHE", ItemName = "Sơ chế", SortOrder = 1, IsRequired = true },
                    new FoodSafetyChecklistItem { Id = Guid.NewGuid(), ChecklistId = checklist, ItemCode = "NAU", ItemName = "Nấu", SortOrder = 2, IsRequired = true },
                    new FoodSafetyChecklistItem { Id = Guid.NewGuid(), ChecklistId = checklist, ItemCode = "CHIA_SUAT", ItemName = "Chia suất / Đóng gói", SortOrder = 3, IsRequired = true });
                foreach (var (teamCode, name) in new[] { ("OPS-KIT-PREP", "Tổ Sơ chế"), ("OPS-KIT-COOK", "Tổ Nấu"), ("OPS-KIT-RICE", "Tổ Cơm / Canh"), ("OPS-KIT-PORT", "Tổ Chia suất / Đóng gói") })
                {
                    db.OrganizationUnit.Add(new OrganizationUnit { Id = Guid.NewGuid(), Code = teamCode, Name = name, UnitType = "TEAM", Status = "Active" });
                }
            }
        }

        private static async Task<LegacyResult> PostJson(HttpClient c, string url, object body) => await LegacyResult.ReadAsync(await c.PostAsJsonAsync(url, body));

        private static Guid Id(JsonElement e) => e.GetProperty("id").GetGuid();

        private async Task<List<JsonElement>> ReleaseAsync(HttpClient c, Scenario s)
        {
            (await c.PostFormAsync("/api/ProductionPlan/Approve", ("Id", s.Plan))).Ok();
            var detail = (await c.PostFormAsync("/api/ProductionPlan/Release", ("Id", s.Plan), ("ShiftId", s.ShiftId), ("ProductionLineId", s.Line))).Ok();
            return [.. detail.GetProperty("listBatch").EnumerateArray()];
        }

        private Task<Guid> OrderOfAsync(Scenario s, Guid dish)
            => _factory.QueryAsync(db => db.ProductionOrder.AsNoTracking().Where(o => o.ProductionPlanId == s.Plan && o.DishId == dish && o.Status != "Cancelled").Select(o => o.Id).SingleAsync());

        private Task<Dictionary<Guid, decimal>> ReservedByLotAsync(Guid ingredient)
            => _factory.QueryAsync(db => db.InventoryBalance.AsNoTracking().Where(b => b.IngredientId == ingredient && !b.IsDeleted)
                .ToDictionaryAsync(b => b.MaterialLotId!.Value, b => b.ReservedQuantity));

        [Fact]
        public async Task Release_reserves_materials_fefo_blocks_other_issues_and_own_issue_consumes_the_reservation()
        {
            var s = await SeedAsync("R1", 1);
            using var c = _factory.CreateAuthorizedClient();
            var batches = await ReleaseAsync(c, s);
            var meatOrder = await OrderOfAsync(s, s.DishMeat);
            var soupOrder = await OrderOfAsync(s, s.DishSoup);

            // Giữ chỗ lúc phát hành: Thịt 250 × 12 / 100 × 1,1 = 33 kg (đủ 50), Rau 80 × 5 / 100 = 4 kg.
            var reservations = await _factory.QueryAsync(db => db.MaterialReservation.AsNoTracking().Where(r => r.ProductionOrderId == meatOrder || r.ProductionOrderId == soupOrder).ToListAsync());
            reservations.Should().HaveCount(2);
            var meatRes = reservations.Single(r => r.IngredientId == s.Meat);
            (meatRes.RequiredQty, meatRes.ReservedQuantity, meatRes.Status, meatRes.UnitId).Should().Be((33m, 33m, "Reserved", s.Kg));
            reservations.Single(r => r.IngredientId == s.Veg).ReservedQuantity.Should().Be(4m);
            // Tồn giữ chỗ phân bổ FEFO: L1 10 + L2 20 + L3 3.
            var byLot = await ReservedByLotAsync(s.Meat);
            (byLot[s.Lots["L1"]], byLot[s.Lots["L2"]], byLot[s.Lots["L3"]]).Should().Be((10m, 20m, 3m));
            var plan = (await c.GetResultAsync($"/api/ProductionPlan/GetDetail?id={s.Plan}")).Ok();
            plan.GetProperty("listReservation").GetArrayLength().Should().Be(2);
            var orderDetail = (await c.GetResultAsync($"/api/ProductionOrder/GetDetail?id={meatOrder}")).Ok();
            orderDetail.GetProperty("listReservation")[0].GetProperty("statusName").GetString().Should().Be("Đã giữ");
            orderDetail.GetProperty("allowedActions").EnumerateArray().Select(a => a.GetString()).Should().Contain(["reserve", "cancel"]);
            (await c.GetResultAsync($"/api/MaterialReservation/GetAll?ProductionOrderId={meatOrder}&Page=1&PageSize=10")).Code.Should().Be(200);
            (await c.PostFormAsync("/api/MaterialReservation/Insert", ("ProductionOrderId", meatOrder), ("IngredientId", s.Meat), ("UnitId", s.Kg))).Fail("ProductionPlan/Release");

            // Xuất tay không gắn lệnh: chỉ còn 50 − 33 = 17 kg khả dụng.
            (await PostJson(c, "/api/MaterialIssue/Create", new { warehouseId = s.Warehouse, listLine = new[] { new { ingredientId = s.Meat, quantity = 18m } } }))
                .Fail("Không đủ tồn khả dụng");

            // Xuất cho mẻ 1 của chính lệnh (84 phần → 11,088 kg) dùng phần đã giữ, FEFO L1 10 + L2 1,088.
            var first = batches.First(b => b.GetProperty("dishId").GetGuid() == s.DishMeat);
            (await c.PostFormAsync("/api/ProductionBatch/CreateMaterialIssue", ("Id", Id(first)), ("WarehouseId", s.Warehouse), ("Post", true))).Ok();
            meatRes = await _factory.QueryAsync(db => db.MaterialReservation.AsNoTracking().SingleAsync(r => r.Id == meatRes.Id));
            (meatRes.ReservedQuantity, meatRes.Status).Should().Be((21.912m, "PartiallyIssued"));
            byLot = await ReservedByLotAsync(s.Meat);
            byLot.Values.Sum().Should().Be(21.912m);
            (byLot[s.Lots["L1"]], byLot[s.Lots["L2"]], byLot[s.Lots["L3"]]).Should().Be((0m, 18.912m, 3m), "phân bổ lại FEFO sau khi xuất");
            var balances = await _factory.QueryAsync(db => db.InventoryBalance.AsNoTracking().Where(b => b.IngredientId == s.Meat).ToListAsync());
            balances.Should().OnlyContain(b => b.AvailableQuantity == b.OnHandQuantity - b.ReservedQuantity && b.ReservedQty == b.ReservedQuantity);

            // Giữ chỗ bổ sung: Rau đã đủ → không đổi; lệnh còn Phát hành.
            var reserve = (await c.PostFormAsync("/api/ProductionOrder/Reserve", ("Id", soupOrder))).Ok();
            reserve.GetProperty("listReservation")[0].GetProperty("reservedQuantity").GetDecimal().Should().Be(4m);
        }

        [Fact]
        public async Task Shortage_is_reported_on_release_and_cancel_releases_reservations_and_reopens_the_shift()
        {
            var s = await SeedAsync("C1", 2);
            // Bớt tồn thịt: 50 − 30 = 20 kg (< 33 cần).
            await _factory.SeedAsync(async db => await new InventoryLedgerService(db).PostAsync(new InventoryPostRequest
            {
                TransactionType = "WASTE", TransactionNumber = "HUY-C1", WarehouseId = s.Warehouse, IngredientId = s.Meat, MaterialLotId = s.Lots["L3"], UnitId = s.Kg, QuantityOut = 20m,
            }));
            await _factory.SeedAsync(async db => await new InventoryLedgerService(db).PostAsync(new InventoryPostRequest
            {
                TransactionType = "WASTE", TransactionNumber = "HUY-C1B", WarehouseId = s.Warehouse, IngredientId = s.Meat, MaterialLotId = s.Lots["L2"], UnitId = s.Kg, QuantityOut = 10m,
            }));
            using var c = _factory.CreateAuthorizedClient();
            (await c.PostFormAsync("/api/ProductionPlan/Approve", ("Id", s.Plan))).Ok();
            var release = await c.PostFormAsync("/api/ProductionPlan/Release", ("Id", s.Plan), ("ShiftId", s.ShiftId), ("ProductionLineId", s.Line));
            release.Ok().GetProperty("listWarning").EnumerateArray().Select(w => w.GetString()).Should().Contain(w => w!.Contains("giữ chỗ") && w.Contains("20 / 33") && w.Contains("thiếu 13"));
            var meatOrder = await OrderOfAsync(s, s.DishMeat);
            var soupOrder = await OrderOfAsync(s, s.DishSoup);
            var detail = (await c.GetResultAsync($"/api/ProductionOrder/GetDetail?id={meatOrder}")).Ok();
            detail.GetProperty("listReservation")[0].GetProperty("shortQuantity").GetDecimal().Should().Be(13m);

            // Hàng về thêm 15 kg → giữ chỗ bổ sung tới đủ 33.
            await _factory.SeedAsync(async db => await new InventoryLedgerService(db).PostAsync(new InventoryPostRequest
            {
                TransactionType = "RECEIPT", TransactionNumber = "NK-C1X", WarehouseId = s.Warehouse, IngredientId = s.Meat, MaterialLotId = s.Lots["L3"], UnitId = s.Kg, QuantityIn = 15m, UnitCost = 1000,
            }));
            detail = (await c.PostFormAsync("/api/ProductionOrder/Reserve", ("Id", meatOrder))).Ok();
            detail.GetProperty("listReservation")[0].GetProperty("reservedQuantity").GetDecimal().Should().Be(33m);

            // Hủy: lý do bắt buộc; mẻ đã nấu → chặn; phiếu xuất đã ghi sổ → chặn.
            (await c.PostFormAsync("/api/ProductionOrder/Cancel", ("Id", soupOrder))).Fail("lý do");
            var soupBatch = await _factory.QueryAsync(db => db.ProductionBatch.AsNoTracking().SingleAsync(b => b.ProductionOrderId == soupOrder));
            (await c.PostFormAsync("/api/ProductionBatch/CreateMaterialIssue", ("Id", soupBatch.Id), ("WarehouseId", s.Warehouse), ("Post", true))).Ok();
            (await c.PostFormAsync("/api/ProductionOrder/Cancel", ("Id", soupOrder), ("Reason", "Khách đổi món"))).Fail("MaterialIssue/Cancel");
            var issueId = await _factory.QueryAsync(db => db.MaterialIssue.AsNoTracking().Where(i => i.ProductionOrderId == soupOrder).Select(i => i.Id).SingleAsync());
            (await c.PostFormAsync("/api/MaterialIssue/Cancel", ("Id", issueId), ("Reason", "Trả lại kho"))).Ok();
            var cancelled = await c.PostFormAsync("/api/ProductionOrder/Cancel", ("Id", soupOrder), ("Reason", "Khách đổi món"));
            cancelled.Ok().GetProperty("productionOrder").GetProperty("status").GetString().Should().Be("Cancelled");
            cancelled.Message.Should().Contain("1 mẻ hủy");
            (await _factory.QueryAsync(db => db.ProductionBatch.AsNoTracking().SingleAsync(b => b.Id == soupBatch.Id))).Status.Should().Be("Cancelled");
            (await _factory.QueryAsync(db => db.MaterialReservation.AsNoTracking().SingleAsync(r => r.ProductionOrderId == soupOrder))).Status
                .Should().Be("Issued", "đã xuất hết trước khi hủy phiếu – hủy phiếu xuất không giữ chỗ lại (D-53 a)");
            (await ReservedByLotAsync(s.Veg)).Values.Sum().Should().Be(0m);
            // Ca vẫn còn lệnh Thịt → kế hoạch vẫn Đã phát hành.
            (await _factory.QueryAsync(db => db.ProductionPlan.AsNoTracking().SingleAsync(p => p.Id == s.Plan))).Status.Should().Be("Released");

            // Lệnh Thịt: mẻ đã bắt đầu (lệnh Đang SX) → chặn hủy.
            var meatBatches = await _factory.QueryAsync(db => db.ProductionBatch.AsNoTracking().Where(b => b.ProductionOrderId == meatOrder).OrderBy(b => b.StartDate).ToListAsync());
            (await c.PostFormAsync("/api/ProductionBatch/Start", ("Id", meatBatches[0].Id))).Ok();
            (await c.PostFormAsync("/api/ProductionOrder/Cancel", ("Id", meatOrder), ("Reason", "Thử"))).Fail("Phát hành (hiện tại: Đang SX)");
        }

        [Fact]
        public async Task Cancelling_every_order_of_a_shift_reopens_the_plan_for_release()
        {
            var s = await SeedAsync("C2", 3);
            using var c = _factory.CreateAuthorizedClient();
            await ReleaseAsync(c, s);
            var meatOrder = await OrderOfAsync(s, s.DishMeat);
            var soupOrder = await OrderOfAsync(s, s.DishSoup);
            var draftBatch = await _factory.QueryAsync(db => db.ProductionBatch.AsNoTracking().Where(b => b.ProductionOrderId == meatOrder).OrderBy(b => b.StartDate).FirstAsync());
            (await c.PostFormAsync("/api/ProductionBatch/CreateMaterialIssue", ("Id", draftBatch.Id), ("WarehouseId", s.Warehouse), ("Post", false))).Ok();
            (await c.PostFormAsync("/api/ProductionOrder/Cancel", ("Id", meatOrder), ("Reason", "Hủy cả ca"))).Ok();
            (await _factory.QueryAsync(db => db.MaterialIssue.AsNoTracking().SingleAsync(i => i.ProductionOrderId == meatOrder))).Status.Should().Be("Cancelled", "phiếu Nháp hủy theo lệnh");
            var last = await c.PostFormAsync("/api/ProductionOrder/Cancel", ("Id", soupOrder), ("Reason", "Hủy cả ca"));
            last.Ok().GetProperty("listWarning").EnumerateArray().Select(w => w.GetString()).Should().Contain(w => w!.Contains("phát hành lại"));
            (await _factory.QueryAsync(db => db.ProductionPlan.AsNoTracking().SingleAsync(p => p.Id == s.Plan))).Status.Should().Be("Approved");
            (await ReservedByLotAsync(s.Meat)).Values.Sum().Should().Be(0m);
            // Phát hành lại ca → lệnh mới + giữ chỗ mới.
            (await c.PostFormAsync("/api/ProductionPlan/Release", ("Id", s.Plan), ("ShiftId", s.ShiftId), ("ProductionLineId", s.Line))).Ok()
                .GetProperty("productionPlan").GetProperty("status").GetString().Should().Be("Released");
            (await ReservedByLotAsync(s.Meat)).Values.Sum().Should().Be(33m);
        }

        [Fact]
        public async Task Stages_by_team_gate_finish_and_pack_then_close_builds_variance_actual_cost_and_meal_cost()
        {
            var s = await SeedAsync("S1", 4);
            Guid employee = Guid.NewGuid(), otherEmployee = Guid.NewGuid();
            Guid prepTeam = await _factory.QueryAsync(db => db.OrganizationUnit.AsNoTracking().Where(o => o.Code == "OPS-KIT-PREP").Select(o => o.Id).SingleAsync());
            await _factory.SeedAsync(db =>
            {
                db.Employee.AddRange(
                    new Employee { Id = employee, Code = "NV-S1", FullName = "Nguyễn Sơ Chế", OrganizationUnitId = prepTeam },
                    new Employee { Id = otherEmployee, Code = "NV-S1B", FullName = "Trần Khác", OrganizationUnitId = Guid.NewGuid() });
                // Giá thành chuẩn Thịt 15 000 đ / phần (lệnh Thịt không tính giá → giá vốn suất dùng giá chuẩn).
                db.StandardCost.Add(new StandardCost { Id = Guid.NewGuid(), DishId = s.DishMeat, RecipeId = Guid.NewGuid(), TotalCost = 15000, CostPerServing = 15000, Status = "Active", EffectiveFrom = Today.AddDays(-10) });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();
            await ReleaseAsync(c, s);
            var soupOrder = await OrderOfAsync(s, s.DishSoup);
            Guid batch = await _factory.QueryAsync(db => db.ProductionBatch.AsNoTracking().Where(b => b.ProductionOrderId == soupOrder).Select(b => b.Id).SingleAsync());
            (await c.PostFormAsync("/api/ProductionBatch/CreateMaterialIssue", ("Id", batch), ("WarehouseId", s.Warehouse), ("Post", true))).Ok();
            // Xuất thêm 0,5 kg rau cho lệnh (không gắn mẻ) → chênh lệch tiêu hao.
            var extra = (await PostJson(c, "/api/MaterialIssue/Create", new { warehouseId = s.Warehouse, productionOrderId = soupOrder,
                listLine = new[] { new { ingredientId = s.Veg, quantity = 0.5m } } })).Ok();
            (await c.PostFormAsync("/api/MaterialIssue/Post", ("Id", extra.GetProperty("materialIssue").GetProperty("id").GetGuid()))).Ok();

            // Công đoạn: không nhảy công đoạn; Sơ chế (người thuộc tổ, người khác tổ → cảnh báo); bắt đầu Nấu = bắt đầu mẻ.
            (await c.PostFormAsync("/api/ProductionBatch/StartStage", ("Id", batch), ("StageCode", "NAU"))).Fail("Chưa xong công đoạn trước");
            (await c.PostFormAsync("/api/ProductionBatch/StartStage", ("Id", batch), ("StageCode", "XYZ"))).Fail("Không có công đoạn");
            var started = await c.PostFormAsync("/api/ProductionBatch/StartStage", ("Id", batch), ("StageCode", "SO_CHE"), ("EmployeeIds", employee), ("EmployeeIds", otherEmployee));
            var d = started.Ok();
            d.GetProperty("listWarning").EnumerateArray().Select(w => w.GetString()).Should().Contain(w => w!.Contains("Trần Khác") && w.Contains("Tổ Sơ chế"));
            var stage = d.GetProperty("listStage")[0];
            (stage.GetProperty("stageCode").GetString(), stage.GetProperty("status").GetString(), stage.GetProperty("teamCode").GetString()).Should().Be(("SO_CHE", "InProgress", "OPS-KIT-PREP"));
            stage.GetProperty("employeeNames").EnumerateArray().Select(n => n.GetString()).Should().Contain("Nguyễn Sơ Chế");
            d.GetProperty("batch").GetProperty("status").GetString().Should().Be("Planned");
            (await c.PostFormAsync("/api/ProductionBatch/StartStage", ("Id", batch), ("StageCode", "SO_CHE"))).Fail("đã bắt đầu");
            _factory.Clock.FixedNow = Today.AddHours(10).AddMinutes(30);
            (await c.PostFormAsync("/api/ProductionBatch/FinishStage", ("Id", batch), ("StageCode", "SO_CHE"))).Ok();
            d = (await c.PostFormAsync("/api/ProductionBatch/StartStage", ("Id", batch), ("StageCode", "NAU"))).Ok();
            d.GetProperty("batch").GetProperty("status").GetString().Should().Be("Cooking");
            d.GetProperty("listStage")[1].GetProperty("teamCode").GetString().Should().Be("OPS-KIT-COOK");
            (await _factory.QueryAsync(db => db.ProductionOrder.AsNoTracking().SingleAsync(o => o.Id == soupOrder))).Status.Should().Be("InProgress");
            (await c.PostFormAsync("/api/ProductionBatch/Finish", ("Id", batch), ("ActualQuantity", 80m))).Fail("công đoạn Nấu");
            (await c.PostFormAsync("/api/ProductionBatch/FinishStage", ("Id", batch), ("StageCode", "NAU"))).Ok();
            (await c.PostFormAsync("/api/ProductionBatch/RecordCoreTemperature", ("Id", batch), ("TemperatureC", 76m))).Ok();
            (await c.PostFormAsync("/api/ProductionBatch/RecordSampleRetained", ("Id", batch))).Ok();
            (await c.PostFormAsync("/api/ProductionBatch/Finish", ("Id", batch), ("ActualQuantity", 80m))).Ok();
            (await c.PostFormAsync("/api/ProductionBatch/RecordOutput", ("Id", batch), ("ProducedQuantity", 80m), ("RejectedQuantity", 2m))).Ok();
            (await c.PostFormAsync("/api/ProductionBatch/RecordWaste", ("Id", batch), ("QuantityMeals", 2m), ("Reason", "Rơi vãi"))).Ok();
            (await c.PostFormAsync("/api/ProductionBatch/StartStage", ("Id", batch), ("StageCode", "CHIA_SUAT"))).Fail("Đạt");
            (await c.PostFormAsync("/api/ProductionBatch/Release", ("Id", batch))).Ok();
            (await c.PostFormAsync("/api/ProductionBatch/Pack", ("Id", batch))).Fail("Chia suất");
            (await c.PostFormAsync("/api/ProductionBatch/StartStage", ("Id", batch), ("StageCode", "CHIA_SUAT"))).Ok();

            var board = (await c.GetResultAsync($"/api/ProductionBatch/GetStageBoard?ProductionDate={s.Day:yyyy-MM-dd}")).Ok();
            var teams = board.GetProperty("listTeam").EnumerateArray().ToList();
            teams.Should().Contain(t => t.GetProperty("stageCode").GetString() == "CHIA_SUAT" && t.GetProperty("inProgress").GetInt32() == 1 && t.GetProperty("teamCode").GetString() == "OPS-KIT-PORT");
            teams.Should().Contain(t => t.GetProperty("stageCode").GetString() == "SO_CHE" && t.GetProperty("done").GetInt32() == 1 && t.GetProperty("waiting").GetInt32() == 3,
                "3 mẻ Thịt chưa sơ chế đang chờ tổ Sơ chế");
            board.GetProperty("listBatch").EnumerateArray().Single(b => b.GetProperty("batch").GetProperty("id").GetGuid() == batch)
                .GetProperty("currentStageCode").GetString().Should().Be("CHIA_SUAT");

            (await c.PostFormAsync("/api/ProductionBatch/FinishStage", ("Id", batch), ("StageCode", "CHIA_SUAT"))).Ok();
            (await c.PostFormAsync("/api/ProductionBatch/Pack", ("Id", batch))).Ok();
            (await _factory.QueryAsync(db => db.ProductionOrder.AsNoTracking().SingleAsync(o => o.Id == soupOrder))).Status.Should().Be("Completed");

            // Giá thành: NVL = 4 kg (mẻ) + 0,5 kg (lệnh, chia cho mẻ duy nhất) × 1 000 = 4 500; hao hụt 2 suất × (4 500 / 80) = 112,5 (chỉ phân tích).
            var cost = (await c.PostFormAsync("/api/ProductionOrder/CalculateCost", ("Id", soupOrder))).Ok();
            var breakdown = cost.GetProperty("breakdown");
            breakdown.GetProperty("materialCost").GetDecimal().Should().Be(4500m);
            breakdown.GetProperty("wasteCost").GetDecimal().Should().Be(112.5m);
            breakdown.GetProperty("totalCost").GetDecimal().Should().Be(4500m);
            breakdown.GetProperty("acceptedPortions").GetDecimal().Should().Be(78m);
            breakdown.GetProperty("costPerPortion").GetDecimal().Should().Be(Math.Round(4500m / 78m, 4));
            breakdown.GetProperty("listWarning").EnumerateArray().Select(w => w.GetString()).Should().Contain(w => w!.Contains("không gắn mẻ"));

            // Đóng lệnh: chênh lệch Rau = 4,5 − 5 / 100 × 80 = +0,5 kg (12,5%, vượt 5%), giải phóng giữ chỗ dư.
            (await c.PostFormAsync("/api/ProductionOrder/Close", ("Id", await OrderOfAsync(s, s.DishMeat)))).Fail("Hoàn thành");
            var closed = (await c.PostFormAsync("/api/ProductionOrder/Close", ("Id", soupOrder))).Ok();
            closed.GetProperty("detail").GetProperty("productionOrder").GetProperty("status").GetString().Should().Be("Closed");
            var variance = closed.GetProperty("listVariance").EnumerateArray().Single();
            (variance.GetProperty("standardQuantity").GetDecimal(), variance.GetProperty("actualQuantity").GetDecimal(), variance.GetProperty("varianceQuantity").GetDecimal(),
                variance.GetProperty("variancePercent").GetDecimal(), variance.GetProperty("varianceCost").GetDecimal(), variance.GetProperty("isOverThreshold").GetBoolean())
                .Should().Be((4m, 4.5m, 0.5m, 12.5m, 500m, true));
            closed.GetProperty("detail").GetProperty("listWarning").EnumerateArray().Select(w => w.GetString()).Should().Contain(w => w!.Contains("vượt 5%"));
            closed.GetProperty("actualCost").GetProperty("totalCost").GetDecimal().Should().Be(4500m);
            (await _factory.QueryAsync(db => db.MaterialReservation.AsNoTracking().SingleAsync(r => r.ProductionOrderId == soupOrder))).Status.Should().Be("Issued");
            (await c.PostFormAsync("/api/ProductionOrder/Close", ("Id", soupOrder))).Fail();
            var list = await c.GetResultAsync($"/api/MaterialConsumptionVariance/GetList?ProductionOrderId={soupOrder}&OverThresholdOnly=true");
            list.Code.Should().Be(200);
            list.ListData.GetArrayLength().Should().Be(1);
            var actual = (await c.GetResultAsync($"/api/ActualCost/GetList?From={s.Day:yyyy-MM-dd}&To={s.Day:yyyy-MM-dd}")).Ok();
            actual.GetProperty("listData").EnumerateArray().Should().ContainSingle(x => x.GetProperty("productionOrderId").GetGuid() == soupOrder);
            (await c.PostFormAsync("/api/ActualCost/Insert", ("ProductionBatchId", batch))).Fail("CalculateCost");

            // Giá vốn suất: đơn phải Đã giao / Đã đối soát; đối soát nhận 300 / 330 suất.
            (await c.PostFormAsync("/api/MealCost/Calculate", ("OrderId", s.Order))).Fail("Đã giao");
            await _factory.SeedAsync(async db =>
            {
                var order = await db.Order.SingleAsync(o => o.Id == s.Order);
                order.Status = "Reconciled";
                db.OrderReconciliation.Add(new OrderReconciliation { Id = Guid.NewGuid(), OrderId = s.Order, ReconciliationDate = s.Day, OrderedQuantity = 330, DeliveredQuantity = 330, AcceptedQuantity = 300, Status = "Matched" });
            });
            var meal = (await c.PostFormAsync("/api/MealCost/Calculate", ("OrderId", s.Order))).Ok();
            // Canh: 4 500 đ phân bổ hết cho 330 phần của ngày / ca (đơn duy nhất); Thịt: giá chuẩn 15 000 × 330 = 4 950 000.
            meal.GetProperty("acceptedMeals").GetDecimal().Should().Be(300m);
            meal.GetProperty("acceptedSource").GetString().Should().Be("RECONCILIATION");
            meal.GetProperty("totalActualCost").GetDecimal().Should().Be(4_954_500m);
            meal.GetProperty("costPerMeal").GetDecimal().Should().Be(Math.Round(4_954_500m / 300m, 2));
            meal.GetProperty("revenue").GetDecimal().Should().Be(9_900_000m);
            meal.GetProperty("grossMargin").GetDecimal().Should().Be(9_900_000m - 4_954_500m);
            var dishes = meal.GetProperty("listLine")[0].GetProperty("listDish").EnumerateArray().ToList();
            dishes.Should().HaveCount(2);
            dishes.Single(x => x.GetProperty("dishId").GetGuid() == s.DishMeat).GetProperty("costSource").GetString().Should().Be("STANDARD");
            dishes.Single(x => x.GetProperty("dishId").GetGuid() == s.DishSoup).GetProperty("costSource").GetString().Should().Be("ACTUAL");
            meal.GetProperty("listWarning").EnumerateArray().Select(w => w.GetString()).Should().Contain(w => w!.Contains("giá thành chuẩn"));
            // Tính lại không nhân đôi dòng.
            (await c.PostFormAsync("/api/MealCost/Calculate", ("OrderId", s.Order))).Ok();
            (await _factory.QueryAsync(db => db.MealCost.AsNoTracking().CountAsync(m => m.OrderId == s.Order && !m.IsDeleted))).Should().Be(2);
            var listed = await c.GetResultAsync($"/api/MealCost/GetList?OrderId={s.Order}");
            listed.ListData.GetArrayLength().Should().Be(1);
        }

        [Fact]
        public async Task Process_expired_lots_writes_waste_marks_expired_skips_on_hold_and_lists_expiring_lots()
        {
            string code = "E1";
            Guid warehouse = Guid.NewGuid(), kg = Guid.NewGuid(), ing = Guid.NewGuid(), expired = Guid.NewGuid(), held = Guid.NewGuid(), soon = Guid.NewGuid();
            await _factory.SeedAsync(async db =>
            {
                db.Warehouse.Add(new Warehouse { Id = warehouse, Code = code, Name = "Kho " + code, Status = "Active" });
                db.UnitMeasure.Add(new UnitMeasure { Id = kg, Code = "KG-" + code, Name = "kg" });
                db.Ingredient.Add(new Ingredient { Id = ing, Code = "SUA-" + code, Name = "Sữa " + code, BaseUnitId = kg });
                var ledger = new InventoryLedgerService(db);
                foreach (var (id, expiry, status, qty) in new[] { (expired, Today.AddDays(-2), "Available", 5m), (held, Today.AddDays(-1), "OnHold", 3m), (soon, Today.AddDays(3), "Available", 7m) })
                {
                    db.MaterialLot.Add(new MaterialLot { Id = id, IngredientId = ing, LotNumber = $"{code}-{qty}", LotNo = $"{code}-{qty}", ExpiryDate = expiry, ReceivedDate = Today.AddDays(-10),
                        QcStatus = "DAT", Status = status, UnitCost = 2000 });
                    await ledger.PostAsync(new InventoryPostRequest { TransactionType = "RECEIPT", TransactionNumber = "NK-" + code + qty, WarehouseId = warehouse, IngredientId = ing,
                        MaterialLotId = id, UnitId = kg, QuantityIn = qty, UnitCost = 2000 });
                }
            });
            using var c = _factory.CreateAuthorizedClient();
            var preview = (await c.PostFormAsync("/api/MaterialLot/ProcessExpired", ("WarehouseId", warehouse), ("Preview", true))).Ok();
            var lots = preview.GetProperty("listLot").EnumerateArray().ToList();
            lots.Should().HaveCount(2);
            lots.Single(l => l.GetProperty("materialLotId").GetGuid() == held).GetProperty("skippedReason").GetString().Should().Contain("tạm giữ");
            preview.GetProperty("listExpiring").EnumerateArray().Should().ContainSingle(l => l.GetProperty("materialLotId").GetGuid() == soon);
            (await _factory.QueryAsync(db => db.InventoryTransaction.AsNoTracking().CountAsync(t => t.MaterialLotId == expired && t.TransactionType == "WASTE"))).Should().Be(0, "xem trước không ghi");

            var run = (await c.PostFormAsync("/api/MaterialLot/ProcessExpired", ("WarehouseId", warehouse))).Ok();
            run.GetProperty("expiredLotCount").GetInt32().Should().Be(1);
            run.GetProperty("totalWasteValue").GetDecimal().Should().Be(10000m);
            run.GetProperty("transactionNumber").GetString().Should().StartWith("GD-");
            var waste = await _factory.QueryAsync(db => db.InventoryTransaction.AsNoTracking().SingleAsync(t => t.MaterialLotId == expired && t.TransactionType == "WASTE"));
            (waste.QuantityOut, waste.ReferenceType, waste.ReferenceId, waste.Reason).Should().Be((5m, "LotExpiry", expired, "Hết hạn sử dụng 18/12/2026"));
            (await _factory.QueryAsync(db => db.MaterialLot.AsNoTracking().SingleAsync(l => l.Id == expired))).Status.Should().Be("Expired");
            (await _factory.QueryAsync(db => db.MaterialLot.AsNoTracking().SingleAsync(l => l.Id == held))).Status.Should().Be("OnHold");
            (await _factory.QueryAsync(db => db.InventoryBalance.AsNoTracking().SingleAsync(b => b.MaterialLotId == expired))).OnHandQuantity.Should().Be(0m);
            // Chạy lại: không còn lô hết hạn cần hủy (lô tạm giữ vẫn bị bỏ qua).
            var again = (await c.PostFormAsync("/api/MaterialLot/ProcessExpired", ("WarehouseId", warehouse))).Ok();
            again.GetProperty("expiredLotCount").GetInt32().Should().Be(0);
        }

        [Fact]
        public async Task Mrp_subtracts_incoming_purchase_orders_due_by_production_date_minus_received()
        {
            string code = "M1";
            DateTime day = Today.AddDays(6);
            Guid kg = Guid.NewGuid(), ing = Guid.NewGuid(), dish = Guid.NewGuid(), menu = Guid.NewGuid(), order = Guid.NewGuid(), supplier = Guid.NewGuid();
            Guid po1 = Guid.NewGuid(), po2 = Guid.NewGuid(), po3 = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.UnitMeasure.Add(new UnitMeasure { Id = kg, Code = "KG-" + code, Name = "kg" });
                db.Ingredient.Add(new Ingredient { Id = ing, Code = "GAO-" + code, Name = "Gạo " + code, BaseUnitId = kg });
                db.Dish.Add(new Dish { Id = dish, Code = "CO-" + code, Name = "Cơm " + code, Status = "Active" });
                db.Recipe.Add(new Recipe { Id = Guid.NewGuid(), DishId = dish, VersionNo = 1, VersionCode = "v1", Status = "Locked", LockedAt = Today.AddDays(-5), IsActive = true, StandardOutputQty = 100,
                    SnapshotJson = $"[{{\"IngredientId\":\"{ing}\",\"Quantity\":10,\"UnitId\":\"{kg}\",\"WastePercent\":0,\"SequenceNo\":1}}]" });
                db.Menu.Add(new Model.Entity.Menu { Id = menu, Code = "TD-" + code, Name = "Thực đơn " + code, VersionNo = 1, Status = "Published", EffectiveFrom = Today.AddDays(-30) });
                db.MenuLine.Add(new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = dish, MealType = "TRUA", DayOfWeek = MenuService.MenuDayOf(day), SequenceNo = 1 });
                db.Order.Add(new Order { Id = order, OrderNumber = "PH-" + code, OrderNo = "PH-" + code, CustomerId = Guid.NewGuid(), OrderDate = Today, MealDate = day, DeliveryDate = day, Status = "Locked" });
                db.OrderLine.Add(new OrderLine { Id = Guid.NewGuid(), OrderId = order, MenuId = menu, MealType = "TRUA", Quantity = 300 });
                // PO1 Đã duyệt giao trước ngày SX 20 kg, đã nhận đạt 5 → còn 15; PO2 chưa có ngày giao → không tính; PO3 giao sau ngày SX → không tính.
                db.PurchaseOrder.AddRange(
                    new PurchaseOrder { Id = po1, SupplierId = supplier, PurchaseOrderNumber = "PO-" + code + "1", OrderDate = Today, Status = "Approved", ExpectedDeliveryDate = day.AddDays(-1) },
                    new PurchaseOrder { Id = po2, SupplierId = supplier, PurchaseOrderNumber = "PO-" + code + "2", OrderDate = Today, Status = "Sent" },
                    new PurchaseOrder { Id = po3, SupplierId = supplier, PurchaseOrderNumber = "PO-" + code + "3", OrderDate = Today, Status = "Approved", ExpectedDeliveryDate = day.AddDays(2) });
                foreach (var (po, qty) in new[] { (po1, 20m), (po2, 7m), (po3, 9m) })
                {
                    db.PurchaseOrderLine.Add(new PurchaseOrderLine { Id = Guid.NewGuid(), PurchaseOrderId = po, IngredientId = ing, Quantity = qty, UnitId = kg, UomId = kg, UnitPrice = 1 });
                }
                Guid gr = Guid.NewGuid();
                db.GoodsReceipt.Add(new GoodsReceipt { Id = gr, PurchaseOrderId = po1, SupplierId = supplier, WarehouseId = Guid.NewGuid(), ReceiptDate = Today, Status = "Posted", ReceiptNumber = "PN-" + code });
                db.GoodsReceiptLine.Add(new GoodsReceiptLine { Id = Guid.NewGuid(), GoodsReceiptId = gr, IngredientId = ing, Quantity = 5, ReceivedQuantity = 5, AcceptedQuantity = 5, UnitId = kg, UomId = kg });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();
            var result = (await c.PostFormAsync("/api/MaterialRequirement/Calculate", ("ProductionDate", day))).Ok();
            var item = result.GetProperty("listItem").EnumerateArray().Single(i => i.GetProperty("ingredientId").GetGuid() == ing);
            // Nhu cầu 300 × 10 / 100 = 30 kg; hàng sắp về 15 → mua 15.
            item.GetProperty("requiredQuantity").GetDecimal().Should().Be(30m);
            item.GetProperty("expectedIncomingQuantity").GetDecimal().Should().Be(15m);
            item.GetProperty("purchaseQuantity").GetDecimal().Should().Be(15m);
            var warnings = result.GetProperty("listWarning").EnumerateArray().Select(w => w.GetString()).ToList();
            warnings.Should().Contain(w => w!.Contains("PO-M12") && w.Contains("chưa có ngày giao"));
            warnings.Should().Contain(w => w!.Contains("Đã trừ hàng sắp về") && w.Contains("PO-M11") && !w.Contains("PO-M13"));
        }
    }
}
