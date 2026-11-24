using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;

namespace AppDelicare.Tests.Costing
{
    /// <summary>
    /// UI-6.27 (D-108): giá thành chuẩn chỉ tự tính (Insert / Update chặn); MealCost/GetMargin khớp tổng giá vốn suất theo khách × hợp đồng / ngày / món,
    /// đơn "tạm tính" khi lệnh SX của món chưa có giá thành thực tế (hết tạm tính khi có ActualCost), chi phí lỗi tách riêng.
    /// </summary>
    public class MealMarginUi627Tests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public MealMarginUi627Tests(InMemoryApiFactory factory) => _factory = factory;

        [Fact]
        public async Task Standard_cost_is_auto_only_and_margin_matches_meal_cost()
        {
            var day = new DateTime(2033, 3, 10);
            _factory.Clock.FixedNow = new DateTime(2033, 3, 25, 9, 0, 0);
            Guid cusA = Guid.NewGuid(), cusB = Guid.NewGuid(), dishX = Guid.NewGuid(), dishY = Guid.NewGuid(), oA = Guid.NewGuid(), oB = Guid.NewGuid(), oFault = Guid.NewGuid();
            Guid lA = Guid.NewGuid(), lB = Guid.NewGuid(), lF = Guid.NewGuid(), plan = Guid.NewGuid(), poX = Guid.NewGuid(), poY = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Customer.AddRange(new Customer { Id = cusA, Code = "KA-M", Name = "Khách A", DisplayName = "Khách A", CIF = "KA-M", CustomerType = "FACTORY", Status = "Active" },
                    new Customer { Id = cusB, Code = "KB-M", Name = "Khách B", DisplayName = "Khách B", CIF = "KB-M", CustomerType = "FACTORY", Status = "Active" });
                db.Dish.AddRange(new Dish { Id = dishX, Code = "MX-M", Name = "Món X", Status = "Active" }, new Dish { Id = dishY, Code = "MY-M", Name = "Món Y", Status = "Active" });
                db.StandardCost.AddRange(new StandardCost { Id = Guid.NewGuid(), DishId = dishX, RecipeId = Guid.NewGuid(), CostPerServing = 10000, Status = "Active", EffectiveFrom = day.AddDays(-30) },
                    new StandardCost { Id = Guid.NewGuid(), DishId = dishY, RecipeId = Guid.NewGuid(), CostPerServing = 5000, Status = "Active", EffectiveFrom = day.AddDays(-30) });
                db.Order.AddRange(new Order { Id = oA, OrderNumber = "DH-MA", CustomerId = cusA, OrderDate = day.AddDays(-2), MealDate = day, DeliveryDate = day, Status = "Reconciled", TotalQuantity = 100, TotalAmount = 3_000_000 },
                    new Order { Id = oB, OrderNumber = "DH-MB", CustomerId = cusB, OrderDate = day.AddDays(-2), MealDate = day.AddDays(1), DeliveryDate = day.AddDays(1), Status = "Reconciled", TotalQuantity = 50, TotalAmount = 1_500_000 },
                    new Order { Id = oFault, OrderNumber = "DH-MF", CustomerId = cusA, OrderDate = day, MealDate = day, DeliveryDate = day, Status = "Reconciled", TotalQuantity = 5, TotalAmount = 0, Note = "[DON_BU:DH-MA] bù thiếu" });
                db.OrderLine.AddRange(new OrderLine { Id = lA, OrderId = oA, MealType = "TRUA", Quantity = 100, LineAmount = 3_000_000 }, new OrderLine { Id = lB, OrderId = oB, MealType = "TRUA", Quantity = 50, LineAmount = 1_500_000 },
                    new OrderLine { Id = lF, OrderId = oFault, MealType = "TRUA", Quantity = 5, LineAmount = 0 });
                // A: X 100 phần (thực tế 1.1 tr) + Y 100 phần (tạm tính 0,5 tr); B: X 50 phần 0,55 tr; bù: X 5 phần.
                db.MealCost.AddRange(
                    new MealCost { Id = Guid.NewGuid(), OrderId = oA, OrderLineId = lA, DishId = dishX, MealQty = 100, AcceptedMealQuantity = 100, TotalActualCost = 1_100_000, Revenue = 2_000_000, CalculatedAt = day },
                    new MealCost { Id = Guid.NewGuid(), OrderId = oA, OrderLineId = lA, DishId = dishY, MealQty = 100, AcceptedMealQuantity = 100, TotalActualCost = 500_000, Revenue = 1_000_000, CalculatedAt = day },
                    new MealCost { Id = Guid.NewGuid(), OrderId = oB, OrderLineId = lB, DishId = dishX, MealQty = 50, AcceptedMealQuantity = 50, TotalActualCost = 550_000, Revenue = 1_500_000, CalculatedAt = day },
                    new MealCost { Id = Guid.NewGuid(), OrderId = oFault, OrderLineId = lF, DishId = dishX, MealQty = 5, AcceptedMealQuantity = 5, TotalActualCost = 55_000, Revenue = 0, CalculatedAt = day });
                // Lệnh SX ngày ăn của X có giá thành thực tế; Y chưa → đơn A tạm tính.
                db.ProductionPlan.Add(new ProductionPlan { Id = plan, PlanNumber = "KH-M", PlanNo = "KH-M", ProductionDate = day, PlanDate = day.AddDays(-1), Status = "Completed" });
                db.ProductionPlan.Add(new ProductionPlan { Id = Guid.NewGuid(), PlanNumber = "KH-M2", PlanNo = "KH-M2", ProductionDate = day.AddDays(1), PlanDate = day, Status = "Completed" });
                db.ProductionOrder.AddRange(new ProductionOrder { Id = poX, ProductionPlanId = plan, DishId = dishX, RecipeId = Guid.NewGuid(), PlannedQty = 105, ActualQty = 105, Status = "Closed", ProductionOrderNumber = "LS-MX" },
                    new ProductionOrder { Id = poY, ProductionPlanId = plan, DishId = dishY, RecipeId = Guid.NewGuid(), PlannedQty = 100, ActualQty = 100, Status = "InProgress", ProductionOrderNumber = "LS-MY" });
                db.ActualCost.Add(new ActualCost { Id = Guid.NewGuid(), ProductionOrderId = poX, ProductionBatchId = Guid.NewGuid(), TotalCost = 1_155_000, CalculatedAt = day });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();
            (await c.PostFormAsync("/api/StandardCost/Insert", ("DishId", dishX), ("RecipeId", Guid.NewGuid()), ("CostPerServing", 1m))).Fail("tự tính");

            var m = (await c.GetResultAsync("/api/MealCost/GetMargin?Month=2033-03&GroupBy=customer")).Ok();
            var sum = m.GetProperty("summary");
            (sum.GetProperty("meals").GetDecimal(), sum.GetProperty("revenue").GetDecimal(), sum.GetProperty("cost").GetDecimal()).Should().Be((150m, 4_500_000m, 2_150_000m), "chỉ đơn bán (không tính đơn bù)");
            sum.GetProperty("faultCost").GetDecimal().Should().Be(55_000m);
            sum.GetProperty("ratioPct").GetDecimal().Should().Be(47.8m);
            m.GetProperty("laborIncluded").GetBoolean().Should().BeFalse();
            m.GetProperty("to").GetDateTime().Date.Should().Be(new DateTime(2033, 3, 25), "tháng hiện tại tính tới hôm nay");
            var rows = m.GetProperty("listRow").EnumerateArray().ToList();
            var a = rows.Single(r => r.GetProperty("customerId").GetGuid() == cusA);
            (a.GetProperty("revenue").GetDecimal(), a.GetProperty("cost").GetDecimal(), a.GetProperty("faultCost").GetDecimal(), a.GetProperty("provisionalOrders").GetInt32()).Should().Be((3_000_000m, 1_600_000m, 55_000m, 1));
            rows[0].GetProperty("ratioPct").GetDecimal().Should().BeGreaterThanOrEqualTo(rows[^1].GetProperty("ratioPct").GetDecimal(), "tỷ lệ cao nhất lên đầu");
            m.GetProperty("listDay").EnumerateArray().Select(d => d.GetProperty("revenue").GetDecimal()).Sum().Should().Be(4_500_000m);

            var dish = (await c.GetResultAsync("/api/MealCost/GetMargin?Month=2033-03&GroupBy=dish")).Ok().GetProperty("listDish").EnumerateArray().Single(d => d.GetProperty("dishId").GetGuid() == dishX);
            (dish.GetProperty("standardPerPortion").GetDecimal(), dish.GetProperty("actualPerPortion").GetDecimal(), dish.GetProperty("variancePct").GetDecimal()).Should().Be((10_000m, 11_000m, 10m));

            // Lệnh của Y có giá thành thực tế → đơn A hết tạm tính.
            await _factory.SeedAsync(db => { db.ActualCost.Add(new ActualCost { Id = Guid.NewGuid(), ProductionOrderId = poY, ProductionBatchId = Guid.NewGuid(), TotalCost = 480_000, CalculatedAt = day }); return Task.CompletedTask; });
            var again = (await c.GetResultAsync($"/api/MealCost/GetMargin?Month=2033-03&CustomerId={cusA}")).Ok();
            again.GetProperty("summary").GetProperty("provisionalOrders").GetInt32().Should().Be(0);
        }
    }
}
