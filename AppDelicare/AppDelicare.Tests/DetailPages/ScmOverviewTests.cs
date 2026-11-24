using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.Json;

namespace AppDelicare.Tests.DetailPages
{
    /// <summary>
    /// UI-6 (D-80) nhóm B – trang chi tiết Mua hàng / Kho: PurchaseOrder / GoodsReceipt / PurchaseRequest / MaterialLot / Ingredient …/GetOverview.
    /// Mỗi API: có dữ liệu (chỉ số + tên), id sai → Fail "không tìm thấy", bản ghi không dòng / không giao dịch vẫn mở được,
    /// quyền theo màn gốc (có quyền xem màn danh sách → 200, chỉ có quyền màn khác → 403).
    /// </summary>
    public class ScmOverviewTests(InMemoryApiFactory factory) : IClassFixture<InMemoryApiFactory>
    {
        private async Task<Guid> UserAsync(params string[] viewLinks)
        {
            Guid userId = Guid.NewGuid(), group = Guid.NewGuid();
            await factory.SeedAsync(async db =>
            {
                db.User.Add(new User { Id = userId, UserName = "dpb-" + userId.ToString("N")[..6], IsActive = true });
                db.UserGroup.Add(new UserGroup { Id = group, Name = "DPB-" + group.ToString("N")[..6] });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = userId, UserGroupId = group });
                foreach (string link in viewLinks)
                {
                    var menu = await db.ObjectMenu.FirstOrDefaultAsync(m => m.MetaLink == link) ?? db.ObjectMenu.Local.FirstOrDefault(m => m.MetaLink == link);
                    if (menu == null)
                    {
                        menu = new ObjectMenu { Id = Guid.NewGuid(), Name = link, MetaLink = link, IsActive = true, LevelShow = 2 };
                        db.ObjectMenu.Add(menu);
                    }
                    db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = group, ObjectMenuId = menu.Id, AllowAccess = true });
                }
            });
            return userId;
        }

        private async Task PermissionAsync(string url, string screen, string otherScreen)
        {
            using var viewer = factory.CreateAuthorizedClient(await UserAsync("#/" + screen));
            (await viewer.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.OK, "có quyền xem " + screen);
            using var other = factory.CreateAuthorizedClient(await UserAsync("#/" + otherScreen));
            (await other.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Forbidden, "chỉ có quyền " + otherScreen);
        }

        /// <summary>Danh mục chung cho các test: kho, đơn vị, nguyên liệu, NCC (mã riêng theo khóa).</summary>
        private record Base(Guid Wh, Guid Kg, Guid Ing, Guid Sup);

        private async Task<Base> BaseAsync(string key)
        {
            var b = new Base(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
            await factory.SeedAsync(db =>
            {
                db.Warehouse.Add(new Warehouse { Id = b.Wh, Code = "KHO-" + key, Name = "Kho " + key });
                db.UnitMeasure.Add(new UnitMeasure { Id = b.Kg, Code = "KG-" + key, Name = "kg" });
                db.Ingredient.Add(new Ingredient { Id = b.Ing, Code = "NL-" + key, Name = "Thịt " + key, BaseUnitId = b.Kg, MinimumStock = 50 });
                db.Supplier.Add(new Supplier { Id = b.Sup, Code = "NCC-" + key, Name = "NCC " + key, Status = "Active" });
                return Task.CompletedTask;
            });
            return b;
        }

        [Fact]
        public async Task PurchaseOrder_overview_receipt_progress_qc_and_approval()
        {
            var b = await BaseAsync("DPB-PO");
            Guid po = Guid.NewGuid(), empty = Guid.NewGuid(), gr = Guid.NewGuid(), apr = Guid.NewGuid();
            await factory.SeedAsync(db =>
            {
                db.PurchaseOrder.AddRange(
                    new PurchaseOrder { Id = po, PurchaseOrderNumber = "PO-DPB1", SupplierId = b.Sup, OrderDate = new DateTime(2026, 11, 18), Status = "PartiallyReceived", TotalAmount = 2_000_000 },
                    new PurchaseOrder { Id = empty, PurchaseOrderNumber = "PO-DPB1-RONG", SupplierId = b.Sup, OrderDate = new DateTime(2026, 11, 18), Status = "Draft" });
                db.PurchaseOrderLine.Add(new PurchaseOrderLine { Id = Guid.NewGuid(), PurchaseOrderId = po, IngredientId = b.Ing, Quantity = 100, UnitId = b.Kg, UomId = b.Kg, UnitPrice = 20_000, LineAmount = 2_000_000 });
                db.GoodsReceipt.Add(new GoodsReceipt { Id = gr, ReceiptNumber = "PN-DPB1", PurchaseOrderId = po, SupplierId = b.Sup, WarehouseId = b.Wh, ReceiptDate = new DateTime(2026, 11, 19), Status = "Posted" });
                db.GoodsReceiptLine.Add(new GoodsReceiptLine { Id = Guid.NewGuid(), GoodsReceiptId = gr, IngredientId = b.Ing, UnitId = b.Kg, UomId = b.Kg, Quantity = 60, ReceivedQuantity = 60, AcceptedQuantity = 55, RejectedQuantity = 5, UnitCost = 20_000 });
                db.QcInspection.Add(new QcInspection { Id = Guid.NewGuid(), GoodsReceiptId = gr, InspectionNumber = "QC-DPB1", InspectionDate = new DateTime(2026, 11, 19), Result = "DAT_MOT_PHAN" });
                db.ApprovalRequest.Add(new ApprovalRequest { Id = apr, RequestNo = "YC-DPB1", BusinessType = "PURCHASE_ORDER", BusinessId = po, RequestedAt = new DateTime(2026, 11, 18, 8, 0, 0), RequestStatus = "Approved" });
                return Task.CompletedTask;
            });
            using var admin = factory.CreateAuthorizedClient();
            var o = (await admin.GetResultAsync($"/api/PurchaseOrder/GetOverview?id={po}")).Ok();
            o.GetProperty("detail").GetProperty("purchaseOrder").GetProperty("purchaseOrderNumber").GetString().Should().Be("PO-DPB1");
            o.GetProperty("detail").GetProperty("supplierName").GetString().Should().Be("NCC DPB-PO");
            o.GetProperty("lineCount").GetInt32().Should().Be(1);
            o.GetProperty("receiptCount").GetInt32().Should().Be(1);
            o.GetProperty("receivedQuantity").GetDecimal().Should().Be(55);
            o.GetProperty("receivedPercent").GetDecimal().Should().Be(55);
            o.GetProperty("remainingLineCount").GetInt32().Should().Be(1);
            o.GetProperty("rejectedQuantity").GetDecimal().Should().Be(5);
            o.GetProperty("approvalRequestId").GetGuid().Should().Be(apr);
            var qc = o.GetProperty("listReceiptQc")[0];
            qc.GetProperty("inspectionNumber").GetString().Should().Be("QC-DPB1");
            qc.GetProperty("warehouseName").GetString().Should().Be("Kho DPB-PO");

            var e = (await admin.GetResultAsync($"/api/PurchaseOrder/GetOverview?id={empty}")).Ok();
            e.GetProperty("lineCount").GetInt32().Should().Be(0, "PO không dòng vẫn mở được trang");
            e.GetProperty("receivedPercent").GetDecimal().Should().Be(0);
            e.GetProperty("approvalRequestId").ValueKind.Should().Be(JsonValueKind.Null, "chưa gửi duyệt");
            (await admin.GetResultAsync($"/api/PurchaseOrder/GetOverview?id={Guid.NewGuid()}")).Fail("Không tìm thấy");

            await PermissionAsync($"/api/PurchaseOrder/GetOverview?id={po}", "scm-po", "scm-stockcount");
        }

        [Fact]
        public async Task GoodsReceipt_overview_counts_rejected_lines_lots_and_value()
        {
            var b = await BaseAsync("DPB-GR");
            Guid gr = Guid.NewGuid(), empty = Guid.NewGuid(), lot = Guid.NewGuid();
            await factory.SeedAsync(db =>
            {
                db.GoodsReceipt.AddRange(
                    new GoodsReceipt { Id = gr, ReceiptNumber = "PN-DPB2", SupplierId = b.Sup, WarehouseId = b.Wh, ReceiptDate = new DateTime(2026, 11, 19), Status = "Posted" },
                    new GoodsReceipt { Id = empty, ReceiptNumber = "PN-DPB2-RONG", SupplierId = b.Sup, WarehouseId = b.Wh, ReceiptDate = new DateTime(2026, 11, 19), Status = "QCPending" });
                db.GoodsReceiptLine.AddRange(
                    new GoodsReceiptLine { Id = Guid.NewGuid(), GoodsReceiptId = gr, IngredientId = b.Ing, UnitId = b.Kg, UomId = b.Kg, Quantity = 10, ReceivedQuantity = 10, AcceptedQuantity = 10, UnitCost = 1_000, LotId = lot },
                    new GoodsReceiptLine { Id = Guid.NewGuid(), GoodsReceiptId = gr, IngredientId = b.Ing, UnitId = b.Kg, UomId = b.Kg, Quantity = 5, ReceivedQuantity = 5, AcceptedQuantity = 0, RejectedQuantity = 5, UnitCost = 1_000, QualityStatus = "KHONG_DAT" });
                db.MaterialLot.Add(new MaterialLot { Id = lot, LotNumber = "NL-DPB2", LotNo = "NL-DPB2", IngredientId = b.Ing, SupplierId = b.Sup, Status = "Available" });
                return Task.CompletedTask;
            });
            using var admin = factory.CreateAuthorizedClient();
            var o = (await admin.GetResultAsync($"/api/GoodsReceipt/GetOverview?id={gr}")).Ok();
            o.GetProperty("detail").GetProperty("goodsReceipt").GetProperty("receiptNumber").GetString().Should().Be("PN-DPB2");
            o.GetProperty("detail").GetProperty("warehouseName").GetString().Should().Be("Kho DPB-GR");
            o.GetProperty("lineCount").GetInt32().Should().Be(2);
            o.GetProperty("rejectedLineCount").GetInt32().Should().Be(1);
            o.GetProperty("lotCount").GetInt32().Should().Be(1);
            o.GetProperty("receivedValue").GetDecimal().Should().Be(15_000);
            o.GetProperty("acceptedValue").GetDecimal().Should().Be(10_000);

            (await admin.GetResultAsync($"/api/GoodsReceipt/GetOverview?id={empty}")).Ok().GetProperty("lineCount").GetInt32().Should().Be(0, "phiếu không dòng vẫn mở được trang");
            (await admin.GetResultAsync($"/api/GoodsReceipt/GetOverview?id={Guid.NewGuid()}")).Fail("Không tìm thấy");

            await PermissionAsync($"/api/GoodsReceipt/GetOverview?id={gr}", "scm-gr", "scm-po");
        }

        [Fact]
        public async Task PurchaseRequest_overview_requester_supplier_coverage_and_pos()
        {
            var b = await BaseAsync("DPB-PR");
            Guid pr = Guid.NewGuid(), empty = Guid.NewGuid(), emp = Guid.NewGuid(), ing2 = Guid.NewGuid();
            await factory.SeedAsync(db =>
            {
                db.Employee.Add(new Employee { Id = emp, Code = "NV-DPB3", FullName = "Bếp trưởng DPB3" });
                db.Ingredient.Add(new Ingredient { Id = ing2, Code = "NL-DPB3-2", Name = "Rau DPB3", BaseUnitId = b.Kg });
                db.SupplierMaterial.Add(new SupplierMaterial { Id = Guid.NewGuid(), SupplierId = b.Sup, IngredientId = b.Ing, UnitPrice = 30_000, IsPreferred = true, Status = "Active" });
                db.PurchaseRequest.AddRange(
                    new PurchaseRequest { Id = pr, RequestNumber = "DN-DPB3", RequestDate = new DateTime(2026, 11, 18), RequiredDate = new DateTime(2026, 11, 20), Status = "Converted", RequestedByEmployeeId = emp },
                    new PurchaseRequest { Id = empty, RequestNumber = "DN-DPB3-RONG", RequestDate = new DateTime(2026, 11, 18), Status = "Draft" });
                db.PurchaseRequestLine.AddRange(
                    new PurchaseRequestLine { Id = Guid.NewGuid(), PurchaseRequestId = pr, IngredientId = b.Ing, Quantity = 10, UnitId = b.Kg, UomId = b.Kg },
                    new PurchaseRequestLine { Id = Guid.NewGuid(), PurchaseRequestId = pr, IngredientId = ing2, Quantity = 4, UnitId = b.Kg, UomId = b.Kg });
                db.PurchaseOrder.Add(new PurchaseOrder { Id = Guid.NewGuid(), PurchaseOrderNumber = "PO-DPB3", SupplierId = b.Sup, PurchaseRequestId = pr, OrderDate = new DateTime(2026, 11, 18), Status = "Draft" });
                return Task.CompletedTask;
            });
            using var admin = factory.CreateAuthorizedClient();
            var o = (await admin.GetResultAsync($"/api/PurchaseRequest/GetOverview?id={pr}")).Ok();
            o.GetProperty("detail").GetProperty("purchaseRequest").GetProperty("requestNumber").GetString().Should().Be("DN-DPB3");
            o.GetProperty("requesterName").GetString().Should().Be("Bếp trưởng DPB3");
            o.GetProperty("lineCount").GetInt32().Should().Be(2);
            o.GetProperty("supplierCount").GetInt32().Should().Be(1);
            o.GetProperty("noSupplierLineCount").GetInt32().Should().Be(1, "rau chưa có bảng giá");
            o.GetProperty("purchaseOrderCount").GetInt32().Should().Be(1);

            var e = (await admin.GetResultAsync($"/api/PurchaseRequest/GetOverview?id={empty}")).Ok();
            e.GetProperty("lineCount").GetInt32().Should().Be(0);
            e.GetProperty("requesterName").ValueKind.Should().Be(JsonValueKind.Null);
            (await admin.GetResultAsync($"/api/PurchaseRequest/GetOverview?id={Guid.NewGuid()}")).Fail("Không tìm thấy");

            await PermissionAsync($"/api/PurchaseRequest/GetOverview?id={pr}", "scm-pr", "scm-gr");
        }

        [Fact]
        public async Task MaterialLot_overview_source_balance_and_transactions()
        {
            var b = await BaseAsync("DPB-LOT");
            Guid lot = Guid.NewGuid(), bare = Guid.NewGuid(), po = Guid.NewGuid(), gr = Guid.NewGuid(), gl = Guid.NewGuid();
            await factory.SeedAsync(db =>
            {
                db.PurchaseOrder.Add(new PurchaseOrder { Id = po, PurchaseOrderNumber = "PO-DPB4", SupplierId = b.Sup, OrderDate = new DateTime(2026, 11, 10), Status = "Received" });
                db.GoodsReceipt.Add(new GoodsReceipt { Id = gr, ReceiptNumber = "PN-DPB4", PurchaseOrderId = po, SupplierId = b.Sup, WarehouseId = b.Wh, ReceiptDate = new DateTime(2026, 11, 11), Status = "Posted" });
                db.GoodsReceiptLine.Add(new GoodsReceiptLine { Id = gl, GoodsReceiptId = gr, IngredientId = b.Ing, UnitId = b.Kg, UomId = b.Kg, Quantity = 20, ReceivedQuantity = 20, AcceptedQuantity = 20, LotId = lot });
                db.MaterialLot.AddRange(
                    new MaterialLot { Id = lot, LotNumber = "NL-DPB4", LotNo = "NL-DPB4", IngredientId = b.Ing, SupplierId = b.Sup, GoodsReceiptLineId = gl, ReceivedDate = new DateTime(2026, 11, 11), ExpiryDate = new DateTime(2099, 1, 1), Status = "Available", QcStatus = "DAT" },
                    new MaterialLot { Id = bare, LotNumber = "NL-DPB4-RONG", LotNo = "NL-DPB4-RONG", IngredientId = b.Ing, Status = "Available" });
                db.InventoryBalance.Add(new InventoryBalance { Id = Guid.NewGuid(), WarehouseId = b.Wh, IngredientId = b.Ing, MaterialLotId = lot, LotId = lot, UnitId = b.Kg, OnHandQuantity = 12, AvailableQuantity = 10, ReservedQuantity = 2 });
                db.InventoryTransaction.AddRange(
                    new InventoryTransaction { Id = Guid.NewGuid(), TransactionNumber = "GD-DPB4-1", TransactionType = "RECEIPT", WarehouseId = b.Wh, IngredientId = b.Ing, MaterialLotId = lot, UnitId = b.Kg, UomId = b.Kg, QuantityIn = 20, Quantity = 20, TransactionDate = new DateTime(2026, 11, 11) },
                    new InventoryTransaction { Id = Guid.NewGuid(), TransactionNumber = "GD-DPB4-2", TransactionType = "ISSUE", WarehouseId = b.Wh, IngredientId = b.Ing, MaterialLotId = lot, UnitId = b.Kg, UomId = b.Kg, QuantityOut = 8, Quantity = -8, TransactionDate = new DateTime(2026, 11, 12) });
                return Task.CompletedTask;
            });
            using var admin = factory.CreateAuthorizedClient();
            var o = (await admin.GetResultAsync($"/api/MaterialLot/GetOverview?id={lot}")).Ok();
            o.GetProperty("lot").GetProperty("lotNumber").GetString().Should().Be("NL-DPB4");
            o.GetProperty("ingredientName").GetString().Should().Be("Thịt DPB-LOT");
            o.GetProperty("supplierName").GetString().Should().Be("NCC DPB-LOT");
            o.GetProperty("goodsReceiptNumber").GetString().Should().Be("PN-DPB4");
            o.GetProperty("purchaseOrderNumber").GetString().Should().Be("PO-DPB4");
            o.GetProperty("onHandQuantity").GetDecimal().Should().Be(12);
            o.GetProperty("availableQuantity").GetDecimal().Should().Be(10);
            o.GetProperty("issuedQuantity").GetDecimal().Should().Be(8);
            o.GetProperty("blockReason").ValueKind.Should().Be(JsonValueKind.Null);
            o.GetProperty("listTransaction").GetArrayLength().Should().Be(2);
            o.GetProperty("listTransaction")[0].GetProperty("transactionNumber").GetString().Should().Be("GD-DPB4-2", "mới nhất trước");
            o.GetProperty("listBalance")[0].GetProperty("warehouseName").GetString().Should().Be("Kho DPB-LOT");

            var e = (await admin.GetResultAsync($"/api/MaterialLot/GetOverview?id={bare}")).Ok();
            e.GetProperty("listTransaction").GetArrayLength().Should().Be(0, "lô chưa có giao dịch vẫn mở được trang");
            e.GetProperty("onHandQuantity").GetDecimal().Should().Be(0);
            (await admin.GetResultAsync($"/api/MaterialLot/GetOverview?id={Guid.NewGuid()}")).Fail("Không tìm thấy");

            await PermissionAsync($"/api/MaterialLot/GetOverview?id={lot}", "scm-inv-balance", "scm-po"); // UI-6.29 (D-110): Lô gộp vào Tồn kho
        }

        [Fact]
        public async Task Ingredient_overview_stock_lots_last_price_and_suppliers()
        {
            var b = await BaseAsync("DPB-ING");
            Guid lot = Guid.NewGuid(), bareIng = Guid.NewGuid(), po1 = Guid.NewGuid(), po2 = Guid.NewGuid(), draft = Guid.NewGuid();
            await factory.SeedAsync(db =>
            {
                db.Ingredient.Add(new Ingredient { Id = bareIng, Code = "NL-DPB5-RONG", Name = "Muối DPB5", BaseUnitId = b.Kg });
                db.MaterialLot.Add(new MaterialLot { Id = lot, LotNumber = "NL-DPB5", LotNo = "NL-DPB5", IngredientId = b.Ing, SupplierId = b.Sup, ExpiryDate = DateTime.Today.AddDays(1), Status = "Available" });
                db.InventoryBalance.Add(new InventoryBalance { Id = Guid.NewGuid(), WarehouseId = b.Wh, IngredientId = b.Ing, MaterialLotId = lot, UnitId = b.Kg, OnHandQuantity = 30, AvailableQuantity = 30 });
                db.InventoryTransaction.Add(new InventoryTransaction { Id = Guid.NewGuid(), TransactionNumber = "GD-DPB5", TransactionType = "RECEIPT", WarehouseId = b.Wh, IngredientId = b.Ing, MaterialLotId = lot, UnitId = b.Kg, UomId = b.Kg, QuantityIn = 30, Quantity = 30, TransactionDate = new DateTime(2026, 11, 15) });
                db.SupplierMaterial.Add(new SupplierMaterial { Id = Guid.NewGuid(), SupplierId = b.Sup, IngredientId = b.Ing, UnitPrice = 21_000, IsPreferred = true, Status = "Active" });
                db.PurchaseOrder.AddRange(
                    new PurchaseOrder { Id = po1, PurchaseOrderNumber = "PO-DPB5-1", SupplierId = b.Sup, OrderDate = new DateTime(2026, 11, 1), Status = "Closed" },
                    new PurchaseOrder { Id = po2, PurchaseOrderNumber = "PO-DPB5-2", SupplierId = b.Sup, OrderDate = new DateTime(2026, 11, 15), Status = "Received" },
                    new PurchaseOrder { Id = draft, PurchaseOrderNumber = "PO-DPB5-NHAP", SupplierId = b.Sup, OrderDate = new DateTime(2026, 11, 20), Status = "Draft" });
                db.PurchaseOrderLine.AddRange(
                    new PurchaseOrderLine { Id = Guid.NewGuid(), PurchaseOrderId = po1, IngredientId = b.Ing, Quantity = 10, UnitId = b.Kg, UomId = b.Kg, UnitPrice = 19_000, LineAmount = 190_000 },
                    new PurchaseOrderLine { Id = Guid.NewGuid(), PurchaseOrderId = po2, IngredientId = b.Ing, Quantity = 10, UnitId = b.Kg, UomId = b.Kg, UnitPrice = 20_500, LineAmount = 205_000 },
                    new PurchaseOrderLine { Id = Guid.NewGuid(), PurchaseOrderId = draft, IngredientId = b.Ing, Quantity = 10, UnitId = b.Kg, UomId = b.Kg, UnitPrice = 99_000, LineAmount = 990_000 });
                return Task.CompletedTask;
            });
            using var admin = factory.CreateAuthorizedClient();
            var o = (await admin.GetResultAsync($"/api/Ingredient/GetOverview?id={b.Ing}")).Ok();
            o.GetProperty("ingredient").GetProperty("code").GetString().Should().Be("NL-DPB-ING");
            o.GetProperty("baseUnitName").GetString().Should().Be("kg");
            o.GetProperty("availableQuantity").GetDecimal().Should().Be(30);
            o.GetProperty("belowMinimum").GetBoolean().Should().BeTrue("30 < tối thiểu 50");
            o.GetProperty("activeLotCount").GetInt32().Should().Be(1);
            o.GetProperty("expiringLotCount").GetInt32().Should().Be(1, "hết hạn trong 3 ngày");
            o.GetProperty("lastPurchasePrice").GetDecimal().Should().Be(20_500, "PO nháp không tính");
            o.GetProperty("lastPurchaseOrderNumber").GetString().Should().Be("PO-DPB5-2");
            o.GetProperty("preferredSupplierName").GetString().Should().Be("NCC DPB-ING");
            o.GetProperty("supplierCount").GetInt32().Should().Be(1);
            o.GetProperty("listPriceHistory").GetArrayLength().Should().Be(2);
            o.GetProperty("listTransaction").GetArrayLength().Should().Be(1);
            o.GetProperty("listLot")[0].GetProperty("lotNumber").GetString().Should().Be("NL-DPB5");

            var e = (await admin.GetResultAsync($"/api/Ingredient/GetOverview?id={bareIng}")).Ok();
            e.GetProperty("listLot").GetArrayLength().Should().Be(0, "nguyên liệu chưa nhập vẫn mở được trang");
            e.GetProperty("lastPurchasePrice").ValueKind.Should().Be(JsonValueKind.Null);
            (await admin.GetResultAsync($"/api/Ingredient/GetOverview?id={Guid.NewGuid()}")).Fail("Không tìm thấy");

            await PermissionAsync($"/api/Ingredient/GetOverview?id={b.Ing}", "scm-ingredient", "scm-stockcount");
        }
    }
}
