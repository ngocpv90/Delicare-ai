using System.Net.Http.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Services.Interface;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.MasterData
{
    /// <summary>UI-6.22 (D-103): đơn vị đóng gói có kích cỡ lưu cùng quy đổi (1 giao dịch), trùng kích cỡ dùng lại, chặn đổi hệ số đơn vị đã dùng, ô chọn chỉ đơn vị quy đổi được.</summary>
    public class UnitPackTests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public UnitPackTests(InMemoryApiFactory factory) => _factory = factory;

        private static async Task<LegacyResult> Save(HttpClient c, object body) => await LegacyResult.ReadAsync(await c.PostAsJsonAsync("/api/UnitMeasure/SaveWithConversion", body));

        [Fact]
        public async Task Pack_unit_saves_conversion_reuses_same_size_blocks_factor_change_when_used()
        {
            Guid kg = Guid.NewGuid(), lit = Guid.NewGuid();
            string k = kg.ToString("N")[..5].ToUpperInvariant();
            await _factory.SeedAsync(db =>
            {
                db.UnitMeasure.AddRange(new UnitMeasure { Id = kg, Code = "kg" + k, Symbol = "kg", Name = "kg", UomType = "BASE", Status = "Active" }, new UnitMeasure { Id = lit, Code = "l" + k, Symbol = "l", Name = "lít", UomType = "BASE", Status = "Active" });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();
            (await Save(c, new { kind = "PACK", packType = "rổ", quantity = 10, baseUnitId = kg })).Fail("loại bao bì");
            (await Save(c, new { kind = "PACK", packType = "thùng", quantity = 0, baseUnitId = kg })).Fail("lớn hơn 0");
            var made = (await Save(c, new { kind = "PACK", packType = "thùng", quantity = 10, baseUnitId = kg })).Ok();
            Guid box = made.GetProperty("id").GetGuid();
            made.GetProperty("name").GetString().Should().Be("thùng 10 kg");
            var conv = await _factory.QueryAsync(db => db.UomConversion.AsNoTracking().SingleAsync(x => x.FromUnitId == box));
            conv.ToUnitId.Should().Be(kg);
            conv.ConversionFactor.Should().Be(10);
            conv.EffectiveFrom.Should().BeNull("hệ số đóng gói luôn hiệu lực");
            var again = (await Save(c, new { kind = "PACK", packType = "thùng", quantity = 10, baseUnitId = kg })).Ok();
            again.GetProperty("id").GetGuid().Should().Be(box, "trùng kích cỡ → dùng lại đơn vị có sẵn");
            again.GetProperty("reused").GetBoolean().Should().BeTrue();
            (await Save(c, new { kind = "PACK", packType = "thùng", quantity = 20, baseUnitId = kg })).Ok().GetProperty("name").GetString().Should().Be("thùng 20 kg");

            using (var scope = _factory.Services.CreateScope())
            {
                (await scope.ServiceProvider.GetRequiredService<IUomConversionService>().ConvertAsync(3m, box, kg)).Should().Be(30m, "3 thùng 10 kg = 30 kg");
            }
            var choice = (await c.GetResultAsync($"/api/UnitMeasure/GetForIngredient?baseUnitId={kg}")).Ok().GetProperty("listUnit").EnumerateArray().ToList();
            choice.Select(x => x.GetProperty("id").GetGuid()).Should().Contain([kg, box]).And.NotContain(lit, "lít không quy đổi được về kg");
            choice.Single(x => x.GetProperty("id").GetGuid() == box).GetProperty("factor").GetDecimal().Should().Be(10);

            // Chưa dùng → đổi kích cỡ được; đã dùng (nguyên liệu mua theo thùng) → chặn.
            (await Save(c, new { id = box, kind = "PACK", packType = "thùng", quantity = 12, baseUnitId = kg })).Ok();
            await _factory.SeedAsync(db => { db.Ingredient.Add(new Ingredient { Id = Guid.NewGuid(), Code = "NL" + k, Name = "Cải " + k, BaseUnitId = kg, PurchaseUomId = box, Status = "Active" }); return Task.CompletedTask; });
            (await Save(c, new { id = box, kind = "PACK", packType = "thùng", quantity = 15, baseUnitId = kg })).Fail("không đổi hệ số");
            var usage = (await c.GetResultAsync("/api/UnitMeasure/GetUsage")).ListData.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == box);
            usage.GetProperty("ingredientCount").GetInt32().Should().Be(1);
            usage.GetProperty("kind").GetString().Should().Be("PACK");
            usage.GetProperty("factor").GetDecimal().Should().Be(12);
        }
    }
}
