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
    /// T08 â€“ sá»• cÃ¡i kho trÃªn SQL Server tháº­t (kiá»ƒu cá»™t, cá»™t deprecated NOT NULL, transaction): cháº¡y trong 1 transaction rá»“i ROLLBACK
    /// vÃ¬ sá»• chá»‰ ghi thÃªm khÃ´ng xÃ³a Ä‘Æ°á»£c. KhÃ´ng cÃ³ DB dev thÃ¬ Skip.
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
            (await db.InventoryTransaction.CountAsync()).Should().Be(before, "rollback khÃ´ng Ä‘á»ƒ láº¡i dÃ²ng sá»•");
        }

        /// <summary>
        /// RÃ  soÃ¡t BE 2026-11-17: 2 bÃºt toÃ¡n cÃ¹ng dÃ²ng tá»“n cháº¡y Láº¦N LÆ¯á»¢T (khÃ³a sp_getapplock theo dÃ²ng tá»“n) â€“ bÃºt toÃ¡n 2 chá» bÃºt toÃ¡n 1
        /// káº¿t thÃºc transaction rá»“i má»›i Ä‘á»c sá»‘ tá»“n (trÆ°á»›c Ä‘Ã¢y cÃ¹ng Ä‘á»c 1 sá»‘ rá»“i ghi Ä‘Ã¨ nhau). Cáº£ 2 transaction Ä‘á»u rollback â€“ khÃ´ng Ä‘á»ƒ láº¡i dá»¯ liá»‡u.
        /// </summary>
        [SkippableFact]
        public async Task Concurrent_postings_on_same_balance_row_run_one_after_another()
        {
            Skip.If(factory.SkipReason != null, factory.SkipReason);
            using var scopeA = factory.Services.CreateScope();
            using var scopeB = factory.Services.CreateScope();
            var dbA = scopeA.ServiceProvider.GetRequiredService<DatabaseContext>();
            var dbB = scopeB.ServiceProvider.GetRequiredService<DatabaseContext>();
            var actorA = scopeA.ServiceProvider.GetRequiredService<ICurrentActor>();
            var actorB = scopeB.ServiceProvider.GetRequiredService<ICurrentActor>();
            Guid admin = await dbA.User.Where(u => u.UserName == "admin").Select(u => u.Id).SingleAsync();
            var key = new { Warehouse = Guid.NewGuid(), Ingredient = Guid.NewGuid(), Unit = Guid.NewGuid() };
            InventoryPostRequest Receipt(string no) => new()
            {
                TransactionType = "RECEIPT", TransactionNumber = no, WarehouseId = key.Warehouse, IngredientId = key.Ingredient, UnitId = key.Unit, QuantityIn = 5,
            };

            using (actorA.BeginScope(ActorInfo.ForUser(admin)))
            using (actorB.BeginScope(ActorInfo.ForUser(admin)))
            await using (var txA = await dbA.Database.BeginTransactionAsync())
            await using (var txB = await dbB.Database.BeginTransactionAsync())
            {
                // A chá»‰ giá»¯ khÃ³a dÃ²ng tá»“n (khÃ´ng ghi gÃ¬) â†’ B bá»‹ cháº·n Ä‘Ãºng bá»Ÿi khÃ³a sp_getapplock, khÃ´ng pháº£i khÃ³a dÃ²ng cá»§a báº£ng.
                await DbLocks.BalanceAsync(dbA, key.Warehouse, null, key.Ingredient, null);
                var postB = new InventoryLedgerService(dbB).PostAsync(Receipt("[DEMO]-REVIEW-B"));
                (await Task.WhenAny(postB, Task.Delay(1500))).Should().NotBeSameAs(postB, "bÃºt toÃ¡n B pháº£i chá» khÃ³a dÃ²ng tá»“n cá»§a A");

                await txA.RollbackAsync();
                await postB.WaitAsync(TimeSpan.FromSeconds(15));
                dbB.ChangeTracker.Clear();
                (await dbB.InventoryBalance.AsNoTracking().Where(b => b.IngredientId == key.Ingredient).Select(b => b.OnHandQuantity).SingleAsync())
                    .Should().Be(5, "B Ä‘á»c sá»‘ tá»“n SAU khi A káº¿t thÃºc (A Ä‘Ã£ rollback) â€“ khÃ´ng cá»™ng trÃªn sá»‘ cÅ©");
                await txB.RollbackAsync();
            }
        }
    }
}
