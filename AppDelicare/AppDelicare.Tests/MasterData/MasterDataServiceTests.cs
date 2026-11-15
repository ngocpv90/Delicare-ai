using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.MasterData
{
    /// <summary>T45R (chuyển từ T45B.3 v1) – danh mục P1 theo khung cũ: luật trong Insert / Update và API tổng quan.</summary>
    public class MasterDataServiceTests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public MasterDataServiceTests(InMemoryApiFactory factory) => _factory = factory;

        [Fact]
        public async Task Customer_cif_is_unique_and_overview_aggregates_contacts_contracts_orders()
        {
            using var c = _factory.CreateAuthorizedClient();
            (await c.PostFormAsync("/api/Customer/Insert", ("Code", "KH-360"), ("Name", "Trường Ánh Dương"), ("DisplayName", "Trường Ánh Dương"), ("CustomerType", "SCHOOL"), ("Status", "Active"), ("CIF", "CIF-360"))).Ok();
            (await c.PostFormAsync("/api/Customer/Insert", ("Code", "KH-361"), ("Name", "Trùng CIF"), ("DisplayName", "Trùng CIF"), ("CustomerType", "SCHOOL"), ("Status", "Active"), ("CIF", "CIF-360"))).Fail("CIF");
            var customer = await _factory.QueryAsync(db => db.Customer.AsNoTracking().SingleAsync(x => x.Code == "KH-360"));
            customer.CIF.Should().Be("CIF-360", "cột mới của DDL đã có trong CustomerRequest (CodeGen --merge)");
            Guid contract = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.CustomerContact.Add(new CustomerContact { Id = Guid.NewGuid(), CustomerId = customer.Id, FullName = "Cô Lan" });
                db.Contract.Add(new Contract { Id = contract, CustomerId = customer.Id, ContractNumber = "HD-360", Status = "Active" });
                db.ContractMeal.Add(new ContractMeal { Id = Guid.NewGuid(), ContractId = contract, MealType = "TRUA", UnitPrice = 30000 });
                db.Order.Add(new Order { Id = Guid.NewGuid(), CustomerId = customer.Id, OrderNumber = "DH-1", MealDate = DateTime.Today.AddDays(-1), DeliveryDate = DateTime.Today.AddDays(-1), TotalQuantity = 140, Status = "Delivered" });
                return Task.CompletedTask;
            });

            var overview = (await c.GetResultAsync($"/api/Customer/GetOverview?id={customer.Id}")).Ok();

            overview.GetProperty("listContact").GetArrayLength().Should().Be(1);
            overview.GetProperty("listContract")[0].GetProperty("listMeal").GetArrayLength().Should().Be(1);
            overview.GetProperty("orderCount").GetInt32().Should().Be(1);
            overview.GetProperty("avgMealsPerDay7d").GetDecimal().Should().Be(20);
            (await c.GetResultAsync($"/api/Customer/GetOverview?id={Guid.NewGuid()}")).Fail();
        }

        [Fact]
        public async Task Delivery_location_window_coordinates_and_inactive_guard()
        {
            Guid customer = Guid.NewGuid();
            using var c = _factory.CreateAuthorizedClient();

            (await c.PostFormAsync("/api/CustomerDeliveryLocation/Insert", ("CustomerId", customer), ("Code", "DG-1"), ("Name", "Cổng A"),
                ("DeliveryWindowFrom", "11:00:00"), ("DeliveryWindowTo", "10:00:00"))).Fail("Giờ giao");
            (await c.PostFormAsync("/api/CustomerDeliveryLocation/Insert", ("CustomerId", customer), ("Code", "DG-1"), ("Name", "Cổng A"), ("Latitude", 91m))).Fail("Vĩ độ");
            (await c.PostFormAsync("/api/CustomerDeliveryLocation/Insert", ("CustomerId", customer), ("Code", "DG-1"), ("Name", "Cổng A"),
                ("DeliveryWindowFrom", "10:00:00"), ("DeliveryWindowTo", "11:00:00"), ("Status", "Active"))).Ok();
            var location = await _factory.QueryAsync(db => db.CustomerDeliveryLocation.AsNoTracking().SingleAsync(x => x.Code == "DG-1"));
            await _factory.SeedAsync(db =>
            {
                db.DeliveryOrder.Add(new DeliveryOrder { Id = Guid.NewGuid(), CustomerDeliveryLocationId = location.Id, Status = "PLANNED" });
                return Task.CompletedTask;
            });

            (await c.PutFormAsync("/api/CustomerDeliveryLocation/Update", ("Id", location.Id), ("CustomerId", customer), ("Code", "DG-1"), ("Name", "Cổng A"), ("Status", "Inactive")))
                .Fail("đơn giao");
        }

        [Fact]
        public async Task Ingredient_rules_and_stock()
        {
            using var c = _factory.CreateAuthorizedClient();
            (await c.PostFormAsync("/api/Ingredient/Insert", ("Code", "NL-Y"), ("Name", "Sai thu hồi"), ("YieldPercent", 0m))).Fail("thu hồi");
            (await c.PostFormAsync("/api/Ingredient/Insert", ("Code", "NL-W"), ("Name", "Sai hao hụt"), ("StandardWastePercent", 100m))).Fail("hao hụt");
            (await c.PostFormAsync("/api/Ingredient/Insert", ("Code", "NL-H"), ("Name", "Thiếu hạn"), ("IsExpiryManaged", true))).Fail("hạn dùng");
            (await c.PostFormAsync("/api/Ingredient/Insert", ("Code", "NL-OK"), ("Name", "Thịt"), ("IsExpiryManaged", true), ("ShelfLifeDays", 3), ("MinimumStock", 50m))).Ok();
            var id = await _factory.QueryAsync(db => db.Ingredient.Where(x => x.Code == "NL-OK").Select(x => x.Id).SingleAsync());
            await _factory.SeedAsync(db =>
            {
                db.InventoryBalance.Add(new InventoryBalance { Id = Guid.NewGuid(), IngredientId = id, WarehouseId = Guid.NewGuid(), AvailableQuantity = 20 });
                db.InventoryBalance.Add(new InventoryBalance { Id = Guid.NewGuid(), IngredientId = id, WarehouseId = Guid.NewGuid(), AvailableQuantity = 10 });
                return Task.CompletedTask;
            });

            var stock = (await c.GetResultAsync($"/api/Ingredient/GetStock?id={id}")).Ok();

            stock.GetProperty("availableQuantity").GetDecimal().Should().Be(30);
            stock.GetProperty("belowMinimum").GetBoolean().Should().BeTrue();
            stock.GetProperty("listWarehouse").GetArrayLength().Should().Be(2);
        }

        [Fact]
        public async Task Only_one_preferred_supplier_per_ingredient()
        {
            Guid ingredient = Guid.NewGuid(), s1 = Guid.NewGuid(), s2 = Guid.NewGuid();
            using var c = _factory.CreateAuthorizedClient();

            (await c.PostFormAsync("/api/SupplierMaterial/Insert", ("SupplierId", s1), ("IngredientId", ingredient), ("UnitPrice", 10000m), ("IsPreferred", true))).Ok();
            (await c.PostFormAsync("/api/SupplierMaterial/Insert", ("SupplierId", s2), ("IngredientId", ingredient), ("UnitPrice", 9000m), ("IsPreferred", true))).Ok();

            var rows = await _factory.QueryAsync(db => db.SupplierMaterial.AsNoTracking().Where(x => x.IngredientId == ingredient).ToListAsync());
            rows.Single(x => x.IsPreferred).SupplierId.Should().Be(s2);
        }

        [Fact]
        public async Task Uom_conversion_rules_and_convert_direct_inverse_and_via_intermediate()
        {
            Guid kg = Guid.NewGuid(), g = Guid.NewGuid(), bag = Guid.NewGuid(), lit = Guid.NewGuid();
            using var c = _factory.CreateAuthorizedClient();
            (await c.PostFormAsync("/api/UomConversion/Insert", ("FromUnitId", kg), ("ToUnitId", g), ("ConversionFactor", 1000m))).Ok();
            (await c.PostFormAsync("/api/UomConversion/Insert", ("FromUnitId", bag), ("ToUnitId", kg), ("ConversionFactor", 25m))).Ok();
            (await c.PostFormAsync("/api/UomConversion/Insert", ("FromUnitId", kg), ("ToUnitId", g), ("ConversionFactor", 999m))).Fail("đã tồn tại");
            (await c.PostFormAsync("/api/UomConversion/Insert", ("FromUnitId", kg), ("ToUnitId", kg), ("ConversionFactor", 1m))).Fail("khác nhau");
            (await c.PostFormAsync("/api/UomConversion/Insert", ("FromUnitId", lit), ("ToUnitId", g), ("ConversionFactor", 0m))).Fail("lớn hơn 0");

            (await c.GetResultAsync($"/api/UomConversion/Convert?Quantity=2&FromUnitId={kg}&ToUnitId={g}")).Ok().GetDecimal().Should().Be(2000);
            (await c.GetResultAsync($"/api/UomConversion/Convert?Quantity=500&FromUnitId={g}&ToUnitId={kg}")).Ok().GetDecimal().Should().Be(0.5m);
            (await c.GetResultAsync($"/api/UomConversion/Convert?Quantity=1&FromUnitId={bag}&ToUnitId={g}")).Ok().GetDecimal().Should().Be(25000);
            (await c.GetResultAsync($"/api/UomConversion/Convert?Quantity=1&FromUnitId={lit}&ToUnitId={g}")).Fail("Chưa có quy đổi");
        }

        [Fact]
        public async Task Inventory_policy_stock_chain_and_one_row_per_ingredient_warehouse()
        {
            Guid ingredient = Guid.NewGuid(), warehouse = Guid.NewGuid();
            using var c = _factory.CreateAuthorizedClient();

            (await c.PostFormAsync("/api/InventoryPolicy/Insert", ("IngredientId", ingredient), ("WarehouseId", warehouse), ("MinimumStock", 10m), ("SafetyStock", 5m))).Fail("Tồn tối thiểu");
            (await c.PostFormAsync("/api/InventoryPolicy/Insert", ("IngredientId", ingredient), ("WarehouseId", warehouse), ("MinimumStock", 5m), ("SafetyStock", 10m), ("ReorderPoint", 20m), ("MaximumStock", 100m))).Ok();
            (await c.PostFormAsync("/api/InventoryPolicy/Insert", ("IngredientId", ingredient), ("WarehouseId", warehouse), ("MinimumStock", 1m))).Fail("Đã có chính sách");
        }

        [Fact]
        public async Task Warehouse_layout_and_supplier_overview()
        {
            Guid warehouse = Guid.NewGuid(), zone = Guid.NewGuid(), supplier = Guid.NewGuid(), ingredient = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Warehouse.Add(new Warehouse { Id = warehouse, Code = "KHO-LAYOUT", Name = "Kho lạnh" });
                db.StorageZone.Add(new StorageZone { Id = zone, WarehouseId = warehouse, Code = "Z1", Name = "Ngăn mát", TemperatureMin = 0, TemperatureMax = 4 });
                db.StorageLocation.Add(new StorageLocation { Id = Guid.NewGuid(), ZoneId = zone, StorageZoneId = zone, Code = "Z1-01", Name = "Kệ 1" });
                db.Supplier.Add(new Supplier { Id = supplier, Code = "NCC-OV", Name = "NCC tổng quan" });
                db.Ingredient.Add(new Ingredient { Id = ingredient, Code = "NL-OV", Name = "Rau" });
                db.SupplierMaterial.Add(new SupplierMaterial { Id = Guid.NewGuid(), SupplierId = supplier, IngredientId = ingredient, UnitPrice = 15000, IsPreferred = true });
                db.PurchaseOrder.Add(new PurchaseOrder { Id = Guid.NewGuid(), SupplierId = supplier, PurchaseOrderNumber = "PO-1", Status = "Submitted" });
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();

            var layout = (await c.GetResultAsync($"/api/Warehouse/GetLayout?id={warehouse}")).Ok();
            layout.GetProperty("listZone")[0].GetProperty("listLocation").GetArrayLength().Should().Be(1);

            var overview = (await c.GetResultAsync($"/api/Supplier/GetOverview?id={supplier}")).Ok();
            overview.GetProperty("listMaterial")[0].GetProperty("ingredientName").GetString().Should().Be("Rau");
            overview.GetProperty("listOpenPurchaseOrder").GetArrayLength().Should().Be(1);
        }
    }
}
