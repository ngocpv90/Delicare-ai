using AppDelicare.Data.Connection;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Implement;
using AppDelicare.Services.Interface;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Purchasing
{
    /// <summary>
    /// T14 – trên SQL Server dev thật (bỏ qua nếu không có DB): đơn Đã chốt × công thức khóa → Calculate (KH- + PH- qua sp_getapplock),
    /// tính lại cùng ngày cập nhật tại chỗ, trong 1 transaction ngoài rồi ROLLBACK (không để lại dữ liệu). Dữ liệu thử gắn nhãn [DEMO].
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class MrpSqlServerTests(ApiFactory factory)
    {
        [SkippableFact]
        public async Task Calculate_and_recalculate_on_sql_server_inside_outer_transaction()
        {
            Skip.If(factory.SkipReason != null, factory.SkipReason);
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            var actor = scope.ServiceProvider.GetRequiredService<ICurrentActor>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            Guid admin = await db.User.Where(u => u.UserName == "admin").Select(u => u.Id).SingleAsync();
            int plansBefore = await db.ProductionPlan.CountAsync();
            int prBefore = await db.PurchaseRequest.CountAsync();
            // Ngày rất xa để không trùng đơn thật của DB dev (MRP lấy mọi đơn Đã chốt của ngày).
            DateTime date = clock.Today.AddDays(3000 + Random.Shared.Next(0, 500));
            string tag = "[DEMO]-T14-" + Guid.NewGuid().ToString("N")[..6];

            using (actor.BeginScope(ActorInfo.ForUser(admin)))
            await using (var tx = await db.Database.BeginTransactionAsync())
            {
                Guid warehouse = Guid.NewGuid(), kg = Guid.NewGuid(), ingredient = Guid.NewGuid(), dish = Guid.NewGuid(), recipe = Guid.NewGuid();
                Guid menu = Guid.NewGuid(), customer = Guid.NewGuid(), order = Guid.NewGuid(), supplier = Guid.NewGuid();
                int day = date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;
                db.Warehouse.Add(new Warehouse { Id = warehouse, Code = tag, Name = "[DEMO] Kho T14", Status = "Active" });
                db.UnitMeasure.Add(new UnitMeasure { Id = kg, Code = tag, Name = "[DEMO] kg" });
                db.Ingredient.Add(new Ingredient { Id = ingredient, Code = tag, Name = "[DEMO] Thịt T14", BaseUnitId = kg, BaseUomId = kg });
                db.Dish.Add(new Dish { Id = dish, Code = tag, Name = "[DEMO] Thịt kho T14" });
                db.Recipe.Add(new Recipe { Id = recipe, DishId = dish, VersionNo = 1, Status = "Locked", IsActive = true, StandardOutputQty = 100,
                    EffectiveFrom = clock.Today, LockedAt = clock.Now, LockedBy = admin, Note = "[DEMO]" });
                db.RecipeLine.Add(new RecipeLine { Id = Guid.NewGuid(), RecipeId = recipe, IngredientId = ingredient, Quantity = 20, UnitId = kg, UomId = kg, WastePercent = 5 });
                db.Menu.Add(new Model.Entity.Menu { Id = menu, Code = tag, Name = "[DEMO] Thực đơn T14", VersionNo = 1, Status = "Published", EffectiveFrom = clock.Today });
                db.MenuLine.Add(new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = dish, MealType = "TRUA", DayOfWeek = day, SequenceNo = 1 });
                db.Customer.Add(new Customer { Id = customer, Code = tag, Name = "[DEMO] Khách T14", CIF = tag, CustomerType = "FACTORY", DisplayName = "[DEMO] Khách T14", Status = "Active" });
                db.Order.Add(new Order { Id = order, OrderNumber = tag, OrderNo = tag, CustomerId = customer, OrderDate = clock.Today, MealDate = date, DeliveryDate = date, Status = "Locked", TotalQuantity = 200 });
                db.OrderLine.Add(new OrderLine { Id = Guid.NewGuid(), OrderId = order, MenuId = menu, MealType = "TRUA", Quantity = 200 });
                db.Supplier.Add(new Supplier { Id = supplier, Code = tag, Name = "[DEMO] NCC T14", Status = "Active" });
                db.SupplierMaterial.Add(new SupplierMaterial { Id = Guid.NewGuid(), SupplierId = supplier, IngredientId = ingredient, IsPreferred = true, UnitPrice = 100000, PurchaseUomId = kg, Status = "Active" });
                await db.SaveChangesAsync("Insert");
                await new InventoryLedgerService(db).PostAsync(new InventoryPostRequest
                {
                    TransactionType = "RECEIPT", TransactionNumber = tag, WarehouseId = warehouse, IngredientId = ingredient, UnitId = kg, QuantityIn = 12,
                });

                var mrp = scope.ServiceProvider.GetRequiredService<IMaterialRequirementService>();
                var first = await mrp.Calculate(new MaterialRequirementCalculateRequest { ProductionDate = date, WarehouseId = warehouse });
                first.ResponseCode.Should().Be(200, first.ResponseMessage);
                // 20 kg × 200/100 × 1,05 = 42; thiếu 42 − 12 = 30.
                var item = first.Data!.ListItem.Single();
                item.RequiredQuantity.Should().Be(42);
                item.AvailableQuantity.Should().Be(12);
                item.PurchaseQuantity.Should().Be(30);
                first.Data.PlanNumber.Should().MatchRegex(@"^KH-\d{6}(-\d+)?$");
                first.Data.PurchaseRequest!.RequestNumber.Should().MatchRegex(@"^PH-\d{6}-\d{4}$");

                var second = await mrp.Calculate(new MaterialRequirementCalculateRequest { ProductionDate = date, WarehouseId = warehouse });
                second.ResponseCode.Should().Be(200, second.ResponseMessage);
                second.Data!.ProductionPlanId.Should().Be(first.Data.ProductionPlanId);
                second.Data.PurchaseRequest!.Id.Should().Be(first.Data.PurchaseRequest.Id);
                db.ChangeTracker.Clear();
                (await db.MaterialRequirement.CountAsync(r => r.ProductionPlanId == first.Data.ProductionPlanId && !r.IsDeleted)).Should().Be(1);
                var line = await db.PurchaseRequestLine.AsNoTracking().SingleAsync(l => l.PurchaseRequestId == first.Data.PurchaseRequest.Id && !l.IsDeleted);
                line.Quantity.Should().Be(30);
                line.UomId.Should().Be(kg);
                await tx.RollbackAsync();
            }

            db.ChangeTracker.Clear();
            (await db.ProductionPlan.CountAsync()).Should().Be(plansBefore, "rollback không để lại dữ liệu");
            (await db.PurchaseRequest.CountAsync()).Should().Be(prBefore);
        }
    }
}
