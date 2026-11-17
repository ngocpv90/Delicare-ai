using AppDelicare.Data.Connection;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Implement;
using AppDelicare.Services.Interface;
using AppDelicare.Tests.Integration;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppDelicare.Tests.Common
{
    /// <summary>
    /// BE2.7 – trên SQL Server dev thật (bỏ qua nếu không có DB), trong 1 transaction ngoài rồi ROLLBACK: truy vấn động của danh sách trắng (tìm chữ, khoảng ngày,
    /// GROUP BY trạng thái, nhãn khóa ngoại theo nhóm Id) dịch được sang SQL; lịch sử đọc AuditChange thật do DbContext ghi (Added / Modified), lọc tệp đính kèm theo
    /// khóa lưu trữ (LIKE 'prefix%'); mẫu in phiếu nhập dựng từ GetDetail thật → xlsx / docx. Dữ liệu thử gắn nhãn [DEMO]; không ghi tệp lên đĩa.
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class CommonSqlServerTests(ApiFactory factory)
    {
        [SkippableFact]
        public async Task Summary_export_history_files_and_print_on_sql_server_inside_outer_transaction()
        {
            Skip.If(factory.SkipReason != null, factory.SkipReason);
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            var actor = scope.ServiceProvider.GetRequiredService<ICurrentActor>();
            Guid admin = await db.User.Where(u => u.UserName == "admin").Select(u => u.Id).SingleAsync();
            int ordersBefore = await db.Order.CountAsync();
            int auditBefore = await db.AuditChange.CountAsync();
            int filesBefore = await db.DocumentFile.CountAsync();
            string key = "B27" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
            string tag = "[DEMO]-" + key;

            using (actor.BeginScope(ActorInfo.ForUser(admin)))
            await using (var tx = await db.Database.BeginTransactionAsync())
            {
                Guid customer = Guid.NewGuid(), supplier = Guid.NewGuid(), warehouse = Guid.NewGuid(), unit = Guid.NewGuid(), ingredient = Guid.NewGuid(), gr = Guid.NewGuid();
                var orders = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToArray();
                db.Customer.Add(new Customer { Id = customer, Code = tag, Name = "[DEMO] Khách " + key, CIF = tag, CustomerType = "FACTORY", DisplayName = "[DEMO] Khách " + key, Status = "Active" });
                for (int i = 0; i < orders.Length; i++)
                {
                    db.Order.Add(new Order { Id = orders[i], OrderNumber = $"{tag}-{i}", OrderNo = $"{tag}-{i}", CustomerId = customer, OrderDate = new DateTime(2027, 3, 1),
                        MealDate = new DateTime(2027, 3, 2 + i), DeliveryDate = new DateTime(2027, 3, 2 + i), Status = i == 2 ? "Confirmed" : "Draft", TotalQuantity = 10, Note = "[DEMO]" });
                }
                db.Supplier.Add(new Supplier { Id = supplier, Code = tag, Name = "[DEMO] NCC " + key, Status = "Active" });
                db.Warehouse.Add(new Warehouse { Id = warehouse, Code = tag, Name = "[DEMO] Kho " + key, Status = "Active" });
                db.UnitMeasure.Add(new UnitMeasure { Id = unit, Code = key, Name = "kg" });
                db.Ingredient.Add(new Ingredient { Id = ingredient, Code = tag, Name = "[DEMO] Gà " + key, Status = "Active" });
                db.GoodsReceipt.Add(new GoodsReceipt { Id = gr, ReceiptNumber = tag, ReceiptNo = tag, SupplierId = supplier, WarehouseId = warehouse, ReceiptDate = new DateTime(2027, 3, 1), Status = "Accepted", Note = "[DEMO]" });
                db.GoodsReceiptLine.Add(new GoodsReceiptLine { Id = Guid.NewGuid(), GoodsReceiptId = gr, IngredientId = ingredient, UomId = unit, UnitId = unit, Quantity = 5, ReceivedQuantity = 5, AcceptedQuantity = 5, UnitCost = 100000 });
                db.QcInspection.Add(new QcInspection { Id = Guid.NewGuid(), GoodsReceiptId = gr, InspectionDate = new DateTime(2027, 3, 1), Result = "DAT", InspectionNumber = tag });
                await db.SaveChangesAsync("Insert");
                await Task.Delay(50); // AuditChange.CreatedDate là datetime (~3 ms) – tách thứ tự Added / Modified
                var draft = await db.Order.SingleAsync(o => o.Id == orders[0]);
                draft.Status = "Submitted";
                await db.SaveChangesAsync("Update");
                db.ChangeTracker.Clear();

                var common = scope.ServiceProvider.GetRequiredService<ICommonService>();
                var summary = await common.GetStatusSummary(new CommonListRequest { Resource = "Order", SearchText = tag, FromDate = new DateTime(2027, 3, 1), ToDate = new DateTime(2027, 3, 3) });
                summary.ResponseCode.Should().Be(200, summary.ResponseMessage);
                summary.Data!.Total.Should().Be(2, "đơn ngày 04/03 nằm ngoài khoảng");
                summary.Data.ListByStatus.Single(s => s.Status == "Submitted").Count.Should().Be(1);
                summary.Data.ListByStatus.Single(s => s.Status == "Draft").Count.Should().Be(1);

                var export = await common.ExportList(new CommonListRequest { Resource = "Order", SearchText = tag });
                export.ResponseCode.Should().Be(200, export.ResponseMessage);
                using (var wb = new XLWorkbook(new MemoryStream(export.Data!.Content)))
                {
                    var cells = wb.Worksheets.First().CellsUsed().Select(c => c.GetString()).ToList();
                    cells.Should().Contain(["[DEMO] Khách " + key, "Đã gửi", "Đã xác nhận", $"{tag}-1"]);
                }

                var history = await common.GetHistory("Order", orders[0]);
                history.ResponseCode.Should().Be(200, history.ResponseMessage);
                history.ListData.Select(h => h.Action).Should().Equal("Modified", "Added");
                history.ListData[0].ListChange.Should().ContainSingle(c => c.Field == "Status" && c.From == "Draft" && c.To == "Submitted");
                history.ListData[0].ByName.Should().Contain("admin");

                // Tệp đính kèm: chỉ ghi dòng DocumentFile (không ghi đĩa) để kiểm lọc theo khóa lưu trữ trên SQL Server.
                string sha = new('c', 64);
                db.DocumentFile.Add(new DocumentFile { Id = Guid.NewGuid(), FileName = "[DEMO] bien ban", FileType = ".pdf", FileSize = 10,
                    FileNameHas = DocumentStorageRules.BuildKey("GoodsReceipt", gr, "evidence", sha, ".pdf") });
                await db.SaveChangesAsync("Insert");
                var files = await scope.ServiceProvider.GetRequiredService<IDocumentFileService>().GetList(new DocumentFileListRequest { Resource = "GoodsReceipt", RecordId = gr, FileType = "pdf", Category = "evidence" });
                files.ResponseCode.Should().Be(200, files.ResponseMessage);
                files.ListData.Should().ContainSingle().Which.Sha256.Should().Be(sha);
                (await common.GetHistory("GoodsReceipt", gr)).ListData.Should().Contain(h => h.Source == "FILE");

                var print = scope.ServiceProvider.GetRequiredService<IPrintService>();
                var doc = await print.Build("GOODS_RECEIPT", gr);
                doc.ResponseCode.Should().Be(200, doc.ResponseMessage);
                doc.Data!.ListRow.Single().Should().Contain("500.000");
                (await print.Print("GOODS_RECEIPT", gr, "xlsx")).Data!.Content.Length.Should().BeGreaterThan(1000);
                (await print.Print("GOODS_RECEIPT", gr, "docx")).Data!.Content.Length.Should().BeGreaterThan(1000);
                await tx.RollbackAsync();
            }

            db.ChangeTracker.Clear();
            (await db.Order.CountAsync()).Should().Be(ordersBefore, "rollback không để lại dữ liệu");
            (await db.AuditChange.CountAsync()).Should().Be(auditBefore);
            (await db.DocumentFile.CountAsync()).Should().Be(filesBefore);
        }
    }
}
