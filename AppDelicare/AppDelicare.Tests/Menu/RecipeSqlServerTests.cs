using AppDelicare.Data.Connection;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Interface;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Menu
{
    /// <summary>
    /// T45R – trên SQL Server dev thật (bỏ qua nếu không có DB):
    /// (1) UnitOfWork.OpenTransaction nay bao cả lần ghi EF (CODE_AUDIT A21): service lỗi giữa chừng → không để lại gì;
    /// (2) vòng đời công thức qua RecipeService trong 1 transaction ngoài rồi ROLLBACK (không để lại dữ liệu).
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class RecipeSqlServerTests(ApiFactory factory)
    {
        [SkippableFact]
        public async Task Legacy_transaction_now_rolls_back_ef_writes()
        {
            Skip.If(factory.SkipReason != null, factory.SkipReason);
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            var actor = scope.ServiceProvider.GetRequiredService<ICurrentActor>();
            Guid admin = await db.User.Where(u => u.UserName == "admin").Select(u => u.Id).SingleAsync();
            string code = "[DEMO]-T45R-" + Guid.NewGuid().ToString("N")[..6];

            using (actor.BeginScope(ActorInfo.ForUser(admin)))
            {
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                using (var tran = await unitOfWork.OpenTransaction())
                {
                    unitOfWork.ShiftRepository.Add(new Shift { Id = Guid.NewGuid(), Code = code, Name = "T45R rollback" });
                    await unitOfWork.ShiftRepository.CommitAsync("Insert");
                    (await db.Shift.CountAsync(s => s.Code == code)).Should().Be(1, "đọc được trong transaction");
                    unitOfWork.RollbackTransaction();
                }
            }

            db.ChangeTracker.Clear();
            (await db.Shift.CountAsync(s => s.Code == code)).Should().Be(0, "rollback phải hủy cả lần ghi EF (trước T45R thì không)");
        }

        [SkippableFact]
        public async Task Recipe_lifecycle_on_sql_server_inside_outer_transaction()
        {
            Skip.If(factory.SkipReason != null, factory.SkipReason);
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            var actor = scope.ServiceProvider.GetRequiredService<ICurrentActor>();
            Guid admin = await db.User.Where(u => u.UserName == "admin").Select(u => u.Id).SingleAsync();
            int recipesBefore = await db.Recipe.CountAsync();

            using (actor.BeginScope(ActorInfo.ForUser(admin)))
            await using (var tx = await db.Database.BeginTransactionAsync())
            {
                Guid dish = Guid.NewGuid(), ingredient = Guid.NewGuid(), kg = Guid.NewGuid();
                db.Dish.Add(new Dish { Id = dish, Code = "[DEMO]-T45R", Name = "[DEMO] Món T45R", Status = "Active" });
                db.UnitMeasure.Add(new UnitMeasure { Id = kg, Name = "[DEMO] kg" });
                db.Ingredient.Add(new Ingredient { Id = ingredient, Code = "[DEMO]-NL-T45R", Name = "[DEMO] Thịt" });
                db.SupplierMaterial.Add(new SupplierMaterial { Id = Guid.NewGuid(), SupplierId = Guid.NewGuid(), IngredientId = ingredient, UnitPrice = 99000, PurchaseUomId = kg, IsPreferred = true });
                await db.SaveChangesAsync("Insert");

                var recipes = scope.ServiceProvider.GetRequiredService<IRecipeService>();
                var lines = scope.ServiceProvider.GetRequiredService<IRecipeLineService>();
                var created = await recipes.CreateVersion(new RecipeVersionRequest { DishId = dish });
                created.ResponseCode.Should().Be(200, created.ResponseMessage);
                Guid recipeId = created.Data!.Id;
                (await lines.Save(new RecipeLineRequest { RecipeId = recipeId, IngredientId = ingredient, Quantity = 10, UnitId = kg }, "Insert")).ResponseCode.Should().Be(200);
                (await recipes.Submit(recipeId, new DateTime(2026, 12, 1))).ResponseCode.Should().Be(200);
                var locked = await recipes.Decide(new RecipeDecisionRequest { Id = recipeId }, approve: true); // BIZ-2.3: duyệt = khóa & áp dụng
                locked.ResponseCode.Should().Be(200, locked.ResponseMessage);

                db.ChangeTracker.Clear();
                var r = await db.Recipe.AsNoTracking().SingleAsync(x => x.Id == recipeId);
                r.Status.Should().Be("Locked");
                r.LockedBy.Should().Be(admin);
                r.SnapshotJson.Should().NotBeNullOrEmpty();
                (await db.StandardCost.AsNoTracking().SingleAsync(s => s.RecipeId == recipeId)).MaterialCost.Should().Be(9900); // 10 kg × 99 000 / 100 suất
                (await db.RecipeLine.AsNoTracking().SingleAsync(l => l.RecipeId == recipeId)).UomId.Should().Be(kg);
                await tx.RollbackAsync();
            }

            db.ChangeTracker.Clear();
            (await db.Recipe.CountAsync()).Should().Be(recipesBefore, "rollback không để lại dữ liệu");
        }
    }
}
