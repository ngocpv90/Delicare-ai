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
    /// T19 – trên SQL Server dev thật (bỏ qua nếu không có DB): chuyến CX- (sp_getapplock) → xuất phát (nhiệt độ 65 °C, mẻ đã đóng gói + lưu mẫu) → đến điểm → bàn giao + POD (64 °C)
    /// → đối soát suất phục vụ → đóng (đơn Đã đối soát) + phản ánh PA- vòng đời, trong 1 transaction ngoài rồi ROLLBACK (không để lại dữ liệu). Dữ liệu thử gắn nhãn [DEMO], ngày 29/12/2099.
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class DeliverySqlServerTests(ApiFactory factory)
    {
        [SkippableFact]
        public async Task Route_dispatch_handover_pod_reconcile_and_complaint_on_sql_server_inside_outer_transaction()
        {
            Skip.If(factory.SkipReason != null, factory.SkipReason);
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            var actor = scope.ServiceProvider.GetRequiredService<ICurrentActor>();
            Guid admin = await db.User.Where(u => u.UserName == "admin").Select(u => u.Id).SingleAsync();
            int routeBefore = await db.DeliveryRoute.CountAsync();
            int eventBefore = await db.DeliveryEvent.CountAsync();
            int tempBefore = await db.TemperatureLog.CountAsync();
            int podBefore = await db.ProofOfDelivery.CountAsync();
            int recBefore = await db.OrderReconciliation.CountAsync();
            int complaintBefore = await db.CustomerComplaint.CountAsync();
            string tag = "[DEMO]-T19-" + Guid.NewGuid().ToString("N")[..6];
            string vehicleCode = "T19" + Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();
            DateTime day = new(2099, 12, 29);

            using (actor.BeginScope(ActorInfo.ForUser(admin)))
            await using (var tx = await db.Database.BeginTransactionAsync())
            {
                Guid shift = Guid.NewGuid(), vehicle = Guid.NewGuid(), customer = Guid.NewGuid(), location = Guid.NewGuid(), order = Guid.NewGuid();
                Guid dish = Guid.NewGuid(), plan = Guid.NewGuid(), productionOrder = Guid.NewGuid(), batch = Guid.NewGuid();
                db.Shift.Add(new Shift { Id = shift, Code = vehicleCode, Name = "[DEMO] Ca T19", StartTime = new TimeSpan(10, 30, 0), Status = "Active" });
                db.Vehicle.Add(new Vehicle { Id = vehicle, Code = vehicleCode, LicensePlate = "[DEMO]", Capacity = 100, Status = "Active", Note = "[DEMO]" });
                db.Customer.Add(new Customer { Id = customer, Code = tag, Name = "[DEMO] Khách T19", CIF = tag, CustomerType = "FACTORY", DisplayName = "[DEMO] Khách T19", Status = "Active" });
                db.CustomerDeliveryLocation.Add(new CustomerDeliveryLocation { Id = location, CustomerId = customer, Code = tag, Name = "[DEMO] Căng tin T19", DeliveryWindowFrom = new TimeSpan(11, 0, 0) });
                db.Order.Add(new Order { Id = order, OrderNumber = tag, OrderNo = tag, CustomerId = customer, DeliveryLocationId = location, OrderDate = day.AddDays(-2), MealDate = day, DeliveryDate = day,
                    Status = "InProduction", TotalQuantity = 50, TotalAmount = 1_500_000, Note = "[DEMO]" });
                db.OrderLine.Add(new OrderLine { Id = Guid.NewGuid(), OrderId = order, MealType = "TRUA", ShiftId = shift, Quantity = 50, UnitPrice = 30_000, Amount = 1_500_000, LineAmount = 1_500_000 });
                db.Dish.Add(new Dish { Id = dish, Code = tag, Name = "[DEMO] Cơm gà T19", Status = "Active" });
                db.ProductionPlan.Add(new ProductionPlan { Id = plan, PlanNumber = tag, PlanNo = tag, ProductionDate = day, PlanDate = day.AddDays(-1), Status = "Completed", Note = "[DEMO]" });
                db.ProductionOrder.Add(new ProductionOrder { Id = productionOrder, ProductionPlanId = plan, DishId = dish, RecipeId = Guid.NewGuid(), ShiftId = shift, PlannedQty = 50,
                    Status = "Completed", ProductionOrderNumber = tag, ProductionOrderNo = tag, PlannedStartDate = day.AddHours(8), Note = "[DEMO]" });
                db.ProductionBatch.Add(new ProductionBatch { Id = batch, ProductionOrderId = productionOrder, DishId = dish, RecipeId = Guid.NewGuid(), BatchNumber = tag, BatchNo = tag,
                    StartDate = day.AddHours(8), PlannedQuantity = 50, PlannedQty = 50, Status = "Packed", QcStatus = "PASS" });
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

                var routes = scope.ServiceProvider.GetRequiredService<IDeliveryRouteService>();
                var stops = scope.ServiceProvider.GetRequiredService<IDeliveryStopService>();
                var reconciliations = scope.ServiceProvider.GetRequiredService<IOrderReconciliationService>();
                var complaints = scope.ServiceProvider.GetRequiredService<ICustomerComplaintService>();

                var created = await routes.Create(new DeliveryRouteCreateRequest { DeliveryDate = day, ShiftId = shift, VehicleId = vehicle, ListOrderId = [order], Description = "[DEMO]" });
                created.ResponseCode.Should().Be(200, created.ResponseMessage);
                created.Data!.Route.RouteNo.Should().MatchRegex($@"^CX-991229-{vehicleCode}-\d{{2}}$");
                var stop = created.Data.ListStop.Single();
                stop.DeliveryNumber.Should().MatchRegex(@"^GH-991229-\d{4}$");
                Guid routeId = created.Data.Route.Id;

                var clock = scope.ServiceProvider.GetRequiredService<IClock>();
                (await routes.RecordTemperature(new DeliveryTemperatureRequest { Id = routeId, TemperatureC = 57.5m, CorrectiveAction = "[DEMO] hâm lại", DeviceCode = "[DEMO]",
                    MeasuredAt = clock.Now.AddMinutes(-10) })).ResponseCode.Should().Be(200);
                var low = await routes.Dispatch(new DeliveryRouteDispatchRequest { Id = routeId });
                low.ResponseCode.Should().Be(400);
                low.ResponseMessage.Should().Contain("57.5 °C dưới ngưỡng 60 °C");
                (await routes.Load(new DeliveryRouteLoadRequest { Id = routeId, Note = "[DEMO]" })).ResponseCode.Should().Be(200); // BE2.1
                var dispatched = await routes.Dispatch(new DeliveryRouteDispatchRequest { Id = routeId, TemperatureC = 65m, DeviceCode = "[DEMO]" });
                dispatched.ResponseCode.Should().Be(200, dispatched.ResponseMessage);
                (await stops.Arrive(new DeliveryStopArriveRequest { Id = stop.Id })).ResponseCode.Should().Be(200);
                var delivered = await stops.Deliver(new DeliveryStopDeliverRequest { Id = stop.Id, TemperatureC = 64m, DeliveredQuantity = 50, AcceptedQuantity = 50, ReceiverName = "[DEMO] Người nhận" });
                delivered.ResponseCode.Should().Be(200, delivered.ResponseMessage);
                delivered.Data!.Route.Status.Should().Be("Completed");

                var reconciled = await reconciliations.Reconcile(new OrderReconcileRequest { OrderId = order, ServedQuantity = 48, ReturnedQuantity = 2, Remark = "[DEMO]" });
                reconciled.ResponseCode.Should().Be(200, reconciled.ResponseMessage);
                reconciled.Data!.Reconciliation.VarianceQty.Should().Be(-2);
                reconciled.Data.Reconciliation.VariancePercent.Should().Be(-4);
                reconciled.Data.Reconciliation.FinalAmount.Should().Be(1_440_000, "1.500.000 × 48 ÷ 50");
                (await reconciliations.Close(new ReconciliationCloseRequest { Id = reconciled.Data.Reconciliation.Id })).ResponseCode.Should().Be(200);

                var complaint = await complaints.Create(new ComplaintCreateRequest { CustomerId = customer, DeliveryStopId = stop.Id, Severity = "Medium", Description = "[DEMO] Thiếu canh" });
                complaint.ResponseCode.Should().Be(200, complaint.ResponseMessage);
                complaint.Data!.Complaint.ComplaintNo.Should().MatchRegex(@"^PA-\d{6}-\d{4}$");
                complaint.Data.Complaint.OrderId.Should().Be(order);
                Guid complaintId = complaint.Data.Complaint.Id;
                (await complaints.Assign(new ComplaintActionRequest { Id = complaintId })).ResponseCode.Should().Be(200);
                (await complaints.Resolve(new ComplaintActionRequest { Id = complaintId, Comment = "[DEMO] Giao bù" })).ResponseCode.Should().Be(200);
                (await complaints.Close(new ComplaintActionRequest { Id = complaintId })).ResponseCode.Should().Be(200);

                db.ChangeTracker.Clear();
                (await db.Order.AsNoTracking().SingleAsync(o => o.Id == order)).Status.Should().Be("Reconciled");
                (await db.TemperatureLog.AsNoTracking().CountAsync(t => t.MeasurementType == "DELIVERY" && t.DeviceCode == "[DEMO]")).Should().BeGreaterThan(0);
                (await db.DeliveryEvent.AsNoTracking().CountAsync(e => e.DeliveryRouteId == routeId)).Should().Be(7, "TEMP 57,5 + LOADED (BE2.1) + TEMP 65 + DISPATCHED + ARRIVED + TEMP 64 + DELIVERED");
                (await db.ComplaintHistory.AsNoTracking().CountAsync(h => h.ComplaintId == complaintId)).Should().Be(4);
                await tx.RollbackAsync();
            }

            db.ChangeTracker.Clear();
            (await db.DeliveryRoute.CountAsync()).Should().Be(routeBefore, "rollback không để lại dữ liệu");
            (await db.DeliveryEvent.CountAsync()).Should().Be(eventBefore);
            (await db.TemperatureLog.CountAsync()).Should().Be(tempBefore);
            (await db.ProofOfDelivery.CountAsync()).Should().Be(podBefore);
            (await db.OrderReconciliation.CountAsync()).Should().Be(recBefore);
            (await db.CustomerComplaint.CountAsync()).Should().Be(complaintBefore);
        }
    }
}
