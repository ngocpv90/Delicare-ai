using AppDelicare.Data.Share;
using AppDelicare.Services.Jobs;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Jobs
{
    /// <summary>
    /// UI-6.47 (D-125 d): mọi job có tên / mô tả tiếng Việt, giờ mặc định hợp lệ; chuỗi tối đúng thứ tự (chốt đơn 20:00 → MRP → đóng lệnh SX 22:00 → KPI ngày sau đóng lệnh
    /// → giá vốn 01:00); chạy mỗi job 2 lần liền không lỗi, job ghi dữ liệu theo trạng thái lần 2 = 0 việc (không nhân đôi).
    /// </summary>
    public class JobChainUi647Tests
    {
        private static readonly DateTime Now = new(2032, 7, 5, 23, 45, 0);
        private static readonly string[] Idempotent = ["MarkAbsent", "ProductionAutoClose", "AccountLockResigned", "ContractExpiry", "LotExpiry", "MenuExpiry"];

        [Fact]
        public async Task Ui649_job_screen_lists_bot_watch_reminders_and_not_checked_in_is_15_minutes()
        {
            new AppDelicare.Model.Option.MessagingOptions().Watch.NotCheckedInMinutes.Should().Be(15, "D-119: bot chưa vào ca sau 15 phút");
            using var f = new InMemoryApiFactory();
            using var admin = f.CreateAuthorizedClient();
            var res = await admin.GetAsync("/api/Job/GetAll");
            string body = await res.Content.ReadAsStringAsync();
            res.IsSuccessStatusCode.Should().BeTrue(body);
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            var items = doc.RootElement.GetProperty("listData").EnumerateArray().ToList();
            var bot = items.Where(i => i.GetProperty("runsInBot").GetBoolean()).ToList();
            bot.Select(i => i.GetProperty("jobCode").GetString()).Should().Contain(["BOT:NotCheckedIn", "BOT:ShiftWeekRemind", "BOT:SwapConsentExpire", "BOT:ContainerOverdue", "BOT:StopLate", "BOT:TemperatureDue"]);
            bot.Should().OnlyContain(i => i.GetProperty("jobName").GetString()!.Length > 3 && !i.GetProperty("jobName").GetString()!.StartsWith("BOT"));
            items.Count(i => !i.GetProperty("runsInBot").GetBoolean()).Should().BeGreaterThan(10, "job theo giờ vẫn đủ");
        }

        [Fact]
        public void Config_defaults_check_lists_only_missing_business_rule_keys()
        {
            var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["BusinessRules:Hr:AutoMarkAbsentTime"] = "23:30", ["BusinessRules:DispatchShortWarnMinutes"] = "30" }).Build();
            var missing = AppDelicare.Services.Ioc.ConfigDefaultsCheck.MissingKeys(config);
            missing.Should().NotContain("BusinessRules:Hr:AutoMarkAbsentTime").And.NotContain("BusinessRules:DispatchShortWarnMinutes");
            missing.Should().Contain("BusinessRules:AccountInactiveWarnDays");
            missing.Should().NotContain("BusinessRules:Hr", "lớp lồng nhau đi vào từng khóa con");
        }

        [Fact]
        public void Jobs_have_vietnamese_names_and_evening_chain_order()
        {
            using var f = new InMemoryApiFactory();
            using var scope = f.Services.CreateScope();
            var jobs = scope.ServiceProvider.GetServices<IScheduledJob>().ToList();
            jobs.Should().OnlyContain(j => !string.IsNullOrWhiteSpace(j.Name) && !string.IsNullOrWhiteSpace(j.Description) && j.Name != j.Code);
            TimeSpan At(string code) => JobSchedule.ParseAt(jobs.Single(j => j.Code == code).DefaultAt, "00:00");
            At("LockOrders").Should().BeLessThan(At("Mrp"));
            At("Mrp").Should().BeLessThan(At("ProductionAutoClose"));
            At("ProductionAutoClose").Should().BeLessThan(At(jobs.Single(j => j.Name == "KPI ngày").Code), "KPI ngày tính sau khi đóng lệnh sản xuất");
        }

        [Fact]
        public async Task Every_job_runs_twice_without_failing_and_state_jobs_do_nothing_the_second_time()
        {
            using var f = new InMemoryApiFactory();
            f.Clock.FixedNow = Now;
            using var scope = f.Services.CreateScope();
            var actor = scope.ServiceProvider.GetRequiredService<ICurrentActor>();
            using var _ = actor.BeginScope(ActorInfo.ForSystem(Guid.NewGuid()));
            foreach (var job in scope.ServiceProvider.GetServices<IScheduledJob>())
            {
                var first = await job.RunAsync(Now, CancellationToken.None);
                var second = await job.RunAsync(Now, CancellationToken.None);
                first.Status.Should().NotBe(JobRunStatus.Failed, $"{job.Code} lần 1: {first.Message}");
                second.Status.Should().NotBe(JobRunStatus.Failed, $"{job.Code} lần 2: {second.Message}");
                if (Idempotent.Contains(job.Code))
                {
                    second.RowsProcessed.Should().Be(0, $"{job.Code} chạy lại không làm thêm việc");
                }
            }
        }
    }
}
