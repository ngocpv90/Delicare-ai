using System.Net;
using AppDelicare.Model.Entity;
using AppDelicare.Tests.Common;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Menu
{
    /// <summary>UI-6.17 (D-98): form món 5 ô (giá thành chỉ đọc, tự ghi MealCategory / StandardPortion, nhóm món bắt buộc), Dish/Create trả id, chép công thức từ món khác.</summary>
    public class DishFormTests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public DishFormTests(InMemoryApiFactory factory) => _factory = factory;

        private static FormUrlEncodedContent Form(params (string, string?)[] kv) => new(kv.Where(x => x.Item2 != null).Select(x => new KeyValuePair<string, string>(x.Item1, x.Item2!)));

        [Fact]
        public async Task Create_ignores_standard_cost_fills_hidden_columns_and_requires_category()
        {
            Guid cat = Guid.NewGuid();
            string code = "DF" + cat.ToString("N")[..5].ToUpperInvariant();
            await _factory.SeedAsync(db => { db.DishCategory.Add(new DishCategory { Id = cat, Code = code, Name = "Món mặn " + code, Status = "Active" }); return Task.CompletedTask; });
            using var c = _factory.CreateAuthorizedClient();
            (await LegacyResult.ReadAsync(await c.PostAsync("/api/Dish/Create", Form(("Name", "Gà " + code))))).Fail("nhóm món");
            var made = (await LegacyResult.ReadAsync(await c.PostAsync("/api/Dish/Create", Form(("Name", " Gà kho " + code + " "), ("DishCategoryId", cat.ToString()),
                ("StandardServingSize", "150"), ("StandardCost", "99999"), ("MealCategory", "gõ tay"), ("StandardPortion", "1"), ("Note", "ghi chú cũ"))))).Ok();
            Guid id = made.GetProperty("id").GetGuid();
            made.GetProperty("code").GetString().Should().NotBeNullOrEmpty();
            var dish = await _factory.QueryAsync(db => db.Dish.AsNoTracking().SingleAsync(d => d.Id == id));
            dish.Name.Should().Be("Gà kho " + code);
            dish.StandardCost.Should().BeNull("giá thành chuẩn do hệ thống tính, bỏ qua giá trị gửi lên");
            dish.MealCategory.Should().Be(code);
            dish.StandardPortion.Should().Be(150);
            dish.Description.Should().Be("ghi chú cũ", "ghi chú gộp vào mô tả khi mô tả trống");
            dish.Status.Should().Be("Active");

            await _factory.SeedAsync(async db => { var d = await db.Dish.SingleAsync(x => x.Id == id); d.StandardCost = 25000; });
            (await LegacyResult.ReadAsync(await c.PutAsync("/api/Dish/Update", Form(("Id", id.ToString()), ("Name", "Gà kho gừng " + code), ("DishCategoryId", cat.ToString()),
                ("StandardServingSize", "0"), ("StandardCost", "1"))))).Fail("lớn hơn 0");
            (await LegacyResult.ReadAsync(await c.PutAsync("/api/Dish/Update", Form(("Id", id.ToString()), ("Name", "Gà kho gừng " + code), ("DishCategoryId", cat.ToString()),
                ("StandardServingSize", "160"), ("StandardCost", "1"))))).Ok();
            dish = await _factory.QueryAsync(db => db.Dish.AsNoTracking().SingleAsync(d => d.Id == id));
            dish.StandardCost.Should().Be(25000, "sửa món giữ giá thành chuẩn đang có");
            dish.StandardPortion.Should().Be(160);

            Guid viewer = await CommonTestUsers.UserAsync(_factory, ("menu-dish", "V"));
            (await _factory.CreateAuthorizedClient(viewer).PostAsync("/api/Dish/Create", Form(("Name", "x"), ("DishCategoryId", cat.ToString())))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task Create_version_copies_lines_from_another_dish_only_when_source_is_effective()
        {
            Guid src = Guid.NewGuid(), dst = Guid.NewGuid(), locked = Guid.NewGuid(), draft = Guid.NewGuid(), other = Guid.NewGuid(), ing = Guid.NewGuid();
            string code = "CV" + src.ToString("N")[..5].ToUpperInvariant();
            await _factory.SeedAsync(db =>
            {
                db.Ingredient.Add(new Ingredient { Id = ing, Code = code + "-NL", Name = "Thịt " + code, Status = "Active" });
                db.Dish.AddRange(new Dish { Id = src, Code = code + "-A", Name = "Nguồn " + code, Status = "Active" }, new Dish { Id = dst, Code = code + "-B", Name = "Đích " + code, Status = "Active" },
                    new Dish { Id = other, Code = code + "-C", Name = "Nháp " + code, Status = "Active" });
                db.Recipe.Add(new Recipe { Id = locked, DishId = src, VersionNo = 1, VersionCode = "V1", Status = "Locked", IsActive = true, YieldQuantity = 100 });
                db.RecipeLine.Add(new RecipeLine { Id = Guid.NewGuid(), RecipeId = locked, IngredientId = ing, Quantity = 12, ExpectedWasteRate = 10, SequenceNo = 1 });
                db.Recipe.Add(new Recipe { Id = draft, DishId = other, VersionNo = 1, VersionCode = "V1", Status = "Draft", YieldQuantity = 100 });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();
            (await LegacyResult.ReadAsync(await c.PostAsync("/api/Recipe/CreateVersion", Form(("DishId", dst.ToString()), ("CopyFromRecipeId", draft.ToString()))))).Fail("chưa có công thức");
            var v = (await LegacyResult.ReadAsync(await c.PostAsync("/api/Recipe/CreateVersion", Form(("DishId", dst.ToString()), ("CopyFromRecipeId", locked.ToString()))))).Ok();
            Guid rid = v.GetProperty("id").GetGuid();
            var lines = await _factory.QueryAsync(db => db.RecipeLine.AsNoTracking().Where(l => l.RecipeId == rid).ToListAsync());
            lines.Should().ContainSingle(l => l.IngredientId == ing && l.Quantity == 12 && l.ExpectedWasteRate == 10);
            (await _factory.QueryAsync(db => db.Recipe.AsNoTracking().SingleAsync(r => r.Id == rid))).Status.Should().Be("Draft");
        }
    }
}
