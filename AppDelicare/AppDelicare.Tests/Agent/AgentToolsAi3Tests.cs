using System.Text.Json;
using System.Text.Json.Nodes;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Implement.Agent;
using AppDelicare.Services.Interface;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Agent
{
    /// <summary>
    /// AI-3 (D-133): danh mục công cụ đọc cho VIVI / WH / KITCHEN / QA. Mỗi công cụ trả 200 + JSON đúng dạng (schema tham số hợp lệ); gọi công cụ ngoài danh sách agent
    /// → 403 + ghi nhật ký Denied; kết quả không còn số điện thoại / email (AgentMask che tập trung + cắt Agent:MaxRows); số liệu = method Service của màn web.
    /// </summary>
    public class AgentToolsAi3Tests
    {
        internal const string Key = "khoa-ai3-test";
        private static readonly string[] Agents = ["VIVI", "WH", "KITCHEN", "QA", "PROC", "SALES", "LOG", "HR", "PLAN", "PKG", "ACC"];

        private static Guid AgentUser(string code) => Guid.Parse($"a3a3a3a3-0000-4000-8000-0000000000{Array.IndexOf(Agents, code) + 10:D2}");

        internal static async Task<InMemoryApiFactory> FactoryAsync(params (string Key, string Value)[] settings)
        {
            var f = new InMemoryApiFactory();
            f.Settings["AGENT_KEY_T3"] = Key;
            foreach (var (k, v) in settings)
            {
                f.Settings[k] = v;
            }
            await f.SeedAsync(db =>
            {
                Guid hermes = Guid.NewGuid();
                db.IntegrationChannel.Add(new IntegrationChannel { Id = hermes, ChannelCode = "HERMES", ChannelName = "Hermes", ProviderCode = "HERMES", ChannelStatus = "Active" });
                var screens = AgentToolRegistry.All.Where(t => t.ScreenId != null).Select(t => t.ScreenId!).Distinct().ToList();
                var menus = new List<Guid>();
                foreach (var s in screens)
                {
                    var m = db.ObjectMenu.FirstOrDefault(x => x.MetaLink == "#/" + s);
                    if (m == null)
                    {
                        m = new ObjectMenu { Id = Guid.NewGuid(), Name = s, MetaLink = "#/" + s, LevelShow = 2, OrderShow = 1, IsActive = true };
                        db.ObjectMenu.Add(m);
                    }
                    menus.Add(m.Id);
                }
                foreach (var code in Agents)
                {
                    Guid g = Guid.NewGuid();
                    db.BotConfiguration.Add(new BotConfiguration { Id = Guid.NewGuid(), IntegrationChannelId = hermes, BotCode = code, BotName = "Agent " + code, BotStatus = "Active", SecretReference = "AGENT_KEY_T3" });
                    db.User.Add(new User { Id = AgentUser(code), UserName = "agent." + code.ToLowerInvariant(), PassWord = "!NO-LOGIN!", IsActive = true, AccountType = "INTERNAL" });
                    db.UserExternalIdentity.Add(new UserExternalIdentity { Id = Guid.NewGuid(), UserId = AgentUser(code), IntegrationChannelId = hermes, ExternalUserId = code, IdentityStatus = "Active" });
                    db.UserGroup.Add(new UserGroup { Id = g, Name = "AGENT_" + code, Code = "AGENT_" + code });
                    db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = AgentUser(code), UserGroupId = g });
                    db.UserRole.AddRange(menus.Select(m => new UserRole { Id = Guid.NewGuid(), UserGroupId = g, ObjectMenuId = m, AllowAccess = true }));
                }
                return Task.CompletedTask;
            });
            return f;
        }

        internal static async Task<AgentCallResult> Call(InMemoryApiFactory f, string agent, string tool, object? args = null)
        {
            using var scope = f.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IAgentGatewayService>().CallAsync(new AgentCallRequest
            {
                AgentCode = agent, AgentKey = Key, Tool = tool, Arguments = JsonSerializer.SerializeToElement(args ?? new { }),
            });
        }

        [Fact]
        public async Task Every_read_tool_returns_json_for_its_agents()
        {
            using var f = await FactoryAsync();
            var readTools = AgentToolRegistry.All.Where(t => t.Name != "ping").ToList();
            readTools.Select(t => t.Name).Should().Contain(["bao_cao_dieu_hanh", "kpi_khoang", "su_co_ngay", "ds_cho_duyet", "canh_bao", "get_ton_kho", "get_lo_can_date", "du_bao_thieu_hut",
                "truy_xuat_lo", "truy_xuat_me", "tien_do_bep", "tien_do_bep_chi_tiet", "ke_hoach_ngay", "thuc_don_tuan", "hao_hut_khoang", "nhiet_do_ngay", "phan_anh_khach"]);
            var failures = new List<string>();
            foreach (var t in readTools)
            {
                // schema tham số: JSON object hợp lệ
                using (var schema = JsonDocument.Parse(t.InputSchemaJson))
                {
                    schema.RootElement.GetProperty("type").GetString().Should().Be("object", t.Name);
                }
                string agent = Agents.First(a => t.AllowedFor(a));
                var r = await Call(f, agent, t.Name);
                bool needsId = t.InputSchemaJson.Contains("\"required\"", StringComparison.Ordinal); // tham số bắt buộc → gọi rỗng phải 400
                if (needsId ? r.Status != 400 : r.Status != 200 || r.Data is not JsonNode)
                {
                    failures.Add($"{t.Name}/{agent}: {r.Status} {r.Message}");
                }
            }
            failures.Should().BeEmpty();
            var miss = await Call(f, "QA", "truy_xuat_lo", new { so_lo = "LO-KHONG-CO" });
            miss.Status.Should().Be(400);
            miss.Message.Should().Contain("Không tìm thấy lô");
            (await Call(f, "VIVI", "bao_cao_dieu_hanh", new { ngay = "31-31-2026" })).Message.Should().Contain("không hợp lệ");
        }

        [Fact]
        public void Tool_screens_are_menu_screens_not_hidden_children()
        {
            // màn ẩn (MENU-4) không có mục menu bật → quyền tính theo màn cha; công cụ phải kiểm đúng màn web kiểm.
            AgentToolRegistry.All.Where(t => t.ScreenId != null && AppDelicare.WebApi.Security.ScreenMap.HiddenScreenParent.ContainsKey(t.ScreenId))
                .Select(t => $"{t.Name}→{t.ScreenId}").Should().BeEmpty();
        }

        [Fact]
        public async Task Tool_outside_agent_list_is_denied_and_logged()
        {
            using var f = await FactoryAsync();
            var r = await Call(f, "WH", "tien_do_bep_chi_tiet");
            r.Status.Should().Be(403);
            r.Message.Should().Contain("không thuộc agent WH");
            (await Call(f, "KITCHEN", "truy_xuat_lo", new { so_lo = "x" })).Status.Should().Be(403);
            var logs = await f.QueryAsync(db => db.IntegrationLog.AsNoTracking().Where(l => l.LogStatus == "Denied").ToListAsync());
            logs.Select(l => l.OperationCode).Should().Contain(["tien_do_bep_chi_tiet", "truy_xuat_lo"]);
            using var scope = f.Services.CreateScope();
            var (_, tools) = await scope.ServiceProvider.GetRequiredService<IAgentGatewayService>().ListToolsAsync(new AgentCallRequest { AgentCode = "WH", AgentKey = Key });
            tools.Select(t => t.Name).Should().Contain("get_lo_can_date").And.NotContain("tien_do_bep_chi_tiet").And.NotContain("thuc_don_tuan");
        }

        [Fact]
        public void Mask_hides_phone_email_and_ids_and_truncates()
        {
            AgentMask.Text("Gọi 0912 345 678 hoặc +84912345678, mail a.b@delicare.vn, STK 0123456789012").Should()
                .NotContainAny("0912", "345 678", "84912345678", "@delicare", "0123456789012").And.Contain(AgentMask.Hidden);
            AgentMask.Text("Lô LO-261124-NC00026-01 hạn 2026-11-30, 12 suất, mã 3fa85f64-5717-4562-b3fc-2c963f66afa6").Should()
                .Be("Lô LO-261124-NC00026-01 hạn 2026-11-30, 12 suất, mã 3fa85f64-5717-4562-b3fc-2c963f66afa6", "không che số lô / ngày / Guid");
            AgentMask.Text("2026-11-27T06:24:05.1234567").Should().Be("2026-11-27T06:24:05.1234567", "mốc ISO (giây lẻ) không bị che");
            var data = new
            {
                customer = new { name = "Cty A", phoneNumber = "028 1234", contactEmail = "x", note = "LH 0987654321" },
                items = Enumerable.Range(1, 8).Select(i => new { i, mobile = "0900000000" }).ToList(),
            };
            var (node, rows) = AgentMask.Apply(data, 5, AgentToolRegistry.WebJson);
            string json = node!.ToJsonString();
            json.Should().NotContain("028 1234").And.NotContain("0987654321").And.NotContain("0900000000");
            node["customer"]!["name"]!.GetValue<string>().Should().Be("Cty A");
            node["items"]!.AsArray().Count.Should().Be(5);
            node["items_total"]!.GetValue<int>().Should().Be(8);
            node["items_truncated"]!.GetValue<bool>().Should().BeTrue();
            rows.Should().Be(8);
            var (top, _) = AgentMask.Apply(Enumerable.Range(1, 9).ToList(), 3, AgentToolRegistry.WebJson);
            top!["total"]!.GetValue<int>().Should().Be(9);
        }

        [Fact]
        public async Task Masking_applies_to_tool_results_end_to_end()
        {
            using var f = await FactoryAsync();
            await f.SeedAsync(db =>
            {
                db.CustomerComplaint.Add(new CustomerComplaint
                {
                    Id = Guid.NewGuid(), ComplaintNo = "PA-AI3-01", CustomerId = Guid.NewGuid(), ReceivedAt = f.Clock.Today, SourceChannel = "gọi 0912345678",
                    Description = "Chị Lan (lan@khach.vn) báo thiếu 3 suất", ComplaintStatus = "Open",
                });
                return Task.CompletedTask;
            });
            var r = await Call(f, "QA", "phan_anh_khach");
            r.Status.Should().Be(200, r.Message);
            string json = ((JsonNode)r.Data!).ToJsonString();
            json.Should().Contain("PA-AI3-01").And.NotContain("0912345678").And.NotContain("lan@khach.vn");
        }

        [Fact]
        public async Task Figures_match_the_web_service()
        {
            using var f = await FactoryAsync();
            using var scope = f.Services.CreateScope();
            var sp = scope.ServiceProvider;
            DateTime today = sp.GetRequiredService<AppDelicare.Data.Share.IClock>().Today;
            using var actor = sp.GetRequiredService<AppDelicare.Data.Share.ICurrentActor>().BeginScope(AppDelicare.Data.Share.ActorInfo.ForAgent(AgentUser("VIVI"), "VIVI"));
            var web = await sp.GetRequiredService<IDashboardService>().GetExecutive(today, null, null);
            var tool = await Call(f, "VIVI", "bao_cao_dieu_hanh");
            var (expected, _) = AgentMask.Apply(web.Data, 50, AgentToolRegistry.WebJson);
            ((JsonNode)tool.Data!).ToJsonString().Should().Be(expected!.ToJsonString(), "cùng method Service với màn Bảng điều hành");

            var lots = await sp.GetRequiredService<IMaterialLotService>().GetList(new MaterialLotListRequest { ExpiringWithinDays = 3, InStock = true });
            var lotTool = await Call(f, "WH", "get_lo_can_date");
            lotTool.Status.Should().Be(200);
            int count = lotTool.Data is JsonArray a ? a.Count : ((JsonNode)lotTool.Data!)["total"]!.GetValue<int>();
            count.Should().Be(lots.ListData?.Count ?? 0);
        }
    }
}
