using System.Text.Json;
using System.Text.Json.Nodes;
using AppDelicare.Data.Share;
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
    /// AI-11 (D-133) – cảnh báo chủ động + luồng liên phòng:
    /// <c>gui_canh_bao</c> gửi bot Vận hành tới người có quyền Thêm màn liên quan, mỗi khóa sự kiện 1 lần, agent không xem được màn → từ chối;
    /// màn Luồng liên phòng nối đề xuất → duyệt → kết quả → tin theo CorrelationId; bật / tắt việc theo lịch ghi app-delicare-agents/schedules.json.
    /// </summary>
    public class AgentFlowAi11Tests
    {
        private static readonly Guid Vh = Guid.Parse("a11a11a1-0000-4000-8000-0000000000a1");

        private static async Task<Guid> SeedRecipientAsync(InMemoryApiFactory f, string screen)
        {
            Guid user = Guid.NewGuid(), group = Guid.NewGuid();
            await f.SeedAsync(async db =>
            {
                db.IntegrationChannel.Add(new IntegrationChannel { Id = Vh, ChannelCode = "TELEGRAM_VH", ChannelName = "VH", ProviderCode = "TELEGRAM", EnvironmentCode = "Development", ChannelStatus = "Active", ConfigJson = "{}" });
                db.BotConfiguration.Add(new BotConfiguration { Id = Guid.NewGuid(), IntegrationChannelId = Vh, BotCode = "VH", BotName = "Vận hành", BotStatus = "Active" });
                db.User.Add(new User { Id = user, UserName = "mua-hang-a11", IsActive = true });
                db.UserGroup.Add(new UserGroup { Id = group, Name = "OPS-SCM-A11", Code = "OPS-SCM-A11" });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = user, UserGroupId = group });
                var menu = await db.ObjectMenu.FirstAsync(m => m.MetaLink == "#/" + screen);
                db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = group, ObjectMenuId = menu.Id, AllowAccess = true, AllowAdd = true });
                db.UserExternalIdentity.Add(new UserExternalIdentity { Id = Guid.NewGuid(), UserId = user, IntegrationChannelId = Vh, ExternalUserId = "9911", LinkedAt = DateTime.Today, IdentityStatus = "Active" });
            });
            return user;
        }

        private static async Task<AgentCallResult> CallAsync(InMemoryApiFactory f, string agent, string tool, object args, string correlation)
        {
            using var scope = f.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IAgentGatewayService>().CallAsync(new AgentCallRequest
            {
                AgentCode = agent, AgentKey = AgentToolsAi3Tests.Key, Tool = tool, Arguments = JsonSerializer.SerializeToElement(args), CorrelationId = correlation,
            });
        }

        [Fact]
        public async Task Alert_goes_once_to_people_who_do_the_work_and_only_for_screens_the_agent_sees()
        {
            using var f = await AgentToolsAi3Tests.FactoryAsync();
            f.CreateAuthorizedClient().Dispose();
            await SeedRecipientAsync(f, "scm-requirement");
            var args = new { khoa_su_kien = "ALERT:STOCK_BELOW_MIN:20261128:3", man = "scm-requirement", tieu_de = "3 nguyên liệu dưới tồn tối thiểu", nhan_xet = "Thịt vai còn 2 kg, cần 40 kg ngày mai", phuong_an = new[] { "Đặt ngay 40 kg NCC Sơn Lâm", "Dùng nạc vai tươi thay 50%" }, muc_do = "CAO" };
            var r1 = await CallAsync(f, "PROC", "gui_canh_bao", args, "flow-a11");
            r1.Status.Should().Be(200, r1.Message);
            ((JsonNode)r1.Data!)["daGui"]!.GetValue<bool>().Should().BeTrue();
            var msgs = await f.QueryAsync(db => db.OutboundMessage.AsNoTracking().Where(m => m.IdempotencyKey != null && m.IdempotencyKey.StartsWith("AGENT_ALERT:PROC:")).ToListAsync());
            msgs.Should().ContainSingle();
            msgs[0].RecipientExternalId.Should().Be("9911");
            msgs[0].CorrelationId.Should().Be("flow-a11");
            JsonNode.Parse(msgs[0].PayloadJson!)!["data"]!["lines"]![0]!.GetValue<string>().Should().Contain("Đặt ngay 40 kg");

            var r2 = await CallAsync(f, "PROC", "gui_canh_bao", args, "flow-a11");
            r2.Status.Should().Be(200);
            ((JsonNode)r2.Data!)["daGui"]!.GetValue<bool>().Should().BeFalse("cùng khóa sự kiện chỉ gửi 1 lần");
            (await f.QueryAsync(db => db.OutboundMessage.CountAsync(m => m.IdempotencyKey != null && m.IdempotencyKey.StartsWith("AGENT_ALERT:")))).Should().Be(1);

            var r3 = await CallAsync(f, "PROC", "gui_canh_bao", new { khoa_su_kien = "x", man = "adm-user", tieu_de = "t", nhan_xet = "n" }, "flow-a11b");
            r3.Status.Should().Be(400);
            r3.Message.Should().Contain("không có quyền xem màn adm-user");
        }

        [Fact]
        public async Task Flow_board_links_proposal_decision_and_result_by_correlation_id()
        {
            using var f = await AgentToolsAi3Tests.FactoryAsync();
            f.CreateAuthorizedClient().Dispose();
            Guid ing = Guid.NewGuid(), uom = Guid.NewGuid(), sup = Guid.NewGuid(), lot = Guid.NewGuid();
            await f.SeedAsync(db =>
            {
                db.UnitMeasure.Add(new UnitMeasure { Id = uom, Code = "KG-A11", Name = "kg" });
                db.Supplier.Add(new Supplier { Id = sup, Code = "NCC-A11", Name = "NCC A11", Status = "Active" });
                db.Ingredient.Add(new Ingredient { Id = ing, Code = "NL-A11", Name = "Thịt A11", BaseUomId = uom });
                db.MaterialLot.Add(new MaterialLot { Id = lot, LotNumber = "LO-A11", LotNo = "LO-A11", IngredientId = ing, SupplierId = sup, Status = "Available", ReceivedDate = DateTime.Today });
                return Task.CompletedTask;
            });
            var p = await CallAsync(f, "QA", "propose_hold_lot", new { so_lo = "LO-A11", ly_do = "Mẻ ME-1 nhiệt độ lõi 62°C" }, "INC:CORE_TEMP_LOW:ME-1");
            p.Status.Should().Be(200, p.Message);
            Guid id = Guid.Parse(((JsonNode)p.Data!)["approvalRequestId"]!.GetValue<string>());
            using (var scope = f.Services.CreateScope())
            using (scope.ServiceProvider.GetRequiredService<ICurrentActor>().BeginScope(ActorInfo.ForUser(InMemoryApiFactory.UserId)))
            {
                (await scope.ServiceProvider.GetRequiredService<IApprovalRequestService>().Decide(new ApprovalDecideRequest { Id = id, Approve = true, Note = "QA lead OK" })).ResponseCode.Should().Be(200);
            }
            using var s2 = f.Services.CreateScope();
            var board = (await s2.ServiceProvider.GetRequiredService<IAgentFlowService>().GetFlows(null)).Data!;
            var flow = board.ListFlow.Single(x => x.CorrelationId == "INC:CORE_TEMP_LOW:ME-1");
            flow.Steps.Select(s => s.Kind).Should().ContainInOrder("CALL", "PROPOSAL", "DECISION", "RESULT");
            flow.Status.Should().Be("DONE");
            flow.Agents.Should().Contain("QA");
            board.Done.Should().BeGreaterThanOrEqualTo(1);
        }

        [Fact]
        public async Task Save_schedule_toggles_enabled_in_schedules_file_and_refuses_items_without_tools()
        {
            string tmp = Path.Combine(Path.GetTempPath(), "sched-a11-" + Guid.NewGuid().ToString("N") + ".json");
            await File.WriteAllTextAsync(tmp, """{"version":1,"items":[{"id":"proc-plan","agent":"PROC","title":"Rà kế hoạch mua","tools":["ke_hoach_mua"],"enabled":true},{"id":"plan-x","agent":"PLAN","title":"Chưa bật","tools":[],"enabled":false}]}""");
            try
            {
                using var f = await AgentToolsAi3Tests.FactoryAsync(("Agent:SchedulesFile", tmp));
                f.CreateAuthorizedClient().Dispose();
                using var scope = f.Services.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<IAgentFlowService>();
                (await svc.SaveSchedule("proc-plan", false)).ResponseCode.Should().Be(200);
                JsonNode.Parse(await File.ReadAllTextAsync(tmp))!["items"]![0]!["enabled"]!.GetValue<bool>().Should().BeFalse();
                (await svc.SaveSchedule("plan-x", true)).ResponseCode.Should().NotBe(200, "việc chưa có công cụ không bật được");
                (await svc.SaveSchedule("khong-co", true)).ResponseCode.Should().NotBe(200);
            }
            finally
            {
                File.Delete(tmp);
            }
        }
    }
}
