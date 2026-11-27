using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AppDelicare.Data.Connection;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace AppDelicare.Tests.Agent
{
    /// <summary>
    /// AI-1 (D-133): AppDelicare.ToolGateway – MCP <c>/mcp</c> (Streamable HTTP, SDK ModelContextProtocol 2.4.0) liệt kê + gọi <c>ping</c>; REST <c>/tools/{name}</c>
    /// cùng bộ công cụ; <c>/health</c> 200; header khóa agent (AI-2) đi qua cả MCP và REST; ToolGateway không truy cập CSDL riêng.
    /// </summary>
    public class ToolGatewayAi1Tests : IClassFixture<ToolGatewayAi1Tests.GatewayFactory>
    {
        public sealed class GatewayFactory : WebApplicationFactory<AppDelicare.ToolGateway.Program>
        {
            private readonly string _db = "gateway-" + Guid.NewGuid().ToString("N");

            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.UseEnvironment("Testing")
                    .UseSetting("ConnectionStrings:MainConnectionString", "Server=(localdb)\\none;Database=none;Trusted_Connection=True")
                    .UseSetting("AGENT_KEY_TWH", AgentGatewayAi2Tests.WhKey)
                    .ConfigureTestServices(services =>
                    {
                        services.RemoveAll<DbContextOptions<DatabaseContext>>();
                        services.AddDbContext<DatabaseContext>(o => o.UseInMemoryDatabase(_db));
                    });
            }

            public async Task SeedAsync()
            {
                using var scope = Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
                if (await db.IntegrationChannel.AnyAsync(c => c.ChannelCode == "HERMES"))
                {
                    return;
                }
                Guid hermes = Guid.NewGuid();
                db.IntegrationChannel.Add(new IntegrationChannel { Id = hermes, ChannelCode = "HERMES", ChannelName = "Hermes", ProviderCode = "HERMES", ChannelStatus = "Active" });
                db.BotConfiguration.Add(new BotConfiguration { Id = Guid.NewGuid(), IntegrationChannelId = hermes, BotCode = "WH", BotName = "Agent Kho", BotStatus = "Active", SecretReference = "AGENT_KEY_TWH" });
                db.User.Add(new User { Id = AgentGatewayAi2Tests.WhUser, UserName = "agent.wh", PassWord = "!NO-LOGIN!", IsActive = true, AccountType = "INTERNAL" });
                db.UserExternalIdentity.Add(new UserExternalIdentity { Id = Guid.NewGuid(), UserId = AgentGatewayAi2Tests.WhUser, IntegrationChannelId = hermes, ExternalUserId = "WH", IdentityStatus = "Active" });
                using (scope.ServiceProvider.GetRequiredService<ICurrentActor>().BeginScope(ActorInfo.ForSystem(AgentGatewayAi2Tests.WhUser)))
                {
                    await db.SaveChangesAsync();
                }
            }
        }

        private readonly GatewayFactory _factory;

        public ToolGatewayAi1Tests(GatewayFactory factory) => _factory = factory;

        private HttpClient Client(bool withKey = true)
        {
            var http = _factory.CreateClient();
            if (withKey)
            {
                http.DefaultRequestHeaders.Add("X-Agent-Code", "WH");
                http.DefaultRequestHeaders.Add("X-Agent-Key", AgentGatewayAi2Tests.WhKey);
            }
            return http;
        }

        [Fact]
        public async Task Health_and_rest_ping()
        {
            await _factory.SeedAsync();
            using var http = Client();
            (await http.GetAsync("/health")).StatusCode.Should().Be(HttpStatusCode.OK);
            var res = await http.PostAsJsonAsync("/tools/ping", new { });
            res.StatusCode.Should().Be(HttpStatusCode.OK);
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            doc.RootElement.GetProperty("data").GetProperty("ok").GetBoolean().Should().BeTrue();
            (await http.PostAsJsonAsync("/tools/khong_co", new { })).StatusCode.Should().Be(HttpStatusCode.NotFound);
            using var anon = Client(withKey: false);
            (await anon.PostAsJsonAsync("/tools/ping", new { })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Mcp_client_lists_and_calls_ping()
        {
            await _factory.SeedAsync();
            var http = Client();
            var transport = new HttpClientTransport(new HttpClientTransportOptions { Endpoint = new Uri(http.BaseAddress!, "/mcp"), TransportMode = HttpTransportMode.StreamableHttp }, http, null, false);
            await using var client = await McpClient.CreateAsync(transport);
            var tools = await client.ListToolsAsync();
            tools.Select(t => t.Name).Should().Contain("ping");
            var call = await client.CallToolAsync("ping", new Dictionary<string, object?>());
            call.IsError.Should().NotBe(true);
            ((TextContentBlock)call.Content[0]).Text.Should().Contain("\"ok\":true").And.Contain("\"agent\":\"WH\"");
        }

        [Fact]
        public void Gateway_has_no_own_database_access()
        {
            string dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AppDelicare.ToolGateway"));
            Directory.Exists(dir).Should().BeTrue(dir);
            Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories).Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
                .Where(f => File.ReadAllText(f).Contains("DbContext") || File.ReadAllText(f).Contains("SqlConnection")).Should().BeEmpty("ToolGateway chỉ gọi Service (D-133)");
        }
    }
}
