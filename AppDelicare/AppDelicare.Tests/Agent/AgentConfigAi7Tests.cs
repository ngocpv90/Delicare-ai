using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.RegularExpressions;
using AppDelicare.Model.Entity;
using AppDelicare.Services.Implement.Agent;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Agent
{
    /// <summary>
    /// AI-7 (D-133) – cấu hình Hermes trong repo (<c>app-delicare-agents/</c>): <c>tools.txt</c> từng agent khớp <see cref="AgentToolRegistry"/>; công cụ trong <c>eval.yaml</c> và
    /// <c>schedules.json</c> thuộc đúng agent; repo không chứa khóa / token (chỉ tên biến môi trường); người hỏi Telegram chưa liên kết → từ chối; nhóm được hỏi theo cấu hình.
    /// Sinh lại tools.txt: đặt biến môi trường UPDATE_AGENT_TOOLS=1 rồi chạy test này.
    /// </summary>
    public class AgentConfigAi7Tests
    {
        private static readonly string[] Codes = AgentCatalog.All.Select(a => a.Code).ToArray();

        private static string Repo()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "app-delicare-agents", "schedules.json")))
            {
                dir = dir.Parent;
            }
            return dir?.FullName ?? throw new DirectoryNotFoundException("Không thấy thư mục app-delicare-agents/");
        }

        private static List<string> Expected(string code) => [.. AgentToolRegistry.All.Where(t => t.AllowedFor(code)).Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal)];

        private static List<string> ToolsTxt(string root, string code)
            => [.. File.ReadAllLines(Path.Combine(root, "app-delicare-agents", code, "tools.txt")).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#'))];

        [Fact]
        public void Tools_txt_matches_registry_for_every_agent()
        {
            string root = Repo();
            if (Environment.GetEnvironmentVariable("UPDATE_AGENT_TOOLS") == "1")
            {
                foreach (string code in Codes)
                {
                    Directory.CreateDirectory(Path.Combine(root, "app-delicare-agents", code));
                    File.WriteAllText(Path.Combine(root, "app-delicare-agents", code, "tools.txt"),
                        $"# Công cụ của agent {code} – SINH TỪ AgentToolRegistry (AgentConfigAi7Tests, UPDATE_AGENT_TOOLS=1). Không sửa tay.\n" + string.Join("\n", Expected(code)) + "\n");
                }
            }
            foreach (string code in Codes)
            {
                ToolsTxt(root, code).Should().Equal(Expected(code), $"app-delicare-agents/{code}/tools.txt phải khớp danh bạ công cụ");
                File.Exists(Path.Combine(root, "app-delicare-agents", code, "SOUL.md")).Should().BeTrue($"app-delicare-agents/{code}/SOUL.md");
            }
        }

        [Fact]
        public void Eval_and_schedule_tools_belong_to_their_agent()
        {
            string root = Repo();
            foreach (string code in new[] { "VIVI", "WH", "KITCHEN", "QA", "KNOW", "SALES", "PROC", "LOG", "HR", "PLAN", "PKG", "ACC", "CSKH" })
            {
                string file = Path.Combine(root, "app-delicare-agents", code, "eval.yaml");
                File.Exists(file).Should().BeTrue(file);
                var tools = Regex.Matches(File.ReadAllText(file), @"^\s*tool:\s*([a-z_]+)\s*$", RegexOptions.Multiline).Select(m => m.Groups[1].Value).ToList();
                tools.Should().NotBeEmpty();
                tools.Should().OnlyContain(t => ToolsTxt(root, code).Contains(t), $"eval.yaml của {code} chỉ dùng công cụ của {code}");
            }
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "app-delicare-agents", "schedules.json")));
            foreach (var s in doc.RootElement.GetProperty("items").EnumerateArray())
            {
                string agent = s.GetProperty("agent").GetString()!;
                foreach (var t in s.GetProperty("tools").EnumerateArray())
                {
                    ToolsTxt(root, agent).Should().Contain(t.GetString()!, $"lịch {s.GetProperty("id").GetString()}");
                }
            }
        }

        [Fact]
        public void Agents_folder_and_compose_have_no_secrets()
        {
            string root = Repo();
            var files = Directory.EnumerateFiles(Path.Combine(root, "app-delicare-agents"), "*", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles(root, "docker-compose*.yml"))
                .Concat(Directory.EnumerateFiles(Path.Combine(root, "tools", "agent-eval"), "*", SearchOption.AllDirectories))
                .Append(Path.Combine(root, "AppDelicare", "AppDelicare.ToolGateway", "toolgateway.json")).ToList();
            var bad = new List<string>();
            var rx = new[]
            {
                new Regex(@"\b\d{8,10}:[A-Za-z0-9_-]{30,}\b"),            // token bot Telegram
                new Regex(@"sk-ant-[A-Za-z0-9_-]{16,}"),                    // khóa Anthropic
                new Regex(@"X-Agent-Key\s*:\s*(?![\s""']*\$\{)[^\s#""']{6,}"), // khóa agent viết thẳng (phải là ${BIẾN})
            };
            foreach (string f in files)
            {
                string text = File.ReadAllText(f);
                bad.AddRange(rx.Where(r => r.IsMatch(text)).Select(r => Path.GetRelativePath(root, f) + " ~ " + r));
            }
            bad.Should().BeEmpty("khóa / token chỉ ở biến môi trường");
        }

        [Fact]
        public async Task Unlinked_asker_no_assistant_right_and_assistant_off_are_refused_with_codes()
        {
            using var f = new InMemoryApiFactory();
            f.Settings["AGENT_KEY_TWH"] = AgentGatewayAi2Tests.WhKey;
            await AgentGatewayAi2Tests.SeedAsync(f);
            async Task<AgentCallResult> Call(string? onBehalf)
            {
                using var scope = f.Services.CreateScope();
                return await scope.ServiceProvider.GetRequiredService<IAgentGatewayService>().CallAsync(new AgentCallRequest
                {
                    AgentCode = "WH", AgentKey = AgentGatewayAi2Tests.WhKey, OnBehalfOf = onBehalf, Tool = "get_ton_kho", Arguments = JsonSerializer.SerializeToElement(new { }),
                });
            }
            var stranger = await Call("999999");
            stranger.Status.Should().Be(403);
            stranger.Message.Should().Contain("chưa liên kết");
            (await Call("777001")).Status.Should().Be(200);

            stranger.Code.Should().Be(AgentCallResult.NotLinked);

            // AI-23.1 (D-142): thay Agent:AskingGroups – bỏ quyền "Dùng bot Trợ lý" trên ma trận → từ chối ngay, mã KHONG_CO_QUYEN_TRO_LY.
            await f.SeedAsync(async db =>
            {
                var bot = await db.ObjectMenu.FirstAsync(m => m.MetaLink == "#/ai-bot-tro-ly");
                foreach (var role in db.UserRole.Where(x => x.ObjectMenuId == bot.Id))
                {
                    role.AllowAccess = false;
                }
            });
            using (var scope = f.Services.CreateScope())
            {
                scope.ServiceProvider.GetRequiredService<AppDelicare.Services.Interface.IPermissionService>().InvalidateAll(); // như màn Phân quyền sau khi lưu
            }
            var noRight = await Call("777001");
            noRight.Status.Should().Be(403);
            noRight.Code.Should().Be(AgentCallResult.NoAssistantRight);

            // Tắt bot Trợ lý (BotConfiguration TRO_LY) → mọi câu Telegram bị từ chối, mã BOT_TRO_LY_TAT.
            await f.SeedAsync(async db => (await db.BotConfiguration.FirstAsync(b => b.BotCode == "TRO_LY")).BotStatus = "Inactive");
            var off = await Call("777001");
            off.Code.Should().Be(AgentCallResult.AssistantOff);
            off.Message.Should().Contain("tạm dừng");
        }
    }
}
