using System.Net.Http.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Approval
{
    /// <summary>
    /// BE2.3 – engine duyệt nhiều cấp theo ApprovalStep: đề nghị mua 2 cấp (D-46: scm-pr-l1 → scm-pr-l2), từ chối ở cấp nào cũng về Nháp, hủy giữa chừng,
    /// nội dung bị sửa giữa 2 cấp, mỗi cấp 1 người, hạn (cờ quá hạn – D-45), cấu hình ApprovalSteps (hợp đồng 2 cấp), PO 2 cấp theo ngưỡng (cấu hình),
    /// điều chỉnh tồn STOCK_ADJUSTMENT, yêu cầu cũ (không có quy trình lưu) vẫn duyệt được. Hôm nay (đồng hồ test) = 10/12/2026 09:00.
    /// </summary>
    public class MultiLevelApprovalTests : IClassFixture<InMemoryApiFactory>
    {
        private static readonly DateTime Now = new(2026, 12, 10, 9, 0, 0);
        private readonly InMemoryApiFactory _factory;

        public MultiLevelApprovalTests(InMemoryApiFactory factory)
        {
            _factory = factory;
            _factory.Clock.FixedNow = Now;
        }

        private static async Task<LegacyResult> PostJson(HttpClient c, string url, object body) => await LegacyResult.ReadAsync(await c.PostAsJsonAsync(url, body));

        /// <summary>User thường thuộc 1 nhóm có quyền Duyệt trên các mục "#/wf-inbox?type=&lt;screen&gt;" + quyền Sửa trên các màn nghiệp vụ "#/&lt;screen&gt;".</summary>
        private static async Task<Guid> UserAsync(InMemoryApiFactory factory, string[] approve, string[]? edit = null)
        {
            Guid userId = Guid.NewGuid(), group = Guid.NewGuid();
            await factory.SeedAsync(async db =>
            {
                db.User.Add(new User { Id = userId, UserName = "ml-" + userId.ToString("N")[..6], IsActive = true });
                db.UserGroup.Add(new UserGroup { Id = group, Name = "ML-" + group.ToString("N")[..6] });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = userId, UserGroupId = group });
                foreach (var link in approve.Select(s => "#/wf-inbox?type=" + s).Concat((edit ?? []).Select(s => "#/" + s)))
                {
                    var menu = await db.ObjectMenu.FirstOrDefaultAsync(m => m.MetaLink == link)
                        ?? db.ObjectMenu.Local.FirstOrDefault(m => m.MetaLink == link);
                    if (menu == null)
                    {
                        menu = new ObjectMenu { Id = Guid.NewGuid(), Name = link, MetaLink = link, IsActive = true, LevelShow = 2 };
                        db.ObjectMenu.Add(menu);
                    }
                    db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = group, ObjectMenuId = menu.Id, AllowAccess = true, AllowAdd = true, AllowEdit = true });
                }
            });
            return userId;
        }

        /// <summary>Đề nghị mua Nháp 1 dòng (Thịt 10 kg) + NCC có bảng giá. Trả (Id đề nghị, Id dòng).</summary>
        private static async Task<(Guid Request, Guid Line)> DraftRequestAsync(InMemoryApiFactory factory, string code)
        {
            Guid kg = Guid.NewGuid(), meat = Guid.NewGuid(), supplier = Guid.NewGuid(), pr = Guid.NewGuid(), line = Guid.NewGuid();
            await factory.SeedAsync(db =>
            {
                db.UnitMeasure.Add(new UnitMeasure { Id = kg, Code = "KG-" + code, Name = "kg" });
                db.Ingredient.Add(new Ingredient { Id = meat, Code = "THIT-" + code, Name = "Thịt " + code, BaseUnitId = kg });
                db.Supplier.Add(new Supplier { Id = supplier, Code = "S-" + code, Name = "NCC " + code, Status = "Active" });
                db.SupplierMaterial.Add(new SupplierMaterial { Id = Guid.NewGuid(), SupplierId = supplier, IngredientId = meat, IsPreferred = true, UnitPrice = 100000, PurchaseUomId = kg, Status = "Active" });
                db.PurchaseRequest.Add(new PurchaseRequest { Id = pr, RequestNumber = "PH-" + code, RequestNo = "PH-" + code, RequestDate = Now.Date, RequiredDate = Now.Date.AddDays(2), Status = "Draft" });
                db.PurchaseRequestLine.Add(new PurchaseRequestLine { Id = line, PurchaseRequestId = pr, IngredientId = meat, Quantity = 10, UnitId = kg, UomId = kg });
                return Task.CompletedTask;
            });
            return (pr, line);
        }

        private static Task<ApprovalRequest> RequestOfAsync(InMemoryApiFactory factory, Guid businessId)
            => factory.QueryAsync(db => db.ApprovalRequest.AsNoTracking().OrderByDescending(a => a.RequestedAt).ThenByDescending(a => a.CreatedDate).FirstAsync(a => a.BusinessId == businessId));

        private static Task<PurchaseRequest> PrAsync(InMemoryApiFactory factory, Guid id)
            => factory.QueryAsync(db => db.PurchaseRequest.AsNoTracking().SingleAsync(p => p.Id == id));

        [Fact]
        public async Task Purchase_request_goes_level1_then_level2_and_only_then_converts()
        {
            var (pr, _) = await DraftRequestAsync(_factory, "ML1");
            using var requester = _factory.CreateAuthorizedClient(await UserAsync(_factory, [], ["scm-pr"]));
            using var head = _factory.CreateAuthorizedClient(await UserAsync(_factory, ["scm-pr-l1"]));
            using var coo = _factory.CreateAuthorizedClient(await UserAsync(_factory, ["scm-pr-l2"]));

            var submitted = (await requester.PostFormAsync("/api/PurchaseRequest/Submit", ("Id", pr), ("Note", "Cần gấp")));
            submitted.Message.Should().Contain("2 cấp");
            var data = submitted.Ok();
            data.GetProperty("purchaseRequest").GetProperty("status").GetString().Should().Be("PendingApproval");
            data.GetProperty("pendingApproval").GetProperty("stepNo").GetInt32().Should().Be(1);
            data.GetProperty("pendingApproval").GetProperty("stepCount").GetInt32().Should().Be(2);
            data.GetProperty("pendingApproval").GetProperty("screenId").GetString().Should().Be("scm-pr-l1");
            (await requester.PostFormAsync("/api/PurchaseRequest/Submit", ("Id", pr))).Fail("Nháp");

            var request = await RequestOfAsync(_factory, pr);
            request.BusinessType.Should().Be("PURCHASE_REQUEST");
            request.RequestStatus.Should().Be("Pending");
            request.CurrentStepNo.Should().Be(1);
            request.DueAt.Should().Be(Now.AddHours(48));
            var steps = await _factory.QueryAsync(db => db.ApprovalStep.AsNoTracking().Where(s => s.ApprovalRequestId == request.Id).OrderBy(s => s.StepNo).ToListAsync());
            steps.Select(s => s.DecisionStatus).Should().Equal("Waiting", "Waiting");
            steps[0].AssignedAt.Should().Be(Now);
            steps[1].AssignedAt.Should().BeNull("cấp 2 chỉ được giao khi cấp 1 duyệt");
            var route = await _factory.QueryAsync(db => db.ApprovalItem.AsNoTracking().SingleAsync(i => i.ApprovalRequestId == request.Id));
            route.ActionCode.Should().Be("APPROVE");
            route.ItemType.Should().Be("PURCHASE_REQUEST");
            route.PayloadJson.Should().Contain("scm-pr-l1").And.Contain("scm-pr-l2");

            // Cấp 2 chưa tới lượt: không thấy, không duyệt được. Cấp 1 thấy trong hộp duyệt scm-pr-l1.
            (await coo.GetResultAsync("/api/ApprovalRequest/GetMyPending?type=scm-pr-l2")).ListData.EnumerateArray().Should().NotContain(x => x.GetProperty("id").GetGuid() == request.Id);
            (await coo.PostFormAsync("/api/PurchaseRequest/Approve", ("Id", pr))).Fail("không có quyền");
            var inbox = (await head.GetResultAsync("/api/ApprovalRequest/GetMyPending?type=scm-pr-l1")).ListData.EnumerateArray()
                .Single(x => x.GetProperty("id").GetGuid() == request.Id);
            inbox.GetProperty("stepNo").GetInt32().Should().Be(1);
            inbox.GetProperty("stepCount").GetInt32().Should().Be(2);
            inbox.GetProperty("title").GetString().Should().Contain("PH-ML1").And.Contain("1,000,000");
            inbox.GetProperty("isOverdue").GetBoolean().Should().BeFalse();

            var level1 = await head.PostFormAsync("/api/PurchaseRequest/Approve", ("Id", pr), ("Note", "Đúng nhu cầu"));
            level1.Ok();
            level1.Message.Should().Contain("cấp 1/2").And.Contain("cấp 2");
            request = await RequestOfAsync(_factory, pr);
            request.RequestStatus.Should().Be("InProgress");
            request.CurrentStepNo.Should().Be(2);
            (await PrAsync(_factory, pr)).Status.Should().Be("PendingApproval", "nghiệp vụ chỉ đổi ở cấp cuối");
            (await requester.PostFormAsync("/api/PurchaseRequest/ConvertToPurchaseOrder", ("Id", pr))).Fail("Đã duyệt");
            (await head.PostFormAsync("/api/PurchaseRequest/Approve", ("Id", pr))).Fail("không có quyền");
            (await head.GetResultAsync("/api/ApprovalRequest/GetMyPending?type=scm-pr-l1")).ListData.EnumerateArray().Should().NotContain(x => x.GetProperty("id").GetGuid() == request.Id);
            (await coo.GetResultAsync("/api/ApprovalRequest/GetMyPending?type=scm-pr-l2")).ListData.EnumerateArray().Should().Contain(x => x.GetProperty("id").GetGuid() == request.Id);

            var detail = (await coo.GetResultAsync($"/api/ApprovalRequest/GetDetail?id={request.Id}")).Ok();
            detail.GetProperty("canDecide").GetBoolean().Should().BeTrue();
            detail.GetProperty("currentStepNo").GetInt32().Should().Be(2);
            detail.GetProperty("statusName").GetString().Should().Be("Đang duyệt");
            var info = detail.GetProperty("listStepInfo").EnumerateArray().ToList();
            info.Select(x => x.GetProperty("screenId").GetString()).Should().Equal("scm-pr-l1", "scm-pr-l2");
            info[0].GetProperty("decisionStatus").GetString().Should().Be("Approved");
            info[1].GetProperty("isCurrent").GetBoolean().Should().BeTrue();
            detail.GetProperty("business").GetProperty("table").GetString().Should().Be("PurchaseRequest");

            (await coo.PostFormAsync("/api/PurchaseRequest/Approve", ("Id", pr), ("Note", "Đồng ý"))).Ok();
            (await PrAsync(_factory, pr)).Status.Should().Be("Approved");
            request = await RequestOfAsync(_factory, pr);
            request.RequestStatus.Should().Be("Approved");
            request.CompletedAt.Should().Be(Now);
            var decisions = await _factory.QueryAsync(db => db.ApprovalDecision.AsNoTracking().Where(d => d.ApprovalRequestId == request.Id).ToListAsync());
            decisions.Should().HaveCount(2).And.OnlyContain(d => d.DecisionCode == "APPROVE" && d.PayloadHash!.Length == 64);
            (await _factory.QueryAsync(db => db.ApprovalItem.AsNoTracking().SingleAsync(i => i.ApprovalRequestId == request.Id))).ItemStatus.Should().Be("Approved");

            var converted = (await requester.PostFormAsync("/api/PurchaseRequest/ConvertToPurchaseOrder", ("Id", pr))).Ok();
            converted.GetProperty("listPurchaseOrder").GetArrayLength().Should().Be(1);
        }

        [Fact]
        public async Task Rejection_at_level2_returns_to_draft_with_reason_and_cancel_midway_skips_remaining_steps()
        {
            var (pr, _) = await DraftRequestAsync(_factory, "ML2");
            Guid requesterId = await UserAsync(_factory, [], ["scm-pr"]);
            using var requester = _factory.CreateAuthorizedClient(requesterId);
            using var head = _factory.CreateAuthorizedClient(await UserAsync(_factory, ["scm-pr-l1"]));
            using var coo = _factory.CreateAuthorizedClient(await UserAsync(_factory, ["scm-pr-l2"]));

            (await requester.PostFormAsync("/api/PurchaseRequest/Submit", ("Id", pr))).Ok();
            (await head.PostFormAsync("/api/PurchaseRequest/Approve", ("Id", pr))).Ok();
            (await coo.PostFormAsync("/api/PurchaseRequest/Reject", ("Id", pr))).Fail("lý do");
            (await coo.PostFormAsync("/api/PurchaseRequest/Reject", ("Id", pr), ("Note", "Tồn kho còn đủ"))).Ok();
            var back = await PrAsync(_factory, pr);
            back.Status.Should().Be("Draft");
            back.Note.Should().Contain("Từ chối: [Cấp 2/2] Tồn kho còn đủ");
            var first = await RequestOfAsync(_factory, pr);
            first.RequestStatus.Should().Be("Rejected");
            (await _factory.QueryAsync(db => db.ApprovalStep.AsNoTracking().Where(s => s.ApprovalRequestId == first.Id).OrderBy(s => s.StepNo).Select(s => s.DecisionStatus).ToListAsync()))
                .Should().Equal("Approved", "Rejected");

            // Gửi lại → yêu cầu mới; cấp 1 duyệt; người gửi hủy giữa chừng → về Nháp, cấp 2 Skipped, ghi thêm CANCEL.
            (await requester.PostFormAsync("/api/PurchaseRequest/Submit", ("Id", pr))).Ok();
            var second = await RequestOfAsync(_factory, pr);
            second.Id.Should().NotBe(first.Id);
            (await head.PostFormAsync("/api/PurchaseRequest/Approve", ("Id", pr))).Ok();
            (await head.PostFormAsync("/api/ApprovalRequest/Cancel", ("Id", second.Id), ("Reason", "x"))).Fail("người gửi");
            (await requester.PostFormAsync("/api/ApprovalRequest/Cancel", ("Id", second.Id), ("Reason", "Đổi số lượng"))).Ok();
            (await PrAsync(_factory, pr)).Status.Should().Be("Draft");
            (await RequestOfAsync(_factory, pr)).RequestStatus.Should().Be("Cancelled");
            (await _factory.QueryAsync(db => db.ApprovalStep.AsNoTracking().Where(s => s.ApprovalRequestId == second.Id).OrderBy(s => s.StepNo).Select(s => s.DecisionStatus).ToListAsync()))
                .Should().Equal("Approved", "Cancelled");
            (await _factory.QueryAsync(db => db.ApprovalDecision.AsNoTracking().Where(d => d.ApprovalRequestId == second.Id).Select(d => d.DecisionCode).ToListAsync()))
                .Should().BeEquivalentTo(["APPROVE", "CANCEL"]);
            (await coo.PostFormAsync("/api/PurchaseRequest/Approve", ("Id", pr))).Fail("không có yêu cầu duyệt");
        }

        [Fact]
        public async Task Same_person_cannot_decide_two_levels_and_payload_changed_between_levels_is_refused()
        {
            var (pr, line) = await DraftRequestAsync(_factory, "ML3");
            using var admin = _factory.CreateAuthorizedClient();
            using var both = _factory.CreateAuthorizedClient(await UserAsync(_factory, ["scm-pr-l1", "scm-pr-l2"]));

            (await admin.PostFormAsync("/api/PurchaseRequest/Submit", ("Id", pr))).Ok();
            (await both.PostFormAsync("/api/PurchaseRequest/Approve", ("Id", pr))).Ok();
            (await both.GetResultAsync("/api/ApprovalRequest/GetMyPending?type=scm-pr-l2")).ListData.EnumerateArray()
                .Should().NotContain(x => x.GetProperty("businessId").GetGuid() == pr, "đã duyệt cấp 1");
            (await both.PostFormAsync("/api/PurchaseRequest/Approve", ("Id", pr))).Fail("cấp 1");
            var request = await RequestOfAsync(_factory, pr);
            (await both.GetResultAsync($"/api/ApprovalRequest/GetDetail?id={request.Id}")).Ok().GetProperty("canDecide").GetBoolean().Should().BeFalse();

            // Sửa số lượng thẳng trong DB giữa 2 cấp → cấp 2 không duyệt được, không ghi gì.
            await _factory.SeedAsync(async db => (await db.PurchaseRequestLine.SingleAsync(l => l.Id == line)).Quantity = 99);
            (await admin.PostFormAsync("/api/PurchaseRequest/Approve", ("Id", pr))).Fail("đã thay đổi");
            (await RequestOfAsync(_factory, pr)).RequestStatus.Should().Be("InProgress");
            (await _factory.QueryAsync(db => db.ApprovalDecision.CountAsync(d => d.ApprovalRequestId == request.Id))).Should().Be(1);
        }

        [Fact]
        public async Task Overdue_step_is_flagged_at_action_time_and_still_decidable_by_default()
        {
            var (pr, _) = await DraftRequestAsync(_factory, "ML4");
            using var admin = _factory.CreateAuthorizedClient();
            (await admin.PostFormAsync("/api/PurchaseRequest/Submit", ("Id", pr))).Ok();
            var request = await RequestOfAsync(_factory, pr);
            try
            {
                _factory.Clock.FixedNow = Now.AddHours(49);
                var item = (await admin.GetResultAsync("/api/ApprovalRequest/GetMyPending?type=scm-pr-l1")).ListData.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == request.Id);
                item.GetProperty("isOverdue").GetBoolean().Should().BeTrue();
                var detail = (await admin.GetResultAsync($"/api/ApprovalRequest/GetDetail?id={request.Id}")).Ok();
                detail.GetProperty("isOverdue").GetBoolean().Should().BeTrue();
                detail.GetProperty("listStepInfo")[0].GetProperty("isOverdue").GetBoolean().Should().BeTrue();
                (await admin.GetResultAsync($"/api/PurchaseRequest/GetDetail?id={pr}")).Ok().GetProperty("pendingApproval").GetProperty("isOverdue").GetBoolean().Should().BeTrue();

                (await admin.PostFormAsync("/api/PurchaseRequest/Approve", ("Id", pr), ("Note", "Duyệt muộn"))).Ok();
                var steps = await _factory.QueryAsync(db => db.ApprovalStep.AsNoTracking().Where(s => s.ApprovalRequestId == request.Id).OrderBy(s => s.StepNo).ToListAsync());
                steps[0].DecisionNote.Should().Be("[QUÁ HẠN] Duyệt muộn");
                steps[1].AssignedAt.Should().Be(Now.AddHours(49));
                steps[1].DueAt.Should().Be(Now.AddHours(97), "hạn cấp 2 tính từ lúc giao");
            }
            finally
            {
                _factory.Clock.FixedNow = Now;
            }
        }

        [Fact]
        public async Task Block_overdue_option_refuses_decision_after_due_time()
        {
            using var factory = new InMemoryApiFactory();
            factory.Settings["BusinessRules:ApprovalBlockOverdueDecision"] = "true";
            factory.Settings["BusinessRules:ApprovalStepDueHoursByType:PURCHASE_REQUEST"] = "4";
            factory.Clock.FixedNow = Now;
            using var admin = factory.CreateAuthorizedClient();
            var (pr, _) = await DraftRequestAsync(factory, "ML5");
            (await admin.PostFormAsync("/api/PurchaseRequest/Submit", ("Id", pr))).Ok();
            (await RequestOfAsync(factory, pr)).DueAt.Should().Be(Now.AddHours(4));
            factory.Clock.FixedNow = Now.AddHours(5);
            (await admin.PostFormAsync("/api/PurchaseRequest/Approve", ("Id", pr))).Fail("quá hạn");
            (await RequestOfAsync(factory, pr)).RequestStatus.Should().Be("Pending");
        }

        [Fact]
        public async Task Configured_steps_make_contract_two_levels_and_po_two_levels_above_director_threshold()
        {
            using var factory = new InMemoryApiFactory();
            factory.Settings["BusinessRules:ApprovalSteps:CONTRACT:0"] = "crm-contract-l1";
            factory.Settings["BusinessRules:ApprovalSteps:CONTRACT:1"] = "crm-contract";
            factory.Settings["BusinessRules:PurchaseOrderDirectorTwoLevel"] = "true";
            factory.Clock.FixedNow = Now;
            using var admin = factory.CreateAuthorizedClient();
            using var sales = factory.CreateAuthorizedClient(await UserAsync(factory, ["crm-contract-l1"]));
            using var ceo = factory.CreateAuthorizedClient(await UserAsync(factory, ["crm-contract", "scm-po-director"]));
            using var buyer = factory.CreateAuthorizedClient(await UserAsync(factory, ["scm-po"]));

            // Hợp đồng: cấu hình ApprovalSteps:CONTRACT = [crm-contract-l1, crm-contract] → 2 cấp, handler cũ không đổi (chỉ chạy ở cấp cuối).
            Guid customer = Guid.NewGuid();
            await factory.SeedAsync(db =>
            {
                db.Customer.Add(new Customer { Id = customer, Code = "KH-ML6", Name = "Khách ML6", CIF = "KH-ML6", CustomerType = "FACTORY", DisplayName = "KH-ML6", Status = "Active" });
                return Task.CompletedTask;
            });
            Guid contract = (await PostJson(admin, "/api/Contract/Create", new { customerId = customer, effectiveFrom = "2026-12-01", listMeal = new[] { new { mealType = "TRUA", unitPrice = 35000m } } }))
                .Ok().GetProperty("contract").GetProperty("id").GetGuid();
            (await admin.PostFormAsync("/api/Contract/Submit", ("Id", contract))).Ok();
            (await ceo.PostFormAsync("/api/Contract/Approve", ("Id", contract))).Fail("không có quyền");
            (await sales.PostFormAsync("/api/Contract/Approve", ("Id", contract), ("Note", "KD ok"))).Ok();
            (await factory.QueryAsync(db => db.Contract.AsNoTracking().SingleAsync(c => c.Id == contract))).Status.Should().Be("PendingApproval");
            (await ceo.PostFormAsync("/api/Contract/Approve", ("Id", contract), ("Note", "GĐ ok"))).Ok();
            (await factory.QueryAsync(db => db.Contract.AsNoTracking().SingleAsync(c => c.Id == contract))).Status.Should().Be("Active");

            // PO ≥ 50 triệu + PurchaseOrderDirectorTwoLevel → scm-po rồi scm-po-director; PO nhỏ vẫn 1 cấp.
            Guid supplier = Guid.NewGuid(), kg = Guid.NewGuid(), meat = Guid.NewGuid(), big = Guid.NewGuid(), small = Guid.NewGuid();
            await factory.SeedAsync(db =>
            {
                db.UnitMeasure.Add(new UnitMeasure { Id = kg, Code = "KG-ML6", Name = "kg" });
                db.Ingredient.Add(new Ingredient { Id = meat, Code = "THIT-ML6", Name = "Thịt ML6", BaseUnitId = kg });
                db.Supplier.Add(new Supplier { Id = supplier, Code = "S-ML6", Name = "NCC ML6", Status = "Active" });
                foreach (var (po, qty) in new[] { (big, 600m), (small, 10m) })
                {
                    db.PurchaseOrder.Add(new PurchaseOrder { Id = po, PurchaseOrderNumber = "PO-ML6-" + qty, SupplierId = supplier, OrderDate = Now.Date, Status = "Draft", TotalAmount = qty * 100000 });
                    db.PurchaseOrderLine.Add(new PurchaseOrderLine { Id = Guid.NewGuid(), PurchaseOrderId = po, IngredientId = meat, Quantity = qty, UnitId = kg, UomId = kg, UnitPrice = 100000, LineAmount = qty * 100000, Amount = qty * 100000 });
                }
                return Task.CompletedTask;
            });
            (await admin.PostFormAsync("/api/PurchaseOrder/Submit", ("Id", big))).Ok();
            var bigDetail = (await admin.GetResultAsync($"/api/PurchaseOrder/GetDetail?id={big}")).Ok();
            bigDetail.GetProperty("pendingApproval").GetProperty("stepCount").GetInt32().Should().Be(2);
            bigDetail.GetProperty("pendingApproval").GetProperty("screenId").GetString().Should().Be("scm-po");
            (await ceo.PostFormAsync("/api/PurchaseOrder/Approve", ("Id", big))).Fail("không có quyền");
            (await buyer.PostFormAsync("/api/PurchaseOrder/Approve", ("Id", big))).Ok();
            (await ceo.GetResultAsync("/api/ApprovalRequest/GetMyPending?type=scm-po-director")).ListData.EnumerateArray().Should().Contain(x => x.GetProperty("businessId").GetGuid() == big);
            (await ceo.PostFormAsync("/api/PurchaseOrder/Approve", ("Id", big))).Ok();
            (await factory.QueryAsync(db => db.PurchaseOrder.AsNoTracking().SingleAsync(p => p.Id == big))).Status.Should().Be("Approved");

            (await admin.PostFormAsync("/api/PurchaseOrder/Submit", ("Id", small))).Ok();
            (await admin.GetResultAsync($"/api/PurchaseOrder/GetDetail?id={small}")).Ok().GetProperty("pendingApproval").GetProperty("stepCount").GetInt32().Should().Be(1);
            (await buyer.PostFormAsync("/api/PurchaseOrder/Approve", ("Id", small))).Ok();
            (await factory.QueryAsync(db => db.PurchaseOrder.AsNoTracking().SingleAsync(p => p.Id == small))).Status.Should().Be("Approved");
        }

        [Fact]
        public async Task Stock_adjustment_is_written_only_after_approval_and_rejection_writes_nothing()
        {
            Guid warehouse = Guid.NewGuid(), kg = Guid.NewGuid(), ingredient = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Warehouse.Add(new Warehouse { Id = warehouse, Code = "K-ML7", Name = "Kho ML7", Status = "Active" });
                db.UnitMeasure.Add(new UnitMeasure { Id = kg, Code = "KG-ML7", Name = "kg" });
                db.Ingredient.Add(new Ingredient { Id = ingredient, Code = "GAO-ML7", Name = "Gạo ML7", BaseUnitId = kg });
                return Task.CompletedTask;
            });
            using var keeper = _factory.CreateAuthorizedClient(await UserAsync(_factory, [], ["scm-inv-txn"]));
            using var manager = _factory.CreateAuthorizedClient(await UserAsync(_factory, ["scm-adjustment"]));

            (await keeper.PostFormAsync("/api/InventoryTransaction/Adjust", ("WarehouseId", warehouse), ("IngredientId", ingredient), ("Quantity", -1m), ("Reason", "Hỏng"))).Fail("Không đủ tồn");
            var pending = await keeper.PostFormAsync("/api/InventoryTransaction/Adjust", ("WarehouseId", warehouse), ("IngredientId", ingredient), ("Quantity", 50m), ("Reason", "Tồn đầu kỳ"));
            pending.Message.Should().Contain("gửi duyệt");
            var data = pending.Ok();
            Guid approval = data.GetProperty("approvalRequestId").GetGuid();
            Guid reference = data.GetProperty("referenceId").GetGuid();
            data.GetProperty("transaction").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);

            var inbox = (await manager.GetResultAsync("/api/ApprovalRequest/GetMyPending?type=scm-adjustment")).ListData.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == approval);
            inbox.GetProperty("title").GetString().Should().Contain("Gạo ML7 +50 kg").And.Contain("Tồn đầu kỳ");
            (await keeper.PostFormAsync("/api/ApprovalRequest/Decide", ("Id", approval), ("Approve", true))).Fail("không có quyền");
            (await manager.PostFormAsync("/api/ApprovalRequest/Decide", ("Id", approval), ("Approve", true), ("Note", "Khớp biên bản"))).Ok();
            var txn = await _factory.QueryAsync(db => db.InventoryTransaction.AsNoTracking().SingleAsync(t => t.ReferenceId == reference));
            txn.TransactionType.Should().Be("ADJUSTMENT");
            txn.ReferenceType.Should().Be("ManualAdjustment");
            txn.QuantityIn.Should().Be(50);
            txn.Note.Should().Contain("Duyệt: Khớp biên bản");
            var detail = (await manager.GetResultAsync($"/api/ApprovalRequest/GetDetail?id={approval}")).Ok();
            detail.GetProperty("business").GetProperty("lines").GetArrayLength().Should().Be(1);
            detail.GetProperty("proposalJson").GetString().Should().Contain("Tồn đầu kỳ");

            var rejected = (await keeper.PostFormAsync("/api/InventoryTransaction/Adjust", ("WarehouseId", warehouse), ("IngredientId", ingredient), ("Quantity", -20m), ("Reason", "Hao hụt"))).Ok();
            (await manager.PostFormAsync("/api/ApprovalRequest/Decide", ("Id", rejected.GetProperty("approvalRequestId").GetGuid()), ("Approve", false), ("Note", "Chưa có biên bản"))).Ok();
            Guid rejectedRef = rejected.GetProperty("referenceId").GetGuid();
            (await _factory.QueryAsync(db => db.InventoryTransaction.CountAsync(t => t.ReferenceId == rejectedRef))).Should().Be(0);
            (await _factory.QueryAsync(db => db.InventoryBalance.Where(b => b.WarehouseId == warehouse && b.IngredientId == ingredient).SumAsync(b => b.OnHandQuantity))).Should().Be(50);
        }

        [Fact]
        public async Task Request_created_before_be23_without_stored_route_is_still_decided_by_handler_screen()
        {
            var (pr, _) = await DraftRequestAsync(_factory, "ML8");
            using var admin = _factory.CreateAuthorizedClient();
            (await admin.PostFormAsync("/api/PurchaseRequest/Submit", ("Id", pr))).Ok();
            var request = await RequestOfAsync(_factory, pr);
            // Giả lập yêu cầu cũ (T12): chỉ 1 bước, không có ApprovalItem lưu quy trình.
            await _factory.SeedAsync(async db =>
            {
                db.ApprovalItem.RemoveRange(await db.ApprovalItem.Where(i => i.ApprovalRequestId == request.Id).ToListAsync());
                db.ApprovalStep.RemoveRange(await db.ApprovalStep.Where(s => s.ApprovalRequestId == request.Id && s.StepNo == 2).ToListAsync());
            });
            using var head = _factory.CreateAuthorizedClient(await UserAsync(_factory, ["scm-pr-l1"]));
            (await head.GetResultAsync("/api/ApprovalRequest/GetMyPending?type=scm-pr-l1")).ListData.EnumerateArray().Should().Contain(x => x.GetProperty("id").GetGuid() == request.Id);
            (await head.PostFormAsync("/api/PurchaseRequest/Approve", ("Id", pr))).Ok();
            (await PrAsync(_factory, pr)).Status.Should().Be("Approved", "1 bước → bước cuối");
        }
    }
}
