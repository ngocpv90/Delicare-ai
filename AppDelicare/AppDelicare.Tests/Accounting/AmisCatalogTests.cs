using System.Net;
using System.Net.Http.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Accounting
{
    /// <summary>
    /// ACC-1 (D-131 c): Kế toán › Khớp mã AMIS – nhận danh mục từ tệp Excel xuất từ AMIS (tự nhận loại, bỏ dòng tiêu đề / tổng), so theo mã
    /// (Đã khớp · Cùng mã khác tên · Khác ĐVT · ĐVT viết hoa khác coi là một · Chưa có trên AMIS + gợi ý · Chỉ có trên AMIS), ghép mã (đổi mã Delicare,
    /// chặn mã đang dùng / mã không có trên AMIS), CIF khách theo mã, xuất danh sách mã cần tạo, quyền theo màn #/acc-amis-code.
    /// </summary>
    public class AmisCatalogTests
    {
        private static byte[] Sheet(string sheet, string[] head, params string[][] rows)
        {
            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet(sheet);
            ws.Cell(1, 1).Value = sheet.ToUpperInvariant();      // dòng tiêu đề như tệp xuất của AMIS
            for (int c = 0; c < head.Length; c++)
            {
                ws.Cell(3, c + 1).Value = head[c];
            }
            for (int r = 0; r < rows.Length; r++)
            {
                for (int c = 0; c < rows[r].Length; c++)
                {
                    ws.Cell(r + 4, c + 1).Value = rows[r][c];
                }
            }
            ws.Cell(rows.Length + 4, 1).Value = $"Số dòng = {rows.Length}";
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        private static async Task<LegacyResult> ImportAsync(HttpClient c, byte[] file, string? kind = null)
        {
            using var form = new MultipartFormDataContent { { new ByteArrayContent(file), "file", "amis.xlsx" } };
            if (kind != null)
            {
                form.Add(new StringContent(kind), "kind");
            }
            return await LegacyResult.ReadAsync(await c.PostAsync("/api/AmisCatalog/ImportExcel", form));
        }

        [Fact]
        public async Task Material_catalog_is_matched_by_code_with_name_unit_checks_suggestions_and_remap()
        {
            using var f = new InMemoryApiFactory();
            Guid chiec = Guid.NewGuid(), kg = Guid.NewGuid(), i1 = Guid.NewGuid(), i2 = Guid.NewGuid(), i3 = Guid.NewGuid(), i4 = Guid.NewGuid(), i5 = Guid.NewGuid();
            await f.SeedAsync(db =>
            {
                db.UnitMeasure.AddRange(new UnitMeasure { Id = chiec, Code = "AMCHIEC", Name = "Chiếc", Status = "Active" }, new UnitMeasure { Id = kg, Code = "AMKG", Name = "kg", Status = "Active" });
                db.Ingredient.AddRange(
                    new Ingredient { Id = i1, Code = "AMT1", Name = "Bánh bao", BaseUnitId = chiec, Status = "Active" },
                    new Ingredient { Id = i2, Code = "AMT2", Name = "Bánh bao chay trắng", BaseUnitId = chiec, Status = "Active" },
                    new Ingredient { Id = i3, Code = "AMT3", Name = "Gừng", BaseUnitId = kg, Status = "Active" },
                    new Ingredient { Id = i4, Code = "AMT4", Name = "Sữa chua", BaseUnitId = chiec, Status = "Active" },
                    new Ingredient { Id = i5, Code = "AMT5X", Name = "Thịt mông", BaseUnitId = kg, Status = "Active" });
                return Task.CompletedTask;
            });
            using var c = f.CreateAuthorizedClient();
            (await c.GetResultAsync("/api/AmisCatalog/GetMatch?kind=material")).Ok().GetProperty("hasSnapshot").GetBoolean().Should().BeFalse();

            var file = Sheet("Danh sách vật tư hàng hóa", ["Mã", "Tên", "Tính chất", "Đơn vị tính chính"],
                ["AMT1", "Bánh Bao", "Vật tư hàng hóa", "Chiếc"], ["AMT2", "Sữa Milo", "Vật tư hàng hóa", "Hộp"], ["AMT3", "Gừng", "Vật tư hàng hóa", "Kg"],
                ["AMT4", "Sữa chua", "Vật tư hàng hóa", "Cốc"], ["AMT9", "Thịt mông sấn", "Vật tư hàng hóa", "kg"], ["AMT1", "Trùng mã", "Vật tư hàng hóa", "Chiếc"]);
            var imp = (await ImportAsync(c, file)).Ok();
            imp.GetProperty("kind").GetString().Should().Be("material", "tự nhận loại theo tên sheet");
            imp.GetProperty("received").GetInt32().Should().Be(5, "bỏ dòng trùng mã + dòng tổng");

            var m = (await c.GetResultAsync("/api/AmisCatalog/GetMatch?kind=material")).Ok();
            m.GetProperty("hasSnapshot").GetBoolean().Should().BeTrue();
            var rows = m.GetProperty("listRow").EnumerateArray().ToList();
            string StatusOf(string code) => rows.Single(r => r.GetProperty("code").GetString() == code).GetProperty("status").GetString()!;
            StatusOf("AMT1").Should().Be("MATCHED", "tên khác hoa / thường vẫn khớp");
            StatusOf("AMT2").Should().Be("NAME_MISMATCH");
            StatusOf("AMT3").Should().Be("MATCHED");
            rows.Single(r => r.GetProperty("code").GetString() == "AMT3").GetProperty("note").GetString().Should().Contain("viết hoa");
            StatusOf("AMT4").Should().Be("UNIT_MISMATCH");
            var missing = rows.Single(r => r.GetProperty("code").GetString() == "AMT5X");
            missing.GetProperty("status").GetString().Should().Be("MISSING_ON_AMIS");
            missing.GetProperty("listSuggestion")[0].GetProperty("code").GetString().Should().Be("AMT9");
            rows.Should().Contain(r => r.GetProperty("amisCode").GetString() == "AMT9" && r.GetProperty("status").GetString() == "ONLY_ON_AMIS");

            // Ghép mã: AMT5X → AMT9 (mã Delicare đổi theo AMIS); mã đang dùng / không có trên AMIS → chặn.
            (await LegacyResult.ReadAsync(await c.PostAsJsonAsync("/api/AmisCatalog/Remap", new { kind = "material", id = i5, amisCode = "AMT9" }))).Ok();
            (await f.QueryAsync(db => db.Ingredient.AsNoTracking().SingleAsync(x => x.Id == i5))).Code.Should().Be("AMT9");
            (await LegacyResult.ReadAsync(await c.PostAsJsonAsync("/api/AmisCatalog/Remap", new { kind = "material", id = i2, amisCode = "AMT1" }))).Fail("đang dùng");
            (await LegacyResult.ReadAsync(await c.PostAsJsonAsync("/api/AmisCatalog/Remap", new { kind = "material", id = i2, amisCode = "KHONGCO" }))).Fail("không có trong danh mục");

            var xls = await c.GetAsync("/api/AmisCatalog/ExportMissing");
            xls.Content.Headers.ContentType!.MediaType.Should().Contain("spreadsheetml", "còn kho / thành phẩm chưa có trên AMIS");
        }

        [Fact]
        public async Task Customer_catalog_detected_by_header_remap_keeps_cif_equal_code_and_needs_permission()
        {
            using var f = new InMemoryApiFactory();
            Guid cus = Guid.NewGuid();
            await f.SeedAsync(db =>
            {
                db.Customer.Add(new Customer { Id = cus, Code = "KHC-AM1", CIF = "KHC-AM1", Name = "Công ty Đầu tư AM", DisplayName = "Đầu tư AM", CustomerType = "BUSINESS", Status = "Active" });
                return Task.CompletedTask;
            });
            using var c = f.CreateAuthorizedClient();
            var imp = (await ImportAsync(c, Sheet("Sheet1", ["Mã khách hàng", "Tên khách hàng", "Mã số thuế"], ["KH00099", "Công ty Đầu tư AM", "0101"]))).Ok();
            imp.GetProperty("kind").GetString().Should().Be("customer", "nhận loại theo tiêu đề cột");
            var row = (await c.GetResultAsync("/api/AmisCatalog/GetMatch?kind=customer")).Ok().GetProperty("listRow").EnumerateArray()
                .Single(r => r.GetProperty("code").GetString() == "KHC-AM1");
            row.GetProperty("status").GetString().Should().Be("MISSING_ON_AMIS");
            row.GetProperty("listSuggestion")[0].GetProperty("code").GetString().Should().Be("KH00099");
            (await LegacyResult.ReadAsync(await c.PostAsJsonAsync("/api/AmisCatalog/Remap", new { kind = "customer", id = cus, amisCode = "KH00099" }))).Ok()
                .GetProperty("status").GetString().Should().Be("MATCHED");
            var saved = await f.QueryAsync(db => db.Customer.AsNoTracking().SingleAsync(x => x.Id == cus));
            saved.Code.Should().Be("KH00099");
            saved.CIF.Should().Be("KH00099", "D-63 h: CIF = mã khách");

            var userId = Guid.NewGuid();
            await f.SeedAsync(db =>
            {
                db.User.Add(new User { Id = userId, UserName = "no-acc", IsActive = true });
                return Task.CompletedTask;
            });
            using var user = f.CreateAuthorizedClient(userId);
            (await user.GetAsync("/api/AmisCatalog/GetMatch?kind=customer")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }
}
