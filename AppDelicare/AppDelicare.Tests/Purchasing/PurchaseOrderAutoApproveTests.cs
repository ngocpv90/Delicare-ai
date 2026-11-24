using System.Net.Http.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Purchasing
{
    /// <summary>
    /// BIZ-2.2 (D-77 – B3, C2): lập PO từ đề nghị mua Đã duyệt → PO đúng NCC gợi ý, đơn giá bảng giá, số = đề nghị, tổng &lt; 50 triệu → tự Đã duyệt.
    /// Thiếu đơn giá / ≥ 50 triệu → Nháp, gửi duyệt như cũ. Sửa dòng PO đã tự duyệt → về Nháp.
    /// </summary>
    public class PurchaseOrderAutoApproveTests : IClassFixture<InMemoryApiFactory>
    {
        private static readonly DateTime Today = new(2027, 2, 1);
        private readonly InMemoryApiFactory _factory;

        public PurchaseOrderAutoApproveTests(InMemoryApiFactory factory)
        {
            _factory = factory;
            _factory.Clock.FixedNow = Today.AddHours(9);
        }

        private async Task<(Guid Request, Guid Kg, Guid Meat, Guid SupplierA, Guid SupplierB)> SeedAsync(string code, decimal meatQty, decimal? vegPrice)
        {
            Guid kg = Guid.NewGuid(), meat = Guid.NewGuid(), veg = Guid.NewGuid(), a = Guid.NewGuid(), b = Guid.NewGuid(), pr = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.UnitMeasure.Add(new UnitMeasure { Id = kg, Code = "KG-" + code, Name = "kg" });
                db.Ingredient.AddRange(
                    new Ingredient { Id = meat, Code = "THIT-" + code, Name = "Thịt " + code, BaseUnitId = kg },
                    new Ingredient { Id = veg, Code = "RAU-" + code, Name = "Rau " + code, BaseUnitId = kg });
                db.Supplier.AddRange(
                    new Supplier { Id = a, Code = "SA-" + code, Name = "NCC A " + code, Status = "Active" },
                    new Supplier { Id = b, Code = "SB-" + code, Name = "NCC B " + code, Status = "Active" });
                db.SupplierMaterial.AddRange(
                    new SupplierMaterial { Id = Guid.NewGuid(), SupplierId = a, IngredientId = meat, IsPreferred = true, UnitPrice = 100000, PurchaseUomId = kg, Status = "Active" },
                    new SupplierMaterial { Id = Guid.NewGuid(), SupplierId = b, IngredientId = veg, IsPreferred = true, UnitPrice = vegPrice, PurchaseUomId = kg, Status = "Active" });
                db.PurchaseRequest.Add(new PurchaseRequest { Id = pr, RequestNumber = "PH-" + code, RequestNo = "PH-" + code, RequestDate = Today, RequiredDate = Today.AddDays(2), Status = "Draft" });
                db.PurchaseRequestLine.AddRange(
                    new PurchaseRequestLine { Id = Guid.NewGuid(), PurchaseRequestId = pr, IngredientId = meat, Quantity = meatQty, UnitId = kg, UomId = kg },
                    new PurchaseRequestLine { Id = Guid.NewGuid(), PurchaseRequestId = pr, IngredientId = veg, Quantity = 10, UnitId = kg, UomId = kg });
                return Task.CompletedTask;
            });
            return (pr, kg, meat, a, b);
        }

        private Task<PurchaseOrder> PoAsync(Guid supplier, Guid request)
            => _factory.QueryAsync(db => db.PurchaseOrder.AsNoTracking().SingleAsync(p => p.SupplierId == supplier && p.PurchaseRequestId == request));

        [Fact]
        public async Task Po_matching_request_is_auto_approved_others_stay_draft_and_edit_reverts_to_draft()
        {
            using var c = _factory.CreateAuthorizedClient();
            var s = await SeedAsync("AUTO1", 20m, null);
            await c.ApprovePurchaseRequestAsync(s.Request);
            // UI-6.18 B, C (D-99): duyệt cấp cuối → tự lập PO cho dòng có NCC đủ điều kiện; rau – bảng giá chưa có đơn giá → không lập PO, chờ "Chọn nhà cung cấp".
            (await c.PostFormAsync("/api/PurchaseRequest/ConvertToPurchaseOrder", ("Id", s.Request))).Fail("Chưa có đơn giá");
            var poA = await PoAsync(s.SupplierA, s.Request);
            (poA.Status, poA.TotalAmount).Should().Be(("Approved", 2_000_000m));
            poA.Note.Should().Contain("[TU_DUYET]").And.Contain("PH-AUTO1");
            (await _factory.QueryAsync(db => db.PurchaseOrder.AsNoTracking().CountAsync(p => p.SupplierId == s.SupplierB && p.PurchaseRequestId == s.Request))).Should().Be(0);
            var board = (await c.GetResultAsync("/api/PurchaseOrder/GetSupplierPickBoard")).Ok().GetProperty("listLine").EnumerateArray().ToList();
            board.Should().Contain(l => l.GetProperty("purchaseRequestId").GetGuid() == s.Request && l.GetProperty("reason").GetString()!.Contains("đơn giá"));

            // Sửa dòng PO đã tự duyệt → về Nháp, phải gửi duyệt lại.
            var edited = await LegacyResult.ReadAsync(await c.PutAsJsonAsync("/api/PurchaseOrder/UpdateLines", new { id = poA.Id, listLine = new[] { new { ingredientId = s.Meat, quantity = 25m } } }));
            edited.Ok();
            edited.Message.Should().Contain("về Nháp");
            (await PoAsync(s.SupplierA, s.Request)).Status.Should().Be("Draft");
        }

        [Fact]
        public async Task Po_at_or_above_director_threshold_is_not_auto_approved()
        {
            using var c = _factory.CreateAuthorizedClient();
            var s = await SeedAsync("AUTO2", 500m, 20000m);
            await c.ApprovePurchaseRequestAsync(s.Request);
            (await c.PostFormAsync("/api/PurchaseRequest/ConvertToPurchaseOrder", ("Id", s.Request))).Ok();
            var poA = await PoAsync(s.SupplierA, s.Request);
            (poA.Status, poA.TotalAmount).Should().Be(("Draft", 50_000_000m), "≥ 50 triệu → Giám đốc duyệt");
            (await PoAsync(s.SupplierB, s.Request)).Status.Should().Be("Approved");
        }
    }
}
