using AppDelicare.Data.Connection;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Interface;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Approval
{
    /// <summary>
    /// BE2.3 – trên SQL Server dev thật (bỏ qua nếu không có DB), 1 transaction ngoài rồi ROLLBACK ([DEMO], không để lại dữ liệu):
    /// điều chỉnh tồn → STOCK_ADJUSTMENT → duyệt → bút toán (số PH- qua sp_getapplock); kiểm kê lệch 20% → Submit → 2 cấp → ghi sổ; đề nghị mua → cấp 1 → từ chối cấp 2
    /// → về Nháp → gửi lại → 2 cấp → Đã duyệt (ràng buộc UNIQUE ApprovalStep (yêu cầu, cấp) và XN- không trùng giữa các lần gửi).
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class MultiLevelApprovalSqlServerTests(ApiFactory factory)
    {
        [SkippableFact]
        public async Task Adjustment_stock_count_and_purchase_request_multi_level_on_sql_server_inside_outer_transaction()
        {
            Skip.If(factory.SkipReason != null, factory.SkipReason);
            using var scope = factory.Services.CreateScope();
            var sp = scope.ServiceProvider;
            var db = sp.GetRequiredService<DatabaseContext>();
            var actor = sp.GetRequiredService<ICurrentActor>();
            var clock = sp.GetRequiredService<IClock>();
            Guid admin = await db.User.Where(u => u.UserName == "admin").Select(u => u.Id).SingleAsync();
            int approvalsBefore = await db.ApprovalRequest.CountAsync(), stepsBefore = await db.ApprovalStep.CountAsync(), txnBefore = await db.InventoryTransaction.CountAsync();
            string tag = "[DEMO]-BE23-" + Guid.NewGuid().ToString("N")[..6];

            using (actor.BeginScope(ActorInfo.ForUser(admin)))
            await using (var tx = await db.Database.BeginTransactionAsync())
            {
                Guid warehouse = Guid.NewGuid(), kg = Guid.NewGuid(), ingredient = Guid.NewGuid(), lot = Guid.NewGuid(), pr = Guid.NewGuid();
                db.Warehouse.Add(new Warehouse { Id = warehouse, Code = tag, Name = "[DEMO] Kho BE2.3", Status = "Active" });
                db.UnitMeasure.Add(new UnitMeasure { Id = kg, Code = tag, Name = "[DEMO] kg" });
                db.Ingredient.Add(new Ingredient { Id = ingredient, Code = tag, Name = "[DEMO] Gạo BE2.3", BaseUnitId = kg, BaseUomId = kg });
                db.MaterialLot.Add(new MaterialLot { Id = lot, IngredientId = ingredient, LotNumber = tag, LotNo = tag, ExpiryDate = clock.Today.AddDays(90), QcStatus = "DAT", UnitCost = 15000, Note = "[DEMO]" });
                db.PurchaseRequest.Add(new PurchaseRequest { Id = pr, RequestNumber = tag, RequestNo = tag, RequestDate = clock.Now, RequiredDate = clock.Today.AddDays(2), Status = "Draft", Note = "[DEMO]" });
                db.PurchaseRequestLine.Add(new PurchaseRequestLine { Id = Guid.NewGuid(), PurchaseRequestId = pr, IngredientId = ingredient, Quantity = 30, UnitId = kg, UomId = kg });
                await db.SaveChangesAsync("Insert");
                var approvals = sp.GetRequiredService<IApprovalRequestService>();

                // 1) Điều chỉnh tồn: gửi duyệt → chưa có bút toán → duyệt → bút toán ADJUSTMENT ReferenceId = khóa nối.
                var inventory = sp.GetRequiredService<IInventoryTransactionService>();
                var adjusted = await inventory.Adjust(new InventoryAdjustRequest { WarehouseId = warehouse, IngredientId = ingredient, MaterialLotId = lot, Quantity = 100, Reason = "[DEMO] Tồn đầu kỳ" });
                adjusted.ResponseCode.Should().Be(200, adjusted.ResponseMessage);
                adjusted.Data!.PendingApproval.Should().BeTrue();
                Guid reference = adjusted.Data.ReferenceId!.Value;
                (await db.InventoryTransaction.CountAsync(t => t.ReferenceId == reference)).Should().Be(0);
                (await approvals.Decide(new ApprovalDecideRequest { Id = adjusted.Data.ApprovalRequestId!.Value, Approve = true, Note = "[DEMO]" })).ResponseCode.Should().Be(200);
                db.ChangeTracker.Clear();
                var txn = await db.InventoryTransaction.AsNoTracking().SingleAsync(t => t.ReferenceId == reference);
                txn.QuantityIn.Should().Be(100);
                txn.TransactionNumber.Should().MatchRegex(@"^PH-\d{6}-\d{4}$");

                // 2) Kiểm kê: đếm 80 (lệch 20% > 2%) → Post chặn → Submit → cấp 1 → cấp 2 → ghi sổ.
                var counts = sp.GetRequiredService<IStockCountService>();
                var created = await counts.Create(new StockCountCreateRequest { WarehouseId = warehouse, IngredientId = ingredient, Note = "[DEMO]" });
                created.ResponseCode.Should().Be(200, created.ResponseMessage);
                Guid count = created.Data!.StockCount!.Id;
                var recorded = await counts.RecordCounts(new StockCountRecordRequest
                {
                    Id = count, ListLine = [new StockCountRecordLineRequest { Id = created.Data.ListLine.Single().Id, PhysicalQuantity = 80, VarianceReason = "[DEMO] Mất mát" }],
                });
                recorded.ResponseCode.Should().Be(200, recorded.ResponseMessage);
                (await counts.Post(new InventoryDocumentActionRequest { Id = count })).ResponseCode.Should().Be(400);
                var submitted = await counts.Submit(new InventoryDocumentActionRequest { Id = count, Note = "[DEMO]" });
                submitted.ResponseCode.Should().Be(200, submitted.ResponseMessage);
                submitted.Data!.PendingApproval!.StepCount.Should().Be(2);
                (await counts.Decide(new InventoryDocumentActionRequest { Id = count, Note = "[DEMO] cấp 1" }, true)).ResponseCode.Should().Be(200);
                (await counts.Post(new InventoryDocumentActionRequest { Id = count })).ResponseCode.Should().Be(400, "mới duyệt cấp 1");
                (await counts.Decide(new InventoryDocumentActionRequest { Id = count, Note = "[DEMO] cấp 2" }, true)).ResponseCode.Should().Be(200);
                var posted = await counts.Post(new InventoryDocumentActionRequest { Id = count });
                posted.ResponseCode.Should().Be(200, posted.ResponseMessage);
                posted.Data!.StockCount!.Status.Should().Be("Posted");
                posted.Data.ListTransaction.Should().ContainSingle(t => t.QuantityOut == 20);

                // 3) Đề nghị mua: cấp 1 duyệt → cấp 2 từ chối → Nháp → gửi lại (yêu cầu mới, bước 1–2 mới) → 2 cấp → Đã duyệt.
                var requests = sp.GetRequiredService<IPurchaseRequestService>();
                (await requests.Submit(new PurchaseActionRequest { Id = pr })).ResponseCode.Should().Be(200);
                (await requests.Decide(new PurchaseActionRequest { Id = pr, Note = "[DEMO] cấp 1" }, true)).ResponseCode.Should().Be(200);
                (await requests.Decide(new PurchaseActionRequest { Id = pr, Note = "[DEMO] cấp 2 từ chối" }, false)).ResponseCode.Should().Be(200);
                (await requests.GetDetail(pr)).Data!.PurchaseRequest.Status.Should().Be("Draft");
                (await requests.Submit(new PurchaseActionRequest { Id = pr })).ResponseCode.Should().Be(200);
                (await requests.Decide(new PurchaseActionRequest { Id = pr, Note = "[DEMO] cấp 1" }, true)).ResponseCode.Should().Be(200);
                (await requests.Decide(new PurchaseActionRequest { Id = pr, Note = "[DEMO] cấp 2" }, true)).ResponseCode.Should().Be(200);
                (await requests.GetDetail(pr)).Data!.PurchaseRequest.Status.Should().Be("Approved");
                var prRequests = await db.ApprovalRequest.AsNoTracking().Where(a => a.BusinessId == pr).OrderBy(a => a.RequestNo).ToListAsync();
                prRequests.Select(a => a.RequestStatus).Should().BeEquivalentTo(["Rejected", "Approved"]);
                prRequests.Select(a => a.RequestNo).Distinct().Should().HaveCount(2);

                await tx.RollbackAsync();
            }

            db.ChangeTracker.Clear();
            (await db.ApprovalRequest.CountAsync()).Should().Be(approvalsBefore, "rollback không để lại dữ liệu");
            (await db.ApprovalStep.CountAsync()).Should().Be(stepsBefore);
            (await db.InventoryTransaction.CountAsync()).Should().Be(txnBefore);
        }
    }
}
