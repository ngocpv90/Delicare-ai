using System.Net.Http.Json;
using System.Text.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Sales
{
    /// <summary>
    /// T13 (Pilot P3) – đơn hàng theo khung cũ: Order/GetNewTemplate, Create, UpdateLines, Submit, Confirm, Lock, LockByMealDate, Cancel.
    /// Giờ chốt 20h D-1 (đồng hồ test), lệch suất &gt; 30% → duyệt ORDER_DEVIATION, thứ tự tra giá, số PH-.
    /// Ngày ăn thử: 10/12/2026 → giờ chốt 20:00 ngày 09/12/2026.
    /// </summary>
    public class OrderTests : IClassFixture<InMemoryApiFactory>
    {
        private static readonly DateTime MealDate = new(2026, 12, 10);
        private static readonly DateTime BeforeCutoff = new(2026, 12, 9, 19, 59, 0);
        private static readonly DateTime AtCutoff = new(2026, 12, 9, 20, 0, 0);

        private readonly InMemoryApiFactory _factory;

        public OrderTests(InMemoryApiFactory factory)
        {
            _factory = factory;
            _factory.Clock.FixedNow = new DateTime(2026, 12, 8, 10, 0, 0);
        }

        private sealed record Scenario(Guid Customer, Guid LocA, Guid LocB, Guid Contract, Guid MealTrua, Guid MealSang, Guid Menu);

        /// <summary>Khách + 2 điểm giao + hợp đồng Active (TRUA 40 000, SANG 20 000) + thực đơn đã công bố gán cấp khách cho TRUA + ca.</summary>
        private async Task<Scenario> SeedAsync(string code)
        {
            Guid customer = Guid.NewGuid(), locA = Guid.NewGuid(), locB = Guid.NewGuid(), contract = Guid.NewGuid(), trua = Guid.NewGuid(), sang = Guid.NewGuid(), menu = Guid.NewGuid();
            await _factory.SeedAsync(async db =>
            {
                foreach (var shift in new[] { "TRUA", "SANG_6H" })
                {
                    if (!await db.Shift.AnyAsync(s => s.Code == shift))
                    {
                        db.Shift.Add(new Shift { Id = Guid.NewGuid(), Code = shift, Name = "Ca " + shift });
                    }
                }
                db.Customer.Add(new Customer { Id = customer, Code = code, Name = "Khách " + code, CIF = code, CustomerType = "FACTORY", DisplayName = code, Status = "Active" });
                db.CustomerDeliveryLocation.AddRange(
                    new CustomerDeliveryLocation { Id = locA, CustomerId = customer, Code = code + "-A", Name = "Điểm A" },
                    new CustomerDeliveryLocation { Id = locB, CustomerId = customer, Code = code + "-B", Name = "Điểm B" });
                db.Contract.Add(new Contract { Id = contract, CustomerId = customer, ContractNumber = "HD-" + code, Status = "Active", EffectiveFrom = new DateTime(2026, 1, 1) });
                db.ContractMeal.AddRange(
                    new ContractMeal { Id = trua, ContractId = contract, MealType = "TRUA", MealName = "Suất trưa", UnitPrice = 40000 },
                    new ContractMeal { Id = sang, ContractId = contract, MealType = "SANG", MealName = "Suất sáng", UnitPrice = 20000 });
                db.Menu.Add(new Model.Entity.Menu { Id = menu, Code = "TD-" + code, Name = "Thực đơn " + code, VersionNo = 1, Status = "Published", MenuType = "COMMON" /* UI-6.12 (D-90): mọi khách dùng thực đơn chung */, EffectiveFrom = new DateTime(2026, 12, 1) });
                db.MenuAssignment.Add(new MenuAssignment { Id = Guid.NewGuid(), CustomerId = customer, MenuId = menu, MealType = "TRUA", Status = "Active", EffectiveFrom = new DateTime(2026, 12, 1) });
            });
            return new Scenario(customer, locA, locB, contract, trua, sang, menu);
        }

        private static async Task<LegacyResult> PostJson(HttpClient c, string url, object body) => await LegacyResult.ReadAsync(await c.PostAsJsonAsync(url, body));
        private static async Task<LegacyResult> PutJson(HttpClient c, string url, object body) => await LegacyResult.ReadAsync(await c.PutAsJsonAsync(url, body));

        private static Task<LegacyResult> CreateAsync(HttpClient c, Scenario s, Guid location, decimal trua, decimal sang = 0)
            => PostJson(c, "/api/Order/Create", new
            {
                customerId = s.Customer, deliveryLocationId = location, mealDate = MealDate,
                listLine = new[] { new { contractMealId = s.MealTrua, quantity = trua }, new { contractMealId = s.MealSang, quantity = sang } },
            });

        private static Guid IdOf(JsonElement detail) => detail.GetProperty("order").GetProperty("id").GetGuid();

        private Task<Order> OrderAsync(Guid id) => _factory.QueryAsync(db => db.Order.AsNoTracking().SingleAsync(o => o.Id == id));

        private Task<List<OrderLine>> LinesAsync(Guid id) => _factory.QueryAsync(db => db.OrderLine.AsNoTracking().Where(l => l.OrderId == id && !l.IsDeleted).ToListAsync());

        private static Task<LegacyResult> Act(HttpClient c, string action, Guid id, params (string, object?)[] extra)
            => c.PostFormAsync("/api/Order/" + action, new (string, object?)[] { ("Id", id) }.Concat(extra).ToArray());

        [Fact]
        public async Task Template_create_submit_confirm_lock_happy_path_with_number_prices_menu_and_totals()
        {
            var s = await SeedAsync("ORD-OK");
            using var c = _factory.CreateAuthorizedClient();

            var template = (await c.GetResultAsync($"/api/Order/GetNewTemplate?customerId={s.Customer}&mealDate=2026-12-10")).Ok();
            template.GetProperty("contractId").GetGuid().Should().Be(s.Contract);
            template.GetProperty("cutoffAt").GetDateTime().Should().Be(AtCutoff);
            var locations = template.GetProperty("listLocation").EnumerateArray().ToList();
            locations.Should().HaveCount(2);
            var truaLine = locations[0].GetProperty("listLine").EnumerateArray().Single(l => l.GetProperty("mealType").GetString() == "TRUA");
            truaLine.GetProperty("unitPrice").GetDecimal().Should().Be(40000);
            truaLine.GetProperty("priceSource").GetString().Should().Be("ContractMeal");
            truaLine.GetProperty("shiftCode").GetString().Should().Be("TRUA");
            truaLine.GetProperty("menuId").GetGuid().Should().Be(s.Menu);

            var created = (await CreateAsync(c, s, s.LocA, trua: 100, sang: 0)).Ok();
            Guid id = IdOf(created);
            var order = await OrderAsync(id);
            order.OrderNumber.Should().MatchRegex(@"^DH-261208-\d{4}$");
            order.OrderNo.Should().Be(order.OrderNumber, "ghi song song cột cũ OrderNo (DEPRECATED_COLUMNS)");
            order.Status.Should().Be("Draft");
            order.TotalQuantity.Should().Be(100);
            order.TotalAmount.Should().Be(4_000_000);
            var lines = await LinesAsync(id);
            lines.Should().ContainSingle("dòng số lượng 0 bị bỏ");
            lines[0].MenuId.Should().Be(s.Menu);
            lines[0].ShiftId.Should().NotBeNull();
            lines[0].LineAmount.Should().Be(4_000_000);
            lines[0].Amount.Should().Be(4_000_000, "ghi song song cột cũ Amount");
            created.GetProperty("allowedActions").EnumerateArray().Select(x => x.GetString()).Should().Contain(["confirm", "updateLines", "cancel"], "BIZ-2.1: Nháp → Xác nhận thẳng");

            (await CreateAsync(c, s, s.LocA, trua: 5)).Fail("Đã có đơn");

            (await Act(c, "Submit", id)).Ok();
            (await Act(c, "Confirm", id, ("Note", "OK khách"))).Ok();
            (await Act(c, "Lock", id)).Ok();
            (await OrderAsync(id)).Status.Should().Be("Locked");

            (await Act(c, "Confirm", id)).Fail("trạng thái");
            (await Act(c, "Submit", id)).Fail("trạng thái");
            (await Act(c, "Cancel", id, ("Reason", "Khách báo nghỉ"))).Fail("trạng thái");
        }

        [Fact]
        public async Task Illegal_transitions_and_cancel_rules()
        {
            var s = await SeedAsync("ORD-ILL");
            using var c = _factory.CreateAuthorizedClient();
            Guid id = IdOf((await CreateAsync(c, s, s.LocA, 50)).Ok());

            (await Act(c, "Lock", id)).Fail("Đã xác nhận");
            (await Act(c, "Cancel", id)).Fail("lý do");
            (await Act(c, "Cancel", id, ("Reason", "Khách đổi lịch"))).Ok();
            var order = await OrderAsync(id);
            order.Status.Should().Be("Cancelled");
            order.Note.Should().Contain("Hủy: Khách đổi lịch");
            (await PutJson(c, "/api/Order/UpdateLines", new { id, listLine = new[] { new { id = (await LinesAsync(id))[0].Id, quantity = 10m } } })).Fail("trạng thái");

            // CRUD cũ không phá máy trạng thái.
            (await c.PostFormAsync("/api/Order/Insert", ("CustomerId", s.Customer), ("OrderDate", MealDate), ("DeliveryDate", MealDate))).Fail("Order/Create");
            (await c.PostFormAsync("/api/OrderLine/Insert", ("OrderId", id), ("Quantity", 5m))).Fail("Order/UpdateLines");
        }

        /// <summary>BIZ-1.5 (D-77 e): giá cố định lúc chốt – chính sách giá mới trước giờ chốt vào đơn khi chốt; chính sách duyệt sau khi chốt không đổi đơn.</summary>
        [Fact]
        public async Task Lock_fixes_unit_price_from_policy_at_meal_date_and_later_policies_do_not_change_locked_order()
        {
            var s = await SeedAsync("ORD-PRC");
            using var c = _factory.CreateAuthorizedClient();
            _factory.Clock.FixedNow = BeforeCutoff;
            Guid id = IdOf((await CreateAsync(c, s, s.LocA, 30)).Ok());
            (await LinesAsync(id)).Single(l => l.MealType == "TRUA").UnitPrice.Should().Be(40000, "giá hợp đồng lúc tạo");
            (await Act(c, "Submit", id)).Ok();
            (await Act(c, "Confirm", id)).Ok();
            // Chính sách giá mới cho ngày ăn, duyệt trước giờ chốt.
            await _factory.SeedAsync(db => { db.PricePolicy.Add(new PricePolicy { Id = Guid.NewGuid(), Code = "PP-PRC-1", CustomerId = s.Customer, ContractId = s.Contract, MealType = "TRUA", UnitPrice = 43000, Status = "Active", EffectiveFrom = MealDate, EffectiveTo = MealDate }); return Task.CompletedTask; });
            _factory.Clock.FixedNow = AtCutoff;
            (await Act(c, "Lock", id)).Ok();
            var line = (await LinesAsync(id)).Single(l => l.MealType == "TRUA");
            (line.UnitPrice, line.LineAmount).Should().Be((43000m, 1_290_000m));
            (await OrderAsync(id)).TotalAmount.Should().Be(1_290_000m);
            // Chính sách giá khác duyệt SAU khi chốt → đơn đã chốt giữ giá.
            await _factory.SeedAsync(db => { db.PricePolicy.Add(new PricePolicy { Id = Guid.NewGuid(), Code = "PP-PRC-2", CustomerId = s.Customer, ContractId = s.Contract, MealType = "TRUA", UnitPrice = 50000, Status = "Active", EffectiveFrom = MealDate, EffectiveTo = MealDate }); return Task.CompletedTask; });
            (await c.GetResultAsync($"/api/Order/GetDetail?id={id}")).Ok();
            (await LinesAsync(id)).Single(l => l.MealType == "TRUA").UnitPrice.Should().Be(43000);
            (await OrderAsync(id)).TotalAmount.Should().Be(1_290_000m);
        }

        [Fact]
        public async Task Cutoff_20h_previous_day_blocks_changes_and_bulk_lock_needs_cutoff()
        {
            var s = await SeedAsync("ORD-CUT");
            using var c = _factory.CreateAuthorizedClient();
            _factory.Clock.FixedNow = BeforeCutoff;
            Guid draft = IdOf((await CreateAsync(c, s, s.LocA, 30)).Ok());
            Guid confirmed = IdOf((await CreateAsync(c, s, s.LocB, 40)).Ok());
            (await Act(c, "Submit", confirmed)).Ok();
            (await Act(c, "Confirm", confirmed)).Ok();
            (await c.PostFormAsync("/api/Order/LockByMealDate", ("MealDate", MealDate))).Fail("Chưa tới giờ chốt");

            _factory.Clock.FixedNow = AtCutoff;
            var line = (await LinesAsync(draft))[0];
            (await PutJson(c, "/api/Order/UpdateLines", new { id = draft, listLine = new[] { new { id = line.Id, quantity = 31m } } })).Fail("quá giờ chốt");
            (await Act(c, "Submit", draft)).Fail("quá giờ chốt");
            (await Act(c, "Cancel", confirmed, ("Reason", "Khách hủy muộn"))).Fail("quá giờ chốt");
            var detail = (await c.GetResultAsync($"/api/Order/GetDetail?id={confirmed}")).Ok();
            detail.GetProperty("isAfterCutoff").GetBoolean().Should().BeTrue();
            detail.GetProperty("allowedActions").EnumerateArray().Select(x => x.GetString()).Should().BeEquivalentTo(["lock", "requestChange"], "BE2.2: sau giờ chốt xin sửa qua duyệt");

            var summary = (await c.PostFormAsync("/api/Order/LockByMealDate", ("MealDate", MealDate))).Ok();
            summary.GetProperty("lockedCount").GetInt32().Should().Be(1);
            summary.GetProperty("listNotConfirmedNumber").GetArrayLength().Should().Be(1);
            (await OrderAsync(confirmed)).Status.Should().Be("Locked");
            (await OrderAsync(draft)).Status.Should().Be("Draft", "đơn chưa xác nhận không bị khóa – điều phối xử lý tay");
            (await Act(c, "Cancel", draft, ("Reason", "Không chốt kịp"))).Ok();

            // Tạo đơn ngày đó sau giờ chốt → chặn; ngày sau vẫn tạo được.
            var other = await SeedAsync("ORD-CUT2");
            (await CreateAsync(c, other, other.LocA, 10)).Fail("quá giờ chốt");
        }

        /// <summary>
        /// BIZ-2.1 (D-77 – B1): trước giờ chốt sửa số không đi duyệt (bỏ ORDER_DEVIATION); lệch &gt; 30% so trung bình 4 tuần cùng thứ của điểm giao → chỉ cảnh báo + ghi Note.
        /// Lịch sử điểm A: 2 tuần trước cùng thứ đơn Đã giao 100 suất (tuần nghỉ không tính).
        /// </summary>
        [Fact]
        public async Task Changes_before_cutoff_apply_without_approval_and_warn_against_four_week_average()
        {
            var s = await SeedAsync("ORD-DEV");
            await _factory.SeedAsync(db =>
            {
                foreach (int w in new[] { 1, 3 })
                {
                    Guid past = Guid.NewGuid();
                    db.Order.Add(new Order { Id = past, OrderNumber = $"DH-DEV-{w}", OrderNo = $"DH-DEV-{w}", CustomerId = s.Customer, DeliveryLocationId = s.LocA, OrderDate = MealDate.AddDays(-7 * w - 1), MealDate = MealDate.AddDays(-7 * w), DeliveryDate = MealDate.AddDays(-7 * w), Status = "Delivered", TotalQuantity = 100 });
                    db.OrderLine.Add(new OrderLine { Id = Guid.NewGuid(), OrderId = past, MealType = "TRUA", Quantity = 100 });
                }
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();
            Guid id = IdOf((await CreateAsync(c, s, s.LocA, 100)).Ok());
            Guid lineId = (await LinesAsync(id))[0].Id;
            var confirmed = (await Act(c, "Confirm", id)).Ok();
            confirmed.GetProperty("listWarning").EnumerateArray().Should().BeEmpty("100 = trung bình");

            // +30% đúng ngưỡng → không cảnh báo.
            var applied = (await PutJson(c, "/api/Order/UpdateLines", new { id, listLine = new[] { new { id = lineId, quantity = 130m } } })).Ok();
            applied.GetProperty("approvalRequested").GetBoolean().Should().BeFalse();
            applied.GetProperty("listWarning").EnumerateArray().Should().BeEmpty();

            // +100% → ghi ngay, chỉ cảnh báo + Note.
            var big = await PutJson(c, "/api/Order/UpdateLines", new { id, listLine = new[] { new { id = lineId, quantity = 200m } } });
            big.Ok().GetProperty("approvalRequested").GetBoolean().Should().BeFalse();
            big.Message.Should().Contain("lệch 100% so với trung bình 4 tuần cùng thứ của điểm giao (100 suất)");
            (await LinesAsync(id))[0].Quantity.Should().Be(200);
            var order = await OrderAsync(id);
            (order.TotalQuantity, order.Status).Should().Be((200m, "Confirmed"));
            order.Note.Should().Contain("[LECH_TB_4_TUAN]");
            (await _factory.QueryAsync(db => db.ApprovalRequest.CountAsync(a => a.BusinessId == id))).Should().Be(0);
        }

        [Fact]
        public async Task Price_resolution_contract_policy_then_customer_policy_then_contract_meal()
        {
            var s = await SeedAsync("ORD-PRICE");
            await _factory.SeedAsync(db =>
            {
                db.PricePolicy.AddRange(
                    new PricePolicy { Id = Guid.NewGuid(), Code = "PP-KH-" + s.Customer.ToString("N")[..6], CustomerId = s.Customer, MealType = "TRUA", UnitPrice = 45000, Status = "Active", EffectiveFrom = new DateTime(2026, 1, 1) },
                    new PricePolicy { Id = Guid.NewGuid(), Code = "PP-HD-" + s.Customer.ToString("N")[..6], CustomerId = s.Customer, ContractId = s.Contract, MealType = "TRUA", UnitPrice = 42000, Status = "Active", EffectiveFrom = new DateTime(2026, 12, 10), EffectiveTo = new DateTime(2026, 12, 10) });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();

            JsonElement Trua(JsonElement t) => t.GetProperty("listLocation")[0].GetProperty("listLine").EnumerateArray().Single(l => l.GetProperty("mealType").GetString() == "TRUA");
            var onContractDay = Trua((await c.GetResultAsync($"/api/Order/GetNewTemplate?customerId={s.Customer}&mealDate=2026-12-10")).Ok());
            onContractDay.GetProperty("unitPrice").GetDecimal().Should().Be(42000);
            onContractDay.GetProperty("priceSource").GetString().Should().Be("PricePolicy:Contract");
            var otherDay = Trua((await c.GetResultAsync($"/api/Order/GetNewTemplate?customerId={s.Customer}&mealDate=2026-12-11")).Ok());
            otherDay.GetProperty("unitPrice").GetDecimal().Should().Be(45000);
            otherDay.GetProperty("priceSource").GetString().Should().Be("PricePolicy:Customer");
            var sang = (await c.GetResultAsync($"/api/Order/GetNewTemplate?customerId={s.Customer}&mealDate=2026-12-11")).Ok()
                .GetProperty("listLocation")[0].GetProperty("listLine").EnumerateArray().Single(l => l.GetProperty("mealType").GetString() == "SANG");
            sang.GetProperty("priceSource").GetString().Should().Be("ContractMeal");

            Guid id = IdOf((await CreateAsync(c, s, s.LocA, trua: 10, sang: 2)).Ok());
            var lines = await LinesAsync(id);
            lines.Single(l => l.MealType == "TRUA").UnitPrice.Should().Be(42000);
            lines.Single(l => l.MealType == "SANG").UnitPrice.Should().Be(20000);
            (await OrderAsync(id)).TotalAmount.Should().Be(10 * 42000m + 2 * 20000m);
        }

        [Fact]
        public async Task No_active_contract_is_a_business_error()
        {
            var s = await SeedAsync("ORD-NOCT");
            using var c = _factory.CreateAuthorizedClient();
            (await c.GetResultAsync($"/api/Order/GetNewTemplate?customerId={s.Customer}&mealDate=2025-06-01")).Fail("không có hợp đồng");
        }

        [Fact]
        public void Approval_payload_wrapper_is_backward_compatible()
        {
            var plain = AppDelicare.Services.Implement.ApprovalRequestService.UnwrapPayload("{\"Id\":\"x\",\"Lines\":[]}");
            plain.State.Should().Be("{\"Id\":\"x\",\"Lines\":[]}");
            plain.Proposal.Should().BeNull();
            var wrapped = AppDelicare.Services.Implement.ApprovalRequestService.UnwrapPayload("{\"state\":\"{\\\"a\\\":1.50}\",\"proposal\":\"[1]\"}");
            wrapped.State.Should().Be("{\"a\":1.50}");
            wrapped.Proposal.Should().Be("[1]");
        }
    }
}
