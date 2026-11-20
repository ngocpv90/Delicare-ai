using System.Net;
using System.Net.Http.Headers;
using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Import
{
    /// <summary>
    /// T49 – nạp danh mục từ Excel: file mẫu (tiêu đề tiếng Việt), file vận hành thật (bố cục Rule Menu / Tổng hợp báo giá – dữ liệu [DEMO] tự tạo),
    /// idempotent theo mã, lỗi từng dòng, quyền Import theo màn hình đích.
    /// </summary>
    public class MasterDataImportTests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public MasterDataImportTests(InMemoryApiFactory factory) => _factory = factory;

        private static async Task<LegacyResult> ImportAsync(HttpClient c, string tableName, byte[] file, string fileName = "demo.xlsx")
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(tableName), "TableName");
            var content = new ByteArrayContent(file);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            form.Add(content, "File", fileName);
            return await LegacyResult.ReadAsync(await c.PostAsync("/api/ImportExcel/Import", form));
        }

        /// <summary>Tải mẫu của danh mục, xóa dòng ví dụ, ghi các dòng dữ liệu (theo thứ tự cột của mẫu).</summary>
        private static async Task<byte[]> FillTemplateAsync(HttpClient c, string tableName, params string?[][] rows)
        {
            var response = await c.GetAsync($"/api/ImportExcel/GetTemplate?tableName={tableName}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            using var wb = new XLWorkbook(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
            var ws = wb.Worksheet("DuLieu");
            ws.Row(2).Clear();
            for (int r = 0; r < rows.Length; r++)
            {
                for (int col = 0; col < rows[r].Length; col++)
                {
                    ws.Cell(r + 2, col + 1).Value = rows[r][col] ?? string.Empty;
                }
            }
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        [Fact]
        public async Task Template_list_and_download()
        {
            using var c = _factory.CreateAuthorizedClient();

            var list = await c.GetResultAsync("/api/ImportExcel/GetTemplateList");
            list.ListData.GetArrayLength().Should().Be(15);   // + AttendanceMachine (BOT-3.3)

            var response = await c.GetAsync("/api/ImportExcel/GetTemplate?tableName=Customer");
            response.Content.Headers.ContentType!.MediaType.Should().Contain("spreadsheetml");
            using var wb = new XLWorkbook(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
            wb.Worksheet("DuLieu").Cell(1, 1).GetString().Should().Be("Mã khách hàng", "MA-1.3: mã trống → máy chủ sinh");
        }

        [Fact]
        public async Task Customer_import_reports_row_errors_and_is_idempotent()
        {
            using var c = _factory.CreateAuthorizedClient();
            // Cột: Mã, Tên, Loại, Tên hiển thị, CIF, MST, ĐT, Email, Địa chỉ, Trạng thái
            var file = await FillTemplateAsync(c, "Customer",
                ["[DEMO]-IMP-KH1", "[DEMO] Trường A", "SCHOOL"],
                ["[DEMO]-IMP-KH2", "[DEMO] Bệnh viện B", "HOSPITAL", null, "[DEMO]-CIF-B"],
                ["[DEMO]-IMP-KH3", null, "SCHOOL"],
                [null, "[DEMO] Khách không mã IMP", "INDIVIDUAL"]);

            var first = await ImportAsync(c, "Customer", file);

            var data = first.Ok();
            data.GetProperty("inserted").GetInt32().Should().Be(3);
            var errors = data.GetProperty("listError");
            errors.GetArrayLength().Should().Be(1);
            errors[0].GetProperty("row").GetInt32().Should().Be(4);
            errors[0].GetProperty("message").GetString().Should().Contain("Tên khách hàng");
            var kh1 = await _factory.QueryAsync(db => db.Customer.AsNoTracking().SingleAsync(x => x.Code == "[DEMO]-IMP-KH1"));
            kh1.CIF.Should().Be("[DEMO]-IMP-KH1", "CIF = mã (D-63 h)");
            kh1.CustomerType.Should().Be("BUSINESS", "D-63 f: giá trị cũ là ngành");
            kh1.Industry.Should().Be("SCHOOL");
            var noCode = await _factory.QueryAsync(db => db.Customer.AsNoTracking().SingleAsync(x => x.Name == "[DEMO] Khách không mã IMP"));
            noCode.Code.Should().MatchRegex(@"^KHC-\d{5}$", "MA-1.3: mã trống → máy chủ sinh theo loại");
            noCode.CIF.Should().Be(noCode.Code);
            kh1.DisplayName.Should().Be("[DEMO] Trường A");
            kh1.Status.Should().Be("Active");

            var second = (await ImportAsync(c, "Customer", file)).Ok();
            second.GetProperty("inserted").GetInt32().Should().Be(1, "dòng không mã luôn là bản ghi mới");
            second.GetProperty("skipped").GetInt32().Should().Be(2, "dòng có mã khớp theo mã");
            (await _factory.QueryAsync(db => db.Customer.CountAsync(x => x.Code!.StartsWith("[DEMO]-IMP-KH")))).Should().Be(2);
        }

        [Fact]
        public async Task Ingredient_dish_recipe_line_and_price_chain()
        {
            using var c = _factory.CreateAuthorizedClient();
            // Ingredient: Mã, Tên, Nhóm, ĐVT, Bảo quản, Thu hồi, Hao hụt, Quản lý hạn, Số ngày, Tồn tối thiểu
            var ing = await FillTemplateAsync(c, "Ingredient",
                ["[DEMO]-IMP-NL1", "[DEMO] Thịt ba chỉ", "[DEMO] Thịt", "[DEMO]kg", "CHILLED", null, "0,05", "Có", "3"],
                ["[DEMO]-IMP-NL2", "[DEMO] Sai hạn", null, null, null, null, null, "Có"]);
            var ingResult = (await ImportAsync(c, "Ingredient", ing)).Ok();
            ingResult.GetProperty("inserted").GetInt32().Should().Be(1);
            ingResult.GetProperty("listError")[0].GetProperty("message").GetString().Should().Contain("hạn dùng");
            var nl1 = await _factory.QueryAsync(db => db.Ingredient.AsNoTracking().SingleAsync(x => x.Code == "[DEMO]-IMP-NL1"));
            nl1.StandardWastePercent.Should().Be(5, "0,05 là tỷ lệ → 5%");
            nl1.BaseUnitId.Should().NotBeNull();
            nl1.BaseUomId.Should().Be(nl1.BaseUnitId, "ghi song song cột cũ");

            (await ImportAsync(c, "Dish", await FillTemplateAsync(c, "Dish", ["[DEMO]-IMP-MON1", "[DEMO] Thịt kho", "[DEMO] Món chính", "250"]))).Ok()
                .GetProperty("inserted").GetInt32().Should().BeGreaterThanOrEqualTo(1);

            var lines = await FillTemplateAsync(c, "RecipeLine",
                ["[DEMO]-IMP-MON1", "[DEMO]-IMP-NL1", "12", "[DEMO]kg", "5", "10"],
                ["[DEMO]-IMP-MON1", "[DEMO]-IMP-KHONGCO", "1", "[DEMO]kg"]);
            var lineResult = (await ImportAsync(c, "RecipeLine", lines)).Ok();
            lineResult.GetProperty("inserted").GetInt32().Should().Be(1);
            lineResult.GetProperty("listError").GetArrayLength().Should().Be(1);
            var recipe = await _factory.QueryAsync(db => db.Recipe.AsNoTracking().SingleAsync(r => db.Dish.Any(d => d.Id == r.DishId && d.Code == "[DEMO]-IMP-MON1")));
            recipe.Status.Should().Be("Draft");
            var line = await _factory.QueryAsync(db => db.RecipeLine.AsNoTracking().SingleAsync(l => l.RecipeId == recipe.Id));
            line.Quantity.Should().Be(12);
            line.ExpectedWasteRate.Should().Be(10);

            // Nhập lại với định lượng mới → cập nhật, không thêm dòng.
            var again = (await ImportAsync(c, "RecipeLine", await FillTemplateAsync(c, "RecipeLine", ["[DEMO]-IMP-MON1", "[DEMO]-IMP-NL1", "13", "[DEMO]kg"]))).Ok();
            again.GetProperty("updated").GetInt32().Should().Be(1);
            (await _factory.QueryAsync(db => db.RecipeLine.CountAsync(l => l.RecipeId == recipe.Id && !l.IsDeleted))).Should().Be(1);

            (await ImportAsync(c, "Supplier", await FillTemplateAsync(c, "Supplier", ["[DEMO]-IMP-NCC1", "[DEMO] NCC thịt"], ["[DEMO]-IMP-NCC2", "[DEMO] NCC thịt 2"]))).Ok();
            var prices = await FillTemplateAsync(c, "SupplierMaterial",
                ["[DEMO]-IMP-NCC1", "[DEMO]-IMP-NL1", "120000", "[DEMO]kg", null, null, "Có"],
                ["[DEMO]-IMP-NCC2", "[DEMO]-IMP-NL1", "118000", "[DEMO]kg", null, null, "Có"],
                ["[DEMO]-IMP-NCC2", "[DEMO]-IMP-NL1", "0"]);
            var priceResult = (await ImportAsync(c, "SupplierMaterial", prices)).Ok();
            priceResult.GetProperty("listError").GetArrayLength().Should().Be(1);
            var rows = await _factory.QueryAsync(db => db.SupplierMaterial.AsNoTracking().Where(x => x.IngredientId == nl1.Id).ToListAsync());
            rows.Should().HaveCount(2);
            rows.Single(x => x.IsPreferred).UnitPrice.Should().Be(118000);
        }

        [Fact]
        public async Task Real_rule_menu_and_price_summary_layouts()
        {
            using var c = _factory.CreateAuthorizedClient();
            byte[] book;
            using (var wb = new XLWorkbook())
            {
                var bank = wb.Worksheets.Add("Ngân hàng món");
                bank.Cell("A1").Value = "[DEMO] Món ăn sáng"; bank.Cell("B1").Value = "[DEMO] Món chính";
                bank.Cell("A2").Value = "[DEMO] Bánh bao"; bank.Cell("B2").Value = "[DEMO] Thịt kho tàu"; bank.Cell("B3").Value = "[DEMO] Cá kho";
                var detail = wb.Worksheets.Add("Chi tiết món và hao hụt, giá");
                string[] heads = ["Group", "Nhóm", "Nguyên liệu chính", "Tên món", "Thành phần nguyên liệu", "Phương pháp chế biến", "% hao hụt sau sơ chế_NL chính", "% hao hụt sau sơ chế_thành phần NL"];
                for (int i = 0; i < heads.Length; i++) detail.Cell(1, i + 1).Value = heads[i];
                detail.Cell("L1").Value = "Mã"; detail.Cell("M1").Value = "Thành phần nguyên liệu"; detail.Cell("N1").Value = "ĐVT"; detail.Cell("O1").Value = "Giá";
                detail.Cell("P1").Value = "% hao hụt sơ chế"; detail.Cell("Q1").Value = "% hao hụt theo phương pháp chế biến";
                detail.Cell("B2").Value = "[DEMO] Món chính"; detail.Cell("D2").Value = "[DEMO] Thịt kho tàu"; detail.Cell("E2").Value = "[DEMO] Thịt";
                detail.Cell("B3").Value = "[DEMO] Món chính"; detail.Cell("D3").Value = "[DEMO] Thịt kho tàu"; detail.Cell("E3").Value = "[DEMO] Trứng";
                detail.Cell("L2").Value = "[DEMO]AD01"; detail.Cell("M2").Value = "[DEMO] Thịt"; detail.Cell("N2").Value = "[DEMO]Kg"; detail.Cell("P2").Value = 0.1;
                detail.Cell("L3").Value = "[DEMO]AD02"; detail.Cell("M3").Value = "[DEMO] Trứng"; detail.Cell("N3").Value = "[DEMO]Quả"; detail.Cell("P3").Value = 2;
                using var ms = new MemoryStream();
                wb.SaveAs(ms);
                book = ms.ToArray();
            }

            var bankResult = (await ImportAsync(c, "RuleMenu_DishBank", book)).Ok();
            bankResult.GetProperty("totalRows").GetInt32().Should().Be(3);
            (await _factory.QueryAsync(db => db.Dish.CountAsync(d => d.Name!.StartsWith("[DEMO] ") && (d.Name == "[DEMO] Bánh bao" || d.Name == "[DEMO] Thịt kho tàu" || d.Name == "[DEMO] Cá kho")))).Should().Be(3);

            var detailResult = (await ImportAsync(c, "RuleMenu_DishDetail", book)).Ok();
            detailResult.GetProperty("listNote").EnumerateArray().Should().Contain(n => n.GetString()!.Contains("GAP-IMP-01"));
            var thit = await _factory.QueryAsync(db => db.Ingredient.AsNoTracking().SingleAsync(x => x.Code == "[DEMO]AD01"));
            thit.StandardWastePercent.Should().Be(10, "0,1 → 10%");
            (await _factory.QueryAsync(db => db.Ingredient.AsNoTracking().SingleAsync(x => x.Code == "[DEMO]AD02"))).StandardWastePercent.Should().Be(2);
            (await _factory.QueryAsync(db => db.Dish.CountAsync(d => d.Name == "[DEMO] Thịt kho tàu"))).Should().Be(1, "món nhận diện theo tên – không tạo trùng");

            byte[] summary;
            using (var wb = new XLWorkbook())
            {
                var ws = wb.Worksheets.Add("Summary");
                string[] heads = ["STT", "Mã", "Tên", "Đơn vị tính chính", "Đơn giá (chưa VAT)", "% thuế VAT", "Đơn giá (gồm VAT)", "Ghi chú", "NCC", "Ma_NCC"];
                for (int i = 0; i < heads.Length; i++) ws.Cell(1, i + 1).Value = heads[i];
                ws.Cell("A2").Value = 1; ws.Cell("B2").Value = "[DEMO]AD01"; ws.Cell("C2").Value = "[DEMO] Thịt"; ws.Cell("D2").Value = "[DEMO]Kg"; ws.Cell("E2").Value = 125000; ws.Cell("I2").Value = "[DEMO] NCC A"; ws.Cell("J2").Value = "[DEMO]NCC-A";
                ws.Cell("A3").Value = 2; ws.Cell("B3").Value = "[DEMO]AD99"; ws.Cell("C3").Value = "[DEMO] Nguyên liệu mới"; ws.Cell("D3").Value = "[DEMO]Kg"; ws.Cell("E3").Value = 5000; ws.Cell("I3").Value = "[DEMO] NCC A"; ws.Cell("J3").Value = "[DEMO]NCC-A";
                ws.Cell("A4").Value = 3; ws.Cell("B4").Value = "[DEMO]AD01"; ws.Cell("E4").Value = 0; ws.Cell("J4").Value = "[DEMO]NCC-A";
                using var ms = new MemoryStream();
                wb.SaveAs(ms);
                summary = ms.ToArray();
            }
            var priceResult = (await ImportAsync(c, "BaoGia_Summary", summary, "bao-gia.xlsm")).Ok();
            priceResult.GetProperty("listError").GetArrayLength().Should().Be(1);
            var supplier = await _factory.QueryAsync(db => db.Supplier.AsNoTracking().SingleAsync(x => x.Code == "[DEMO]NCC-A"));
            (await _factory.QueryAsync(db => db.SupplierMaterial.AsNoTracking().SingleAsync(x => x.SupplierId == supplier.Id && x.IngredientId == thit.Id))).UnitPrice.Should().Be(125000);
            (await _factory.QueryAsync(db => db.Ingredient.AnyAsync(x => x.Code == "[DEMO]AD99"))).Should().BeTrue();

            var again = (await ImportAsync(c, "BaoGia_Summary", summary, "bao-gia.xlsm")).Ok();
            again.GetProperty("inserted").GetInt32().Should().Be(0);
            (await _factory.QueryAsync(db => db.SupplierMaterial.CountAsync(x => x.SupplierId == supplier.Id))).Should().Be(2);
        }

        [Fact]
        public async Task Wrong_layout_and_missing_permission()
        {
            using var c = _factory.CreateAuthorizedClient();
            byte[] empty;
            using (var wb = new XLWorkbook())
            {
                wb.Worksheets.Add("Khac").Cell("A1").Value = "x";
                using var ms = new MemoryStream();
                wb.SaveAs(ms);
                empty = ms.ToArray();
            }
            (await ImportAsync(c, "Customer", empty)).Fail("tiêu đề");
            (await ImportAsync(c, "RuleMenu_DishBank", empty)).Fail("Ngân hàng món");
            (await ImportAsync(c, "KhongCo", empty)).Fail("không tồn tại");

            var userId = Guid.NewGuid();
            await _factory.SeedAsync(db =>
            {
                var menu = new ObjectMenu { Id = Guid.NewGuid(), Name = "Khách", MetaLink = "#/crm-customer-imp", IsActive = true };
                db.User.Add(new User { Id = userId, UserName = "no-import", IsActive = true });
                return Task.CompletedTask;
            });
            using var user = _factory.CreateAuthorizedClient(userId);
            using var form = new MultipartFormDataContent { { new StringContent("Customer"), "TableName" }, { new ByteArrayContent(empty), "File", "a.xlsx" } };
            (await user.PostAsync("/api/ImportExcel/Import", form)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }
}
