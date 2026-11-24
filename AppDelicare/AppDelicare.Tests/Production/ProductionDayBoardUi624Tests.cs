using System.Text.Json;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Option;
using AppDelicare.Services.Implement;
using AppDelicare.Services.Interface;
using AppDelicare.Services.Jobs;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AppDelicare.Tests.Production
{
    /// <summary>
    /// UI-6.24 (D-105): Bảng sản xuất ngày (GetDayBoard – ma trận Món × Ca khớp lệnh / mẻ / giữ chỗ), job cuối ngày đóng lệnh Hoàn thành còn sót
    /// (cờ tắt → không đóng; bật → đóng + giải phóng giữ chỗ dư; chạy lại = 0), thêm mới lệnh / mẻ vẫn bị chặn bằng câu tiếng Việt.
    /// </summary>
    public class ProductionDayBoardUi624Tests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public ProductionDayBoardUi624Tests(InMemoryApiFactory factory) => _factory = factory;

        private static int MenuDay(DateTime d) => d.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)d.DayOfWeek;

        private async Task<(Guid Plan, Guid Dish, Guid Category)> SeedAndReleaseAsync(string code, DateTime day)
        {
            Guid shift = Guid.NewGuid(), dish = Guid.NewGuid(), menu = Guid.NewGuid(), customer = Guid.NewGuid(), order = Guid.NewGuid(), kg = Guid.NewGuid(), ing = Guid.NewGuid(), cat = Guid.NewGuid();
            await _factory.SeedAsync(async db =>
            {
                db.Shift.Add(new Shift { Id = shift, Code = "T" + code, Name = "Trưa " + code, StartTime = new TimeSpan(10, 30, 0), Status = "Active" });
                db.DishCategory.Add(new DishCategory { Id = cat, Code = "NM" + code, Name = "Món mặn " + code });
                db.Dish.Add(new Dish { Id = dish, Code = "M" + code, Name = "Món " + code, Status = "Active", DishCategoryId = cat });
                db.Menu.Add(new Model.Entity.Menu { Id = menu, Code = "TD" + code, Name = "TĐ " + code, VersionNo = 1, Status = "Published" });
                db.MenuLine.Add(new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = dish, MealType = "TRUA", DayOfWeek = MenuDay(day), SequenceNo = 1, SortOrder = 1 });
                db.UnitMeasure.Add(new UnitMeasure { Id = kg, Code = "KG" + code, Name = "kg" });
                db.Ingredient.Add(new Ingredient { Id = ing, Code = "NL" + code, Name = "NL " + code, BaseUnitId = kg });
                db.Recipe.Add(new Recipe { Id = Guid.NewGuid(), DishId = dish, VersionNo = 1, Status = "Locked", IsActive = true, StandardOutputQty = 100, EffectiveFrom = day.AddDays(-30),
                    LockedAt = day.AddDays(-30), SnapshotJson = JsonSerializer.Serialize(new[] { new { IngredientId = ing, Quantity = 10m, UnitId = kg, SequenceNo = 1 } }) });
                Guid warehouse = Guid.NewGuid(), lot = Guid.NewGuid();
                db.Warehouse.Add(new Warehouse { Id = warehouse, Code = "W" + code, Name = "Kho " + code, Status = "Active" });
                db.MaterialLot.Add(new MaterialLot { Id = lot, IngredientId = ing, LotNumber = "L" + code, LotNo = "L" + code, ExpiryDate = day.AddDays(30), ReceivedDate = day.AddDays(-2), QcStatus = "DAT", QualityStatus = "DAT", Status = "Available" });
                await new InventoryLedgerService(db).PostAsync(new InventoryPostRequest { TransactionType = "RECEIPT", TransactionNumber = "NK" + code, WarehouseId = warehouse, IngredientId = ing, MaterialLotId = lot, UnitId = kg, QuantityIn = 30 });
                db.Customer.Add(new Customer { Id = customer, Code = "K" + code, Name = "Khách " + code, DisplayName = "Khách " + code, CIF = "K" + code, CustomerType = "FACTORY", Status = "Active" });
                db.Order.Add(new Order { Id = order, OrderNumber = "DH-" + code, CustomerId = customer, OrderDate = day.AddDays(-3), MealDate = day, DeliveryDate = day, Status = "Confirmed", TotalQuantity = 100 });
                db.OrderLine.Add(new OrderLine { Id = Guid.NewGuid(), OrderId = order, MenuId = menu, MealType = "TRUA", ShiftId = shift, Quantity = 100, LineAmount = 0 });
            });
            using var c = _factory.CreateAuthorizedClient();
            var auto = (await c.PostFormAsync("/api/Order/LockByMealDate", ("MealDate", day))).Ok().GetProperty("autoPlan");
            auto.GetProperty("status").GetString().Should().Be("RELEASED");
            Guid plan = await _factory.QueryAsync(db => db.ProductionPlan.AsNoTracking().Where(p => p.ProductionDate == day && !p.IsDeleted).Select(p => p.Id).SingleAsync());
            return (plan, dish, cat);
        }

        [Fact]
        public async Task Day_board_matches_orders_batches_and_reservations_and_job_closes_leftover_orders()
        {
            var day = new DateTime(2032, 7, 6);
            _factory.Clock.FixedNow = day.AddDays(-1).AddHours(20).AddMinutes(5);
            var (plan, dish, cat) = await SeedAndReleaseAsync("B1", day);
            using var c = _factory.CreateAuthorizedClient();

            var board = (await c.GetResultAsync($"/api/ProductionPlan/GetDayBoard?id={plan}")).Ok();
            board.GetProperty("totalPortions").GetDecimal().Should().Be(100);
            (board.GetProperty("orderTotal").GetInt32(), board.GetProperty("orderClosed").GetInt32()).Should().Be((1, 0));
            board.GetProperty("pendingShiftCount").GetInt32().Should().Be(0);
            board.GetProperty("allowedActions").EnumerateArray().Select(a => a.GetString()).Should().Equal("exportProductionSheet");
            var group = board.GetProperty("listGroup").EnumerateArray().Single();
            (group.GetProperty("categoryId").GetGuid(), group.GetProperty("categoryName").GetString()).Should().Be((cat, "Món mặn B1"));
            var cell = group.GetProperty("listRow").EnumerateArray().Single().GetProperty("listCell").EnumerateArray().Single();
            (cell.GetProperty("portions").GetDecimal(), cell.GetProperty("state").GetString(), cell.GetProperty("stateName").GetString()).Should().Be((100m, "waiting", "Chờ nấu"));
            int batchTotal = cell.GetProperty("batchTotal").GetInt32();
            batchTotal.Should().Be(await _factory.QueryAsync(db => db.ProductionBatch.CountAsync(b => db.ProductionOrder.Any(o => o.Id == b.ProductionOrderId && o.ProductionPlanId == plan))));
            cell.GetProperty("batchDone").GetInt32().Should().Be(0);
            var order = cell.GetProperty("listOrder").EnumerateArray().Single();
            order.GetProperty("allowedActions").EnumerateArray().Select(a => a.GetString()).Should().Contain(["reserve", "cancel"]);
            var material = board.GetProperty("listMaterial").EnumerateArray().Single();
            (material.GetProperty("required").GetDecimal(), material.GetProperty("reserved").GetDecimal(), material.GetProperty("short").GetDecimal()).Should().Be((10m, 10m, 0m));
            (await c.GetResultAsync($"/api/ProductionPlan/GetDayBoard?id={Guid.NewGuid()}")).Fail("Không tìm thấy");

            // Thêm mới lệnh / mẻ tay vẫn bị chặn – câu tiếng Việt chỉ đường làm.
            (await c.PostFormAsync("/api/ProductionOrder/Insert", ("ProductionPlanId", plan), ("DishId", dish), ("PlannedStartDate", day))).Fail("Vào Kế hoạch sản xuất → chọn ngày → Ra lệnh");

            // Lệnh Hoàn thành còn sót (vd tự đóng lỗi) → job cuối ngày đóng; cờ tắt → không đóng.
            Guid orderId = order.GetProperty("id").GetGuid();
            await _factory.SeedAsync(async db => { var o = await db.ProductionOrder.SingleAsync(x => x.Id == orderId); o.Status = "Completed"; o.ActualQty = 100; });
            var rules = _factory.Services.GetRequiredService<IOptions<BusinessRulesOptions>>().Value;
            using (var scope = _factory.Services.CreateScope())
            using (scope.ServiceProvider.GetRequiredService<ICurrentActor>().BeginScope(ActorInfo.ForSystem(InMemoryApiFactory.UserId)))
            {
                var job = scope.ServiceProvider.GetServices<IScheduledJob>().Single(j => j.Code == ProductionAutoCloseJob.JobCode);
                job.DefaultAt.Should().Be(rules.ProductionAutoCloseTime);
                rules.ProductionAutoCloseOrder = false;
                try
                {
                    (await job.RunAsync(day.AddHours(22), CancellationToken.None)).Message.Should().Contain("đang tắt");
                }
                finally
                {
                    rules.ProductionAutoCloseOrder = true;
                }
                (await _factory.QueryAsync(db => db.ProductionOrder.AsNoTracking().SingleAsync(o => o.Id == orderId))).Status.Should().Be("Completed");
                var outcome = await job.RunAsync(day.AddHours(22), CancellationToken.None);
                (outcome.Status, outcome.RowsProcessed).Should().Be(("Succeeded", 1L), outcome.Message);
            }
            (await _factory.QueryAsync(db => db.ProductionOrder.AsNoTracking().SingleAsync(o => o.Id == orderId))).Status.Should().Be("Closed");
            (await _factory.QueryAsync(db => db.MaterialReservation.AsNoTracking().Where(r => r.ProductionOrderId == orderId).Select(r => r.Status).ToListAsync()))
                .Should().OnlyContain(st => st == "Released", "giữ chỗ dư được giải phóng khi đóng");
            using (var scope = _factory.Services.CreateScope())
            using (scope.ServiceProvider.GetRequiredService<ICurrentActor>().BeginScope(ActorInfo.ForSystem(InMemoryApiFactory.UserId)))
            {
                var job = scope.ServiceProvider.GetServices<IScheduledJob>().Single(j => j.Code == ProductionAutoCloseJob.JobCode);
                (await job.RunAsync(day.AddHours(22), CancellationToken.None)).RowsProcessed.Should().Be(0, "chạy lại = 0 lệnh");
            }
            var after = (await c.GetResultAsync($"/api/ProductionPlan/GetDayBoard?id={plan}")).Ok();
            (after.GetProperty("orderTotal").GetInt32(), after.GetProperty("orderClosed").GetInt32()).Should().Be((1, 1));
            after.GetProperty("listCost").EnumerateArray().Should().ContainSingle();
        }
    }
}
