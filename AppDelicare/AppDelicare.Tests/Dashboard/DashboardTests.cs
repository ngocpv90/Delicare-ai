using System.Net;
using System.Text.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Services.Implement;
using AppDelicare.Tests.Common;
using AppDelicare.Tests.Integration;
using FluentAssertions;

namespace AppDelicare.Tests.Dashboard
{
    /// <summary>
    /// BE3.5 – dashboard điều hành (Catalog 231, spec 12.1) và hành trình đơn hàng (232, spec 12.2): từng khối trên dữ liệu seed, số liệu khớp các màn danh sách
    /// (Order/GetBoard, ProductionBatch/GetBoard, DeliveryExecution/GetBoard, Common/GetAlerts), lọc ca / chi nhánh (đơn vị con), cache 60 giây theo (ngày, ca, chi nhánh)
    /// nhưng cảnh báo theo người, quyền (dash-exec; hành trình: flow-e2e / dash-exec / crm-order + chứng từ theo quyền Xem từng màn), không tên bảng / cột (D-58).
    /// Mỗi test dùng ngày / mã riêng (factory dùng chung).
    /// </summary>
    public class DashboardTests : IClassFixture<InMemoryApiFactory>
    {
        private static readonly DateTime Now = new(2031, 7, 20, 10, 0, 0);
        private readonly InMemoryApiFactory _factory;

        public DashboardTests(InMemoryApiFactory factory)
        {
            _factory = factory;
            _factory.Clock.FixedNow = Now;
        }

        private static string Key() => Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        private static string Day(DateTime d) => d.ToString("yyyy-MM-dd");

        private static int AlertCount(LegacyResult alerts, params string[] codes)
            => alerts.ListData.EnumerateArray().Where(a => codes.Contains(a.GetProperty("code").GetString())).Sum(a => a.GetProperty("count").GetInt32());

        private sealed record Seeded(Guid Branch, Guid Kitchen, Guid Other, Guid S1, Guid S2, Guid CustA, Guid CustB, Guid O1, Guid O4, string Key);

