using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text;

namespace AppDelicare.Tests.Security
{
    /// <summary>
    /// MENU-5 (D-79): thao tác kiểm soát = quyền Duyệt (5.2), người gửi không tự duyệt (5.3), menu kèm nhóm cha cho người không phải admin (5.5),
    /// Job tự động theo mục #/adm-job. Các ca chặn đại diện theo ROADMAP MENU-5.6 (quyền đặt đúng như ma trận 5.1 của vai trò đó).
    /// </summary>
    public class Menu5PermissionTests(InMemoryApiFactory factory) : IClassFixture<InMemoryApiFactory>
    {
        private static readonly DateTime Now = new(2026, 11, 16, 9, 0, 0);

        /// <summary>Quyền 1 màn: link ('#/…'), Thêm, Sửa (Truy cập luôn bật). Mục "#/wf-inbox?type=…" + Sửa = quyền Duyệt.</summary>
        private sealed record R(string Link, bool Add = false, bool Edit = false);

        private async Task<Guid> UserAsync(string role, params R[] rights)
        {
            Guid userId = Guid.NewGuid(), group = Guid.NewGuid();
            await factory.SeedAsync(async db =>
            {
                db.User.Add(new User { Id = userId, UserName = "m5-" + userId.ToString("N")[..6], IsActive = true });
                db.UserGroup.Add(new UserGroup { Id = group, Name = role + "-" + group.ToString("N")[..6] });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = userId, UserGroupId = group });
                foreach (var r in rights)
                {
                    var menu = await db.ObjectMenu.FirstOrDefaultAsync(m => m.MetaLink == r.Link) ?? db.ObjectMenu.Local.FirstOrDefault(m => m.MetaLink == r.Link);
                    if (menu == null)
                    {
                        menu = new ObjectMenu { Id = Guid.NewGuid(), Name = r.Link, MetaLink = r.Link, IsActive = true, LevelShow = 2 };
                        db.ObjectMenu.Add(menu);
                    }
                    db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = group, ObjectMenuId = menu.Id, AllowAccess = true, AllowAdd = r.Add, AllowEdit = r.Edit });
                }
            });
            return userId;
        }

        private static FormUrlEncodedContent Id(Guid id) => new(new Dictionary<string, string> { ["Id"] = id.ToString(), ["Note"] = "MENU-5 test", ["Reason"] = "MENU-5 test" });

        [Fact]
        public async Task Batch_qc_lot_hold_and_incoming_conclusion_need_the_approve_right_not_edit()
        {
            // OPS-KIT: Sửa Mẻ nấu, Sửa Kiểm tra theo ca, xem Kiểm tra hàng nhập – KHÔNG Đạt mẻ tay, KHÔNG kết luận QC đầu vào.
            using var kit = factory.CreateAuthorizedClient(await UserAsync("OPS-KIT", new R("#/prod-batch", true, true), new R("#/qc-inspection")));
            // OPS-WH: Sửa Lô – KHÔNG thả lô QA giữ.
            using var wh = factory.CreateAuthorizedClient(await UserAsync("OPS-WH", new R("#/scm-lot", true, true)));
            // QA: xem Mẻ nấu / Lô, Sửa Kiểm tra hàng nhập + 3 mục Duyệt.
            using var qa = factory.CreateAuthorizedClient(await UserAsync("QA", new R("#/prod-batch"), new R("#/scm-lot"), new R("#/qc-inspection", true, true),
                new R("#/wf-inbox?type=prod-batch", Edit: true), new R("#/wf-inbox?type=scm-lot", Edit: true), new R("#/wf-inbox?type=qc-inspection", Edit: true)));

            foreach (string action in new[] { "Release", "Hold", "Reject" })
            {
                (await kit.PostAsync("/api/ProductionBatch/" + action, Id(Guid.NewGuid()))).StatusCode.Should().Be(HttpStatusCode.Forbidden, "bếp không tự " + action + " mẻ");
                (await qa.PostAsync("/api/ProductionBatch/" + action, Id(Guid.NewGuid()))).StatusCode.Should().NotBe(HttpStatusCode.Forbidden, "QA có quyền Duyệt Mẻ nấu");
            }
            foreach (string action in new[] { "Hold", "Release" })
            {
                (await wh.PostAsync("/api/MaterialLot/" + action, Id(Guid.NewGuid()))).StatusCode.Should().Be(HttpStatusCode.Forbidden, "kho không tự " + action + " lô");
                (await qa.PostAsync("/api/MaterialLot/" + action, Id(Guid.NewGuid()))).StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
            }
            var conclude = new StringContent($$"""{"id":"{{Guid.NewGuid()}}","listLine":[]}""", Encoding.UTF8, "application/json");
            (await kit.PostAsync("/api/QcInspection/Conclude", conclude)).StatusCode.Should().Be(HttpStatusCode.Forbidden, "bếp không kết luận QC đầu vào");
            conclude = new StringContent($$"""{"id":"{{Guid.NewGuid()}}","listLine":[]}""", Encoding.UTF8, "application/json");
            (await qa.PostAsync("/api/QcInspection/Conclude", conclude)).StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task Compensation_order_needs_add_on_complaint_and_on_order()
        {
            // SAL-CS: Thêm / Sửa Phản ánh, chỉ xem Đơn đặt suất → không tạo đơn bù. SAL-HEAD: Thêm Phản ánh + Thêm Đơn đặt suất → được.
            using var cs = factory.CreateAuthorizedClient(await UserAsync("SAL-CS", new R("#/log-complaint", true, true), new R("#/crm-order")));
            using var head = factory.CreateAuthorizedClient(await UserAsync("SAL-HEAD", new R("#/log-complaint", true, true), new R("#/crm-order", true, true)));
            (await cs.PostAsync("/api/CustomerComplaint/CreateCompensationOrder", Id(Guid.NewGuid()))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await head.PostAsync("/api/CustomerComplaint/CreateCompensationOrder", Id(Guid.NewGuid()))).StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task View_only_roles_cannot_write_screens_owned_by_other_departments()
        {
            // OPS-POS chỉ xem Hợp đồng? – không có quyền: ma trận 5.1 không cho POS vào Hợp đồng; cấp Xem để kiểm đúng "xem được, không sửa được".
            using var pos = factory.CreateAuthorizedClient(await UserAsync("OPS-POS", new R("#/crm-contract"), new R("#/crm-reconciliation", true, true)));
            (await pos.PutAsync("/api/Contract/Update", Id(Guid.NewGuid()))).StatusCode.Should().Be(HttpStatusCode.Forbidden, "điểm phát không sửa hợp đồng");
            (await pos.GetAsync("/api/Contract/GetAll")).StatusCode.Should().Be(HttpStatusCode.OK);
            // SAL-CS xem Theo dõi chuyến, không có Chuyến giao hàng → không sửa chuyến.
            using var cs = factory.CreateAuthorizedClient(await UserAsync("SAL-CS", new R("#/log-execution")));
            (await cs.PutAsync("/api/DeliveryRoute/Update", Id(Guid.NewGuid()))).StatusCode.Should().Be(HttpStatusCode.Forbidden, "CSKH không sửa chuyến");
            // QA xem NCC / Nguyên liệu, không có Đơn mua → không lập PO.
            using var qa = factory.CreateAuthorizedClient(await UserAsync("QA", new R("#/scm-supplier"), new R("#/scm-ingredient")));
            (await qa.PostAsync("/api/PurchaseOrder/Insert", Id(Guid.NewGuid()))).StatusCode.Should().Be(HttpStatusCode.Forbidden, "QA không lập đơn mua");
        }

        [Fact]
        public async Task Requester_never_approves_own_request_at_any_level_but_can_cancel()
        {
            Guid kg = Guid.NewGuid(), meat = Guid.NewGuid(), supplier = Guid.NewGuid(), pr = Guid.NewGuid();
            await factory.SeedAsync(db =>
            {
                db.UnitMeasure.Add(new UnitMeasure { Id = kg, Code = "KG-M5", Name = "kg" });
                db.Ingredient.Add(new Ingredient { Id = meat, Code = "THIT-M5", Name = "Thịt M5", BaseUnitId = kg });
                db.Supplier.Add(new Supplier { Id = supplier, Code = "S-M5", Name = "NCC M5", Status = "Active" });
                db.SupplierMaterial.Add(new SupplierMaterial { Id = Guid.NewGuid(), SupplierId = supplier, IngredientId = meat, IsPreferred = true, UnitPrice = 100000, PurchaseUomId = kg, Status = "Active" });
                db.PurchaseRequest.Add(new PurchaseRequest { Id = pr, RequestNumber = "PH-M5", RequestNo = "PH-M5", RequestDate = Now.Date, RequiredDate = Now.Date.AddDays(2), Status = "Draft" });
                db.PurchaseRequestLine.Add(new PurchaseRequestLine { Id = Guid.NewGuid(), PurchaseRequestId = pr, IngredientId = meat, Quantity = 10, UnitId = kg, UomId = kg });
                return Task.CompletedTask;
            });
            // Người lập đề nghị có cả quyền duyệt cấp 1 và cấp 2 (vd. nhóm vừa lập vừa duyệt) – vẫn không được tự duyệt cấp nào.
            using var requester = factory.CreateAuthorizedClient(await UserAsync("OPS-SCM-HEAD", new R("#/scm-pr", true, true),
                new R("#/wf-inbox?type=scm-pr-l1", Edit: true), new R("#/wf-inbox?type=scm-pr-l2", Edit: true)));
            using var head = factory.CreateAuthorizedClient(await UserAsync("OPS-SCM-HEAD", new R("#/wf-inbox?type=scm-pr-l1", Edit: true)));

            (await requester.PostFormAsync("/api/PurchaseRequest/Submit", ("Id", pr))).Ok();
            Guid request = await factory.QueryAsync(db => db.ApprovalRequest.AsNoTracking().Where(a => a.BusinessId == pr).Select(a => a.Id).SingleAsync());

            (await requester.GetResultAsync("/api/ApprovalRequest/GetMyPending")).ListData.EnumerateArray()
                .Should().NotContain(x => x.GetProperty("id").GetGuid() == request, "người gửi không thấy yêu cầu của mình trong Chờ tôi duyệt");
            (await requester.PostFormAsync("/api/ApprovalRequest/Decide", ("Id", request), ("Approve", true))).Fail("Bạn là người gửi yêu cầu này – cần người khác duyệt");
            var detail = (await requester.GetResultAsync($"/api/ApprovalRequest/GetDetail?id={request}")).Ok();
            detail.GetProperty("canDecide").GetBoolean().Should().BeFalse();
            detail.GetProperty("canCancel").GetBoolean().Should().BeTrue("người gửi vẫn hủy được yêu cầu của mình");

            (await head.GetResultAsync("/api/ApprovalRequest/GetMyPending")).ListData.EnumerateArray()
                .Should().Contain(x => x.GetProperty("id").GetGuid() == request);
            (await head.PostFormAsync("/api/ApprovalRequest/Decide", ("Id", request), ("Approve", true))).Ok();
            // Cấp 2: người gửi có quyền scm-pr-l2 nhưng vẫn bị chặn.
            (await requester.PostFormAsync("/api/ApprovalRequest/Decide", ("Id", request), ("Approve", true))).Fail("người gửi");
        }

        [Fact]
        public async Task Current_menu_includes_parent_group_for_non_admin_and_job_screen_follows_its_menu_item()
        {
            Guid groupMenu = Guid.NewGuid();
            await factory.SeedAsync(async db =>
            {
                db.ObjectMenu.Add(new ObjectMenu { Id = groupMenu, Name = "Nhật ký & Tích hợp (test)", MetaLink = "#/group/m5t", IsActive = true, LevelShow = 1, OrderShow = 1050 });
                if (!await db.ObjectMenu.AnyAsync(m => m.MetaLink == "#/adm-job"))
                {
                    db.ObjectMenu.Add(new ObjectMenu { Id = Guid.NewGuid(), Name = "Job tự động", MetaLink = "#/adm-job", ParentId = groupMenu, IsActive = true, LevelShow = 2, OrderShow = 1051 });
                }
            });
            using var it = factory.CreateAuthorizedClient(await UserAsync("FIN-IT", new R("#/adm-job", true, true)));
            var roots = (await it.GetResultAsync("/api/User/GetCurrent")).Ok().GetProperty("listMenu").EnumerateArray().ToList();
            var parent = roots.Should().ContainSingle(x => x.GetProperty("metaLink").GetString() == "#/group/m5t").Subject;
            parent.GetProperty("children").EnumerateArray().Select(c => c.GetProperty("metaLink").GetString()).Should().Contain("#/adm-job", "mục con nằm dưới nhóm cha dù nhóm không có dòng UserRole");

            (await it.GetAsync("/api/Job/GetAll")).StatusCode.Should().Be(HttpStatusCode.OK, "FIN-IT có quyền mục Job tự động");
            using var kit = factory.CreateAuthorizedClient(await UserAsync("OPS-KIT", new R("#/prod-batch", true, true)));
            (await kit.GetAsync("/api/Job/GetAll")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }
}
