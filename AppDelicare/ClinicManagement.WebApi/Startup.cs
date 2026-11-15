using System.Reflection;
using Autofac;
using AutoMapper;
using GemBox.Document;
using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.QuickPulse;
using Microsoft.OpenApi.Models;
using AppDelicare.WebApi.Infrastructure;
using AppDelicare.WebApi.Ioc;

namespace AppDelicare.WebApi
{
    public class Startup(IConfiguration configuration)
    {
        public const string CorsPolicyName = "Default";

        public IConfiguration Configuration { get; } = configuration;

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // T01: thiếu bí mật bắt buộc thì dừng ngay, báo rõ khóa nào thiếu.
            StartupSecrets.Validate(Configuration);
            // Cho các lớp cũ đọc cấu hình tĩnh (DapperRepository, UnitOfWork) thấy cả user-secrets / biến môi trường.
            AppDelicare.Model.Common.ConfigurationCommon.Initialize(Configuration);

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

            services.AddControllers(options => options.Conventions.Add(new ApiV1Convention()));
            // T03: mọi ProblemDetails do MVC sinh (400 validation, Problem()) mang traceId = CorrelationId.
            services.AddProblemDetails(options => options.CustomizeProblemDetails = ctx =>
                ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.Items[AppDelicare.Data.Share.CurrentActor.CorrelationIdItemKey] as string ?? ctx.HttpContext.TraceIdentifier);
            services.InitConnection(Configuration);
            services.InitAuthentication(Configuration);
            var autoMapper = new MapperConfiguration(x =>
            {
                x.AddProfile(new AutoMapperConfiguraion());
                x.AddProfile(new AutoMapperCustomConfiguraion());
            });
            IMapper mapper = autoMapper.CreateMapper();
            services.AddSingleton(mapper);
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = Configuration["AppSettingHelper:ApiName"]?.ToString(),
                    Version = "v1",
                    Description = "DELICARE backend API. Route cũ `api/[controller]/...` giữ nguyên cho Angular (HTTP 200 + `responseCode` trong body). "
                        + "Route mới `api/v1/...` trả mã HTTP thật, lỗi dạng ProblemDetails (RFC 9457). Mọi response có header `X-Correlation-Id`.",
                });
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
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                string swaggerJsonBasePath = string.IsNullOrWhiteSpace(c.RoutePrefix) ? "." : "..";
                c.SwaggerEndpoint($"{swaggerJsonBasePath}" + "/swagger/v1/swagger.json", Configuration["AppSettingHelper:ApiName"]?.ToString());
            });
            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseCors(CorsPolicyName);
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            //SyncfusionLicenseProvider.RegisterLicense("YOUR LICENSE KEY");
            // T01: license GemBox đọc từ cấu hình. Không có thì chạy chế độ miễn phí giới hạn của GemBox.
            string? gemBoxLicense = Configuration[StartupSecrets.GemBoxLicense];
            if (string.IsNullOrWhiteSpace(gemBoxLicense))
            {
                Serilog.Log.Warning("Chưa cấu hình '{Key}': GemBox.Document chạy chế độ FREE-LIMITED.", StartupSecrets.GemBoxLicense);
                gemBoxLicense = "FREE-LIMITED-KEY";
            }
            ComponentInfo.SetLicense(gemBoxLicense);
            ComponentInfo.FreeLimitReached += (sender, e) => e.FreeLimitReachedAction = FreeLimitReachedAction.ContinueAsTrial;
        }
    }
}
