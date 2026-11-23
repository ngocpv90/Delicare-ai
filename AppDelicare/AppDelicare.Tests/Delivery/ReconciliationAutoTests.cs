using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Delivery
{
    /// <summary>
    /// BIZ-2.9 (D-77 – C8): đối soát Khớp, không phản ánh mở, quá 24 giờ → tự chốt khi mở màn đối soát (cùng hàm với job 20:00), đơn → Đã đối soát;
    /// còn phản ánh mở / chưa đủ 24 giờ / Chênh lệch → giữ nguyên. Tự tạo đối soát khi giao xong chạy trong DeliveryTests (lần ghi tay thay bản tự tạo, ghi chú tự sinh không tính là ghi chú người).
    /// </summary>
    public class ReconciliationAutoTests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public ReconciliationAutoTests(InMemoryApiFactory factory)
        {
            _factory = factory;
            _factory.Clock.FixedNow = new DateTime(2031, 5, 10, 9, 0, 0);
        }

        [Fact]
        public async Task Matched_reconciliation_without_open_complaint_closes_after_24_hours()
        {
            string key = Guid.NewGuid().ToString("N")[..5];
            Guid customer = Guid.NewGuid();
            var ids = new Dictionary<string, (Guid Order, Guid Rec)>();
            await _factory.SeedAsync(db =>
            {
                db.Customer.Add(new Customer { Id = customer, Code = "RA" + key, Name = "Khách " + key, CIF = "RA" + key, DisplayName = "Khách " + key, CustomerType = "FACTORY", Status = "Active" });
                void Add(string name, string status, DateTime created, bool complaint)
                {
                    Guid order = Guid.NewGuid(), rec = Guid.NewGuid();
                    db.Order.Add(new Order { Id = order, OrderNumber = $"DH-{key}-{name}", OrderNo = $"DH-{key}-{name}", CustomerId = customer, OrderDate = created.AddDays(-2), MealDate = created.Date, DeliveryDate = created.Date, Status = "Delivered", TotalQuantity = 50 });
                    db.OrderReconciliation.Add(new OrderReconciliation { Id = rec, OrderId = order, Status = status, ReconciliationDate = created.Date, OrderedQuantity = 50, DeliveredQuantity = 50, AcceptedQuantity = 50, VariancePercent = 0, CreatedDate = created });
                    if (complaint)
                    {
                        db.CustomerComplaint.Add(new CustomerComplaint { Id = Guid.NewGuid(), ComplaintNo = $"PA-{key}-{name}", CustomerId = customer, OrderId = order, ReceivedAt = created, ComplaintStatus = "Open", Description = "[DEMO]" });
                    }
                    ids[name] = (order, rec);
                }
                Add("OLD", "Matched", new DateTime(2031, 5, 8, 12, 0, 0), complaint: false);
                Add("NEW", "Matched", new DateTime(2031, 5, 9, 20, 0, 0), complaint: false);
                Add("PA", "Matched", new DateTime(2031, 5, 8, 12, 0, 0), complaint: true);
                Add("VAR", "Variance", new DateTime(2031, 5, 8, 12, 0, 0), complaint: false);
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();
            (await c.GetResultAsync($"/api/OrderReconciliation/GetList?From=2031-05-01&To=2031-05-10&CustomerId={customer}")).Ok();

            async Task<(string? Rec, string? Order, string? Remark)> State(string name) => await _factory.QueryAsync(async db =>
            {
                var r = await db.OrderReconciliation.AsNoTracking().SingleAsync(x => x.Id == ids[name].Rec);
                var o = await db.Order.AsNoTracking().SingleAsync(x => x.Id == ids[name].Order);
                return (r.Status, o.Status, r.Remark);
            });
            var old = await State("OLD");
            (old.Rec, old.Order).Should().Be(("Closed", "Reconciled"));
            old.Remark.Should().Contain("Hệ thống tự chốt");
            (await State("NEW")).Rec.Should().Be("Matched", "chưa đủ 24 giờ");
            (await State("PA")).Rec.Should().Be("Matched", "còn phản ánh mở");
            (await State("VAR")).Rec.Should().Be("Variance", "chênh lệch – người chốt");
        }
    }
}
