using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Interface;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Notifications
{
    /// <summary>
    /// BOT-2.4 – phía api: DeliveryRoute/GetMine trả tọa độ điểm giao (CustomerDeliveryLocation) + danh sách bếp có tọa độ (bot nhận biết rời bếp / vào điểm);
    /// GetBoard không kèm bếp; ProductionBatch/GetBoard lọc theo mã dây chuyền / bàn nấu (QR LINE_).
    /// </summary>
    public class QrLiveApiTests
    {
        private static readonly DateTime Day = new(2026, 12, 18);

        [Fact]
        public async Task GetMine_returns_stop_coordinates_and_kitchens_for_the_driver()
        {
            using var f = new InMemoryApiFactory();
            f.Clock.FixedNow = Day.AddHours(8);
            Guid user = Guid.NewGuid(), employee = Guid.NewGuid(), route = Guid.NewGuid(), location = Guid.NewGuid(), customer = Guid.NewGuid();
            await f.SeedAsync(db =>
            {
                db.User.Add(new User { Id = user, UserName = "tx-24", IsActive = true });
                db.Employee.Add(new Employee { Id = employee, Code = "tx-24", FullName = "Tài xế 24", Status = "Active" });
                db.Customer.Add(new Customer { Id = customer, Code = "KH-Q24", Name = "Khách Q24", CIF = "KH-Q24", DisplayName = "Khách Q24", CustomerType = "FACTORY", Status = "Active" });
                db.CustomerDeliveryLocation.Add(new CustomerDeliveryLocation { Id = location, CustomerId = customer, Code = "D1", Name = "Cổng 1", Latitude = 21.03m, Longitude = 105.83m, Status = "Active" });
                db.KitchenWorkstation.Add(new KitchenWorkstation { Id = Guid.NewGuid(), Code = "BEP-Q24", Name = "Bếp", Latitude = 21m, Longitude = 105.8m, RadiusMeters = 120m, Status = "Active" });
                db.DeliveryRoute.Add(new DeliveryRoute { Id = route, RouteNo = "CX-Q24", DeliveryDate = Day, DriverId = employee, PlannedStart = Day.AddHours(9), Status = DeliveryRouteStatus.Planned });
                db.DeliveryStop.Add(new DeliveryStop { Id = Guid.NewGuid(), DeliveryRouteId = route, CustomerId = customer, DeliveryLocationId = location, SequenceNo = 1, PlannedArrival = Day.AddHours(10), StopStatus = DeliveryStopStatus.Planned });
                return Task.CompletedTask;
            });
            using var scope = f.Services.CreateScope();
            using var actor = scope.ServiceProvider.GetRequiredService<ICurrentActor>().BeginScope(ActorInfo.ForUser(user));
            var routes = scope.ServiceProvider.GetRequiredService<IDeliveryRouteService>();
            var mine = await routes.GetMine(Day);
            mine.ResponseCode.Should().Be(200, mine.ResponseMessage);
            var stop = mine.Data!.ListRoute.Single().ListStop.Single();
            (stop.Latitude, stop.Longitude).Should().Be((21.03m, 105.83m));
            mine.Data.ListKitchen.Should().ContainSingle(k => k.Code == "BEP-Q24" && k.RadiusMeters == 120m);
            (await routes.GetBoard(new DeliveryDateRequest { DeliveryDate = Day })).Data!.ListKitchen.Should().BeEmpty("bếp chỉ trả cho tài xế (GetMine)");
        }

        [Fact]
        public async Task Production_board_filters_by_line_or_workstation_code()
        {
            using var f = new InMemoryApiFactory();
            Guid plan = Guid.NewGuid(), line = Guid.NewGuid(), station = Guid.NewGuid(), orderOnLine = Guid.NewGuid(), orderOther = Guid.NewGuid();
            Guid onLine = Guid.NewGuid(), atStation = Guid.NewGuid(), elsewhere = Guid.NewGuid();
            await f.SeedAsync(db =>
            {
                db.ProductionPlan.Add(new ProductionPlan { Id = plan, PlanNo = "KH-Q24", ProductionDate = Day, PlanDate = Day, Status = "Released" });
                db.ProductionLine.Add(new ProductionLine { Id = line, Code = "LINE-A", Name = "Dây chuyền A" });
                db.KitchenWorkstation.Add(new KitchenWorkstation { Id = station, Code = "BAN-3", Name = "Bàn 3", Status = "Active" });
                db.ProductionOrder.AddRange(
                    new ProductionOrder { Id = orderOnLine, ProductionPlanId = plan, DishId = Guid.NewGuid(), RecipeId = Guid.NewGuid(), PlannedQty = 10, PlannedStartDate = Day, ProductionLineId = line, Status = "Released" },
                    new ProductionOrder { Id = orderOther, ProductionPlanId = plan, DishId = Guid.NewGuid(), RecipeId = Guid.NewGuid(), PlannedQty = 10, PlannedStartDate = Day, Status = "Released" });
                db.ProductionBatch.AddRange(
                    new ProductionBatch { Id = onLine, BatchNo = "M-A", ProductionOrderId = orderOnLine, DishId = Guid.NewGuid(), RecipeId = Guid.NewGuid(), StartDate = Day.AddHours(7), Status = ProductionBatchStatus.Planned },
                    new ProductionBatch { Id = atStation, BatchNo = "M-B", ProductionOrderId = orderOther, DishId = Guid.NewGuid(), RecipeId = Guid.NewGuid(), StartDate = Day.AddHours(7), Status = ProductionBatchStatus.Planned, KitchenWorkstationId = station },
                    new ProductionBatch { Id = elsewhere, BatchNo = "M-C", ProductionOrderId = orderOther, DishId = Guid.NewGuid(), RecipeId = Guid.NewGuid(), StartDate = Day.AddHours(7), Status = ProductionBatchStatus.Planned });
                return Task.CompletedTask;
            });
            using var scope = f.Services.CreateScope();
            var batches = scope.ServiceProvider.GetRequiredService<IProductionBatchService>();
            async Task<List<Guid>> IdsAsync(string? code) => [.. (await batches.GetBoard(new ProductionBoardRequest { ProductionDate = Day, LineCode = code })).Data!.ListBatch.Select(b => b.Id)];
            (await IdsAsync(null)).Should().BeEquivalentTo([onLine, atStation, elsewhere]);
            (await IdsAsync("LINE-A")).Should().Equal([onLine], "mã dây chuyền của lệnh SX");
            (await IdsAsync("BAN-3")).Should().Equal([atStation], "mã bàn nấu của mẻ");
            (await IdsAsync("KHONG-CO")).Should().BeEmpty();
        }
    }
}
