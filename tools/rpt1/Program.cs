// RPT-1.3 (D-140 i): nạp MỘT LẦN lịch sử Kết quả kinh doanh (lớp Chính thức – AMIS) từ các sheet ẩn của file báo cáo kế toán.
//
//   dotnet run --project tools/rpt1 -- "<đường dẫn Report_202608_sent.xlsx>" [--api http://localhost:5125/api] [--months 202601-202608] [--check]
//   Tài khoản: biến môi trường RPT1_USER / RPT1_PASS (người có quyền Nhập mục Kết quả kinh doanh – Kế toán). Không in mật khẩu / token.
//
// Làm gì:
//   1. Sinh bảng quy tắc phân loại từ chính phân loại của Kế toán trong file: GVHB (621 → cột Phân loại_NVL; 627 → Phân loại), CP (641 / 642 / 811 → Phân loại)
//      – mỗi diễn giải một quy tắc (nguyên cụm), đặt TRƯỚC các quy tắc đang có → Dashboard/SavePlRules.
//   2. Mỗi tháng: dòng sổ 511 (DTBH), 621 / 622 / 627 (GVHB), 641 / 642 / 811 (CP), 711 (TNK – lấy số bút toán kết chuyển 911 vì sheet thiếu dòng phát sinh ở vài tháng),
//      bảng cân đối mã 280 / 400 (Raw BCD – cuối kỳ = cột tháng, đầu kỳ = cột tháng trước) → Dashboard/ImportAmisLines (cùng quy tắc với nhập Excel AMIS).
//   3. So từng tháng với sheet Data_DB (Doanh thu, GVHB, Lợi nhuận gộp, Chi phí, Lợi nhuận ròng – triệu đồng) → KHỚP / LỆCH. --check: chỉ so, không ghi.
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;

Console.OutputEncoding = Encoding.UTF8;
if (args.Length == 0 || !File.Exists(args[0]))
{
    Console.WriteLine("Cần đường dẫn file báo cáo kế toán (Report_YYYYMM.xlsx) – xem chú thích đầu Program.cs");
    return 2;
}
string api = Arg("--api") ?? "http://localhost:5125/api";
bool checkOnly = args.Contains("--check");
var range = (Arg("--months") ?? "202601-202608").Split('-');
int mFrom = int.Parse(range[0]), mTo = int.Parse(range.Length > 1 ? range[1] : range[0]);

