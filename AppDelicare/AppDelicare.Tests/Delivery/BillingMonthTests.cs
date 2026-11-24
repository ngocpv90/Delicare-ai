using System.IO.Compression;
using System.Net.Http.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using ClosedXML.Excel;
using FluentAssertions;

namespace AppDelicare.Tests.Delivery
{
    /// <summary>
    /// UI-6.9 (D-87): bảng kê tháng 6/2033 mọi khách – khách A sẵn sàng (đã chốt hết), khách B chờ đối soát (1 đơn Đã giao), khách C có hợp đồng nhưng không phát sinh (xếp cuối);
    /// ⚠ khi tiền suất lệch &gt; 20 % so tháng trước; xuất lô Excel (sheet Tổng hợp + mỗi khách 1 sheet), PDF (zip mỗi khách 1 tệp); lịch sử xuất.
    /// </summary>
    public class BillingMonthTests(InMemoryApiFactory factory) : IClassFixture<InMemoryApiFactory>
    {
        [Fact]
        public async Task Month_overview_statuses_warning_batch_exports_and_history()
        {
            string k = Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
            Guid a = Guid.NewGuid(), b = Guid.NewGuid(), cc = Guid.NewGuid();
            factory.Clock.FixedNow = new DateTime(2033, 7, 2, 9, 0, 0);
            await factory.SeedAsync(db =>
            {
                foreach (var (id, code) in new[] { (a, "A"), (b, "B"), (cc, "C") })
                {
                    db.Customer.Add(new Customer { Id = id, Code = "BM" + code + k, Name = "Khách " + code + k, DisplayName = "Khách " + code + k, CIF = "BM" + code + k, CustomerType = "BUSINESS", Status = "Active" });
                    db.Contract.Add(new Contract { Id = Guid.NewGuid(), CustomerId = id, ContractNumber = "HD-BM" + code + k, Status = "Active", EffectiveFrom = new DateTime(2033, 1, 1) });
                }
                void Order(Guid customer, DateTime day, string status, decimal? finalAmount, string recStatus)
                {
                    Guid id = Guid.NewGuid();
                    db.Order.Add(new Order { Id = id, OrderNumber = $"DH-BM{k}-{customer.ToString("N")[..3]}-{day:MMdd}", CustomerId = customer, OrderDate = day.AddDays(-1), MealDate = day, DeliveryDate = day, Status = status, TotalQuantity = 100, TotalAmount = 4_000_000 });
                    db.OrderLine.Add(new OrderLine { Id = Guid.NewGuid(), OrderId = id, MealType = "TRUA", Quantity = 100, UnitPrice = 40000, LineAmount = 4_000_000 });
                    if (finalAmount != null)
                    {
                        db.OrderReconciliation.Add(new OrderReconciliation { Id = Guid.NewGuid(), OrderId = id, Status = recStatus, ReconciliationDate = day, OrderedQuantity = 100, DeliveredQuantity = 100, AcceptedQuantity = 100,
                            FinalAmount = finalAmount, Note = "{\"podAccepted\":100,\"billingBasis\":\"RECEIVED\",\"billingQuantity\":100}" });
                    }
                }
                Order(a, new DateTime(2033, 6, 10), "Reconciled", 4_000_000, "Closed");
                Order(a, new DateTime(2033, 5, 10), "Reconciled", 2_000_000, "Closed");   // tháng trước 2 tr → tháng này 4 tr = +100 % ⚠
                Order(b, new DateTime(2033, 6, 11), "Reconciled", 4_000_000, "Closed");
                Order(b, new DateTime(2033, 6, 12), "Delivered", null, "");
                return Task.CompletedTask;
            });
            using var c = factory.CreateAuthorizedClient();
            var m = (await c.GetResultAsync("/api/BillingStatement/GetMonthOverview")).Ok();
            m.GetProperty("from").GetDateTime().Should().Be(new DateTime(2033, 6, 1), "mặc định tháng trước");
            var list = m.GetProperty("listCustomer").EnumerateArray().Where(x => x.GetProperty("customerCode").GetString()!.EndsWith(k)).ToList();
            list.Select(x => x.GetProperty("customerCode").GetString()).Should().Equal(["BMA" + k, "BMB" + k, "BMC" + k], "không phát sinh xếp cuối");
            var ra = list[0];
            ra.GetProperty("status").GetString().Should().Be("READY");
            ra.GetProperty("mealAmount").GetDecimal().Should().Be(4_000_000);
            ra.GetProperty("warn").GetBoolean().Should().BeTrue("+100 % so tháng trước > 20 %");
            list[1].GetProperty("status").GetString().Should().Be("PENDING");
            list[1].GetProperty("pendingOrders").GetInt32().Should().Be(1);
            list[2].GetProperty("status").GetString().Should().Be("NONE");
            m.GetProperty("listPendingGroup").EnumerateArray().Should().Contain(g => g.GetProperty("customerId").GetGuid() == b);

            var xlsx = await c.PostAsJsonAsync("/api/BillingStatement/ExportStatementBatch", new { month = "2033-06-15", listCustomerId = new[] { a, b } });
            xlsx.Content.Headers.ContentType!.MediaType.Should().Contain("spreadsheetml");
            using (var wb = new XLWorkbook(new MemoryStream(await xlsx.Content.ReadAsByteArrayAsync())))
            {
                wb.Worksheets.Count.Should().Be(3, "Tổng hợp + 2 khách");
            }
            var zip = await c.PostAsJsonAsync("/api/BillingStatement/PrintStatementBatch", new { month = "2033-06-15", listCustomerId = new[] { a, b } });
            zip.Content.Headers.ContentType!.MediaType.Should().Be("application/zip");
            using (var z = new ZipArchive(new MemoryStream(await zip.Content.ReadAsByteArrayAsync())))
            {
                z.Entries.Select(e => e.Name).Should().BeEquivalentTo([$"BangKe_BMA{k}_203306.pdf", $"BangKe_BMB{k}_203306.pdf"]);
            }
            (await LegacyResult.ReadAsync(await c.PostAsJsonAsync("/api/BillingStatement/ExportStatementBatch", new { listCustomerId = Array.Empty<Guid>() }))).Fail("Chưa chọn");

            var hist = await c.GetResultAsync("/api/BillingStatement/GetExportHistory?Month=2033-06-01");
            var items = hist.ListData.EnumerateArray().ToList();
            items.Count(x => x.GetProperty("customerName").GetString() == "Khách A" + k).Should().Be(2, "1 lần Excel + 1 lần PDF");
        }
    }
}
