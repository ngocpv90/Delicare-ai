using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;

namespace AppDelicare.Tests.Delivery
{
    /// <summary>
    /// BIZ-3.2 (D-77 – A7): bảng kê tháng 4/2032 của 1 khách – đơn 05/04 đối soát Đã chốt (tính tiền 98 suất, lỗi Delicare 2, tiền chốt 3.920.000 – TRUA 40.000);
    /// đơn 06/04 Đã giao chưa đối soát → "chờ đối soát"; đơn bù giá 0 không tính. VAT 8 % → 4.233.600. Xuất Excel / in PDF trả tệp.
    /// </summary>
    public class BillingStatementTests(InMemoryApiFactory factory) : IClassFixture<InMemoryApiFactory>
    {
        [Fact]
        public async Task Statement_bills_closed_reconciliations_lists_pending_and_exports()
        {
            string k = Guid.NewGuid().ToString("N")[..5];
            Guid customer = Guid.NewGuid(), contract = Guid.NewGuid(), meal = Guid.NewGuid(), o1 = Guid.NewGuid(), o2 = Guid.NewGuid(), o3 = Guid.NewGuid();
            factory.Clock.FixedNow = new DateTime(2032, 5, 3, 9, 0, 0);
            await factory.SeedAsync(db =>
            {
                db.Customer.Add(new Customer { Id = customer, Code = "BK" + k, Name = "Khách " + k, LegalName = "Công ty " + k, DisplayName = "Khách " + k, CIF = "BK" + k, CustomerType = "BUSINESS", TaxCode = "0101", Status = "Active" });
                db.Contract.Add(new Contract { Id = contract, CustomerId = customer, ContractNumber = "HD-" + k, Status = "Active", EffectiveFrom = new DateTime(2032, 1, 1) });
                db.ContractMeal.Add(new ContractMeal { Id = meal, ContractId = contract, MealType = "TRUA", MealName = "Suất trưa", UnitPrice = 40000 });
                void Order(Guid id, DateTime day, string status, decimal amount, string? note = null)
                {
                    db.Order.Add(new Order { Id = id, OrderNumber = $"DH-{k}-{day:dd}", OrderNo = $"DH-{k}-{day:dd}", CustomerId = customer, ContractId = contract, OrderDate = day.AddDays(-1), MealDate = day, DeliveryDate = day, Status = status, TotalQuantity = 100, TotalAmount = amount, Note = note });
                    db.OrderLine.Add(new OrderLine { Id = Guid.NewGuid(), OrderId = id, ContractMealId = meal, MealType = "TRUA", Quantity = 100, UnitPrice = amount == 0 ? 0 : 40000, LineAmount = amount });
                }
                Order(o1, new DateTime(2032, 4, 5), "Reconciled", 4_000_000);
                db.OrderReconciliation.Add(new OrderReconciliation { Id = Guid.NewGuid(), OrderId = o1, Status = "Closed", ReconciliationDate = new DateTime(2032, 4, 5), OrderedQuantity = 100, DeliveredQuantity = 100, AcceptedQuantity = 95,
                    FinalAmount = 3_920_000, Note = "{\"podAccepted\":100,\"fault\":2,\"billingBasis\":\"RECEIVED\",\"billingQuantity\":98}" });
                Order(o2, new DateTime(2032, 4, 6), "Delivered", 4_000_000);
                Order(o3, new DateTime(2032, 4, 7), "Reconciled", 0, "[DON_BU:DH-X|PA-1]");
                return Task.CompletedTask;
            });
            using var c = factory.CreateAuthorizedClient();
            var d = (await c.GetResultAsync($"/api/BillingStatement/GetStatement?CustomerId={customer}")).Ok();
            d.GetProperty("from").GetDateTime().Should().Be(new DateTime(2032, 4, 1), "mặc định tháng trước");
            var row = d.GetProperty("listRow").EnumerateArray().Single();
            (row.GetProperty("billedQuantity").GetDecimal(), row.GetProperty("faultQuantity").GetDecimal(), row.GetProperty("unitPrice").GetDecimal(), row.GetProperty("amount").GetDecimal())
                .Should().Be((98m, 2m, 40000m, 3_920_000m));
            d.GetProperty("listPending").EnumerateArray().Single().GetProperty("orderNumber").GetString().Should().Be($"DH-{k}-06");
            d.GetProperty("compensationOrderCount").GetInt32().Should().Be(1);
            (d.GetProperty("subTotal").GetDecimal(), d.GetProperty("vatAmount").GetDecimal(), d.GetProperty("grandTotal").GetDecimal()).Should().Be((3_920_000m, 313_600m, 4_233_600m));

            var xlsx = await c.GetAsync($"/api/BillingStatement/ExportStatement?CustomerId={customer}");
            xlsx.Content.Headers.ContentType!.MediaType.Should().Contain("spreadsheetml");
            (await xlsx.Content.ReadAsByteArrayAsync()).Length.Should().BeGreaterThan(1000);
            var pdf = await c.GetAsync($"/api/BillingStatement/PrintStatement?CustomerId={customer}");
            pdf.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
            (await c.GetResultAsync($"/api/BillingStatement/GetStatement?CustomerId={customer}&From=2032-01-01&To=2032-05-01")).Fail("tối đa 3 tháng");
        }
    }
}
