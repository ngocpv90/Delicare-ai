using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Services.Interface;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// ACC-1 (D-131 c – anh chốt 2026-11-26): danh mục kế toán – khớp mã với MISA AMIS. AMIS là nguồn mã chuẩn: nhận danh mục (vật tư hàng hóa, ĐVT, khách,
    /// NCC, kho) từ tệp Excel xuất từ AMIS (API: ACC-7), lưu BẢN CHỤP mới nhất thành tệp JSON trong kho tài liệu (<c>DocumentFile</c>, khóa
    /// <c>_chung/amis/&lt;sha&gt;.json</c>) – không bảng mới. So theo mã: Đã khớp · Chưa có trên AMIS · Cùng mã khác tên / Khác ĐVT · Chỉ có trên AMIS.
    /// Ghép mã = đổi mã Delicare (nguyên liệu / khách / NCC) thành mã AMIS (ngoại lệ có chủ đích của D-63 như D-129 a; AuditChange qua repository).
    /// Thành phẩm theo buổi và kho khớp bằng cấu hình <c>Integration:Misa</c>.
    /// </summary>
    public class AmisCatalogService(IUnitOfWork unitOfWork, IOptions<MisaOptions> misa, IOptions<DocumentStorageOptions> storage, AppDelicare.Data.Share.IClock clock) : IAmisCatalogService
    {
        private const string Category = "amis";
        private static string Prefix => DocumentStorageRules.GeneralFolder + "/" + Category + "/";
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly MisaOptions _misa = misa.Value;
        private readonly DocumentStorageOptions _storage = storage.Value;
        private readonly AppDelicare.Data.Share.IClock _clock = clock;
        private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

        private string Root => Path.GetFullPath(string.IsNullOrWhiteSpace(_storage.RootPath) ? "App_Data/DocumentFiles" : _storage.RootPath);

        // ============================================================ bản chụp
        public async Task<AmisCatalogSnapshot?> GetSnapshotAsync()
        {
            var files = await _unitOfWork.DbContext.DocumentFile.AsNoTracking()
                .Where(f => !f.IsDeleted && f.FileNameHas != null && f.FileNameHas.StartsWith(Prefix))
                .OrderByDescending(f => f.CreatedDate).Take(Math.Max(1, _misa.SnapshotKeep)).Select(f => f.FileNameHas!).ToListAsync();
            foreach (string key in files)
            {
                try
                {
                    string path = DocumentStorageRules.FullPath(Root, key);
                    if (!File.Exists(path))
                    {
                        continue;
                    }
                    var snap = JsonSerializer.Deserialize<AmisCatalogSnapshot>(await File.ReadAllTextAsync(path), Json);
                    if (snap != null)
                    {
                        return snap;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning("[AmisCatalogService] bỏ qua bản chụp hỏng {Key}: {Error}", key, ex.Message);
                }
            }
            return null;
        }

        private async Task SaveSnapshotAsync(AmisCatalogSnapshot snap)
        {
            byte[] content = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(snap, Json));
            string sha = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
            string key = DocumentStorageRules.BuildKey(null, null, Category, sha, ".json");
            string path = DocumentStorageRules.FullPath(Root, key);
            if (!File.Exists(path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllBytesAsync(path, content);
            }
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                _unitOfWork.DocumentFileRepository.Add(new DocumentFile
                {
                    Id = Guid.NewGuid(), FileName = $"AMIS-danh-muc-{snap.ReceivedAt:yyyyMMdd-HHmmss}", FileType = ".json", FileSize = content.Length, FileNameHas = key,
                });
                await _unitOfWork.DocumentFileRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
                _unitOfWork.CommitTransaction();
            }
            catch
            {
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        // ============================================================ nhận Excel
        public async Task<IResult<AmisImportResultModel>> ImportExcel(Stream file, string? fileName, string? kind)
        {
            try
            {
                string? wanted = string.IsNullOrWhiteSpace(kind) ? null : kind.Trim().ToLowerInvariant();
                if (wanted != null && !AmisKind.Importable.Contains(wanted))
                {
                    return Result<AmisImportResultModel>.Fail(string.Format(ConstantHelper.Message.AmisKindInvalid, kind));
                }
                XLWorkbook wb;
                try
                {
                    wb = new XLWorkbook(file);
                }
                catch (Exception)
                {
                    return Result<AmisImportResultModel>.Fail(ConstantHelper.Message.AmisFileInvalid);
                }
                using (wb)
                {
                    foreach (var ws in wb.Worksheets)
                    {
                        var parsed = ParseSheet(ws, wanted);
                        if (parsed == null)
                        {
                            continue;
                        }
                        var (k, items, skipped, warnings) = parsed.Value;
                        if (items.Count == 0)
                        {
                            continue;
                        }
                        var now = _clock.Now;
                        var snap = await GetSnapshotAsync() ?? new AmisCatalogSnapshot();
                        snap.ReceivedAt = now;
                        snap.Source = "excel";
                        snap.Items[k] = items;
                        snap.ReceivedByKind[k] = now;
                        snap.SourceFiles[k] = fileName;
                        await SaveSnapshotAsync(snap);
                        var result = new AmisImportResultModel { Kind = k, KindName = AmisKind.Label(k), Sheet = ws.Name, Received = items.Count, SkippedRows = skipped, ReceivedAt = now, ListWarning = warnings };
                        return Result<AmisImportResultModel>.Success(result, string.Format(ConstantHelper.Message.AmisImported, items.Count, AmisKind.Label(k).ToLowerInvariant()));
                    }
                }
                return Result<AmisImportResultModel>.Fail(wanted == null ? ConstantHelper.Message.AmisFileUnknownKind : ConstantHelper.Message.AmisFileNoRows);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ImportExcel: ", ex.ToString()));
                throw;
            }
        }

        private static string Fold(string? s) => MenuService.NormName(s);

        /// <summary>Đọc 1 sheet danh mục AMIS: tìm dòng tiêu đề (≤ 25 dòng đầu) có cột Mã + Tên (ĐVT: cột "Đơn vị tính"), nhận loại theo tên sheet / tiêu đề.</summary>
        private static (string Kind, List<AmisCatalogItem> Items, int Skipped, List<string> Warnings)? ParseSheet(IXLWorksheet ws, string? wanted)
        {
            int last = ws.LastRowUsed()?.RowNumber() ?? 0;
            int lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
            if (last == 0 || lastCol == 0)
            {
                return null;
            }
            for (int r = 1; r <= Math.Min(25, last); r++)
            {
                var head = Enumerable.Range(1, lastCol).Select(c => (Col: c, Text: Fold(ws.Cell(r, c).GetString()))).Where(x => x.Text.Length > 0).ToList();
                if (head.Count < 2 && wanted != AmisKind.Unit)
                {
                    continue;
                }
                bool IsCode(string t) => (t == "ma" || t.StartsWith("ma ", StringComparison.Ordinal)) && !t.Contains("so thue") && !t.Contains("nhom") && !t.Contains("vach") && !t.Contains("tham chieu") && !t.Contains("tinh");
                bool IsName(string t) => (t == "ten" || t.StartsWith("ten ", StringComparison.Ordinal)) && !t.Contains("nhom") && !t.Contains("tieng anh") && !t.Contains("viet tat");
                bool IsUnit(string t) => t.Contains("don vi tinh") || t == "dvt" || t.StartsWith("dvt ", StringComparison.Ordinal);
                int code = head.FirstOrDefault(x => IsCode(x.Text)).Col;
                int name = head.FirstOrDefault(x => IsName(x.Text)).Col;
                var units = head.Where(x => IsUnit(x.Text)).ToList();
                int unit = (units.FirstOrDefault(x => x.Text.Contains("chinh")).Col is var u && u > 0) ? u : units.FirstOrDefault().Col;
                string hint = Fold(ws.Name) + " | " + string.Join(" | ", head.Select(x => x.Text))
                    + " | " + string.Join(" | ", Enumerable.Range(1, Math.Max(0, r - 1)).Select(rr => Fold(ws.Cell(rr, 1).GetString())));
                string? k = wanted ?? Detect(hint, code > 0);
                if (k == AmisKind.Unit && code == 0 && unit > 0)
                {
                    code = unit; // danh sách ĐVT của AMIS: cột "Đơn vị tính" vừa là mã vừa là tên
                    name = name > 0 ? name : unit;
                }
                if (k == null || code == 0 || name == 0)
                {
                    continue;
                }
                var items = new List<AmisCatalogItem>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var warnings = new List<string>();
                int skipped = 0, dup = 0;
                for (int i = r + 1; i <= last; i++)
                {
                    string c = ws.Cell(i, code).GetString().Trim();
                    string folded = Fold(c);
                    if (c.Length == 0 || c.Contains('=') || folded.StartsWith("tong", StringComparison.Ordinal) || folded.StartsWith("so dong", StringComparison.Ordinal))
                    {
                        skipped += c.Length == 0 ? 0 : 1;
                        continue;
                    }
                    if (!seen.Add(c))
                    {
                        dup++;
                        continue;
                    }
                    items.Add(new AmisCatalogItem
                    {
                        Code = c, Name = ws.Cell(i, name).GetString().Trim(), Unit = unit > 0 && k == AmisKind.Material ? ws.Cell(i, unit).GetString().Trim() : null,
                    });
                }
                if (dup > 0)
                {
                    warnings.Add($"{dup} dòng trùng mã – lấy dòng đầu.");
                }
                if (k == AmisKind.Material && unit == 0)
                {
                    warnings.Add("Tệp không có cột Đơn vị tính – không so ĐVT.");
                }
                return (k, items, skipped, warnings);
            }
            return null;
        }

        private static string? Detect(string hint, bool hasCode)
        {
            if (hint.Contains("khach hang"))
            {
                return AmisKind.Customer;
            }
            if (hint.Contains("nha cung cap"))
            {
                return AmisKind.Supplier;
            }
            if (hint.Contains("vat tu") || hint.Contains("hang hoa") || hint.Contains("ma hang"))
            {
                return AmisKind.Material;
            }
            if (hint.Contains("ma kho") || hint.Contains("danh sach kho") || hint.StartsWith("kho", StringComparison.Ordinal))
            {
                return AmisKind.Warehouse;
            }
            if (hint.Contains("don vi tinh") && !hasCode)
            {
                return AmisKind.Unit;
            }
            return null;
        }

        // ============================================================ khớp
        private sealed record DelRow(string Kind, Guid? Id, string? Code, string? Name, string? Unit, string[] UnitAliases, string? AmisCode, string? Label);

        private async Task<List<DelRow>> DelicareRowsAsync(string kind)
        {
            var db = _unitOfWork.DbContext;
            switch (kind)
            {
                case AmisKind.Material:
                {
                    var units = await db.UnitMeasure.AsNoTracking().Where(u => !u.IsDeleted).Select(u => new { u.Id, u.Code, u.Name, u.Symbol }).ToListAsync();
                    var byId = units.ToDictionary(u => u.Id);
                    return [.. (await db.Ingredient.AsNoTracking().Where(i => !i.IsDeleted).OrderBy(i => i.Code).Select(i => new { i.Id, i.Code, i.Name, i.BaseUnitId }).ToListAsync())
                        .Select(i =>
                        {
                            var u = i.BaseUnitId is { } uid && byId.TryGetValue(uid, out var x) ? x : null;
                            return new DelRow(kind, i.Id, i.Code, i.Name, u?.Name ?? u?.Code, u == null ? [] : new[] { u.Name, u.Symbol, u.Code }.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!).ToArray(), i.Code, null);
                        })];
                }
                case AmisKind.Unit:
                    return [.. (await db.UnitMeasure.AsNoTracking().Where(u => !u.IsDeleted).OrderBy(u => u.Code).Select(u => new { u.Id, u.Code, u.Name }).ToListAsync())
                        .Select(u => new DelRow(kind, u.Id, u.Code, u.Name, null, [], u.Name, null))];
                case AmisKind.Customer:
                    return [.. (await db.Customer.AsNoTracking().Where(c => !c.IsDeleted).OrderBy(c => c.Code).Select(c => new { c.Id, c.Code, c.Name }).ToListAsync())
                        .Select(c => new DelRow(kind, c.Id, c.Code, c.Name, null, [], c.Code, null))];
                case AmisKind.Supplier:
                    return [.. (await db.Supplier.AsNoTracking().Where(s => !s.IsDeleted).OrderBy(s => s.Code).Select(s => new { s.Id, s.Code, s.Name }).ToListAsync())
                        .Select(s => new DelRow(kind, s.Id, s.Code, s.Name, null, [], s.Code, null))];
                case AmisKind.Warehouse:
                    return [.. (await db.Warehouse.AsNoTracking().Where(w => !w.IsDeleted).OrderBy(w => w.Code).Select(w => new { w.Id, w.Code, w.Name }).ToListAsync())
                        .Select(w => new DelRow(kind, w.Id, w.Code, w.Name, null, [],
                            (w.Code != null && _misa.WarehouseMap.TryGetValue(w.Code, out var a) ? a : null) ?? (w.Name != null && _misa.WarehouseMap.TryGetValue(w.Name, out var b) ? b : null), null))];
                case AmisKind.Product:
                    return [.. _misa.ProductByMeal.OrderBy(p => MealRank(p.Key)).Select(p => new DelRow(kind, null, p.Key, MealName(p.Key), null, [], p.Value, null))];
                default:
                    return [];
            }
        }

        private static int MealRank(string m) => m.ToUpperInvariant() switch { "SANG" => 1, "TRUA" => 2, "TOI" => 3, "DEM" => 4, _ => 9 };
        private static string MealName(string m) => m.ToUpperInvariant() switch
        {
            "SANG" => "Suất sáng", "TRUA" => "Suất trưa", "TOI" => "Suất tối", "DEM" => "Suất đêm", "COM_SUAT" => "Cơm suất (bán lẻ)", _ => m,
        };

        private static List<AmisCatalogItem> AmisList(AmisCatalogSnapshot? snap, string kind)
            => snap == null ? [] : snap.Items.TryGetValue(kind == AmisKind.Product ? AmisKind.Material : kind, out var list) ? list : [];

        private static List<AmisMatchRowModel> Match(string kind, List<DelRow> rows, List<AmisCatalogItem> amis, bool hasSnapshot)
        {
            var byCode = new Dictionary<string, AmisCatalogItem>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in amis)
            {
                byCode.TryAdd(a.Code.Trim(), a);
            }
            var claimed = new HashSet<string>(rows.Where(r => r.AmisCode != null).Select(r => r.AmisCode!.Trim()), StringComparer.OrdinalIgnoreCase);
            var free = amis.Where(a => !claimed.Contains(a.Code)).ToList();
            var outRows = new List<AmisMatchRowModel>();
            foreach (var r in rows)
            {
                var row = new AmisMatchRowModel { Kind = kind, Id = r.Id, Code = r.Code, Name = r.Name, Unit = r.Unit, CanRemap = AmisKind.Remappable.Contains(kind) && r.Id != null };
                if (!hasSnapshot)
                {
                    row.Status = "WAITING";
                    row.StatusName = "Chờ danh mục AMIS";
                }
                else if (string.IsNullOrWhiteSpace(r.AmisCode))
                {
                    row.Status = AmisMatchStatus.NotConfigured;
                    row.Note = kind == AmisKind.Warehouse ? "Khai mã kho AMIS ở cấu hình Integration:Misa:WarehouseMap" : null;
                }
                else if (!byCode.TryGetValue(r.AmisCode.Trim(), out var a))
                {
                    row.AmisCode = kind is AmisKind.Product or AmisKind.Warehouse ? r.AmisCode : null;
                    row.Status = AmisMatchStatus.MissingOnAmis;
                    if (row.CanRemap)
                    {
                        row.ListSuggestion = Suggest(r.Name, free);
                    }
                }
                else
                {
                    row.AmisCode = a.Code;
                    row.AmisName = a.Name;
                    row.AmisUnit = a.Unit;
                    bool nameOk = kind is AmisKind.Product or AmisKind.Warehouse || Fold(a.Name) == Fold(r.Name);
                    if (!nameOk)
                    {
                        row.Status = AmisMatchStatus.NameMismatch;
                        row.Note = "Chọn mã AMIS đúng (ghép mã) hoặc sửa tên trên AMIS";
                        row.ListSuggestion = Suggest(r.Name, free);
                    }
                    else if (kind == AmisKind.Material && !string.IsNullOrWhiteSpace(a.Unit) && r.UnitAliases.Length > 0
                             && !r.UnitAliases.Any(x => string.Equals(x.Trim(), a.Unit!.Trim(), StringComparison.OrdinalIgnoreCase)))
                    {
                        row.Status = AmisMatchStatus.UnitMismatch;
                        row.Note = "Chứng từ gửi AMIS dùng ĐVT của AMIS";
                    }
                    else
                    {
                        row.Status = AmisMatchStatus.Matched;
                        if (kind == AmisKind.Material && !string.IsNullOrWhiteSpace(a.Unit) && r.UnitAliases.Length > 0 && !r.UnitAliases.Any(x => x.Trim() == a.Unit!.Trim()))
                        {
                            row.Note = "ĐVT viết hoa khác – coi là một";
                        }
                    }
                }
                row.StatusName = row.Status == "WAITING" ? row.StatusName : AmisMatchStatus.Label(row.Status);
                outRows.Add(row);
            }
            if (hasSnapshot && kind is not AmisKind.Product)
            {
                outRows.AddRange(free.Select(a => new AmisMatchRowModel
                {
                    Kind = kind, AmisCode = a.Code, AmisName = a.Name, AmisUnit = a.Unit, Status = AmisMatchStatus.OnlyOnAmis, StatusName = AmisMatchStatus.Label(AmisMatchStatus.OnlyOnAmis),
                }));
            }
            return outRows;
        }

        /// <summary>Gợi ý mã AMIS: tên chứa nhau trước, rồi nhiều từ chung nhất (bỏ dấu); tối đa 3.</summary>
        private static List<AmisCatalogItem> Suggest(string? name, List<AmisCatalogItem> free)
        {
            string n = Fold(name);
            if (n.Length == 0)
            {
                return [];
            }
            var words = n.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
            return [.. free.Select(a => new { a, f = Fold(a.Name) })
                .Select(x => new { x.a, score = (x.f.Length > 0 && (x.f.Contains(n) || n.Contains(x.f)) ? 100 : 0) + x.f.Split(' ', StringSplitOptions.RemoveEmptyEntries).Count(words.Contains) })
                .Where(x => x.score > 0).OrderByDescending(x => x.score).ThenBy(x => x.a.Code, StringComparer.Ordinal).Take(3).Select(x => x.a)];
        }

        public async Task<IResult<AmisMatchModel>> GetMatch(string? kind)
        {
            try
            {
                string k = string.IsNullOrWhiteSpace(kind) ? AmisKind.Material : kind.Trim().ToLowerInvariant();
                if (!AmisKind.All.Contains(k))
                {
                    return Result<AmisMatchModel>.Fail(string.Format(ConstantHelper.Message.AmisKindInvalid, kind));
                }
                var snap = await GetSnapshotAsync();
                var model = new AmisMatchModel { Kind = k, KindName = AmisKind.Label(k), HasSnapshot = snap != null, ReceivedAt = snap?.ReceivedAt, AmisRecords = snap?.Items.Values.Sum(v => v.Count) ?? 0 };
                foreach (string each in AmisKind.All)
                {
                    var rows = Match(each, await DelicareRowsAsync(each), AmisList(snap, each), snap != null);
                    var mine = rows.Where(r => r.Status != AmisMatchStatus.OnlyOnAmis).ToList();
                    model.ListKind.Add(new AmisKindCountModel
                    {
                        Kind = each, KindName = AmisKind.Label(each), Total = mine.Count, NeedsAction = mine.Count(r => AmisMatchStatus.NeedsAction(r.Status)),
                        ReceivedAt = snap != null && snap.ReceivedByKind.TryGetValue(each == AmisKind.Product ? AmisKind.Material : each, out var at) ? at : null,
                    });
                    model.Total += mine.Count;
                    model.Matched += mine.Count(r => r.Status == AmisMatchStatus.Matched);
                    model.Missing += mine.Count(r => r.Status is AmisMatchStatus.MissingOnAmis or AmisMatchStatus.NotConfigured);
                    model.Mismatch += mine.Count(r => r.Status is AmisMatchStatus.NameMismatch or AmisMatchStatus.UnitMismatch);
                    if (each == k)
                    {
                        model.ListRow = rows;
                        model.OnlyOnAmis = rows.Count(r => r.Status == AmisMatchStatus.OnlyOnAmis);
                    }
                }
                return Result<AmisMatchModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetMatch: ", ex.ToString()));
                throw;
            }
        }

        // ============================================================ ghép mã
        public async Task<IResult<AmisMatchRowModel>> Remap(AmisRemapRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                string kind = (request.Kind ?? string.Empty).Trim().ToLowerInvariant();
                string code = (request.AmisCode ?? string.Empty).Trim();
                if (!AmisKind.Remappable.Contains(kind))
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<AmisMatchRowModel>.Fail(string.Format(ConstantHelper.Message.AmisKindInvalid, request.Kind));
                }
                if (code.Length == 0)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<AmisMatchRowModel>.Fail(ConstantHelper.Message.AmisCodeRequired);
                }
                var snap = await GetSnapshotAsync();
                var amis = AmisList(snap, kind).FirstOrDefault(a => string.Equals(a.Code, code, StringComparison.OrdinalIgnoreCase));
                if (amis == null)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<AmisMatchRowModel>.Fail(string.Format(ConstantHelper.Message.AmisCodeNotInCatalog, code, AmisKind.Label(kind).ToLowerInvariant()));
                }
                code = amis.Code;
                var db = _unitOfWork.DbContext;
                string? oldCode;
                string? name;
                switch (kind)
                {
                    case AmisKind.Material:
                    {
                        var e = await db.Ingredient.FirstOrDefaultAsync(x => x.Id == request.Id && !x.IsDeleted);
                        if (e == null)
                        {
                            _unitOfWork.RollbackTransaction();
                            return Result<AmisMatchRowModel>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
                        }
                        if (await db.Ingredient.AsNoTracking().FirstOrDefaultAsync(x => x.Id != e.Id && !x.IsDeleted && x.Code == code) is { } other)
                        {
                            _unitOfWork.RollbackTransaction();
                            return Result<AmisMatchRowModel>.Fail(string.Format(ConstantHelper.Message.AmisCodeInUse, code, other.Name));
                        }
                        (oldCode, name) = (e.Code, e.Name);
                        e.Code = code;
                        _unitOfWork.IngredientRepository.Update(e);
                        await _unitOfWork.IngredientRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                        break;
                    }
                    case AmisKind.Customer:
                    {
                        var e = await db.Customer.FirstOrDefaultAsync(x => x.Id == request.Id && !x.IsDeleted);
                        if (e == null)
                        {
                            _unitOfWork.RollbackTransaction();
                            return Result<AmisMatchRowModel>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
                        }
                        if (await db.Customer.AsNoTracking().FirstOrDefaultAsync(x => x.Id != e.Id && !x.IsDeleted && x.Code == code) is { } other)
                        {
                            _unitOfWork.RollbackTransaction();
                            return Result<AmisMatchRowModel>.Fail(string.Format(ConstantHelper.Message.AmisCodeInUse, code, other.Name));
                        }
                        (oldCode, name) = (e.Code, e.Name);
                        if (string.IsNullOrWhiteSpace(e.CIF) || e.CIF == e.Code)
                        {
                            e.CIF = code; // D-63 h: CIF = mã khách
                        }
                        e.Code = code;
                        _unitOfWork.CustomerRepository.Update(e);
                        await _unitOfWork.CustomerRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                        break;
                    }
                    default:
                    {
                        var e = await db.Supplier.FirstOrDefaultAsync(x => x.Id == request.Id && !x.IsDeleted);
                        if (e == null)
                        {
                            _unitOfWork.RollbackTransaction();
                            return Result<AmisMatchRowModel>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
                        }
                        if (await db.Supplier.AsNoTracking().FirstOrDefaultAsync(x => x.Id != e.Id && !x.IsDeleted && x.Code == code) is { } other)
                        {
                            _unitOfWork.RollbackTransaction();
                            return Result<AmisMatchRowModel>.Fail(string.Format(ConstantHelper.Message.AmisCodeInUse, code, other.Name));
                        }
                        (oldCode, name) = (e.Code, e.Name);
                        e.Code = code;
                        _unitOfWork.SupplierRepository.Update(e);
                        await _unitOfWork.SupplierRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                        break;
                    }
                }
                _unitOfWork.CommitTransaction();
                var row = Match(kind, (await DelicareRowsAsync(kind)).Where(r => r.Id == request.Id).ToList(), AmisList(snap, kind), true).First();
                return Result<AmisMatchRowModel>.Success(row, string.Format(ConstantHelper.Message.AmisRemapped, name, oldCode, code));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Remap: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        // ============================================================ xuất mã cần tạo
        public async Task<IResult<FileContentModel>> ExportMissing()
        {
            try
            {
                var snap = await GetSnapshotAsync();
                if (snap == null)
                {
                    return Result<FileContentModel>.Fail(ConstantHelper.Message.AmisNoSnapshot);
                }
                using var wb = new XLWorkbook();
                int total = 0;
                foreach (string kind in AmisKind.All)
                {
                    var rows = Match(kind, await DelicareRowsAsync(kind), AmisList(snap, kind), true)
                        .Where(r => r.Status is AmisMatchStatus.MissingOnAmis or AmisMatchStatus.NotConfigured).ToList();
                    if (rows.Count == 0)
                    {
                        continue;
                    }
                    var ws = wb.AddWorksheet(AmisKind.Label(kind).Length > 31 ? AmisKind.Label(kind)[..31] : AmisKind.Label(kind));
                    string[] head = ["Mã Delicare", "Tên", "ĐVT", "Mã AMIS (cấu hình)", "Kết quả", "Gợi ý mã AMIS có sẵn"];
                    for (int c = 0; c < head.Length; c++)
                    {
                        ws.Cell(1, c + 1).Value = head[c];
                    }
                    ws.Row(1).Style.Font.Bold = true;
                    int r = 2;
                    foreach (var x in rows)
                    {
                        ws.Cell(r, 1).Value = x.Code ?? string.Empty;
                        ws.Cell(r, 2).Value = x.Name ?? string.Empty;
                        ws.Cell(r, 3).Value = x.Unit ?? string.Empty;
                        ws.Cell(r, 4).Value = x.AmisCode ?? string.Empty;
                        ws.Cell(r, 5).Value = x.StatusName;
                        ws.Cell(r, 6).Value = string.Join("; ", x.ListSuggestion.Select(s => $"{s.Code} {s.Name}"));
                        r++;
                    }
                    ws.Columns().AdjustToContents(1, 200);
                    total += rows.Count;
                }
                if (total == 0)
                {
                    return Result<FileContentModel>.Fail(ConstantHelper.Message.AmisNothingMissing);
                }
                using var ms = new MemoryStream();
                wb.SaveAs(ms);
                return Result<FileContentModel>.Success(new FileContentModel
                {
                    Content = ms.ToArray(), ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", FileName = $"Ma-can-tao-tren-AMIS-{_clock.Today:yyyyMMdd}.xlsx",
                }, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ExportMissing: ", ex.ToString()));
                throw;
            }
        }
    }
}
