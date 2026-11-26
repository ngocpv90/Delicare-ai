using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Services.Ioc;
using AppDelicare.Services.Jobs;
using AppDelicare.Tests.Integration;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Dapper;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Jobs
{
    /// <summary>T23: mốc giờ chạy hằng ngày (giờ VN) – chạy bù trong ngày, lần kế tiếp.</summary>
    public class JobScheduleTests
    {
        [Theory]
        [InlineData("20:00", 20, 0)]
        [InlineData("0:15", 0, 15)]
        [InlineData(" 08:05 ", 8, 5)]
        public void ParseAt_reads_hh_mm(string at, int hour, int minute)
            => JobSchedule.ParseAt(at, "01:00").Should().Be(new TimeSpan(hour, minute, 0));

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("25:00")]
        [InlineData("8h")]
        public void ParseAt_falls_back_to_default_on_bad_value(string? at)
            => JobSchedule.ParseAt(at, "20:30").Should().Be(new TimeSpan(20, 30, 0));

        [Fact]
        public void DueSlot_and_NextRun_follow_today_slot()
        {
            var at = new TimeSpan(20, 0, 0);
            JobSchedule.DueSlot(new DateTime(2026, 12, 1, 19, 59, 0), at).Should().BeNull("chưa tới giờ");
            JobSchedule.DueSlot(new DateTime(2026, 12, 1, 20, 0, 0), at).Should().Be(new DateTime(2026, 12, 1, 20, 0, 0));
            JobSchedule.DueSlot(new DateTime(2026, 12, 1, 23, 10, 0), at).Should().Be(new DateTime(2026, 12, 1, 20, 0, 0), "lỡ giờ → chạy bù trong ngày");
            JobSchedule.NextRun(new DateTime(2026, 12, 1, 19, 0, 0), at).Should().Be(new DateTime(2026, 12, 1, 20, 0, 0));
            JobSchedule.NextRun(new DateTime(2026, 12, 1, 20, 0, 0), at).Should().Be(new DateTime(2026, 12, 2, 20, 0, 0));
        }
    }

    /// <summary>T23: DI – WebApi và host AppDelicare.Job (chỉ AddDelicareCore + DelicareServicesModule, không có phần riêng của web) dựng được mọi job.</summary>
    public class JobDependencyInjectionTests(InMemoryApiFactory factory) : IClassFixture<InMemoryApiFactory>
    {
        // BE3.4: + KPI_DAILY / KPI_MONTHLY (cấu hình Enabled = false).
        private static readonly string[] Codes = ["AccountingPeriodReminder", "AccountingVoucherDigest", "AccountLockResigned", "BillingMonthReady", "ContractExpiry", "DailyReport", "DayCard", "ForecastAutoPublish", "ForecastAutoSuggest", "ForecastPublishReminder", "ForecastReadyNotice", "IngredientsTonight", "KPI_DAILY", "KPI_MONTHLY", "LeaveHandover", "LockOrders", "LotExpiry", "MarkAbsent", "MealCostRecalc", "MenuAutoDraft", "MenuDraftNotice", "MenuExpiry", "MenuWeekReminder", "MorningBrief", "Mrp", "OrderMissingAtCutoff", "OrderMissingReminder", "OrderWeekReminder", "PackagingIssueAutoPost", "PeriodReport", "ProductionAutoClose", "RemindUnconfirmedOrders"];

        [Fact]
        public void WebApi_container_resolves_all_jobs_and_runner()
        {
            using var scope = factory.Services.CreateScope();
            scope.ServiceProvider.GetServices<IScheduledJob>().Select(j => j.Code).Order().Should().Equal(Codes);
            scope.ServiceProvider.GetRequiredService<IJobRunService>().Should().NotBeNull();
        }

        [Fact]
        public void Job_host_container_without_web_registrations_resolves_all_jobs()
        {
            var configuration = new ConfigurationBuilder()
                .AddConfiguration(factory.Services.GetRequiredService<IConfiguration>())
                .Build();
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddLogging();
            services.AddDelicareCore(configuration, AppContext.BaseDirectory);
            var builder = new ContainerBuilder();
            builder.Populate(services);
            builder.RegisterModule<DelicareServicesModule>();
            using var container = builder.Build();
            using var scope = container.BeginLifetimeScope();
            var jobs = scope.Resolve<IEnumerable<IScheduledJob>>().ToList();
            jobs.Select(j => j.Code).Order().Should().Equal(Codes);
            jobs.All(j => JobSchedule.TryParse(j.DefaultAt, out _)).Should().BeTrue("giờ mặc định của mọi job đúng HH:mm");
            scope.Resolve<IJobRunService>().Should().NotBeNull();
        }

        [Fact]
        public async Task Job_api_is_admin_only()
        {
            Guid userId = Guid.NewGuid();
            await factory.SeedAsync(db =>
            {
                db.User.Add(new User { Id = userId, UserName = "job-" + userId.ToString("N")[..8], IsActive = true, IsAdmin = false });
                return Task.CompletedTask;
            });
            using var user = factory.CreateAuthorizedClient(userId);
            (await user.GetAsync("/api/Job/GetAll")).StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
            (await user.PostAsync("/api/Job/Run", LegacyHttp.Form(("jobCode", "ContractExpiry")))).StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
        }
    }

    /// <summary>T23 (Catalog 81): hợp đồng Hiệu lực quá ngày hết hiệu lực → Hết hạn; xem trước không đổi; chạy lại không đổi thêm.</summary>
    public class ContractExpireTests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public ContractExpireTests(InMemoryApiFactory factory)
        {
            _factory = factory;
            _factory.Clock.FixedNow = new DateTime(2026, 12, 1, 0, 30, 0);
        }

        [Fact]
        public async Task ExpireOverdue_moves_only_active_contracts_past_effective_to()
        {
            string code = "EXP" + Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();
            Guid customer = Guid.NewGuid(), overdue = Guid.NewGuid(), endsToday = Guid.NewGuid(), open = Guid.NewGuid(), draft = Guid.NewGuid();
            await _factory.SeedAsync(async db =>
            {
                db.Customer.Add(new Customer { Id = customer, Code = code, Name = "Khách " + code, CIF = code, CustomerType = "FACTORY", DisplayName = code, Status = "Active" });
                db.Contract.AddRange(
                    new Contract { Id = overdue, CustomerId = customer, ContractNumber = "HD-" + code + "-1", Status = ContractStatus.Active, EffectiveFrom = new DateTime(2026, 1, 1), EffectiveTo = new DateTime(2026, 11, 30) },
                    new Contract { Id = endsToday, CustomerId = customer, ContractNumber = "HD-" + code + "-2", Status = ContractStatus.Active, EffectiveFrom = new DateTime(2026, 1, 1), EffectiveTo = new DateTime(2026, 12, 1) },
                    new Contract { Id = open, CustomerId = customer, ContractNumber = "HD-" + code + "-3", Status = ContractStatus.Active, EffectiveFrom = new DateTime(2026, 1, 1) },
                    new Contract { Id = draft, CustomerId = customer, ContractNumber = "HD-" + code + "-4", Status = ContractStatus.Draft, EffectiveFrom = new DateTime(2026, 1, 1), EffectiveTo = new DateTime(2026, 6, 30) });
                await Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();

            var preview = (await c.PostFormAsync("/api/Contract/ExpireOverdue", ("Preview", true))).Ok();
            preview.GetProperty("listContractNumber").EnumerateArray().Select(x => x.GetString()).Should().Contain("HD-" + code + "-1")
                .And.NotContain(["HD-" + code + "-2", "HD-" + code + "-3", "HD-" + code + "-4"]);
            (await _factory.QueryAsync(db => db.Contract.AsNoTracking().Where(x => x.Id == overdue).Select(x => x.Status).SingleAsync())).Should().Be(ContractStatus.Active, "xem trước không đổi");

            (await c.PostFormAsync("/api/Contract/ExpireOverdue")).Ok().GetProperty("listContractNumber").EnumerateArray()
                .Select(x => x.GetString()).Should().Contain("HD-" + code + "-1");
            var after = await _factory.QueryAsync(db => db.Contract.AsNoTracking().Where(x => x.CustomerId == customer).ToDictionaryAsync(x => x.Id));
            after[overdue].Status.Should().Be(ContractStatus.Expired);
            after[overdue].Note.Should().Contain("Hết hạn ngày 30/11/2026");
            after[endsToday].Status.Should().Be(ContractStatus.Active, "còn hiệu lực hết hôm nay");
            after[open].Status.Should().Be(ContractStatus.Active, "không có ngày hết hiệu lực");
            after[draft].Status.Should().Be(ContractStatus.Draft, "chỉ xét hợp đồng Hiệu lực");

            (await c.PostFormAsync("/api/Contract/ExpireOverdue")).Ok().GetProperty("listContractNumber").EnumerateArray()
                .Select(x => x.GetString()).Should().NotContain("HD-" + code + "-1", "chạy lại không đổi thêm");
        }
    }

    /// <summary>
    /// T23 trên SQL Server dev (bỏ qua nếu không có DB): JobRunService với job giả – chạy dưới tài khoản system (SCHEDULE), ghi AnalyticsRefreshLog,
    /// đến giờ / đã chạy, khóa sp_getapplock chống chạy trùng, job lỗi → Failed, job không tồn tại. Dòng log của job giả xóa lại cuối test (theo JobCode riêng).
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class JobRunSqlServerTests(ApiFactory factory)
    {
        private static readonly Guid SystemUserId = Guid.Parse("061877ad-396c-4386-8868-25d4940bf467");

        private sealed class FakeJob(string code, string at, Func<ICurrentActor?, DateTime, Task<JobOutcome>> run, ICurrentActor actor) : IScheduledJob
        {
            public string Code => code;
            public string Name => "[DEMO] " + code;
            public string Description => "Job giả của test";
            public string DefaultAt => at;
            public Task<JobOutcome> RunAsync(DateTime now, CancellationToken cancellationToken) => run(actor, now);
        }

        [SkippableFact]
        public async Task Runner_logs_runs_under_system_user_and_blocks_concurrent_runs()
        {
            Skip.If(factory.SkipReason != null, factory.SkipReason);
            using var scope = factory.Services.CreateScope();
            var baseConfig = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var actor = scope.ServiceProvider.GetRequiredService<ICurrentActor>();
            string connectionString = baseConfig.GetConnectionString(ConstantHelper.MainConnectionString)!;
            await using (var check = new SqlConnection(connectionString))
            {
                Skip.If(await check.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.[User] WHERE Id = @Id", new { Id = SystemUserId }) == 0,
                    "DB chưa chạy DB/seed/SystemUser_seed.sql");
            }
            string tag = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
            string ok = "TEST_OK_" + tag, fail = "TEST_FAIL_" + tag, later = "TEST_LATER_" + tag;
            ActorInfo? seen = null;
            var jobs = new IScheduledJob[]
            {
                new FakeJob(ok, "09:00", (a, now) => { seen = a!.Actor; return Task.FromResult(new JobOutcome(JobRunStatus.Succeeded, 3, now.Date.AddDays(1), "3 dòng")); }, actor),
                new FakeJob(fail, "09:00", (_, _) => throw new InvalidOperationException("lỗi thử"), actor),
                new FakeJob(later, "23:59", (_, now) => Task.FromResult(new JobOutcome(JobRunStatus.Skipped, 0, now.Date, "không có gì")), actor),
            };
            var configuration = new ConfigurationBuilder().AddConfiguration(baseConfig)
                .AddInMemoryCollection(new Dictionary<string, string?> { [$"Jobs:{fail}:Enabled"] = "false" }).Build();
            var clock = new TestClock { FixedNow = new DateTime(2099, 12, 1, 10, 0, 0) };
            // BOT-1.7: thông báo job lỗi tắt trong test (không xếp tin thật vào DB dev).
            var runner = new JobRunService(jobs, configuration, clock, actor, scope.ServiceProvider.GetRequiredService<AppDelicare.Data.Interface.IUnitOfWork>(), new NoNotifications());
            try
            {
                (await runner.GetDueJobCodes()).Should().Equal([ok], "đã tới 09:00; job TẮT và job 23:59 không chạy");

                var run = await runner.Run(ok, JobTrigger.Schedule);
                run.ResponseCode.Should().Be(200, run.ResponseMessage);
                run.Data!.RunStatus.Should().Be(JobRunStatus.Succeeded);
                run.Data.RowsProcessed.Should().Be(3);
                seen.Should().NotBeNull();
                seen!.Type.Should().Be(ActorType.System);
                seen.UserId.Should().Be(SystemUserId);
                (await runner.GetDueJobCodes()).Should().BeEmpty("hôm nay đã chạy");

                var info = (await runner.GetJobs()).Single(j => j.JobCode == ok);
                info.LastRun!.RunStatus.Should().Be(JobRunStatus.Succeeded);
                info.LastRun.Trigger.Should().Be(JobTrigger.Schedule);
                info.LastRun.TargetDate.Should().Be(new DateTime(2099, 12, 2));
                info.NextRunAt.Should().Be(new DateTime(2099, 12, 2, 9, 0, 0));
                (await runner.GetJobs()).Single(j => j.JobCode == fail).Enabled.Should().BeFalse();

                await using (var log = new SqlConnection(connectionString))
                {
                    var row = await log.QuerySingleAsync<(string RunStatus, long RowsProcessed, Guid CreatedBy, string SourceWatermark)>(
                        "SELECT RunStatus, RowsProcessed, CreatedBy, SourceWatermark FROM dbo.AnalyticsRefreshLog WHERE RunId = @RunId", new { run.Data.RunId });
                    row.RunStatus.Should().Be(JobRunStatus.Succeeded);
                    row.RowsProcessed.Should().Be(3);
                    row.CreatedBy.Should().Be(SystemUserId);
                    row.SourceWatermark.Should().Be("SCHEDULE|2099-12-02");
                }

                // Khóa: 1 kết nối khác đang giữ sp_getapplock của job → lần chạy này bỏ qua (Locked), không gọi job.
                seen = null;
                await using (var holder = new SqlConnection(connectionString))
                {
                    await holder.OpenAsync();
                    (await holder.ExecuteScalarAsync<int>(
                        "DECLARE @r int; EXEC @r = sp_getapplock @Resource = @Res, @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = 0; SELECT @r;",
                        new { Res = "DELICARE_JOB_" + ok })).Should().BeGreaterThanOrEqualTo(0);
                    var locked = await runner.Run(ok, JobTrigger.Schedule);
                    locked.ResponseCode.Should().Be(400);
                    locked.ResponseMessage.Should().Be(ConstantHelper.Message.JobAlreadyRunning);
                    seen.Should().BeNull("không chạy job khi đang bị khóa");
                }

                // Job lỗi → Failed + thông báo; vẫn nhả khóa (chạy lại được).
                var failed = await runner.Run(fail, JobTrigger.Schedule);
                failed.ResponseCode.Should().Be(400);
                failed.ResponseMessage.Should().Contain("lỗi thử");
                (await runner.GetJobs()).Single(j => j.JobCode == fail).LastRun!.RunStatus.Should().Be(JobRunStatus.Failed);
                (await runner.Run(fail, JobTrigger.Schedule)).ResponseMessage.Should().NotBe(ConstantHelper.Message.JobAlreadyRunning);

                (await runner.Run("KHONG_CO", JobTrigger.Schedule)).ResponseMessage.Should().Contain("KHONG_CO");
            }
            finally
            {
                // Chỉ xóa dòng log của 3 job giả vừa tạo (JobCode có tag riêng) – không đụng log thật.
                await using var cleanup = new SqlConnection(connectionString);
                await cleanup.ExecuteAsync("DELETE FROM dbo.AnalyticsRefreshLog WHERE JobCode IN @Codes", new { Codes = new[] { ok, fail, later } });
            }
        }

        /// <summary>BOT-1.7: bỏ qua thông báo (test chạy trên DB dev).</summary>
        private sealed class NoNotifications : AppDelicare.Services.Interface.INotificationService
        {
            public Task<List<Guid>> EnqueueAsync(AppDelicare.Model.Model.NotificationRequest request, CancellationToken ct = default) => Task.FromResult(new List<Guid>());
        }
    }
}
