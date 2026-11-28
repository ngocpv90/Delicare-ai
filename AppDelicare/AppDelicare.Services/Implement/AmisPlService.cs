using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Services.Jobs;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace AppDelicare.Services.Implement
{
    // ======================================================================= mô hình

    /// <summary>RPT-1.2: 1 dòng sổ chi tiết AMIS đã chuẩn hóa (TK, TK đối ứng, PS Nợ, PS Có, diễn giải).</summary>
    public sealed class AmisLedgerLine
    {
        public string Account { get; set; } = string.Empty;
        public string? CounterAccount { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public string? Description { get; set; }
        public string? DocNo { get; set; }
        public DateTime? Date { get; set; }
    }

    /// <summary>RPT-1.2: số bảng cân đối (B01-DN) – mã 280 tổng tài sản, 400 vốn chủ sở hữu, cuối kỳ / đầu kỳ.</summary>
    public sealed class AmisBalance
    {
        public decimal? TotalAssetsEnd { get; set; }
        public decimal? TotalAssetsBegin { get; set; }
        public decimal? EquityEnd { get; set; }
        public decimal? EquityBegin { get; set; }
        /// <summary>RPT-1.5 (đối chiếu): mã 100 tài sản ngắn hạn, 200 tài sản dài hạn, 440 tổng nguồn vốn – cuối kỳ.</summary>
        public decimal? ShortTermAssetsEnd { get; set; }
        public decimal? LongTermAssetsEnd { get; set; }
        public decimal? TotalSourcesEnd { get; set; }
    }

    public sealed class AmisPlImportRequest
    {
        /// <summary>yyyy-MM.</summary>
        public string? Month { get; set; }
        public List<AmisLedgerLine> Lines { get; set; } = [];
        public AmisBalance? Balance { get; set; }
        public string? Source { get; set; }
    }

    /// <summary>Quy tắc phân loại: TK (621, 627, 641, 642, 511…) + từ khóa trong diễn giải (không dấu) HOẶC TK đối ứng → khoản mục. Thứ tự = ưu tiên.</summary>
    public sealed class AmisPlRule
    {
        public string Account { get; set; } = string.Empty;
        public string? Keyword { get; set; }
        public string? Counter { get; set; }
        public string Item { get; set; } = string.Empty;
    }

    public sealed class AmisPlItemModel
    {
        public string Account { get; set; } = string.Empty;
        public string Item { get; set; } = string.Empty;
        public decimal Value { get; set; }
    }

    public sealed class AmisPlUnclassifiedModel
    {
        public string Account { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? CounterAccount { get; set; }
        public decimal Amount { get; set; }
        public int Count { get; set; }
    }

    public sealed class AmisPlMonthModel
    {
        public string Month { get; set; } = string.Empty;
        public bool PeriodLocked { get; set; }
        /// <summary>Chỉ tiêu chính (mã → giá trị VND hoặc % với ROA / ROE).</summary>
        public Dictionary<string, decimal> Metrics { get; set; } = [];
        public List<string> Accounts { get; set; } = [];
        public List<AmisPlItemModel> ListItem { get; set; } = [];
        public List<AmisPlUnclassifiedModel> ListUnclassified { get; set; } = [];
        public DateTime? ImportedAt { get; set; }
        public string? ImportedBy { get; set; }
        public string? Source { get; set; }
        public List<string> Messages { get; set; } = [];
    }

    /// <summary>
    /// RPT-1.2 (D-140 d, e): nhập <b>sổ AMIS</b> của tháng → chỉ tiêu KQKD lớp <b>Chính thức</b>. Không bảng / cột mới:
    /// số lưu <c>KpiMonthlySnapshot</c> (BranchId null, MetricVersion 1) mã <c>PL_AMIS_*</c> (định nghĩa <c>KpiMetricDefinition</c> DomainCode PL, IsActive = 0 – không lên tab Chỉ số);
    /// quy tắc phân loại + dòng chưa phân loại lưu <c>AuditChange</c> (EntityName <c>AmisPlRules</c> / <c>AmisPlImport</c>).
    /// Quy tắc cộng (khớp file báo cáo kế toán): DT = PS Có 511; GVHB = PS Nợ 621 + 622 + 627; CP bán hàng &amp; QLDN = PS Nợ 641 + 642; TN khác = PS Có 711; CP khác = PS Nợ 811;
    /// bỏ bút toán kết chuyển (TK đối ứng 911 / 154). LN gộp = DT − GVHB; LN thuần = LN gộp − CP; LN trước thuế = LN thuần + TN khác − CP khác;
    /// ROA / ROE theo tháng = LN trước thuế ÷ bình quân (đầu + cuối kỳ) tổng tài sản / vốn chủ sở hữu (B01-DN mã 280 / 400).
    /// Nhập lại = ghi đè đúng các tài khoản có trong lần nhập (không nhân đôi); tháng đã khóa kỳ: chỉ người có quyền Nhập (Kế toán).
    /// </summary>
    public interface IAmisPlService
    {
        Task<IResult<AmisPlMonthModel>> Import(AmisPlImportRequest request);
        Task<IResult<AmisPlMonthModel>> ImportExcel(string? month, IReadOnlyList<(string FileName, Stream Stream)> files);
        Task<IResult<AmisPlMonthModel>> GetMonth(string? month);
        Task<IResults<AmisPlRule>> GetRules();
        Task<IResult<bool>> SaveRules(List<AmisPlRule> rules);
        /// <summary>Lớp Tạm tính: chụp thẻ Delicare của tháng vào KpiMonthlySnapshot PL_EST_* (job hằng đêm).</summary>
        Task<int> SnapshotEstimateAsync(DateTime month);
    }

    public sealed class AmisPlService(IUnitOfWork unitOfWork, IClock clock, ICurrentActor currentActor, IDashboardPlService pl) : IAmisPlService
    {
        public const string Prefix = "PL_AMIS_";
        public const string EstPrefix = "PL_EST_";
        public const string RulesEntity = "AmisPlRules";
        public const string ImportEntity = "AmisPlImport";
        public static readonly string[] Accounts = ["511", "621", "622", "627", "641", "642", "711", "811"];
        private static readonly string[] Closing = ["911", "154"];

        /// <summary>Tên khoản mục theo TK (chỉ tiêu cha) – D-140 f.</summary>
        public static readonly IReadOnlyDictionary<string, string> AccountName = new Dictionary<string, string>
        {
            ["511"] = "Doanh thu", ["621"] = "Nguyên vật liệu trực tiếp", ["622"] = "Nhân công trực tiếp", ["627"] = "Chi phí sản xuất chung",
            ["641"] = "Chi phí bán hàng", ["642"] = "Chi phí quản lý doanh nghiệp", ["711"] = "Thu nhập khác", ["811"] = "Chi phí khác",
        };

        /// <summary>Quy tắc mặc định (khởi tạo – Kế toán sửa trên màn; công cụ tools/rpt1 nạp thêm bảng tên hàng → nhóm NVL từ sheet Ref).</summary>
        public static List<AmisPlRule> DefaultRules() =>
        [
            R("511", "sang", "Bữa sáng"), R("511", "trua", "Bữa trưa"), R("511", "toi", "Bữa tối"), R("511", "dem", "Bữa đêm"),
            R("511", "com suat", "Cơm suất"), R("511", "50.000|50000|50k", "Suất ăn 50.000"), R("511", "com trang", "Cơm trắng"), R("511", "giam tru", "Giảm trừ doanh thu"),
            R("621", "lon|heo|ba chi|nac vai|suon|chan gio", "Thịt lợn"), R("621", "bo ", "Thịt bò"), R("621", "trau", "Thịt trâu"), R("621", "ga |vit|canh ga|dui ga|uc ga", "Thịt gia cầm"),
            R("621", "ca |tom|muc|ngao|hai san", "Thủy hải sản"), R("621", "trung", "Trứng"), R("621", "gao", "Gạo"), R("621", "cha |gio ", "Giò chả"),
            R("621", "muoi|hat nem|mi chinh|duong|nuoc mam|dau an|dau hao|tuong|bot canh|tieu|toi |hanh kho", "Gia vị"), R("621", "chuoi|cam |tao|dua hau|buoi|thanh long|hoa qua", "Hoa quả"),
            R("621", "sua|banh|xoi|xuc xich", "Đồ ăn sáng mua sẵn"), R("621", "rau|cai|bi |cu |qua", "Rau củ quả"), R("621", "bot|mien|bun|pho|dau xanh", "Đồ khô"),
            C("627", "2141", "Khấu hao TSCĐ"), C("627", "3383", "BHXH"), C("627", "153", "Đũa, thìa, hộp…"),
            R("627", "dien", "Điện"), R("627", "nuoc sach|nuoc", "Nước sạch"), R("627", "gas", "Gas"), R("627", "ve sinh|rua|nuoc tay|khau trang", "Đồ dùng vệ sinh"), R("627", "dung cu|thiet bi", "Dụng cụ"),
            C("641", "334", "Lương"), C("641", "2141", "Khấu hao TSCĐ"), R("641", "xang|dau", "Xăng dầu"), R("641", "quang cao|mkt|marketing|chup anh|quay phim|in an", "Marketing"), R("641", "tiep khach|cong tac", "Tiếp khách, công tác"),
            C("642", "334", "Lương"), C("642", "2141", "Khấu hao TSCĐ"), C("642", "3383", "Bảo hiểm xã hội"), C("642", "242", "Phân bổ chi phí"), R("642", "thue nha|thue van phong", "Thuê nhà xưởng"),
            R("642", "phi ngan hang|phi quan ly tai khoan|phi chuyen", "Phí ngân hàng"), R("642", "tiep khach|cong tac", "Tiếp khách, công tác"),
        ];

        private static AmisPlRule R(string acc, string kw, string item) => new() { Account = acc, Keyword = kw, Item = item };
        private static AmisPlRule C(string acc, string counter, string item) => new() { Account = acc, Counter = counter, Item = item };

        private static Result<T> Status<T>(int code, string message) => new() { ResponseCode = code, ResponseMessage = message };

        /// <summary>Không dấu, thường, gộp khoảng trắng – so khớp từ khóa.</summary>
        public static string Fold(string? s)
        {
            string n = (s ?? string.Empty).Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(n.Length);
            foreach (char ch in n)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(char.ToLowerInvariant(ch));
                }
            }
            return " " + Regex.Replace(sb.ToString(), @"\s+", " ").Trim() + " ";
        }

        public static string Slug(string s)
        {
            string f = Fold(s).Trim().ToUpperInvariant();
            string slug = Regex.Replace(f, "[^A-Z0-9]+", "_").Trim('_');
            return slug.Length > 60 ? slug[..60] : slug;
        }

        /// <summary>Khoản mục của 1 dòng theo quy tắc (null = chưa phân loại).</summary>
        public static string? Classify(IReadOnlyList<AmisPlRule> rules, AmisLedgerLine l)
        {
            string desc = Fold(l.Description);
            foreach (var r in rules.Where(r => r.Account == l.Account))
            {
                if (!string.IsNullOrWhiteSpace(r.Counter) && (l.CounterAccount ?? string.Empty).Trim().StartsWith(r.Counter.Trim(), StringComparison.Ordinal))
                {
                    return r.Item;
                }
                if (!string.IsNullOrWhiteSpace(r.Keyword) && r.Keyword.Split('|', StringSplitOptions.RemoveEmptyEntries).Any(k => Fold(k).Trim().Length > 0 && desc.Contains(Fold(k), StringComparison.Ordinal))) // nguyên từ / cụm từ (Fold đệm khoảng trắng 2 đầu)
                {
                    return r.Item;
                }
            }
            return null;
        }

        /// <summary>Số phát sinh của dòng theo phía tài khoản (doanh thu / thu nhập = Có, chi phí = Nợ); bút toán kết chuyển (911 / 154) = 0.</summary>
        public static decimal Amount(AmisLedgerLine l)
        {
            if (Closing.Any(c => (l.CounterAccount ?? string.Empty).Trim().StartsWith(c, StringComparison.Ordinal)))
            {
                return 0;
            }
            return l.Account is "511" or "711" ? l.Credit - l.Debit : l.Debit - l.Credit;
        }

        // ---------------------------------------------------------------- quy tắc
        public async Task<IResults<AmisPlRule>> GetRules() => Results<AmisPlRule>.Success(await RulesAsync(), ConstantHelper.Message.GetAllSuccessfully);

        private async Task<List<AmisPlRule>> RulesAsync()
        {
            var last = await LatestAsync<RulesDoc>(RulesEntity, Guid.Empty);
            return last?.Rules is { Count: > 0 } list ? list : DefaultRules();
        }

        public async Task<IResult<bool>> SaveRules(List<AmisPlRule> rules)
        {
            var actor = currentActor.RequireActor();
            var clean = (rules ?? []).Select(r => new AmisPlRule
            {
                Account = (r.Account ?? string.Empty).Trim(), Keyword = string.IsNullOrWhiteSpace(r.Keyword) ? null : r.Keyword.Trim(),
                Counter = string.IsNullOrWhiteSpace(r.Counter) ? null : r.Counter.Trim(), Item = (r.Item ?? string.Empty).Trim(),
            }).Where(r => r.Account.Length > 0 && r.Item.Length > 0 && (r.Keyword != null || r.Counter != null)).ToList();
            if (clean.Count == 0)
            {
                return Result<bool>.Fail("Cần ít nhất 1 quy tắc có Tài khoản, Khoản mục và Từ khóa hoặc TK đối ứng");
            }
            if (clean.FirstOrDefault(r => !Accounts.Contains(r.Account)) is { } bad)
            {
                return Result<bool>.Fail($"Tài khoản {bad.Account} không thuộc các sổ nhập ({string.Join(", ", Accounts)})");
            }
            int seq = ((await LatestAsync<RulesDoc>(RulesEntity, Guid.Empty))?.Seq ?? 0) + 1;
            unitOfWork.DbContext.AuditChange.Add(new AuditChange
            {
                Id = Guid.NewGuid(), IdChange = Guid.Empty, EntityName = RulesEntity, Action = "Saved", Changes = JsonConvert.SerializeObject(new RulesDoc { Seq = seq, Rules = clean }),
                CreatedDate = clock.Now, CreatedBy = actor.UserId, IsDeleted = false,
            });
            await unitOfWork.DbContext.SaveChangesAsync();
            return Result<bool>.Success(true, $"Đã lưu {clean.Count} quy tắc phân loại – áp dụng từ lần nhập sổ sau (nhập lại sổ để phân loại lại tháng cũ)");
        }

        // ---------------------------------------------------------------- nhập
        /// <summary>AuditChange chỉ ghi thêm → mỗi bản mang số thứ tự tăng dần; bản hiệu lực = Seq lớn nhất.</summary>
        private interface ISeq
        {
            int Seq { get; }
        }

        private sealed class RulesDoc : ISeq
        {
            public int Seq { get; set; }
            public List<AmisPlRule> Rules { get; set; } = [];
        }

        private async Task<T?> LatestAsync<T>(string entity, Guid id) where T : class, ISeq
        {
            var all = await unitOfWork.DbContext.AuditChange.AsNoTracking().Where(a => a.EntityName == entity && a.IdChange == id && !a.IsDeleted).Select(a => new { a.Changes, a.CreatedDate }).ToListAsync();
            T? best = null;
            DateTime bestAt = DateTime.MinValue;
            foreach (var a in all)
            {
                try
                {
                    if (JsonConvert.DeserializeObject<T>(a.Changes ?? "{}") is { } d && (best == null || d.Seq > best.Seq || (d.Seq == best.Seq && a.CreatedDate > bestAt)))
                    {
                        best = d;
                        bestAt = a.CreatedDate;
                    }
                }
                catch (JsonException)
                {
                }
            }
            return best;
        }

        private sealed class ImportLog : ISeq
        {
            public int Seq { get; set; }
            public string Month { get; set; } = string.Empty;
            public List<string> Accounts { get; set; } = [];
            public List<AmisPlUnclassifiedModel> Unclassified { get; set; } = [];
            public string? Source { get; set; }
            public DateTime? ImportedAt { get; set; }
            public string? ImportedBy { get; set; }
        }

        public async Task<IResult<AmisPlMonthModel>> Import(AmisPlImportRequest request)
        {
            var actor = currentActor.RequireActor();
            if (!DateTime.TryParseExact(request.Month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var m0))
            {
                return Result<AmisPlMonthModel>.Fail("Thiếu tháng (yyyy-MM)");
            }
            var lines = request.Lines.Where(l => Accounts.Contains((l.Account ?? string.Empty).Trim())).ToList();
            if (lines.Count == 0 && request.Balance == null)
            {
                return Result<AmisPlMonthModel>.Fail("Không có dòng sổ nào của các tài khoản " + string.Join(", ", Accounts));
            }
            if (lines.FirstOrDefault(l => l.Date is { } d && (d.Year != m0.Year || d.Month != m0.Month)) is { } other)
            {
                return Result<AmisPlMonthModel>.Fail($"Sổ TK {other.Account} có chứng từ ngày {other.Date:dd/MM/yyyy} – không thuộc tháng {m0:MM/yyyy}");
            }
            var rules = await RulesAsync();
            var db = unitOfWork.DbContext;
            DateTime now = clock.Now;
            var values = new Dictionary<string, decimal>();
            var unclassified = new List<AmisPlUnclassifiedModel>();
            var accounts = lines.Select(l => l.Account.Trim()).Distinct().OrderBy(a => a).ToList();
            foreach (string acc in accounts)
            {
                var mine = lines.Where(l => l.Account.Trim() == acc).ToList();
                values[$"{Prefix}ACC_{acc}"] = mine.Sum(Amount);
                foreach (var g in mine.Where(l => Amount(l) != 0).GroupBy(l => Classify(rules, l)))
                {
                    if (g.Key == null)
                    {
                        unclassified.AddRange(g.GroupBy(l => (Fold(l.Description).Trim(), l.CounterAccount)).Select(x => new AmisPlUnclassifiedModel
                        {
                            Account = acc, Description = x.First().Description, CounterAccount = x.Key.CounterAccount, Amount = x.Sum(Amount), Count = x.Count(),
                        }));
                        values[$"{Prefix}I{acc}_KHAC"] = values.GetValueOrDefault($"{Prefix}I{acc}_KHAC") + g.Sum(Amount);
                    }
                    else
                    {
                        string code = $"{Prefix}I{acc}_{Slug(g.Key)}";
                        values[code] = values.GetValueOrDefault(code) + g.Sum(Amount);
                    }
                }
            }
            var names = rules.GroupBy(r => $"{Prefix}I{r.Account}_{Slug(r.Item)}").ToDictionary(g => g.Key, g => g.First().Item);
            if (request.Balance is { } b)
            {
                void Put(string k, decimal? v) { if (v is { } x) values[$"{Prefix}{k}"] = x; }
                Put("TOTAL_ASSETS_END", b.TotalAssetsEnd); Put("TOTAL_ASSETS_BEGIN", b.TotalAssetsBegin); Put("EQUITY_END", b.EquityEnd); Put("EQUITY_BEGIN", b.EquityBegin);
                Put("ST_ASSETS_END", b.ShortTermAssetsEnd); Put("LT_ASSETS_END", b.LongTermAssetsEnd); Put("TOTAL_SOURCES_END", b.TotalSourcesEnd);
            }

            // Ghi đè số của các tài khoản / bảng cân đối có trong lần nhập (mã cũ của tài khoản đó không còn → xóa mềm).
            var existing = await db.KpiMonthlySnapshot.Where(k => k.SnapshotMonth == m0 && k.BranchId == null && k.MetricCode.StartsWith(Prefix) && !k.IsDeleted).ToListAsync();
            bool Owned(string code) => accounts.Any(a => code == $"{Prefix}ACC_{a}" || code.StartsWith($"{Prefix}I{a}_", StringComparison.Ordinal))
                || (request.Balance != null && BalanceCodes.Any(b => code.StartsWith(Prefix + b, StringComparison.Ordinal)));
            foreach (var k in existing.Where(k => Owned(k.MetricCode) && !values.ContainsKey(k.MetricCode)))
            {
                k.IsDeleted = true; k.ModifiedDate = now; k.ModifiedBy = actor.UserId;
            }
            // Chỉ tiêu suy ra – tính lại từ số đang lưu (sau khi ghi đè).
            var all = existing.Where(k => !k.IsDeleted && !Owned(k.MetricCode)).ToDictionary(k => k.MetricCode, k => k.MetricValue ?? 0m);
            foreach (var kv in values)
            {
                all[kv.Key] = kv.Value;
            }
            foreach (var kv in Derive(all))
            {
                values[kv.Key] = kv.Value;
            }
            var derivedNow = DeriveCodes.Where(c => !values.ContainsKey(c)).ToList();
            foreach (var k in existing.Where(k => derivedNow.Contains(k.MetricCode) && !k.IsDeleted))
            {
                k.IsDeleted = true; k.ModifiedDate = now; k.ModifiedBy = actor.UserId;
            }
            await EnsureDefinitionsAsync(values.Keys, names, now);
            foreach (var kv in values)
            {
                var row = existing.FirstOrDefault(k => k.MetricCode == kv.Key);
                if (row == null)
                {
                    row = await db.KpiMonthlySnapshot.FirstOrDefaultAsync(k => k.SnapshotMonth == m0 && k.BranchId == null && k.MetricCode == kv.Key && k.MetricVersion == 1);
                }
                if (row == null)
                {
                    db.KpiMonthlySnapshot.Add(new KpiMonthlySnapshot
                    {
                        Id = Guid.NewGuid(), SnapshotMonth = m0, BranchId = null, MetricCode = kv.Key, MetricVersion = 1, MetricValue = kv.Value, CalculationVersion = "RPT-1.2",
                        CalculatedAt = now, SourceWatermark = Cut(request.Source, 100), DataQualityStatus = "COMPLETE", CreatedDate = now, CreatedBy = actor.UserId, IsDeleted = false,
                    });
                }
                else
                {
                    row.MetricValue = kv.Value; row.IsDeleted = false; row.CalculatedAt = now; row.CalculationVersion = "RPT-1.2"; row.SourceWatermark = Cut(request.Source, 100);
                    row.DataQualityStatus = "COMPLETE"; row.ModifiedDate = now; row.ModifiedBy = actor.UserId;
                }
            }
            var prevLog = await LastLogAsync(m0);
            var log = new ImportLog
            {
                Seq = (prevLog?.Seq ?? 0) + 1, Month = m0.ToString("yyyy-MM"), Source = request.Source, ImportedAt = now,
                ImportedBy = await db.User.AsNoTracking().Where(x => x.Id == actor.UserId).Select(x => x.UserName).FirstOrDefaultAsync(),
                Accounts = [.. (prevLog?.Accounts ?? []).Union(accounts).Union(request.Balance != null ? ["B01"] : []).Distinct().OrderBy(x => x)],
                Unclassified = [.. (prevLog?.Unclassified ?? []).Where(u => !accounts.Contains(u.Account)).Concat(unclassified).OrderBy(u => u.Account).ThenByDescending(u => Math.Abs(u.Amount))],
            };
            db.AuditChange.Add(new AuditChange
            {
                Id = Guid.NewGuid(), IdChange = PeriodId(m0), EntityName = ImportEntity, Action = "Imported", Changes = JsonConvert.SerializeObject(log),
                CreatedDate = now, CreatedBy = actor.UserId, IsDeleted = false,
            });
            await db.SaveChangesAsync();
            var res = (await GetMonth(m0.ToString("yyyy-MM"))).Data!;
            res.Messages.Add($"Đã nhập {lines.Count} dòng sổ ({string.Join(", ", accounts)}){(request.Balance != null ? " + bảng cân đối" : string.Empty)} tháng {m0:MM/yyyy}"
                + (unclassified.Count > 0 ? $" · {unclassified.Count} nhóm dòng chưa phân loại (tính vào Khác) – thêm quy tắc rồi nhập lại" : string.Empty));
            return Result<AmisPlMonthModel>.Success(res, res.Messages[0]);
        }

        /// <summary>Mã bảng cân đối (tiền tố) – nhập B01 = ghi đè cả nhóm.</summary>
        private static readonly string[] BalanceCodes = ["TOTAL_ASSETS_", "EQUITY_", "ST_ASSETS_", "LT_ASSETS_", "TOTAL_SOURCES_"];

        private static string? Cut(string? s, int n) => s == null ? null : s.Length <= n ? s : s[..n];

        public static Guid PeriodId(DateTime m) => new(MD5Hash("AMISPL:" + m.ToString("yyyy-MM")));

        private static byte[] MD5Hash(string s) => System.Security.Cryptography.MD5.HashData(Encoding.UTF8.GetBytes(s));

        private Task<ImportLog?> LastLogAsync(DateTime m0) => LatestAsync<ImportLog>(ImportEntity, PeriodId(m0));

        private static readonly string[] DeriveCodes =
        [
            Prefix + "REVENUE", Prefix + "COGS", Prefix + "GROSS_PROFIT", Prefix + "SELLING_ADMIN", Prefix + "NET_OPERATING", Prefix + "OTHER_INCOME", Prefix + "OTHER_EXPENSE",
            Prefix + "PROFIT_BEFORE_TAX", Prefix + "ROA", Prefix + "ROE",
        ];

        /// <summary>Chỉ tiêu KQKD suy ra từ số theo tài khoản – chỉ tính khi đủ tài khoản nguồn.</summary>
        public static Dictionary<string, decimal> Derive(IReadOnlyDictionary<string, decimal> v)
        {
            var o = new Dictionary<string, decimal>();
            decimal? A(string acc) => v.TryGetValue($"{Prefix}ACC_{acc}", out var x) ? x : null;
            decimal? Sum(params string[] accs) => accs.All(a => A(a) != null) ? accs.Sum(a => A(a)!.Value) : null;
            decimal? rev = A("511"), cogs = Sum("621", "622", "627"), sa = Sum("641", "642"), ti = A("711"), te = A("811");
            if (rev is { } r) o[Prefix + "REVENUE"] = r;
            if (cogs is { } c) o[Prefix + "COGS"] = c;
            if (ti is { } t1) o[Prefix + "OTHER_INCOME"] = t1;
            if (te is { } t2) o[Prefix + "OTHER_EXPENSE"] = t2;
            if (sa is { } s) o[Prefix + "SELLING_ADMIN"] = s;
            if (rev != null && cogs != null)
            {
                o[Prefix + "GROSS_PROFIT"] = rev.Value - cogs.Value;
                if (sa != null)
                {
                    o[Prefix + "NET_OPERATING"] = rev.Value - cogs.Value - sa.Value;
                    // 711 / 811 không phát sinh → AMIS không xuất sổ → tính 0 (đủ 6 TK chính là tính LN trước thuế).
                    {
                        decimal pbt = rev.Value - cogs.Value - sa.Value + (ti ?? 0) - (te ?? 0);
                        o[Prefix + "PROFIT_BEFORE_TAX"] = pbt;
                        decimal? Avg(string k) => v.TryGetValue($"{Prefix}{k}_END", out var e) && v.TryGetValue($"{Prefix}{k}_BEGIN", out var b) && e + b != 0 ? (e + b) / 2 : null;
                        if (Avg("TOTAL_ASSETS") is { } ta) o[Prefix + "ROA"] = Math.Round(pbt / ta * 100m, 2);
                        if (Avg("EQUITY") is { } eq) o[Prefix + "ROE"] = Math.Round(pbt / eq * 100m, 2);
                    }
                }
            }
            return o;
        }

        /// <summary>Định nghĩa chỉ tiêu PL (DomainCode PL, IsActive = 0 – không lên tab Chỉ số kinh doanh) cho mã mới – chỉ thêm.</summary>
        private async Task EnsureDefinitionsAsync(IEnumerable<string> codes, IReadOnlyDictionary<string, string> names, DateTime now)
        {
            var db = unitOfWork.DbContext;
            var list = codes.Distinct().ToList();
            var have = await db.KpiMetricDefinition.AsNoTracking().Where(d => d.MetricCode != null && list.Contains(d.MetricCode)).Select(d => d.MetricCode!).ToListAsync();
            foreach (string code in list.Except(have))
            {
                db.KpiMetricDefinition.Add(new KpiMetricDefinition
                {
                    Id = Guid.NewGuid(), MetricCode = code, MetricName = MetricName(code, names), DomainCode = "PL", UnitCode = code.EndsWith("ROA") || code.EndsWith("ROE") ? "%" : "VND",
                    FormulaDescription = "RPT-1.2 (D-140 e): số AMIS (Chính thức) nhập từ sổ chi tiết – không lên tab Chỉ số kinh doanh", AggregationCode = "SUM", VersionNo = 1,
                    EffectiveFrom = new DateTime(2026, 1, 1), IsActive = false, CreatedDate = now, IsDeleted = false,
                });
            }
        }

        public static string MetricName(string code, IReadOnlyDictionary<string, string>? names = null)
        {
            string k = code.StartsWith(Prefix, StringComparison.Ordinal) ? code[Prefix.Length..] : code;
            if (names != null && names.TryGetValue(code, out var n)) return n;
            if (k.StartsWith("ACC_", StringComparison.Ordinal)) return AccountName.GetValueOrDefault(k[4..], "TK " + k[4..]);
            if (Regex.Match(k, @"^I(\d{3})_KHAC$") is { Success: true } mk) return AccountName.GetValueOrDefault(mk.Groups[1].Value, "") + " – khác / chưa phân loại";
            return k switch
            {
                "REVENUE" => "Doanh thu", "COGS" => "Giá vốn hàng bán", "GROSS_PROFIT" => "Lợi nhuận gộp", "SELLING_ADMIN" => "Chi phí bán hàng & quản lý", "NET_OPERATING" => "Lợi nhuận thuần",
                "OTHER_INCOME" => "Thu nhập khác", "OTHER_EXPENSE" => "Chi phí khác", "PROFIT_BEFORE_TAX" => "Lợi nhuận trước thuế", "ROA" => "ROA (theo tháng)", "ROE" => "ROE (theo tháng)",
                "TOTAL_ASSETS_END" => "Tổng tài sản cuối kỳ", "TOTAL_ASSETS_BEGIN" => "Tổng tài sản đầu kỳ", "EQUITY_END" => "Vốn chủ sở hữu cuối kỳ", "EQUITY_BEGIN" => "Vốn chủ sở hữu đầu kỳ",
                "ST_ASSETS_END" => "Tài sản ngắn hạn cuối kỳ", "LT_ASSETS_END" => "Tài sản dài hạn cuối kỳ", "TOTAL_SOURCES_END" => "Tổng nguồn vốn cuối kỳ",
                _ => k,
            };
        }

        // ---------------------------------------------------------------- đọc Excel AMIS
        public async Task<IResult<AmisPlMonthModel>> ImportExcel(string? month, IReadOnlyList<(string FileName, Stream Stream)> files)
        {
            if (files.Count == 0)
            {
                return Result<AmisPlMonthModel>.Fail("Chưa chọn tệp sổ AMIS");
            }
            var lines = new List<AmisLedgerLine>();
            AmisBalance? balance = null;
            var detected = new HashSet<string>();
            foreach (var (name, stream) in files)
            {
                try
                {
                    using var wb = new XLWorkbook(stream);
                    var bs = wb.Worksheets.FirstOrDefault(w => w.Name.Trim().Equals("B01-DN", StringComparison.OrdinalIgnoreCase));
                    if (bs != null)
                    {
                        balance = ReadBalance(bs);
                        continue;
                    }
                    var (acc, mon, rows) = ReadLedger(wb.Worksheets.First());
                    if (acc == null)
                    {
                        return Result<AmisPlMonthModel>.Fail($"Tệp «{name}» không đúng mẫu sổ chi tiết các tài khoản AMIS (cần dòng \"Tài khoản: …\" và cột Ngày hạch toán / TK đối ứng / Phát sinh Nợ / Phát sinh Có) hoặc bảng cân đối B01-DN");
                    }
                    month ??= mon;
                    detected.Add(acc);
                    lines.AddRange(rows);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    return Result<AmisPlMonthModel>.Fail($"Không đọc được tệp «{name}»: {ex.Message}");
                }
            }
            return await Import(new AmisPlImportRequest { Month = month, Lines = lines, Balance = balance, Source = "Excel AMIS: " + string.Join(", ", files.Select(f => f.FileName)) });
        }

        private static decimal Num(IXLCell c)
        {
            if (c.TryGetValue<decimal>(out var d)) return d;
            string s = c.GetString().Trim().Replace(" ", string.Empty);
            if (s.Length == 0) return 0;
            s = s.Contains(',') && s.Contains('.') ? s.Replace(".", string.Empty).Replace(',', '.') : s.Replace(",", string.Empty);
            return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var x) ? x : 0;
        }

        /// <summary>Sổ chi tiết các tài khoản AMIS: dòng "Tài khoản: 627, Tháng 8 năm 2026", tiêu đề "Ngày hạch toán … Phát sinh Nợ | Phát sinh Có".</summary>
        public static (string? Account, string? Month, List<AmisLedgerLine> Lines) ReadLedger(IXLWorksheet ws)
        {
            var used = ws.RangeUsed();
            if (used == null) return (null, null, []);
            int last = used.LastRow().RowNumber();
            var accRx = new Regex(@"T[àa]i kho[ảa]n:\s*(\d{3,5})", RegexOptions.IgnoreCase);
            var monRx = new Regex(@"Th[áa]ng\s*(\d{1,2})\s*n[ăa]m\s*(\d{4})", RegexOptions.IgnoreCase);
            string? acc = null, month = null;
            int head = 0;
            var col = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            // Tiêu đề: "Tài khoản: 627, Tháng 8 năm 2026" hoặc "Tháng 8 năm 2026" + dòng tổng "Tài khoản: 641" ngay dưới hàng tiêu đề cột.
            for (int r = 1; r <= Math.Min(20, last) && head == 0; r++)
            {
                for (int c = 1; c <= 15; c++)
                {
                    string v = ws.Cell(r, c).GetString().Trim();
                    if (acc == null && accRx.Match(v) is { Success: true } ma) acc = ma.Groups[1].Value[..3];
                    if (month == null && monRx.Match(v) is { Success: true } mm) month = $"{mm.Groups[2].Value}-{int.Parse(mm.Groups[1].Value):00}";
                    if (v.Equals("Ngày hạch toán", StringComparison.OrdinalIgnoreCase)) head = r;
                }
            }
            if (head == 0) return (null, null, []);
            for (int c = 1; c <= 20; c++)
            {
                string v = ws.Cell(head, c).GetString().Trim();
                if (v.Length > 0 && !col.ContainsKey(v)) col[v] = c;
            }
            if (!col.TryGetValue("TK đối ứng", out int cDu) || !col.TryGetValue("Phát sinh Nợ", out int cNo) || !col.TryGetValue("Phát sinh Có", out int cCo)) return (null, null, []);
            col.TryGetValue("Diễn giải", out int cDg); col.TryGetValue("Số chứng từ", out int cSo); col.TryGetValue("Ngày hạch toán", out int cNg);
            var lines = new List<AmisLedgerLine>();
            string? first = acc;
            for (int r = head + 1; r <= last; r++)
            {
                string du = ws.Cell(r, cDu).GetString().Trim();
                if (du.Length == 0)
                {
                    // dòng tổng "Tài khoản: 641" (đổi tài khoản đang đọc) / "Cộng"
                    if (accRx.Match(ws.Cell(r, 1).GetString()) is { Success: true } ma)
                    {
                        acc = ma.Groups[1].Value[..3];
                        first ??= acc;
                    }
                    continue;
                }
                if (acc == null) continue;
                DateTime? date = ws.Cell(r, cNg).TryGetValue<DateTime>(out var dt) ? dt : DateTime.TryParseExact(ws.Cell(r, cNg).GetString().Trim(), ["dd/MM/yyyy", "d/M/yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt2) ? dt2 : null;
                lines.Add(new AmisLedgerLine
                {
                    Account = acc, CounterAccount = du, Debit = Num(ws.Cell(r, cNo)), Credit = Num(ws.Cell(r, cCo)), Description = cDg > 0 ? ws.Cell(r, cDg).GetString().Trim() : null,
                    DocNo = cSo > 0 ? ws.Cell(r, cSo).GetString().Trim() : null, Date = date,
                });
            }
            return (first, month, lines);
        }

        /// <summary>Bảng cân đối B01-DN: cột "Mã số", "Số cuối kỳ", "Số đầu kỳ" – mã 280 (tổng tài sản), 400 (vốn chủ sở hữu); RPT-1.5 đối chiếu: 100, 200, 440 (cuối kỳ).</summary>
        public static AmisBalance ReadBalance(IXLWorksheet ws)
        {
            var used = ws.RangeUsed()!;
            int head = 0, cMa = 0, cEnd = 0, cBeg = 0;
            for (int r = 1; r <= Math.Min(20, used.LastRow().RowNumber()) && head == 0; r++)
            {
                for (int c = 1; c <= 10; c++)
                {
                    string v = ws.Cell(r, c).GetString().Trim();
                    if (v == "Mã số") { head = r; cMa = c; }
                    if (v == "Số cuối kỳ") cEnd = c;
                    if (v == "Số đầu kỳ") cBeg = c;
                }
            }
            var b = new AmisBalance();
            for (int r = head + 1; head > 0 && r <= used.LastRow().RowNumber(); r++)
            {
                string ma = ws.Cell(r, cMa).GetString().Trim();
                if (ma == "280") { b.TotalAssetsEnd = Num(ws.Cell(r, cEnd)); b.TotalAssetsBegin = cBeg > 0 ? Num(ws.Cell(r, cBeg)) : null; }
                if (ma == "400") { b.EquityEnd = Num(ws.Cell(r, cEnd)); b.EquityBegin = cBeg > 0 ? Num(ws.Cell(r, cBeg)) : null; }
                if (ma == "100") b.ShortTermAssetsEnd = Num(ws.Cell(r, cEnd));
                if (ma == "200") b.LongTermAssetsEnd = Num(ws.Cell(r, cEnd));
                if (ma == "440") b.TotalSourcesEnd = Num(ws.Cell(r, cEnd));
            }
            return b;
        }

        // ---------------------------------------------------------------- xem tháng
        public async Task<IResult<AmisPlMonthModel>> GetMonth(string? month)
        {
            DateTime m0 = DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var mm) ? mm : new DateTime(clock.Today.Year, clock.Today.Month, 1);
            var db = unitOfWork.DbContext;
            var rows = await db.KpiMonthlySnapshot.AsNoTracking().Where(k => k.SnapshotMonth == m0 && k.BranchId == null && k.MetricCode.StartsWith(Prefix) && !k.IsDeleted)
                .Select(k => new { k.MetricCode, k.MetricValue }).ToListAsync();
            var rules = await RulesAsync();
            var names = rules.GroupBy(r => $"{Prefix}I{r.Account}_{Slug(r.Item)}").ToDictionary(g => g.Key, g => g.First().Item);
            var model = new AmisPlMonthModel { Month = m0.ToString("yyyy-MM"), PeriodLocked = await AccountingPeriodRules.IsLockedAsync(db, m0) };
            foreach (var r in rows)
            {
                var mi = Regex.Match(r.MetricCode, @"^PL_AMIS_I(\d{3})_(.+)$");
                if (mi.Success)
                {
                    model.ListItem.Add(new AmisPlItemModel { Account = mi.Groups[1].Value, Item = MetricName(r.MetricCode, names), Value = r.MetricValue ?? 0 });
                }
                else
                {
                    model.Metrics[r.MetricCode[Prefix.Length..]] = r.MetricValue ?? 0;
                }
            }
            model.ListItem = [.. model.ListItem.OrderBy(i => i.Account).ThenByDescending(i => i.Value)];
            if (await LastLogAsync(m0) is { } log)
            {
                model.Accounts = log.Accounts;
                model.ListUnclassified = log.Unclassified;
                model.Source = log.Source;
                model.ImportedAt = log.ImportedAt;
                model.ImportedBy = log.ImportedBy;
            }
            return Result<AmisPlMonthModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
        }

        // ---------------------------------------------------------------- lớp Tạm tính
        public async Task<int> SnapshotEstimateAsync(DateTime month)
        {
            DateTime m0 = new(month.Year, month.Month, 1);
            var data = (await pl.GetPl(m0.ToString("yyyy-MM"), null)).Data;
            if (data == null) return 0;
            var db = unitOfWork.DbContext;
            DateTime now = clock.Now;
            Guid? by = currentActor.Actor?.UserId;
            var est = data.ListCard.Where(c => c.Source == "EST" && c.Value != null).ToDictionary(c => c.Code, c => c.Value!.Value);
            var have = await db.KpiMetricDefinition.AsNoTracking().Where(d => d.MetricCode != null && d.MetricCode.StartsWith(EstPrefix)).Select(d => d.MetricCode!).ToListAsync();
            foreach (var c in data.ListCard.Where(c => c.Source == "EST" && !have.Contains(c.Code)))
            {
                db.KpiMetricDefinition.Add(new KpiMetricDefinition
                {
                    Id = Guid.NewGuid(), MetricCode = c.Code, MetricName = c.Name + " (tạm tính)", DomainCode = "PL", UnitCode = c.Unit == "PCT" ? "%" : c.Unit == "MEAL" ? "suất" : "VND",
                    FormulaDescription = "RPT-1.2: lớp Tạm tính (Delicare) chụp hằng đêm – không lên tab Chỉ số kinh doanh", AggregationCode = "SUM", VersionNo = 1,
                    EffectiveFrom = new DateTime(2026, 1, 1), IsActive = false, CreatedDate = now, IsDeleted = false,
                });
            }
            var rows = await db.KpiMonthlySnapshot.Where(k => k.SnapshotMonth == m0 && k.BranchId == null && k.MetricCode.StartsWith(EstPrefix)).ToListAsync();
            foreach (var kv in est)
            {
                var row = rows.FirstOrDefault(r => r.MetricCode == kv.Key && r.MetricVersion == 1);
                if (row == null)
                {
                    db.KpiMonthlySnapshot.Add(new KpiMonthlySnapshot
                    {
                        Id = Guid.NewGuid(), SnapshotMonth = m0, MetricCode = kv.Key, MetricVersion = 1, MetricValue = kv.Value, CalculationVersion = "RPT-1.2", CalculatedAt = now,
                        DataQualityStatus = "ESTIMATE", CreatedDate = now, CreatedBy = by, IsDeleted = false,
                    });
                }
                else if (row.MetricValue != kv.Value || row.IsDeleted)
                {
                    row.MetricValue = kv.Value; row.IsDeleted = false; row.CalculatedAt = now; row.ModifiedDate = now; row.ModifiedBy = by;
                }
            }
            await db.SaveChangesAsync();
            return est.Count;
        }
    }

    /// <summary>RPT-1.2 (D-140 b): job hằng đêm chụp lớp Tạm tính (PL_EST_*) của tháng này + tháng trước – để có xu hướng 12 tháng khi chưa có số AMIS.</summary>
    public sealed class PlEstimateJob(IAmisPlService service) : IScheduledJob
    {
        public string Code => "PlEstimate";
        public string Name => "Kết quả kinh doanh – số tạm tính";
        public string Description => "23:30 – chụp số tạm tính (doanh thu, NVL, bao bì, lãi gộp theo NVL) tháng này và tháng trước vào lịch sử KQKD.";
        public string DefaultAt => "23:30";

        public async Task<JobOutcome> RunAsync(DateTime now, CancellationToken cancellationToken)
        {
            DateTime m = new(now.Year, now.Month, 1);
            int n = await service.SnapshotEstimateAsync(m) + await service.SnapshotEstimateAsync(m.AddMonths(-1));
            return new JobOutcome(JobRunStatus.Succeeded, n, m, $"Đã chụp {n} chỉ tiêu tạm tính tháng {m:MM/yyyy} và {m.AddMonths(-1):MM/yyyy}");
        }
    }
}
