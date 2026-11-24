using System.Net.Http.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Qc
{
    /// <summary>
    /// UI-6.31 (D-112): "Ghi một lượt" (1 khu ngoài ngưỡng thiếu khắc phục → lỗi cả lượt), bảng "Hôm nay" theo khu (đến giờ chưa đo sau 60′ đầu ca,
    /// cảm biến mất tín hiệu sau 30′, có số đo lại → hết; ngoài ngưỡng có lần đo sau đạt → đã khắc phục), Sổ nhiệt độ (mặc định bỏ lõi mẻ), lõi mẻ vẫn chặn ghi ở đây.
    /// </summary>
    public class TemperatureBoardUi631Tests : IClassFixture<InMemoryApiFactory>
    {
        private static readonly DateTime Day = new(2036, 5, 12);
        private static readonly Guid Wh = Guid.NewGuid(), Z1 = Guid.NewGuid(), Z2 = Guid.NewGuid(), Z3 = Guid.NewGuid(), Z4 = Guid.NewGuid(), Z5 = Guid.NewGuid();
        private readonly InMemoryApiFactory _factory;

        public TemperatureBoardUi631Tests(InMemoryApiFactory factory)
        {
            _factory = factory;
            _factory.Clock.FixedNow = Day.AddHours(10).AddMinutes(40);
            _factory.Settings["Iot:Devices:CB-631:StorageZoneId"] = Z4.ToString();
        }

        private static async Task<LegacyResult> PostJson(HttpClient c, string url, object body) => await LegacyResult.ReadAsync(await c.PostAsJsonAsync(url, body));

        private static Dictionary<Guid, string> Status(System.Text.Json.JsonElement board)
            => board.GetProperty("listZone").EnumerateArray().ToDictionary(z => z.GetProperty("storageZoneId").GetGuid(), z => z.GetProperty("status").GetString()!);

        [Fact]
        public async Task Round_board_statuses_and_book()
        {
            await _factory.SeedAsync(db =>
            {
                db.Shift.Add(new Shift { Id = Guid.NewGuid(), Code = "S631", Name = "Ca 631", StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(14, 0, 0), Status = "Active" });
                db.Warehouse.Add(new Warehouse { Id = Wh, Code = "K631", Name = "Kho 631", Status = "Active" });
                db.StorageZone.AddRange(
                    new StorageZone { Id = Z1, WarehouseId = Wh, Code = "MAT631", Name = "Kho mát 631", TemperatureMin = 0, TemperatureMax = 5, Status = "Active" },
                    new StorageZone { Id = Z2, WarehouseId = Wh, Code = "DONG631", Name = "Kho đông 631", TemperatureMax = -18, Status = "Active" },
                    new StorageZone { Id = Z3, WarehouseId = Wh, Code = "KHO631", Name = "Kho khô 631", Status = "Active" },
                    new StorageZone { Id = Z4, WarehouseId = Wh, Code = "CB631", Name = "Tủ mát cảm biến 631", TemperatureMin = 0, TemperatureMax = 5, Status = "Active" },
                    new StorageZone { Id = Z5, WarehouseId = Wh, Code = "CS631", Name = "Khu chia suất 631", TemperatureMin = 15, TemperatureMax = 25, Status = "Active" });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();
            object Round(string? corrective) => new
            {
                listItem = new object[]
                {
                    new { storageZoneId = Z1, temperatureC = 8m, correctiveAction = corrective }, new { storageZoneId = Z2, temperatureC = -20m },
                    new { storageZoneId = Z3, temperatureC = 27m }, new { storageZoneId = Z4, temperatureC = 3m }, new { storageZoneId = Z5, temperatureC = 20m },
                },
            };
            (await PostJson(c, "/api/TemperatureLog/RecordRound", Round(null))).Fail("bắt buộc ghi hành động khắc phục");
            (await _factory.QueryAsync(db => db.TemperatureLog.AsNoTracking().CountAsync(t => t.StorageZoneId == Z1 || t.StorageZoneId == Z2 || t.StorageZoneId == Z3 || t.StorageZoneId == Z4 || t.StorageZoneId == Z5))).Should().Be(0, "lỗi cả lượt – không lưu khu nào");
            var round = (await PostJson(c, "/api/TemperatureLog/RecordRound", Round("Đóng cửa tủ, chuyển hàng sang tủ khác"))).Ok();
            (round.GetProperty("saved").GetInt32(), round.GetProperty("outOfRange").GetInt32()).Should().Be((5, 1));
            var types = await _factory.QueryAsync(db => db.TemperatureLog.AsNoTracking().Where(t => t.StorageZoneId == Z5).Select(t => t.MeasurementType).ToListAsync());
            types.Should().Equal("PACKING_AREA");

            // 10:40: khắc phục đã ghi → khu mát Đạt; cảm biến chưa gửi lần nào → mất tín hiệu.
            var board = (await c.GetResultAsync($"/api/TemperatureLog/GetDayBoard?date={Day:yyyy-MM-dd}")).Ok();
            var st = Status(board);
            (st[Z1], st[Z2], st[Z4]).Should().Be(("OK", "OK", "SILENT"));
            board.GetProperty("out7dResolved").GetInt32().Should().Be(1);

            // Ngày sau 11:30: khu đo tay có ngưỡng chưa đo trong ca (quá 60′ đầu ca) → đến giờ chưa đo; kho khô không ngưỡng → không nhắc.
            _factory.Clock.FixedNow = Day.AddDays(1).AddHours(11).AddMinutes(30);
            await _factory.SeedAsync(db =>
            {
                db.TemperatureLog.Add(new TemperatureLog { Id = Guid.NewGuid(), StorageZoneId = Z4, MeasurementType = "STORAGE", TemperatureC = 3, MinAllowedC = 0, MaxAllowedC = 5, IsWithinRange = true, MeasuredAt = Day.AddDays(1).AddHours(11).AddMinutes(25), SourceChannel = "IOT", DeviceCode = "CB-631" });
                return Task.CompletedTask;
            });
            st = Status((await c.GetResultAsync($"/api/TemperatureLog/GetDayBoard?date={Day.AddDays(1):yyyy-MM-dd}")).Ok());
            (st[Z1], st[Z2], st[Z5], st[Z4]).Should().Be(("DUE", "DUE", "DUE", "OK"), "cảm biến có số đo 5′ trước → hết mất tín hiệu");
            st[Z3].Should().NotBe("DUE");

            // Ghi lẻ ngoài ngưỡng không khắc phục → Ngoài ngưỡng chưa khắc phục; lần đo sau đạt → đã khắc phục.
            (await c.PostFormAsync("/api/TemperatureLog/Record", ("StorageZoneId", Z1), ("MeasurementType", "STORAGE"), ("TemperatureC", 9m))).Ok();
            Status((await c.GetResultAsync($"/api/TemperatureLog/GetDayBoard?date={Day.AddDays(1):yyyy-MM-dd}")).Ok())[Z1].Should().Be("OUT");
            _factory.Clock.FixedNow = Day.AddDays(1).AddHours(11).AddMinutes(45);
            (await PostJson(c, "/api/TemperatureLog/RecordRound", new { listItem = new[] { new { storageZoneId = Z1, temperatureC = 4m } } })).Ok();
            Status((await c.GetResultAsync($"/api/TemperatureLog/GetDayBoard?date={Day.AddDays(1):yyyy-MM-dd}")).Ok())[Z1].Should().Be("OK");

            // Lõi mẻ chặn ghi ở đây; sổ mặc định bỏ loại đo nghiệp vụ.
            (await c.PostFormAsync("/api/TemperatureLog/Record", ("StorageZoneId", Z1), ("MeasurementType", "CORE"), ("TemperatureC", 75m))).Fail("màn Mẻ nấu");
            await _factory.SeedAsync(db =>
            {
                db.TemperatureLog.Add(new TemperatureLog { Id = Guid.NewGuid(), StorageZoneId = Z1, MeasurementType = "CORE", TemperatureC = 80, MeasuredAt = Day.AddDays(1).AddHours(11), SourceChannel = "MANUAL" });
                return Task.CompletedTask;
            });
            string q = $"From={Day:yyyy-MM-dd}&To={Day.AddDays(1):yyyy-MM-dd}&StorageZoneId={Z1}";
            var book = (await c.GetResultAsync($"/api/TemperatureLog/GetBook?{q}")).Ok();
            book.GetProperty("count").GetInt32().Should().Be(3);
            (await c.GetResultAsync($"/api/TemperatureLog/GetBook?{q}&IncludeProcess=true")).Ok().GetProperty("count").GetInt32().Should().Be(4);
            var outRows = (await c.GetResultAsync($"/api/TemperatureLog/GetBook?{q}&OnlyOutOfRange=true")).Ok().GetProperty("listDay").EnumerateArray().SelectMany(d => d.GetProperty("listRow").EnumerateArray()).ToList();
            outRows.Should().HaveCount(2).And.OnlyContain(r => r.GetProperty("resolved").GetBoolean(), "một lần có khắc phục, một lần có lần đo sau đạt");
            outRows.Select(r => r.GetProperty("measurementName").GetString()).Should().OnlyContain(n => n == "Kho / tủ bảo quản");
        }
    }

}
