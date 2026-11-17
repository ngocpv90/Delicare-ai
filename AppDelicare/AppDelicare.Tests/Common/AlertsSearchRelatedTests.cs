using System.Net;
using System.Text.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Services.Implement;
using AppDelicare.Tests.Integration;
using AppDelicare.WebApi.Security;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Common
{
    /// <summary>
    /// BE3.3 – chuông cảnh báo (Catalog 18, spec 12.1), tìm kiếm toàn cục (17), tab Liên quan (16): quy tắc từng cảnh báo + lọc chi nhánh (đơn vị con),
    /// lọc theo quyền Xem từng màn (duyệt = hộp duyệt của chính người dùng), cache 60 giây theo (người, chi nhánh); tìm kiếm ≥ 2 ký tự, ≤ 5 / loại,
    /// chỉ loại được Xem, màn hình theo menu được Xem (không dấu); Liên quan theo danh sách trắng, ≤ 8 dòng + tổng, bỏ nhóm không được Xem, 403 / 400.
    /// Không trả tên bảng / cột (D-58).
    /// </summary>
    public class AlertsSearchRelatedTests : IClassFixture<InMemoryApiFactory>
    {
        private static readonly DateTime Now = new(2032, 5, 10, 10, 0, 0);
        private readonly InMemoryApiFactory _factory;

        public AlertsSearchRelatedTests(InMemoryApiFactory factory)
        {
            _factory = factory;
            _factory.Clock.FixedNow = Now;
        }

        private static string Key() => Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        private static Dictionary<string, JsonElement> ByCode(LegacyResult result)
        {
            result.Code.Should().Be(200, result.Message);
            return result.ListData.EnumerateArray().ToDictionary(a => a.GetProperty("code").GetString()!);
        }

        private static int CountOf(Dictionary<string, JsonElement> alerts, string code) => alerts.TryGetValue(code, out var a) ? a.GetProperty("count").GetInt32() : 0;

        [Fact]
        public void Search_types_and_related_groups_are_a_whitelist_of_real_screens()
        {
            CommonRelations.SearchTypes.Select(t => t.Resource.Key).Should().Equal("Customer", "Order", "Ingredient", "Dish", "Supplier", "MaterialLot", "Employee", "PurchaseOrder");
            foreach (var (source, groups) in CommonRelations.Related)
            {
                CommonRegistry.Find(source).Should().NotBeNull($"nguồn {source} phải thuộc danh sách trắng của API dùng chung");
                groups.Select(g => g.Key).Should().OnlyHaveUniqueItems();
                foreach (var g in groups)
                {
                    ScreenMap.ByClass.Should().ContainKey(g.Key);
                    ScreenMap.ByClass[g.Key].Should().Be(g.ScreenId, $"màn của nhóm {g.Key} phải khớp ScreenMap");
                    g.Title.Should().NotBeNullOrWhiteSpace();
                }
            }
            CommonService.AlertScreens.Values.Should().OnlyContain(s => ScreenMap.ByClass.Values.Contains(s), "route cảnh báo phải là màn hình có thật");
            CommonService.Fold("Đơn Hàng Tồn Kho").Should().Be("don hang ton kho");
        }

        [Fact]
        public async Task Alerts_follow_rules_branch_tree_permissions_and_cache()
        {
            string key = Key();
            Guid company = Guid.NewGuid(), branch = Guid.NewGuid(), kitchen = Guid.NewGuid(), other = Guid.NewGuid();
            Guid wh = Guid.NewGuid(), whOther = Guid.NewGuid(), unit = Guid.NewGuid(), supplier = Guid.NewGuid();
            Guid ingPolicy = Guid.NewGuid(), ingOtherPolicy = Guid.NewGuid(), ingNoPolicy = Guid.NewGuid(), ingLots = Guid.NewGuid();
            Guid zone = Guid.NewGuid(), zoneOther = Guid.NewGuid(), channel = Guid.NewGuid();
            Guid custBranch = Guid.NewGuid(), custOther = Guid.NewGuid();
            DateTime today = Now.Date;
            await _factory.SeedAsync(db =>
            {
                db.OrganizationUnit.AddRange(
                    new OrganizationUnit { Id = branch, CompanyId = company, Code = "CN-" + key, Name = "Chi nhánh " + key, UnitType = "BRANCH" },
                    new OrganizationUnit { Id = kitchen, CompanyId = company, ParentId = branch, Code = "BEP-" + key, Name = "Bếp " + key, UnitType = "KITCHEN" },
                    new OrganizationUnit { Id = other, CompanyId = company, Code = "CN2-" + key, Name = "Chi nhánh khác " + key, UnitType = "BRANCH" });
                // Kho thuộc đơn vị con của chi nhánh → vẫn tính cho chi nhánh.
                db.Warehouse.AddRange(new Warehouse { Id = wh, Code = "K-" + key, Name = "Kho " + key, OrganizationUnitId = kitchen, Status = "Active" },
                    new Warehouse { Id = whOther, Code = "K2-" + key, Name = "Kho khác " + key, OrganizationUnitId = other, Status = "Active" });
                db.UnitMeasure.Add(new UnitMeasure { Id = unit, Code = key, Name = "kg" });
                db.Supplier.Add(new Supplier { Id = supplier, Code = "NCC-" + key, Name = "NCC " + key, Status = "Active" });
                db.Ingredient.AddRange(
                    new Ingredient { Id = ingPolicy, Code = "A-" + key, Name = "NL chính sách " + key, Status = "Active" },
                    new Ingredient { Id = ingOtherPolicy, Code = "B-" + key, Name = "NL kho khác " + key, Status = "Active" },
                    new Ingredient { Id = ingNoPolicy, Code = "C-" + key, Name = "NL tối thiểu " + key, Status = "Active", MinimumStock = 100 },
                    new Ingredient { Id = ingLots, Code = "D-" + key, Name = "NL lô " + key, Status = "Active" });
                db.InventoryPolicy.AddRange(
                    new InventoryPolicy { Id = Guid.NewGuid(), WarehouseId = wh, IngredientId = ingPolicy, MinimumStock = 10, UseFefo = true },
                    new InventoryPolicy { Id = Guid.NewGuid(), WarehouseId = whOther, IngredientId = ingOtherPolicy, MinimumStock = 10, UseFefo = true });
                InventoryBalance Bal(Guid w, Guid ing, decimal onHand, decimal available, Guid? lot = null) => new()
                {
                    Id = Guid.NewGuid(), WarehouseId = w, IngredientId = ing, MaterialLotId = lot, UnitId = unit, OnHandQuantity = onHand, OnHandQty = onHand,
                    AvailableQuantity = available, AvailableQty = available, UpdatedDate = Now, LastCalculatedDate = Now,
                };
                db.InventoryBalance.AddRange(Bal(wh, ingPolicy, 5, 5), Bal(wh, ingNoPolicy, 50, 50));

                // Lô: tạm giữ còn tồn (tính), khả dụng hết hạn ngày mai còn tồn (tính), hết hạn sau 5 ngày (không), hết hạn mai nhưng hết tồn (không),
                // tạm giữ ở kho chi nhánh khác (không tính cho chi nhánh).
                Guid lotHold = Guid.NewGuid(), lotSoon = Guid.NewGuid(), lotLater = Guid.NewGuid(), lotEmpty = Guid.NewGuid(), lotHoldOther = Guid.NewGuid();
                MaterialLot Lot(Guid id, string no, string status, string qc, DateTime expiry) => new()
                {
                    Id = id, IngredientId = ingLots, SupplierId = supplier, LotNumber = no + key, LotNo = no + key, Status = status, QcStatus = qc, ExpiryDate = expiry, ReceivedDate = today.AddDays(-3),
                };
                db.MaterialLot.AddRange(Lot(lotHold, "H-", "Available", "HOLD", today.AddDays(30)), Lot(lotSoon, "S-", "Available", "DAT", today.AddDays(1)),
                    Lot(lotLater, "L-", "Available", "DAT", today.AddDays(5)), Lot(lotEmpty, "E-", "Available", "DAT", today.AddDays(1)),
                    Lot(lotHoldOther, "O-", "OnHold", "DAT", today.AddDays(30)));
                db.InventoryBalance.AddRange(Bal(wh, ingLots, 3, 0, lotHold), Bal(wh, ingLots, 4, 4, lotSoon), Bal(wh, ingLots, 4, 4, lotLater), Bal(wh, ingLots, 0, 0, lotEmpty),
                    Bal(whOther, ingLots, 2, 0, lotHoldOther));

                // QC đầu vào: 1 phiếu Chờ QC (2 dòng) ở kho chi nhánh, 1 phiếu đã ghi sổ, 1 phiếu Chờ QC ở kho khác.
                Guid grPending = Guid.NewGuid();
                db.GoodsReceipt.AddRange(
                    new GoodsReceipt { Id = grPending, ReceiptNumber = "PN-" + key, SupplierId = supplier, WarehouseId = wh, ReceiptDate = today, Status = "QCPending" },
                    new GoodsReceipt { Id = Guid.NewGuid(), ReceiptNumber = "PN2-" + key, SupplierId = supplier, WarehouseId = wh, ReceiptDate = today, Status = "Posted" },
                    new GoodsReceipt { Id = Guid.NewGuid(), ReceiptNumber = "PN3-" + key, SupplierId = supplier, WarehouseId = whOther, ReceiptDate = today, Status = "QCPending" });
                for (int i = 0; i < 2; i++)
                {
                    db.GoodsReceiptLine.Add(new GoodsReceiptLine { Id = Guid.NewGuid(), GoodsReceiptId = grPending, IngredientId = ingLots, UomId = unit, UnitId = unit, Quantity = 1, ReceivedQuantity = 1 });
                }

                // Nhiệt độ: ngoài ngưỡng 10 giờ trước (IoT – tính), 60 giờ trước (ngoài 48 giờ), trong ngưỡng, ngoài ngưỡng ở kho khác, mẻ nấu không gắn kho (tính mọi chi nhánh).
                db.StorageZone.AddRange(new StorageZone { Id = zone, WarehouseId = wh, Code = "Z-" + key, Name = "Kho lạnh" }, new StorageZone { Id = zoneOther, WarehouseId = whOther, Code = "Z2-" + key, Name = "Kho lạnh 2" });
                TemperatureLog Temp(Guid? z, double hoursAgo, bool ok, string source = "MANUAL") => new()
                {
                    Id = Guid.NewGuid(), StorageZoneId = z, MeasurementType = "STORAGE", TemperatureC = ok ? 3 : 12, MinAllowedC = 0, MaxAllowedC = 5, IsWithinRange = ok,
                    MeasuredAt = Now.AddHours(-hoursAgo), SourceChannel = source, DeviceCode = "DEV-" + key,
                };
                db.TemperatureLog.AddRange(Temp(zone, 10, false, "IOT"), Temp(zone, 60, false), Temp(zone, 2, true), Temp(zoneOther, 1, false), Temp(null, 3, false));

                // Phản ánh: khách của chi nhánh – 1 Mới quá hạn, 1 Đang xử lý còn hạn, 1 Đóng; khách chi nhánh khác – 1 Mới.
                db.Customer.AddRange(new Customer { CIF = Guid.NewGuid().ToString("N")[..10], CustomerType = "FACTORY", DisplayName = "KH", Id = custBranch, Code = "KH-" + key, Name = "Khách " + key, BranchId = branch, Status = "Active" },
                    new Customer { CIF = Guid.NewGuid().ToString("N")[..10], CustomerType = "FACTORY", DisplayName = "KH", Id = custOther, Code = "KH2-" + key, Name = "Khách khác " + key, BranchId = other, Status = "Active" });
                CustomerComplaint Complaint(Guid customer, string status, DateTime sla, int i) => new()
                {
                    Id = Guid.NewGuid(), ComplaintNo = $"PA-{key}-{i}", CustomerId = customer, ComplaintStatus = status, ReceivedAt = Now.AddDays(-2), SlaDueAt = sla, Severity = "MEDIUM", Description = "Phản ánh",
                };
                db.CustomerComplaint.AddRange(Complaint(custBranch, "Open", Now.AddHours(-1), 1), Complaint(custBranch, "InProgress", Now.AddHours(5), 2),
                    Complaint(custBranch, "Closed", Now.AddHours(-30), 3), Complaint(custOther, "Open", Now.AddHours(-1), 4));

                // Tin nhắn: 1 lỗi, 1 đã gửi.
                db.OutboundMessage.AddRange(
                    new OutboundMessage { Id = Guid.NewGuid(), IntegrationChannelId = channel, PayloadJson = "{}", QueuedAt = Now, MessageStatus = "Failed", LastError = "timeout" },
                    new OutboundMessage { Id = Guid.NewGuid(), IntegrationChannelId = channel, PayloadJson = "{}", QueuedAt = Now, MessageStatus = "Sent" });
                return Task.CompletedTask;
            });

            using var admin = _factory.CreateAuthorizedClient();
            var inBranch = ByCode(await admin.GetResultAsync($"/api/Common/GetAlerts?branchId={branch}"));
            CountOf(inBranch, "STOCK_BELOW_MIN").Should().Be(2, "NL dưới chính sách ở kho của chi nhánh + NL không có chính sách dưới Ingredient.MinimumStock");
            CountOf(inBranch, "QC_INCOMING_PENDING").Should().Be(1);
            inBranch["QC_INCOMING_PENDING"].GetProperty("detail").GetString().Should().Contain("2 dòng");
            CountOf(inBranch, "LOT_ON_HOLD").Should().Be(1);
            CountOf(inBranch, "LOT_EXPIRING").Should().Be(1);
            CountOf(inBranch, "TEMPERATURE_OUT_OF_RANGE").Should().Be(2, "lần đo IoT trong 48 giờ + lần đo không gắn kho");
            CountOf(inBranch, "COMPLAINT_OVERDUE").Should().Be(1);
            CountOf(inBranch, "COMPLAINT_OPEN").Should().Be(1);
            CountOf(inBranch, "OUTBOUND_FAILED").Should().Be(1, "tin nhắn không gắn chi nhánh");
            inBranch["STOCK_BELOW_MIN"].GetProperty("route").GetString().Should().Be("#/scm-inv-balance");
            inBranch["STOCK_BELOW_MIN"].GetProperty("severity").GetString().Should().Be("HIGH");
            inBranch["STOCK_BELOW_MIN"].GetProperty("title").GetString().Should().NotBeNullOrWhiteSpace();
            var severities = (await admin.GetResultAsync($"/api/Common/GetAlerts?branchId={branch}")).ListData.EnumerateArray().Select(a => a.GetProperty("severity").GetString()).ToList();
            severities.Should().Equal(severities.OrderBy(s => s == "HIGH" ? 0 : s == "MEDIUM" ? 1 : 2), "cao trước");

            var otherBranch = ByCode(await admin.GetResultAsync($"/api/Common/GetAlerts?branchId={other}"));
            CountOf(otherBranch, "STOCK_BELOW_MIN").Should().Be(2, "NL có chính sách ở kho khác + NL không chính sách (chi nhánh này không có tồn)");
            CountOf(otherBranch, "LOT_ON_HOLD").Should().Be(1);
            otherBranch.Should().NotContainKey("LOT_EXPIRING", "chỉ trả cảnh báo có số > 0");
            CountOf(otherBranch, "COMPLAINT_OVERDUE").Should().Be(1);
            CountOf(otherBranch, "TEMPERATURE_OUT_OF_RANGE").Should().Be(2);

            var all = ByCode(await admin.GetResultAsync("/api/Common/GetAlerts"));
            CountOf(all, "STOCK_BELOW_MIN").Should().Be(3);
            CountOf(all, "QC_INCOMING_PENDING").Should().Be(2);
            CountOf(all, "LOT_ON_HOLD").Should().Be(2);
            CountOf(all, "TEMPERATURE_OUT_OF_RANGE").Should().Be(3);
            CountOf(all, "COMPLAINT_OVERDUE").Should().Be(2);

            (await admin.GetResultAsync($"/api/Common/GetAlerts?branchId={Guid.NewGuid()}")).Fail("chi nhánh");

            // Cache 60 giây theo (người, chi nhánh): thêm 1 tin lỗi → cùng khóa vẫn số cũ, khóa khác (đơn vị con) thấy số mới.
            await _factory.SeedAsync(db =>
            {
                db.OutboundMessage.Add(new OutboundMessage { Id = Guid.NewGuid(), IntegrationChannelId = channel, PayloadJson = "{}", QueuedAt = Now, MessageStatus = "Failed" });
                return Task.CompletedTask;
            });
            CountOf(ByCode(await admin.GetResultAsync($"/api/Common/GetAlerts?branchId={branch}")), "OUTBOUND_FAILED").Should().Be(1, "trong 60 giây trả kết quả đã cache");
            CountOf(ByCode(await admin.GetResultAsync($"/api/Common/GetAlerts?branchId={kitchen}")), "OUTBOUND_FAILED").Should().Be(2);

            // Quyền: chỉ Xem "Lô nguyên liệu" → chỉ cảnh báo lô; không có màn nào → rỗng.
            using var lotKeeper = _factory.CreateAuthorizedClient(await CommonTestUsers.UserAsync(_factory, ("scm-lot", "V")));
            ByCode(await lotKeeper.GetResultAsync($"/api/Common/GetAlerts?branchId={branch}")).Keys.Should().BeEquivalentTo("LOT_ON_HOLD", "LOT_EXPIRING");
            using var nobody = _factory.CreateAuthorizedClient(await CommonTestUsers.UserAsync(_factory, ("scm-lot", "")));
            ByCode(await nobody.GetResultAsync($"/api/Common/GetAlerts?branchId={branch}")).Should().BeEmpty("có dòng quyền nhưng không có Xem");
        }

        [Fact]
        public async Task Approval_alert_counts_only_own_inbox()
        {
            string key = "AL-" + Key();
            Guid dish = Guid.NewGuid(), ing = Guid.NewGuid(), kg = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Dish.Add(new Dish { Id = dish, Code = key, Name = "Món " + key, Status = "Active" });
                db.UnitMeasure.Add(new UnitMeasure { Id = kg, Name = "kg-" + key });
                db.Ingredient.Add(new Ingredient { Id = ing, Code = key + "-NL", Name = "NL " + key });
                return Task.CompletedTask;
            });
            using var admin = _factory.CreateAuthorizedClient();
            Guid recipe = (await admin.PostFormAsync("/api/Recipe/CreateVersion", ("DishId", dish))).Ok().GetProperty("id").GetGuid();
            (await admin.PostFormAsync("/api/RecipeLine/Insert", ("RecipeId", recipe), ("IngredientId", ing), ("Quantity", 10m), ("UnitId", kg))).Ok();
            (await admin.PostFormAsync("/api/Recipe/Submit", ("id", recipe))).Ok();

            Guid approver = Guid.NewGuid(), group = Guid.NewGuid();
            await _factory.SeedAsync(async db =>
            {
                db.User.Add(new User { Id = approver, UserName = "al-" + approver.ToString("N")[..6], IsActive = true });
                db.UserGroup.Add(new UserGroup { Id = group, Name = "AL-" + group.ToString("N")[..6] });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = approver, UserGroupId = group });
                var menu = await db.ObjectMenu.FirstOrDefaultAsync(m => m.MetaLink == "#/wf-inbox?type=menu-recipe");
                if (menu == null)
                {
                    menu = new ObjectMenu { Id = Guid.NewGuid(), Name = "Duyệt – Công thức", MetaLink = "#/wf-inbox?type=menu-recipe", IsActive = true, LevelShow = 2 };
                    db.ObjectMenu.Add(menu);
                }
                db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = group, ObjectMenuId = menu.Id, AllowAccess = true, AllowEdit = true });
            });
            using var approverClient = _factory.CreateAuthorizedClient(approver);
            var alerts = ByCode(await approverClient.GetResultAsync("/api/Common/GetAlerts"));
            alerts.Keys.Should().Equal("APPROVAL_PENDING");
            alerts["APPROVAL_PENDING"].GetProperty("count").GetInt32().Should().Be(1, "không cần quyền Xem màn nào – hộp duyệt của chính người dùng");
            alerts["APPROVAL_PENDING"].GetProperty("route").GetString().Should().Be("#/wf-inbox");

            using var stranger = _factory.CreateAuthorizedClient(await CommonTestUsers.UserAsync(_factory, ("menu-recipe", "V")));
            ByCode(await stranger.GetResultAsync("/api/Common/GetAlerts")).Should().NotContainKey("APPROVAL_PENDING", "không có quyền Duyệt");
        }

        [Fact]
        public async Task Global_search_returns_allowed_screens_and_whitelisted_records_only()
        {
            string key = "Q" + Key();
            Guid customer = Guid.NewGuid(), supplier = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Customer.Add(new Customer { CIF = Guid.NewGuid().ToString("N")[..10], CustomerType = "FACTORY", DisplayName = "KH", Id = customer, Code = key + "-KH", Name = "Khách " + key, Status = "Active" });
                db.Order.Add(new Order { Id = Guid.NewGuid(), OrderNumber = key + "-DH", OrderNo = key + "-DH", CustomerId = customer, OrderDate = Now.Date, MealDate = Now.Date.AddDays(1), DeliveryDate = Now.Date.AddDays(1), Status = "Draft" });
                for (int i = 0; i < 7; i++)
                {
                    db.Ingredient.Add(new Ingredient { Id = Guid.NewGuid(), Code = $"{key}-NL{i}", Name = "Nguyên liệu " + i, Status = "Active" });
                }
                db.Dish.Add(new Dish { Id = Guid.NewGuid(), Code = key + "-M", Name = "Món " + key, Status = "Active" });
                db.Supplier.Add(new Supplier { Id = supplier, Code = key + "-NCC", Name = "NCC " + key, Status = "Active" });
                db.PurchaseOrder.Add(new PurchaseOrder { Id = Guid.NewGuid(), PurchaseOrderNumber = key + "-PO", PONo = key + "-PO", SupplierId = supplier, OrderDate = Now.Date, Status = "Draft" });
                db.Employee.Add(new Employee { Id = Guid.NewGuid(), Code = key + "-NV", EmployeeCode = key + "-NV", FullName = "Nhân viên " + key, Status = "Active" });
                db.Warehouse.Add(new Warehouse { Id = Guid.NewGuid(), Code = key + "-KHO", Name = "Kho " + key, Status = "Active" }); // không thuộc danh sách tìm kiếm
                db.ObjectMenu.AddRange(
                    new ObjectMenu { Id = Guid.NewGuid(), Name = "Tra cứu đặc biệt " + key, MetaLink = "#/zz-" + key.ToLowerInvariant(), IsActive = true, LevelShow = 2 },
                    new ObjectMenu { Id = Guid.NewGuid(), Name = "Đặc biệt ẩn " + key, MetaLink = "#/zz-hidden-" + key.ToLowerInvariant(), IsActive = true, LevelShow = 2 });
                return Task.CompletedTask;
            });
            using var admin = _factory.CreateAuthorizedClient();
            (await admin.GetResultAsync("/api/Common/GlobalSearch?q=a")).Fail("2");
            (await admin.GetResultAsync("/api/Common/GlobalSearch?q=%20%20")).Fail();
            (await admin.GetResultAsync("/api/Common/GlobalSearch")).Fail();

            var data = (await admin.GetResultAsync($"/api/Common/GlobalSearch?q={key}")).Ok();
            var records = data.GetProperty("records").EnumerateArray().ToList();
            records.GroupBy(r => r.GetProperty("type").GetString()).ToDictionary(g => g.Key!, g => g.Count()).Should().BeEquivalentTo(new Dictionary<string, int>
            {
                ["Customer"] = 1, ["Order"] = 1, ["Ingredient"] = 5, ["Dish"] = 1, ["Supplier"] = 1, ["Employee"] = 1, ["PurchaseOrder"] = 1,
            }, "mỗi loại ≤ 5; kho không thuộc danh sách tìm kiếm");
            var order = records.Single(r => r.GetProperty("type").GetString() == "Order");
            order.GetProperty("route").GetString().Should().Be("#/crm-order");
            order.GetProperty("typeTitle").GetString().Should().Be("Đơn đặt suất ăn");
            order.GetProperty("code").GetString().Should().Be(key + "-DH");
            order.GetProperty("label").GetString().Should().Contain("Khách " + key);
            order.GetProperty("statusName").GetString().Should().NotBeNullOrEmpty();

            // Màn hình: không dấu, không phân biệt hoa thường; admin thấy cả 2 mục.
            var screens = (await admin.GetResultAsync($"/api/Common/GlobalSearch?q=dac biet {key.ToLowerInvariant()}")).Ok().GetProperty("screens").EnumerateArray().ToList();
            screens.Should().ContainSingle(s => s.GetProperty("route").GetString() == "#/zz-" + key.ToLowerInvariant());

            // User chỉ Xem khách hàng + mục menu "Tra cứu đặc biệt": chỉ bản ghi khách, chỉ màn được Xem.
            using var sales = _factory.CreateAuthorizedClient(await CommonTestUsers.UserAsync(_factory, ("crm-customer", "V"), ("zz-" + key.ToLowerInvariant(), "V"), ("crm-order", "")));
            var limited = (await sales.GetResultAsync($"/api/Common/GlobalSearch?q={key}")).Ok();
            limited.GetProperty("records").EnumerateArray().Select(r => r.GetProperty("type").GetString()).Should().Equal("Customer");
            var limitedScreens = (await sales.GetResultAsync("/api/Common/GlobalSearch?q=đặc biệt")).Ok().GetProperty("screens").EnumerateArray()
                .Select(s => s.GetProperty("route").GetString()).ToList();
            limitedScreens.Should().Contain("#/zz-" + key.ToLowerInvariant()).And.NotContain("#/zz-hidden-" + key.ToLowerInvariant());

            // D-58: không có tên bảng / cột trong kết quả.
            string raw = await admin.GetStringAsync($"/api/Common/GlobalSearch?q={key}");
            foreach (var forbidden in new[] { "Dishe", "OrderNumber", "PurchaseOrderNumber", "LotNumber", "IngredientId", "CustomerId", "\"table\"", "\"column\"" })
            {
                raw.Should().NotContain(forbidden);
            }
        }

        [Fact]
        public async Task Related_tab_groups_limits_permissions_and_guards()
        {
            string key = "R" + Key();
            Guid customer = Guid.NewGuid(), contract = Guid.NewGuid();
            var orders = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray();
            await _factory.SeedAsync(db =>
            {
                db.Customer.Add(new Customer { CIF = Guid.NewGuid().ToString("N")[..10], CustomerType = "FACTORY", DisplayName = "KH", Id = customer, Code = key, Name = "Khách " + key, BranchId = Guid.NewGuid(), Status = "Active" });
                db.Contract.Add(new Contract { Id = contract, CustomerId = customer, ContractNumber = "HD-" + key, Name = "Hợp đồng " + key, EffectiveFrom = Now.Date.AddMonths(-1), Status = "Active" });
                for (int i = 0; i < orders.Length; i++)
                {
                    db.Order.Add(new Order { Id = orders[i], OrderNumber = $"{key}-{i:00}", OrderNo = $"{key}-{i:00}", CustomerId = customer, ContractId = i < 3 ? contract : null,
                        OrderDate = Now.Date, MealDate = Now.Date.AddDays(i), DeliveryDate = Now.Date.AddDays(i), Status = "Confirmed" });
                }
                db.CustomerComplaint.Add(new CustomerComplaint { Id = Guid.NewGuid(), ComplaintNo = "PA-" + key, CustomerId = customer, OrderId = orders[0], ReceivedAt = Now, ComplaintStatus = "Open", Description = "Thiếu suất" });
                return Task.CompletedTask;
            });
            using var admin = _factory.CreateAuthorizedClient();
            var groups = (await admin.GetResultAsync($"/api/Common/GetRelated?resource=Customer&id={customer}")).ListData.EnumerateArray().ToList();
            groups.Select(g => g.GetProperty("key").GetString()).Should().Equal("Contract", "Order", "DeliveryOrder", "CustomerComplaint");
            var orderGroup = groups.Single(g => g.GetProperty("key").GetString() == "Order");
            orderGroup.GetProperty("count").GetInt32().Should().Be(10);
            orderGroup.GetProperty("title").GetString().Should().Be("Đơn đặt suất ăn");
            orderGroup.GetProperty("route").GetString().Should().Be("#/crm-order");
            var items = orderGroup.GetProperty("items").EnumerateArray().ToList();
            items.Should().HaveCount(8, "tối đa 8 dòng / nhóm");
            items[0].GetProperty("id").GetGuid().Should().Be(orders[9], "ngày ăn mới nhất trước");
            items[0].GetProperty("label").GetString().Should().StartWith($"{key}-09");
            items[0].GetProperty("statusName").GetString().Should().Be("Đã xác nhận");
            groups.Single(g => g.GetProperty("key").GetString() == "Contract").GetProperty("items")[0].GetProperty("statusName").GetString().Should().Be("Hiệu lực");
            groups.Single(g => g.GetProperty("key").GetString() == "DeliveryOrder").GetProperty("count").GetInt32().Should().Be(0);

            var contractGroups = (await admin.GetResultAsync($"/api/Common/GetRelated?resource=contract&id={contract}")).ListData.EnumerateArray().ToList();
            contractGroups.Single().GetProperty("count").GetInt32().Should().Be(3, "tên danh sách không phân biệt hoa thường");
            var orderRelated = (await admin.GetResultAsync($"/api/Common/GetRelated?resource=Order&id={orders[0]}")).ListData.EnumerateArray().ToList();
            orderRelated.Single(g => g.GetProperty("key").GetString() == "CustomerComplaint").GetProperty("count").GetInt32().Should().Be(1);

            // Quyền: Xem khách + đơn → chỉ nhóm Đơn; không Xem khách → 403.
            using var sales = _factory.CreateAuthorizedClient(await CommonTestUsers.UserAsync(_factory, ("crm-customer", "V"), ("crm-order", "V")));
            (await sales.GetResultAsync($"/api/Common/GetRelated?resource=Customer&id={customer}")).ListData.EnumerateArray()
                .Select(g => g.GetProperty("key").GetString()).Should().Equal("Order");
            using var outsider = _factory.CreateAuthorizedClient(await CommonTestUsers.UserAsync(_factory, ("crm-order", "V")));
            (await outsider.GetAsync($"/api/Common/GetRelated?resource=Customer&id={customer}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

            // Danh sách trắng + bản ghi không có → 400; không có tên bảng / cột trong kết quả.
            (await admin.GetResultAsync($"/api/Common/GetRelated?resource=Dishe&id={customer}")).Fail("không hỗ trợ");
            (await admin.GetResultAsync($"/api/Common/GetRelated?resource=Patient&id={customer}")).Fail();
            (await admin.GetResultAsync($"/api/Common/GetRelated?resource=Warehouse&id={customer}")).Fail("Liên quan");
            (await admin.GetResultAsync($"/api/Common/GetRelated?resource=Customer&id={Guid.NewGuid()}")).Fail("Không tìm thấy");
            string raw = await admin.GetStringAsync($"/api/Common/GetRelated?resource=Customer&id={customer}");
            foreach (var forbidden in new[] { "CustomerId", "ContractId", "OrderNumber", "\"table\"", "\"column\"" })
            {
                raw.Should().NotContain(forbidden);
            }
        }

        [Fact]
        public async Task Related_lot_traces_receipt_issue_and_batches()
        {
            string key = "T" + Key();
            Guid ing = Guid.NewGuid(), unit = Guid.NewGuid(), supplier = Guid.NewGuid(), wh = Guid.NewGuid(), gr = Guid.NewGuid(), grl = Guid.NewGuid(), lot = Guid.NewGuid();
            Guid dish = Guid.NewGuid(), recipe = Guid.NewGuid(), po = Guid.NewGuid(), issue = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Ingredient.Add(new Ingredient { Id = ing, Code = key, Name = "Gà " + key, Status = "Active" });
                db.Supplier.Add(new Supplier { Id = supplier, Code = key, Name = "NCC " + key, Status = "Active" });
                db.Warehouse.Add(new Warehouse { Id = wh, Code = key, Name = "Kho " + key, Status = "Active" });
                db.GoodsReceipt.Add(new GoodsReceipt { Id = gr, ReceiptNumber = "PN-" + key, SupplierId = supplier, WarehouseId = wh, ReceiptDate = Now.Date, Status = "Posted" });
                db.GoodsReceiptLine.Add(new GoodsReceiptLine { Id = grl, GoodsReceiptId = gr, IngredientId = ing, UomId = unit, UnitId = unit, Quantity = 5, ReceivedQuantity = 5, AcceptedQuantity = 5 });
                db.MaterialLot.Add(new MaterialLot { Id = lot, IngredientId = ing, SupplierId = supplier, GoodsReceiptLineId = grl, LotNumber = "LOT-" + key, LotNo = "LOT-" + key, Status = "Available", ReceivedDate = Now.Date });
                db.Dish.Add(new Dish { Id = dish, Code = key, Name = "Gà kho " + key, Status = "Active" });
                db.Recipe.Add(new Recipe { Id = recipe, DishId = dish, VersionCode = "CT-" + key, Status = "Locked" });
                db.RecipeLine.Add(new RecipeLine { Id = Guid.NewGuid(), RecipeId = recipe, IngredientId = ing, Quantity = 1, UomId = unit, UnitId = unit });
                db.MaterialIssue.Add(new MaterialIssue { Id = issue, IssueNumber = "PX-" + key, ProductionOrderId = po, WarehouseId = wh, IssueDate = Now.Date, Status = "Issued" });
                db.MaterialIssueLine.Add(new MaterialIssueLine { Id = Guid.NewGuid(), MaterialIssueId = issue, IngredientId = ing, MaterialLotId = lot, Quantity = 2, UomId = unit, UnitId = unit });
                db.ProductionBatch.AddRange(
                    new ProductionBatch { Id = Guid.NewGuid(), BatchNumber = "ME-" + key + "-1", ProductionOrderId = po, DishId = dish, RecipeId = recipe, StartDate = Now, Status = "Passed" },
                    new ProductionBatch { Id = Guid.NewGuid(), BatchNumber = "ME-" + key + "-2", ProductionOrderId = Guid.NewGuid(), DishId = dish, RecipeId = recipe, StartDate = Now, Status = "Passed" });
                return Task.CompletedTask;
            });
            using var admin = _factory.CreateAuthorizedClient();
            var groups = (await admin.GetResultAsync($"/api/Common/GetRelated?resource=MaterialLot&id={lot}")).ListData.EnumerateArray()
                .ToDictionary(g => g.GetProperty("key").GetString()!);
            groups["GoodsReceipt"].GetProperty("items")[0].GetProperty("label").GetString().Should().StartWith("PN-" + key);
            groups["MaterialIssue"].GetProperty("count").GetInt32().Should().Be(1);
            groups["ProductionBatch"].GetProperty("count").GetInt32().Should().Be(1, "chỉ mẻ của lệnh sản xuất có phiếu xuất lấy lô này");
            groups["ProductionBatch"].GetProperty("items")[0].GetProperty("label").GetString().Should().Contain("Gà kho " + key);

            var ingGroups = (await admin.GetResultAsync($"/api/Common/GetRelated?resource=Ingredient&id={ing}")).ListData.EnumerateArray()
                .ToDictionary(g => g.GetProperty("key").GetString()!);
            ingGroups["Recipe"].GetProperty("count").GetInt32().Should().Be(1);
            ingGroups["Recipe"].GetProperty("items")[0].GetProperty("statusName").GetString().Should().Be("Đã khóa");
            ingGroups["MaterialLot"].GetProperty("count").GetInt32().Should().Be(1);
        }
    }
}
