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
    /// BE2.4 – trên SQL Server dev thật (bỏ qua nếu không có DB), trong 1 transaction ngoài rồi ROLLBACK: hủy tồn lô hết hạn (WASTE) → phát hành (giữ chỗ FEFO trên lô còn hạn)
    /// → xuất NL cho mẻ (dùng phần đã giữ) → công đoạn Sơ chế / Nấu / Chia suất → đạt QC → đóng gói → đóng lệnh (chênh lệch tiêu hao + giá thành thực tế, giải phóng giữ chỗ)
    /// → giá vốn suất. Dữ liệu thử gắn nhãn [DEMO].
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class InventoryProductionSqlServerTests(ApiFactory factory)
    {
        [SkippableFact]
        public async Task Expiry_reservation_stages_close_and_costs_on_sql_server_inside_outer_transaction()
        {
            Skip.If(factory.SkipReason != null, factory.SkipReason);
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            var actor = scope.ServiceProvider.GetRequiredService<ICurrentActor>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            Guid admin = await db.User.Where(u => u.UserName == "admin").Select(u => u.Id).SingleAsync();
            int reservationBefore = await db.MaterialReservation.CountAsync();
            int costBefore = await db.ActualCost.CountAsync();
            int varianceBefore = await db.MaterialConsumptionVariance.CountAsync();
            int mealBefore = await db.MealCost.CountAsync();
            int txnBefore = await db.InventoryTransaction.CountAsync();
            int resultBefore = await db.FoodSafetyChecklistResult.CountAsync();
            string tag = "[DEMO]-B24-" + Guid.NewGuid().ToString("N")[..6];
            string shiftCode = "B24" + Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();
            DateTime day = clock.Today.AddDays(1);

            using (actor.BeginScope(ActorInfo.ForUser(admin)))
            await using (var tx = await db.Database.BeginTransactionAsync())
            {
                Guid shift = Guid.NewGuid(), warehouse = Guid.NewGuid(), kg = Guid.NewGuid(), ingredient = Guid.NewGuid(), dish = Guid.NewGuid(), plan = Guid.NewGuid();
                Guid goodLot = Guid.NewGuid(), expiredLot = Guid.NewGuid(), menu = Guid.NewGuid(), customer = Guid.NewGuid(), order = Guid.NewGuid();
                db.Shift.Add(new Shift { Id = shift, Code = shiftCode, Name = "[DEMO] Ca B24", StartTime = new TimeSpan(10, 30, 0), EndTime = new TimeSpan(13, 0, 0), Status = "Active" });
                db.Warehouse.Add(new Warehouse { Id = warehouse, Code = tag, Name = "[DEMO] Kho B24", Status = "Active" });
                db.UnitMeasure.Add(new UnitMeasure { Id = kg, Code = tag, Name = "[DEMO] kg" });
                db.Ingredient.Add(new Ingredient { Id = ingredient, Code = tag, Name = "[DEMO] Thịt B24", BaseUnitId = kg, BaseUomId = kg });
                db.Dish.Add(new Dish { Id = dish, Code = tag, Name = "[DEMO] Thịt kho B24", Status = "Active" });
                db.Recipe.Add(new Recipe
                {
                    Id = Guid.NewGuid(), DishId = dish, VersionNo = 1, VersionCode = "v1", Status = "Locked", LockedAt = clock.Now, IsActive = true, StandardOutputQty = 100,
                    SnapshotJson = $"[{{\"IngredientId\":\"{ingredient}\",\"Quantity\":10,\"UnitId\":\"{kg}\",\"WastePercent\":0,\"SequenceNo\":1}}]", Note = "[DEMO]",
                });
                db.ProductionPlan.Add(new ProductionPlan { Id = plan, PlanNumber = tag, PlanNo = tag, ProductionDate = day, PlanDate = clock.Today, Status = "Draft", Note = "[DEMO]" });
                db.ProductionPlanLine.Add(new ProductionPlanLine { Id = Guid.NewGuid(), ProductionPlanId = plan, DishId = dish, PlannedQuantity = 50, PlannedQty = 50, Status = "Draft" });
                db.Menu.Add(new Model.Entity.Menu { Id = menu, Code = tag, Name = "[DEMO] Thực đơn B24", VersionNo = 1, Status = "Published", EffectiveFrom = clock.Today });
                db.MenuLine.Add(new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = dish, MealType = "TRUA", DayOfWeek = day.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)day.DayOfWeek, SequenceNo = 1 });
                db.Customer.Add(new Customer { Id = customer, Code = tag, Name = "[DEMO] Khách B24", CIF = tag, CustomerType = "FACTORY", DisplayName = "[DEMO] Khách B24", Status = "Active" });
                db.Order.Add(new Order { Id = order, OrderNumber = tag, OrderNo = tag, CustomerId = customer, OrderDate = clock.Today, MealDate = day, DeliveryDate = day, Status = "Locked", TotalQuantity = 50, Note = "[DEMO]" });
                db.OrderLine.Add(new OrderLine { Id = Guid.NewGuid(), OrderId = order, MenuId = menu, MealType = "TRUA", ShiftId = shift, Quantity = 50, UnitPrice = 30000, LineAmount = 1_500_000 });
                db.MaterialLot.AddRange(
                    new MaterialLot { Id = goodLot, IngredientId = ingredient, LotNumber = tag + "-A", LotNo = tag + "-A", ExpiryDate = day.AddDays(3), ReceivedDate = clock.Today, QcStatus = "DAT", Status = "Available", UnitCost = 1000 },
                    new MaterialLot { Id = expiredLot, IngredientId = ingredient, LotNumber = tag + "-X", LotNo = tag + "-X", ExpiryDate = clock.Today.AddDays(-1), ReceivedDate = clock.Today.AddDays(-9), QcStatus = "DAT", Status = "Available", UnitCost = 1000 });
                if (!await db.FoodSafetyChecklist.AnyAsync(c => c.Code == "PRD_STAGE" && !c.IsDeleted))
                {
                    Guid checklist = Guid.NewGuid();
                    db.FoodSafetyChecklist.Add(new FoodSafetyChecklist { Id = checklist, Code = "PRD_STAGE", Name = "[DEMO] Công đoạn", VersionNo = 1, Status = "Active" });
                    db.FoodSafetyChecklistItem.AddRange(
                        new FoodSafetyChecklistItem { Id = Guid.NewGuid(), ChecklistId = checklist, ItemCode = "SO_CHE", ItemName = "Sơ chế", SortOrder = 1, IsRequired = true },
                        new FoodSafetyChecklistItem { Id = Guid.NewGuid(), ChecklistId = checklist, ItemCode = "NAU", ItemName = "Nấu", SortOrder = 2, IsRequired = true },
                        new FoodSafetyChecklistItem { Id = Guid.NewGuid(), ChecklistId = checklist, ItemCode = "CHIA_SUAT", ItemName = "Chia suất", SortOrder = 3, IsRequired = true });
                }
                await db.SaveChangesAsync("Insert");
                var ledger = scope.ServiceProvider.GetRequiredService<IInventoryLedgerService>();
                foreach (var (lot, qty) in new[] { (expiredLot, 30m), (goodLot, 8m) })
                {
                    await ledger.PostAsync(new InventoryPostRequest
                    {
                        TransactionType = "RECEIPT", TransactionNumber = tag, WarehouseId = warehouse, IngredientId = ingredient, MaterialLotId = lot, UnitId = kg, QuantityIn = qty, UnitCost = 1000, Note = "[DEMO]",
                    });
                }

                // 1) Hủy tồn lô hết hạn (chỉ kho thử) – WASTE 30 kg, lô → Hết hạn.
                var lotService = scope.ServiceProvider.GetRequiredService<IMaterialLotService>();
                var expiry = await lotService.ProcessExpired(new MaterialLotExpireRequest { WarehouseId = warehouse, Note = "[DEMO]" });
                expiry.ResponseCode.Should().Be(200, expiry.ResponseMessage);
                expiry.Data!.ExpiredLotCount.Should().Be(1);
                expiry.Data.TransactionNumber.Should().MatchRegex(@"^PH-\d{6}-\d{4}$");
                (await db.InventoryBalance.AsNoTracking().Where(b => b.MaterialLotId == expiredLot).SumAsync(b => b.OnHandQuantity)).Should().Be(0);

                // 2) Phát hành → giữ chỗ 5 kg (10 kg / 100 suất × 50 phần) trên lô còn hạn.
                var planService = scope.ServiceProvider.GetRequiredService<IProductionPlanService>();
                var batchService = scope.ServiceProvider.GetRequiredService<IProductionBatchService>();
                var orderService = scope.ServiceProvider.GetRequiredService<IProductionOrderService>();
                (await planService.Approve(new ProductionActionRequest { Id = plan })).ResponseCode.Should().Be(200);
                var released = await planService.Release(new ProductionPlanReleaseRequest { Id = plan, ShiftId = shift, DispatchTime = "10:30", Note = "[DEMO]" });
                released.ResponseCode.Should().Be(200, released.ResponseMessage);
                released.Data!.ListReservation.Should().ContainSingle().Which.ReservedQuantity.Should().Be(5);
                var goodBalance = await db.InventoryBalance.AsNoTracking().SingleAsync(b => b.MaterialLotId == goodLot);
                (goodBalance.ReservedQuantity, goodBalance.AvailableQuantity).Should().Be((5m, 3m));
                var batchBrief = released.Data.ListBatch.Single();
                Guid productionOrder = batchBrief.ProductionOrderId;

                // 3) Xuất NL cho mẻ dùng phần đã giữ → giữ chỗ Đã xuất, tồn giữ chỗ 0.
                var issue = await batchService.CreateMaterialIssue(new ProductionBatchIssueRequest { Id = batchBrief.Id, WarehouseId = warehouse, Post = true, Note = "[DEMO]" });
                issue.ResponseCode.Should().Be(200, issue.ResponseMessage);
                db.ChangeTracker.Clear();
                (await db.MaterialReservation.AsNoTracking().SingleAsync(r => r.ProductionOrderId == productionOrder)).Status.Should().Be("Issued");
                (await db.InventoryBalance.AsNoTracking().SingleAsync(b => b.MaterialLotId == goodLot)).ReservedQuantity.Should().Be(0);

                // 4) Công đoạn theo tổ + ATTP → đóng gói.
                foreach (string stage in new[] { "SO_CHE", "NAU" })
                {
                    (await batchService.StartStage(new ProductionStageRequest { Id = batchBrief.Id, StageCode = stage })).ResponseCode.Should().Be(200);
                    (await batchService.FinishStage(new ProductionStageRequest { Id = batchBrief.Id, StageCode = stage })).ResponseCode.Should().Be(200);
                }
                (await batchService.RecordCoreTemperature(new ProductionBatchTemperatureRequest { Id = batchBrief.Id, TemperatureC = 74.5m, DeviceCode = "[DEMO]" })).ResponseCode.Should().Be(200);
                (await batchService.RecordSampleRetained(new ProductionBatchSampleRequest { Id = batchBrief.Id, ActualValue = "[DEMO]" })).ResponseCode.Should().Be(200);
                (await batchService.Finish(new ProductionBatchFinishRequest { Id = batchBrief.Id, ActualQuantity = 50 })).ResponseCode.Should().Be(200);
                (await batchService.RecordOutput(new ProductionBatchOutputRequest { Id = batchBrief.Id, ProducedQuantity = 50, RejectedQuantity = 0, Note = "[DEMO]" })).ResponseCode.Should().Be(200);
                (await batchService.Release(new ProductionActionRequest { Id = batchBrief.Id })).ResponseCode.Should().Be(200);
                (await batchService.Pack(new ProductionActionRequest { Id = batchBrief.Id })).ResponseCode.Should().Be(400, "chưa xong công đoạn Chia suất");
                (await batchService.StartStage(new ProductionStageRequest { Id = batchBrief.Id, StageCode = "CHIA_SUAT" })).ResponseCode.Should().Be(200);
                (await batchService.FinishStage(new ProductionStageRequest { Id = batchBrief.Id, StageCode = "CHIA_SUAT" })).ResponseCode.Should().Be(200);
                var packed = await batchService.Pack(new ProductionActionRequest { Id = batchBrief.Id });
                packed.ResponseCode.Should().Be(200, packed.ResponseMessage);
                packed.Data!.ListStage.Should().OnlyContain(s => s.Status == "Done");

                // 5) Đóng lệnh: chênh lệch 0 (xuất đúng định mức), giá thành NVL 5 000.
                var closed = await orderService.Close(new ProductionActionRequest { Id = productionOrder, Note = "[DEMO]" });
                closed.ResponseCode.Should().Be(200, closed.ResponseMessage);
                closed.Data!.ListVariance.Should().ContainSingle().Which.VarianceQuantity.Should().Be(0);
                closed.Data.ActualCost.MaterialCost.Should().Be(5000);
                closed.Data.ActualCost.CostPerPortion.Should().Be(100);

                // 6) Giá vốn suất sau đối soát.
                db.ChangeTracker.Clear();
                var customerOrder = await db.Order.SingleAsync(o => o.Id == order);
                customerOrder.Status = "Reconciled";
                db.OrderReconciliation.Add(new OrderReconciliation { Id = Guid.NewGuid(), OrderId = order, ReconciliationDate = day, OrderedQuantity = 50, DeliveredQuantity = 50, AcceptedQuantity = 50, Status = "Matched", Note = "[DEMO]" });
                await db.SaveChangesAsync("Update");
                var meal = await scope.ServiceProvider.GetRequiredService<IMealCostService>().Calculate(new MealCostCalculateRequest { OrderId = order });
                meal.ResponseCode.Should().Be(200, meal.ResponseMessage);
                meal.Data!.TotalActualCost.Should().Be(5000);
                meal.Data.CostPerMeal.Should().Be(100);
                meal.Data.GrossMargin.Should().Be(1_495_000);
                await tx.RollbackAsync();
            }

            db.ChangeTracker.Clear();
            (await db.MaterialReservation.CountAsync()).Should().Be(reservationBefore, "rollback không để lại dữ liệu");
            (await db.ActualCost.CountAsync()).Should().Be(costBefore);
            (await db.MaterialConsumptionVariance.CountAsync()).Should().Be(varianceBefore);
            (await db.MealCost.CountAsync()).Should().Be(mealBefore);
            (await db.InventoryTransaction.CountAsync()).Should().Be(txnBefore);
            (await db.FoodSafetyChecklistResult.CountAsync()).Should().Be(resultBefore);
        }
    }
}
