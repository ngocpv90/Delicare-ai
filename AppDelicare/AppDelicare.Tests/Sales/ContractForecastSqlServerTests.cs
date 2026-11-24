using AppDelicare.Data.Connection;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Interface;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Sales
{
    /// <summary>
    /// BE2.2 – trên SQL Server dev thật (bỏ qua nếu không có DB), 1 transaction ngoài rồi ROLLBACK (không để lại dữ liệu; [DEMO]):
    /// hợp đồng Create (số HD- qua sp_getapplock) → Submit → duyệt (engine CONTRACT) → Hiệu lực; dự báo Import → Publish → GetMatrix → gợi ý trong mẫu tạo đơn;
    /// đơn → xác nhận (lệch dự báo ghi nhận); đổi món (số PH-) → duyệt; thêm đơn sau giờ chốt (ngày ăn = hôm nay) → duyệt → Đã chốt → xin sửa → duyệt.
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class ContractForecastSqlServerTests(ApiFactory factory)
    {
        [SkippableFact]
        public async Task Contract_forecast_menu_change_and_after_cutoff_change_on_sql_server_inside_outer_transaction()
        {
            Skip.If(factory.SkipReason != null, factory.SkipReason);
            using var scope = factory.Services.CreateScope();
            var sp = scope.ServiceProvider;
            var db = sp.GetRequiredService<DatabaseContext>();
            var actor = sp.GetRequiredService<ICurrentActor>();
            var clock = sp.GetRequiredService<IClock>();
            Guid admin = await db.User.Where(u => u.UserName == "admin").Select(u => u.Id).SingleAsync();
            int contractsBefore = await db.Contract.CountAsync(), forecastsBefore = await db.Forecast.CountAsync(), changesBefore = await db.MenuChangeRequest.CountAsync();
            string tag = "[DEMO]-BE22-" + Guid.NewGuid().ToString("N")[..6];
            DateTime future = clock.Today.AddDays(30), today = clock.Today;

            using (actor.BeginScope(ActorInfo.ForUser(admin)))
            await using (var tx = await db.Database.BeginTransactionAsync())
            {
                // DB dev có thể có thực đơn chung đã công bố khác phủ cùng ngày (dữ liệu full-flow / UAT) → tạm ẩn trong giao dịch (rollback cuối test) để đơn dùng đúng thực đơn của test.
                await db.Menu.Where(m => m.MenuType == "COMMON" && m.Status == "Published" && !m.IsDeleted).ExecuteUpdateAsync(u => u.SetProperty(m => m.Status, "Draft"));
                Guid customer = Guid.NewGuid(), location = Guid.NewGuid(), shift = Guid.NewGuid(), chicken = Guid.NewGuid(), fish = Guid.NewGuid(), menu = Guid.NewGuid();
                db.Customer.Add(new Customer { Id = customer, Code = tag, Name = "[DEMO] Khách BE2.2", CIF = tag, CustomerType = "FACTORY", DisplayName = "[DEMO] Khách BE2.2", Status = "Active" });
                db.CustomerDeliveryLocation.Add(new CustomerDeliveryLocation { Id = location, CustomerId = customer, Code = tag, Name = "[DEMO] Điểm giao" });
                db.Shift.Add(new Shift { Id = shift, Code = tag, Name = "[DEMO] Ca trưa" });
                db.Dish.AddRange(new Dish { Id = chicken, Code = tag + "GA", Name = "[DEMO] Gà" }, new Dish { Id = fish, Code = tag + "CA", Name = "[DEMO] Cá" });
                db.Menu.Add(new Model.Entity.Menu { Id = menu, Code = tag, Name = "[DEMO] Thực đơn", VersionNo = 1, Status = "Published", MenuType = "COMMON" /* UI-6.12 (D-90): mọi khách dùng thực đơn chung */, EffectiveFrom = today.AddDays(-1) });
                foreach (var d in new[] { future, today })
                {
                    db.MenuLine.Add(new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = chicken, MealType = "TRUA", DayOfWeek = d.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)d.DayOfWeek, SequenceNo = 1 });
                }
                db.MenuAssignment.Add(new MenuAssignment { Id = Guid.NewGuid(), CustomerId = customer, MenuId = menu, MealType = "TRUA", Status = "Active", EffectiveFrom = today.AddDays(-1) });
                await db.SaveChangesAsync("Insert");

                // Hợp đồng: Create → Submit → duyệt.
                var contracts = sp.GetRequiredService<IContractService>();
                var created = await contracts.Create(new ContractCreateRequest
                {
                    CustomerId = customer, Name = "[DEMO] HĐ BE2.2", EffectiveFrom = today.AddDays(-1),
                    ListMeal = [new ContractMealCreateRequest { MealType = "TRUA", MealName = "[DEMO] Suất trưa", UnitPrice = 38000 }],
                });
                created.ResponseCode.Should().Be(200, created.ResponseMessage);
                Guid contract = created.Data!.Contract!.Id;
                created.Data.Contract.ContractNumber.Should().MatchRegex(@"^HD-\d{6}-\d{3}$");
                (await contracts.Submit(new ContractActionRequest { Id = contract })).ResponseCode.Should().Be(200);
                var approved = await contracts.Decide(new ContractActionRequest { Id = contract, Note = "[DEMO]" }, true);
                approved.ResponseCode.Should().Be(200, approved.ResponseMessage);
                Guid meal = created.Data.ListMeal.Single().Id;

                // Dự báo: Import → Publish → ma trận → gợi ý.
                var forecasts = sp.GetRequiredService<IForecastService>();
                var imported = await forecasts.Import(new ForecastImportRequest
                {
                    ListItem = [new ForecastImportItemRequest { CustomerId = customer, DeliveryLocationId = location, ForecastDate = future, MealType = "TRUA", ForecastQuantity = 100 }],
                });
                imported.ResponseCode.Should().Be(200, imported.ResponseMessage);
                (await forecasts.Publish(new ForecastPublishRequest { From = future, To = future, ListCustomerId = [customer] })).Data!.Published.Should().Be(1);
                var matrix = await forecasts.GetMatrix(new ForecastMatrixRequest { From = future, To = future, CustomerId = customer });
                matrix.Data!.GrandTotal.Should().Be(100);
                var orders = sp.GetRequiredService<IOrderService>();
                var template = await orders.GetNewTemplate(customer, future);
                template.Data!.ListLocation.Single().ListLine.Single().SuggestedQuantity.Should().Be(100);

                // Đơn 120 → xác nhận: lệch +20% → ghi nhận.
                var order = await orders.Create(new OrderCreateRequest
                {
                    CustomerId = customer, DeliveryLocationId = location, MealDate = future,
                    ListLine = [new OrderCreateLineRequest { ContractMealId = meal, ShiftId = shift, Quantity = 120 }],
                });
                order.ResponseCode.Should().Be(200, order.ResponseMessage);
                Guid orderId = order.Data!.Order!.Id;
                (await orders.Submit(new OrderActionRequest { Id = orderId })).ResponseCode.Should().Be(200);
                var confirmed = await orders.Confirm(new OrderActionRequest { Id = orderId });
                confirmed.Data!.ForecastVariancePercent.Should().Be(20);
                confirmed.Data.ForecastVarianceExceeded.Should().BeTrue();

                // Đổi món gà → cá 40 suất → duyệt.
                var changes = sp.GetRequiredService<IMenuChangeRequestService>();
                var change = await changes.Create(new MenuChangeCreateRequest { OrderId = orderId, OriginalDishId = chicken, RequestedDishId = fish, Quantity = 40, Reason = "[DEMO] dị ứng" });
                change.ResponseCode.Should().Be(200, change.ResponseMessage);
                change.Data!.Request!.RequestNo.Should().MatchRegex(@"^DM-\d{6}-\d{4}$");
                var changeApproved = await changes.Decide(new MenuChangeActionRequest { Id = change.Data.Request.Id }, true);
                changeApproved.ResponseCode.Should().Be(200, changeApproved.ResponseMessage);

                // Thêm đơn sau giờ chốt (ngày ăn hôm nay – giờ chốt 20h hôm qua đã qua) → duyệt → Đã chốt → xin sửa → duyệt.
                var late = await orders.Create(new OrderCreateRequest
                {
                    CustomerId = customer, DeliveryLocationId = location, MealDate = today, AfterCutoffReason = "[DEMO] thêm muộn",
                    ListLine = [new OrderCreateLineRequest { ContractMealId = meal, ShiftId = shift, Quantity = 30 }],
                });
                late.ResponseCode.Should().Be(200, late.ResponseMessage);
                Guid lateId = late.Data!.Order!.Id;
                var approvals = sp.GetRequiredService<IApprovalRequestService>();
                db.ChangeTracker.Clear();
                Guid lateApproval = await db.ApprovalRequest.Where(a => a.BusinessId == lateId && a.RequestStatus == "Pending").Select(a => a.Id).SingleAsync();
                (await approvals.Decide(new ApprovalDecideRequest { Id = lateApproval, Approve = true })).ResponseCode.Should().Be(200);
                Guid lineId = await db.OrderLine.Where(l => l.OrderId == lateId && !l.IsDeleted).Select(l => l.Id).SingleAsync();
                var asked = await orders.RequestChange(new OrderChangeAfterCutoffRequest { Id = lateId, Reason = "[DEMO] tăng suất", ListLine = [new OrderLineQuantityRequest { Id = lineId, Quantity = 35 }] });
                asked.ResponseCode.Should().Be(200, asked.ResponseMessage);
                db.ChangeTracker.Clear();
                Guid changeApproval = await db.ApprovalRequest.Where(a => a.BusinessId == lateId && a.RequestStatus == "Pending").Select(a => a.Id).SingleAsync();
                var decided = await approvals.Decide(new ApprovalDecideRequest { Id = changeApproval, Approve = true });
                decided.ResponseCode.Should().Be(200, decided.ResponseMessage);

                db.ChangeTracker.Clear();
                (await db.Contract.AsNoTracking().SingleAsync(c => c.Id == contract)).Status.Should().Be("Active");
                (await db.MenuChangeRequest.AsNoTracking().SingleAsync(r => r.Id == change.Data.Request.Id)).RequestStatus.Should().Be("Approved");
                var lateOrder = await db.Order.AsNoTracking().SingleAsync(o => o.Id == lateId);
                lateOrder.Status.Should().Be("Locked");
                lateOrder.TotalAmount.Should().Be(35 * 38000m);
                (await db.Order.AsNoTracking().SingleAsync(o => o.Id == orderId)).Note.Should().Contain("[LECH_DU_BAO]");
                await tx.RollbackAsync();
            }

            db.ChangeTracker.Clear();
            (await db.Contract.CountAsync()).Should().Be(contractsBefore, "rollback không để lại dữ liệu");
            (await db.Forecast.CountAsync()).Should().Be(forecastsBefore);
            (await db.MenuChangeRequest.CountAsync()).Should().Be(changesBefore);
        }
    }
}
