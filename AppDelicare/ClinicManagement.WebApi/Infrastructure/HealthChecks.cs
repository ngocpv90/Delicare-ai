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
            services.AddHealthChecks().AddDbContextCheck<DatabaseContext>("database", tags: [DbTag]);
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
                checks = report.Entries.ToDictionary(e => e.Key, e => new { status = e.Value.Status.ToString(), ms = (int)e.Value.Duration.TotalMilliseconds }),
                environment = context.RequestServices.GetRequiredService<IWebHostEnvironment>().EnvironmentName,
                version = typeof(HealthChecks).Assembly.GetName().Version?.ToString(),
            };
            return context.Response.WriteAsync(JsonSerializer.Serialize(body));
        }
    }
}
