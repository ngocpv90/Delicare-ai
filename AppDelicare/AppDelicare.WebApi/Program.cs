using Autofac.Extensions.DependencyInjection;
using Serilog;

namespace AppDelicare.WebApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseServiceProviderFactory(new AutofacServiceProviderFactory())
                // T04: Serilog.AspNetCore 8 chỉ còn UseSerilog trên IHostBuilder (bản trên IWebHostBuilder đã bị bỏ).
                .UseSerilog((hostingContext, loggerConfiguration) => AppDelicare.Services.Ioc.SerilogSetup.Configure(loggerConfiguration, hostingContext.Configuration)) // T48.4: Logging:ConsoleOnly trong Docker
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}