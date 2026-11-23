using System.Net.Http.Json;
using System.Text.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Delivery
{
    /// <summary>
    /// T19 (Pilot P8) – giao + đối soát + phản ánh. Đồng hồ test = 21/12/2026 10:00 (ngày giao).
    /// <para>Bộ số tính tay: đơn A 60 suất × 30.000 = 1.800.000 (điểm giao 11:30), đơn B 40 suất × 30.000 = 1.200.000 (điểm giao 11:00) → chuyến 2 điểm: #1 = B, #2 = A, tải 100 / xe 120.
    /// Mẻ của ngày / ca: M1 đã đóng gói + lưu mẫu, M2 đã đóng gói CHƯA lưu mẫu → chặn xuất phát (anh chốt 2026-11-16).
    /// Bàn giao A: giao 58, nhận 56 (từ chối 2 → phản ánh PA- tự động), thiếu 2. Đối soát A: phục vụ 50, trả về 4, hủy 2 → chênh 50 − 60 = −10 (−16,67%),
    /// tính tiền (BIZ-1.4, cơ sở RECEIVED) = khách nhận 56 − 2 suất lỗi Delicare (không đạt nhiệt độ) = 54 → 1.800.000 × 54 ÷ 60 = 1.620.000; phục vụ 50 chỉ tham khảo. Đối soát B: phục vụ 40 = đặt → Khớp, nhận 40 → 1.200.000.</para>
    /// </summary>
    public class DeliveryTests : IClassFixture<InMemoryApiFactory>
    {
        private static readonly DateTime Day = new(2026, 12, 21);
        private readonly InMemoryApiFactory _factory;

        public DeliveryTests(InMemoryApiFactory factory)
        {
            _factory = factory;
            _factory.Clock.FixedNow = Day.AddHours(10);
        }

        private sealed record Scenario(string Code, Guid Shift, Guid Vehicle, Guid Driver, Guid CustomerA, Guid CustomerB, Guid OrderA, Guid OrderB, Guid BatchSampled, Guid BatchNoSample,
            string BatchNoSampleNumber);

        private async Task<Scenario> SeedAsync(string code, string secondBatchStatus = "Packed")
        {
            Guid shift = Guid.NewGuid(), vehicle = Guid.NewGuid(), driver = Guid.NewGuid(), customerA = Guid.NewGuid(), customerB = Guid.NewGuid();
            Guid locationA = Guid.NewGuid(), locationB = Guid.NewGuid(), orderA = Guid.NewGuid(), orderB = Guid.NewGuid();
            Guid plan = Guid.NewGuid(), productionOrder = Guid.NewGuid(), dish = Guid.NewGuid(), b1 = Guid.NewGuid(), b2 = Guid.NewGuid();
            string b2Number = $"ME-261221-{code}-02";
            await _factory.SeedAsync(async db =>
            {
                db.Shift.Add(new Shift { Id = shift, Code = "CA" + code, Name = "Trưa " + code, StartTime = new TimeSpan(10, 30, 0), Status = "Active" });
                db.Vehicle.Add(new Vehicle { Id = vehicle, Code = "XE" + code, LicensePlate = "51C-" + code, Capacity = 120, Status = "Active" });
                db.Employee.Add(new Employee { Id = driver, Code = "TX" + code, FullName = "Tài xế " + code, Status = "Active" });
                db.Customer.AddRange(
                    new Customer { Id = customerA, Code = "A" + code, Name = "[DEMO] Khách A " + code, CIF = "A" + code, CustomerType = "FACTORY", DisplayName = "Khách A " + code, Status = "Active" },
                    new Customer { Id = customerB, Code = "B" + code, Name = "[DEMO] Khách B " + code, CIF = "B" + code, CustomerType = "FACTORY", DisplayName = "Khách B " + code, Status = "Active" });
                db.CustomerDeliveryLocation.AddRange(
                    new CustomerDeliveryLocation { Id = locationA, CustomerId = customerA, Code = "DA" + code, Name = "Căng tin A", Address = "KCN A", DeliveryWindowFrom = new TimeSpan(11, 30, 0) },
                    new CustomerDeliveryLocation { Id = locationB, CustomerId = customerB, Code = "DB" + code, Name = "Căng tin B", Address = "KCN B", DeliveryWindowFrom = new TimeSpan(11, 0, 0) });
                db.Order.AddRange(
                    new Order { Id = orderA, OrderNumber = "PH-A" + code, OrderNo = "PH-A" + code, CustomerId = customerA, DeliveryLocationId = locationA, OrderDate = Day.AddDays(-2), MealDate = Day, DeliveryDate = Day, Status = "InProduction", TotalQuantity = 60, TotalAmount = 1_800_000 },
                    new Order { Id = orderB, OrderNumber = "PH-B" + code, OrderNo = "PH-B" + code, CustomerId = customerB, DeliveryLocationId = locationB, OrderDate = Day.AddDays(-2), MealDate = Day, DeliveryDate = Day, Status = "InProduction", TotalQuantity = 40, TotalAmount = 1_200_000 });
                db.OrderLine.AddRange(
                    new OrderLine { Id = Guid.NewGuid(), OrderId = orderA, MealType = "TRUA", ShiftId = shift, Quantity = 60, UnitPrice = 30_000, LineAmount = 1_800_000 },
                    new OrderLine { Id = Guid.NewGuid(), OrderId = orderB, MealType = "TRUA", ShiftId = shift, Quantity = 40, UnitPrice = 30_000, LineAmount = 1_200_000 });
                db.Dish.Add(new Dish { Id = dish, Code = "MON" + code, Name = "Cơm gà " + code, Status = "Active" });
                db.ProductionPlan.Add(new ProductionPlan { Id = plan, PlanNumber = "KH-" + code, PlanNo = "KH-" + code, ProductionDate = Day, PlanDate = Day.AddDays(-1), Status = "Released" });
                db.ProductionOrder.Add(new ProductionOrder { Id = productionOrder, ProductionPlanId = plan, DishId = dish, RecipeId = Guid.NewGuid(), ShiftId = shift, PlannedQty = 100, Status = "InProgress", ProductionOrderNumber = "PH-SX" + code, PlannedStartDate = Day.AddHours(7) });
                db.ProductionBatch.AddRange(
                    new ProductionBatch { Id = b1, ProductionOrderId = productionOrder, DishId = dish, BatchNumber = $"ME-261221-{code}-01", BatchNo = $"ME-261221-{code}-01", StartDate = Day.AddHours(7), PlannedQuantity = 50, Status = "Packed", QcStatus = "PASS" },
                    new ProductionBatch { Id = b2, ProductionOrderId = productionOrder, DishId = dish, BatchNumber = b2Number, BatchNo = b2Number, StartDate = Day.AddHours(8), PlannedQuantity = 50, Status = secondBatchStatus, QcStatus = "PASS" });
                Guid item;
                var existing = await db.FoodSafetyChecklistItem.FirstOrDefaultAsync(i => i.ItemCode == "SAMPLE_RETAINED");
                if (existing == null)
                {
                    Guid checklist = Guid.NewGuid();
                    item = Guid.NewGuid();
                    db.FoodSafetyChecklist.Add(new FoodSafetyChecklist { Id = checklist, Code = "PRD_BATCH", Name = "Kiểm soát mẻ nấu", VersionNo = 1, Status = "Active" });
                    db.FoodSafetyChecklistItem.Add(new FoodSafetyChecklistItem { Id = item, ChecklistId = checklist, ItemCode = "SAMPLE_RETAINED", ItemName = "Lưu mẫu", SortOrder = 1, IsCritical = true, IsRequired = true });
                }
                else
                {
                    item = existing.Id;
                }
                db.FoodSafetyChecklistResult.Add(new FoodSafetyChecklistResult { Id = Guid.NewGuid(), ChecklistId = Guid.NewGuid(), ChecklistItemId = item, CheckDate = Day, ProductionBatchId = b1, ResultCode = "PASS", CheckedAt = Day.AddHours(8) });
            });
            return new Scenario(code, shift, vehicle, driver, customerA, customerB, orderA, orderB, b1, b2, b2Number);
        }

        private static async Task<LegacyResult> PostJson(HttpClient c, string url, object body) => await LegacyResult.ReadAsync(await c.PostAsJsonAsync(url, body));

        private static Task<LegacyResult> CreateRoute(HttpClient c, Scenario s, params Guid[] orders)
            => PostJson(c, "/api/DeliveryRoute/Create", new { deliveryDate = Day, shiftId = s.Shift, vehicleId = s.Vehicle, driverId = s.Driver, listOrderId = orders });

        private static List<JsonElement> Stops(JsonElement detail) => [.. detail.GetProperty("listStop").EnumerateArray()];

        private static IEnumerable<string?> Warnings(JsonElement detail) => detail.GetProperty("listWarning").EnumerateArray().Select(w => w.GetString());

        [Fact]
        public async Task Route_dispatch_handover_pod_and_reconciliation_follow_temperature_sample_and_hand_checked_numbers()
        {
            var s = await SeedAsync("D1");
            using var c = _factory.CreateAuthorizedClient();

            var ready = await c.GetResultAsync($"/api/DeliveryRoute/GetReadyOrders?DeliveryDate=2026-12-21&ShiftId={s.Shift}");
            ready.Code.Should().Be(200);
            ready.ListData.EnumerateArray().Select(o => o.GetProperty("orderId").GetGuid()).Should().Contain([s.OrderA, s.OrderB]);

            // Lập chuyến: CX-261221-XED1-01, điểm #1 = B (11:00), #2 = A (11:30), đơn giao PH- liên tiếp; cảnh báo mẻ chưa lưu mẫu.
            var route = (await CreateRoute(c, s, s.OrderA, s.OrderB)).Ok();
            Guid routeId = route.GetProperty("route").GetProperty("id").GetGuid();
            route.GetProperty("route").GetProperty("routeNo").GetString().Should().Be("CX-261221-XED1-01");
            route.GetProperty("route").GetProperty("status").GetString().Should().Be("Planned");
            route.GetProperty("route").GetProperty("plannedStart").GetDateTime().Should().Be(Day.AddHours(10), "11:00 − 60′ vận chuyển");
            route.GetProperty("loadMeals").GetDecimal().Should().Be(100);
            route.GetProperty("loadPercent").GetDecimal().Should().Be(83.33m);
            route.GetProperty("driverName").GetString().Should().Be("Tài xế D1");
            var stops = Stops(route);
            stops.Select(x => (x.GetProperty("sequenceNo").GetInt32(), x.GetProperty("orderId").GetGuid(), x.GetProperty("plannedMealQty").GetDecimal()))
                .Should().Equal((1, s.OrderB, 40m), (2, s.OrderA, 60m));
            string do1 = stops[0].GetProperty("deliveryNumber").GetString()!, do2 = stops[1].GetProperty("deliveryNumber").GetString()!;
            do1.Should().MatchRegex(@"^GH-261221-\d{4}$");
            int.Parse(do2[^4..]).Should().Be(int.Parse(do1[^4..]) + 1);
            Warnings(route).Should().Contain(w => w!.Contains("chưa lưu mẫu") && w.Contains(s.BatchNoSampleNumber));
            // Đơn đã có chuyến không xếp lại được.
            (await CreateRoute(c, s, s.OrderA)).Fail("đã có chuyến");

            // Xuất phát: chưa đo nhiệt độ + mẻ chưa lưu mẫu → chặn, liệt kê số mẻ.
            var blocked = await c.PostFormAsync("/api/DeliveryRoute/Dispatch", ("Id", routeId));
            blocked.Fail("Chưa đo nhiệt độ xuất phát");
            blocked.Message.Should().Contain("Mẻ chưa lưu mẫu thức ăn (bắt buộc trước khi giao): " + s.BatchNoSampleNumber);
            // 55 °C không ghi khắc phục → 400, không ghi sổ.
            (await c.PostFormAsync("/api/DeliveryRoute/Dispatch", ("Id", routeId), ("TemperatureC", 55m))).Fail("hành động khắc phục");
            (await _factory.QueryAsync(db => db.DeliveryEvent.CountAsync(e => e.DeliveryRouteId == routeId))).Should().Be(0);
            // 55 °C có khắc phục → lưu lần đo nhưng vẫn chặn (dưới 60 °C).
            var low = await c.PostFormAsync("/api/DeliveryRoute/Dispatch", ("Id", routeId), ("TemperatureC", 55m), ("CorrectiveAction", "Hâm lại thùng giữ nhiệt"));
            low.Fail("Đã lưu lần đo nhiệt độ");
            low.Message.Should().Contain("55 °C dưới ngưỡng 60 °C");
            var log = await _factory.QueryAsync(db => db.TemperatureLog.AsNoTracking().Where(t => t.MeasurementType == "DELIVERY" && t.TemperatureC == 55m && t.CorrectiveAction == "Hâm lại thùng giữ nhiệt").SingleAsync());
            log.IsWithinRange.Should().BeFalse();
            log.MinAllowedC.Should().Be(60);
            log.RecordedBy.Should().Be(InMemoryApiFactory.UserId);
            (await _factory.QueryAsync(db => db.DeliveryEvent.SingleAsync(e => e.DeliveryRouteId == routeId))).ExternalEventId.Should().Be("TemperatureLog:" + log.Id);

            // Lưu mẫu mẻ đã đóng gói (ghi được khi Packed) + đo lại 65 °C → xuất phát.
            (await c.PostFormAsync("/api/ProductionBatch/RecordSampleRetained", ("Id", s.BatchNoSample), ("ActualValue", "Tủ mẫu 3"))).Ok();
            (await c.PostFormAsync("/api/DeliveryRoute/RecordTemperature", ("Id", routeId), ("TemperatureC", 65m), ("DeviceCode", "NK-02"))).Ok();
            // BE2.1: chưa xếp hàng lên xe → vẫn chặn; xếp hàng (DeliveryLoading + DeliveryExecution LOADED) rồi mới xuất phát.
            (await c.PostFormAsync("/api/DeliveryRoute/Dispatch", ("Id", routeId))).Fail("Chưa xếp hàng lên xe (DeliveryRoute/Load): " + do1 + ", " + do2);
            var loaded = (await PostJson(c, "/api/DeliveryRoute/Load", new { id = routeId, employeeId = s.Driver })).Ok();
            loaded.GetProperty("route").GetProperty("route").GetProperty("status").GetString().Should().Be("Loading");
            loaded.GetProperty("listLoading").EnumerateArray().Select(l => l.GetProperty("loadedQty").GetDecimal()).Should().BeEquivalentTo([40m, 60m]);
            loaded.GetProperty("listExecution").EnumerateArray().Select(l => l.GetProperty("status").GetString()).Should().OnlyContain(x => x == "LOADED");
            var dispatched = (await c.PostFormAsync("/api/DeliveryRoute/Dispatch", ("Id", routeId), ("Latitude", 10.8m), ("Longitude", 106.7m))).Ok();
            (await _factory.QueryAsync(db => db.DeliveryExecution.Where(e => e.DeliveryRouteId == routeId).Select(e => e.Status).ToListAsync())).Should().OnlyContain(x => x == "DISPATCHED");
            dispatched.GetProperty("route").GetProperty("status").GetString().Should().Be("InTransit");
            dispatched.GetProperty("route").GetProperty("actualStart").GetDateTime().Should().Be(Day.AddHours(10));
            dispatched.GetProperty("departureTemperatureC").GetDecimal().Should().Be(65);
            (await _factory.QueryAsync(db => db.DeliveryOrder.Where(d => d.DeliveryRouteId == routeId).Select(d => d.Status).ToListAsync())).Should().OnlyContain(x => x == "InTransit");
            (await c.PostFormAsync("/api/DeliveryRoute/Dispatch", ("Id", routeId))).Fail("Đang giao");

            // Điểm #1 (B): đến; bàn giao 58 °C (có khắc phục) → lưu lần đo, chặn; 62 °C → giao 40 / nhận 40.
            Guid stopB = stops[0].GetProperty("id").GetGuid(), stopA = stops[1].GetProperty("id").GetGuid();
            (await c.PostFormAsync("/api/DeliveryStop/Arrive", ("Id", stopB), ("Latitude", 10.81m), ("Longitude", 106.71m))).Ok();
            var cold = await c.PostFormAsync("/api/DeliveryStop/Deliver", ("Id", stopB), ("TemperatureC", 58m), ("CorrectiveAction", "Báo bếp"), ("DeliveredQuantity", 40m), ("ReceiverName", "Chị Lan"));
            cold.Fail("58 °C dưới ngưỡng 60 °C");
            (await _factory.QueryAsync(db => db.DeliveryStop.AsNoTracking().SingleAsync(x => x.Id == stopB))).StopStatus.Should().Be("Arrived");
            var deliveredB = (await c.PostFormAsync("/api/DeliveryStop/Deliver", ("Id", stopB), ("TemperatureC", 62m), ("DeliveredQuantity", 40m), ("ReceiverName", "Chị Lan"),
                ("SignatureUrl", "/files/pod/sign-b.png"))).Ok();
            Stops(deliveredB)[0].GetProperty("stopStatus").GetString().Should().Be("Delivered");
            Stops(deliveredB)[0].GetProperty("pod").GetProperty("signatureUrl").GetString().Should().Be("/files/pod/sign-b.png");
            (await _factory.QueryAsync(db => db.Order.AsNoTracking().SingleAsync(o => o.Id == s.OrderB))).Status.Should().Be("Delivered");
            (await _factory.QueryAsync(db => db.Order.AsNoTracking().SingleAsync(o => o.Id == s.OrderA))).Status.Should().Be("InProduction");

            // Điểm #2 (A): chưa đo nhiệt độ tại điểm → chặn; 63 °C, giao 58, nhận 56 → thiếu 2, phản ánh PA- tự động, chuyến hoàn thành.
            (await c.PostFormAsync("/api/DeliveryStop/Deliver", ("Id", stopA), ("DeliveredQuantity", 58m), ("ReceiverName", "Anh Minh"))).Fail("Chưa đo nhiệt độ bàn giao");
            (await c.PostFormAsync("/api/DeliveryStop/Deliver", ("Id", stopA), ("TemperatureC", 63m), ("DeliveredQuantity", 58m), ("AcceptedQuantity", 59m), ("ReceiverName", "Anh Minh"))).Fail("số nhận");
            var deliveredA = (await c.PostFormAsync("/api/DeliveryStop/Deliver", ("Id", stopA), ("TemperatureC", 63m), ("DeliveredQuantity", 58m), ("AcceptedQuantity", 56m),
                ("ReceiverName", "Anh Minh"), ("Remark", "2 suất móp hộp"))).Ok();
            deliveredA.GetProperty("route").GetProperty("status").GetString().Should().Be("Completed");
            var stopAResult = Stops(deliveredA)[1];
            stopAResult.GetProperty("deliveredMealQty").GetDecimal().Should().Be(58);
            stopAResult.GetProperty("shortageQty").GetDecimal().Should().Be(2);
            stopAResult.GetProperty("pod").GetProperty("rejectedQuantity").GetDecimal().Should().Be(2);
            Warnings(deliveredA).Should().Contain(w => w!.StartsWith("Đã tạo phản ánh PA-261221-"));
            var autoComplaint = await _factory.QueryAsync(db => db.CustomerComplaint.AsNoTracking().SingleAsync(x => x.OrderId == s.OrderA));
            autoComplaint.SourceChannel.Should().Be("POD");
            autoComplaint.CategoryCode.Should().Be("SHORTAGE");
            autoComplaint.DeliveryRouteId.Should().Be(routeId);
            (await _factory.QueryAsync(db => db.Order.AsNoTracking().SingleAsync(o => o.Id == s.OrderA))).Status.Should().Be("Delivered");
            (await c.PostFormAsync("/api/DeliveryStop/Fail", ("Id", stopA), ("Reason", "x"))).Fail("Đang giao");

            // In biên bản giao hàng mở được.
            var file = await c.GetAsync($"/api/DeliveryRoute/ExportDeliveryNote?id={routeId}");
            file.Content.Headers.ContentType!.MediaType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            using (var wb = new XLWorkbook(await file.Content.ReadAsStreamAsync()))
            {
                var ws = wb.Worksheet(1);
                ws.Cell(4, 1).GetString().Should().Contain("CX-261221-XED1-01");
                ws.Cell(8, 3).GetString().Should().Be("Căng tin B");
                ws.Cell(9, 10).GetValue<decimal>().Should().Be(56);
            }

            // Đối soát A: kiểm số; phục vụ 50, trả về 4, hủy 2.
            (await c.PostFormAsync("/api/OrderReconciliation/Reconcile", ("OrderId", s.OrderA), ("ServedQuantity", 57m))).Fail("Suất phục vụ từ 0 tới số suất khách nhận khi bàn giao (56)");
            (await c.PostFormAsync("/api/OrderReconciliation/Reconcile", ("OrderId", s.OrderA), ("ServedQuantity", 50m), ("ReturnedQuantity", 4m), ("WastedQuantity", 3m))).Fail("không vượt số nhận");
            // BIZ-1.4: suất lỗi Delicare phải có lý do, ≤ suất nhận.
            (await c.PostFormAsync("/api/OrderReconciliation/Reconcile", ("OrderId", s.OrderA), ("ServedQuantity", 50m), ("DelicareFaultQuantity", 2m))).Fail("chọn lý do");
            (await c.PostFormAsync("/api/OrderReconciliation/Reconcile", ("OrderId", s.OrderA), ("ServedQuantity", 50m), ("DelicareFaultQuantity", 57m), ("FaultReason", "TEMPERATURE"))).Fail("lỗi Delicare");
            var recA = (await c.PostFormAsync("/api/OrderReconciliation/Reconcile", ("OrderId", s.OrderA), ("ServedQuantity", 50m), ("ReturnedQuantity", 4m), ("WastedQuantity", 2m),
                ("DelicareFaultQuantity", 2m), ("FaultReason", "TEMPERATURE"))).Ok();
            var r = recA.GetProperty("reconciliation");
            r.GetProperty("orderedQuantity").GetDecimal().Should().Be(60);
            r.GetProperty("deliveredQuantity").GetDecimal().Should().Be(58);
            r.GetProperty("acceptedQuantity").GetDecimal().Should().Be(50);
            r.GetProperty("varianceQty").GetDecimal().Should().Be(-10);
            r.GetProperty("variancePercent").GetDecimal().Should().Be(-16.67m);
            r.GetProperty("finalAmount").GetDecimal().Should().Be(1_620_000, "1.800.000 × (56 nhận − 2 lỗi) ÷ 60");
            r.GetProperty("status").GetString().Should().Be("Variance");
            recA.GetProperty("podAcceptedQuantity").GetDecimal().Should().Be(56);
            recA.GetProperty("leftoverQuantity").GetDecimal().Should().Be(6);
            recA.GetProperty("returnedQuantity").GetDecimal().Should().Be(4);
            recA.GetProperty("billingBasis").GetString().Should().Be("RECEIVED");
            recA.GetProperty("billingQuantity").GetDecimal().Should().Be(54);
            recA.GetProperty("delicareFaultQuantity").GetDecimal().Should().Be(2);
            recA.GetProperty("faultReasonName").GetString().Should().Be("Không đạt nhiệt độ");
            Guid recAId = r.GetProperty("id").GetGuid();
            (await c.PostFormAsync("/api/OrderReconciliation/Close", ("Id", recAId))).Fail("phải ghi chú");
            var closedA = (await c.PostFormAsync("/api/OrderReconciliation/Close", ("Id", recAId), ("Remark", "Khách nghỉ ca 10 người"))).Ok();
            closedA.GetProperty("reconciliation").GetProperty("status").GetString().Should().Be("Closed");
            (await _factory.QueryAsync(db => db.Order.AsNoTracking().SingleAsync(o => o.Id == s.OrderA))).Status.Should().Be("Reconciled");
            (await c.PostFormAsync("/api/OrderReconciliation/Reconcile", ("OrderId", s.OrderA), ("ServedQuantity", 50m))).Fail("Đã giao");

            // Đối soát B: phục vụ = đặt → Khớp, đóng không cần ghi chú.
            var recB = (await c.PostFormAsync("/api/OrderReconciliation/Reconcile", ("OrderId", s.OrderB), ("ServedQuantity", 40m))).Ok();
            recB.GetProperty("reconciliation").GetProperty("status").GetString().Should().Be("Matched");
            recB.GetProperty("reconciliation").GetProperty("finalAmount").GetDecimal().Should().Be(1_200_000);
            (await c.PostFormAsync("/api/OrderReconciliation/Close", ("Id", recB.GetProperty("reconciliation").GetProperty("id").GetGuid()))).Ok();
        }

        [Fact]
        public async Task Dispatch_is_blocked_while_batches_are_not_packed_and_legacy_crud_cannot_break_the_flow()
        {
            var s = await SeedAsync("D2", secondBatchStatus: "Passed");
            using var c = _factory.CreateAuthorizedClient();
            var route = (await CreateRoute(c, s)).Ok();
            Guid routeId = route.GetProperty("route").GetProperty("id").GetGuid();
            Stops(route).Should().HaveCount(2, "trống danh sách = mọi đơn Đang sản xuất của ngày / ca chưa có chuyến");
            (await c.PostFormAsync("/api/DeliveryRoute/RecordTemperature", ("Id", routeId), ("TemperatureC", 70m))).Ok();
            var blocked = await c.PostFormAsync("/api/DeliveryRoute/Dispatch", ("Id", routeId));
            blocked.Fail("Mẻ chưa đóng gói xong: ME-261221-D2-02 (Đạt)");
            blocked.Message.Should().NotContain("nhiệt độ xuất phát");

            // CRUD cũ không phá máy trạng thái.
            (await c.PostFormAsync("/api/DeliveryRoute/Insert", ("Code", "X"), ("DeliveryDate", Day))).Fail("DeliveryRoute/Create");
            (await c.PostFormAsync("/api/DeliveryEvent/Insert", ("DeliveryRouteId", routeId), ("EventType", "TEMP_CHECK"), ("TemperatureC", 80m))).Fail("Sự kiện giao hàng");
            (await c.PostFormAsync("/api/ProofOfDelivery/Insert", ("DeliveryOrderId", Guid.NewGuid()))).Fail("DeliveryStop/Deliver");
            (await c.PostFormAsync("/api/OrderReconciliation/Insert", ("OrderId", s.OrderA))).Fail("OrderReconciliation/Reconcile");
            (await c.PostFormAsync("/api/CustomerComplaint/Insert", ("CustomerId", s.CustomerA), ("ComplaintNo", "PA-X"), ("Description", "x"))).Fail("CustomerComplaint/Create");
            (await c.PostFormAsync("/api/ComplaintHistory/Insert", ("ComplaintId", Guid.NewGuid()), ("EventType", "X"))).Fail("Lịch sử phản ánh");
            (await c.PostFormAsync("/api/DeliveryStop/Arrive", ("Id", Stops(route)[0].GetProperty("id").GetGuid()))).Fail("Đang giao");

            // Xóa chuyến Kế hoạch đã có lần đo nhiệt độ → chặn; chuyến mới chưa đo → xóa được, đơn trở lại chờ xếp chuyến.
            (await c.DeleteResultAsync($"/api/DeliveryRoute/Delete?id={routeId}")).Fail("không xóa được");
            await _factory.SeedAsync(async db =>
            {
                foreach (var d in await db.DeliveryOrder.Where(x => x.DeliveryRouteId == routeId).ToListAsync())
                {
                    d.Status = "Failed";
                }
            });
            var second = (await CreateRoute(c, s, s.OrderA)).Ok();
            second.GetProperty("route").GetProperty("routeNo").GetString().Should().Be("CX-261221-XED2-02");
            Guid secondId = second.GetProperty("route").GetProperty("id").GetGuid();
            (await c.DeleteResultAsync($"/api/DeliveryRoute/Delete?id={secondId}")).Ok();
            var ready = await c.GetResultAsync($"/api/DeliveryRoute/GetReadyOrders?DeliveryDate=2026-12-21&ShiftId={s.Shift}");
            ready.ListData.EnumerateArray().Select(o => o.GetProperty("orderId").GetGuid()).Should().Contain(s.OrderA);
        }

        [Fact]
        public async Task Complaint_lifecycle_numbers_sla_and_history()
        {
            var s = await SeedAsync("D3");
            using var c = _factory.CreateAuthorizedClient();
            (await c.PostFormAsync("/api/CustomerComplaint/Create", ("CustomerId", s.CustomerA), ("Severity", "urgent"), ("Description", "x"))).Fail("Mức độ phản ánh không hợp lệ");
            (await c.PostFormAsync("/api/CustomerComplaint/Create", ("CustomerId", s.CustomerA), ("OrderId", s.OrderB), ("Description", "x"))).Fail("Đơn hàng");

            var created = (await c.PostFormAsync("/api/CustomerComplaint/Create", ("CustomerId", s.CustomerA), ("OrderId", s.OrderA), ("ProductionBatchId", s.BatchSampled),
                ("Severity", "high"), ("SourceChannel", "zalo"), ("CategoryCode", "quality"), ("Description", "Cơm bị khô"))).Ok();
            var complaint = created.GetProperty("complaint");
            Guid id = complaint.GetProperty("id").GetGuid();
            complaint.GetProperty("complaintNo").GetString().Should().MatchRegex(@"^PA-261221-\d{4}$");
            complaint.GetProperty("severity").GetString().Should().Be("High");
            complaint.GetProperty("sourceChannel").GetString().Should().Be("ZALO");
            complaint.GetProperty("complaintStatus").GetString().Should().Be("Open");
            complaint.GetProperty("receivedAt").GetDateTime().Should().Be(Day.AddHours(10));
            complaint.GetProperty("slaDueAt").GetDateTime().Should().Be(Day.AddHours(14), "HIGH = 4 giờ");
            complaint.GetProperty("description").GetString().Should().Be("Cơm bị khô [Mẻ ME-261221-D3-01]");
            created.GetProperty("isOverdue").GetBoolean().Should().BeFalse();

            (await c.PostFormAsync("/api/CustomerComplaint/Resolve", ("Id", id), ("Comment", "x"))).Fail("Đang xử lý");
            (await c.PostFormAsync("/api/CustomerComplaint/Assign", ("Id", id), ("Comment", "Bếp trưởng kiểm tra"))).Ok()
                .GetProperty("complaint").GetProperty("assignedTo").GetGuid().Should().Be(InMemoryApiFactory.UserId);
            (await c.PostFormAsync("/api/CustomerComplaint/Resolve", ("Id", id))).Fail("Phải ghi nội dung");
            (await c.PostFormAsync("/api/CustomerComplaint/AddComment", ("Id", id), ("Comment", "Đã gọi khách"))).Ok();
            var resolved = (await c.PostFormAsync("/api/CustomerComplaint/Resolve", ("Id", id), ("Comment", "Giảm 5% hóa đơn ngày"))).Ok();
            resolved.GetProperty("complaint").GetProperty("resolution").GetString().Should().Be("Giảm 5% hóa đơn ngày");
            resolved.GetProperty("complaint").GetProperty("resolvedAt").GetDateTime().Should().Be(Day.AddHours(10));
            (await c.PostFormAsync("/api/CustomerComplaint/Reject", ("Id", id), ("Comment", "x"))).Fail("Mới / Đang xử lý");
            var closed = (await c.PostFormAsync("/api/CustomerComplaint/Close", ("Id", id))).Ok();
            closed.GetProperty("complaint").GetProperty("complaintStatus").GetString().Should().Be("Closed");
            closed.GetProperty("listHistory").EnumerateArray().Select(h => (h.GetProperty("eventType").GetString(), h.GetProperty("newStatus").GetString()))
                .Should().Equal(("CREATED", "Open"), ("ASSIGNED", "InProgress"), ("COMMENT", "InProgress"), ("RESOLVED", "Resolved"), ("CLOSED", "Closed"));
            closed.GetProperty("allowedActions").GetArrayLength().Should().Be(0);
            (await c.PutFormAsync("/api/CustomerComplaint/Update", ("Id", id), ("CustomerId", s.CustomerA), ("ComplaintNo", "PA-X"), ("Description", "y"))).Fail("Mới / Đang xử lý");

            // LOW nhận cách đây 80 giờ (hạn 72 giờ) → quá hạn; từ chối được khi Mới.
            var old = (await c.PostFormAsync("/api/CustomerComplaint/Create", ("CustomerId", s.CustomerB), ("Severity", "Low"), ("Description", "Giao trễ"),
                ("ReceivedAt", Day.AddHours(10).AddHours(-80).ToString("yyyy-MM-ddTHH:mm:ss")))).Ok();
            old.GetProperty("isOverdue").GetBoolean().Should().BeTrue();
            int number = int.Parse(old.GetProperty("complaint").GetProperty("complaintNo").GetString()![^4..]);
            number.Should().BeGreaterThan(0);
            var rejected = (await c.PostFormAsync("/api/CustomerComplaint/Reject", ("Id", old.GetProperty("complaint").GetProperty("id").GetGuid()), ("Comment", "Không đúng xe của DELICARE"))).Ok();
            rejected.GetProperty("isOverdue").GetBoolean().Should().BeFalse("đã Từ chối");
        }
    }
}
