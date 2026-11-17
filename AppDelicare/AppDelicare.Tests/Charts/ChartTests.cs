using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Tests.Integration;
using FluentAssertions;

namespace AppDelicare.Tests.Charts
{
    /// <summary>
    /// BE3.1 – biểu đồ + sự kiện giao: đối soát + biểu đồ Đặt – Giao – Nhận (Catalog 100), tổng hợp hao hụt (157), chuỗi nhiệt độ (163),
    /// timeline sự kiện đơn giao (192), vị trí xe theo sự kiện + tài xế ghi vị trí (193). Mỗi test dùng mã / ngày riêng (factory dùng chung).
    /// </summary>
    public class ChartTests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public ChartTests(InMemoryApiFactory factory)
        {
            _factory = factory;
            _factory.Clock.FixedNow = new DateTime(2031, 3, 20, 10, 0, 0);
        }

        private static string Key() => Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        [Fact]
        public async Task Reconciliation_list_returns_daily_chart_with_zero_days_status_counts_and_filters()
        {
            string key = Key();
            Guid customer = Guid.NewGuid(), other = Guid.NewGuid();
            DateTime d1 = new(2031, 3, 1), d3 = new(2031, 3, 3);
            await _factory.SeedAsync(db =>
            {
                db.Customer.AddRange(
                    new Customer { Id = customer, Code = "C" + key, Name = "Khách " + key, CIF = "C" + key, DisplayName = "Khách " + key, CustomerType = "FACTORY", Status = "Active" },
                    new Customer { Id = other, Code = "O" + key, Name = "Khác " + key, CIF = "O" + key, DisplayName = "Khác " + key, CustomerType = "FACTORY", Status = "Active" });
                void Add(Guid cust, DateTime day, decimal ordered, decimal delivered, decimal accepted, string status, int i)
                {
                    Guid order = Guid.NewGuid();
                    db.Order.Add(new Order { Id = order, OrderNumber = $"DH-{key}-{i}", OrderNo = $"DH-{key}-{i}", CustomerId = cust, OrderDate = day.AddDays(-2), MealDate = day, DeliveryDate = day, Status = "Reconciled" });
                    db.OrderReconciliation.Add(new OrderReconciliation
                    {
                        Id = Guid.NewGuid(), OrderId = order, Status = status, ReconciliationDate = day,
                        OrderedQuantity = ordered, DeliveredQuantity = delivered, AcceptedQuantity = accepted, OrderedQty = ordered, DeliveredQty = delivered, AcceptedQty = accepted,
                    });
                }
                Add(customer, d1, 100, 98, 95, ReconciliationStatus.Closed, 1);
                Add(customer, d1, 50, 50, 50, ReconciliationStatus.Matched, 2);
                Add(customer, d3, 80, 80, 79, ReconciliationStatus.Variance, 3);
                Add(other, d3, 999, 999, 999, ReconciliationStatus.Closed, 4);
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();

            var data = (await c.GetResultAsync($"/api/OrderReconciliation/GetList?From=2031-03-01&To=2031-03-03&CustomerId={customer}")).Ok();
            data.GetProperty("totalRecords").GetInt64().Should().Be(3);
            var chart = data.GetProperty("chart").EnumerateArray().ToList();
            chart.Should().HaveCount(3, "đủ mọi ngày trong khoảng, kể cả ngày 02 không có đối soát");
            chart[0].GetProperty("ordered").GetDecimal().Should().Be(150m);
            chart[0].GetProperty("delivered").GetDecimal().Should().Be(148m);
            chart[0].GetProperty("accepted").GetDecimal().Should().Be(145m);
            chart[1].GetProperty("ordered").GetDecimal().Should().Be(0m);
            chart[2].GetProperty("accepted").GetDecimal().Should().Be(79m);
            data.GetProperty("statusCounts").GetProperty("Closed").GetInt32().Should().Be(1);
            var first = data.GetProperty("listItem")[0];
            first.GetProperty("mealDate").GetDateTime().Should().Be(d3, "ngày ăn mới nhất trước");
            first.GetProperty("customerName").GetString().Should().Be("Khách " + key);
            first.GetProperty("statusName").GetString().Should().NotBeNullOrEmpty();

            var closed = (await c.GetResultAsync($"/api/OrderReconciliation/GetList?From=2031-03-01&To=2031-03-03&CustomerId={customer}&Status=Closed")).Ok();
            closed.GetProperty("totalRecords").GetInt64().Should().Be(1);
            closed.GetProperty("chart")[0].GetProperty("ordered").GetDecimal().Should().Be(100m, "biểu đồ theo đúng bộ lọc trạng thái");
            closed.GetProperty("statusCounts").GetProperty("Matched").GetInt32().Should().Be(1, "số theo trạng thái tính trước khi lọc trạng thái");

            (await c.GetResultAsync($"/api/OrderReconciliation/GetList?SearchText=DH-{key}-3")).Ok().GetProperty("totalRecords").GetInt64().Should().Be(1, "không gửi from / to = 30 ngày gần nhất (19/02–20/03/2031)");
        }

        [Fact]
        public async Task Reconciliation_list_rejects_bad_range()
        {
            using var c = _factory.CreateAuthorizedClient();
            (await c.GetResultAsync("/api/OrderReconciliation/GetList?From=2031-03-10&To=2031-03-01")).Fail("Khoảng thời gian");
            (await c.GetResultAsync("/api/OrderReconciliation/GetList?From=2029-01-01&To=2031-03-01")).Fail("366");
        }

        [Fact]
        public async Task Waste_summary_groups_by_type_dish_shift_date_with_standard_cost()
        {
            string key = Key();
            Guid dish = Guid.NewGuid(), shift = Guid.NewGuid();
            DateTime day = new(2030, 6, 10, 12, 0, 0);
            await _factory.SeedAsync(db =>
            {
                db.Dish.Add(new Dish { Id = dish, Code = "M" + key, Name = "Món " + key });
                db.Shift.Add(new Shift { Id = shift, Code = "S" + key, Name = "Ca " + key });
                db.StandardCost.AddRange(
                    new StandardCost { Id = Guid.NewGuid(), DishId = dish, RecipeId = Guid.NewGuid(), CostPerServing = 20000m, Status = "Active", EffectiveFrom = new DateTime(2030, 1, 1) },
                    new StandardCost { Id = Guid.NewGuid(), DishId = dish, RecipeId = Guid.NewGuid(), CostPerServing = 99000m, Status = "Active", EffectiveFrom = new DateTime(2030, 7, 1) });
                db.MealWasteRecord.AddRange(
                    new MealWasteRecord { Id = Guid.NewGuid(), DishId = dish, ShiftId = shift, WasteType = "QC_REJECT", QuantityMeals = 5, QuantityKg = 1.5m, OccurredAt = day },
                    new MealWasteRecord { Id = Guid.NewGuid(), DishId = dish, WasteType = "QC_REJECT", QuantityMeals = 3, OccurredAt = day.AddDays(1) },
                    new MealWasteRecord { Id = Guid.NewGuid(), WasteType = "PACKING_DAMAGE", QuantityMeals = 2, QuantityKg = 0.8m, OccurredAt = day.AddDays(1) },
                    new MealWasteRecord { Id = Guid.NewGuid(), DishId = dish, WasteType = "QC_REJECT", QuantityMeals = 100, OccurredAt = day.AddDays(40) });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();
            const string range = "From=2030-06-10&To=2030-06-11";

            var byType = (await c.GetResultAsync($"/api/MealWasteRecord/GetSummary?{range}")).Ok();
            byType.GetProperty("groupBy").GetString().Should().Be("wasteType");
            byType.GetProperty("totalMeals").GetDecimal().Should().Be(10m);
            byType.GetProperty("totalKg").GetDecimal().Should().Be(2.3m);
            byType.GetProperty("totalCost").GetDecimal().Should().Be(160000m, "8 suất × 20 000 (bản giá hiệu lực tháng 6)");
            byType.GetProperty("recordsWithoutCost").GetInt32().Should().Be(1);
            var items = byType.GetProperty("listItem").EnumerateArray().ToList();
            items.Should().HaveCount(2);
            items[0].GetProperty("key").GetString().Should().Be("QC_REJECT", "nhiều suất trước");
            items[0].GetProperty("quantityMeals").GetDecimal().Should().Be(8m);
            items[1].GetProperty("cost").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null, "không gắn món → không tính giá");

            var byDish = (await c.GetResultAsync($"/api/MealWasteRecord/GetSummary?{range}&GroupBy=dish")).Ok().GetProperty("listItem").EnumerateArray().ToList();
            byDish.Select(x => x.GetProperty("label").GetString()).Should().Contain(["Món " + key, "(Không gắn món)"]);
            var byShift = (await c.GetResultAsync($"/api/MealWasteRecord/GetSummary?{range}&GroupBy=Shift")).Ok().GetProperty("listItem").EnumerateArray().ToList();
            byShift.Select(x => x.GetProperty("label").GetString()).Should().Contain("Ca " + key);
            var byDate = (await c.GetResultAsync($"/api/MealWasteRecord/GetSummary?{range}&GroupBy=date")).Ok().GetProperty("listItem").EnumerateArray().ToList();
            byDate.Select(x => x.GetProperty("label").GetString()).Should().Equal("10/06/2030", "11/06/2030");

            (await c.GetResultAsync($"/api/MealWasteRecord/GetSummary?{range}&GroupBy=customer")).Fail("Nhóm theo");
        }

        [Fact]
        public async Task Temperature_series_returns_zone_limits_points_and_buckets_large_series()
        {
            string key = Key();
            Guid warehouse = Guid.NewGuid(), zone = Guid.NewGuid(), big = Guid.NewGuid();
            DateTime now = _factory.Clock.Now;
            await _factory.SeedAsync(db =>
            {
                db.Warehouse.Add(new Warehouse { Id = warehouse, Code = "K" + key, Name = "Kho " + key, Status = "Active" });
                db.StorageZone.AddRange(
                    new StorageZone { Id = zone, WarehouseId = warehouse, Code = "Z" + key, Name = "Mát " + key, TemperatureMin = 0m, TemperatureMax = 5m, Status = "Active" },
                    new StorageZone { Id = big, WarehouseId = warehouse, Code = "B" + key, Name = "Đông " + key, TemperatureMin = -25m, TemperatureMax = -18m, Status = "Active" });
                db.TemperatureLog.AddRange(
                    new TemperatureLog { Id = Guid.NewGuid(), StorageZoneId = zone, TemperatureC = 3m, IsWithinRange = true, MeasuredAt = now.AddHours(-2), MeasurementType = "STORAGE" },
                    new TemperatureLog { Id = Guid.NewGuid(), StorageZoneId = zone, TemperatureC = 7.5m, IsWithinRange = false, MeasuredAt = now.AddHours(-1), MeasurementType = "STORAGE" },
                    new TemperatureLog { Id = Guid.NewGuid(), StorageZoneId = zone, TemperatureC = 4m, IsWithinRange = true, MeasuredAt = now.AddHours(-60), MeasurementType = "STORAGE" });
                // 2 500 lần đo trong 2 500 phút (> 2 000 điểm) → gộp; 1 lần vượt ngưỡng phải còn thấy sau khi gộp.
                for (int i = 0; i < 2500; i++)
                {
                    db.TemperatureLog.Add(new TemperatureLog { Id = Guid.NewGuid(), StorageZoneId = big, TemperatureC = i == 1234 ? -10m : -20m, IsWithinRange = i != 1234, MeasuredAt = now.AddMinutes(-2500 + i), MeasurementType = "STORAGE" });
                }
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();

            var series = (await c.GetResultAsync($"/api/TemperatureLog/GetSeries?StorageZoneId={zone}")).Ok();
            series.GetProperty("temperatureMin").GetDecimal().Should().Be(0m);
            series.GetProperty("temperatureMax").GetDecimal().Should().Be(5m);
            series.GetProperty("measurementCount").GetInt32().Should().Be(2, "mặc định 48 giờ gần nhất");
            series.GetProperty("outOfRangeCount").GetInt32().Should().Be(1);
            series.GetProperty("bucketMinutes").GetInt32().Should().Be(0);
            var points = series.GetProperty("points").EnumerateArray().ToList();
            points.Select(p => p.GetProperty("temperatureC").GetDecimal()).Should().Equal(3m, 7.5m);
            points[1].GetProperty("isWithinRange").GetBoolean().Should().BeFalse();

            var wide = (await c.GetResultAsync($"/api/TemperatureLog/GetSeries?StorageZoneId={big}&From={now.AddDays(-3):yyyy-MM-ddTHH:mm}&To={now:yyyy-MM-ddTHH:mm}")).Ok();
            wide.GetProperty("measurementCount").GetInt32().Should().Be(2500);
            wide.GetProperty("bucketMinutes").GetInt32().Should().BeGreaterThan(1);
            var buckets = wide.GetProperty("points").EnumerateArray().ToList();
            buckets.Count.Should().BeLessThanOrEqualTo(2000);
            buckets.Count(p => p.GetProperty("isWithinRange").ValueKind == System.Text.Json.JsonValueKind.False).Should().Be(1, "lần đo vượt ngưỡng không bị làm mờ khi gộp");
            buckets.Max(p => p.GetProperty("maxC").GetDecimal()).Should().Be(-10m);

            (await c.GetResultAsync($"/api/TemperatureLog/GetSeries?StorageZoneId={Guid.NewGuid()}")).Fail("khu vực");
            (await c.GetResultAsync($"/api/TemperatureLog/GetSeries?StorageZoneId={zone}&From=2031-01-01&To=2031-03-01")).Fail("31");
        }

        private async Task<(Guid Route, Guid Stop, Guid DeliveryOrder, Guid OtherStop)> SeedRouteAsync(string key, DateTime day, string status)
        {
            Guid customer = Guid.NewGuid(), order = Guid.NewGuid(), route = Guid.NewGuid(), stop = Guid.NewGuid(), stop2 = Guid.NewGuid(), dorder = Guid.NewGuid(), dorder2 = Guid.NewGuid();
            Guid vehicle = Guid.NewGuid(), driver = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Customer.Add(new Customer { Id = customer, Code = "C" + key, Name = "Khách " + key, CIF = "C" + key, DisplayName = "Khách " + key, CustomerType = "FACTORY", Status = "Active" });
                db.Order.Add(new Order { Id = order, OrderNumber = "DH-" + key, OrderNo = "DH-" + key, CustomerId = customer, OrderDate = day.AddDays(-2), MealDate = day, DeliveryDate = day, Status = "InProduction" });
                db.Vehicle.Add(new Vehicle { Id = vehicle, Code = "X" + key, LicensePlate = "51C-" + key });
                db.Employee.Add(new Employee { Id = driver, Code = "TX" + key, EmployeeCode = "TX" + key, FullName = "Tài xế " + key, Status = "Active" });
                db.DeliveryRoute.Add(new DeliveryRoute { Id = route, RouteNo = "CX-" + key, Code = "CX-" + key, DeliveryDate = day, VehicleId = vehicle, DriverId = driver, Status = status });
                db.DeliveryOrder.AddRange(
                    new DeliveryOrder { Id = dorder, DeliveryNumber = "PG-" + key, DeliveryNo = "PG-" + key, OrderId = order, CustomerId = customer, DeliveryDate = day, PlannedDeliveryTime = day.AddHours(11), DeliveryRouteId = route, Status = "Dispatched" },
                    new DeliveryOrder { Id = dorder2, DeliveryNumber = "PG2-" + key, DeliveryNo = "PG2-" + key, OrderId = order, CustomerId = customer, DeliveryDate = day, PlannedDeliveryTime = day.AddHours(11), DeliveryRouteId = route, Status = "Dispatched" });
                db.DeliveryStop.AddRange(
                    new DeliveryStop { Id = stop, DeliveryRouteId = route, DeliveryOrderId = dorder, CustomerId = customer, SequenceNo = 1, StopStatus = "Arrived" },
                    new DeliveryStop { Id = stop2, DeliveryRouteId = route, DeliveryOrderId = dorder2, CustomerId = customer, SequenceNo = 2, StopStatus = "Planned" });
                db.DeliveryEvent.AddRange(
                    new DeliveryEvent { Id = Guid.NewGuid(), DeliveryRouteId = route, EventType = DeliveryCode.EventDispatched, EventAt = day.AddHours(9), Description = "Xuất phát" },
                    new DeliveryEvent { Id = Guid.NewGuid(), DeliveryRouteId = route, DeliveryStopId = stop, DeliveryOrderId = dorder, EventType = DeliveryCode.EventArrived, EventAt = day.AddHours(10), Latitude = 10.80m, Longitude = 106.70m, Description = "Đến điểm #1" },
                    new DeliveryEvent { Id = Guid.NewGuid(), DeliveryRouteId = route, DeliveryStopId = stop, EventType = DeliveryCode.EventTemperature, EventAt = day.AddHours(10).AddMinutes(5), TemperatureC = 65m },
                    new DeliveryEvent { Id = Guid.NewGuid(), DeliveryRouteId = route, DeliveryStopId = stop2, DeliveryOrderId = dorder2, EventType = DeliveryCode.EventArrived, EventAt = day.AddHours(10).AddMinutes(30), Latitude = 10.85m, Longitude = 106.75m });
                return Task.CompletedTask;
            });
            return (route, stop, dorder, stop2);
        }

        [Fact]
        public async Task Delivery_order_events_return_order_stop_and_route_level_timeline()
        {
            string key = Key();
            var s = await SeedRouteAsync(key, new DateTime(2030, 8, 5), "InTransit");
            using var c = _factory.CreateAuthorizedClient();

            var result = await c.GetResultAsync($"/api/DeliveryOrder/GetEvents?id={s.DeliveryOrder}");
            result.Ok();
            var events = result.ListData.EnumerateArray().ToList();
            events.Select(e => e.GetProperty("eventType").GetString()).Should().Equal(DeliveryCode.EventDispatched, DeliveryCode.EventArrived, DeliveryCode.EventTemperature);
            events[0].GetProperty("eventTypeName").GetString().Should().Be("Xuất phát");
            events[1].GetProperty("stopSequenceNo").GetInt32().Should().Be(1);
            events[1].GetProperty("vehiclePlate").GetString().Should().Be("51C-" + key);
            events[1].GetProperty("driverName").GetString().Should().Be("Tài xế " + key);
            events.Should().NotContain(e => e.GetProperty("deliveryStopId").ValueKind != System.Text.Json.JsonValueKind.Null && e.GetProperty("deliveryStopId").GetGuid() == s.OtherStop, "không lẫn sự kiện của đơn giao khác");

            (await c.GetResultAsync($"/api/DeliveryOrder/GetEvents?id={Guid.NewGuid()}")).Fail("đơn giao");
        }

        [Fact]
        public async Task Vehicle_positions_come_from_events_and_driver_can_record_position_on_running_route()
        {
            string key = Key();
            DateTime day = new(2030, 8, 6);
            var s = await SeedRouteAsync(key, day, "InTransit");
            string planKey = Key();
            var planned = await SeedRouteAsync(planKey, day, "Planned");
            using var c = _factory.CreateAuthorizedClient();

            var recorded = (await c.PostFormAsync("/api/DeliveryEvent/RecordPosition", ("RouteId", s.Route), ("Latitude", 10.9m), ("Longitude", 106.8m), ("Note", "Kẹt xe cầu Sài Gòn"))).Ok();
            recorded.GetProperty("eventType").GetString().Should().Be(DeliveryCode.EventPosition);
            recorded.GetProperty("description").GetString().Should().Be("Kẹt xe cầu Sài Gòn");
            recorded.GetProperty("eventAt").GetDateTime().Should().Be(_factory.Clock.Now);
            (await c.PostFormAsync("/api/DeliveryEvent/RecordPosition", ("RouteId", planned.Route), ("Latitude", 10.9m), ("Longitude", 106.8m))).Fail("đang giao");
            (await c.PostFormAsync("/api/DeliveryEvent/RecordPosition", ("RouteId", s.Route), ("Latitude", 91m), ("Longitude", 106.8m))).Fail("Tọa độ");
            (await c.PostFormAsync("/api/DeliveryEvent/RecordPosition", ("RouteId", Guid.NewGuid()), ("Latitude", 10m), ("Longitude", 106m))).Fail("chuyến");

            var positions = (await c.GetResultAsync($"/api/DeliveryEvent/GetVehiclePositions?DeliveryDate=2030-08-06&RouteId={s.Route}")).Ok();
            var list = positions.GetProperty("listPosition").EnumerateArray().ToList();
            list.Select(e => e.GetProperty("eventType").GetString()).Should().Equal(DeliveryCode.EventArrived, DeliveryCode.EventArrived, DeliveryCode.EventPosition);
            list.Should().OnlyContain(e => e.GetProperty("latitude").ValueKind == System.Text.Json.JsonValueKind.Number, "chỉ sự kiện có tọa độ");
            var latest = positions.GetProperty("listLatest").EnumerateArray().Single();
            latest.GetProperty("eventType").GetString().Should().Be(DeliveryCode.EventPosition);
            latest.GetProperty("latitude").GetDecimal().Should().Be(10.9m);

            var allRoutes = (await c.GetResultAsync("/api/DeliveryEvent/GetVehiclePositions?DeliveryDate=2030-08-06")).Ok();
            allRoutes.GetProperty("listLatest").EnumerateArray().Select(e => e.GetProperty("routeNumber").GetString()).Should().Contain(["CX-" + key, "CX-" + planKey]);
            (await c.GetResultAsync("/api/DeliveryEvent/GetVehiclePositions")).Fail("ngày giao");
        }
    }
}
