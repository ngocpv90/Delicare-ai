using AppDelicare.Data.Connection;
using AppDelicare.Data.Share;
using AppDelicare.Services.Implement;
using AppDelicare.Services.Interface;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Ledger
{
    /// <summary>
    /// T08 – sổ cái kho trên SQL Server thật (kiểu cột, cột deprecated NOT NULL, transaction): chạy trong 1 transaction rồi ROLLBACK
    /// vì sổ chỉ ghi thêm không xóa được. Không có DB dev thì Skip.
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class LedgerSqlServerTests(ApiFactory factory)
    {
        [SkippableFact]
        public async Task Post_and_reverse_on_sql_server_roll_back_cleanly()
        {
            Skip.If(factory.SkipReason != null, factory.SkipReason);
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            var actor = scope.ServiceProvider.GetRequiredService<ICurrentActor>();
            Guid admin = await db.User.Where(u => u.UserName == "admin").Select(u => u.Id).SingleAsync();
            var ingredient = Guid.NewGuid();
            int before = await db.InventoryTransaction.CountAsync();

            using (actor.BeginScope(ActorInfo.ForUser(admin)))
            await using (var tx = await db.Database.BeginTransactionAsync())
            {
                var ledger = new InventoryLedgerService(db);
                var common = new { Warehouse = Guid.NewGuid(), Unit = Guid.NewGuid() };
                var receipt = await ledger.PostAsync(new InventoryPostRequest
                {
                    TransactionType = "RECEIPT", TransactionNumber = "[DEMO]-T08", WarehouseId = common.Warehouse, IngredientId = ingredient,
                    UnitId = common.Unit, QuantityIn = 12.3456m, UnitCost = 118000,
                });
                await ledger.ReverseAsync(receipt.Id, "[DEMO] T08 rollback test");

                db.ChangeTracker.Clear();
                var balance = await db.InventoryBalance.AsNoTracking().SingleAsync(b => b.IngredientId == ingredient);
                balance.OnHandQuantity.Should().Be(0);
                (await db.InventoryTransaction.CountAsync(x => x.IngredientId == ingredient)).Should().Be(2);
                var stored = await db.InventoryTransaction.AsNoTracking().SingleAsync(x => x.Id == receipt.Id);
                stored.Quantity.Should().Be(12.3456m);
                stored.UomId.Should().Be(common.Unit);
                await tx.RollbackAsync();
            }

            db.ChangeTracker.Clear();
            (await db.InventoryTransaction.CountAsync()).Should().Be(before, "rollback không để lại dòng sổ");
        }
    }
}
