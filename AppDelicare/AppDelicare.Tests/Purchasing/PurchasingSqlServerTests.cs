using AppDelicare.Data.Connection;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Interface;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Purchasing
{
    /// <summary>
    /// T15 – trên SQL Server dev thật (bỏ qua nếu không có DB): đề nghị mua → PO (PO- qua sp_getapplock) → gửi duyệt + duyệt qua engine T12 (XN-)
    /// → phiếu nhập (PH-) → QC DAT_MOT_PHAN → ghi sổ (lô NL-, nhập kho phần đạt, nhiệt độ nhận, sự cố NCC), trong 1 transaction ngoài rồi ROLLBACK
    /// (không để lại dữ liệu). Dữ liệu thử gắn nhãn [DEMO].
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class PurchasingSqlServerTests(ApiFactory factory)
    {
        [SkippableFact]
        public async Task Request_to_po_to_receipt_to_qc_to_stock_on_sql_server_inside_outer_transaction()
        {
            Skip.If(factory.SkipReason != null, factory.SkipReason);
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            var actor = scope.ServiceProvider.GetRequiredService<ICurrentActor>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            Guid admin = await db.User.Where(u => u.UserName == "admin").Select(u => u.Id).SingleAsync();
            int poBefore = await db.PurchaseOrder.CountAsync();
            int lotBefore = await db.MaterialLot.CountAsync();
            int txnBefore = await db.InventoryTransaction.CountAsync();
            int tempBefore = await db.TemperatureLog.CountAsync();
            string tag = "[DEMO]-T15-" + Guid.NewGuid().ToString("N")[..6];
            string supplierCode = "T15" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

            using (actor.BeginScope(ActorInfo.ForUser(admin)))
            await using (var tx = await db.Database.BeginTransactionAsync())
            {
                Guid warehouse = Guid.NewGuid(), kg = Guid.NewGuid(), ingredient = Guid.NewGuid(), supplier = Guid.NewGuid(), pr = Guid.NewGuid();
                db.Warehouse.Add(new Warehouse { Id = warehouse, Code = tag, Name = "[DEMO] Kho T15", Status = "Active" });
                db.UnitMeasure.Add(new UnitMeasure { Id = kg, Code = tag, Name = "[DEMO] kg" });
                db.Ingredient.Add(new Ingredient { Id = ingredient, Code = tag, Name = "[DEMO] Thịt T15", BaseUnitId = kg, BaseUomId = kg, StorageType = "CHILLED" });
                db.Supplier.Add(new Supplier { Id = supplier, Code = supplierCode, Name = "[DEMO] NCC T15", Status = "Active" });
                db.SupplierMaterial.Add(new SupplierMaterial { Id = Guid.NewGuid(), SupplierId = supplier, IngredientId = ingredient, IsPreferred = true, UnitPrice = 100000, PurchaseUomId = kg, Status = "Active" });
                db.PurchaseRequest.Add(new PurchaseRequest { Id = pr, RequestNumber = tag, RequestNo = tag, RequestDate = clock.Now, RequiredDate = clock.Today.AddDays(1), Status = "Draft", Note = "[DEMO]" });
                db.PurchaseRequestLine.Add(new PurchaseRequestLine { Id = Guid.NewGuid(), PurchaseRequestId = pr, IngredientId = ingredient, Quantity = 10, UnitId = kg, UomId = kg });
                await db.SaveChangesAsync("Insert");

                var prService = scope.ServiceProvider.GetRequiredService<IPurchaseRequestService>();
                var poService = scope.ServiceProvider.GetRequiredService<IPurchaseOrderService>();
                var grService = scope.ServiceProvider.GetRequiredService<IGoodsReceiptService>();
                var qcService = scope.ServiceProvider.GetRequiredService<IQcInspectionService>();

                // BE2.3 (D-46): đề nghị Nháp → gửi duyệt → 2 cấp (admin) → Đã duyệt mới lập PO (trên SQL Server: ApprovalStep UNIQUE (yêu cầu, cấp)).
                (await prService.ConvertToPurchaseOrder(new PurchaseRequestConvertRequest { Id = pr })).ResponseCode.Should().Be(400);
                var prSubmitted = await prService.Submit(new PurchaseActionRequest { Id = pr, Note = "[DEMO]" });
                prSubmitted.ResponseCode.Should().Be(200, prSubmitted.ResponseMessage);
                prSubmitted.Data!.PendingApproval!.StepCount.Should().Be(2);
                (await prService.Decide(new PurchaseActionRequest { Id = pr, Note = "[DEMO] cấp 1" }, true)).ResponseCode.Should().Be(200);
                (await prService.GetDetail(pr)).Data!.PendingApproval!.ScreenId.Should().Be("scm-pr-l2");
                (await prService.Decide(new PurchaseActionRequest { Id = pr, Note = "[DEMO] cấp 2" }, true)).ResponseCode.Should().Be(200);
                (await prService.GetDetail(pr)).Data!.PurchaseRequest.Status.Should().Be("Approved");
                var converted = await prService.ConvertToPurchaseOrder(new PurchaseRequestConvertRequest { Id = pr, Note = "[DEMO]" });
                converted.ResponseCode.Should().Be(200, converted.ResponseMessage);
                var poBrief = converted.Data!.ListPurchaseOrder.Single();
                poBrief.PurchaseOrderNumber.Should().MatchRegex(@"^PO-\d{6}-\d{4}$");
                poBrief.TotalAmount.Should().Be(1_000_000m);

                var submitted = await poService.Submit(new PurchaseActionRequest { Id = poBrief.Id });
                submitted.ResponseCode.Should().Be(200, submitted.ResponseMessage);
                submitted.Data!.PurchaseOrder.Status.Should().Be("PendingApproval");
                submitted.Data.PendingApprovalNo.Should().MatchRegex(@"^XN-\d{6}-\d{3}$");
                var approved = await poService.Decide(new PurchaseActionRequest { Id = poBrief.Id, Note = "[DEMO] duyệt" }, true);
                approved.ResponseCode.Should().Be(200, approved.ResponseMessage);

                var detail = (await poService.GetDetail(poBrief.Id)).Data!;
                detail.PurchaseOrder.Status.Should().Be("Approved");
                var created = await grService.Create(new GoodsReceiptCreateRequest
                {
                    PurchaseOrderId = poBrief.Id, WarehouseId = warehouse, Note = "[DEMO]",
                    ListLine = [new GoodsReceiptLineInput { PurchaseOrderLineId = detail.ListLine.Single().Id, ReceivedQuantity = 10, ExpiryDate = clock.Today.AddDays(5), TemperatureC = 4, SupplierLotNumber = "[DEMO]" }],
                });
                created.ResponseCode.Should().Be(200, created.ResponseMessage);
                created.Data!.GoodsReceipt.ReceiptNumber.Should().MatchRegex(@"^PN-\d{6}-\d{4}$");

                var concluded = await qcService.Conclude(new QcInspectionConcludeRequest
                {
                    Id = created.Data.QcInspectionId!.Value,
                    ListLine = [new QcInspectionLineInput { GoodsReceiptLineId = created.Data.ListLine.Single().Id, Result = "DAT_MOT_PHAN", AcceptedQuantity = 8, Remark = "[DEMO] 2 kg dập" }],
                });
                concluded.ResponseCode.Should().Be(200, concluded.ResponseMessage);
                var posted = await grService.Post(new GoodsReceiptPostRequest { Id = created.Data.GoodsReceipt.Id });
                posted.ResponseCode.Should().Be(200, posted.ResponseMessage);
                posted.Data!.SupplierIssueId.Should().NotBeNull();

                db.ChangeTracker.Clear();
                var lot = await db.MaterialLot.AsNoTracking().SingleAsync(l => l.IngredientId == ingredient);
                lot.LotNumber.Should().MatchRegex($@"^LO-\d{{6}}-{supplierCode}-\d{{2}}$");
                lot.QcStatus.Should().Be("DAT_MOT_PHAN");
                var txn = await db.InventoryTransaction.AsNoTracking().SingleAsync(t => t.MaterialLotId == lot.Id);
                txn.QuantityIn.Should().Be(8, "chỉ nhập phần đạt");
                txn.LotId.Should().Be(lot.Id);
                (await db.InventoryBalance.AsNoTracking().Where(b => b.MaterialLotId == lot.Id).SumAsync(b => b.AvailableQuantity)).Should().Be(8);
                (await db.TemperatureLog.AsNoTracking().CountAsync(t => t.MaterialLotId == lot.Id && t.MeasurementType == "RECEIVING")).Should().Be(1);
                (await db.PurchaseOrder.AsNoTracking().SingleAsync(p => p.Id == poBrief.Id)).Status.Should().Be("PartiallyReceived");
                var approval = await db.ApprovalRequest.AsNoTracking().SingleAsync(a => a.BusinessId == poBrief.Id);
                approval.RequestStatus.Should().Be("Approved");
                (await db.ApprovalDecision.AsNoTracking().CountAsync(d => d.ApprovalRequestId == approval.Id && d.DecisionCode == "APPROVE")).Should().Be(1);
                await tx.RollbackAsync();
            }

            db.ChangeTracker.Clear();
            (await db.PurchaseOrder.CountAsync()).Should().Be(poBefore, "rollback không để lại dữ liệu");
            (await db.MaterialLot.CountAsync()).Should().Be(lotBefore);
            (await db.InventoryTransaction.CountAsync()).Should().Be(txnBefore);
            (await db.TemperatureLog.CountAsync()).Should().Be(tempBefore);
        }
    }
}
