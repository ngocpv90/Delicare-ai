using System.Text;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Model;
using AppDelicare.Services.Interface;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Sales
{
    /// <summary>
    /// UI-6.5 (D-84): dự báo theo tuần – khách có hợp đồng hiệu lực tự hiện (kể cả chưa có dự báo), ngày ngoài hiệu lực khóa,
    /// ô khách đã đăng ký (đơn Đã xác nhận trở lên) chỉ đọc + dùng số đăng ký, sửa ô = lưu Nháp, công bố tuần bỏ qua ô đã đăng ký, tự công bố ghi chú.
    /// </summary>
    public class ForecastWeekTests(InMemoryApiFactory factory) : IClassFixture<InMemoryApiFactory>
    {
        private static DateTime NextMonday()
        {
            DateTime today = DateTime.Today;
            int days = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
            return today.AddDays(days == 0 ? 7 : days);
        }

        private sealed record Scenario(Guid Customer, Guid Location, DateTime Monday);

        private async Task<Scenario> SeedAsync(string code, int contractDays = 3)
        {
            Guid customer = Guid.NewGuid(), location = Guid.NewGuid(), contract = Guid.NewGuid(), order = Guid.NewGuid();
            DateTime monday = NextMonday();
            await factory.SeedAsync(db =>
            {
                db.Customer.Add(new Customer { Id = customer, Code = code, Name = "Khách " + code, CIF = code, CustomerType = "BUSINESS", DisplayName = "Khách " + code, Status = "Active" });
                db.CustomerDeliveryLocation.Add(new CustomerDeliveryLocation { Id = location, CustomerId = customer, Code = code + "-A", Name = "Căng tin " + code, Status = "Active" });
                db.Contract.Add(new Contract { Id = contract, CustomerId = customer, ContractNumber = "HD-" + code, Status = "Active", EffectiveFrom = DateTime.Today.AddDays(-60), EffectiveTo = monday.AddDays(contractDays) });
                db.ContractMeal.Add(new ContractMeal { Id = Guid.NewGuid(), ContractId = contract, MealType = "TRUA", MealName = "Suất trưa", UnitPrice = 40000 });
                // Thứ Ba: khách đã đăng ký 120 suất (đơn Đã xác nhận)
                db.Order.Add(new Order { Id = order, CustomerId = customer, ContractId = contract, DeliveryLocationId = location, MealDate = monday.AddDays(1), Status = "Confirmed", OrderNumber = "DH-" + code, TotalQuantity = 120 });
                db.OrderLine.Add(new OrderLine { Id = Guid.NewGuid(), OrderId = order, MealType = "TRUA", Quantity = 120 });
                return Task.CompletedTask;
            });
            return new Scenario(customer, location, monday);
        }

        private static StringContent Json(string s) => new(s, Encoding.UTF8, "application/json");

        [Fact]
        public async Task Week_lists_contract_customers_registered_cells_are_read_only_and_cells_save_as_draft()
        {
            var s = await SeedAsync("FW1");
            using var c = factory.CreateAuthorizedClient();
            var week = (await c.GetResultAsync($"/api/Forecast/GetWeek?WeekStart={s.Monday:yyyy-MM-dd}")).Ok();
            week.GetProperty("weekStart").GetDateTime().Date.Should().Be(s.Monday);
            var cust = week.GetProperty("listCustomer").EnumerateArray().Single(x => x.GetProperty("customerId").GetGuid() == s.Customer);
            var row = cust.GetProperty("listRow").EnumerateArray().Single();
            var cells = row.GetProperty("listCell").EnumerateArray().ToList();
            cells.Should().HaveCount(7);
            cells[1].GetProperty("status").GetString().Should().Be("REGISTERED");
            cells[1].GetProperty("value").GetDecimal().Should().Be(120);
            cells[0].GetProperty("status").GetString().Should().BeOneOf("MISSING", "EMPTY");
            cells[5].GetProperty("status").GetString().Should().Be("LOCKED", "thứ Bảy ngoài hiệu lực hợp đồng (hết thứ Năm)");

            string Cell(int day, decimal? qty) => $$"""{"customerId":"{{s.Customer}}","deliveryLocationId":"{{s.Location}}","mealType":"TRUA","date":"{{s.Monday.AddDays(day):yyyy-MM-dd}}","quantity":{{(qty == null ? "null" : qty.ToString())}}}""";
            (await LegacyResult.ReadAsync(await c.PostAsync("/api/Forecast/SaveCell", Json(Cell(1, 150))))).Fail("đã đăng ký");
            (await LegacyResult.ReadAsync(await c.PostAsync("/api/Forecast/SaveCell", Json(Cell(0, 100))))).Ok();
            (await LegacyResult.ReadAsync(await c.PostAsync("/api/Forecast/SaveCell", Json(Cell(0, 105))))).Ok();
            var drafts = await factory.QueryAsync(db => db.Forecast.Where(f => f.CustomerId == s.Customer && !f.IsDeleted && f.Status == "Draft").ToListAsync());
            drafts.Should().ContainSingle("sửa lại ô = cập nhật cùng bản Nháp").Which.ForecastQuantity.Should().Be(105);
            (await LegacyResult.ReadAsync(await c.PostAsync("/api/Forecast/SaveCell", Json(Cell(2, 90))))).Ok();
            (await LegacyResult.ReadAsync(await c.PostAsync("/api/Forecast/SaveCell", Json(Cell(2, null))))).Ok();
            (await factory.QueryAsync(db => db.Forecast.CountAsync(f => f.CustomerId == s.Customer && !f.IsDeleted && f.Status == "Draft"))).Should().Be(1, "ô để trống = xóa Nháp");

            week = (await c.GetResultAsync($"/api/Forecast/GetWeek?WeekStart={s.Monday:yyyy-MM-dd}")).Ok();
            cust = week.GetProperty("listCustomer").EnumerateArray().Single(x => x.GetProperty("customerId").GetGuid() == s.Customer);
            var first = cust.GetProperty("listRow")[0].GetProperty("listCell")[0];
            first.GetProperty("status").GetString().Should().Be("DRAFT");
            first.GetProperty("value").GetDecimal().Should().Be(105);
            cust.GetProperty("listTotal")[0].GetDecimal().Should().Be(105);
            cust.GetProperty("listTotal")[1].GetDecimal().Should().Be(120, "ô đã đăng ký tính theo số đăng ký");
        }

        [Fact]
        public async Task Publish_week_skips_registered_cells_and_auto_publish_marks_system()
        {
            var s = await SeedAsync("FW2");
            await factory.SeedAsync(db =>
            {
                // Nháp ở ô đã đăng ký (tạo trước khi khách đăng ký) → bỏ qua khi công bố
                db.Forecast.Add(new Forecast { Id = Guid.NewGuid(), CustomerId = s.Customer, DeliveryLocationId = s.Location, ForecastDate = s.Monday.AddDays(1), MealType = "TRUA", Quantity = 99, ForecastQuantity = 99, Status = "Draft", VersionNo = 1 });
                return Task.CompletedTask;
            });
            using var c = factory.CreateAuthorizedClient();
            (await LegacyResult.ReadAsync(await c.PostAsync("/api/Forecast/SaveCell",
                Json($$"""{"customerId":"{{s.Customer}}","deliveryLocationId":"{{s.Location}}","mealType":"TRUA","date":"{{s.Monday:yyyy-MM-dd}}","quantity":80}""")))).Ok();
            using var scope = factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IForecastService>();
            using var job = scope.ServiceProvider.GetRequiredService<ICurrentActor>().BeginScope(ActorInfo.ForSystem(Guid.NewGuid()), "ForecastAutoPublish");
            var r = await svc.PublishWeek(new ForecastWeekActionRequest { WeekStart = s.Monday, CustomerId = s.Customer }, auto: true);
            r.Data!.Published.Should().Be(1);
            r.Data.SkippedRegistered.Should().Be(1);
            var pub = await factory.QueryAsync(db => db.Forecast.SingleAsync(f => f.CustomerId == s.Customer && f.ForecastDate == s.Monday && f.Status == "Published"));
            pub.Note.Should().Contain("Hệ thống tự công bố");
            (await factory.QueryAsync(db => db.Forecast.CountAsync(f => f.CustomerId == s.Customer && f.ForecastDate == s.Monday.AddDays(1) && f.Status == "Draft"))).Should().Be(1, "ô đã đăng ký giữ Nháp, không công bố");
        }
    }
}
