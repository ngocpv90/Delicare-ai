using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Agent
{
    /// <summary>
    /// AI-21 (D-138) – Trung tâm Agent: chi phí AI chỉ CEO / COO / FIN-IT (mục quyền ẩn ai-cost – API 403 cho người khác, ma trận + seed agent không cấp);
    /// tab theo quyền mục (Tổng quan = ai-agents, Giao việc = ai-tasks, Luồng = ai-flow); việc tự đóng khi nguồn hết điều kiện, mở lại khi nguồn có lại,
    /// "Đã xử lý" ghi người + ghi chú vào sổ việc thư mục 98; chỉ hiện việc người xem có quyền Xem màn nguồn; 12 bước QT-SX-01 tính từ bảng sẵn có.
    /// </summary>
    public class AgentCenterAi21Tests
    {
        private static async Task<JsonNode?> Get(HttpClient c, string url) => JsonNode.Parse(await (await c.GetAsync(url)).Content.ReadAsStringAsync());

        /// <summary>Người dùng thuộc 1 nhóm với quyền (mục, Xem, Thêm, Sửa).</summary>
        private static async Task<Guid> UserAsync(InMemoryApiFactory f, string code, params (string Screen, bool Add, bool Edit)[] rights)
        {
            Guid u = Guid.NewGuid(), g = Guid.NewGuid();
            await f.SeedAsync(async db =>
            {
                db.User.Add(new User { Id = u, UserName = "uat." + code.ToLowerInvariant(), IsActive = true, AccountType = "INTERNAL" });
                db.UserGroup.Add(new UserGroup { Id = g, Name = code, Code = code });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = u, UserGroupId = g });
                foreach (var (screen, add, edit) in rights)
                {
                    string link = "#/" + screen;
                    var m = db.ObjectMenu.Local.FirstOrDefault(x => x.MetaLink == link) ?? await db.ObjectMenu.FirstOrDefaultAsync(x => x.MetaLink == link);
                    if (m == null)
                    {
                        m = new ObjectMenu { Id = Guid.NewGuid(), Name = screen, MetaLink = link, LevelShow = 2, OrderShow = 1, IsActive = true };
                        db.ObjectMenu.Add(m);
                    }
                    db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = g, ObjectMenuId = m.Id, AllowAccess = true, AllowAdd = add, AllowEdit = edit });
                }
            });
            return u;
        }

        private static (string, bool, bool) V(string s) => (s, false, false);

        [Fact]
        public async Task Cost_is_visible_only_to_ceo_coo_finit_and_other_tabs_follow_their_own_rights()
        {
            using var f = await AgentToolsAi3Tests.FactoryAsync();
            var bod = new[] { V("ai-agents"), V("ai-cost"), V("ai-flow"), V("ai-tasks"), V("ai-agent-log"), V("ai-schedule") };
            Guid ceo = await UserAsync(f, "CEO-T21", bod), coo = await UserAsync(f, "COO-T21", bod), it = await UserAsync(f, "FINIT-T21", bod);
            Guid wh = await UserAsync(f, "OPSWH-T21", V("ai-tasks"), V("scm-inv-balance"));
            Guid agentsOnly = await UserAsync(f, "KHAC-T21", V("ai-agents"), V("ai-agent-log"));
            foreach (Guid u in new[] { ceo, coo, it })
            {
                using var c = f.CreateAuthorizedClient(u);
                (await c.GetAsync("/api/Agent/GetCost")).StatusCode.Should().Be(HttpStatusCode.OK);
                (await c.GetAsync("/api/Agent/GetOverview")).StatusCode.Should().Be(HttpStatusCode.OK);
            }
            using (var c = f.CreateAuthorizedClient(agentsOnly))
            {
                (await c.GetAsync("/api/Agent/GetCost")).StatusCode.Should().Be(HttpStatusCode.Forbidden, "có Tổng quan + Nhật ký nhưng không có mục ai-cost → không thấy chi phí");
                (await c.GetAsync("/api/Agent/GetOverview")).StatusCode.Should().Be(HttpStatusCode.OK);
            }
            using (var c = f.CreateAuthorizedClient(wh))
            {
                (await c.GetAsync("/api/Agent/GetCost")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
                (await c.GetAsync("/api/Agent/GetOverview")).StatusCode.Should().Be(HttpStatusCode.Forbidden, "chỉ có tab Giao việc");
                (await c.GetAsync("/api/Agent/GetFlowDay")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
                (await c.GetAsync("/api/Agent/GetTasks")).StatusCode.Should().Be(HttpStatusCode.OK);
                var agents = await Get(c, "/api/Agent/GetChatAgents");
                agents!["listData"]!.AsArray().Should().HaveCount(13, "tab Chat mở cho người vào được Trung tâm Agent");
            }
            Guid none = await UserAsync(f, "NONE-T21", V("crm-order"));
            using (var c = f.CreateAuthorizedClient(none))
            {
                (await c.GetAsync("/api/Agent/GetChatAgents")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
                (await c.GetAsync("/api/Agent/GetTasks")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            }
        }

        [Fact]
        public async Task Permission_matrix_and_agent_seed_grant_cost_only_to_ceo_coo_finit()
        {
            string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
            string matrix = await File.ReadAllTextAsync(Path.Combine(root, "DB", "scripts", "D79_permission_matrix.sql"));
            var granted = Regex.Matches(matrix, @"\(N'([A-Z\-]+)', N'#/ai-cost', 1,").Select(m => m.Groups[1].Value).ToList();
            granted.Should().BeEquivalentTo(["CEO", "COO", "FIN-IT"]);
            string agents = await File.ReadAllTextAsync(Path.Combine(root, "DB", "seed", "Agent_seed.sql"));
            Regex.IsMatch(agents, @"\(N'AGENT_[A-Z]+', N'#/ai-cost'\)").Should().BeFalse("agent không bao giờ có mục chi phí AI");
            string sync = await File.ReadAllTextAsync(Path.Combine(root, "DB", "seed", "Z_Menu_sync.sql"));
            sync.Should().Contain("(N'#/ai-agents', 2, N'#/group/01',").And.Contain("(N'#/ai-cost', 2, N'#/group/01',").And.Contain("(N'#/group/14', 1, NULL, 1060, N'Trợ lý AI', N'ai', 0)");
        }

        [Fact]
        public async Task Tasks_auto_close_when_source_is_resolved_reopen_and_record_done_note()
        {
            string vault = Path.Combine(Path.GetTempPath(), "dc-kb-ai21-" + Guid.NewGuid().ToString("N"));
            using var f = await AgentToolsAi3Tests.FactoryAsync(("BusinessRules:Alerts:CacheSeconds", "0"), ("Knowledge:VaultPath", vault));
            Guid cs = await UserAsync(f, "SALCS-T21", ("ai-tasks", true, true), ("log-complaint", true, true));
            Guid other = await UserAsync(f, "OPSKIT-T21", ("ai-tasks", true, true), V("prod-batch"));
            Guid cust = Guid.NewGuid(), c1 = Guid.NewGuid();
            await f.SeedAsync(db =>
            {
                db.Customer.Add(new Customer { Id = cust, Code = "KH-T21", Name = "Khách AI-21", CIF = "KH-T21", CustomerType = "B2B", DisplayName = "Khách AI-21", Status = "Active" });
                db.CustomerComplaint.Add(new CustomerComplaint { Id = c1, ComplaintNo = "PA-T21-1", CustomerId = cust, Description = "Thiếu 2 suất", ReceivedAt = DateTime.Now, SlaDueAt = DateTime.Now.AddDays(2), ComplaintStatus = ComplaintStatus.Open });
                return Task.CompletedTask;
            });
            using var c = f.CreateAuthorizedClient(cs);
            string key = $"ALERT:{AppDelicare.Services.Implement.CommonService.AlertCode.ComplaintOpen}:{DateTime.Today:yyyyMMdd}";
            JsonNode? Find(JsonNode? board) => board!["data"]!["listTask"]!.AsArray().FirstOrDefault(t => t!["key"]!.GetValue<string>() == key);

            var b1 = await Get(c, "/api/Agent/GetTasks?includeDone=true");
            var t1 = Find(b1);
            t1.Should().NotBeNull("phản ánh đang mở → việc cho CSKH");
            t1!["status"]!.GetValue<string>().Should().Be("OPEN");
            t1["mine"]!.GetValue<bool>().Should().BeTrue("có quyền Thêm màn Phản ánh");
            t1["agent"]!.GetValue<string>().Should().Be("CSKH");
            t1["listTodo"]!.AsArray().Should().NotBeEmpty();
            using (var o = f.CreateAuthorizedClient(other))
            {
                Find(await Get(o, "/api/Agent/GetTasks?includeDone=true")).Should().BeNull("không có quyền Xem màn nguồn → không thấy việc");
            }

            // Nguồn đã xử lý → việc tự đóng (không ai bấm).
            await f.SeedAsync(async db => { (await db.CustomerComplaint.SingleAsync(x => x.Id == c1)).ComplaintStatus = ComplaintStatus.Resolved; });
            var b2 = await Get(c, "/api/Agent/GetTasks?includeDone=true");
            Find(b2)!["status"]!.GetValue<string>().Should().Be("AUTO");
            b2!["data"]!["open"]!.GetValue<int>().Should().Be(0);
            Find(await Get(c, "/api/Agent/GetTasks"))!.Should().BeNull("mặc định chỉ việc đang mở");

            // Nguồn có lại → mở lại; "Đã xử lý" ghi người + ghi chú; không mở lại trong ngày.
            await f.SeedAsync(async db => { (await db.CustomerComplaint.SingleAsync(x => x.Id == c1)).ComplaintStatus = ComplaintStatus.InProgress; });
            Find(await Get(c, "/api/Agent/GetTasks"))!["status"]!.GetValue<string>().Should().Be("OPEN");
            var done = await c.PostAsJsonAsync("/api/Agent/CloseTask", new { key, note = "Đã gọi khách, hẹn giao bù" });
            var dj = JsonNode.Parse(await done.Content.ReadAsStringAsync())!;
            dj["responseCode"]!.GetValue<int>().Should().Be(200, dj.ToJsonString());
            dj["data"]!["status"]!.GetValue<string>().Should().Be("DONE");
            var t3 = Find(await Get(c, "/api/Agent/GetTasks?includeDone=true"))!;
            t3["status"]!.GetValue<string>().Should().Be("DONE");
            t3["closedBy"]!.GetValue<string>().Should().Be("uat.salcs-t21");
            t3["note"]!.GetValue<string>().Should().Contain("hẹn giao bù");

            string ledger = await File.ReadAllTextAsync(Path.Combine(vault, "98 Công việc", "_so-viec", $"{DateTime.Today:yyyy-MM-dd}.md"));
            ledger.Should().Contain($"- [mo] {key}").And.Contain($"- [tu-dong] {key}").And.Contain($"- [xu-ly] {key}").And.Contain("uat.salcs-t21");

            // Người không có quyền Sửa Giao việc → 403.
            Guid viewOnly = await UserAsync(f, "VIEW-T21", V("ai-tasks"), V("log-complaint"));
            using var v = f.CreateAuthorizedClient(viewOnly);
            (await v.PostAsJsonAsync("/api/Agent/CloseTask", new { key })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task Flow_day_has_12_steps_computed_from_existing_tables()
        {
            using var f = await AgentToolsAi3Tests.FactoryAsync();
            Guid ceo = await UserAsync(f, "CEO-F21", V("ai-flow"), V("ai-tasks"), V("crm-order"), V("prod-plan"));
            DateTime tomorrow = DateTime.Today.AddDays(1);
            using var c = f.CreateAuthorizedClient(ceo);
            var d0 = await Get(c, $"/api/Agent/GetFlowDay?date={tomorrow:yyyy-MM-dd}");
            var steps = d0!["data"]!["listStep"]!.AsArray();
            steps.Should().HaveCount(12);
            steps.Select(s => s!["no"]!.GetValue<int>()).Should().Equal(Enumerable.Range(1, 12));
            steps[0]!["status"]!.GetValue<string>().Should().Be("BAD", "ngày mai chưa có đơn");
            steps[0]!["canOpen"]!.GetValue<bool>().Should().BeTrue();
            steps[4]!["canOpen"]!.GetValue<bool>().Should().BeFalse("không có quyền Tồn kho");
            steps[0]!["listChain"]!.AsArray().Should().NotBeEmpty();
            var board = await Get(c, "/api/Agent/GetTasks");
            board!["data"]!["listTask"]!.AsArray().Select(t => t!["key"]!.GetValue<string>()).Should().Contain($"FLOW:{tomorrow:yyyyMMdd}:01", "bước có vấn đề tự sinh việc cho agent phòng đó");

            Guid cust = Guid.NewGuid();
            await f.SeedAsync(db =>
            {
                db.Customer.Add(new Customer { Id = cust, Code = "KH-F21", Name = "Khách luồng", CIF = "KH-F21", CustomerType = "B2B", DisplayName = "Khách luồng", Status = "Active" });
                db.Order.Add(new Order { Id = Guid.NewGuid(), CustomerId = cust, OrderNumber = "DH-F21-1", OrderDate = DateTime.Today, MealDate = tomorrow, DeliveryDate = tomorrow, Status = OrderStatus.Draft, TotalQuantity = 120 });
                db.Order.Add(new Order { Id = Guid.NewGuid(), CustomerId = cust, OrderNumber = "DH-F21-2", OrderDate = DateTime.Today, MealDate = tomorrow, DeliveryDate = tomorrow, Status = OrderStatus.Locked, TotalQuantity = 80 });
                return Task.CompletedTask;
            });
            var s1 = (await Get(c, $"/api/Agent/GetFlowDay?date={tomorrow:yyyy-MM-dd}"))!["data"]!["listStep"]![0]!;
            s1["status"]!.GetValue<string>().Should().Be("WARN");
            s1["note"]!.GetValue<string>().Should().Contain("1 / 2 đơn chưa xác nhận");
            await f.SeedAsync(async db => { foreach (var o in await db.Order.Where(x => x.CustomerId == cust).ToListAsync()) o.Status = OrderStatus.Locked; });
            var s2 = (await Get(c, $"/api/Agent/GetFlowDay?date={tomorrow:yyyy-MM-dd}"))!["data"]!["listStep"]!;
            s2[0]!["status"]!.GetValue<string>().Should().Be("OK");
            s2[0]!["note"]!.GetValue<string>().Should().Contain("200 suất");
            s2[3]!["status"]!.GetValue<string>().Should().Be("BAD", "đơn đã khóa nhưng chưa có kế hoạch sản xuất");
            var after = await Get(c, "/api/Agent/GetTasks?includeDone=true");
            after!["data"]!["listTask"]!.AsArray().First(t => t!["key"]!.GetValue<string>() == $"FLOW:{tomorrow:yyyyMMdd}:01")!["status"]!.GetValue<string>().Should().Be("AUTO", "đơn đã chốt → việc bước 1 tự đóng");
        }
    }
}
