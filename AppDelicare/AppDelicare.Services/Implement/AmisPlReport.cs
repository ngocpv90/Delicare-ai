using System.Globalization;
using System.Text.RegularExpressions;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Share;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
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
        /// <summary>RPT-1.4 (D-140 h): nhận xét tự động theo quy tắc (ngưỡng BusinessRules:Pl).</summary>
        public List<PlCommentModel> Comments { get; set; } = [];
    }

    public sealed class PlCommentModel
    {
        /// <summary>good · warn · info.</summary>
        public string Level { get; set; } = "info";
        public string Text { get; set; } = string.Empty;
    }

    public interface IAmisPlReportService
    {
        Task<IResult<PlReportModel>> GetReport(string? month);
        /// <summary>RPT-1.4: xuất tab Kết quả kinh doanh (xlsx / pdf) – thẻ chỉ số, nhận xét, báo cáo KQKD tháng + lũy kế, theo quý.</summary>
        Task<IResult<FileContentModel>> Export(string? month, string? format);
    }

    /// <summary>
    /// RPT-1.3 (D-140 f, i): báo cáo KQKD chi tiết Tháng | Lũy kế năm (bố cục sheet Monthly / YTD, tên chỉ tiêu D-140 f: "Lợi nhuận trước thuế", "Chi phí bán hàng &amp; quản lý"),
    /// bảng theo quý, chuỗi 12 tháng (lớp Chính thức PL_AMIS_* và Tạm tính PL_EST_*), cơ cấu giá vốn. Chỉ đọc KpiMonthlySnapshot (BranchId null) – số do RPT-1.2 nạp.
    /// </summary>
    public sealed class AmisPlReportService(IUnitOfWork unitOfWork, IClock clock, IAmisPlService amis, IDashboardPlService pl, Microsoft.Extensions.Options.IOptions<AppDelicare.Model.Option.BusinessRulesOptions> rules) : IAmisPlReportService
    {
        private readonly AppDelicare.Model.Option.PlRulesOptions _o = rules.Value.Pl ?? new();
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
            model.Comments = await CommentsAsync(model, m0);
            return Result<PlReportModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
        }

        // ---------------------------------------------------------------- RPT-1.4 nhận xét tự động (D-140 h)
        private static string Tr(decimal v) => (v / 1_000_000m).ToString("#,0.0", CultureInfo.GetCultureInfo("vi-VN")) + " trđ";

        private static string Pc(decimal v) => v.ToString("0.0", CultureInfo.GetCultureInfo("vi-VN")) + "%";

        private static decimal? Growth(decimal? cur, decimal? prev) => cur is { } c && prev is { } p && p != 0 ? (c - p) / Math.Abs(p) * 100m : null;

        /// <summary>
        /// Lớp Chính thức (có số AMIS tháng): doanh thu ± so tháng trước / cùng kỳ; giá vốn tăng nhanh hơn doanh thu; % giá vốn / doanh thu; lợi nhuận trước thuế + biên thấp;
        /// 3 khoản chi phí tăng mạnh nhất (621 nhóm NVL, 627, 641, 642). Chưa có số AMIS → nhận xét lớp Tạm tính (doanh thu, % NVL / doanh thu, bao bì).
        /// </summary>
        private async Task<List<PlCommentModel>> CommentsAsync(PlReportModel r, DateTime m0)
        {
            var list = new List<PlCommentModel>();
            void Add(string level, string text) => list.Add(new PlCommentModel { Level = level, Text = text });
            string prevName = $"tháng {m0.AddMonths(-1):MM/yyyy}";
            if (r.HasAmis)
            {
                var st = r.Statement.ToDictionary(l => l.Code, l => l);
                decimal? Cur(string c) => st.TryGetValue(c, out var l) ? l.Month : null;
                decimal? Prev(string c) => st.TryGetValue(c, out var l) ? l.PrevMonth : null;
                decimal rev = Cur("REVENUE") ?? 0;
                var g = Growth(Cur("REVENUE"), Prev("REVENUE"));
                if (g is { } gr && Math.Abs(gr) >= _o.RevenueChangePct)
                {
                    Add(gr > 0 ? "good" : "warn", $"Doanh thu {Tr(rev)}, {(gr > 0 ? "tăng" : "giảm")} {Pc(Math.Abs(gr))} so {prevName}.");
                }
                else if (g != null)
                {
                    Add("info", $"Doanh thu {Tr(rev)}, gần như không đổi so {prevName} ({(g >= 0 ? "+" : "")}{Pc(g.Value)}).");
                }
                int yIdx = r.Months.IndexOf(m0.AddMonths(-12).ToString("yyyy-MM"));
                var ser = r.Series.GetValueOrDefault("AMIS_REVENUE");
                if (yIdx >= 0 && ser != null && Growth(rev, ser[yIdx]) is { } gy)
                {
                    Add(gy >= 0 ? "good" : "warn", $"So cùng kỳ {m0.AddMonths(-12):MM/yyyy}: doanh thu {(gy >= 0 ? "tăng" : "giảm")} {Pc(Math.Abs(gy))}.");
                }
                var gc = Growth(Cur("COGS"), Prev("COGS"));
                if (gc is { } c && g is { } gv && c - gv >= _o.CostGapPct)
                {
                    Add("warn", $"Giá vốn tăng nhanh hơn doanh thu: giá vốn +{Pc(c)}, doanh thu {(gv >= 0 ? "+" : "")}{Pc(gv)} so {prevName}.");
                }
                if (rev != 0 && Cur("COGS") is { } cogs)
                {
                    decimal ratio = cogs / rev * 100m;
                    decimal? prevRatio = Prev("REVENUE") is { } pr && pr != 0 && Prev("COGS") is { } pc ? pc / pr * 100m : null;
                    Add(prevRatio != null && ratio > prevRatio + 0.5m ? "warn" : "info", $"Giá vốn hàng bán = {Pc(ratio)} doanh thu" + (prevRatio is { } p2 ? $" ({prevName}: {Pc(p2)})." : "."));
                }
                if (Cur("PROFIT_BEFORE_TAX") is { } pbt)
                {
                    decimal margin = rev == 0 ? 0 : pbt / rev * 100m;
                    var gp = Growth(pbt, Prev("PROFIT_BEFORE_TAX"));
                    Add(pbt < 0 || margin < _o.MarginWarnPct ? "warn" : "good",
                        $"Lợi nhuận trước thuế {Tr(pbt)} (biên {Pc(margin)} doanh thu){(gp is { } x ? $", {(x >= 0 ? "tăng" : "giảm")} {Pc(Math.Abs(x))} so {prevName}" : string.Empty)}{(margin < _o.MarginWarnPct ? $" – dưới ngưỡng {Pc(_o.MarginWarnPct)}" : string.Empty)}.");
                }
                var up = r.Statement.Where(l => l.Level == 2 && l.Code.Length > 12 && new[] { "I621_", "I627_", "I641_", "I642_" }.Any(a => l.Code.Contains(a)) && l.Month is { } mv && l.PrevMonth is { } pv && pv > 0
                        && mv - pv >= _o.ItemMinAmount && (mv - pv) / pv * 100m >= _o.ItemIncreasePct)
                    .OrderByDescending(l => l.Month!.Value - l.PrevMonth!.Value).Take(3).ToList();
                if (up.Count > 0)
                {
                    Add("warn", "Chi phí tăng mạnh nhất so " + prevName + ": " + string.Join("; ", up.Select(l => $"{l.Name} +{Tr(l.Month!.Value - l.PrevMonth!.Value)} (+{Pc((l.Month!.Value - l.PrevMonth!.Value) / l.PrevMonth!.Value * 100m)})")) + ".");
                }
                return list;
            }
            var est = (await pl.GetPl(m0.ToString("yyyy-MM"), null)).Data;
            if (est == null)
            {
                return list;
            }
            Add("info", "Chưa có số AMIS của tháng – nhận xét theo số tạm tính (Delicare).");
            var card = est.ListCard.ToDictionary(c => c.Code, c => c);
            if (card.GetValueOrDefault("PL_EST_REVENUE") is { Value: { } rv } rc)
            {
                var g = Growth(rv, rc.Prev);
                Add(g is { } x && x <= -_o.RevenueChangePct ? "warn" : g is { } y && y >= _o.RevenueChangePct ? "good" : "info",
                    $"Doanh thu tạm tính {Tr(rv)}{(g is { } z ? $", {(z >= 0 ? "tăng" : "giảm")} {Pc(Math.Abs(z))} so {prevName}" : string.Empty)}.");
            }
            if (card.GetValueOrDefault("PL_EST_FOOD_PCT") is { Value: { } fp } fc)
            {
                Add(fp > _o.FoodCostWarnPct ? "warn" : "info", $"Nguyên vật liệu = {Pc(fp)} doanh thu{(fc.Prev is { } p ? $" ({prevName}: {Pc(p)})" : string.Empty)}{(fp > _o.FoodCostWarnPct ? $" – vượt ngưỡng {Pc(_o.FoodCostWarnPct)}" : string.Empty)}.");
            }
            if (est.ListGroup.Count > 0)
            {
                var top = est.ListGroup.Where(x => x.Prev > 0 && x.Value - x.Prev >= _o.ItemMinAmount && (x.Value - x.Prev) / x.Prev * 100m >= _o.ItemIncreasePct)
                    .OrderByDescending(x => x.Value - x.Prev).Take(3).ToList();
                if (top.Count > 0)
                {
                    Add("warn", "Nhóm NVL tăng mạnh nhất: " + string.Join("; ", top.Select(x => $"{x.Group} +{Tr(x.Value - x.Prev)}")) + ".");
                }
            }
            return list;
        }

        // ---------------------------------------------------------------- RPT-1.4 xuất Excel / PDF
        public async Task<IResult<FileContentModel>> Export(string? month, string? format)
        {
            string fmt = (format ?? PrintRenderer.Xlsx).Trim().ToLowerInvariant() == PrintRenderer.Pdf ? PrintRenderer.Pdf : PrintRenderer.Xlsx;
            var rep = (await GetReport(month)).Data!;
            var cards = (await pl.GetPl(rep.Month, null)).Data;
            DateTime m0 = DateTime.ParseExact(rep.Month, "yyyy-MM", CultureInfo.InvariantCulture);
            string Mn(decimal? v) => v is { } x ? (x / 1_000_000m).ToString("#,0.0", CultureInfo.GetCultureInfo("vi-VN")) : "–";
            string Pp(decimal? a, decimal? b) => a is { } x && b is { } y && y != 0 ? (x / y * 100m).ToString("0.0", CultureInfo.GetCultureInfo("vi-VN")) + "%" : "";
            var revRow = rep.Statement.FirstOrDefault(l => l.Code == "REVENUE");
            var docs = new List<PrintDocumentModel>();
            var doc = new PrintDocumentModel
            {
                Type = "PL", Title = $"KẾT QUẢ KINH DOANH THÁNG {m0:MM/yyyy}", DocumentDate = clock.Today, CompanyName = "CÔNG TY CỔ PHẦN THƯƠNG MẠI DỊCH VỤ DELICARE VN",
                ListInfo =
                [
                    new PrintInfoRowModel { Label = "Kỳ", Value = $"Tháng {m0:MM/yyyy} · lũy kế năm {m0.Year}" },
                    new PrintInfoRowModel { Label = "Lớp số", Value = rep.HasAmis ? "Chính thức (sổ AMIS)" : "Tạm tính (Delicare) – chưa nhập sổ AMIS" },
                    new PrintInfoRowModel { Label = "Đơn vị", Value = "triệu đồng" },
                ],
                ListHeader = ["Chỉ tiêu", $"Tháng {m0:MM/yyyy}", "% doanh thu", $"Lũy kế {m0.Year}", "% doanh thu"],
            };
            foreach (var l in rep.Statement.Where(l => l.Level < 2 || l.Month != null || l.Ytd != null))
            {
                bool ratio = l.Code is "ROA" or "ROE";
                string ind = new(' ', l.Level * 3);
                doc.ListRow.Add(ratio
                    ? [ind + l.Name, l.Month is { } rv ? rv.ToString("0.00", CultureInfo.GetCultureInfo("vi-VN")) + "%" : "–", "", "", ""]
                    : [ind + (l.Level == 0 ? l.Name.ToUpperInvariant() : l.Name), Mn(l.Month), Pp(l.Month, revRow?.Month), Mn(l.Ytd), Pp(l.Ytd, revRow?.Ytd)]);
            }
            doc.ListNote.AddRange(rep.Comments.Select(c => (c.Level == "warn" ? "⚠ " : c.Level == "good" ? "✓ " : "• ") + c.Text));
            if (cards != null)
            {
                doc.ListNote.Add("Thẻ chỉ số: " + string.Join(" · ", cards.ListCard.Where(c => c.Value != null).Select(c => $"{c.Name} {(c.Unit == "PCT" ? c.Value!.Value.ToString("0.0", CultureInfo.GetCultureInfo("vi-VN")) + "%" : c.Unit == "MEAL" ? c.Value!.Value.ToString("#,0", CultureInfo.GetCultureInfo("vi-VN")) + " suất" : Mn(c.Value) + " trđ")}")));
            }
            docs.Add(doc);
            var q = new PrintDocumentModel
            {
                Type = "PL", Title = $"KẾT QUẢ KINH DOANH THEO QUÝ – NĂM {m0.Year}", DocumentDate = clock.Today, CompanyName = doc.CompanyName,
                ListInfo = [new PrintInfoRowModel { Label = "Đơn vị", Value = "triệu đồng" }],
                ListHeader = ["Chỉ tiêu", .. rep.Quarters.Select(x => x.Quarter + (x.MonthsWithData is > 0 and < 3 ? $" ({x.MonthsWithData} tháng)" : string.Empty))],
            };
            foreach (var (name, f) in new (string, Func<PlQuarterModel, decimal?>)[] { ("Doanh thu", x => x.Revenue), ("Giá vốn hàng bán", x => x.Cogs), ("Lợi nhuận gộp", x => x.GrossProfit), ("Chi phí bán hàng & quản lý", x => x.SellingAdmin), ("Lợi nhuận thuần", x => x.NetOperating), ("Lợi nhuận trước thuế", x => x.ProfitBeforeTax) })
            {
                q.ListRow.Add([name, .. rep.Quarters.Select(x => Mn(f(x)))]);
            }
            docs.Add(q);
            return Result<FileContentModel>.Success(new FileContentModel
            {
                Content = PrintRenderer.Render(docs, fmt), ContentType = PrintRenderer.ContentTypeOf(fmt), FileName = $"Ket-qua-kinh-doanh-{m0:yyyy-MM}.{fmt}",
            }, ConstantHelper.Message.GetAllSuccessfully);
        }
    }
}
