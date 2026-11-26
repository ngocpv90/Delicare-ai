using System.Net.Http.Json;
using System.Text.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Accounting
{
    /// <summary>
    /// ACC-2 (D-131 d, e): Chứng từ kế toán tính khi đọc – nhập mua (số đạt × giá), xuất NVL theo phiếu (ca → buổi → mã thành phẩm), nhập kho / xuất thành phẩm khác
    /// theo ngày × buổi (suất bù đơn giá 0), bảng công; trạng thái: Lỗi (chưa nhận danh mục / thiếu mã AMIS) → Sẵn sàng → tải Excel = Chờ lập → đánh dấu = Đã lập
    /// → nghiệp vụ gốc đổi = Đã sửa – cần điều chỉnh.
    /// </summary>
    public class AccountingVoucherTests
    {
        private static readonly DateTime Day = new(2033, 3, 10);
        private const string Month = "2033-03";

        private static byte[] Catalog(string sheet, string[] head, params string[][] rows)
        {
            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet(sheet);
            for (int c = 0; c < head.Length; c++) ws.Cell(1, c + 1).Value = head[c];
            for (int r = 0; r < rows.Length; r++) for (int c = 0; c < rows[r].Length; c++) ws.Cell(r + 2, c + 1).Value = rows[r][c];
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        private static async Task ImportAsync(HttpClient c, byte[] file)
        {
            using var form = new MultipartFormDataContent { { new ByteArrayContent(file), "file", "amis.xlsx" } };
            (await LegacyResult.ReadAsync(await c.PostAsync("/api/AmisCatalog/ImportExcel", form))).Ok();
        }

        private static async Task<List<JsonElement>> MonthAsync(HttpClient c, string tab = "all")
            => [.. (await c.GetResultAsync($"/api/AccountingVoucher/GetMonth?month={Month}&tab={tab}")).Ok().GetProperty("listVoucher").EnumerateArray()];

        private static JsonElement One(List<JsonElement> list, string type) => list.Single(v => v.GetProperty("type").GetString() == type);

        [Fact]
        public async Task Vouchers_are_built_from_finished_business_and_track_sending_status()
        {
            using var f = new InMemoryApiFactory();
            Guid unit = Guid.NewGuid(), ing = Guid.NewGuid(), sup = Guid.NewGuid(), wh = Guid.NewGuid(), gr = Guid.NewGuid(), grl = Guid.NewGuid(), shift = Guid.NewGuid(),
                po = Guid.NewGuid(), mi = Guid.NewGuid(), cus = Guid.NewGuid(), o1 = Guid.NewGuid(), o2 = Guid.NewGuid();
            await f.SeedAsync(db =>
            {
                db.UnitMeasure.Add(new UnitMeasure { Id = unit, Code = "VKG", Name = "kg", Status = "Active" });
                db.Ingredient.Add(new Ingredient { Id = ing, Code = "VNL1", Name = "Thịt nạc", BaseUnitId = unit, Status = "Active" });
                db.Supplier.Add(new Supplier { Id = sup, Code = "NCC-V1", Name = "NCC thịt", Status = "Active" });
                db.Warehouse.Add(new Warehouse { Id = wh, Code = "VKHO", Name = "Kho mát V", Status = "Active" });
                db.GoodsReceipt.Add(new GoodsReceipt { Id = gr, ReceiptNumber = "PH-V-001", SupplierId = sup, WarehouseId = wh, ReceiptDate = Day, Status = "Posted" });
                db.GoodsReceiptLine.Add(new GoodsReceiptLine { Id = grl, GoodsReceiptId = gr, IngredientId = ing, Quantity = 52, UomId = unit, UnitId = unit, ReceivedQuantity = 52, AcceptedQuantity = 50, RejectedQuantity = 2, UnitCost = 100000 });
                db.Shift.Add(new Shift { Id = shift, Code = "TRUA", Name = "Trưa", Status = "Active" });
                db.ProductionOrder.Add(new ProductionOrder { Id = po, ProductionPlanId = Guid.NewGuid(), DishId = Guid.NewGuid(), RecipeId = Guid.NewGuid(), ShiftId = shift, PlannedQty = 100, Status = "Completed", PlannedStartDate = Day });
                db.MaterialIssue.Add(new MaterialIssue { Id = mi, IssueNumber = "PX-V-001", ProductionOrderId = po, WarehouseId = wh, IssueDate = Day, Status = "Posted" });
                db.InventoryTransaction.Add(new InventoryTransaction
                {
                    Id = Guid.NewGuid(), TransactionNumber = "PX-V-001", TransactionType = "ISSUE", WarehouseId = wh, IngredientId = ing, ReferenceType = "MaterialIssue", ReferenceId = mi,
                    Quantity = 20, QuantityOut = 20, UomId = unit, UnitId = unit, UnitCost = 100000, TransactionDate = Day,
                });
                db.Customer.Add(new Customer { Id = cus, Code = "KHV1", CIF = "KHV1", Name = "Khách V", DisplayName = "Khách V", CustomerType = "BUSINESS", Status = "Active" });
                db.Order.Add(new Order { Id = o1, OrderNumber = "DH-V1", CustomerId = cus, OrderDate = Day, MealDate = Day, DeliveryDate = Day, Status = "Delivered", TotalAmount = 4000000 });
                db.Order.Add(new Order { Id = o2, OrderNumber = "DH-V2", CustomerId = cus, OrderDate = Day, MealDate = Day, DeliveryDate = Day, Status = "Delivered", TotalAmount = 0, Note = "[DON_BU:DH-V1|PA-1] suất bù" });
                db.OrderLine.Add(new OrderLine { Id = Guid.NewGuid(), OrderId = o1, MealType = "TRUA", Quantity = 100, UnitPrice = 40000, LineAmount = 4000000 });
                db.OrderLine.Add(new OrderLine { Id = Guid.NewGuid(), OrderId = o2, MealType = "TRUA", Quantity = 3, UnitPrice = 0, LineAmount = 0 });
                return Task.CompletedTask;
            });
            using var c = f.CreateAuthorizedClient();

            var list = await MonthAsync(c);
            var purchase = One(list, "PURCHASE");
            purchase.GetProperty("number").GetString().Should().Be("PH-V-001");
            purchase.GetProperty("amount").GetDecimal().Should().Be(5000000, "số đạt 50 × giá 100.000");
            purchase.GetProperty("status").GetString().Should().Be("ERROR", "chưa nhận danh mục AMIS");
            var issue = One(list, "ISSUE");
            issue.GetProperty("objectCode").GetString().Should().Be("TP0002", "ca Trưa → thành phẩm suất trưa = đối tượng THCP");
            issue.GetProperty("amount").GetDecimal().Should().Be(2000000);
            One(list, "FG_IN").GetProperty("quantity").GetDecimal().Should().Be(103, "suất đã giao gồm suất bù");
            One(list, "FG_OUT").GetProperty("quantity").GetDecimal().Should().Be(3, "suất bù đơn giá 0");

            // Nhận danh mục AMIS (vật tư gồm thành phẩm, NCC) → Sẵn sàng.
            await ImportAsync(c, Catalog("Vật tư hàng hóa", ["Mã", "Tên", "Đơn vị tính"], ["VNL1", "Thịt nạc", "kg"], ["TP0002", "Suất ăn trưa", "Suất"]));
            await ImportAsync(c, Catalog("Nhà cung cấp", ["Mã nhà cung cấp", "Tên nhà cung cấp"], ["NCC-V1", "NCC thịt"]));
            list = await MonthAsync(c);
            One(list, "PURCHASE").GetProperty("status").GetString().Should().Be("READY");
            One(list, "ISSUE").GetProperty("status").GetString().Should().Be("READY");
            string pKey = One(list, "PURCHASE").GetProperty("key").GetString()!;

            // Tải Excel → Chờ lập trên AMIS; đánh dấu → Đã lập; nghiệp vụ gốc đổi giá → Đã sửa – cần điều chỉnh.
            var xls = await c.PostAsJsonAsync("/api/AccountingVoucher/ExportExcel", new { month = Month, listKey = new[] { pKey } });
            xls.Content.Headers.ContentType!.MediaType.Should().Contain("spreadsheetml");
            using (var wb = new XLWorkbook(new MemoryStream(await xls.Content.ReadAsByteArrayAsync())))
            {
                var ws = wb.Worksheet(1);
                ws.Cell(1, 2).GetString().Should().Be("Số chứng từ");
                ws.Cell(2, 6).GetString().Should().Be("VNL1");
                ws.Cell(2, 11).GetValue<decimal>().Should().Be(5000000);
            }
            One(await MonthAsync(c), "PURCHASE").GetProperty("status").GetString().Should().Be("WAITING_AMIS");
            (await LegacyResult.ReadAsync(await c.PostAsJsonAsync("/api/AccountingVoucher/MarkCreated", new { month = Month, listKey = new[] { pKey } }))).Ok();
            One(await MonthAsync(c), "PURCHASE").GetProperty("status").GetString().Should().Be("CREATED_ON_AMIS");
            await f.SeedAsync(async db =>
            {
                var line = await db.GoodsReceiptLine.SingleAsync(x => x.Id == grl);
                line.UnitCost = 110000;
            });
            var changed = One(await MonthAsync(c), "PURCHASE");
            changed.GetProperty("status").GetString().Should().Be("CHANGED");
            (await MonthAsync(c, "need")).Should().Contain(v => v.GetProperty("key").GetString() == pKey, "Cần xử lý gồm dòng đã sửa sau khi gửi");
        }
    }
}
