using System.Net.Http.Json;
using System.Text.Json;
using AppDelicare.Tests.Integration;
using FluentAssertions;

namespace AppDelicare.Tests.Costing
{
    /// <summary>
    /// RPT-1.3 (D-140 f, i): nạp lịch sử bằng ImportAmisLines (như công cụ tools/rpt1) → báo cáo KQKD Tháng / Lũy kế năm, theo quý (Q3 = tháng 7 + 8), chuỗi 12 tháng;
    /// tháng không phát sinh 711 / 811 (AMIS không xuất sổ) vẫn tính Lợi nhuận trước thuế.
    /// </summary>
    public class AmisPlReportRpt13Tests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public AmisPlReportRpt13Tests(InMemoryApiFactory factory) => _factory = factory;

        private static object L(string acc, string du, decimal no, decimal co, string dg) => new { account = acc, counterAccount = du, debit = no, credit = co, description = dg };

        [Fact]
        public async Task History_lines_build_monthly_ytd_quarter_report_and_trend()
        {
            _factory.Clock.FixedNow = new DateTime(2036, 9, 3, 9, 0, 0);
            using var c = _factory.CreateAuthorizedClient();
            async Task Import(string month, decimal rev, decimal nvl, decimal sa, decimal? other)
            {
                var lines = new List<object>
                {
                    L("511", "131", 0, rev, "suất ăn trưa"), L("621", "152", nvl, 0, "Thịt lợn"), L("622", "334", 100, 0, "Lương"), L("627", "2141", 50, 0, "Khấu hao"),
                    L("641", "334", sa, 0, "Lương bán hàng"), L("642", "112", 10, 0, "Phí ngân hàng"),
                };
                if (other is { } o) lines.Add(L("711", "711-KC", 0, o, "Thu nhập khác"));
                var r = await c.PostAsJsonAsync("/api/Dashboard/ImportAmisLines", new { month, lines, source = "test rpt1" });
                var j = JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;
                j.GetProperty("responseCode").GetInt32().Should().Be(200, j.ToString());
            }
            await Import("2036-07", 10_000, 4_000, 1_000, 500);
            await Import("2036-08", 20_000, 9_000, 2_000, null);

            var d = JsonDocument.Parse(await c.GetStringAsync("/api/Dashboard/GetPlReport?month=2036-08")).RootElement.GetProperty("data");
            var st = d.GetProperty("statement").EnumerateArray().ToDictionary(l => l.GetProperty("code").GetString()!, l => l);
            decimal? N(JsonElement e, string p) => e.GetProperty(p).ValueKind == JsonValueKind.Null ? null : e.GetProperty(p).GetDecimal();
            N(st["PROFIT_BEFORE_TAX"], "month").Should().Be(20_000 - 9_150 - 2_010, "tháng không có 711 / 811 vẫn tính");
            N(st["PROFIT_BEFORE_TAX"], "ytd").Should().Be((20_000 - 9_150 - 2_010) + (10_000 - 4_150 - 1_010 + 500));
            N(st["REVENUE"], "ytd").Should().Be(30_000);
            N(st["REVENUE"], "prevMonth").Should().Be(10_000);
            N(st["ACC_621"], "month").Should().Be(9_000);
            st.Keys.Should().Contain(k => k.StartsWith("PL_AMIS_I621_"), "khoản mục chi tiết (nhóm NVL)");
            var q3 = d.GetProperty("quarters").EnumerateArray().Single(q => q.GetProperty("quarter").GetString() == "Q3");
            q3.GetProperty("revenue").GetDecimal().Should().Be(30_000, "lũy kế quý 3 = tháng 7 + 8");
            q3.GetProperty("monthsWithData").GetInt32().Should().Be(2);
            d.GetProperty("quarters").EnumerateArray().Single(q => q.GetProperty("quarter").GetString() == "Q1").GetProperty("revenue").ValueKind.Should().Be(JsonValueKind.Null);
            var rev = d.GetProperty("series").GetProperty("AMIS_REVENUE").EnumerateArray().ToList();
            rev.Should().HaveCount(12);
            rev[^1].GetDecimal().Should().Be(20_000);
            rev[^2].GetDecimal().Should().Be(10_000);
            rev[0].ValueKind.Should().Be(JsonValueKind.Null);
            d.GetProperty("cogsMix").EnumerateArray().Select(i => i.GetProperty("account").GetString()).Should().Contain(["621", "622", "627"]);
        }
    }
}
