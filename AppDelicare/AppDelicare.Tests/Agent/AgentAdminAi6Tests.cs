using System.Net;
using System.Text.Json.Nodes;
using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Agent
{
    /// <summary>
    /// AI-6 (D-133) – Trung tâm agent: bật / tắt agent có hiệu lực NGAY ở ToolGateway; người không có quyền màn Trợ lý AI bị 403; nhật ký đọc từ IntegrationLog
    /// (agent, công cụ, kết quả); lịch chạy đọc app-delicare-agents/schedules.json (một nguồn với cron Hermes).
    /// </summary>
    public class AgentAdminAi6Tests
    {
        private static async Task<JsonNode?> Get(HttpClient c, string url) => JsonNode.Parse(await (await c.GetAsync(url)).Content.ReadAsStringAsync());

        [Fact]
        public async Task Toggle_takes_effect_immediately_and_activity_is_listed()
        {
            using var f = await AgentToolsAi3Tests.FactoryAsync();
            using var admin = f.CreateAuthorizedClient();
            (await AgentToolsAi3Tests.Call(f, "WH", "ping")).Status.Should().Be(200);

            var off = await admin.PostAsync("/api/Agent/SaveIsActive", new FormUrlEncodedContent(new Dictionary<string, string> { ["code"] = "WH", ["active"] = "false" }));
            JsonNode.Parse(await off.Content.ReadAsStringAsync())!["responseCode"]!.GetValue<int>().Should().Be(200);
            (await AgentToolsAi3Tests.Call(f, "WH", "ping")).Status.Should().Be(403, "tắt agent → ToolGateway từ chối ngay lời gọi sau");
            (await f.QueryAsync(db => db.BotConfiguration.AsNoTracking().SingleAsync(b => b.BotCode == "WH"))).BotStatus.Should().Be("Inactive");
            await admin.PostAsync("/api/Agent/SaveIsActive", new FormUrlEncodedContent(new Dictionary<string, string> { ["code"] = "WH", ["active"] = "true" }));
            (await AgentToolsAi3Tests.Call(f, "WH", "ping")).Status.Should().Be(200);

            var all = await Get(admin, "/api/Agent/GetAll");
            var wh = all!["listData"]!.AsArray().First(a => a!["code"]!.GetValue<string>() == "WH")!;
            wh["active"]!.GetValue<bool>().Should().BeTrue();
            wh["callsToday"]!.GetValue<int>().Should().Be(3);
            wh["deniedToday"]!.GetValue<int>().Should().Be(1);
            all["listData"]!.AsArray().Should().HaveCount(13);

            var log = await Get(admin, "/api/Agent/GetActivity?agent=WH");
            log!["data"]!["total"]!.GetValue<int>().Should().Be(3);
            log["data"]!["listRow"]!.AsArray().Select(r => r!["status"]!.GetValue<string>()).Should().Contain("Denied").And.Contain("Succeeded");
            var det = await Get(admin, "/api/Agent/GetById?code=WH");
            det!["data"]!["listFolder"]!.AsArray().Select(x => x!.GetValue<string>()).Should().Contain("40 Kho & Truy xuất").And.NotContain("BOD Bảo mật");
            (await admin.GetAsync("/api/Agent/GetById?code=XYZ")).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Users_without_rights_get_403_and_schedules_come_from_repo_file()
        {
            using var f = await AgentToolsAi3Tests.FactoryAsync();
            Guid plain = Guid.NewGuid();
            await f.SeedAsync(db =>
            {
                db.User.Add(new User { Id = plain, UserName = "uat.khong-quyen-ai6", PassWord = "!NO-LOGIN!", IsActive = true, AccountType = "INTERNAL" });
                return Task.CompletedTask;
            });
            using var user = f.CreateAuthorizedClient(plain);
            (await user.GetAsync("/api/Agent/GetAll")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await user.GetAsync("/api/Agent/GetActivity")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await user.PostAsync("/api/Agent/SaveIsActive", new FormUrlEncodedContent(new Dictionary<string, string> { ["code"] = "WH", ["active"] = "false" }))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            var tree = await Get(user, "/api/Knowledge/GetTree");
            tree!["listData"]!.AsArray().Should().BeEmpty("không được cấp thư mục nào");

            using var admin = f.CreateAuthorizedClient();
            var sched = await Get(admin, "/api/Agent/GetSchedules");
            var items = sched!["listData"]!.AsArray();
            items.Select(i => i!["id"]!.GetValue<string>()).Should().Contain(["vivi-morning-brief", "vivi-daily-report", "vivi-events"]);
            items.First(i => i!["id"]!.GetValue<string>() == "vivi-morning-brief")!["agentActive"]!.GetValue<bool>().Should().BeTrue();
        }
    }
}
