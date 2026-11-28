using System.Net.Http.Json;
using System.Text.Json;
using AppDelicare.Services.Implement.Agent;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Agent
{
    /// <summary>
    /// AI-23 (D-142): tab Bot Trợ lý (trạng thái, chỉ số hôm nay theo mã từ chối, bật / tắt có hiệu lực ngay ở ToolGateway), khối tài khoản (chỉ người có quyền
    /// "Dùng bot Trợ lý"), bộ lọc Kênh của nhật ký agent (Telegram / Web / Theo lịch).
    /// </summary>
    public class AgentAssistantBotAi23Tests
    {
        private static async Task<AgentCallResult> CallAsync(InMemoryApiFactory f, string? onBehalf)
        {
            using var scope = f.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IAgentGatewayService>().CallAsync(new AgentCallRequest
            {
                AgentCode = "WH", AgentKey = AgentGatewayAi2Tests.WhKey, OnBehalfOf = onBehalf, Tool = "get_ton_kho", Arguments = JsonSerializer.SerializeToElement(new { }),
            });
        }

        [Fact]
        public async Task Bot_tab_counts_today_toggle_refuses_telegram_and_account_block_follows_right()
        {
            using var f = new InMemoryApiFactory();
            f.Settings["AGENT_KEY_TWH"] = AgentGatewayAi2Tests.WhKey;
            await AgentGatewayAi2Tests.SeedAsync(f);
            (await CallAsync(f, "777001")).Status.Should().Be(200);
            (await CallAsync(f, "999999")).Code.Should().Be(AgentCallResult.NotLinked);
            (await CallAsync(f, null)).Status.Should().Be(200, "việc theo lịch – không người hỏi");

            using var admin = f.CreateAuthorizedClient();
            var bot = (await admin.GetResultAsync("/api/Agent/GetAssistantBot")).Ok();
            bot.GetProperty("configured").GetBoolean().Should().BeTrue();
            bot.GetProperty("active").GetBoolean().Should().BeTrue();
            bot.GetProperty("asksToday").GetInt32().Should().Be(2);
            bot.GetProperty("askersToday").GetInt32().Should().Be(2);
            bot.GetProperty("deniedNotLinked").GetInt32().Should().Be(1);
            bot.GetProperty("allowedUsers").GetInt32().Should().Be(2);
            bot.GetProperty("canEdit").GetBoolean().Should().BeTrue();

            // Nhật ký – lọc kênh.
            int Total(JsonElement d) => d.GetProperty("total").GetInt32();
            Total((await admin.GetResultAsync("/api/Agent/GetActivity?kenh=telegram")).Ok()).Should().Be(2);
            Total((await admin.GetResultAsync("/api/Agent/GetActivity?kenh=lich")).Ok()).Should().Be(1);
            Total((await admin.GetResultAsync("/api/Agent/GetActivity?kenh=web")).Ok()).Should().Be(0);

            // Tên bot + tạm dừng → ToolGateway từ chối Telegram ngay, việc theo lịch vẫn chạy.
            var save = await admin.PostAsJsonAsync("/api/Agent/SaveAssistantBot", new { active = false, username = "@delicare_tro_ly_bot" });
            JsonDocument.Parse(await save.Content.ReadAsStringAsync()).RootElement.GetProperty("responseCode").GetInt32().Should().Be(200);
            (await f.QueryAsync(db => db.BotConfiguration.AsNoTracking().FirstAsync(b => b.BotCode == "TRO_LY"))).BotUsername.Should().Be("delicare_tro_ly_bot");
            var off = await CallAsync(f, "777001");
            off.Code.Should().Be(AgentCallResult.AssistantOff);
            (await CallAsync(f, null)).Status.Should().Be(200);
            var bad = await admin.PostAsJsonAsync("/api/Agent/SaveAssistantBot", new { username = "ten co dau" });
            JsonDocument.Parse(await bad.Content.ReadAsStringAsync()).RootElement.GetProperty("responseCode").GetInt32().Should().Be(400);

            // Khối tài khoản: người có quyền thấy link + đã liên kết; người không có quyền → Allowed = false.
            await admin.PostAsJsonAsync("/api/Agent/SaveAssistantBot", new { active = true });
            using var allowed = f.CreateAuthorizedClient(AgentGatewayAi2Tests.Allowed);
            var mine = (await allowed.GetResultAsync("/api/Agent/GetMyAssistant")).Ok();
            mine.GetProperty("allowed").GetBoolean().Should().BeTrue();
            mine.GetProperty("linked").GetBoolean().Should().BeTrue();
            mine.GetProperty("link").GetString().Should().Be("https://t.me/delicare_tro_ly_bot");
            mine.GetProperty("qrSvg").GetString().Should().StartWith("<svg");
            Guid stranger = Guid.NewGuid();
            await f.SeedAsync(db =>
            {
                db.User.Add(new AppDelicare.Model.Entity.User { Id = stranger, UserName = "khong-quyen-a23", IsActive = true, AccountType = "INTERNAL" });
                return Task.CompletedTask;
            });
            using var other = f.CreateAuthorizedClient(stranger);
            (await other.GetResultAsync("/api/Agent/GetMyAssistant")).Ok().GetProperty("allowed").GetBoolean().Should().BeFalse();
            (await other.GetAsync("/api/Agent/GetAssistantBot")).StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
        }
    }
}
