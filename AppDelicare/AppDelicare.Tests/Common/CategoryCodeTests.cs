using AppDelicare.Data.Connection;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Request;
using AppDelicare.Services.Interface;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Common
{
    /// <summary>Anh chốt 2026-11-22: mã nhóm món (NM-) / nhóm nguyên liệu (NN-) do máy chủ tự sinh, bỏ qua mã gửi lên, giữ nguyên khi sửa.</summary>
    public class CategoryCodeTests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public CategoryCodeTests(InMemoryApiFactory factory) => _factory = factory;

        [Fact]
        public async Task Dish_category_code_is_generated_and_kept_on_update()
        {
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IDishCategoryService>();
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            using var job = scope.ServiceProvider.GetRequiredService<ICurrentActor>().BeginScope(ActorInfo.ForSystem(Guid.NewGuid()), "test-category-code");

            var saved = await service.Save(new DishCategoryRequest { Code = "go tay", Name = "Món mặn kiểm mã" }, ConstantHelper.TypeSave.Insert);
            saved.Data.Should().BeTrue(saved.ResponseMessage);
            var row = await db.Set<DishCategory>().AsNoTracking().SingleAsync(x => x.Name == "Món mặn kiểm mã");
            row.Code.Should().MatchRegex(@"^NM-\d{5}$");
            saved.ResponseMessage.Should().Contain(row.Code);

            var updated = await service.Save(new DishCategoryRequest { Id = row.Id, Code = "DOI_MA", Name = "Món mặn (sửa)" }, ConstantHelper.TypeSave.Update);
            updated.Data.Should().BeTrue(updated.ResponseMessage);
            var after = await db.Set<DishCategory>().AsNoTracking().SingleAsync(x => x.Id == row.Id);
            after.Code.Should().Be(row.Code, "mã do máy chủ cấp không đổi khi sửa");
            after.Name.Should().Be("Món mặn (sửa)");
        }

        [Fact]
        public async Task Ingredient_category_code_is_generated()
        {
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IIngredientCategoryService>();
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            using var job = scope.ServiceProvider.GetRequiredService<ICurrentActor>().BeginScope(ActorInfo.ForSystem(Guid.NewGuid()), "test-category-code");

            var saved = await service.Save(new IngredientCategoryRequest { Name = "Rau củ kiểm mã" }, ConstantHelper.TypeSave.Insert);
            saved.Data.Should().BeTrue(saved.ResponseMessage);
            (await db.Set<IngredientCategory>().AsNoTracking().SingleAsync(x => x.Name == "Rau củ kiểm mã")).Code.Should().MatchRegex(@"^NN-\d{5}$");
        }
    }
}
