using System.Globalization;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Services.Interface;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// ACC-8 (D-131 l – anh chốt 2026-11-26): Kế toán › Đối chiếu với AMIS (#/acc-amis-recon) – 8 chỉ tiêu của kỳ tháng: số Delicare tính từ chứng từ kế toán
    /// (ACC-2) + sổ kho; số AMIS nhập bằng Excel mẫu (tải ở màn) hoặc nhập tay (API AMIS: ACC-7). Số AMIS lưu AuditChange (EntityName <c>AmisRecon</c>,
    /// IdChange = mã kỳ, Action <c>AmisFigures</c>, JSON { f: {mã: số}, src }) – không bảng mới; lần nhập sau ghép đè từng chỉ tiêu.
    /// Phân loại lệch: còn chứng từ chưa lập / lỗi (nút tới Chứng từ kế toán) · do giá xuất (AMIS bình quân ≠ giá lô – chỉ để biết, giá vốn chính thức lấy AMIS)
    /// · lệch chưa rõ · tồn cuối kỳ chờ kiểm kê. Số chính thức là số AMIS.
    /// </summary>
    public class AmisReconService(IUnitOfWork unitOfWork, IClock clock, ICurrentActor currentActor, IOptions<MisaOptions> misa, IAccountingVoucherService vouchers) : IAmisReconService
    {
        public const string Entity = "AmisRecon";
        public const string Action = "AmisFigures";
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly ICurrentActor _currentActor = currentActor;
        private readonly MisaOptions _misa = misa.Value;
        private readonly IAccountingVoucherService _vouchers = vouchers;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>8 chỉ tiêu: mã, tên, loại số, đơn vị.</summary>
        public static readonly (string Code, string Title, string Kind, string Unit)[] Indicators =
        [
            ("REVENUE", "Doanh thu bán hàng (chưa VAT)", "amount", "đồng"),
            ("PORTIONS", "Số suất bán (5 loại thành phẩm)", "qty", "suất"),
            ("PURCHASE", "Nhập mua NVL (số phiếu)", "count", "phiếu"),
            ("ISSUE_COUNT", "Xuất NVL cho sản xuất (số phiếu)", "count", "phiếu"),
            ("ISSUE_AMOUNT", "Xuất NVL cho sản xuất (giá trị)", "amount", "đồng"),
            ("PACKAGING", "Xuất bao bì (số phiếu)", "count", "phiếu"),
            ("FG_OUT", "Xuất thành phẩm khác (suất bù, hủy, dư)", "qty", "suất"),
            ("STOCK_END", "Tồn NVL cuối kỳ (giá trị)", "amount", "đồng"),
        ];

        private (DateTime From, DateTime To, string Key) Period(string? month)
        {
            DateTime first = DateTime.TryParseExact(month ?? string.Empty, "yyyy-MM", Inv, DateTimeStyles.None, out var m) ? m : new DateTime(_clock.Today.Year, _clock.Today.Month, 1).AddMonths(-1);
            return (first, first.AddMonths(1).AddDays(-1), first.ToString("yyyy-MM", Inv));
        }

        private sealed class Stored
        {
            [JsonProperty("f")] public Dictionary<string, decimal> Figures { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            [JsonProperty("src")] public string? Source { get; set; }
        }

        private async Task<(Stored? Data, AuditChange? Row)> LatestAsync(DateTime month)
        {
            var id = AccountingPeriodRules.PeriodId(month);
            var row = await _unitOfWork.DbContext.AuditChange.AsNoTracking().Where(a => !a.IsDeleted && a.EntityName == Entity && a.IdChange == id && a.Action == Action)
                .OrderByDescending(a => a.CreatedDate).FirstOrDefaultAsync();
            if (row == null)
            {
                return (null, null);
            }
            try
            {
                return (JsonConvert.DeserializeObject<Stored>(row.Changes ?? "{}") ?? new Stored(), row);
            }
            catch (JsonException)
            {
                return (null, row);
            }
        }

        public async Task<IResult<AmisReconModel>> GetMonth(string? month)
        {
            try
            {
                var (from, to, key) = Period(month);
                var db = _unitOfWork.DbContext;
                var list = (await _vouchers.GetMonth(key, "all")).Data?.ListVoucher ?? [];
                var (stored, row) = await LatestAsync(from);
                var model = new AmisReconModel { Month = key, AmisAt = row?.CreatedDate, AmisSource = stored?.Source };
                if (row?.CreatedBy is { } by)
                {
                    model.AmisBy = (await CommonAccess.UserNamesAsync(db, [by])).GetValueOrDefault(by);
                }
                List<VoucherModel> Of(string type) => [.. list.Where(v => v.Type == type)];
                int NotCreated(IEnumerable<VoucherModel> vs) => vs.Count(v => v.Status != VoucherStatus.Created);
                string link = $"#/acc-voucher?month={key}&tab=need";
                var products = _misa.ProductByMeal;
                var sale = Of(VoucherType.Sale);
                var meals = products.ToDictionary(p => p.Key, p => sale.SelectMany(v => v.ListLine).Where(l => string.Equals(l.Code, p.Value, StringComparison.OrdinalIgnoreCase)).Sum(l => l.Quantity));
                var issue = Of(VoucherType.Issue);
                var packaging = Of(VoucherType.Packaging);
                var purchase = Of(VoucherType.Purchase);
                var fgOut = Of(VoucherType.FinishedOut);
                DateTime end = to.AddDays(1);
                decimal stockEnd = await db.InventoryTransaction.AsNoTracking().Where(t => !t.IsDeleted && t.TransactionDate < end)
                    .SumAsync(t => (decimal?)((t.QuantityIn - t.QuantityOut) * (t.UnitCost ?? 0m))) ?? 0m;
                DateTime cFrom = to.AddDays(-4), cTo = to.AddDays(4);
                bool counted = await db.StockCount.AsNoTracking().AnyAsync(s => !s.IsDeleted && s.Status == "Posted" && s.CountDate >= cFrom && s.CountDate < cTo);

                var values = new Dictionary<string, (decimal Value, int NotCreated, string Note)>
                {
                    ["REVENUE"] = (sale.Sum(v => v.Amount ?? 0m), NotCreated(sale), $"{sale.Count} phiếu bán tháng từ bảng kê"),
                    ["PORTIONS"] = (meals.Values.Sum(), NotCreated(sale), string.Join(" · ", meals.Where(m => m.Value > 0).Select(m => $"{MealLabel(m.Key)} {m.Value.ToString("#,##0", CultureInfo.GetCultureInfo("vi-VN"))}"))),
                    ["PURCHASE"] = (purchase.Count, NotCreated(purchase), $"Giá trị {purchase.Sum(v => v.Amount ?? 0m).ToString("#,##0", CultureInfo.GetCultureInfo("vi-VN"))} đ"),
                    ["ISSUE_COUNT"] = (issue.Count, NotCreated(issue), $"Theo {issue.Select(v => v.ObjectCode).Distinct().Count()} buổi"),
                    ["ISSUE_AMOUNT"] = (issue.Sum(v => v.Amount ?? 0m), NotCreated(issue), "Giá lô thực tế"),
                    ["PACKAGING"] = (packaging.Count, NotCreated(packaging), $"Giá trị {packaging.Sum(v => v.Amount ?? 0m).ToString("#,##0", CultureInfo.GetCultureInfo("vi-VN"))} đ"),
                    ["FG_OUT"] = (fgOut.Sum(v => v.Quantity ?? 0m), NotCreated(fgOut), $"{fgOut.Count} phiếu theo ngày × buổi"),
                    ["STOCK_END"] = (Math.Round(stockEnd, 0), 0, counted ? "Theo sổ kho tới cuối kỳ (giá lô)" : "Chưa ghi sổ kiểm kê cuối tháng"),
                };
                foreach (var (code, title, kind, unit) in Indicators)
                {
                    var (val, notCreated, note) = values[code];
                    decimal? amis = stored != null && stored.Figures.TryGetValue(code, out var a) ? a : null;
                    var r = new AmisReconRowModel { Code = code, Title = title, Kind = kind, UnitLabel = unit, Delicare = val, Amis = amis, Difference = amis is { } x ? val - x : null, Reason = note };
                    if (code == "STOCK_END" && !counted && (amis == null || r.Difference != 0))
                    {
                        (r.Result, r.Reason, r.Link, r.LinkLabel) = (AmisReconResult.WaitCount, "So sau khi ghi sổ kiểm kê cuối tháng", "#/scm-stockcount", "Kiểm kê");
                    }
                    else if (amis == null)
                    {
                        r.Result = AmisReconResult.NoAmis;
                    }
                    else if (r.Difference == 0)
                    {
                        r.Result = AmisReconResult.Match;
                    }
                    else if (notCreated > 0)
                    {
                        (r.Result, r.Reason, r.Link, r.LinkLabel) = (AmisReconResult.Missing, $"{notCreated} chứng từ chưa lập trên AMIS / còn lỗi", link, $"Xem {notCreated} chứng từ");
                    }
                    else if (code == "STOCK_END" || (code == "ISSUE_AMOUNT" && values["ISSUE_COUNT"].Value == (stored!.Figures.TryGetValue("ISSUE_COUNT", out var ic) ? ic : -1)))
                    {
                        (r.Result, r.Reason) = (AmisReconResult.Price, "Delicare tính giá theo lô thực tế, AMIS tính bình quân. Giá vốn chính thức lấy AMIS");
                    }
                    else
                    {
                        (r.Result, r.Reason, r.Link, r.LinkLabel) = (AmisReconResult.Diff, "Mọi chứng từ đã lập nhưng số vẫn lệch – kiểm tra chứng từ sửa tay trên AMIS", $"#/acc-voucher?month={key}&tab=all", "Xem chứng từ");
                    }
                    r.ResultName = AmisReconResult.Label(r.Result);
                    model.ListRow.Add(r);
                }
                model.Total = model.ListRow.Count;
                model.MatchCount = model.ListRow.Count(r => r.Result == AmisReconResult.Match);
                model.MissingCount = model.ListRow.Count(r => r.Result == AmisReconResult.Missing);
                model.PriceCount = model.ListRow.Count(r => r.Result == AmisReconResult.Price);
                return Result<AmisReconModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetMonth: ", ex.ToString()));
                throw;
            }
        }

        private static string MealLabel(string meal) => meal.ToUpperInvariant() switch
        {
            "SANG" => "Sáng", "TRUA" => "Trưa", "TOI" => "Tối", "DEM" => "Đêm", "COM_SUAT" => "Cơm suất", _ => meal,
        };

        /// <summary>Nhập tay số AMIS (ghép đè chỉ tiêu có giá trị; giá trị trống = xóa chỉ tiêu đó).</summary>
        public Task<IResult<bool>> SaveFigures(AmisReconSaveRequest request)
            => SaveAsync(request.Month, request.ListFigure.Where(f => Indicators.Any(i => i.Code == f.Code)).ToDictionary(f => f.Code, f => f.Value), "Nhập tay");

        private async Task<IResult<bool>> SaveAsync(string? month, Dictionary<string, decimal?> input, string source)
        {
            try
            {
                if (input.Count == 0)
                {
                    return Result<bool>.Fail(Msg.Empty, false);
                }
                var actor = _currentActor.RequireActor().UserId;
                var (from, _, key) = Period(month);
                var db = _unitOfWork.DbContext;
                var (stored, row) = await LatestAsync(from);
                stored ??= new Stored();
                foreach (var (code, v) in input)
                {
                    if (v is { } x)
                    {
                        stored.Figures[code] = x;
                    }
                    else
                    {
                        stored.Figures.Remove(code);
                    }
                }
                stored.Source = source;
                DateTime now = _clock.Now;
                db.AuditChange.Add(new AuditChange
                {
                    Id = Guid.NewGuid(), IdChange = AccountingPeriodRules.PeriodId(from), EntityName = Entity, Action = Action, Changes = JsonConvert.SerializeObject(stored),
                    CreatedDate = row != null && row.CreatedDate >= now ? row.CreatedDate.AddSeconds(1) : now, CreatedBy = actor, IsDeleted = false,
                });
                await db.SaveChangesAsync();
                return Result<bool>.Success(true, string.Format(Msg.Saved, input.Count(x => x.Value != null), key));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Save: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>Excel mẫu: 1 dòng / chỉ tiêu (Mã chỉ tiêu · Chỉ tiêu · Đơn vị · Số AMIS) – Kế toán điền số từ sổ AMIS rồi nhập lại.</summary>
        public async Task<IResult<FileContentModel>> Template(string? month)
        {
            var (from, _, key) = Period(month);
            var (stored, _) = await LatestAsync(from);
            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("So AMIS");
            ws.Cell(1, 1).Value = $"Số AMIS kỳ {from:MM/yyyy} – điền cột D từ sổ / báo cáo AMIS rồi nhập lại ở Kế toán › Đối chiếu với AMIS";
            ws.Cell(1, 1).Style.Font.Bold = true;
            string[] head = ["Mã chỉ tiêu", "Chỉ tiêu", "Đơn vị", "Số AMIS", "Lấy từ AMIS"];
            for (int c = 0; c < head.Length; c++)
            {
                ws.Cell(3, c + 1).Value = head[c];
                ws.Cell(3, c + 1).Style.Font.Bold = true;
                ws.Cell(3, c + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#EFF5E5");
            }
            string[] hint =
            [
                "Sổ chi tiết bán hàng – doanh thu chưa VAT", "Sổ chi tiết bán hàng – số lượng 5 mã thành phẩm", "Số chứng từ mua hàng nhập kho", "Số phiếu xuất kho sản xuất (NVL)",
                "Giá trị phiếu xuất kho sản xuất (NVL)", "Số phiếu xuất kho bao bì", "Số lượng xuất kho thành phẩm khác", "Tồn kho NVL cuối kỳ – giá trị",
            ];
            for (int i = 0; i < Indicators.Length; i++)
            {
                var (code, title, _, unit) = Indicators[i];
                ws.Cell(4 + i, 1).Value = code;
                ws.Cell(4 + i, 2).Value = title;
                ws.Cell(4 + i, 3).Value = unit;
                if (stored != null && stored.Figures.TryGetValue(code, out var v))
                {
                    ws.Cell(4 + i, 4).Value = v;
                }
                ws.Cell(4 + i, 4).Style.NumberFormat.Format = "#,##0";
                ws.Cell(4 + i, 5).Value = hint[i];
            }
            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return Result<FileContentModel>.Success(new FileContentModel
            {
                Content = ms.ToArray(), ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", FileName = $"So-AMIS-{key}.xlsx",
            }, ConstantHelper.Message.GetAllSuccessfully);
        }

        /// <summary>Nhận Excel số AMIS (theo mẫu: cột "Mã chỉ tiêu" + "Số AMIS"; ô trống bỏ qua).</summary>
        public async Task<IResult<bool>> ImportExcel(string? month, Stream stream, string fileName)
        {
            Dictionary<string, decimal?> input = new(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var wb = new XLWorkbook(stream);
                var ws = wb.Worksheets.First();
                int headRow = 0, codeCol = 0, valCol = 0;
                foreach (var row in ws.RowsUsed().Take(20))
                {
                    foreach (var cell in row.CellsUsed())
                    {
                        string t = cell.GetString().Trim();
                        if (t.Equals("Mã chỉ tiêu", StringComparison.OrdinalIgnoreCase))
                        {
                            (headRow, codeCol) = (row.RowNumber(), cell.Address.ColumnNumber);
                        }
                        if (t.Equals("Số AMIS", StringComparison.OrdinalIgnoreCase))
                        {
                            valCol = cell.Address.ColumnNumber;
                        }
                    }
                    if (headRow > 0 && valCol > 0)
                    {
                        break;
                    }
                }
                if (headRow == 0 || valCol == 0)
                {
                    return Result<bool>.Fail(Msg.BadTemplate, false);
                }
                foreach (var row in ws.RowsUsed().Where(r => r.RowNumber() > headRow))
                {
                    string code = row.Cell(codeCol).GetString().Trim().ToUpperInvariant();
                    if (!Indicators.Any(i => i.Code == code))
                    {
                        continue;
                    }
                    var cell = row.Cell(valCol);
                    if (cell.IsEmpty())
                    {
                        continue;
                    }
                    if (cell.TryGetValue(out double d))
                    {
                        input[code] = (decimal)d;
                    }
                    else if (decimal.TryParse(cell.GetString().Replace(".", string.Empty).Replace(",", "."), NumberStyles.Number | NumberStyles.AllowLeadingSign, Inv, out var p))
                    {
                        input[code] = p;
                    }
                    else
                    {
                        return Result<bool>.Fail(string.Format(Msg.BadNumber, code, cell.GetString()), false);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.Warning(string.Concat("[", GetType().Name, "]", " ImportExcel: ", ex.Message));
                return Result<bool>.Fail(Msg.BadTemplate, false);
            }
            return await SaveAsync(month, input, "Excel: " + Path.GetFileName(fileName));
        }

        public static class Msg
        {
            public const string Empty = "Chưa có số AMIS nào để lưu";
            public const string Saved = "Đã lưu {0} số AMIS kỳ {1}";
            public const string BadTemplate = "Tệp không đúng mẫu – cần cột «Mã chỉ tiêu» và «Số AMIS» (tải mẫu ở màn Đối chiếu với AMIS)";
            public const string BadNumber = "Chỉ tiêu {0}: «{1}» không phải số";
        }
    }
}
