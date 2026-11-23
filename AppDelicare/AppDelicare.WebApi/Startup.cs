using System.Reflection;
using Autofac;
using AutoMapper;
using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.QuickPulse;
using Microsoft.OpenApi.Models;
using AppDelicare.WebApi.Infrastructure;
using AppDelicare.WebApi.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using AppDelicare.WebApi.Ioc;

namespace AppDelicare.WebApi
{
    public class Startup(IConfiguration configuration, IWebHostEnvironment environment)
    {
        public const string CorsPolicyName = "Default";

        public IConfiguration Configuration { get; } = configuration;

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // T01: thiếu bí mật bắt buộc thì dừng ngay, báo rõ khóa nào thiếu.
            StartupSecrets.Validate(Configuration);
            // T23 (D-48): phần dùng chung với AppDelicare.Job (AppDelicare.Services/Ioc): ConfigurationCommon cho lớp cũ, HttpContextAccessor, MemoryCache,
            // IClock + BusinessRules / Permission / ReferenceLists / DocumentStorage options, IReferenceGuard, DbContext, AutoMapper.
            AppDelicare.Services.Ioc.ServiceRegistration.AddDelicareCore(services, Configuration, environment.ContentRootPath);

            services.AddApplicationInsightsTelemetry();
            string? quickPulseApiKey = Configuration[StartupSecrets.QuickPulseApiKey];
            if (!string.IsNullOrWhiteSpace(quickPulseApiKey))
            {
                services.ConfigureTelemetryModule<QuickPulseTelemetryModule>((module, o) => module.AuthenticationApiKey = quickPulseApiKey);
            }

            // T03: CORS theo danh sách origin cấu hình (thay AllowAnyOrigin + CorsMiddleware cũ).
            // Angular dev chạy qua proxy cùng origin (proxy.conf.json) nên không cần CORS; danh sách này cho môi trường tách domain.
            string[] allowedOrigins = Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
            services.AddCors(options => options.AddPolicy(CorsPolicyName, policy =>
            {
                policy.WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .WithExposedHeaders(CorrelationIdMiddleware.HeaderName, "Content-Disposition");
            }));

            // D-30: khung cũ – controller `api/[controller]/<Action>` trả Result<T> / Results<T>. JSON camelCase, không bao giờ trả mật khẩu (ApiJson).
            services.AddControllers(options =>
                {
                    // T07: phân quyền UserRole × ObjectMenu; controller CRUD cũ được gắn quyền tự động.
                    options.Filters.Add<PermissionFilter>();
                    // Lỗi UAT 2026-11-21: cột mã danh mục (ReferenceCatalogs) chỉ nhận giá trị trong ReferenceLists – Insert / Update chung.
                    options.Filters.Add<ReferenceListFilter>();
                    options.Conventions.Add(new PermissionConvention());
                    // FE-2: form đọc số / ngày theo InvariantCulture (máy chủ vi-VN không biến "12.5" thành 125); dữ liệu ra không đổi.
                    InvariantCultureValueProviderFactory.Apply(options.ValueProviderFactories);
                })
                .AddJsonOptions(options => ApiJson.Configure(options.JsonSerializerOptions));
            services.AddDelicareHealthChecks(); // T48.1
            // T03: mọi ProblemDetails do MVC sinh (400 validation, Problem()) mang traceId = CorrelationId.
            services.AddProblemDetails(options => options.CustomizeProblemDetails = ctx =>
                ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.Items[AppDelicare.Data.Share.CurrentActor.CorrelationIdItemKey] as string ?? ctx.HttpContext.TraceIdentifier);
            services.InitAuthentication(Configuration);
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = Configuration["AppSettingHelper:ApiName"]?.ToString(),
                    Version = "v1",
                    Description = "DELICARE backend API – khung cũ (D-30): route `api/<Controller>/<Action>`; kết quả `Result<T>` { responseCode, responseMessage, data } / `Results<T>` { responseCode, responseMessage, listData, totalRecords }; lỗi nghiệp vụ trả responseCode = 400 kèm thông báo tiếng Việt. Mọi response có header `X-Correlation-Id`. Ánh xạ màn hình → action: docs/frontend-handoff/.",
                });
                // Nhóm theo controller.
                c.TagActionsBy(api => [api.ActionDescriptor.RouteValues["controller"]]);
                c.OrderActionsBy(api => api.RelativePath);
                // JWT: dán token (không cần chữ "Bearer ").
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Dán JWT lấy từ POST /api/Authentication/Login (trường data.token).",
                });
                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
                        Array.Empty<string>()
                    }
                });
                // XML comments (summary, example) của WebApi và Model.
                foreach (var xml in new[] { Assembly.GetExecutingAssembly().GetName().Name, "AppDelicare.Model" })
                {
                    string path = Path.Combine(AppContext.BaseDirectory, $"{xml}.xml");
                    if (File.Exists(path))
                    {
                        c.IncludeXmlComments(path, includeControllerXmlComments: true);
                    }
                }
            });
        }

        public void ConfigureContainer(ContainerBuilder builder)
        {
            builder.RegisterModule<ComponentRegistrar>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        // T03 thứ tự: CorrelationId → ExceptionHandler → HttpsRedirection → Swagger/Static → Routing → CORS → Authentication → Authorization → Endpoints.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseMiddleware<CorrelationIdMiddleware>();
            app.UseExceptionHandler(errorApp => errorApp.Run(ProblemDetailsExceptionHandler.HandleAsync));
            app.UseHttpsRedirection();
            // Rà soát BE: Production tắt Swagger (lộ toàn bộ danh sách API) – bật lại bằng Swagger:Enabled=true nếu cần. Dev / Staging giữ như cũ.
            if (!env.IsProduction() || Configuration.GetValue<bool>("Swagger:Enabled"))
            {
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    string swaggerJsonBasePath = string.IsNullOrWhiteSpace(c.RoutePrefix) ? "." : "..";
                    c.SwaggerEndpoint($"{swaggerJsonBasePath}" + "/swagger/v1/swagger.json", Configuration["AppSettingHelper:ApiName"]?.ToString());
                });
            }
            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseCors(CorsPolicyName);
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapDelicareHealthChecks(); // T48.1: /health, /health/live
            });

            //SyncfusionLicenseProvider.RegisterLicense("YOUR LICENSE KEY");
            // T01: license GemBox đọc từ cấu hình (trống → FREE-LIMITED). Chỉ đặt 1 lần mỗi tiến trình.
            GemBoxLicense.Apply(Configuration[StartupSecrets.GemBoxLicense]);
        }
    }
}
