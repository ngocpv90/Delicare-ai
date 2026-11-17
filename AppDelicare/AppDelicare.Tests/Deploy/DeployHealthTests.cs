using AppDelicare.Model.Helper;
using AppDelicare.Services.Ioc;
using AppDelicare.Services.Jobs;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Deploy
{
    /// <summary>T48.4 (D-60): file nhịp của Job (Docker HEALTHCHECK), log chỉ console trong Docker, kiểm lệch giờ SQL Server.</summary>
    public class DeployHealthTests
    {
        [Fact]
        public void Heartbeat_is_fresh_only_within_max_age()
        {
            string path = Path.Combine(Path.GetTempPath(), "delicare-hb-test-" + Guid.NewGuid().ToString("N"), "beat");
            try
            {
                var now = new DateTime(2031, 3, 20, 3, 0, 0, DateTimeKind.Utc);
                JobHeartbeat.IsFresh(path, TimeSpan.FromMinutes(2), now).Should().BeFalse("chưa có file");
                JobHeartbeat.Write(path, now);
                JobHeartbeat.IsFresh(path, TimeSpan.FromMinutes(2), now.AddSeconds(90)).Should().BeTrue();
                JobHeartbeat.IsFresh(path, TimeSpan.FromMinutes(2), now.AddMinutes(3)).Should().BeFalse("vòng hẹn giờ đứng quá 2 phút");
                File.WriteAllText(path, "rác");
                JobHeartbeat.IsFresh(path, TimeSpan.FromMinutes(2), now).Should().BeFalse("nội dung hỏng");
            }
            finally
            {
                Directory.Delete(Path.GetDirectoryName(path)!, true);
            }
        }

        [Fact]
        public void Console_only_drops_file_sinks_and_keeps_others()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Serilog:MinimumLevel"] = "Information",
                ["Serilog:WriteTo:0:Name"] = "Console",
                ["Serilog:WriteTo:0:Args:outputTemplate"] = "{Message}",
                ["Serilog:WriteTo:1:Name"] = "File",
                ["Serilog:WriteTo:1:Args:path"] = "Logs/x.log",
                ["Serilog:WriteTo:2:Name"] = "Async",
                [SerilogSetup.ConsoleOnlyKey] = "true",
            }).Build();
            SerilogSetup.ConsoleOnly(config).Should().BeTrue();
            var trimmed = SerilogSetup.WithoutFileSinks(config);
            trimmed["Serilog:WriteTo:0:Name"].Should().Be("Console");
            trimmed["Serilog:WriteTo:0:Args:outputTemplate"].Should().Be("{Message}");
            trimmed["Serilog:WriteTo:1:Name"].Should().Be("Async", "đánh lại chỉ số liên tục");
            trimmed["Serilog:WriteTo:2:Name"].Should().BeNull();
            trimmed["Serilog:MinimumLevel"].Should().Be("Information");
            trimmed.AsEnumerable().Should().NotContain(kv => kv.Value == "Logs/x.log");
            SerilogSetup.ConsoleOnly(new ConfigurationBuilder().Build()).Should().BeFalse("mặc định giữ log file (chạy ngoài Docker)");
        }

        [Fact]
        public void Describe_drift_mentions_timezone_fix()
            => DatabaseClockCheck.Describe(TimeSpan.FromHours(-15)).Should().Contain("-900").And.Contain("TZ=Asia/Ho_Chi_Minh");
    }

    /// <summary>T48.4: đọc giờ SQL Server thật (DB dev) – chỉ kiểm đọc được và ra số hữu hạn (máy dev có thể không đặt giờ VN).</summary>
    [Collection(ApiCollection.Name)]
    public class DatabaseClockSqlServerTests(ApiFactory factory)
    {
        [SkippableFact]
        public async Task Reads_sql_server_clock_drift()
        {
            Skip.If(factory.SkipReason != null, factory.SkipReason);
            using var scope = factory.Services.CreateScope();
            string cs = scope.ServiceProvider.GetRequiredService<IConfiguration>().GetConnectionString(ConstantHelper.MainConnectionString)!;
            var clock = scope.ServiceProvider.GetRequiredService<AppDelicare.Data.Share.IClock>();
            TimeSpan drift = await DatabaseClockCheck.GetDriftAsync(cs, clock);
            drift.Duration().Should().BeLessThan(TimeSpan.FromHours(27));
        }
    }
}
