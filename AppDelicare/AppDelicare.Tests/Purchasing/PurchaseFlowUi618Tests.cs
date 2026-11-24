using System.Net.Http.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Purchasing
{
    /// <summary>UI-6.18 (D-99): quy tắc gợi ý NCC (ưu tiên / tăng giá ≥ 10% có NCC rẻ hơn / không giao kịp / tối thiểu / hết hiệu lực), đề nghị mua tạo kèm dòng,
    /// màn Chọn nhà cung cấp (lập PO từ lựa chọn – tự duyệt khi đủ điều kiện, còn lại gửi duyệt) và gửi nhiều PO.</summary>
    public class PurchaseFlowUi618Tests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;
        private static readonly DateTime Today = new(2031, 8, 4);

        public PurchaseFlowUi618Tests(InMemoryApiFactory factory)
        {
            _factory = factory;
            _factory.Clock.FixedNow = Today.AddHours(9);
        }

        private static async Task<LegacyResult> Post(HttpClient c, string url, object body) => await LegacyResult.ReadAsync(await c.PostAsJsonAsync(url, body));

        [Fact]
        public async Task Supplier_rule_create_with_lines_pick_board_and_send_many()
        {
            Guid kg = Guid.NewGuid(), beef = Guid.NewGuid(), fish = Guid.NewGuid(), rice = Guid.NewGuid(), a = Guid.NewGuid(), b = Guid.NewGuid(), slow = Guid.NewGuid();
            string k = kg.ToString("N")[..5].ToUpperInvariant();
            await _factory.SeedAsync(db =>
            {
                db.UnitMeasure.Add(new UnitMeasure { Id = kg, Code = "kg" + k, Name = "kg" });
                db.Ingredient.AddRange(new Ingredient { Id = beef, Code = "BO" + k, Name = "Bò " + k, BaseUnitId = kg }, new Ingredient { Id = fish, Code = "CA" + k, Name = "Cá " + k, BaseUnitId = kg },
                    new Ingredient { Id = rice, Code = "GAO" + k, Name = "Gạo " + k, BaseUnitId = kg });
                db.Supplier.AddRange(new Supplier { Id = a, Code = "A" + k, Name = "NCC A " + k, Status = "Active" }, new Supplier { Id = b, Code = "B" + k, Name = "NCC B " + k, Status = "Active" },
                    new Supplier { Id = slow, Code = "S" + k, Name = "NCC chậm " + k, Status = "Active" });
                // Bò: A ưu tiên 200k (trước đó 150k → +33%), B 180k rẻ hơn → chọn B.
                db.SupplierMaterial.AddRange(
                    new SupplierMaterial { Id = Guid.NewGuid(), SupplierId = a, IngredientId = beef, IsPreferred = true, UnitPrice = 150000, PurchaseUomId = kg, EffectiveFrom = Today.AddDays(-60), EffectiveTo = Today.AddDays(-11), Status = "Active" },
                    new SupplierMaterial { Id = Guid.NewGuid(), SupplierId = a, IngredientId = beef, IsPreferred = true, UnitPrice = 200000, PurchaseUomId = kg, EffectiveFrom = Today.AddDays(-10), Status = "Active" },
                    new SupplierMaterial { Id = Guid.NewGuid(), SupplierId = b, IngredientId = beef, UnitPrice = 180000, PurchaseUomId = kg, EffectiveFrom = Today.AddDays(-10), Status = "Active" },
                    // Cá: A ưu tiên, tối thiểu 20 kg → cần 12 làm tròn 20.
                    new SupplierMaterial { Id = Guid.NewGuid(), SupplierId = a, IngredientId = fish, IsPreferred = true, UnitPrice = 90000, PurchaseUomId = kg, MinimumOrderQuantity = 20, EffectiveFrom = Today.AddDays(-5), Status = "Active" },
                    // Gạo: chỉ NCC chậm giao 10 ngày → cần chọn NCC (không giao kịp); NCC B hết hiệu lực giá.
                    new SupplierMaterial { Id = Guid.NewGuid(), SupplierId = slow, IngredientId = rice, IsPreferred = true, UnitPrice = 20000, PurchaseUomId = kg, LeadTimeDays = 10, Status = "Active" },
                    new SupplierMaterial { Id = Guid.NewGuid(), SupplierId = b, IngredientId = rice, UnitPrice = 19000, PurchaseUomId = kg, EffectiveTo = Today.AddDays(-1), Status = "Active" });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();
            DateTime need = Today.AddDays(3);
            (await Post(c, "/api/PurchaseRequest/Create", new { requiredDate = need, listLine = Array.Empty<object>() })).Fail("chưa có dòng");
            (await Post(c, "/api/PurchaseRequest/Create", new { requiredDate = need, listLine = new[] { new { ingredientId = beef, quantity = 0m } } })).Fail("lớn hơn 0");

            var sug = (await Post(c, "/api/PurchaseRequest/GetSupplierSuggestion", new { requiredDate = need, listLine = new object[] { new { ingredientId = beef, quantity = 10m }, new { ingredientId = fish, quantity = 12m }, new { ingredientId = rice, quantity = 50m } } }))
                .ListData.EnumerateArray().ToList();
            sug[0].GetProperty("supplierId").GetGuid().Should().Be(b, "NCC ưu tiên tăng giá ≥ 10% và có NCC rẻ hơn đúng hẹn");
            sug[0].GetProperty("reason").GetString().Should().Contain("tăng giá");
            sug[1].GetProperty("supplierId").GetGuid().Should().Be(a);
            sug[1].GetProperty("orderQuantity").GetDecimal().Should().Be(20, "làm tròn lên mức tối thiểu");
            sug[2].GetProperty("supplierId").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
            sug[2].GetProperty("reason").GetString().Should().Contain("Không giao kịp").And.Contain("Hết hiệu lực giá");

            // Tạo kèm dòng: gộp nguyên liệu trùng, số / ngày lập máy chủ điền, Nháp.
            var made = (await Post(c, "/api/PurchaseRequest/Create", new { requiredDate = need, note = "[DEMO] UI-6.18", listLine = new object[] {
                new { ingredientId = beef, quantity = 6m }, new { ingredientId = beef, quantity = 4m }, new { ingredientId = fish, quantity = 12m }, new { ingredientId = rice, quantity = 50m } } })).Ok();
            Guid pr = made.GetProperty("purchaseRequest").GetProperty("id").GetGuid();
            made.GetProperty("purchaseRequest").GetProperty("status").GetString().Should().Be("Draft");
            made.GetProperty("purchaseRequest").GetProperty("requestNumber").GetString().Should().StartWith("MH-");
            (await _factory.QueryAsync(db => db.PurchaseRequestLine.AsNoTracking().Where(l => l.PurchaseRequestId == pr && !l.IsDeleted).ToListAsync()))
                .Should().HaveCount(3).And.Contain(l => l.IngredientId == beef && l.Quantity == 10);

            // Duyệt 2 cấp → hệ thống tự lập PO cho bò (B) + cá (A); gạo chờ chọn NCC.
            await c.ApprovePurchaseRequestAsync(pr);
            var pos = await _factory.QueryAsync(db => db.PurchaseOrder.AsNoTracking().Where(p => p.PurchaseRequestId == pr).ToListAsync());
            pos.Select(p => p.SupplierId).Should().BeEquivalentTo([a, b]);
            (await _factory.QueryAsync(db => db.PurchaseRequest.AsNoTracking().SingleAsync(p => p.Id == pr))).Status.Should().Be("Approved", "còn dòng gạo chờ chọn NCC");
            pos.Single(p => p.SupplierId == a).Status.Should().Be("Approved", "mọi dòng NCC ưu tiên, dưới 50 triệu → tự duyệt");
            pos.Single(p => p.SupplierId == b).Status.Should().Be("Draft", "NCC không ưu tiên → phải duyệt");

            var board = (await c.GetResultAsync("/api/PurchaseOrder/GetSupplierPickBoard")).Ok();
            var riceLine = board.GetProperty("listLine").EnumerateArray().Single(l => l.GetProperty("purchaseRequestId").GetGuid() == pr);
            riceLine.GetProperty("ingredientId").GetGuid().Should().Be(rice);
            Guid riceLineId = riceLine.GetProperty("purchaseRequestLineId").GetGuid();
            var fromPick = (await Post(c, "/api/PurchaseOrder/CreateFromPick", new { listLine = new[] { new { purchaseRequestLineId = riceLineId, supplierId = slow } } })).Ok();
            fromPick.GetProperty("listPurchaseOrder").GetArrayLength().Should().Be(1);
            (await _factory.QueryAsync(db => db.PurchaseRequest.AsNoTracking().SingleAsync(p => p.Id == pr))).Status.Should().Be("Converted", "hết dòng chờ chọn NCC");

            var poA = pos.Single(p => p.SupplierId == a).Id;
            var sent = (await Post(c, "/api/PurchaseOrder/SendMany", new { listId = new[] { poA } })).Ok();
            sent.GetInt32().Should().Be(1);
            (await _factory.QueryAsync(db => db.PurchaseOrder.AsNoTracking().SingleAsync(p => p.Id == poA))).Status.Should().Be("Sent");
        }
    }
}
