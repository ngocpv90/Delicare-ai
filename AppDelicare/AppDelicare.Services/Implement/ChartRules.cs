using AppDelicare.Data.Connection;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Implement
{
    /// <summary>BE3.1: hàm dùng chung cho dữ liệu biểu đồ (khoảng ngày) và sự kiện giao (timeline / vị trí xe).</summary>
    public static class ChartRules
    {
        /// <summary>
        /// Khoảng ngày [from, to] (cả 2 đầu, theo ngày). Trống cả 2 → <paramref name="defaultDays"/> ngày gần nhất tính tới hôm nay;
        /// chỉ có from → tới from + defaultDays − 1; chỉ có to → lùi defaultDays − 1. Quá <paramref name="maxDays"/> ngày hoặc from &gt; to → thông báo lỗi.
        /// </summary>
        public static string? DayRange(DateTime? from, DateTime? to, DateTime today, int defaultDays, int maxDays, out DateTime start, out DateTime end)
        {
            int days = Math.Max(1, defaultDays);
            end = (to ?? (from?.Date.AddDays(days - 1) ?? today)).Date;
            start = (from ?? end.AddDays(-(days - 1))).Date;
            return start > end || (end - start).TotalDays + 1 > Math.Max(1, maxDays)
                ? string.Format(ConstantHelper.Message.ChartRangeInvalid, maxDays)
                : null;
        }

        /// <summary>Sự kiện giao → dòng hiển thị (số chuyến, biển số, tài xế, thứ tự điểm, người ghi, nhãn loại). Giữ thứ tự đầu vào.</summary>
        public static async Task<List<DeliveryEventItemModel>> DeliveryEventItemsAsync(DatabaseContext db, List<DeliveryEvent> events)
        {
            var routeIds = events.Select(e => e.DeliveryRouteId).Distinct().ToList();
            var routes = await db.DeliveryRoute.AsNoTracking().Where(r => routeIds.Contains(r.Id))
                .Select(r => new { r.Id, Number = r.RouteNo ?? r.Code, r.VehicleId, r.DriverId }).ToDictionaryAsync(r => r.Id);
            var vehicleIds = routes.Values.Where(r => r.VehicleId != null).Select(r => r.VehicleId!.Value).Distinct().ToList();
            var vehicles = await db.Vehicle.AsNoTracking().Where(v => vehicleIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, v => v.LicensePlate ?? v.Code);
            var driverIds = routes.Values.Where(r => r.DriverId != null).Select(r => r.DriverId!.Value).Distinct().ToList();
            var drivers = await db.Employee.AsNoTracking().Where(x => driverIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FullName);
            var stopIds = events.Where(e => e.DeliveryStopId != null).Select(e => e.DeliveryStopId!.Value).Distinct().ToList();
            var stops = await db.DeliveryStop.AsNoTracking().Where(s => stopIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.SequenceNo);
            var userIds = events.Where(e => e.RecordedBy != null).Select(e => e.RecordedBy!.Value).Distinct().ToList();
            var users = await db.User.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.UserName);
            return events.Select(e =>
            {
                var route = routes.GetValueOrDefault(e.DeliveryRouteId);
                return new DeliveryEventItemModel
                {
                    Id = e.Id,
                    DeliveryRouteId = e.DeliveryRouteId,
                    RouteNumber = route?.Number,
                    VehiclePlate = route?.VehicleId is { } v ? vehicles.GetValueOrDefault(v) : null,
                    DriverName = route?.DriverId is { } d ? drivers.GetValueOrDefault(d) : null,
                    DeliveryStopId = e.DeliveryStopId,
                    StopSequenceNo = e.DeliveryStopId is { } s && stops.TryGetValue(s, out int seq) ? seq : null,
                    DeliveryOrderId = e.DeliveryOrderId,
                    EventType = e.EventType,
                    EventTypeName = DeliveryCode.EventLabel(e.EventType),
                    EventAt = e.EventAt,
                    Latitude = e.Latitude,
                    Longitude = e.Longitude,
                    TemperatureC = e.TemperatureC,
                    PhotoUrl = e.PhotoUrl,
                    Description = e.Description,
                    RecordedByName = e.RecordedBy is { } u ? users.GetValueOrDefault(u) : null,
                    SourceChannel = e.SourceChannel,
                };
            }).ToList();
        }
    }
}
