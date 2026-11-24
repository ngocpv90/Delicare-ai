using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Menu
{
    /// <summary>
    /// BIZ-3.3 (D-77 – C10): sửa giá NCC của thịt 100.000 → 300.000 đ/kg → giá thành chuẩn món (10 kg / 100 suất) tự tính lại 10.000 → 30.000 đ/suất;
    /// thực đơn Đã công bố có món, giá bán suất trưa thấp nhất 40.000 → FC 75 % > 55 % → cảnh báo trong kết quả lưu.
    /// </summary>
    public class SupplierPriceCostTests(InMemoryApiFactory factory) : IClassFixture<InMemoryApiFactory>
    {
        [Fact]
        public async Task Supplier_price_change_recalculates_standard_cost_and_warns_food_cost()
        {
            string k = Guid.NewGuid().ToString("N")[..5];
            Guid kg = Guid.NewGuid(), meat = Guid.NewGuid(), dish = Guid.NewGuid(), recipe = Guid.NewGuid(), supplier = Guid.NewGuid(), sm = Guid.NewGuid(), menu = Guid.NewGuid(), customer = Guid.NewGuid(), contract = Guid.NewGuid();
            factory.Clock.FixedNow = new DateTime(2032, 3, 2, 9, 0, 0);
            await factory.SeedAsync(db =>
            {
                db.UnitMeasure.Add(new UnitMeasure { Id = kg, Code = "KG" + k, Name = "kg" });
                db.Ingredient.Add(new Ingredient { Id = meat, Code = "TH" + k, Name = "Thịt " + k, BaseUnitId = kg });
                db.Dish.Add(new Dish { Id = dish, Code = "M" + k, Name = "Món " + k, Status = "Active" });
                db.Recipe.Add(new Recipe { Id = recipe, DishId = dish, VersionNo = 1, Status = "Locked", IsActive = true, StandardOutputQty = 100, EffectiveFrom = new DateTime(2032, 1, 1), LockedAt = new DateTime(2032, 1, 1) });
                db.RecipeLine.Add(new RecipeLine { Id = Guid.NewGuid(), RecipeId = recipe, IngredientId = meat, Quantity = 10, UnitId = kg, UomId = kg, SequenceNo = 1 });
                db.Supplier.Add(new Supplier { Id = supplier, Code = "NCC" + k, Name = "NCC " + k, Status = "Active" });
                db.SupplierMaterial.Add(new SupplierMaterial { Id = sm, SupplierId = supplier, IngredientId = meat, IsPreferred = true, UnitPrice = 100000, PurchaseUomId = kg, EffectiveFrom = new DateTime(2032, 1, 1), Status = "Active" });
                db.Menu.Add(new Model.Entity.Menu { Id = menu, Code = "TD" + k, Name = "TĐ " + k, VersionNo = 1, Status = "Published", MenuType = "COMMON", EffectiveFrom = new DateTime(2032, 3, 2) });
                db.MenuLine.Add(new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = dish, MealType = "TRUA", DayOfWeek = 2, SequenceNo = 1, SortOrder = 1 });
                db.Customer.Add(new Customer { Id = customer, Code = "K" + k, Name = "Khách " + k, DisplayName = "Khách " + k, CIF = "K" + k, CustomerType = "FACTORY", Status = "Active" });
                db.Contract.Add(new Contract { Id = contract, CustomerId = customer, ContractNumber = "HD" + k, Status = "Active", EffectiveFrom = new DateTime(2032, 1, 1) });
                db.ContractMeal.Add(new ContractMeal { Id = Guid.NewGuid(), ContractId = contract, MealType = "TRUA", MealName = "Suất trưa", UnitPrice = 40000 });
                return Task.CompletedTask;
            });
            using var c = factory.CreateAuthorizedClient();
            // UI-6.19 (D-100): giá đã áp dụng không sửa đè – sửa thẳng bị chặn, lưu thành giá mới từ hôm nay (giá cũ tự kết thúc hôm trước).
            (await c.PutFormAsync("/api/SupplierMaterial/Update", ("Id", sm), ("SupplierId", supplier), ("IngredientId", meat), ("UnitPrice", 300000m), ("PurchaseUomId", kg), ("IsPreferred", true), ("EffectiveFrom", new DateTime(2032, 1, 1)), ("Status", "Active"))).Fail("lưu thành giá mới");
            var saved = await c.PostFormAsync("/api/SupplierMaterial/SaveNewPrice", ("SupplierId", supplier), ("IngredientId", meat), ("UnitPrice", 300000m),
                ("PurchaseUomId", kg), ("IsPreferred", true), ("EffectiveFrom", new DateTime(2032, 3, 2)), ("Status", "Active"));
            saved.Ok();
            (await factory.QueryAsync(db => db.SupplierMaterial.AsNoTracking().SingleAsync(x => x.Id == sm))).EffectiveTo.Should().Be(new DateTime(2032, 3, 1), "giá cũ tự kết thúc hôm trước");
            saved.Message.Should().Contain("đã tự tính lại giá thành chuẩn 1 món").And.Contain("TD" + k).And.Contain("FC 75%");
            (await factory.QueryAsync(db => db.Dish.AsNoTracking().SingleAsync(d => d.Id == dish))).StandardCost.Should().Be(30000);
        }
    }
}
