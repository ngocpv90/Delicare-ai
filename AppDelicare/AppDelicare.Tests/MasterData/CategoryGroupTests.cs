using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;

namespace AppDelicare.Tests.MasterData
{
    /// <summary>UI-6.21 (D-102): nhóm nguyên liệu / nhóm món – đếm bản ghi, chặn trùng tên, chặn ngừng dùng khi còn bản ghi đang dùng, gợi ý theo đa số.</summary>
    public class CategoryGroupTests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public CategoryGroupTests(InMemoryApiFactory factory) => _factory = factory;

        [Fact]
        public async Task Ingredient_groups_count_block_duplicate_and_in_use_and_suggest_defaults()
        {
            Guid g1 = Guid.NewGuid(), g2 = Guid.NewGuid(), kg = Guid.NewGuid(), gram = Guid.NewGuid();
            string k = g1.ToString("N")[..5].ToUpperInvariant();
            await _factory.SeedAsync(db =>
            {
                db.IngredientCategory.AddRange(new IngredientCategory { Id = g1, Code = "G1" + k, Name = "Thịt " + k, Status = "Active" }, new IngredientCategory { Id = g2, Code = "G2" + k, Name = "Rỗng " + k, Status = "Active" });
                db.UnitMeasure.AddRange(new UnitMeasure { Id = kg, Code = "kg" + k, Name = "kg", Status = "Active" }, new UnitMeasure { Id = gram, Code = "g" + k, Name = "g", Status = "Active" });
                db.Ingredient.AddRange(
                    new Ingredient { Id = Guid.NewGuid(), Code = "A" + k, Name = "Bò " + k, IngredientCategoryId = g1, BaseUnitId = kg, StorageType = "CHILLED", Status = "Active" },
                    new Ingredient { Id = Guid.NewGuid(), Code = "B" + k, Name = "Heo " + k, IngredientCategoryId = g1, BaseUnitId = kg, StorageType = "CHILLED", Status = "Active" },
                    new Ingredient { Id = Guid.NewGuid(), Code = "C" + k, Name = "Gia vị " + k, IngredientCategoryId = g1, BaseUnitId = gram, StorageType = "DRY", Status = "Active" },
                    new Ingredient { Id = Guid.NewGuid(), Code = "D" + k, Name = "Cũ " + k, IngredientCategoryId = g1, BaseUnitId = kg, StorageType = "DRY", Status = "Inactive" });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();
            var data = (await c.GetResultAsync("/api/IngredientCategory/GetWithCount")).Ok();
            var list = data.GetProperty("listGroup").EnumerateArray().ToList();
            var a = list.Single(x => x.GetProperty("id").GetGuid() == g1);
            a.GetProperty("activeCount").GetInt32().Should().Be(3);
            a.GetProperty("totalCount").GetInt32().Should().Be(4);
            list.Single(x => x.GetProperty("id").GetGuid() == g2).GetProperty("activeCount").GetInt32().Should().Be(0);

            (await c.PostFormAsync("/api/IngredientCategory/Insert", ("Name", "thịt " + k.ToLowerInvariant()), ("Status", "Active"))).Fail("đã có");
            (await c.PutFormAsync("/api/IngredientCategory/Update", ("Id", g1), ("Name", "Thịt " + k), ("Status", "Inactive"))).Fail("còn 3 nguyên liệu");
            (await c.PutFormAsync("/api/IngredientCategory/Update", ("Id", g2), ("Name", "Rỗng mới " + k), ("Status", "Inactive"))).Ok();
            (await c.PutFormAsync("/api/IngredientCategory/Update", ("Id", g2), ("Name", "Rỗng mới " + k), ("Status", "Active"))).Ok();

            var def = (await c.GetResultAsync($"/api/Ingredient/GetCategoryDefaults?categoryId={g1}")).Ok();
            def.GetProperty("sampleCount").GetInt32().Should().Be(3, "chỉ nguyên liệu đang dùng");
            def.GetProperty("storageType").GetString().Should().Be("CHILLED");
            def.GetProperty("baseUnitId").GetGuid().Should().Be(kg);
            (await c.GetResultAsync($"/api/Ingredient/GetCategoryDefaults?categoryId={g2}")).Ok().GetProperty("sampleCount").GetInt32().Should().Be(0);
        }

        [Fact]
        public async Task Dish_groups_count_and_block_in_use()
        {
            Guid g = Guid.NewGuid();
            string k = g.ToString("N")[..5].ToUpperInvariant();
            await _factory.SeedAsync(db =>
            {
                db.DishCategory.Add(new DishCategory { Id = g, Code = "DG" + k, Name = "Món mặn " + k, Status = "Active" });
                db.Dish.Add(new Dish { Id = Guid.NewGuid(), Code = "M" + k, Name = "Gà " + k, DishCategoryId = g, Status = "Active" });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();
            var list = (await c.GetResultAsync("/api/DishCategory/GetWithCount")).Ok().GetProperty("listGroup").EnumerateArray().ToList();
            list.Single(x => x.GetProperty("id").GetGuid() == g).GetProperty("activeCount").GetInt32().Should().Be(1);
            (await c.PutFormAsync("/api/DishCategory/Update", ("Id", g), ("Name", "Món mặn " + k), ("Status", "Inactive"))).Fail("còn 1 món");
            (await c.PostFormAsync("/api/DishCategory/Insert", ("Name", "MÓN MẶN " + k), ("Status", "Active"))).Fail("đã có");
        }
    }
}
