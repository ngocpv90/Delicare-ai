using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;

namespace AppDelicare.Tests.Costing
{
    /// <summary>
    /// UI-6.52 (D-139 f): tab Giá vốn & lãi gộp của Bảng điều hành dùng chung chi nhánh – MealCost/GetMargin và GetList lọc khách thuộc chi nhánh (gồm đơn vị con);
    /// không truyền chi nhánh = như cũ; chi nhánh không tồn tại → báo lỗi.
    /// </summary>
    public class MealMarginBranchUi652Tests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public MealMarginBranchUi652Tests(InMemoryApiFactory factory) => _factory = factory;

        [Fact]
        public async Task Margin_and_list_filter_by_branch_including_child_units()
        {
            var day = new DateTime(2034, 5, 12);
            _factory.Clock.FixedNow = new DateTime(2034, 5, 20, 9, 0, 0);
            Guid company = Guid.NewGuid(), root = Guid.NewGuid(), branch = Guid.NewGuid(), child = Guid.NewGuid(), other = Guid.NewGuid();
            Guid cusA = Guid.NewGuid(), cusB = Guid.NewGuid(), cusC = Guid.NewGuid(), dish = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.OrganizationUnit.AddRange(new OrganizationUnit { Id = root, CompanyId = company, Code = "U652-ROOT", Name = "Công ty", UnitType = "COMPANY" },
                    new OrganizationUnit { Id = branch, CompanyId = company, Code = "U652-B1", Name = "Bếp 1", UnitType = "BRANCH", ParentId = root },
                    new OrganizationUnit { Id = child, CompanyId = company, Code = "U652-B1A", Name = "Điểm 1A", UnitType = "BRANCH", ParentId = branch },
                    new OrganizationUnit { Id = other, CompanyId = company, Code = "U652-B2", Name = "Bếp 2", UnitType = "BRANCH", ParentId = root });
                Customer C(Guid id, string code, Guid? br) => new() { Id = id, Code = code, Name = code, DisplayName = code, CIF = code, CustomerType = "FACTORY", Status = "Active", BranchId = br };
                db.Customer.AddRange(C(cusA, "K652-A", branch), C(cusB, "K652-B", child), C(cusC, "K652-C", other));
                foreach (var (cus, amount) in new[] { (cusA, 1_000_000m), (cusB, 2_000_000m), (cusC, 4_000_000m) })
                {
                    Guid o = Guid.NewGuid(), l = Guid.NewGuid();
                    db.Order.Add(new Order { Id = o, OrderNumber = "DH652-" + amount, CustomerId = cus, OrderDate = day.AddDays(-1), MealDate = day, DeliveryDate = day, Status = "Reconciled", TotalQuantity = 10, TotalAmount = amount });
                    db.OrderLine.Add(new OrderLine { Id = l, OrderId = o, MealType = "TRUA", Quantity = 10, LineAmount = amount });
                    db.MealCost.Add(new MealCost { Id = Guid.NewGuid(), OrderId = o, OrderLineId = l, DishId = dish, MealQty = 10, AcceptedMealQuantity = 10, TotalActualCost = amount / 2, Revenue = amount, CalculatedAt = day });
                }
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();
            decimal Revenue(System.Text.Json.JsonElement m) => m.GetProperty("summary").GetProperty("revenue").GetDecimal();

            Revenue((await c.GetResultAsync("/api/MealCost/GetMargin?Month=2034-05&GroupBy=customer")).Ok()).Should().Be(7_000_000m, "không chọn chi nhánh = mọi khách");
            var b1 = (await c.GetResultAsync($"/api/MealCost/GetMargin?Month=2034-05&GroupBy=customer&BranchId={branch}")).Ok();
            Revenue(b1).Should().Be(3_000_000m, "Bếp 1 gồm cả Điểm 1A (đơn vị con)");
            b1.GetProperty("listRow").EnumerateArray().Select(r => r.GetProperty("customerId").GetGuid()).Should().BeEquivalentTo([cusA, cusB]);
            Revenue((await c.GetResultAsync($"/api/MealCost/GetMargin?Month=2034-05&GroupBy=day&BranchId={other}")).Ok()).Should().Be(4_000_000m);
            (await c.GetResultAsync($"/api/MealCost/GetMargin?Month=2034-05&BranchId={Guid.NewGuid()}")).Fail();

            var list = await c.GetResultAsync($"/api/MealCost/GetList?From=2034-05-12&To=2034-05-12&BranchId={branch}");
            list.ListData.EnumerateArray().Select(o => o.GetProperty("customerId").GetGuid()).Should().BeEquivalentTo([cusA, cusB]);
        }
    }
}
