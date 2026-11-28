using AppDelicare.Model.Entity;
using AppDelicare.Services.Implement.Agent;
using AppDelicare.Services.Jobs;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Agent
{
    /// <summary>
    /// AI-25.4 (D-143): bản tin dựng bằng mẫu – người nhận = quyền mục ai-ban-tin-*, câu chạy bằng quyền từng người (khối ngoài quyền bỏ), khối rỗng theo an_khi_rong,
    /// 1 tin / người / ngày (chạy lại không gửi trùng), không gọi Hermes khi Agent:BriefingAiComment = false, bản Ban GĐ ghi thư mục 10; 5 job có giờ theo ban-tin.yaml.
    /// </summary>
    [Collection(AppDelicare.Tests.Integration.StaticLoggerCollection.Name)]
    public class AgentBriefingAi25Tests
    {
        private static readonly Guid Dh = Guid.Parse("a25a25a2-0000-4000-8000-0000000000d1");

        private static async Task<Guid> UserAsync(InMemoryApiFactory f, string name, string[] screens, bool linked = true)
        {
            Guid user = Guid.NewGuid(), group = Guid.NewGuid();
            await f.SeedAsync(async db =>
            {
                db.User.Add(new User { Id = user, UserName = name, IsActive = true, AccountType = "INTERNAL" });
                db.UserGroup.Add(new UserGroup { Id = group, Name = name, Code = name.ToUpperInvariant() });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = user, UserGroupId = group });
                foreach (string s in screens)
                {
                    var menu = await db.ObjectMenu.FirstOrDefaultAsync(m => m.MetaLink == "#/" + s);
                    if (menu == null)
                    {
                        menu = new ObjectMenu { Id = Guid.NewGuid(), Name = s, MetaLink = "#/" + s, LevelShow = 2, OrderShow = 1, IsActive = true };
                        db.ObjectMenu.Add(menu);
                        await db.SaveChangesAsync();
                    }
                    db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = group, ObjectMenuId = menu.Id, AllowAccess = true });
                }
                if (linked)
                {
                    db.UserExternalIdentity.Add(new UserExternalIdentity { Id = Guid.NewGuid(), UserId = user, IntegrationChannelId = Dh, ExternalUserId = "77" + name.Length + user.ToString("N")[..6], LinkedAt = DateTime.Today, IdentityStatus = "Active" });
                }
            });
            return user;
        }

        [Fact]
        public async Task Morning_briefing_goes_to_menu_holders_with_their_own_rights_once_per_day_without_ai()
        {
            using var f = await AgentToolsAi3Tests.FactoryAsync(("Agent:HermesApiUrl", "http://127.0.0.1:9/khong-duoc-goi"));
            f.CreateAuthorizedClient().Dispose();
            await f.SeedAsync(db =>
            {
                db.IntegrationChannel.Add(new IntegrationChannel { Id = Dh, ChannelCode = "TELEGRAM_DH", ChannelName = "DH", ProviderCode = "TELEGRAM", EnvironmentCode = "Development", ChannelStatus = "Active", ConfigJson = "{}" });
                db.BotConfiguration.Add(new BotConfiguration { Id = Guid.NewGuid(), IntegrationChannelId = Dh, BotCode = "DH", BotName = "Điều hành", BotStatus = "Active" });
                return Task.CompletedTask;
            });
            var tools = AgentToolRegistry.All.Where(t => t.ScreenId != null).Select(t => t.ScreenId!).Distinct().ToArray();
            Guid ceo = await UserAsync(f, "ceo-a254", ["ai-ban-tin-sang", .. tools]);
            Guid bare = await UserAsync(f, "bare-a254", ["ai-ban-tin-sang"]); // có mục bản tin nhưng không xem được màn nào → chỉ khối không cần quyền màn (việc chờ chính mình duyệt)
            Guid other = await UserAsync(f, "kho-a254", [.. tools]); // không có mục bản tin → không nhận
            DateTime now = DateTime.Today.AddHours(7);

            using (var scope = f.Services.CreateScope())
            {
                using var job = scope.ServiceProvider.GetRequiredService<AppDelicare.Data.Share.ICurrentActor>().BeginScope(AppDelicare.Data.Share.ActorInfo.ForSystem(Guid.NewGuid())); // như JobRunService
                var r = await scope.ServiceProvider.GetRequiredService<IAgentBriefingService>().RunAsync("sang-bgd", now);
                r.Recipients.Should().Be(2);
                r.Sent.Should().Be(2);
                r.Lines.Should().Contain(l => l.StartsWith("▪ Tình hình hôm nay")).And.NotContain(l => l.Contains('{'));
                r.KnowledgePath.Should().StartWith("10 Ban GĐ/Bản tin/");
                File.ReadAllText(Path.Combine(scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()["Knowledge:VaultPath"]!, r.KnowledgePath!)).Should().Contain("source: delicare-core").And.Contain("Tình hình hôm nay");
            }
            using (var scope = f.Services.CreateScope())
            {
                using var job = scope.ServiceProvider.GetRequiredService<AppDelicare.Data.Share.ICurrentActor>().BeginScope(AppDelicare.Data.Share.ActorInfo.ForSystem(Guid.NewGuid())); // như JobRunService
                (await scope.ServiceProvider.GetRequiredService<IAgentBriefingService>().RunAsync("sang-bgd", now.AddMinutes(5))).Sent.Should().Be(0, "1 tin / người / ngày");
            }
            var msgs = await f.QueryAsync(db => db.OutboundMessage.AsNoTracking().Where(m => m.IdempotencyKey != null && m.IdempotencyKey.StartsWith("BRIEF:sang-bgd:")).ToListAsync());
            msgs.Should().HaveCount(2, "1 tin / người / ngày – người không có mục bản tin không nhận");
            var mCeo = msgs.Single(m => m.IdempotencyKey!.Contains(ceo.ToString("N")));
            static string Text(string json)
            {
                var root = System.Text.Json.JsonDocument.Parse(json).RootElement;
                var d = root.GetProperty("data");
                return root.GetProperty("template").GetString() + "\n" + d.GetProperty("title").GetString() + "\n" + string.Join("\n", d.GetProperty("lines").EnumerateArray().Select(x => x.GetString()));
            }
            Text(mCeo.PayloadJson!).Should().Contain("AGENT_BRIEFING").And.Contain("Bản tin sáng").And.Contain("Tình hình hôm nay");
            Text(msgs.Single(m => m.IdempotencyKey!.Contains(bare.ToString("N"))).PayloadJson!).Should().NotContain("Tình hình hôm nay", "câu chạy bằng quyền người nhận – không xem Bảng điều hành thì không có khối đó");
            (await f.QueryAsync(db => db.IntegrationLog.CountAsync(l => l.OperationCode == "agent_chat"))).Should().Be(0, "Agent:BriefingAiComment mặc định false – không gọi Hermes");
            (await f.QueryAsync(db => db.IntegrationLog.CountAsync(l => l.ErrorCode!.StartsWith(AgentQuickService.LogPrefix)))).Should().BeGreaterThan(0);
            _ = other;
        }

        [Fact]
        public async Task Five_briefing_jobs_take_time_from_ban_tin_yaml()
        {
            using var f = await AgentToolsAi3Tests.FactoryAsync();
            using var scope = f.Services.CreateScope();
            var jobs = scope.ServiceProvider.GetRequiredService<IEnumerable<IScheduledJob>>().OfType<BriefingJobBase>().ToDictionary(j => j.Code, j => j.DefaultAt);
            jobs.Should().BeEquivalentTo(new Dictionary<string, string>
            {
                ["BriefingMorningBod"] = "07:00", ["BriefingEveningBod"] = "17:00", ["BriefingWarehouse"] = "05:30", ["BriefingKitchen"] = "05:45", ["BriefingOrderClose"] = "19:00",
            });
        }
    }
}
