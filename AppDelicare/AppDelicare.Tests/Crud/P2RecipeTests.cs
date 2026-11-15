using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using AppDelicare.WebApi.Api;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Crud
{
    /// <summary>T45B.4 – công thức / phiên bản / giá thành / đơn giá (spec mục 5).</summary>
    public class P2RecipeTests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public P2RecipeTests(InMemoryApiFactory factory) => _factory = factory;

        private static async Task<JsonElement> Data(HttpResponseMessage r)
        {
            string raw = await r.Content.ReadAsStringAsync();
            r.IsSuccessStatusCode.Should().BeTrue(raw);
            return JsonDocument.Parse(raw).RootElement.GetProperty("data").Clone();
        }

        private static async Task<string> ErrorCode(HttpResponseMessage r)
            => JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.GetProperty("errors")[0].GetProperty("code").GetString()!;

        /// <summary>Món + 2 nguyên liệu: gạo (giá theo kg, định lượng theo g → cần quy đổi), thịt (giá theo kg, định lượng theo kg).</summary>
        private async Task<(Guid Dish, Guid Rice, Guid Pork, Guid Kg, Guid G)> SeedDishAsync(string code)
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
                    new SupplierMaterial { Id = Guid.NewGuid(), SupplierId = sup, IngredientId = rice, UnitPrice = 20000, PurchaseUomId = kg, IsPreferred = true },
                    new SupplierMaterial { Id = Guid.NewGuid(), SupplierId = sup, IngredientId = pork, UnitPrice = 120000, PurchaseUomId = kg, IsPreferred = true });
                return Task.CompletedTask;
            });
            return (dish, rice, pork, kg, g);
        }

        [Fact]
        public async Task Recipe_lifecycle_draft_lines_submit_approve_lock_and_cost()
        {
            var (dish, rice, pork, kg, g) = await SeedDishAsync("MON-RV");
            using var c = _factory.CreateAuthorizedClient();

            var v1 = await Data(await c.PostAsJsonAsync($"/api/v1/menu/dishes/{dish}/recipes", new { }));
            Guid r1 = v1.GetProperty("id").GetGuid();
            v1.GetProperty("versionCode").GetString().Should().Be("MON-RV-V1");
            v1.GetProperty("status").GetString().Should().Be("Draft");
            (await c.PostAsJsonAsync($"/api/v1/menu/dishes/{dish}/recipes", new { })).StatusCode.Should().Be(HttpStatusCode.Conflict, "đã có bản nháp");

            // 100 suất: 12 000 g gạo (= 12 kg × 20 000) + 15 kg sườn (× 120 000) → 240 000 + 1 800 000 = 2 040 000 → 20 400 / suất.
            (await c.PostAsync($"/api/v1/menu/recipes/{r1}/submit", null)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "chưa có dòng");
            var line = await Data(await c.PostAsJsonAsync($"/api/v1/menu/recipes/{r1}/lines", new { ingredientId = rice, quantity = 12000, unitId = g, sequenceNo = 1 }));
            await Data(await c.PostAsJsonAsync($"/api/v1/menu/recipes/{r1}/lines", new { ingredientId = pork, quantity = 15, unitId = kg, sequenceNo = 2 }));
            (await c.PostAsJsonAsync($"/api/v1/menu/recipes/{r1}/lines", new { ingredientId = pork, quantity = 0, unitId = kg })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

            var cost = await Data(await c.GetAsync($"/api/v1/menu/recipes/{r1}/cost?mealCount=50"));
            cost.GetProperty("costPerMeal").GetDecimal().Should().Be(20400);
            cost.GetProperty("totalMaterialCost").GetDecimal().Should().Be(1_020_000);
            cost.GetProperty("complete").GetBoolean().Should().BeTrue();

            (await Data(await c.PostAsync($"/api/v1/menu/recipes/{r1}/submit", null))).GetProperty("status").GetString().Should().Be("PendingApproval");
            (await c.PutAsJsonAsync($"/api/v1/menu/recipe-lines/{line.GetProperty("id").GetGuid()}", new { ingredientId = rice, quantity = 1, unitId = g }))
                .StatusCode.Should().Be(HttpStatusCode.Conflict, "chỉ sửa dòng khi Draft");
            (await c.PostAsJsonAsync($"/api/v1/menu/recipes/{r1}/lock", new { effectiveFrom = "2026-12-01" })).StatusCode.Should().Be(HttpStatusCode.Conflict, "chưa duyệt");
            (await c.PostAsJsonAsync($"/api/v1/menu/recipes/{r1}/reject", new { note = "" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await Data(await c.PostAsJsonAsync($"/api/v1/menu/recipes/{r1}/approve", new { }))).GetProperty("status").GetString().Should().Be("Approved");

            var locked = await Data(await c.PostAsJsonAsync($"/api/v1/menu/recipes/{r1}/lock", new { effectiveFrom = "2026-12-01" }));
            locked.GetProperty("status").GetString().Should().Be("Locked");
            locked.GetProperty("isActive").GetBoolean().Should().BeTrue();
            locked.GetProperty("snapshotJson").GetString().Should().Contain(rice.ToString());

            var stdCost = await _factory.QueryAsync(db => db.StandardCost.AsNoTracking().SingleAsync(s => s.RecipeId == r1));
            stdCost.MaterialCost.Should().Be(20400);
            stdCost.CostPerServing.Should().Be(20400);
            (await _factory.QueryAsync(db => db.Dish.AsNoTracking().Where(d => d.Id == dish).Select(d => d.StandardCost).SingleAsync())).Should().Be(20400);
            (await _factory.QueryAsync(db => db.ApprovalRequest.AsNoTracking().SingleAsync(a => a.BusinessId == r1))).RequestStatus.Should().Be("Approved");

            // Phiên bản 2 sao chép từ V1 → khóa → V1 thành Obsolete.
            var v2 = await Data(await c.PostAsJsonAsync($"/api/v1/menu/dishes/{dish}/recipes", new { copyFromRecipeId = r1 }));
            Guid r2 = v2.GetProperty("id").GetGuid();
            v2.GetProperty("versionNo").GetInt32().Should().Be(2);
            await Data(await c.PostAsync($"/api/v1/menu/recipes/{r2}/submit", null));
            await Data(await c.PostAsJsonAsync($"/api/v1/menu/recipes/{r2}/approve", new { }));
            await Data(await c.PostAsJsonAsync($"/api/v1/menu/recipes/{r2}/lock", new { effectiveFrom = "2027-01-01" }));
            var versions = await Data(await c.GetAsync($"/api/v1/menu/dishes/{dish}/recipes"));
            versions[0].GetProperty("versionNo").GetInt32().Should().Be(2);
            versions[0].GetProperty("lines").GetArrayLength().Should().Be(2, "đã sao chép dòng");
            versions[1].GetProperty("status").GetString().Should().Be("Obsolete");
            versions[1].GetProperty("effectiveTo").GetDateTime().Should().Be(new DateTime(2026, 12, 31));
            (await _factory.QueryAsync(db => db.StandardCost.AsNoTracking().Where(s => s.DishId == dish).Select(s => s.Status).ToListAsync()))
                .Should().BeEquivalentTo(["Inactive", "Active"]);

            var dishes = await Data(await c.GetAsync("/api/v1/menu/dishes?q=MON-RV"));
            var item = dishes.GetProperty("items")[0];
            item.GetProperty("activeRecipe").GetProperty("versionCode").GetString().Should().Be("MON-RV-V2");
            item.GetProperty("estimatedMaterialCost").GetDecimal().Should().Be(20400);
        }

        [Fact]
        public async Task Recalculate_uses_current_prices_and_missing_price_is_flagged()
        {
            var (dish, rice, pork, kg, g) = await SeedDishAsync("MON-RC");
            using var c = _factory.CreateAuthorizedClient();
            Guid r = (await Data(await c.PostAsJsonAsync($"/api/v1/menu/dishes/{dish}/recipes", new { }))).GetProperty("id").GetGuid();
            await Data(await c.PostAsJsonAsync($"/api/v1/menu/recipes/{r}/lines", new { ingredientId = pork, quantity = 10, unitId = kg }));
            await Data(await c.PostAsync($"/api/v1/menu/recipes/{r}/submit", null));
            await Data(await c.PostAsJsonAsync($"/api/v1/menu/recipes/{r}/approve", new { }));
            await Data(await c.PostAsJsonAsync($"/api/v1/menu/recipes/{r}/lock", new { effectiveFrom = "2026-11-01" }));

            await _factory.SeedAsync(async db => (await db.SupplierMaterial.FirstAsync(m => m.IngredientId == pork)).UnitPrice = 150000);
            var recalculated = await Data(await c.PostAsJsonAsync("/api/v1/production/standard-costs/recalculate", new { dishIds = new[] { dish } }));
            recalculated[0].GetProperty("costPerServing").GetDecimal().Should().Be(15000);

            var lonely = Guid.NewGuid();
            await _factory.SeedAsync(db => { db.Ingredient.Add(new Ingredient { Id = lonely, Code = "NL-NOPRICE", Name = "Không giá" }); return Task.CompletedTask; });
            Guid r2 = (await Data(await c.PostAsJsonAsync($"/api/v1/menu/dishes/{dish}/recipes", new { copyFromRecipeId = r }))).GetProperty("id").GetGuid();
            await Data(await c.PostAsJsonAsync($"/api/v1/menu/recipes/{r2}/lines", new { ingredientId = lonely, quantity = 1, unitId = kg }));
            var cost = await Data(await c.GetAsync($"/api/v1/menu/recipes/{r2}/cost"));
            cost.GetProperty("complete").GetBoolean().Should().BeFalse();
            cost.GetProperty("lines").EnumerateArray().Should().Contain(l => l.GetProperty("warning").GetString() != null);
        }

        [Fact]
        public async Task Price_policy_resolve_order_and_no_overlap()
        {
            Guid customer = Guid.NewGuid(), contract = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Customer.Add(new Customer { Id = customer, Code = "KH-PP", CIF = "CIF-PP", CustomerType = "B2B", DisplayName = "KH PP", Status = "Active" });
                db.Contract.Add(new Contract { Id = contract, CustomerId = customer, ContractNumber = "HD-PP", Status = "Active" });
                db.ContractMeal.Add(new ContractMeal { Id = Guid.NewGuid(), ContractId = contract, MealType = "TRUA", UnitPrice = 30000 });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();
            string Url(Guid? ct = null) => $"/api/v1/sales/price-policies/resolve?customerId={customer}&mealType=TRUA&date=2026-12-10" + (ct == null ? "" : $"&contractId={ct}");

            (await Data(await c.GetAsync(Url(contract)))).GetProperty("source").GetString().Should().Be("ContractMeal");
            (await c.GetAsync(Url())).StatusCode.Should().Be(HttpStatusCode.NotFound);

            await Data(await c.PostAsJsonAsync("/api/v1/sales/price-policies", new { code = "PP-KH", name = "Giá khách", customerId = customer, mealType = "TRUA", unitPrice = 32000, status = "Active", effectiveFrom = "2026-01-01", effectiveTo = "2026-12-31" }));
            (await Data(await c.GetAsync(Url()))).GetProperty("unitPrice").GetDecimal().Should().Be(32000);

            await Data(await c.PostAsJsonAsync("/api/v1/sales/price-policies", new { code = "PP-HD", name = "Giá HĐ", customerId = customer, contractId = contract, mealType = "TRUA", unitPrice = 31000, status = "Active", effectiveFrom = "2026-12-01" }));
            var byContract = await Data(await c.GetAsync(Url(contract)));
            byContract.GetProperty("unitPrice").GetDecimal().Should().Be(31000);
            byContract.GetProperty("source").GetString().Should().Be("PricePolicie:Contract");

            var overlap = await c.PostAsJsonAsync("/api/v1/sales/price-policies", new { code = "PP-KH2", name = "Giá khách 2", customerId = customer, mealType = "TRUA", unitPrice = 33000, status = "Active", effectiveFrom = "2026-06-01", effectiveTo = "2027-06-30" });
            overlap.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            (await ErrorCode(overlap)).Should().Be(ErrorCodes.BusinessRule);
            (await c.PostAsJsonAsync("/api/v1/sales/price-policies", new { code = "PP-KH3", name = "Giá 2027", customerId = customer, mealType = "TRUA", unitPrice = 34000, status = "Active", effectiveFrom = "2027-01-01" }))
                .StatusCode.Should().Be(HttpStatusCode.Created, "nối tiếp, không chồng");
        }

        [Fact]
        public async Task Only_approver_groups_can_approve_recipes()
        {
            var (dish, _, pork, kg, _) = await SeedDishAsync("MON-AP");
            using var admin = _factory.CreateAuthorizedClient();
            Guid r = (await Data(await admin.PostAsJsonAsync($"/api/v1/menu/dishes/{dish}/recipes", new { }))).GetProperty("id").GetGuid();
            await Data(await admin.PostAsJsonAsync($"/api/v1/menu/recipes/{r}/lines", new { ingredientId = pork, quantity = 1, unitId = kg }));
            await Data(await admin.PostAsync($"/api/v1/menu/recipes/{r}/submit", null));

            Guid userId = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                Guid group = Guid.NewGuid(), menu = Guid.NewGuid();
                db.User.Add(new User { Id = userId, UserName = "ops-plan-t454", IsActive = true });
                db.UserGroup.Add(new UserGroup { Id = group, Name = "OPS-PLAN" });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = userId, UserGroupId = group });
                db.ObjectMenu.Add(new ObjectMenu { Id = menu, Name = "CT", MetaLink = "#/menu-recipe", IsActive = true });
                db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = group, ObjectMenuId = menu, AllowAccess = true, AllowAdd = true, AllowEdit = true });
                return Task.CompletedTask;
            });
            using var planner = _factory.CreateAuthorizedClient(userId);

            (await planner.GetAsync($"/api/v1/menu/dishes/{dish}/recipes")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await planner.PostAsJsonAsync($"/api/v1/menu/recipes/{r}/approve", new { })).StatusCode.Should().Be(HttpStatusCode.Forbidden, "OPS-PLAN không thuộc nhóm duyệt thực đơn (CEO, COO)");
        }
    }
}
