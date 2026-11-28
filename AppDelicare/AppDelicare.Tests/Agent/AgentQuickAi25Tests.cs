using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Services.Implement.Agent;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Agent
{
    /// <summary>
    /// AI-25.1 (D-143): 72 câu nhanh chạy qua API (0 token – không gọi Hermes), &lt; 2 giây, ghi IntegrationLog AGENT_QUICK; người không có quyền màn không thấy / không chạy được câu.
    /// </summary>
    [Collection(AppDelicare.Tests.Integration.StaticLoggerCollection.Name)] // chạy không song song – đo thời gian xử lý thật
    public class AgentQuickAi25Tests
    {
        [Fact]
        public async Task All_quick_questions_run_without_ai_within_two_seconds_and_follow_screen_rights()
        {
            using var f = await AgentToolsAi3Tests.FactoryAsync(("Agent:HermesApiUrl", "http://127.0.0.1:9/khong-duoc-goi"));
            using var c = f.CreateAuthorizedClient();
            var list = JsonDocument.Parse(await c.GetStringAsync("/api/Agent/GetQuick?kenh=web")).RootElement.GetProperty("listData").EnumerateArray().ToList();
            var (all, _) = QuickCatalog.Load(QuickCatalog.Root());
            list.Should().HaveCount(all.Count(q => q.Kenh.Contains("web")), "admin thấy mọi câu web");
            var fails = new List<string>();
            foreach (var q in all.Where(q => q.Kenh.Contains("web") || q.Kenh.Contains("VH") || q.Kenh.Contains("DH")))
            {
                var sw = Stopwatch.StartNew();
                var r = await c.PostAsJsonAsync("/api/Agent/RunQuick", new { id = q.Id, nhap = q.Nhap.Keys.ToDictionary(k => k, _ => "KHONG-CO") });
                var j = JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;
                int code = j.GetProperty("responseCode").GetInt32();
                if (code == 200)
                {
                    var d = j.GetProperty("data");
                    if (d.GetProperty("tieuDe").GetString()!.Contains('{') || (d.GetProperty("tomTat").GetString() ?? string.Empty).Contains('{') || (d.GetProperty("rong").GetString() ?? string.Empty).Contains('{'))
                        fails.Add($"{q.Id}: còn chỗ trống chưa thay – {d}");
                }
                else if (!(code == 400 && q.Nhap.Count > 0) && code != 400)
                {
                    fails.Add($"{q.Id}: {code} {j.GetProperty("responseMessage").GetString()}");
                }
                long ms = code == 200 ? j.GetProperty("data").GetProperty("ms").GetInt64() : sw.ElapsedMilliseconds; // thời gian xử lý ở máy chủ
                if (ms > 2000) fails.Add($"{q.Id}: {ms} ms");
            }
            fails.Should().BeEmpty(string.Join(" ¦ ", fails));
            int logs = await f.QueryAsync(db => db.IntegrationLog.CountAsync(l => l.ErrorCode!.StartsWith(AgentQuickService.LogPrefix)));
            logs.Should().BeGreaterThan(60, "mỗi lần chạy 1 dòng AGENT_QUICK (token 0)");
            (await f.QueryAsync(db => db.IntegrationLog.CountAsync(l => l.OperationCode == "Agent/Chat" || l.ErrorCode!.Contains("HERMES")))).Should().Be(0, "không gọi Hermes");

            // Người không có quyền màn kho: không thấy / không chạy câu kho.
            Guid u = Guid.NewGuid(), g = Guid.NewGuid();
            await f.SeedAsync(db =>
            {
                db.User.Add(new User { Id = u, UserName = "kd-a25", IsActive = true, AccountType = "INTERNAL" });
                db.UserGroup.Add(new UserGroup { Id = g, Name = "kd-a25", Code = "KD-A25" });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = u, UserGroupId = g });
                return Task.CompletedTask;
            });
            using var plain = f.CreateAuthorizedClient(u);
            var mine = JsonDocument.Parse(await plain.GetStringAsync("/api/Agent/GetQuick?agent=WH")).RootElement.GetProperty("listData").EnumerateArray().Select(x => x.GetProperty("id").GetString()).ToList();
            mine.Should().NotContain("wh-ton-kho");
            var deny = JsonDocument.Parse(await (await plain.PostAsJsonAsync("/api/Agent/RunQuick", new { id = "wh-ton-kho" })).Content.ReadAsStringAsync()).RootElement;
            deny.GetProperty("responseCode").GetInt32().Should().Be(403);
        }
    }
}
