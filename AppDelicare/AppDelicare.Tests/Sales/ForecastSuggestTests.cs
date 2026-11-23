using AppDelicare.Model.Entity;
using AppDelicare.Services.Implement;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Sales
{
    /// <summary>
    /// FC-1 (D-76): dự báo hệ thống tự gợi ý. Hôm nay (đồng hồ test) = thứ Hai 03/05/2027 → mặc định 10/05 → 23/05.
    /// <list type="bullet">
    /// <item>Khách A (điểm L1, TRUA): thứ Ba 27/04 đối soát tính tiền 98 (đặt 100) · 20/04 POD nhận 105 (đặt 110) · 13/04 nghỉ (đơn Hủy – không tính)
    ///   · 06/04 đơn Đã chốt 120 → TB (98 + 105 + 120) / 3 = 107,67 → gợi ý <b>108</b> cho thứ Ba 11/05 và 18/05.</item>
    /// <item>Khách A thứ Tư: chỉ 28/04 có đơn 50 → &lt; 2 tuần → Thiếu dữ liệu.</item>
    /// <item>Khách B: hợp đồng hết hiệu lực 11/05 → không có gợi ý sau 11/05; điểm giao Ngừng → không có khóa.</item>
    /// </list>
    /// </summary>
    public class ForecastSuggestTests : IClassFixture<InMemoryApiFactory>
    {
        private static readonly DateTime Today = new(2027, 5, 3);
        private readonly InMemoryApiFactory _factory;

        public ForecastSuggestTests(InMemoryApiFactory factory)
        {
            _factory = factory;
            _factory.Clock.FixedNow = Today.AddHours(10);
        }

        private sealed record Scenario(Guid A, Guid L1, Guid B, Guid BL1, Guid BLOff);

        private async Task<Scenario> SeedAsync(string code)
        {
            Guid a = Guid.NewGuid(), l1 = Guid.NewGuid(), b = Guid.NewGuid(), bl1 = Guid.NewGuid(), blOff = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Customer.AddRange(
                    new Customer { Id = a, Code = code + "A", Name = "Khách A " + code, CIF = code + "A", CustomerType = "FACTORY", DisplayName = code + "A", Status = "Active" },
                    new Customer { Id = b, Code = code + "B", Name = "Khách B " + code, CIF = code + "B", CustomerType = "FACTORY", DisplayName = code + "B", Status = "Active" });
                db.CustomerDeliveryLocation.AddRange(
                    new CustomerDeliveryLocation { Id = l1, CustomerId = a, Code = code + "-L1", Name = "Điểm L1" },
                    new CustomerDeliveryLocation { Id = bl1, CustomerId = b, Code = code + "-B1", Name = "Điểm B1", Status = "Active" },
                    new CustomerDeliveryLocation { Id = blOff, CustomerId = b, Code = code + "-B2", Name = "Điểm B2", Status = "Inactive" });
                Guid ca = Guid.NewGuid(), cb = Guid.NewGuid();
                db.Contract.AddRange(
                    new Contract { Id = ca, CustomerId = a, ContractNumber = "HD-" + code + "A", Status = "Active", EffectiveFrom = new DateTime(2027, 1, 1) },
                    new Contract { Id = cb, CustomerId = b, ContractNumber = "HD-" + code + "B", Status = "Active", EffectiveFrom = new DateTime(2027, 1, 1), EffectiveTo = new DateTime(2027, 5, 11) });
                db.ContractMeal.AddRange(
                    new ContractMeal { Id = Guid.NewGuid(), ContractId = ca, MealType = "TRUA", MealName = "Suất trưa", UnitPrice = 40000 },
                    new ContractMeal { Id = Guid.NewGuid(), ContractId = cb, MealType = "TRUA", MealName = "Suất trưa", UnitPrice = 40000 });

                Guid Order(Guid customer, Guid location, DateTime date, string status, decimal qty)
                {
                    Guid id = Guid.NewGuid();
                    string no = $"DH-{code}-{customer.ToString("N")[..4]}-{date:MMdd}-{status}";
                    db.Order.Add(new Order { Id = id, OrderNumber = no, OrderNo = no, CustomerId = customer, DeliveryLocationId = location, OrderDate = date.AddDays(-1), MealDate = date, DeliveryDate = date, Status = status, TotalQuantity = qty });
                    db.OrderLine.Add(new OrderLine { Id = Guid.NewGuid(), OrderId = id, MealType = "TRUA", Quantity = qty });
                    return id;
                }
                Guid o27 = Order(a, l1, new DateTime(2027, 4, 27), "Reconciled", 100);
                db.OrderReconciliation.Add(new OrderReconciliation { Id = Guid.NewGuid(), OrderId = o27, ReconciliationDate = new DateTime(2027, 4, 28), OrderedQuantity = 100, DeliveredQuantity = 100, AcceptedQuantity = 95, Status = "Closed",
                    Note = "{\"podAccepted\":100,\"fault\":2,\"billingBasis\":\"RECEIVED\",\"billingQuantity\":98}" });
                Guid o20 = Order(a, l1, new DateTime(2027, 4, 20), "Delivered", 110);
                Guid delivery = Guid.NewGuid();
                db.DeliveryOrder.Add(new DeliveryOrder { Id = delivery, OrderId = o20, CustomerId = a, DeliveryDate = new DateTime(2027, 4, 20), PlannedDeliveryTime = new DateTime(2027, 4, 20, 11, 0, 0), Status = "Delivered" });
                db.ProofOfDelivery.Add(new ProofOfDelivery { Id = Guid.NewGuid(), DeliveryOrderId = delivery, AcceptedQuantity = 105, DeliveredQuantity = 110, ConfirmedAt = new DateTime(2027, 4, 20, 11, 30, 0) });
                Order(a, l1, new DateTime(2027, 4, 13), "Cancelled", 90);
                Order(a, l1, new DateTime(2027, 4, 6), "Locked", 120);
                Order(a, l1, new DateTime(2027, 4, 28), "Delivered", 50);
                Order(b, bl1, new DateTime(2027, 4, 26), "Delivered", 30);
                Order(b, bl1, new DateTime(2027, 4, 19), "Delivered", 30);
                // Dự báo đã công bố của các ngày đã qua (độ chính xác) + bản Nháp nhập tay cho 11/05.
                db.Forecast.AddRange(
                    new Forecast { Id = Guid.NewGuid(), CustomerId = a, DeliveryLocationId = l1, ForecastDate = new DateTime(2027, 4, 20), MealType = "TRUA", ForecastQuantity = 100, Quantity = 100, VersionNo = 1, Status = "Published" },
                    new Forecast { Id = Guid.NewGuid(), CustomerId = a, DeliveryLocationId = l1, ForecastDate = new DateTime(2027, 4, 27), MealType = "TRUA", ForecastQuantity = 100, Quantity = 100, VersionNo = 1, Status = "Published" },
                    new Forecast { Id = Guid.NewGuid(), CustomerId = a, DeliveryLocationId = l1, ForecastDate = new DateTime(2027, 5, 11), MealType = "TRUA", ForecastQuantity = 200, Quantity = 200, VersionNo = 1, Status = "Draft", Note = "nhập tay" });
                return Task.CompletedTask;
            });
            return new Scenario(a, l1, b, bl1, blOff);
        }

        [Fact]
        public async Task Suggest_averages_same_weekday_skips_holidays_and_keeps_edited_drafts()
        {
            var s = await SeedAsync("FS" + Guid.NewGuid().ToString("N")[..4]);
            using var c = _factory.CreateAuthorizedClient();

            // Xem trước: không ghi.
            var preview = (await c.PostFormAsync("/api/Forecast/Suggest", ("CustomerId", s.A), ("Preview", true))).Ok();
            preview.GetProperty("from").GetDateTime().Should().Be(new DateTime(2027, 5, 10));
            preview.GetProperty("to").GetDateTime().Should().Be(new DateTime(2027, 5, 23));
            var tue = preview.GetProperty("listCell").EnumerateArray().Single(x => x.GetProperty("date").GetDateTime() == new DateTime(2027, 5, 11));
            tue.GetProperty("suggested").GetDecimal().Should().Be(108, "TB 98 / 105 / 120 = 107,67 làm tròn lên; tuần 13/04 nghỉ không tính");
            tue.GetProperty("weeksWithData").GetInt32().Should().Be(3);
            tue.GetProperty("lastWeek").GetDecimal().Should().Be(98, "đối soát: suất tính tiền");
            tue.GetProperty("listHistory").EnumerateArray().Select(h => h.GetProperty("actual").ValueKind == System.Text.Json.JsonValueKind.Null ? (decimal?)null : h.GetProperty("actual").GetDecimal())
                .Should().Equal(98m, 105m, null, 120m);
            preview.GetProperty("listCell").EnumerateArray().Single(x => x.GetProperty("date").GetDateTime() == new DateTime(2027, 5, 12))
                .GetProperty("result").GetString().Should().Be("INSUFFICIENT");
            (await _factory.QueryAsync(db => db.Forecast.CountAsync(f => f.CustomerId == s.A && f.Note != null && f.Note.StartsWith("[GOI_Y=")))).Should().Be(0);

            // Ghi: bản Nháp nhập tay 11/05 được giữ.
            var run = (await c.PostFormAsync("/api/Forecast/Suggest", ("CustomerId", s.A))).Ok();
            run.GetProperty("skippedEdited").GetInt32().Should().Be(1);
            run.GetProperty("created").GetInt32().Should().Be(1, "chỉ thứ Ba 18/05 (thứ Ba 11/05 có Nháp nhập tay; các thứ khác thiếu dữ liệu)");
            var d18 = await _factory.QueryAsync(db => db.Forecast.AsNoTracking().SingleAsync(f => f.CustomerId == s.A && f.ForecastDate == new DateTime(2027, 5, 18) && f.Status == "Draft"));
            (d18.ForecastQuantity, d18.DeliveryLocationId, d18.ShiftId, ForecastService.SuggestedOf(d18.Note)).Should().Be((108m, s.L1, (Guid?)null, 108m));
            d18.Note.Should().Contain("TB 4 tuần cùng thứ").And.Contain("– (13/04)");

            // Ghi đè khi người dùng đồng ý; lần sau cùng số → giữ nguyên.
            var over = (await c.PostFormAsync("/api/Forecast/Suggest", ("CustomerId", s.A), ("Overwrite", true))).Ok();
            over.GetProperty("overwritten").GetInt32().Should().Be(1);
            over.GetProperty("unchanged").GetInt32().Should().Be(1);
            var d11 = await _factory.QueryAsync(db => db.Forecast.AsNoTracking().Where(f => f.CustomerId == s.A && f.ForecastDate == new DateTime(2027, 5, 11)).OrderBy(f => f.VersionNo).ToListAsync());
            d11.Select(f => (f.Status, f.ForecastQuantity)).Should().Equal(("Superseded", 200m), ("Draft", 108m));

            // Người dùng sửa 108 → 115 → bản đã sửa, lần chạy sau bỏ qua.
            (await c.PutFormAsync("/api/Forecast/Update", ("Id", d11[1].Id), ("CustomerId", s.A), ("ForecastDate", new DateTime(2027, 5, 11)), ("ForecastQuantity", 115m), ("Note", d11[1].Note!))).Ok();
            (await c.PostFormAsync("/api/Forecast/Suggest", ("CustomerId", s.A))).Ok().GetProperty("skippedEdited").GetInt32().Should().Be(1);

            // Ma trận kèm gợi ý.
            var matrix = (await c.GetResultAsync($"/api/Forecast/GetMatrix?From=2027-05-10&To=2027-05-16&CustomerId={s.A}&WithSuggestion=true")).Ok();
            var row = matrix.GetProperty("rows").EnumerateArray().Single();
            var info = row.GetProperty("suggest").GetProperty("2027-05-11");
            (info.GetProperty("suggested").GetDecimal(), info.GetProperty("edited").GetBoolean(), info.GetProperty("insufficient").GetBoolean()).Should().Be((108m, true, false));
            row.GetProperty("suggest").GetProperty("2027-05-12").GetProperty("insufficient").GetBoolean().Should().BeTrue();
            var draft = row.GetProperty("drafts").GetProperty("2027-05-11").EnumerateArray().Single();
            (draft.GetProperty("forecastQuantity").GetDecimal(), draft.GetProperty("suggested").GetDecimal(), draft.GetProperty("edited").GetBoolean()).Should().Be((115m, 108m, true));
        }

        [Fact]
        public async Task Suggest_respects_contract_end_inactive_location_and_range()
        {
            var s = await SeedAsync("FT" + Guid.NewGuid().ToString("N")[..4]);
            using var c = _factory.CreateAuthorizedClient();
            var cells = (await c.PostFormAsync("/api/Forecast/Suggest", ("CustomerId", s.B), ("Preview", true))).Ok().GetProperty("listCell").EnumerateArray().ToList();
            cells.Select(x => x.GetProperty("date").GetDateTime()).Distinct().Should().Equal(new DateTime(2027, 5, 10), new DateTime(2027, 5, 11));
            cells.Should().OnlyContain(x => x.GetProperty("deliveryLocationId").GetGuid() == s.BL1, "điểm giao Ngừng không có khóa");
            cells.Single(x => x.GetProperty("date").GetDateTime() == new DateTime(2027, 5, 10)).GetProperty("suggested").GetDecimal().Should().Be(30);
            (await c.PostFormAsync("/api/Forecast/Suggest", ("From", new DateTime(2027, 5, 10)), ("To", new DateTime(2027, 5, 30)))).Fail("tối đa 14 ngày");
        }

        [Fact]
        public async Task Accuracy_compares_published_forecast_with_actual()
        {
            var s = await SeedAsync("FU" + Guid.NewGuid().ToString("N")[..4]);
            using var c = _factory.CreateAuthorizedClient();
            var acc = (await c.GetResultAsync($"/api/Forecast/GetAccuracy?From=2027-04-19&To=2027-05-02&CustomerId={s.A}")).Ok();
            // 20/04: 100 vs 105 · 27/04: 100 vs 98 · 28/04: không dự báo vs 50 → |5| + |2| + |50| = 57 / 253.
            (acc.GetProperty("forecastTotal").GetDecimal(), acc.GetProperty("actualTotal").GetDecimal()).Should().Be((200m, 253m));
            acc.GetProperty("errorPct").GetDecimal().Should().Be(22.5m);
            acc.GetProperty("biasPct").GetDecimal().Should().Be(-20.9m);
            acc.GetProperty("customerOverWarning").GetInt32().Should().Be(1);
            acc.GetProperty("listWeek").GetArrayLength().Should().Be(2);
        }
    }
}
