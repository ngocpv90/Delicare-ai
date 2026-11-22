using System.Net.Http.Json;
using System.Text.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Services.Implement;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Sales
{
    /// <summary>
    /// BE2.2 – hợp đồng (Create / Submit / Approve / Terminate), dự báo (Import / Publish / GetMatrix, gợi ý số suất, lệch dự báo khi xác nhận),
    /// đổi món (MenuChangeRequest → duyệt → kế hoạch sản xuất), sửa / thêm đơn sau giờ chốt (ORDER_CHANGE_AFTER_CUTOFF). Ngày ăn thử 10/12/2026 (thứ 5),
    /// giờ chốt 20:00 ngày 09/12/2026.
    /// </summary>
    public class ContractForecastTests : IClassFixture<InMemoryApiFactory>
    {
        private static readonly DateTime MealDate = new(2026, 12, 10);
        private static readonly DateTime BeforeCutoff = new(2026, 12, 8, 10, 0, 0);
        private static readonly DateTime AfterCutoff = new(2026, 12, 9, 21, 0, 0);

        private readonly InMemoryApiFactory _factory;

        public ContractForecastTests(InMemoryApiFactory factory)
        {
            _factory = factory;
            _factory.Clock.FixedNow = BeforeCutoff;
        }

        private sealed record Scenario(Guid Customer, Guid LocA, Guid LocB, Guid Contract, Guid MealTrua, Guid Menu, Guid Chicken, Guid Fish, Guid Rice, Guid Shift);

        /// <summary>Khách + 2 điểm giao + hợp đồng Active TRUA 40 000 + thực đơn công bố (thứ 5: gà + cơm) gán cấp khách + ca TRUA + món cá (để đổi).</summary>
        private async Task<Scenario> SeedAsync(string code)
        {
            Guid customer = Guid.NewGuid(), locA = Guid.NewGuid(), locB = Guid.NewGuid(), contract = Guid.NewGuid(), trua = Guid.NewGuid(), menu = Guid.NewGuid();
            Guid chicken = Guid.NewGuid(), fish = Guid.NewGuid(), rice = Guid.NewGuid();
            Guid shift = Guid.Empty;
            await _factory.SeedAsync(async db =>
            {
                var existing = await db.Shift.FirstOrDefaultAsync(s => s.Code == "TRUA");
                if (existing == null)
                {
                    existing = new Shift { Id = Guid.NewGuid(), Code = "TRUA", Name = "Ca trưa" };
                    db.Shift.Add(existing);
                }
                shift = existing.Id;
                db.Customer.Add(new Customer { Id = customer, Code = code, Name = "Khách " + code, CIF = code, CustomerType = "FACTORY", DisplayName = code, Status = "Active" });
                db.CustomerDeliveryLocation.AddRange(
                    new CustomerDeliveryLocation { Id = locA, CustomerId = customer, Code = code + "-A", Name = "Điểm A" },
                    new CustomerDeliveryLocation { Id = locB, CustomerId = customer, Code = code + "-B", Name = "Điểm B" });
                db.Contract.Add(new Contract { Id = contract, CustomerId = customer, ContractNumber = "HD-" + code, Status = "Active", EffectiveFrom = new DateTime(2026, 1, 1) });
                db.ContractMeal.Add(new ContractMeal { Id = trua, ContractId = contract, MealType = "TRUA", MealName = "Suất trưa", UnitPrice = 40000 });
                db.Dish.AddRange(
                    new Dish { Id = chicken, Code = code + "-GA", Name = "Gà kho " + code },
                    new Dish { Id = fish, Code = code + "-CA", Name = "Cá kho " + code },
                    new Dish { Id = rice, Code = code + "-COM", Name = "Cơm " + code });
                db.Menu.Add(new Model.Entity.Menu { Id = menu, Code = "TD-" + code, Name = "Thực đơn " + code, VersionNo = 1, Status = "Published", EffectiveFrom = new DateTime(2026, 12, 1) });
                db.MenuLine.AddRange(
                    new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = chicken, MealType = "TRUA", DayOfWeek = 4, SequenceNo = 1 },
                    new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = rice, MealType = "TRUA", DayOfWeek = 4, SequenceNo = 1 });
                db.MenuAssignment.Add(new MenuAssignment { Id = Guid.NewGuid(), CustomerId = customer, MenuId = menu, MealType = "TRUA", Status = "Active", EffectiveFrom = new DateTime(2026, 12, 1) });
            });
            return new Scenario(customer, locA, locB, contract, trua, menu, chicken, fish, rice, shift);
        }

        private static async Task<LegacyResult> PostJson(HttpClient c, string url, object body) => await LegacyResult.ReadAsync(await c.PostAsJsonAsync(url, body));
        private static async Task<LegacyResult> PutJson(HttpClient c, string url, object body) => await LegacyResult.ReadAsync(await c.PutAsJsonAsync(url, body));

        private static async Task<Guid> ConfirmedOrderAsync(HttpClient c, Scenario s, Guid location, decimal quantity)
        {
            Guid id = (await PostJson(c, "/api/Order/Create", new { customerId = s.Customer, contractId = s.Contract, deliveryLocationId = location, mealDate = MealDate, listLine = new[] { new { contractMealId = s.MealTrua, quantity } } }))
                .Ok().GetProperty("order").GetProperty("id").GetGuid();
            (await c.PostFormAsync("/api/Order/Submit", ("Id", id))).Ok();
            (await c.PostFormAsync("/api/Order/Confirm", ("Id", id))).Ok();
            return id;
        }

        private Task<T> Q<T>(Func<Data.Connection.DatabaseContext, Task<T>> query) => _factory.QueryAsync(query);

        private async Task<Guid> PlainUserAsync()
        {
            Guid userId = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.User.Add(new User { Id = userId, UserName = "be22-" + userId.ToString("N")[..6], IsActive = true });
                return Task.CompletedTask;
            });
            return userId;
        }

        // ------------------------------------------------------------------ Hợp đồng (Catalog 78–80)
        [Fact]
        public async Task Contract_create_with_meals_submit_approve_then_terminate_rules()
        {
            var s = await SeedAsync("BE22-CT");
            using var c = _factory.CreateAuthorizedClient();

            (await PostJson(c, "/api/Contract/Create", new { customerId = s.Customer, effectiveFrom = "2026-12-01", effectiveTo = "2026-11-30", listMeal = new[] { new { mealType = "TRUA", unitPrice = 1m } } }))
                .Fail("Ngày hiệu lực từ");
            (await PostJson(c, "/api/Contract/Create", new { customerId = s.Customer, effectiveFrom = "2026-12-01", listMeal = Array.Empty<object>() })).Fail("ít nhất 1 loại suất");
            (await PostJson(c, "/api/Contract/Create", new { customerId = s.Customer, effectiveFrom = "2026-12-01", listMeal = new[] { new { mealType = "TOI", unitPrice = 1m }, new { mealType = "toi", unitPrice = 2m } } }))
                .Fail("nhiều lần");
            (await PostJson(c, "/api/Contract/Create", new { customerId = s.Customer, effectiveFrom = "2026-12-01", listMeal = new[] { new { mealType = "TOI", unitPrice = 0m } } })).Fail("lớn hơn 0");
            // Lỗi UAT 2026-11-21: loại suất phải có trong danh mục ReferenceLists:MealType (không nhận chữ tự do).
            (await PostJson(c, "/api/Contract/Create", new { customerId = s.Customer, effectiveFrom = "2026-12-01", listMeal = new[] { new { mealType = "Suất VIP", unitPrice = 1m } } })).Fail("không có trong danh mục");
            (await PostJson(c, "/api/Contract/Create", new { customerId = s.Customer, effectiveFrom = "2026-12-01", listMeal = new[] { new { mealType = "Trưa", unitPrice = 1m }, new { mealType = "TRUA", unitPrice = 2m } } })).Fail("nhiều lần");

            var created = (await PostJson(c, "/api/Contract/Create", new
            {
                customerId = s.Customer, name = "HĐ mới", effectiveFrom = "2026-12-01", effectiveTo = "2026-12-31", contractValue = 1_000_000m,
                listMeal = new[] { new { mealType = "TOI", mealName = "Suất tối", unitPrice = 30000m }, new { mealType = "SANG", mealName = "Suất sáng", unitPrice = 15000m } },
            })).Ok();
            Guid id = created.GetProperty("contract").GetProperty("id").GetGuid();
            created.GetProperty("contract").GetProperty("contractNumber").GetString().Should().MatchRegex(@"^HD-261208-\d{3}$");
            created.GetProperty("contract").GetProperty("status").GetString().Should().Be("Draft");
            created.GetProperty("listMeal").GetArrayLength().Should().Be(2);
            created.GetProperty("allowedActions").EnumerateArray().Select(x => x.GetString()).Should().Contain(["submit", "update"]);

            // Loại suất chỉ sửa khi Nháp; MealType không trùng.
            (await c.PostFormAsync("/api/ContractMeal/Insert", ("ContractId", id), ("MealType", "TOI"), ("UnitPrice", 1m))).Fail("nhiều lần");
            (await c.PostFormAsync("/api/ContractMeal/Insert", ("ContractId", id), ("MealType", "Suất VIP"), ("UnitPrice", 1m))).Fail("không có trong danh mục");
            (await c.PostFormAsync("/api/ContractMeal/Insert", ("ContractId", id), ("MealType", "dem"), ("UnitPrice", 25000m))).Ok();
            (await Q(db => db.ContractMeal.AsNoTracking().Where(x => x.ContractId == id).Select(x => x.MealType).ToListAsync())).Should().BeEquivalentTo(["TOI", "SANG", "DEM"], "lưu mã chuẩn của danh mục");
            (await c.PostFormAsync("/api/ContractMeal/Insert", ("ContractId", s.Contract), ("MealType", "DEM"), ("UnitPrice", 25000m))).Fail("Nháp");

            // Gửi duyệt → Chờ duyệt + XN-; người không có quyền không duyệt được; admin duyệt → Hiệu lực.
            var submitted = await c.PostFormAsync("/api/Contract/Submit", ("Id", id));
            submitted.Ok().GetProperty("contract").GetProperty("status").GetString().Should().Be("PendingApproval");
            submitted.Message.Should().Contain("XN-");
            (await c.PostFormAsync("/api/ContractMeal/Insert", ("ContractId", id), ("MealType", "SANG"), ("UnitPrice", 1m))).Fail("Nháp");
            var pending = (await c.GetResultAsync("/api/ApprovalRequest/GetMyPending?type=crm-contract"));
            pending.ListData.EnumerateArray().Should().Contain(x => x.GetProperty("businessId").GetGuid() == id);
            using (var plain = _factory.CreateAuthorizedClient(await PlainUserAsync()))
            {
                (await plain.PostFormAsync("/api/Contract/Approve", ("Id", id))).Fail("không có quyền");
            }
            (await c.PostFormAsync("/api/Contract/Approve", ("Id", id), ("Note", "OK"))).Ok();
            var contract = await Q(db => db.Contract.AsNoTracking().SingleAsync(x => x.Id == id));
            contract.Status.Should().Be("Active");
            contract.ContractNo.Should().Be(contract.ContractNumber, "ghi song song cột cũ");
            (await Q(db => db.ApprovalDecision.CountAsync(d => d.DecisionCode == "APPROVE" && db.ApprovalRequest.Any(a => a.Id == d.ApprovalRequestId && a.BusinessId == id)))).Should().Be(1);
            (await c.PutFormAsync("/api/Contract/Update", ("Id", id), ("CustomerId", s.Customer), ("EffectiveFrom", new DateTime(2026, 12, 1)), ("Status", "Draft"))).Fail("Nháp");

            // CRUD Insert: luôn Nháp, số tự sinh khi trống.
            (await c.PostFormAsync("/api/Contract/Insert", ("CustomerId", s.Customer), ("EffectiveFrom", new DateTime(2026, 12, 1)), ("Status", "Active"))).Ok();
            (await Q(db => db.Contract.AsNoTracking().Where(x => x.CustomerId == s.Customer && x.Id != id && x.Id != s.Contract).Select(x => x.Status).SingleAsync())).Should().Be("Draft");

            // Chấm dứt: lý do bắt buộc; còn đơn chưa giao sau ngày chấm dứt → chặn.
            Guid order = await ConfirmedOrderAsync(c, s, s.LocA, 50);
            (await c.PostFormAsync("/api/Contract/Terminate", ("Id", s.Contract), ("EffectiveTo", new DateTime(2026, 12, 9)))).Fail("lý do");
            (await c.PostFormAsync("/api/Contract/Terminate", ("Id", s.Contract), ("Reason", "Khách dừng"), ("EffectiveTo", new DateTime(2026, 12, 9)))).Fail("chưa giao");
            (await c.PostFormAsync("/api/Order/Cancel", ("Id", order), ("Reason", "Khách dừng"))).Ok();
            var terminated = (await c.PostFormAsync("/api/Contract/Terminate", ("Id", s.Contract), ("Reason", "Khách dừng"), ("EffectiveTo", new DateTime(2026, 12, 10)))).Ok();
            terminated.GetProperty("contract").GetProperty("status").GetString().Should().Be("Terminated");
            // Đơn tới hết ngày chấm dứt vẫn tạo được; ngày sau thì không còn hợp đồng.
            (await c.GetResultAsync($"/api/Order/GetNewTemplate?customerId={s.Customer}&mealDate=2026-12-10")).Ok();
            (await c.GetResultAsync($"/api/Order/GetNewTemplate?customerId={s.Customer}&mealDate=2026-12-11")).Ok()
                .GetProperty("contractId").GetGuid().Should().Be(id, "hợp đồng mới Hiệu lực tới 31/12");
            (await c.PostFormAsync("/api/Contract/Terminate", ("Id", s.Contract), ("Reason", "Lần 2"))).Fail("trạng thái");
        }

        [Fact]
        public async Task Contract_reject_returns_to_draft_and_edit_after_submit_blocks_approval()
        {
            var s = await SeedAsync("BE22-CTR");
            using var c = _factory.CreateAuthorizedClient();
            Guid id = (await PostJson(c, "/api/Contract/Create", new { customerId = s.Customer, contractNumber = "GIAY-01/2026", effectiveFrom = "2026-12-01", listMeal = new[] { new { mealType = "TOI", unitPrice = 30000m } } }))
                .Ok().GetProperty("contract").GetProperty("id").GetGuid();
            (await Q(db => db.Contract.AsNoTracking().SingleAsync(x => x.Id == id))).ContractNumber.Should().MatchRegex(@"^HD-\d{6}-\d{3}$", "MA-1 (D-63 c): số hợp đồng máy chủ sinh, số gửi lên bị bỏ qua");
            // MA-1 (D-63 c): số gửi lên bị bỏ qua → không còn lỗi trùng số hợp đồng giấy.
            (await c.PostFormAsync("/api/Contract/Submit", ("Id", id))).Ok();
            (await c.PostFormAsync("/api/Contract/Reject", ("Id", id))).Fail("lý do");
            (await c.PostFormAsync("/api/Contract/Reject", ("Id", id), ("Note", "Sai giá"))).Ok();
            var contract = await Q(db => db.Contract.AsNoTracking().SingleAsync(x => x.Id == id));
            contract.Status.Should().Be("Draft");
            contract.Note.Should().Contain("Từ chối: Sai giá");

            // Gửi lại, sửa giá ngay trong DB (giả lập sửa chui) → hash khác → không duyệt được.
            (await c.PostFormAsync("/api/Contract/Submit", ("Id", id))).Ok();
            await _factory.SeedAsync(async db => (await db.ContractMeal.SingleAsync(m => m.ContractId == id)).UnitPrice = 99999m);
            (await c.PostFormAsync("/api/Contract/Approve", ("Id", id))).Fail("đã thay đổi");
        }

        // ------------------------------------------------------------------ Dự báo (Catalog 87–89, 92, 96)
        [Fact]
        public async Task Forecast_import_versions_publish_matrix_template_suggestion_and_confirm_variance()
        {
            var s = await SeedAsync("BE22-FC");
            using var c = _factory.CreateAuthorizedClient();

            (await PostJson(c, "/api/Forecast/Import", new { listItem = Array.Empty<object>() })).Fail("trống");
            var other = await SeedAsync("BE22-FC2");
            (await PostJson(c, "/api/Forecast/Import", new { listItem = new[] { new { customerId = s.Customer, deliveryLocationId = (Guid?)other.LocA, forecastDate = MealDate, mealType = "TRUA", forecastQuantity = 10m } } }))
                .Fail("điểm giao không thuộc khách");
            (await PostJson(c, "/api/Forecast/Import", new
            {
                listItem = new[]
                {
                    new { customerId = s.Customer, deliveryLocationId = (Guid?)s.LocA, forecastDate = MealDate, mealType = "TRUA", forecastQuantity = 10m },
                    new { customerId = s.Customer, deliveryLocationId = (Guid?)s.LocA, forecastDate = MealDate, mealType = "trua", forecastQuantity = 11m },
                },
            })).Fail("trùng");

            // v1: điểm A 100, cấp khách 51 (chia cho điểm B) → công bố.
            var imported = (await PostJson(c, "/api/Forecast/Import", new
            {
                listItem = new[]
                {
                    new { customerId = s.Customer, deliveryLocationId = (Guid?)s.LocA, forecastDate = MealDate, mealType = "TRUA", forecastQuantity = 100m },
                    new { customerId = s.Customer, deliveryLocationId = (Guid?)null, forecastDate = MealDate, mealType = "TRUA", forecastQuantity = 51m },
                },
            })).Ok();
            imported.GetProperty("created").GetInt32().Should().Be(2);
            imported.GetProperty("versionNo").GetInt32().Should().Be(1);
            var template0 = (await c.GetResultAsync($"/api/Order/GetNewTemplate?customerId={s.Customer}&mealDate=2026-12-10")).Ok();
            template0.GetProperty("listLocation")[0].GetProperty("listLine")[0].GetProperty("suggestedQuantity").GetDecimal().Should().Be(0, "bản Nháp chưa dùng làm gợi ý");
            (await c.PostFormAsync("/api/Forecast/Publish", ("From", MealDate), ("To", MealDate), ("ListCustomerId", s.Customer))).Ok().GetProperty("published").GetInt32().Should().Be(2);
            (await c.PostFormAsync("/api/Forecast/Publish", ("From", MealDate), ("To", MealDate), ("ListCustomerId", s.Customer))).Fail("Không có dự báo Nháp");

            var template = (await c.GetResultAsync($"/api/Order/GetNewTemplate?customerId={s.Customer}&mealDate=2026-12-10")).Ok();
            decimal Suggest(JsonElement t, int i) => t.GetProperty("listLocation")[i].GetProperty("listLine")[0].GetProperty("suggestedQuantity").GetDecimal();
            Suggest(template, 0).Should().Be(100, "dự báo riêng điểm A");
            Suggest(template, 1).Should().Be(51, "dự báo cấp khách cho điểm chưa có dự báo riêng");

            // v2 Nháp cho điểm A = 120: ma trận vẫn hiện bản đã công bố (151) + draftValues 120; công bố → v1 điểm A Bị thay thế.
            (await PostJson(c, "/api/Forecast/Import", new { listItem = new[] { new { customerId = s.Customer, deliveryLocationId = (Guid?)s.LocA, forecastDate = MealDate, mealType = "TRUA", forecastQuantity = 120m } } }))
                .Ok().GetProperty("versionNo").GetInt32().Should().Be(2);
            var matrix = (await c.GetResultAsync($"/api/Forecast/GetMatrix?From=2026-12-09&To=2026-12-11&CustomerId={s.Customer}")).Ok();
            matrix.GetProperty("dates").GetArrayLength().Should().Be(3);
            var row = matrix.GetProperty("rows")[0];
            row.GetProperty("values").GetProperty("2026-12-10").GetDecimal().Should().Be(151);
            row.GetProperty("draftValues").GetProperty("2026-12-10").GetDecimal().Should().Be(120);
            (await c.PostFormAsync("/api/Forecast/Publish", ("From", MealDate), ("To", MealDate))).Ok().GetProperty("superseded").GetInt32().Should().Be(1);
            matrix = (await c.GetResultAsync($"/api/Forecast/GetMatrix?From=2026-12-10&To=2026-12-10&CustomerId={s.Customer}")).Ok();
            matrix.GetProperty("rows")[0].GetProperty("values").GetProperty("2026-12-10").GetDecimal().Should().Be(171);
            matrix.GetProperty("totals").GetProperty("2026-12-10").GetDecimal().Should().Be(171);
            (await c.GetResultAsync("/api/Forecast/GetMatrix?From=2026-12-10&To=2027-06-10")).Fail("tối đa");

            // CRUD: Insert qua Import; sửa bản đã công bố bị chặn.
            (await c.PostFormAsync("/api/Forecast/Insert", ("CustomerId", s.Customer), ("ForecastDate", MealDate), ("ForecastQuantity", 5m))).Fail("Forecast/Import");
            Guid published = await Q(db => db.Forecast.Where(f => f.CustomerId == s.Customer && f.Status == "Published" && f.DeliveryLocationId == s.LocA).Select(f => f.Id).SingleAsync());
            (await c.PutFormAsync("/api/Forecast/Update", ("Id", published), ("CustomerId", s.Customer), ("ForecastDate", MealDate), ("ForecastQuantity", 5m))).Fail("Nháp");

            // Xác nhận đơn: điểm A dự báo 120, đặt 135 → +12,5% > 10% → cảnh báo + ghi nhận; điểm B dự báo 51, đặt 55 → +7,84% không cảnh báo.
            Guid orderA = (await PostJson(c, "/api/Order/Create", new { customerId = s.Customer, deliveryLocationId = s.LocA, mealDate = MealDate, listLine = new[] { new { contractMealId = s.MealTrua, quantity = 135m } } }))
                .Ok().GetProperty("order").GetProperty("id").GetGuid();
            (await c.PostFormAsync("/api/Order/Submit", ("Id", orderA))).Ok();
            var confirmed = await c.PostFormAsync("/api/Order/Confirm", ("Id", orderA));
            var detail = confirmed.Ok();
            confirmed.Message.Should().Contain("Lệch dự báo +12.5%");
            detail.GetProperty("forecastQuantity").GetDecimal().Should().Be(120);
            detail.GetProperty("forecastVariancePercent").GetDecimal().Should().Be(12.5m);
            detail.GetProperty("forecastVarianceExceeded").GetBoolean().Should().BeTrue();
            detail.GetProperty("listLine")[0].GetProperty("forecastQuantity").GetDecimal().Should().Be(120);
            (await Q(db => db.Order.AsNoTracking().SingleAsync(o => o.Id == orderA))).Note.Should().Contain("[LECH_DU_BAO]");
            Guid orderB = (await PostJson(c, "/api/Order/Create", new { customerId = s.Customer, deliveryLocationId = s.LocB, mealDate = MealDate, listLine = new[] { new { contractMealId = s.MealTrua, quantity = 55m } } }))
                .Ok().GetProperty("order").GetProperty("id").GetGuid();
            (await c.PostFormAsync("/api/Order/Submit", ("Id", orderB))).Ok();
            var okB = await c.PostFormAsync("/api/Order/Confirm", ("Id", orderB));
            okB.Ok().GetProperty("forecastVarianceExceeded").GetBoolean().Should().BeFalse();
            okB.Message.Should().NotContain("Lệch dự báo");
        }

        [Fact]
        public void Forecast_rules_split_customer_level_and_variance()
        {
            Guid customer = Guid.NewGuid(), a = Guid.NewGuid(), b = Guid.NewGuid(), cLoc = Guid.NewGuid();
            var rows = new List<Forecast>
            {
                new() { CustomerId = customer, DeliveryLocationId = null, MealType = "TRUA", ForecastQuantity = 100 },
                new() { CustomerId = customer, DeliveryLocationId = cLoc, MealType = "TRUA", ForecastQuantity = 7 },
            };
            var locations = new List<Guid> { a, b, cLoc };
            ForecastRules.ForLocation(rows, customer, a, locations, "trua", null).Should().Be(50);
            ForecastRules.ForLocation(rows, customer, b, locations, "TRUA", null).Should().Be(50);
            ForecastRules.ForLocation(rows, customer, cLoc, locations, "TRUA", null).Should().Be(7);
            ForecastRules.ForLocation(rows, customer, a, locations, "TOI", null).Should().BeNull();
            rows[0].ForecastQuantity = 101;
            ForecastRules.ForLocation(rows, customer, a, locations, "TRUA", null).Should().Be(51, "điểm đầu nhận phần dư");
            ForecastRules.VariancePercent(115, 100).Should().Be(15);
            ForecastRules.VariancePercent(10, 0).Should().BeNull();
        }

        // ------------------------------------------------------------------ Đổi món (Catalog 104)
        [Fact]
        public async Task Menu_change_request_approval_updates_draft_plan_and_survives_regenerate()
        {
            var s = await SeedAsync("BE22-MC");
            using var c = _factory.CreateAuthorizedClient();
            Guid order = await ConfirmedOrderAsync(c, s, s.LocA, 100);
            (await c.PostFormAsync("/api/ProductionPlan/Generate", ("ProductionDate", MealDate))).Ok();
            async Task<Dictionary<Guid, decimal>> PlanAsync() => (await Q(db => (from p in db.ProductionPlan
                                                                               join l in db.ProductionPlanLine on p.Id equals l.ProductionPlanId
                                                                               where p.ProductionDate == MealDate && !l.IsDeleted && (l.DishId == s.Chicken || l.DishId == s.Fish || l.DishId == s.Rice)
                                                                               select l).ToListAsync())).GroupBy(l => l.DishId).ToDictionary(g => g.Key, g => g.Sum(l => l.PlannedQuantity));
            (await PlanAsync())[s.Chicken].Should().Be(100);

            (await c.PostFormAsync("/api/MenuChangeRequest/Create", ("OrderId", order), ("OriginalDishId", s.Chicken), ("RequestedDishId", s.Fish), ("Quantity", 30m))).Fail("lý do");
            (await c.PostFormAsync("/api/MenuChangeRequest/Create", ("OrderId", order), ("OriginalDishId", s.Fish), ("RequestedDishId", s.Chicken), ("Reason", "Khách dị ứng"))).Fail("Món cũ không có");
            (await c.PostFormAsync("/api/MenuChangeRequest/Create", ("OrderId", order), ("OriginalDishId", s.Chicken), ("RequestedDishId", s.Chicken), ("Reason", "Khách dị ứng"))).Fail("món cũ và món mới");
            var created = await c.PostFormAsync("/api/MenuChangeRequest/Create", ("OrderId", order), ("OriginalDishId", s.Chicken), ("RequestedDishId", s.Fish), ("Quantity", 30m), ("Reason", "Khách dị ứng gà"));
            var detail = created.Ok();
            created.Message.Should().Contain("XN-");
            Guid id = detail.GetProperty("request").GetProperty("id").GetGuid();
            detail.GetProperty("request").GetProperty("requestNo").GetString().Should().MatchRegex(@"^DM-261208-\d{4}$");
            detail.GetProperty("request").GetProperty("requestStatus").GetString().Should().Be("Pending");
            detail.GetProperty("request").GetProperty("menuId").GetGuid().Should().Be(s.Menu);
            detail.GetProperty("planNeedsManualAction").GetBoolean().Should().BeFalse();
            (await c.PutFormAsync("/api/MenuChangeRequest/Update", ("Id", id), ("RequestNo", "X"), ("MealDate", MealDate), ("Reason", "x"))).Fail("MenuChangeRequest/Create");
            (await c.GetResultAsync("/api/ApprovalRequest/GetMyPending?type=menu-change-request")).ListData.EnumerateArray()
                .Should().Contain(x => x.GetProperty("businessId").GetGuid() == id && x.GetProperty("title").GetString()!.Contains("→"));

            (await c.PostFormAsync("/api/MenuChangeRequest/Approve", ("Id", id))).Ok();
            var request = await Q(db => db.MenuChangeRequest.AsNoTracking().SingleAsync(r => r.Id == id));
            request.RequestStatus.Should().Be("Approved");
            request.ApprovedBy.Should().Be(InMemoryApiFactory.UserId);
            var plan = await PlanAsync();
            plan[s.Chicken].Should().Be(70);
            plan[s.Fish].Should().Be(30);
            plan[s.Rice].Should().Be(100);

            // Sinh lại kế hoạch (Nháp) → đổi món vẫn được áp.
            (await c.PostFormAsync("/api/ProductionPlan/Generate", ("ProductionDate", MealDate))).Ok();
            plan = await PlanAsync();
            plan[s.Chicken].Should().Be(70);
            plan[s.Fish].Should().Be(30);

            // Yêu cầu 2 → người gửi hủy qua ApprovalRequest/Cancel → Đã hủy, kế hoạch không đổi; xóa được.
            Guid second = (await c.PostFormAsync("/api/MenuChangeRequest/Create", ("OrderId", order), ("OriginalDishId", s.Rice), ("RequestedDishId", s.Fish), ("Quantity", 10m), ("Reason", "Thử hủy")))
                .Ok().GetProperty("request").GetProperty("id").GetGuid();
            Guid approval = await Q(db => db.ApprovalRequest.Where(a => a.BusinessId == second).Select(a => a.Id).SingleAsync());
            (await c.DeleteResultAsync($"/api/MenuChangeRequest/Delete?id={second}")).Fail("trạng thái");
            (await c.PostFormAsync("/api/ApprovalRequest/Cancel", ("Id", approval), ("Reason", "Khách đổi ý"))).Ok();
            (await Q(db => db.MenuChangeRequest.AsNoTracking().SingleAsync(r => r.Id == second))).RequestStatus.Should().Be("Cancelled");
            (await PlanAsync())[s.Rice].Should().Be(100);
            (await c.DeleteResultAsync($"/api/MenuChangeRequest/Delete?id={second}")).Ok();

            // Kế hoạch đã duyệt → yêu cầu mới chỉ cảnh báo xử lý tay.
            await _factory.SeedAsync(async db => (await db.ProductionPlan.SingleAsync(p => p.ProductionDate == MealDate)).Status = "Approved");
            var third = (await c.PostFormAsync("/api/MenuChangeRequest/Create", ("OrderId", order), ("OriginalDishId", s.Rice), ("RequestedDishId", s.Fish), ("Quantity", 5m), ("Reason", "Sau duyệt KH"))).Ok();
            third.GetProperty("planNeedsManualAction").GetBoolean().Should().BeTrue();
            third.GetProperty("listWarning").GetArrayLength().Should().Be(1);
            (await c.PostFormAsync("/api/MenuChangeRequest/Reject", ("Id", third.GetProperty("request").GetProperty("id").GetGuid()), ("Note", "Không kịp"))).Ok();
        }

        [Fact]
        public void Apply_menu_changes_moves_quantity_within_order_and_caps_at_available()
        {
            Guid o1 = Guid.NewGuid(), o2 = Guid.NewGuid(), l1 = Guid.NewGuid(), l2 = Guid.NewGuid(), shift = Guid.NewGuid(), ga = Guid.NewGuid(), ca = Guid.NewGuid();
            var demands = new List<ProductionRules.DishDemand> { new(o1, l1, shift, ga, 40), new(o2, l2, shift, ga, 60) };
            var result = ProductionRules.ApplyMenuChanges(demands, [new ProductionRules.MenuChange(o1, null, ga, ca, 100)]);
            result.Where(d => d.DishId == ga).Sum(d => d.Quantity).Should().Be(60, "chỉ đơn 1 đổi, tối đa số đang có (40)");
            result.Single(d => d.DishId == ca).Should().Be(new ProductionRules.DishDemand(o1, l1, shift, ca, 40));
            var all = ProductionRules.ApplyMenuChanges(demands, [new ProductionRules.MenuChange(null, shift, ga, ca, 50)]);
            all.Where(d => d.DishId == ca).Sum(d => d.Quantity).Should().Be(50);
            all.Where(d => d.DishId == ga).Sum(d => d.Quantity).Should().Be(50);
        }

        // ------------------------------------------------------------------ Sửa / thêm đơn sau giờ chốt (ORDER_CHANGE_AFTER_CUTOFF)
        [Fact]
        public async Task Order_change_after_cutoff_goes_through_approval_and_survives_bulk_lock()
        {
            var s = await SeedAsync("BE22-AC");
            Guid sang = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.ContractMeal.Add(new ContractMeal { Id = sang, ContractId = s.Contract, MealType = "SANG", MealName = "Suất sáng", UnitPrice = 20000 });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();
            Guid order = await ConfirmedOrderAsync(c, s, s.LocA, 100);
            Guid line = await Q(db => db.OrderLine.Where(l => l.OrderId == order && !l.IsDeleted).Select(l => l.Id).SingleAsync());
            object Change(decimal qty, bool addSang = false) => new
            {
                id = order, reason = "Khách tăng ca đột xuất", listLine = new[] { new { id = line, quantity = qty } },
                listNewLine = addSang ? new[] { new { contractMealId = sang, shiftId = (Guid?)s.Shift, quantity = 20m } } : [],
            };

            (await PostJson(c, "/api/Order/RequestChange", Change(120))).Fail("Chưa tới giờ chốt");
            _factory.Clock.FixedNow = AfterCutoff;
            (await PutJson(c, "/api/Order/UpdateLines", new { id = order, listLine = new[] { new { id = line, quantity = 120m } } })).Fail("Order/RequestChange");
            (await PostJson(c, "/api/Order/RequestChange", new { id = order, listLine = new[] { new { id = line, quantity = 120m } } })).Fail("lý do");
            (await PostJson(c, "/api/Order/RequestChange", new { id = order, reason = "Không đổi gì", listLine = new[] { new { id = line, quantity = 100m } } })).Fail("Không có thay đổi");

            var asked = await PostJson(c, "/api/Order/RequestChange", Change(120, addSang: true));
            asked.Ok().GetProperty("approvalRequested").GetBoolean().Should().BeTrue();
            asked.Message.Should().Contain("XN-");
            (await Q(db => db.OrderLine.SingleAsync(l => l.Id == line))).Quantity.Should().Be(100, "chưa duyệt chưa ghi");
            (await PostJson(c, "/api/Order/RequestChange", Change(130))).Fail("đang có yêu cầu duyệt");
            var pendingDetail = (await c.GetResultAsync($"/api/Order/GetDetail?id={order}")).Ok();
            pendingDetail.GetProperty("pendingApprovalType").GetString().Should().Be("ORDER_CHANGE_AFTER_CUTOFF");

            // Nút chốt 20h chạy trong lúc chờ duyệt → đơn Đã chốt, yêu cầu vẫn duyệt được.
            var summary = (await c.PostFormAsync("/api/Order/LockByMealDate", ("MealDate", MealDate))).Ok();
            summary.GetProperty("listPendingApprovalNumber").GetArrayLength().Should().Be(1);
            var approval = await Q(db => db.ApprovalRequest.AsNoTracking().SingleAsync(a => a.BusinessId == order && a.BusinessType == "ORDER_CHANGE_AFTER_CUTOFF"));
            (await c.GetResultAsync("/api/ApprovalRequest/GetMyPending?type=crm-order")).ListData.EnumerateArray()
                .Should().Contain(x => x.GetProperty("id").GetGuid() == approval.Id);
            (await c.PostFormAsync("/api/ApprovalRequest/Decide", ("Id", approval.Id), ("Approve", true), ("Note", "Đồng ý"))).Ok();
            var lines = await Q(db => db.OrderLine.AsNoTracking().Where(l => l.OrderId == order && !l.IsDeleted).ToListAsync());
            lines.Should().HaveCount(2);
            lines.Single(l => l.Id == line).Quantity.Should().Be(120);
            lines.Single(l => l.ContractMealId == sang).LineAmount.Should().Be(400000);
            var saved = await Q(db => db.Order.AsNoTracking().SingleAsync(o => o.Id == order));
            saved.Status.Should().Be("Locked");
            saved.TotalQuantity.Should().Be(140);
            saved.TotalAmount.Should().Be(120 * 40000m + 20 * 20000m);
            saved.Note.Should().Contain("[SAU_GIO_CHOT] Duyệt sửa đơn sau giờ chốt");

            // Lần 2: từ chối → không đổi số.
            (await PostJson(c, "/api/Order/RequestChange", Change(90))).Ok();
            var second = await Q(db => db.ApprovalRequest.AsNoTracking().SingleAsync(a => a.BusinessId == order && a.RequestStatus == "Pending"));
            (await c.PostFormAsync("/api/ApprovalRequest/Decide", ("Id", second.Id), ("Approve", false), ("Note", "Bếp không kịp"))).Ok();
            (await Q(db => db.OrderLine.SingleAsync(l => l.Id == line))).Quantity.Should().Be(120);
            (await Q(db => db.Order.AsNoTracking().SingleAsync(o => o.Id == order))).Note.Should().Contain("Từ chối sửa đơn sau giờ chốt: Bếp không kịp");
        }

        [Fact]
        public async Task Order_create_after_cutoff_needs_reason_and_approval_locks_or_reject_cancels()
        {
            var s = await SeedAsync("BE22-AN");
            using var c = _factory.CreateAuthorizedClient();
            _factory.Clock.FixedNow = AfterCutoff;
            object Body(Guid location, string? reason) => new { customerId = s.Customer, deliveryLocationId = location, mealDate = MealDate, afterCutoffReason = reason, listLine = new[] { new { contractMealId = s.MealTrua, quantity = 25m } } };
            (await PostJson(c, "/api/Order/Create", Body(s.LocA, null))).Fail("quá giờ chốt");
            var created = await PostJson(c, "/api/Order/Create", Body(s.LocA, "Khách mới ký bổ sung"));
            var detail = created.Ok();
            created.Message.Should().Contain("gửi duyệt thêm đơn");
            Guid id = detail.GetProperty("order").GetProperty("id").GetGuid();
            detail.GetProperty("order").GetProperty("status").GetString().Should().Be("Draft");
            var approval = await Q(db => db.ApprovalRequest.AsNoTracking().SingleAsync(a => a.BusinessId == id));
            approval.BusinessType.Should().Be("ORDER_CHANGE_AFTER_CUTOFF");
            (await c.PostFormAsync("/api/ApprovalRequest/Decide", ("Id", approval.Id), ("Approve", true))).Ok();
            (await Q(db => db.Order.AsNoTracking().SingleAsync(o => o.Id == id))).Status.Should().Be("Locked");

            Guid rejected = (await PostJson(c, "/api/Order/Create", Body(s.LocB, "Thêm điểm B"))).Ok().GetProperty("order").GetProperty("id").GetGuid();
            var r = await Q(db => db.ApprovalRequest.AsNoTracking().SingleAsync(a => a.BusinessId == rejected));
            (await c.PostFormAsync("/api/ApprovalRequest/Decide", ("Id", r.Id), ("Approve", false), ("Note", "Hết nguyên liệu"))).Ok();
            var order = await Q(db => db.Order.AsNoTracking().SingleAsync(o => o.Id == rejected));
            order.Status.Should().Be("Cancelled");
            order.Note.Should().Contain("Từ chối thêm đơn sau giờ chốt");
        }
    }
}