using var fs = new FileStream(args[0], FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
using var wb = new XLWorkbook(fs);

var lines = new List<(int Month, Line L)>();
var rules = new List<Rule>();
var seen = new HashSet<string>();
ReadLedger("DTBH", null);
ReadLedger("GVHB", r => r.Acc == "621" ? r.Cell("Phân loại_NVL") : r.Acc == "627" ? r.Cell("Phân loại") : null);
ReadLedger("CP", r => r.Cell("Phân loại"));
var other = ReadOtherIncome();
var balance = ReadBalance();
var target = ReadTargets();

using var http = new HttpClient { BaseAddress = new Uri(api.TrimEnd('/') + "/") };
if (!checkOnly)
{
    string? user = Environment.GetEnvironmentVariable("RPT1_USER"), pass = Environment.GetEnvironmentVariable("RPT1_PASS");
    if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
    {
        Console.WriteLine("Thiếu biến môi trường RPT1_USER / RPT1_PASS");
        return 2;
    }
    var login = await (await http.PostAsJsonAsync("Authentication/Login", new { userName = user, passWord = pass })).Content.ReadFromJsonAsync<JsonElement>();
    string token = login.GetProperty("data").TryGetProperty("accessToken", out var at) ? at.GetString()! : login.GetProperty("data").GetProperty("token").GetString()!;
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    var current = (await http.GetFromJsonAsync<JsonElement>("Dashboard/GetPlRules")).GetProperty("listData").EnumerateArray()
        .Select(x => new Rule(x.GetProperty("account").GetString()!, Str(x, "keyword"), Str(x, "counter"), x.GetProperty("item").GetString()!)).ToList();
    var merged = rules.Concat(current.Where(c => !(c.account == "621" && c.item == "Bữa sáng") && !rules.Any(r => r.account == c.account && r.keyword == c.keyword && r.counter == c.counter))).ToList();
    var rs = await http.PostAsJsonAsync("Dashboard/SavePlRules", merged);
    Console.WriteLine($"Quy tắc: {rules.Count} quy tắc từ phân loại của Kế toán + {merged.Count - rules.Count} quy tắc đang có → {(int)rs.StatusCode}");
}

Console.WriteLine("Tháng   | Chỉ tiêu        |    Data_DB (trđ) |    Delicare (trđ) | Kết quả");
int bad = 0;
for (int m = mFrom; m <= mTo; m = m % 100 == 12 ? m + 89 : m + 1)
{
    var ml = lines.Where(x => x.Month == m).Select(x => x.L).ToList();
    if (other.TryGetValue(m, out var oi))
    {
        ml.Add(new Line("711", "711-KC", 0, oi, "Thu nhập khác trong tháng (theo bút toán kết chuyển 911)", null));
    }
    var bal = balance.GetValueOrDefault(m);
    var sums = Derive(ml);
    if (!checkOnly)
    {
        var req = new { month = $"{m / 100}-{m % 100:00}", lines = ml, balance = bal, source = $"tools/rpt1 – lịch sử {Path.GetFileName(args[0])}" };
        var res = await http.PostAsJsonAsync("Dashboard/ImportAmisLines", req);
        var j = await res.Content.ReadFromJsonAsync<JsonElement>();
        if (j.GetProperty("responseCode").GetInt32() != 200)
        {
            Console.WriteLine($"{m}: LỖI {j.GetProperty("responseMessage").GetString()}");
            bad++;
            continue;
        }
        var mt = j.GetProperty("data").GetProperty("metrics");
        sums = new Dictionary<string, decimal>();
        foreach (var p in mt.EnumerateObject()) sums[p.Name] = p.Value.GetDecimal();
    }
    foreach (var (k, name, tk) in new[] { ("REVENUE", "Doanh thu", "DT"), ("COGS", "GVHB", "GVHB"), ("GROSS_PROFIT", "Lợi nhuận gộp", "LNG"), ("SELLING_ADMIN", "Chi phí", "CP"), ("PROFIT_BEFORE_TAX", "LN trước thuế", "LN") })
    {
        decimal? want = target.GetValueOrDefault((m, tk));
        decimal got = Math.Round(sums.GetValueOrDefault(k) / 1_000_000m, 6);
        bool ok = want != null && Math.Abs(want.Value - got) < 0.001m;
        if (!ok) bad++;
        Console.WriteLine($"{m} | {name,-15} | {want,16:#,0.000} | {got,17:#,0.000} | {(want == null ? "không có số file" : ok ? "KHỚP" : "LỆCH")}");
    }
}
Console.WriteLine(bad == 0 ? "Tất cả khớp Data_DB." : $"{bad} chỉ tiêu lệch / lỗi.");
return bad == 0 ? 0 : 1;

// ---------------------------------------------------------------- đọc file
string? Arg(string name) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
static string? Str(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

void ReadLedger(string sheet, Func<Row, string?>? classify)
{
    var ws = wb.Worksheet(sheet);
    var head = Head(ws);
    int last = ws.RangeUsed()!.LastRow().RowNumber();
    for (int r = 2; r <= last; r++)
    {
        var row = new Row(ws, r, head);
        string du = row.Cell("TK đối ứng") ?? string.Empty, acc = row.Cell("TK") ?? string.Empty;
        if (du.Length == 0 || acc.Length < 3 || !int.TryParse(row.Cell("Month") ?? row.Cell("MONTH"), out int month)) continue;
        acc = acc[..3];
        var l = new Line(acc, du, row.Num("Phát sinh Nợ"), row.Num("Phát sinh Có"), row.Cell("Diễn giải"), row.Cell("Số chứng từ"));
        lines.Add((month, l));
        string? cat = classify?.Invoke(row with { Acc = acc });
        string? desc = Clean(l.description);
        if (cat != null && desc != null && !Regex.IsMatch(cat, "^(0|Kết chuyển|#N/A)$", RegexOptions.IgnoreCase) && !du.StartsWith("911") && !du.StartsWith("154") && seen.Add(acc + "|" + Fold(desc)))
        {
            // D-140 f: nhóm NVL "Bữa sáng" → "Đồ ăn sáng mua sẵn" (tránh nhầm doanh thu bữa sáng).
            string item = acc == "621" && cat.Trim() == "Bữa sáng" ? "Đồ ăn sáng mua sẵn" : cat.Trim();
            rules.Add(new Rule(acc, desc, null, item));
        }
    }
}

Dictionary<int, decimal> ReadOtherIncome()
{
    var ws = wb.Worksheet("TNK");
    var head = Head(ws);
    var res = new Dictionary<int, decimal>();
    int last = ws.RangeUsed()!.LastRow().RowNumber();
    for (int r = 2; r <= last; r++)
    {
        var row = new Row(ws, r, head);
        if ((row.Cell("TK đối ứng") ?? string.Empty).StartsWith("911") && int.TryParse(row.Cell("MONTH") ?? row.Cell("Month"), out int m))
        {
            res[m] = res.GetValueOrDefault(m) + row.Num("Phát sinh Nợ");
        }
    }
    return res;
}

Dictionary<int, Balance> ReadBalance()
{
    var ws = wb.Worksheet("Raw BCD");
    var head = Head(ws);
    var res = new Dictionary<int, Balance>();
    int last = ws.RangeUsed()!.LastRow().RowNumber();
    decimal? Val(string code, int month)
    {
        if (!head.TryGetValue(month.ToString(), out int c)) return null;
        for (int r = 2; r <= last; r++)
        {
            if (X.Text(ws.Cell(r, head["Mã số"])) == code) return X.Num(ws.Cell(r, c));
        }
        return null;
    }
    foreach (var m in head.Keys.Where(k => Regex.IsMatch(k, @"^\d{6}$")).Select(int.Parse))
    {
        int prev = m % 100 == 1 ? m - 89 : m - 1;
        res[m] = new Balance(Val("280", m), Val("280", prev), Val("400", m), Val("400", prev));
    }
    return res;
}

Dictionary<(int, string), decimal> ReadTargets()
{
    var ws = wb.Worksheet("Data_DB");
    var res = new Dictionary<(int, string), decimal>();
    // Bảng 12 tháng: dòng có ô cột C..O là tháng yyyyMM; các dòng dưới: Doanh thu, GVHB, Lợi nhuận gộp, Chi phí, Lợi nhuận ròng.
    int last = ws.RangeUsed()!.LastRow().RowNumber();
    for (int r = 1; r <= last; r++)
    {
        var months = Enumerable.Range(3, 13).Select(c => (c, X.Text(ws.Cell(r, c)))).Where(x => Regex.IsMatch(x.Item2, @"^\d{6}$")).ToList();
        if (months.Count < 6 || X.Text(ws.Cell(r, 2)).Length > 0) continue;
        foreach (var (label, key) in new[] { ("Doanh thu", "DT"), ("GVHB", "GVHB"), ("Lợi nhuận gộp", "LNG"), ("Chi phí", "CP"), ("Lợi nhuận ròng", "LN") })
        {
            for (int rr = r + 1; rr <= r + 6; rr++)
            {
                if (X.Text(ws.Cell(rr, 2)) != label) continue;
                foreach (var (c, mo) in months)
                {
                    if (X.Num(ws.Cell(rr, c)) is { } v) res[(int.Parse(mo), key)] = v;
                }
            }
        }
        break;
    }
    return res;
}

static Dictionary<string, int> Head(IXLWorksheet ws)
{
    var h = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    for (int c = 1; c <= 50; c++)
    {
        string v = X.Text(ws.Cell(1, c));
        if (v.Length > 0 && !h.ContainsKey(v)) h[v] = c;
    }
    return h;
}

static string? Clean(string? s)
{
    string v = Regex.Replace((s ?? string.Empty).Replace('|', ' '), @"\s+", " ").Trim();
    return v.Length == 0 ? null : v.Length > 200 ? v[..200] : v;
}

static string Fold(string s)
{
    string n = s.Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
    return new string(n.Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark).ToArray()).ToLowerInvariant();
}

// Chỉ dùng khi --check (không gọi API): cùng công thức với AmisPlService.
static Dictionary<string, decimal> Derive(List<Line> ls)
{
    decimal A(string acc) => ls.Where(l => l.account == acc && !l.counterAccount.StartsWith("911") && !l.counterAccount.StartsWith("154"))
        .Sum(l => acc is "511" or "711" ? l.credit - l.debit : l.debit - l.credit);
    decimal rev = A("511"), cogs = A("621") + A("622") + A("627"), sa = A("641") + A("642");
    return new() { ["REVENUE"] = rev, ["COGS"] = cogs, ["GROSS_PROFIT"] = rev - cogs, ["SELLING_ADMIN"] = sa, ["PROFIT_BEFORE_TAX"] = rev - cogs - sa + A("711") - A("811") };
}

record Line(string account, string counterAccount, decimal debit, decimal credit, string? description, string? docNo);
record Rule(string account, string? keyword, string? counter, string item);
record Balance(decimal? totalAssetsEnd, decimal? totalAssetsBegin, decimal? equityEnd, decimal? equityBegin);

record Row(IXLWorksheet Ws, int R, Dictionary<string, int> Head)
{
    public string Acc { get; init; } = string.Empty;
    public string? Cell(string name) => Head.TryGetValue(name, out int c) ? X.Text(Ws.Cell(R, c)) is { Length: > 0 } v ? v : null : null;
    public decimal Num(string name) => Head.TryGetValue(name, out int c) ? X.Num(Ws.Cell(R, c)) ?? 0 : 0;
}

/// <summary>Đọc giá trị ĐÃ LƯU trong file (không tính lại công thức – file có công thức ClosedXML không tính được).</summary>
static class X
{
    public static string Text(IXLCell c)
    {
        try { return (c.HasFormula ? c.CachedValue.ToString() : c.Value.ToString())?.Trim() ?? string.Empty; } catch { return string.Empty; }
    }

    public static decimal? Num(IXLCell c)
    {
        try
        {
            var v = c.HasFormula ? c.CachedValue : c.Value;
            if (v.IsNumber) return (decimal)v.GetNumber();
            return decimal.TryParse(v.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;
        }
        catch { return null; }
    }
}
