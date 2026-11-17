using System.Text.Json;
using AppDelicare.Data.Connection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AppDelicare.WebApi.Infrastructure
{
    /// <summary>
    /// T48.1: <c>/health</c> = API + kết nối DB (dùng cho IIS / Docker / giám sát); <c>/health/live</c> = chỉ tiến trình còn sống.
    /// Không cần đăng nhập, không lộ chuỗi kết nối hay chi tiết lỗi.
    /// </summary>
    public static class HealthChecks
    {
        public const string DbTag = "db";

        public static IServiceCollection AddDelicareHealthChecks(this IServiceCollection services)
        {
            services.AddHealthChecks().AddDbContextCheck<DatabaseContext>("database", tags: [DbTag])
                // T48.4 (D-60): giờ SQL Server lệch giờ VN của IClock > 5 phút → Degraded (HTTP vẫn 200, giám sát thấy "Degraded").
                .AddCheck<DatabaseClockHealthCheck>("dbclock", tags: [DbTag]);
            return services;
        }

        public static void MapDelicareHealthChecks(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = WriteAsync }).AllowAnonymous();
            endpoints.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false, ResponseWriter = WriteAsync }).AllowAnonymous();
        }

        private static Task WriteAsync(HttpContext context, HealthReport report)
        {
            context.Response.ContentType = "application/json";
            var body = new
            {
                status = report.Status.ToString(),
                totalMs = (int)report.TotalDuration.TotalMilliseconds,
                checks = report.Entries.ToDictionary(e => e.Key, e => new { status = e.Value.Status.ToString(), ms = (int)e.Value.Duration.TotalMilliseconds, description = e.Key == "dbclock" ? e.Value.Description : null }),
                environment = context.RequestServices.GetRequiredService<IWebHostEnvironment>().EnvironmentName,
                version = typeof(HealthChecks).Assembly.GetName().Version?.ToString(),
            };
            return context.Response.WriteAsync(JsonSerializer.Serialize(body));
        }
    }

    /// <summary>T48.4 (D-60): so giờ SQL Server với giờ VN của ứng dụng (DatabaseClockCheck).</summary>
    public sealed class DatabaseClockHealthCheck(IConfiguration configuration, AppDelicare.Data.Share.IClock clock) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                string? cs = configuration.GetConnectionString(AppDelicare.Model.Helper.ConstantHelper.MainConnectionString);
                if (string.IsNullOrWhiteSpace(cs))
                {
                    return HealthCheckResult.Degraded("Thiếu chuỗi kết nối");
                }
                TimeSpan drift = await AppDelicare.Services.Ioc.DatabaseClockCheck.GetDriftAsync(cs, clock, cancellationToken);
                return drift.Duration() > AppDelicare.Services.Ioc.DatabaseClockCheck.MaxDrift
                    ? HealthCheckResult.Degraded(AppDelicare.Services.Ioc.DatabaseClockCheck.Describe(drift))
                    : HealthCheckResult.Healthy($"Lệch {drift.TotalSeconds:0} giây");
            }
            catch (Exception)
            {
                return HealthCheckResult.Degraded("Không đọc được giờ SQL Server");
            }
        }
    }
}
