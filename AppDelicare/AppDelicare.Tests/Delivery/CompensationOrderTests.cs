using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Delivery
{
    /// <summary>
    /// BIZ-3.1 (D-77 – A6): phản ánh thiếu suất của đơn đã giao (POD khách từ chối 10) → Tạo đơn bù: đơn mới cùng ngày ăn / điểm giao / loại suất, 10 suất, Đã chốt,
    /// giá 0, Note [DON_BU:&lt;đơn gốc&gt;|&lt;PA&gt;]; phản ánh → Đang xử lý; tạo lần 2 bị chặn; phản ánh loại Chất lượng không tạo được; "Khách trả tiền" → giá dòng gốc.
    /// </summary>
    public class CompensationOrderTests(InMemoryApiFactory factory) : IClassFixture<InMemoryApiFactory>
    {
        private async Task<(Guid Complaint, Guid Source, string SourceNo, Guid Location)> SeedAsync(string k, string category)
        {
            Guid customer = Guid.NewGuid(), location = Guid.NewGuid(), order = Guid.NewGuid(), delivery = Guid.NewGuid(), complaint = Guid.NewGuid();
            DateTime day = new(2032, 6, 8);
            await factory.SeedAsync(db =>
            {
                db.Customer.Add(new Customer { Id = customer, Code = "CB" + k, Name = "Khách " + k, DisplayName = "Khách " + k, CIF = "CB" + k, CustomerType = "FACTORY", Status = "Active" });
                db.CustomerDeliveryLocation.Add(new CustomerDeliveryLocation { Id = location, CustomerId = customer, Code = "L" + k, Name = "Điểm " + k });
                db.Order.Add(new Order { Id = order, OrderNumber = "DH-" + k, OrderNo = "DH-" + k, CustomerId = customer, DeliveryLocationId = location, OrderDate = day.AddDays(-1), MealDate = day, DeliveryDate = day, Status = "Delivered", TotalQuantity = 100, TotalAmount = 4_000_000 });
                db.OrderLine.AddRange(
                    new OrderLine { Id = Guid.NewGuid(), OrderId = order, MealType = "TRUA", MenuOptionNo = 1, Quantity = 60, UnitPrice = 40000, LineAmount = 2_400_000 },
                    new OrderLine { Id = Guid.NewGuid(), OrderId = order, MealType = "TRUA", MenuOptionNo = 2, Quantity = 40, UnitPrice = 40000, LineAmount = 1_600_000 });
                db.DeliveryOrder.Add(new DeliveryOrder { Id = delivery, OrderId = order, CustomerId = customer, DeliveryDate = day, PlannedDeliveryTime = day.AddHours(11), Status = "Delivered" });
                db.ProofOfDelivery.Add(new ProofOfDelivery { Id = Guid.NewGuid(), DeliveryOrderId = delivery, DeliveredQuantity = 100, AcceptedQuantity = 90, RejectedQuantity = 10, ConfirmedAt = day.AddHours(11) });
                db.CustomerComplaint.Add(new CustomerComplaint { Id = complaint, ComplaintNo = "PA-" + k, CustomerId = customer, OrderId = order, DeliveryOrderId = delivery, CategoryCode = category, Severity = "Medium",
                    ReceivedAt = day.AddHours(12), ComplaintStatus = "Open", Description = "Thiếu 10 suất" });
                return Task.CompletedTask;
            });
            return (complaint, order, "DH-" + k, location);
        }

        [Fact]
        public async Task Shortage_complaint_creates_one_free_locked_compensation_order()
        {
            factory.Clock.FixedNow = new DateTime(2032, 6, 8, 13, 0, 0);
            string k = Guid.NewGuid().ToString("N")[..5];
            var s = await SeedAsync(k, "SHORTAGE");
            using var c = factory.CreateAuthorizedClient();
            var created = await c.PostFormAsync("/api/CustomerComplaint/CreateCompensationOrder", ("Id", s.Complaint));
            created.Ok().GetProperty("complaint").GetProperty("complaintStatus").GetString().Should().Be("InProgress");
            created.Message.Should().Contain("Đã tạo đơn bù");
            var order = await factory.QueryAsync(db => db.Order.AsNoTracking().SingleAsync(o => o.Note!.Contains("[DON_BU:" + s.SourceNo + "|PA-" + k + "]")));
            (order.Status, order.TotalQuantity, order.TotalAmount, order.MealDate, order.DeliveryLocationId).Should().Be(("Locked", 10m, 0m, new DateTime(2032, 6, 8), (Guid?)s.Location));
            var lines = await factory.QueryAsync(db => db.OrderLine.AsNoTracking().Where(l => l.OrderId == order.Id).OrderBy(l => l.MenuOptionNo).ToListAsync());
            lines.Select(l => (l.MenuOptionNo, l.Quantity, l.UnitPrice)).Should().Equal((1, 6m, 0m), (2, 4m, 0m));
            (await c.PostFormAsync("/api/CustomerComplaint/CreateCompensationOrder", ("Id", s.Complaint))).Fail("đã có đơn bù");
        }

        [Fact]
        public async Task Quality_complaint_is_rejected_and_paid_compensation_uses_source_price()
        {
            factory.Clock.FixedNow = new DateTime(2032, 6, 8, 13, 0, 0);
            using var c = factory.CreateAuthorizedClient();
            var q = await SeedAsync(Guid.NewGuid().ToString("N")[..5], "QUALITY");
            (await c.PostFormAsync("/api/CustomerComplaint/CreateCompensationOrder", ("Id", q.Complaint))).Fail("Thiếu suất");
            string k = Guid.NewGuid().ToString("N")[..5];
            var s = await SeedAsync(k, "DAMAGED");
            (await c.PostFormAsync("/api/CustomerComplaint/CreateCompensationOrder", ("Id", s.Complaint), ("Quantity", 5m), ("ChargeCustomer", true))).Ok();
            var order = await factory.QueryAsync(db => db.Order.AsNoTracking().SingleAsync(o => o.Note!.Contains("|PA-" + k + "]")));
            (order.TotalQuantity, order.TotalAmount).Should().Be((5m, 200_000m));
        }
        /// <summary>D-78 (3): giá vốn đơn bù giá 0 gắn nhãn Chi phí lỗi (tách khỏi giá vốn suất bán).</summary>
        [Fact]
        public async Task Compensation_order_cost_is_reported_as_fault_cost()
        {
            string k = Guid.NewGuid().ToString("N")[..5];
            Guid customer = Guid.NewGuid(), bu = Guid.NewGuid(), normal = Guid.NewGuid(), dish = Guid.NewGuid();
            DateTime day = new(2032, 7, 2);
            factory.Clock.FixedNow = day.AddDays(1).AddHours(9);
            await factory.SeedAsync(db =>
            {
                db.Customer.Add(new Customer { Id = customer, Code = "CF" + k, Name = "Khách " + k, DisplayName = "Khách " + k, CIF = "CF" + k, CustomerType = "FACTORY", Status = "Active" });
                db.Dish.Add(new Dish { Id = dish, Code = "MF" + k, Name = "Món " + k, Status = "Active" });
                db.Order.AddRange(
                    new Order { Id = bu, OrderNumber = "DH-BU-" + k, CustomerId = customer, OrderDate = day, MealDate = day, DeliveryDate = day, Status = "Delivered", TotalQuantity = 5, TotalAmount = 0, Note = "[DON_BU:DH-X|PA-" + k + "] Đơn bù" },
                    new Order { Id = normal, OrderNumber = "DH-N-" + k, CustomerId = customer, OrderDate = day, MealDate = day, DeliveryDate = day, Status = "Delivered", TotalQuantity = 50, TotalAmount = 2_000_000 });
                foreach (var o in new[] { bu, normal })
                {
                    db.MealCost.Add(new MealCost { Id = Guid.NewGuid(), OrderId = o, DishId = dish, MealQty = 5, TotalCost = 100000, TotalActualCost = 100000, AcceptedMealQuantity = 5, CalculatedAt = day.AddHours(20) });
                }
                return Task.CompletedTask;
            });
            using var c = factory.CreateAuthorizedClient();
            var list = (await c.GetResultAsync("/api/MealCost/GetList?From=2032-07-02&To=2032-07-02")).ListData.EnumerateArray().ToList();
            list.Single(x => x.GetProperty("orderNumber").GetString() == "DH-BU-" + k).GetProperty("isFaultCost").GetBoolean().Should().BeTrue();
            list.Single(x => x.GetProperty("orderNumber").GetString() == "DH-N-" + k).GetProperty("costCategoryName").GetString().Should().Be("Suất bán");
        }
    }
}
