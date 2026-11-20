using AppDelicare.Data.Connection;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Interface;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Sales
{
    /// <summary>
    /// T13 – trên SQL Server dev thật (bỏ qua nếu không có DB): tạo đơn (số PH- qua sp_getapplock) → gửi → xác nhận → chốt
    /// bằng OrderService trong 1 transaction ngoài rồi ROLLBACK (không để lại dữ liệu). Dữ liệu thử gắn nhãn [DEMO].
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class OrderSqlServerTests(ApiFactory factory)
    {
        [SkippableFact]
        public async Task Order_create_submit_confirm_lock_on_sql_server_inside_outer_transaction()
        {
            Skip.If(factory.SkipReason != null, factory.SkipReason);
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            var actor = scope.ServiceProvider.GetRequiredService<ICurrentActor>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            Guid admin = await db.User.Where(u => u.UserName == "admin").Select(u => u.Id).SingleAsync();
            int ordersBefore = await db.Order.CountAsync();
            DateTime mealDate = clock.Today.AddDays(30);
            string tag = "[DEMO]-T13-" + Guid.NewGuid().ToString("N")[..6];

            using (actor.BeginScope(ActorInfo.ForUser(admin)))
            await using (var tx = await db.Database.BeginTransactionAsync())
            {
                Guid customer = Guid.NewGuid(), location = Guid.NewGuid(), contract = Guid.NewGuid(), meal = Guid.NewGuid(), shift = Guid.NewGuid();
                db.Customer.Add(new Customer { Id = customer, Code = tag, Name = "[DEMO] Khách T13", CIF = tag, CustomerType = "FACTORY", DisplayName = "[DEMO] Khách T13", Status = "Active" });
                db.CustomerDeliveryLocation.Add(new CustomerDeliveryLocation { Id = location, CustomerId = customer, Code = tag, Name = "[DEMO] Điểm giao" });
                db.Contract.Add(new Contract { Id = contract, CustomerId = customer, ContractNumber = tag, ContractNo = tag, Status = "Active", EffectiveFrom = clock.Today.AddDays(-1) });
                db.ContractMeal.Add(new ContractMeal { Id = meal, ContractId = contract, MealType = "TRUA", MealName = "[DEMO] Suất trưa", UnitPrice = 38000 });
                db.Shift.Add(new Shift { Id = shift, Code = tag, Name = "[DEMO] Ca trưa" });
                await db.SaveChangesAsync("Insert");

                var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();
                var created = await orders.Create(new OrderCreateRequest
                {
                    CustomerId = customer, DeliveryLocationId = location, MealDate = mealDate,
                    ListLine = [new OrderCreateLineRequest { ContractMealId = meal, ShiftId = shift, Quantity = 120 }],
                });
                created.ResponseCode.Should().Be(200, created.ResponseMessage);
                Guid id = created.Data!.Order!.Id;
                created.Data.Order.OrderNumber.Should().MatchRegex(@"^DH-\d{6}-\d{4}$");
                (await orders.Submit(new OrderActionRequest { Id = id })).ResponseCode.Should().Be(200);
                (await orders.Confirm(new OrderActionRequest { Id = id })).ResponseCode.Should().Be(200);
                var locked = await orders.Lock(new OrderActionRequest { Id = id });
                locked.ResponseCode.Should().Be(200, locked.ResponseMessage);

                db.ChangeTracker.Clear();
                var order = await db.Order.AsNoTracking().SingleAsync(o => o.Id == id);
                order.Status.Should().Be("Locked");
                order.OrderNo.Should().Be(order.OrderNumber);
                order.TotalAmount.Should().Be(120 * 38000m);
                var line = await db.OrderLine.AsNoTracking().SingleAsync(l => l.OrderId == id);
                line.LineAmount.Should().Be(120 * 38000m);
                line.Amount.Should().Be(120 * 38000m);
                await tx.RollbackAsync();
            }

            db.ChangeTracker.Clear();
            (await db.Order.CountAsync()).Should().Be(ordersBefore, "rollback không để lại dữ liệu");
        }
    }
}
