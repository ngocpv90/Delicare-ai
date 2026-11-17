using AppDelicare.Data.Connection;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Interface;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Inventory
{
    /// <summary>
    /// T16 – trên SQL Server dev thật (bỏ qua nếu không có DB): nhập 2 lô qua sổ cái → tạo phiếu xuất (số PH- qua sp_getapplock, FEFO tự chọn lô)
    /// → ghi sổ (bút toán ISSUE, tồn giảm) → lô hết hạn bị chặn, trong 1 transaction ngoài rồi ROLLBACK (không để lại dữ liệu). Dữ liệu thử gắn [DEMO].
    /// Sau rollback gọi SP <c>uspInventoryTransaction_GetLedger</c> (phải đã chạy trên DB dev).
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class InventorySqlServerTests(ApiFactory factory)
    {
        [SkippableFact]
        public async Task Issue_create_and_post_writes_out_entries_on_sql_server_inside_outer_transaction()
        {
            Skip.If(factory.SkipReason != null, factory.SkipReason);
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            var actor = scope.ServiceProvider.GetRequiredService<ICurrentActor>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            Guid admin = await db.User.Where(u => u.UserName == "admin").Select(u => u.Id).SingleAsync();
            int txnsBefore = await db.InventoryTransaction.CountAsync();
            string tag = "[DEMO]-T16-" + Guid.NewGuid().ToString("N")[..6];
            Guid warehouse = Guid.NewGuid(), unit = Guid.NewGuid(), ingredient = Guid.NewGuid(), soon = Guid.NewGuid(), later = Guid.NewGuid(), expired = Guid.NewGuid();

            using (actor.BeginScope(ActorInfo.ForUser(admin)))
            await using (var tx = await db.Database.BeginTransactionAsync())
            {
                db.Warehouse.Add(new Warehouse { Id = warehouse, Code = tag, Name = "[DEMO] Kho T16", Status = "Active" });
                db.UnitMeasure.Add(new UnitMeasure { Id = unit, Code = tag, Name = "[DEMO] kg" });
                db.Ingredient.Add(new Ingredient { Id = ingredient, Code = tag, Name = "[DEMO] Thịt heo", BaseUnitId = unit, BaseUomId = unit, IsExpiryManaged = true });
                foreach (var (lotId, days) in new[] { (soon, 2), (later, 9), (expired, -1) })
                {
                    db.MaterialLot.Add(new MaterialLot
                    {
                        Id = lotId, IngredientId = ingredient, LotNumber = $"{tag}-{days}", LotNo = $"{tag}-{days}", ExpiryDate = clock.Today.AddDays(days),
                        ReceivedDate = clock.Today.AddDays(-1), QcStatus = "DAT", QualityStatus = "DAT", Status = "Available", UnitCost = 120000,
                    });
                }
                await db.SaveChangesAsync("Insert");
                var ledger = scope.ServiceProvider.GetRequiredService<IInventoryLedgerService>();
                foreach (var lot in new[] { soon, later, expired })
                {
                    await ledger.PostAsync(new InventoryPostRequest
                    {
                        TransactionType = "RECEIPT", TransactionNumber = tag, WarehouseId = warehouse, IngredientId = ingredient, MaterialLotId = lot,
                        UnitId = unit, QuantityIn = 20, UnitCost = 120000, Reason = "[DEMO] nhập thử",
                    });
                }

                var issues = scope.ServiceProvider.GetRequiredService<IMaterialIssueService>();
                (await issues.Create(new MaterialIssueCreateRequest
                {
                    WarehouseId = warehouse, ListLine = [new MaterialIssueCreateLineRequest { IngredientId = ingredient, MaterialLotId = expired, Quantity = 1 }],
                })).ResponseCode.Should().Be(400, "lô hết hạn bị chặn");

                var created = await issues.Create(new MaterialIssueCreateRequest
                {
                    WarehouseId = warehouse, Note = "[DEMO] xuất thử T16",
                    ListLine = [new MaterialIssueCreateLineRequest { IngredientId = ingredient, Quantity = 25 }],
                });
                created.ResponseCode.Should().Be(200, created.ResponseMessage);
                Guid id = created.Data!.MaterialIssue!.Id;
                created.Data.MaterialIssue.IssueNumber.Should().MatchRegex(@"^PH-\d{6}-\d{4}$");
                created.Data.ListLine.Select(l => (l.MaterialLotId, l.Quantity)).Should().BeEquivalentTo([(soon, 20m), (later, 5m)], "FEFO: lô hạn gần trước, bỏ lô hết hạn");

                var posted = await issues.Post(new InventoryDocumentActionRequest { Id = id });
                posted.ResponseCode.Should().Be(200, posted.ResponseMessage);
                posted.Data!.ListTransaction.Should().HaveCount(2).And.OnlyContain(t => t.TransactionType == "ISSUE" && t.ReferenceId == id && t.TransactionNo == t.TransactionNumber);

                db.ChangeTracker.Clear();
                var balances = await db.InventoryBalance.AsNoTracking().Where(b => b.IngredientId == ingredient).ToListAsync();
                balances.Single(b => b.MaterialLotId == soon).OnHandQuantity.Should().Be(0);
                balances.Single(b => b.MaterialLotId == later).OnHandQuantity.Should().Be(15);
                balances.Single(b => b.MaterialLotId == later).OnHandQty.Should().Be(15, "ghi song song cột cũ");
                (await db.MaterialIssueLine.AsNoTracking().Where(l => l.MaterialIssueId == id).ToListAsync()).Should().OnlyContain(l => l.LotId == l.MaterialLotId && l.UomId == l.UnitId);

                var again = await issues.Create(new MaterialIssueCreateRequest
                {
                    WarehouseId = warehouse, ListLine = [new MaterialIssueCreateLineRequest { IngredientId = ingredient, Quantity = 16 }],
                });
                again.ResponseCode.Should().Be(400, "chỉ còn 15 xuất được");
                await tx.RollbackAsync();
            }

            db.ChangeTracker.Clear();
            (await db.InventoryTransaction.CountAsync()).Should().Be(txnsBefore, "rollback không để lại dữ liệu");
            var txnService = scope.ServiceProvider.GetRequiredService<IInventoryTransactionService>();
            var ledgerList = await txnService.GetLedger(new InventoryLedgerSearchRequest { IngredientId = ingredient, FromDate = clock.Today.AddDays(-1), ToDate = clock.Today });
            ledgerList.ResponseCode.Should().Be(200);
            ledgerList.ListData.Should().BeEmpty();
        }
    }
}
