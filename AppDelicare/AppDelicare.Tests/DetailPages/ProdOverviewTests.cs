using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.Json;

namespace AppDelicare.Tests.DetailPages
{
    /// <summary>
    /// UI-6 (D-80) nhóm C – trang chi tiết toàn màn Mẻ nấu / Lệnh SX / Kế hoạch SX / Món ăn / Chuyến giao (…/GetOverview):
    /// có dữ liệu, id sai (Fail "không tìm thấy"), dữ liệu con rỗng vẫn mở được, quyền theo màn gốc (xem màn gốc → 200, màn khác → 403).
    /// </summary>
    public class ProdOverviewTests(InMemoryApiFactory factory) : IClassFixture<InMemoryApiFactory>
    {
        private static readonly DateTime Day = new(2026, 12, 21);

        private async Task<Guid> UserAsync(params string[] viewLinks)
        {
            Guid userId = Guid.NewGuid(), group = Guid.NewGuid();
            await factory.SeedAsync(async db =>
            {
                db.User.Add(new User { Id = userId, UserName = "dpc-" + userId.ToString("N")[..6], IsActive = true });
                db.UserGroup.Add(new UserGroup { Id = group, Name = "DPC-" + group.ToString("N")[..6] });
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

        private async Task PermissionAsync(string url, string screen, string otherScreen)
        {
            using var viewer = factory.CreateAuthorizedClient(await UserAsync("#/" + screen));
            (await viewer.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.OK, "có quyền xem " + screen);
            using var other = factory.CreateAuthorizedClient(await UserAsync("#/" + otherScreen));
            (await other.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Forbidden, "chỉ có quyền " + otherScreen);
        }

        private sealed record Prod(Guid Dish, Guid EmptyDish, Guid Plan, Guid EmptyPlan, Guid Order, Guid EmptyOrder, Guid Batch, Guid EmptyBatch);

        private async Task<Prod> SeedProdAsync(string code)
        {
            Guid shift = Guid.NewGuid(), line = Guid.NewGuid(), dish = Guid.NewGuid(), emptyDish = Guid.NewGuid(), recipe = Guid.NewGuid(), plan = Guid.NewGuid(), emptyPlan = Guid.NewGuid();
            Guid order = Guid.NewGuid(), emptyOrder = Guid.NewGuid(), batch = Guid.NewGuid(), batch2 = Guid.NewGuid(), emptyBatch = Guid.NewGuid(), emp = Guid.NewGuid(), menu = Guid.NewGuid();
            Guid cat = Guid.NewGuid(), kg = Guid.NewGuid(), ing = Guid.NewGuid();
            await factory.SeedAsync(db =>
            {
                db.Shift.Add(new Shift { Id = shift, Code = "CA-" + code, Name = "Ca trưa " + code, StartTime = new TimeSpan(10, 30, 0), EndTime = new TimeSpan(13, 30, 0), Status = "Active" });
                db.ProductionLine.Add(new ProductionLine { Id = line, Code = "DC-" + code, Name = "Bếp nóng " + code, CapacityMealPerBatch = 100, MinutesPerBatch = 60, Status = "Active" });
                db.Employee.Add(new Employee { Id = emp, Code = "NV-" + code, EmployeeCode = "NV-" + code, FullName = "Bếp trưởng " + code, Status = "Active" });
                db.DishCategory.Add(new DishCategory { Id = cat, Code = "NM-" + code, Name = "Món mặn " + code });
                db.UnitMeasure.Add(new UnitMeasure { Id = kg, Code = "KG-" + code, Name = "kg" });
                db.Ingredient.Add(new Ingredient { Id = ing, Code = "NL-" + code, Name = "Thịt " + code, BaseUnitId = kg });
                db.Dish.AddRange(
                    new Dish { Id = dish, Code = "MON-" + code, Name = "Thịt kho " + code, Status = "Active", DishCategoryId = cat, StandardCost = 18000 },
                    new Dish { Id = emptyDish, Code = "MON-" + code + "-RONG", Name = "Món rỗng " + code, Status = "Active" });
                db.Recipe.Add(new Recipe { Id = recipe, DishId = dish, VersionNo = 1, VersionCode = "v1", Status = "Locked", IsActive = true, StandardOutputQty = 100 });
                db.RecipeLine.Add(new RecipeLine { Id = Guid.NewGuid(), RecipeId = recipe, IngredientId = ing, Quantity = 10, UnitId = kg });
                db.Menu.Add(new Model.Entity.Menu { Id = menu, Code = "TD-" + code, Name = "Thực đơn " + code, VersionNo = 1, Status = "Published", EffectiveFrom = Day.AddDays(-30) });
                db.MenuLine.AddRange(
                    new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = dish, MealType = "TRUA", DayOfWeek = 1, SequenceNo = 1 },
                    new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = dish, MealType = "TOI", DayOfWeek = 2, SequenceNo = 1 });
                db.ProductionPlan.AddRange(
                    new ProductionPlan { Id = plan, PlanNumber = "KH-" + code, PlanNo = "KH-" + code, ProductionDate = Day, PlanDate = Day.AddDays(-1), Status = "Released", ShiftId = shift },
                    new ProductionPlan
                    {
                        Id = emptyPlan, PlanNumber = "KH-" + code + "-RONG", PlanNo = "KH-" + code + "-RONG", ProductionDate = Day.AddDays(1), PlanDate = Day, Status = "Draft",
                        Note = "{\"AutoRun\":\"Hệ thống KHÔNG tự phát hành – chờ COO duyệt: lệch suất lớn\"}",
                    });
                db.ProductionPlanLine.Add(new ProductionPlanLine { Id = Guid.NewGuid(), ProductionPlanId = plan, DishId = dish, PlannedQuantity = 200, PlannedQty = 200, Status = "Released" });
                db.ProductionOrder.AddRange(
                    new ProductionOrder
                    {
                        Id = order, ProductionOrderNumber = "LSX-" + code, ProductionOrderNo = "LSX-" + code, ProductionPlanId = plan, DishId = dish, RecipeId = recipe, ShiftId = shift,
                        ProductionLineId = line, ResponsibleEmployeeId = emp, PlannedQty = 200, Status = "InProgress", PlannedStartDate = Day.AddHours(7),
                    },
                    new ProductionOrder
                    {
                        Id = emptyOrder, ProductionOrderNumber = "LSX-" + code + "-RONG", ProductionOrderNo = "LSX-" + code + "-RONG", ProductionPlanId = plan, DishId = dish, RecipeId = recipe,
                        PlannedQty = 50, Status = "Released", PlannedStartDate = Day.AddHours(7),
                    });
                db.ProductionBatch.AddRange(
                    new ProductionBatch
                    {
                        Id = batch, BatchNumber = "ME-" + code + "-1", BatchNo = "ME-" + code + "-1", ProductionOrderId = order, DishId = dish, RecipeId = recipe, PlannedQuantity = 100,
                        ActualQuantity = 100, AcceptedQty = 98, StartDate = Day.AddHours(7), Status = "Passed", QcStatus = "PASS",
                    },
                    new ProductionBatch
                    {
                        Id = batch2, BatchNumber = "ME-" + code + "-2", BatchNo = "ME-" + code + "-2", ProductionOrderId = order, DishId = dish, RecipeId = recipe, PlannedQuantity = 100,
                        StartDate = Day.AddHours(8), Status = "Failed", QcStatus = "REJECT",
                    },
                    new ProductionBatch
                    {
                        Id = emptyBatch, BatchNumber = "ME-" + code + "-R", BatchNo = "ME-" + code + "-R", ProductionOrderId = emptyOrder, DishId = dish, RecipeId = recipe, PlannedQuantity = 50,
                        StartDate = Day.AddHours(9), Status = "Planned",
                    });
                db.TemperatureLog.AddRange(
                    new TemperatureLog { Id = Guid.NewGuid(), ProductionBatchId = batch, MeasurementType = "CORE", TemperatureC = 65, MinAllowedC = 70, IsWithinRange = false, MeasuredAt = Day.AddHours(7).AddMinutes(40), CorrectiveAction = "Gia nhiệt lại" },
                    new TemperatureLog { Id = Guid.NewGuid(), ProductionBatchId = batch, MeasurementType = "CORE", TemperatureC = 78, MinAllowedC = 70, IsWithinRange = true, MeasuredAt = Day.AddHours(7).AddMinutes(55) });
                db.MealWasteRecord.Add(new MealWasteRecord { Id = Guid.NewGuid(), ProductionBatchId = batch, ProductionOrderId = order, DishId = dish, WasteType = "QC_REJECT", QuantityMeals = 2, OccurredAt = Day.AddHours(9) });
                return Task.CompletedTask;
            });
            return new Prod(dish, emptyDish, plan, emptyPlan, order, emptyOrder, batch, emptyBatch);
        }

        [Fact]
        public async Task Batch_overview_returns_detail_plan_shift_counts_and_checks_permission()
        {
            var s = await SeedProdAsync("DPC1");
            using var admin = factory.CreateAuthorizedClient();
            var o = (await admin.GetResultAsync($"/api/ProductionBatch/GetOverview?id={s.Batch}")).Ok();
            o.GetProperty("detail").GetProperty("batch").GetProperty("batchNumber").GetString().Should().Be("ME-DPC1-1");
            o.GetProperty("planNumber").GetString().Should().Be("KH-DPC1");
            o.GetProperty("shiftName").GetString().Should().Be("Ca trưa DPC1");
            o.GetProperty("responsibleName").GetString().Should().Be("Bếp trưởng DPC1");
            o.GetProperty("temperatureCount").GetInt32().Should().Be(2);
            o.GetProperty("temperatureFailCount").GetInt32().Should().Be(1);
            o.GetProperty("wasteMeals").GetDecimal().Should().Be(2m);

            var e = (await admin.GetResultAsync($"/api/ProductionBatch/GetOverview?id={s.EmptyBatch}")).Ok();
            e.GetProperty("temperatureCount").GetInt32().Should().Be(0, "mẻ chưa đo vẫn mở được trang");
            e.GetProperty("responsibleName").ValueKind.Should().Be(JsonValueKind.Null);
            (await admin.GetResultAsync($"/api/ProductionBatch/GetOverview?id={Guid.NewGuid()}")).Fail("Không tìm thấy");

            await PermissionAsync($"/api/ProductionBatch/GetOverview?id={s.Batch}", "prod-batch", "scm-po");
        }

        [Fact]
        public async Task Order_overview_counts_batches_and_names_and_checks_permission()
        {
            var s = await SeedProdAsync("DPC2");
            using var admin = factory.CreateAuthorizedClient();
            var o = (await admin.GetResultAsync($"/api/ProductionOrder/GetOverview?id={s.Order}")).Ok();
            o.GetProperty("detail").GetProperty("productionOrder").GetProperty("productionOrderNumber").GetString().Should().Be("LSX-DPC2");
            o.GetProperty("planNumber").GetString().Should().Be("KH-DPC2");
            o.GetProperty("productionLineName").GetString().Should().Be("Bếp nóng DPC2");
            o.GetProperty("responsibleName").GetString().Should().Be("Bếp trưởng DPC2");
            o.GetProperty("batchCount").GetInt32().Should().Be(2);
            o.GetProperty("batchDoneCount").GetInt32().Should().Be(1);
            o.GetProperty("batchFailedCount").GetInt32().Should().Be(1);

            var e = (await admin.GetResultAsync($"/api/ProductionOrder/GetOverview?id={s.EmptyOrder}")).Ok();
            e.GetProperty("batchDoneCount").GetInt32().Should().Be(0);
            e.GetProperty("productionLineName").ValueKind.Should().Be(JsonValueKind.Null, "lệnh chưa gán dây chuyền vẫn mở được trang");
            (await admin.GetResultAsync($"/api/ProductionOrder/GetOverview?id={Guid.NewGuid()}")).Fail("Không tìm thấy");

            await PermissionAsync($"/api/ProductionOrder/GetOverview?id={s.Order}", "prod-order", "prod-batch");
        }

        [Fact]
        public async Task Plan_overview_counts_and_marks_held_plan_for_approval_tab()
        {
            var s = await SeedProdAsync("DPC3");
            using var admin = factory.CreateAuthorizedClient();
            var o = (await admin.GetResultAsync($"/api/ProductionPlan/GetOverview?id={s.Plan}")).Ok();
            o.GetProperty("detail").GetProperty("productionPlan").GetProperty("planNumber").GetString().Should().Be("KH-DPC3");
            o.GetProperty("shiftName").GetString().Should().Be("Ca trưa DPC3");
            o.GetProperty("dishCount").GetInt32().Should().Be(1);
            o.GetProperty("orderCount").GetInt32().Should().Be(2);
            o.GetProperty("batchCount").GetInt32().Should().Be(3);
            o.GetProperty("batchDoneCount").GetInt32().Should().Be(1);
            o.GetProperty("approvalRequestId").ValueKind.Should().Be(JsonValueKind.Null, "kế hoạch đã phát hành – không chờ duyệt");

            // Kế hoạch Nháp giữ chờ COO, chưa có dòng / lệnh → vẫn mở được; tab Duyệt dùng Id kế hoạch (ApprovalRequest/GetDetail nhận Id này).
            var h = (await admin.GetResultAsync($"/api/ProductionPlan/GetOverview?id={s.EmptyPlan}")).Ok();
            h.GetProperty("dishCount").GetInt32().Should().Be(0);
            h.GetProperty("approvalRequestId").GetGuid().Should().Be(s.EmptyPlan);
            h.GetProperty("heldReason").GetString().Should().Contain("chờ COO duyệt");
            var apr = (await admin.GetResultAsync($"/api/ApprovalRequest/GetDetail?id={s.EmptyPlan}")).Ok();
            apr.GetProperty("screenId").GetString().Should().Be("prod-plan");
            (await admin.GetResultAsync($"/api/ProductionPlan/GetOverview?id={Guid.NewGuid()}")).Fail("Không tìm thấy");

            await PermissionAsync($"/api/ProductionPlan/GetOverview?id={s.Plan}", "prod-plan", "prod-order");
        }

        [Fact]
        public async Task Dish_overview_returns_recipe_cost_menus_batches_and_checks_permission()
        {
            var s = await SeedProdAsync("DPC4");
            using var admin = factory.CreateAuthorizedClient();
            var o = (await admin.GetResultAsync($"/api/Dish/GetOverview?id={s.Dish}")).Ok();
            o.GetProperty("dish").GetProperty("code").GetString().Should().Be("MON-DPC4");
            o.GetProperty("categoryName").GetString().Should().Be("Món mặn DPC4");
            o.GetProperty("activeRecipe").GetProperty("versionCode").GetString().Should().Be("v1");
            o.GetProperty("ingredientCount").GetInt32().Should().Be(1);
            o.GetProperty("listRecipe").GetArrayLength().Should().Be(1);
            o.GetProperty("menuCount").GetInt32().Should().Be(1);
            var m = o.GetProperty("listMenu")[0];
            m.GetProperty("lineCount").GetInt32().Should().Be(2);
            m.GetProperty("listMealType").EnumerateArray().Select(x => x.GetString()).Should().BeEquivalentTo(["TOI", "TRUA"]);
            o.GetProperty("batchCount").GetInt32().Should().Be(3);
            o.GetProperty("portionsAccepted").GetDecimal().Should().Be(98m);
            o.GetProperty("qcPassPercent").GetDecimal().Should().Be(50m, "1 Đạt / (1 Đạt + 1 Loại)");
            o.GetProperty("cost").ValueKind.Should().Be(JsonValueKind.Object);

            var e = (await admin.GetResultAsync($"/api/Dish/GetOverview?id={s.EmptyDish}")).Ok();
            e.GetProperty("activeRecipe").ValueKind.Should().Be(JsonValueKind.Null, "món chưa có công thức vẫn mở được trang");
            e.GetProperty("menuCount").GetInt32().Should().Be(0);
            e.GetProperty("qcPassPercent").ValueKind.Should().Be(JsonValueKind.Null);
            (await admin.GetResultAsync($"/api/Dish/GetOverview?id={Guid.NewGuid()}")).Fail("Không tìm thấy");

            await PermissionAsync($"/api/Dish/GetOverview?id={s.Dish}", "menu-dish", "prod-plan");
        }

        [Fact]
        public async Task Route_overview_counts_stops_meals_complaints_and_checks_permission()
        {
            const string key = "DPC5";
            Guid customer = Guid.NewGuid(), order = Guid.NewGuid(), route = Guid.NewGuid(), empty = Guid.NewGuid(), vehicle = Guid.NewGuid(), driver = Guid.NewGuid();
            Guid do1 = Guid.NewGuid(), do2 = Guid.NewGuid();
            await factory.SeedAsync(db =>
            {
                db.Customer.Add(new Customer { Id = customer, Code = "C" + key, Name = "Khách " + key, CIF = "C" + key, DisplayName = "Khách " + key, CustomerType = "FACTORY", Status = "Active" });
                db.Order.Add(new Order { Id = order, OrderNumber = "DH-" + key, OrderNo = "DH-" + key, CustomerId = customer, OrderDate = Day.AddDays(-2), MealDate = Day, DeliveryDate = Day, Status = "InProduction" });
                db.Vehicle.Add(new Vehicle { Id = vehicle, Code = "X" + key, LicensePlate = "51C-" + key, Capacity = 500 });
                db.Employee.Add(new Employee { Id = driver, Code = "TX" + key, EmployeeCode = "TX" + key, FullName = "Tài xế " + key, Status = "Active" });
                db.DeliveryRoute.AddRange(
                    new DeliveryRoute { Id = route, RouteNo = "CX-" + key, Code = "CX-" + key, DeliveryDate = Day, VehicleId = vehicle, DriverId = driver, Status = "InTransit" },
                    new DeliveryRoute { Id = empty, RouteNo = "CX-" + key + "-RONG", Code = "CX-" + key + "-RONG", DeliveryDate = Day, VehicleId = vehicle, Status = "Planned" });
                db.DeliveryOrder.AddRange(
                    new DeliveryOrder { Id = do1, DeliveryNumber = "PG-" + key, DeliveryNo = "PG-" + key, OrderId = order, CustomerId = customer, DeliveryDate = Day, PlannedDeliveryTime = Day.AddHours(11), DeliveryRouteId = route, Status = "Delivered" },
                    new DeliveryOrder { Id = do2, DeliveryNumber = "PG2-" + key, DeliveryNo = "PG2-" + key, OrderId = order, CustomerId = customer, DeliveryDate = Day, PlannedDeliveryTime = Day.AddHours(11), DeliveryRouteId = route, Status = "Failed" });
                db.DeliveryStop.AddRange(
                    new DeliveryStop { Id = Guid.NewGuid(), DeliveryRouteId = route, DeliveryOrderId = do1, CustomerId = customer, SequenceNo = 1, StopStatus = "Delivered", PlannedMealQty = 120, DeliveredMealQty = 118 },
                    new DeliveryStop { Id = Guid.NewGuid(), DeliveryRouteId = route, DeliveryOrderId = do2, CustomerId = customer, SequenceNo = 2, StopStatus = "Failed", PlannedMealQty = 80 });
                db.CustomerComplaint.AddRange(
                    new CustomerComplaint { Id = Guid.NewGuid(), ComplaintNo = "PA-" + key + "-1", CustomerId = customer, DeliveryRouteId = route, CategoryCode = "LATE", Description = "Giao trễ", ReceivedAt = Day.AddHours(12), ComplaintStatus = "Open" },
                    new CustomerComplaint { Id = Guid.NewGuid(), ComplaintNo = "PA-" + key + "-2", CustomerId = customer, DeliveryRouteId = route, CategoryCode = "SHORT", Description = "Thiếu suất", ReceivedAt = Day.AddHours(13), ComplaintStatus = "Resolved" });
                return Task.CompletedTask;
            });
            using var admin = factory.CreateAuthorizedClient();
            var o = (await admin.GetResultAsync($"/api/DeliveryRoute/GetOverview?id={route}")).Ok();
            o.GetProperty("detail").GetProperty("route").GetProperty("routeNo").GetString().Should().Be("CX-" + key);
            o.GetProperty("stopCount").GetInt32().Should().Be(2);
            o.GetProperty("deliveredStopCount").GetInt32().Should().Be(1);
            o.GetProperty("failedStopCount").GetInt32().Should().Be(1);
            o.GetProperty("plannedMeals").GetDecimal().Should().Be(200m);
            o.GetProperty("deliveredMeals").GetDecimal().Should().Be(118m);
            o.GetProperty("complaintCount").GetInt32().Should().Be(2);
            o.GetProperty("openComplaintCount").GetInt32().Should().Be(1);
            o.GetProperty("listComplaint")[0].GetProperty("complaintNo").GetString().Should().Be("PA-" + key + "-2", "mới nhất trước");
            o.GetProperty("listComplaint")[0].GetProperty("customerName").GetString().Should().Be("Khách " + key);

            var e = (await admin.GetResultAsync($"/api/DeliveryRoute/GetOverview?id={empty}")).Ok();
            e.GetProperty("stopCount").GetInt32().Should().Be(0, "chuyến chưa có điểm giao vẫn mở được trang");
            e.GetProperty("complaintCount").GetInt32().Should().Be(0);
            (await admin.GetResultAsync($"/api/DeliveryRoute/GetOverview?id={Guid.NewGuid()}")).Fail("Không tìm thấy");

            await PermissionAsync($"/api/DeliveryRoute/GetOverview?id={route}", "log-route", "prod-batch");
        }
    }
}
