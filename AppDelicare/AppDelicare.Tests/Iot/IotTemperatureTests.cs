using System.Net;
using System.Net.Http.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Iot
{
    /// <summary>
    /// BE3.2 (Catalog 166): cổng nhận nhiệt độ cảm biến – khóa API riêng (không JWT), ánh xạ thiết bị → khu vực theo cấu hình, ngưỡng tính ở server,
    /// ghi TemperatureLog SourceChannel = IOT dưới tài khoản system, chống trùng (DeviceCode, MeasuredAt), từ chối từng dòng lỗi, giới hạn số dòng.
    /// </summary>
    public class IotTemperatureTests : IClassFixture<InMemoryApiFactory>
    {
        public const string ApiKey = "test-iot-key-0123456789";
        private static readonly Guid SystemUser = Guid.Parse("061877ad-396c-4386-8868-25d4940bf467");
        private static readonly Guid ZoneById = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000166");
        private readonly InMemoryApiFactory _factory;

        public IotTemperatureTests(InMemoryApiFactory factory)
        {
            _factory = factory;
            _factory.Clock.FixedNow = new DateTime(2031, 3, 20, 10, 0, 0);
            _factory.Settings["Iot:ApiKeys:0"] = "other-key-not-used";
            _factory.Settings["Iot:ApiKeys:1"] = ApiKey;
            _factory.Settings["Iot:RateLimitPerMinute"] = "1000";
            _factory.Settings["Iot:Devices:SENSOR-MAT:StorageZoneCode"] = "IOT-MAT";
            _factory.Settings["Iot:Devices:SENSOR-ID:StorageZoneId"] = ZoneById.ToString();
            _factory.Settings["Iot:Devices:SENSOR-ID:MeasurementType"] = "BLAST_CHILL";
            _factory.Settings["Iot:Devices:SENSOR-OFF:StorageZoneCode"] = "IOT-MAT";
            _factory.Settings["Iot:Devices:SENSOR-OFF:Enabled"] = "false";
            _factory.Settings["Iot:Devices:SENSOR-DUP:StorageZoneCode"] = "IOT-DUP";
        }

        private async Task SeedZonesAsync()
        {
            await _factory.SeedAsync(async db =>
            {
                if (await db.StorageZone.AnyAsync(z => z.Code == "IOT-MAT"))
                {
                    return;
                }
                Guid wh1 = Guid.NewGuid(), wh2 = Guid.NewGuid();
                db.Warehouse.AddRange(
                    new Warehouse { Id = wh1, Code = "IOT-W1", Name = "Kho IoT 1", Status = "Active" },
                    new Warehouse { Id = wh2, Code = "IOT-W2", Name = "Kho IoT 2", Status = "Active" });
                db.StorageZone.AddRange(
                    new StorageZone { Id = Guid.NewGuid(), WarehouseId = wh1, Code = "IOT-MAT", Name = "Kho mát", TemperatureMin = 0m, TemperatureMax = 5m, Status = "Active" },
                    new StorageZone { Id = ZoneById, WarehouseId = wh1, Code = "IOT-LANH", Name = "Tủ làm lạnh nhanh", TemperatureMax = 3m, Status = "Active" },
                    new StorageZone { Id = Guid.NewGuid(), WarehouseId = wh1, Code = "IOT-DUP", Name = "Trùng 1", Status = "Active" },
                    new StorageZone { Id = Guid.NewGuid(), WarehouseId = wh2, Code = "IOT-DUP", Name = "Trùng 2", Status = "Active" });
            });
        }

        private HttpClient Client(string? key = ApiKey)
        {
            var client = _factory.CreateClient();
            if (key != null)
            {
                client.DefaultRequestHeaders.Add("X-Api-Key", key);
            }
            return client;
        }

        [Fact]
        public async Task Requires_valid_api_key_not_user_token()
        {
            await SeedZonesAsync();
            object[] body = [new { deviceCode = "SENSOR-MAT", measuredAt = "2031-03-20T09:00:00", temperatureC = 3.0 }];
            using (var none = Client(null))
            {
                (await none.PostAsJsonAsync("/api/IotTemperature/Receive", body)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            }
            using (var wrong = Client("sai-khoa"))
            {
                (await wrong.PostAsJsonAsync("/api/IotTemperature/Receive", body)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            }
            using (var user = _factory.CreateAuthorizedClient())
            {
                (await user.PostAsJsonAsync("/api/IotTemperature/Receive", body)).StatusCode.Should().Be(HttpStatusCode.Unauthorized, "token người dùng không thay được khóa API");
            }
        }

        [Fact]
        public async Task Receives_batch_rejects_bad_rows_dedupes_and_writes_as_system()
        {
            await SeedZonesAsync();
            using var c = Client();
            object[] body =
            [
                new { deviceCode = "sensor-mat", measuredAt = "2031-03-20T09:00:00", temperatureC = 3.5 },        // 0 đạt (mã không phân biệt hoa thường)
                new { deviceCode = "SENSOR-MAT", measuredAt = "2031-03-20T09:05:00.700", temperatureC = 7.2 },    // 1 vượt ngưỡng 0–5
                new { deviceCode = "SENSOR-ID", measuredAt = "2031-03-20T02:10:00Z", temperatureC = 2.0 },        // 2 giờ UTC → 09:10 VN; BLAST_CHILL
                new { deviceCode = "SENSOR-MAT", measuredAt = "2031-03-20T09:00:00", temperatureC = 3.5 },        // 3 trùng dòng 0
                new { deviceCode = "SENSOR-LA", measuredAt = "2031-03-20T09:00:00", temperatureC = 3.0 },         // 4 thiết bị lạ
                new { deviceCode = "SENSOR-OFF", measuredAt = "2031-03-20T09:00:00", temperatureC = 3.0 },        // 5 tạm ngưng
                new { deviceCode = "SENSOR-DUP", measuredAt = "2031-03-20T09:00:00", temperatureC = 3.0 },        // 6 mã khu vực trùng
                new { deviceCode = "SENSOR-MAT", temperatureC = 3.0 },                                            // 7 thiếu giờ
                new { deviceCode = "SENSOR-MAT", measuredAt = "2031-03-01T09:00:00", temperatureC = 3.0 },        // 8 cũ > 7 ngày
                new { deviceCode = "SENSOR-MAT", measuredAt = "2031-03-20T10:30:00", temperatureC = 3.0 },        // 9 tương lai > 5 phút
                new { deviceCode = "SENSOR-MAT", measuredAt = "2031-03-20T09:20:00", temperatureC = 999.0 },      // 10 lỗi cảm biến
                new { measuredAt = "2031-03-20T09:00:00", temperatureC = 3.0 },                                   // 11 thiếu mã
            ];
            var data = (await LegacyResult.ReadAsync(await c.PostAsJsonAsync("/api/IotTemperature/Receive", body))).Ok();
            data.GetProperty("received").GetInt32().Should().Be(12);
            data.GetProperty("accepted").GetInt32().Should().Be(3);
            data.GetProperty("duplicate").GetInt32().Should().Be(1);
            data.GetProperty("outOfRange").GetInt32().Should().Be(1);
            var rejected = data.GetProperty("rejected").EnumerateArray().ToDictionary(r => r.GetProperty("index").GetInt32(), r => r.GetProperty("reason").GetString());
            rejected.Keys.Should().BeEquivalentTo([4, 5, 6, 7, 8, 9, 10, 11]);
            rejected[4].Should().Contain("chưa khai báo");
            rejected[5].Should().Contain("tạm ngưng");
            rejected[6].Should().Contain("trùng");
            rejected[9].Should().Contain("tương lai");
            rejected[10].Should().Contain("ngoài khoảng");

            var logs = await _factory.QueryAsync(db => db.TemperatureLog.AsNoTracking().Where(t => t.SourceChannel == "IOT").OrderBy(t => t.MeasuredAt).ToListAsync());
            logs.Should().HaveCount(3);
            logs.Should().OnlyContain(t => t.RecordedBy == SystemUser && t.CreatedBy == SystemUser);
            logs[0].DeviceCode.Should().Be("SENSOR-MAT");
            logs[0].MeasurementType.Should().Be("STORAGE");
            logs[0].MinAllowedC.Should().Be(0m);
            logs[0].MaxAllowedC.Should().Be(5m);
            logs[0].IsWithinRange.Should().BeTrue();
            logs[1].MeasuredAt.Should().Be(new DateTime(2031, 3, 20, 9, 5, 0), "bỏ phần lẻ giây");
            logs[1].IsWithinRange.Should().BeFalse();
            logs[2].MeasuredAt.Should().Be(new DateTime(2031, 3, 20, 9, 10, 0), "giờ UTC đổi về giờ Việt Nam");
            logs[2].StorageZoneId.Should().Be(ZoneById);
            logs[2].MeasurementType.Should().Be("BLAST_CHILL");

            // Gửi lại cả lô (mất mạng gửi bù) → không ghi thêm.
            var again = (await LegacyResult.ReadAsync(await c.PostAsJsonAsync("/api/IotTemperature/Receive", body))).Ok();
            again.GetProperty("accepted").GetInt32().Should().Be(0);
            again.GetProperty("duplicate").GetInt32().Should().Be(4);
            (await _factory.QueryAsync(db => db.TemperatureLog.CountAsync(t => t.SourceChannel == "IOT"))).Should().Be(3);

            // Biểu đồ khu vực (BE3.1) thấy lần đo IoT.
            using var user = _factory.CreateAuthorizedClient();
            Guid mat = await _factory.QueryAsync(db => db.StorageZone.Where(z => z.Code == "IOT-MAT").Select(z => z.Id).SingleAsync());
            (await user.GetResultAsync($"/api/TemperatureLog/GetSeries?StorageZoneId={mat}")).Ok().GetProperty("outOfRangeCount").GetInt32().Should().Be(1);
        }

        [Fact]
        public async Task Rejects_empty_and_oversized_batches()
        {
            await SeedZonesAsync();
            using var c = Client();
            (await LegacyResult.ReadAsync(await c.PostAsJsonAsync("/api/IotTemperature/Receive", Array.Empty<object>()))).Fail("Không có lần đo");
            var big = Enumerable.Range(0, 501).Select(i => new { deviceCode = "SENSOR-MAT", measuredAt = new DateTime(2031, 3, 20, 8, 0, 0).AddSeconds(i), temperatureC = 3.0 }).ToArray();
            (await LegacyResult.ReadAsync(await c.PostAsJsonAsync("/api/IotTemperature/Receive", big))).Fail("500");
        }
    }

    /// <summary>BE3.2: giới hạn tần suất theo khóa (factory riêng – RateLimitPerMinute = 2).</summary>
    public class IotRateLimitTests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public IotRateLimitTests(InMemoryApiFactory factory)
        {
            _factory = factory;
            _factory.Settings["Iot:ApiKeys:0"] = IotTemperatureTests.ApiKey;
            _factory.Settings["Iot:RateLimitPerMinute"] = "2";
        }

        [Fact]
        public async Task Third_request_in_a_minute_gets_429()
        {
            using var c = _factory.CreateClient();
            c.DefaultRequestHeaders.Add("X-Api-Key", IotTemperatureTests.ApiKey);
            object[] body = [new { deviceCode = "KHONG-CO", measuredAt = "2031-03-20T09:00:00", temperatureC = 3.0 }];
            var codes = new List<HttpStatusCode>();
            for (int i = 0; i < 3; i++)
            {
                codes.Add((await c.PostAsJsonAsync("/api/IotTemperature/Receive", body)).StatusCode);
            }
            // Biên phút có thể rơi giữa các lần gọi → ít nhất 1 lần 429 khi 3 lần cùng phút; chấp nhận tối đa 1 lần "qua phút mới".
            codes.Take(2).Should().AllBeEquivalentTo(HttpStatusCode.OK);
            if (codes[2] != HttpStatusCode.TooManyRequests)
            {
                codes.Add((await c.PostAsJsonAsync("/api/IotTemperature/Receive", body)).StatusCode);
                codes.Add((await c.PostAsJsonAsync("/api/IotTemperature/Receive", body)).StatusCode);
                codes.Should().Contain(HttpStatusCode.TooManyRequests);
            }
        }
    }
}
