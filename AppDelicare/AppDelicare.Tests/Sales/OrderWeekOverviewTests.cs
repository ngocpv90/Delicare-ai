using System.Net.Http.Json;
using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Services.Implement;
using AppDelicare.Tests.Integration;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Sales
{
    /// <summary>
    /// UI-6.6 (D-85): tổng quan tuần (khách hợp đồng × điểm giao × ngày, trạng thái, dự báo), xác nhận nhiều điểm một lần,
    /// lấy số dự báo chia lựa chọn theo tỉ lệ 4 tuần, tải nhiều phiếu một lần (tự nhận điểm, phiếu sai báo riêng), điểm chưa có số cho job 18:00 / 20:05.
    /// Đồng hồ test 08/12/2026 10:00 – tuần thử 28/12 chưa tới giờ chốt.
    /// </summary>
    public class OrderWeekOverviewTests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;
        private static readonly DateTime Monday = new(2026, 12, 28);

        public OrderWeekOverviewTests(InMemoryApiFactory factory)
        {
            _factory = factory;
            _factory.Clock.FixedNow = new DateTime(2026, 12, 8, 10, 0, 0);
        }

        private sealed record Scenario(Guid Customer, Guid LocA, Guid LocB, Guid MealSang, Guid MealTrua);

        /// <summary>Khách + 2 điểm giao + hợp đồng (SANG, TRUA) + thực đơn chung tuần 28/12: SANG 3 lựa chọn, TRUA 1 lựa chọn.</summary>
        private async Task<Scenario> SeedAsync(string code)
        {
            Guid customer = Guid.NewGuid(), locA = Guid.NewGuid(), locB = Guid.NewGuid(), contract = Guid.NewGuid(), sang = Guid.NewGuid(), trua = Guid.NewGuid(), menu = Guid.NewGuid();
            Guid d1 = Guid.NewGuid(), d2 = Guid.NewGuid(), d3 = Guid.NewGuid(), com = Guid.NewGuid();
            await _factory.SeedAsync(async db =>
            {
                foreach (var shift in new[] { "TRUA", "SANG_6H" })
                {
                    if (!await db.Shift.AnyAsync(x => x.Code == shift))
                    {
                        db.Shift.Add(new Shift { Id = Guid.NewGuid(), Code = shift, Name = "Ca " + shift });
                    }
                }
                db.Customer.Add(new Customer { Id = customer, Code = code, Name = "Khách " + code, CIF = code, CustomerType = "BUSINESS", DisplayName = code, Status = "Active" });
                db.CustomerDeliveryLocation.AddRange(
                    new CustomerDeliveryLocation { Id = locA, CustomerId = customer, Code = code + "-A", Name = "Nhà máy A" },
                    new CustomerDeliveryLocation { Id = locB, CustomerId = customer, Code = code + "-B", Name = "Nhà máy B" });
                db.Contract.Add(new Contract { Id = contract, CustomerId = customer, ContractNumber = "HD-" + code, Status = "Active", EffectiveFrom = new DateTime(2026, 1, 1) });
                db.ContractMeal.AddRange(
                    new ContractMeal { Id = sang, ContractId = contract, MealType = "SANG", MealName = "Suất sáng", UnitPrice = 20000 },
                    new ContractMeal { Id = trua, ContractId = contract, MealType = "TRUA", MealName = "Suất trưa", UnitPrice = 40000 });
                foreach (var (id, name) in new[] { (d1, "Bánh mì"), (d2, "Bánh bao"), (d3, "Xôi"), (com, "Cơm") })
                {
                    db.Dish.Add(new Dish { Id = id, Code = code + "-" + name.GetHashCode().ToString("X"), Name = name });
                }
                db.Menu.Add(new Model.Entity.Menu { Id = menu, Code = "NU-" + code, Name = "Thực đơn chung " + code, VersionNo = 1, Status = "Published", MenuType = "COMMON", EffectiveFrom = Monday, EffectiveTo = Monday.AddDays(6) });
                for (int day = 1; day <= 7; day++)
                {
                    foreach (var (option, dish) in new[] { (1, d1), (2, d2), (3, d3) })
                    {
                        db.MenuLine.Add(new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = dish, MealType = "SANG", SequenceNo = option, DayOfWeek = day, SortOrder = 0, PortionQty = 1 });
                    }
                    db.MenuLine.Add(new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = com, MealType = "TRUA", SequenceNo = 1, DayOfWeek = day, SortOrder = 0, PortionQty = 1 });
                }
            });
            return new Scenario(customer, locA, locB, sang, trua);
        }

        private Task SeedForecastAsync(Scenario s, Guid location, DateTime day, string meal, decimal qty) => _factory.SeedAsync(db =>
        {
            db.Forecast.Add(new Forecast { Id = Guid.NewGuid(), CustomerId = s.Customer, DeliveryLocationId = location, ForecastDate = day, MealType = meal, Quantity = qty, ForecastQuantity = qty, Status = "Published", VersionNo = 1 });
            return Task.CompletedTask;
        });

        private static async Task<LegacyResult> PostJson(HttpClient c, string url, object body) => await LegacyResult.ReadAsync(await c.PostAsJsonAsync(url, body));

        [Fact]
        public async Task Overview_lists_contract_points_with_status_and_forecast_then_confirm_many_points()
        {
            var s = await SeedAsync("OWV1");
            await SeedForecastAsync(s, s.LocB, Monday, "TRUA", 500);
            await SeedForecastAsync(s, s.LocA, Monday, "TRUA", 400);
            using var c = _factory.CreateAuthorizedClient();
            var cells = new object[]
            {
                new { mealDate = Monday, contractMealId = s.MealTrua, menuOptionNo = 1, quantity = 440 },
                new { mealDate = Monday.AddDays(1), contractMealId = s.MealTrua, menuOptionNo = 1, quantity = 420 },
            };
            (await PostJson(c, "/api/Order/SaveWeek", new { customerId = s.Customer, deliveryLocationId = s.LocA, weekStart = Monday, confirm = false, listCell = cells })).Ok();

            var ov = (await c.GetResultAsync($"/api/Order/GetWeekOverview?WeekStart={Monday.AddDays(2):yyyy-MM-dd}")).Ok();
            ov.GetProperty("weekStart").GetDateTime().Should().Be(Monday);
            var cust = ov.GetProperty("listCustomer").EnumerateArray().Single(x => x.GetProperty("customerId").GetGuid() == s.Customer);
            var points = cust.GetProperty("listPoint").EnumerateArray().ToList();
            points.Should().HaveCount(2, "mọi điểm giao đang hoạt động của khách có hợp đồng hiệu lực");
            var a = points.Single(p => p.GetProperty("deliveryLocationId").GetGuid() == s.LocA);
            var b = points.Single(p => p.GetProperty("deliveryLocationId").GetGuid() == s.LocB);
            a.GetProperty("listCell")[0].GetProperty("status").GetString().Should().Be("DRAFT");
            a.GetProperty("listCell")[0].GetProperty("quantity").GetDecimal().Should().Be(440);
            a.GetProperty("listCell")[2].GetProperty("status").GetString().Should().Be("NONE");
            a.GetProperty("totalWeek").GetDecimal().Should().Be(860);
            b.GetProperty("status").GetString().Should().Be("NONE");
            b.GetProperty("listCell")[0].GetProperty("forecast").GetDecimal().Should().Be(500, "ô chưa đăng ký hiện số dự báo đã công bố");
            cust.GetProperty("listTotal")[0].GetDecimal().Should().Be(440);
            ov.GetProperty("forecastDeviationPct").GetDecimal().Should().BeGreaterThan(0, "440 so dự báo 400 (chỉ tính ô có cả hai)");

            var conf = (await PostJson(c, "/api/Order/ConfirmWeekPoints", new { weekStart = Monday, listDeliveryLocationId = new[] { s.LocA, s.LocB } })).Ok();
            conf.GetProperty("confirmed").GetInt32().Should().Be(2);
            conf.GetProperty("points").GetInt32().Should().Be(1);
            (await _factory.QueryAsync(db => db.Order.Where(o => o.DeliveryLocationId == s.LocA).Select(o => o.Status).ToListAsync())).Should().OnlyContain(x => x == "Confirmed");
            (await PostJson(c, "/api/Order/ConfirmWeekPoints", new { weekStart = Monday, listDeliveryLocationId = new[] { s.LocA } })).Fail("Không có điểm giao");

            ov = (await c.GetResultAsync($"/api/Order/GetWeekOverview?WeekStart={Monday:yyyy-MM-dd}")).Ok();
            cust = ov.GetProperty("listCustomer").EnumerateArray().Single(x => x.GetProperty("customerId").GetGuid() == s.Customer);
            cust.GetProperty("listPoint").EnumerateArray().Single(p => p.GetProperty("deliveryLocationId").GetGuid() == s.LocA).GetProperty("listCell")[0].GetProperty("status").GetString().Should().Be("CONFIRMED");
        }

        [Fact]
        public async Task Fill_from_forecast_splits_options_by_four_week_ratio_and_does_not_save()
        {
            var s = await SeedAsync("OWV2");
            // 4 thứ Hai trước: SANG LC1 2.000, LC2 1.000 (tỉ lệ 2 : 1), LC3 không đặt.
            await _factory.SeedAsync(db =>
            {
                for (int w = 1; w <= 4; w++)
                {
                    var id = Guid.NewGuid();
                    db.Order.Add(new Order { Id = id, CustomerId = s.Customer, DeliveryLocationId = s.LocA, MealDate = Monday.AddDays(-7 * w), Status = "Reconciled", OrderNumber = $"DH-OWV2-{w}", TotalQuantity = 3000 });
                    db.OrderLine.Add(new OrderLine { Id = Guid.NewGuid(), OrderId = id, ContractMealId = s.MealSang, MealType = "SANG", MenuOptionNo = 1, Quantity = 2000 });
                    db.OrderLine.Add(new OrderLine { Id = Guid.NewGuid(), OrderId = id, ContractMealId = s.MealSang, MealType = "SANG", MenuOptionNo = 2, Quantity = 1000 });
                }
                return Task.CompletedTask;
            });
            await SeedForecastAsync(s, s.LocA, Monday, "SANG", 301);
            await SeedForecastAsync(s, s.LocA, Monday, "TRUA", 450);
            using var c = _factory.CreateAuthorizedClient();
            var week = (await c.GetResultAsync($"/api/Order/FillWeekFromForecast?customerId={s.Customer}&deliveryLocationId={s.LocA}&weekStart={Monday:yyyy-MM-dd}")).Ok();
            var rows = week.GetProperty("listRow").EnumerateArray().ToList();
            decimal Q(string meal, int opt, int day) => rows.Single(r => r.GetProperty("mealType").GetString() == meal && r.GetProperty("menuOptionNo").GetInt32() == opt).GetProperty("listCell")[day].GetProperty("quantity").GetDecimal();
            Q("SANG", 1, 0).Should().Be(201, "301 × 2/3 = 200,67 → 200 + phần lẻ 1 vào lựa chọn lớn nhất");
            Q("SANG", 2, 0).Should().Be(100);
            Q("SANG", 3, 0).Should().Be(0);
            Q("TRUA", 1, 0).Should().Be(450);
            Q("TRUA", 1, 1).Should().Be(0, "ngày không có dự báo để trống");
            (await _factory.QueryAsync(db => db.Order.CountAsync(o => o.DeliveryLocationId == s.LocA && o.MealDate >= Monday))).Should().Be(0, "chỉ trả về, không lưu");
        }

        private async Task<byte[]> SlipAsync(HttpClient c, Scenario s, Guid loc, Action<IXLWorksheet> fill)
        {
            var file = await c.GetByteArrayAsync($"/api/Order/ExportWeekSlip?customerId={s.Customer}&deliveryLocationId={loc}&weekStart={Monday:yyyy-MM-dd}");
            using var input = new MemoryStream(file);
            using var wb = new XLWorkbook(input);
            fill(wb.Worksheet(1));
            using var output = new MemoryStream();
            wb.SaveAs(output);
            return output.ToArray();
        }

        [Fact]
        public async Task Import_many_slips_recognises_points_saves_drafts_and_reports_bad_files()
        {
            var s = await SeedAsync("OWV3");
            using var c = _factory.CreateAuthorizedClient();
            void Trua(IXLWorksheet ws, decimal qty)
            {
                int row = Enumerable.Range(3, ws.LastRowUsed()!.RowNumber()).First(r => ws.Cell(r, 10).GetString().StartsWith(s.MealTrua.ToString("D")));
                ws.Cell(row, 3).Value = qty;
            }
            var slipA = await SlipAsync(c, s, s.LocA, ws => Trua(ws, 300));
            var slipB = await SlipAsync(c, s, s.LocB, ws => Trua(ws, 200));
            var otherWeek = await SlipAsync(c, s, s.LocB, ws => ws.Cell(1, 1).Value = ws.Cell(1, 1).GetString().Replace("2026-12-28", "2027-01-04"));
            using var junk = new XLWorkbook();
            junk.AddWorksheet("x").Cell(1, 1).Value = "khong phai phieu";
            using var junkStream = new MemoryStream();
            junk.SaveAs(junkStream);
            using var form = new MultipartFormDataContent
            {
                { new StringContent(Monday.ToString("yyyy-MM-dd")), "weekStart" },
                { new ByteArrayContent(slipA), "files", "A.xlsx" },
                { new ByteArrayContent(slipB), "files", "B.xlsx" },
                { new ByteArrayContent(otherWeek), "files", "tuan-khac.xlsx" },
                { new ByteArrayContent(junkStream.ToArray()), "files", "rac.xlsx" },
            };
            var r = (await LegacyResult.ReadAsync(await c.PostAsync("/api/Order/ImportWeekSlips", form))).Ok();
            r.GetProperty("files").GetInt32().Should().Be(4);
            r.GetProperty("saved").GetInt32().Should().Be(2);
            r.GetProperty("failed").GetInt32().Should().Be(2);
            var files = r.GetProperty("listFile").EnumerateArray().ToList();
            files.Single(f => f.GetProperty("fileName").GetString() == "B.xlsx").GetProperty("locationName").GetString().Should().Be("Nhà máy B", "tự nhận điểm giao qua mã ẩn");
            files.Single(f => f.GetProperty("fileName").GetString() == "tuan-khac.xlsx").GetProperty("message").GetString().Should().Contain("không phải tuần đang mở");
            files.Single(f => f.GetProperty("fileName").GetString() == "rac.xlsx").GetProperty("message").GetString().Should().Contain("Không phải phiếu");
            var orders = await _factory.QueryAsync(db => db.Order.AsNoTracking().Where(o => o.CustomerId == s.Customer && o.MealDate == Monday).ToListAsync());
            orders.Should().HaveCount(2).And.OnlyContain(o => o.Status == "Draft", "tải phiếu = lưu Nháp, KD kiểm rồi xác nhận");
            orders.Sum(o => o.TotalQuantity).Should().Be(500);
        }

        [Fact]
        public async Task Points_without_order_list_contract_points_missing_tomorrow()
        {
            var s = await SeedAsync("OWV4");
            DateTime day = Monday.AddDays(3);
            await _factory.SeedAsync(db =>
            {
                db.Order.Add(new Order { Id = Guid.NewGuid(), CustomerId = s.Customer, DeliveryLocationId = s.LocA, MealDate = day, Status = "Confirmed", OrderNumber = "DH-OWV4", TotalQuantity = 100 });
                return Task.CompletedTask;
            });
            using var scope = _factory.Services.CreateScope();
            var points = await OrderService.PointsWithoutOrderAsync(scope.ServiceProvider.GetRequiredService<IUnitOfWork>(), day);
            points.Where(p => p.CustomerId == s.Customer).Select(p => p.LocationName).Should().Equal(["Nhà máy B"], "điểm A đã có đơn; không tự tạo đơn cho B");
        }
    }
}
