using System.Net.Http.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.MasterData
{
    /// <summary>UI-6.19 (D-100): luật lưu giá NCC (tự kết thúc giá cũ, sửa giá đã dùng bị chặn, đơn vị quy đổi được, đơn giá &gt; 0, ưu tiên chuyển + tên),
    /// lịch sử / ma trận (quy về ĐVT kho, cờ việc cần làm), gia hạn hàng loạt, nhập báo giá Excel (xem trước / áp dụng).</summary>
    public class SupplierPriceRulesTests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public SupplierPriceRulesTests(InMemoryApiFactory factory)
        {
            _factory = factory;
            _factory.Clock.FixedNow = new DateTime(2031, 5, 10, 9, 0, 0);
        }

        [Fact]
        public async Task Price_rules_history_matrix_extend_and_quote_import()
        {
            Guid kg = Guid.NewGuid(), box = Guid.NewGuid(), lit = Guid.NewGuid(), ing = Guid.NewGuid(), s1 = Guid.NewGuid(), s2 = Guid.NewGuid();
            string k = kg.ToString("N")[..5].ToUpperInvariant();
            await _factory.SeedAsync(db =>
            {
                db.UnitMeasure.AddRange(new UnitMeasure { Id = kg, Code = "kg" + k, Symbol = "kg", Name = "kg" }, new UnitMeasure { Id = box, Code = "th" + k, Name = "thùng 10 kg", UomType = "PACK" }, new UnitMeasure { Id = lit, Code = "l" + k, Name = "lít" });
                db.UomConversion.Add(new UomConversion { Id = Guid.NewGuid(), FromUnitId = box, ToUnitId = kg, FromUomId = box, ToUomId = kg, ConversionFactor = 10, IsActive = true, Status = "Active" });
                db.Ingredient.Add(new Ingredient { Id = ing, Code = "NL" + k, Name = "Cải " + k, BaseUnitId = kg, Status = "Active" });
                db.Supplier.AddRange(new Supplier { Id = s1, Code = "A" + k, Name = "NCC A " + k, Status = "Active" }, new Supplier { Id = s2, Code = "B" + k, Name = "NCC B " + k, Status = "Active" });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();
            (await c.PostFormAsync("/api/SupplierMaterial/Insert", ("SupplierId", s1), ("IngredientId", ing), ("PurchaseUomId", kg), ("EffectiveFrom", new DateTime(2031, 5, 1)))).Fail("đơn giá");
            (await c.PostFormAsync("/api/SupplierMaterial/Insert", ("SupplierId", s1), ("IngredientId", ing), ("UnitPrice", 50000m), ("PurchaseUomId", lit), ("EffectiveFrom", new DateTime(2031, 5, 1)))).Fail("quy đổi");
            (await c.PostFormAsync("/api/SupplierMaterial/Insert", ("SupplierId", s1), ("IngredientId", ing), ("UnitPrice", 200000m), ("PurchaseUomId", box), ("EffectiveFrom", new DateTime(2031, 5, 1)),
                ("IsPreferred", true), ("DeliveryFrequency", "t2, T4 ; t6"), ("Status", "Active"))).Ok();
            var first = await _factory.QueryAsync(db => db.SupplierMaterial.AsNoTracking().SingleAsync(x => x.SupplierId == s1 && x.IngredientId == ing));
            first.DeliveryFrequency.Should().Be("T2,T4,T6");
            // Giá mới cùng NCC × NL từ 20/05 → giá cũ tự kết thúc 19/05 (không báo trùng); cùng ngày bắt đầu → báo.
            var added = await c.PostFormAsync("/api/SupplierMaterial/SaveNewPrice", ("SupplierId", s1), ("IngredientId", ing), ("UnitPrice", 230000m), ("PurchaseUomId", box), ("EffectiveFrom", new DateTime(2031, 5, 20)), ("IsPreferred", true), ("Status", "Active"));
            added.Ok(); added.Message.Should().Contain("tự kết thúc");
            (await _factory.QueryAsync(db => db.SupplierMaterial.AsNoTracking().SingleAsync(x => x.Id == first.Id))).EffectiveTo.Should().Be(new DateTime(2031, 5, 19));
            (await c.PostFormAsync("/api/SupplierMaterial/SaveNewPrice", ("SupplierId", s1), ("IngredientId", ing), ("UnitPrice", 1m), ("PurchaseUomId", kg), ("EffectiveFrom", new DateTime(2031, 5, 20)))).Fail("sửa giá ngày 20/05/2031");
            // Sửa đơn giá của giá đã áp dụng (01/05 ≤ hôm nay 10/05) → chặn.
            (await c.PutFormAsync("/api/SupplierMaterial/Update", ("Id", first.Id), ("SupplierId", s1), ("IngredientId", ing), ("UnitPrice", 1m), ("PurchaseUomId", box), ("EffectiveFrom", new DateTime(2031, 5, 1)))).Fail("lưu thành giá mới");
            // NCC B ưu tiên → báo bỏ ưu tiên NCC A.
            var pref = await c.PostFormAsync("/api/SupplierMaterial/Insert", ("SupplierId", s2), ("IngredientId", ing), ("UnitPrice", 19000m), ("PurchaseUomId", kg), ("EffectiveFrom", new DateTime(2031, 5, 1)), ("IsPreferred", true), ("Status", "Active"));
            pref.Ok(); pref.Message.Should().Contain("NCC A " + k);

            var hist = (await c.GetResultAsync($"/api/SupplierMaterial/GetPriceHistory?supplierId={s1}&ingredientId={ing}")).ListData.EnumerateArray().ToList();
            hist.Should().HaveCount(2);
            hist[0].GetProperty("pricePerBase").GetDecimal().Should().Be(23000, "230.000 / thùng 10 kg");
            hist[0].GetProperty("changePct").GetDecimal().Should().Be(15);

            var mx = (await c.GetResultAsync("/api/SupplierMaterial/GetMatrix?Date=2031-05-25")).Ok();
            var row = mx.GetProperty("listRow").EnumerateArray().Single(r => r.GetProperty("ingredientId").GetGuid() == ing);
            row.GetProperty("listFlag").EnumerateArray().Select(f => f.GetString()).Should().Contain(["PRICE_UP"]);
            var cells = row.GetProperty("listCell").EnumerateArray().ToList();
            cells.Single(x => x.GetProperty("supplierId").GetGuid() == s2).GetProperty("isCheapest").GetBoolean().Should().BeTrue("19.000 / kg < 23.000 / kg");
            var cmp = (await c.GetResultAsync($"/api/SupplierMaterial/GetCompare?ingredientId={ing}&date=2031-05-25")).ListData.EnumerateArray().ToList();
            cmp.Should().HaveCount(2); cmp[0].GetProperty("supplierId").GetGuid().Should().Be(s2);

            var newer = await _factory.QueryAsync(db => db.SupplierMaterial.AsNoTracking().SingleAsync(x => x.SupplierId == s1 && x.EffectiveFrom == new DateTime(2031, 5, 20)));
            var ext = await LegacyResult.ReadAsync(await c.PostAsJsonAsync("/api/SupplierMaterial/ExtendMany", new { listId = new[] { newer.Id }, effectiveTo = new DateTime(2031, 12, 31) }));
            ext.Ok().GetInt32().Should().Be(1);

            // Nhập báo giá Excel của NCC A: tăng giá → xem trước UP, áp dụng tạo giá mới.
            var tpl = await c.GetAsync($"/api/SupplierMaterial/GetQuoteTemplate?supplierId={s1}");
            using var wb = new XLWorkbook(await tpl.Content.ReadAsStreamAsync());
            var ws = wb.Worksheet(1);
            int r = Enumerable.Range(2, 50).First(i => ws.Cell(i, 1).GetString() == "NL" + k);
            ws.Cell(r, 5).Value = 260000; ws.Cell(r, 6).Value = "th" + k; ws.Cell(r, 7).Value = "01/06/2031";
            using var ms = new MemoryStream(); wb.SaveAs(ms);
            async Task<LegacyResult> Import(bool apply)
            {
                using var form = new MultipartFormDataContent { { new StringContent(s1.ToString()), "supplierId" }, { new StringContent(apply ? "true" : "false"), "apply" }, { new ByteArrayContent(ms.ToArray()), "file", "bao-gia.xlsx" } };
                return await LegacyResult.ReadAsync(await c.PostAsync("/api/SupplierMaterial/ImportQuote", form));
            }
            var pv = (await Import(false)).Ok();
            pv.GetProperty("up").GetInt32().Should().Be(1);
            (await Import(true)).Ok().GetProperty("applied").GetInt32().Should().Be(1);
            (await _factory.QueryAsync(db => db.SupplierMaterial.AsNoTracking().CountAsync(x => x.SupplierId == s1 && x.IngredientId == ing && !x.IsDeleted))).Should().Be(3);
        }
    }
}
