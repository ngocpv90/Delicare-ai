using System.Net.Http.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Purchasing
{
    /// <summary>
    /// UI-6.29 (D-110): phiếu nhận mọi dòng tự đạt → tự ghi sổ ngay; hồ sơ kiểm tra hàng nhập không sửa ghi đè; Sổ kiểm tra hàng nhập (lọc, vùng nhiệt độ, lô);
    /// tổng hợp theo nhà cung cấp; đơn mua đến hạn nhận (quyền Xem "Nhận hàng").
    /// </summary>
    public class KhoQcBookUi629Tests : IClassFixture<InMemoryApiFactory>
    {
        private static readonly DateTime Today = new(2034, 2, 14);
        private readonly InMemoryApiFactory _factory;

        public KhoQcBookUi629Tests(InMemoryApiFactory factory)
        {
            _factory = factory;
            _factory.Clock.FixedNow = Today.AddHours(9);
            _factory.Settings["BusinessRules:GoodsReceiptAutoPostAfterQc"] = "true";
        }

        private static async Task<LegacyResult> PostJson(HttpClient c, string url, object body) => await LegacyResult.ReadAsync(await c.PostAsJsonAsync(url, body));

        [Fact]
        public async Task Auto_pass_receipt_posts_book_lists_lines_and_due_orders()
        {
            Guid wh = Guid.NewGuid(), kg = Guid.NewGuid(), sup = Guid.NewGuid(), poAuto = Guid.NewGuid(), poQc = Guid.NewGuid(), poLater = Guid.NewGuid();
            Guid rice = Guid.NewGuid(), pork = Guid.NewGuid(), lRice = Guid.NewGuid(), lPork = Guid.NewGuid(), lLater = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                db.Warehouse.Add(new Warehouse { Id = wh, Code = "K629", Name = "Kho 629", Status = "Active" });
                db.UnitMeasure.Add(new UnitMeasure { Id = kg, Code = "KG-629", Name = "kg" });
                db.Supplier.Add(new Supplier { Id = sup, Code = "S629", Name = "NCC 629", Status = "Active" });
                db.Ingredient.AddRange(new Ingredient { Id = rice, Code = "GAO-629", Name = "Gạo 629", BaseUnitId = kg, StorageType = "DRY", QcRequired = false },
                    new Ingredient { Id = pork, Code = "THIT-629", Name = "Thịt 629", BaseUnitId = kg, StorageType = "CHILLED", QcRequired = true });
                db.PurchaseOrder.AddRange(
                    new PurchaseOrder { Id = poAuto, PurchaseOrderNumber = "PO-629A", PONo = "PO-629A", SupplierId = sup, OrderDate = Today.AddDays(-3), ExpectedDeliveryDate = Today.AddDays(-1), Status = "Approved", TotalAmount = 100_000 },
                    new PurchaseOrder { Id = poQc, PurchaseOrderNumber = "PO-629Q", PONo = "PO-629Q", SupplierId = sup, OrderDate = Today.AddDays(-1), ExpectedDeliveryDate = Today, Status = "Sent", TotalAmount = 200_000 },
                    new PurchaseOrder { Id = poLater, PurchaseOrderNumber = "PO-629L", PONo = "PO-629L", SupplierId = sup, OrderDate = Today, ExpectedDeliveryDate = Today.AddDays(3), Status = "Approved", TotalAmount = 50_000 });
                db.PurchaseOrderLine.AddRange(
                    new PurchaseOrderLine { Id = lRice, PurchaseOrderId = poAuto, IngredientId = rice, Quantity = 10, UnitId = kg, UomId = kg, UnitPrice = 10_000, LineAmount = 100_000, Amount = 100_000 },
                    new PurchaseOrderLine { Id = lPork, PurchaseOrderId = poQc, IngredientId = pork, Quantity = 2, UnitId = kg, UomId = kg, UnitPrice = 100_000, LineAmount = 200_000, Amount = 200_000 },
                    new PurchaseOrderLine { Id = lLater, PurchaseOrderId = poLater, IngredientId = rice, Quantity = 5, UnitId = kg, UomId = kg, UnitPrice = 10_000, LineAmount = 50_000, Amount = 50_000 });
                return Task.CompletedTask;
            });
            using var admin = _factory.CreateAuthorizedClient();

            // Đơn đến hạn: quá hạn lên đầu, đơn hẹn sau hôm nay không hiện.
            var due = (await admin.GetResultAsync($"/api/PurchaseOrder/GetDueForReceiving?date={Today:yyyy-MM-dd}")).Ok().EnumerateArray()
                .Where(d => d.GetProperty("supplierId").GetGuid() == sup).ToList();
            due.Select(d => d.GetProperty("purchaseOrderNumber").GetString()).Should().Equal("PO-629A", "PO-629Q");
            due[0].GetProperty("overdue").GetBoolean().Should().BeTrue();

            // Mọi dòng tự đạt (gạo khô, không yêu cầu QC) → tự ghi sổ, lập lô, đơn mua nhận đủ.
            var auto = await PostJson(admin, "/api/GoodsReceipt/Create", new { purchaseOrderId = poAuto, warehouseId = wh, listLine = new[] { new { purchaseOrderLineId = lRice, receivedQuantity = 10m, expiryDate = Today.AddDays(180) } } });
            auto.Message.Should().Contain("đã nhập kho");
            auto.Ok().GetProperty("goodsReceipt").GetProperty("status").GetString().Should().Be("Posted");
            (await _factory.QueryAsync(db => db.MaterialLot.AsNoTracking().CountAsync(l => l.IngredientId == rice))).Should().Be(1);

            // Thịt cần QC, nhiệt độ 7 °C (vùng có điều kiện) → chờ kết luận, chưa ghi sổ.
            var qc = (await PostJson(admin, "/api/GoodsReceipt/Create", new { purchaseOrderId = poQc, warehouseId = wh, listLine = new[] { new { purchaseOrderLineId = lPork, receivedQuantity = 2m, expiryDate = Today.AddDays(5), temperatureC = 7m } } })).Ok();
            qc.GetProperty("goodsReceipt").GetProperty("status").GetString().Should().Be("QCPending");
            var inspectionId = await _factory.QueryAsync(db => db.QcInspection.AsNoTracking().Where(i => i.GoodsReceiptId == qc.GetProperty("goodsReceipt").GetProperty("id").GetGuid()).Select(i => i.Id).SingleAsync());
            (await admin.PutFormAsync("/api/QcInspection/Update", ("Id", inspectionId), ("Note", "sửa"))).Fail("không sửa được");

            var book = (await admin.GetResultAsync($"/api/QcInspection/GetBook?From={Today:yyyy-MM-dd}&To={Today:yyyy-MM-dd}&SupplierId={sup}")).Ok();
            var sum = book.GetProperty("summary");
            (sum.GetProperty("lines").GetInt32(), sum.GetProperty("receipts").GetInt32(), sum.GetProperty("dat").GetInt32(), sum.GetProperty("pending").GetInt32(), sum.GetProperty("tempOut").GetInt32())
                .Should().Be((2, 2, 1, 1, 1));
            var rows = book.GetProperty("listDay")[0].GetProperty("listRow").EnumerateArray().ToList();
            var riceRow = rows.Single(r => r.GetProperty("ingredientId").GetGuid() == rice);
            riceRow.GetProperty("autoPass").GetBoolean().Should().BeTrue();
            riceRow.GetProperty("tempZone").GetString().Should().Be("NOT_CHECKED");
            riceRow.GetProperty("lotId").ValueKind.Should().Be(System.Text.Json.JsonValueKind.String, "dòng đã ghi sổ có lô");
            rows.Single(r => r.GetProperty("ingredientId").GetGuid() == pork).GetProperty("tempZone").GetString().Should().Be("CONDITIONAL");

            var outOnly = (await admin.GetResultAsync($"/api/QcInspection/GetBook?From={Today:yyyy-MM-dd}&To={Today:yyyy-MM-dd}&SupplierId={sup}&OnlyTempOut=true")).Ok();
            outOnly.GetProperty("summary").GetProperty("lines").GetInt32().Should().Be(1);
            (await admin.GetResultAsync($"/api/QcInspection/GetBook?From={Today:yyyy-MM-dd}&To={Today:yyyy-MM-dd}&SupplierId={sup}&Result=PENDING&Keyword=Thịt")).Ok()
                .GetProperty("summary").GetProperty("lines").GetInt32().Should().Be(1);

            var bySup = (await admin.GetResultAsync($"/api/QcInspection/GetSupplierSummary?SupplierId={sup}")).Ok().EnumerateArray().Single();
            (bySup.GetProperty("lines").GetInt32(), bySup.GetProperty("dat").GetInt32(), bySup.GetProperty("tempOut").GetInt32()).Should().Be((2, 1, 1));
            bySup.GetProperty("passPct").GetDecimal().Should().Be(100m, "chỉ tính dòng đã kết luận");

            // Đơn đã nhận đủ (đã ghi sổ hoặc đang chờ kết luận QC) rời khối "đến hạn".
            (await admin.GetResultAsync($"/api/PurchaseOrder/GetDueForReceiving?date={Today:yyyy-MM-dd}")).Ok().EnumerateArray()
                .Where(d => d.GetProperty("supplierId").GetGuid() == sup).Should().BeEmpty();
        }
    }
}
