using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace AppDelicare.Tests.Agent
{
    /// <summary>
    /// AI-1 (D-133): AppDelicare.ToolGateway – MCP <c>/mcp</c> (Streamable HTTP, SDK ModelContextProtocol 2.4.0) liệt kê + gọi <c>ping</c>; REST <c>/tools/{name}</c>
    /// cùng bộ công cụ; <c>/health</c> 200; ToolGateway không có DbContext / SqlConnection riêng.
    /// </summary>
    public class ToolGatewayAi1Tests : IClassFixture<ToolGatewayAi1Tests.GatewayFactory>
    {
        public sealed class GatewayFactory : WebApplicationFactory<AppDelicare.ToolGateway.Program>
        {
            // Program kiểm chuỗi kết nối sau Build (ping không chạm DB) – chỉ cấp cho host test, không đặt biến môi trường của tiến trình.
            protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Testing").UseSetting("ConnectionStrings:MainConnectionString", "Server=(localdb)\\none;Database=none;Trusted_Connection=True");
        }

        private readonly GatewayFactory _factory;

        public ToolGatewayAi1Tests(GatewayFactory factory) => _factory = factory;

        [Fact]
        public async Task Health_and_rest_ping()
        {
            using var http = _factory.CreateClient();
            (await http.GetAsync("/health")).StatusCode.Should().Be(HttpStatusCode.OK);
            var res = await http.PostAsJsonAsync("/tools/ping", new { });
            res.StatusCode.Should().Be(HttpStatusCode.OK);
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            doc.RootElement.GetProperty("data").GetProperty("ok").GetBoolean().Should().BeTrue();
            (await http.PostAsJsonAsync("/tools/khong_co", new { })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Mcp_client_lists_and_calls_ping()
        {
            var http = _factory.CreateClient();
            var transport = new HttpClientTransport(new HttpClientTransportOptions { Endpoint = new Uri(http.BaseAddress!, "/mcp"), TransportMode = HttpTransportMode.StreamableHttp }, http, null, false);
            await using var client = await McpClient.CreateAsync(transport);
            var tools = await client.ListToolsAsync();
            tools.Select(t => t.Name).Should().Contain("ping");
            var call = await client.CallToolAsync("ping", new Dictionary<string, object?>());
            call.IsError.Should().NotBe(true);
            ((TextContentBlock)call.Content[0]).Text.Should().Contain("\"ok\":true");
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
