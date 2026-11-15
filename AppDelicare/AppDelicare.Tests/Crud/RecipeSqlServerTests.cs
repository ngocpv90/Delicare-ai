using AppDelicare.Application.Common;
using AppDelicare.Application.Menu;
using AppDelicare.Data.Connection;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Crud
{
    /// <summary>T45B.4 – vòng đời công thức trên SQL Server thật, trong 1 transaction rồi ROLLBACK. Không có DB dev thì Skip.</summary>
    [Collection(ApiCollection.Name)]
    public class RecipeSqlServerTests(ApiFactory factory)
    {
        [SkippableFact]
        public async Task Recipe_lock_and_standard_cost_on_sql_server_roll_back_cleanly()
        {
            Skip.If(factory.SkipReason != null, factory.SkipReason);
            using var scope = factory.Services.CreateScope();
            var sp = scope.ServiceProvider;
            var db = sp.GetRequiredService<DatabaseContext>();
            var dispatcher = sp.GetRequiredService<IDispatcher>();
            Guid admin = await db.User.Where(u => u.UserName == "admin").Select(u => u.Id).SingleAsync();
            int recipesBefore = await db.Recipe.CountAsync();

            using (sp.GetRequiredService<ICurrentActor>().BeginScope(ActorInfo.ForUser(admin)))
            await using (var tx = await db.Database.BeginTransactionAsync())
            {
                Guid dish = Guid.NewGuid(), kg = Guid.NewGuid(), ing = Guid.NewGuid(), sup = Guid.NewGuid();
                db.Dish.Add(new Dish { Id = dish, Code = "DEMO-T454", Name = "[DEMO] Món T45B.4", Status = "Active" });
                db.UnitMeasure.Add(new UnitMeasure { Id = kg, Name = "[DEMO] kg" });
                db.Ingredient.Add(new Ingredient { Id = ing, Code = "DEMO-T454-NL", Name = "[DEMO] Thịt" });
                db.Supplier.Add(new Supplier { Id = sup, Code = "DEMO-T454-NCC", Name = "[DEMO] NCC" });
                db.SupplierMaterial.Add(new SupplierMaterial { Id = Guid.NewGuid(), SupplierId = sup, IngredientId = ing, UnitPrice = 99000, PurchaseUomId = kg, IsPreferred = true });
                await db.SaveChangesAsync(AppDelicare.Model.Helper.ConstantHelper.TypeSave.Insert);

                var recipe = await dispatcher.SendAsync(new CreateRecipeVersionCommand(dish, null));
                await dispatcher.SendAsync(new AddRecipeLineCommand(recipe.Id, new RecipeLineInput(ing, 10, kg, 5, 1, "[DEMO]")));
                await dispatcher.SendAsync(new SubmitRecipeCommand(recipe.Id, admin));
                await dispatcher.SendAsync(new DecideRecipeCommand(recipe.Id, true, null, admin));
                var locked = await dispatcher.SendAsync(new LockRecipeCommand(recipe.Id, new DateTime(2026, 12, 1), admin));

                db.ChangeTracker.Clear();
                locked.Status.Should().Be(RecipeStatus.Locked);
                var stored = await db.Recipe.AsNoTracking().SingleAsync(r => r.Id == recipe.Id);
                stored.LockedAt.Should().NotBeNull();
                stored.SnapshotJson.Should().Contain(ing.ToString());
                var cost = await db.StandardCost.AsNoTracking().SingleAsync(s => s.RecipeId == recipe.Id);
                cost.CostPerServing.Should().Be(9900m, "10 kg × 99 000 ÷ 100 suất");
                (await db.Dish.AsNoTracking().Where(d => d.Id == dish).Select(d => d.StandardCost).SingleAsync()).Should().Be(9900m);
                (await db.RecipeLine.AsNoTracking().SingleAsync(l => l.RecipeId == recipe.Id)).UomId.Should().Be(kg, "cột cũ UomId ghi song song");
                await tx.RollbackAsync();
            }

            db.ChangeTracker.Clear();
            (await db.Recipe.CountAsync()).Should().Be(recipesBefore, "rollback không để lại dữ liệu");
        }
    }
}
