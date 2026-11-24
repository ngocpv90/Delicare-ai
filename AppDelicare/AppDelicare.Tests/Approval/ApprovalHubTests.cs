using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Option;
using AppDelicare.Services.Implement;
using AppDelicare.Services.Implement.Approval;
using AppDelicare.Services.Interface;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Approval
{
    /// <summary>
    /// APR-UI-1 (D-81): khuôn tóm tắt chung (có / không cảnh báo), duyệt nhiều chỉ dòng Bình thường thuộc loại rủi ro thấp (máy chủ kiểm lại),
    /// Tôi đã gửi + Nhắc (chặn &lt; 4 giờ), kế hoạch SX giữ Nháp chờ COO nằm trong hộp và duyệt được, người duyệt nghỉ → leo thang ngay.
    /// </summary>
    public class ApprovalHubTests : IClassFixture<InMemoryApiFactory>
    {
        private static readonly DateTime Today = new(2026, 12, 10);
        private static readonly DateTime Monday = new(2026, 12, 14);
        private readonly InMemoryApiFactory _factory;

        public ApprovalHubTests(InMemoryApiFactory factory)
        {
            _factory = factory;
            _factory.Clock.FixedNow = Today.AddHours(8);
        }

        private Task<Guid> UserAsync(string userName, string[]? approve = null, string? group = null) => UserAsync(_factory, userName, approve, group);

        private static async Task<Guid> UserAsync(InMemoryApiFactory factory, string userName, string[]? approve = null, string? group = null)
        {
            Guid userId = Guid.NewGuid(), groupId = Guid.NewGuid();
            await factory.SeedAsync(async db =>
            {
                db.User.Add(new User { Id = userId, UserName = userName, IsActive = true });
                db.UserGroup.Add(new UserGroup { Id = groupId, Name = group ?? "AH-" + groupId.ToString("N")[..6] });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = userId, UserGroupId = groupId });
                foreach (string link in (approve ?? []).Select(s => "#/wf-inbox?type=" + s))
                {
                    var menu = await db.ObjectMenu.FirstOrDefaultAsync(m => m.MetaLink == link) ?? db.ObjectMenu.Local.FirstOrDefault(m => m.MetaLink == link);
                    if (menu == null)
                    {
                        menu = new ObjectMenu { Id = Guid.NewGuid(), Name = link, MetaLink = link, IsActive = true, LevelShow = 2 };
                        db.ObjectMenu.Add(menu);
                    }
                    db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = groupId, ObjectMenuId = menu.Id, AllowAccess = true, AllowEdit = true });
                }
            });
            return userId;
        }

        /// <summary>Ca + đơn vị + nhân viên (mã = tên đăng nhập – HrRules.ResolveEmployeeAsync).</summary>
        private Task<(Guid Shift, Guid[] Employees)> WorldAsync(string key, params string[] codes) => WorldAsync(_factory, key, codes);

        private static async Task<(Guid Shift, Guid[] Employees)> WorldAsync(InMemoryApiFactory factory, string key, params string[] codes)
        {
            Guid shift = Guid.NewGuid(), ou = Guid.NewGuid();
            var ids = codes.Select(_ => Guid.NewGuid()).ToArray();
            await factory.SeedAsync(db =>
            {
                db.Shift.Add(new Shift { Id = shift, Code = key + "-TRUA", Name = "Trưa " + key, StartTime = new TimeSpan(10, 30, 0), EndTime = new TimeSpan(13, 30, 0), Status = "Active" });
                db.OrganizationUnit.Add(new OrganizationUnit { Id = ou, CompanyId = Guid.NewGuid(), Code = key + "-BEP", Name = "Bếp " + key, UnitType = "BRANCH" });
                for (int i = 0; i < codes.Length; i++)
                {
                    db.Employee.Add(new Employee { Id = ids[i], Code = codes[i], EmployeeCode = codes[i], FullName = $"NV {codes[i]}", OrganizationUnitId = ou, Status = "Active" });
                }
                return Task.CompletedTask;
            });
            return (shift, ids);
        }

        [Fact]
        public async Task Leave_summary_flags_assigned_shifts_and_bulk_only_accepts_normal_rows()
        {
            var w = await WorldAsync("AH1", "AH1-E1", "AH1-E2");
            using var admin = _factory.CreateAuthorizedClient();
            using var e1 = _factory.CreateAuthorizedClient(await UserAsync("AH1-E1"));
            using var e2 = _factory.CreateAuthorizedClient(await UserAsync("AH1-E2"));
            using var hr = _factory.CreateAuthorizedClient(await UserAsync("ah1-hr", ["hr-leave"]));
            (await admin.PostFormAsync("/api/ShiftAssignment/BulkAssign", ("EmployeeIds", w.Employees[1]), ("ShiftId", w.Shift), ("FromDate", Monday), ("ToDate", Monday.AddDays(4)))).Ok();

            Guid normal = (await e1.PostFormAsync("/api/LeaveRequest/Create", ("FromDate", Monday.AddDays(1)), ("ToDate", Monday.AddDays(2)), ("LeaveTypeCode", "ANNUAL"), ("Reason", "Về quê")))
                .Ok().GetProperty("request").GetProperty("id").GetGuid();
            Guid flagged = (await e2.PostFormAsync("/api/LeaveRequest/Create", ("FromDate", Monday.AddDays(1)), ("ToDate", Monday.AddDays(1)), ("LeaveTypeCode", "ANNUAL"), ("Reason", "Việc nhà")))
                .Ok().GetProperty("request").GetProperty("id").GetGuid();
            Guid rNormal = await _factory.QueryAsync(db => db.ApprovalRequest.Where(a => a.BusinessId == normal).Select(a => a.Id).SingleAsync());
            Guid rFlagged = await _factory.QueryAsync(db => db.ApprovalRequest.Where(a => a.BusinessId == flagged).Select(a => a.Id).SingleAsync());

            var list = (await hr.GetResultAsync("/api/ApprovalRequest/GetMyPending")).ListData.EnumerateArray().ToList();
            var a = list.Single(x => x.GetProperty("id").GetGuid() == rNormal);
            var s = a.GetProperty("summary");
            s.GetProperty("typeName").GetString().Should().Be("Đơn xin nghỉ");
            s.GetProperty("moduleName").GetString().Should().Be("Nhân sự");
            s.GetProperty("title").GetString().Should().Contain("NV AH1-E1").And.Contain("2 ngày");
            s.GetProperty("reason").GetString().Should().Be("Về quê");
            s.GetProperty("flags").GetArrayLength().Should().Be(0, "không có ca đã phân → Bình thường");
            a.GetProperty("canBulk").GetBoolean().Should().BeTrue("đơn nghỉ là loại rủi ro thấp (ApprovalBulkTypes) và Bình thường");
            var b = list.Single(x => x.GetProperty("id").GetGuid() == rFlagged);
            b.GetProperty("summary").GetProperty("flags")[0].GetProperty("text").GetString().Should().Contain("ca đã phân");
            b.GetProperty("summary").GetProperty("lines").GetArrayLength().Should().Be(1);
            b.GetProperty("canBulk").GetBoolean().Should().BeFalse("có cảnh báo → phải mở từng yêu cầu");

            var bulk = await hr.PostAsync("/api/ApprovalRequest/DecideBulk", System.Net.Http.Json.JsonContent.Create(new { listId = new[] { rNormal, rFlagged, Guid.NewGuid() } }));
            var results = (await LegacyResult.ReadAsync(bulk)).ListData.EnumerateArray().ToList();
            results.Single(x => x.GetProperty("id").GetGuid() == rNormal).GetProperty("ok").GetBoolean().Should().BeTrue();
            results.Single(x => x.GetProperty("id").GetGuid() == rFlagged).GetProperty("message").GetString().Should().Be(ConstantHelper.Message.ApprovalBulkNotAllowed);
            results.Count(x => !x.GetProperty("ok").GetBoolean()).Should().Be(2, "yêu cầu có cảnh báo và id lạ bị máy chủ từ chối");
            (await _factory.QueryAsync(db => db.LeaveRequest.AsNoTracking().SingleAsync(l => l.Id == normal))).RequestStatus.Should().Be(LeaveRequestStatus.Approved);
            (await _factory.QueryAsync(db => db.LeaveRequest.AsNoTracking().SingleAsync(l => l.Id == flagged))).RequestStatus.Should().Be(LeaveRequestStatus.Pending);

            // Tôi đã duyệt.
            (await hr.GetResultAsync("/api/ApprovalRequest/GetMyDecided?page=1&pageSize=10")).ListData.EnumerateArray()
                .Should().Contain(x => x.GetProperty("id").GetGuid() == rNormal && x.GetProperty("decisionCode").GetString() == "APPROVE");

            // Tôi đã gửi + Nhắc: lần 1 được, lần 2 trong 4 giờ bị chặn, qua 4 giờ được lại.
            var sent = (await e2.GetResultAsync("/api/ApprovalRequest/GetMySent")).ListData.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == rFlagged);
            sent.GetProperty("holderNames").GetString().Should().Contain("ah1-hr");
            sent.GetProperty("canRemind").GetBoolean().Should().BeTrue();
            (await e2.PostFormAsync("/api/ApprovalRequest/Remind", ("Id", rFlagged))).Ok();
            (await e2.PostFormAsync("/api/ApprovalRequest/Remind", ("Id", rFlagged))).Fail("Đã nhắc lúc");
            (await hr.PostFormAsync("/api/ApprovalRequest/Remind", ("Id", rFlagged))).Fail("Chỉ người gửi");
            _factory.Clock.FixedNow = Today.AddHours(12).AddMinutes(1);
            (await e2.PostFormAsync("/api/ApprovalRequest/Remind", ("Id", rFlagged))).Ok();
            _factory.Clock.FixedNow = Today.AddHours(8);
        }

        [Fact]
        public async Task Purchase_order_and_stock_count_summaries_flag_price_jump_manual_po_and_large_count_lines()
        {
            Guid sup = Guid.NewGuid(), other = Guid.NewGuid(), kg = Guid.NewGuid(), tomato = Guid.NewGuid(), oldPo = Guid.NewGuid(), newPo = Guid.NewGuid(), okPo = Guid.NewGuid(), pr = Guid.NewGuid();
            Guid wh = Guid.NewGuid(), sc = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.UnitMeasure.Add(new UnitMeasure { Id = kg, Code = "KG-AH2", Name = "kg" });
                db.Ingredient.Add(new Ingredient { Id = tomato, Code = "CC-AH2", Name = "Cà chua AH2", BaseUnitId = kg });
                db.Supplier.AddRange(new Supplier { Id = sup, Code = "S-AH2", Name = "Rau sạch AH2", Status = "Active" }, new Supplier { Id = other, Code = "S-AH2B", Name = "NCC ưu tiên AH2", Status = "Active" });
                db.SupplierMaterial.Add(new SupplierMaterial { Id = Guid.NewGuid(), SupplierId = other, IngredientId = tomato, IsPreferred = true, UnitPrice = 21000, PurchaseUomId = kg, Status = "Active" });
                db.PurchaseOrder.AddRange(
                    new PurchaseOrder { Id = oldPo, PurchaseOrderNumber = "PO-AH2-OLD", SupplierId = sup, OrderDate = Today.AddDays(-7), Status = PurchaseOrderStatus.Received, TotalAmount = 2_200_000 },
                    new PurchaseOrder { Id = newPo, PurchaseOrderNumber = "PO-AH2-NEW", SupplierId = sup, OrderDate = Today, Status = PurchaseOrderStatus.PendingApproval, TotalAmount = 10_920_000 },
                    new PurchaseOrder { Id = okPo, PurchaseOrderNumber = "PO-AH2-OK", SupplierId = other, PurchaseRequestId = pr, OrderDate = Today, Status = PurchaseOrderStatus.PendingApproval, TotalAmount = 2_100_000 });
                db.PurchaseOrderLine.AddRange(
                    new PurchaseOrderLine { Id = Guid.NewGuid(), PurchaseOrderId = oldPo, IngredientId = tomato, Quantity = 100, UnitPrice = 22000, UnitId = kg },
                    new PurchaseOrderLine { Id = Guid.NewGuid(), PurchaseOrderId = newPo, IngredientId = tomato, Quantity = 420, UnitPrice = 26000, UnitId = kg },
                    new PurchaseOrderLine { Id = Guid.NewGuid(), PurchaseOrderId = okPo, IngredientId = tomato, Quantity = 100, UnitPrice = 21000, UnitId = kg });
                db.Warehouse.Add(new Warehouse { Id = wh, Code = "KHO-AH2", Name = "Kho lạnh AH2" });
                db.StockCount.Add(new StockCount { Id = sc, CountNumber = "KK-AH2", WarehouseId = wh, CountDate = Today, Status = "PendingApproval" });
                db.StockCountLine.AddRange(
                    new StockCountLine { Id = Guid.NewGuid(), StockCountId = sc, IngredientId = tomato, UnitId = kg, SystemQuantity = 75, PhysicalQuantity = 70.8m, VarianceQuantity = -4.2m, VarianceValue = -1_000_000 },
                    new StockCountLine { Id = Guid.NewGuid(), StockCountId = sc, IngredientId = tomato, UnitId = kg, SystemQuantity = 120, PhysicalQuantity = 118.5m, VarianceQuantity = -1.5m });
                return Task.CompletedTask;
            });
            ApprovalSummaryContext Ctx(Data.Connection.DatabaseContext db) => new() { Db = db, Rules = new BusinessRulesOptions(), Now = Today.AddHours(8) };

            var po = (await _factory.QueryAsync(db => ApprovalSummaries.PurchaseOrderAsync(newPo, Ctx(db))))!;
            po.Title.Should().Contain("Rau sạch AH2").And.Contain("10.920.000 đ").And.Contain("1 dòng");
            po.Flags.Select(f => f.Text).Should().Contain(t => t.Contains("lập tay"))
                .And.Contain(t => t.StartsWith("Giá Cà chua AH2 cao hơn lần mua trước 18,2 %"))
                .And.Contain(t => t == "Không phải NCC gợi ý cho Cà chua AH2");
            po.Lines.Single().Highlight.Should().BeTrue();
            po.Lines.Single().Cells[2].Should().Contain("+18,2 %");
            var ok = (await _factory.QueryAsync(db => ApprovalSummaries.PurchaseOrderAsync(okPo, Ctx(db))))!;
            ok.Flags.Should().BeEmpty("NCC ưu tiên, từ đề nghị mua, chưa có lần mua trước để so → Bình thường");

            var count = (await _factory.QueryAsync(db => ApprovalSummaries.StockCountAsync(sc, Ctx(db))))!;
            count.Flags.Should().ContainSingle().Which.Text.Should().Contain("Cà chua AH2 lệch").And.Contain("5,6 %");
            count.Lines.First().Highlight.Should().BeTrue();
            count.Lines.Last().Highlight.Should().BeFalse("lệch 1,3 % ≤ 5 %");
            count.MainValue.Should().Be("−1.000.000 đ");
        }

        [Fact]
        public async Task Held_production_plan_shows_in_coo_inbox_and_is_approved_through_decide()
        {
            Guid plan = Guid.NewGuid(), dish = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Dish.Add(new Dish { Id = dish, Code = "MON-AH3", Name = "Món AH3" });
                db.ProductionPlan.Add(new ProductionPlan
                {
                    Id = plan, PlanNumber = "KH-AH3", ProductionDate = Today.AddDays(1), PlanDate = Today.AddDays(1), Status = ProductionPlanStatus.Draft,
                    Note = "{\"Source\":\"GENERATE\",\"AutoRun\":\"20:02 10/12/2026 Hệ thống KHÔNG tự phát hành – chờ COO duyệt: Thiếu 35 kg ức gà cho ca trưa\"}",
                });
                db.ProductionPlanLine.Add(new ProductionPlanLine { Id = Guid.NewGuid(), ProductionPlanId = plan, DishId = dish, PlannedQuantity = 120, PlannedQty = 120 });
                return Task.CompletedTask;
            });
            using var coo = _factory.CreateAuthorizedClient(await UserAsync("ah3-coo", ["prod-plan"]));
            using var hr = _factory.CreateAuthorizedClient(await UserAsync("ah3-hr", ["hr-leave"]));
            var item = (await coo.GetResultAsync("/api/ApprovalRequest/GetMyPending")).ListData.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == plan);
            item.GetProperty("source").GetString().Should().Be("PLAN");
            item.GetProperty("summary").GetProperty("typeName").GetString().Should().Be("Kế hoạch sản xuất");
            item.GetProperty("summary").GetProperty("flags")[0].GetProperty("text").GetString().Should().Be("Thiếu 35 kg ức gà cho ca trưa");
            (await hr.GetResultAsync("/api/ApprovalRequest/GetMyPending")).ListData.EnumerateArray().Should().NotContain(x => x.GetProperty("id").GetGuid() == plan, "không có quyền duyệt kế hoạch");
            (await coo.GetResultAsync($"/api/ApprovalRequest/GetDetail?id={plan}")).Ok().GetProperty("canDecide").GetBoolean().Should().BeTrue();
            (await coo.PostFormAsync("/api/ApprovalRequest/Decide", ("Id", plan), ("Approve", false), ("Note", "x"))).Fail("không có thao tác từ chối");
            (await hr.PostFormAsync("/api/ApprovalRequest/Decide", ("Id", plan), ("Approve", true))).Fail("không có quyền");
            (await coo.PostFormAsync("/api/ApprovalRequest/Decide", ("Id", plan), ("Approve", true))).Ok();
            (await _factory.QueryAsync(db => db.ProductionPlan.AsNoTracking().SingleAsync(p => p.Id == plan))).Status.Should().Be(ProductionPlanStatus.Approved);
            (await coo.GetResultAsync("/api/ApprovalRequest/GetMyPending")).ListData.EnumerateArray().Should().NotContain(x => x.GetProperty("id").GetGuid() == plan);
        }

        [Fact]
        public async Task Absent_approver_escalates_immediately_to_next_level()
        {
            // Factory riêng: các test khác trong lớp có người duyệt đơn nghỉ không nghỉ → sẽ không phải leo thang.
            using var factory = new InMemoryApiFactory();
            factory.Clock.FixedNow = Today.AddHours(8);
            var w = await WorldAsync(factory, "AH4", "AH4-E1", "AH4-HR");
            using var e1 = factory.CreateAuthorizedClient(await UserAsync(factory, "AH4-E1"));
            await UserAsync(factory, "AH4-HR", ["hr-leave"]);
            Guid cooUser = await UserAsync(factory, "ah4-coo", group: "COO");
            // Người duyệt duy nhất (AH4-HR) có đơn nghỉ Đã duyệt phủ hôm nay.
            await factory.SeedAsync(db =>
            {
                db.LeaveRequest.Add(new LeaveRequest { Id = Guid.NewGuid(), RequestNo = "DN-AH4", EmployeeId = w.Employees[1], FromDate = Today.AddDays(-1), ToDate = Today.AddDays(1), LeaveTypeCode = "ANNUAL", RequestStatus = LeaveRequestStatus.Approved });
                return Task.CompletedTask;
            });
            Guid leave = (await e1.PostFormAsync("/api/LeaveRequest/Create", ("FromDate", Monday), ("ToDate", Monday), ("LeaveTypeCode", "ANNUAL"), ("Reason", "Khám bệnh")))
                .Ok().GetProperty("request").GetProperty("id").GetGuid();
            var request = await factory.QueryAsync(db => db.ApprovalRequest.AsNoTracking().SingleAsync(a => a.BusinessId == leave));
            var mark = await factory.QueryAsync(db => db.ApprovalItem.AsNoTracking().SingleAsync(i => i.ApprovalRequestId == request.Id && i.ActionCode == ApprovalRequestService.AbsentActionCode));
            mark.PayloadJson.Should().Contain("2026-12-10").And.Contain("\"level\":2");
            using var admin = factory.CreateAuthorizedClient();
            (await admin.GetResultAsync($"/api/ApprovalRequest/GetDetail?id={request.Id}")).Ok().GetProperty("summary").GetProperty("flags")[0].GetProperty("text").GetString()
                .Should().Contain("đang nghỉ");
            cooUser.Should().NotBeEmpty();
        }
    }
}
