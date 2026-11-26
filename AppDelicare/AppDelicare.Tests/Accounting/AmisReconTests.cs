using System.Net.Http.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using ClosedXML.Excel;
using FluentAssertions;

namespace AppDelicare.Tests.Accounting
{
    /// <summary>
    /// ACC-8 (D-131 l): đối chiếu 8 chỉ tiêu – chưa có số AMIS; nhập tay ghép đè; khớp / lệch do giá (tồn cuối kỳ); Excel mẫu tải về → điền → nhập lại;
    /// tệp sai mẫu báo lỗi; chưa kiểm kê cuối tháng → "Chờ kiểm kê".
    /// </summary>
    public class AmisReconTests
    {
        [Fact]
        public async Task Recon_compares_eight_indicators_with_manual_and_excel_figures()
        {
            using var f = new InMemoryApiFactory();
            f.Clock.FixedNow = new DateTime(2035, 2, 5, 9, 0, 0);
            Guid wh = Guid.NewGuid(), ing = Guid.NewGuid(), unit = Guid.NewGuid();
            await f.SeedAsync(db =>
            {
                db.Warehouse.Add(new Warehouse { Id = wh, Code = "AKHO", Name = "Kho A", Status = "Active" });
                db.InventoryTransaction.Add(new InventoryTransaction
                {
                    Id = Guid.NewGuid(), TransactionNumber = "NK-A-1", TransactionType = "RECEIPT", WarehouseId = wh, IngredientId = ing, Quantity = 10, QuantityIn = 10, UomId = unit, UnitId = unit, UnitCost = 1000, TransactionDate = new DateTime(2035, 1, 10),
                });
                return Task.CompletedTask;
            });
            using var c = f.CreateAuthorizedClient();
            async Task<Dictionary<string, (string Result, decimal Delicare)>> Rows()
            {
                var m = (await c.GetResultAsync("/api/AmisRecon/GetMonth?month=2035-01")).Ok();
                return m.GetProperty("listRow").EnumerateArray().ToDictionary(r => r.GetProperty("code").GetString()!, r => (r.GetProperty("result").GetString()!, r.GetProperty("delicare").GetDecimal()));
            }

            var r0 = await Rows();
            r0.Should().HaveCount(8);
            r0["REVENUE"].Result.Should().Be("NO_AMIS");
            r0["STOCK_END"].Should().Be(("WAIT_COUNT", 10000m), "chưa ghi sổ kiểm kê cuối tháng");

            await f.SeedAsync(db =>
            {
                db.StockCount.Add(new StockCount { Id = Guid.NewGuid(), CountNumber = "KK-A", WarehouseId = wh, CountDate = new DateTime(2035, 1, 31), Status = "Posted" });
                return Task.CompletedTask;
            });
            (await LegacyResult.ReadAsync(await c.PostAsJsonAsync("/api/AmisRecon/SaveFigures", new { month = "2035-01", listFigure = Array.Empty<object>() }))).Fail("Chưa có số");
            (await LegacyResult.ReadAsync(await c.PostAsJsonAsync("/api/AmisRecon/SaveFigures", new { month = "2035-01", listFigure = new[] { new { code = "REVENUE", value = (decimal?)0 }, new { code = "STOCK_END", value = (decimal?)9500 } } }))).Ok();
            var r1 = await Rows();
            r1["REVENUE"].Result.Should().Be("MATCH");
            r1["STOCK_END"].Result.Should().Be("PRICE_METHOD", "tồn cuối lệch do AMIS tính bình quân");
            r1["PURCHASE"].Result.Should().Be("NO_AMIS");

            // Excel mẫu: tải về, điền PURCHASE = 2 (Delicare 0 phiếu) → Lệch; số đã nhập trước vẫn giữ.
            var bytes = await (await c.GetAsync("/api/AmisRecon/Template?month=2035-01")).Content.ReadAsByteArrayAsync();
            using (var ms = new MemoryStream(bytes))
            using (var wb = new XLWorkbook(ms))
            {
                var ws = wb.Worksheets.First();
                var row = ws.RowsUsed().First(r => r.Cell(1).GetString() == "PURCHASE");
                row.Cell(4).Value = 2;
                ws.RowsUsed().First(r => r.Cell(1).GetString() == "STOCK_END").Cell(4).GetDouble().Should().Be(9500);
                using var outMs = new MemoryStream();
                wb.SaveAs(outMs);
                bytes = outMs.ToArray();
            }
            using (var form = new MultipartFormDataContent { { new ByteArrayContent(bytes), "file", "so-amis.xlsx" }, { new StringContent("2035-01"), "month" } })
            {
                (await LegacyResult.ReadAsync(await c.PostAsync("/api/AmisRecon/ImportExcel", form))).Ok();
            }
            var r2 = await Rows();
            r2["PURCHASE"].Result.Should().Be("DIFF");
            r2["STOCK_END"].Result.Should().Be("PRICE_METHOD");
            r2["REVENUE"].Result.Should().Be("MATCH");

            using var wrong = new XLWorkbook();
            wrong.AddWorksheet("x").Cell(1, 1).Value = "abc";
            using var wms = new MemoryStream();
            wrong.SaveAs(wms);
            using var form2 = new MultipartFormDataContent { { new ByteArrayContent(wms.ToArray()), "file", "sai.xlsx" }, { new StringContent("2035-01"), "month" } };
            (await LegacyResult.ReadAsync(await c.PostAsync("/api/AmisRecon/ImportExcel", form2))).Fail("không đúng mẫu");
        }
    }
}
