using System.Net.Http.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Tests.Common;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Sales
{
    /// <summary>UI-6.15 (D-96): Order/GetDay (điểm chưa có đơn + dự báo, cờ cần chú ý, đơn phát sinh, đã chốt) + Order/CreateExtra. Đồng hồ 08/12/2026 10:00, ngày ăn 29/12.</summary>
    public class OrderDayTests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;
        private static readonly DateTime Day = new(2026, 12, 29);

        public OrderDayTests(InMemoryApiFactory factory)
        {
            _factory = factory;
            _factory.Clock.FixedNow = new DateTime(2026, 12, 8, 10, 0, 0);
        }

        private sealed record Scenario(Guid Customer, Guid LocA, Guid LocB, Guid MealTrua);

        private async Task<Scenario> SeedAsync(string code)
        {
            Guid customer = Guid.NewGuid(), locA = Guid.NewGuid(), locB = Guid.NewGuid(), contract = Guid.NewGuid(), trua = Guid.NewGuid(), menu = Guid.NewGuid(), com = Guid.NewGuid();
            await _factory.SeedAsync(async db =>
            {
                if (!await db.Shift.AnyAsync(x => x.Code == "TRUA"))
                {
                    db.Shift.Add(new Shift { Id = Guid.NewGuid(), Code = "TRUA", Name = "Ca TRUA" });
                }
                db.Customer.Add(new Customer { Id = customer, Code = code, Name = "Khách " + code, CIF = code, CustomerType = "BUSINESS", DisplayName = code, Status = "Active" });
                db.CustomerDeliveryLocation.AddRange(
                    new CustomerDeliveryLocation { Id = locA, CustomerId = customer, Code = code + "-A", Name = "Xưởng A" },
                    new CustomerDeliveryLocation { Id = locB, CustomerId = customer, Code = code + "-B", Name = "Xưởng B" });
                db.Contract.Add(new Contract { Id = contract, CustomerId = customer, ContractNumber = "HD-" + code, Status = "Active", EffectiveFrom = new DateTime(2026, 1, 1) });
                db.ContractMeal.Add(new ContractMeal { Id = trua, ContractId = contract, MealType = "TRUA", MealName = "Suất trưa", UnitPrice = 40000 });
                db.Dish.Add(new Dish { Id = com, Code = code + "-COM", Name = "Cơm " + code });
                db.Menu.Add(new Model.Entity.Menu { Id = menu, Code = "NU-" + code, Name = "Thực đơn " + code, VersionNo = 1, Status = "Published", MenuType = "COMMON", EffectiveFrom = Day.AddDays(-1), EffectiveTo = Day.AddDays(5) });
                for (int d = 1; d <= 7; d++)
                {
                    db.MenuLine.Add(new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = com, MealType = "TRUA", SequenceNo = 1, DayOfWeek = d, SortOrder = 0, PortionQty = 1 });
                }
                db.Forecast.Add(new Forecast { Id = Guid.NewGuid(), CustomerId = customer, DeliveryLocationId = locA, ForecastDate = Day, MealType = "TRUA", Quantity = 100, ForecastQuantity = 100, Status = "Published", VersionNo = 1 });
                db.Forecast.Add(new Forecast { Id = Guid.NewGuid(), CustomerId = customer, DeliveryLocationId = locB, ForecastDate = Day, MealType = "TRUA", Quantity = 150, ForecastQuantity = 150, Status = "Published", VersionNo = 1 });
            });
            return new Scenario(customer, locA, locB, trua);
        }

        private static async Task<LegacyResult> PostJson(HttpClient c, string url, object body) => await LegacyResult.ReadAsync(await c.PostAsJsonAsync(url, body));

        [Fact]
        public async Task Day_lists_points_without_order_with_forecast_flags_and_extra_orders()
        {
            var s = await SeedAsync("ODY1");
            using var c = _factory.CreateAuthorizedClient();
            // Điểm A: đơn tuần Nháp 160 suất (lệch +60 % so dự báo 100).
            (await PostJson(c, "/api/Order/SaveWeek", new { customerId = s.Customer, deliveryLocationId = s.LocA, weekStart = Day.AddDays(-1), confirm = false,
                listCell = new object[] { new { mealDate = Day, contractMealId = s.MealTrua, menuOptionNo = 1, quantity = 160 } } })).Ok();

            var day = (await c.GetResultAsync($"/api/Order/GetDay?MealDate={Day:yyyy-MM-dd}")).Ok();
            day.GetProperty("mealDate").GetDateTime().Should().Be(Day);
            day.GetProperty("dayState").GetString().Should().Be("OPEN");
            day.GetProperty("listMealType").EnumerateArray().Select(x => x.GetString()).Should().Equal("TRUA");
            var cust = day.GetProperty("listCustomer").EnumerateArray().Single(x => x.GetProperty("customerId").GetGuid() == s.Customer);
            var rows = cust.GetProperty("listRow").EnumerateArray().ToList();
            rows.Should().HaveCount(2);
            var a = rows.Single(r => r.GetProperty("deliveryLocationId").GetGuid() == s.LocA);
            var b = rows.Single(r => r.GetProperty("deliveryLocationId").GetGuid() == s.LocB);
            a.GetProperty("source").GetString().Should().Be("WEEK");
            a.GetProperty("total").GetDecimal().Should().Be(160);
            a.GetProperty("listMeal")[0].GetProperty("listOption")[0].GetProperty("quantity").GetDecimal().Should().Be(160);
            var codes = a.GetProperty("listAttention").EnumerateArray().Select(x => x.GetProperty("code").GetString()).ToList();
            codes.Should().Contain(["UNCONFIRMED", "DEVIATION"]);
            b.GetProperty("orderId").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
            b.GetProperty("forecast").GetDecimal().Should().Be(150, "điểm chưa có đơn hiện dự báo");
            b.GetProperty("listAttention")[0].GetProperty("action").GetString().Should().Be("INPUT");
            cust.GetProperty("attentionCount").GetInt32().Should().Be(2);

            // Đơn phát sinh vào điểm đã có đơn tuần: cảnh báo, không chặn; trước giờ chốt → Đã xác nhận.
            var extra = (await PostJson(c, "/api/Order/CreateExtra", new { customerId = s.Customer, deliveryLocationId = s.LocA, mealDate = Day, reason = "EVENT", note = "Tiệc 10 năm",
                listLine = new object[] { new { contractMealId = s.MealTrua, menuOptionNo = 1, quantity = 30 } } })).Ok();
            extra.GetProperty("order").GetProperty("status").GetString().Should().Be("Confirmed");
            extra.GetProperty("listWarning").EnumerateArray().Select(w => w.GetString()).Should().Contain(w => w!.Contains("đơn thêm"));
            (await PostJson(c, "/api/Order/CreateExtra", new { customerId = s.Customer, deliveryLocationId = s.LocA, mealDate = Day, reason = "XYZ",
                listLine = new object[] { new { contractMealId = s.MealTrua, quantity = 5 } } })).Fail("Lý do");
            // Tạo đơn thường vẫn chặn trùng.
            (await PostJson(c, "/api/Order/Create", new { customerId = s.Customer, deliveryLocationId = s.LocA, mealDate = Day,
                listLine = new object[] { new { contractMealId = s.MealTrua, quantity = 5 } } })).Fail("Đã có đơn");

            day = (await c.GetResultAsync($"/api/Order/GetDay?MealDate={Day:yyyy-MM-dd}")).Ok();
            cust = day.GetProperty("listCustomer").EnumerateArray().Single(x => x.GetProperty("customerId").GetGuid() == s.Customer);
            var aRows = cust.GetProperty("listRow").EnumerateArray().Where(r => r.GetProperty("deliveryLocationId").GetGuid() == s.LocA).ToList();
            aRows.Should().HaveCount(2, "mỗi đơn một dòng");
            aRows[1].GetProperty("source").GetString().Should().Be("EXTRA");
            aRows[1].GetProperty("extraReason").GetString().Should().Be("Sự kiện / tiệc");
            aRows[1].GetProperty("progress").GetInt32().Should().Be(1);
            aRows[1].GetProperty("listAttention").EnumerateArray().Should().OnlyContain(x => x.GetProperty("tone").GetString() == "info");
            cust.GetProperty("total").GetDecimal().Should().Be(190);
            day.GetProperty("extraCount").GetInt32().Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task Day_after_cutoff_is_locked_and_needs_view_permission()
        {
            var s = await SeedAsync("ODY2");
            using var c = _factory.CreateAuthorizedClient();
            (await PostJson(c, "/api/Order/SaveWeek", new { customerId = s.Customer, deliveryLocationId = s.LocA, weekStart = Day.AddDays(-1), confirm = true,
                listCell = new object[] { new { mealDate = Day, contractMealId = s.MealTrua, menuOptionNo = 1, quantity = 100 } } })).Ok();
            _factory.Clock.FixedNow = new DateTime(2026, 12, 28, 21, 0, 0);
            try
            {
                var day = (await c.GetResultAsync($"/api/Order/GetDay?MealDate={Day:yyyy-MM-dd}")).Ok();
                day.GetProperty("dayState").GetString().Should().Be("LOCKED");
                var cust = day.GetProperty("listCustomer").EnumerateArray().Single(x => x.GetProperty("customerId").GetGuid() == s.Customer);
                var b = cust.GetProperty("listRow").EnumerateArray().Single(r => r.GetProperty("deliveryLocationId").GetGuid() == s.LocB);
                b.GetProperty("listAttention")[0].GetProperty("tone").GetString().Should().Be("info", "đã chốt – không còn việc nhập số");
                // Sau giờ chốt: đơn phát sinh gửi duyệt.
                var extra = await PostJson(c, "/api/Order/CreateExtra", new { customerId = s.Customer, deliveryLocationId = s.LocB, mealDate = Day, reason = "ADDON",
                    listLine = new object[] { new { contractMealId = s.MealTrua, quantity = 20 } } });
                extra.Ok().GetProperty("approvalRequested").GetBoolean().Should().BeTrue();
            }
            finally
            {
                _factory.Clock.FixedNow = new DateTime(2026, 12, 8, 10, 0, 0);
            }
            Guid viewer = await CommonTestUsers.UserAsync(_factory, ("crm-order", "V"));
            (await _factory.CreateAuthorizedClient(viewer).GetResultAsync($"/api/Order/GetDay?MealDate={Day:yyyy-MM-dd}")).Ok();
            (await _factory.CreateAuthorizedClient(viewer).PostAsJsonAsync("/api/Order/CreateExtra", new { customerId = s.Customer })).StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden, "chỉ quyền Xem – không thêm đơn");
            Guid nobody = await CommonTestUsers.UserAsync(_factory);
            (await _factory.CreateAuthorizedClient(nobody).GetAsync($"/api/Order/GetDay?MealDate={Day:yyyy-MM-dd}")).StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
        }
    }
}
