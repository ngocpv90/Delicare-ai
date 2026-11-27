using System.Net.Http.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Purchasing
{
    /// <summary>
    /// UI-6.50 (D-132): thẻ NCC hẹn giao; gói nhận gộp (tìm theo số đơn / số hóa đơn NCC); CreateMany chia số nhận đơn cũ trước, mỗi đơn 1 phiếu, 1 giao dịch
    /// (lỗi → không lập phiếu nào), lô / hạn chỉ khi quản lý lô, nhiệt độ thùng xe, "[HĐ NCC]" trong ghi chú, tự đạt → ghi sổ (UI-6.29), cần QA → chờ QA;
    /// kết luận ngay: Đạt một phần trừ đơn mới nhất trước; Excel phiếu giao NCC tải → điền → đọc; in đơn mua có QR; chứng từ AMIS mỗi phiếu 1 đơn.
    /// </summary>
    public class ReceivingUi650Tests : IClassFixture<InMemoryApiFactory>
    {
        private static readonly DateTime Today = new(2034, 6, 14);
        private readonly InMemoryApiFactory _factory;

        public ReceivingUi650Tests(InMemoryApiFactory factory)
        {
            _factory = factory;
            _factory.Clock.FixedNow = Today.AddHours(9);
            _factory.Settings["BusinessRules:GoodsReceiptAutoPostAfterQc"] = "true";
        }

        private static async Task<LegacyResult> PostJson(HttpClient c, string url, object body) => await LegacyResult.ReadAsync(await c.PostAsJsonAsync(url, body));

        [Fact]
        public async Task Supplier_cards_bundle_and_create_many_split_oldest_first()
        {
            Guid wh = Guid.NewGuid(), kg = Guid.NewGuid(), sup = Guid.NewGuid(), sup2 = Guid.NewGuid();
            Guid rice = Guid.NewGuid(), pork = Guid.NewGuid(), fish = Guid.NewGuid(), veg = Guid.NewGuid();
            Guid poA = Guid.NewGuid(), poB = Guid.NewGuid(), poC = Guid.NewGuid(), poX = Guid.NewGuid();
            Guid aRice = Guid.NewGuid(), aPork = Guid.NewGuid(), aVeg = Guid.NewGuid(), bRice = Guid.NewGuid(), bFish = Guid.NewGuid(), bVeg = Guid.NewGuid(), cRice = Guid.NewGuid(), xRice = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Warehouse.Add(new Warehouse { Id = wh, Code = "K650", Name = "Kho 650", Status = "Active" });
                db.UnitMeasure.Add(new UnitMeasure { Id = kg, Code = "KG-650", Name = "kg" });
                db.Supplier.AddRange(new Supplier { Id = sup, Code = "S650", Name = "NCC 650", Status = "Active" }, new Supplier { Id = sup2, Code = "S650X", Name = "NCC khác 650", Status = "Active" });
                db.Ingredient.AddRange(
                    new Ingredient { Id = rice, Code = "GAO-650", Name = "Gạo 650", BaseUnitId = kg, StorageType = "DRY", QcRequired = false, LotControl = false },
                    new Ingredient { Id = pork, Code = "THIT-650", Name = "Thịt 650", BaseUnitId = kg, StorageType = "CHILLED", QcRequired = false, LotControl = true, ExpiryControl = true, ShelfLifeDays = 5 },
                    new Ingredient { Id = fish, Code = "CA-650", Name = "Cá 650", BaseUnitId = kg, StorageType = "CHILLED", QcRequired = true },
                    new Ingredient { Id = veg, Code = "RAU-650", Name = "Rau 650", BaseUnitId = kg, StorageType = "DRY", QcRequired = true });
                db.SupplierMaterial.Add(new SupplierMaterial { Id = Guid.NewGuid(), SupplierId = sup, IngredientId = rice, SupplierMaterialCode = "NCC-GAO", Status = "Active" });
                db.PurchaseOrder.AddRange(
                    new PurchaseOrder { Id = poA, PurchaseOrderNumber = "PO-650A", PONo = "PO-650A", SupplierId = sup, OrderDate = Today.AddDays(-3), ExpectedDeliveryDate = Today.AddDays(-1), Status = "Approved" },
                    new PurchaseOrder { Id = poB, PurchaseOrderNumber = "PO-650B", PONo = "PO-650B", SupplierId = sup, OrderDate = Today.AddDays(-1), ExpectedDeliveryDate = Today.AddHours(8), Status = "Sent" },
                    new PurchaseOrder { Id = poC, PurchaseOrderNumber = "PO-650C", PONo = "PO-650C", SupplierId = sup, OrderDate = Today, ExpectedDeliveryDate = Today.AddDays(3), Status = "Approved" },
                    new PurchaseOrder { Id = poX, PurchaseOrderNumber = "PO-650X", PONo = "PO-650X", SupplierId = sup2, OrderDate = Today, ExpectedDeliveryDate = Today, Status = "Approved" });
                PurchaseOrderLine L(Guid id, Guid po, Guid ing, decimal q) => new() { Id = id, PurchaseOrderId = po, IngredientId = ing, Quantity = q, UnitId = kg, UomId = kg, UnitPrice = 10_000, LineAmount = q * 10_000, Amount = q * 10_000 };
                db.PurchaseOrderLine.AddRange(L(aRice, poA, rice, 10), L(aPork, poA, pork, 4), L(aVeg, poA, veg, 5), L(bRice, poB, rice, 6), L(bFish, poB, fish, 3), L(bVeg, poB, veg, 5),
                    L(cRice, poC, rice, 2), L(xRice, poX, rice, 1));
                return Task.CompletedTask;
            });
            using var c = _factory.CreateAuthorizedClient();

            // Thẻ NCC: gom PO-A + PO-B (PO-C hẹn sau), quá hẹn.
            var due = (await c.GetResultAsync($"/api/PurchaseOrder/GetDueBySupplier?date={Today:yyyy-MM-dd}")).Ok();
            var card = due.GetProperty("listSupplier").EnumerateArray().Single(s => s.GetProperty("supplierId").GetGuid() == sup);
            (card.GetProperty("orderCount").GetInt32(), card.GetProperty("lineCount").GetInt32()).Should().Be((2, 6));
            card.GetProperty("overdueMinutes").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Number);
            due.GetProperty("supplierCount").GetInt32().Should().BeGreaterThanOrEqualTo(2);

            // Gói nhận: tìm theo số đơn → gom mọi đơn còn mở của NCC; dòng gạo = 3 đơn, đơn cũ trước.
            var bundle = (await c.GetResultAsync("/api/PurchaseOrder/GetReceivingBundle?keyword=PO-650B")).Ok();
            bundle.GetProperty("supplierId").GetGuid().Should().Be(sup);
            bundle.GetProperty("matchedBy").GetString().Should().Be("order");
            var orders = bundle.GetProperty("listOrder").EnumerateArray().ToList();
            orders.Select(o => o.GetProperty("purchaseOrderNumber").GetString()).Should().Equal("PO-650A", "PO-650B", "PO-650C");
            orders.Single(o => o.GetProperty("purchaseOrderNumber").GetString() == "PO-650C").GetProperty("due").GetBoolean().Should().BeFalse();
            var riceLine = bundle.GetProperty("listLine").EnumerateArray().Single(l => l.GetProperty("ingredientId").GetGuid() == rice);
            riceLine.GetProperty("remaining").GetDecimal().Should().Be(18);
            riceLine.GetProperty("supplierMaterialCode").GetString().Should().Be("NCC-GAO");
            riceLine.GetProperty("listPart").EnumerateArray().Select(p => p.GetProperty("purchaseOrderLineId").GetGuid()).Should().Equal(aRice, bRice, cRice);
            bundle.GetProperty("listLine").EnumerateArray().Single(l => l.GetProperty("ingredientId").GetGuid() == pork).GetProperty("lotControl").GetBoolean().Should().BeTrue();
            bundle.GetProperty("needsVehicleTemperature").GetBoolean().Should().BeTrue();

            // Nhận vượt → báo lỗi, không lập phiếu nào (1 giao dịch).
            (await PostJson(c, "/api/GoodsReceipt/CreateMany", new
            {
                warehouseId = wh, listLine = new object[]
                {
                    new { ingredientId = pork, listPurchaseOrderLineId = new[] { aPork }, receivedQuantity = 4m },
                    new { ingredientId = rice, listPurchaseOrderLineId = new[] { aRice, bRice }, receivedQuantity = 100m },
                },
            })).Fail("vượt");
            (await _factory.QueryAsync(db => db.GoodsReceipt.CountAsync(r => r.SupplierId == sup))).Should().Be(0);

            // Nhận gộp: gạo 14 → PO-A 10 + PO-B 4; thịt lô L1 nhiệt độ theo xe 3 °C; cá cần QC → chờ QA.
            var many = (await PostJson(c, "/api/GoodsReceipt/CreateMany", new
            {
                warehouseId = wh, vehicleTemperatureC = 3m, supplierInvoiceNo = "HD-650",
                listLine = new object[]
                {
                    new { ingredientId = rice, listPurchaseOrderLineId = new[] { aRice, bRice }, receivedQuantity = 14m, supplierLotNumber = "KHONG-LUU" },
                    new { ingredientId = pork, listPurchaseOrderLineId = new[] { aPork }, receivedQuantity = 4m, supplierLotNumber = "L1" },
                    new { ingredientId = fish, listPurchaseOrderLineId = new[] { bFish }, receivedQuantity = 3m },
                },
            })).Ok();
            many.GetProperty("listReceipt").GetArrayLength().Should().Be(2);
            (many.GetProperty("postedCount").GetInt32(), many.GetProperty("qcPendingCount").GetInt32()).Should().Be((1, 1));
            var receipts = await _factory.QueryAsync(db => db.GoodsReceipt.AsNoTracking().Where(r => r.SupplierId == sup).ToListAsync());
            receipts.Should().OnlyContain(r => r.Note!.Contains("[HĐ NCC] HD-650") && r.Note.Contains("Nhận gộp 2 đơn"));
            var grLines = await _factory.QueryAsync(db => db.GoodsReceiptLine.AsNoTracking().Where(l => receipts.Select(r => r.Id).Contains(l.GoodsReceiptId)).ToListAsync());
            grLines.Where(l => l.IngredientId == rice).Select(l => (receipts.Single(r => r.Id == l.GoodsReceiptId).PurchaseOrderId, l.ReceivedQuantity)).Should().BeEquivalentTo(new[] { ((Guid?)poA, 10m), ((Guid?)poB, 4m) });
            grLines.Where(l => l.IngredientId == rice).Should().OnlyContain(l => l.LotNumber == null, "gạo không quản lý lô");
            grLines.Single(l => l.IngredientId == pork).LotNumber.Should().Be("L1");
            var porkLineId = grLines.Single(l => l.IngredientId == pork).Id;
            (await _factory.QueryAsync(db => db.QcInspectionLine.AsNoTracking().Where(q => q.GoodsReceiptLineId == porkLineId).Select(q => q.Temperature).SingleAsync())).Should().Be(3m);
            receipts.Single(r => r.PurchaseOrderId == poA).Status.Should().Be("Posted");
            receipts.Single(r => r.PurchaseOrderId == poB).Status.Should().Be("QCPending");

            // Tìm theo số hóa đơn NCC.
            (await c.GetResultAsync("/api/PurchaseOrder/GetReceivingBundle?keyword=HD-650")).Ok().GetProperty("matchedBy").GetString().Should().Be("invoice");

            // QA tại cửa kho: rau 10 (PO-A 5 + PO-B 5) Đạt một phần 7 → không đạt 3 trừ PO-B (đơn mới nhất) trước.
            var concluded = (await PostJson(c, "/api/GoodsReceipt/CreateMany", new
            {
                warehouseId = wh, conclude = true,
                listLine = new object[] { new { ingredientId = veg, listPurchaseOrderLineId = new[] { aVeg, bVeg }, receivedQuantity = 10m, result = "DAT_MOT_PHAN", acceptedQuantity = 7m, remark = "Rau dập 3 kg" } },
            })).Ok();
            concluded.GetProperty("listReceipt").GetArrayLength().Should().Be(2);
            var vegLines = await _factory.QueryAsync(db => (from l in db.GoodsReceiptLine.AsNoTracking()
                                                            join r in db.GoodsReceipt.AsNoTracking() on l.GoodsReceiptId equals r.Id
                                                            where l.IngredientId == veg
                                                            select new { r.PurchaseOrderId, l.AcceptedQuantity, l.QualityStatus, r.Status }).ToListAsync());
            vegLines.Single(x => x.PurchaseOrderId == poA).Should().BeEquivalentTo(new { PurchaseOrderId = (Guid?)poA, AcceptedQuantity = 5m, QualityStatus = "DAT", Status = "Posted" });
            vegLines.Single(x => x.PurchaseOrderId == poB).Should().BeEquivalentTo(new { PurchaseOrderId = (Guid?)poB, AcceptedQuantity = 2m, QualityStatus = "DAT_MOT_PHAN", Status = "Posted" });
            (await _factory.QueryAsync(db => db.SupplierIssue.CountAsync(i => i.SupplierId == sup))).Should().Be(1);

            // AMIS (D-131): mỗi phiếu nhập đã ghi sổ = 1 chứng từ nhập mua gắn đúng 1 đơn mua.
            _factory.Clock.FixedNow = Today.AddDays(20);
            var vouchers = (await c.GetResultAsync("/api/AccountingVoucher/GetMonth?month=2034-06&tab=purchase")).Ok().GetProperty("listVoucher").EnumerateArray()
                .Where(v => v.GetProperty("type").GetString() == "PURCHASE" && v.GetProperty("objectCode").GetString() == "S650").ToList();
            var posted = await _factory.QueryAsync(db => db.GoodsReceipt.AsNoTracking().CountAsync(r => r.SupplierId == sup && r.Status == "Posted"));
            vouchers.Should().HaveCount(posted);
            _factory.Clock.FixedNow = Today.AddHours(9);

            // Excel phiếu giao NCC: tải mẫu → NCC điền số giao / lô → đọc khớp dòng đơn mua (không lưu tệp).
            var bytes = await (await c.GetAsync($"/api/PurchaseOrder/DeliveryNoteTemplate?supplierId={sup}")).Content.ReadAsByteArrayAsync();
            using (var ms = new MemoryStream(bytes))
            using (var wb = new XLWorkbook(ms))
            {
                var ws = wb.Worksheets.First();
                var row = ws.RowsUsed().First(r => r.Cell(1).GetString() == "PO-650C");
                row.Cell(2).GetString().Should().Be("NCC-GAO");
                row.Cell(6).Value = 2;
                row.Cell(7).Value = "LO-C";
                using var outMs = new MemoryStream();
                wb.SaveAs(outMs);
                bytes = outMs.ToArray();
            }
            using var form = new MultipartFormDataContent { { new ByteArrayContent(bytes), "file", "giao.xlsx" }, { new StringContent(sup.ToString()), "supplierId" } };
            var parsed = (await LegacyResult.ReadAsync(await c.PostAsync("/api/PurchaseOrder/ParseDeliveryNote", form))).Ok().EnumerateArray().ToList();
            var cRow = parsed.Single(r => r.GetProperty("purchaseOrderNumber").GetString() == "PO-650C");
            (cRow.GetProperty("purchaseOrderLineId").GetGuid(), cRow.GetProperty("quantity").GetDecimal(), cRow.GetProperty("supplierLotNumber").GetString()).Should().Be((cRice, 2m, "LO-C"));

            // In đơn mua (PDF) có QR / link bot DOC_PO_.
            var pdf = await c.GetAsync($"/api/PurchaseOrder/Print?id={poC}");
            pdf.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
            var doc = (await c.GetResultAsync($"/api/Common/GetPrintDocument?type=PurchaseOrder&id={poC}")).Ok();
            doc.GetProperty("title").GetString().Should().Be("ĐƠN ĐẶT HÀNG");
        }
    }
}
