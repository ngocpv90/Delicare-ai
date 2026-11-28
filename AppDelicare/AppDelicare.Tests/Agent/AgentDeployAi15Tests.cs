using System.Text.Json;
using System.Text.RegularExpressions;
using AppDelicare.Model.Option;
using AppDelicare.Services.Implement.Agent;
using FluentAssertions;

namespace AppDelicare.Tests.Agent
{
    /// <summary>
    /// AI-15 (D-133) – triển khai 3 container Hermes: mỗi agent thuộc ĐÚNG 1 nhóm (agents/hermes/groups.json); container chỉ giữ khóa agent của nhóm mình,
    /// mỗi container 1 volume dữ liệu riêng; api / toolgateway có đủ khóa 13 agent; chat web của agent ngoài nhóm vivi trỏ đúng container; mẫu config nhóm chỉ có MCP của nhóm.
    /// </summary>
    public class AgentDeployAi15Tests
    {
        private static string Repo()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "docker-compose.yml")))
            {
                dir = dir.Parent;
            }
            return dir?.FullName ?? throw new DirectoryNotFoundException("Không thấy docker-compose.yml");
        }

        /// <summary>Khối dịch vụ (thụt 2 dấu cách) trong docker-compose.yml.</summary>
        private static string Service(string compose, string name)
        {
            var m = Regex.Match(compose, $@"^  {Regex.Escape(name)}:\n(?<b>(?:(?:    .*|\s*)\n)*)", RegexOptions.Multiline);
            m.Success.Should().BeTrue($"compose có dịch vụ {name}");
            return m.Groups["b"].Value;
        }

        private static HashSet<string> Keys(string block) => [.. Regex.Matches(block, @"AGENT_KEY_([A-Z]+):").Select(x => x.Groups[1].Value)];

        [Fact]
        public void Each_agent_runs_in_exactly_one_hermes_container_with_its_own_volume_and_keys()
        {
            string root = Repo();
            string compose = File.ReadAllText(Path.Combine(root, "docker-compose.yml")).Replace("\r\n", "\n");
            var groups = JsonSerializer.Deserialize<Dictionary<string, string[]>>(JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "agents", "hermes", "groups.json"))).RootElement.GetProperty("groups").GetRawText())!;
            string[] all = [.. AgentCatalog.All.Select(a => a.Code)];
            groups.Values.SelectMany(v => v).Should().BeEquivalentTo(all, "13 agent, mỗi agent đúng 1 nhóm").And.OnlyHaveUniqueItems();
            Regex.IsMatch(compose, @"^  hermes:\n", RegexOptions.Multiline).Should().BeFalse("bỏ container hermes đơn");

            var volumes = new List<string>();
            foreach (var (g, agents) in groups)
            {
                string block = Service(compose, "hermes-" + g);
                Keys(block).Should().BeEquivalentTo(agents, $"hermes-{g} chỉ giữ khóa agent của nhóm");
                block.Should().Contain("<<: *hermes").And.Contain("<<: *hermes-env");
                var vol = Regex.Match(block, @"- ([a-z0-9-]+):/opt/data");
                vol.Success.Should().BeTrue($"hermes-{g} có volume dữ liệu");
                volumes.Add(vol.Groups[1].Value);
                (block.Contains("TELEGRAM_BOT_TOKEN_TRO_LY") == (g == "vivi")).Should().BeTrue("chỉ hermes-vivi chạy bot Trợ lý");

                string cfg = File.ReadAllText(Path.Combine(root, "agents", "hermes", g + ".config.example.yaml"));
                Regex.Matches(cfg, @"X-Agent-Code: ([A-Z]+)").Select(x => x.Groups[1].Value).Should().BeEquivalentTo(agents, $"mẫu config {g}: MCP đúng agent của nhóm");
                Regex.IsMatch(cfg, @"(sk-ant|bot\d{6,}:|AGENT_KEY_[A-Z]+:\s*""?[A-Za-z0-9]{16,})").Should().BeFalse("mẫu không chứa khóa thật");
            }
            volumes.Should().OnlyHaveUniqueItems("mỗi container 1 volume, không dùng chung SQLite");
            Service(compose, "hermes-vivi").Should().Contain("hermes-data:/opt/data", "giữ volume cũ – lịch sử bot Trợ lý còn nguyên");

            foreach (string svc in new[] { "api", "toolgateway" })
            {
                Keys(Service(compose, svc)).Should().BeEquivalentTo(all, $"{svc} có đủ khóa 13 agent");
            }
            string api = Service(compose, "api");
            api.Should().Contain("Agent__HermesApiUrl: ${HERMES_API_URL:-http://hermes-vivi:8642/v1}");
            foreach (var (g, agents) in groups.Where(x => x.Key != "vivi"))
            {
                foreach (string a in agents)
                {
                    api.Should().Contain($"Agent__HermesApiUrlByAgent__{a}: http://hermes-{g}:8642/v1", $"chat web {a} → hermes-{g}");
                }
            }
        }

        [Fact]
        public void Chat_url_picks_the_agent_container_and_reply_reads_openai_shape()
        {
            var o = new AgentOptions { HermesApiUrl = "http://hermes-vivi:8642/v1" };
            o.HermesApiUrlByAgent["KITCHEN"] = "http://hermes-van-hanh:8642/v1";
            AgentChatService.ChatUrl(o, "KITCHEN").Should().Be("http://hermes-van-hanh:8642/v1");
            AgentChatService.ChatUrl(o, "kitchen").Should().Be("http://hermes-van-hanh:8642/v1", "mã không phân biệt hoa thường");
            AgentChatService.ChatUrl(o, "VIVI").Should().Be("http://hermes-vivi:8642/v1");
            AgentChatService.ReadReply(JsonDocument.Parse("""{"choices":[{"message":{"role":"assistant","content":"Chào anh"}}]}""").RootElement).Should().Be("Chào anh");
            AgentChatService.ReadReply(JsonDocument.Parse("""{"choices":[{"message":{"content":[{"type":"text","text":"A"},{"type":"text","text":"B"}]}}]}""").RootElement).Should().Be("AB");
            AgentChatService.ReadReply(JsonDocument.Parse("""{"error":{"message":"x"}}""").RootElement).Should().BeEmpty();
        }
    }
}
