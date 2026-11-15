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
    /// <summary>T45B.3 – logic riêng P1 (spec mục 4).</summary>
    public class P1SpecialTests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public P1SpecialTests(InMemoryApiFactory factory) => _factory = factory;

        private static async Task<JsonElement> Data(HttpResponseMessage r)
        {
            string raw = await r.Content.ReadAsStringAsync();
            r.IsSuccessStatusCode.Should().BeTrue(raw);
            return JsonDocument.Parse(raw).RootElement.GetProperty("data").Clone();
        }

        [Fact]
        public async Task Customer_list_is_enriched_cif_is_unique_and_overview_aggregates()
        {
            Guid customer = Guid.NewGuid(), order = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Customer.Add(new Customer { Id = customer, Code = "KH-P1", CIF = "CIF-P1", CustomerType = "B2B", DisplayName = "Công ty P1", Status = "Active" });
                db.Contract.Add(new Contract { Id = Guid.NewGuid(), CustomerId = customer, ContractNumber = "HD-P1", Status = "Active" });
                db.CustomerDeliveryLocation.Add(new CustomerDeliveryLocation { Id = Guid.NewGuid(), CustomerId = customer, Name = "Căng tin A" });
                db.Order.Add(new Order { Id = order, CustomerId = customer, MealDate = DateTime.Today.AddDays(-1), TotalQuantity = 700, Status = "Delivered", OrderNumber = "DH-P1" });
                db.OrderReconciliation.Add(new OrderReconciliation { Id = Guid.NewGuid(), OrderId = order, FinalAmount = 21_000_000 });
                db.CustomerComplaint.Add(new CustomerComplaint { Id = Guid.NewGuid(), CustomerId = customer, ComplaintNo = "PA-1", Description = "x", ComplaintStatus = "Open", Severity = "LOW", SourceChannel = "PHONE", ReceivedAt = DateTime.Now });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();

            var list = await Data(await c.GetAsync("/api/v1/sales/customers?q=KH-P1"));
            var item = list.GetProperty("items")[0];
            item.GetProperty("activeContracts")[0].GetProperty("contractNumber").GetString().Should().Be("HD-P1");
            item.GetProperty("locationCount").GetInt32().Should().Be(1);
            item.GetProperty("avgMealsPerDay7d").GetDecimal().Should().Be(100m);

            var dupCif = await c.PostAsJsonAsync("/api/v1/sales/customers", new { code = "KH-P1B", cif = "CIF-P1", customerType = "B2B", displayName = "Trùng CIF", status = "Active" });
            dupCif.StatusCode.Should().Be(HttpStatusCode.Conflict);

            var overview = await Data(await c.GetAsync($"/api/v1/sales/customers/{customer}/overview"));
            overview.GetProperty("kpi").GetProperty("orderCount").GetInt32().Should().Be(1);
            overview.GetProperty("kpi").GetProperty("reconciledRevenue").GetDecimal().Should().Be(21_000_000);
            overview.GetProperty("kpi").GetProperty("complaintCount").GetInt32().Should().Be(1);
            overview.GetProperty("recentOrders").GetArrayLength().Should().Be(1);
        }

        [Fact]
        public async Task Delivery_location_cannot_be_deactivated_with_open_delivery_orders()
        {
            Guid customer = Guid.NewGuid(), location = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Customer.Add(new Customer { Id = customer, Code = "KH-LOC", CIF = "CIF-LOC", CustomerType = "B2B", DisplayName = "KH LOC", Status = "Active" });
                db.CustomerDeliveryLocation.Add(new CustomerDeliveryLocation { Id = location, CustomerId = customer, Name = "Điểm 1", Status = "Active" });
                db.DeliveryOrder.Add(new DeliveryOrder { Id = Guid.NewGuid(), CustomerId = customer, CustomerDeliveryLocationId = location, Status = "Planned", DeliveryNumber = "GH-1" });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();

            var r = await c.PutAsJsonAsync($"/api/v1/sales/delivery-locations/{location}", new { status = "Inactive" });

            r.StatusCode.Should().Be(HttpStatusCode.Conflict);
            JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.GetProperty("errors")[0].GetProperty("code").GetString().Should().Be(ErrorCodes.InUse);
            (await c.PutAsJsonAsync($"/api/v1/sales/delivery-locations/{location}", new { name = "Điểm 1 (đổi tên)" })).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Supplier_material_single_preferred_and_effective_on_filter()
        {
            Guid supplier1 = Guid.NewGuid(), supplier2 = Guid.NewGuid(), ingredient = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Supplier.AddRange(new Supplier { Id = supplier1, Code = "NCC-A", Name = "NCC A" }, new Supplier { Id = supplier2, Code = "NCC-B", Name = "NCC B" });
                db.Ingredient.Add(new Ingredient { Id = ingredient, Code = "NL-GAO", Name = "Gạo" });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();

            var first = await Data(await c.PostAsJsonAsync("/api/v1/scm/supplier-materials", new { supplierId = supplier1, ingredientId = ingredient, unitPrice = 18000, isPreferred = true, effectiveFrom = "2026-01-01", effectiveTo = "2026-06-30" }));
            await Data(await c.PostAsJsonAsync("/api/v1/scm/supplier-materials", new { supplierId = supplier2, ingredientId = ingredient, unitPrice = 17500, isPreferred = true, effectiveFrom = "2026-07-01" }));

            var rows = await _factory.QueryAsync(db => db.SupplierMaterial.AsNoTracking().Where(x => x.IngredientId == ingredient).ToListAsync());
            rows.Count(x => x.IsPreferred).Should().Be(1, "mỗi nguyên liệu chỉ một dòng ưu tiên");
            rows.Single(x => x.IsPreferred).SupplierId.Should().Be(supplier2);
            rows.Single(x => x.Id == first.GetProperty("id").GetGuid()).IsPreferred.Should().BeFalse();

            var march = await Data(await c.GetAsync($"/api/v1/scm/supplier-materials?ingredientId={ingredient}&effectiveOn=2026-03-15"));
            march.GetProperty("total").GetInt32().Should().Be(1);
            march.GetProperty("items")[0].GetProperty("supplierId").GetGuid().Should().Be(supplier1);
            (await c.GetAsync("/api/v1/scm/supplier-materials?effectiveOn=hôm-nay")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Ingredient_list_includes_stock_and_below_min()
        {
            Guid ingredient = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Ingredient.Add(new Ingredient { Id = ingredient, Code = "NL-THIT", Name = "Thịt heo", MinimumStock = 50 });
                db.InventoryBalance.AddRange(
                    new InventoryBalance { Id = Guid.NewGuid(), IngredientId = ingredient, WarehouseId = Guid.NewGuid(), UnitId = Guid.NewGuid(), AvailableQuantity = 20 },
                    new InventoryBalance { Id = Guid.NewGuid(), IngredientId = ingredient, WarehouseId = Guid.NewGuid(), UnitId = Guid.NewGuid(), AvailableQuantity = 10 });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();

            var item = (await Data(await c.GetAsync("/api/v1/scm/ingredients?q=NL-THIT"))).GetProperty("items")[0];

            item.GetProperty("availableQuantity").GetDecimal().Should().Be(30);
            item.GetProperty("belowMin").GetBoolean().Should().BeTrue();
        }

        [Fact]
        public async Task Warehouse_layout_and_uom_conversion_paths()
        {
            Guid wh = Guid.NewGuid(), zone = Guid.NewGuid(), kg = Guid.NewGuid(), g = Guid.NewGuid(), tan = Guid.NewGuid(), lit = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Warehouse.Add(new Warehouse { Id = wh, Code = "WH-LAYOUT", Name = "Kho lạnh" });
                db.StorageZone.Add(new StorageZone { Id = zone, WarehouseId = wh, Code = "Z-LANH", Name = "Khu lạnh", TemperatureMin = 0, TemperatureMax = 4 });
                db.StorageLocation.Add(new StorageLocation { Id = Guid.NewGuid(), StorageZoneId = zone, WarehouseId = wh, Code = "L-01", Name = "Kệ 1" });
                db.UnitMeasure.AddRange(new UnitMeasure { Id = kg, Name = "kg" }, new UnitMeasure { Id = g, Name = "g" }, new UnitMeasure { Id = tan, Name = "tấn" }, new UnitMeasure { Id = lit, Name = "lít" });
                db.UomConversion.AddRange(
                    new UomConversion { Id = Guid.NewGuid(), FromUnitId = kg, ToUnitId = g, FromUomId = kg, ToUomId = g, ConversionFactor = 1000 },
                    new UomConversion { Id = Guid.NewGuid(), FromUnitId = tan, ToUnitId = kg, FromUomId = tan, ToUomId = kg, ConversionFactor = 1000 });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();

            var layout = await Data(await c.GetAsync($"/api/v1/scm/warehouses/{wh}/layout"));
            layout.GetProperty("zones")[0].GetProperty("locations")[0].GetProperty("code").GetString().Should().Be("L-01");

            async Task<decimal> Convert(decimal q, Guid from, Guid to)
                => (await Data(await c.GetAsync($"/api/v1/scm/uom-conversions/convert?quantity={q}&fromUnitId={from}&toUnitId={to}"))).GetProperty("result").GetDecimal();
            (await Convert(2.5m, kg, g)).Should().Be(2500, "chiều xuôi");
            (await Convert(500, g, kg)).Should().Be(0.5m, "chiều ngược");
            (await Convert(0.002m, tan, g)).Should().Be(2000, "qua đơn vị trung gian kg");
            var missing = await c.GetAsync($"/api/v1/scm/uom-conversions/convert?quantity=1&fromUnitId={kg}&toUnitId={lit}");
            missing.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            JsonDocument.Parse(await missing.Content.ReadAsStringAsync()).RootElement.GetProperty("errors")[0].GetProperty("code").GetString().Should().Be(ErrorCodes.UomConversionMissing);
        }
    }
}
