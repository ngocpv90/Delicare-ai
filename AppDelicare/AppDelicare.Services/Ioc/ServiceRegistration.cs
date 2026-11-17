using AppDelicare.Data.Connection;
using AppDelicare.Data.Share;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Option;
using AppDelicare.Services.Mapping;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Services.Ioc
{
    /// <summary>
    /// T23 (D-48): phần cấu hình dùng chung giữa AppDelicare.WebApi và AppDelicare.Job – cấu hình tĩnh cho lớp cũ, IClock, các Options nghiệp vụ,
    /// DbContext, AutoMapper. Phần riêng của web (JWT, CORS, Swagger, health check) vẫn ở WebApi/Startup.
    /// </summary>
    public static class ServiceRegistration
    {
        /// <param name="contentRootPath">Thư mục gốc của ứng dụng – đường dẫn tương đối của <c>DocumentStorage:RootPath</c> tính từ đây.</param>
        public static IServiceCollection AddDelicareCore(this IServiceCollection services, IConfiguration configuration, string contentRootPath)
        {
            // Cho các lớp cũ đọc cấu hình tĩnh (DapperRepository, UnitOfWork) thấy cả user-secrets / biến môi trường.
            AppDelicare.Model.Common.ConfigurationCommon.Initialize(configuration);
            services.AddHttpContextAccessor();
            services.AddMemoryCache();
            // T45R: IClock, BusinessRules, quyền ở tầng cũ.
            services.Configure<BusinessRulesOptions>(configuration.GetSection(BusinessRulesOptions.Section));
            services.AddSingleton<IClock, SystemClock>();
            services.Configure<PermissionOptions>(configuration.GetSection(PermissionOptions.Section));
            services.Configure<ReferenceListsOptions>(configuration.GetSection(ReferenceListsOptions.Section)); // T45B.5
            // BE2.7: tệp đính kèm lưu NGOÀI wwwroot; đường dẫn tương đối tính từ ContentRoot (Docker / IIS: gắn volume / thư mục có quyền ghi – deploy/README.md).
            services.AddOptions<DocumentStorageOptions>()
                .Bind(configuration.GetSection(DocumentStorageOptions.Section))
                .PostConfigure(o =>
                {
                    string root = string.IsNullOrWhiteSpace(o.RootPath) ? "App_Data/DocumentFiles" : o.RootPath;
                    o.RootPath = Path.GetFullPath(Path.IsPathRooted(root) ? root : Path.Combine(contentRootPath, root));
                });
            services.AddScoped<IReferenceGuard, ReferenceGuard>();
            services.AddDbContext<DatabaseContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString(ConnectionHelper.DatabaseName.MainConnectionString.ToString())));
            var mapperConfiguration = new MapperConfiguration(x =>
            {
                x.AddProfile(new AutoMapperConfiguraion());
                x.AddProfile(new AutoMapperCustomConfiguraion());
                x.AddProfile(new AutoMapperGeneratedProfile()); // T05: CodeGen
            });
            services.AddSingleton(mapperConfiguration.CreateMapper());
            return services;
        }
    }
}