        /// <summary>Dữ liệu 1 ngày: 2 chi nhánh (chi nhánh có đơn vị con), 2 ca, đơn / mẻ / đơn giao / POD / ca làm / KPI.</summary>
        private async Task<Seeded> SeedDayAsync(DateTime day)
        {
            string key = Key();
            Guid company = Guid.NewGuid(), branch = Guid.NewGuid(), kitchen = Guid.NewGuid(), other = Guid.NewGuid();
            Guid s1 = Guid.NewGuid(), s2 = Guid.NewGuid(), custA = Guid.NewGuid(), custB = Guid.NewGuid();
            Guid o1 = Guid.NewGuid(), o2 = Guid.NewGuid(), o3 = Guid.NewGuid(), o4 = Guid.NewGuid(), o5 = Guid.NewGuid(), o6 = Guid.NewGuid();
            Guid dish = Guid.NewGuid(), recipe = Guid.NewGuid(), plan = Guid.NewGuid(), po1 = Guid.NewGuid(), po2 = Guid.NewGuid();
            Guid do1 = Guid.NewGuid(), do2 = Guid.NewGuid(), do3 = Guid.NewGuid(), do4 = Guid.NewGuid(), do6 = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.OrganizationUnit.AddRange(
                    new OrganizationUnit { Id = branch, CompanyId = company, Code = "CN-" + key, Name = "Chi nhánh " + key, UnitType = "BRANCH" },
                    new OrganizationUnit { Id = kitchen, CompanyId = company, ParentId = branch, Code = "BEP-" + key, Name = "Bếp " + key, UnitType = "KITCHEN" },
                    new OrganizationUnit { Id = other, CompanyId = company, Code = "CN2-" + key, Name = "Chi nhánh khác " + key, UnitType = "BRANCH" });
                db.Shift.AddRange(new Shift { Id = s1, Code = "S1" + key, Name = "Ca 1 " + key, StartTime = new TimeSpan(6, 0, 0), Status = "Active" },
                    new Shift { Id = s2, Code = "S2" + key, Name = "Ca 2 " + key, StartTime = new TimeSpan(14, 0, 0), Status = "Active" });
                // Khách A thuộc đơn vị con của chi nhánh → vẫn tính cho chi nhánh.
                db.Customer.AddRange(
                    new Customer { Id = custA, Code = "A" + key, Name = "Khách A " + key, CIF = "A" + key, DisplayName = "Khách A " + key, CustomerType = "FACTORY", Status = "Active", BranchId = kitchen },
                    new Customer { Id = custB, Code = "B" + key, Name = "Khách B " + key, CIF = "B" + key, DisplayName = "Khách B " + key, CustomerType = "FACTORY", Status = "Active", BranchId = other });
                void Ord(Guid id, Guid cust, int i, string status, DateTime meal, params (Guid Shift, decimal Qty)[] lines)
                {
                    db.Order.Add(new Order
                    {
                        Id = id, OrderNumber = $"DH-{key}-{i}", OrderNo = $"DH-{key}-{i}", CustomerId = cust, OrderDate = meal.AddDays(-2), MealDate = meal, DeliveryDate = meal,
                        Status = status, TotalQuantity = lines.Sum(l => l.Qty),
                    });
                    foreach (var (shift, qty) in lines)
                    {
                        db.OrderLine.Add(new OrderLine { Id = Guid.NewGuid(), OrderId = id, MealType = "TRUA", ShiftId = shift, Quantity = qty });
                    }
                }
                Ord(o1, custA, 1, OrderStatus.Confirmed, day, (s1, 100), (s2, 50));
                Ord(o2, custA, 2, OrderStatus.Draft, day, (s1, 30));
                Ord(o3, custA, 3, OrderStatus.Cancelled, day, (s1, 20));
                Ord(o4, custA, 4, OrderStatus.Locked, day, (s2, 40));
                Ord(o5, custB, 5, OrderStatus.Confirmed, day, (s1, 70));
                Ord(o6, custA, 6, OrderStatus.Confirmed, day.AddDays(-1), (s1, 10));

                // Sản xuất: 1 kế hoạch (đơn vị trống), lệnh ca 1 + ca 2, 5 mẻ (1 Hủy).
                db.ProductionPlan.Add(new ProductionPlan { Id = plan, PlanNumber = "KH-" + key, PlanNo = "KH-" + key, ProductionDate = day, PlanDate = day.AddDays(-1), Status = "Released" });
                db.ProductionOrder.AddRange(
                    new ProductionOrder { Id = po1, ProductionPlanId = plan, DishId = dish, RecipeId = recipe, ShiftId = s1, PlannedQty = 170, Status = "InProgress", ProductionOrderNumber = "SX1-" + key, PlannedStartDate = day.AddHours(6) },
                    new ProductionOrder { Id = po2, ProductionPlanId = plan, DishId = dish, RecipeId = recipe, ShiftId = s2, PlannedQty = 50, Status = "InProgress", ProductionOrderNumber = "SX2-" + key, PlannedStartDate = day.AddHours(14) });
                ProductionBatch Batch(Guid po, int i, string status, decimal planned, decimal? actual, decimal? acceptedQty, string? qc) => new()
                {
                    Id = Guid.NewGuid(), BatchNumber = $"ME-{key}-{i}", ProductionOrderId = po, DishId = dish, RecipeId = recipe, StartDate = day.AddHours(6), Status = status,
                    PlannedQuantity = planned, PlannedQty = planned, ActualQuantity = actual, ActualQty = actual, AcceptedQty = acceptedQty, QcStatus = qc,
                };
                db.ProductionBatch.AddRange(Batch(po1, 1, "Packed", 100, 100, 98, "PASS"), Batch(po1, 2, "Passed", 50, 50, null, "PASS"), Batch(po1, 3, "QCPending", 20, 20, null, "HOLD"),
                    Batch(po2, 4, "Failed", 40, 40, 0, "REJECT"), Batch(po2, 5, "Cancelled", 10, null, null, null));

                // Giao: A – đơn giao Đã giao (có thực hiện DELIVERED + POD 95), Kế hoạch (READY), Hủy; B – đang giao (DISPATCHED). Hôm trước: POD 9.
                DeliveryOrder Dlv(Guid id, Guid order, Guid cust, int i, string status, DateTime d) => new()
                {
                    Id = id, DeliveryNumber = $"PG-{key}-{i}", DeliveryNo = $"PG-{key}-{i}", OrderId = order, CustomerId = cust, DeliveryDate = d, PlannedDeliveryTime = d.AddHours(11), Status = status,
                };
                db.DeliveryOrder.AddRange(Dlv(do1, o1, custA, 1, "Delivered", day), Dlv(do2, o4, custA, 2, "Planned", day), Dlv(do3, o1, custA, 3, "Cancelled", day),
                    Dlv(do4, o5, custB, 4, "InTransit", day), Dlv(do6, o6, custA, 6, "Delivered", day.AddDays(-1)));
                db.DeliveryExecution.AddRange(
                    new DeliveryExecution { Id = Guid.NewGuid(), DeliveryOrderId = do1, Status = "DELIVERED", DeliveredAt = day.AddHours(11) },
                    new DeliveryExecution { Id = Guid.NewGuid(), DeliveryOrderId = do4, Status = "DISPATCHED", DispatchedAt = day.AddHours(10) });
                db.ProofOfDelivery.AddRange(
                    new ProofOfDelivery { Id = Guid.NewGuid(), DeliveryOrderId = do1, ConfirmedAt = day.AddHours(11), DeliveredQuantity = 100, AcceptedQuantity = 95, ReceiverName = "KH" },
                    new ProofOfDelivery { Id = Guid.NewGuid(), DeliveryOrderId = do6, ConfirmedAt = day.AddDays(-1).AddHours(11), DeliveredQuantity = 10, AcceptedQuantity = 9, ReceiverName = "KH" });

                // Phản ánh: 1 đang mở (khách A), 1 đã đóng.
                db.CustomerComplaint.AddRange(
                    new CustomerComplaint { Id = Guid.NewGuid(), ComplaintNo = "PA1-" + key, CustomerId = custA, OrderId = o1, ComplaintStatus = "Open", ReceivedAt = day, SlaDueAt = day.AddYears(5), Severity = "Medium", Description = "Thiếu suất" },
                    new CustomerComplaint { Id = Guid.NewGuid(), ComplaintNo = "PA2-" + key, CustomerId = custA, ComplaintStatus = "Closed", ReceivedAt = day, Severity = "Medium", Description = "Đã xử lý" });

                // Nhân sự: 2 nhân viên của bếp (đơn vị con), 1 của chi nhánh khác.
                Guid e1 = Guid.NewGuid(), e2 = Guid.NewGuid(), e3 = Guid.NewGuid();
                db.Employee.AddRange(new Employee { Id = e1, OrganizationUnitId = kitchen, FullName = "NV1 " + key }, new Employee { Id = e2, OrganizationUnitId = kitchen, FullName = "NV2 " + key },
                    new Employee { Id = e3, OrganizationUnitId = other, FullName = "NV3 " + key });
                db.ShiftAssignment.AddRange(
                    new ShiftAssignment { Id = Guid.NewGuid(), ShiftId = s1, EmployeeId = e1, WorkDate = day, AssignmentStatus = "CheckedIn" },
                    new ShiftAssignment { Id = Guid.NewGuid(), ShiftId = s2, EmployeeId = e2, WorkDate = day, AssignmentStatus = "Planned" },
                    new ShiftAssignment { Id = Guid.NewGuid(), ShiftId = s1, EmployeeId = e3, WorkDate = day, AssignmentStatus = "Completed" });

                // KPI snapshot của ngày: toàn công ty + 1 dòng của chi nhánh.
                KpiDailySnapshot Kpi(string code, int version, decimal? value, decimal? target, Guid? b = null) => new()
                {
                    Id = Guid.NewGuid(), SnapshotDate = day, BranchId = b, MetricCode = code, MetricVersion = version, MetricValue = value, TargetValue = target,
                    CalculatedAt = day.AddHours(23), DataQualityStatus = "OK", CalculationVersion = "1",
                };
                db.KpiDailySnapshot.AddRange(Kpi("PROD_ACHIEVEMENT", 1, 96, 98), Kpi("FIN_FOOD_COST", 1, 50, 55), Kpi("INV_ACCURACY", 1, 90, 98), Kpi("INV_ACCURACY", 2, 99, 98),
                    Kpi("WASTE_RATE", 1, 1, 2), Kpi("PROD_ACHIEVEMENT", 1, 80, 98, branch));
                return Task.CompletedTask;
            });
            return new Seeded(branch, kitchen, other, s1, s2, custA, custB, o1, o4, key);
        }

        [Fact]
        public async Task Executive_blocks_follow_spec_and_match_list_screens()
        {
            DateTime day = new(2031, 7, 15);
            var s = await SeedDayAsync(day);
            using var admin = _factory.CreateAuthorizedClient();

            // ---- Toàn công ty (không lọc): so với các màn danh sách / bảng.
            var all = (await admin.GetResultAsync($"/api/Dashboard/GetExecutive?date={Day(day)}")).Ok();
            var kpi = all.GetProperty("kpi");
            kpi.GetProperty("plannedMeals").GetDecimal().Should().Be(260m, "Σ dòng đơn ≠ Nháp / Hủy: 150 + 40 + 70");
            var board = (await admin.GetResultAsync($"/api/Order/GetBoard?MealDate={Day(day)}")).Ok();
            kpi.GetProperty("orders").GetProperty("total").GetInt32().Should().Be(board.GetProperty("totalRecords").GetInt32());
            var byStatus = kpi.GetProperty("orders").GetProperty("byStatus");
            foreach (var col in board.GetProperty("statusCounts").EnumerateObject().Where(c => c.Value.GetInt32() > 0))
            {
                byStatus.GetProperty(col.Name).GetInt32().Should().Be(col.Value.GetInt32(), $"trạng thái {col.Name} khớp Order/GetBoard");
            }
            var boardMeals = board.GetProperty("listColumn").EnumerateArray().Where(c => c.GetProperty("status").GetString() is not ("Draft" or "Cancelled"))
                .Sum(c => c.GetProperty("totalQuantity").GetDecimal());
            kpi.GetProperty("plannedMeals").GetDecimal().Should().Be(boardMeals, "khớp tổng suất đơn ≠ Nháp / Hủy của Order/GetBoard");

            var progress = kpi.GetProperty("productionProgress");
            progress.GetProperty("batchCount").GetInt32().Should().Be(4, "mẻ Hủy không tính");
            progress.GetProperty("plannedPortions").GetDecimal().Should().Be(210m);
            progress.GetProperty("donePortions").GetDecimal().Should().Be(148m, "Đã đóng gói 98 (số đạt) + Đạt 50 (thực nấu)");
            progress.GetProperty("percent").GetDecimal().Should().Be(70.5m);
            var prodBoard = (await admin.GetResultAsync($"/api/ProductionBatch/GetBoard?ProductionDate={Day(day)}")).Ok();
            prodBoard.GetProperty("passedPortions").GetDecimal().Should().Be(148m, "khớp ProductionBatch/GetBoard");
            prodBoard.GetProperty("plannedPortions").GetDecimal().Should().Be(220m, "bảng mẻ tính cả mẻ Hủy (10) – dashboard bỏ (PROPOSED)");

            var delivery = kpi.GetProperty("delivery");
            var execBoard = (await admin.GetResultAsync($"/api/DeliveryExecution/GetBoard?DeliveryDate={Day(day)}")).Ok();
            delivery.GetProperty("total").GetInt32().Should().Be(execBoard.GetProperty("listItem").GetArrayLength()).And.Be(3);
            delivery.GetProperty("delivered").GetInt32().Should().Be(execBoard.GetProperty("statusCounts").GetProperty("DELIVERED").GetInt32()).And.Be(1);
            delivery.GetProperty("byStatus").GetProperty("READY").GetInt32().Should().Be(1);
            delivery.GetProperty("byStatus").GetProperty("DISPATCHED").GetInt32().Should().Be(1);

            kpi.GetProperty("qcDetail").GetProperty("batchHold").GetInt32().Should().Be(1);
            kpi.GetProperty("qcDetail").GetProperty("batchReject").GetInt32().Should().Be(1);
            var alerts = await admin.GetResultAsync("/api/Common/GetAlerts");
            kpi.GetProperty("qcDetail").GetProperty("lotOnHold").GetInt32().Should().Be(AlertCount(alerts, "LOT_ON_HOLD"));
            kpi.GetProperty("qcHoldReject").GetInt32().Should().Be(2 + AlertCount(alerts, "LOT_ON_HOLD"));
            kpi.GetProperty("stockAlerts").GetInt32().Should().Be(AlertCount(alerts, "STOCK_BELOW_MIN"), "cùng quy tắc chuông");
            kpi.GetProperty("openComplaints").GetInt32().Should().Be(AlertCount(alerts, "COMPLAINT_OVERDUE", "COMPLAINT_OPEN"));
            kpi.GetProperty("staffing").GetProperty("assigned").GetInt32().Should().Be(3);
            kpi.GetProperty("staffing").GetProperty("checkedIn").GetInt32().Should().Be(2, "Đã vào ca + Hoàn thành");

            var meals = all.GetProperty("chart").GetProperty("meals7d").EnumerateArray().ToList();
            meals.Should().HaveCount(7);
            meals[6].GetProperty("date").GetDateTime().Should().Be(day);
            meals[6].GetProperty("planned").GetDecimal().Should().Be(260m);
            meals[6].GetProperty("accepted").GetDecimal().Should().Be(95m);
            meals[5].GetProperty("planned").GetDecimal().Should().Be(10m);
            meals[5].GetProperty("accepted").GetDecimal().Should().Be(9m);
            meals[0].GetProperty("planned").GetDecimal().Should().Be(0m);

            var shifts = all.GetProperty("shiftProgress").EnumerateArray().ToDictionary(x => x.GetProperty("shiftId").GetGuid());
            shifts[s.S1].GetProperty("batchCount").GetInt32().Should().Be(3);
            shifts[s.S1].GetProperty("percent").GetDecimal().Should().Be(87.1m, "148 / 170");
            shifts[s.S2].GetProperty("batchCount").GetInt32().Should().Be(1);
            shifts[s.S2].GetProperty("percent").GetDecimal().Should().Be(0m);

            var snapshot = all.GetProperty("kpiSnapshot").EnumerateArray().ToDictionary(x => x.GetProperty("metricCode").GetString()!);
            snapshot.Keys.Should().Equal("PROD_ACHIEVEMENT", "DEL_ONTIME", "INV_ACCURACY", "FIN_FOOD_COST");
            snapshot["PROD_ACHIEVEMENT"].GetProperty("value").GetDecimal().Should().Be(96m, "dòng toàn công ty");
            snapshot["PROD_ACHIEVEMENT"].GetProperty("met").GetBoolean().Should().BeFalse();
            snapshot["INV_ACCURACY"].GetProperty("value").GetDecimal().Should().Be(99m, "phiên bản chỉ tiêu cao nhất");
            snapshot["INV_ACCURACY"].GetProperty("met").GetBoolean().Should().BeTrue();
            snapshot["FIN_FOOD_COST"].GetProperty("lowerIsBetter").GetBoolean().Should().BeTrue();
            snapshot["FIN_FOOD_COST"].GetProperty("met").GetBoolean().Should().BeTrue("50 ≤ 55 – chi phí càng thấp càng tốt");
            snapshot["DEL_ONTIME"].GetProperty("value").ValueKind.Should().Be(JsonValueKind.Null, "chưa có snapshot");
            snapshot["DEL_ONTIME"].GetProperty("met").ValueKind.Should().Be(JsonValueKind.Null);

            all.GetProperty("ordersTotal").GetInt32().Should().Be(5);
            var orders = all.GetProperty("orders").EnumerateArray().ToList();
            orders.Select(o => o.GetProperty("orderNumber").GetString()).Should().Equal($"DH-{s.Key}-1", $"DH-{s.Key}-2", $"DH-{s.Key}-3", $"DH-{s.Key}-4", $"DH-{s.Key}-5");
            var o1 = orders[0].GetProperty("listDelivery").EnumerateArray().ToList();
            o1.Should().ContainSingle("đơn giao Hủy không hiện");
            o1[0].GetProperty("status").GetString().Should().Be("DELIVERED");
            orders[3].GetProperty("listDelivery")[0].GetProperty("status").GetString().Should().Be("READY");
            orders[0].GetProperty("route").GetString().Should().Be("#/crm-order");
            all.GetProperty("alerts").GetArrayLength().Should().Be(alerts.ListData.GetArrayLength(), "khối cảnh báo = Common/GetAlerts");

            // ---- Lọc chi nhánh (gồm đơn vị con).
            var br = (await admin.GetResultAsync($"/api/Dashboard/GetExecutive?date={Day(day)}&branchId={s.Branch}")).Ok();
            var bk = br.GetProperty("kpi");
            bk.GetProperty("plannedMeals").GetDecimal().Should().Be(190m);
            bk.GetProperty("orders").GetProperty("total").GetInt32().Should().Be(4);
            bk.GetProperty("delivery").GetProperty("total").GetInt32().Should().Be(2);
            bk.GetProperty("delivery").GetProperty("delivered").GetInt32().Should().Be(1);
            bk.GetProperty("staffing").GetProperty("assigned").GetInt32().Should().Be(2);
            bk.GetProperty("staffing").GetProperty("checkedIn").GetInt32().Should().Be(1);
            br.GetProperty("chart").GetProperty("meals7d")[6].GetProperty("planned").GetDecimal().Should().Be(190m);
            br.GetProperty("kpiSnapshot")[0].GetProperty("value").GetDecimal().Should().Be(80m, "dòng KPI của đúng chi nhánh");
            br.GetProperty("kpiSnapshot")[1].GetProperty("value").ValueKind.Should().Be(JsonValueKind.Null);
            bk.GetProperty("productionProgress").GetProperty("batchCount").GetInt32().Should().Be(4, "kế hoạch không gắn đơn vị tính cho mọi chi nhánh");
            var branchAlerts = await admin.GetResultAsync($"/api/Common/GetAlerts?branchId={s.Branch}");
            bk.GetProperty("openComplaints").GetInt32().Should().Be(AlertCount(branchAlerts, "COMPLAINT_OVERDUE", "COMPLAINT_OPEN"));
            bk.GetProperty("stockAlerts").GetInt32().Should().Be(AlertCount(branchAlerts, "STOCK_BELOW_MIN"));
            var other = (await admin.GetResultAsync($"/api/Dashboard/GetExecutive?date={Day(day)}&branchId={s.Other}")).Ok().GetProperty("kpi");
            other.GetProperty("plannedMeals").GetDecimal().Should().Be(70m);
            other.GetProperty("delivery").GetProperty("total").GetInt32().Should().Be(1);

            // ---- Lọc ca.
            var sh = (await admin.GetResultAsync($"/api/Dashboard/GetExecutive?date={Day(day)}&branchId={s.Branch}&shiftId={s.S1}")).Ok();
            sh.GetProperty("kpi").GetProperty("plannedMeals").GetDecimal().Should().Be(100m, "chỉ dòng đơn ca 1 của đơn hợp lệ");
            sh.GetProperty("kpi").GetProperty("orders").GetProperty("total").GetInt32().Should().Be(3, "đơn có dòng ca 1");
            sh.GetProperty("kpi").GetProperty("productionProgress").GetProperty("batchCount").GetInt32().Should().Be(3);
            sh.GetProperty("kpi").GetProperty("staffing").GetProperty("assigned").GetInt32().Should().Be(1);
            sh.GetProperty("shiftProgress").GetArrayLength().Should().Be(1);
            var prodBoardShift = (await admin.GetResultAsync($"/api/ProductionBatch/GetBoard?ProductionDate={Day(day)}&ShiftId={s.S1}")).Ok();
            sh.GetProperty("kpi").GetProperty("productionProgress").GetProperty("donePortions").GetDecimal().Should().Be(prodBoardShift.GetProperty("passedPortions").GetDecimal());

            // ---- Lỗi tham số → 400.
            (await admin.GetResultAsync($"/api/Dashboard/GetExecutive?date={Day(day)}&shiftId={Guid.NewGuid()}")).Fail("ca");
            (await admin.GetResultAsync($"/api/Dashboard/GetExecutive?date={Day(day)}&branchId={Guid.NewGuid()}")).Fail("chi nhánh");
            (await admin.GetResultAsync("/api/Dashboard/GetExecutive?date=1900-01-01")).Fail("Ngày");

            // ---- Không ngày = hôm nay (giờ máy chủ).
            (await admin.GetResultAsync("/api/Dashboard/GetExecutive")).Ok().GetProperty("date").GetDateTime().Should().Be(Now.Date);

            // ---- D-58: không trả tên bảng / cột.
            string raw = all.GetRawText();
            raw.Should().NotContain("ProductionBatche").And.NotContain("ProofOfDeliverie").And.NotContain("OrderLine").And.NotContain("KpiDailySnapshot");
        }

        [Fact]
        public async Task Executive_caches_shared_part_60s_per_key_but_alerts_per_user()
        {
            DateTime day = new(2031, 7, 16);
            var s = await SeedDayAsync(day);
            using var admin = _factory.CreateAuthorizedClient();
            string url = $"/api/Dashboard/GetExecutive?date={Day(day)}&branchId={s.Branch}";
            var first = (await admin.GetResultAsync(url)).Ok();
            first.GetProperty("kpi").GetProperty("plannedMeals").GetDecimal().Should().Be(190m);

            await _factory.SeedAsync(db =>
            {
                Guid id = Guid.NewGuid();
                db.Order.Add(new Order { Id = id, OrderNumber = $"DH-{s.Key}-9", OrderNo = $"DH-{s.Key}-9", CustomerId = s.CustA, OrderDate = day, MealDate = day, DeliveryDate = day, Status = "Confirmed", TotalQuantity = 7 });
                db.OrderLine.Add(new OrderLine { Id = Guid.NewGuid(), OrderId = id, MealType = "TRUA", ShiftId = s.S2, Quantity = 7 });
                return Task.CompletedTask;
            });
            (await admin.GetResultAsync(url)).Ok().GetProperty("kpi").GetProperty("plannedMeals").GetDecimal().Should().Be(190m, "trong 60 giây trả phần chung đã cache");
            (await admin.GetResultAsync(url + $"&shiftId={s.S2}")).Ok().GetProperty("kpi").GetProperty("plannedMeals").GetDecimal().Should().Be(97m, "khóa khác (ca) tính lại: 50 + 40 + 7");

            // Cùng khóa cache, người khác: phần chung giống, cảnh báo theo quyền của người đó (chỉ Xem dashboard → không có cảnh báo nào).
            first.GetProperty("alerts").GetArrayLength().Should().BeGreaterThan(0, "admin có cảnh báo (vd. mẻ / lô / phản ánh của các test khác hoặc tồn)");
            using var viewer = _factory.CreateAuthorizedClient(await CommonTestUsers.UserAsync(_factory, ("dash-exec", "V")));
            var other = (await viewer.GetResultAsync(url)).Ok();
            other.GetProperty("kpi").GetProperty("plannedMeals").GetDecimal().Should().Be(190m, "cùng khóa cache");
            other.GetProperty("alerts").GetArrayLength().Should().Be(0, "không có quyền Xem màn nào của cảnh báo – không thấy cảnh báo của admin");
        }

        [Fact]
        public async Task Executive_requires_view_on_dash_exec()
        {
            using var none = _factory.CreateAuthorizedClient(await CommonTestUsers.UserAsync(_factory, ("crm-order", "V")));
            (await none.GetAsync("/api/Dashboard/GetExecutive?date=2031-07-17")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            using var viewer = _factory.CreateAuthorizedClient(await CommonTestUsers.UserAsync(_factory, ("dash-exec", "V")));
            (await viewer.GetResultAsync("/api/Dashboard/GetExecutive?date=2031-07-17")).Ok();
        }

        [Fact]
        public async Task Order_journey_links_every_stage_with_screen_and_document_numbers()
        {
            DateTime day = new(2031, 8, 20);
            string key = Key();
            Guid cust = Guid.NewGuid(), order = Guid.NewGuid(), s1 = Guid.NewGuid(), s2 = Guid.NewGuid(), dish = Guid.NewGuid(), recipe = Guid.NewGuid();
            Guid plan = Guid.NewGuid(), po1 = Guid.NewGuid(), po2 = Guid.NewGuid(), b1 = Guid.NewGuid(), b2 = Guid.NewGuid();
            Guid doA = Guid.NewGuid(), doB = Guid.NewGuid(), doC = Guid.NewGuid(), route = Guid.NewGuid(), contract = Guid.NewGuid();
            Guid draft = Guid.NewGuid(), cancelled = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Shift.AddRange(new Shift { Id = s1, Code = "J1" + key, Name = "Ca J1" }, new Shift { Id = s2, Code = "J2" + key, Name = "Ca J2" });
                db.Customer.Add(new Customer { Id = cust, Code = "J" + key, Name = "Khách J " + key, CIF = "J" + key, DisplayName = "Khách J " + key, CustomerType = "FACTORY", Status = "Active" });
                db.Contract.Add(new Contract { Id = contract, CustomerId = cust, ContractNumber = "HD-" + key, ContractNo = "HD-" + key, Status = "Active" });
                db.Order.AddRange(
                    new Order { Id = order, OrderNumber = "DH-J" + key, OrderNo = "DH-J" + key, CustomerId = cust, ContractId = contract, OrderDate = day.AddDays(-3), MealDate = day, DeliveryDate = day, Status = "Reconciled", TotalQuantity = 50 },
                    new Order { Id = draft, OrderNumber = "DH-JD" + key, OrderNo = "DH-JD" + key, CustomerId = cust, OrderDate = day, MealDate = day.AddDays(30), DeliveryDate = day.AddDays(30), Status = "Draft" },
                    new Order { Id = cancelled, OrderNumber = "DH-JC" + key, OrderNo = "DH-JC" + key, CustomerId = cust, OrderDate = day, MealDate = day.AddDays(31), DeliveryDate = day.AddDays(31), Status = "Cancelled" });
                db.OrderLine.Add(new OrderLine { Id = Guid.NewGuid(), OrderId = order, MealType = "TRUA", ShiftId = s1, Quantity = 50 });
                db.ProductionPlan.Add(new ProductionPlan { Id = plan, PlanNumber = "KH-J" + key, PlanNo = "KH-J" + key, ProductionDate = day, PlanDate = day.AddDays(-1), Status = "Completed" });
                db.ProductionOrder.AddRange(
                    new ProductionOrder { Id = po1, ProductionPlanId = plan, DishId = dish, RecipeId = recipe, ShiftId = s1, PlannedQty = 50, Status = "Completed", ProductionOrderNumber = "SX-J1" + key, PlannedStartDate = day.AddHours(6) },
                    new ProductionOrder { Id = po2, ProductionPlanId = plan, DishId = dish, RecipeId = recipe, ShiftId = s2, PlannedQty = 50, Status = "Completed", ProductionOrderNumber = "SX-J2" + key, PlannedStartDate = day.AddHours(14) });
                db.ProductionBatch.AddRange(
                    new ProductionBatch { Id = b1, BatchNumber = "ME-J1" + key, ProductionOrderId = po1, DishId = dish, RecipeId = recipe, StartDate = day, Status = "Packed", EndTime = day.AddHours(9) },
                    new ProductionBatch { Id = b2, BatchNumber = "ME-J2" + key, ProductionOrderId = po1, DishId = dish, RecipeId = recipe, StartDate = day, Status = "Failed", EndTime = day.AddHours(8) });
                db.PackingOrder.AddRange(
                    new PackingOrder { Id = Guid.NewGuid(), PackingOrderNumber = "DG-J1" + key, ProductionBatchId = b1, OrderId = order, PlannedQty = 50, PackingDate = day.AddHours(9), Status = "Packed" },
                    new PackingOrder { Id = Guid.NewGuid(), PackingOrderNumber = "DG-J2" + key, ProductionBatchId = b1, OrderId = order, PlannedQty = 5, PackingDate = day.AddHours(9), Status = "Cancelled" });
                db.DeliveryRoute.Add(new DeliveryRoute { Id = route, RouteNo = "CX-J" + key, Code = "CX-J" + key, DeliveryDate = day, Status = "Completed" });
                DeliveryOrder Dlv(Guid id, string no, string status) => new()
                {
                    Id = id, DeliveryNumber = no + key, DeliveryNo = no + key, OrderId = order, CustomerId = cust, DeliveryDate = day, PlannedDeliveryTime = day.AddHours(11), Status = status,
                };
                db.DeliveryOrder.AddRange(Dlv(doA, "PG-JA", "Delivered"), Dlv(doB, "PG-JB", "Failed"), Dlv(doC, "PG-JC", "Cancelled"));
                db.DeliveryStop.Add(new DeliveryStop { Id = Guid.NewGuid(), DeliveryRouteId = route, DeliveryOrderId = doA, CustomerId = cust, SequenceNo = 1, StopStatus = "Delivered" });
                db.DeliveryExecution.Add(new DeliveryExecution { Id = Guid.NewGuid(), DeliveryOrderId = doA, DeliveryRouteId = route, Status = "DELIVERED", DeliveredAt = day.AddHours(11) });
                db.ProofOfDelivery.Add(new ProofOfDelivery { Id = Guid.NewGuid(), DeliveryOrderId = doA, ConfirmedAt = day.AddHours(11).AddMinutes(5), DeliveredQuantity = 50, AcceptedQuantity = 48, ReceiverName = "KH" });
                db.OrderReconciliation.Add(new OrderReconciliation { Id = Guid.NewGuid(), OrderId = order, Status = "Matched", ReconciliationDate = day.AddDays(1), OrderedQuantity = 50, DeliveredQuantity = 50, AcceptedQuantity = 48 });
                db.CustomerComplaint.Add(new CustomerComplaint { Id = Guid.NewGuid(), ComplaintNo = "PA-J" + key, CustomerId = cust, DeliveryOrderId = doA, ComplaintStatus = "Open", ReceivedAt = day.AddHours(12), Severity = "Medium", Description = "Thiếu suất" });
                return Task.CompletedTask;
            });
            using var admin = _factory.CreateAuthorizedClient();
            var j = (await admin.GetResultAsync($"/api/Dashboard/GetOrderJourney?orderId={order}")).Ok();
            j.GetProperty("orderNumber").GetString().Should().Be("DH-J" + key);
            j.GetProperty("contractNumber").GetString().Should().Be("HD-" + key);
            var stages = j.GetProperty("stages").EnumerateArray().ToDictionary(x => x.GetProperty("stage").GetString()!);
            stages.Keys.Should().Equal("ORDER", "PRODUCTION_PLAN", "PRODUCTION", "PACKING", "DELIVERY", "POD", "RECONCILIATION", "COMPLAINT");
            static List<string?> Numbers(JsonElement st) => [.. st.GetProperty("documents").EnumerateArray().Select(d => d.GetProperty("number").GetString())];

            stages["ORDER"].GetProperty("status").GetString().Should().Be("Done");
            stages["ORDER"].GetProperty("route").GetString().Should().Be("#/crm-order");
            stages["PRODUCTION_PLAN"].GetProperty("done").GetBoolean().Should().BeTrue();
            Numbers(stages["PRODUCTION_PLAN"]).Should().Equal("KH-J" + key);
            Numbers(stages["PRODUCTION"]).Should().Equal("SX-J1" + key, "ME-J1" + key, "ME-J2" + key);
            stages["PRODUCTION"].GetProperty("status").GetString().Should().Be("Done");
            stages["PRODUCTION"].GetProperty("note").GetString().Should().Contain("1 mẻ không đạt");
            stages["PRODUCTION"].GetProperty("documents")[1].GetProperty("route").GetString().Should().Be("#/prod-batch");
            Numbers(stages["PACKING"]).Should().Equal("DG-J1" + key);
            stages["PACKING"].GetProperty("status").GetString().Should().Be("Done");
            Numbers(stages["DELIVERY"]).Should().Equal("PG-JA" + key, "PG-JB" + key, "CX-J" + key);
            stages["DELIVERY"].GetProperty("status").GetString().Should().Be("Done", "đơn giao còn hiệu lực đã giao hết; đơn giao thất bại ghi chú");
            stages["DELIVERY"].GetProperty("note").GetString().Should().Contain("1 đơn giao");
            stages["DELIVERY"].GetProperty("documents")[2].GetProperty("route").GetString().Should().Be("#/log-route");
            stages["DELIVERY"].GetProperty("at").GetDateTime().Should().Be(day.AddHours(11));
            Numbers(stages["POD"]).Should().Equal("PG-JA" + key);
            stages["POD"].GetProperty("status").GetString().Should().Be("Done");
            stages["RECONCILIATION"].GetProperty("status").GetString().Should().Be("Done");
            stages["RECONCILIATION"].GetProperty("documents")[0].GetProperty("statusName").GetString().Should().Be("Khớp");
            stages["COMPLAINT"].GetProperty("status").GetString().Should().Be("Issue");
            Numbers(stages["COMPLAINT"]).Should().Equal("PA-J" + key);
            j.GetProperty("currentStage").GetString().Should().Be("COMPLAINT");
            j.GetRawText().Should().NotContain("ProductionBatche").And.NotContain("ProofOfDeliverie").And.NotContain("DeliveryOrderId");

            // Đơn Nháp: chỉ chặng đơn đang làm, phản ánh "Không có".
            var d = (await admin.GetResultAsync($"/api/Dashboard/GetOrderJourney?orderId={draft}")).Ok();
            var ds = d.GetProperty("stages").EnumerateArray().ToList();
            ds[0].GetProperty("status").GetString().Should().Be("InProgress");
            ds.Skip(1).Take(6).Should().OnlyContain(x => x.GetProperty("status").GetString() == "NotStarted");
            ds[7].GetProperty("status").GetString().Should().Be("None");
            ds[7].GetProperty("done").GetBoolean().Should().BeTrue();
            d.GetProperty("currentStage").GetString().Should().Be("ORDER");
            // Đơn Hủy.
            var c = (await admin.GetResultAsync($"/api/Dashboard/GetOrderJourney?orderId={cancelled}")).Ok();
            c.GetProperty("stages").EnumerateArray().Take(7).Should().OnlyContain(x => x.GetProperty("status").GetString() == "Cancelled");
            c.GetProperty("currentStage").ValueKind.Should().Be(JsonValueKind.Null);

            (await admin.GetResultAsync($"/api/Dashboard/GetOrderJourney?orderId={Guid.Empty}")).Fail("Chưa chọn");
            (await admin.GetResultAsync($"/api/Dashboard/GetOrderJourney?orderId={Guid.NewGuid()}")).Fail("Không tìm thấy");

            // Quyền: không có flow-e2e / dash-exec / crm-order → 403; chỉ Xem đơn hàng → thấy trạng thái mọi chặng nhưng chỉ chứng từ đơn hàng.
            using var stranger = _factory.CreateAuthorizedClient(await CommonTestUsers.UserAsync(_factory, ("prod-batch", "V")));
            var forbidden = await stranger.GetAsync($"/api/Dashboard/GetOrderJourney?orderId={order}");
            forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            using var sales = _factory.CreateAuthorizedClient(await CommonTestUsers.UserAsync(_factory, ("crm-order", "V"), ("log-execution", "V") /* UI-6.35: Xác nhận đã giao gộp vào Theo dõi */));
            var limited = (await sales.GetResultAsync($"/api/Dashboard/GetOrderJourney?orderId={order}")).Ok().GetProperty("stages").EnumerateArray()
                .ToDictionary(x => x.GetProperty("stage").GetString()!);
            Numbers(limited["ORDER"]).Should().Equal("DH-J" + key);
            limited["PRODUCTION"].GetProperty("documents").GetArrayLength().Should().Be(0);
            limited["PRODUCTION"].GetProperty("documentCount").GetInt32().Should().Be(3);
            limited["PRODUCTION"].GetProperty("canView").GetBoolean().Should().BeFalse();
            limited["PRODUCTION"].GetProperty("status").GetString().Should().Be("Done");
            Numbers(limited["POD"]).Should().Equal("PG-JA" + key);
            using var exec = _factory.CreateAuthorizedClient(await CommonTestUsers.UserAsync(_factory, ("dash-exec", "V")));
            (await exec.GetResultAsync($"/api/Dashboard/GetOrderJourney?orderId={order}")).Ok();
        }

        [Fact]
        public void Kpi_direction_reads_target_tag_then_code()
        {
            DashboardService.LowerIsBetter("PROD_ACHIEVEMENT", "Hoàn thành [TARGET >= 98]").Should().BeFalse();
            DashboardService.LowerIsBetter("PROD_ACHIEVEMENT", "[TARGET <= 5]").Should().BeTrue();
            DashboardService.LowerIsBetter("FIN_FOOD_COST", null).Should().BeTrue();
            DashboardService.LowerIsBetter("DEL_ONTIME", "Giao đúng giờ").Should().BeFalse();
        }
    }
}
