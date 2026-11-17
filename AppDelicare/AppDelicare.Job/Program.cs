using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AppDelicare.Job;
using AppDelicare.Model.Helper;
using AppDelicare.Services.Ioc;
using AppDelicare.Services.Jobs;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Serilog;

// T23 (D-48): AppDelicare.Job – hẹn giờ gọi Service cũ (ARCHITECTURE.md mục 1b).
//   dotnet AppDelicare.Job.dll                 → chạy liên tục, mỗi 30 giây kiểm job đến giờ (giờ VN), chạy bù job lỡ giờ trong ngày.
//   dotnet AppDelicare.Job.dll --run <JobCode> → chạy 1 job ngay rồi thoát (mã thoát 0 = Succeeded / Skipped, 1 = lỗi) – dùng cho Task Scheduler / kiểm tra.
//   dotnet AppDelicare.Job.dll --list          → in danh sách job + lần chạy kế tiếp rồi thoát.
// Môi trường: DOTNET_ENVIRONMENT hoặc ASPNETCORE_ENVIRONMENT (giống WebApi). Bí mật (chuỗi kết nối) qua biến môi trường / user-secrets.
string environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
    ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
    ?? "Production";
string? runOnce = ArgValue(args, "--run");
bool listOnly = args.Contains("--list", StringComparer.OrdinalIgnoreCase);

IHost host = Host.CreateDefaultBuilder(args)
    .UseEnvironment(environment)
    .UseContentRoot(AppContext.BaseDirectory)
    .UseServiceProviderFactory(new AutofacServiceProviderFactory())
    .ConfigureAppConfiguration((context, config) =>
    {
        config.Sources.Clear();
        config.SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.shared.json", optional: false)
            .AddJsonFile($"appsettings.shared.{environment}.json", optional: true)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true);
        if (context.HostingEnvironment.IsDevelopment())
        {
            config.AddUserSecrets(typeof(JobSchedulerService).Assembly, optional: true);
        }
        config.AddEnvironmentVariables();
    })
    .UseSerilog((context, logger) => logger.ReadFrom.Configuration(context.Configuration))
    .ConfigureServices((context, services) =>
    {
        if (string.IsNullOrWhiteSpace(context.Configuration.GetConnectionString(ConstantHelper.MainConnectionString)))
        {
            throw new InvalidOperationException("Thiếu ConnectionStrings:MainConnectionString (biến môi trường ConnectionStrings__MainConnectionString).");
        }
        services.AddDelicareCore(context.Configuration, context.HostingEnvironment.ContentRootPath);
        if (runOnce == null && !listOnly)
        {
            services.AddHostedService<JobSchedulerService>();
        }
    })
    .ConfigureContainer<ContainerBuilder>((_, builder) => builder.RegisterModule<DelicareServicesModule>())
    .Build();

if (listOnly)
{
    await using var scope = host.Services.CreateAsyncScope();
    foreach (var job in await scope.ServiceProvider.GetRequiredService<IJobRunService>().GetJobs())
    {
        Console.WriteLine($"{job.JobCode,-16} {(job.Enabled ? "bật" : "TẮT"),-4} {job.At}  kế tiếp {job.NextRunAt:dd/MM/yyyy HH:mm}  {job.JobName}"
            + (job.LastRun is { } last ? $"  | gần nhất {last.StartedAt:dd/MM HH:mm} {last.RunStatus}" : string.Empty));
    }
    return 0;
}
if (runOnce != null)
{
    await using var scope = host.Services.CreateAsyncScope();
    var result = await scope.ServiceProvider.GetRequiredService<IJobRunService>().Run(runOnce, JobTrigger.Schedule);
    Log.Information("[Job] --run {JobCode}: {Code} {Message}", runOnce, result.ResponseCode, result.ResponseMessage);
    await Log.CloseAndFlushAsync();
    return result.ResponseCode == 200 || result.Data?.RunStatus == JobRunStatus.Skipped ? 0 : 1;
}
await host.RunAsync();
return 0;

static string? ArgValue(string[] args, string name)
{
    int i = Array.FindIndex(args, a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}
