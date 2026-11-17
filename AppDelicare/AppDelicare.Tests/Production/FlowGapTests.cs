using System.Net.Http.Json;
using System.Text.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Production
{
    /// <summary>
    /// BE2.1 – kế hoạch SX sinh theo ca (137), đề nghị mua chọn dòng + dự phòng (107), nhiệt độ tự do (165), hao hụt (156), bảng đơn (90), thực đơn (82),
    /// phản ánh (197), chi tiết / hủy yêu cầu duyệt (211, 213), truy xuất 2 chiều (118, 119).
    /// </summary>
    public class FlowGapTests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public FlowGapTests(InMemoryApiFactory factory)
        {
            _factory = factory;
        }

        private static async Task<LegacyResult> PostJson(HttpClient c, string url, object body) => await LegacyResult.ReadAsync(await c.PostAsJsonAsync(url, body));

        private static int MenuDay(DateTime d) => d.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)d.DayOfWeek;

        [Fact]
        public async Task Generate_builds_one_day_plan_from_confirmed_and_locked_orders_split_by_shift_and_refreshes_while_draft()
        {
            var day = new DateTime(2026, 11, 30);
            _factory.Clock.FixedNow = day.AddDays(-1).AddHours(21);
            Guid lunch = Guid.NewGuid(), dinner = Guid.NewGuid(), rice = Guid.NewGuid(), chicken = Guid.NewGuid(), fish = Guid.NewGuid(), menu = Guid.NewGuid();
            Guid customer = Guid.NewGuid(), o1 = Guid.NewGuid(), o2 = Guid.NewGuid(), o3 = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Shift.AddRange(new Shift { Id = lunch, Code = "TRUAG1", Name = "Trưa", StartTime = new TimeSpan(10, 30, 0), Status = "Active" },
                    new Shift { Id = dinner, Code = "TOIG1", Name = "Tối", StartTime = new TimeSpan(17, 0, 0), Status = "Active" });
                db.Dish.AddRange(new Dish { Id = rice, Code = "COMG1", Name = "Cơm G1", Status = "Active" }, new Dish { Id = chicken, Code = "GAG1", Name = "Gà G1", Status = "Active" },
                    new Dish { Id = fish, Code = "CAG1", Name = "Cá G1", Status = "Active" });
                db.Menu.Add(new AppDelicare.Model.Entity.Menu { Id = menu, Code = "TDG1", Name = "Thực đơn G1", VersionNo = 1, Status = "Published" });
                // Trưa: cơm + gà (1 lựa chọn); Tối: cơm + cá.
                db.MenuLine.AddRange(
                    new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = rice, MealType = "TRUA", DayOfWeek = MenuDay(day), SequenceNo = 1, SortOrder = 1 },
                    new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = chicken, MealType = "TRUA", DayOfWeek = MenuDay(day), SequenceNo = 1, SortOrder = 2 },
                    new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = rice, MealType = "TOI", DayOfWeek = MenuDay(day), SequenceNo = 1, SortOrder = 1 },
                    new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = fish, MealType = "TOI", DayOfWeek = MenuDay(day), SequenceNo = 1, SortOrder = 2 });
                db.Customer.Add(new Customer { Id = customer, Code = "KG1", Name = "[DEMO] Khách G1", DisplayName = "Khách G1", CIF = "KG1", CustomerType = "FACTORY", Status = "Active" });
                db.Order.AddRange(
                    new Order { Id = o1, OrderNumber = "PH-G1-1", CustomerId = customer, OrderDate = day.AddDays(-3), MealDate = day, DeliveryDate = day, Status = "Confirmed", TotalQuantity = 100 },
                    new Order { Id = o2, OrderNumber = "PH-G1-2", CustomerId = customer, OrderDate = day.AddDays(-3), MealDate = day, DeliveryDate = day, Status = "Locked", TotalQuantity = 50 },
                    new Order { Id = o3, OrderNumber = "PH-G1-3", CustomerId = customer, OrderDate = day.AddDays(-3), MealDate = day, DeliveryDate = day, Status = "Draft", TotalQuantity = 999 });
                db.OrderLine.AddRange(
                    new OrderLine { Id = Guid.NewGuid(), OrderId = o1, MenuId = menu, MealType = "TRUA", ShiftId = lunch, Quantity = 100, LineAmount = 0 },
                    new OrderLine { Id = Guid.NewGuid(), OrderId = o2, MenuId = menu, MealType = "TOI", ShiftId = dinner, Quantity = 50, LineAmount = 0 },
                    new OrderLine { Id = Guid.NewGuid(), OrderId = o3, MenuId = menu, MealType = "TRUA", ShiftId = lunch, Quantity = 999, LineAmount = 0 });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();

            var plan = (await c.PostFormAsync("/api/ProductionPlan/Generate", ("ProductionDate", day))).Ok();
            plan.GetProperty("productionPlan").GetProperty("planNumber").GetString().Should().Be("KH-261130");
            plan.GetProperty("productionPlan").GetProperty("status").GetString().Should().Be("Draft");
            plan.GetProperty("productionPlan").GetProperty("shiftId").ValueKind.Should().Be(JsonValueKind.Null, "1 KH- / ngày (GAP-MRP-01)");
            var lines = plan.GetProperty("listLine").EnumerateArray().ToDictionary(l => l.GetProperty("dishCode").GetString()!);
            lines["COMG1"].GetProperty("plannedQuantity").GetDecimal().Should().Be(150);
            lines["COMG1"].GetProperty("shiftId").ValueKind.Should().Be(JsonValueKind.Null, "cơm thuộc 2 ca");
            lines["GAG1"].GetProperty("plannedQuantity").GetDecimal().Should().Be(100);
            lines["GAG1"].GetProperty("shiftId").GetGuid().Should().Be(lunch);
            lines["CAG1"].GetProperty("plannedQuantity").GetDecimal().Should().Be(50);
            lines["CAG1"].GetProperty("shiftId").GetGuid().Should().Be(dinner);
            plan.GetProperty("listWarning").EnumerateArray().Select(w => w.GetString()).Should().Contain(w => w!.Contains("1 đơn Nháp / Đã gửi"));

            // Sinh lại khi còn Nháp: đơn 1 đổi 100 → 80 suất → cập nhật tại chỗ (cùng số KH-).
            await _factory.SeedAsync(async db => (await db.OrderLine.SingleAsync(l => l.OrderId == o1)).Quantity = 80);
            var again = (await c.PostFormAsync("/api/ProductionPlan/Generate", ("ProductionDate", day))).Ok();
            again.GetProperty("productionPlan").GetProperty("id").GetGuid().Should().Be(plan.GetProperty("productionPlan").GetProperty("id").GetGuid());
            again.GetProperty("listLine").EnumerateArray().Single(l => l.GetProperty("dishCode").GetString() == "GAG1").GetProperty("plannedQuantity").GetDecimal().Should().Be(80);

            // Duyệt rồi → không sinh lại được; ngày không có đơn → lỗi.
            (await c.PostFormAsync("/api/ProductionPlan/Approve", ("Id", again.GetProperty("productionPlan").GetProperty("id").GetGuid()))).Ok();
            (await c.PostFormAsync("/api/ProductionPlan/Generate", ("ProductionDate", day))).Fail("không sinh lại được");
            (await c.PostFormAsync("/api/ProductionPlan/Generate", ("ProductionDate", day.AddDays(40)))).Fail("Không có đơn");
        }

        [Fact]
        public async Task CreatePurchaseRequest_uses_selected_shortage_lines_with_buffer_and_rewrites_the_draft_request()
        {
            var day = new DateTime(2026, 11, 29);
            _factory.Clock.FixedNow = day.AddDays(-1).AddHours(21);
            Guid plan = Guid.NewGuid(), unit = Guid.NewGuid(), beef = Guid.NewGuid(), rice = Guid.NewGuid(), salt = Guid.NewGuid();
            Guid rBeef = Guid.NewGuid(), rRice = Guid.NewGuid(), rSalt = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.ProductionPlan.Add(new ProductionPlan { Id = plan, PlanNumber = "KH-261129", PlanNo = "KH-261129", ProductionDate = day, PlanDate = day.AddDays(-1), Status = "Draft" });
                db.Ingredient.AddRange(new Ingredient { Id = beef, Code = "BOPR", Name = "Bò", Status = "Active" }, new Ingredient { Id = rice, Code = "GAOPR", Name = "Gạo", Status = "Active" },
                    new Ingredient { Id = salt, Code = "MUOIPR", Name = "Muối", Status = "Active" });
                db.MaterialRequirement.AddRange(
                    new MaterialRequirement { Id = rBeef, ProductionPlanId = plan, IngredientId = beef, UnitId = unit, RequiredQuantity = 120, PurchaseQuantity = 100, Status = "Shortage" },
                    new MaterialRequirement { Id = rRice, ProductionPlanId = plan, IngredientId = rice, UnitId = unit, RequiredQuantity = 80, PurchaseQuantity = 33.3333m, Status = "Shortage" },
                    new MaterialRequirement { Id = rSalt, ProductionPlanId = plan, IngredientId = salt, UnitId = unit, RequiredQuantity = 1, PurchaseQuantity = 0, Status = "Sufficient" });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();

            (await PostJson(c, "/api/MaterialRequirement/CreatePurchaseRequest", new { productionPlanId = plan, bufferPercent = 120m })).Fail("dự phòng");
            (await PostJson(c, "/api/MaterialRequirement/CreatePurchaseRequest", new { productionPlanId = plan, listRequirementId = new[] { rSalt } })).Fail("Không có dòng thiếu");
            var first = (await PostJson(c, "/api/MaterialRequirement/CreatePurchaseRequest", new { productionPlanId = plan, listRequirementId = new[] { rBeef, rRice }, bufferPercent = 5m })).Ok();
            Guid prId = first.GetProperty("purchaseRequest").GetProperty("id").GetGuid();
            var prLines = await _factory.QueryAsync(db => db.PurchaseRequestLine.AsNoTracking().Where(l => l.PurchaseRequestId == prId && !l.IsDeleted).ToListAsync());
            prLines.Single(l => l.IngredientId == beef).Quantity.Should().Be(105, "100 × 1,05");
            prLines.Single(l => l.IngredientId == rice).Quantity.Should().Be(35, "33,3333 × 1,05 = 34,999965 → làm tròn lên 4 số lẻ 35");
            (await _factory.QueryAsync(db => db.MaterialRequirement.AsNoTracking().SingleAsync(r => r.Id == rBeef))).Status.Should().Be("Requested");

            // Chọn lại chỉ dòng bò, dự phòng 10% → cùng đề nghị Nháp, dòng gạo xóa mềm, gạo về Thiếu.
            var second = (await PostJson(c, "/api/MaterialRequirement/CreatePurchaseRequest", new { productionPlanId = plan, listRequirementId = new[] { rBeef }, bufferPercent = 10m })).Ok();
            second.GetProperty("purchaseRequest").GetProperty("id").GetGuid().Should().Be(prId);
            prLines = await _factory.QueryAsync(db => db.PurchaseRequestLine.AsNoTracking().Where(l => l.PurchaseRequestId == prId && !l.IsDeleted).ToListAsync());
            prLines.Should().ContainSingle().Which.Quantity.Should().Be(110);
            (await _factory.QueryAsync(db => db.MaterialRequirement.AsNoTracking().SingleAsync(r => r.Id == rRice))).Status.Should().Be("Shortage");
            (await _factory.QueryAsync(db => db.PurchaseRequest.AsNoTracking().SingleAsync(p => p.Id == prId))).Status.Should().Be("Draft");

            // Đề nghị đã gửi duyệt → chặn (dùng CalculateTopUp).
            await _factory.SeedAsync(async db => (await db.PurchaseRequest.SingleAsync(p => p.Id == prId)).Status = "PendingApproval");
            (await PostJson(c, "/api/MaterialRequirement/CreatePurchaseRequest", new { productionPlanId = plan })).Fail("Chờ duyệt");
        }

        [Fact]
        public async Task Free_temperature_log_computes_range_on_server_and_redirects_controlled_types()
        {
            _factory.Clock.FixedNow = new DateTime(2026, 11, 20, 9, 0, 0);
            Guid warehouse = Guid.NewGuid(), zone = Guid.NewGuid(), location = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.StorageZone.Add(new StorageZone { Id = zone, WarehouseId = warehouse, Code = "KL-T1", Name = "Kho lạnh T1", TemperatureMin = 0, TemperatureMax = 5, Status = "Active" });
                db.StorageLocation.Add(new StorageLocation { Id = location, ZoneId = zone, StorageZoneId = zone, WarehouseId = warehouse, Code = "KL-T1-01", Name = "Kệ 1", Status = "Active" });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();

            (await c.PostFormAsync("/api/TemperatureLog/Record", ("MeasurementType", "STORAGE"), ("TemperatureC", 3m))).Fail("ít nhất 1 đối tượng đo");
            (await c.PostFormAsync("/api/TemperatureLog/Record", ("StorageZoneId", zone), ("MeasurementType", "CORE"), ("TemperatureC", 80m))).Fail("ProductionBatch/RecordCoreTemperature");
            (await c.PostFormAsync("/api/TemperatureLog/Record", ("StorageZoneId", zone), ("MeasurementType", "DELIVERY"), ("TemperatureC", 80m))).Fail("DeliveryRoute/RecordTemperature");
            (await c.PostFormAsync("/api/TemperatureLog/Record", ("StorageZoneId", zone), ("MeasurementType", "XYZ"), ("TemperatureC", 3m))).Fail("TemperatureMeasurementType");

            // Vị trí → ngưỡng của khu (0…5 °C): 3 °C đạt; 7 °C không đạt, thiếu khắc phục → vẫn lưu + cảnh báo.
            var ok = (await c.PostFormAsync("/api/TemperatureLog/Record", ("StorageLocationId", location), ("MeasurementType", "storage"), ("TemperatureC", 3m), ("DeviceCode", "NK-1"))).Ok();
            ok.GetProperty("log").GetProperty("isWithinRange").GetBoolean().Should().BeTrue();
            ok.GetProperty("log").GetProperty("storageZoneId").GetGuid().Should().Be(zone);
            ok.GetProperty("log").GetProperty("maxAllowedC").GetDecimal().Should().Be(5);
            var high = (await c.PostFormAsync("/api/TemperatureLog/Record", ("StorageZoneId", zone), ("MeasurementType", "STORAGE"), ("TemperatureC", 7m), ("IsWithinRange", true))).Ok();
            high.GetProperty("log").GetProperty("isWithinRange").GetBoolean().Should().BeFalse("server tính, không nhận từ client");
            high.GetProperty("listWarning")[0].GetString().Should().Contain("7 °C ngoài ngưỡng 0…5 °C");
            high.GetProperty("log").GetProperty("recordedBy").GetGuid().Should().Be(InMemoryApiFactory.UserId);
            // Giữ nóng thành phẩm < 60 °C → không đạt. CRUD Insert cũng tính ở server.
            var hot = (await c.PostFormAsync("/api/TemperatureLog/Record", ("StorageZoneId", zone), ("MeasurementType", "HOT_HOLDING"), ("TemperatureC", 55m), ("CorrectiveAction", "Hâm lại"))).Ok();
            hot.GetProperty("log").GetProperty("isWithinRange").GetBoolean().Should().BeFalse();
            hot.GetProperty("listWarning").GetArrayLength().Should().Be(0, "đã có khắc phục");
            (await c.PostFormAsync("/api/TemperatureLog/Insert", ("StorageZoneId", zone), ("MeasurementType", "STORAGE"), ("TemperatureC", 9m), ("IsWithinRange", true))).Ok();
            (await _factory.QueryAsync(db => db.TemperatureLog.AsNoTracking().SingleAsync(t => t.StorageZoneId == zone && t.TemperatureC == 9m))).IsWithinRange.Should().BeFalse();
        }

        [Fact]
        public async Task Waste_records_validate_type_update_batch_waste_and_list_totals()
        {
            var day = new DateTime(2026, 11, 21);
            _factory.Clock.FixedNow = day.AddHours(14);
            Guid dish = Guid.NewGuid(), shift = Guid.NewGuid(), plan = Guid.NewGuid(), po = Guid.NewGuid(), batch = Guid.NewGuid(), planned = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Dish.Add(new Dish { Id = dish, Code = "MONW1", Name = "Canh W1", Status = "Active" });
                db.Shift.Add(new Shift { Id = shift, Code = "CAW1", Name = "Ca W1", StartTime = new TimeSpan(10, 0, 0), Status = "Active" });
                db.ProductionPlan.Add(new ProductionPlan { Id = plan, PlanNumber = "KH-W1", ProductionDate = day, PlanDate = day, Status = "Released" });
                db.ProductionOrder.Add(new ProductionOrder { Id = po, ProductionPlanId = plan, DishId = dish, RecipeId = Guid.NewGuid(), ShiftId = shift, PlannedQty = 100, Status = "InProgress", ProductionOrderNumber = "PH-W1", PlannedStartDate = day });
                db.ProductionBatch.AddRange(
                    new ProductionBatch { Id = batch, ProductionOrderId = po, DishId = dish, BatchNumber = "ME-W1-01", StartDate = day, PlannedQuantity = 100, Status = "Packed", QcStatus = "PASS", WasteQty = 0 },
                    new ProductionBatch { Id = planned, ProductionOrderId = po, DishId = dish, BatchNumber = "ME-W1-02", StartDate = day, PlannedQuantity = 100, Status = "Planned" });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();

            (await c.PostFormAsync("/api/MealWasteRecord/Record", ("ProductionBatchId", batch), ("WasteType", "XYZ"), ("QuantityMeals", 2m))).Fail("Loại hao hụt không hợp lệ");
            (await c.PostFormAsync("/api/MealWasteRecord/Record", ("ProductionBatchId", batch), ("WasteType", "SPOILAGE"))).Fail("Nhập số suất hoặc số kg");
            (await c.PostFormAsync("/api/MealWasteRecord/Record", ("ProductionBatchId", planned), ("WasteType", "SPOILAGE"), ("QuantityMeals", 1m))).Fail("đã bắt đầu nấu");
            (await c.PostFormAsync("/api/MealWasteRecord/Record", ("WasteType", "SPOILAGE"), ("QuantityMeals", 1m))).Fail("Chọn mẻ nấu, lệnh sản xuất hoặc món");

            var w1 = (await c.PostFormAsync("/api/MealWasteRecord/Record", ("ProductionBatchId", batch), ("WasteType", "returned"), ("QuantityMeals", 3m), ("Reason", "Khách trả"))).Ok();
            w1.GetProperty("record").GetProperty("wasteType").GetString().Should().Be("RETURNED");
            w1.GetProperty("record").GetProperty("shiftId").GetGuid().Should().Be(shift);
            w1.GetProperty("wasteTypeName").GetString().Should().Be("Khách trả lại");
            w1.GetProperty("batchNumber").GetString().Should().Be("ME-W1-01");
            (await c.PostFormAsync("/api/MealWasteRecord/Record", ("ProductionBatchId", batch), ("WasteType", "SPOILAGE"), ("QuantityMeals", 2m), ("QuantityKg", 1.5m))).Ok();
            (await _factory.QueryAsync(db => db.ProductionBatch.AsNoTracking().SingleAsync(b => b.Id == batch))).WasteQty.Should().Be(5);
            (await c.PostFormAsync("/api/MealWasteRecord/Record", ("DishId", dish), ("ShiftId", shift), ("WasteType", "PREP_LOSS"), ("QuantityKg", 4m))).Ok();

            var list = (await c.GetResultAsync($"/api/MealWasteRecord/GetList?From=2026-11-21&To=2026-11-21&DishId={dish}")).Ok();
            list.GetProperty("listItem").GetArrayLength().Should().Be(3);
            list.GetProperty("totalMeals").GetDecimal().Should().Be(5);
            list.GetProperty("totalKg").GetDecimal().Should().Be(5.5m);
            list.GetProperty("mealsByType").GetProperty("RETURNED").GetDecimal().Should().Be(3);
            (await c.PostFormAsync("/api/MealWasteRecord/Insert", ("DishId", dish), ("WasteType", "SPOILAGE"), ("QuantityMeals", 1m))).Fail("MealWasteRecord/Record");
        }

        [Fact]
        public async Task Order_board_menu_list_and_complaint_list_return_counts_and_overdue()
        {
            var day = new DateTime(2026, 11, 22);
            _factory.Clock.FixedNow = day.AddHours(10);
            Guid branch = Guid.NewGuid(), customer = Guid.NewGuid(), other = Guid.NewGuid(), menu = Guid.NewGuid(), menu2 = Guid.NewGuid(), dish1 = Guid.NewGuid(), dish2 = Guid.NewGuid();
            Guid o1 = Guid.NewGuid(), o2 = Guid.NewGuid(), o3 = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Customer.AddRange(
                    new Customer { Id = customer, Code = "KB1", Name = "[DEMO] Khách Bảng", DisplayName = "Khách Bảng", CIF = "KB1", CustomerType = "FACTORY", Status = "Active", BranchId = branch },
                    new Customer { Id = other, Code = "KB2", Name = "[DEMO] Khách khác", DisplayName = "Khách khác", CIF = "KB2", CustomerType = "FACTORY", Status = "Active" });
                db.Dish.AddRange(new Dish { Id = dish1, Code = "D1B", Name = "Món 1", Status = "Active" }, new Dish { Id = dish2, Code = "D2B", Name = "Món 2", Status = "Active" });
                db.Menu.AddRange(
                    new AppDelicare.Model.Entity.Menu { Id = menu, Code = "TD-BANG", Name = "Thực đơn bảng", VersionNo = 1, Status = "Published", EffectiveFrom = day.AddDays(-10), EffectiveTo = day.AddDays(10) },
                    new AppDelicare.Model.Entity.Menu { Id = menu2, Code = "TD-BANG-CU", Name = "Thực đơn cũ", VersionNo = 1, Status = "Expired", EffectiveFrom = day.AddDays(-60), EffectiveTo = day.AddDays(-30) });
                db.MenuLine.AddRange(
                    new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = dish1, MealType = "TRUA", DayOfWeek = 1, SortOrder = 1 },
                    new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = dish2, MealType = "TRUA", DayOfWeek = 1, SortOrder = 2 },
                    new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = dish1, MealType = "TRUA", DayOfWeek = 2, SortOrder = 1 });
                db.MenuAssignment.AddRange(
                    new MenuAssignment { Id = Guid.NewGuid(), CustomerId = customer, MenuId = menu, MealType = "TRUA", Status = "Active" },
                    new MenuAssignment { Id = Guid.NewGuid(), CustomerId = customer, MenuId = menu, MealType = "TOI", Status = "Active" },
                    new MenuAssignment { Id = Guid.NewGuid(), CustomerId = other, MenuId = menu, MealType = "TRUA", Status = "Inactive" });
                db.Order.AddRange(
                    new Order { Id = o1, OrderNumber = "PH-BANG-1", CustomerId = customer, OrderDate = day.AddDays(-2), MealDate = day, DeliveryDate = day, Status = "Confirmed", TotalQuantity = 70 },
                    new Order { Id = o2, OrderNumber = "PH-BANG-2", CustomerId = customer, OrderDate = day.AddDays(-2), MealDate = day, DeliveryDate = day, Status = "Cancelled", TotalQuantity = 20 },
                    new Order { Id = o3, OrderNumber = "PH-BANG-3", CustomerId = other, OrderDate = day.AddDays(-2), MealDate = day, DeliveryDate = day, Status = "Draft", TotalQuantity = 10 });
                db.OrderLine.AddRange(
                    new OrderLine { Id = Guid.NewGuid(), OrderId = o1, MenuId = menu, MealType = "TRUA", Quantity = 70, LineAmount = 0 },
                    new OrderLine { Id = Guid.NewGuid(), OrderId = o2, MenuId = menu, MealType = "TRUA", Quantity = 20, LineAmount = 0 },
                    new OrderLine { Id = Guid.NewGuid(), OrderId = o3, MenuId = menu, MealType = "TRUA", Quantity = 10, LineAmount = 0 });
                db.CustomerComplaint.AddRange(
                    new CustomerComplaint { Id = Guid.NewGuid(), ComplaintNo = "PA-B-1", CustomerId = customer, OrderId = o1, Severity = "High", Description = "Thiếu canh", ReceivedAt = day.AddHours(2), SlaDueAt = day.AddHours(6), ComplaintStatus = "Open" },
                    new CustomerComplaint { Id = Guid.NewGuid(), ComplaintNo = "PA-B-2", CustomerId = customer, Severity = "Low", Description = "Muộn", ReceivedAt = day.AddHours(1), SlaDueAt = day.AddHours(73), ComplaintStatus = "InProgress" },
                    new CustomerComplaint { Id = Guid.NewGuid(), ComplaintNo = "PA-B-3", CustomerId = customer, Severity = "High", Description = "Đã xong", ReceivedAt = day.AddHours(-20), SlaDueAt = day.AddHours(-16), ComplaintStatus = "Closed" });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();

            // Bảng đơn: 8 cột trạng thái theo thứ tự STATUS_CATALOG + đếm; lọc chi nhánh của khách.
            var board = (await c.GetResultAsync("/api/Order/GetBoard?MealDate=2026-11-22")).Ok();
            board.GetProperty("listColumn").EnumerateArray().Select(col => col.GetProperty("status").GetString()).Take(8)
                .Should().Equal("Draft", "Submitted", "Confirmed", "Locked", "InProduction", "Delivered", "Reconciled", "Cancelled");
            board.GetProperty("statusCounts").GetProperty("Confirmed").GetInt32().Should().Be(1);
            board.GetProperty("statusCounts").GetProperty("Cancelled").GetInt32().Should().Be(1);
            var confirmed = board.GetProperty("listColumn").EnumerateArray().Single(col => col.GetProperty("status").GetString() == "Confirmed");
            confirmed.GetProperty("listItem")[0].GetProperty("customerName").GetString().Should().Be("Khách Bảng");
            confirmed.GetProperty("totalQuantity").GetDecimal().Should().Be(70);
            var byBranch = (await c.GetResultAsync($"/api/Order/GetBoard?MealDate=2026-11-22&BranchId={branch}")).Ok();
            byBranch.GetProperty("totalRecords").GetInt32().Should().Be(2);
            (await c.GetResultAsync("/api/Order/GetBoard?MealDate=2026-11-22&SearchText=BANG-3")).Ok().GetProperty("totalRecords").GetInt32().Should().Be(1);

            // Thực đơn: 3 dòng / 2 món, 1 khách được phân công (Inactive không tính), suất đã đặt = 70 + 10 (đơn Hủy không tính).
            var menus = await c.GetResultAsync("/api/Menu/GetList?SearchText=TD-BANG&EffectiveOn=2026-11-22");
            var m = menus.ListData.EnumerateArray().Single();
            m.GetProperty("menu").GetProperty("code").GetString().Should().Be("TD-BANG");
            m.GetProperty("lineCount").GetInt32().Should().Be(3);
            m.GetProperty("dishCount").GetInt32().Should().Be(2);
            m.GetProperty("assignedCustomerCount").GetInt32().Should().Be(1);
            m.GetProperty("orderedMeals").GetDecimal().Should().Be(80);

            // Phản ánh: quá hạn xếp trước; lọc quá hạn; đếm theo trạng thái.
            var complaints = (await c.GetResultAsync($"/api/CustomerComplaint/GetList?CustomerId={customer}")).Ok();
            complaints.GetProperty("totalRecords").GetInt32().Should().Be(3);
            complaints.GetProperty("overdueCount").GetInt32().Should().Be(1, "PA-B-1 (High) hạn 06:00 đã qua lúc 10:00; PA-B-3 đã đóng không tính");
            var first = complaints.GetProperty("listItem")[0];
            first.GetProperty("complaint").GetProperty("complaintNo").GetString().Should().Be("PA-B-1");
            first.GetProperty("isOverdue").GetBoolean().Should().BeTrue();
            first.GetProperty("hoursToDue").GetDecimal().Should().Be(-4);
            first.GetProperty("orderNumber").GetString().Should().Be("PH-BANG-1");
            first.GetProperty("severityName").GetString().Should().Be("Cao");
            complaints.GetProperty("statusCounts").GetProperty("Open").GetInt32().Should().Be(1);
            var overdue = (await c.GetResultAsync($"/api/CustomerComplaint/GetList?CustomerId={customer}&Overdue=true")).Ok();
            overdue.GetProperty("listItem").EnumerateArray().Select(x => x.GetProperty("complaint").GetProperty("complaintNo").GetString()).Should().Equal("PA-B-1");
            (await c.GetResultAsync($"/api/CustomerComplaint/GetList?CustomerId={customer}&Severity=low")).Ok().GetProperty("totalRecords").GetInt32().Should().Be(1);
        }

        [Fact]
        public async Task Approval_detail_shows_business_record_and_requester_can_cancel_returning_it_to_draft()
        {
            _factory.Clock.FixedNow = new DateTime(2026, 11, 23, 9, 0, 0);
            Guid dish = Guid.NewGuid(), recipe = Guid.NewGuid(), request = Guid.NewGuid(), step = Guid.NewGuid(), other = Guid.NewGuid();
            Guid otherRequest = Guid.NewGuid(), otherRecipe = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Dish.Add(new Dish { Id = dish, Code = "MONAP", Name = "Bò kho AP", Status = "Active" });
                db.Recipe.AddRange(
                    new Recipe { Id = recipe, DishId = dish, VersionNo = 2, VersionCode = "v2", Status = "PendingApproval", StandardOutputQty = 100 },
                    new Recipe { Id = otherRecipe, DishId = dish, VersionNo = 3, VersionCode = "v3", Status = "PendingApproval", StandardOutputQty = 100 });
                db.RecipeLine.Add(new RecipeLine { Id = Guid.NewGuid(), RecipeId = recipe, IngredientId = Guid.NewGuid(), Quantity = 12, UnitId = Guid.NewGuid(), SortOrder = 1 });
                db.ApprovalRequest.AddRange(
                    new ApprovalRequest { Id = request, RequestNo = "XN-AP-01", BusinessType = "RECIPE_VERSION", BusinessId = recipe, RequestStatus = "Pending", CurrentStepNo = 1,
                        RequestedAt = new DateTime(2026, 11, 23, 8, 0, 0), RequestedBy = InMemoryApiFactory.UserId, PayloadJson = "{}" },
                    new ApprovalRequest { Id = otherRequest, RequestNo = "XN-AP-02", BusinessType = "RECIPE_VERSION", BusinessId = otherRecipe, RequestStatus = "Pending", CurrentStepNo = 1,
                        RequestedAt = new DateTime(2026, 11, 23, 8, 0, 0), RequestedBy = other, PayloadJson = "{}" });
                db.ApprovalStep.Add(new ApprovalStep { Id = step, ApprovalRequestId = request, StepNo = 1, DecisionStatus = "Waiting", AssignedAt = new DateTime(2026, 11, 23, 8, 0, 0) });
                db.User.Add(new User { Id = other, UserName = "nguoi-gui-khac", IsActive = true });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();

            var detail = (await c.GetResultAsync($"/api/ApprovalRequest/GetDetail?id={request}")).Ok();
            detail.GetProperty("statusName").GetString().Should().Be("Chờ duyệt");
            detail.GetProperty("screenId").GetString().Should().Be("menu-recipe");
            detail.GetProperty("title").GetString().Should().Be("Công thức v2 – Bò kho AP");
            detail.GetProperty("business").GetProperty("table").GetString().Should().Be("Recipe");
            detail.GetProperty("business").GetProperty("record").GetProperty("versionCode").GetString().Should().Be("v2");
            detail.GetProperty("business").GetProperty("lines").GetArrayLength().Should().Be(1);
            detail.GetProperty("listStep").GetArrayLength().Should().Be(1);
            detail.GetProperty("canDecide").GetBoolean().Should().BeTrue("admin");
            detail.GetProperty("canCancel").GetBoolean().Should().BeTrue();

            // Người thường không phải người gửi → không hủy được yêu cầu của người khác (admin thì được – test dùng admin nên kiểm bằng user khác).
            using (var stranger = _factory.CreateAuthorizedClient(other))
            {
                (await stranger.PostFormAsync("/api/ApprovalRequest/Cancel", ("Id", request), ("Reason", "x"))).Fail("Chỉ người gửi");
                (await stranger.GetResultAsync($"/api/ApprovalRequest/GetDetail?id={request}")).Fail("không có quyền");
            }
            (await c.PostFormAsync("/api/ApprovalRequest/Cancel", ("Id", request))).Fail("lý do");
            (await c.PostFormAsync("/api/ApprovalRequest/Cancel", ("Id", request), ("Reason", "Gửi nhầm phiên bản"))).Ok();
            (await _factory.QueryAsync(db => db.Recipe.AsNoTracking().SingleAsync(r => r.Id == recipe))).Status.Should().Be("Draft");
            var after = (await c.GetResultAsync($"/api/ApprovalRequest/GetDetail?id={request}")).Ok();
            after.GetProperty("request").GetProperty("requestStatus").GetString().Should().Be("Cancelled");
            after.GetProperty("listStep")[0].GetProperty("decisionStatus").GetString().Should().Be("Cancelled");
            after.GetProperty("listDecision")[0].GetProperty("decision").GetProperty("decisionCode").GetString().Should().Be("CANCEL");
            after.GetProperty("canCancel").GetBoolean().Should().BeFalse();
            (await c.PostFormAsync("/api/ApprovalRequest/Cancel", ("Id", request), ("Reason", "lần 2"))).Fail("không còn chờ duyệt");
            // Admin hủy được yêu cầu của người khác.
            (await c.PostFormAsync("/api/ApprovalRequest/Cancel", ("Id", otherRequest), ("Reason", "Admin hủy"))).Ok();
        }

        [Fact]
        public async Task Trace_goes_forward_from_a_lot_to_customers_and_backward_from_a_batch_to_the_supplier()
        {
            var day = new DateTime(2026, 11, 24);
            _factory.Clock.FixedNow = day.AddHours(15);
            Guid supplier = Guid.NewGuid(), po = Guid.NewGuid(), gr = Guid.NewGuid(), grLine = Guid.NewGuid(), lot = Guid.NewGuid(), ingredient = Guid.NewGuid(), warehouse = Guid.NewGuid();
            Guid dish = Guid.NewGuid(), plan = Guid.NewGuid(), prodOrder = Guid.NewGuid(), batch1 = Guid.NewGuid(), batch2 = Guid.NewGuid(), issue = Guid.NewGuid(), shift = Guid.NewGuid();
            Guid customerA = Guid.NewGuid(), customerB = Guid.NewGuid(), orderA = Guid.NewGuid(), orderB = Guid.NewGuid(), menu = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Supplier.Add(new Supplier { Id = supplier, Code = "NCC-TR", Name = "NCC thịt TR", Status = "Active" });
                db.Ingredient.Add(new Ingredient { Id = ingredient, Code = "BO-TR", Name = "Thịt bò TR", Status = "Active" });
                db.PurchaseOrder.Add(new PurchaseOrder { Id = po, PurchaseOrderNumber = "PO-TR-01", SupplierId = supplier, OrderDate = day.AddDays(-3), Status = "Received", TotalAmount = 10_000_000 });
                db.GoodsReceipt.Add(new GoodsReceipt { Id = gr, ReceiptNumber = "PH-GR-TR", PurchaseOrderId = po, SupplierId = supplier, WarehouseId = warehouse, ReceiptDate = day.AddDays(-1), Status = "Posted" });
                db.GoodsReceiptLine.Add(new GoodsReceiptLine { Id = grLine, GoodsReceiptId = gr, IngredientId = ingredient, Quantity = 50, ReceivedQuantity = 50, AcceptedQuantity = 50, LotId = lot });
                db.MaterialLot.Add(new MaterialLot { Id = lot, LotNumber = "NL-TR-01", LotNo = "NL-TR-01", IngredientId = ingredient, SupplierId = supplier, GoodsReceiptLineId = grLine, QcStatus = "DAT", Status = "Available",
                    ReceivedDate = day.AddDays(-1), ExpiryDate = day.AddDays(3) });
                db.TemperatureLog.Add(new TemperatureLog { Id = Guid.NewGuid(), MaterialLotId = lot, MeasurementType = "RECEIVING", TemperatureC = 3, IsWithinRange = true, MeasuredAt = day.AddDays(-1) });
                db.Shift.Add(new Shift { Id = shift, Code = "CATR", Name = "Trưa TR", StartTime = new TimeSpan(10, 30, 0), Status = "Active" });
                db.Dish.Add(new Dish { Id = dish, Code = "MONTR", Name = "Bò kho TR", Status = "Active" });
                db.ProductionPlan.Add(new ProductionPlan { Id = plan, PlanNumber = "KH-TR", ProductionDate = day, PlanDate = day, Status = "Released" });
                db.ProductionOrder.Add(new ProductionOrder { Id = prodOrder, ProductionPlanId = plan, DishId = dish, RecipeId = Guid.NewGuid(), ShiftId = shift, PlannedQty = 100, Status = "InProgress", ProductionOrderNumber = "PH-SX-TR", PlannedStartDate = day });
                db.ProductionBatch.AddRange(
                    new ProductionBatch { Id = batch1, ProductionOrderId = prodOrder, DishId = dish, BatchNumber = "ME-TR-01", StartDate = day.AddHours(7), PlannedQuantity = 50, AcceptedQty = 50, Status = "Packed", QcStatus = "PASS" },
                    new ProductionBatch { Id = batch2, ProductionOrderId = prodOrder, DishId = dish, BatchNumber = "ME-TR-02", StartDate = day.AddHours(8), PlannedQuantity = 50, AcceptedQty = 50, Status = "Packed", QcStatus = "PASS" });
                db.MaterialIssue.Add(new MaterialIssue { Id = issue, IssueNumber = "PH-XK-TR", ProductionOrderId = prodOrder, WarehouseId = warehouse, IssueDate = day.AddHours(6), Status = "Issued", Note = "Mẻ ME-TR-01 – xuất cho mẻ" });
                db.MaterialIssueLine.Add(new MaterialIssueLine { Id = Guid.NewGuid(), MaterialIssueId = issue, IngredientId = ingredient, MaterialLotId = lot, LotId = lot, Quantity = 20 });
                db.TemperatureLog.Add(new TemperatureLog { Id = Guid.NewGuid(), ProductionBatchId = batch1, MeasurementType = "CORE", TemperatureC = 78, IsWithinRange = true, MeasuredAt = day.AddHours(8) });
                db.Customer.AddRange(
                    new Customer { Id = customerA, Code = "KTR-A", Name = "[DEMO] Khách TR A", DisplayName = "Khách TR A", CIF = "KTR-A", CustomerType = "FACTORY", Status = "Active" },
                    new Customer { Id = customerB, Code = "KTR-B", Name = "[DEMO] Khách TR B", DisplayName = "Khách TR B", CIF = "KTR-B", CustomerType = "FACTORY", Status = "Active" });
                db.Menu.Add(new AppDelicare.Model.Entity.Menu { Id = menu, Code = "TD-TR", Name = "TD TR", VersionNo = 1, Status = "Published" });
                db.MenuLine.Add(new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = dish, MealType = "TRUA", DayOfWeek = MenuDay(day), SequenceNo = 1, SortOrder = 1 });
                db.Order.AddRange(
                    new Order { Id = orderA, OrderNumber = "PH-TR-A", CustomerId = customerA, OrderDate = day.AddDays(-2), MealDate = day, DeliveryDate = day, Status = "Delivered", TotalQuantity = 60 },
                    new Order { Id = orderB, OrderNumber = "PH-TR-B", CustomerId = customerB, OrderDate = day.AddDays(-2), MealDate = day, DeliveryDate = day, Status = "Delivered", TotalQuantity = 40 });
                db.OrderLine.AddRange(
                    new OrderLine { Id = Guid.NewGuid(), OrderId = orderA, MenuId = menu, MealType = "TRUA", ShiftId = shift, Quantity = 60, LineAmount = 0 },
                    new OrderLine { Id = Guid.NewGuid(), OrderId = orderB, MenuId = menu, MealType = "TRUA", ShiftId = shift, Quantity = 40, LineAmount = 0 });
                // Mẻ 1 có lệnh đóng gói cho đơn A (liên kết trực tiếp); mẻ 2 đóng gói thẳng (đơn suy ra).
                db.PackingOrder.Add(new PackingOrder { Id = Guid.NewGuid(), PackingOrderNumber = "PH-DG-TR", ProductionBatchId = batch1, OrderId = orderA, PlannedQty = 50, PackingDate = day.AddHours(9), Status = "Packed" });
                db.DeliveryOrder.Add(new DeliveryOrder { Id = Guid.NewGuid(), DeliveryNumber = "PH-GH-TR-A", OrderId = orderA, CustomerId = customerA, DeliveryDate = day, PlannedDeliveryTime = day.AddHours(11), Status = "Delivered", Note = "[SUAT:60]" });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();

            // Xuôi từ lô: nguồn gốc (phiếu nhập, PO, NCC, nhiệt độ nhận) + phiếu xuất → mẻ 1 (khớp Note) → lệnh đóng gói → đơn A → đơn giao → khách A.
            var forward = (await c.GetResultAsync($"/api/MaterialLot/Trace?id={lot}")).Ok();
            forward.GetProperty("direction").GetString().Should().Be("FORWARD");
            forward.GetProperty("listSupplier")[0].GetProperty("name").GetString().Should().Be("NCC thịt TR");
            forward.GetProperty("listPurchaseOrder")[0].GetProperty("number").GetString().Should().Be("PO-TR-01");
            forward.GetProperty("listGoodsReceipt")[0].GetProperty("number").GetString().Should().Be("PH-GR-TR");
            forward.GetProperty("listIssue")[0].GetProperty("quantity").GetDecimal().Should().Be(20);
            forward.GetProperty("listBatch").EnumerateArray().Select(b => b.GetProperty("number").GetString()).Should().Equal("ME-TR-01");
            forward.GetProperty("listPackingOrder")[0].GetProperty("number").GetString().Should().Be("PH-DG-TR");
            forward.GetProperty("listOrder").EnumerateArray().Select(o => (o.GetProperty("number").GetString(), o.GetProperty("inferred").GetBoolean())).Should().Equal(("PH-TR-A", false));
            forward.GetProperty("listDeliveryOrder")[0].GetProperty("quantity").GetDecimal().Should().Be(60);
            forward.GetProperty("listCustomer").EnumerateArray().Select(x => x.GetProperty("number").GetString()).Should().Equal("KTR-A");
            forward.GetProperty("listTemperature").EnumerateArray().Select(t => t.GetProperty("number").GetString()).Should().Contain("RECEIVING");

            // Ngược từ mẻ 2 (không có phiếu xuất riêng, không có lệnh đóng gói): cảnh báo + đơn suy ra theo ngày / ca / món (A, B).
            var back2 = (await c.GetResultAsync($"/api/ProductionBatch/Trace?id={batch2}")).Ok();
            back2.GetProperty("listLot").GetArrayLength().Should().Be(0);
            back2.GetProperty("listWarning").EnumerateArray().Select(w => w.GetString()).Should().Contain(w => w!.Contains("chưa có phiếu xuất"));
            back2.GetProperty("listOrder").EnumerateArray().Select(o => (o.GetProperty("number").GetString(), o.GetProperty("inferred").GetBoolean()))
                .Should().BeEquivalentTo([("PH-TR-A", true), ("PH-TR-B", true)]);
            // Ngược từ mẻ 1: lô NL-TR-01 → phiếu nhập → PO → NCC; nhiệt độ lõi 78 °C.
            var back1 = (await c.GetResultAsync($"/api/ProductionBatch/Trace?id={batch1}")).Ok();
            back1.GetProperty("direction").GetString().Should().Be("BACKWARD");
            back1.GetProperty("listLot")[0].GetProperty("number").GetString().Should().Be("NL-TR-01");
            back1.GetProperty("listSupplier")[0].GetProperty("number").GetString().Should().Be("NCC-TR");
            back1.GetProperty("listTemperature").EnumerateArray().Select(t => t.GetProperty("quantity").GetDecimal()).Should().Contain(78m);
            back1.GetProperty("listCustomer").EnumerateArray().Select(x => x.GetProperty("number").GetString()).Should().Equal("KTR-A");

            // Danh sách lô: còn 3 ngày tới hạn, lọc sắp hết hạn trong 3 ngày.
            var lots = await c.GetResultAsync($"/api/MaterialLot/GetList?IngredientId={ingredient}&ExpiringWithinDays=3");
            var item = lots.ListData.EnumerateArray().Single();
            item.GetProperty("daysToExpiry").GetInt32().Should().Be(3);
            item.GetProperty("isBlocked").GetBoolean().Should().BeFalse();
            item.GetProperty("supplierName").GetString().Should().Be("NCC thịt TR");
            (await c.GetResultAsync($"/api/MaterialLot/GetList?IngredientId={ingredient}&ExpiringWithinDays=2")).ListData.GetArrayLength().Should().Be(0);
            (await c.GetResultAsync($"/api/MaterialLot/Trace?id={Guid.NewGuid()}")).Fail("Không tìm thấy lô");
        }
    }
}
