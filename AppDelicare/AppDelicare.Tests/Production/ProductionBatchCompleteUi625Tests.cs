using System.Net.Http.Json;
using System.Text.Json;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Services.Implement;
using AppDelicare.Services.Interface;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Production
{
    /// <summary>
    /// UI-6.25 (D-106): Xong mẻ một chạm (Complete) – một giao dịch: lõi đạt + lưu mẫu → tự Đạt; mẻ sau cùng món × ca dùng lại mẫu; thiếu mẫu → Chờ QC có lý do;
    /// lõi dưới ngưỡng không khắc phục / mẻ kế tiếp khác dây chuyền → lỗi, không ghi gì; StartNextBatchId cùng dây chuyền; StartMany; RecordSampleRetained báo đã lưu (không lỗi); GetBoard.
    /// </summary>
    public class ProductionBatchCompleteUi625Tests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public ProductionBatchCompleteUi625Tests(InMemoryApiFactory factory) => _factory = factory;

        private static int MenuDay(DateTime d) => d.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)d.DayOfWeek;

        private sealed record Seed(Guid Order, Guid Menu, Guid Shift, Guid Customer, Guid LineA, Guid LineB, Guid LineOff, Guid DishA, Guid DishB, Guid DishC, Guid DishD, Guid CatA, Guid CatParent, Guid CatChild, Guid CatNone);

        private async Task<Seed> SeedAsync(string code, DateTime day)
        {
            Guid shift = Guid.NewGuid(), menu = Guid.NewGuid(), customer = Guid.NewGuid(), order = Guid.NewGuid(), kg = Guid.NewGuid(), ing = Guid.NewGuid();
            Guid lineA = Guid.NewGuid(), lineB = Guid.NewGuid(), lineOff = Guid.NewGuid();
            Guid catA = Guid.NewGuid(), catParent = Guid.NewGuid(), catChild = Guid.NewGuid(), catNone = Guid.NewGuid(), catOff = Guid.NewGuid();
            Guid dishA = Guid.NewGuid(), dishB = Guid.NewGuid(), dishC = Guid.NewGuid(), dishD = Guid.NewGuid();
            await _factory.SeedAsync(async db =>
            {
                if (!await db.FoodSafetyChecklistItem.AnyAsync(i => i.ItemCode == "SAMPLE_RETAINED"))
                {
                    Guid checklist = Guid.NewGuid();
                    db.FoodSafetyChecklist.Add(new FoodSafetyChecklist { Id = checklist, Code = "PRD_BATCH", Name = "Kiểm soát mẻ nấu", VersionNo = 1, Status = "Active" });
                    db.FoodSafetyChecklistItem.Add(new FoodSafetyChecklistItem { Id = Guid.NewGuid(), ChecklistId = checklist, ItemCode = "SAMPLE_RETAINED", ItemName = "Lưu mẫu", SortOrder = 1, IsCritical = true, IsRequired = true });
                }
                db.Shift.Add(new Shift { Id = shift, Code = "T" + code, Name = "Trưa " + code, StartTime = new TimeSpan(10, 30, 0), Status = "Active" });
                db.ProductionLine.AddRange(new ProductionLine { Id = lineA, Code = "LA" + code, Name = "Bếp mặn " + code, CapacityMealPerBatch = 40, MinutesPerBatch = 30, Status = "Active" },
                    new ProductionLine { Id = lineB, Code = "LB" + code, Name = "Rau canh " + code, CapacityMealPerBatch = 100, MinutesPerBatch = 45, Status = "Active" },
                    new ProductionLine { Id = lineOff, Code = "LO" + code, Name = "Bếp cũ " + code, CapacityMealPerBatch = 10, MinutesPerBatch = 20, Status = "Inactive" });
                db.DishCategory.AddRange(new DishCategory { Id = catA, Code = "CA" + code, Name = "Mặn " + code, ProductionLineId = lineA },
                    new DishCategory { Id = catParent, Code = "CP" + code, Name = "Rau cha " + code, ProductionLineId = lineB },
                    new DishCategory { Id = catChild, Code = "CC" + code, Name = "Rau con " + code, ParentId = catParent },
                    new DishCategory { Id = catNone, Code = "CN" + code, Name = "Tráng miệng " + code },
                    new DishCategory { Id = catOff, Code = "CO" + code, Name = "Cũ " + code, ProductionLineId = lineOff });
                db.Dish.AddRange(new Dish { Id = dishA, Code = "MA" + code, Name = "Thịt " + code, Status = "Active", DishCategoryId = catA },
                    new Dish { Id = dishB, Code = "MB" + code, Name = "Rau " + code, Status = "Active", DishCategoryId = catChild },
                    new Dish { Id = dishC, Code = "MC" + code, Name = "Chè " + code, Status = "Active", DishCategoryId = catNone },
                    new Dish { Id = dishD, Code = "MD" + code, Name = "Món cũ " + code, Status = "Active", DishCategoryId = catOff });
                db.Menu.Add(new Model.Entity.Menu { Id = menu, Code = "TD" + code, Name = "TĐ " + code, VersionNo = 1, Status = "Published" });
                int seq = 0;
                foreach (var d in new[] { dishA, dishB, dishC, dishD })
                {
                    db.MenuLine.Add(new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = d, MealType = "TRUA", DayOfWeek = MenuDay(day), SequenceNo = 1, SortOrder = ++seq });
                }
                db.UnitMeasure.Add(new UnitMeasure { Id = kg, Code = "KG" + code, Name = "kg" });
                db.Ingredient.Add(new Ingredient { Id = ing, Code = "NL" + code, Name = "NL " + code, BaseUnitId = kg });
                foreach (var d in new[] { dishA, dishB, dishC, dishD })
                {
                    db.Recipe.Add(new Recipe { Id = Guid.NewGuid(), DishId = d, VersionNo = 1, Status = "Locked", IsActive = true, StandardOutputQty = 100, EffectiveFrom = day.AddDays(-30),
                        LockedAt = day.AddDays(-30), SnapshotJson = JsonSerializer.Serialize(new[] { new { IngredientId = ing, Quantity = 1m, UnitId = kg, SequenceNo = 1 } }) });
                }
                Guid warehouse = Guid.NewGuid(), lot = Guid.NewGuid();
                db.Warehouse.Add(new Warehouse { Id = warehouse, Code = "W" + code, Name = "Kho " + code, Status = "Active" });
                db.MaterialLot.Add(new MaterialLot { Id = lot, IngredientId = ing, LotNumber = "L" + code, LotNo = "L" + code, ExpiryDate = day.AddDays(30), ReceivedDate = day.AddDays(-2), QcStatus = "DAT", QualityStatus = "DAT", Status = "Available" });
                await new InventoryLedgerService(db).PostAsync(new InventoryPostRequest { TransactionType = "RECEIPT", TransactionNumber = "NK" + code, WarehouseId = warehouse, IngredientId = ing, MaterialLotId = lot, UnitId = kg, QuantityIn = 100 });
                db.Customer.Add(new Customer { Id = customer, Code = "K" + code, Name = "Khách " + code, DisplayName = "Khách " + code, CIF = "K" + code, CustomerType = "FACTORY", Status = "Active" });
                db.Order.Add(new Order { Id = order, OrderNumber = "DH-" + code, CustomerId = customer, OrderDate = day.AddDays(-3), MealDate = day, DeliveryDate = day, Status = "Confirmed", TotalQuantity = 100 });
                db.OrderLine.Add(new OrderLine { Id = Guid.NewGuid(), OrderId = order, MenuId = menu, MealType = "TRUA", ShiftId = shift, Quantity = 100, LineAmount = 0 });
            });
            return new Seed(order, menu, shift, customer, lineA, lineB, lineOff, dishA, dishB, dishC, dishD, catA, catParent, catChild, catNone);
        }

        private static async Task<LegacyResult> Json(HttpClient c, string url, object body) => await LegacyResult.ReadAsync(await c.PostAsJsonAsync(url, body));

        [Fact]
        public async Task Complete_one_tap_reuses_sample_per_dish_shift_and_is_atomic()
        {
            var day = new DateTime(2032, 10, 5);
            _factory.Clock.FixedNow = day.AddDays(-1).AddHours(20).AddMinutes(5);
            var s = await SeedAsync("C5", day);
            using var c = _factory.CreateAuthorizedClient();
            (await c.PostFormAsync("/api/Order/LockByMealDate", ("MealDate", day))).Ok().GetProperty("autoPlan").GetProperty("status").GetString().Should().Be("RELEASED");
            _factory.Clock.FixedNow = day.AddHours(6);
            var batches = await _factory.QueryAsync(db => (from b in db.ProductionBatch.AsNoTracking() join o in db.ProductionOrder.AsNoTracking() on b.ProductionOrderId equals o.Id
                                                            join p in db.ProductionPlan.AsNoTracking() on o.ProductionPlanId equals p.Id where p.ProductionDate == day
                                                            select new { b.Id, b.DishId, b.StartDate, b.BatchNumber }).ToListAsync());
            var a = batches.Where(b => b.DishId == s.DishA).OrderBy(b => b.StartDate).Select(b => b.Id).ToList();
            Guid b1 = batches.Single(b => b.DishId == s.DishB).Id, c1 = batches.Single(b => b.DishId == s.DishC).Id;
            a.Should().HaveCount(3);

            (await c.PostFormAsync("/api/ProductionBatch/Start", ("Id", a[0]))).Ok();
            var done = (await Json(c, "/api/ProductionBatch/Complete", new { id = a[0], coreTemperatureC = 78m, retainSample = true, startNextBatchId = a[1] })).Ok();
            done.GetProperty("batch").GetProperty("status").GetString().Should().Be("Passed", "lõi đạt + đã lưu mẫu + sản lượng mặc định = kế hoạch → tự Đạt");
            done.GetProperty("batch").GetProperty("actualQuantity").GetDecimal().Should().BeGreaterThan(0);
            (await _factory.QueryAsync(db => db.ProductionBatch.AsNoTracking().SingleAsync(b => b.Id == a[1]))).Status.Should().Be("Cooking", "bắt đầu mẻ kế tiếp cùng dây chuyền");
            var a2 = (await Json(c, "/api/ProductionBatch/Complete", new { id = a[1], coreTemperatureC = 75m, retainSample = false })).Ok();
            a2.GetProperty("batch").GetProperty("status").GetString().Should().Be("Passed", "món × ca đã lưu mẫu ở mẻ trước");
            a2.GetProperty("batch").GetProperty("sampleRetained").GetBoolean().Should().BeTrue();

            var many = (await Json(c, "/api/ProductionBatch/StartMany", new { listId = new[] { a[2], b1 } })).Ok();
            many.GetProperty("listStarted").GetArrayLength().Should().Be(2);
            (await c.PostFormAsync("/api/ProductionBatch/RecordSampleRetained", ("Id", a[2]))).Ok().GetProperty("listWarning").EnumerateArray()
                .Select(w => w.GetString()).Should().Contain(w => w!.Contains("đã lưu mẫu lúc"));

            int temps = await _factory.QueryAsync(db => db.TemperatureLog.CountAsync(t => t.ProductionBatchId == b1));
            (await Json(c, "/api/ProductionBatch/Complete", new { id = b1, coreTemperatureC = 62m })).Fail("khắc phục");
            (await Json(c, "/api/ProductionBatch/Complete", new { id = b1, coreTemperatureC = 80m, startNextBatchId = c1 })).Fail("cùng dây chuyền");
            (await _factory.QueryAsync(db => db.TemperatureLog.CountAsync(t => t.ProductionBatchId == b1))).Should().Be(temps, "lỗi → không ghi gì");
            (await _factory.QueryAsync(db => db.ProductionBatch.AsNoTracking().SingleAsync(b => b.Id == b1))).Status.Should().Be("Cooking");
            (await _factory.QueryAsync(db => db.ProductionBatch.AsNoTracking().SingleAsync(b => b.Id == c1))).Status.Should().Be("Planned");

            var held = (await Json(c, "/api/ProductionBatch/Complete", new { id = b1, coreTemperatureC = 80m, producedQuantity = 95m,
                listWaste = new[] { new { wasteType = "DAMAGED", quantityMeals = 3m, reason = "rơi" } } })).Ok();
            held.GetProperty("batch").GetProperty("status").GetString().Should().Be("QCPending");
            held.GetProperty("listWarning").EnumerateArray().Select(w => w.GetString()).Should().Contain(w => w!.Contains("lưu mẫu"));
            (await _factory.QueryAsync(db => db.MealWasteRecord.CountAsync(w => w.ProductionBatchId == b1 && w.WasteType == "DAMAGED" && w.QuantityMeals == 3))).Should().Be(1);

            var board = (await c.GetResultAsync($"/api/ProductionBatch/GetBoard?ProductionDate={day:yyyy-MM-dd}")).Ok();
            board.GetProperty("listShift").EnumerateArray().Should().ContainSingle(x => x.GetProperty("shiftId").GetGuid() == s.Shift);
            var bb = board.GetProperty("listBatch").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == b1);
            bb.GetProperty("listAutoPassMissing").EnumerateArray().Select(x => x.GetString()).Should().Contain(x => x!.Contains("lưu mẫu"));
            board.GetProperty("coreTempMinC").GetDecimal().Should().BeGreaterThan(0);
        }
    }
}
