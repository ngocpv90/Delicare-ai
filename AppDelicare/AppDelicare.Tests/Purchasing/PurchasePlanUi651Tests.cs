using System.Text.Json;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Implement;
using AppDelicare.Services.Implement.PurchasePlan;
using AppDelicare.Services.Interface;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Purchasing
{
    /// <summary>
    /// UI-6.51 (D-137) – kế hoạch mua tự động. Hôm nay (đồng hồ test) = thứ Hai 01/03/2027 10:00. Món 100 suất / ngày, công thức 10 kg / 100 suất.
    /// NCC giao 1 ngày + đệm 1 ngày → ngày cần 03/03 phải đặt hôm nay; 04/03 chưa tới hạn.
    /// (a) job lập đề nghị đúng ngày đến hạn, chạy 2 lần không trùng; Calculate trừ đề nghị đang mở;
    /// (b) thay thế theo giá dùng được: rẻ hơn 8,5% → 50%; 3% → giữ chuẩn; khách chỉ dùng chuẩn không bị thay; NCC thay thế không đạt → giữ chuẩn; sản xuất áp đúng tỷ lệ đã mua;
    /// (c) chia NCC 60/40 bù lệch lũy kế trong ±15; dưới mức tối thiểu dồn NCC khác; NCC bị loại chia lại; tổng tỷ lệ ≠ 100 → lỗi; lập 2 PO;
    /// (d) duyệt theo rủi ro: đúng định mức tự duyệt + tự PO; vượt → 1 cấp; ≥ ngưỡng GĐ → 2 cấp; đề nghị lập tay vẫn 2 cấp;
    /// (e) gửi NCC: không kênh → chờ gửi tay; kênh LOG → đã gửi.
    /// </summary>
    public class PurchasePlanUi651Tests
    {
        private static readonly DateTime Today = new(2027, 3, 1);
        private static readonly DateTime Need = Today.AddDays(2);

        private sealed class World
        {
            public Guid Kg = Guid.NewGuid(), Dish = Guid.NewGuid(), Menu = Guid.NewGuid(), CustomerA = Guid.NewGuid(), CustomerB = Guid.NewGuid();
            public Guid Vai = Guid.NewGuid(), Mong = Guid.NewGuid(), SupVai = Guid.NewGuid(), SupMong = Guid.NewGuid(), SupVai2 = Guid.NewGuid();
        }

        private static async Task<(InMemoryApiFactory F, World W)> SetupAsync(decimal vaiPrice = 128_000m, decimal? mongPrice = 112_000m, int mongLead = 1, bool substitute = true,
            decimal customerBMeals = 0m, bool customerBStandardOnly = false, params (string, string?)[] settings)
        {
            var f = new InMemoryApiFactory();
            foreach (var (k, v) in settings)
            {
                f.Settings[k] = v;
            }
            f.Clock.FixedNow = Today.AddHours(10);
            f.CreateAuthorizedClient().Dispose(); // tạo admin test
            var w = new World();
            static int DayOf(DateTime d) => d.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)d.DayOfWeek;
            await f.SeedAsync(db =>
            {
                db.UnitMeasure.Add(new UnitMeasure { Id = w.Kg, Code = "KG", Name = "kg" });
                db.Ingredient.Add(new Ingredient { Id = w.Vai, Code = "NL-VAI", Name = "Thịt lợn vai", BaseUnitId = w.Kg, PurchaseUomId = w.Kg });
                db.Ingredient.Add(new Ingredient { Id = w.Mong, Code = "NL-MONG", Name = "Thịt lợn mông", BaseUnitId = w.Kg, PurchaseUomId = w.Kg });
                db.Dish.Add(new Dish { Id = w.Dish, Code = "M-KT", Name = "Thịt kho tàu" });
                var lines = new List<object> { new { IngredientId = w.Vai, Quantity = 10m, UnitId = w.Kg, WastePercent = 8m, SequenceNo = 1, SubstituteGroup = substitute ? "THIT" : null } };
                if (substitute)
                {
                    lines.Add(new { IngredientId = w.Mong, Quantity = 0m, UnitId = w.Kg, WastePercent = 12m, SequenceNo = 2, SubstituteGroup = "THIT", MaxSharePct = 50m });
                }
                db.Recipe.Add(new Recipe { Id = Guid.NewGuid(), DishId = w.Dish, VersionNo = 1, Status = "Locked", IsActive = true, StandardOutputQty = 100, EffectiveFrom = new DateTime(2027, 1, 1),
                    LockedAt = new DateTime(2027, 1, 1), SnapshotJson = JsonSerializer.Serialize(lines) });
                db.Menu.Add(new Model.Entity.Menu { Id = w.Menu, Code = "TD-651", Name = "Thực đơn 651", VersionNo = 1, Status = "Published", MenuType = "COMMON", EffectiveFrom = new DateTime(2027, 1, 1) });
                for (int i = 1; i <= 7; i++)
                {
                    db.MenuLine.Add(new MenuLine { Id = Guid.NewGuid(), MenuId = w.Menu, DishId = w.Dish, MealType = "TRUA", DayOfWeek = i, SequenceNo = 1 });
                }
                foreach (var (cust, meals) in new[] { (w.CustomerA, 100m), (w.CustomerB, customerBMeals) }.Where(x => x.Item2 > 0))
                {
                    db.Customer.Add(new Customer { Id = cust, Code = "K" + cust.ToString("N")[..4], Name = "Khách " + cust.ToString("N")[..4], CIF = cust.ToString("N")[..8], DisplayName = "Khách", CustomerType = "FACTORY", Status = "Active" });
                    Guid order = Guid.NewGuid();
                    db.Order.Add(new Order { Id = order, OrderNumber = "DH-" + order.ToString("N")[..6], CustomerId = cust, OrderDate = Today, MealDate = Need, DeliveryDate = Need, Status = "Locked", TotalQuantity = meals });
                    db.OrderLine.Add(new OrderLine { Id = Guid.NewGuid(), OrderId = order, MenuId = w.Menu, MealType = "TRUA", Quantity = meals });
                }
                if (customerBStandardOnly)
                {
                    db.Contract.Add(new Contract { Id = Guid.NewGuid(), CustomerId = w.CustomerB, ContractNumber = "HD-STD", EffectiveFrom = new DateTime(2027, 1, 1), Status = "Active", StandardIngredientOnly = true });
                }
                db.Supplier.Add(new Supplier { Id = w.SupVai, Code = "NCC-HN", Name = "Thịt sạch Hà Nội", Status = "Active" });
                db.Supplier.Add(new Supplier { Id = w.SupMong, Code = "NCC-HY", Name = "Thịt sạch Hưng Yên", Status = "Active" });
                db.SupplierMaterial.Add(new SupplierMaterial { Id = Guid.NewGuid(), SupplierId = w.SupVai, IngredientId = w.Vai, IsPreferred = true, UnitPrice = vaiPrice, PurchaseUomId = w.Kg, LeadTimeDays = 1, EffectiveFrom = new DateTime(2027, 1, 1), Status = "Active" });
                if (mongPrice != null)
                {
                    db.SupplierMaterial.Add(new SupplierMaterial { Id = Guid.NewGuid(), SupplierId = w.SupMong, IngredientId = w.Mong, IsPreferred = true, UnitPrice = mongPrice, PurchaseUomId = w.Kg, LeadTimeDays = mongLead, EffectiveFrom = new DateTime(2027, 1, 1), Status = "Active" });
                }
                return Task.CompletedTask;
            });
            return (f, w);
        }

        private static async Task<T> AsSystemAsync<T>(InMemoryApiFactory f, Func<IServiceProvider, Task<T>> run)
        {
            using var scope = f.Services.CreateScope();
            using var actor = scope.ServiceProvider.GetRequiredService<ICurrentActor>().BeginScope(ActorInfo.ForSystem(InMemoryApiFactory.UserId));
            return await run(scope.ServiceProvider);
        }

        private static Task<PurchasePlanRunResultModel> RunJobAsync(InMemoryApiFactory f) => AsSystemAsync(f, sp => sp.GetRequiredService<IPurchasePlanService>().RunJobAsync(true));

        private static Task<MaterialHorizonModel> HorizonAsync(InMemoryApiFactory f)
            => AsSystemAsync(f, async sp => (await sp.GetRequiredService<IMaterialRequirementService>().PlanHorizonAsync(new MaterialHorizonRequest())).Model!);

        // ---------------------------------------------------------------- (a)

        [Fact]
        public async Task Job_creates_request_on_due_date_auto_approves_and_second_run_creates_no_duplicate()
        {
            var (f, w) = await SetupAsync(substitute: false);
            using var _ = f;
            var r1 = await RunJobAsync(f);
            r1.Created.Should().Be(1);
            r1.AutoApproved.Should().Be(1, "đúng định mức: số = nhu cầu tính, NCC ưu tiên, giá không tăng, tổng < ngưỡng");
            var pr = await f.QueryAsync(db => db.PurchaseRequest.SingleAsync());
            pr.Note.Should().StartWith(PurchasePlanRules.Marker).And.Contain(PurchasePlanRules.AutoApprovedNote);
            pr.RequiredDate.Should().Be(Need);
            pr.Status.Should().Be(PurchaseRequestStatus.Converted);
            var line = await f.QueryAsync(db => db.PurchaseRequestLine.SingleAsync(l => !l.IsDeleted));
            line.Quantity.Should().Be(Math.Ceiling(10m / 0.92m * 10000m) / 10000m, "10 kg tinh ÷ (1 − 8% sơ chế)");
            var po = await f.QueryAsync(db => db.PurchaseOrder.SingleAsync());
            po.Status.Should().Be(PurchaseOrderStatus.Approved);
            po.Note.Should().Contain(PurchasePlanRules.AutoApprovedNote);

            var r2 = await RunJobAsync(f);
            (r2.Created + r2.Updated).Should().Be(0, "PO sắp về đã trừ – chạy lại không lập trùng");
            (await f.QueryAsync(db => db.PurchaseRequest.CountAsync())).Should().Be(1);
            (await f.QueryAsync(db => db.PurchaseOrder.CountAsync())).Should().Be(1);
        }

        [Fact]
        public async Task Draft_is_updated_in_place_not_duplicated_and_calculate_subtracts_open_requests()
        {
            var (f, w) = await SetupAsync(substitute: false, settings: ("PurchasePlan:AutoSubmit", "false"));
            using var _ = f;
            (await RunJobAsync(f)).Created.Should().Be(1);
            (await RunJobAsync(f)).Created.Should().Be(0);
            (await f.QueryAsync(db => db.PurchaseRequest.CountAsync(p => p.Status == PurchaseRequestStatus.Draft))).Should().Be(1, "đề nghị Nháp được cập nhật, không lập thêm");
            (await f.QueryAsync(db => db.PurchaseRequestLine.CountAsync(l => !l.IsDeleted))).Should().Be(1);

            // MRP chính thức ngày 03/03: dòng đề nghị đang mở của ngày cần được trừ → không lập đề nghị trùng.
            var calc = await AsSystemAsync(f, sp => sp.GetRequiredService<IMaterialRequirementService>().Calculate(new MaterialRequirementCalculateRequest { ProductionDate = Need }));
            calc.ResponseCode.Should().Be(200, calc.ResponseMessage);
            (await f.QueryAsync(db => db.PurchaseRequest.CountAsync())).Should().Be(1, "Calculate trừ đề nghị Nháp của kế hoạch mua");
            (await f.QueryAsync(db => db.MaterialRequirement.SingleAsync(r => r.IngredientId == w.Vai))).PurchaseQuantity.Should().Be(0);
        }

        // ---------------------------------------------------------------- (b)

        [Theory]
        [InlineData(112_000, 50, 8.5)]  // 112.000 ÷ 0,88 = 127.273 vs 128.000 ÷ 0,92 = 139.130 → rẻ hơn 8,5% → dùng tới 50%
        [InlineData(118_800, 0, 3.0)]   // 118.800 ÷ 0,88 = 135.000 → rẻ hơn 3% (< 5%) → giữ chuẩn
        public async Task Substitute_by_usable_price(int mongPrice, int expectedShare, double saving)
        {
            var (f, w) = await SetupAsync(mongPrice: mongPrice);
            using var _ = f;
            var hz = await HorizonAsync(f);
            var s = hz.ListSubstitution.Single(x => x.Date == Need);
            s.SavingPct.Should().BeApproximately((decimal)saving, 0.1m);
            s.SharePct.Should().Be(expectedShare);
            var mong = hz.ListItem.SingleOrDefault(i => i.IngredientId == w.Mong)?.ListDay.Single(c => c.Date == Need).Required ?? 0m;
            var vai = hz.ListItem.Single(i => i.IngredientId == w.Vai).ListDay.Single(c => c.Date == Need).Required;
            if (expectedShare == 50)
            {
                vai.Should().BeApproximately(5m / 0.92m, 0.001m);
                mong.Should().BeApproximately(5m / 0.88m, 0.001m, "phần dùng được 5 kg ÷ (1 − 12% hao của thịt mông)");
            }
            else
            {
                vai.Should().BeApproximately(10m / 0.92m, 0.001m);
                mong.Should().Be(0);
            }
        }

        [Fact]
        public async Task Standard_only_customer_is_never_substituted_and_production_uses_purchased_ratio()
        {
            var (f, w) = await SetupAsync(customerBMeals: 100m, customerBStandardOnly: true);
            using var _ = f;
            var hz = await HorizonAsync(f);
            var s = hz.ListSubstitution.Single(x => x.Date == Need);
            var dish = s.ListDish.Single();
            (dish.Portions, dish.StdOnlyPortions, dish.EffectivePct).Should().Be((200m, 100m, 25m), "chỉ 100 suất của khách thường được thay 50%");
            dish.SubGross.Should().BeApproximately(20m * 0.25m / 0.88m, 0.001m, "lượng mua thịt mông = phần tinh 5 kg ÷ (1 − 12%)");

            // Job lập đề nghị → lưu quyết định của ngày → sản xuất (chọn công thức) áp đúng tỷ lệ đã mua.
            await RunJobAsync(f);
            var lines = await f.QueryAsync(db => ProductionRules.SelectRecipeAsync(db, w.Dish, Need));
            lines.Lines.Single(l => l.IngredientId == w.Vai).Quantity.Should().BeApproximately(7.5m, 0.0001m);
            lines.Lines.Single(l => l.IngredientId == w.Mong && l.Substituted).Quantity.Should().BeApproximately(2.5m, 0.0001m);
            (await f.QueryAsync(db => db.PurchaseRequestLine.CountAsync(l => l.IngredientId == w.Mong && !l.IsDeleted))).Should().Be(1);
        }

        [Fact]
        public async Task Substitute_supplier_not_eligible_keeps_standard()
        {
            var (f, w) = await SetupAsync(mongLead: 5); // NCC thịt mông giao 5 ngày – không kịp ngày cần 03/03
            using var _ = f;
            var s = (await HorizonAsync(f)).ListSubstitution.Single(x => x.Date == Need);
            s.SharePct.Should().Be(0);
            s.Reason.Should().Contain("không đạt");
            var lines = await f.QueryAsync(db => ProductionRules.SelectRecipeAsync(db, w.Dish, Need));
            lines.Lines.Should().ContainSingle().Which.IngredientId.Should().Be(w.Vai, "chưa có quyết định đã lưu / giữ chuẩn – dòng khai thay thế không vào sản xuất");
        }

        // ---------------------------------------------------------------- (c)

        private static SupplierSplit.Input In(string name, decimal target, decimal cum, decimal? min = null, bool eligible = true)
            => new(Guid.NewGuid(), name, Guid.NewGuid(), 100m, 1, false, target, cum, min, eligible, eligible ? null : "Không giao kịp");

        [Fact]
        public void Split_corrects_cumulative_drift_within_limit_and_handles_minimum_and_excluded()
        {
            var parts = SupplierSplit.Split(100m, [In("Hà Nội", 60, 68), In("Đông Anh", 40, 32)], 15m);
            parts.Select(p => (p.SupplierName, p.Pct, p.Quantity)).Should().Equal(("Hà Nội", 52m, 52m), ("Đông Anh", 48m, 48m));
            parts[0].CumPct.Should().Be(68m);

            var clamped = SupplierSplit.Split(100m, [In("Hà Nội", 60, 100), In("Đông Anh", 40, 0)], 15m);
            clamped.Select(p => p.Pct).Should().Equal(45m, 55m); // 60 − 40 → chặn 45; 40 + 40 → chặn 55

            var under = SupplierSplit.Split(50m, [In("Hà Nội", 60, 0), In("Đông Anh", 40, 0, min: 25m)], 15m);
            under.Should().ContainSingle().Which.Quantity.Should().Be(50m, "phần 20 < mức tối thiểu 25 → dồn NCC còn lại");
            under[0].Note.Should().Contain("dưới mức tối thiểu");

            var excluded = SupplierSplit.Split(90m, [In("Hà Nội", 50, 0), In("Đông Anh", 30, 0, eligible: false), In("Văn Nội", 20, 0)], 15m);
            excluded.Select(p => p.SupplierName).Should().Equal("Hà Nội", "Văn Nội");
            excluded[0].Quantity.Should().BeApproximately(90m * 50m / 70m, 0.01m, "phần 30% của NCC bị loại chia lại theo tỷ lệ 50 : 20");
            excluded.Sum(p => p.Quantity).Should().Be(90m);
            excluded[0].Note.Should().Contain("bị loại");
        }

        [Fact]
        public async Task Allocation_must_total_100_and_split_creates_one_purchase_order_per_supplier()
        {
            var (f, w) = await SetupAsync(substitute: false);
            using var _ = f;
            Guid p1 = await f.QueryAsync(db => db.SupplierMaterial.Where(m => m.IngredientId == w.Vai).Select(m => m.Id).SingleAsync()), p2 = Guid.NewGuid();
            await f.SeedAsync(db =>
            {
                db.Supplier.Add(new Supplier { Id = w.SupVai2, Code = "NCC-DA", Name = "HTX Đông Anh", Status = "Active" });
                db.SupplierMaterial.Add(new SupplierMaterial { Id = p2, SupplierId = w.SupVai2, IngredientId = w.Vai, UnitPrice = 126_000m, PurchaseUomId = w.Kg, LeadTimeDays = 1, EffectiveFrom = new DateTime(2027, 1, 1), Status = "Active" });
                return Task.CompletedTask;
            });
            var bad = await AsSystemAsync(f, sp => sp.GetRequiredService<ISupplierMaterialService>().SaveAllocation(new SupplierMaterialAllocationRequest { IngredientId = w.Vai, Pct = new() { [p1] = 60m, [p2] = 30m } }));
            bad.ResponseCode.Should().NotBe(200);
            bad.ResponseMessage.Should().Contain("100%");
            var ok = await AsSystemAsync(f, sp => sp.GetRequiredService<ISupplierMaterialService>().SaveAllocation(new SupplierMaterialAllocationRequest { IngredientId = w.Vai, Pct = new() { [p1] = 60m, [p2] = 40m } }));
            ok.ResponseCode.Should().Be(200, ok.ResponseMessage);

            await RunJobAsync(f);
            var pos = await f.QueryAsync(db => db.PurchaseOrder.ToListAsync());
            pos.Select(p => p.SupplierId).Should().BeEquivalentTo([w.SupVai, w.SupVai2], "mỗi NCC một đơn mua");
            var qty = await f.QueryAsync(db => db.PurchaseOrderLine.ToListAsync());
            qty.Sum(l => l.Quantity).Should().BeApproximately(10m / 0.92m, 0.001m);
            qty.Single(l => l.PurchaseOrderId == pos.Single(p => p.SupplierId == w.SupVai).Id).Quantity.Should().BeApproximately(10m / 0.92m * 0.6m, 0.001m);
        }

        // ---------------------------------------------------------------- (d)

        [Fact]
        public async Task Price_increase_goes_to_one_level_and_above_director_threshold_adds_coo_manual_request_keeps_two_levels()
        {
            var (f, w) = await SetupAsync(substitute: false);
            using var _ = f;
            // Giá thịt vai tăng 20% so giá liền trước → vượt định mức (ApprovalFlagPricePct 10).
            await f.SeedAsync(db =>
            {
                db.SupplierMaterial.Add(new SupplierMaterial { Id = Guid.NewGuid(), SupplierId = w.SupVai, IngredientId = w.Vai, IsPreferred = true, UnitPrice = 153_600m, PurchaseUomId = w.Kg, LeadTimeDays = 1, EffectiveFrom = new DateTime(2027, 2, 1), Status = "Active" });
                return Task.CompletedTask;
            });
            var r = await RunJobAsync(f);
            r.Submitted.Should().Be(1);
            r.AutoApproved.Should().Be(0);
            var pr = await f.QueryAsync(db => db.PurchaseRequest.SingleAsync());
            pr.Status.Should().Be(PurchaseRequestStatus.PendingApproval);
            pr.Note.Should().Contain("giá NCC tăng");
            (await StepsAsync(f, pr.Id)).Should().Equal(PurchaseRequestStatus.Level1ScreenId);

            var (g, w2) = await SetupAsync(substitute: false, settings: ("BusinessRules:PurchaseOrderDirectorApprovalAmount", "1000000"));
            using var __ = g;
            await RunJobAsync(g); // 10,87 kg × 128.000 ≈ 1,39 triệu ≥ ngưỡng GĐ 1 triệu → vượt ngưỡng tự duyệt + thêm COO
            var pr2 = await g.QueryAsync(db => db.PurchaseRequest.SingleAsync());
            pr2.Status.Should().Be(PurchaseRequestStatus.PendingApproval);
            (await StepsAsync(g, pr2.Id)).Should().Equal(PurchaseRequestStatus.Level1ScreenId, PurchaseRequestStatus.Level2ScreenId);

            // Đề nghị lập tay giữ 2 cấp.
            Guid manual = Guid.NewGuid();
            await g.SeedAsync(db =>
            {
                db.PurchaseRequest.Add(new PurchaseRequest { Id = manual, RequestNumber = "PH-TAY", RequestDate = Today, RequiredDate = Need, Status = PurchaseRequestStatus.Draft, Note = "Lập tay" });
                db.PurchaseRequestLine.Add(new PurchaseRequestLine { Id = Guid.NewGuid(), PurchaseRequestId = manual, IngredientId = w2.Vai, Quantity = 1m, UnitId = w2.Kg, UomId = w2.Kg, RequiredDate = Need });
                return Task.CompletedTask;
            });
            (await AsSystemAsync(g, sp => sp.GetRequiredService<IPurchaseRequestService>().ApprovalStepScreenIdsAsync(manual))).Should().Equal(PurchaseRequestStatus.Level1ScreenId, PurchaseRequestStatus.Level2ScreenId);
        }

        private static async Task<List<string>> StepsAsync(InMemoryApiFactory f, Guid prId)
        {
            var req = await f.QueryAsync(db => db.ApprovalRequest.SingleAsync(a => a.BusinessId == prId && a.BusinessType == PurchaseRequestStatus.BusinessType));
            int steps = await f.QueryAsync(db => db.ApprovalStep.CountAsync(s => s.ApprovalRequestId == req.Id));
            string payload = string.Join(" ", await f.QueryAsync(db => db.ApprovalItem.Where(i => i.ApprovalRequestId == req.Id).Select(i => i.PayloadJson).ToListAsync()));
            var screens = new[] { PurchaseRequestStatus.Level1ScreenId, PurchaseRequestStatus.Level2ScreenId }.Where(s => payload.Contains("\"" + s + "\"")).ToList();
            screens.Should().HaveCount(steps, payload);
            return screens;
        }

        // ---------------------------------------------------------------- (e)

        [Fact]
        public async Task Send_without_channel_waits_for_manual_send_and_log_channel_sends()
        {
            var (f, w) = await SetupAsync(substitute: false);
            using var _ = f;
            await RunJobAsync(f);
            var first = await AsSystemAsync(f, sp => sp.GetRequiredService<IPurchasePlanService>().SendDueAsync());
            (first.Sent, first.Failed).Should().Be((0, 1));
            (await f.QueryAsync(db => db.PurchaseOrder.SingleAsync())).Status.Should().Be(PurchaseOrderStatus.Approved, "không kênh → giữ Chờ gửi NCC");

            await f.SeedAsync(async db =>
            {
                db.IntegrationChannel.Add(new IntegrationChannel { Id = Guid.NewGuid(), ChannelCode = SupplierOrderSendService.EmailChannel, ChannelName = "Email NCC", ProviderCode = "LOG", ChannelStatus = "Active" });
                var s = await db.Supplier.SingleAsync(x => x.Id == w.SupVai);
                s.Email = "ncc-test@example.invalid";
            });
            var second = await AsSystemAsync(f, sp => sp.GetRequiredService<IPurchasePlanService>().SendDueAsync());
            second.Sent.Should().Be(1);
            (await f.QueryAsync(db => db.PurchaseOrder.SingleAsync())).Status.Should().Be(PurchaseOrderStatus.Sent);
            (await f.QueryAsync(db => db.IntegrationLog.CountAsync(l => l.OperationCode == "PO_SEND_SUPPLIER" && l.LogStatus == "Succeeded"))).Should().Be(1);
        }

        // ---------------------------------------------------------------- màn Kế hoạch mua (API)

        [Fact]
        public async Task Board_cell_detail_and_override_through_api()
        {
            var (f, w) = await SetupAsync();
            using var _ = f;
            using var c = f.CreateAuthorizedClient();
            var board = (await c.GetResultAsync("/api/PurchasePlan/GetBoard")).Ok();
            board.GetProperty("listDay").GetArrayLength().Should().Be(7);
            board.GetProperty("dueCount").GetInt32().Should().Be(2, "thịt vai + thịt mông cần 03/03 phải đặt hôm nay");
            var rows = board.GetProperty("listRow").EnumerateArray().ToList();
            rows.Select(r => r.GetProperty("ingredientId").GetGuid()).Should().Equal(w.Vai, w.Mong);
            rows[0].GetProperty("hasSubstitute").GetBoolean().Should().BeTrue();
            rows[1].GetProperty("isSubstitute").GetBoolean().Should().BeTrue();
            rows[0].GetProperty("listCell").EnumerateArray().Single(x => x.GetProperty("date").GetDateTime() == Need).GetProperty("status").GetString().Should().Be("DUE_TODAY");
            board.GetProperty("kpi").GetProperty("dueToday").GetInt32().Should().Be(2);

            var cell = (await c.GetResultAsync($"/api/PurchasePlan/GetCell?IngredientId={w.Vai}&NeedDate={Need:yyyy-MM-dd}")).Ok();
            cell.GetProperty("substitution").GetProperty("sharePct").GetDecimal().Should().Be(50);
            cell.GetProperty("listDish").GetArrayLength().Should().BeGreaterThan(0);
            cell.GetProperty("listNet").GetArrayLength().Should().Be(2);
            cell.GetProperty("listSplit").GetArrayLength().Should().Be(2);
            cell.GetProperty("canOrder").GetBoolean().Should().BeTrue();

            // Giữ 100% chuẩn: bắt buộc lý do; sau đó kế hoạch không còn thịt mông.
            (await LegacyResult.ReadAsync(await System.Net.Http.Json.HttpClientJsonExtensions.PostAsJsonAsync(c, "/api/PurchasePlan/OverrideSubstitute",
                new { needDate = Need, stdIngredientId = w.Vai, subIngredientId = w.Mong, sharePct = 0 }))).Fail("lý do");
            (await LegacyResult.ReadAsync(await System.Net.Http.Json.HttpClientJsonExtensions.PostAsJsonAsync(c, "/api/PurchasePlan/OverrideSubstitute",
                new { needDate = Need, stdIngredientId = w.Vai, subIngredientId = w.Mong, sharePct = 0, reason = "Khách phản ánh độ mềm" }))).Ok();
            var hz = await HorizonAsync(f);
            hz.ListItem.SingleOrDefault(i => i.IngredientId == w.Mong)?.ListDay.Single(x => x.Date == Need).Required.Should().Be(0);
            hz.ListItem.Single(i => i.IngredientId == w.Vai).ListDay.Single(x => x.Date == Need).Required.Should().BeApproximately(10m / 0.92m, 0.001m);

            var run = (await LegacyResult.ReadAsync(await System.Net.Http.Json.HttpClientJsonExtensions.PostAsJsonAsync(c, "/api/PurchasePlan/OrderToday", new { }))).Ok();
            run.GetProperty("created").GetInt32().Should().Be(1);
        }
    }
}
