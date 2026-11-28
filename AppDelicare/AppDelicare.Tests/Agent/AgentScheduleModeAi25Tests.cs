using System.Text.Json;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Services.Implement;
using AppDelicare.Services.Implement.Agent;
using AppDelicare.Services.Jobs;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Agent
{
    /// <summary>
    /// AI-25.5 (D-143): mọi lịch trong schedules.json có mode template | ai; template có job chạy thay thật; chỉ việc cần suy luận là ai.
    /// vivi-events chạy ở Job theo mẫu: cảnh báo đang mở → 1 tin / cảnh báo / ngày cho người có quyền Thêm màn, không gọi Hermes (Agent:EventAiAnalysis mặc định false);
    /// mode ai / lịch tắt → pump không chạy.
    /// </summary>
    [Collection(AppDelicare.Tests.Integration.StaticLoggerCollection.Name)]
    public class AgentScheduleModeAi25Tests
    {
        private static readonly DateTime Now = new(2031, 6, 2, 8, 0, 0);
        private static readonly Guid Vh = Guid.Parse("a25a25a5-0000-4000-8000-0000000000e1");

        private static string Root()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "app-delicare-agents", "schedules.json")))
            {
                dir = dir.Parent;
            }
            return dir!.FullName;
        }

        [Fact]
        public async Task Every_schedule_has_mode_and_template_items_name_a_real_job()
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "app-delicare-agents", "schedules.json")));
            var items = doc.RootElement.GetProperty("items").EnumerateArray().ToList();
            using var f = await AgentToolsAi3Tests.FactoryAsync();
            using var scope = f.Services.CreateScope();
            var jobs = scope.ServiceProvider.GetRequiredService<IEnumerable<IScheduledJob>>().Select(j => j.Code).Append(AgentEventPumpService.JobCode).ToHashSet();
            foreach (var s in items)
            {
                string id = s.GetProperty("id").GetString()!, mode = s.GetProperty("mode").GetString()!;
                mode.Should().BeOneOf(["template", "ai"], id);
                if (mode == "template")
                {
                    jobs.Should().Contain(s.GetProperty("templateJob").GetString()!, $"{id}: job chạy thay phải có thật");
                    s.GetProperty("replacedBy").GetString().Should().NotBeNullOrWhiteSpace(id);
                }
            }
            items.Where(s => s.GetProperty("mode").GetString() == "ai").Select(s => s.GetProperty("id").GetString())
                .Should().BeEquivalentTo(["know-review", "acc-month", "know-doc-summary", "know-sop-review"], "chỉ giữ AI cho việc cần suy luận");
            items.Single(s => s.GetProperty("id").GetString() == "vivi-events").GetProperty("templateJob").GetString().Should().Be(AgentEventPumpService.JobCode);
        }

        private static async Task<InMemoryApiFactory> PumpFactoryAsync(params (string, string)[] settings)
        {
            var f = await AgentToolsAi3Tests.FactoryAsync([("BusinessRules:Hr:ShiftMinStaff:T255:S255", "2"), .. settings]);
            f.Clock.FixedNow = Now;
            Guid unit = Guid.NewGuid(), shift = Guid.NewGuid(), e1 = Guid.NewGuid(), user = Guid.NewGuid(), group = Guid.NewGuid();
            DateTime d1 = Now.Date.AddDays(1);
            await f.SeedAsync(async db =>
            {
                db.OrganizationUnit.Add(new OrganizationUnit { Id = unit, Code = "T255", Name = "Tổ 255" });
                db.Shift.Add(new Shift { Id = shift, Code = "S255", Name = "Sáng 255", StartTime = new TimeSpan(6, 0, 0), EndTime = new TimeSpan(14, 0, 0), Status = "Active" });
                db.Employee.Add(new Employee { Id = e1, Code = "nv255", EmployeeCode = "nv255", FullName = "NV 255", OrganizationUnitId = unit, Status = "Active" });
                db.ShiftAssignment.Add(new ShiftAssignment { Id = Guid.NewGuid(), EmployeeId = e1, ShiftId = shift, WorkDate = d1, PlannedStart = d1.AddHours(6), PlannedEnd = d1.AddHours(14), AssignmentStatus = "Planned" });
                var menu = await db.ObjectMenu.FirstOrDefaultAsync(m => m.MetaLink == "#/hr-shift");
                if (menu == null)
                {
                    menu = new ObjectMenu { Id = Guid.NewGuid(), Name = "hr-shift", MetaLink = "#/hr-shift", LevelShow = 2, OrderShow = 1, IsActive = true };
                    db.ObjectMenu.Add(menu);
                }
                var vivi = await db.UserGroup.FirstAsync(g => g.Code == "AGENT_VIVI");
                db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = vivi.Id, ObjectMenuId = menu.Id, AllowAccess = true });
                db.IntegrationChannel.Add(new IntegrationChannel { Id = Vh, ChannelCode = "TELEGRAM_VH", ChannelName = "VH", ProviderCode = "TELEGRAM", EnvironmentCode = "Development", ChannelStatus = "Active", ConfigJson = "{}" });
                db.BotConfiguration.Add(new BotConfiguration { Id = Guid.NewGuid(), IntegrationChannelId = Vh, BotCode = "VH", BotName = "Vận hành", BotStatus = "Active" });
                db.User.Add(new User { Id = user, UserName = "ns-a255", IsActive = true, AccountType = "INTERNAL" });
                db.UserGroup.Add(new UserGroup { Id = group, Name = "NS-A255", Code = "NS-A255" });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = user, UserGroupId = group });
                db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = group, ObjectMenuId = menu.Id, AllowAccess = true, AllowAdd = true });
                db.UserExternalIdentity.Add(new UserExternalIdentity { Id = Guid.NewGuid(), UserId = user, IntegrationChannelId = Vh, ExternalUserId = "92551", LinkedAt = Now, IdentityStatus = "Active" });
            });
            return f;
        }

        private static async Task<EventPumpRunModel> PumpAsync(InMemoryApiFactory f)
        {
            using var scope = f.Services.CreateScope();
            using var job = scope.ServiceProvider.GetRequiredService<ICurrentActor>().BeginScope(ActorInfo.ForSystem(Guid.NewGuid()));
            return await scope.ServiceProvider.GetRequiredService<IAgentEventPumpService>().RunOnceAsync();
        }

        [Fact]
        public async Task Vivi_events_pump_sends_template_alert_once_per_day_to_people_who_can_add_without_ai()
        {
            using var f = await PumpFactoryAsync(("Agent:HermesApiUrl", "http://127.0.0.1:9/khong-duoc-goi"));
            f.CreateAuthorizedClient().Dispose();
            var r = await PumpAsync(f);
            r.Active.Should().BeTrue(r.Reason);
            r.Sent.Should().BeGreaterThan(0);
            r.AiComments.Should().Be(0);
            var key = AgentEventPumpService.KeyPrefix + CommonService.AlertCode.ShiftShort + ":" + Now.ToString("yyyyMMdd");
            (await f.QueryAsync(db => db.OutboundMessage.CountAsync(m => m.IdempotencyKey!.StartsWith(key)))).Should().Be(1);
            (await PumpAsync(f)).Sent.Should().Be(0, "mỗi cảnh báo 1 tin / ngày");
            (await f.QueryAsync(db => db.IntegrationLog.CountAsync(l => l.OperationCode == "agent_chat"))).Should().Be(0, "không gọi Hermes");
        }

        [Fact]
        public async Task Pump_does_not_run_when_vivi_events_is_ai_mode()
        {
            string tmp = Path.Combine(Path.GetTempPath(), "sched-a255-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(tmp, "{\"items\":[{\"id\":\"vivi-events\",\"agent\":\"VIVI\",\"enabled\":true,\"mode\":\"ai\",\"tools\":[\"lay_su_kien\"]}]}");
            try
            {
                using var f = await PumpFactoryAsync(("Agent:SchedulesFile", tmp));
                f.CreateAuthorizedClient().Dispose();
                var r = await PumpAsync(f);
                r.Active.Should().BeFalse();
                r.Reason.Should().Contain("mode = ai");
                (await f.QueryAsync(db => db.OutboundMessage.CountAsync(m => m.IdempotencyKey!.StartsWith(AgentEventPumpService.KeyPrefix)))).Should().Be(0);
            }
            finally
            {
                File.Delete(tmp);
            }
        }
    }
}
