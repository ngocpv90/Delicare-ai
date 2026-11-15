using Autofac;
using AutoMapper;
using GemBox.Document;
using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.QuickPulse;
using Microsoft.OpenApi.Models;
using AppDelicare.WebApi.Infrastructure;
using AppDelicare.WebApi.Ioc;

namespace AppDelicare.WebApi
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "ASP0000:Do not call 'IServiceCollection.BuildServiceProvider' in 'ConfigureServices'", Justification = "<Pending>")]
    public class Startup(IConfiguration configuration)
    {
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
            ServiceLocator.SetLocatorProvider(services.BuildServiceProvider());
            services.AddCors();
            services.AddControllers();
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
                c.SwaggerDoc("v1", new OpenApiInfo { Title = Configuration["AppSettingHelper:ApiName"]?.ToString(), Version = "v1" });
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "JWT Authorization header using the Bearer scheme."
                });
                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                          new OpenApiSecurityScheme
                            {
                                Reference = new OpenApiReference
                                {
                                    Type = ReferenceType.SecurityScheme,
                                    Id = "Bearer"
                                }
                            },
                            Array.Empty<string>()

                    }
                });
            });
        }

        public void ConfigureContainer(ContainerBuilder builder)
        {
            builder.RegisterModule<ComponentRegistrar>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            app.UseAuthentication();
            app.UseCors(x => x
              .AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                string swaggerJsonBasePath = string.IsNullOrWhiteSpace(c.RoutePrefix) ? "." : "..";
                c.SwaggerEndpoint($"{swaggerJsonBasePath}" + "/swagger/v1/swagger.json", Configuration["AppSettingHelper:ApiName"]?.ToString());

            });
            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.UseHttpsRedirection();
            app.UseRouting();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
            app.UseMiddleware<CorsMiddleware>();
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
