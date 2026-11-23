using AppDelicare.Data.Connection;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Interface;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Production
{
    /// <summary>
    /// T17 – trên SQL Server dev thật (bỏ qua nếu không có DB): kế hoạch → duyệt → phát hành (PH- + ME- qua sp_getapplock) → xuất NL cho mẻ (FEFO, bỏ lô hết hạn)
    /// → bắt đầu → nhiệt độ lõi → lưu mẫu → xong → sản lượng → đạt QC → đóng gói, trong 1 transaction ngoài rồi ROLLBACK (không để lại dữ liệu). Dữ liệu thử gắn nhãn [DEMO].
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class ProductionSqlServerTests(ApiFactory factory)
    {
        [SkippableFact]
        public async Task Plan_to_batch_to_issue_to_temperature_sample_output_and_pack_on_sql_server_inside_outer_transaction()
        {
            Skip.If(factory.SkipReason != null, factory.SkipReason);
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            var actor = scope.ServiceProvider.GetRequiredService<ICurrentActor>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            Guid admin = await db.User.Where(u => u.UserName == "admin").Select(u => u.Id).SingleAsync();
            int batchBefore = await db.ProductionBatch.CountAsync();
            int orderBefore = await db.ProductionOrder.CountAsync();
            int txnBefore = await db.InventoryTransaction.CountAsync();
            int tempBefore = await db.TemperatureLog.CountAsync();
            int resultBefore = await db.FoodSafetyChecklistResult.CountAsync();
            string tag = "[DEMO]-T17-" + Guid.NewGuid().ToString("N")[..6];
            string shiftCode = "T17" + Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();
            DateTime day = clock.Today.AddDays(1);

            using (actor.BeginScope(ActorInfo.ForUser(admin)))
            await using (var tx = await db.Database.BeginTransactionAsync())
            {
                Guid shift = Guid.NewGuid(), warehouse = Guid.NewGuid(), kg = Guid.NewGuid(), ingredient = Guid.NewGuid(), dish = Guid.NewGuid(), plan = Guid.NewGuid();
                Guid goodLot = Guid.NewGuid(), expiredLot = Guid.NewGuid();
                db.Shift.Add(new Shift { Id = shift, Code = shiftCode, Name = "[DEMO] Ca T17", StartTime = new TimeSpan(10, 30, 0), EndTime = new TimeSpan(13, 0, 0), Status = "Active" });
                db.Warehouse.Add(new Warehouse { Id = warehouse, Code = tag, Name = "[DEMO] Kho T17", Status = "Active" });
                db.UnitMeasure.Add(new UnitMeasure { Id = kg, Code = tag, Name = "[DEMO] kg" });
                db.Ingredient.Add(new Ingredient { Id = ingredient, Code = tag, Name = "[DEMO] Thịt T17", BaseUnitId = kg, BaseUomId = kg });
                db.Dish.Add(new Dish { Id = dish, Code = tag, Name = "[DEMO] Thịt kho T17", Status = "Active" });
                db.Recipe.Add(new Recipe
                {
                    Id = Guid.NewGuid(), DishId = dish, VersionNo = 1, VersionCode = "v1", Status = "Locked", LockedAt = clock.Now, IsActive = true, StandardOutputQty = 100,
                    SnapshotJson = $"[{{\"IngredientId\":\"{ingredient}\",\"Quantity\":10,\"UnitId\":\"{kg}\",\"WastePercent\":0,\"SequenceNo\":1}}]", Note = "[DEMO]",
                });
                db.ProductionPlan.Add(new ProductionPlan { Id = plan, PlanNumber = tag, PlanNo = tag, ProductionDate = day, PlanDate = clock.Today, Status = "Draft", Note = "[DEMO]" });
                db.ProductionPlanLine.Add(new ProductionPlanLine { Id = Guid.NewGuid(), ProductionPlanId = plan, DishId = dish, PlannedQuantity = 50, PlannedQty = 50, Status = "Draft" });
                // T17.1: phát hành chia kế hoạch theo ca của đơn Đã chốt (dòng đơn × thực đơn) → đơn 50 suất ca test.
                Guid menu = Guid.NewGuid(), customer = Guid.NewGuid(), order = Guid.NewGuid();
                db.Menu.Add(new Model.Entity.Menu { Id = menu, Code = tag, Name = "[DEMO] Thực đơn T17", VersionNo = 1, Status = "Published", EffectiveFrom = clock.Today });
                db.MenuLine.Add(new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = dish, MealType = "TRUA", DayOfWeek = day.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)day.DayOfWeek, SequenceNo = 1 });
                db.Customer.Add(new Customer { Id = customer, Code = tag, Name = "[DEMO] Khách T17", CIF = tag, CustomerType = "FACTORY", DisplayName = "[DEMO] Khách T17", Status = "Active" });
                db.Order.Add(new Order { Id = order, OrderNumber = tag, OrderNo = tag, CustomerId = customer, OrderDate = clock.Today, MealDate = day, DeliveryDate = day, Status = "Locked", TotalQuantity = 50, Note = "[DEMO]" });
                db.OrderLine.Add(new OrderLine { Id = Guid.NewGuid(), OrderId = order, MenuId = menu, MealType = "TRUA", ShiftId = shift, Quantity = 50 });
                db.MaterialLot.AddRange(
                    new MaterialLot { Id = goodLot, IngredientId = ingredient, LotNumber = tag + "-A", LotNo = tag + "-A", ExpiryDate = day.AddDays(3), ReceivedDate = clock.Today, QcStatus = "DAT", QualityStatus = "DAT", Status = "Available" },
                    new MaterialLot { Id = expiredLot, IngredientId = ingredient, LotNumber = tag + "-X", LotNo = tag + "-X", ExpiryDate = clock.Today.AddDays(-1), ReceivedDate = clock.Today.AddDays(-9), QcStatus = "DAT", QualityStatus = "DAT", Status = "Available" });
                if (!await db.FoodSafetyChecklistItem.AnyAsync(i => i.ItemCode == "SAMPLE_RETAINED" && !i.IsDeleted))
                {
                    Guid checklist = Guid.NewGuid();
                    db.FoodSafetyChecklist.Add(new FoodSafetyChecklist { Id = checklist, Code = "PRD_BATCH", Name = "[DEMO] Kiểm soát mẻ nấu", VersionNo = 1, Status = "Active" });
                    db.FoodSafetyChecklistItem.Add(new FoodSafetyChecklistItem { Id = Guid.NewGuid(), ChecklistId = checklist, ItemCode = "SAMPLE_RETAINED", ItemName = "[DEMO] Lưu mẫu", SortOrder = 1, IsCritical = true, IsRequired = true });
                }
                await db.SaveChangesAsync("Insert");
                var ledger = scope.ServiceProvider.GetRequiredService<IInventoryLedgerService>();
                foreach (var (lot, qty) in new[] { (expiredLot, 30m), (goodLot, 8m) })
                {
                    await ledger.PostAsync(new InventoryPostRequest
                    {
                        TransactionType = "RECEIPT", TransactionNumber = tag, WarehouseId = warehouse, IngredientId = ingredient, MaterialLotId = lot, UnitId = kg, QuantityIn = qty, Note = "[DEMO]",
                    });
                }

                var planService = scope.ServiceProvider.GetRequiredService<IProductionPlanService>();
                var batchService = scope.ServiceProvider.GetRequiredService<IProductionBatchService>();
                (await planService.Approve(new ProductionActionRequest { Id = plan })).ResponseCode.Should().Be(200);
                var released = await planService.Release(new ProductionPlanReleaseRequest { Id = plan, ShiftId = shift, DispatchTime = "10:30", Note = "[DEMO]" });
                released.ResponseCode.Should().Be(200, released.ResponseMessage);
                var batchBrief = released.Data!.ListBatch.Single();
                batchBrief.BatchNumber.Should().MatchRegex($@"^ME-{day:yyMMdd}-{shiftCode}-\d{{2}}$");
                batchBrief.PlannedStart.Should().Be(day.AddHours(9), "xuất bếp 10:30 − 30′ đóng gói − 60′ mẻ");
                released.Data.ListOrder.Single().ProductionOrderNumber.Should().MatchRegex(@"^LS-\d{6}-\d{4}$");
                released.Data.ProductionPlan.Status.Should().Be("Released");
                released.Data.ListShift.Should().ContainSingle(x => x.ShiftId == shift && x.Released && x.Portions == 50);
                (await db.Order.AsNoTracking().SingleAsync(o => o.Id == order)).Status.Should().Be("InProduction");

                // Cần 5 kg (10 kg / 100 suất × 50 phần): lô hết hạn 30 kg bị bỏ, lấy 5 / 8 kg của lô còn hạn.
                var issue = await batchService.CreateMaterialIssue(new ProductionBatchIssueRequest { Id = batchBrief.Id, WarehouseId = warehouse, Post = true, Note = "[DEMO]" });
                issue.ResponseCode.Should().Be(200, issue.ResponseMessage);
                issue.Data!.ListLine.Should().ContainSingle().Which.MaterialLotId.Should().Be(goodLot);
                issue.Data.ListLine[0].Quantity.Should().Be(5);

                (await batchService.Start(new ProductionBatchStartRequest { Id = batchBrief.Id })).ResponseCode.Should().Be(200);
                (await batchService.RecordCoreTemperature(new ProductionBatchTemperatureRequest { Id = batchBrief.Id, TemperatureC = 74.5m, DeviceCode = "[DEMO]" })).ResponseCode.Should().Be(200);
                (await batchService.RecordSampleRetained(new ProductionBatchSampleRequest { Id = batchBrief.Id, ActualValue = "[DEMO] mẫu 150 g" })).ResponseCode.Should().Be(200);
                (await batchService.Finish(new ProductionBatchFinishRequest { Id = batchBrief.Id, ActualQuantity = 50 })).ResponseCode.Should().Be(200);
                (await batchService.RecordOutput(new ProductionBatchOutputRequest { Id = batchBrief.Id, ProducedQuantity = 50, RejectedQuantity = 1, Note = "[DEMO]" })).ResponseCode.Should().Be(200);
                var passed = await batchService.Release(new ProductionActionRequest { Id = batchBrief.Id });
                passed.ResponseCode.Should().Be(200, passed.ResponseMessage);
                var packed = await batchService.Pack(new ProductionActionRequest { Id = batchBrief.Id });
                packed.ResponseCode.Should().Be(200, packed.ResponseMessage);

                db.ChangeTracker.Clear();
                var batch = await db.ProductionBatch.AsNoTracking().SingleAsync(b => b.Id == batchBrief.Id);
                batch.Status.Should().Be("Packed");
                batch.AcceptedQty.Should().Be(49);
                (await db.ProductionOrder.AsNoTracking().SingleAsync(o => o.Id == batch.ProductionOrderId)).Status.Should().Be("Completed");
                (await db.ProductionPlan.AsNoTracking().SingleAsync(p => p.Id == plan)).Status.Should().Be("Completed");
                (await db.MaterialIssue.AsNoTracking().SingleAsync(i => i.ProductionOrderId == batch.ProductionOrderId && (i.Note == null || !i.Note.StartsWith("[XUAT_CA")))).Status.Should().Be("Issued");
                (await db.InventoryBalance.AsNoTracking().Where(b => b.MaterialLotId == goodLot).SumAsync(b => b.OnHandQuantity)).Should().Be(3);
                (await db.InventoryBalance.AsNoTracking().Where(b => b.MaterialLotId == expiredLot).SumAsync(b => b.OnHandQuantity)).Should().Be(30, "lô hết hạn không bị xuất");
                (await db.TemperatureLog.AsNoTracking().SingleAsync(t => t.ProductionBatchId == batch.Id)).IsWithinRange.Should().BeTrue();
                (await db.FoodSafetyChecklistResult.AsNoTracking().CountAsync(r => r.ProductionBatchId == batch.Id)).Should().Be(1);
                (await db.ProductionOutput.AsNoTracking().SingleAsync(o => o.ProductionBatchId == batch.Id)).AcceptedQuantity.Should().Be(49);
                await tx.RollbackAsync();
            }

            db.ChangeTracker.Clear();
            (await db.ProductionBatch.CountAsync()).Should().Be(batchBefore, "rollback không để lại dữ liệu");
            (await db.ProductionOrder.CountAsync()).Should().Be(orderBefore);
            (await db.InventoryTransaction.CountAsync()).Should().Be(txnBefore);
            (await db.TemperatureLog.CountAsync()).Should().Be(tempBefore);
            (await db.FoodSafetyChecklistResult.CountAsync()).Should().Be(resultBefore);
        }
    }
}
