using Microsoft.Extensions.Configuration;
using Serilog;

namespace AppDelicare.Services.Ioc
{
    /// <summary>
    /// T48.4 (D-60): cấu hình Serilog dùng chung WebApi + Job. <c>Logging:ConsoleOnly = true</c> (đặt trong Docker Staging / Production) → bỏ mọi sink
    /// <c>File</c> trong <c>Serilog:WriteTo</c>, chỉ ghi console (Docker giữ + xoay vòng log bằng logging driver). Chạy ngoài Docker (máy dev) giữ nguyên log file.
    /// </summary>
    public static class SerilogSetup
    {
        public const string ConsoleOnlyKey = "Logging:ConsoleOnly";

        public static LoggerConfiguration Configure(LoggerConfiguration logger, IConfiguration configuration)
            => logger.ReadFrom.Configuration(ConsoleOnly(configuration) ? WithoutFileSinks(configuration) : configuration);

        public static bool ConsoleOnly(IConfiguration configuration)
            => bool.TryParse(configuration[ConsoleOnlyKey], out bool value) && value;

        /// <summary>Bản sao mục <c>Serilog</c> đã bỏ các phần tử <c>WriteTo:&lt;i&gt;</c> có <c>Name = File</c> (đánh lại chỉ số liên tục).</summary>
        public static IConfiguration WithoutFileSinks(IConfiguration configuration)
        {
            var serilog = configuration.GetSection("Serilog");
            var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in serilog.AsEnumerable(makePathsRelative: true))
            {
                if (item.Key.StartsWith("WriteTo", StringComparison.OrdinalIgnoreCase) || item.Value == null)
                {
                    continue;
                }
                data["Serilog:" + item.Key] = item.Value;
            }
            int index = 0;
            foreach (var sink in serilog.GetSection("WriteTo").GetChildren())
            {
                if (string.Equals(sink["Name"], "File", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                foreach (var item in sink.AsEnumerable(makePathsRelative: true).Where(i => i.Value != null))
                {
                    data[$"Serilog:WriteTo:{index}:{item.Key}"] = item.Value;
                }
                index++;
            }
            // "Using" vẫn liệt kê Serilog.Sinks.File – vô hại khi không có sink File.
            return new ConfigurationBuilder().AddInMemoryCollection(data).Build();
        }
    }
}
