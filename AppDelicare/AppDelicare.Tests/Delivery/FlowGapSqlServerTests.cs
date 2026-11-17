using AppDelicare.Data.Connection;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Interface;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Delivery
{
    /// <summary>
    /// BE2.1 – trên SQL Server dev thật (bỏ qua nếu không có DB), 1 transaction ngoài rồi ROLLBACK (không để lại dữ liệu; [DEMO], ngày 28/12/2099):
    /// sinh lệnh đóng gói từ mẻ → kết quả → mẻ Đã đóng gói (số PH- qua sp_getapplock); đơn giao tay / tách / gán chuyến / đổi thứ tự / hủy – kiểm ràng buộc
    /// UNIQUE (DeliveryRouteId, SequenceNo) của DDL (tính cả dòng đã xóa mềm) không bị vi phạm; xếp hàng lên xe → xuất phát → thực hiện giao DISPATCHED.
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class FlowGapSqlServerTests(ApiFactory factory)
    {
        [SkippableFact]
        public async Task Packing_manual_delivery_orders_resequencing_and_loading_on_sql_server_inside_outer_transaction()
        {
            Skip.If(factory.SkipReason != null, factory.SkipReason);
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            var actor = scope.ServiceProvider.GetRequiredService<ICurrentActor>();
            Guid admin = await db.User.Where(u => u.UserName == "admin").Select(u => u.Id).SingleAsync();
            int packingBefore = await db.PackingOrder.CountAsync();
            int deliveryBefore = await db.DeliveryOrder.CountAsync();
            int stopBefore = await db.DeliveryStop.CountAsync();
            int loadingBefore = await db.DeliveryLoading.CountAsync();
            int executionBefore = await db.DeliveryExecution.CountAsync();
            string tag = "[DEMO]-BE21-" + Guid.NewGuid().ToString("N")[..6];
            string code = "B21" + Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();
            DateTime day = new(2099, 12, 28);
            int menuDay = day.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)day.DayOfWeek;

            using (actor.BeginScope(ActorInfo.ForUser(admin)))
            await using (var tx = await db.Database.BeginTransactionAsync())
            {
                Guid shift = Guid.NewGuid(), vehicle = Guid.NewGuid(), customer = Guid.NewGuid(), location = Guid.NewGuid(), order = Guid.NewGuid(), menu = Guid.NewGuid();
                Guid dish = Guid.NewGuid(), plan = Guid.NewGuid(), productionOrder = Guid.NewGuid(), batch = Guid.NewGuid();
                db.Shift.Add(new Shift { Id = shift, Code = code, Name = "[DEMO] Ca BE2.1", StartTime = new TimeSpan(10, 30, 0), Status = "Active" });
                db.Vehicle.Add(new Vehicle { Id = vehicle, Code = code, LicensePlate = "[DEMO]", Capacity = 100, Status = "Active", Note = "[DEMO]" });
                db.Customer.Add(new Customer { Id = customer, Code = tag, Name = "[DEMO] Khách BE2.1", CIF = tag, CustomerType = "FACTORY", DisplayName = "[DEMO] Khách BE2.1", Status = "Active" });
                db.CustomerDeliveryLocation.Add(new CustomerDeliveryLocation { Id = location, CustomerId = customer, Code = tag, Name = "[DEMO] Căng tin BE2.1", Address = "[DEMO], Quận 9",
                    DeliveryWindowFrom = new TimeSpan(11, 0, 0) });
                db.Dish.Add(new Dish { Id = dish, Code = tag, Name = "[DEMO] Cơm gà BE2.1", Status = "Active" });
                db.Menu.Add(new AppDelicare.Model.Entity.Menu { Id = menu, Code = tag, Name = "[DEMO] Thực đơn BE2.1", VersionNo = 1, Status = "Published", Note = "[DEMO]" });
                db.MenuLine.Add(new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = dish, MealType = "TRUA", DayOfWeek = menuDay, SequenceNo = 1, SortOrder = 1 });
                db.Order.Add(new Order { Id = order, OrderNumber = tag, OrderNo = tag, CustomerId = customer, DeliveryLocationId = location, OrderDate = day.AddDays(-2), MealDate = day, DeliveryDate = day,
                    Status = "InProduction", TotalQuantity = 60, TotalAmount = 1_800_000, Note = "[DEMO]" });
                db.OrderLine.Add(new OrderLine { Id = Guid.NewGuid(), OrderId = order, MenuId = menu, MealType = "TRUA", ShiftId = shift, Quantity = 60, UnitPrice = 30_000, Amount = 1_800_000, LineAmount = 1_800_000 });
                db.ProductionPlan.Add(new ProductionPlan { Id = plan, PlanNumber = tag, PlanNo = tag, ProductionDate = day, PlanDate = day.AddDays(-1), Status = "Released", Note = "[DEMO]" });
                db.ProductionOrder.Add(new ProductionOrder { Id = productionOrder, ProductionPlanId = plan, DishId = dish, RecipeId = Guid.NewGuid(), ShiftId = shift, PlannedQty = 60,
                    Status = "InProgress", ProductionOrderNumber = tag, ProductionOrderNo = tag, PlannedStartDate = day.AddHours(8), Note = "[DEMO]" });
                db.ProductionBatch.Add(new ProductionBatch { Id = batch, ProductionOrderId = productionOrder, DishId = dish, RecipeId = Guid.NewGuid(), BatchNumber = tag, BatchNo = tag,
                    StartDate = day.AddHours(8), PlannedQuantity = 60, PlannedQty = 60, ActualQuantity = 60, AcceptedQty = 60, Status = "Passed", QcStatus = "PASS" });
                db.ProductionOutput.Add(new ProductionOutput { Id = Guid.NewGuid(), ProductionBatchId = batch, DishId = dish, ProducedQuantity = 60, OutputQty = 60, AcceptedQuantity = 60, AcceptedQty = 60, QualityStatus = "PASS" });
                db.TemperatureLog.Add(new TemperatureLog { Id = Guid.NewGuid(), ProductionBatchId = batch, MeasurementType = "CORE", TemperatureC = 75, MinAllowedC = 70, IsWithinRange = true, MeasuredAt = day.AddHours(9),
                    DeviceCode = "[DEMO]", SourceChannel = "MANUAL", RecordedBy = admin });
                var item = await db.FoodSafetyChecklistItem.Where(i => i.ItemCode == "SAMPLE_RETAINED" && !i.IsDeleted).FirstOrDefaultAsync();
                if (item == null)
                {
                    Guid checklist = Guid.NewGuid();
                    item = new FoodSafetyChecklistItem { Id = Guid.NewGuid(), ChecklistId = checklist, ItemCode = "SAMPLE_RETAINED", ItemName = "[DEMO] Lưu mẫu", SortOrder = 1, IsCritical = true, IsRequired = true };
                    db.FoodSafetyChecklist.Add(new FoodSafetyChecklist { Id = checklist, Code = "PRD_BATCH", Name = "[DEMO] Kiểm soát mẻ nấu", VersionNo = 1, Status = "Active" });
                    db.FoodSafetyChecklistItem.Add(item);
                }
                db.FoodSafetyChecklistResult.Add(new FoodSafetyChecklistResult { Id = Guid.NewGuid(), ChecklistId = item.ChecklistId, ChecklistItemId = item.Id, CheckDate = day, ProductionBatchId = batch,
                    ResultCode = "PASS", ActualValue = "[DEMO]", CheckedBy = admin, CheckedAt = day.AddHours(9) });
                await db.SaveChangesAsync("Insert");

                // ----- Đóng gói từ mẻ → mẻ Đã đóng gói.
                var packing = scope.ServiceProvider.GetRequiredService<IPackingOrderService>();
                var generated = await packing.GenerateFromBatch(new PackingOrderGenerateRequest { ProductionBatchId = batch, PackingDate = day.AddHours(10), Note = "[DEMO]" });
                generated.ResponseCode.Should().Be(200, generated.ResponseMessage);
                var po = generated.Data!.ListPackingOrder.Single();
                po.PackingOrder.PlannedQty.Should().Be(60);
                po.PackingOrder.PackingOrderNumber.Should().MatchRegex(@"^PH-991228-\d{4}$");
                (await packing.Start(new PackingOrderActionRequest { Id = po.PackingOrder.Id })).ResponseCode.Should().Be(200);
                var packed = await packing.RecordResult(new PackingOrderResultRequest { Id = po.PackingOrder.Id, PackedQuantity = 60, Note = "[DEMO]" });
                packed.ResponseCode.Should().Be(200, packed.ResponseMessage);
                packed.Data!.BatchStatus.Should().Be("Packed");

                // ----- Đơn giao tay 20 → chuyến (40 còn lại + đơn giao tay) → tách / gán / đổi thứ tự / hủy (đánh số lại qua số âm, UNIQUE gồm dòng xóa mềm).
                var deliveryOrders = scope.ServiceProvider.GetRequiredService<IDeliveryOrderService>();
                var routes = scope.ServiceProvider.GetRequiredService<IDeliveryRouteService>();
                var manual = await deliveryOrders.Create(new DeliveryOrderCreateRequest { OrderId = order, MealQuantity = 20, Note = "[DEMO]" });
                manual.ResponseCode.Should().Be(200, manual.ResponseMessage);
                Guid do1 = manual.Data!.ListDeliveryOrder.Single().DeliveryOrder.Id;
                var created = await routes.Create(new DeliveryRouteCreateRequest { DeliveryDate = day, VehicleId = vehicle, ListOrderId = [order], ListDeliveryOrderId = [do1], Description = "[DEMO]" });
                created.ResponseCode.Should().Be(200, created.ResponseMessage);
                Guid routeId = created.Data!.Route.Id;
                created.Data.ListStop.Select(s => s.PlannedMealQty).Should().BeEquivalentTo(new decimal?[] { 40, 20 });
                var split = await deliveryOrders.Split(new DeliveryOrderSplitRequest { Id = do1, ListPart = [new() { MealQuantity = 12 }, new() { MealQuantity = 8 }] });
                split.ResponseCode.Should().Be(200, split.ResponseMessage);
                Guid do3 = split.Data!.ListDeliveryOrder.Single(d => d.MealQuantity == 8).DeliveryOrder.Id;
                var assigned = await deliveryOrders.AssignRoute(new DeliveryOrderAssignRouteRequest { Id = do3, DeliveryRouteId = routeId, SequenceNo = 1 });
                assigned.ResponseCode.Should().Be(200, assigned.ResponseMessage);
                assigned.Data!.ListRoute.Single().ListStop.Select(s => (s.SequenceNo, s.PlannedMealQty)).Should().Equal((1, 8m), (2, 40m), (3, 12m));
                var reversed = await routes.UpdateStops(new DeliveryRouteStopsRequest
                {
                    Id = routeId,
                    ListStop = [new() { DeliveryOrderId = do3, SequenceNo = 3 }, new() { DeliveryOrderId = do1, SequenceNo = 1 }],
                });
                reversed.ResponseCode.Should().Be(200, reversed.ResponseMessage);
                reversed.Data!.ListStop.Select(s => (s.SequenceNo, s.PlannedMealQty)).Should().Equal((1, 12m), (2, 40m), (3, 8m));
                var cancelled = await deliveryOrders.Cancel(new DeliveryOrderCancelRequest { Id = do3, Reason = "[DEMO] hủy" });
                cancelled.ResponseCode.Should().Be(200, cancelled.ResponseMessage);
                cancelled.Data!.ListRoute.Single().ListStop.Select(s => s.SequenceNo).Should().Equal(1, 2);
                // Gán lại phần 8 suất: lập đơn giao mới cho phần trả lại, đưa vào cuối chuyến (số 3 – trước đó đã dùng rồi giải phóng).
                var again = await deliveryOrders.Create(new DeliveryOrderCreateRequest { OrderId = order, Note = "[DEMO]" });
                again.ResponseCode.Should().Be(200, again.ResponseMessage);
                again.Data!.ListDeliveryOrder.Single().MealQuantity.Should().Be(8);
                var appended = await deliveryOrders.AssignRoute(new DeliveryOrderAssignRouteRequest { Id = again.Data.ListDeliveryOrder.Single().DeliveryOrder.Id, DeliveryRouteId = routeId });
                appended.ResponseCode.Should().Be(200, appended.ResponseMessage);
                appended.Data!.ListRoute.Single().ListStop.Select(s => s.SequenceNo).Should().Equal(1, 2, 3);

                // ----- Xếp hàng lên xe → xuất phát.
                var loaded = await routes.Load(new DeliveryRouteLoadRequest { Id = routeId, Note = "[DEMO]" });
                loaded.ResponseCode.Should().Be(200, loaded.ResponseMessage);
                loaded.Data!.ListLoading.Should().HaveCount(3).And.OnlyContain(l => l.Status == "Loaded" && l.VehicleCode == code);
                var dispatched = await routes.Dispatch(new DeliveryRouteDispatchRequest { Id = routeId, TemperatureC = 65m, DeviceCode = "[DEMO]" });
                dispatched.ResponseCode.Should().Be(200, dispatched.ResponseMessage);
                db.ChangeTracker.Clear();
                (await db.DeliveryExecution.AsNoTracking().Where(e => e.DeliveryRouteId == routeId).Select(e => e.Status).ToListAsync()).Should().HaveCount(3).And.OnlyContain(s => s == "DISPATCHED");
                (await db.DeliveryStop.AsNoTracking().CountAsync(s => s.DeliveryRouteId == routeId && s.IsDeleted && s.SequenceNo < 0)).Should().Be(1, "điểm của đơn giao hủy giữ số âm");
                await tx.RollbackAsync();
            }

            db.ChangeTracker.Clear();
            (await db.PackingOrder.CountAsync()).Should().Be(packingBefore, "rollback không để lại dữ liệu");
            (await db.DeliveryOrder.CountAsync()).Should().Be(deliveryBefore);
            (await db.DeliveryStop.CountAsync()).Should().Be(stopBefore);
            (await db.DeliveryLoading.CountAsync()).Should().Be(loadingBefore);
            (await db.DeliveryExecution.CountAsync()).Should().Be(executionBefore);
        }
    }
}
