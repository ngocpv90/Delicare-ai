
using System.Text.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Services.Implement;
using AppDelicare.Services.Interface;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Purchasing
{
    /// <summary>
    /// T14 (Pilot P5) – tính nhu cầu NL (MaterialRequirement/Calculate) với bộ dữ liệu nhỏ tính tay được.
    /// Hôm nay (đồng hồ test) = 14/12/2026; mỗi test dùng ngày sản xuất riêng (MRP lấy mọi đơn Đã chốt của ngày).
    /// <para>Bộ dữ liệu (ngày SX = thứ 3): 2 đơn Locked TRUA 200 + 100 suất cùng thực đơn; 1 đơn Confirmed (không tính, cảnh báo).
    /// Thực đơn TRUA thứ 3: lựa chọn 1 = Thịt kho + Cơm + Món chưa có CT, lựa chọn 2 = Rau xào + Cơm → 300 suất chia đều 2 lựa chọn:
    /// Thịt kho 150, Rau xào 150, Cơm 300, Món chưa có CT 150.</para>
    /// <list type="bullet">
    /// <item>Thịt kho: CT đã khóa, snapshot 15 000 g thịt / 100 suất, hao hụt 10% (dòng định lượng hiện tại 99 999 g – không được dùng)
    ///   → 15 kg × 150/100 × 1,1 = <b>24,75 kg</b>. Tồn: lô tốt 10 kg + lô hết hạn trước ngày SX 5 kg (bị chặn). Tồn an toàn (chính sách kho) 2.
    ///   Thiếu 24,75 + 2 − 10 = 16,75 → MOQ NCC ưu tiên 20 kg → mua <b>20</b> × 100 000 = 2 000 000.</item>
    /// <item>Rau xào: CT chỉ Đã duyệt (cảnh báo), 10 kg / 100 suất, hao hụt lấy Ingredient.StandardWastePercent 20% → 10 × 1,5 × 1,2 = <b>18 kg</b>.
    ///   Tồn: lô QC KHONG_DAT 20 kg (bị chặn). Thiếu = mua <b>18</b> × 15 000 = 270 000; NCC giao 3 ngày → cảnh báo.</item>
    /// <item>Cơm: CT khóa, 12 kg gạo / 100 suất → 12 × 3 = <b>36 kg</b>. Tồn 50 000 g (quy đổi 50 kg). Tồn an toàn Ingredient.SafetyStock 20.
    ///   Thiếu 36 + 20 − 50 = <b>6</b>, chưa có NCC → mua 6, cảnh báo.</item>
    /// </list>
    /// </summary>
    public class MrpTests : IClassFixture<InMemoryApiFactory>
    {
        private static readonly DateTime Today = new(2026, 12, 14);
        private readonly InMemoryApiFactory _factory;

        public MrpTests(InMemoryApiFactory factory)
        {
            _factory = factory;
            _factory.Clock.FixedNow = Today.AddHours(10);
        }

        private sealed record Scenario(DateTime Date, Guid Warehouse, Guid Kg, Guid Meat, Guid Veg, Guid Rice);

        private static string Snapshot(params (Guid Ingredient, decimal Quantity, Guid Unit, decimal? Waste)[] lines)
            => JsonSerializer.Serialize(lines.Select(l => new { IngredientId = l.Ingredient, l.Quantity, UnitId = l.Unit, WastePercent = l.Waste, SequenceNo = 1 }));

        private async Task<Scenario> SeedAsync(string code, DateTime date)
        {
            Guid warehouse = Guid.NewGuid(), kg = Guid.NewGuid(), g = Guid.NewGuid();
            Guid meat = Guid.NewGuid(), veg = Guid.NewGuid(), rice = Guid.NewGuid();
            Guid dishMeat = Guid.NewGuid(), dishVeg = Guid.NewGuid(), dishRice = Guid.NewGuid(), dishNoRecipe = Guid.NewGuid();
            Guid menu = Guid.NewGuid(), customer = Guid.NewGuid();
            Guid s1 = Guid.NewGuid(), s2 = Guid.NewGuid(), s3 = Guid.NewGuid();
            int day = date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;
            await _factory.SeedAsync(async db =>
            {
                db.Warehouse.Add(new Warehouse { Id = warehouse, Code = code, Name = "Kho " + code, Status = "Active" });
                db.UnitMeasure.AddRange(new UnitMeasure { Id = kg, Code = "KG-" + code, Name = "kg" }, new UnitMeasure { Id = g, Code = "G-" + code, Name = "g" });
                db.UomConversion.Add(new UomConversion { Id = Guid.NewGuid(), FromUnitId = kg, ToUnitId = g, FromUomId = kg, ToUomId = g, ConversionFactor = 1000, IsActive = true });
                db.Ingredient.AddRange(
                    new Ingredient { Id = meat, Code = "THIT-" + code, Name = "Thịt " + code, BaseUnitId = kg, StandardWastePercent = 50 },
                    new Ingredient { Id = veg, Code = "RAU-" + code, Name = "Rau " + code, BaseUnitId = kg, StandardWastePercent = 20 },
                    new Ingredient { Id = rice, Code = "GAO-" + code, Name = "Gạo " + code, BaseUnitId = kg, SafetyStock = 20 });
                db.InventoryPolicy.Add(new InventoryPolicy { Id = Guid.NewGuid(), IngredientId = meat, WarehouseId = warehouse, SafetyStock = 2, UseFefo = true, Status = "Active" });

                db.Dish.AddRange(
                    new Dish { Id = dishMeat, Code = "TK-" + code, Name = "Thịt kho " + code },
                    new Dish { Id = dishVeg, Code = "RX-" + code, Name = "Rau xào " + code },
                    new Dish { Id = dishRice, Code = "COM-" + code, Name = "Cơm " + code },
                    new Dish { Id = dishNoRecipe, Code = "X-" + code, Name = "Món chưa có CT " + code });
                Guid rMeat = Guid.NewGuid(), rVeg = Guid.NewGuid(), rRice = Guid.NewGuid();
                db.Recipe.AddRange(
                    new Recipe { Id = rMeat, DishId = dishMeat, VersionNo = 2, Status = "Locked", IsActive = true, StandardOutputQty = 100, EffectiveFrom = new DateTime(2026, 12, 1),
                        LockedAt = new DateTime(2026, 12, 1), LockedBy = InMemoryApiFactory.UserId, SnapshotJson = Snapshot((meat, 15000, g, 10)) },
                    new Recipe { Id = rVeg, DishId = dishVeg, VersionNo = 1, Status = "Approved", StandardOutputQty = 100 },
                    new Recipe { Id = rRice, DishId = dishRice, VersionNo = 1, Status = "Locked", IsActive = true, StandardOutputQty = 100, EffectiveFrom = new DateTime(2026, 11, 1),
                        LockedAt = new DateTime(2026, 11, 1), SnapshotJson = Snapshot((rice, 12, kg, null)) });
                db.RecipeLine.AddRange(
                    new RecipeLine { Id = Guid.NewGuid(), RecipeId = rMeat, IngredientId = meat, Quantity = 99999, UnitId = g, UomId = g, WastePercent = 10 },
                    new RecipeLine { Id = Guid.NewGuid(), RecipeId = rVeg, IngredientId = veg, Quantity = 10, UnitId = kg, UomId = kg },
                    new RecipeLine { Id = Guid.NewGuid(), RecipeId = rRice, IngredientId = rice, Quantity = 12, UnitId = kg, UomId = kg });

                db.Menu.Add(new Model.Entity.Menu { Id = menu, Code = "TD-" + code, Name = "Thực đơn " + code, VersionNo = 1, Status = "Published", EffectiveFrom = new DateTime(2026, 12, 1) });
                db.MenuLine.AddRange(
                    new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = dishMeat, MealType = "TRUA", DayOfWeek = day, SequenceNo = 1 },
                    new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = dishRice, MealType = "TRUA", DayOfWeek = day, SequenceNo = 1 },
                    new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = dishNoRecipe, MealType = "TRUA", DayOfWeek = day, SequenceNo = 1 },
                    new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = dishVeg, MealType = "TRUA", DayOfWeek = day, SequenceNo = 2 },
                    new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = dishRice, MealType = "TRUA", DayOfWeek = day, SequenceNo = 2 },
                    // Thứ khác – không được tính.
                    new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = dishMeat, MealType = "TRUA", DayOfWeek = day % 7 + 1, SequenceNo = 1 });

                db.Customer.Add(new Customer { Id = customer, Code = code, Name = "Khách " + code, CIF = code, CustomerType = "FACTORY", DisplayName = code, Status = "Active" });
                foreach (var (status, quantity) in new[] { ("Locked", 200m), ("Locked", 100m), ("Confirmed", 999m) })
                {
                    Guid order = Guid.NewGuid();
                    db.Order.Add(new Order { Id = order, OrderNumber = "PH-" + code + quantity, OrderNo = "PH-" + code + quantity, CustomerId = customer, OrderDate = Today, MealDate = date, DeliveryDate = date, Status = status, TotalQuantity = quantity });
                    db.OrderLine.Add(new OrderLine { Id = Guid.NewGuid(), OrderId = order, MenuId = menu, MealType = "TRUA", Quantity = quantity });
                }

                db.Supplier.AddRange(
                    new Supplier { Id = s1, Code = "NCC1-" + code, Name = "NCC thịt ưu tiên", Status = "Active" },
                    new Supplier { Id = s2, Code = "NCC2-" + code, Name = "NCC thịt rẻ", Status = "Active" },
                    new Supplier { Id = s3, Code = "NCC3-" + code, Name = "NCC rau", Status = "Active" });
                db.SupplierMaterial.AddRange(
                    new SupplierMaterial { Id = Guid.NewGuid(), SupplierId = s1, IngredientId = meat, IsPreferred = true, UnitPrice = 100000, PurchaseUomId = kg, MinimumOrderQuantity = 20, LeadTimeDays = 1, EffectiveFrom = new DateTime(2026, 1, 1), Status = "Active" },
                    new SupplierMaterial { Id = Guid.NewGuid(), SupplierId = s2, IngredientId = meat, IsPreferred = false, UnitPrice = 90000, PurchaseUomId = kg, EffectiveFrom = new DateTime(2026, 1, 1), Status = "Active" },
                    new SupplierMaterial { Id = Guid.NewGuid(), SupplierId = s3, IngredientId = veg, IsPreferred = false, UnitPrice = 15000, PurchaseUomId = kg, LeadTimeDays = 3, Status = "Active" },
                    // Hết hiệu lực – bỏ qua.
                    new SupplierMaterial { Id = Guid.NewGuid(), SupplierId = s3, IngredientId = rice, IsPreferred = true, UnitPrice = 1, EffectiveTo = new DateTime(2026, 6, 30), Status = "Active" });

                var ledger = new InventoryLedgerService(db);
                async Task Receive(Guid ingredient, Guid unit, decimal quantity, int expiryInDays, string? qc, bool lot = true)
                {
                    Guid? lotId = null;
                    if (lot)
                    {
                        lotId = Guid.NewGuid();
                        string number = "L-" + code + "-" + Guid.NewGuid().ToString("N")[..4];
                        db.MaterialLot.Add(new MaterialLot { Id = lotId.Value, IngredientId = ingredient, LotNumber = number, LotNo = number, ExpiryDate = Today.AddDays(expiryInDays), ReceivedDate = Today.AddDays(-1), QcStatus = qc, QualityStatus = qc, Status = "Available" });
                    }
                    await ledger.PostAsync(new InventoryPostRequest { TransactionType = "RECEIPT", TransactionNumber = "NK-" + code, WarehouseId = warehouse, IngredientId = ingredient, MaterialLotId = lotId, UnitId = unit, QuantityIn = quantity });
                }
                await Receive(meat, kg, 10, 6, "DAT");
                await Receive(meat, kg, 5, 0, "DAT");          // hết hạn hôm nay → trước ngày SX → bị chặn
                await Receive(veg, kg, 20, 5, "KHONG_DAT");    // QC không đạt → bị chặn
                await Receive(rice, g, 50000, 0, null, lot: false); // không lô, ghi theo g
            });
            return new Scenario(date, warehouse, kg, meat, veg, rice);
        }

        private static Task<LegacyResult> Calculate(HttpClient c, Scenario s)
            => c.PostFormAsync("/api/MaterialRequirement/Calculate", ("ProductionDate", s.Date), ("WarehouseId", s.Warehouse), ("Note", "[DEMO] MRP thử"));

        private static JsonElement Item(JsonElement result, Guid ingredient)
            => result.GetProperty("listItem").EnumerateArray().Single(i => i.GetProperty("ingredientId").GetGuid() == ingredient);

        [Fact]
        public async Task Calculate_hand_checked_requirements_shortage_safety_stock_moq_and_draft_purchase_request()
        {
            var s = await SeedAsync("MRP1", new DateTime(2026, 12, 15));
            using var c = _factory.CreateAuthorizedClient();

            var before = await c.GetResultAsync("/api/MaterialRequirement/GetByDate?productionDate=2026-12-15");
            before.Code.Should().Be(200);
            before.Data.ValueKind.Should().Be(JsonValueKind.Null, "chưa tính");

            var result = (await Calculate(c, s)).Ok();
            result.GetProperty("planNumber").GetString().Should().Be("KH-261215");
            result.GetProperty("planStatus").GetString().Should().Be("Draft");
            result.GetProperty("orderCount").GetInt32().Should().Be(2, "chỉ đơn Đã chốt");
            result.GetProperty("totalMeals").GetDecimal().Should().Be(300);

            var dishes = result.GetProperty("listDish").EnumerateArray().ToDictionary(d => d.GetProperty("dishName").GetString()!, d => d);
            dishes["Thịt kho MRP1"].GetProperty("portions").GetDecimal().Should().Be(150);
            dishes["Thịt kho MRP1"].GetProperty("recipeSource").GetString().Should().Be("Snapshot");
            dishes["Cơm MRP1"].GetProperty("portions").GetDecimal().Should().Be(300);
            dishes["Rau xào MRP1"].GetProperty("portions").GetDecimal().Should().Be(150);
            dishes["Rau xào MRP1"].GetProperty("recipeSource").GetString().Should().Be("Lines");
            dishes["Món chưa có CT MRP1"].GetProperty("recipeId").ValueKind.Should().Be(JsonValueKind.Null);

            var meat = Item(result, s.Meat);
            meat.GetProperty("requiredQuantity").GetDecimal().Should().Be(24.75m);
            meat.GetProperty("availableQuantity").GetDecimal().Should().Be(10);
            meat.GetProperty("blockedQuantity").GetDecimal().Should().Be(5, "lô hết hạn trước ngày sản xuất");
            meat.GetProperty("safetyStock").GetDecimal().Should().Be(2);
            meat.GetProperty("shortageQuantity").GetDecimal().Should().Be(16.75m);
            meat.GetProperty("purchaseQuantity").GetDecimal().Should().Be(20, "làm tròn lên MOQ 20 kg");
            meat.GetProperty("status").GetString().Should().Be("Requested");
            meat.GetProperty("unitName").GetString().Should().Be("kg");
            var meatSupplier = meat.GetProperty("supplier");
            meatSupplier.GetProperty("supplierName").GetString().Should().Be("NCC thịt ưu tiên", "ưu tiên trước giá rẻ");
            meatSupplier.GetProperty("estimatedAmount").GetDecimal().Should().Be(2_000_000);
            meat.GetProperty("coveragePercent").GetDecimal().Should().Be(Math.Round(10m / 24.75m * 100m, 2));

            var veg = Item(result, s.Veg);
            veg.GetProperty("requiredQuantity").GetDecimal().Should().Be(18);
            veg.GetProperty("availableQuantity").GetDecimal().Should().Be(0);
            veg.GetProperty("blockedQuantity").GetDecimal().Should().Be(20, "lô KHONG_DAT");
            veg.GetProperty("purchaseQuantity").GetDecimal().Should().Be(18);
            veg.GetProperty("supplier").GetProperty("estimatedAmount").GetDecimal().Should().Be(270_000);

            var rice = Item(result, s.Rice);
            rice.GetProperty("requiredQuantity").GetDecimal().Should().Be(36);
            rice.GetProperty("availableQuantity").GetDecimal().Should().Be(50, "50 000 g quy đổi về kg");
            rice.GetProperty("safetyStock").GetDecimal().Should().Be(20);
            rice.GetProperty("purchaseQuantity").GetDecimal().Should().Be(6);
            rice.GetProperty("supplier").ValueKind.Should().Be(JsonValueKind.Null);
            rice.GetProperty("warning").GetString().Should().Contain("Chưa có NCC");

            result.GetProperty("estimatedAmount").GetDecimal().Should().Be(2_270_000);
            var warnings = result.GetProperty("listWarning").EnumerateArray().Select(w => w.GetString()).ToList();
            warnings.Should().Contain(w => w!.Contains("1 đơn chưa chốt"));
            warnings.Should().Contain(w => w!.Contains("Món chưa có CT MRP1"));
            warnings.Should().Contain(w => w!.Contains("Rau xào MRP1") && w.Contains("chưa khóa"));
            warnings.Should().Contain(w => w!.Contains("NCC rau") && w.Contains("3 ngày"));

            var pr = result.GetProperty("purchaseRequest");
            pr.GetProperty("requestNumber").GetString().Should().MatchRegex(@"^PH-261214-\d{4}$");
            pr.GetProperty("status").GetString().Should().Be("Draft");
            pr.GetProperty("lineCount").GetInt32().Should().Be(3);
            Guid prId = pr.GetProperty("id").GetGuid();
            Guid planId = result.GetProperty("productionPlanId").GetGuid();
            var prLines = await _factory.QueryAsync(db => db.PurchaseRequestLine.AsNoTracking().Where(l => l.PurchaseRequestId == prId && !l.IsDeleted).ToListAsync());
            prLines.Single(l => l.IngredientId == s.Meat).Quantity.Should().Be(20);
            prLines.Should().OnlyContain(l => l.UnitId == s.Kg && l.UomId == s.Kg && l.RequiredDate == s.Date);
            var rows = await _factory.QueryAsync(db => db.MaterialRequirement.AsNoTracking().Where(r => r.ProductionPlanId == planId && !r.IsDeleted).ToListAsync());
            rows.Should().HaveCount(3);
            rows.Should().OnlyContain(r => r.RequiredQty == r.RequiredQuantity && r.PurchaseQty == r.PurchaseQuantity, "ghi song song cột cũ");
            (await _factory.QueryAsync(db => db.ProductionPlanLine.CountAsync(l => l.ProductionPlanId == planId && !l.IsDeleted))).Should().Be(4);

            // Tính lại sau khi nhập thêm 10 kg gạo: gạo đủ → dòng đề nghị mua gạo bị xóa mềm; cùng kế hoạch, cùng đề nghị mua, không thêm dòng nhu cầu.
            // Dòng tồn gạo không lô đang ghi theo g → nhập 10 000 g.
            Guid gram = await _factory.QueryAsync(db => db.InventoryBalance.Where(b => b.IngredientId == s.Rice).Select(b => b.UnitId).FirstAsync());
            await _factory.SeedAsync(db => new InventoryLedgerService(db).PostAsync(new InventoryPostRequest
            {
                TransactionType = "RECEIPT", TransactionNumber = "NK-MRP1-2", WarehouseId = s.Warehouse, IngredientId = s.Rice, UnitId = gram, QuantityIn = 10000,
            }));
            var second = (await Calculate(c, s)).Ok();
            second.GetProperty("productionPlanId").GetGuid().Should().Be(planId);
            second.GetProperty("purchaseRequest").GetProperty("id").GetGuid().Should().Be(prId);
            second.GetProperty("purchaseRequest").GetProperty("lineCount").GetInt32().Should().Be(2);
            var rice2 = Item(second, s.Rice);
            rice2.GetProperty("status").GetString().Should().Be("Sufficient");
            rice2.GetProperty("purchaseQuantity").GetDecimal().Should().Be(0);
            (await _factory.QueryAsync(db => db.MaterialRequirement.CountAsync(r => r.ProductionPlanId == planId && !r.IsDeleted))).Should().Be(3);
            (await _factory.QueryAsync(db => db.PurchaseRequest.CountAsync(p => p.Note != null && p.Note.Contains("KH-261215") && !p.IsDeleted))).Should().Be(1);

            var byDate = (await c.GetResultAsync($"/api/MaterialRequirement/GetByDate?productionDate=2026-12-15")).Ok();
            byDate.GetProperty("productionPlanId").GetGuid().Should().Be(planId);
            byDate.GetProperty("warehouseId").GetGuid().Should().Be(s.Warehouse);
            (await c.GetResultAsync($"/api/MaterialRequirement/GetDetail?productionPlanId={planId}")).Ok().GetProperty("listItem").GetArrayLength().Should().Be(3);

            // Đề nghị mua đã gửi duyệt → không tính lại.
            await _factory.SeedAsync(async db => (await db.PurchaseRequest.SingleAsync(p => p.Id == prId)).Status = "PendingApproval");
            (await Calculate(c, s)).Fail("không tính lại");
        }

        [Fact]
        public async Task Calculate_requires_locked_orders_and_crud_cannot_write_requirements()
        {
            using var c = _factory.CreateAuthorizedClient();
            (await c.PostFormAsync("/api/MaterialRequirement/Calculate", ("ProductionDate", new DateTime(2026, 12, 20)))).Fail("Không có đơn Đã chốt");
            (await c.PostFormAsync("/api/MaterialRequirement/Calculate", ("ProductionDate", new DateTime(2026, 12, 20)), ("WarehouseId", Guid.NewGuid()))).Fail("Không tìm thấy kho");
            (await c.PostFormAsync("/api/MaterialRequirement/Insert", ("ProductionPlanId", Guid.NewGuid()), ("IngredientId", Guid.NewGuid()), ("RequiredQuantity", 1m))).Fail("Calculate");
            (await c.DeleteResultAsync($"/api/MaterialRequirement/Delete?id={Guid.NewGuid()}")).Fail("Calculate");
        }

        [Fact]
        public async Task Calculate_on_approved_plan_is_blocked()
        {
            var s = await SeedAsync("MRP3", new DateTime(2026, 12, 17));
            using var c = _factory.CreateAuthorizedClient();
            Guid planId = (await Calculate(c, s)).Ok().GetProperty("productionPlanId").GetGuid();
            await _factory.SeedAsync(async db => (await db.ProductionPlan.SingleAsync(p => p.Id == planId)).Status = "Approved");
            (await Calculate(c, s)).Fail("không tính lại nhu cầu");
        }
    }
}
