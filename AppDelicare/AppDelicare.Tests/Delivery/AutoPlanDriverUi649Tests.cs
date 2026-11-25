using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Services.Implement;
using AppDelicare.Tests.Integration;
using FluentAssertions;

namespace AppDelicare.Tests.Delivery
{
    /// <summary>
    /// UI-6.49 (10) (D-127 g – phương án tạm, GAP-LOG-DRV PROPOSED): tự lập chuyến gán tài xế = người lái chuyến gần nhất của xe (trước ngày giao),
    /// bỏ qua tài xế đã nghỉ việc; xe chưa có chuyến nào → để trống (bot nhắc điều phối).
    /// </summary>
    public class AutoPlanDriverUi649Tests
    {
        private static readonly DateTime Day = new(2032, 9, 6);

        [Fact]
        public async Task Last_driver_of_each_vehicle_is_reused_skipping_resigned_drivers()
        {
            using var f = new InMemoryApiFactory();
            Guid v1 = Guid.NewGuid(), v2 = Guid.NewGuid(), v3 = Guid.NewGuid(), d1 = Guid.NewGuid(), d2 = Guid.NewGuid(), d3 = Guid.NewGuid(), gone = Guid.NewGuid();
            await f.SeedAsync(db =>
            {
                db.Employee.AddRange(
                    new Employee { Id = d1, Code = "TX1", FullName = "TX 1", Status = "Active" }, new Employee { Id = d2, Code = "TX2", FullName = "TX 2", Status = "Active" },
                    new Employee { Id = d3, Code = "TX3", FullName = "TX 3", Status = "Active" }, new Employee { Id = gone, Code = "TX9", FullName = "TX nghỉ", Status = EmployeeStatus.Resigned });
                void Route(Guid v, Guid d, int daysBefore) => db.DeliveryRoute.Add(new DeliveryRoute { Id = Guid.NewGuid(), RouteNo = $"CX-{daysBefore}-{v.ToString()[..4]}", VehicleId = v, DriverId = d, DeliveryDate = Day.AddDays(-daysBefore), Status = "Completed" });
                Route(v1, d1, 5); Route(v1, d2, 1);          // xe 1: chuyến gần nhất do TX 2 lái
                Route(v2, d3, 4); Route(v2, gone, 2);        // xe 2: gần nhất là người đã nghỉ → lấy TX 3
                db.DeliveryRoute.Add(new DeliveryRoute { Id = Guid.NewGuid(), RouteNo = "CX-FUT", VehicleId = v1, DriverId = d1, DeliveryDate = Day.AddDays(1), Status = "Planned" }); // sau ngày giao – không tính
                return Task.CompletedTask;
            });
            var map = await f.QueryAsync(db => DeliveryRouteService.LastDriversAsync(db, [v1, v2, v3], Day));
            map[v1].Should().Be(d2);
            map[v2].Should().Be(d3, "bỏ qua tài xế đã nghỉ việc");
            map.Should().NotContainKey(v3, "xe chưa có chuyến → để trống, bot nhắc điều phối");

            // GAP-LOG-DRV (D-128): tài xế mặc định của xe thắng chuyến gần nhất; mặc định đã nghỉ việc → về chuyến gần nhất.
            var vehicles = new List<Vehicle>
            {
                new() { Id = v1, DefaultDriverEmployeeId = d3 }, new() { Id = v2, DefaultDriverEmployeeId = gone }, new() { Id = v3, DefaultDriverEmployeeId = d1 },
            };
            var plan = await f.QueryAsync(db => DeliveryRouteService.PlanDriversAsync(db, vehicles, Day));
            plan[v1].Should().Be(d3, "tài xế mặc định");
            plan[v2].Should().Be(d3, "mặc định đã nghỉ → tài xế chuyến gần nhất còn làm");
            plan[v3].Should().Be(d1, "xe chưa có chuyến nhưng có tài xế mặc định");
        }

        [Fact]
        public async Task Vehicle_default_driver_must_be_a_working_employee()
        {
            using var f = new InMemoryApiFactory();
            Guid ok = Guid.NewGuid(), gone = Guid.NewGuid();
            await f.SeedAsync(db =>
            {
                db.Employee.Add(new Employee { Id = ok, Code = "TXOK", FullName = "TX còn làm", Status = "Active" });
                db.Employee.Add(new Employee { Id = gone, Code = "TXGONE", FullName = "TX nghỉ", Status = EmployeeStatus.Resigned });
                return Task.CompletedTask;
            });
            using var admin = f.CreateAuthorizedClient();
            (await admin.PostFormAsync("/api/Vehicle/Insert", ("LicensePlate", "51C-DRV1"), ("Capacity", 100), ("Status", "Active"), ("DefaultDriverEmployeeId", gone))).Fail("Tài xế mặc định");
            (await admin.PostFormAsync("/api/Vehicle/Insert", ("LicensePlate", "51C-DRV2"), ("Capacity", 100), ("Status", "Active"), ("DefaultDriverEmployeeId", ok))).Ok();
            (await f.QueryAsync(db => Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(db.Vehicle, v => v.LicensePlate == "51C-DRV2"))).DefaultDriverEmployeeId.Should().Be(ok);
        }
    }
}
