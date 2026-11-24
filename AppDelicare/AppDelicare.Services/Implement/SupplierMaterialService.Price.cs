using System.Globalization;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request;
using AppDelicare.Services.Interface;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// UI-6.19 (D-100): bảng giá nhà cung cấp đúng logic + giữ lịch sử giá.
    /// Luật lưu: đơn giá &gt; 0; "giá tính theo" (PurchaseUomId, mặc định ĐVT mua / kho của nguyên liệu) phải quy đổi được về ĐVT kho; thêm giá mới cùng NCC ×
    /// nguyên liệu → giá đang mở bắt đầu trước đó tự kết thúc = ngày áp dụng mới − 1 (không báo trùng); cùng ngày bắt đầu / chồng khoảng đã đóng → báo;
    /// sửa đơn giá / đơn vị của giá đã áp dụng (ngày bắt đầu ≤ hôm nay) → không ghi đè, phải "lưu giá mới từ ngày"; ngày giao trong tuần chuẩn hóa
    /// (T2…T7, CN, HANG_NGAY); bật ưu tiên báo tên NCC bị bỏ ưu tiên. Không thêm bảng / cột.
    /// </summary>
    public partial class SupplierMaterialService
    {
        private string? _priceNote;
        public static readonly string[] Weekdays = ["T2", "T3", "T4", "T5", "T6", "T7", "CN"];

        /// <summary>Chuẩn hóa ngày giao: "T2, t4 ; T6" → "T2,T4,T6"; "hằng ngày" / "daily" / đủ 7 ngày → HANG_NGAY; không nhận diện được → giữ nguyên (giá trị cũ).</summary>
        public static string? NormalizeDeliveryDays(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }
            string s = raw.Trim().ToUpperInvariant();
            if (s is "HANG_NGAY" or "HÀNG NGÀY" or "HẰNG NGÀY" or "DAILY" or "MỖI NGÀY")
            {
                return "HANG_NGAY";
            }
            var parts = s.Replace(';', ',').Replace(' ', ',').Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Replace("THỨ", "T").Replace("CHỦ NHẬT", "CN")).ToList();
            if (parts.Count == 0 || parts.Any(p => !Weekdays.Contains(p)))
            {
                return raw.Trim();
            }
            var set = Weekdays.Where(parts.Contains).ToList();
            return set.Count == 7 ? "HANG_NGAY" : string.Join(",", set);
        }

        private async Task<decimal?> FactorAsync(Guid? from, Guid? to)
        {
            if (from == null || to == null)
            {
                return null;
            }
            return await _services.GetRequiredService<IUomConversionService>().ConvertAsync(1m, from.Value, to.Value);
        }

        /// <summary>% đúng hẹn của NCC trong N ngày: phiếu nhập có ngày nhận ≤ ngày giao dự kiến của PO (NCC chưa có lịch sử → null = coi như đạt).</summary>
        public static async Task<Dictionary<Guid, decimal?>> OnTimeAsync(AppDelicare.Data.Connection.DatabaseContext db, IReadOnlyCollection<Guid> supplierIds, DateTime today, int lookbackDays)
        {
            DateTime since = today.AddDays(-Math.Max(1, lookbackDays));
            var rows = await (from g in db.GoodsReceipt.AsNoTracking()
                              join p in db.PurchaseOrder.AsNoTracking() on g.PurchaseOrderId equals p.Id
                              where !g.IsDeleted && !p.IsDeleted && supplierIds.Contains(p.SupplierId) && g.ReceiptDate >= since
                              select new { p.SupplierId, g.ReceiptDate, Due = p.ExpectedDeliveryDate ?? p.ExpectedDate }).ToListAsync();
            return supplierIds.ToDictionary(s => s, s =>
            {
                var list = rows.Where(r => r.SupplierId == s && r.Due != null).ToList();
                return list.Count == 0 ? (decimal?)null : Math.Round(list.Count(r => r.ReceiptDate.Date <= r.Due!.Value.Date) * 100m / list.Count, 1);
            });
        }

        /// <summary>Luật B – chạy trong Insert / Update (cùng giao dịch); có thể tự kết thúc giá cũ (đánh dấu Update, ghi khi commit).</summary>
        private async Task<string> PriceRulesAsync(SupplierMaterialRequest request, SupplierMaterial? exist)
        {
            var db = _unitOfWork.DbContext;
            DateTime today = _clock.Today;
            if (!(request.UnitPrice > 0))
            {
                return ConstantHelper.Message.SupplierPriceRequired;
            }
            var ing = await db.Ingredient.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.IngredientId && !x.IsDeleted);
            Guid? baseUnit = ing?.BaseUnitId ?? ing?.BaseUomId;
            request.PurchaseUomId ??= exist?.PurchaseUomId ?? ing?.PurchaseUomId ?? baseUnit;
            if (request.PurchaseUomId != null && baseUnit != null && request.PurchaseUomId != baseUnit && await FactorAsync(request.PurchaseUomId, baseUnit) == null)
            {
                return ConstantHelper.Message.SupplierPriceUnitNotConvertible;
            }
            request.EffectiveFrom = (request.EffectiveFrom ?? exist?.EffectiveFrom ?? today).Date;
            request.EffectiveTo = request.EffectiveTo?.Date;
            request.DeliveryFrequency = NormalizeDeliveryDays(request.DeliveryFrequency);
            DateTime from = request.EffectiveFrom.Value;
            DateTime? to = request.EffectiveTo;
            if (exist != null)
            {
                bool priceChanged = exist.UnitPrice != request.UnitPrice || exist.PurchaseUomId != request.PurchaseUomId || (exist.EffectiveFrom?.Date ?? DateTime.MinValue) != from;
                if (priceChanged && exist.EffectiveFrom != null && exist.EffectiveFrom.Value.Date <= today)
                {
                    return string.Format(ConstantHelper.Message.SupplierPriceUsed, exist.EffectiveFrom.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
                }
            }
            Guid self = exist?.Id ?? Guid.Empty;
            var others = await db.SupplierMaterial.Where(x => x.SupplierId == request.SupplierId && x.IngredientId == request.IngredientId && x.Id != self && !x.IsDeleted).ToListAsync();
            var same = others.FirstOrDefault(o => (o.EffectiveFrom?.Date ?? DateTime.MinValue) == from);
            if (same != null)
            {
                return string.Format(ConstantHelper.Message.SupplierPriceSameStart, from.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
            }
            var autoEnd = exist == null
                ? others.Where(o => (o.EffectiveFrom?.Date ?? DateTime.MinValue) < from && (o.EffectiveTo == null || o.EffectiveTo.Value.Date >= from)).ToList()
                : [];
            bool Overlap(SupplierMaterial o) => (to == null || (o.EffectiveFrom?.Date ?? DateTime.MinValue) <= to.Value) && (o.EffectiveTo == null || o.EffectiveTo.Value.Date >= from);
            var conflict = others.Where(o => !autoEnd.Contains(o) && Overlap(o)).ToList();
            if (conflict.Count > 0)
            {
                var c = conflict[0];
                return string.Format(ConstantHelper.Message.SupplierPriceOverlap, (c.EffectiveFrom ?? today).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                    c.EffectiveTo?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "không hạn");
            }
            var notes = new List<string>();
            foreach (var o in autoEnd)
            {
                o.EffectiveTo = from.AddDays(-1);
                _unitOfWork.SupplierMaterialRepository.Update(o);
                notes.Add(string.Format(ConstantHelper.Message.SupplierPriceAutoEnded, (o.EffectiveFrom ?? from).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), from.AddDays(-1).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)));
            }
            if (request.IsPreferred)
            {
                var cleared = await (from m in db.SupplierMaterial.AsNoTracking()
                                     join s in db.Supplier.AsNoTracking() on m.SupplierId equals s.Id
                                     where m.IngredientId == request.IngredientId && m.Id != self && m.IsPreferred && !m.IsDeleted && m.SupplierId != request.SupplierId
                                     select s.Name ?? s.Code).Distinct().ToListAsync();
                if (cleared.Count > 0)
                {
                    notes.Add(string.Format(ConstantHelper.Message.SupplierPreferredMoved, string.Join(", ", cleared)));
                }
            }
            _priceNote = notes.Count == 0 ? null : string.Join("; ", notes);
            return string.Empty;
        }

        /// <summary>"Lưu giá mới từ ngày …": luôn tạo dòng giá mới (không ghi đè) – giá đang mở tự kết thúc hôm trước.</summary>
        public Task<IResult<bool>> SaveNewPrice(SupplierMaterialRequest request)
        {
            request.Id = null;
            request.EffectiveFrom ??= _clock.Today;
            return Save(request, ConstantHelper.TypeSave.Insert);
        }

        public async Task<IResults<SupplierPriceHistoryModel>> GetPriceHistory(Guid supplierId, Guid ingredientId)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                var ing = await db.Ingredient.AsNoTracking().FirstOrDefaultAsync(x => x.Id == ingredientId);
                Guid? baseUnit = ing?.BaseUnitId ?? ing?.BaseUomId;
                var rows = await db.SupplierMaterial.AsNoTracking().Where(x => x.SupplierId == supplierId && x.IngredientId == ingredientId && !x.IsDeleted)
                    .OrderBy(x => x.EffectiveFrom).ToListAsync();
                var units = await UnitNamesAsync(rows.Select(r => r.PurchaseUomId).Append(baseUnit));
                var list = new List<SupplierPriceHistoryModel>();
                decimal? prev = null;
                foreach (var r in rows)
                {
                    decimal? f = r.PurchaseUomId == null || r.PurchaseUomId == baseUnit ? 1m : await FactorAsync(r.PurchaseUomId, baseUnit);
                    decimal? perBase = f is > 0 && r.UnitPrice != null ? Math.Round(r.UnitPrice.Value / f.Value, 2) : null;
                    list.Add(new SupplierPriceHistoryModel
                    {
                        Id = r.Id, UnitPrice = r.UnitPrice, UnitId = r.PurchaseUomId, UnitName = r.PurchaseUomId is { } u ? units.GetValueOrDefault(u) : null,
                        PricePerBase = perBase, BaseUnitName = baseUnit is { } b ? units.GetValueOrDefault(b) : null, EffectiveFrom = r.EffectiveFrom, EffectiveTo = r.EffectiveTo,
                        ChangePct = prev is > 0 && perBase != null ? Math.Round((perBase.Value - prev.Value) / prev.Value * 100m, 1) : null, IsPreferred = r.IsPreferred, Status = r.Status,
                    });
                    prev = perBase ?? prev;
                }
                list.Reverse();
                return new Results<SupplierPriceHistoryModel>(list, list.Count) { ResponseCode = 200, ResponseMessage = ConstantHelper.Message.GetAllSuccessfully };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetPriceHistory: ", ex.ToString()));
                throw;
            }
        }

        private async Task<Dictionary<Guid, string>> UnitNamesAsync(IEnumerable<Guid?> ids)
        {
            var list = ids.Where(x => x != null).Select(x => x!.Value).Distinct().ToList();
            return await _unitOfWork.DbContext.UnitMeasure.AsNoTracking().Where(u => list.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Symbol ?? u.Code ?? u.Name ?? string.Empty);
        }

        /// <summary>Giá đang hiệu lực tại ngày của mọi NCC cho 1 nguyên liệu (quy về ĐVT kho) + ưu tiên + % đúng hẹn.</summary>
        public async Task<IResults<SupplierPriceCompareModel>> GetCompare(Guid ingredientId, DateTime? date)
        {
            try
            {
                var m = await MatrixCoreAsync((date ?? _clock.Today).Date, [ingredientId], null);
                var row = m.ListRow.FirstOrDefault();
                var list = row == null ? [] : row.ListCell.Select(c => new SupplierPriceCompareModel
                {
                    SupplierId = c.SupplierId, SupplierName = m.ListSupplier.FirstOrDefault(s => s.SupplierId == c.SupplierId)?.SupplierName, PriceId = c.PriceId, UnitPrice = c.UnitPrice,
                    UnitName = c.UnitName, PricePerBase = c.PricePerBase, IsPreferred = c.IsPreferred, IsCheapest = c.IsCheapest, LeadTimeDays = c.LeadTimeDays,
                    OnTimePct = m.ListSupplier.FirstOrDefault(s => s.SupplierId == c.SupplierId)?.OnTimePct, EffectiveTo = c.EffectiveTo,
                }).OrderBy(c => c.PricePerBase ?? decimal.MaxValue).ToList();
                return new Results<SupplierPriceCompareModel>(list, list.Count) { ResponseCode = 200, ResponseMessage = ConstantHelper.Message.GetAllSuccessfully };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetCompare: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<SupplierPriceMatrixModel>> GetMatrix(SupplierPriceMatrixRequest request)
        {
            try
            {
                var m = await MatrixCoreAsync((request.Date ?? _clock.Today).Date, null, request.CategoryId);
                string flag = (request.Flag ?? string.Empty).Trim().ToUpperInvariant();
                if (flag.Length > 0)
                {
                    m.ListRow = [.. m.ListRow.Where(r => r.ListFlag.Contains(flag))];
                }
                if (!string.IsNullOrWhiteSpace(request.Search))
                {
                    string q = request.Search.Trim().ToLower();
                    m.ListRow = [.. m.ListRow.Where(r => (r.IngredientName ?? string.Empty).ToLower().Contains(q) || (r.IngredientCode ?? string.Empty).ToLower().Contains(q))];
                }
                var used = m.ListRow.SelectMany(r => r.ListCell).Select(c => c.SupplierId).ToHashSet();
                m.ListSupplier = [.. m.ListSupplier.Where(s => used.Contains(s.SupplierId))];
                return Result<SupplierPriceMatrixModel>.Success(m, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetMatrix: ", ex.ToString()));
                throw;
            }
        }

        private async Task<SupplierPriceMatrixModel> MatrixCoreAsync(DateTime day, IReadOnlyCollection<Guid>? ingredientIds, Guid? categoryId)
        {
            var db = _unitOfWork.DbContext;
            var ings = await db.Ingredient.AsNoTracking().Where(x => !x.IsDeleted && (x.Status == null || x.Status == "Active")
                    && (ingredientIds == null || ingredientIds.Contains(x.Id)) && (categoryId == null || x.IngredientCategoryId == categoryId || (x.IngredientCategoryId == null && x.CategoryId == categoryId)))
                .Select(x => new { x.Id, x.Code, x.Name, Cat = x.IngredientCategoryId ?? x.CategoryId, Base = x.BaseUnitId ?? x.BaseUomId }).ToListAsync();
            var ingIds = ings.Select(i => i.Id).ToList();
            var prices = await db.SupplierMaterial.AsNoTracking().Where(x => !x.IsDeleted && ingIds.Contains(x.IngredientId)).ToListAsync();
            var cats = await db.IngredientCategory.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name);
            var supIds = prices.Select(p => p.SupplierId).Distinct().ToList();
            var sups = await db.Supplier.AsNoTracking().Where(s => supIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name ?? s.Code);
            var onTime = await OnTimeAsync(db, supIds, day, _rules.SupplierOnTimeLookbackDays);
            var units = await UnitNamesAsync(prices.Select(p => p.PurchaseUomId).Concat(ings.Select(i => i.Base)));
            var factors = new Dictionary<(Guid?, Guid?), decimal?>();
            async Task<decimal?> F(Guid? u, Guid? b)
            {
                if (u == null || u == b) return 1m;
                if (!factors.TryGetValue((u, b), out var f)) { f = await FactorAsync(u, b); factors[(u, b)] = f; }
                return f;
            }
            var model = new SupplierPriceMatrixModel { Date = day };
            foreach (var i in ings)
            {
                var row = new SupplierPriceRowModel { IngredientId = i.Id, IngredientCode = i.Code, IngredientName = i.Name, CategoryId = i.Cat, CategoryName = i.Cat is { } c ? cats.GetValueOrDefault(c) : null, BaseUnitName = i.Base is { } b ? units.GetValueOrDefault(b) : null };
                var mine = prices.Where(p => p.IngredientId == i.Id).ToList();
                foreach (var g in mine.GroupBy(p => p.SupplierId))
                {
                    var cur = g.Where(p => (p.Status == null || p.Status == "Active") && (p.EffectiveFrom == null || p.EffectiveFrom.Value.Date <= day) && (p.EffectiveTo == null || p.EffectiveTo.Value.Date >= day))
                        .OrderByDescending(p => p.EffectiveFrom).FirstOrDefault();
                    if (cur == null)
                    {
                        continue;
                    }
                    decimal? f = await F(cur.PurchaseUomId, i.Base);
                    var prev = g.Where(p => p.Id != cur.Id && p.EffectiveFrom < cur.EffectiveFrom).OrderByDescending(p => p.EffectiveFrom).FirstOrDefault();
                    decimal? pf = prev == null ? null : await F(prev.PurchaseUomId, i.Base);
                    decimal? per = f is > 0 && cur.UnitPrice != null ? Math.Round(cur.UnitPrice.Value / f.Value, 2) : null;
                    decimal? prevPer = pf is > 0 && prev?.UnitPrice != null ? prev.UnitPrice.Value / pf.Value : null;
                    row.ListCell.Add(new SupplierPriceCellModel
                    {
                        SupplierId = g.Key, PriceId = cur.Id, UnitPrice = cur.UnitPrice, UnitName = cur.PurchaseUomId is { } u ? units.GetValueOrDefault(u) : row.BaseUnitName, PricePerBase = per,
                        IsPreferred = cur.IsPreferred, MinimumOrderQty = cur.MinimumOrderQuantity ?? cur.MinimumOrderQty, LeadTimeDays = cur.LeadTimeDays, DeliveryDays = cur.DeliveryFrequency,
                        EffectiveFrom = cur.EffectiveFrom, EffectiveTo = cur.EffectiveTo, ChangePct = prevPer is > 0 && per != null ? Math.Round((per.Value - prevPer.Value) / prevPer.Value * 100m, 1) : null,
                        NoConversion = f == null,
                    });
                }
                var priced = row.ListCell.Where(c => c.PricePerBase != null).ToList();
                if (priced.Count > 0)
                {
                    decimal min = priced.Min(c => c.PricePerBase!.Value);
                    foreach (var cell in priced.Where(x => x.PricePerBase == min))
                    {
                        cell.IsCheapest = true;
                    }
                }
                if (row.ListCell.Count == 0) row.ListFlag.Add("NO_PRICE");
                if (row.ListCell.Count > 0 && !row.ListCell.Any(c => c.IsPreferred)) row.ListFlag.Add("NO_PREFERRED");
                if (row.ListCell.Any(c => c.EffectiveTo != null && c.EffectiveTo.Value.Date <= day.AddDays(7))) row.ListFlag.Add("EXPIRING");
                if (row.ListCell.Any(c => c.ChangePct >= _rules.ApprovalFlagPricePct)) row.ListFlag.Add("PRICE_UP");
                if (row.ListCell.Any(c => c.NoConversion)) row.ListFlag.Add("NO_CONVERSION");
                var pref = row.ListCell.FirstOrDefault(c => c.IsPreferred);
                if (pref != null && !pref.IsCheapest && row.ListCell.Any(c => c.IsCheapest && c.SupplierId != pref.SupplierId && (onTime.GetValueOrDefault(c.SupplierId) ?? 100m) >= _rules.SupplierOnTimeMinPct))
                {
                    row.ListFlag.Add("PREFERRED_NOT_CHEAPEST");
                }
                model.ListRow.Add(row);
            }
            model.ListRow = [.. model.ListRow.OrderBy(r => r.CategoryName ?? "~").ThenBy(r => r.IngredientName)];
            model.ListSupplier = [.. supIds.Select(s => new SupplierColumnModel { SupplierId = s, SupplierName = sups.GetValueOrDefault(s), OnTimePct = onTime.GetValueOrDefault(s) }).OrderBy(s => s.SupplierName)];
            model.IngredientCount = model.ListRow.Count;
            model.PricedCount = model.ListRow.Count(r => r.ListCell.Count > 0);
            model.NoPreferredCount = model.ListRow.Count(r => r.ListFlag.Contains("NO_PREFERRED"));
            model.ExpiringCount = model.ListRow.Count(r => r.ListFlag.Contains("EXPIRING"));
            model.PriceUpCount = model.ListRow.Count(r => r.ListFlag.Contains("PRICE_UP"));
            foreach (string fl in new[] { "NO_PRICE", "NO_PREFERRED", "EXPIRING", "PRICE_UP", "PREFERRED_NOT_CHEAPEST", "NO_CONVERSION" })
            {
                model.FlagCount[fl] = model.ListRow.Count(r => r.ListFlag.Contains(fl));
            }
            return model;
        }

        /// <summary>Gia hạn hàng loạt: đặt (hoặc bỏ – null) ngày kết thúc cho các dòng giá.</summary>
        public async Task<IResult<int>> ExtendMany(SupplierPriceExtendRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var ids = request.ListId ?? [];
                var rows = await _unitOfWork.DbContext.SupplierMaterial.Where(x => ids.Contains(x.Id) && !x.IsDeleted).ToListAsync();
                int n = 0;
                foreach (var r in rows)
                {
                    if (request.EffectiveTo is { } to && r.EffectiveFrom != null && to.Date < r.EffectiveFrom.Value.Date)
                    {
                        continue;
                    }
                    r.EffectiveTo = request.EffectiveTo?.Date;
                    _unitOfWork.SupplierMaterialRepository.Update(r);
                    n++;
                }
                await _unitOfWork.SupplierMaterialRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                return Result<int>.Success(n, string.Format(ConstantHelper.Message.SupplierPriceExtended, n, rows.Count - n));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ExtendMany: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        private const string QuoteSheet = "Bao gia";

        /// <summary>Mẫu báo giá của 1 NCC: nguyên liệu đang có giá + giá hiện hành + cột giá mới / đơn vị / ngày áp dụng.</summary>
        public async Task<IResult<byte[]>> ExportQuoteTemplate(Guid supplierId)
        {
            try
            {
                var m = await MatrixCoreAsync(_clock.Today, null, null);
                var sup = await _unitOfWork.DbContext.Supplier.AsNoTracking().FirstOrDefaultAsync(s => s.Id == supplierId);
                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add(QuoteSheet);
                string[] head = ["Mã nguyên liệu", "Nguyên liệu", "Đơn vị giá hiện hành", "Giá hiện hành", "Giá mới", "Đơn vị giá mới (mã)", "Áp dụng từ (dd/MM/yyyy)"];
                for (int i = 0; i < head.Length; i++) ws.Cell(1, i + 1).Value = head[i];
                ws.Range(1, 1, 1, head.Length).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#91AF42")).Font.SetFontColor(XLColor.White);
                int r = 2;
                foreach (var row in m.ListRow)
                {
                    var c = row.ListCell.FirstOrDefault(x => x.SupplierId == supplierId);
                    if (c == null) continue;
                    ws.Cell(r, 1).Value = row.IngredientCode; ws.Cell(r, 2).Value = row.IngredientName; ws.Cell(r, 3).Value = c.UnitName; ws.Cell(r, 4).Value = c.UnitPrice ?? 0;
                    ws.Cell(r, 6).Value = c.UnitName; ws.Cell(r, 7).Value = _clock.Today.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
                    r++;
                }
                ws.Cell(r + 1, 1).Value = $"Nhà cung cấp: {sup?.Name ?? sup?.Code} · ghi Giá mới (để trống = không đổi), đơn vị theo mã đơn vị tính; thêm dòng mới bằng mã nguyên liệu.";
                ws.Columns(1, 7).AdjustToContents(8, 40);
                using var stream = new MemoryStream();
                wb.SaveAs(stream);
                return Result<byte[]>.Success(stream.ToArray(), ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ExportQuoteTemplate: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>Đọc báo giá (xem trước – apply = false) hoặc áp dụng (apply = true: mỗi dòng hợp lệ = 1 giá mới theo luật B).</summary>
        public async Task<IResult<SupplierQuoteImportModel>> ImportQuote(Guid supplierId, Stream file, bool apply)
        {
            try
            {
                XLWorkbook wb;
                try { wb = new XLWorkbook(file); } catch (Exception) { return Result<SupplierQuoteImportModel>.Fail(ConstantHelper.Message.SupplierQuoteInvalid); }
                using (wb)
                {
                    var ws = wb.Worksheets.FirstOrDefault(s => s.Name == QuoteSheet) ?? wb.Worksheets.First();
                    if (!ws.Cell(1, 1).GetString().Trim().StartsWith("Mã nguyên liệu", StringComparison.OrdinalIgnoreCase))
                    {
                        return Result<SupplierQuoteImportModel>.Fail(ConstantHelper.Message.SupplierQuoteInvalid);
                    }
                    var db = _unitOfWork.DbContext;
                    var m = await MatrixCoreAsync(_clock.Today, null, null);
                    var unitsByCode = await db.UnitMeasure.AsNoTracking().Where(u => !u.IsDeleted).ToListAsync();
                    var model = new SupplierQuoteImportModel();
                    int last = ws.LastRowUsed()?.RowNumber() ?? 1;
                    for (int r = 2; r <= last; r++)
                    {
                        string code = ws.Cell(r, 1).GetString().Trim();
                        string priceText = ws.Cell(r, 5).GetString().Trim();
                        if (code.Length == 0 || priceText.Length == 0) continue;
                        var line = new SupplierQuoteLineModel { Row = r, IngredientCode = code, Text = priceText };
                        var ing = m.ListRow.FirstOrDefault(x => string.Equals(x.IngredientCode, code, StringComparison.OrdinalIgnoreCase));
                        line.IngredientId = ing?.IngredientId;
                        line.IngredientName = ing?.IngredientName ?? ws.Cell(r, 2).GetString();
                        if (!decimal.TryParse(priceText.Replace(".", string.Empty).Replace(",", "."), NumberStyles.Number, CultureInfo.InvariantCulture, out var price) || price <= 0) { line.Status = "ERROR"; line.Message = "Giá mới không hợp lệ"; model.ListLine.Add(line); continue; }
                        line.NewPrice = price;
                        string unitCode = ws.Cell(r, 6).GetString().Trim();
                        var unit = unitsByCode.FirstOrDefault(u => string.Equals(u.Code, unitCode, StringComparison.OrdinalIgnoreCase) || string.Equals(u.Symbol, unitCode, StringComparison.OrdinalIgnoreCase) || string.Equals(u.Name, unitCode, StringComparison.OrdinalIgnoreCase));
                        line.UnitId = unit?.Id; line.UnitName = unit?.Symbol ?? unit?.Code ?? unitCode;
                        string dateText = ws.Cell(r, 7).GetString().Trim();
                        line.EffectiveFrom = DateTime.TryParseExact(dateText, ["dd/MM/yyyy", "d/M/yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d.Date
                            : ws.Cell(r, 7).TryGetValue<DateTime>(out var d2) ? d2.Date : _clock.Today;
                        if (ing == null) { line.Status = "ERROR"; line.Message = "Không có mã nguyên liệu này"; model.ListLine.Add(line); continue; }
                        if (unit == null) { line.Status = "ERROR"; line.Message = "Không có đơn vị «" + unitCode + "»"; model.ListLine.Add(line); continue; }
                        var cur = ing.ListCell.FirstOrDefault(c => c.SupplierId == supplierId);
                        line.CurrentPrice = cur?.UnitPrice;
                        line.Status = cur == null ? "NEW" : cur.UnitPrice == price && cur.UnitName == line.UnitName ? "SAME" : price > (cur.UnitPrice ?? 0) ? "UP" : "DOWN";
                        if (apply && line.Status != "SAME")
                        {
                            var res = await SaveNewPrice(new SupplierMaterialRequest { SupplierId = supplierId, IngredientId = ing.IngredientId, UnitPrice = price, PurchaseUomId = unit.Id, EffectiveFrom = line.EffectiveFrom, IsPreferred = cur?.IsPreferred ?? false, Status = "Active",
                                LeadTimeDays = cur?.LeadTimeDays, MinimumOrderQuantity = cur?.MinimumOrderQty, DeliveryFrequency = cur?.DeliveryDays });
                            if (res.ResponseCode == 200) { line.Applied = true; model.Applied++; }
                            else { line.Status = "ERROR"; line.Message = res.ResponseMessage; }
                        }
                        model.ListLine.Add(line);
                    }
                    model.Up = model.ListLine.Count(l => l.Status == "UP"); model.Down = model.ListLine.Count(l => l.Status == "DOWN");
                    model.New = model.ListLine.Count(l => l.Status == "NEW"); model.Error = model.ListLine.Count(l => l.Status == "ERROR");
                    return Result<SupplierQuoteImportModel>.Success(model, apply ? string.Format(ConstantHelper.Message.SupplierQuoteApplied, model.Applied, model.Error) : ConstantHelper.Message.GetAllSuccessfully);
                }
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ImportQuote: ", ex.ToString()));
                throw;
            }
        }
    }
}
