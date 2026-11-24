using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// UI-6.18 D (D-99): màn Đơn mua hàng – tab "Chọn nhà cung cấp" (dòng đề nghị Đã duyệt chưa nằm trong PO nào – khớp theo nguyên liệu như GAP-PUR-01,
    /// mỗi dòng ≤ 3 thẻ NCC theo quy tắc gợi ý chung) + "Chờ gửi NCC" (PO Đã duyệt chưa gửi) + lập PO từ lựa chọn (mỗi NCC 1 PO; đủ 3 điều kiện → tự duyệt, không thì gửi duyệt)
    /// + gửi nhiều PO một lần. Không thêm bảng / cột.
    /// </summary>
    public partial class PurchaseOrderService
    {
        private IServiceProvider _sp => _serviceProvider;
        private static readonly string[] OpenPo = [PurchaseOrderStatus.Draft, PurchaseOrderStatus.PendingApproval, PurchaseOrderStatus.Approved, PurchaseOrderStatus.Sent, PurchaseOrderStatus.PartiallyReceived, PurchaseOrderStatus.Received, PurchaseOrderStatus.Closed];


        /// <summary>Dòng đề nghị Đã duyệt (ngày cần trong kỳ) chưa có PO cùng nguyên liệu.</summary>
        private async Task<List<(PurchaseRequestLine Line, PurchaseRequest Pr)>> NeedPickLinesAsync(DateTime? fromDay, DateTime? toDay)
        {
            var db = _unitOfWork.DbContext;
            var rows = await (from l in db.PurchaseRequestLine.AsNoTracking()
                              join p in db.PurchaseRequest.AsNoTracking() on l.PurchaseRequestId equals p.Id
                              where !l.IsDeleted && !p.IsDeleted && l.Quantity > 0 && p.Status == PurchaseRequestStatus.Approved
                                    && (fromDay == null || (l.RequiredDate ?? p.RequiredDate) >= fromDay) && (toDay == null || (l.RequiredDate ?? p.RequiredDate) <= toDay)
                              select new { l, p }).ToListAsync();
            var prIds = rows.Select(r => r.p.Id).Distinct().ToList();
            var covered = await (from po in db.PurchaseOrder.AsNoTracking()
                                 join pl in db.PurchaseOrderLine.AsNoTracking() on po.Id equals pl.PurchaseOrderId
                                 where !po.IsDeleted && !pl.IsDeleted && po.PurchaseRequestId != null && prIds.Contains(po.PurchaseRequestId.Value) && po.Status != PurchaseOrderStatus.Cancelled
                                 select new { Pr = po.PurchaseRequestId!.Value, pl.IngredientId }).ToListAsync();
            return [.. rows.Where(r => !covered.Any(c => c.Pr == r.p.Id && c.IngredientId == r.l.IngredientId)).Select(r => (r.l, r.p))];
        }

        public async Task<IResult<SupplierPickBoardModel>> GetSupplierPickBoard(SupplierPickBoardRequest request)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                var uom = _sp.GetRequiredService<IUomConversionService>();
                var lines = await NeedPickLinesAsync(request.From?.Date, request.To?.Date);
                var picks = await SupplierPick.SuggestAsync(db, uom, _rules, _clock.Today,
                    lines.Select(x => new SupplierPick.Need(x.Line.Id, x.Line.IngredientId, x.Line.Quantity, x.Line.UnitId != Guid.Empty ? x.Line.UnitId : x.Line.UomId, (x.Line.RequiredDate ?? x.Pr.RequiredDate ?? _clock.Today).Date)).ToList());
                var ingIds = lines.Select(x => x.Line.IngredientId).Distinct().ToList();
                var catOf = await (from i in db.Ingredient.AsNoTracking()
                                   join c in db.IngredientCategory.AsNoTracking() on (i.IngredientCategoryId ?? i.CategoryId) equals c.Id into cj
                                   from c in cj.DefaultIfEmpty()
                                   where ingIds.Contains(i.Id)
                                   select new { i.Id, Name = c == null ? null : c.Name }).ToDictionaryAsync(x => x.Id, x => x.Name);
                var model = new SupplierPickBoardModel();
                foreach (var x in lines)
                {
                    var p = picks[x.Line.Id];
                    p.PurchaseRequestId = x.Pr.Id;
                    p.PurchaseRequestNumber = x.Pr.RequestNumber ?? x.Pr.RequestNo;
                    p.PurchaseRequestLineId = x.Line.Id;
                    p.CategoryName = catOf.GetValueOrDefault(x.Line.IngredientId) ?? "Chưa phân nhóm";
                    model.ListLine.Add(p);
                }
                model.ListLine = [.. model.ListLine.OrderBy(l => l.CategoryName).ThenBy(l => l.IngredientName)];
                var unsent = await (from po in db.PurchaseOrder.AsNoTracking()
                                    join s in db.Supplier.AsNoTracking() on po.SupplierId equals s.Id
                                    where !po.IsDeleted && (po.Status == PurchaseOrderStatus.Draft || po.Status == PurchaseOrderStatus.PendingApproval || po.Status == PurchaseOrderStatus.Approved)
                                    select new { po.Id, No = po.PurchaseOrderNumber ?? po.PONo, po.SupplierId, SupplierName = s.Name, po.Status, po.TotalAmount, po.Note, po.PurchaseRequestId, po.OrderDate }).ToListAsync();
                model.ListUnsent = [.. unsent.Select(u => new PurchaseOrderBriefModel
                {
                    Id = u.Id, PurchaseOrderNumber = u.No, SupplierId = u.SupplierId, SupplierName = u.SupplierName, Status = u.Status, StatusName = PurchaseOrderStatus.Label(u.Status), TotalAmount = u.TotalAmount ?? 0m,
                    Source = u.PurchaseRequestId != null ? "Tự lập / từ đề nghị" : "Lập tay", AutoApproved = (u.Note ?? string.Empty).Contains("tự duyệt", StringComparison.OrdinalIgnoreCase) || (u.Note ?? string.Empty).Contains("Hệ thống tự", StringComparison.OrdinalIgnoreCase),
                }).OrderBy(u => u.SupplierName)];
                model.NeedPickCount = model.ListLine.Count;
                model.ToSendCount = model.ListUnsent.Count(u => u.Status == PurchaseOrderStatus.Approved);
                model.PendingApprovalCount = model.ListUnsent.Count(u => u.Status == PurchaseOrderStatus.PendingApproval);
                model.PickedAmount = model.ListLine.Sum(l => l.ListCard.FirstOrDefault(c => c.Suggested)?.Amount ?? 0m);
                return Result<SupplierPickBoardModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetSupplierPickBoard: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>Lập PO từ các dòng đã chọn NCC: mỗi NCC 1 PO Nháp (giá theo bảng giá hiện hành, làm tròn tối thiểu); mọi dòng NCC ưu tiên + đủ điều kiện + tổng &lt; ngưỡng GĐ → tự duyệt; không thì gửi duyệt.</summary>
        public async Task<IResult<PurchaseRequestConvertResultModel>> CreateFromPick(PurchasePickCreateRequest request)
        {
            var created = new List<Guid>();
            var result = new PurchaseRequestConvertResultModel();
            using (var tran = await _unitOfWork.OpenTransaction())
            {
                try
                {
                    var db = _unitOfWork.DbContext;
                    var uom = _sp.GetRequiredService<IUomConversionService>();
                    var input = (request.ListLine ?? []).Where(x => x.PurchaseRequestLineId != Guid.Empty && x.SupplierId != Guid.Empty).ToList();
                    var need = (await NeedPickLinesAsync(null, null)).Where(x => input.Any(i => i.PurchaseRequestLineId == x.Line.Id)).ToList();
                    if (need.Count == 0)
                    {
                        _unitOfWork.RollbackTransaction();
                        return Result<PurchaseRequestConvertResultModel>.Fail(ConstantHelper.Message.PurchaseRequestNeedsLine);
                    }
                    var picks = await SupplierPick.SuggestAsync(db, uom, _rules, _clock.Today,
                        need.Select(x => new SupplierPick.Need(x.Line.Id, x.Line.IngredientId, x.Line.Quantity, x.Line.UnitId != Guid.Empty ? x.Line.UnitId : x.Line.UomId, (x.Line.RequiredDate ?? x.Pr.RequiredDate ?? _clock.Today).Date)).ToList());
                    foreach (var bySupplier in need.GroupBy(x => input.First(i => i.PurchaseRequestLineId == x.Line.Id).SupplierId))
                    {
                        var supplier = await db.Supplier.AsNoTracking().FirstAsync(s => s.Id == bySupplier.Key);
                        var first = bySupplier.First();
                        string number = await _documentNumberService.NextAsync(DocumentKind.PurchaseOrder, _clock.Today);
                        var po = new PurchaseOrder
                        {
                            Id = Guid.NewGuid(), PurchaseOrderNumber = number, PONo = number, SupplierId = supplier.Id, PurchaseRequestId = first.Pr.Id, OrderDate = _clock.Today,
                            ExpectedDeliveryDate = bySupplier.Min(x => x.Line.RequiredDate ?? x.Pr.RequiredDate), ExpectedDate = bySupplier.Min(x => x.Line.RequiredDate ?? x.Pr.RequiredDate),
                            Status = PurchaseOrderStatus.Draft,
                            Note = "Lập từ màn Chọn nhà cung cấp – đề nghị " + string.Join(", ", bySupplier.Select(x => x.Pr.RequestNumber ?? x.Pr.RequestNo).Distinct()),
                        };
                        decimal total = 0m;
                        bool allPreferred = true, priced = true;
                        foreach (var x in bySupplier)
                        {
                            var card = picks[x.Line.Id].ListCard.FirstOrDefault(c => c.SupplierId == supplier.Id);
                            if (card?.PricePerLineUnit == null)
                            {
                                var all = await SupplierPick.SuggestAsync(db, uom, _rules, _clock.Today, [new SupplierPick.Need(x.Line.Id, x.Line.IngredientId, x.Line.Quantity, x.Line.UnitId != Guid.Empty ? x.Line.UnitId : x.Line.UomId, _clock.Today.AddDays(365))]);
                                card = all[x.Line.Id].ListCard.FirstOrDefault(c => c.SupplierId == supplier.Id);
                            }
                            decimal price = card?.PricePerLineUnit ?? 0m;
                            decimal qty = card?.OrderQuantity > 0 ? card.OrderQuantity : x.Line.Quantity;
                            priced &= price > 0;
                            allPreferred &= card is { IsPreferred: true, Eligible: true };
                            decimal amount = Math.Round(qty * price, 0, MidpointRounding.AwayFromZero);
                            Guid unit = x.Line.UnitId != Guid.Empty ? x.Line.UnitId : x.Line.UomId;
                            _unitOfWork.PurchaseOrderLineRepository.Add(new PurchaseOrderLine
                            {
                                Id = Guid.NewGuid(), PurchaseOrderId = po.Id, IngredientId = x.Line.IngredientId, Quantity = qty, UnitId = unit, UomId = unit, UnitPrice = price, LineAmount = amount, Amount = amount,
                                Note = card?.Note,
                            });
                            total += amount;
                        }
                        po.TotalAmount = total;
                        if (_rules.PurchaseOrderAutoApproveFromApprovedRequest && allPreferred && priced && total > 0 && total < _rules.PurchaseOrderDirectorApprovalAmount)
                        {
                            po.Status = PurchaseOrderStatus.Approved;
                            po.Note = PurchasingRules.AppendNote(po.Note, "Hệ thống tự duyệt – từ đề nghị đã duyệt 2 cấp, NCC ưu tiên, dưới ngưỡng Giám đốc");
                        }
                        _unitOfWork.PurchaseOrderRepository.Add(po);
                        created.Add(po.Id);
                        result.ListPurchaseOrder.Add(new PurchaseOrderBriefModel { Id = po.Id, PurchaseOrderNumber = number, SupplierId = supplier.Id, SupplierName = supplier.Name, Status = po.Status, StatusName = PurchaseOrderStatus.Label(po.Status), TotalAmount = total, LineCount = bySupplier.Count() });
                    }
                    await _unitOfWork.PurchaseOrderRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
                    // Đề nghị không còn dòng chờ chọn NCC → Đã lập đơn mua.
                    foreach (var prId in need.Select(x => x.Pr.Id).Distinct())
                    {
                        if (!(await NeedPickLinesAsync(null, null)).Any(x => x.Pr.Id == prId))
                        {
                            var pr = await db.PurchaseRequest.FirstAsync(p => p.Id == prId);
                            pr.Status = PurchaseRequestStatus.Converted;
                            _unitOfWork.PurchaseRequestRepository.Update(pr);
                        }
                    }
                    await _unitOfWork.PurchaseRequestRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                    _unitOfWork.CommitTransaction();
                }
                catch (Exception ex)
                {
                    Log.Error(string.Concat("[", GetType().Name, "]", " CreateFromPick: ", ex.ToString()));
                    _unitOfWork.RollbackTransaction();
                    throw;
                }
            }
            // PO chưa tự duyệt → gửi duyệt (cấp duyệt PO hiện có; ≥ 50 tr Giám đốc).
            foreach (var po in result.ListPurchaseOrder.Where(p => p.Status == PurchaseOrderStatus.Draft))
            {
                var sub = await Submit(new PurchaseActionRequest { Id = po.Id, Note = "Gửi duyệt từ màn Chọn nhà cung cấp" });
                if (sub.ResponseCode == 200 && sub.Data?.PurchaseOrder != null)
                {
                    po.Status = sub.Data.PurchaseOrder.Status;
                    po.StatusName = PurchaseOrderStatus.Label(po.Status);
                }
                else
                {
                    result.ListWarning.Add($"{po.PurchaseOrderNumber}: chưa gửi duyệt – {sub.ResponseMessage}");
                }
            }
            return Result<PurchaseRequestConvertResultModel>.Success(result, string.Format(ConstantHelper.Message.PurchasePickCreated, result.ListPurchaseOrder.Count,
                string.Join(", ", result.ListPurchaseOrder.Select(p => $"{p.PurchaseOrderNumber} ({p.SupplierName} – {p.StatusName})"))));
        }

        /// <summary>Gửi nhiều PO Đã duyệt cho NCC một lần (mỗi PO dùng luồng Send hiện có).</summary>
        public async Task<IResult<int>> SendMany(PurchaseOrderSendManyRequest request)
        {
            int ok = 0;
            var errors = new List<string>();
            foreach (var id in (request.ListId ?? []).Distinct())
            {
                var r = await Send(new PurchaseActionRequest { Id = id });
                if (r.ResponseCode == 200) ok++; else errors.Add(r.ResponseMessage ?? string.Empty);
            }
            return new Result<int> { ResponseCode = 200, Data = ok, ResponseMessage = string.Format(ConstantHelper.Message.PurchaseSendDone, ok, (request.ListId ?? []).Distinct().Count()) + (errors.Count > 0 ? " – " + string.Join("; ", errors.Distinct().Take(3)) : string.Empty) };
        }
    }
}
