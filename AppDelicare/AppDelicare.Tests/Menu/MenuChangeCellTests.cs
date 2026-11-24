using System.Net.Http.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Menu
{
    /// <summary>
    /// UI-6.13 (D-91): đổi món cho mọi khách trên ô thực đơn tuần – xem trước số khách / suất (mọi đơn dùng thực đơn ở ngày / buổi / lựa chọn),
    /// tự duyệt khi cùng nhóm + có công thức khóa + FC đạt + trước giờ chốt; thiếu điều kiện → Chờ duyệt (engine MENU_CHANGE); lưới hiện "đã đổi"; tab Đổi món.
    /// </summary>
    public class MenuChangeCellTests(InMemoryApiFactory factory) : IClassFixture<InMemoryApiFactory>
    {
        private static readonly DateTime Week = new(2034, 6, 5);   // thứ Hai

        [Fact]
        public async Task Cell_change_previews_scope_auto_approves_when_safe_and_routes_others_to_approval()
        {
            string k = Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
            factory.Clock.FixedNow = Week.AddDays(-3).AddHours(9);
            Guid cat = Guid.NewGuid(), other = Guid.NewGuid(), dOld = Guid.NewGuid(), dSame = Guid.NewGuid(), dOtherCat = Guid.NewGuid(), menu = Guid.NewGuid();
            Guid c1 = Guid.NewGuid(), c2 = Guid.NewGuid();
            await factory.SeedAsync(db =>
            {
                db.DishCategory.AddRange(new DishCategory { Id = cat, Code = "MC" + k, Name = "Món chính " + k }, new DishCategory { Id = other, Code = "CA" + k, Name = "Canh " + k });
                foreach (var (id, name, c) in new[] { (dOld, "Gà kho " + k, cat), (dSame, "Cá kho " + k, cat), (dOtherCat, "Canh chua " + k, other) })
                {
                    db.Dish.Add(new Dish { Id = id, Code = "D" + id.ToString("N")[..6], Name = name, DishCategoryId = c, StandardCost = 12000, Status = "Active" });
                    db.Recipe.Add(new Recipe { Id = Guid.NewGuid(), DishId = id, VersionNo = 1, Status = "Locked", IsActive = true, YieldQuantity = 1 });
                }
                db.Menu.Add(new Model.Entity.Menu { Id = menu, Code = "NU-CC-" + k, Name = "Chung", VersionNo = 1, Status = "Published", MenuType = "COMMON", EffectiveFrom = Week, EffectiveTo = Week.AddDays(6) });
                db.MenuLine.Add(new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = dOld, MealType = "TRUA", SequenceNo = 1, DayOfWeek = 3, SortOrder = 0, PortionQty = 1 });
                foreach (var (cus, qty) in new[] { (c1, 100m), (c2, 50m) })
                {
                    db.Customer.Add(new Customer { Id = cus, Code = "CC" + cus.ToString("N")[..5], Name = "Khách", DisplayName = "Khách " + cus.ToString("N")[..4], CIF = "CC" + cus.ToString("N")[..5], CustomerType = "BUSINESS", Status = "Active" });
                    var ct = new Contract { Id = Guid.NewGuid(), CustomerId = cus, ContractNumber = "HD-" + cus.ToString("N")[..6], Status = "Active", EffectiveFrom = new DateTime(2034, 1, 1) };
                    db.Contract.Add(ct);
                    db.ContractMeal.Add(new ContractMeal { Id = Guid.NewGuid(), ContractId = ct.Id, MealType = "TRUA", MealName = "Trưa", UnitPrice = 40000 });
                    var o = new Order { Id = Guid.NewGuid(), CustomerId = cus, OrderNumber = "DH-CC-" + cus.ToString("N")[..5], MealDate = Week.AddDays(2), Status = "Confirmed", TotalQuantity = qty };
                    db.Order.Add(o);
                    db.OrderLine.Add(new OrderLine { Id = Guid.NewGuid(), OrderId = o.Id, MenuId = menu, MealType = "TRUA", MenuOptionNo = 1, Quantity = qty });
                }
                return Task.CompletedTask;
            });
            using var c = factory.CreateAuthorizedClient();
            var pre = (await c.GetResultAsync($"/api/MenuChangeRequest/GetCellPreview?MenuId={menu}&MealType=TRUA&OptionNo=1&Day=3&OriginalDishId={dOld}&RequestedDishId={dSame}")).Ok();
            pre.GetProperty("customers").GetInt32().Should().Be(2);
            pre.GetProperty("quantity").GetDecimal().Should().Be(150);
            pre.GetProperty("autoApprove").GetBoolean().Should().BeTrue("cùng nhóm, có công thức, FC 30 %, trước giờ chốt");

            // Khác nhóm món → không tự duyệt.
            var pre2 = (await c.GetResultAsync($"/api/MenuChangeRequest/GetCellPreview?MenuId={menu}&MealType=TRUA&OptionNo=1&Day=3&OriginalDishId={dOld}&RequestedDishId={dOtherCat}")).Ok();
            pre2.GetProperty("autoApprove").GetBoolean().Should().BeFalse();
            pre2.GetProperty("listBlock")[0].GetString().Should().Contain("khác nhóm");

            (await LegacyResult.ReadAsync(await c.PostAsJsonAsync("/api/MenuChangeRequest/CreateForCell", new { menuId = menu, mealType = "TRUA", optionNo = 1, day = 3, originalDishId = dOld, requestedDishId = dSame }))).Fail("lý do");
            var auto = await LegacyResult.ReadAsync(await c.PostAsJsonAsync("/api/MenuChangeRequest/CreateForCell", new { menuId = menu, mealType = "TRUA", optionNo = 1, day = 3, originalDishId = dOld, requestedDishId = dSame, reasonCode = "SHORTAGE", note = "Hết gà" }));
            auto.Ok().GetProperty("request").GetProperty("requestStatus").GetString().Should().Be("Approved");
            auto.Message.Should().Contain("tự duyệt");
            var req = await factory.QueryAsync(db => db.MenuChangeRequest.AsNoTracking().SingleAsync(r => r.MenuId == menu && r.RequestedDishId == dSame));
            req.OrderId.Should().BeNull("phạm vi mọi khách");
            req.Quantity.Should().Be(150);
            req.Reason.Should().Contain("Thiếu nguyên liệu").And.Contain("Hệ thống tự duyệt");

            var w = (await c.GetResultAsync($"/api/Menu/GetWeek?WeekStart={Week:yyyy-MM-dd}")).Ok();
            var cell = w.GetProperty("listMeal").EnumerateArray().Single(m => m.GetProperty("mealType").GetString() == "TRUA").GetProperty("listOption")[0].GetProperty("listRow")
                .EnumerateArray().SelectMany(r => r.GetProperty("listCell").EnumerateArray()).Single(x => x.GetProperty("day").GetInt32() == 3 && x.GetProperty("dishId").ValueKind != System.Text.Json.JsonValueKind.Null);
            cell.GetProperty("changedDishName").GetString().Should().Be("Cá kho " + k);

            // Khác nhóm → Chờ duyệt (COO).
            var manual = (await LegacyResult.ReadAsync(await c.PostAsJsonAsync("/api/MenuChangeRequest/CreateForCell", new { menuId = menu, mealType = "TRUA", optionNo = 1, day = 3, originalDishId = dOld, requestedDishId = dOtherCat, reasonCode = "QUALITY" }))).Ok();
            manual.GetProperty("request").GetProperty("requestStatus").GetString().Should().Be("Pending");
            (await factory.QueryAsync(db => db.ApprovalRequest.CountAsync(a => a.BusinessType == "MENU_CHANGE" && a.BusinessId == manual.GetProperty("request").GetProperty("id").GetGuid()))).Should().Be(1);

            var tab = await c.GetResultAsync($"/api/MenuChangeRequest/GetWeekChanges?WeekStart={Week:yyyy-MM-dd}");
            var items = tab.ListData.EnumerateArray().Where(x => x.GetProperty("menuId").ValueKind != System.Text.Json.JsonValueKind.Null && x.GetProperty("menuId").GetGuid() == menu).ToList();
            items.Should().HaveCount(2);
            items.Should().Contain(x => x.GetProperty("autoApproved").GetBoolean() && x.GetProperty("scope").GetString() == "Mọi khách");
        }

        [Fact]
        public async Task Order_dish_change_lists_dishes_applies_to_customer_range_and_auto_approves_each_cell()
        {
            string k = Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
            DateTime week = Week.AddDays(14);
            factory.Clock.FixedNow = week.AddDays(-3).AddHours(9);
            Guid cat = Guid.NewGuid(), dOld = Guid.NewGuid(), dNew = Guid.NewGuid(), menu = Guid.NewGuid(), cus = Guid.NewGuid(), o1 = Guid.NewGuid(), o2 = Guid.NewGuid();
            await factory.SeedAsync(db =>
            {
                db.DishCategory.Add(new DishCategory { Id = cat, Code = "OB" + k, Name = "Món chính OB" + k });
                foreach (var (id, name) in new[] { (dOld, "Gà rang " + k), (dNew, "Cá rán " + k) })
                {
                    db.Dish.Add(new Dish { Id = id, Code = "D" + id.ToString("N")[..6], Name = name, DishCategoryId = cat, StandardCost = 10000, Status = "Active" });
                    db.Recipe.Add(new Recipe { Id = Guid.NewGuid(), DishId = id, VersionNo = 1, Status = "Locked", IsActive = true, YieldQuantity = 1 });
                }
                db.Menu.Add(new Model.Entity.Menu { Id = menu, Code = "NU-OB-" + k, Name = "Chung", VersionNo = 1, Status = "Published", MenuType = "COMMON", EffectiveFrom = week, EffectiveTo = week.AddDays(6) });
                foreach (int day in new[] { 3, 4 })
                {
                    db.MenuLine.Add(new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = dOld, MealType = "TRUA", SequenceNo = 1, DayOfWeek = day, SortOrder = 0, PortionQty = 1 });
                }
                db.Customer.Add(new Customer { Id = cus, Code = "OB" + k, Name = "Khách", DisplayName = "Khách OB" + k, CIF = "OB" + k, CustomerType = "BUSINESS", Status = "Active" });
                var ct = new Contract { Id = Guid.NewGuid(), CustomerId = cus, ContractNumber = "HD-OB" + k, Status = "Active", EffectiveFrom = new DateTime(2034, 1, 1) };
                db.Contract.Add(ct);
                db.ContractMeal.Add(new ContractMeal { Id = Guid.NewGuid(), ContractId = ct.Id, MealType = "TRUA", MealName = "Trưa", UnitPrice = 40000 });
                foreach (var (oid, day, qty) in new[] { (o1, 2, 80m), (o2, 3, 60m) })
                {
                    db.Order.Add(new Order { Id = oid, CustomerId = cus, OrderNumber = "DH-OB-" + k + day, MealDate = week.AddDays(day), Status = "Confirmed", TotalQuantity = qty });
                    db.OrderLine.Add(new OrderLine { Id = Guid.NewGuid(), OrderId = oid, MenuId = menu, MealType = "TRUA", MenuOptionNo = 1, Quantity = qty });
                }
                return Task.CompletedTask;
            });
            using var c = factory.CreateAuthorizedClient();
            var dishes = (await c.GetResultAsync($"/api/Order/GetDishChange?OrderId={o1}")).Ok();
            dishes.GetProperty("listDish").EnumerateArray().Single().GetProperty("dishName").GetString().Should().Be("Gà rang " + k);
            var pre = (await c.GetResultAsync($"/api/Order/GetDishChange?OrderId={o1}&OriginalDishId={dOld}&RequestedDishId={dNew}&ApplyFrom={week.AddDays(2):yyyy-MM-dd}&ApplyTo={week.AddDays(3):yyyy-MM-dd}")).Ok();
            pre.GetProperty("listItem").GetArrayLength().Should().Be(2, "đơn này + đơn hôm sau của khách có cùng món");
            pre.GetProperty("listSuggestion").EnumerateArray().Should().Contain(x => x.GetProperty("dishId").GetGuid() == dNew);
            (await LegacyResult.ReadAsync(await c.PostAsJsonAsync("/api/Order/RequestDishChange", new { orderId = o1, originalDishId = dOld, requestedDishId = dNew }))).Fail("nguồn");
            var r = (await LegacyResult.ReadAsync(await c.PostAsJsonAsync("/api/Order/RequestDishChange", new { orderId = o1, originalDishId = dOld, requestedDishId = dNew, quantity = 30, source = "PHONE", note = "Khách dị ứng gà", applyFrom = week.AddDays(2), applyTo = week.AddDays(3) }))).Ok();
            r.GetProperty("autoApproved").GetInt32().Should().Be(2);
            var reqs = await factory.QueryAsync(db => db.MenuChangeRequest.AsNoTracking().Where(x => x.MenuId == menu).ToListAsync());
            reqs.Should().HaveCount(2).And.OnlyContain(x => x.RequestStatus == "Approved" && x.Reason!.Contains("Điện thoại"));
            reqs.Single(x => x.OrderId == o1).Quantity.Should().Be(30, "đổi một phần đơn này");
            reqs.Single(x => x.OrderId == o2).Quantity.Should().Be(60);
        }
    }
}
