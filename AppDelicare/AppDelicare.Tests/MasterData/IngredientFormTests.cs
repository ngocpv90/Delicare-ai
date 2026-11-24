using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.MasterData
{
    /// <summary>UI-6.20 (D-101): luật lưu nguyên liệu – loại bảo quản 3 mã, 1 công tắc hạn dùng, mặc định lô / QC, đơn vị mua / xuất phải quy đổi được, cảnh báo ngừng dùng.</summary>
    public class IngredientFormTests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public IngredientFormTests(InMemoryApiFactory factory) => _factory = factory;

        [Fact]
        public async Task Save_normalizes_storage_expiry_lot_qc_units_and_yield()
        {
            Guid cat = Guid.NewGuid(), kg = Guid.NewGuid(), box = Guid.NewGuid(), bag = Guid.NewGuid();
            string k = cat.ToString("N")[..5].ToUpperInvariant();
            await _factory.SeedAsync(db =>
            {
                db.IngredientCategory.Add(new IngredientCategory { Id = cat, Code = "IF" + k, Name = "Thịt " + k, Status = "Active" });
                db.UnitMeasure.AddRange(new UnitMeasure { Id = kg, Code = "kg" + k, Name = "kg", Status = "Active" }, new UnitMeasure { Id = box, Code = "th" + k, Name = "thùng 10 kg", Status = "Active" },
                    new UnitMeasure { Id = bag, Code = "bao" + k, Name = "bao", Status = "Active" });
                db.UomConversion.Add(new UomConversion { Id = Guid.NewGuid(), FromUomId = box, ToUomId = kg, FromUnitId = box, ToUnitId = kg, ConversionFactor = 10, IsActive = true, Status = "Active" });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();
            (string, object)[] baseOk = [("IngredientCategoryId", cat), ("BaseUnitId", kg)];
            (await c.PostFormAsync("/api/Ingredient/Insert", [("Name", "Lạ " + k), ("StorageType", "TU_LANH"), .. baseOk])).Fail("bảo quản");
            (await c.PostFormAsync("/api/Ingredient/Insert", [("Name", "Không nhóm " + k), ("StorageType", "DRY"), ("BaseUnitId", kg)])).Fail("nhóm nguyên liệu");
            (await c.PostFormAsync("/api/Ingredient/Insert", [("Name", "Hạn " + k), ("StorageType", "DRY"), ("ExpiryControl", true), .. baseOk])).Fail("hạn dùng");
            (await c.PostFormAsync("/api/Ingredient/Insert", [("Name", "Bao " + k), ("StorageType", "DRY"), ("PurchaseUomId", bag), .. baseOk])).Fail("quy đổi");

            (await c.PostFormAsync("/api/Ingredient/Insert", [("Name", "Bò " + k), ("StorageType", "chilled"), ("ExpiryControl", true), ("ShelfLifeDays", 5),
                ("PurchaseUomId", box), ("StandardWastePercent", 12m), ("YieldPercent", 50m), .. baseOk])).Ok();
            var beef = await _factory.QueryAsync(db => db.Ingredient.AsNoTracking().SingleAsync(x => x.Name == "Bò " + k));
            beef.StorageType.Should().Be("CHILLED");
            beef.IsExpiryManaged.Should().BeTrue();
            beef.ExpiryControl.Should().BeTrue("2 cờ hạn dùng đồng bộ");
            beef.LotControl.Should().BeTrue("có hạn dùng → quản lý lô");
            beef.QcRequired.Should().BeTrue("Mát → kiểm tra khi nhận");
            beef.PurchaseUomId.Should().Be(box);
            beef.IssueUomId.Should().Be(kg, "trống = đơn vị kho");
            beef.YieldPercent.Should().Be(88, "= 100 − hao hụt chuẩn");
            beef.CategoryId.Should().Be(cat);
            beef.BaseUomId.Should().Be(kg);

            (await c.PostFormAsync("/api/Ingredient/Insert", [("Name", "Gạo " + k), ("StorageType", "AMBIENT"), .. baseOk])).Ok();
            var rice = await _factory.QueryAsync(db => db.Ingredient.AsNoTracking().SingleAsync(x => x.Name == "Gạo " + k));
            rice.StorageType.Should().Be("DRY", "AMBIENT cũ = Khô / thường");
            rice.QcRequired.Should().BeFalse();
            rice.LotControl.Should().BeFalse();

            // Ngừng dùng nguyên liệu còn trong công thức đang dùng → lưu được, kèm cảnh báo số chứng từ.
            Guid dish = Guid.NewGuid(), recipe = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Dish.Add(new Dish { Id = dish, Code = "MA" + k, Name = "Bò kho " + k, Status = "Active" });
                db.Recipe.Add(new Recipe { Id = recipe, DishId = dish, VersionNo = 1, VersionCode = "V1", Status = "Locked", IsActive = true });
                db.RecipeLine.Add(new RecipeLine { Id = Guid.NewGuid(), RecipeId = recipe, IngredientId = beef.Id, Quantity = 10 });
                return Task.CompletedTask;
            });
            var off = (await c.PutFormAsync("/api/Ingredient/Update", [("Id", beef.Id), ("Name", "Bò " + k), ("StorageType", "CHILLED"), ("ExpiryControl", true), ("ShelfLifeDays", 5), ("Status", "Inactive"), .. baseOk]));
            off.Ok();
            off.Message.Should().Contain("Bò kho " + k);
            (await _factory.QueryAsync(db => db.Ingredient.AsNoTracking().SingleAsync(x => x.Id == beef.Id))).Status.Should().Be("Inactive");
        }
    }
}
