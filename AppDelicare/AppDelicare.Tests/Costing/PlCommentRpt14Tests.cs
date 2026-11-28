using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Tests.Costing
{
    /// <summary>
    /// RPT-1.4 (D-140 h): nhận xét tự động theo quy tắc (BusinessRules:Pl) – doanh thu tăng / giảm, giá vốn tăng nhanh hơn doanh thu, lợi nhuận trước thuế + biên,
    /// 3 khoản chi phí tăng mạnh nhất; xuất tab Excel / PDF (quyền Xuất mục dash-pl – người chỉ Xem bị 403).
    /// </summary>
    public class PlCommentRpt14Tests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public PlCommentRpt14Tests(InMemoryApiFactory factory) => _factory = factory;

        private static object L(string acc, string du, decimal no, decimal co, string dg) => new { account = acc, counterAccount = du, debit = no, credit = co, description = dg };

        [Fact]
        public async Task Comments_follow_rules_and_tab_exports_to_excel_and_pdf()
        {
            _factory.Clock.FixedNow = new DateTime(2037, 9, 3, 9, 0, 0);
            using var c = _factory.CreateAuthorizedClient();
            async Task Import(string month, decimal rev, decimal nvl, decimal elec)
            {
                var lines = new object[]
                {
                    L("511", "131", 0, rev, "suất ăn trưa"), L("621", "152", nvl, 0, "Thịt lợn"), L("622", "334", 50_000_000, 0, "Lương"), L("627", "331", elec, 0, "Tiền điện"),
                    L("641", "334", 30_000_000, 0, "Lương bán hàng"), L("642", "112", 20_000_000, 0, "Phí ngân hàng"),
                };
                var r = await c.PostAsJsonAsync("/api/Dashboard/ImportAmisLines", new { month, lines, source = "test" });
                JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.GetProperty("responseCode").GetInt32().Should().Be(200);
            }
            await Import("2037-07", 1_000_000_000, 600_000_000, 20_000_000);
            await Import("2037-08", 1_200_000_000, 900_000_000, 45_000_000); // DT +20 %, giá vốn +47 %, điện +125 %

            var d = JsonDocument.Parse(await c.GetStringAsync("/api/Dashboard/GetPlReport?month=2037-08")).RootElement.GetProperty("data");
            var cm = d.GetProperty("comments").EnumerateArray().Select(x => (Level: x.GetProperty("level").GetString(), Text: x.GetProperty("text").GetString()!)).ToList();
            cm.Should().Contain(x => x.Level == "good" && x.Text.StartsWith("Doanh thu 1.200,0 trđ, tăng 20,0%"));
            cm.Should().Contain(x => x.Level == "warn" && x.Text.StartsWith("Giá vốn tăng nhanh hơn doanh thu"));
            cm.Should().Contain(x => x.Level == "warn" && x.Text.Contains("Chi phí tăng mạnh nhất") && x.Text.Contains("Thịt lợn") && x.Text.Contains("Điện"));
            cm.Should().Contain(x => x.Text.StartsWith("Lợi nhuận trước thuế"));

            var xls = await c.GetAsync("/api/Dashboard/ExportPl?month=2037-08&format=xlsx");
            xls.StatusCode.Should().Be(HttpStatusCode.OK);
            xls.Content.Headers.ContentDisposition!.FileName!.Trim('"').Should().Be("Ket-qua-kinh-doanh-2037-08.xlsx");
            (await xls.Content.ReadAsByteArrayAsync()).Take(2).Should().Equal((byte)'P', (byte)'K');
            var pdf = await c.GetAsync("/api/Dashboard/ExportPl?month=2037-08&format=pdf");
            pdf.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
            System.Text.Encoding.ASCII.GetString((await pdf.Content.ReadAsByteArrayAsync()).Take(4).ToArray()).Should().Be("%PDF");

            Guid viewer = Guid.NewGuid(), g = Guid.NewGuid();
            await _factory.SeedAsync(async db =>
            {
                db.User.Add(new User { Id = viewer, UserName = "uat.xem-pl", IsActive = true, AccountType = "INTERNAL" });
                db.UserGroup.Add(new UserGroup { Id = g, Name = "XEMPL", Code = "XEMPL" });
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = viewer, UserGroupId = g });
                var menu = await db.ObjectMenu.FirstOrDefaultAsync(x => x.MetaLink == "#/dash-pl");
                if (menu == null)
                {
                    menu = new ObjectMenu { Id = Guid.NewGuid(), Name = "dash-pl", MetaLink = "#/dash-pl", LevelShow = 2, OrderShow = 1, IsActive = true };
                    db.ObjectMenu.Add(menu);
                }
                db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = g, ObjectMenuId = menu.Id, AllowAccess = true });
            });
            using var v = _factory.CreateAuthorizedClient(viewer);
            (await v.GetAsync("/api/Dashboard/GetPlReport?month=2037-08")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await v.GetAsync("/api/Dashboard/ExportPl?month=2037-08")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }
}
