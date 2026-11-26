using System.Globalization;
using AppDelicare.Data.Connection;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
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
    /// <summary>UI-6.50 (D-132): nhận hàng theo nhà cung cấp – thẻ NCC, gói nhận gộp nhiều đơn mua, Excel "Phiếu giao hàng" của NCC.</summary>
    public interface IReceivingService
    {
        Task<IResult<ReceivingDueModel>> GetDueBySupplier(DateTime? date);
        Task<IResult<ReceivingBundleModel>> GetReceivingBundle(Guid? supplierId, Guid? purchaseOrderId, string? keyword, DateTime? date);
        Task<IResult<FileContentModel>> DeliveryNoteTemplate(Guid? supplierId, Guid? purchaseOrderId);
        Task<IResult<List<DeliveryNoteRowModel>>> ParseDeliveryNote(Guid supplierId, Stream stream);
    }

    /// <summary>
    /// UI-6.50 (D-132 – anh chốt 2026-11-26). Không bảng / cột mới. "Còn phải nhận" của dòng đơn mua = số đặt − đã nhận (phiếu đã ghi sổ: số đạt) − đang chờ (phiếu chưa ghi sổ:
    /// số nhận) – cùng cách tính với kiểm "nhận vượt" của GoodsReceipt/Create. Số hóa đơn NCC nằm trong ghi chú phiếu nhập dạng "[HĐ NCC] số".
    /// </summary>
    public class ReceivingService(IUnitOfWork unitOfWork, IClock clock, IOptions<BusinessRulesOptions> rules) : IReceivingService
    {
        public const string InvoiceTag = "[HĐ NCC]";
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly BusinessRulesOptions _rules = rules.Value;
        private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

        public sealed record OpenLine(Guid PurchaseOrderId, Guid LineId, Guid IngredientId, Guid UnitId, decimal Ordered, decimal Remaining);

        /// <summary>Dòng đơn mua kèm số còn phải nhận (nhiều dòng cùng nguyên liệu trong 1 đơn: trừ lần lượt theo thứ tự dòng).</summary>
        public static async Task<List<OpenLine>> OpenLinesAsync(DatabaseContext db, IReadOnlyCollection<Guid> poIds)
        {
            var lines = await db.PurchaseOrderLine.AsNoTracking().Where(l => !l.IsDeleted && poIds.Contains(l.PurchaseOrderId)).OrderBy(l => l.CreatedDate).ToListAsync();
            var used = (await (from r in db.GoodsReceipt.AsNoTracking()
                               join l in db.GoodsReceiptLine.AsNoTracking() on r.Id equals l.GoodsReceiptId
                               where !r.IsDeleted && !l.IsDeleted && r.PurchaseOrderId != null && poIds.Contains(r.PurchaseOrderId.Value) && r.Status != GoodsReceiptStatus.Cancelled
                               select new { Po = r.PurchaseOrderId!.Value, l.IngredientId, Qty = r.Status == GoodsReceiptStatus.Posted ? l.AcceptedQuantity : l.ReceivedQuantity }).ToListAsync())
                .GroupBy(x => (x.Po, x.IngredientId)).ToDictionary(g => g.Key, g => g.Sum(x => x.Qty));
            var list = new List<OpenLine>();
            foreach (var l in lines)
            {
                decimal u = used.GetValueOrDefault((l.PurchaseOrderId, l.IngredientId));
                decimal take = Math.Min(u, l.Quantity);
                used[(l.PurchaseOrderId, l.IngredientId)] = u - take;
                list.Add(new OpenLine(l.PurchaseOrderId, l.Id, l.IngredientId, l.UnitId != Guid.Empty ? l.UnitId : l.UomId, l.Quantity, Math.Max(0m, l.Quantity - take)));
            }
            return list;
        }

        private static DateTime? Expected(Model.Entity.PurchaseOrder p) => p.ExpectedDeliveryDate ?? p.ExpectedDate;

        public async Task<IResult<ReceivingDueModel>> GetDueBySupplier(DateTime? date)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                DateTime now = _clock.Now, day = (date ?? now).Date, end = day.AddDays(1);
                var receivable = PurchaseOrderStatus.Receivable.ToList();
                var pos = await db.PurchaseOrder.AsNoTracking().Where(p => !p.IsDeleted && p.Status != null && receivable.Contains(p.Status)
                    && ((p.ExpectedDeliveryDate ?? p.ExpectedDate) == null || (p.ExpectedDeliveryDate ?? p.ExpectedDate) < end)).ToListAsync();
                var open = (await OpenLinesAsync(db, [.. pos.Select(p => p.Id)])).Where(l => l.Remaining > 0).ToList();
                var duePos = pos.Where(p => open.Any(l => l.PurchaseOrderId == p.Id)).ToList();
                var receipts = await db.GoodsReceipt.AsNoTracking().Where(r => !r.IsDeleted && r.Status != GoodsReceiptStatus.Cancelled && r.ReceiptDate >= day && r.ReceiptDate < end)
                    .Select(r => new { r.Id, r.SupplierId, r.ReceiptDate }).ToListAsync();
                var supIds = duePos.Select(p => p.SupplierId).Concat(receipts.Select(r => r.SupplierId)).Distinct().ToList();
                var sups = await db.Supplier.AsNoTracking().Where(s => supIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => new { s.Code, s.Name });
                var model = new ReceivingDueModel { Date = day };
                foreach (var g in duePos.GroupBy(p => p.SupplierId))
                {
                    var ids = g.Select(p => p.Id).ToHashSet();
                    DateTime? earliest = g.Select(Expected).Where(e => e != null).Min();
                    int? overdue = null;
                    if (earliest is { } e)
                    {
                        // Hẹn chỉ có ngày (00:00) → quá hẹn khi đã qua ngày đó; có giờ → quá hẹn tính theo phút.
                        bool dateOnly = e.TimeOfDay == TimeSpan.Zero;
                        if (dateOnly ? e.Date < now.Date : e < now)
                        {
                            overdue = (int)Math.Max(1, (now - (dateOnly ? e.Date.AddDays(1) : e)).TotalMinutes);
                        }
                    }
                    var rs = receipts.Where(r => r.SupplierId == g.Key).OrderByDescending(r => r.ReceiptDate).ToList();
                    model.ListSupplier.Add(new ReceivingSupplierCardModel
                    {
                        SupplierId = g.Key, SupplierCode = sups.GetValueOrDefault(g.Key)?.Code, SupplierName = sups.GetValueOrDefault(g.Key)?.Name, OrderCount = g.Count(),
                        LineCount = open.Count(l => ids.Contains(l.PurchaseOrderId)), EarliestExpected = earliest, OverdueMinutes = overdue,
                        ListOrderNumber = [.. g.OrderBy(p => Expected(p) ?? DateTime.MaxValue).Select(p => p.PurchaseOrderNumber ?? p.PONo ?? string.Empty)],
                        ReceiptCount = rs.Count, LastReceiptId = rs.FirstOrDefault()?.Id,
                    });
                }
                foreach (var g in receipts.Where(r => !duePos.Any(p => p.SupplierId == r.SupplierId)).GroupBy(r => r.SupplierId))
                {
                    model.ListSupplier.Add(new ReceivingSupplierCardModel
                    {
                        SupplierId = g.Key, SupplierCode = sups.GetValueOrDefault(g.Key)?.Code, SupplierName = sups.GetValueOrDefault(g.Key)?.Name, ReceivedFull = true,
                        ReceiptCount = g.Count(), LastReceiptId = g.OrderByDescending(r => r.ReceiptDate).First().Id,
                    });
                }
                model.ListSupplier = [.. model.ListSupplier.OrderBy(c => c.ReceivedFull).ThenByDescending(c => c.OverdueMinutes ?? -1).ThenBy(c => c.EarliestExpected ?? DateTime.MaxValue).ThenBy(c => c.SupplierName)];
                var due = model.ListSupplier.Where(c => !c.ReceivedFull).ToList();
                model.SupplierCount = due.Count;
                model.OrderCount = due.Sum(c => c.OrderCount);
                model.LineCount = due.Sum(c => c.LineCount);
                model.ReceivedFullCount = model.ListSupplier.Count(c => c.ReceivedFull);
                model.OverdueCount = due.Count(c => c.OverdueMinutes != null);
                model.QcPendingCount = await db.GoodsReceipt.AsNoTracking().CountAsync(r => !r.IsDeleted && r.Status == GoodsReceiptStatus.QcPending);
                return Result<ReceivingDueModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetDueBySupplier: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<ReceivingBundleModel>> GetReceivingBundle(Guid? supplierId, Guid? purchaseOrderId, string? keyword, DateTime? date)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                DateTime day = (date ?? _clock.Now).Date, end = day.AddDays(1);
                var receivable = PurchaseOrderStatus.Receivable.ToList();
                string? q = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
                Guid? matchedPo = purchaseOrderId;
                string? matchedBy = supplierId != null ? "supplier" : purchaseOrderId != null ? "order" : null;
                if (purchaseOrderId is { } pid)
                {
                    supplierId = await db.PurchaseOrder.AsNoTracking().Where(p => p.Id == pid && !p.IsDeleted).Select(p => (Guid?)p.SupplierId).FirstOrDefaultAsync();
                }
                else if (supplierId == null && q != null)
                {
                    string qu = q.ToUpperInvariant();
                    var byNo = await db.PurchaseOrder.AsNoTracking().Where(p => !p.IsDeleted && ((p.PurchaseOrderNumber != null && p.PurchaseOrderNumber.ToUpper() == qu) || (p.PONo != null && p.PONo.ToUpper() == qu)))
                        .Select(p => new { p.Id, p.SupplierId }).FirstOrDefaultAsync()
                        ?? await db.PurchaseOrder.AsNoTracking().Where(p => !p.IsDeleted && p.Status != null && receivable.Contains(p.Status) && p.PurchaseOrderNumber != null && p.PurchaseOrderNumber.ToUpper().EndsWith(qu))
                            .OrderByDescending(p => p.OrderDate).Select(p => new { p.Id, p.SupplierId }).FirstOrDefaultAsync();
                    if (byNo != null)
                    {
                        (supplierId, matchedPo, matchedBy) = (byNo.SupplierId, byNo.Id, "order");
                    }
                    else
                    {
                        string tag = InvoiceTag + " " + q;
                        var byInvoice = await db.GoodsReceipt.AsNoTracking().Where(r => !r.IsDeleted && r.Note != null && r.Note.Contains(tag)).OrderByDescending(r => r.ReceiptDate)
                            .Select(r => (Guid?)r.SupplierId).FirstOrDefaultAsync();
                        if (byInvoice != null)
                        {
                            (supplierId, matchedBy) = (byInvoice, "invoice");
                        }
                        else
                        {
                            var openSup = await db.PurchaseOrder.AsNoTracking().Where(p => !p.IsDeleted && p.Status != null && receivable.Contains(p.Status)).Select(p => p.SupplierId).Distinct().ToListAsync();
                            var cands = await db.Supplier.AsNoTracking().Where(s => !s.IsDeleted && openSup.Contains(s.Id) && ((s.Name != null && s.Name.Contains(q)) || (s.Code != null && s.Code.Contains(q))))
                                .Select(s => new { s.Id, s.Name }).ToListAsync();
                            if (cands.Count > 1)
                            {
                                return Result<ReceivingBundleModel>.Fail(string.Format(Msg.ManySuppliers, q, string.Join(", ", cands.Take(5).Select(c => c.Name))));
                            }
                            (supplierId, matchedBy) = (cands.FirstOrDefault()?.Id, "keyword");
                        }
                    }
                }
                if (supplierId == null)
                {
                    return Result<ReceivingBundleModel>.Fail(q == null ? Msg.NeedSupplier : string.Format(Msg.NotFound, q));
                }
                var sup = await db.Supplier.AsNoTracking().FirstOrDefaultAsync(s => s.Id == supplierId);
                var pos = await db.PurchaseOrder.AsNoTracking().Where(p => !p.IsDeleted && p.SupplierId == supplierId && p.Status != null && receivable.Contains(p.Status)).ToListAsync();
                var open = (await OpenLinesAsync(db, [.. pos.Select(p => p.Id)])).Where(l => l.Remaining > 0).ToList();
                pos = [.. pos.Where(p => open.Any(l => l.PurchaseOrderId == p.Id)).OrderBy(p => p.OrderDate).ThenBy(p => p.CreatedDate).ThenBy(p => p.PurchaseOrderNumber)];
                if (pos.Count == 0)
                {
                    string? poNo = matchedPo is { } m ? await db.PurchaseOrder.AsNoTracking().Where(p => p.Id == m).Select(p => p.PurchaseOrderNumber ?? p.PONo).FirstOrDefaultAsync() : null;
                    return Result<ReceivingBundleModel>.Fail(string.Format(Msg.NothingOpen, poNo != null ? "Đơn mua " + poNo : sup?.Name));
                }
                var order = pos.Select((p, i) => (p.Id, i)).ToDictionary(x => x.Id, x => x.i);
                var ingIds = open.Select(l => l.IngredientId).Distinct().ToList();
                var ings = await db.Ingredient.AsNoTracking().Where(i => ingIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id);
                var unitIds = open.Select(l => l.UnitId).Distinct().ToList();
                var units = await db.UnitMeasure.AsNoTracking().Where(u => unitIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name ?? u.Code);
                var codes = (await db.SupplierMaterial.AsNoTracking().Where(m => !m.IsDeleted && m.SupplierId == supplierId && ingIds.Contains(m.IngredientId) && m.SupplierMaterialCode != null)
                    .Select(m => new { m.IngredientId, m.SupplierMaterialCode }).ToListAsync()).GroupBy(m => m.IngredientId).ToDictionary(g => g.Key, g => g.First().SupplierMaterialCode);
                var model = new ReceivingBundleModel
                {
                    SupplierId = supplierId.Value, SupplierCode = sup?.Code, SupplierName = sup?.Name, MatchedBy = matchedBy, MatchedText = q,
                    DefaultWarehouseId = await db.GoodsReceipt.AsNoTracking().Where(r => !r.IsDeleted && r.SupplierId == supplierId && r.Status != GoodsReceiptStatus.Cancelled)
                        .OrderByDescending(r => r.ReceiptDate).Select(r => (Guid?)r.WarehouseId).FirstOrDefaultAsync(),
                    ListOrder = [.. pos.Select(p => new ReceivingOrderModel
                    {
                        PurchaseOrderId = p.Id, PurchaseOrderNumber = p.PurchaseOrderNumber ?? p.PONo, ExpectedDate = Expected(p), LineCount = open.Count(l => l.PurchaseOrderId == p.Id),
                        Due = Expected(p) == null || Expected(p) < end, Matched = p.Id == matchedPo,
                    })],
                };
                var poNo2 = pos.ToDictionary(p => p.Id, p => p.PurchaseOrderNumber ?? p.PONo);
                foreach (var g in open.GroupBy(l => (l.IngredientId, l.UnitId)))
                {
                    var ing = ings.GetValueOrDefault(g.Key.IngredientId);
                    var rule = PurchasingRules.ReceivingTempRuleOf(_rules, ing?.StorageType);
                    model.ListLine.Add(new ReceivingLineModel
                    {
                        Key = $"{g.Key.IngredientId:N}.{g.Key.UnitId:N}", IngredientId = g.Key.IngredientId, IngredientCode = ing?.Code, IngredientName = ing?.Name,
                        SupplierMaterialCode = codes.GetValueOrDefault(g.Key.IngredientId), UnitId = g.Key.UnitId, UnitName = units.GetValueOrDefault(g.Key.UnitId), StorageType = ing?.StorageType,
                        LotControl = ing?.LotControl == true, ExpiryControl = ing?.ExpiryControl == true || ing?.IsExpiryManaged == true, QcRequired = ing?.QcRequired != false,
                        ShelfLifeDays = ing?.ShelfLifeDays, TempAcceptMax = rule?.AcceptMax, TempConditionalMax = rule?.ConditionalMax,
                        Ordered = g.Sum(l => l.Ordered), Remaining = g.Sum(l => l.Remaining),
                        ListPart = [.. g.OrderBy(l => order[l.PurchaseOrderId]).Select(l => new ReceivingPartModel
                        {
                            PurchaseOrderId = l.PurchaseOrderId, PurchaseOrderNumber = poNo2[l.PurchaseOrderId], PurchaseOrderLineId = l.LineId, Ordered = l.Ordered, Remaining = l.Remaining,
                        })],
                    });
                }
                model.ListLine = [.. model.ListLine.OrderBy(l => l.IngredientName)];
                model.NeedsVehicleTemperature = model.ListLine.Any(l => l.TempAcceptMax != null);
                return Result<ReceivingBundleModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetReceivingBundle: ", ex.ToString()));
                throw;
            }
        }

        private static readonly string[] NoteHead = ["Số đơn mua", "Mã hàng NCC", "Tên nguyên liệu", "ĐVT", "Số đặt", "Số giao", "Lô NCC", "NSX (dd/MM/yyyy)", "Hạn dùng (dd/MM/yyyy)"];

        /// <summary>Excel "Phiếu giao hàng" gửi NCC: dòng còn phải giao của 1 đơn mua hoặc mọi đơn còn mở của NCC; NCC điền số giao / lô / NSX / hạn dùng.</summary>
        public async Task<IResult<FileContentModel>> DeliveryNoteTemplate(Guid? supplierId, Guid? purchaseOrderId)
        {
            var db = _unitOfWork.DbContext;
            var receivable = PurchaseOrderStatus.Receivable.ToList();
            var pos = await db.PurchaseOrder.AsNoTracking().Where(p => !p.IsDeleted && (purchaseOrderId != null ? p.Id == purchaseOrderId
                    : p.SupplierId == supplierId && p.Status != null && receivable.Contains(p.Status))).OrderBy(p => p.OrderDate).ToListAsync();
            if (pos.Count == 0)
            {
                return Result<FileContentModel>.Fail(Msg.NeedSupplier);
            }
            Guid sid = pos[0].SupplierId;
            var open = (await OpenLinesAsync(db, [.. pos.Select(p => p.Id)])).Where(l => l.Remaining > 0 || purchaseOrderId != null).ToList();
            var ingIds = open.Select(l => l.IngredientId).Distinct().ToList();
            var ings = await db.Ingredient.AsNoTracking().Where(i => ingIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.Name);
            var units = await db.UnitMeasure.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.Name ?? u.Code);
            var codes = (await db.SupplierMaterial.AsNoTracking().Where(m => !m.IsDeleted && m.SupplierId == sid && ingIds.Contains(m.IngredientId) && m.SupplierMaterialCode != null)
                .Select(m => new { m.IngredientId, m.SupplierMaterialCode }).ToListAsync()).GroupBy(m => m.IngredientId).ToDictionary(g => g.Key, g => g.First().SupplierMaterialCode);
            string? supName = await db.Supplier.AsNoTracking().Where(s => s.Id == sid).Select(s => s.Name).FirstOrDefaultAsync();
            string title = purchaseOrderId != null ? (pos[0].PurchaseOrderNumber ?? pos[0].PONo ?? string.Empty) : (supName ?? string.Empty);
            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Phieu giao hang");
            ws.Cell(1, 1).Value = "Phiếu giao hàng – " + title;
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(2, 1).Value = $"Nhà cung cấp: {supName} · NCC điền cột Số giao, Lô NCC, NSX, Hạn dùng rồi gửi lại / mang theo khi giao (Delicare tải lên form Nhận hàng).";
            for (int c = 0; c < NoteHead.Length; c++)
            {
                ws.Cell(4, c + 1).Value = NoteHead[c];
                ws.Cell(4, c + 1).Style.Font.Bold = true;
                ws.Cell(4, c + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#EFF5E5");
            }
            int r = 5;
            var poNo = pos.ToDictionary(p => p.Id, p => p.PurchaseOrderNumber ?? p.PONo);
            foreach (var l in open.OrderBy(l => pos.FindIndex(p => p.Id == l.PurchaseOrderId)).ThenBy(l => ings.GetValueOrDefault(l.IngredientId)))
            {
                ws.Cell(r, 1).Value = poNo[l.PurchaseOrderId];
                ws.Cell(r, 2).Value = codes.GetValueOrDefault(l.IngredientId);
                ws.Cell(r, 3).Value = ings.GetValueOrDefault(l.IngredientId);
                ws.Cell(r, 4).Value = units.GetValueOrDefault(l.UnitId);
                ws.Cell(r, 5).Value = purchaseOrderId != null ? l.Ordered : l.Remaining;
                ws.Cell(r, 5).Style.NumberFormat.Format = "#,##0.###";
                r++;
            }
            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return Result<FileContentModel>.Success(new FileContentModel
            {
                Content = ms.ToArray(), ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                FileName = $"Phieu-giao-hang-{(purchaseOrderId != null ? title : "NCC")}.xlsx".Replace(' ', '-'),
            }, ConstantHelper.Message.GetAllSuccessfully);
        }

        private static string Norm(string? s) => new string((s ?? string.Empty).Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray()).Replace('đ', 'd').Replace('Đ', 'D').ToLowerInvariant().Trim();

        /// <summary>Đọc Excel phiếu giao NCC (theo mẫu): khớp dòng theo số đơn + mã hàng NCC (hoặc tên nguyên liệu) với dòng đơn còn mở của NCC. Không lưu tệp.</summary>
        public async Task<IResult<List<DeliveryNoteRowModel>>> ParseDeliveryNote(Guid supplierId, Stream stream)
        {
            var db = _unitOfWork.DbContext;
            var rows = new List<DeliveryNoteRowModel>();
            try
            {
                using var wb = new XLWorkbook(stream);
                var ws = wb.Worksheets.First();
                int head = 0;
                var col = new Dictionary<string, int>();
                foreach (var row in ws.RowsUsed().Take(15))
                {
                    var cells = row.CellsUsed().ToDictionary(c => Norm(c.GetString()), c => c.Address.ColumnNumber);
                    if (cells.Keys.Any(k => k.StartsWith("so don mua")))
                    {
                        head = row.RowNumber();
                        string[] keys = ["so don mua", "ma hang ncc", "ten nguyen lieu", "so giao", "lo ncc", "nsx", "han dung"];
                        foreach (var k in keys)
                        {
                            var hit = cells.FirstOrDefault(c => c.Key.StartsWith(k));
                            if (hit.Key != null)
                            {
                                col[k] = hit.Value;
                            }
                        }
                        break;
                    }
                }
                if (head == 0 || !col.ContainsKey("so giao"))
                {
                    return Result<List<DeliveryNoteRowModel>>.Fail(Msg.BadTemplate);
                }
                string? S(IXLRow row, string k) => col.TryGetValue(k, out int c) ? (row.Cell(c).GetString().Trim() is { Length: > 0 } v ? v : null) : null;
                DateTime? Dt(IXLRow row, string k)
                {
                    if (!col.TryGetValue(k, out int c) || row.Cell(c).IsEmpty())
                    {
                        return null;
                    }
                    if (row.Cell(c).TryGetValue(out DateTime d))
                    {
                        return d.Date;
                    }
                    return DateTime.TryParseExact(row.Cell(c).GetString().Trim(), ["dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var p) ? p : null;
                }
                foreach (var row in ws.RowsUsed().Where(r => r.RowNumber() > head))
                {
                    var cell = row.Cell(col["so giao"]);
                    decimal? qty = cell.IsEmpty() ? null : cell.TryGetValue(out double dq) ? (decimal)dq
                        : decimal.TryParse(cell.GetString().Replace(".", string.Empty).Replace(",", "."), NumberStyles.Number, CultureInfo.InvariantCulture, out var pq) ? pq : null;
                    string? po = S(row, "so don mua"), code = S(row, "ma hang ncc"), name = S(row, "ten nguyen lieu");
                    if (po == null && code == null && name == null)
                    {
                        continue;
                    }
                    rows.Add(new DeliveryNoteRowModel
                    {
                        Row = row.RowNumber(), PurchaseOrderNumber = po, ItemCode = code, ItemName = name, Quantity = qty, SupplierLotNumber = S(row, "lo ncc"),
                        ManufactureDate = Dt(row, "nsx"), ExpiryDate = Dt(row, "han dung"),
                    });
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.Warning(string.Concat("[", GetType().Name, "]", " ParseDeliveryNote: ", ex.Message));
                return Result<List<DeliveryNoteRowModel>>.Fail(Msg.BadTemplate);
            }
            var receivable = PurchaseOrderStatus.Receivable.ToList();
            var pos = await db.PurchaseOrder.AsNoTracking().Where(p => !p.IsDeleted && p.SupplierId == supplierId && p.Status != null && receivable.Contains(p.Status)).ToListAsync();
            var open = await OpenLinesAsync(db, [.. pos.Select(p => p.Id)]);
            var poByNo = pos.GroupBy(p => (p.PurchaseOrderNumber ?? p.PONo ?? string.Empty).ToUpperInvariant()).ToDictionary(g => g.Key, g => g.First().Id);
            var ingIds = open.Select(l => l.IngredientId).Distinct().ToList();
            var ingNames = await db.Ingredient.AsNoTracking().Where(i => ingIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => Norm(i.Name));
            var codes = await db.SupplierMaterial.AsNoTracking().Where(m => !m.IsDeleted && m.SupplierId == supplierId && m.SupplierMaterialCode != null)
                .Select(m => new { m.IngredientId, m.SupplierMaterialCode }).ToListAsync();
            foreach (var r in rows)
            {
                Guid? poId = r.PurchaseOrderNumber is { } n && poByNo.TryGetValue(n.ToUpperInvariant(), out var p) ? p : null;
                Guid? ing = (r.ItemCode is { } c ? codes.FirstOrDefault(x => string.Equals(x.SupplierMaterialCode, c, StringComparison.OrdinalIgnoreCase))?.IngredientId : null)
                    ?? (r.ItemName is { } nm ? ingNames.FirstOrDefault(x => x.Value == Norm(nm)).Key : null);
                if (ing == Guid.Empty)
                {
                    ing = null;
                }
                var line = open.Where(l => l.IngredientId == ing && (poId == null || l.PurchaseOrderId == poId)).OrderByDescending(l => l.Remaining).FirstOrDefault();
                (r.IngredientId, r.PurchaseOrderLineId) = (ing, line?.LineId);
                r.Problem = r.PurchaseOrderNumber != null && poId == null ? $"Không có đơn mua {r.PurchaseOrderNumber} đang chờ nhận của NCC này"
                    : ing == null ? "Không khớp mã hàng NCC / tên nguyên liệu – chọn tay"
                    : line == null ? "Nguyên liệu không có trong đơn mua còn mở"
                    : r.Quantity is null or < 0 ? "Thiếu số giao" : null;
            }
            return Result<List<DeliveryNoteRowModel>>.Success(rows, string.Format(Msg.Parsed, rows.Count(r => r.Problem == null), rows.Count));
        }

        public static class Msg
        {
            public const string NeedSupplier = "Chọn nhà cung cấp hoặc đơn mua";
            public const string NotFound = "Không tìm thấy NCC / đơn mua / số hóa đơn «{0}» đang chờ nhận";
            public const string ManySuppliers = "«{0}» khớp nhiều NCC: {1} – gõ rõ hơn hoặc bấm thẻ NCC";
            public const string NothingOpen = "{0} không còn đơn mua nào chờ nhận";
            public const string BadTemplate = "Tệp không đúng mẫu phiếu giao hàng – cần cột «Số đơn mua», «Số giao» (tải mẫu ở form Nhận hàng / trang đơn mua)";
            public const string Parsed = "Khớp {0} / {1} dòng phiếu giao";
        }
    }
}
