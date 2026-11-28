using System.Net.Http.Json;
using System.Text.Json;
using AppDelicare.Model.Entity;
using AppDelicare.Tests.Integration;
using FluentAssertions;

namespace AppDelicare.Tests.Costing
{
    /// <summary>
    /// RPT-1.5 (anh duyệt 2026-11-28, sau khi soát file Report_202608): đối chiếu số liệu tháng trên tab Kết quả kinh doanh –
    /// thiếu sổ bắt buộc, doanh thu 511 chưa xếp bữa, phần "khác" ≥ ngưỡng, doanh thu AMIS lệch doanh thu Delicare, bảng cân đối bất thường
    /// (tài sản dài hạn âm, không cân, đầu kỳ ≠ cuối kỳ tháng trước); ROA / ROE quy năm = tháng × 12; ROA = LN trước thuế ÷ bình quân (đầu + cuối kỳ).
    /// </summary>
    public class PlChecksRpt15Tests : IClassFixture<InMemoryApiFactory>
    {
        private readonly InMemoryApiFactory _factory;

        public PlChecksRpt15Tests(InMemoryApiFactory factory) => _factory = factory;

        private static object L(string acc, string du, decimal no, decimal co, string dg) => new { account = acc, counterAccount = du, debit = no, credit = co, description = dg };

        private static List<(string Level, string Text)> Checks(JsonElement d) =>
            [.. d.GetProperty("checks").EnumerateArray().Select(x => (x.GetProperty("level").GetString()!, x.GetProperty("text").GetString()!))];

