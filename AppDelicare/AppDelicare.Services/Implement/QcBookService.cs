using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;

namespace AppDelicare.Services.Interface
{
    /// <summary>UI-6.29 (D-110): Sổ kiểm tra hàng nhập (chỉ đọc, ATTP) + tổng hợp theo nhà cung cấp + đơn mua đến hạn nhận.</summary>
    public interface IQcBookService
    {
        Task<IResult<QcBookModel>> GetBook(QcBookRequest request);
        Task<IResult<List<QcSupplierSummaryModel>>> GetSupplierSummary(QcBookRequest request);
        Task<IResult<List<PoDueModel>>> GetDueForReceiving(DateTime? date);
    }
}

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// UI-6.29 (D-110): mỗi dòng sổ = một dòng kiểm tra (QCInspectionLine) của phiếu nhận – kể cả dòng tự đạt (không yêu cầu QC).
    /// Vùng nhiệt độ tính lại theo ngưỡng hiện hành (BusinessRules:ReceivingTemp); "nhận có điều kiện" = ghi chú ưu tiên dùng trước hoặc vùng có điều kiện.
    /// Không thêm bảng / cột.
    /// </summary>
    public class QcBookService(IUnitOfWork unitOfWork, IClock clock, IOptions<BusinessRulesOptions> rules) : Interface.IQcBookService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly BusinessRulesOptions _rules = rules.Value;
        private const int MaxDays = 93;

        private (DateTime From, DateTime To) Range(QcBookRequest r)
        {
            DateTime dTo = (r.To ?? _clock.Now).Date, dFrom = (r.From ?? dTo.AddDays(-6)).Date;
            if (dFrom > dTo)
            {
                (dFrom, dTo) = (dTo, dFrom);
            }
            if ((dTo - dFrom).TotalDays > MaxDays)
            {
                dFrom = dTo.AddDays(-MaxDays);
            }
            return (dFrom, dTo);
        }

        private static bool IsTempOut(string zone) => zone is ReceivingTempZone.Conditional or ReceivingTempZone.Reject or ReceivingTempZone.Missing;

        private async Task<List<QcBookRowModel>> RowsAsync(QcBookRequest request, DateTime dFrom, DateTime dTo)
        {
            var db = _unitOfWork.DbContext;
            DateTime end = dTo.AddDays(1);
            var raw = await (from i in db.QcInspection.AsNoTracking()
                             join r in db.GoodsReceipt.AsNoTracking() on i.GoodsReceiptId equals r.Id
                             join l in db.QcInspectionLine.AsNoTracking() on i.Id equals l.QCInspectionId
                             where !i.IsDeleted && !r.IsDeleted && !l.IsDeleted && r.Status != GoodsReceiptStatus.Cancelled
                                   && i.InspectionDate >= dFrom && i.InspectionDate < end
                                   && (request.SupplierId == null || r.SupplierId == request.SupplierId)
                             select new { i, r, l }).ToListAsync();
            var ingIds = raw.Select(x => x.l.IngredientId).Distinct().ToList();
            var supIds = raw.Select(x => x.r.SupplierId).Distinct().ToList();
            var grlIds = raw.Where(x => x.l.GoodsReceiptLineId != null).Select(x => x.l.GoodsReceiptLineId!.Value).Distinct().ToList();
            var empIds = raw.Where(x => x.i.InspectorEmployeeId != null).Select(x => x.i.InspectorEmployeeId!.Value).Distinct().ToList();
            var userIds = raw.Where(x => x.i.InspectorId != null).Select(x => x.i.InspectorId!.Value).Distinct().ToList();
            var ings = await db.Ingredient.AsNoTracking().Where(x => ingIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => new { x.Name, x.StorageType });
            var sups = await db.Supplier.AsNoTracking().Where(x => supIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name);
            var grls = await db.GoodsReceiptLine.AsNoTracking().Where(x => grlIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
            var lots = (await db.MaterialLot.AsNoTracking().Where(x => !x.IsDeleted && x.GoodsReceiptLineId != null && grlIds.Contains(x.GoodsReceiptLineId.Value))
                .Select(x => new { x.Id, x.GoodsReceiptLineId, x.LotNo, x.LotNumber }).ToListAsync())
                .GroupBy(x => x.GoodsReceiptLineId!.Value).ToDictionary(g => g.Key, g => g.First());
            var emps = await db.Employee.AsNoTracking().Where(x => empIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FullName);
            var users = await db.User.AsNoTracking().Where(x => userIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.UserName);

            var rows = new List<QcBookRowModel>(raw.Count);
            foreach (var x in raw)
            {
                ings.TryGetValue(x.l.IngredientId, out var ing);
                string zone = PurchasingRules.TempZone(PurchasingRules.ReceivingTempRuleOf(_rules, ing?.StorageType), x.l.Temperature);
                GoodsReceiptLineLite? grl = x.l.GoodsReceiptLineId is { } gid && grls.TryGetValue(gid, out var g) ? new(g.ReceivedQuantity, g.AcceptedQuantity, g.RejectedQuantity) : null;
                var lot = x.l.GoodsReceiptLineId is { } lid && lots.TryGetValue(lid, out var lt) ? lt : null;
                string? inspector = x.i.InspectorEmployeeId is { } e && emps.TryGetValue(e, out var en) ? en
                    : x.i.InspectorId is { } u && users.TryGetValue(u, out var un) ? un : null;
                bool conditional = x.l.Result != IncomingQcResult.KhongDat && x.l.Result != null
                                   && (zone == ReceivingTempZone.Conditional || (x.l.Note?.Contains(ReceivingCode.UseFirstMarker) ?? false));
                rows.Add(new QcBookRowModel
                {
                    InspectionId = x.i.Id, InspectionLineId = x.l.Id, InspectionNumber = x.i.InspectionNumber, InspectionDate = x.i.InspectionDate,
                    GoodsReceiptId = x.r.Id, ReceiptNumber = x.r.ReceiptNumber ?? x.r.ReceiptNo, ReceiptStatus = x.r.Status, PurchaseOrderId = x.r.PurchaseOrderId,
                    SupplierId = x.r.SupplierId, SupplierName = sups.GetValueOrDefault(x.r.SupplierId),
                    IngredientId = x.l.IngredientId, IngredientName = ing?.Name, StorageType = ing?.StorageType,
                    Temperature = x.l.Temperature, TempZone = zone, TempZoneLabel = ReceivingTempZone.Label(zone),
                    ExpiryDate = x.l.ExpiryDate, Appearance = x.l.Appearance, Packaging = x.l.Packaging,
                    Result = x.l.Result, Conditional = conditional, AutoPass = x.i.InspectorId == null && x.i.InspectorEmployeeId == null && x.l.Result == IncomingQcResult.Dat,
                    Remark = PurchasingRules.AppendNote(x.l.Remark, x.l.Note?.Replace(ReceivingCode.UseFirstMarker, string.Empty).Trim()),
                    Inspector = inspector,
                    ReceivedQuantity = grl?.Received ?? 0m, AcceptedQuantity = grl?.Accepted ?? 0m, RejectedQuantity = grl?.Rejected ?? 0m,
                    LotId = lot?.Id, LotNo = lot?.LotNo ?? lot?.LotNumber,
                });
            }
            string kw = (request.Keyword ?? string.Empty).Trim();
            string res = (request.Result ?? string.Empty).Trim().ToUpperInvariant();
            return rows.Where(r => kw.Length == 0 || Has(r.IngredientName, kw) || Has(r.SupplierName, kw) || Has(r.ReceiptNumber, kw) || Has(r.InspectionNumber, kw) || Has(r.LotNo, kw))
                .Where(r => string.IsNullOrWhiteSpace(request.StorageType) || string.Equals(r.StorageType, request.StorageType.Trim(), StringComparison.OrdinalIgnoreCase))
                .Where(r => !request.OnlyTempOut || IsTempOut(r.TempZone))
                .Where(r => res.Length == 0 || res switch
                {
                    "PENDING" => r.Result == null,
                    "CONDITIONAL" => r.Conditional,
                    _ => string.Equals(r.Result, res, StringComparison.OrdinalIgnoreCase),
                })
                .OrderByDescending(r => r.InspectionDate).ThenBy(r => r.ReceiptNumber).ThenBy(r => r.IngredientName).ToList();
        }

        private sealed record GoodsReceiptLineLite(decimal Received, decimal Accepted, decimal Rejected);

        private static bool Has(string? s, string kw) => s?.Contains(kw, StringComparison.OrdinalIgnoreCase) ?? false;

        private static decimal? Pass(int dat, int concluded) => concluded == 0 ? null : Math.Round(100m * dat / concluded, 1);

        public async Task<IResult<QcBookModel>> GetBook(QcBookRequest request)
        {
            try
            {
                var (dFrom, dTo) = Range(request ?? new QcBookRequest());
                var rows = await RowsAsync(request ?? new QcBookRequest(), dFrom, dTo);
                int dat = rows.Count(r => r.Result == IncomingQcResult.Dat), part = rows.Count(r => r.Result == IncomingQcResult.DatMotPhan), ko = rows.Count(r => r.Result == IncomingQcResult.KhongDat);
                var model = new QcBookModel
                {
                    From = dFrom, To = dTo,
                    Summary = new QcBookSummaryModel
                    {
                        Receipts = rows.Select(r => r.GoodsReceiptId).Distinct().Count(), Lines = rows.Count,
                        Dat = dat + part, Conditional = rows.Count(r => r.Conditional), KhongDat = ko, Pending = rows.Count(r => r.Result == null),
                        TempOut = rows.Count(r => IsTempOut(r.TempZone)), Suppliers = rows.Select(r => r.SupplierId).Distinct().Count(),
                        PassPct = Pass(dat + part, dat + part + ko),
                    },
                    ListDay = rows.GroupBy(r => r.InspectionDate.Date).OrderByDescending(g => g.Key).Select(g => new QcBookDayModel
                    {
                        Date = g.Key, Lines = g.Count(), KhongDat = g.Count(r => r.Result == IncomingQcResult.KhongDat), TempOut = g.Count(r => IsTempOut(r.TempZone)), ListRow = g.ToList(),
                    }).ToList(),
                };
                return Result<QcBookModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetBook: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<List<QcSupplierSummaryModel>>> GetSupplierSummary(QcBookRequest request)
        {
            try
            {
                request ??= new QcBookRequest();
                var (dFrom, dTo) = Range(request.From == null && request.To == null ? new QcBookRequest { From = _clock.Now.Date.AddDays(-29), To = _clock.Now.Date } : request);
                var rows = await RowsAsync(new QcBookRequest { SupplierId = request.SupplierId, StorageType = request.StorageType, Keyword = request.Keyword }, dFrom, dTo);
                var list = rows.GroupBy(r => r.SupplierId).Select(g =>
                {
                    int dat = g.Count(r => r.Result is IncomingQcResult.Dat or IncomingQcResult.DatMotPhan), ko = g.Count(r => r.Result == IncomingQcResult.KhongDat);
                    return new QcSupplierSummaryModel
                    {
                        SupplierId = g.Key, SupplierName = g.First().SupplierName, Receipts = g.Select(r => r.GoodsReceiptId).Distinct().Count(), Lines = g.Count(),
                        Dat = dat, Conditional = g.Count(r => r.Conditional), KhongDat = ko, TempOut = g.Count(r => IsTempOut(r.TempZone)),
                        PassPct = Pass(dat, dat + ko), LastDate = g.Max(r => r.InspectionDate),
                    };
                }).OrderBy(s => s.PassPct ?? 101m).ThenByDescending(s => s.KhongDat).ThenBy(s => s.SupplierName).ToList();
                return Result<List<QcSupplierSummaryModel>>.Success(list, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetSupplierSummary: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>Đơn mua Đã duyệt / Đã gửi / Nhận một phần có ngày dự kiến ≤ ngày xem (hoặc chưa có ngày) và còn dòng chưa nhận đủ – quá hạn lên đầu.</summary>
        public async Task<IResult<List<PoDueModel>>> GetDueForReceiving(DateTime? date)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                DateTime day = (date ?? _clock.Now).Date, end = day.AddDays(1);
                var receivable = PurchaseOrderStatus.Receivable.ToList();
                var pos = await db.PurchaseOrder.AsNoTracking()
                    .Where(p => !p.IsDeleted && p.Status != null && receivable.Contains(p.Status)
                                && ((p.ExpectedDeliveryDate ?? p.ExpectedDate) == null || (p.ExpectedDeliveryDate ?? p.ExpectedDate) < end))
                    .ToListAsync();
                var poIds = pos.Select(p => p.Id).ToList();
                var lines = await db.PurchaseOrderLine.AsNoTracking().Where(l => !l.IsDeleted && poIds.Contains(l.PurchaseOrderId))
                    .Select(l => new { l.PurchaseOrderId, l.IngredientId, l.Quantity }).ToListAsync();
                var got = (await (from r in db.GoodsReceipt.AsNoTracking()
                                  join l in db.GoodsReceiptLine.AsNoTracking() on r.Id equals l.GoodsReceiptId
                                  where !r.IsDeleted && !l.IsDeleted && r.PurchaseOrderId != null && poIds.Contains(r.PurchaseOrderId.Value) && r.Status != GoodsReceiptStatus.Cancelled
                                  select new { PoId = r.PurchaseOrderId!.Value, l.IngredientId, Qty = r.Status == GoodsReceiptStatus.Posted || r.Status == GoodsReceiptStatus.Rejected ? l.AcceptedQuantity : l.ReceivedQuantity }).ToListAsync())
                    .GroupBy(x => (x.PoId, x.IngredientId)).ToDictionary(g => g.Key, g => g.Sum(x => x.Qty));
                var supIds = pos.Select(p => p.SupplierId).Distinct().ToList();
                var sups = await db.Supplier.AsNoTracking().Where(s => supIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name);
                var list = pos.Select(p =>
                {
                    var pl = lines.Where(l => l.PurchaseOrderId == p.Id).ToList();
                    int remaining = pl.Count(l => got.GetValueOrDefault((p.Id, l.IngredientId)) < l.Quantity);
                    DateTime? expected = p.ExpectedDeliveryDate ?? p.ExpectedDate;
                    return new PoDueModel
                    {
                        PurchaseOrderId = p.Id, PurchaseOrderNumber = p.PurchaseOrderNumber ?? p.PONo, SupplierId = p.SupplierId, SupplierName = sups.GetValueOrDefault(p.SupplierId),
                        ExpectedDate = expected, Status = p.Status, Overdue = expected != null && expected.Value.Date < day, LineCount = pl.Count, RemainingLines = remaining, TotalAmount = p.TotalAmount,
                    };
                }).Where(x => x.LineCount == 0 || x.RemainingLines > 0)
                  .OrderByDescending(x => x.Overdue).ThenBy(x => x.ExpectedDate ?? DateTime.MaxValue).ThenBy(x => x.PurchaseOrderNumber).ToList();
                return Result<List<PoDueModel>>.Success(list, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetDueForReceiving: ", ex.ToString()));
                throw;
            }
        }
    }
}
