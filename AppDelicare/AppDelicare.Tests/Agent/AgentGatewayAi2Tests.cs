using System.Net.Http.Json;
using System.Text.Json;
using AppDelicare.Data.Connection;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Services.Implement.Agent;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Agent
{
    /// <summary>
    /// AI-2 (D-133): danh tính agent + công tắc + giới hạn. Khóa sai / thiếu → 401; agent Inactive → 403; Agent:Enabled=false → 503; vượt giới hạn → 429;
    /// người hỏi không có quyền Xem màn → công cụ từ chối dù agent có quyền; Agent:ReadOnly ẩn propose_*; tài khoản agent đăng nhập web bị từ chối;
    /// mọi lời gọi ghi IntegrationLog (kênh HERMES); công cụ chạy dưới actor agent.
    /// </summary>
    public class AgentGatewayAi2Tests
    {
        public static readonly Guid WhUser = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");
        public static readonly Guid KitUser = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000002");
        public static readonly Guid Allowed = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000003");
        public static readonly Guid Denied = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000004");
        public const string WhKey = "khoa-wh-test";

        /// <summary>Kênh HERMES + 2 agent (WH bật, KITCHEN tắt) + quyền Xem Tồn kho cho AGENT_WH và người hỏi "Allowed"; 2 người hỏi liên kết Telegram.</summary>
        public static async Task SeedAsync(InMemoryApiFactory f)
        {
            await f.SeedAsync(db =>
            {
                Guid hermes = Guid.NewGuid(), tg = Guid.NewGuid(), gAgent = Guid.NewGuid(), gUser = Guid.NewGuid(), menu = Guid.NewGuid();
                db.IntegrationChannel.AddRange(
                    new IntegrationChannel { Id = hermes, ChannelCode = "HERMES", ChannelName = "Hermes", ProviderCode = "HERMES", ChannelStatus = "Active" },
                    new IntegrationChannel { Id = tg, ChannelCode = "TELEGRAM_TRO_LY", ChannelName = "Trợ lý", ProviderCode = "TELEGRAM", ChannelStatus = "Active" });
                db.BotConfiguration.AddRange(
                    new BotConfiguration { Id = Guid.NewGuid(), IntegrationChannelId = hermes, BotCode = "WH", BotName = "Agent Kho", BotStatus = "Active", SecretReference = "AGENT_KEY_TWH" },
                    new BotConfiguration { Id = Guid.NewGuid(), IntegrationChannelId = hermes, BotCode = "KITCHEN", BotName = "Agent Bếp", BotStatus = "Inactive", SecretReference = "AGENT_KEY_TWH" },
                    new BotConfiguration { Id = Guid.NewGuid(), IntegrationChannelId = tg, BotCode = "TRO_LY", BotName = "Trợ lý", BotStatus = "Active" });
                db.User.AddRange(
                    new User { Id = WhUser, UserName = "agent.wh", PassWord = EncriptFunctionsHelper.GeneratePassword("Agent-123456"), IsActive = true, AccountType = "INTERNAL" },
                    new User { Id = KitUser, UserName = "agent.kitchen", PassWord = "!NO-LOGIN!", IsActive = true, AccountType = "INTERNAL" },
                    new User { Id = Allowed, UserName = "thu.kho.ai2", PassWord = "!NO-LOGIN!", IsActive = true, AccountType = "INTERNAL" },
                    new User { Id = Denied, UserName = "ke.toan.ai2", PassWord = "!NO-LOGIN!", IsActive = true, AccountType = "INTERNAL" });
                db.UserExternalIdentity.AddRange(
                    new UserExternalIdentity { Id = Guid.NewGuid(), UserId = WhUser, IntegrationChannelId = hermes, ExternalUserId = "WH", IdentityStatus = "Active" },
                    new UserExternalIdentity { Id = Guid.NewGuid(), UserId = KitUser, IntegrationChannelId = hermes, ExternalUserId = "KITCHEN", IdentityStatus = "Active" },
                    new UserExternalIdentity { Id = Guid.NewGuid(), UserId = Allowed, IntegrationChannelId = tg, ExternalUserId = "777001", IdentityStatus = "Active" },
                    new UserExternalIdentity { Id = Guid.NewGuid(), UserId = Denied, IntegrationChannelId = tg, ExternalUserId = "777002", IdentityStatus = "Active" });
                db.UserGroup.AddRange(new UserGroup { Id = gAgent, Name = "AGENT_WH", Code = "AGENT_WH" }, new UserGroup { Id = gUser, Name = "OPS-WH-AI2", Code = "OPS-WH-AI2" });
                db.UserClaim.AddRange(new UserClaim { Id = Guid.NewGuid(), UserId = WhUser, UserGroupId = gAgent }, new UserClaim { Id = Guid.NewGuid(), UserId = Allowed, UserGroupId = gUser });
                if (!db.ObjectMenu.Any(m => m.MetaLink == "#/scm-inv-balance"))
                {
                    db.ObjectMenu.Add(new ObjectMenu { Id = menu, Name = "Tồn kho", MetaLink = "#/scm-inv-balance", LevelShow = 2, OrderShow = 1, IsActive = true });
                }
                else
                {
                    menu = db.ObjectMenu.First(m => m.MetaLink == "#/scm-inv-balance").Id;
                }
                db.UserRole.AddRange(new UserRole { Id = Guid.NewGuid(), UserGroupId = gAgent, ObjectMenuId = menu, AllowAccess = true },
                    new UserRole { Id = Guid.NewGuid(), UserGroupId = gUser, ObjectMenuId = menu, AllowAccess = true });
                // AI-23.1: người hỏi phải có quyền mục "Dùng bot Trợ lý" – cả 2 người hỏi mẫu đều có (Denied chỉ thiếu quyền màn Tồn kho).
                Guid bot = db.ObjectMenu.FirstOrDefault(m => m.MetaLink == "#/ai-bot-tro-ly")?.Id ?? Guid.NewGuid(), gAsk = Guid.NewGuid();
                if (!db.ObjectMenu.Any(m => m.MetaLink == "#/ai-bot-tro-ly"))
                {
                    db.ObjectMenu.Add(new ObjectMenu { Id = bot, Name = "Dùng bot Trợ lý", MetaLink = "#/ai-bot-tro-ly", LevelShow = 2, OrderShow = 1, IsActive = true });
                }
                db.UserGroup.Add(new UserGroup { Id = gAsk, Name = "TRO-LY-AI2", Code = "TRO-LY-AI2" });
                db.UserClaim.AddRange(new UserClaim { Id = Guid.NewGuid(), UserId = Allowed, UserGroupId = gAsk }, new UserClaim { Id = Guid.NewGuid(), UserId = Denied, UserGroupId = gAsk });
                db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = gAsk, ObjectMenuId = bot, AllowAccess = true });
                return Task.CompletedTask;
            });
        }

        private static InMemoryApiFactory Factory(params (string Key, string Value)[] settings)
        {
            var f = new InMemoryApiFactory();
            f.Settings["AGENT_KEY_TWH"] = WhKey;
            foreach (var (k, v) in settings)
            {
                f.Settings[k] = v;
            }
            return f;
        }

        private static async Task<AgentCallResult> Call(InMemoryApiFactory f, string tool, string? agent = "WH", string? key = WhKey, string? onBehalf = null)
        {
            using var scope = f.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IAgentGatewayService>().CallAsync(new AgentCallRequest
            {
                AgentCode = agent, AgentKey = key, OnBehalfOf = onBehalf, Tool = tool, Arguments = JsonSerializer.SerializeToElement(new { }),
            });
        }

        [Fact]
        public async Task Key_status_permissions_and_log()
        {
            using var f = Factory();
            await SeedAsync(f);
            (await Call(f, "ping", key: null)).Status.Should().Be(401);
            (await Call(f, "ping", key: "sai")).Status.Should().Be(401);
            (await Call(f, "ping", agent: "KITCHEN")).Status.Should().Be(403, "agent đang tắt");
            (await Call(f, "khong_co")).Status.Should().Be(404);
            var ping = await Call(f, "ping");
            ping.Status.Should().Be(200);
            JsonSerializer.SerializeToElement(ping.Data).GetProperty("agent").GetString().Should().Be("WH");

            (await Call(f, "get_ton_kho")).Status.Should().Be(200, "việc theo lịch – chỉ quyền agent");
            (await Call(f, "get_ton_kho", onBehalf: "777001")).Status.Should().Be(200, "người hỏi có quyền Xem Tồn kho");
            var denied = await Call(f, "get_ton_kho", onBehalf: "777002");
            denied.Status.Should().Be(403);
            denied.Message.Should().Contain("Người hỏi");

            var logs = await f.QueryAsync(db => db.IntegrationLog.AsNoTracking().Where(l => l.DirectionCode == "IN").ToListAsync());
            logs.Select(l => $"{l.OperationCode}|{l.HttpStatus}|{l.LogStatus}|{l.ErrorCode}|{l.CreatedBy}").Should().HaveCount(8, string.Join("; ", logs.Select(l => $"{l.OperationCode}|{l.HttpStatus}|{l.ErrorCode}|{l.CreatedBy}")));
            logs.Count(l => l.LogStatus == "Denied").Should().Be(4);
            logs.Should().Contain(l => l.OperationCode == "get_ton_kho" && l.HttpStatus == 403 && l.ErrorMessage!.Contains("Người hỏi"));
            logs.Where(l => l.HttpStatus == 200).Should().OnlyContain(l => l.ErrorCode == "AGENT:WH" && l.CreatedBy == WhUser, "ghi dưới actor agent");
        }

        [Fact]
        public async Task Disabled_switch_returns_503_and_rate_limit_429()
        {
            using (var off = Factory(("Agent:Enabled", "false")))
            {
                await SeedAsync(off);
                (await Call(off, "ping")).Status.Should().Be(503);
            }
            using var f = Factory(("Agent:RateLimitPerMinute", "3"));
            await SeedAsync(f);
            for (int i = 0; i < 3; i++)
            {
                (await Call(f, "ping")).Status.Should().Be(200);
            }
            (await Call(f, "ping")).Status.Should().Be(429);
        }

        [Fact]
        public async Task Read_only_hides_propose_tools_and_tool_list_follows_rights()
        {
            using var f = Factory(("Agent:ReadOnly", "true"));
            await SeedAsync(f);
            using var scope = f.Services.CreateScope();
            var gw = scope.ServiceProvider.GetRequiredService<IAgentGatewayService>();
            var (err, tools) = await gw.ListToolsAsync(new AgentCallRequest { AgentCode = "WH", AgentKey = WhKey, OnBehalfOf = "777002" });
            err.Status.Should().Be(200);
            tools.Select(t => t.Name).Should().Contain("ping").And.NotContain("get_ton_kho", "người hỏi không có quyền Xem Tồn kho").And.NotContain(n => n.StartsWith("propose_"));
            (await gw.ListToolsAsync(new AgentCallRequest { AgentCode = "WH", AgentKey = "sai" })).Error.Status.Should().Be(401);
        }

        [Fact]
        public async Task Agent_account_cannot_log_in_to_web()
        {
            using var f = Factory();
            await SeedAsync(f);
            using var client = f.CreateClient();
            var res = await client.PostAsJsonAsync("/api/Authentication/Login", new { userName = "agent.wh", passWord = "Agent-123456" });
            (await res.Content.ReadAsStringAsync()).Should().Contain(AgentAccounts.LoginDenied);
            (await f.QueryAsync(db => AgentAccounts.IsAgentAsync(db, WhUser))).Should().BeTrue();
            (await f.QueryAsync(db => AgentAccounts.IsAgentAsync(db, Allowed))).Should().BeFalse();
        }
    }
}
