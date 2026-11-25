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
    /// BE2.5 – trên SQL Server dev thật (bỏ qua nếu không có DB), trong 1 transaction ngoài rồi ROLLBACK: checklist thành phẩm có tiêu chí trọng yếu KHÔNG ĐẠT trên mẻ
    /// Đã đóng gói → xuất chuyến bị chặn → kiểm lại ĐẠT → xuất phát → bàn giao (sổ bao bì OUT + phiếu Chờ thu hồi) → thu hồi (IN tốt / hỏng) → kiểm tra (tự lập mẻ VS-)
    /// → 6 bước vệ sinh → đạt (AVAILABLE) → số dư / thất thoát → sự cố NCC Mới → Đang xử lý → Đã xử lý → Đóng. Dữ liệu thử gắn nhãn [DEMO], ngày 30/12/2099.
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class FoodSafetyPackagingSqlServerTests(ApiFactory factory)
    {
        [SkippableFact]
        public async Task Checklist_hold_delivery_packaging_cleaning_and_supplier_issue_on_sql_server_inside_outer_transaction()
        {
            Skip.If(factory.SkipReason != null, factory.SkipReason);
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            var actor = scope.ServiceProvider.GetRequiredService<ICurrentActor>();
            Guid admin = await db.User.Where(u => u.UserName == "admin").Select(u => u.Id).SingleAsync();
            int ledgerBefore = await db.PackagingLedger.CountAsync();
            int returnBefore = await db.ContainerReturn.CountAsync();
            int inspectionBefore = await db.ReturnInspection.CountAsync();
            int cleaningBefore = await db.CleaningBatch.CountAsync();
            int cleaningResultBefore = await db.CleaningChecklistResult.CountAsync();
            int resultBefore = await db.FoodSafetyChecklistResult.CountAsync();
            int issueBefore = await db.SupplierIssue.CountAsync();
            string tag = "[DEMO]-B25-" + Guid.NewGuid().ToString("N")[..6];
            string code = "B25" + Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();
            DateTime day = new(2099, 12, 30);

            using (actor.BeginScope(ActorInfo.ForUser(admin)))
            await using (var tx = await db.Database.BeginTransactionAsync())
            {
                Guid shift = Guid.NewGuid(), vehicle = Guid.NewGuid(), customer = Guid.NewGuid(), location = Guid.NewGuid(), order = Guid.NewGuid(), supplier = Guid.NewGuid();
                Guid dish = Guid.NewGuid(), plan = Guid.NewGuid(), productionOrder = Guid.NewGuid(), batch = Guid.NewGuid(), packing = Guid.NewGuid(), checklist = Guid.NewGuid();
                db.Shift.Add(new Shift { Id = shift, Code = code, Name = "[DEMO] Ca B25", StartTime = new TimeSpan(10, 30, 0), Status = "Active" });
                db.Vehicle.Add(new Vehicle { Id = vehicle, Code = code, LicensePlate = "[DEMO]", Capacity = 100, Status = "Active", Note = "[DEMO]" });
                db.Customer.Add(new Customer { Id = customer, Code = tag, Name = "[DEMO] Khách B25", CIF = tag, CustomerType = "FACTORY", DisplayName = "[DEMO] Khách B25", Status = "Active" });
                db.CustomerDeliveryLocation.Add(new CustomerDeliveryLocation { Id = location, CustomerId = customer, Code = tag, Name = "[DEMO] Căng tin B25", DeliveryWindowFrom = new TimeSpan(11, 0, 0) });
                db.Order.Add(new Order { Id = order, OrderNumber = tag, OrderNo = tag, CustomerId = customer, DeliveryLocationId = location, OrderDate = day.AddDays(-2), MealDate = day, DeliveryDate = day,
                    Status = "InProduction", TotalQuantity = 50, TotalAmount = 1_500_000, Note = "[DEMO]" });
                db.OrderLine.Add(new OrderLine { Id = Guid.NewGuid(), OrderId = order, MealType = "TRUA", ShiftId = shift, Quantity = 50, UnitPrice = 30_000, Amount = 1_500_000, LineAmount = 1_500_000 });
                db.Dish.Add(new Dish { Id = dish, Code = tag, Name = "[DEMO] Cơm gà B25", Status = "Active" });
                db.ProductionPlan.Add(new ProductionPlan { Id = plan, PlanNumber = tag, PlanNo = tag, ProductionDate = day, PlanDate = day.AddDays(-1), Status = "Completed", Note = "[DEMO]" });
                db.ProductionOrder.Add(new ProductionOrder { Id = productionOrder, ProductionPlanId = plan, DishId = dish, RecipeId = Guid.NewGuid(), ShiftId = shift, PlannedQty = 50,
                    Status = "Completed", ProductionOrderNumber = tag, ProductionOrderNo = tag, PlannedStartDate = day.AddHours(8), Note = "[DEMO]" });
                db.ProductionBatch.Add(new ProductionBatch { Id = batch, ProductionOrderId = productionOrder, DishId = dish, RecipeId = Guid.NewGuid(), BatchNumber = tag, BatchNo = tag,
                    StartDate = day.AddHours(8), PlannedQuantity = 50, PlannedQty = 50, Status = "Packed", QcStatus = "PASS" });
                db.PackingOrder.Add(new PackingOrder { Id = packing, ProductionBatchId = batch, OrderId = order, PlannedQty = 50, Status = "Packed", PackingOrderNumber = tag, PackingDate = day, Note = "[DEMO]" });
                db.PackingResult.Add(new PackingResult { Id = Guid.NewGuid(), PackingOrderId = packing, PlannedQty = 50, PackedQuantity = 50, AcceptedQty = 50, PackagingType = "[DEMO] KHAY B25" });
                db.Supplier.Add(new Supplier { Id = supplier, Code = code, Name = "[DEMO] NCC B25", Status = "Active" });
                var sample = await db.FoodSafetyChecklistItem.Where(i => i.ItemCode == "SAMPLE_RETAINED" && !i.IsDeleted).FirstOrDefaultAsync();
                if (sample == null)
                {
                    Guid sampleChecklist = Guid.NewGuid();
                    sample = new FoodSafetyChecklistItem { Id = Guid.NewGuid(), ChecklistId = sampleChecklist, ItemCode = "SAMPLE_RETAINED", ItemName = "[DEMO] Lưu mẫu", SortOrder = 1, IsCritical = true, IsRequired = true };
                    db.FoodSafetyChecklist.Add(new FoodSafetyChecklist { Id = sampleChecklist, Code = "PRD_BATCH", Name = "[DEMO] Kiểm soát mẻ nấu", VersionNo = 1, Status = "Active" });
                    db.FoodSafetyChecklistItem.Add(sample);
                }
                db.FoodSafetyChecklistResult.Add(new FoodSafetyChecklistResult { Id = Guid.NewGuid(), ChecklistId = sample.ChecklistId, ChecklistItemId = sample.Id, CheckDate = day, ProductionBatchId = batch,
                    ResultCode = "PASS", ActualValue = "[DEMO]", CheckedBy = admin, CheckedAt = day.AddHours(9) });
                db.FoodSafetyChecklist.Add(new FoodSafetyChecklist { Id = checklist, Code = code, Name = "[DEMO] Thành phẩm B25", VersionNo = 1, Status = "Active", Note = "[DEMO]" });
                db.FoodSafetyChecklistItem.AddRange(
                    new FoodSafetyChecklistItem { Id = Guid.NewGuid(), ChecklistId = checklist, ItemCode = "CAM_QUAN", ItemName = "[DEMO] Cảm quan", SortOrder = 1, IsCritical = true, IsRequired = true },
                    new FoodSafetyChecklistItem { Id = Guid.NewGuid(), ChecklistId = checklist, ItemCode = "LUU_MAU_KY", ItemName = "[DEMO] Lưu mẫu + ký", SortOrder = 2, IsCritical = false, IsRequired = true });
                if (!await db.FoodSafetyChecklist.AnyAsync(c => c.Code == "VS-KHAY" && !c.IsDeleted))
                {
                    Guid cleaning = Guid.NewGuid();
                    db.FoodSafetyChecklist.Add(new FoodSafetyChecklist { Id = cleaning, Code = "VS-KHAY", Name = "[DEMO] Vệ sinh khay", VersionNo = 1, Status = "Active" });
                    int n = 0;
                    foreach (string step in new[] { "NHAN_QR", "PHAN_LOAI", "RUA_SO", "RUA_MAY", "KHU_KHUAN", "KIEM_TRA_NHAP_KHO" })
                    {
                        db.FoodSafetyChecklistItem.Add(new FoodSafetyChecklistItem { Id = Guid.NewGuid(), ChecklistId = cleaning, ItemCode = step, ItemName = "[DEMO] " + step, SortOrder = ++n, IsRequired = true });
                    }
                }
                await db.SaveChangesAsync("Insert");

                var results = scope.ServiceProvider.GetRequiredService<IFoodSafetyChecklistResultService>();
                var routes = scope.ServiceProvider.GetRequiredService<IDeliveryRouteService>();
                var stops = scope.ServiceProvider.GetRequiredService<IDeliveryStopService>();
                var returns = scope.ServiceProvider.GetRequiredService<IContainerReturnService>();
                var cleanings = scope.ServiceProvider.GetRequiredService<ICleaningBatchService>();
                var ledgers = scope.ServiceProvider.GetRequiredService<IPackagingLedgerService>();
                var issues = scope.ServiceProvider.GetRequiredService<ISupplierIssueService>();

                // 1) Cảm quan KHÔNG ĐẠT trên mẻ Đã đóng gói → chặn xuất chuyến.
                var failed = await results.Record(new ChecklistRecordRequest { ChecklistId = checklist, CheckDate = day, ProductionBatchId = batch, ListItem = [
                    new ChecklistRecordItemRequest { ItemCode = "CAM_QUAN", ResultCode = "FAIL", ActualValue = "[DEMO] chua", CorrectiveAction = "[DEMO] nếm lại" },
                    new ChecklistRecordItemRequest { ItemCode = "LUU_MAU_KY", ResultCode = "PASS" }] });
                failed.ResponseCode.Should().Be(200, failed.ResponseMessage);
                failed.Data!.ListCriticalFailure.Should().ContainSingle().Which.BatchAction.Should().Be("Đã đóng gói – chặn xuất chuyến");
                failed.Data.Run.ShiftId.Should().Be(shift);
                var created = await routes.Create(new DeliveryRouteCreateRequest { DeliveryDate = day, ShiftId = shift, VehicleId = vehicle, ListOrderId = [order], Description = "[DEMO]" });
                created.ResponseCode.Should().Be(200, created.ResponseMessage);
                Guid routeId = created.Data!.Route.Id;
                var stop = created.Data.ListStop.Single();
                (await routes.Load(new DeliveryRouteLoadRequest { Id = routeId, Note = "[DEMO]" })).ResponseCode.Should().Be(200);
                var blocked = await routes.Dispatch(new DeliveryRouteDispatchRequest { Id = routeId, TemperatureC = 65m, DeviceCode = "[DEMO]" });
                blocked.ResponseCode.Should().Be(400);
                blocked.ResponseMessage.Should().Contain("Mẻ có tiêu chí ATTP trọng yếu chưa đạt: " + tag + " ([DEMO] Cảm quan)");
                var recheck = await results.Record(new ChecklistRecordRequest { ChecklistId = checklist, CheckDate = day, ProductionBatchId = batch,
                    ListItem = [new ChecklistRecordItemRequest { ItemCode = "CAM_QUAN", ResultCode = "PASS", ActualValue = "[DEMO] đạt" }] });
                recheck.ResponseCode.Should().Be(200, recheck.ResponseMessage);
                recheck.Data!.Run.ListItem[0].HistoryCount.Should().Be(2);
                recheck.Data.Run.ListPassRate7d.Last().Rate.Should().Be(100);
                var dispatched = await routes.Dispatch(new DeliveryRouteDispatchRequest { Id = routeId, TemperatureC = 65m, DeviceCode = "[DEMO]" });
                dispatched.ResponseCode.Should().Be(200, dispatched.ResponseMessage);

                // 2) Bàn giao 50 → OUT 50 khay (loại của kết quả đóng gói) + phiếu Chờ thu hồi.
                var delivered = await stops.Deliver(new DeliveryStopDeliverRequest { Id = stop.Id, TemperatureC = 64m, DeliveredQuantity = 50, ReceiverName = "[DEMO] Người nhận" });
                delivered.ResponseCode.Should().Be(200, delivered.ResponseMessage);
                delivered.Data!.ListWarning.Should().Contain("Sổ bao bì: giao 50 [DEMO] KHAY B25 cho khách (OUT), chờ thu hồi");
                Guid deliveryOrder = stop.DeliveryOrderId!.Value;

                // 3) Thu hồi 48 (hỏng 2) → IN 46 + 2; kiểm tra → mẻ vệ sinh 46 khay.
                var returned = await returns.Record(new ContainerReturnRecordRequest { DeliveryOrderId = deliveryOrder, ReturnedQuantity = 48, DamagedQuantity = 2, Note = "[DEMO]" });
                returned.ResponseCode.Should().Be(200, returned.ResponseMessage);
                returned.Data!.OutstandingOfDelivery.Should().Be(2);
                returned.Data.PackagingType.Should().Be("[DEMO] KHAY B25");
                var inspected = await returns.Inspect(new ContainerReturnInspectRequest { Id = returned.Data.Return.Id, InspectionResult = "DIRTY", Note = "[DEMO] bẩn dầu" });
                inspected.ResponseCode.Should().Be(200, inspected.ResponseMessage);
                var batchVs = inspected.Data!.ListCleaningBatch.Single();
                batchVs.ContainerQuantity.Should().Be(46);
                batchVs.CleaningBatchNumber.Should().MatchRegex(@"^VS-\d{6}-[A-Z0-9_]+(-\d+)?$", "DB dev có thể đã có mẻ VS- cùng ngày / ca → hậu tố -2, -3…");

                // 4) 6 bước → đạt → AVAILABLE 46.
                var steps = await cleanings.RecordResults(new CleaningRecordRequest { Id = batchVs.Id, ListItem = [.. new[] { "NHAN_QR", "PHAN_LOAI", "RUA_SO", "RUA_MAY", "KHU_KHUAN", "KIEM_TRA_NHAP_KHO" }
                    .Select(x => new CleaningRecordItemRequest { ItemCode = x, ResultCode = "PASS", ChemicalUsed = x == "KHU_KHUAN" ? "[DEMO] Chlorine 200 ppm" : null })] });
                steps.ResponseCode.Should().Be(200, steps.ResponseMessage);
                steps.Data!.CanPass.Should().BeTrue();
                var completed = await cleanings.Complete(new CleaningCompleteRequest { Id = batchVs.Id, QcResult = "PASS" });
                completed.ResponseCode.Should().Be(200, completed.ResponseMessage);
                completed.Data!.Batch.Status.Should().Be("Completed");

                // 5) Số dư + thất thoát 2 → còn 0, tỷ lệ 4% > 3% (ngưỡng chung).
                var loss = await ledgers.Record(new PackagingRecordRequest { CustomerId = customer, TransactionType = "LOSS", PackagingType = "[DEMO] KHAY B25", Quantity = 2,
                    DeliveryOrderId = deliveryOrder, Reason = "[DEMO] mất" });
                loss.ResponseCode.Should().Be(200, loss.ResponseMessage);
                (loss.Data!.Balance!.OutQuantity, loss.Data.Balance.InQuantity, loss.Data.Balance.LossQuantity, loss.Data.Balance.Outstanding, loss.Data.Balance.LossRatePct, loss.Data.Balance.IsOverThreshold)
                    .Should().Be((50m, 48m, 2m, 0m, (decimal?)4m, true));
                var balance = await ledgers.GetBalance(new PackagingBalanceRequest { CustomerId = customer });
                balance.Data!.ListItem.Should().ContainSingle().Which.DamagedInQuantity.Should().Be(2);
                db.ChangeTracker.Clear();
                (await db.PackagingLedger.AsNoTracking().Where(l => l.ReferenceNo == batchVs.CleaningBatchNumber).Select(l => l.TransactionType).SingleAsync()).Should().Be("AVAILABLE");
                (await db.ContainerReturn.AsNoTracking().SingleAsync(r => r.DeliveryOrderId == deliveryOrder)).Status.Should().Be("Inspected");

                // 6) Sự cố NCC: Mới → Đang xử lý → Đã xử lý → Đóng; danh sách có cờ quá hạn.
                var issue = new SupplierIssue { Id = Guid.NewGuid(), SupplierId = supplier, IssueType = "QUALITY", Severity = "High", Description = "[DEMO] Rau dập", ReportedAt = day.AddDays(-5),
                    DueAt = day.AddDays(-3), IssueStatus = "Open" };
                db.SupplierIssue.Add(issue);
                await db.SaveChangesAsync("Insert");
                var list = await issues.GetList(new SupplierIssueListRequest { SupplierId = supplier });
                list.ResponseCode.Should().Be(200, list.ResponseMessage);
                list.Data!.ListItem.Should().ContainSingle().Which.AllowedTransitions.Should().Equal("InProgress", "Resolved");
                (await issues.Transition(new SupplierIssueTransitionRequest { Id = issue.Id, To = "InProgress" })).Data!.Issue.OwnerId.Should().Be(admin);
                (await issues.Transition(new SupplierIssueTransitionRequest { Id = issue.Id, To = "Resolved" })).ResponseCode.Should().Be(400);
                (await issues.Transition(new SupplierIssueTransitionRequest { Id = issue.Id, To = "Resolved", ResolutionType = "EXCHANGE", Resolution = "[DEMO] đổi hàng" })).ResponseCode.Should().Be(200);
                var closed = await issues.Transition(new SupplierIssueTransitionRequest { Id = issue.Id, To = "Closed" });
                closed.ResponseCode.Should().Be(200, closed.ResponseMessage);
                closed.Data!.Issue.ClosedAt.Should().NotBeNull();
                closed.Data.IsOverdue.Should().BeFalse("đã đóng");
                await tx.RollbackAsync();
            }

            db.ChangeTracker.Clear();
            (await db.PackagingLedger.CountAsync()).Should().Be(ledgerBefore, "rollback không để lại dữ liệu");
            (await db.ContainerReturn.CountAsync()).Should().Be(returnBefore);
            (await db.ReturnInspection.CountAsync()).Should().Be(inspectionBefore);
            (await db.CleaningBatch.CountAsync()).Should().Be(cleaningBefore);
            (await db.CleaningChecklistResult.CountAsync()).Should().Be(cleaningResultBefore);
            (await db.FoodSafetyChecklistResult.CountAsync()).Should().Be(resultBefore);
            (await db.SupplierIssue.CountAsync()).Should().Be(issueBefore);
        }
    }
}
