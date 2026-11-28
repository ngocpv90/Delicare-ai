using System.Text.Json;
using System.Text.Json.Nodes;
using AppDelicare.Data.Interface;
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
    /// AI-9 (D-133) – ghi 2 pha: <c>propose_*</c> chỉ tạo đề xuất (ApprovalRequest AGENT_WRITE), KHÔNG đổi bảng nghiệp vụ; duyệt → chạy method Service thật dưới người duyệt;
    /// duyệt 2 lần ghi 1 lần; quá hạn → từ chối + đóng; người không có quyền mục "Lệnh của agent" không duyệt được; PO ≥ ngưỡng vẫn qua bước Giám đốc; Agent:ReadOnly chặn propose.
    /// </summary>
    public class AgentWriteAi9Tests
    {
        private static readonly Guid Ing = Guid.Parse("a9a9a9a9-0000-4000-8000-000000000001");
        private static readonly Guid Sup = Guid.Parse("a9a9a9a9-0000-4000-8000-000000000002");
        private static readonly Guid Lot = Guid.Parse("a9a9a9a9-0000-4000-8000-000000000003");
        private static readonly Guid Uom = Guid.Parse("a9a9a9a9-0000-4000-8000-000000000004");

        private static async Task SeedBusinessAsync(InMemoryApiFactory f)
        {
            await f.SeedAsync(db =>
            {
                db.UnitMeasure.Add(new UnitMeasure { Id = Uom, Code = "KG-A9", Name = "kg" });
                db.Supplier.Add(new Supplier { Id = Sup, Code = "NCC-A9", Name = "NCC rau A9", Status = "Active" });
                db.Ingredient.Add(new Ingredient { Id = Ing, Code = "NL-A9", Name = "Thịt heo A9", BaseUomId = Uom, PurchaseUomId = Uom });
                db.MaterialLot.Add(new MaterialLot { Id = Lot, LotNumber = "LO-A9-01", LotNo = "LO-A9-01", IngredientId = Ing, SupplierId = Sup, Status = "Available", ReceivedDate = DateTime.Today });
                return Task.CompletedTask;
            });
        }

        private static async Task<(int Lots, int Pos, int Audits)> CountsAsync(InMemoryApiFactory f) => (
            await f.QueryAsync(db => db.MaterialLot.CountAsync(l => l.Status == "OnHold")),
            await f.QueryAsync(db => db.PurchaseOrder.CountAsync()),
            await f.QueryAsync(db => db.AuditChange.CountAsync(a => a.EntityName == "AgentCommand")));

        private static async Task<IResult<bool>> DecideAsync(InMemoryApiFactory f, Guid requestId, Guid user, bool approve = true)
        {
            using var scope = f.Services.CreateScope();
            using var actor = scope.ServiceProvider.GetRequiredService<ICurrentActor>().BeginScope(ActorInfo.ForUser(user));
            return await scope.ServiceProvider.GetRequiredService<IApprovalRequestService>().Decide(new ApprovalDecideRequest { Id = requestId, Approve = approve, Note = "OK" });
        }

        private static Guid RequestIdOf(AgentCallResult r) => Guid.Parse(((JsonNode)r.Data!)["approvalRequestId"]!.GetValue<string>());

        [Fact]
        public async Task Propose_writes_nothing_then_approve_executes_once()
        {
            using var f = await AgentToolsAi3Tests.FactoryAsync();
            await SeedBusinessAsync(f);
            f.CreateAuthorizedClient().Dispose(); // tạo admin test
            var before = await CountsAsync(f);
            var r = await AgentToolsAi3Tests.Call(f, "QA", "propose_hold_lot", new { so_lo = "LO-A9-01", ly_do = "Nhiệt độ nhận 9°C > 5°C" });
            r.Status.Should().Be(200, r.Message);
            ((JsonNode)r.Data!)["requestNo"]!.GetValue<string>().Should().NotBeNullOrEmpty();
            (await CountsAsync(f)).Should().Be(before, "đề xuất không đổi bảng nghiệp vụ");
            (await f.QueryAsync(db => db.MaterialLot.SingleAsync(l => l.Id == Lot))).Status.Should().Be("Available");

            Guid id = RequestIdOf(r);
            var ok = await DecideAsync(f, id, InMemoryApiFactory.UserId);
            ok.ResponseCode.Should().Be(200, ok.ResponseMessage);
            (await f.QueryAsync(db => db.MaterialLot.SingleAsync(l => l.Id == Lot))).Status.Should().Be("OnHold");
            var again = await DecideAsync(f, id, InMemoryApiFactory.UserId);
            again.ResponseCode.Should().NotBe(200, "đã duyệt – không duyệt lần 2");
            (await f.QueryAsync(db => db.AuditChange.CountAsync(a => a.EntityName == "AgentCommand" && a.Action == "Executed"))).Should().Be(1, "ghi đúng 1 lần");
            var req = await f.QueryAsync(db => db.ApprovalRequest.SingleAsync(a => a.Id == id));
            req.RequestStatus.Should().Be("Approved");
            req.BusinessType.Should().Be(AgentCommandService.BusinessType);
        }

        [Fact]
        public async Task Expired_proposal_is_refused_and_closed_and_unprivileged_user_cannot_approve()
        {
            using var f = await AgentToolsAi3Tests.FactoryAsync();
            await SeedBusinessAsync(f);
            f.CreateAuthorizedClient().Dispose();
            Guid plain = Guid.NewGuid();
            await f.SeedAsync(db => { db.User.Add(new User { Id = plain, UserName = "uat.khong-duyet-ai9", PassWord = "!NO-LOGIN!", IsActive = true, AccountType = "INTERNAL" }); return Task.CompletedTask; });

            var r = await AgentToolsAi3Tests.Call(f, "QA", "propose_hold_lot", new { so_lo = "LO-A9-01", ly_do = "Kiểm thử" });
            Guid id = RequestIdOf(r);
            (await DecideAsync(f, id, plain)).ResponseCode.Should().NotBe(200, "không có quyền mục Lệnh của agent");

            f.Clock.FixedNow = DateTime.Now.AddMinutes(30);
            var late = await DecideAsync(f, id, InMemoryApiFactory.UserId);
            late.ResponseCode.Should().NotBe(200);
            late.ResponseMessage.Should().Contain("hết hạn");
            (await f.QueryAsync(db => db.MaterialLot.SingleAsync(l => l.Id == Lot))).Status.Should().Be("Available");
            using (var scope = f.Services.CreateScope())
            {
                using var sys = scope.ServiceProvider.GetRequiredService<ICurrentActor>().BeginScope(ActorInfo.ForSystem(InMemoryApiFactory.UserId)); // như job
                (await scope.ServiceProvider.GetRequiredService<IAgentCommandService>().ExpireOverdueAsync()).Should().Be(1);
            }
            (await f.QueryAsync(db => db.ApprovalRequest.SingleAsync(a => a.Id == id))).RequestStatus.Should().Be("Cancelled");
            (await f.QueryAsync(db => db.ApprovalDecision.CountAsync(d => d.ApprovalRequestId == id && d.DecisionCode == "CANCEL"))).Should().Be(1);
        }

        [Fact]
        public async Task Large_purchase_order_still_needs_director_and_read_only_blocks_propose()
        {
            using var f = await AgentToolsAi3Tests.FactoryAsync(("BusinessRules:PurchaseOrderDirectorApprovalAmount", "1000000"));
            await SeedBusinessAsync(f);
            f.CreateAuthorizedClient().Dispose();
            var r = await AgentToolsAi3Tests.Call(f, "PROC", "propose_purchase_order", new
            {
                ma_ncc = "NCC-A9", ly_do = "Thiếu thịt heo ngày mai 200 kg",
                dong = new[] { new { ma_nl = "NL-A9", so_luong = 200m, don_gia = 120000m } },
            });
            r.Status.Should().Be(200, r.Message);
            (await f.QueryAsync(db => db.PurchaseOrder.CountAsync())).Should().Be(0, "chưa ghi gì trước khi duyệt");
            var ok = await DecideAsync(f, RequestIdOf(r), InMemoryApiFactory.UserId);
            ok.ResponseCode.Should().Be(200, ok.ResponseMessage);
            var po = await f.QueryAsync(db => db.PurchaseOrder.SingleAsync());
            po.TotalAmount.Should().Be(24_000_000m);
            using (var scope = f.Services.CreateScope())
            {
                var progress = await scope.ServiceProvider.GetRequiredService<IApprovalRequestService>().OpenProgressAsync("PURCHASE_ORDER", po.Id);
                progress.Should().NotBeNull("đơn mua đã gửi duyệt như thao tác web");
                var steps = await f.QueryAsync(db => db.ApprovalItem.Where(i => i.ApprovalRequestId == progress!.Id).Select(i => i.PayloadJson).ToListAsync());
                string.Join(" ", steps).Should().Contain("scm-po-director", "đơn ≥ ngưỡng vẫn qua bước Giám đốc");
            }

            using var ro = await AgentToolsAi3Tests.FactoryAsync(("Agent:ReadOnly", "true"));
            var blocked = await AgentToolsAi3Tests.Call(ro, "QA", "propose_hold_lot", new { so_lo = "LO-A9-01", ly_do = "x" });
            blocked.Status.Should().Be(403);
            blocked.Message.Should().Contain("chỉ đọc");
        }
    }
}
