using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Sales
{
    /// <summary>
    /// UI-2R (D-69): trường bổ sung của GetOverview cho khối thông tin rút gọn – khách hàng (ngày sinh / CCCD chỉ người có quyền Sửa crm-customer,
    /// lọc ở máy chủ; tạo / cập nhật bởi; điều khoản thanh toán; suất / ngày theo điểm giao), nhà cung cấp (phiếu nhập gần nhất, bắt đầu giao dịch,
    /// giá thấp nhất trong N NCC), nhân viên (ca thường làm, đơn gần đây).
    /// </summary>
    public class InfoPanelOverviewTests(InMemoryApiFactory factory) : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory = factory;
        private static DateTime Today => DateTime.UtcNow.AddHours(7).Date;

        [Fact]
        public async Task Customer_personal_fields_only_for_edit_right_and_info_panel_extras()
        {
            Guid cus = Guid.NewGuid(), loc = Guid.NewGuid(), contract = Guid.NewGuid(), creator = Guid.NewGuid(), person = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Person.Add(new Person { Id = person, FullName = "[DEMO] Lê Thu Hà" });
                db.User.Add(new User { Id = creator, UserName = "ha.lt-" + creator.ToString("N")[..4], PersonId = person, IsActive = true });
                db.Customer.Add(new Customer { Id = cus, Code = "KHC-UI2R", CIF = "KHC-UI2R", Name = "[DEMO] Trần An", DisplayName = "[DEMO] Trần An", CustomerType = "INDIVIDUAL",
                    Status = "Active", DateOfBirth = new DateTime(1990, 2, 3), IdentityNumber = "079123450000", IdentityDate = new DateTime(2015, 1, 1), IdentityPlace = "Cục CS", CreatedBy = creator });
                db.CustomerDeliveryLocation.Add(new CustomerDeliveryLocation { Id = loc, CustomerId = cus, Code = "DP-UI2R", Name = "Cổng", Status = "Active" });
                db.Contract.Add(new Contract { Id = contract, CustomerId = cus, ContractNumber = "HD-UI2R", Status = "Active", EffectiveFrom = Today.AddDays(-10), PaymentTerm = "30 ngày" });
                for (int i = 1; i <= 3; i++)
                {
                    db.Order.Add(new Order { Id = Guid.NewGuid(), CustomerId = cus, DeliveryLocationId = loc, OrderNumber = "DH-UI2R-" + i, MealDate = Today.AddDays(-i), DeliveryDate = Today.AddDays(-i), TotalQuantity = 300, Status = "Delivered" });
                }
                return Task.CompletedTask;
            });
            using var admin = _factory.CreateAuthorizedClient();
            var o = (await admin.GetResultAsync($"/api/Customer/GetOverview?id={cus}")).Ok();
            o.GetProperty("canViewPersonal").GetBoolean().Should().BeTrue();
            o.GetProperty("customer").GetProperty("identityNumber").GetString().Should().Be("079123450000");
            o.GetProperty("createdByName").GetString().Should().Be("[DEMO] Lê Thu Hà", "họ tên hồ sơ của người tạo");
            o.GetProperty("listContract")[0].GetProperty("paymentTerm").GetString().Should().Be("30 ngày");
            var stat = o.GetProperty("listLocationStat").EnumerateArray().Single();
            stat.GetProperty("locationId").GetGuid().Should().Be(loc);
            stat.GetProperty("avgMealsPerDay").GetDecimal().Should().Be(30m, "900 suất / 30 ngày");

            // Chỉ có quyền Xem crm-customer → không nhận ngày sinh / CCCD (máy chủ lọc, D-69 c); DB không đổi.
            using var viewer = _factory.CreateAuthorizedClient(await ViewerAsync("#/crm-customer"));
            var v = (await viewer.GetResultAsync($"/api/Customer/GetOverview?id={cus}")).Ok();
            v.GetProperty("canViewPersonal").GetBoolean().Should().BeFalse();
            var c = v.GetProperty("customer");
            c.GetProperty("name").GetString().Should().Be("[DEMO] Trần An");
            foreach (var f in new[] { "dateOfBirth", "identityNumber", "identityDate", "identityPlace" })
            {
                c.GetProperty(f).ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null, f);
            }
            (await _factory.QueryAsync(db => db.Customer.AsNoTracking().SingleAsync(x => x.Id == cus))).IdentityNumber.Should().Be("079123450000");
        }

        [Fact]
        public async Task Supplier_overview_has_last_receipt_first_trade_and_lowest_price_flags()
        {
            Guid s1 = Guid.NewGuid(), s2 = Guid.NewGuid(), ing = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Supplier.Add(new Supplier { Id = s1, Code = "NC-UI2R1", Name = "[DEMO] NCC rẻ", Status = "Active" });
                db.Supplier.Add(new Supplier { Id = s2, Code = "NC-UI2R2", Name = "[DEMO] NCC đắt", Status = "Active" });
                db.Ingredient.Add(new Ingredient { Id = ing, Code = "NL-UI2R", Name = "[DEMO] Cải ngọt" });
                db.SupplierMaterial.Add(new SupplierMaterial { Id = Guid.NewGuid(), SupplierId = s1, IngredientId = ing, UnitPrice = 18000, Status = "Active", EffectiveFrom = Today.AddDays(-5) });
                db.SupplierMaterial.Add(new SupplierMaterial { Id = Guid.NewGuid(), SupplierId = s2, IngredientId = ing, UnitPrice = 21000, Status = "Active", EffectiveFrom = Today.AddDays(-5) });
                db.PurchaseOrder.Add(new PurchaseOrder { Id = Guid.NewGuid(), SupplierId = s1, PurchaseOrderNumber = "PO-UI2R", OrderDate = Today.AddDays(-40), Status = "Closed" });
                db.GoodsReceipt.Add(new GoodsReceipt { Id = Guid.NewGuid(), SupplierId = s1, ReceiptNumber = "PN-UI2R-1", ReceiptDate = Today.AddDays(-20) });
                db.GoodsReceipt.Add(new GoodsReceipt { Id = Guid.NewGuid(), SupplierId = s1, ReceiptNumber = "PN-UI2R-2", ReceiptDate = Today.AddDays(-2) });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();
            var o = (await c.GetResultAsync($"/api/Supplier/GetOverview?id={s1}")).Ok();
            o.GetProperty("lastReceiptNumber").GetString().Should().Be("PN-UI2R-2");
            o.GetProperty("firstTradeDate").GetDateTime().Date.Should().Be(Today.AddDays(-40));
            var m = o.GetProperty("listMaterial")[0];
            m.GetProperty("supplierCount").GetInt32().Should().Be(2);
            m.GetProperty("isLowest").GetBoolean().Should().BeTrue();
            (await c.GetResultAsync($"/api/Supplier/GetOverview?id={s2}")).Ok().GetProperty("listMaterial")[0].GetProperty("isLowest").GetBoolean().Should().BeFalse();
        }

        [Fact]
        public async Task Employee_overview_has_usual_shift_and_recent_requests_pending_first()
        {
            Guid emp = Guid.NewGuid(), other = Guid.NewGuid(), ca1 = Guid.NewGuid(), ca2 = Guid.NewGuid(), a1 = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Employee.Add(new Employee { Id = emp, Code = "NV-UI2R-" + emp.ToString("N")[..4], FullName = "[DEMO] Trần Thị Hoa", Status = "Active", HireDate = Today.AddMonths(-30) });
                db.Employee.Add(new Employee { Id = other, Code = "NV-UI2R-" + other.ToString("N")[..4], FullName = "[DEMO] Phạm Văn Nam", Status = "Active" });
                db.Shift.Add(new Shift { Id = ca1, Code = "CA1-UI2R", Name = "Ca 1" });
                db.Shift.Add(new Shift { Id = ca2, Code = "CA2-UI2R", Name = "Ca 2" });
                for (int i = 1; i <= 3; i++)
                {
                    db.ShiftAssignment.Add(new ShiftAssignment { Id = i == 1 ? a1 : Guid.NewGuid(), EmployeeId = emp, ShiftId = ca1, WorkDate = Today.AddDays(-i), PlannedStart = Today.AddDays(-i).AddHours(6), PlannedEnd = Today.AddDays(-i).AddHours(14), AssignmentStatus = "Completed" });
                }
                db.ShiftAssignment.Add(new ShiftAssignment { Id = Guid.NewGuid(), EmployeeId = emp, ShiftId = ca2, WorkDate = Today.AddDays(-5), PlannedStart = Today.AddDays(-5).AddHours(14), PlannedEnd = Today.AddDays(-5).AddHours(22), AssignmentStatus = "Completed" });
                db.LeaveRequest.Add(new LeaveRequest { Id = Guid.NewGuid(), RequestNo = "NP-UI2R-1", EmployeeId = emp, FromDate = Today.AddDays(4), ToDate = Today.AddDays(5), LeaveTypeCode = "ANNUAL", RequestStatus = "Pending", SubmittedAt = Today.AddDays(-3) });
                db.ShiftSwapRequest.Add(new ShiftSwapRequest { Id = Guid.NewGuid(), RequestNo = "DC-UI2R-1", RequesterEmployeeId = emp, TargetEmployeeId = other, CurrentShiftAssignmentId = a1, RequestStatus = "Approved", SubmittedAt = Today.AddDays(-1) });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();
            var o = (await c.GetResultAsync($"/api/Employee/GetOverview?id={emp}")).Ok();
            o.GetProperty("usualShiftCode").GetString().Should().Be("CA1-UI2R");
            o.GetProperty("usualShiftStart").GetDateTime().Hour.Should().Be(6);
            var reqs = o.GetProperty("listRecentRequest").EnumerateArray().ToList();
            reqs.Should().HaveCount(2);
            reqs[0].GetProperty("kind").GetString().Should().Be("LEAVE", "đơn chờ duyệt lên đầu");
            reqs[0].GetProperty("days").GetInt32().Should().Be(2);
            reqs[1].GetProperty("kind").GetString().Should().Be("SWAP");
            reqs[1].GetProperty("otherEmployeeName").GetString().Should().Be("[DEMO] Phạm Văn Nam");
        }

        /// <summary>User thường chỉ có quyền Xem 1 màn.</summary>
        private async Task<Guid> ViewerAsync(string metaLink)
        {
            Guid userId = Guid.NewGuid(), group = Guid.NewGuid();
            await _factory.SeedAsync(async db =>
            {
                db.User.Add(new User { Id = userId, UserName = "vw-" + userId.ToString("N")[..6], IsActive = true });
                db.UserGroup.Add(new UserGroup { Id = group, Name = "VW-" + group.ToString("N")[..6] });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = userId, UserGroupId = group });
                var menu = await db.ObjectMenu.FirstOrDefaultAsync(m => m.MetaLink == metaLink);
                if (menu == null)
                {
                    menu = new ObjectMenu { Id = Guid.NewGuid(), Name = "Khách hàng", MetaLink = metaLink, IsActive = true, LevelShow = 2 };
                    db.ObjectMenu.Add(menu);
                }
                db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = group, ObjectMenuId = menu.Id, AllowAccess = true });
            });
            return userId;
        }
    }
}
