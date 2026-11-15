using System.Net;
using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Menu
{
    /// <summary>
    /// T45R (chuyển từ T45B.4 v1) – công thức / phiên bản / giá thành / đơn giá theo khung cũ:
    /// Recipe/*, RecipeLine/*, StandardCost/Recalculate, PricePolicy/*, FoodSafetyChecklist/*.
    /// </summary>
    public class RecipeServiceTests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public RecipeServiceTests(InMemoryApiFactory factory) => _factory = factory;

        /// <summary>Món + 2 nguyên liệu: gạo (giá theo kg, định lượng theo g → cần quy đổi), sườn (giá theo kg, định lượng theo kg).</summary>
        private async Task<(Guid Dish, Guid Rice, Guid Pork, Guid Kg, Guid G)> SeedDishAsync(string code, decimal ricePrice = 20000)
        {
            Guid dish = Guid.NewGuid(), rice = Guid.NewGuid(), pork = Guid.NewGuid(), kg = Guid.NewGuid(), g = Guid.NewGuid(), sup = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Dish.Add(new Dish { Id = dish, Code = code, Name = "Cơm sườn " + code, Status = "Active" });
                db.UnitMeasure.AddRange(new UnitMeasure { Id = kg, Name = "kg" }, new UnitMeasure { Id = g, Name = "g" });
                db.UomConversion.Add(new UomConversion { Id = Guid.NewGuid(), FromUnitId = kg, ToUnitId = g, FromUomId = kg, ToUomId = g, ConversionFactor = 1000 });
                db.Ingredient.AddRange(new Ingredient { Id = rice, Code = code + "-GAO", Name = "Gạo" }, new Ingredient { Id = pork, Code = code + "-SUON", Name = "Sườn" });
                db.Supplier.Add(new Supplier { Id = sup, Code = code + "-NCC", Name = "NCC" });
                db.SupplierMaterial.AddRange(
                    new SupplierMaterial { Id = Guid.NewGuid(), SupplierId = sup, IngredientId = rice, UnitPrice = ricePrice, PurchaseUomId = kg, IsPreferred = true },
                    new SupplierMaterial { Id = Guid.NewGuid(), SupplierId = sup, IngredientId = pork, UnitPrice = 120000, PurchaseUomId = kg, IsPreferred = true });
                return Task.CompletedTask;
            });
            return (dish, rice, pork, kg, g);
        }

        private static Task<LegacyResult> AddLine(HttpClient c, Guid recipe, Guid ingredient, decimal qty, Guid unit, int seq)
            => c.PostFormAsync("/api/RecipeLine/Insert", ("RecipeId", recipe), ("IngredientId", ingredient), ("Quantity", qty), ("UnitId", unit), ("SequenceNo", seq));

        private Task<Recipe> RecipeAsync(Guid id) => _factory.QueryAsync(db => db.Recipe.AsNoTracking().SingleAsync(r => r.Id == id));

        [Fact]
        public async Task Recipe_lifecycle_draft_lines_submit_approve_lock_and_cost()
        {
            var (dish, rice, pork, kg, g) = await SeedDishAsync("MON-RV");
            using var c = _factory.CreateAuthorizedClient();

            var v1 = (await c.PostFormAsync("/api/Recipe/CreateVersion", ("DishId", dish))).Ok();
            Guid r1 = v1.GetProperty("id").GetGuid();
            v1.GetProperty("versionCode").GetString().Should().Be("MON-RV-V1");
            v1.GetProperty("status").GetString().Should().Be("Draft");
            (await c.PostFormAsync("/api/Recipe/CreateVersion", ("DishId", dish))).Fail("nháp");

            // 100 suất: 12 000 g gạo (= 12 kg × 20 000) + 15 kg sườn (× 120 000) → 240 000 + 1 800 000 = 2 040 000 → 20 400 / suất.
            (await c.PostFormAsync("/api/Recipe/Submit", ("id", r1))).Fail("ít nhất 1 dòng");
            (await AddLine(c, r1, rice, 12000, g, 1)).Ok();
            (await AddLine(c, r1, pork, 15, kg, 2)).Ok();
            (await AddLine(c, r1, pork, 0, kg, 3)).Fail("lớn hơn 0");
            var line = await _factory.QueryAsync(db => db.RecipeLine.AsNoTracking().FirstAsync(l => l.RecipeId == r1 && l.IngredientId == rice));
            line.UomId.Should().Be(g, "ghi song song cột cũ UomId");

            var cost = (await c.GetResultAsync($"/api/Recipe/CalculateCost?id={r1}&mealCount=50")).Ok();
            cost.GetProperty("costPerMeal").GetDecimal().Should().Be(20400);
            cost.GetProperty("totalMaterialCost").GetDecimal().Should().Be(1_020_000);
            cost.GetProperty("complete").GetBoolean().Should().BeTrue();

            (await c.PostFormAsync("/api/Recipe/Submit", ("id", r1))).Ok();
            (await RecipeAsync(r1)).Status.Should().Be("PendingApproval");
            (await c.PutFormAsync("/api/RecipeLine/Update", ("Id", line.Id), ("RecipeId", r1), ("IngredientId", rice), ("Quantity", 1m), ("UnitId", g))).Fail("Draft");
            (await c.DeleteResultAsync($"/api/RecipeLine/Delete?id={line.Id}")).Fail("Draft");
            (await c.PostFormAsync("/api/Recipe/Lock", ("Id", r1), ("EffectiveFrom", new DateTime(2026, 12, 1)))).Fail("Approved");
            (await c.PostFormAsync("/api/Recipe/Reject", ("Id", r1), ("Note", ""))).Fail("lý do");
            (await c.PostFormAsync("/api/Recipe/Approve", ("Id", r1))).Ok();
            (await RecipeAsync(r1)).Status.Should().Be("Approved");

            (await c.PostFormAsync("/api/Recipe/Lock", ("Id", r1), ("EffectiveFrom", new DateTime(2026, 12, 1)))).Ok();
            var locked = await RecipeAsync(r1);
            locked.Status.Should().Be("Locked");
            locked.IsActive.Should().BeTrue();
            locked.LockedAt.Should().NotBeNull();
            locked.LockedBy.Should().Be(InMemoryApiFactory.UserId);
            locked.SnapshotJson.Should().Contain(rice.ToString());
            (await _factory.QueryAsync(db => db.ApprovalRequest.SingleAsync(a => a.BusinessId == r1))).RequestStatus.Should().Be("Approved");
            (await _factory.QueryAsync(db => db.Dish.SingleAsync(d => d.Id == dish))).StandardCost.Should().Be(20400);

            // V2 sao chép V1 → khóa → V1 Obsolete, EffectiveTo = ngày trước hiệu lực V2.
            Guid r2 = (await c.PostFormAsync("/api/Recipe/CreateVersion", ("DishId", dish), ("CopyFromRecipeId", r1))).Ok().GetProperty("id").GetGuid();
            (await _factory.QueryAsync(db => db.RecipeLine.CountAsync(l => l.RecipeId == r2 && !l.IsDeleted))).Should().Be(2);
            (await c.PostFormAsync("/api/Recipe/Submit", ("id", r2))).Ok();
            (await c.PostFormAsync("/api/Recipe/Approve", ("Id", r2))).Ok();
            (await c.PostFormAsync("/api/Recipe/Lock", ("Id", r2), ("EffectiveFrom", new DateTime(2027, 1, 1)))).Ok();
            var old = await RecipeAsync(r1);
            old.Status.Should().Be("Obsolete");
            old.IsActive.Should().BeFalse();
            old.EffectiveTo.Should().Be(new DateTime(2026, 12, 31));
            var costs = await _factory.QueryAsync(db => db.StandardCost.AsNoTracking().Where(s => s.DishId == dish).ToListAsync());
            costs.Should().HaveCount(2);
            costs.Single(s => s.RecipeId == r2).Status.Should().Be("Active");
            costs.Single(s => s.RecipeId == r1).Status.Should().Be("Inactive");

            var versions = await c.GetResultAsync($"/api/Recipe/GetByDishId?dishId={dish}");
            versions.ListData.GetArrayLength().Should().Be(2);
            (await c.GetResultAsync($"/api/Dish/GetActiveRecipe?id={dish}")).Ok().GetProperty("recipe").GetProperty("id").GetGuid().Should().Be(r2);
        }

        [Fact]
        public async Task Update_cannot_change_status_and_insert_always_creates_a_draft()
        {
            var (dish, _, _, _, _) = await SeedDishAsync("MON-UPD");
            using var c = _factory.CreateAuthorizedClient();

            (await c.PostFormAsync("/api/Recipe/Insert", ("DishId", dish), ("Status", "Locked"), ("IsActive", true), ("YieldQuantity", 100m), ("EffectiveFrom", DateTime.Today))).Ok();
            var r = await _factory.QueryAsync(db => db.Recipe.AsNoTracking().SingleAsync(x => x.DishId == dish));
            r.Status.Should().Be("Draft");
            r.IsActive.Should().BeFalse();
            r.VersionNo.Should().Be(1);

            (await c.PutFormAsync("/api/Recipe/Update", ("Id", r.Id), ("DishId", dish), ("Status", "Approved"), ("YieldQuantity", 100m), ("EffectiveFrom", DateTime.Today), ("Note", "sửa ghi chú"))).Ok();
            var after = await RecipeAsync(r.Id);
            after.Status.Should().Be("Draft");
            after.Note.Should().Be("sửa ghi chú");
        }

        [Fact]
        public async Task Recalculate_uses_current_supplier_price_and_missing_price_is_a_warning()
        {
            var (dish, rice, pork, kg, g) = await SeedDishAsync("MON-RC");
            using var c = _factory.CreateAuthorizedClient();
            Guid r = (await c.PostFormAsync("/api/Recipe/CreateVersion", ("DishId", dish))).Ok().GetProperty("id").GetGuid();
            await AddLine(c, r, rice, 12000, g, 1);
            await AddLine(c, r, pork, 15, kg, 2);
            await c.PostFormAsync("/api/Recipe/Submit", ("id", r));
            await c.PostFormAsync("/api/Recipe/Approve", ("Id", r));
            (await c.PostFormAsync("/api/Recipe/Lock", ("Id", r))).Ok();

            await _factory.SeedAsync(async db =>
            {
                var price = await db.SupplierMaterial.FirstAsync(m => m.IngredientId == rice);
                price.UnitPrice = 30000; // 12 kg × 30 000 = 360 000 → (360 000 + 1 800 000) / 100 = 21 600
            });
            (await c.PostFormAsync("/api/StandardCost/Recalculate", ("ListDishId", dish))).Ok().GetInt32().Should().Be(1);
            (await _factory.QueryAsync(db => db.StandardCost.SingleAsync(s => s.DishId == dish && s.Status == "Active"))).MaterialCost.Should().Be(21600);

            Guid noPrice = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Ingredient.Add(new Ingredient { Id = noPrice, Code = "MON-RC-NOPRICE", Name = "Chưa có giá" });
                return Task.CompletedTask;
            });
            var (dish2, _, _, _, _) = await SeedDishAsync("MON-RC2");
            Guid r2 = (await c.PostFormAsync("/api/Recipe/CreateVersion", ("DishId", dish2))).Ok().GetProperty("id").GetGuid();
            await AddLine(c, r2, noPrice, 1, kg, 1);
            var cost = (await c.GetResultAsync($"/api/Recipe/CalculateCost?id={r2}")).Ok();
            cost.GetProperty("complete").GetBoolean().Should().BeFalse();
            cost.GetProperty("listLine")[0].GetProperty("warning").GetString().Should().Contain("Chưa có giá");
        }

        [Fact]
        public async Task User_without_the_approval_menu_cannot_approve()
        {
            var (dish, rice, _, _, g) = await SeedDishAsync("MON-AP");
            Guid userId = Guid.NewGuid(), groupId = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                var menu = new ObjectMenu { Id = Guid.NewGuid(), Name = "Công thức", MetaLink = "#/menu-recipe", IsActive = true, LevelShow = 2 };
                db.ObjectMenu.Add(menu);
                db.User.Add(new User { Id = userId, UserName = "ops-plan-ap", IsActive = true });
                db.UserGroup.Add(new UserGroup { Id = groupId, Name = "OPS-PLAN-AP" });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = userId, UserGroupId = groupId });
                db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = groupId, ObjectMenuId = menu.Id, AllowAccess = true, AllowAdd = true, AllowEdit = true });
                return Task.CompletedTask;
            });
            using var admin = _factory.CreateAuthorizedClient();
            Guid r = (await admin.PostFormAsync("/api/Recipe/CreateVersion", ("DishId", dish))).Ok().GetProperty("id").GetGuid();
            await AddLine(admin, r, rice, 100, g, 1);
            using var user = _factory.CreateAuthorizedClient(userId);

            (await user.PostFormAsync("/api/Recipe/Submit", ("id", r))).Ok(); // sửa được (AllowEdit)
            (await user.PostAsync("/api/Recipe/Approve", LegacyHttp.Form(("Id", r)))).StatusCode.Should().Be(HttpStatusCode.Forbidden);

            await _factory.SeedAsync(db =>
            {
                var approve = new ObjectMenu { Id = Guid.NewGuid(), Name = "Duyệt – Công thức", MetaLink = "#/approve/menu-recipe", IsActive = true, LevelShow = 2 };
                db.ObjectMenu.Add(approve);
                db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = groupId, ObjectMenuId = approve.Id, AllowAccess = true, AllowEdit = true });
                return Task.CompletedTask;
            });
            // Cache quyền 5′ – admin đổi qua màn hình ma trận sẽ xóa cache; ở đây seed trực tiếp nên dùng client mới sau khi xóa cache bằng UserRole/Update rỗng.
            await admin.PutFormAsync("/api/UserRole/Update", ("ListPermission", "[]"));
            (await user.PostFormAsync("/api/Recipe/Approve", ("Id", r))).Ok();
        }

        [Fact]
        public async Task Price_policy_resolve_order_overlap_and_positive_price()
        {
            Guid customer = Guid.NewGuid(), contract = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.ContractMeal.Add(new ContractMeal { Id = Guid.NewGuid(), ContractId = contract, MealType = "TRUA", UnitPrice = 30000 });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();
            var from = new DateTime(2026, 1, 1);

            (await c.GetResultAsync($"/api/PricePolicy/Resolve?CustomerId={customer}&ContractId={contract}&MealType=TRUA&Date=2026-06-01")).Ok()
                .GetProperty("source").GetString().Should().Be("ContractMeal");
            (await c.PostFormAsync("/api/PricePolicy/Insert", ("Code", "PP-KH"), ("Name", "Theo khách"), ("CustomerId", customer), ("MealType", "TRUA"),
                ("UnitPrice", 32000m), ("EffectiveFrom", from), ("Status", "Active"))).Ok();
            (await c.GetResultAsync($"/api/PricePolicy/Resolve?CustomerId={customer}&ContractId={contract}&MealType=TRUA&Date=2026-06-01")).Ok()
                .GetProperty("unitPrice").GetDecimal().Should().Be(32000);
            (await c.PostFormAsync("/api/PricePolicy/Insert", ("Code", "PP-HD"), ("Name", "Theo HĐ"), ("CustomerId", customer), ("ContractId", contract), ("MealType", "TRUA"),
                ("UnitPrice", 31000m), ("EffectiveFrom", from), ("Status", "Active"))).Ok();
            var resolved = (await c.GetResultAsync($"/api/PricePolicy/Resolve?CustomerId={customer}&ContractId={contract}&MealType=TRUA&Date=2026-06-01")).Ok();
            resolved.GetProperty("unitPrice").GetDecimal().Should().Be(31000);
            resolved.GetProperty("source").GetString().Should().Be("PricePolicy:Contract");

            (await c.PostFormAsync("/api/PricePolicy/Insert", ("Code", "PP-KH-2"), ("Name", "Chồng"), ("CustomerId", customer), ("MealType", "TRUA"),
                ("UnitPrice", 33000m), ("EffectiveFrom", new DateTime(2026, 3, 1)), ("Status", "Active"))).Fail("Chồng");
            (await c.PostFormAsync("/api/PricePolicy/Insert", ("Code", "PP-0"), ("Name", "Giá 0"), ("CustomerId", Guid.NewGuid()), ("UnitPrice", 0m), ("EffectiveFrom", from))).Fail("lớn hơn 0");
            (await c.GetResultAsync($"/api/PricePolicy/Resolve?CustomerId={Guid.NewGuid()}")).Fail("Không tìm thấy đơn giá");
        }

        [Fact]
        public async Task Checklist_code_and_version_must_be_unique()
        {
            using var c = _factory.CreateAuthorizedClient();

            (await c.PostFormAsync("/api/FoodSafetyChecklist/Insert", ("Code", "CL-01"), ("Name", "Checklist bếp"), ("VersionNo", 1))).Ok();
            (await c.PostFormAsync("/api/FoodSafetyChecklist/Insert", ("Code", "CL-01"), ("Name", "Checklist bếp"), ("VersionNo", 1))).Fail();
            (await c.PostFormAsync("/api/FoodSafetyChecklist/Insert", ("Code", "CL-01"), ("Name", "Checklist bếp v2"), ("VersionNo", 2))).Ok();
        }
    }
}
