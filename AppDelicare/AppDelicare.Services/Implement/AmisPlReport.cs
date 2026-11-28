using System.Globalization;
using System.Text.RegularExpressions;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Share;
using AppDelicare.Model.Helper;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Implement
{
    /// <summary>RPT-1.3: 1 dòng báo cáo KQKD – Level 0 = chỉ tiêu chính (in đậm), 1 = nhóm, 2 = khoản mục chi tiết.</summary>
    public sealed class PlStatementLine
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Level { get; set; }
        public decimal? Month { get; set; }
        public decimal? Ytd { get; set; }
        public decimal? PrevMonth { get; set; }
    }

    public sealed class PlQuarterModel
    {
        public string Quarter { get; set; } = string.Empty;
        public int MonthsWithData { get; set; }
        public decimal? Revenue { get; set; }
        public decimal? Cogs { get; set; }
        public decimal? GrossProfit { get; set; }
        public decimal? SellingAdmin { get; set; }
        public decimal? NetOperating { get; set; }
        public decimal? ProfitBeforeTax { get; set; }
    }

    public sealed class PlReportModel
    {
        public string Month { get; set; } = string.Empty;
        public int Year { get; set; }
        /// <summary>12 tháng kết thúc tại tháng đang xem (yyyy-MM).</summary>
        public List<string> Months { get; set; } = [];
        /// <summary>Chuỗi 12 tháng theo mã (không tiền tố): AMIS_REVENUE, AMIS_COGS, … EST_PL_EST_REVENUE …; null = chưa có số.</summary>
        public Dictionary<string, List<decimal?>> Series { get; set; } = [];
        public List<PlStatementLine> Statement { get; set; } = [];
        public List<PlQuarterModel> Quarters { get; set; } = [];
        /// <summary>Cơ cấu giá vốn hàng bán tháng đang xem (khoản mục → số).</summary>
        public List<AmisPlItemModel> CogsMix { get; set; } = [];
        public bool HasAmis { get; set; }
    }

    public interface IAmisPlReportService
    {
        Task<IResult<PlReportModel>> GetReport(string? month);
    }

    /// <summary>
    /// RPT-1.3 (D-140 f, i): báo cáo KQKD chi tiết Tháng | Lũy kế năm (bố cục sheet Monthly / YTD, tên chỉ tiêu D-140 f: "Lợi nhuận trước thuế", "Chi phí bán hàng &amp; quản lý"),
    /// bảng theo quý, chuỗi 12 tháng (lớp Chính thức PL_AMIS_* và Tạm tính PL_EST_*), cơ cấu giá vốn. Chỉ đọc KpiMonthlySnapshot (BranchId null) – số do RPT-1.2 nạp.
    /// </summary>
    public sealed class AmisPlReportService(IUnitOfWork unitOfWork, IClock clock, IAmisPlService amis) : IAmisPlReportService
    {
        private static readonly (string Code, string Name)[] Main =
        [
            ("REVENUE", "Doanh thu"), ("COGS", "Giá vốn hàng bán"), ("GROSS_PROFIT", "Lợi nhuận gộp"), ("SELLING_ADMIN", "Chi phí bán hàng & quản lý"), ("NET_OPERATING", "Lợi nhuận thuần"),
            ("OTHER_INCOME", "Thu nhập khác"), ("OTHER_EXPENSE", "Chi phí khác"), ("PROFIT_BEFORE_TAX", "Lợi nhuận trước thuế"), ("ROA", "ROA (theo tháng)"), ("ROE", "ROE (theo tháng)"),
        ];

        public async Task<IResult<PlReportModel>> GetReport(string? month)
        {
            DateTime m0 = DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var mm) ? mm : new DateTime(clock.Today.Year, clock.Today.Month, 1);
            DateTime from = new[] { m0.AddMonths(-11), new DateTime(m0.Year, 1, 1) }.Min(), y0 = new DateTime(m0.Year, 1, 1);
            var rows = await unitOfWork.DbContext.KpiMonthlySnapshot.AsNoTracking()
                .Where(k => k.BranchId == null && !k.IsDeleted && k.SnapshotMonth >= from && k.SnapshotMonth <= m0 && (k.MetricCode.StartsWith(AmisPlService.Prefix) || k.MetricCode.StartsWith(AmisPlService.EstPrefix)))
                .Select(k => new { k.SnapshotMonth, k.MetricCode, k.MetricValue, k.MetricVersion }).ToListAsync();
            var v = rows.GroupBy(r => (r.SnapshotMonth, r.MetricCode)).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.MetricVersion).First().MetricValue);
            decimal? Get(DateTime m, string code) => v.TryGetValue((m, code), out var x) ? x : null;
            string A(string k) => AmisPlService.Prefix + k;

            var months = Enumerable.Range(0, 12).Select(i => m0.AddMonths(i - 11)).ToList();
            var model = new PlReportModel { Month = m0.ToString("yyyy-MM"), Year = m0.Year, Months = [.. months.Select(m => m.ToString("yyyy-MM"))], HasAmis = Get(m0, A("REVENUE")) != null };
            foreach (var (code, _) in Main)
            {
                model.Series["AMIS_" + code] = [.. months.Select(m => Get(m, A(code)))];
            }
            foreach (string code in rows.Where(r => r.MetricCode.StartsWith(AmisPlService.EstPrefix)).Select(r => r.MetricCode).Distinct())
            {
                model.Series["EST_" + code] = [.. months.Select(m => Get(m, code))];
            }

            // Báo cáo tháng + lũy kế năm (tháng có số AMIS).
            var ytdMonths = Enumerable.Range(0, m0.Month).Select(i => y0.AddMonths(i)).ToList();
            decimal? Ytd(string code) { var xs = ytdMonths.Select(m => Get(m, code)).Where(x => x != null).ToList(); return xs.Count == 0 ? null : xs.Sum(); }
            var rules = (await amis.GetRules()).ListData ?? [];
            var names = rules.GroupBy(r => $"{AmisPlService.Prefix}I{r.Account}_{AmisPlService.Slug(r.Item)}").ToDictionary(g => g.Key, g => g.First().Item);
            List<PlStatementLine> Items(string acc)
            {
                var codes = rows.Where(r => r.MetricCode.StartsWith($"{AmisPlService.Prefix}I{acc}_", StringComparison.Ordinal) && r.SnapshotMonth >= y0).Select(r => r.MetricCode).Distinct();
                return [.. codes.Select(c => new PlStatementLine { Code = c, Name = AmisPlService.MetricName(c, names), Level = 2, Month = Get(m0, c), Ytd = Ytd(c), PrevMonth = Get(m0.AddMonths(-1), c) })
                    .Where(l => (l.Month ?? 0) != 0 || (l.Ytd ?? 0) != 0).OrderByDescending(l => Math.Abs(l.Ytd ?? 0))];
            }
            PlStatementLine Line(string code, string name, int level, bool ratio = false) => new()
            {
                Code = code, Name = name, Level = level, Month = Get(m0, A(code)), Ytd = ratio ? null : Ytd(A(code)), PrevMonth = Get(m0.AddMonths(-1), A(code)),
            };
            var st = model.Statement;
            st.Add(Line("REVENUE", "Doanh thu bán hàng", 0)); st.AddRange(Items("511"));
            st.Add(Line("COGS", "Giá vốn hàng bán", 0));
            st.Add(Line("ACC_621", "Nguyên vật liệu trực tiếp", 1)); st.AddRange(Items("621"));
            st.Add(Line("ACC_622", "Nhân công trực tiếp", 1));
            st.Add(Line("ACC_627", "Chi phí sản xuất chung", 1)); st.AddRange(Items("627"));
            st.Add(Line("GROSS_PROFIT", "Lợi nhuận gộp", 0));
            st.Add(Line("SELLING_ADMIN", "Chi phí bán hàng & quản lý", 0));
            st.Add(Line("ACC_641", "Chi phí bán hàng", 1)); st.AddRange(Items("641"));
            st.Add(Line("ACC_642", "Chi phí quản lý doanh nghiệp", 1)); st.AddRange(Items("642"));
            st.Add(Line("NET_OPERATING", "Lợi nhuận thuần", 0));
            st.Add(Line("OTHER_INCOME", "Thu nhập khác", 1)); st.AddRange(Items("711"));
            st.Add(Line("OTHER_EXPENSE", "Chi phí khác", 1)); st.AddRange(Items("811"));
            st.Add(Line("PROFIT_BEFORE_TAX", "Lợi nhuận trước thuế", 0));
            st.Add(Line("ROA", "ROA (theo tháng)", 1, ratio: true));
            st.Add(Line("ROE", "ROE (theo tháng)", 1, ratio: true));

            // Theo quý của năm.
            for (int q = 1; q <= 4; q++)
            {
                var qm = Enumerable.Range(0, 3).Select(i => new DateTime(m0.Year, (q - 1) * 3 + 1 + i, 1)).Where(x => x <= m0).ToList();
                decimal? S(string code) { var xs = qm.Select(x => Get(x, A(code))).Where(x => x != null).ToList(); return xs.Count == 0 ? null : xs.Sum(); }
                model.Quarters.Add(new PlQuarterModel
                {
                    Quarter = "Q" + q, MonthsWithData = qm.Count(x => Get(x, A("REVENUE")) != null), Revenue = S("REVENUE"), Cogs = S("COGS"), GrossProfit = S("GROSS_PROFIT"),
                    SellingAdmin = S("SELLING_ADMIN"), NetOperating = S("NET_OPERATING"), ProfitBeforeTax = S("PROFIT_BEFORE_TAX"),
                });
            }

            // Cơ cấu giá vốn tháng: NVL theo nhóm (621), nhân công (622), sản xuất chung theo khoản mục (627).
            var mix = new List<AmisPlItemModel>();
            foreach (var l in Items("621")) mix.Add(new AmisPlItemModel { Account = "621", Item = l.Name, Value = l.Month ?? 0 });
            if (Get(m0, A("ACC_622")) is { } labor) mix.Add(new AmisPlItemModel { Account = "622", Item = "Nhân công trực tiếp", Value = labor });
            foreach (var l in Items("627")) mix.Add(new AmisPlItemModel { Account = "627", Item = l.Name, Value = l.Month ?? 0 });
            model.CogsMix = [.. mix.Where(x => x.Value != 0).OrderByDescending(x => x.Value)];
            return Result<PlReportModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
        }
    }
}
