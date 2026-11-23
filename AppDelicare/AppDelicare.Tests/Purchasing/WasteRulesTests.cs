using AppDelicare.Model.Entity;
using AppDelicare.Services.Implement;
using AppDelicare.Tests.Integration;
using FluentAssertions;

namespace AppDelicare.Tests.Purchasing
{
    /// <summary>
    /// BIZ-1.1 (D-77 a): hao hụt 2 tầng, công thức chia như file vận hành thật W3 – Thịt hầm đậu đỏ: tinh 70 g, chế biến 35 %, sơ chế 5 %
    /// → sau sơ chế 107,7 g, lượng mua 113,4 g / suất. Giá vốn công thức tính trên lượng mua.
    /// </summary>
    public class WasteRulesTests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public WasteRulesTests(InMemoryApiFactory factory) => _factory = factory;

        [Fact]
        public void Two_tier_waste_divides_like_the_real_file()
        {
            Math.Round(WasteRules.AfterPrep(70m, 35m), 1).Should().Be(107.7m);
            Math.Round(WasteRules.Gross(70m, 5m, 35m), 1).Should().Be(113.4m);
            WasteRules.Gross(70m, null, null).Should().Be(70m);
            WasteRules.Gross(90m, 10m, null).Should().Be(100m, "chỉ hao hụt sơ chế: 90 ÷ 0,9");
            WasteRules.Gross(10m, 100m, -5m).Should().Be(10m, "% ngoài [0, 100) bị bỏ qua");
        }

        [Fact]
        public async Task Recipe_cost_uses_purchase_quantity_with_ingredient_default_prep_waste()
        {
            Guid unit = Guid.NewGuid(), meat = Guid.NewGuid(), dish = Guid.NewGuid(), recipe = Guid.NewGuid(), supplier = Guid.NewGuid();
            string code = "W3-" + recipe.ToString("N")[..6];
            await _factory.SeedAsync(db =>
            {
                db.UnitMeasure.Add(new UnitMeasure { Id = unit, Code = code, Name = "g" });
                // % sơ chế để trống trên dòng → lấy Ingredient.StandardWastePercent 5 %.
                db.Ingredient.Add(new Ingredient { Id = meat, Code = code, Name = "Thịt " + code, BaseUnitId = unit, StandardWastePercent = 5 });
                db.Dish.Add(new Dish { Id = dish, Code = code, Name = "Thịt hầm đậu đỏ " + code });
                db.Recipe.Add(new Recipe { Id = recipe, DishId = dish, VersionNo = 1, VersionCode = code, Status = "Draft", StandardOutputQty = 1 });
                db.RecipeLine.Add(new RecipeLine { Id = Guid.NewGuid(), RecipeId = recipe, IngredientId = meat, Quantity = 70, UnitId = unit, UomId = unit, ExpectedWasteRate = 35, SortOrder = 1 });
                db.Supplier.Add(new Supplier { Id = supplier, Code = code, Name = "NCC " + code, Status = "Active" });
                db.SupplierMaterial.Add(new SupplierMaterial { Id = Guid.NewGuid(), SupplierId = supplier, IngredientId = meat, UnitPrice = 100, PurchaseUomId = unit, IsPreferred = true });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();
            var cost = (await c.GetResultAsync($"/api/Recipe/CalculateCost?id={recipe}&mealCount=1")).Ok();
            var line = cost.GetProperty("listLine")[0];
            line.GetProperty("quantity").GetDecimal().Should().Be(70);
            Math.Round(line.GetProperty("quantityAfterPrep").GetDecimal(), 1).Should().Be(107.7m);
            Math.Round(line.GetProperty("grossQuantity").GetDecimal(), 1).Should().Be(113.4m);
            line.GetProperty("prepWastePercent").GetDecimal().Should().Be(5);
            // 113,36 g × 100 đ = 11 336 đ (trước đây tính trên 70 g = 7 000 đ).
            line.GetProperty("amount").GetDecimal().Should().Be(Math.Round(70m / 0.65m / 0.95m * 100m, 2));
        }
    }
}
