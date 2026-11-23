using System.Net.Http.Json;
using System.Text.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Option;
using AppDelicare.Services.Implement;
using AppDelicare.Services.Interface;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
    ///   → 15 kg × 150/100 ÷ (1 − 10 %) = <b>25 kg</b> (BIZ-1.1 – công thức chia). Tồn: lô tốt 10 kg + lô hết hạn trước ngày SX 5 kg (bị chặn). Tồn an toàn (chính sách kho) 2.
    ///   Thiếu 25 + 2 − 10 = 17 → MOQ NCC ưu tiên 20 kg → mua <b>20</b> × 100 000 = 2 000 000.</item>
    /// <item>Rau xào: CT chỉ Đã duyệt (cảnh báo), 10 kg / 100 suất, hao hụt lấy Ingredient.StandardWastePercent 20% → 10 × 1,5 ÷ 0,8 = <b>18,75 kg</b>.
    ///   Tồn: lô QC KHONG_DAT 20 kg (bị chặn). Thiếu = mua <b>18,75</b> × 15 000 = 281 250; NCC giao 3 ngày → cảnh báo.</item>
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

        /// <summary>T14.1: đặt tạm <c>BusinessRules:MrpOptionBufferPct</c> (IOptions singleton của factory) – trả lại giá trị cũ khi Dispose.</summary>
        private IDisposable OptionBuffer(decimal pct)
        {
            var rules = _factory.Services.GetRequiredService<IOptions<BusinessRulesOptions>>().Value;
            decimal old = rules.MrpOptionBufferPct;
            rules.MrpOptionBufferPct = pct;
            return new Restore(() => rules.MrpOptionBufferPct = old);
        }

        private sealed class Restore(Action action) : IDisposable
        {
            public void Dispose() => action();
        }

        [Fact]
        public async Task Calculate_hand_checked_requirements_shortage_safety_stock_moq_and_draft_purchase_request()
        {
            // Số tay của T14 (chia đều 2 lựa chọn, không dự phòng) – dự phòng lựa chọn T14.1 thử riêng ở test dưới.
            using var noBuffer = OptionBuffer(0);
            var s = await SeedAsync("MRP1", new DateTime(2026, 12, 15));
            using var c = _factory.CreateAuthorizedClient();

            var before = await c.GetResultAsync("/api/MaterialRequirement/GetByDate?productionDate=2026-12-15");
            before.Code.Should().Be(200);
            before.Data.ValueKind.Should().Be(JsonValueKind.Null, "chưa tính");

            var result = (await Calculate(c, s)).Ok();
            result.GetProperty("planNumber").GetString().Should().Be("KS-261215");
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
            meat.GetProperty("requiredQuantity").GetDecimal().Should().Be(25m);
            meat.GetProperty("availableQuantity").GetDecimal().Should().Be(10);
            meat.GetProperty("blockedQuantity").GetDecimal().Should().Be(5, "lô hết hạn trước ngày sản xuất");
            meat.GetProperty("safetyStock").GetDecimal().Should().Be(2);
            meat.GetProperty("shortageQuantity").GetDecimal().Should().Be(17m);
            meat.GetProperty("purchaseQuantity").GetDecimal().Should().Be(20, "làm tròn lên MOQ 20 kg");
            meat.GetProperty("status").GetString().Should().Be("Requested");
            meat.GetProperty("unitName").GetString().Should().Be("kg");
            var meatSupplier = meat.GetProperty("supplier");
            meatSupplier.GetProperty("supplierName").GetString().Should().Be("NCC thịt ưu tiên", "ưu tiên trước giá rẻ");
            meatSupplier.GetProperty("estimatedAmount").GetDecimal().Should().Be(2_000_000);
            meat.GetProperty("coveragePercent").GetDecimal().Should().Be(Math.Round(10m / 25m * 100m, 2));

            var veg = Item(result, s.Veg);
            veg.GetProperty("requiredQuantity").GetDecimal().Should().Be(18.75m);
            veg.GetProperty("availableQuantity").GetDecimal().Should().Be(0);
            veg.GetProperty("blockedQuantity").GetDecimal().Should().Be(20, "lô KHONG_DAT");
            veg.GetProperty("purchaseQuantity").GetDecimal().Should().Be(18.75m);
            veg.GetProperty("supplier").GetProperty("estimatedAmount").GetDecimal().Should().Be(281_250);

            var rice = Item(result, s.Rice);
            rice.GetProperty("requiredQuantity").GetDecimal().Should().Be(36);
            rice.GetProperty("availableQuantity").GetDecimal().Should().Be(50, "50 000 g quy đổi về kg");
            rice.GetProperty("safetyStock").GetDecimal().Should().Be(20);
            rice.GetProperty("purchaseQuantity").GetDecimal().Should().Be(6);
            rice.GetProperty("supplier").ValueKind.Should().Be(JsonValueKind.Null);
            rice.GetProperty("warning").GetString().Should().Contain("Chưa có NCC");

            result.GetProperty("estimatedAmount").GetDecimal().Should().Be(2_281_250);
            var warnings = result.GetProperty("listWarning").EnumerateArray().Select(w => w.GetString()).ToList();
            warnings.Should().Contain(w => w!.Contains("1 đơn chưa chốt"));
            warnings.Should().Contain(w => w!.Contains("Món chưa có CT MRP1"));
            warnings.Should().Contain(w => w!.Contains("Rau xào MRP1") && w.Contains("chưa khóa"));
            warnings.Should().Contain(w => w!.Contains("NCC rau") && w.Contains("3 ngày"));

            var pr = result.GetProperty("purchaseRequest");
            pr.GetProperty("requestNumber").GetString().Should().MatchRegex(@"^MH-261214-\d{4}$");
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
            (await _factory.QueryAsync(db => db.PurchaseRequest.CountAsync(p => p.Note != null && p.Note.Contains("KS-261215") && !p.IsDeleted))).Should().Be(1);

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

        // ============================== T14.1 ==============================

        /// <summary>Món có công thức đã khóa: <paramref name="kgPer100"/> kg nguyên liệu / 100 suất, không hao hụt.</summary>
        private static (Dish Dish, Recipe Recipe) LockedDish(string code, string name, Guid ingredient, decimal kgPer100, Guid kg)
        {
            var dish = new Dish { Id = Guid.NewGuid(), Code = code, Name = name };
            var recipe = new Recipe
            {
                Id = Guid.NewGuid(), DishId = dish.Id, VersionNo = 1, Status = "Locked", IsActive = true, StandardOutputQty = 100, EffectiveFrom = new DateTime(2026, 12, 1),
                LockedAt = new DateTime(2026, 12, 1), SnapshotJson = Snapshot((ingredient, kgPer100, kg, 0)),
            };
            return (dish, recipe);
        }

        /// <summary>
        /// T14.1 (anh chốt 2026-11-16, GAP-MRP-03): đơn Locked TRUA 100 suất, thực đơn 2 lựa chọn (1 = món A + cơm C, 2 = món B + cơm C) và TOI 40 suất 1 lựa chọn (món D).
        /// Mỗi món 10 kg nguyên liệu riêng / 100 suất. Dự phòng 15%: A = B = 50 × 1,15 = <b>57,5</b> suất (dự phòng 7,5) → 5,75 kg; cơm C có ở cả 2 lựa chọn →
        /// không vượt số suất dòng đơn = <b>100</b> (PROPOSED) → 10 kg; D 1 lựa chọn không dự phòng = <b>40</b> → 4 kg. Cấu hình 0 → A = B = 50.
        /// </summary>
        [Fact]
        public async Task Calculate_multi_option_menu_adds_option_buffer_single_option_none_and_config_zero_disables()
        {
            DateTime date = new(2026, 12, 22);
            int day = date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;
            Guid kg = Guid.NewGuid(), ia = Guid.NewGuid(), ib = Guid.NewGuid(), ic = Guid.NewGuid(), id = Guid.NewGuid(), menu = Guid.NewGuid(), customer = Guid.NewGuid(), order = Guid.NewGuid();
            var a = LockedDish("A-BUF", "Món A BUF", ia, 10, kg);
            var b = LockedDish("B-BUF", "Món B BUF", ib, 10, kg);
            var com = LockedDish("C-BUF", "Cơm BUF", ic, 10, kg);
            var d = LockedDish("D-BUF", "Món D BUF", id, 10, kg);
            await _factory.SeedAsync(db =>
            {
                db.UnitMeasure.Add(new UnitMeasure { Id = kg, Code = "KG-BUF", Name = "kg" });
                db.Ingredient.AddRange(
                    new Ingredient { Id = ia, Code = "NLA-BUF", Name = "NL A BUF", BaseUnitId = kg },
                    new Ingredient { Id = ib, Code = "NLB-BUF", Name = "NL B BUF", BaseUnitId = kg },
                    new Ingredient { Id = ic, Code = "NLC-BUF", Name = "NL C BUF", BaseUnitId = kg },
                    new Ingredient { Id = id, Code = "NLD-BUF", Name = "NL D BUF", BaseUnitId = kg });
                db.Dish.AddRange(a.Dish, b.Dish, com.Dish, d.Dish);
                db.Recipe.AddRange(a.Recipe, b.Recipe, com.Recipe, d.Recipe);
                db.Menu.Add(new Model.Entity.Menu { Id = menu, Code = "TD-BUF", Name = "Thực đơn BUF", VersionNo = 1, Status = "Published", EffectiveFrom = new DateTime(2026, 12, 1) });
                db.MenuLine.AddRange(
                    new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = a.Dish.Id, MealType = "TRUA", DayOfWeek = day, SequenceNo = 1 },
                    new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = com.Dish.Id, MealType = "TRUA", DayOfWeek = day, SequenceNo = 1 },
                    new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = b.Dish.Id, MealType = "TRUA", DayOfWeek = day, SequenceNo = 2 },
                    new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = com.Dish.Id, MealType = "TRUA", DayOfWeek = day, SequenceNo = 2 },
                    new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = d.Dish.Id, MealType = "TOI", DayOfWeek = day, SequenceNo = 1 });
                db.Customer.Add(new Customer { Id = customer, Code = "BUF", Name = "Khách BUF", CIF = "BUF", CustomerType = "FACTORY", DisplayName = "BUF", Status = "Active" });
                db.Order.Add(new Order { Id = order, OrderNumber = "PH-BUF", OrderNo = "PH-BUF", CustomerId = customer, OrderDate = Today, MealDate = date, DeliveryDate = date, Status = "Locked", TotalQuantity = 140 });
                db.OrderLine.AddRange(
                    new OrderLine { Id = Guid.NewGuid(), OrderId = order, MenuId = menu, MealType = "TRUA", Quantity = 100 },
                    new OrderLine { Id = Guid.NewGuid(), OrderId = order, MenuId = menu, MealType = "TOI", Quantity = 40 });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();

            using (OptionBuffer(15))
            {
                var result = (await c.PostFormAsync("/api/MaterialRequirement/Calculate", ("ProductionDate", date))).Ok();
                var dishes = result.GetProperty("listDish").EnumerateArray().ToDictionary(x => x.GetProperty("dishName").GetString()!, x => x);
                dishes["Món A BUF"].GetProperty("portions").GetDecimal().Should().Be(57.5m, "50 × 1,15");
                dishes["Món A BUF"].GetProperty("bufferPortions").GetDecimal().Should().Be(7.5m);
                dishes["Món B BUF"].GetProperty("portions").GetDecimal().Should().Be(57.5m);
                dishes["Cơm BUF"].GetProperty("portions").GetDecimal().Should().Be(100, "món chung cả 2 lựa chọn không vượt số suất dòng đơn");
                dishes["Cơm BUF"].GetProperty("bufferPortions").GetDecimal().Should().Be(0);
                dishes["Món D BUF"].GetProperty("portions").GetDecimal().Should().Be(40, "1 lựa chọn – không dự phòng");
                dishes["Món D BUF"].GetProperty("bufferPortions").GetDecimal().Should().Be(0);
                Item(result, ia).GetProperty("requiredQuantity").GetDecimal().Should().Be(5.75m);
                Item(result, ib).GetProperty("requiredQuantity").GetDecimal().Should().Be(5.75m);
                Item(result, ic).GetProperty("requiredQuantity").GetDecimal().Should().Be(10);
                Item(result, id).GetProperty("requiredQuantity").GetDecimal().Should().Be(4);
                result.GetProperty("totalMeals").GetDecimal().Should().Be(140, "tổng suất đơn không đổi");
            }

            using (OptionBuffer(0))
            {
                var result = (await c.PostFormAsync("/api/MaterialRequirement/Calculate", ("ProductionDate", date))).Ok();
                var dishes = result.GetProperty("listDish").EnumerateArray().ToDictionary(x => x.GetProperty("dishName").GetString()!, x => x);
                dishes["Món A BUF"].GetProperty("portions").GetDecimal().Should().Be(50, "cấu hình 0 = tắt dự phòng");
                dishes["Món A BUF"].GetProperty("bufferPortions").GetDecimal().Should().Be(0);
                dishes["Cơm BUF"].GetProperty("portions").GetDecimal().Should().Be(100);
                Item(result, ia).GetProperty("requiredQuantity").GetDecimal().Should().Be(5);
                Item(result, id).GetProperty("requiredQuantity").GetDecimal().Should().Be(4);
            }
        }

        /// <summary>
        /// T14.1 (anh chốt 2026-11-16) – tính bổ sung. Món M (1 lựa chọn): 10 kg thịt + 1 kg muối / 100 suất, không hao hụt; muối tồn 5 kg; thịt không tồn;
        /// NCC thịt MOQ 2 kg, 100 000/kg. Đơn Locked 100 suất → đề nghị 1: thịt 10. Gửi duyệt đề nghị 1, tăng đơn lên 130 → Calculate bị chặn;
        /// CalculateTopUp → đề nghị bổ sung Nháp mới chỉ có thịt 13 − 10 = <b>3</b> (300 000). Gọi lại → không thiếu thêm, không lập. Giảm đơn còn 110 →
        /// thiếu 11 &lt; đã đề nghị 13 → chỉ cảnh báo thừa 2.
        /// </summary>
        [Fact]
        public async Task CalculateTopUp_creates_new_draft_purchase_request_for_increase_only_and_warns_on_decrease()
        {
            DateTime date = new(2026, 12, 23);
            int day = date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;
            Guid kg = Guid.NewGuid(), meat = Guid.NewGuid(), salt = Guid.NewGuid(), menu = Guid.NewGuid(), customer = Guid.NewGuid(), order = Guid.NewGuid();
            Guid orderLine = Guid.NewGuid(), supplier = Guid.NewGuid(), warehouse = Guid.NewGuid();
            var dish = new Dish { Id = Guid.NewGuid(), Code = "M-TOP", Name = "Món M TOP" };
            await _factory.SeedAsync(async db =>
            {
                db.Warehouse.Add(new Warehouse { Id = warehouse, Code = "TOP", Name = "Kho TOP", Status = "Active" });
                db.UnitMeasure.Add(new UnitMeasure { Id = kg, Code = "KG-TOP", Name = "kg" });
                db.Ingredient.AddRange(
                    new Ingredient { Id = meat, Code = "THIT-TOP", Name = "Thịt TOP", BaseUnitId = kg },
                    new Ingredient { Id = salt, Code = "MUOI-TOP", Name = "Muối TOP", BaseUnitId = kg });
                db.Dish.Add(dish);
                db.Recipe.Add(new Recipe
                {
                    Id = Guid.NewGuid(), DishId = dish.Id, VersionNo = 1, Status = "Locked", IsActive = true, StandardOutputQty = 100, EffectiveFrom = new DateTime(2026, 12, 1),
                    LockedAt = new DateTime(2026, 12, 1), SnapshotJson = Snapshot((meat, 10, kg, 0), (salt, 1, kg, 0)),
                });
                db.Menu.Add(new Model.Entity.Menu { Id = menu, Code = "TD-TOP", Name = "Thực đơn TOP", VersionNo = 1, Status = "Published", EffectiveFrom = new DateTime(2026, 12, 1) });
                db.MenuLine.Add(new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = dish.Id, MealType = "TRUA", DayOfWeek = day, SequenceNo = 1 });
                db.Customer.Add(new Customer { Id = customer, Code = "TOP", Name = "Khách TOP", CIF = "TOP", CustomerType = "FACTORY", DisplayName = "TOP", Status = "Active" });
                db.Order.Add(new Order { Id = order, OrderNumber = "PH-TOP", OrderNo = "PH-TOP", CustomerId = customer, OrderDate = Today, MealDate = date, DeliveryDate = date, Status = "Locked", TotalQuantity = 100 });
                db.OrderLine.Add(new OrderLine { Id = orderLine, OrderId = order, MenuId = menu, MealType = "TRUA", Quantity = 100 });
                db.Supplier.Add(new Supplier { Id = supplier, Code = "NCC-TOP", Name = "NCC thịt TOP", Status = "Active" });
                db.SupplierMaterial.Add(new SupplierMaterial { Id = Guid.NewGuid(), SupplierId = supplier, IngredientId = meat, IsPreferred = true, UnitPrice = 100000, PurchaseUomId = kg, MinimumOrderQuantity = 2, EffectiveFrom = new DateTime(2026, 1, 1), Status = "Active" });
                await new InventoryLedgerService(db).PostAsync(new InventoryPostRequest { TransactionType = "RECEIPT", TransactionNumber = "NK-TOP", WarehouseId = warehouse, IngredientId = salt, UnitId = kg, QuantityIn = 5 });
            });
            using var c = _factory.CreateAuthorizedClient();
            Task<LegacyResult> Calc() => c.PostFormAsync("/api/MaterialRequirement/Calculate", ("ProductionDate", date), ("WarehouseId", warehouse));
            Task<LegacyResult> TopUp() => c.PostFormAsync("/api/MaterialRequirement/CalculateTopUp", ("ProductionDate", date), ("WarehouseId", warehouse), ("Note", "[DEMO] thêm suất"));
            Task SetOrderQuantity(decimal quantity) => _factory.SeedAsync(async db => (await db.OrderLine.SingleAsync(l => l.Id == orderLine)).Quantity = quantity);
            Task<int> PlanRequests() => _factory.QueryAsync(db => db.PurchaseRequest.CountAsync(p => p.Note != null && p.Note.Contains("KS-261223") && !p.IsDeleted));

            (await TopUp()).Fail("Tính nhu cầu (Calculate)"); // chưa tính
            var first = (await Calc()).Ok();
            Guid planId = first.GetProperty("productionPlanId").GetGuid();
            Guid pr1 = first.GetProperty("purchaseRequest").GetProperty("id").GetGuid();
            string pr1No = first.GetProperty("purchaseRequest").GetProperty("requestNumber").GetString()!;
            Item(first, meat).GetProperty("purchaseQuantity").GetDecimal().Should().Be(10);
            Item(first, salt).GetProperty("purchaseQuantity").GetDecimal().Should().Be(0);
            (await TopUp()).Fail("còn Nháp"); // đề nghị còn Nháp → dùng Calculate

            // Gửi duyệt đề nghị 1, khách tăng suất 100 → 130.
            await _factory.SeedAsync(async db => (await db.PurchaseRequest.SingleAsync(p => p.Id == pr1)).Status = "PendingApproval");
            await SetOrderQuantity(130);
            (await Calc()).Fail("CalculateTopUp");

            var topUp = await TopUp();
            topUp.Message.Should().Contain("Đã lập đề nghị mua bổ sung");
            var data = topUp.Ok();
            data.GetProperty("productionPlanId").GetGuid().Should().Be(planId);
            data.GetProperty("listDish")[0].GetProperty("portions").GetDecimal().Should().Be(130);
            var t = data.GetProperty("topUp");
            t.GetProperty("listComparedRequestNumber").EnumerateArray().Select(x => x.GetString()).Should().Equal(pr1No);
            var newPr = t.GetProperty("purchaseRequest");
            Guid pr2 = newPr.GetProperty("id").GetGuid();
            pr2.Should().NotBe(pr1);
            newPr.GetProperty("status").GetString().Should().Be("Draft");
            newPr.GetProperty("requestNumber").GetString().Should().MatchRegex(@"^MH-261214-\d{4}$").And.NotBe(pr1No);
            newPr.GetProperty("lineCount").GetInt32().Should().Be(1);
            newPr.GetProperty("estimatedAmount").GetDecimal().Should().Be(300_000);
            var meatLine = t.GetProperty("listLine").EnumerateArray().Single(l => l.GetProperty("ingredientId").GetGuid() == meat);
            meatLine.GetProperty("shortageQuantity").GetDecimal().Should().Be(13);
            meatLine.GetProperty("requestedQuantity").GetDecimal().Should().Be(10);
            meatLine.GetProperty("differenceQuantity").GetDecimal().Should().Be(3);
            meatLine.GetProperty("topUpQuantity").GetDecimal().Should().Be(3, "3 ≥ MOQ 2 – chỉ phần chênh lệch");
            var saltLine = t.GetProperty("listLine").EnumerateArray().Single(l => l.GetProperty("ingredientId").GetGuid() == salt);
            saltLine.GetProperty("topUpQuantity").GetDecimal().Should().Be(0, "muối 1,3 kg vẫn đủ tồn 5 kg");
            data.GetProperty("purchaseRequest").GetProperty("id").GetGuid().Should().Be(pr1, "đề nghị chính giữ nguyên");
            data.GetProperty("listTopUpPurchaseRequest").GetArrayLength().Should().Be(1);
            var meatRow = Item(data, meat);
            meatRow.GetProperty("requiredQuantity").GetDecimal().Should().Be(13);
            meatRow.GetProperty("purchaseQuantity").GetDecimal().Should().Be(13, "đã đề nghị 10 + bổ sung 3");
            meatRow.GetProperty("status").GetString().Should().Be("Requested");

            var prs = await _factory.QueryAsync(db => db.PurchaseRequest.AsNoTracking().Where(p => p.Id == pr1 || p.Id == pr2).ToListAsync());
            prs.Single(p => p.Id == pr1).Status.Should().Be("PendingApproval");
            prs.Single(p => p.Id == pr2).Note.Should().StartWith("BỔ SUNG").And.Contain(pr1No).And.Contain("[DEMO] thêm suất");
            var lines = await _factory.QueryAsync(db => db.PurchaseRequestLine.AsNoTracking().Where(l => (l.PurchaseRequestId == pr1 || l.PurchaseRequestId == pr2) && !l.IsDeleted).ToListAsync());
            lines.Single(l => l.PurchaseRequestId == pr1).Quantity.Should().Be(10, "đề nghị đã gửi duyệt không bị sửa");
            lines.Single(l => l.PurchaseRequestId == pr2).Should().Match<PurchaseRequestLine>(l => l.IngredientId == meat && l.Quantity == 3 && l.UnitId == kg && l.RequiredDate == date);
            (await _factory.QueryAsync(db => db.MaterialRequirement.CountAsync(r => r.ProductionPlanId == planId && !r.IsDeleted))).Should().Be(2);

            // Gọi lại khi không đổi → không lập thêm.
            var again = await TopUp();
            again.Message.Should().Contain("Không thiếu thêm");
            again.Ok().GetProperty("topUp").GetProperty("purchaseRequest").ValueKind.Should().Be(JsonValueKind.Null);
            (await PlanRequests()).Should().Be(2);

            // Khách giảm còn 110 → thiếu 11 < đã đề nghị 13 → chỉ cảnh báo, không lập / không sửa đề nghị.
            await SetOrderQuantity(110);
            var down = await TopUp();
            down.Message.Should().Contain("Không thiếu thêm");
            var downData = down.Ok();
            var downMeat = downData.GetProperty("topUp").GetProperty("listLine").EnumerateArray().Single(l => l.GetProperty("ingredientId").GetGuid() == meat);
            downMeat.GetProperty("differenceQuantity").GetDecimal().Should().Be(-2);
            downMeat.GetProperty("topUpQuantity").GetDecimal().Should().Be(0);
            downData.GetProperty("topUp").GetProperty("listWarning").EnumerateArray().Select(w => w.GetString())
                .Should().ContainSingle(w => w!.Contains("Thịt TOP") && w.Contains("thừa 2"));
            Item(downData, meat).GetProperty("requiredQuantity").GetDecimal().Should().Be(11);
            (await PlanRequests()).Should().Be(2);
            (await _factory.QueryAsync(db => db.PurchaseRequestLine.Where(l => l.PurchaseRequestId == pr2 && !l.IsDeleted).SumAsync(l => l.Quantity))).Should().Be(3);

            // Đề nghị chính bị hủy nhưng đề nghị bổ sung còn hiệu lực → Calculate vẫn chặn (tránh lập trùng).
            await _factory.SeedAsync(async db => (await db.PurchaseRequest.SingleAsync(p => p.Id == pr1)).Status = "Cancelled");
            (await Calc()).Fail("bổ sung");
        }

        /// <summary>
        /// T15.1 (anh chốt 2026-11-16, GAP-MRP-08 ý 2): "đã đề nghị" trừ phần đã nhận đạt (phiếu nhập Đã ghi sổ) – phần đó đã nằm trong tồn, không bị đề nghị lại.
        /// 100 suất × 10 kg thịt / 100 suất = 10 kg → đề nghị 1 = 10 → lập PO → nhận 6 kg DAT, ghi sổ (tồn 6). Bổ sung khi không đổi suất: thiếu 10 − 6 = 4,
        /// đã đề nghị 10 − 6 = 4 → chênh 0, không lập, không cảnh báo "thừa". Tăng 130 suất: thiếu 13 − 6 = 7, đã đề nghị 4 → bổ sung đúng 3.
        /// </summary>
        [Fact]
        public async Task CalculateTopUp_subtracts_received_qc_passed_quantity_from_already_requested()
        {
            DateTime date = new(2026, 12, 24);
            int day = date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;
            Guid kg = Guid.NewGuid(), meat = Guid.NewGuid(), menu = Guid.NewGuid(), customer = Guid.NewGuid(), order = Guid.NewGuid();
            Guid orderLine = Guid.NewGuid(), supplier = Guid.NewGuid(), warehouse = Guid.NewGuid();
            var dish = new Dish { Id = Guid.NewGuid(), Code = "M-TOP2", Name = "Món M TOP2" };
            await _factory.SeedAsync(db =>
            {
                db.Warehouse.Add(new Warehouse { Id = warehouse, Code = "TOP2", Name = "Kho TOP2", Status = "Active" });
                db.UnitMeasure.Add(new UnitMeasure { Id = kg, Code = "KG-TOP2", Name = "kg" });
                db.Ingredient.Add(new Ingredient { Id = meat, Code = "THIT-TOP2", Name = "Thịt TOP2", BaseUnitId = kg });
                db.Dish.Add(dish);
                db.Recipe.Add(new Recipe
                {
                    Id = Guid.NewGuid(), DishId = dish.Id, VersionNo = 1, Status = "Locked", IsActive = true, StandardOutputQty = 100, EffectiveFrom = new DateTime(2026, 12, 1),
                    LockedAt = new DateTime(2026, 12, 1), SnapshotJson = Snapshot((meat, 10, kg, 0)),
                });
                db.Menu.Add(new Model.Entity.Menu { Id = menu, Code = "TD-TOP2", Name = "Thực đơn TOP2", VersionNo = 1, Status = "Published", EffectiveFrom = new DateTime(2026, 12, 1) });
                db.MenuLine.Add(new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = dish.Id, MealType = "TRUA", DayOfWeek = day, SequenceNo = 1 });
                db.Customer.Add(new Customer { Id = customer, Code = "TOP2", Name = "Khách TOP2", CIF = "TOP2", CustomerType = "FACTORY", DisplayName = "TOP2", Status = "Active" });
                db.Order.Add(new Order { Id = order, OrderNumber = "PH-TOP2", OrderNo = "PH-TOP2", CustomerId = customer, OrderDate = Today, MealDate = date, DeliveryDate = date, Status = "Locked", TotalQuantity = 100 });
                db.OrderLine.Add(new OrderLine { Id = orderLine, OrderId = order, MenuId = menu, MealType = "TRUA", Quantity = 100 });
                db.Supplier.Add(new Supplier { Id = supplier, Code = "NCC-TOP2", Name = "NCC thịt TOP2", Status = "Active" });
                db.SupplierMaterial.Add(new SupplierMaterial { Id = Guid.NewGuid(), SupplierId = supplier, IngredientId = meat, IsPreferred = true, UnitPrice = 100000, PurchaseUomId = kg, MinimumOrderQuantity = 2, EffectiveFrom = new DateTime(2026, 1, 1), Status = "Active" });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();
            Task<LegacyResult> TopUp() => c.PostFormAsync("/api/MaterialRequirement/CalculateTopUp", ("ProductionDate", date), ("WarehouseId", warehouse));
            static JsonElement MeatLine(JsonElement data, Guid meat)
                => data.GetProperty("topUp").GetProperty("listLine").EnumerateArray().Single(l => l.GetProperty("ingredientId").GetGuid() == meat);

            var first = (await c.PostFormAsync("/api/MaterialRequirement/Calculate", ("ProductionDate", date), ("WarehouseId", warehouse))).Ok();
            Guid pr1 = first.GetProperty("purchaseRequest").GetProperty("id").GetGuid();
            Item(first, meat).GetProperty("purchaseQuantity").GetDecimal().Should().Be(10);

            // Đề nghị → PO (đề nghị Đã lập PO) → PO Đã duyệt → nhận 6 kg, QC DAT, ghi sổ.
            await c.ApprovePurchaseRequestAsync(pr1); // BE2.3 (D-46)
            (await c.PostFormAsync("/api/PurchaseRequest/ConvertToPurchaseOrder", ("Id", pr1))).Ok();
            var po = await _factory.QueryAsync(db => db.PurchaseOrder.AsNoTracking().SingleAsync(p => p.PurchaseRequestId == pr1));
            await _factory.SeedAsync(async db => (await db.PurchaseOrder.SingleAsync(p => p.Id == po.Id)).Status = "Approved");
            Guid poLine = await _factory.QueryAsync(db => db.PurchaseOrderLine.AsNoTracking().Where(l => l.PurchaseOrderId == po.Id).Select(l => l.Id).SingleAsync());
            var receipt = (await LegacyResult.ReadAsync(await c.PostAsJsonAsync("/api/GoodsReceipt/Create", new
            {
                purchaseOrderId = po.Id, warehouseId = warehouse, listLine = new[] { new { purchaseOrderLineId = poLine, receivedQuantity = 6m } },
            }))).Ok();
            Guid receiptId = receipt.GetProperty("goodsReceipt").GetProperty("id").GetGuid();
            (await LegacyResult.ReadAsync(await c.PostAsJsonAsync("/api/QcInspection/Conclude", new
            {
                id = receipt.GetProperty("qcInspectionId").GetGuid(),
                listLine = new[] { new { goodsReceiptLineId = receipt.GetProperty("listLine")[0].GetProperty("id").GetGuid(), result = "DAT" } },
            }))).Ok();
            (await c.PostFormAsync("/api/GoodsReceipt/Post", ("Id", receiptId))).Ok();

            // Không đổi suất: phần đã nhận (6) không bị tính 2 lần (tồn + đã đề nghị) → chênh 0, không lập, không cảnh báo thừa.
            var same = await TopUp();
            same.Message.Should().Contain("Không thiếu thêm");
            var sameData = same.Ok();
            MeatLine(sameData, meat).GetProperty("shortageQuantity").GetDecimal().Should().Be(4);
            MeatLine(sameData, meat).GetProperty("requestedQuantity").GetDecimal().Should().Be(4, "đã đề nghị 10 − đã nhận đạt 6");
            MeatLine(sameData, meat).GetProperty("differenceQuantity").GetDecimal().Should().Be(0);
            sameData.GetProperty("topUp").GetProperty("listWarning").EnumerateArray().Select(w => w.GetString()).Should().NotContain(w => w!.Contains("thừa"));

            // Tăng 130 suất → bổ sung đúng phần thiếu thêm 3.
            await _factory.SeedAsync(async db => (await db.OrderLine.SingleAsync(l => l.Id == orderLine)).Quantity = 130);
            var up = (await TopUp()).Ok();
            MeatLine(up, meat).GetProperty("shortageQuantity").GetDecimal().Should().Be(7);
            MeatLine(up, meat).GetProperty("requestedQuantity").GetDecimal().Should().Be(4);
            MeatLine(up, meat).GetProperty("topUpQuantity").GetDecimal().Should().Be(3);
            up.GetProperty("topUp").GetProperty("purchaseRequest").GetProperty("estimatedAmount").GetDecimal().Should().Be(300_000);
        }
    }
}
