using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Model;
using AppDelicare.Services.Implement.Agent;
using AppDelicare.Services.Implement.Messaging;
using AppDelicare.Services.Interface;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Agent
{
    /// <summary>
    /// AI-4 (D-133): nguồn sự kiện <c>lay_su_kien(since)</c> – gọi lại với since = next không trả trùng (cả cảnh báo không có mốc thời gian);
    /// sự cố gấp gửi webhook Hermes đúng 1 lần, đã che dữ liệu cá nhân; Hermes tắt / không tới được thì nghiệp vụ vẫn chạy, không ném lỗi.
    /// </summary>
    public class AgentEventsAi4Tests
    {
        private static int FreePort()
        {
            var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            int port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }

        private static async Task<int> RaiseIncidentAsync(InMemoryApiFactory f, string key, object data)
        {
            using var scope = f.Services.CreateScope();
            var sp = scope.ServiceProvider;
            using var actor = sp.GetRequiredService<ICurrentActor>().BeginScope(ActorInfo.ForSystem(InMemoryApiFactory.UserId));
            int n = await BusinessNotifier.SafeEnqueueAsync(sp.GetRequiredService<INotificationService>(),
                new NotificationRequest { Template = NotificationCatalog.IotOutOfRange, IdempotencyKey = key, Data = data });
            await sp.GetRequiredService<AppDelicare.Data.Interface.IUnitOfWork>().DbContext.SaveChangesAsync();
            return n;
        }

        private static JsonArray Items(AgentCallResult r)
        {
            r.Status.Should().Be(200, r.Message);
            return ((JsonNode)r.Data!)["items"]!.AsArray();
        }

        [Fact]
        public async Task Since_next_returns_no_duplicates()
        {
            using var f = await AgentToolsAi3Tests.FactoryAsync();
            DateTime now = f.Clock.Now;
            await f.SeedAsync(db =>
            {
                db.ApprovalRequest.Add(new ApprovalRequest { Id = Guid.NewGuid(), RequestNo = "YC-AI4-01", BusinessType = "PURCHASE_ORDER", RequestedAt = now.AddMinutes(-10), RequestStatus = "Pending" });
                db.CustomerComplaint.Add(new CustomerComplaint { Id = Guid.NewGuid(), ComplaintNo = "PA-AI4-01", CustomerId = Guid.NewGuid(), Description = "Thiếu suất", ReceivedAt = now, Severity = "High", ComplaintStatus = "Open", CreatedDate = now.AddMinutes(-5) });
                db.AnalyticsRefreshLog.Add(new AnalyticsRefreshLog { Id = Guid.NewGuid(), JobCode = "Mrp", RunId = Guid.NewGuid(), StartedAt = now.AddMinutes(-4), CompletedAt = now.AddMinutes(-3), RunStatus = "Failed", ErrorMessage = "timeout" });
                return Task.CompletedTask;
            });
            await RaiseIncidentAsync(f, "IOT:AI4:since", new { zone = "Kho lạnh 1" });

            f.Clock.FixedNow = now.AddMinutes(5); // cửa sổ chắc chắn chứa mọi sự kiện vừa tạo (next làm tròn giây)
            var first = await AgentToolsAi3Tests.Call(f, "VIVI", "lay_su_kien", new { since = now.AddHours(-1).ToString("yyyy-MM-ddTHH:mm:ss") });
            var items = Items(first);
            var keys = items.Select(i => i!["key"]!.GetValue<string>()).ToList();
            keys.Should().Contain(k => k.StartsWith("APPROVAL:") && k.EndsWith(":NEW"), string.Join(", ", keys))
                .And.Contain(k => k.StartsWith("COMPLAINT:")).And.Contain(k => k.StartsWith("JOB:")).And.Contain("INC:IOT:AI4:since");
            keys.Should().OnlyHaveUniqueItems();
            items.First()!["severity"]!.GetValue<string>().Should().Be("HIGH", "sự kiện gấp lên đầu");
            string next = ((JsonNode)first.Data!)["next"]!.GetValue<string>();

            f.Clock.FixedNow = now.AddMinutes(6);
            var second = Items(await AgentToolsAi3Tests.Call(f, "VIVI", "lay_su_kien", new { since = next }));
            second.Select(i => i!["key"]!.GetValue<string>()).Should().NotIntersectWith(keys, "since = next không trả lại sự kiện cũ (kể cả cảnh báo)");

            // định tuyến: WH không thấy sự cố nhiệt độ (QA), QA thấy
            f.Clock.FixedNow = now.AddMinutes(5);
            var qa = Items(await AgentToolsAi3Tests.Call(f, "QA", "lay_su_kien", new { since = now.AddHours(-1).ToString("yyyy-MM-ddTHH:mm:ss") }));
            qa.Select(i => i!["key"]!.GetValue<string>()).Should().Contain("INC:IOT:AI4:since").And.Contain(k => k.StartsWith("COMPLAINT:"), "phản ánh mức cao → QA");
            var wh = Items(await AgentToolsAi3Tests.Call(f, "WH", "lay_su_kien", new { since = now.AddHours(-1).ToString("yyyy-MM-ddTHH:mm:ss") }));
            wh.Select(i => i!["key"]!.GetValue<string>()).Should().NotContain("INC:IOT:AI4:since").And.NotContain(k => k.StartsWith("APPROVAL:"));
            (await AgentToolsAi3Tests.Call(f, "VIVI", "lay_su_kien", new { since = "hôm qua" })).Status.Should().Be(400);
        }

        [Fact]
        public async Task Urgent_incident_is_pushed_to_hermes_once_and_masked()
        {
            int port = FreePort();
            string url = $"http://localhost:{port}/delicare/event/";
            using var listener = new HttpListener();
            listener.Prefixes.Add(url);
            listener.Start();
            var bodies = new List<string>();
            var serve = Task.Run(async () =>
            {
                while (listener.IsListening)
                {
                    HttpListenerContext ctx;
                    try { ctx = await listener.GetContextAsync(); } catch { break; }
                    using (var rd = new StreamReader(ctx.Request.InputStream))
                    {
                        string body = await rd.ReadToEndAsync();
                        lock (bodies) { bodies.Add(body); }
                    }
                    ctx.Response.StatusCode = 200;
                    ctx.Response.Close();
                }
            });
            using var f = await AgentToolsAi3Tests.FactoryAsync(("Agent:HermesWebhookUrl", url));
            await RaiseIncidentAsync(f, "IOT:AI4:once", new { zone = "Kho lạnh 2", contactPhone = "0912345678", note = "gọi 0987654321" });
            await RaiseIncidentAsync(f, "IOT:AI4:once", new { zone = "Kho lạnh 2" });
            // Máy test tải nặng: đợi listener đọc xong body rồi mới dừng (tránh mất body – test chập chờn).
            for (int i = 0; i < 30 && bodies.Count == 0; i++)
            {
                await Task.Delay(100);
            }
            await Task.Delay(200);
            listener.Stop();
            await serve;

            bodies.Should().HaveCount(1, "1 sự việc gửi 1 lần");
            bodies[0].Should().Contain("INC:IOT:AI4:once").And.Contain("Kho lạnh 2").And.NotContain("0912345678").And.NotContain("0987654321");
            var logs = await f.QueryAsync(db => db.IntegrationLog.AsNoTracking().Where(l => l.OperationCode == AgentIncidentService.Operation).ToListAsync());
            logs.Should().ContainSingle().Which.LogStatus.Should().Be("Sent");
        }

        [Fact]
        public async Task Hermes_down_or_disabled_does_not_block_business()
        {
            string dead = $"http://127.0.0.1:{FreePort()}/khong-co/";
            using (var f = await AgentToolsAi3Tests.FactoryAsync(("Agent:HermesWebhookUrl", dead)))
            {
                var watch = Stopwatch.StartNew();
                await RaiseIncidentAsync(f, "IOT:AI4:down", new { zone = "Kho" });
                watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(60), "timeout webhook 3 giây – không treo nghiệp vụ");
                (await f.QueryAsync(db => db.IntegrationLog.AsNoTracking().SingleAsync(l => l.OperationCode == AgentIncidentService.Operation))).LogStatus.Should().Be("Failed");
            }
            using (var off = await AgentToolsAi3Tests.FactoryAsync(("Agent:Enabled", "false"), ("Agent:HermesWebhookUrl", dead)))
            {
                await RaiseIncidentAsync(off, "IOT:AI4:off", new { zone = "Kho" });
                (await off.QueryAsync(db => db.IntegrationLog.AsNoTracking().SingleAsync(l => l.OperationCode == AgentIncidentService.Operation))).LogStatus.Should().Be("Recorded", "tắt agent: không gửi, nghiệp vụ vẫn ghi");
                (await AgentToolsAi3Tests.Call(off, "VIVI", "lay_su_kien")).Status.Should().Be(503);
            }
            // mẫu không phải sự cố → không ghi sự kiện agent
            using var g = await AgentToolsAi3Tests.FactoryAsync();
            using var scope = g.Services.CreateScope();
            await BusinessNotifier.SafeEnqueueAsync(scope.ServiceProvider.GetRequiredService<INotificationService>(), new NotificationRequest { Template = NotificationCatalog.StockLow, IdempotencyKey = "x" });
            scope.ServiceProvider.GetRequiredService<AppDelicare.Data.Interface.IUnitOfWork>().DbContext.ChangeTracker.Entries<IntegrationLog>().Should().BeEmpty();
        }
    }
}
