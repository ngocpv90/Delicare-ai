using System.Net.Http.Json;
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
    /// BIZ-1.3 (D-77 c): nhu cầu nguyên liệu nhiều ngày. Hôm nay (đồng hồ test) = thứ Hai 01/03/2027 10:00; kỳ 02/03 → 04/03.
    /// Món: 10 kg nguyên liệu / 100 suất (không hao hụt). Tồn 30 kg. NCC ưu tiên giao 2 ngày; đệm 1 ngày.
    /// <list type="bullet">
    /// <item>02/03: đơn Đã chốt 100 suất (đơn Đã xác nhận 999 cùng khách / điểm / buổi bị bỏ – đã có đơn chốt) → 10 kg.</item>
    /// <item>03/03: đơn Đã xác nhận 200 suất → 20 kg. Lũy kế 30 = tồn → chưa thiếu.</item>
    /// <item>04/03: chưa có đơn, dự báo Đã công bố 150 (bản Nháp 999 bị bỏ) → thực đơn theo phân công → 15 kg. Thiếu lũy kế 15 → phải đặt 04/03 − 2 − 1 = 01/03 = hôm nay.</item>
    /// </list>
    /// Lập đề nghị mua phần phải đặt → 1 dòng 15 kg ngày cần 04/03; tính lại → không còn thiếu (đề nghị mua đang mở được trừ).
    /// </summary>
    public class MrpHorizonTests : IClassFixture<InMemoryApiFactory>
    {
        private static readonly DateTime Today = new(2027, 3, 1);
        private readonly InMemoryApiFactory _factory;

        public MrpHorizonTests(InMemoryApiFactory factory)
        {
            _factory = factory;
            _factory.Clock.FixedNow = Today.AddHours(10);
        }

        private static int DayOf(DateTime d) => d.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)d.DayOfWeek;

        [Fact]
        public async Task Horizon_uses_locked_then_confirmed_then_forecast_and_deducts_stock_cumulatively()
        {
            string code = "HZ" + Guid.NewGuid().ToString("N")[..5];
            Guid warehouse = Guid.NewGuid(), kg = Guid.NewGuid(), ing = Guid.NewGuid(), dish = Guid.NewGuid(), recipe = Guid.NewGuid(), menu = Guid.NewGuid(), customer = Guid.NewGuid(), supplier = Guid.NewGuid();
            DateTime d1 = Today.AddDays(1), d2 = Today.AddDays(2), d3 = Today.AddDays(3);
            await _factory.SeedAsync(async db =>
            {
                db.Warehouse.Add(new Warehouse { Id = warehouse, Code = code, Name = "Kho " + code, Status = "Active" });
                db.UnitMeasure.Add(new UnitMeasure { Id = kg, Code = "KG-" + code, Name = "kg" });
                db.Ingredient.Add(new Ingredient { Id = ing, Code = "NL-" + code, Name = "Thịt " + code, BaseUnitId = kg });
                db.Dish.Add(new Dish { Id = dish, Code = "M-" + code, Name = "Món " + code });
                db.Recipe.Add(new Recipe { Id = recipe, DishId = dish, VersionNo = 1, Status = "Locked", IsActive = true, StandardOutputQty = 100, EffectiveFrom = new DateTime(2027, 1, 1),
                    LockedAt = new DateTime(2027, 1, 1), SnapshotJson = JsonSerializer.Serialize(new[] { new { IngredientId = ing, Quantity = 10m, UnitId = kg, SequenceNo = 1 } }) });
                db.Menu.Add(new Model.Entity.Menu { Id = menu, Code = "TD-" + code, Name = "Thực đơn " + code, VersionNo = 1, Status = "Published", EffectiveFrom = new DateTime(2027, 1, 1) });
                foreach (var d in new[] { d1, d2, d3 })
                {
                    db.MenuLine.Add(new MenuLine { Id = Guid.NewGuid(), MenuId = menu, DishId = dish, MealType = "TRUA", DayOfWeek = DayOf(d), SequenceNo = 1 });
                }
                db.Customer.Add(new Customer { Id = customer, Code = code, Name = "Khách " + code, CIF = code, CustomerType = "FACTORY", DisplayName = code, Status = "Active" });
                db.MenuAssignment.Add(new MenuAssignment { Id = Guid.NewGuid(), CustomerId = customer, MenuId = menu, MealType = "TRUA", EffectiveFrom = new DateTime(2027, 1, 1), Status = "Active" });
                foreach (var (date, status, qty) in new[] { (d1, "Locked", 100m), (d1, "Confirmed", 999m), (d2, "Confirmed", 200m), (d2, "Draft", 777m) })
                {
                    Guid order = Guid.NewGuid();
                    string no = $"DH-{code}-{date:dd}-{qty}";
                    db.Order.Add(new Order { Id = order, OrderNumber = no, OrderNo = no, CustomerId = customer, OrderDate = Today, MealDate = date, DeliveryDate = date, Status = status, TotalQuantity = qty });
                    db.OrderLine.Add(new OrderLine { Id = Guid.NewGuid(), OrderId = order, MenuId = menu, MealType = "TRUA", Quantity = qty });
                }
                db.Forecast.AddRange(
                    new Forecast { Id = Guid.NewGuid(), CustomerId = customer, ForecastDate = d3, MealType = "TRUA", Quantity = 150, ForecastQuantity = 150, VersionNo = 1, Status = "Published" },
                    new Forecast { Id = Guid.NewGuid(), CustomerId = customer, ForecastDate = d3, MealType = "TRUA", Quantity = 999, ForecastQuantity = 999, VersionNo = 2, Status = "Draft" });
                db.Supplier.Add(new Supplier { Id = supplier, Code = "NCC-" + code, Name = "NCC " + code, Status = "Active" });
                db.SupplierMaterial.Add(new SupplierMaterial { Id = Guid.NewGuid(), SupplierId = supplier, IngredientId = ing, IsPreferred = true, UnitPrice = 100, PurchaseUomId = kg, LeadTimeDays = 2, EffectiveFrom = new DateTime(2027, 1, 1), Status = "Active" });
                Guid lot = Guid.NewGuid();
                db.MaterialLot.Add(new MaterialLot { Id = lot, IngredientId = ing, LotNumber = "L-" + code, LotNo = "L-" + code, ExpiryDate = Today.AddDays(30), ReceivedDate = Today.AddDays(-1), QcStatus = "DAT", QualityStatus = "DAT", Status = "Available" });
                await new InventoryLedgerService(db).PostAsync(new InventoryPostRequest { TransactionType = "RECEIPT", TransactionNumber = "NK-" + code, WarehouseId = warehouse, IngredientId = ing, MaterialLotId = lot, UnitId = kg, QuantityIn = 30 });
            });

            using var c = _factory.CreateAuthorizedClient();
            var result = (await c.GetResultAsync($"/api/MaterialRequirement/GetHorizon?FromDate={d1:yyyy-MM-dd}&Days=3&WarehouseId={warehouse}")).Ok();
            var days = result.GetProperty("listDay").EnumerateArray().ToList();
            days.Select(d => d.GetProperty("source").GetString()).Should().Equal("LOCKED", "CONFIRMED", "FORECAST");
            days[0].GetProperty("lockedMeals").GetDecimal().Should().Be(100, "đơn Đã xác nhận cùng khách / buổi bị bỏ khi đã có đơn chốt");
            days[1].GetProperty("confirmedMeals").GetDecimal().Should().Be(200, "đơn Nháp không tính");
            days[2].GetProperty("forecastMeals").GetDecimal().Should().Be(150, "chỉ dự báo Đã công bố");

            var item = result.GetProperty("listItem").EnumerateArray().Single(i => i.GetProperty("ingredientId").GetGuid() == ing);
            item.GetProperty("listDay").EnumerateArray().Select(x => x.GetProperty("required").GetDecimal()).Should().Equal(10m, 20m, 15m);
            item.GetProperty("listDay").EnumerateArray().Select(x => x.GetProperty("shortage").GetDecimal()).Should().Equal(0m, 0m, 15m);
            item.GetProperty("totalShortage").GetDecimal().Should().Be(15);
            item.GetProperty("orderByDate").GetDateTime().Should().Be(Today, "04/03 − 2 ngày giao − 1 ngày đệm");
            item.GetProperty("orderStatus").GetString().Should().Be("TODAY");
            result.GetProperty("dueTodayCount").GetInt32().Should().Be(1);

            // Lập đề nghị mua phần phải đặt → 1 dòng 15 kg ngày cần 04/03; tính lại → hết thiếu.
            var created = await LegacyResult.ReadAsync(await c.PostAsJsonAsync("/api/MaterialRequirement/CreateHorizonPurchaseRequest",
                new { fromDate = d1, days = 3, warehouseId = warehouse, onlyDue = true }));
            created.Ok().GetProperty("listItem").EnumerateArray().Single(i => i.GetProperty("ingredientId").GetGuid() == ing)
                .GetProperty("totalShortage").GetDecimal().Should().Be(0, "đề nghị mua đang mở được trừ");
            var line = await _factory.QueryAsync(db => db.PurchaseRequestLine.AsNoTracking().SingleAsync(l => l.IngredientId == ing && !l.IsDeleted));
            (line.Quantity, line.RequiredDate, line.UnitId).Should().Be((15m, d3, kg));
            (await _factory.QueryAsync(db => db.PurchaseRequest.AsNoTracking().SingleAsync(p => p.Id == line.PurchaseRequestId))).Status.Should().Be("Draft");
            (await LegacyResult.ReadAsync(await c.PostAsJsonAsync("/api/MaterialRequirement/CreateHorizonPurchaseRequest",
                new { fromDate = d1, days = 3, warehouseId = warehouse, onlyDue = true }))).Fail("Không có nguyên liệu nào phải đặt");
        }

        [Fact]
        public async Task Horizon_rejects_bad_day_count()
        {
            using var c = _factory.CreateAuthorizedClient();
            (await c.GetResultAsync("/api/MaterialRequirement/GetHorizon?Days=40")).Fail("từ 1 đến 31");
        }
    }
}
