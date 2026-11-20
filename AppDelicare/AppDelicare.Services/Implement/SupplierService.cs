using AppDelicare.Data.Share;
using AutoMapper;
using Dapper;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using AppDelicare.Services.Interface;
using Serilog;
using System.Net;
using Microsoft.EntityFrameworkCore;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;

namespace AppDelicare.Services.Implement
{
    public class SupplierService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService, IClock clock, IDocumentNumberService documentNumberService
        , Microsoft.Extensions.Options.IOptions<AppDelicare.Model.Option.AlertRulesOptions> alertRules) : ISupplierService
    {
        private readonly IDocumentNumberService _documentNumberService = documentNumberService;
        /// <summary>MA-1.3 (D-63): mã vừa cấp khi thêm mới – báo kèm trong thông báo thành công.</summary>
        private string? _createdCode;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;

        public async Task<IResults<SupplierViewModel>> GetAll(SupplierSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@CompanyId", request.CompanyId == Guid.Empty ? null : request.CompanyId);
                dynamicParameters.Add("@Status", request.Status);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<SupplierViewModel>("uspSupplier_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<SupplierViewModel>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.GetAllSuccessfully,
                    ListData = list,
                    TotalRecords = list.FirstOrDefault()?.TotalRow ?? 0
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetAll: ", ex.ToString()));
                throw;
            }
        }

        public IResults<Supplier> GetAllData()
        {
            try
            {
                var list = _unitOfWork.SupplierRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<Supplier>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.GetAllSuccessfully,
                    ListData = list,
                    TotalRecords = list.Count
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetAllData: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<bool>> Save(SupplierRequest request, string typeSave)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var result = string.Empty;
                switch (typeSave)
                {
                    case ConstantHelper.TypeSave.Insert:
                        result = await Insert(request, typeSave);
                        break;
                    case ConstantHelper.TypeSave.Update:
                        result = await Update(request, typeSave);
                        break;
                    case ConstantHelper.TypeSave.Delete:
                        result = await Delete(request.Id, typeSave);
                        break;
                    default:
                        result = "Loại thao tác lưu dữ liệu không hợp lệ.";
                        break;
                }
                _unitOfWork.CommitTransaction();
                if (!string.IsNullOrEmpty(result))
                {
                    return new Result<bool>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = result,
                        Data = false
                    };
                }
                return new Result<bool>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = typeSave == ConstantHelper.TypeSave.Delete ? ConstantHelper.Message.DeleteSuccess
                        : _createdCode != null ? string.Format(ConstantHelper.Message.SaveSuccessWithCode, "nhà cung cấp", _createdCode)
                        : ConstantHelper.Message.SaveSuccess,
                    Data = true
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Save: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        private async Task<string> Insert(SupplierRequest request, string typeSave)
        {
            var insert = _mapper.Map<Supplier>(request);
            // MA-1.3 (D-63): mã danh mục máy chủ tự sinh (…), bỏ qua giá trị gửi lên; không đổi sau khi cấp.
            insert.Code = await _documentNumberService.NextCodeAsync(DocumentKind.Supplier);
            _createdCode = insert.Code;
            _unitOfWork.SupplierRepository.Add(insert);
            await _unitOfWork.SupplierRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Update(SupplierRequest request, string typeSave)
        {
            var exist = _unitOfWork.SupplierRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (request.Id == null || exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // MA-1.3 (D-63): mã không đổi sau khi cấp – bỏ qua giá trị gửi lên.
            request.Code = exist.Code;
            var update = _mapper.Map(request, exist);
            _unitOfWork.SupplierRepository.Update(update);
            await _unitOfWork.SupplierRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Delete(Guid? id, string typeSave)
        {
            if (id == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // T07: ReferenceGuard (quan hệ logic, truy vấn có tham số) thay SQL động uspSystem_ExecuteQuery (A10).
            if (await _auditChangeService.IsReferencedAsync(nameof(Supplier), (Guid)id))
            {
                return ConstantHelper.Message.DeleteExistFail;
            }
            var delete = _unitOfWork.SupplierRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            _unitOfWork.SupplierRepository.Update(delete);
            await _unitOfWork.SupplierRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        public async Task<IResult<Supplier>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.SupplierRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<Supplier>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<Supplier>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.GetAllSuccessfully,
                    Data = data,
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetById: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>PO đã đóng (không tính "đang mở").</summary>
        private static readonly string[] ClosedPoStatuses = ["Received", "Closed", "Cancelled"];

        /// <summary>T45R (spec 4.3): tổng quan NCC – bảng giá, PO đang mở, 20 sự cố gần nhất, tỷ lệ đúng hạn / trả hàng 90 ngày.</summary>
        public async Task<IResult<SupplierOverviewModel>> GetOverview(Guid id)
        {
            try
            {
                var supplier = await _unitOfWork.SupplierRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (supplier == null)
                {
                    return Result<SupplierOverviewModel>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
                }
                var db = _unitOfWork.DbContext;
                DateTime since = clock.Today /* Rà soát BE: giờ nghiệp vụ VN */.AddDays(-90);
                var lines = await (from l in db.GoodsReceiptLine.AsNoTracking()
                                   join r in db.GoodsReceipt.AsNoTracking() on l.GoodsReceiptId equals r.Id
                                   where !l.IsDeleted && !r.IsDeleted && r.SupplierId == id && r.ReceiptDate >= since
                                   select new { l.ReceivedQuantity, l.RejectedQuantity }).ToListAsync();
                decimal received = lines.Sum(l => l.ReceivedQuantity);
                var receipts = await (from r in db.GoodsReceipt.AsNoTracking()
                                      join p in db.PurchaseOrder.AsNoTracking() on r.PurchaseOrderId equals p.Id
                                      where !r.IsDeleted && r.SupplierId == id && r.ReceiptDate >= since
                                      select new { r.ReceiptDate, Expected = p.ExpectedDeliveryDate ?? p.ExpectedDate }).ToListAsync();
                var withExpected = receipts.Where(r => r.Expected != null).ToList();
                var model = new SupplierOverviewModel
                {
                    Supplier = supplier,
                    ListMaterial = await (from m in db.SupplierMaterial.AsNoTracking()
                                          join i in db.Ingredient.AsNoTracking() on m.IngredientId equals i.Id into ig
                                          from i in ig.DefaultIfEmpty()
                                          where !m.IsDeleted && m.SupplierId == id
                                          orderby i.Name
                                          select new SupplierMaterialBriefModel
                                          {
                                              Id = m.Id, IngredientId = m.IngredientId, IngredientName = i.Name, UnitPrice = m.UnitPrice, IsPreferred = m.IsPreferred,
                                              EffectiveFrom = m.EffectiveFrom, EffectiveTo = m.EffectiveTo, Status = m.Status,
                                          }).ToListAsync(),
                    ListOpenPurchaseOrder = await db.PurchaseOrder.AsNoTracking().Where(p => !p.IsDeleted && p.SupplierId == id && !ClosedPoStatuses.Contains(p.Status!))
                        .OrderBy(p => p.ExpectedDeliveryDate).ToListAsync(),
                    ListIssue = await db.SupplierIssue.AsNoTracking().Where(x => !x.IsDeleted && x.SupplierId == id).OrderByDescending(x => x.ReportedAt).Take(20).ToListAsync(),
                    RejectRate = received > 0 ? Math.Round(lines.Sum(l => l.RejectedQuantity) / received * 100, 2) : null,
                    OnTimeRate = withExpected.Count > 0 ? Math.Round(withExpected.Count(r => r.ReceiptDate.Date <= r.Expected!.Value.Date) * 100m / withExpected.Count, 2) : null,
                    ReceiptCount90d = await db.GoodsReceipt.AsNoTracking().CountAsync(r => !r.IsDeleted && r.SupplierId == id && r.ReceiptDate >= since),
                };
                model.QcPassRate = model.RejectRate is { } rr ? 100 - rr : null;
                await FillProfileAsync(model, id);
                return Result<SupplierOverviewModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetOverview: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>PO đã cam kết với NCC – tính "trễ hạn giao" khi hẹn giao &lt; hôm nay.</summary>
        private static readonly string[] CommittedPoStatuses = [PurchaseOrderStatus.Approved, PurchaseOrderStatus.Sent, PurchaseOrderStatus.PartiallyReceived];
        private const int PriceExpiryDays = 15;

        /// <summary>UI-3 (D-67 f1): số liệu thẻ chỉ số, đánh giá và khối Cần chú ý của NCC.</summary>
        private async Task FillProfileAsync(SupplierOverviewModel m, Guid id)
        {
            var db = _unitOfWork.DbContext;
            DateTime today = clock.Today, now = clock.Now;
            var open = m.ListOpenPurchaseOrder;
            m.OpenPoCount = open.Count;
            m.OpenPoAmount = open.Sum(p => p.TotalAmount ?? 0);
            var overdue = open.Where(p => CommittedPoStatuses.Contains(p.Status) && (p.ExpectedDeliveryDate ?? p.ExpectedDate) is { } d && d.Date < today).ToList();
            m.OverduePoCount = overdue.Count;
            m.LastReceiptDate = await db.GoodsReceipt.AsNoTracking().Where(r => !r.IsDeleted && r.SupplierId == id).MaxAsync(r => (DateTime?)r.ReceiptDate);

            var issues = await db.SupplierIssue.AsNoTracking().Where(x => !x.IsDeleted && x.SupplierId == id)
                .Select(x => new { x.IssueStatus, x.DueAt, x.ClosedAt }).ToListAsync();
            bool IsOpen(string? s) => s is SupplierIssueStatus.Open or SupplierIssueStatus.InProgress;
            m.IssueCount = issues.Count;
            m.OpenIssueCount = issues.Count(x => IsOpen(x.IssueStatus));
            m.OverdueIssueCount = issues.Count(x => IsOpen(x.IssueStatus) && x.DueAt != null && x.DueAt < now);
            var withDue = issues.Where(x => x.DueAt != null).ToList();
            m.IssueOnTimePct = withDue.Count > 0
                ? Math.Round(withDue.Count(x => x.ClosedAt != null ? x.ClosedAt <= x.DueAt : x.DueAt >= now) * 100m / withDue.Count, 2) : null;

            var stock = db.InventoryBalance.AsNoTracking().Where(b => !b.IsDeleted && b.OnHandQuantity > 0);
            var lots = db.MaterialLot.AsNoTracking().Where(l => !l.IsDeleted && l.SupplierId == id && stock.Any(b => b.MaterialLotId == l.Id));
            m.LotOnHoldCount = await lots.CountAsync(l => l.QcStatus == LotQcStatus.Hold || l.Status == MaterialLotStatus.OnHold);
            m.LotExpiryDays = Math.Max(0, alertRules.Value.LotExpiryDays);
            DateTime lotUntil = today.AddDays(m.LotExpiryDays + 1);
            m.LotNearExpiryCount = await lots.CountAsync(l => l.Status == MaterialLotStatus.Available && l.ExpiryDate != null && l.ExpiryDate >= today && l.ExpiryDate < lotUntil);

            m.PriceExpiryDays = PriceExpiryDays;
            DateTime priceUntil = today.AddDays(PriceExpiryDays + 1);
            m.PriceExpiringCount = m.ListMaterial.Count(x => (x.EffectiveFrom == null || x.EffectiveFrom.Value.Date <= today)
                && x.EffectiveTo != null && x.EffectiveTo.Value.Date >= today && x.EffectiveTo.Value.Date < priceUntil);

            void Add(int n, string code, string severity, string message, string screen)
            {
                if (n > 0)
                {
                    m.ListAlert.Add(new CustomerAlertModel { Code = code, Severity = severity, Message = message, Screen = screen });
                }
            }
            Add(m.OverduePoCount, "PO_OVERDUE", "HIGH", string.Format(ConstantHelper.Message.SupplierAlertPoOverdue, m.OverduePoCount,
                string.Join(", ", overdue.Take(3).Select(p => p.PurchaseOrderNumber ?? p.PONo)) + (overdue.Count > 3 ? "…" : "")), "scm-po");
            Add(m.OverdueIssueCount, "ISSUE_OVERDUE", "HIGH", string.Format(ConstantHelper.Message.SupplierAlertIssueOverdue, m.OverdueIssueCount), "qc-supplier-issue");
            Add(m.LotOnHoldCount, "LOT_ON_HOLD", "MEDIUM", string.Format(ConstantHelper.Message.SupplierAlertLotOnHold, m.LotOnHoldCount), "scm-lot");
            Add(m.PriceExpiringCount, "PRICE_EXPIRING", "MEDIUM", string.Format(ConstantHelper.Message.SupplierAlertPriceExpiring, m.PriceExpiringCount, PriceExpiryDays), "scm-supplier-material");
            Add(m.LotNearExpiryCount, "LOT_EXPIRING", "LOW", string.Format(ConstantHelper.Message.SupplierAlertLotExpiring, m.LotNearExpiryCount, m.LotExpiryDays), "scm-lot");
        }

        /// <summary>UI-3 (D-67 f2): theo tháng – giá trị PO đặt, giá trị đã nhận (phiếu Đã ghi sổ: SL đạt × đơn giá), % đúng hạn, % bị loại.</summary>
        public async Task<IResult<SupplierTrendModel>> GetTrend(Guid id, DateTime? from, DateTime? to)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                if (!await db.Supplier.AsNoTracking().AnyAsync(x => x.Id == id && !x.IsDeleted))
                {
                    return Result<SupplierTrendModel>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
                }
                DateTime last = to ?? clock.Today;
                DateTime end = new(last.Year, last.Month, 1);
                DateTime start = from is { } f ? new DateTime(f.Year, f.Month, 1) : end.AddMonths(-5);
                int months = (end.Year - start.Year) * 12 + end.Month - start.Month + 1;
                if (months < 1 || months > 24)
                {
                    return Result<SupplierTrendModel>.Fail(ConstantHelper.Message.SupplierTrendRangeInvalid);
                }
                DateTime endExcl = end.AddMonths(1);
                var pos = await db.PurchaseOrder.AsNoTracking()
                    .Where(p => !p.IsDeleted && p.SupplierId == id && p.OrderDate >= start && p.OrderDate < endExcl && p.Status != PurchaseOrderStatus.Cancelled)
                    .Select(p => new { p.OrderDate, p.TotalAmount }).ToListAsync();
                var receipts = await (from r in db.GoodsReceipt.AsNoTracking()
                                      join p in db.PurchaseOrder.AsNoTracking() on r.PurchaseOrderId equals p.Id into pj
                                      from p in pj.DefaultIfEmpty()
                                      where !r.IsDeleted && r.SupplierId == id && r.ReceiptDate >= start && r.ReceiptDate < endExcl && r.Status != GoodsReceiptStatus.Cancelled
                                      select new { r.Id, r.ReceiptDate, r.Status, Expected = p == null ? null : p.ExpectedDeliveryDate ?? p.ExpectedDate }).ToListAsync();
                var ids = receipts.Select(r => r.Id).ToList();
                var lines = await db.GoodsReceiptLine.AsNoTracking().Where(l => !l.IsDeleted && ids.Contains(l.GoodsReceiptId))
                    .Select(l => new { l.GoodsReceiptId, l.ReceivedQuantity, l.AcceptedQuantity, l.RejectedQuantity, l.UnitCost }).ToListAsync();
                var model = new SupplierTrendModel { From = start, To = endExcl.AddDays(-1) };
                for (DateTime mo = start; mo < endExcl; mo = mo.AddMonths(1))
                {
                    DateTime next = mo.AddMonths(1);
                    var rs = receipts.Where(r => r.ReceiptDate >= mo && r.ReceiptDate < next).ToList();
                    var rIds = rs.Select(r => r.Id).ToHashSet();
                    var posted = rs.Where(r => r.Status == GoodsReceiptStatus.Posted).Select(r => r.Id).ToHashSet();
                    var ls = lines.Where(l => rIds.Contains(l.GoodsReceiptId)).ToList();
                    decimal received = ls.Sum(l => l.ReceivedQuantity);
                    var withExp = rs.Where(r => r.Expected != null).ToList();
                    model.ListMonth.Add(new SupplierTrendMonthModel
                    {
                        Month = mo,
                        PoAmount = pos.Where(p => p.OrderDate >= mo && p.OrderDate < next).Sum(p => p.TotalAmount ?? 0),
                        ReceivedAmount = Math.Round(ls.Where(l => posted.Contains(l.GoodsReceiptId)).Sum(l => l.AcceptedQuantity * (l.UnitCost ?? 0)), 0),
                        ReceiptCount = rs.Count,
                        OnTimePct = withExp.Count > 0 ? Math.Round(withExp.Count(r => r.ReceiptDate.Date <= r.Expected!.Value.Date) * 100m / withExp.Count, 2) : null,
                        RejectPct = received > 0 ? Math.Round(ls.Sum(l => l.RejectedQuantity) / received * 100, 2) : null,
                    });
                }
                return Result<SupplierTrendModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetTrend: ", ex.ToString()));
                throw;
            }
        }
    }
}
