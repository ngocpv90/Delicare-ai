using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Costing
{
    /// <summary>
    /// RPT-1.2 (D-140 d, e): nhập sổ chi tiết AMIS (mẫu "SỔ CHI TIẾT CÁC TÀI KHOẢN" – dòng "Tài khoản: 511, Tháng 8 năm …") + B01-DN → chỉ tiêu PL_AMIS_* đúng quy tắc
    /// (DT = Có 511; GVHB = Nợ 621 + 622 + 627; CP = Nợ 641 + 642; LN trước thuế = LN thuần + 711 − 811; bỏ kết chuyển 911 / 154; ROA / ROE bình quân);
    /// nhập lại không nhân đôi; dòng chưa phân loại hiện riêng, thêm quy tắc rồi nhập lại thì hết; chỉ người có quyền Nhập (Kế toán); thẻ AMIS của tab hiện số.
    /// </summary>
    public class AmisPlRpt12Tests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public AmisPlRpt12Tests(InMemoryApiFactory factory) => _factory = factory;

        /// <summary>Sổ chi tiết theo mẫu AMIS: (TK đối ứng, Nợ, Có, diễn giải).</summary>
        private static byte[] Ledger(string acc, int month, int year, params (string Du, decimal No, decimal Co, string Dg)[] rows)
        {
            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("SỔ CHI TIẾT CÁC TÀI KHOẢN");
            ws.Cell(1, 1).Value = "SỔ CHI TIẾT CÁC TÀI KHOẢN";
            // 2 mẫu AMIS: "Tài khoản: 627, Tháng 8 năm …" hoặc chỉ "Tháng 8 năm …" (tài khoản ở dòng tổng dưới tiêu đề cột) – 641 dùng mẫu thứ 2.
            ws.Cell(2, 1).Value = acc == "641" ? $"Tháng {month} năm {year}" : $"Tài khoản: {acc}, Tháng {month} năm {year}";
            string[] head = ["Ngày hạch toán", "Ngày chứng từ", "Số chứng từ", "Diễn giải", "TK đối ứng", "Phát sinh Nợ", "Phát sinh Có", "Dư Nợ", "Dư Có"];
            for (int i = 0; i < head.Length; i++) ws.Cell(4, i + 1).Value = head[i];
            ws.Cell(5, 1).Value = "Tài khoản: " + acc;
            ws.Cell(5, 6).Value = rows.Sum(r => r.No);
            ws.Cell(5, 7).Value = rows.Sum(r => r.Co);
            int n = 6;
            foreach (var r in rows)
            {
                var d = new DateTime(year, month, 10);
                ws.Cell(n, 1).Value = d; ws.Cell(n, 2).Value = d; ws.Cell(n, 3).Value = "CT" + n; ws.Cell(n, 4).Value = r.Dg; ws.Cell(n, 5).Value = r.Du;
                ws.Cell(n, 6).Value = r.No; ws.Cell(n, 7).Value = r.Co;
                n++;
            }
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        private static byte[] Balance()
        {
            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("B01-DN");
            ws.Cell(8, 1).Value = "Chỉ tiêu"; ws.Cell(8, 2).Value = "Mã số"; ws.Cell(8, 4).Value = "Số cuối kỳ"; ws.Cell(8, 5).Value = "Số đầu kỳ";
            ws.Cell(9, 1).Value = "TỔNG CỘNG TÀI SẢN (280 = 100 + 200)"; ws.Cell(9, 2).Value = "280"; ws.Cell(9, 4).Value = 10_000_000; ws.Cell(9, 5).Value = 8_000_000;
            ws.Cell(10, 1).Value = "D - VỐN CHỦ SỞ HỮU"; ws.Cell(10, 2).Value = "400"; ws.Cell(10, 4).Value = 4_000_000; ws.Cell(10, 5).Value = 2_000_000;
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        private static MultipartFormDataContent Form(string? month, params (string Name, byte[] Bytes)[] files)
        {
            var f = new MultipartFormDataContent();
            if (month != null) f.Add(new StringContent(month), "month");
            foreach (var (name, bytes) in files)
            {
                var c = new ByteArrayContent(bytes);
                c.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
                f.Add(c, "files", name);
            }
            return f;
        }

        private async Task<Guid> UserAsync(string code, bool import)
        {
            Guid u = Guid.NewGuid(), g = Guid.NewGuid();
            await _factory.SeedAsync(async db =>
            {
                db.User.Add(new User { Id = u, UserName = "uat." + code.ToLowerInvariant(), IsActive = true, AccountType = "INTERNAL" });
                db.UserGroup.Add(new UserGroup { Id = g, Name = code, Code = code });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = u, UserGroupId = g });
                var menu = db.ObjectMenu.Local.FirstOrDefault(x => x.MetaLink == "#/dash-pl") ?? await db.ObjectMenu.FirstOrDefaultAsync(x => x.MetaLink == "#/dash-pl");
                if (menu == null)
                {
                    menu = new ObjectMenu { Id = Guid.NewGuid(), Name = "dash-pl", MetaLink = "#/dash-pl", LevelShow = 2, OrderShow = 1, IsActive = true };
                    db.ObjectMenu.Add(menu);
                }
                db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = g, ObjectMenuId = menu.Id, AllowAccess = true, AllowEdit = import, AllowImport = import });
            });
            return u;
        }

        [Fact]
        public async Task Amis_ledgers_import_to_exact_pl_figures_reimport_is_idempotent_and_rules_reclassify()
        {
            _factory.Clock.FixedNow = new DateTime(2034, 9, 5, 9, 0, 0);
            var files = new (string, byte[])[]
            {
                ("So_chi_tiet_cac_tai_khoan 511.xlsx", Ledger("511", 8, 2034, ("131", 0, 1_000_000, "suất ăn trưa"), ("131", 0, 500_000, "suất ăn sáng"), ("911", 1_500_000, 0, "Kết chuyển doanh thu"))),
                ("621.xlsx", Ledger("621", 8, 2034, ("152", 400_000, 0, "Thịt lợn ba chỉ"), ("152", 100_000, 0, "Lá chanh lạ"), ("154", 0, 500_000, "Kết chuyển chi phí 621"))),
                ("622.xlsx", Ledger("622", 8, 2034, ("334", 100_000, 0, "Lương công nhân"), ("154", 0, 100_000, "Kết chuyển"))),
                ("627.xlsx", Ledger("627", 8, 2034, ("2141", 50_000, 0, "Khấu hao"), ("331", 30_000, 0, "Tiền điện T8"), ("154", 0, 80_000, "Kết chuyển"))),
                ("641.xlsx", Ledger("641", 8, 2034, ("334", 60_000, 0, "Lương NV bán hàng"), ("911", 0, 60_000, "Kết chuyển"))),
                ("642.xlsx", Ledger("642", 8, 2034, ("112", 40_000, 0, "Phí ngân hàng"), ("911", 0, 40_000, "Kết chuyển"))),
                ("711.xlsx", Ledger("711", 8, 2034, ("331", 0, 20_000, "Chênh lệch"), ("911", 20_000, 0, "Kết chuyển"))),
                ("811.xlsx", Ledger("811", 8, 2034, ("111", 10_000, 0, "Phạt"), ("911", 0, 10_000, "Kết chuyển"))),
                ("BCTC T8.xlsx", Balance()),
            };
            Guid acc = await UserAsync("FINACC-12", true), viewer = await UserAsync("CEO-12", false);
            using var c = _factory.CreateAuthorizedClient(acc);
            async Task<JsonElement> ImportAsync(params (string, byte[])[] f)
            {
                var r = await c.PostAsync("/api/Dashboard/ImportAmis", Form(null, f));
                var j = JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;
                j.GetProperty("responseCode").GetInt32().Should().Be(200, j.ToString());
                return j.GetProperty("data");
            }
            var d = await ImportAsync(files);
            d.GetProperty("month").GetString().Should().Be("2034-08", "tháng lấy từ dòng \"Tài khoản: …, Tháng 8 năm 2034\"");
            var m = d.GetProperty("metrics");
            decimal M(string k) => m.GetProperty(k).GetDecimal();
            M("REVENUE").Should().Be(1_500_000m);
            M("COGS").Should().Be(680_000m, "621 + 622 + 627, bỏ kết chuyển 154");
            M("GROSS_PROFIT").Should().Be(820_000m);
            M("SELLING_ADMIN").Should().Be(100_000m);
            M("NET_OPERATING").Should().Be(720_000m);
            M("PROFIT_BEFORE_TAX").Should().Be(730_000m, "+ 711 − 811");
            M("ROA").Should().Be(8.11m);
            M("ROE").Should().Be(24.33m);
            var items = d.GetProperty("listItem").EnumerateArray().ToDictionary(i => i.GetProperty("account").GetString() + ":" + i.GetProperty("item").GetString(), i => i.GetProperty("value").GetDecimal());
            items["511:Bữa trưa"].Should().Be(1_000_000m);
            items["621:Thịt lợn"].Should().Be(400_000m);
            items["627:Khấu hao TSCĐ"].Should().Be(50_000m);
            items["627:Điện"].Should().Be(30_000m);
            items["642:Phí ngân hàng"].Should().Be(40_000m);
            d.GetProperty("listUnclassified").EnumerateArray().Select(u => u.GetProperty("description").GetString()).Should().Contain("Lá chanh lạ");

            var month = new DateTime(2034, 8, 1);
            int rows = await _factory.QueryAsync(db => db.KpiMonthlySnapshot.CountAsync(k => k.SnapshotMonth == month && !k.IsDeleted));
            await ImportAsync(files);
            (await _factory.QueryAsync(db => db.KpiMonthlySnapshot.CountAsync(k => k.SnapshotMonth == month && !k.IsDeleted))).Should().Be(rows, "nhập lại không nhân đôi");
            (await _factory.QueryAsync(db => db.KpiMonthlySnapshot.Where(k => k.SnapshotMonth == month && k.MetricCode == "PL_AMIS_REVENUE" && !k.IsDeleted).SumAsync(k => k.MetricValue))).Should().Be(1_500_000m);

            // Thêm quy tắc cho dòng chưa phân loại → nhập lại 621 → hết dòng chưa phân loại, số tổng giữ nguyên.
            var rules = JsonDocument.Parse(await c.GetStringAsync("/api/Dashboard/GetPlRules")).RootElement.GetProperty("listData").EnumerateArray()
                .Select(r => new { account = r.GetProperty("account").GetString(), keyword = r.TryGetProperty("keyword", out var k) && k.ValueKind == JsonValueKind.String ? k.GetString() : null,
                    counter = r.TryGetProperty("counter", out var cc) && cc.ValueKind == JsonValueKind.String ? cc.GetString() : null, item = r.GetProperty("item").GetString() }).ToList();
            rules.Insert(0, new { account = (string?)"621", keyword = (string?)"la chanh", counter = (string?)null, item = (string?)"Gia vị" });
            (await c.PostAsJsonAsync("/api/Dashboard/SavePlRules", rules)).StatusCode.Should().Be(HttpStatusCode.OK);
            var d2 = await ImportAsync(files[1]);
            d2.GetProperty("listUnclassified").EnumerateArray().Where(u => u.GetProperty("account").GetString() == "621").Should().BeEmpty();
            d2.GetProperty("listItem").EnumerateArray().Should().Contain(i => i.GetProperty("item").GetString() == "Gia vị" && i.GetProperty("value").GetDecimal() == 100_000m);
            d2.GetProperty("listItem").EnumerateArray().Should().NotContain(i => i.GetProperty("account").GetString() == "621" && i.GetProperty("item").GetString()!.Contains("chưa phân loại"));
            d2.GetProperty("metrics").GetProperty("PROFIT_BEFORE_TAX").GetDecimal().Should().Be(730_000m);

            // Nhập lại 1 sổ với số khác → chỉ tiêu suy ra tính lại.
            await ImportAsync(("641.xlsx", Ledger("641", 8, 2034, ("334", 160_000, 0, "Lương NV bán hàng"))));
            var d3 = JsonDocument.Parse(await c.GetStringAsync("/api/Dashboard/GetAmisMonth?month=2034-08")).RootElement.GetProperty("data").GetProperty("metrics");
            d3.GetProperty("SELLING_ADMIN").GetDecimal().Should().Be(200_000m);
            d3.GetProperty("PROFIT_BEFORE_TAX").GetDecimal().Should().Be(630_000m);

            // Thẻ AMIS của tab Kết quả kinh doanh có số; người chỉ có quyền Xem không nhập / sửa quy tắc được.
            using var v = _factory.CreateAuthorizedClient(viewer);
            var pl = JsonDocument.Parse(await v.GetStringAsync("/api/Dashboard/GetPl?month=2034-08")).RootElement.GetProperty("data");
            pl.GetProperty("hasAmis").GetBoolean().Should().BeTrue();
            pl.GetProperty("listCard").EnumerateArray().Single(x => x.GetProperty("code").GetString() == "PL_AMIS_PROFIT_BEFORE_TAX").GetProperty("value").GetDecimal().Should().Be(630_000m);
            (await v.PostAsync("/api/Dashboard/ImportAmis", Form("2034-08", files[0]))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await v.PostAsJsonAsync("/api/Dashboard/SavePlRules", rules)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

            // Sổ lẫn tháng khác → từ chối, không ghi.
            var bad = await c.PostAsync("/api/Dashboard/ImportAmis", Form("2034-07", files[0]));
            JsonDocument.Parse(await bad.Content.ReadAsStringAsync()).RootElement.GetProperty("responseCode").GetInt32().Should().Be(400);
            // Định nghĩa PL không lên tab Chỉ số kinh doanh.
            (await _factory.QueryAsync(db => db.KpiMetricDefinition.AnyAsync(k => k.MetricCode!.StartsWith("PL_AMIS_") && k.IsActive))).Should().BeFalse();
        }
    }
}
