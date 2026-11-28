using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Services.Implement.Agent;
using AppDelicare.Services.Implement.Messaging;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Agent
{
    /// <summary>
    /// AI-13 (D-133) chi phí: Hermes báo chi phí mỗi lượt (bao_chi_phi) – cùng lượt không cộng 2 lần, USD quy VND, tổng theo agent; chạm 80 % / 100 % ngân sách → 1 tin / ngưỡng.
    /// AI-14 chat web: chưa cấu hình Hermes → báo tiếng Việt; có Hermes → gửi câu hỏi + vé người hỏi ký HMAC; cổng công cụ nhận vé → quyền giao nhau agent ∩ người hỏi; vé sửa / hết hạn → 403.
    /// </summary>
    public class AgentCostChatAi13Tests
    {
        private static readonly Guid Dh = Guid.Parse("a13a13a1-0000-4000-8000-0000000000d1");

        private static async Task<AgentCallResult> CallAsync(InMemoryApiFactory f, string agent, string tool, object args, string? onBehalfOf = null)
        {
            using var scope = f.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IAgentGatewayService>().CallAsync(new AgentCallRequest
            {
                AgentCode = agent, AgentKey = AgentToolsAi3Tests.Key, Tool = tool, Arguments = JsonSerializer.SerializeToElement(args), OnBehalfOf = onBehalfOf, CorrelationId = "a13",
            });
        }

        [Fact]
        public async Task Cost_is_recorded_once_per_turn_converted_and_alerts_at_budget_thresholds()
        {
            using var f = await AgentToolsAi3Tests.FactoryAsync(("Agent:MonthlyBudget", "1000000"), ("Agent:UsdToVnd", "25000"), ("Agent:CostAlertPct", "80"));
            f.CreateAuthorizedClient().Dispose();
            Guid ceo = Guid.NewGuid(), g = Guid.NewGuid();
            await f.SeedAsync(async db =>
            {
                db.IntegrationChannel.Add(new IntegrationChannel { Id = Dh, ChannelCode = "TELEGRAM_DH", ChannelName = "DH", ProviderCode = "TELEGRAM", EnvironmentCode = "Development", ChannelStatus = "Active", ConfigJson = "{}" });
                db.BotConfiguration.Add(new BotConfiguration { Id = Guid.NewGuid(), IntegrationChannelId = Dh, BotCode = "DH", BotName = "Điều hành", BotStatus = "Active" });
                db.User.Add(new User { Id = ceo, UserName = "ceo-a13", IsActive = true });
                db.UserGroup.Add(new UserGroup { Id = g, Name = "CEO-A13", Code = "CEO-A13" });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = ceo, UserGroupId = g });
                var menu = await db.ObjectMenu.FirstOrDefaultAsync(m => m.MetaLink == "#/ai-agents");
                if (menu == null)
                {
                    menu = new ObjectMenu { Id = Guid.NewGuid(), Name = "Trung tâm agent", MetaLink = "#/ai-agents", LevelShow = 2, IsActive = true };
                    db.ObjectMenu.Add(menu);
                }
                db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = g, ObjectMenuId = menu.Id, AllowAccess = true, AllowAdd = true, AllowEdit = true });
                db.UserExternalIdentity.Add(new UserExternalIdentity { Id = Guid.NewGuid(), UserId = ceo, IntegrationChannelId = Dh, ExternalUserId = "8801", LinkedAt = DateTime.Today, IdentityStatus = "Active" });
            });
            var r1 = await CallAsync(f, "VIVI", "bao_chi_phi", new { mo_hinh = "m1", token_vao = 1200, token_ra = 300, chi_phi = 20m, don_vi = "USD", luot = "t1" });
            r1.Status.Should().Be(200, r1.Message);
            ((JsonNode)r1.Data!)["chiPhiVnd"]!.GetValue<decimal>().Should().Be(500_000m);
            ((JsonNode)(await CallAsync(f, "VIVI", "bao_chi_phi", new { chi_phi = 20m, luot = "t1" })).Data!)["daGhi"]!.GetValue<bool>().Should().BeFalse("cùng lượt không cộng 2 lần");
            (await f.QueryAsync(db => db.OutboundMessage.CountAsync(m => m.MessageType == NotificationCatalog.AgentCostAlert))).Should().Be(0, "50 % chưa tới ngưỡng");

            await CallAsync(f, "QA", "bao_chi_phi", new { chi_phi = 350_000m, don_vi = "VND", luot = "t2", token_vao = 100, token_ra = 50 });
            var at85 = await f.QueryAsync(db => db.OutboundMessage.Where(m => m.MessageType == NotificationCatalog.AgentCostAlert).ToListAsync());
            at85.Should().ContainSingle("85 % ≥ 80 % → 1 tin");
            at85[0].RecipientExternalId.Should().Be("8801");
            await CallAsync(f, "QA", "bao_chi_phi", new { chi_phi = 10_000m, don_vi = "VND", luot = "t3" });
            (await f.QueryAsync(db => db.OutboundMessage.CountAsync(m => m.MessageType == NotificationCatalog.AgentCostAlert))).Should().Be(1, "cùng ngưỡng tháng không gửi lại");
            await CallAsync(f, "QA", "bao_chi_phi", new { chi_phi = 200_000m, don_vi = "VND", luot = "t4" });
            (await f.QueryAsync(db => db.OutboundMessage.CountAsync(m => m.MessageType == NotificationCatalog.AgentCostAlert))).Should().Be(2, "chạm 100 % gửi thêm 1 tin");

            using var scope = f.Services.CreateScope();
            var cost = (await scope.ServiceProvider.GetRequiredService<IAgentCostService>().GetCost(null)).Data!;
            cost.TotalVnd.Should().Be(1_060_000m);
            cost.Pct.Should().Be(106m);
            cost.Turns.Should().Be(4);
            cost.ListAgent.Select(a => (a.Agent, a.CostVnd)).Should().Equal(("QA", 560_000m), ("VIVI", 500_000m));
            cost.TokensIn.Should().Be(1300);
        }

        /// <summary>Hermes giả (API server kiểu OpenAI): nhận 1 yêu cầu HTTP, ghi lại nguyên yêu cầu (header + thân), trả choices[0].message.content.</summary>
        private static (int Port, Task<string> Body) FakeHermes(string reply)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var body = Task.Run(async () =>
            {
                using var client = await listener.AcceptTcpClientAsync();
                var stream = client.GetStream();
                var buf = new byte[65536];
                var sb = new StringBuilder();
                int contentLength = -1, headerEnd = -1;
                while (true)
                {
                    int n = await stream.ReadAsync(buf);
                    if (n == 0) break;
                    sb.Append(Encoding.UTF8.GetString(buf, 0, n));
                    string s = sb.ToString();
                    if (headerEnd < 0 && (headerEnd = s.IndexOf("\r\n\r\n", StringComparison.Ordinal)) >= 0)
                    {
                        var m = System.Text.RegularExpressions.Regex.Match(s[..headerEnd], @"Content-Length:\s*(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        contentLength = m.Success ? int.Parse(m.Groups[1].Value) : 0;
                    }
                    if (headerEnd >= 0 && Encoding.UTF8.GetByteCount(s[(headerEnd + 4)..]) >= contentLength) break;
                }
                string req = sb.ToString();
                string resp = JsonSerializer.Serialize(new { choices = new[] { new { message = new { role = "assistant", content = reply } } } });
                byte[] payload = Encoding.UTF8.GetBytes(resp);
                byte[] head = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: {payload.Length}\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(head);
                await stream.WriteAsync(payload);
                listener.Stop();
                return req;
            });
            return (port, body);
        }

        [Fact]
        public async Task Web_chat_sends_signed_asker_ticket_and_gateway_applies_asker_permissions()
        {
            using (var off = await AgentToolsAi3Tests.FactoryAsync())
            {
                off.CreateAuthorizedClient().Dispose();
                using var s0 = off.Services.CreateScope();
                using var a0 = s0.ServiceProvider.GetRequiredService<ICurrentActor>().BeginScope(ActorInfo.ForUser(InMemoryApiFactory.UserId));
                var r0 = await s0.ServiceProvider.GetRequiredService<IAgentChatService>().Chat(new AgentChatRequest { Agent = "VIVI", Message = "Hôm nay thế nào?" });
                r0.ResponseCode.Should().NotBe(200);
                r0.ResponseMessage.Should().Contain("chưa kết nối máy chủ Hermes");
            }

            var (port, body) = FakeHermes("Hôm nay 3 sự cố, đã xử lý 2.");
            using var f = await AgentToolsAi3Tests.FactoryAsync(("Agent:HermesApiUrl", $"http://127.0.0.1:{port}"));
            f.CreateAuthorizedClient().Dispose();
            Guid plain = Guid.NewGuid();
            await f.SeedAsync(db => { db.User.Add(new User { Id = plain, UserName = "nv-khong-quyen-a14", PassWord = "!NO-LOGIN!", IsActive = true, AccountType = "INTERNAL" }); return Task.CompletedTask; });
            using (var scope = f.Services.CreateScope())
            using (scope.ServiceProvider.GetRequiredService<ICurrentActor>().BeginScope(ActorInfo.ForUser(plain)))
            {
                var r = await scope.ServiceProvider.GetRequiredService<IAgentChatService>().Chat(new AgentChatRequest { Agent = "VIVI", Message = "Hôm nay thế nào?", History = [new AgentChatTurn { Role = "user", Content = "chào" }] });
                r.ResponseCode.Should().Be(200, r.ResponseMessage);
                r.Data!.Reply.Should().Be("Hôm nay 3 sự cố, đã xử lý 2.");
            }
            string raw = await body;
            int split = raw.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            string head = raw[..split], payload = raw[(split + 4)..];
            head.Should().StartWith("POST /chat/completions ");
            var sent = JsonNode.Parse(payload)!;
            sent["model"]!.GetValue<string>().Should().Be("delicare-vivi");
            sent["user"]!.GetValue<string>().Should().Be("user:" + plain.ToString("N"));
            sent["messages"]!.AsArray().Select(m => m!["content"]!.GetValue<string>()).Should().Equal("chào", "Hôm nay thế nào?");
            string ticket = System.Text.RegularExpressions.Regex.Match(head, @"X-On-Behalf-Of:\s*(\S+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Groups[1].Value;
            ticket.Should().StartWith("web:");
            payload.Should().NotContain(ticket, "vé chỉ ở header – mô hình không thấy");

            // Hermes dùng vé gọi công cụ: người hỏi không có quyền màn → từ chối theo người hỏi (không theo quyền agent).
            var denied = await CallAsync(f, "VIVI", "bao_cao_dieu_hanh", new { }, ticket);
            denied.Status.Should().Be(403);
            denied.Message.Should().Contain("Người hỏi không có quyền");
            var tampered = await CallAsync(f, "VIVI", "bao_cao_dieu_hanh", new { }, ticket[..^2] + (ticket.EndsWith("00") ? "11" : "00"));
            tampered.Status.Should().Be(403);
            tampered.Message.Should().Contain("Phiên chat trên web");
            var cfg = f.Services.GetRequiredService<IConfiguration>();
            AgentWebTicket.Verify(cfg, AgentWebTicket.Issue(cfg, plain, DateTime.UtcNow.AddMinutes(-30)), DateTime.UtcNow).Should().BeNull("vé hết hạn");
            AgentWebTicket.Verify(cfg, ticket, DateTime.UtcNow).Should().Be(plain);
        }
    }
}