        [Fact]
        public async Task Checks_flag_unclassified_revenue_gap_and_balance_sheet_anomalies_and_roa_is_annualised()
        {
            _factory.Clock.FixedNow = new DateTime(2036, 9, 3, 9, 0, 0);
            using var c = _factory.CreateAuthorizedClient();
            async Task Import(string month, object[] lines, object? balance)
            {
                var r = await c.PostAsJsonAsync("/api/Dashboard/ImportAmisLines", new { month, lines, balance, source = "test" });
                JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.GetProperty("responseCode").GetInt32().Should().Be(200);
            }
            object[] Full(decimal rev, decimal other642) =>
            [
                L("511", "131", 0, rev, "suất ăn trưa"), L("621", "152", 600_000_000, 0, "Thịt lợn"), L("622", "334", 50_000_000, 0, "Lương"), L("627", "331", 20_000_000, 0, "Tiền điện"),
                L("641", "334", 30_000_000, 0, "Lương bán hàng"), L("642", "112", 20_000_000, 0, "Phí ngân hàng"), L("642", "111", other642, 0, "Mua văn phòng phẩm linh tinh"),
            ];
            // Tháng 7: đủ sổ, cân đối chuẩn.
            await Import("2036-07", Full(1_000_000_000, 1_000_000), new { totalAssetsEnd = 18_000_000_000m, totalAssetsBegin = 12_000_000_000m, equityEnd = 5_000_000_000m, equityBegin = 4_000_000_000m, shortTermAssetsEnd = 17_000_000_000m, longTermAssetsEnd = 1_000_000_000m, totalSourcesEnd = 18_000_000_000m });
            var d7 = JsonDocument.Parse(await c.GetStringAsync("/api/Dashboard/GetPlReport?month=2036-07")).RootElement.GetProperty("data");
            Checks(d7).Should().ContainSingle(x => x.Level == "warn" || x.Level == "good").Which.Text.Should().Be("Đối chiếu không thấy bất thường.");
            var st7 = d7.GetProperty("statement").EnumerateArray().ToDictionary(x => x.GetProperty("code").GetString()!, x => x);
            // LN trước thuế 1.000 − 670 − 51 = 279 trđ; bình quân tổng tài sản (12 + 18) / 2 = 15 tỷ → 1,86 %; quy năm × 12.
            st7["ROA"].GetProperty("month").GetDecimal().Should().Be(1.86m);
            st7["ROA_Y"].GetProperty("month").GetDecimal().Should().Be(22.32m);
            st7["ROE_Y"].GetProperty("month").GetDecimal().Should().Be(st7["ROE"].GetProperty("month").GetDecimal() * 12);

            // Tháng 8: thêm dòng doanh thu không rõ bữa, 642 "khác" lớn, tài sản dài hạn âm, không cân, đầu kỳ ≠ cuối kỳ T7; doanh thu Delicare lệch 20 %.
            var lines8 = Full(1_200_000_000, 40_000_000).Append(L("511", "131", 0, 30_000_000, "Doanh thu suất ăn chiều")).ToArray();
            await Import("2036-08", lines8, new { totalAssetsEnd = 23_600_000_000m, totalAssetsBegin = 18_400_000_000m, equityEnd = 6_000_000_000m, equityBegin = 5_000_000_000m, shortTermAssetsEnd = 23_760_000_000m, longTermAssetsEnd = -160_000_000m, totalSourcesEnd = 23_500_000_000m });
            await _factory.SeedAsync(db =>
            {
                db.KpiMonthlySnapshot.Add(new KpiMonthlySnapshot { Id = Guid.NewGuid(), SnapshotMonth = new DateTime(2036, 8, 1), MetricCode = "PL_EST_REVENUE", MetricVersion = 1, MetricValue = 1_025_000_000m, CalculationVersion = "test", CalculatedAt = DateTime.Now, DataQualityStatus = "ESTIMATE", CreatedDate = DateTime.Now });
                return Task.CompletedTask;
            });
            var d8 = JsonDocument.Parse(await c.GetStringAsync("/api/Dashboard/GetPlReport?month=2036-08")).RootElement.GetProperty("data");
            var ch = Checks(d8);
            ch.Should().Contain(x => x.Level == "warn" && x.Text.StartsWith("30,0 trđ doanh thu (TK 511) chưa xếp được vào bữa") && x.Text.Contains("suất ăn chiều"));
            ch.Should().Contain(x => x.Level == "warn" && x.Text.Contains("chi phí quản lý (TK 642, 40,0 trđ)") && x.Text.Contains("khác / chưa phân loại"));
            ch.Should().Contain(x => x.Level == "warn" && x.Text.StartsWith("Doanh thu sổ AMIS 1.230,0 trđ lệch doanh thu Delicare") && x.Text.Contains("+20,0%"));
            ch.Should().Contain(x => x.Level == "warn" && x.Text.StartsWith("Bảng cân đối: tài sản dài hạn (mã 200) âm"));
            ch.Should().Contain(x => x.Level == "warn" && x.Text.StartsWith("Bảng cân đối không cân"));
            ch.Should().Contain(x => x.Level == "warn" && x.Text.StartsWith("Số đầu kỳ tổng tài sản 18.400,0 trđ khác số cuối kỳ tháng 07/2036 18.000,0 trđ"));
            ch.Should().NotContain(x => x.Text.StartsWith("Số đầu kỳ vốn chủ sở hữu"), "đầu kỳ VCSH T8 = cuối kỳ T7");
            ch.Should().NotContain(x => x.Text == "Đối chiếu không thấy bất thường.");

            // Tháng 9: chỉ nhập 511 → thiếu sổ, chưa có bảng cân đối.
            await Import("2036-09", [L("511", "131", 0, 900_000_000, "suất ăn trưa")], null);
            var ch9 = Checks(JsonDocument.Parse(await c.GetStringAsync("/api/Dashboard/GetPlReport?month=2036-09")).RootElement.GetProperty("data"));
            ch9.Should().Contain(x => x.Level == "warn" && x.Text == "Chưa nhập sổ TK 621, 622, 627, 641, 642 – chưa đủ để tính giá vốn / lợi nhuận của tháng.");
            ch9.Should().Contain(x => x.Level == "info" && x.Text.StartsWith("Chưa nhập bảng cân đối"));

            // Tháng chưa nhập sổ: không có khối đối chiếu.
            Checks(JsonDocument.Parse(await c.GetStringAsync("/api/Dashboard/GetPlReport?month=2036-10")).RootElement.GetProperty("data")).Should().BeEmpty();
        }
    }
}
