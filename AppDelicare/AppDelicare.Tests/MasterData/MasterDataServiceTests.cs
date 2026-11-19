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
            Guid branch = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.OrganizationUnit.Add(new OrganizationUnit { Id = branch, CompanyId = Guid.NewGuid(), Code = "CN-360", Name = "[DEMO] Chi nhánh 360" });
                return Task.CompletedTask;
            });
            // MA-1.2 (D-63): Code / CIF gửi lên bị bỏ qua; CIF = Code; MST không trùng trong khách doanh nghiệp (thay luật CIF không trùng cũ).
            (await c.PostFormAsync("/api/Customer/Insert", ("Code", "KH-360"), ("CIF", "CIF-360"), ("CustomerType", "BUSINESS"), ("LegalName", "Trường Ánh Dương"),
                ("DisplayName", "Trường Ánh Dương"), ("TaxCode", "0312345360"), ("LegalRepresentativeName", "Ông A"), ("ContactPersonName", "Cô Lan"),
                ("ContactPersonPhone", "0901 234 360"), ("Address", "[DEMO] Q1"), ("PhoneNumber", "0281234360"), ("BranchId", branch))).Ok();
            (await c.PostFormAsync("/api/Customer/Insert", ("CustomerType", "BUSINESS"), ("LegalName", "Trùng MST"), ("DisplayName", "Trùng MST"), ("TaxCode", "0312345360"),
                ("LegalRepresentativeName", "Ông B"), ("ContactPersonName", "Cô B"), ("ContactPersonPhone", "0901234361"), ("Address", "[DEMO] Q1"),
                ("PhoneNumber", "0281234361"), ("BranchId", branch))).Fail("Mã số thuế");
            var customer = await _factory.QueryAsync(db => db.Customer.AsNoTracking().SingleAsync(x => x.TaxCode == "0312345360"));
            customer.Code.Should().MatchRegex(@"^KHB-\d{5}$");
            customer.CIF.Should().Be(customer.Code, "D-63 h: CIF = Code");
            customer.Name.Should().Be("Trường Ánh Dương");
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
            var location = await _factory.QueryAsync(db => db.CustomerDeliveryLocation.AsNoTracking().SingleAsync(x => x.CustomerId == customer));
            location.Code.Should().MatchRegex(@"^DP-\d{5}$", "MA-1.7: mã điểm giao máy chủ sinh");
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
            (await c.PostFormAsync("/api/Ingredient/Insert", ("Code", "NL-OK"), ("Name", "Thịt NL-OK"), ("IsExpiryManaged", true), ("ShelfLifeDays", 3), ("MinimumStock", 50m))).Ok();
            var id = await _factory.QueryAsync(db => db.Ingredient.Where(x => x.Name == "Thịt NL-OK").Select(x => x.Id).SingleAsync());
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

        [Fact]
        public async Task Supplier_overview_profile_alerts_and_monthly_trend()
        {
            // UI-3 (D-67 f1, f2): thẻ chỉ số, đánh giá, khối Cần chú ý và xu hướng theo tháng của 1 NCC.
            DateTime today = DateTime.UtcNow.AddHours(7).Date, now = DateTime.UtcNow.AddHours(7);
            Guid supplier = Guid.NewGuid(), ingredient = Guid.NewGuid(), warehouse = Guid.NewGuid(), unit = Guid.NewGuid();
            Guid poLate = Guid.NewGuid(), poDone = Guid.NewGuid(), gr = Guid.NewGuid(), lotHold = Guid.NewGuid(), lotSoon = Guid.NewGuid(), lotEmpty = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Supplier.Add(new Supplier { Id = supplier, Code = "NC-UI3", Name = "NCC chi tiết" });
                db.Ingredient.Add(new Ingredient { Id = ingredient, Code = "NL-UI3", Name = "Thịt" });
                db.SupplierMaterial.Add(new SupplierMaterial { Id = Guid.NewGuid(), SupplierId = supplier, IngredientId = ingredient, UnitPrice = 100, EffectiveFrom = today.AddDays(-30), EffectiveTo = today.AddDays(10) });
                db.SupplierMaterial.Add(new SupplierMaterial { Id = Guid.NewGuid(), SupplierId = supplier, IngredientId = ingredient, UnitPrice = 90, EffectiveTo = today.AddDays(40) });
                db.PurchaseOrder.Add(new PurchaseOrder { Id = poLate, SupplierId = supplier, PurchaseOrderNumber = "PO-UI3-LATE", Status = "Sent", OrderDate = today.AddDays(-5), ExpectedDeliveryDate = today.AddDays(-1), TotalAmount = 1_000_000 });
                db.PurchaseOrder.Add(new PurchaseOrder { Id = Guid.NewGuid(), SupplierId = supplier, PurchaseOrderNumber = "PO-UI3-DRAFT", Status = "Draft", OrderDate = today, ExpectedDeliveryDate = today.AddDays(-3), TotalAmount = 500_000 });
                db.PurchaseOrder.Add(new PurchaseOrder { Id = poDone, SupplierId = supplier, PurchaseOrderNumber = "PO-UI3-DONE", Status = "Received", OrderDate = today.AddDays(-3), ExpectedDeliveryDate = today.AddDays(-2), TotalAmount = 2_000_000 });
                db.GoodsReceipt.Add(new GoodsReceipt { Id = gr, SupplierId = supplier, PurchaseOrderId = poDone, WarehouseId = warehouse, ReceiptDate = today.AddDays(-2), Status = "Posted", ReceiptNumber = "NK-UI3" });
                db.GoodsReceiptLine.Add(new GoodsReceiptLine { Id = Guid.NewGuid(), GoodsReceiptId = gr, IngredientId = ingredient, ReceivedQuantity = 100, AcceptedQuantity = 90, RejectedQuantity = 10, UnitCost = 20_000, UnitId = unit, UomId = unit });
                db.SupplierIssue.Add(new SupplierIssue { Id = Guid.NewGuid(), SupplierId = supplier, IssueType = "LATE", IssueStatus = "Open", ReportedAt = now.AddDays(-3), DueAt = now.AddDays(-1), Description = "Giao thiếu" });
                db.SupplierIssue.Add(new SupplierIssue { Id = Guid.NewGuid(), SupplierId = supplier, IssueType = "QUALITY", IssueStatus = "Closed", ReportedAt = now.AddDays(-10), DueAt = now.AddDays(-5), ClosedAt = now.AddDays(-6), Description = "Sai quy cách" });
                db.MaterialLot.Add(new MaterialLot { Id = lotHold, SupplierId = supplier, IngredientId = ingredient, Status = "OnHold", LotNumber = "L-HOLD", LotNo = "L-HOLD" });
                db.MaterialLot.Add(new MaterialLot { Id = lotSoon, SupplierId = supplier, IngredientId = ingredient, Status = "Available", ExpiryDate = today.AddDays(1), LotNumber = "L-SOON", LotNo = "L-SOON" });
                db.MaterialLot.Add(new MaterialLot { Id = lotEmpty, SupplierId = supplier, IngredientId = ingredient, Status = "OnHold", LotNumber = "L-EMPTY", LotNo = "L-EMPTY" });
                foreach (var lot in new[] { lotHold, lotSoon })
                {
                    db.InventoryBalance.Add(new InventoryBalance { Id = Guid.NewGuid(), WarehouseId = warehouse, IngredientId = ingredient, MaterialLotId = lot, OnHandQuantity = 5, UnitId = unit });
                }
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();

            var o = (await c.GetResultAsync($"/api/Supplier/GetOverview?id={supplier}")).Ok();
            o.GetProperty("openPoCount").GetInt32().Should().Be(2, "Draft + Sent; Received không còn mở");
            o.GetProperty("openPoAmount").GetDecimal().Should().Be(1_500_000);
            o.GetProperty("overduePoCount").GetInt32().Should().Be(1, "PO Nháp chưa gửi NCC không tính trễ");
            o.GetProperty("receiptCount90d").GetInt32().Should().Be(1);
            o.GetProperty("onTimeRate").GetDecimal().Should().Be(100);
            o.GetProperty("rejectRate").GetDecimal().Should().Be(10);
            o.GetProperty("qcPassRate").GetDecimal().Should().Be(90);
            o.GetProperty("openIssueCount").GetInt32().Should().Be(1);
            o.GetProperty("overdueIssueCount").GetInt32().Should().Be(1);
            o.GetProperty("issueOnTimePct").GetDecimal().Should().Be(50);
            o.GetProperty("lotOnHoldCount").GetInt32().Should().Be(1, "lô hết tồn không tính");
            o.GetProperty("lotNearExpiryCount").GetInt32().Should().Be(1);
            o.GetProperty("priceExpiringCount").GetInt32().Should().Be(1, "chỉ giá hết hiệu lực trong 15 ngày");
            var alerts = o.GetProperty("listAlert").EnumerateArray().ToList();
            alerts.Select(a => a.GetProperty("code").GetString()).Should().Equal("PO_OVERDUE", "ISSUE_OVERDUE", "LOT_ON_HOLD", "PRICE_EXPIRING", "LOT_EXPIRING");
            alerts[0].GetProperty("message").GetString().Should().Contain("PO-UI3-LATE");
            alerts[0].GetProperty("screen").GetString().Should().Be("scm-po");

            var trend = (await c.GetResultAsync($"/api/Supplier/GetTrend?id={supplier}")).Ok();
            var months = trend.GetProperty("listMonth").EnumerateArray().ToList();
            months.Should().HaveCount(6);
            months.Sum(m => m.GetProperty("poAmount").GetDecimal()).Should().Be(3_500_000);
            months.Sum(m => m.GetProperty("receivedAmount").GetDecimal()).Should().Be(1_800_000, "90 đạt × 20.000");
            months.Sum(m => m.GetProperty("receiptCount").GetInt32()).Should().Be(1);
            (await c.GetResultAsync($"/api/Supplier/GetTrend?id={supplier}&from={today:yyyy-MM-dd}&to={today.AddMonths(-1):yyyy-MM-dd}")).Fail("Khoảng tháng");
            (await c.GetResultAsync($"/api/Supplier/GetTrend?id={supplier}&from={today.AddMonths(-30):yyyy-MM-dd}")).Fail("Khoảng tháng");
        }
    }
}
