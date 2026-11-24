using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.Json;

namespace AppDelicare.Tests.DetailPages
{
    /// <summary>
    /// UI-6 (6.2 nhóm A – D-80): trang chi tiết toàn màn nhóm Kinh doanh – Order / Contract / CustomerComplaint / CustomerDeliveryLocation …/GetOverview:
    /// có dữ liệu, id sai (Fail "Không tìm thấy"), dữ liệu con rỗng vẫn mở được, quyền theo màn gốc (xem màn gốc → 200, màn khác → 403).
    /// </summary>
    public class SalesOverviewTests(InMemoryApiFactory factory) : IClassFixture<InMemoryApiFactory>
    {
        private async Task<Guid> UserAsync(params string[] viewLinks)
        {
            Guid userId = Guid.NewGuid(), group = Guid.NewGuid();
            await factory.SeedAsync(async db =>
            {
                db.User.Add(new User { Id = userId, UserName = "dps-" + userId.ToString("N")[..6], IsActive = true });
                db.UserGroup.Add(new UserGroup { Id = group, Name = "DPS-" + group.ToString("N")[..6] });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = userId, UserGroupId = group });
                foreach (string link in viewLinks)
                {
                    var menu = await db.ObjectMenu.FirstOrDefaultAsync(m => m.MetaLink == link) ?? db.ObjectMenu.Local.FirstOrDefault(m => m.MetaLink == link);
                    if (menu == null)
                    {
                        menu = new ObjectMenu { Id = Guid.NewGuid(), Name = link, MetaLink = link, IsActive = true, LevelShow = 2 };
                        db.ObjectMenu.Add(menu);
                    }
                    db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = group, ObjectMenuId = menu.Id, AllowAccess = true });
                }
            });
            return userId;
        }

        /// <summary>Khách có NV phụ trách (User → Person) + 1 điểm giao + 1 hợp đồng Hiệu lực có 2 loại suất.</summary>
        private async Task<(Guid Customer, Guid Location, Guid Contract)> CustomerAsync(string key)
        {
            Guid customer = Guid.NewGuid(), loc = Guid.NewGuid(), ct = Guid.NewGuid(), user = Guid.NewGuid(), person = Guid.NewGuid();
            var today = factory.Clock.Today;
            await factory.SeedAsync(db =>
            {
                db.Person.Add(new Person { Id = person, FullName = "NV KD " + key });
                db.User.Add(new User { Id = user, UserName = "kd-" + key.ToLowerInvariant(), PersonId = person, IsActive = true });
                db.Customer.Add(new Customer { Id = customer, Code = "KH-" + key, Name = "Khách " + key, CIF = "KH-" + key, DisplayName = "Khách " + key, CustomerType = "FACTORY", Status = "Active", UserAssignId = user });
                db.CustomerDeliveryLocation.Add(new CustomerDeliveryLocation { Id = loc, CustomerId = customer, Code = "DG-" + key, Name = "Điểm giao " + key, Address = "KCN " + key, Status = "Active" });
                db.Contract.Add(new Contract { Id = ct, CustomerId = customer, ContractNumber = "HD-" + key, ContractNo = "HD-" + key, Name = "Hợp đồng " + key, Status = "Active", EffectiveFrom = today.AddMonths(-3), EffectiveTo = today.AddDays(45), ContractValue = 900_000_000m });
                db.ContractMeal.AddRange(
                    new ContractMeal { Id = Guid.NewGuid(), ContractId = ct, MealType = "TRUA", MealName = "Suất trưa", UnitPrice = 30000m, Status = "Active" },
                    new ContractMeal { Id = Guid.NewGuid(), ContractId = ct, MealType = "TOI", MealName = "Suất tối", UnitPrice = 32000m, Status = "Active" });
                return Task.CompletedTask;
            });
            return (customer, loc, ct);
        }

        private static void Forbidden(HttpResponseMessage r) => r.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        [Fact]
        public async Task Order_overview_returns_lines_deliveries_reconciliation_complaints_and_checks_permission()
        {
            var (cus, loc, ct) = await CustomerAsync("DPA1");
            var day = factory.Clock.Today.AddDays(-2);
            Guid order = Guid.NewGuid(), empty = Guid.NewGuid(), dorder = Guid.NewGuid(), approval = Guid.NewGuid();
            await factory.SeedAsync(db =>
            {
                db.Order.AddRange(
                    new Order { Id = order, OrderNumber = "DH-DPA1", OrderNo = "DH-DPA1", CustomerId = cus, ContractId = ct, DeliveryLocationId = loc, OrderDate = day.AddDays(-2), MealDate = day, DeliveryDate = day, Status = "Delivered", TotalQuantity = 150, TotalAmount = 4_500_000m },
                    new Order { Id = empty, OrderNumber = "DH-DPA1-RONG", OrderNo = "DH-DPA1-RONG", CustomerId = cus, OrderDate = day, MealDate = day.AddDays(5), DeliveryDate = day.AddDays(5), Status = "Draft" });
                db.OrderLine.AddRange(
                    new OrderLine { Id = Guid.NewGuid(), OrderId = order, MealType = "TRUA", Quantity = 100, UnitPrice = 30000m, LineAmount = 3_000_000m },
                    new OrderLine { Id = Guid.NewGuid(), OrderId = order, MealType = "TOI", Quantity = 50, UnitPrice = 30000m, LineAmount = 1_500_000m });
                db.DeliveryOrder.Add(new DeliveryOrder { Id = dorder, DeliveryNumber = "PG-DPA1", DeliveryNo = "PG-DPA1", OrderId = order, CustomerId = cus, CustomerDeliveryLocationId = loc, DeliveryDate = day, PlannedDeliveryTime = day.AddHours(11), ActualDeliveryTime = day.AddHours(11), Status = "Delivered" });
                db.ProofOfDelivery.Add(new ProofOfDelivery { Id = Guid.NewGuid(), DeliveryOrderId = dorder, DeliveredQuantity = 150, AcceptedQuantity = 148, RejectedQuantity = 2, ReceiverName = "Bếp KH", ConfirmedAt = day.AddHours(11) });
                db.OrderReconciliation.Add(new OrderReconciliation { Id = Guid.NewGuid(), OrderId = order, OrderedQuantity = 150, DeliveredQuantity = 150, AcceptedQuantity = 148, VariancePercent = -1.33m, FinalAmount = 4_440_000m, Status = "Variance", ReconciliationDate = day });
                db.CustomerComplaint.Add(new CustomerComplaint { Id = Guid.NewGuid(), ComplaintNo = "PA-DPA1", CustomerId = cus, OrderId = order, CategoryCode = "SHORTAGE", Severity = "High", Description = "Thiếu 2 suất", ReceivedAt = day.AddHours(12), ComplaintStatus = "Open" });
                db.ApprovalRequest.Add(new ApprovalRequest { Id = approval, RequestNo = "PD-DPA1", BusinessType = "ORDER_DEVIATION", BusinessId = order, RequestedAt = day.AddDays(-1), RequestStatus = "Approved" });
                return Task.CompletedTask;
            });
            using var admin = factory.CreateAuthorizedClient();
            var o = (await admin.GetResultAsync($"/api/Order/GetOverview?id={order}")).Ok();
            o.GetProperty("detail").GetProperty("order").GetProperty("orderNumber").GetString().Should().Be("DH-DPA1");
            o.GetProperty("detail").GetProperty("contractNumber").GetString().Should().Be("HD-DPA1");
            o.GetProperty("lineCount").GetInt32().Should().Be(2);
            o.GetProperty("deliveryCount").GetInt32().Should().Be(1);
            o.GetProperty("acceptedQuantity").GetDecimal().Should().Be(148);
            o.GetProperty("listDelivery")[0].GetProperty("locationName").GetString().Should().Be("Điểm giao DPA1");
            o.GetProperty("reconciliation").GetProperty("finalAmount").GetDecimal().Should().Be(4_440_000m);
            o.GetProperty("complaintCount").GetInt32().Should().Be(1);
            o.GetProperty("openComplaintCount").GetInt32().Should().Be(1);
            o.GetProperty("responsibleName").GetString().Should().Be("NV KD DPA1");
            o.GetProperty("approvalRequestId").GetGuid().Should().Be(approval);

            var e = (await admin.GetResultAsync($"/api/Order/GetOverview?id={empty}")).Ok();
            e.GetProperty("lineCount").GetInt32().Should().Be(0, "đơn chưa có dòng vẫn mở được trang");
            e.GetProperty("listDelivery").GetArrayLength().Should().Be(0);
            e.GetProperty("reconciliation").ValueKind.Should().Be(JsonValueKind.Null);
            e.GetProperty("approvalRequestId").ValueKind.Should().Be(JsonValueKind.Null);
            (await admin.GetResultAsync($"/api/Order/GetOverview?id={Guid.NewGuid()}")).Fail("Không tìm thấy");

            using var viewer = factory.CreateAuthorizedClient(await UserAsync("#/crm-order"));
            (await viewer.GetAsync($"/api/Order/GetOverview?id={order}")).StatusCode.Should().Be(HttpStatusCode.OK);
            using var other = factory.CreateAuthorizedClient(await UserAsync("#/crm-contract"));
            Forbidden(await other.GetAsync($"/api/Order/GetOverview?id={order}"));
        }

        [Fact]
        public async Task Contract_overview_returns_meals_orders_amounts_and_checks_permission()
        {
            var (cus, loc, ct) = await CustomerAsync("DPA2");
            var today = factory.Clock.Today;
            Guid o1 = Guid.NewGuid(), o2 = Guid.NewGuid(), draft = Guid.NewGuid(), approval = Guid.NewGuid();
            await factory.SeedAsync(db =>
            {
                db.Order.AddRange(
                    new Order { Id = o1, OrderNumber = "DH-DPA2-1", OrderNo = "DH-DPA2-1", CustomerId = cus, ContractId = ct, DeliveryLocationId = loc, OrderDate = today.AddDays(-5), MealDate = today.AddDays(-3), DeliveryDate = today.AddDays(-3), Status = "Reconciled", TotalQuantity = 120 },
                    new Order { Id = o2, OrderNumber = "DH-DPA2-2", OrderNo = "DH-DPA2-2", CustomerId = cus, ContractId = ct, DeliveryLocationId = loc, OrderDate = today.AddDays(-4), MealDate = today.AddDays(-2), DeliveryDate = today.AddDays(-2), Status = "Cancelled", TotalQuantity = 80 });
                db.OrderReconciliation.Add(new OrderReconciliation { Id = Guid.NewGuid(), OrderId = o1, OrderedQuantity = 120, DeliveredQuantity = 120, AcceptedQuantity = 120, FinalAmount = 3_600_000m, Status = "Closed", ReconciliationDate = today.AddDays(-2) });
                db.Contract.Add(new Contract { Id = draft, CustomerId = cus, ContractNumber = "HD-DPA2-NHAP", ContractNo = "HD-DPA2-NHAP", Name = "Nháp", Status = "Draft", EffectiveFrom = today });
                db.ApprovalRequest.Add(new ApprovalRequest { Id = approval, RequestNo = "PD-DPA2", BusinessType = "CONTRACT", BusinessId = ct, RequestedAt = today.AddMonths(-3), RequestStatus = "Approved" });
                return Task.CompletedTask;
            });
            using var admin = factory.CreateAuthorizedClient();
            var c = (await admin.GetResultAsync($"/api/Contract/GetOverview?id={ct}")).Ok();
            c.GetProperty("detail").GetProperty("contract").GetProperty("contractNumber").GetString().Should().Be("HD-DPA2");
            c.GetProperty("mealCount").GetInt32().Should().Be(2);
            c.GetProperty("orderCount").GetInt32().Should().Be(2);
            c.GetProperty("orderedQuantity30d").GetDecimal().Should().Be(120, "đơn Hủy không tính");
            c.GetProperty("reconciledAmount").GetDecimal().Should().Be(3_600_000m);
            c.GetProperty("daysToEnd").GetInt32().Should().Be(45);
            c.GetProperty("locationCount").GetInt32().Should().Be(1);
            c.GetProperty("listRecentOrder").GetArrayLength().Should().Be(2);
            c.GetProperty("customerCode").GetString().Should().Be("KH-DPA2");
            c.GetProperty("responsibleName").GetString().Should().Be("NV KD DPA2");
            c.GetProperty("approvalRequestId").GetGuid().Should().Be(approval);

            var e = (await admin.GetResultAsync($"/api/Contract/GetOverview?id={draft}")).Ok();
            e.GetProperty("mealCount").GetInt32().Should().Be(0, "hợp đồng Nháp chưa có loại suất vẫn mở được trang");
            e.GetProperty("orderCount").GetInt32().Should().Be(0);
            e.GetProperty("daysToEnd").ValueKind.Should().Be(JsonValueKind.Null, "không thời hạn");
            (await admin.GetResultAsync($"/api/Contract/GetOverview?id={Guid.NewGuid()}")).Fail("Không tìm thấy");

            using var viewer = factory.CreateAuthorizedClient(await UserAsync("#/crm-contract"));
            (await viewer.GetAsync($"/api/Contract/GetOverview?id={ct}")).StatusCode.Should().Be(HttpStatusCode.OK);
            using var other = factory.CreateAuthorizedClient(await UserAsync("#/crm-order"));
            Forbidden(await other.GetAsync($"/api/Contract/GetOverview?id={ct}"));
        }

        [Fact]
        public async Task Complaint_overview_returns_sla_history_compensation_and_checks_permission()
        {
            var (cus, loc, _) = await CustomerAsync("DPA3");
            var now = factory.Clock.Now;
            Guid order = Guid.NewGuid(), cp = Guid.NewGuid(), bare = Guid.NewGuid(), actor = Guid.NewGuid();
            await factory.SeedAsync(db =>
            {
                db.User.Add(new User { Id = actor, UserName = "cskh-dpa3", IsActive = true });
                db.Order.AddRange(
                    new Order { Id = order, OrderNumber = "DH-DPA3", OrderNo = "DH-DPA3", CustomerId = cus, DeliveryLocationId = loc, OrderDate = now.Date.AddDays(-1), MealDate = now.Date, DeliveryDate = now.Date, Status = "Delivered", TotalQuantity = 100 },
                    new Order { Id = Guid.NewGuid(), OrderNumber = "DH-DPA3-BU", OrderNo = "DH-DPA3-BU", CustomerId = cus, DeliveryLocationId = loc, OrderDate = now.Date, MealDate = now.Date, DeliveryDate = now.Date, Status = "Locked", TotalQuantity = 5, TotalAmount = 0, Note = "[DON_BU:DH-DPA3|PA-DPA3]" });
                db.CustomerComplaint.AddRange(
                    new CustomerComplaint { Id = cp, ComplaintNo = "PA-DPA3", CustomerId = cus, OrderId = order, CategoryCode = "SHORTAGE", Severity = "High", Description = "Thiếu 5 suất", ReceivedAt = now.AddHours(-2), SlaDueAt = now.AddHours(2), ComplaintStatus = "InProgress", AssignedTo = actor },
                    new CustomerComplaint { Id = bare, ComplaintNo = "PA-DPA3-2", CustomerId = cus, CategoryCode = "OTHER", Severity = "Low", Description = "Góp ý", ReceivedAt = now.AddHours(-1), ComplaintStatus = "Open" });
                db.ComplaintHistory.AddRange(
                    new ComplaintHistory { Id = Guid.NewGuid(), ComplaintId = cp, EventType = "CREATED", NewStatus = "Open", EventAt = now.AddHours(-2), ActorId = actor },
                    new ComplaintHistory { Id = Guid.NewGuid(), ComplaintId = cp, EventType = "STATUS_CHANGED", OldStatus = "Open", NewStatus = "InProgress", Comment = "Nhận xử lý", EventAt = now.AddHours(-1), ActorId = actor });
                return Task.CompletedTask;
            });
            using var admin = factory.CreateAuthorizedClient();
            var c = (await admin.GetResultAsync($"/api/CustomerComplaint/GetOverview?id={cp}")).Ok();
            c.GetProperty("detail").GetProperty("complaint").GetProperty("complaintNo").GetString().Should().Be("PA-DPA3");
            c.GetProperty("detail").GetProperty("orderNumber").GetString().Should().Be("DH-DPA3");
            c.GetProperty("hoursToDue").GetDecimal().Should().BeApproximately(2m, 0.2m);
            c.GetProperty("historyCount").GetInt32().Should().Be(2);
            c.GetProperty("listHistory")[1].GetProperty("actorName").GetString().Should().Be("cskh-dpa3");
            c.GetProperty("listHistory")[1].GetProperty("newStatusName").GetString().Should().Be("Đang xử lý");
            c.GetProperty("customerOpenComplaintCount").GetInt32().Should().Be(1);
            c.GetProperty("listCompensationOrder").GetArrayLength().Should().Be(1);
            c.GetProperty("canCompensate").GetBoolean().Should().BeFalse("đã có đơn bù");
            c.GetProperty("responsibleName").GetString().Should().Be("NV KD DPA3");

            var e = (await admin.GetResultAsync($"/api/CustomerComplaint/GetOverview?id={bare}")).Ok();
            e.GetProperty("historyCount").GetInt32().Should().Be(0, "phản ánh chưa có diễn biến vẫn mở được trang");
            e.GetProperty("listCompensationOrder").GetArrayLength().Should().Be(0);
            e.GetProperty("hoursToDue").ValueKind.Should().Be(JsonValueKind.Null, "không có hạn SLA");
            (await admin.GetResultAsync($"/api/CustomerComplaint/GetOverview?id={Guid.NewGuid()}")).Fail("Không tìm thấy");

            using var viewer = factory.CreateAuthorizedClient(await UserAsync("#/log-complaint"));
            (await viewer.GetAsync($"/api/CustomerComplaint/GetOverview?id={cp}")).StatusCode.Should().Be(HttpStatusCode.OK);
            using var other = factory.CreateAuthorizedClient(await UserAsync("#/crm-order"));
            Forbidden(await other.GetAsync($"/api/CustomerComplaint/GetOverview?id={cp}"));
        }

        [Fact]
        public async Task Location_overview_returns_30_day_stats_on_time_rate_and_checks_permission()
        {
            var (cus, loc, ct) = await CustomerAsync("DPA4");
            var today = factory.Clock.Today;
            Guid o1 = Guid.NewGuid(), o2 = Guid.NewGuid(), empty = Guid.NewGuid();
            await factory.SeedAsync(db =>
            {
                db.CustomerDeliveryLocation.Add(new CustomerDeliveryLocation { Id = empty, CustomerId = cus, Code = "DG-DPA4-RONG", Name = "Điểm giao mới", Status = "Active" });
                db.Order.AddRange(
                    new Order { Id = o1, OrderNumber = "DH-DPA4-1", OrderNo = "DH-DPA4-1", CustomerId = cus, ContractId = ct, DeliveryLocationId = loc, OrderDate = today.AddDays(-4), MealDate = today.AddDays(-3), DeliveryDate = today.AddDays(-3), Status = "Delivered", TotalQuantity = 100 },
                    new Order { Id = o2, OrderNumber = "DH-DPA4-2", OrderNo = "DH-DPA4-2", CustomerId = cus, ContractId = ct, DeliveryLocationId = loc, OrderDate = today.AddDays(-3), MealDate = today.AddDays(-2), DeliveryDate = today.AddDays(-2), Status = "Delivered", TotalQuantity = 140 });
                db.DeliveryOrder.AddRange(
                    new DeliveryOrder { Id = Guid.NewGuid(), DeliveryNumber = "PG-DPA4-1", DeliveryNo = "PG-DPA4-1", OrderId = o1, CustomerId = cus, CustomerDeliveryLocationId = loc, DeliveryDate = today.AddDays(-3), PlannedDeliveryTime = today.AddDays(-3).AddHours(11), ActualDeliveryTime = today.AddDays(-3).AddHours(11).AddMinutes(10), Status = "Delivered" },
                    new DeliveryOrder { Id = Guid.NewGuid(), DeliveryNumber = "PG-DPA4-2", DeliveryNo = "PG-DPA4-2", OrderId = o2, CustomerId = cus, CustomerDeliveryLocationId = loc, DeliveryDate = today.AddDays(-2), PlannedDeliveryTime = today.AddDays(-2).AddHours(11), ActualDeliveryTime = today.AddDays(-2).AddHours(12), Status = "Delivered" });
                db.CustomerComplaint.Add(new CustomerComplaint { Id = Guid.NewGuid(), ComplaintNo = "PA-DPA4", CustomerId = cus, OrderId = o2, CategoryCode = "LATE", Severity = "Medium", Description = "Giao trễ 1 giờ", ReceivedAt = today.AddDays(-2).AddHours(13), ComplaintStatus = "Resolved" });
                return Task.CompletedTask;
            });
            using var admin = factory.CreateAuthorizedClient();
            var l = (await admin.GetResultAsync($"/api/CustomerDeliveryLocation/GetOverview?id={loc}")).Ok();
            l.GetProperty("location").GetProperty("code").GetString().Should().Be("DG-DPA4");
            l.GetProperty("customerName").GetString().Should().Be("Khách DPA4");
            l.GetProperty("orderCount30d").GetInt32().Should().Be(2);
            l.GetProperty("mealQuantity30d").GetDecimal().Should().Be(240);
            l.GetProperty("avgMealsPerDay30d").GetDecimal().Should().Be(120);
            l.GetProperty("deliveryCount30d").GetInt32().Should().Be(2);
            l.GetProperty("onTimePct30d").GetDecimal().Should().Be(50, "trễ 10 phút trong ngưỡng 15 phút, trễ 1 giờ là không đúng giờ");
            l.GetProperty("complaintCount").GetInt32().Should().Be(1);
            l.GetProperty("openComplaintCount").GetInt32().Should().Be(0);
            l.GetProperty("listRecentDelivery").GetArrayLength().Should().Be(2);
            l.GetProperty("responsibleName").GetString().Should().Be("NV KD DPA4");

            var e = (await admin.GetResultAsync($"/api/CustomerDeliveryLocation/GetOverview?id={empty}")).Ok();
            e.GetProperty("orderCount30d").GetInt32().Should().Be(0, "điểm giao chưa có đơn vẫn mở được trang");
            e.GetProperty("onTimePct30d").ValueKind.Should().Be(JsonValueKind.Null);
            e.GetProperty("listRecentOrder").GetArrayLength().Should().Be(0);
            (await admin.GetResultAsync($"/api/CustomerDeliveryLocation/GetOverview?id={Guid.NewGuid()}")).Fail("Không tìm thấy");

            using var viewer = factory.CreateAuthorizedClient(await UserAsync("#/crm-customer")); // UI-6.16 (D-97): quyền điểm giao theo màn Khách hàng
            (await viewer.GetAsync($"/api/CustomerDeliveryLocation/GetOverview?id={loc}")).StatusCode.Should().Be(HttpStatusCode.OK);
            using var other = factory.CreateAuthorizedClient(await UserAsync("#/crm-order")); // không có quyền Khách hàng → 403
            Forbidden(await other.GetAsync($"/api/CustomerDeliveryLocation/GetOverview?id={loc}"));
        }
    }
}
