using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;
using Microsoft.EntityFrameworkCore;
using AppDelicare.Services.Interface;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// UI-6.18 A, B (D-99): đề nghị mua đúng chuẩn chứng từ – tạo kèm dòng (số / ngày lập / người đề nghị máy chủ tự điền, luôn Nháp, gộp nguyên liệu trùng, chặn 0 dòng /
    /// số ≤ 0), sửa dòng khi Nháp, gợi ý NCC từng dòng (SupplierPick), duyệt cấp cuối → hệ thống tự lập đơn mua (lỗi không làm hỏng việc duyệt).
    /// </summary>
    public partial class PurchaseRequestService
    {
        public const string AutoNoteMarker = "[TU_LAP]";

        private async Task<(List<PurchaseRequestLine> Lines, string? Error)> BuildLinesAsync(Guid prId, DateTime? requiredDate, IReadOnlyList<PurchaseRequestLineInput> input)
        {
            var db = _unitOfWork.DbContext;
            var valid = (input ?? []).Where(l => l.IngredientId != Guid.Empty).ToList();
            if (valid.Count == 0)
            {
                return ([], ConstantHelper.Message.PurchaseRequestNeedsLine);
            }
            if (valid.Any(l => !(l.Quantity > 0)))
            {
                return ([], ConstantHelper.Message.PurchaseRequestQuantityInvalid);
            }
            var ids = valid.Select(l => l.IngredientId).Distinct().ToList();
            var ings = await db.Ingredient.AsNoTracking().Where(i => ids.Contains(i.Id) && !i.IsDeleted).ToDictionaryAsync(i => i.Id);
            if (ings.Count != ids.Count)
            {
                return ([], string.Format(ConstantHelper.Message.NotExist, "Nguyên liệu").Trim());
            }
            var result = new List<PurchaseRequestLine>();
            foreach (var g in valid.GroupBy(l => (l.IngredientId, Unit: l.UnitId ?? ings[l.IngredientId].BaseUnitId ?? ings[l.IngredientId].BaseUomId ?? Guid.Empty)))
            {
                if (g.Key.Unit == Guid.Empty)
                {
                    return ([], string.Format(ConstantHelper.Message.PurchaseRequestLineUnitMissing, ings[g.Key.IngredientId].Name));
                }
                result.Add(new PurchaseRequestLine
                {
                    Id = Guid.NewGuid(), PurchaseRequestId = prId, IngredientId = g.Key.IngredientId, Quantity = g.Sum(x => x.Quantity), UnitId = g.Key.Unit, UomId = g.Key.Unit,
                    RequiredDate = (g.Min(x => x.RequiredDate) ?? requiredDate)?.Date, Note = string.Join("; ", g.Select(x => x.Note).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct()) is { Length: > 0 } n ? n : null,
                });
            }
            return (result, null);
        }

        public async Task<IResult<PurchaseRequestDetailModel>> CreateWithLines(PurchaseRequestCreateRequest request)
        {
            Guid id = Guid.NewGuid();
            using (var tran = await _unitOfWork.OpenTransaction())
            {
                try
                {
                    var db = _unitOfWork.DbContext;
                    if (request.RequiredDate is { } rd && rd.Date < _clock.Today)
                    {
                        _unitOfWork.RollbackTransaction();
                        return Result<PurchaseRequestDetailModel>.Fail(ConstantHelper.Message.PurchaseRequestDatePast);
                    }
                    var (lines, error) = await BuildLinesAsync(id, request.RequiredDate, request.ListLine);
                    if (error != null)
                    {
                        _unitOfWork.RollbackTransaction();
                        return Result<PurchaseRequestDetailModel>.Fail(error);
                    }
                    Guid? actor = _currentActor.Actor?.UserId;
                    Guid? person = actor == null ? null : await db.User.AsNoTracking().Where(u => u.Id == actor).Select(u => u.PersonId).FirstOrDefaultAsync();
                    Guid? employee = person == null ? null : await db.Employee.AsNoTracking().Where(e => e.PersonId == person && !e.IsDeleted).Select(e => (Guid?)e.Id).FirstOrDefaultAsync();
                    string number = await _documentNumberService.NextAsync(DocumentKind.PurchaseRequest, _clock.Today);
                    _unitOfWork.PurchaseRequestRepository.Add(new PurchaseRequest
                    {
                        Id = id, RequestNumber = number, RequestNo = number, RequestDate = _clock.Now, RequesterId = actor, RequestedByEmployeeId = employee,
                        RequiredDate = (request.RequiredDate ?? lines.Min(l => l.RequiredDate))?.Date, Status = PurchaseRequestStatus.Draft, Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
                    });
                    foreach (var l in lines)
                    {
                        _unitOfWork.PurchaseRequestLineRepository.Add(l);
                    }
                    await _unitOfWork.PurchaseRequestRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
                    _unitOfWork.CommitTransaction();
                }
                catch (Exception ex)
                {
                    Log.Error(string.Concat("[", GetType().Name, "]", " CreateWithLines: ", ex.ToString()));
                    _unitOfWork.RollbackTransaction();
                    throw;
                }
            }
            if (request.Submit)
            {
                var sub = await Submit(new PurchaseActionRequest { Id = id });
                if (sub.ResponseCode != 200)
                {
                    var draft = await DetailAsync(id);
                    return new Result<PurchaseRequestDetailModel> { ResponseCode = 200, ResponseMessage = ConstantHelper.Message.SaveSuccess + " – chưa gửi duyệt: " + sub.ResponseMessage, Data = draft };
                }
                return sub;
            }
            return Result<PurchaseRequestDetailModel>.Success((await DetailAsync(id))!, ConstantHelper.Message.SaveSuccess);
        }

        /// <summary>Sửa dòng đề nghị Nháp (thay toàn bộ dòng – cùng luật tạo mới).</summary>
        public async Task<IResult<PurchaseRequestDetailModel>> UpdateLines(PurchaseRequestUpdateLinesRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var db = _unitOfWork.DbContext;
                var pr = _unitOfWork.PurchaseRequestRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
                if (pr == null || pr.Status != PurchaseRequestStatus.Draft)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<PurchaseRequestDetailModel>.Fail(pr == null ? ConstantHelper.Message.RecordNotFoundMessage : InvalidStatus(pr, "sửa dòng đề nghị mua", PurchaseRequestStatus.Draft));
                }
                if (request.RequiredDate != null)
                {
                    pr.RequiredDate = request.RequiredDate.Value.Date;
                }
                if (request.Note != null)
                {
                    pr.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
                }
                var (lines, error) = await BuildLinesAsync(pr.Id, pr.RequiredDate, request.ListLine);
                if (error != null)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<PurchaseRequestDetailModel>.Fail(error);
                }
                foreach (var old in await db.PurchaseRequestLine.Where(l => l.PurchaseRequestId == pr.Id && !l.IsDeleted).ToListAsync())
                {
                    _unitOfWork.PurchaseRequestLineRepository.Update(old);
                    old.IsDeleted = true;
                }
                foreach (var l in lines)
                {
                    _unitOfWork.PurchaseRequestLineRepository.Add(l);
                }
                _unitOfWork.PurchaseRequestRepository.Update(pr);
                await _unitOfWork.PurchaseRequestRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                return Result<PurchaseRequestDetailModel>.Success((await DetailAsync(pr.Id))!, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " UpdateLines: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        /// <summary>Gợi ý NCC (≤ 3 thẻ + NCC gợi ý + lý do) + tồn hiện tại cho từng dòng đang nhập.</summary>
        public async Task<IResults<SupplierPickLineModel>> GetSupplierSuggestion(SupplierSuggestRequest request)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                var input = (request.ListLine ?? []).Where(l => l.IngredientId != Guid.Empty).ToList();
                var ids = input.Select(l => l.IngredientId).Distinct().ToList();
                var ings = await db.Ingredient.AsNoTracking().Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id);
                var needs = input.Select((l, i) => new SupplierPick.Need(l.Key ?? Guid.NewGuid(), l.IngredientId, l.Quantity > 0 ? l.Quantity : 1m,
                    l.UnitId ?? ings.GetValueOrDefault(l.IngredientId)?.BaseUnitId ?? ings.GetValueOrDefault(l.IngredientId)?.BaseUomId ?? Guid.Empty, (l.RequiredDate ?? request.RequiredDate ?? _clock.Today.AddDays(2)).Date)).ToList();
                var picks = await SupplierPick.SuggestAsync(db, _uomConversionService, _rules, _clock.Today, needs);
                var stock = await db.InventoryBalance.AsNoTracking().Where(b => ids.Contains(b.IngredientId)).GroupBy(b => b.IngredientId)
                    .Select(g => new { g.Key, Qty = g.Sum(x => x.AvailableQuantity) }).ToDictionaryAsync(x => x.Key, x => x.Qty);
                var list = needs.Select(n => { var p = picks[n.Key]; p.StockQuantity = stock.GetValueOrDefault(n.IngredientId); p.BelowMinimum = ings.GetValueOrDefault(n.IngredientId)?.MinimumStock is { } min && p.StockQuantity < min; return p; }).ToList();
                return new Results<SupplierPickLineModel>(list, list.Count) { ResponseCode = 200, ResponseMessage = ConstantHelper.Message.GetAllSuccessfully };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetSupplierSuggestion: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>UI-6.18 B: duyệt cấp cuối xong → tự lập đơn mua (sau khi việc duyệt đã ghi). Lỗi chỉ trả câu báo, không làm hỏng việc duyệt.</summary>
        public async Task<string?> AutoCreatePoAsync(Guid purchaseRequestId)
        {
            if (!_rules.PurchaseAutoCreatePoOnApprove)
            {
                return null;
            }
            try
            {
                var res = await ConvertToPurchaseOrder(new PurchaseRequestConvertRequest { Id = purchaseRequestId, Note = "Hệ thống tự lập sau khi đề nghị được duyệt" });
                if (res.ResponseCode != 200 || res.Data == null)
                {
                    return string.Format(ConstantHelper.Message.PurchaseAutoPoFailed, res.ResponseMessage);
                }
                var d = res.Data;
                string pos = string.Join(", ", d.ListPurchaseOrder.Select(p => $"{p.PurchaseOrderNumber} ({p.SupplierName}, {PurchasingRules.Money(p.TotalAmount)}{(p.Status == PurchaseOrderStatus.Approved ? ", tự duyệt" : string.Empty)})"));
                string msg = string.Format(ConstantHelper.Message.PurchaseAutoPoCreated, d.RequestNumber, pos) + (d.ListNeedPick.Count > 0 ? string.Format(ConstantHelper.Message.PurchaseAutoPoNeedPick, d.ListNeedPick.Count) : string.Empty);
                await NotifyAutoPoAsync(purchaseRequestId, msg);
                return msg;
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " AutoCreatePoAsync: ", ex.ToString()));
                return string.Format(ConstantHelper.Message.PurchaseAutoPoFailed, "lỗi hệ thống");
            }
        }

        public async Task<IReadOnlyList<string>> ApprovalStepScreenIdsAsync(Guid id)
        {
            var db = _unitOfWork.DbContext;
            var pr = await db.PurchaseRequest.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            if (!PurchasePlan.PurchasePlanRules.IsSystem(pr))
            {
                return [PurchaseRequestStatus.Level1ScreenId, PurchaseRequestStatus.Level2ScreenId];
            }
            var (total, _) = await PurchasePlan.PurchasePlanRules.EstimateAsync(db, _uomConversionService, _rules, _plan, _clock.Today, id);
            return total >= _rules.PurchaseOrderDirectorApprovalAmount
                ? [PurchaseRequestStatus.Level1ScreenId, PurchaseRequestStatus.Level2ScreenId]
                : [PurchaseRequestStatus.Level1ScreenId];
        }

        private async Task NotifyAutoPoAsync(Guid prId, string message)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                var requester = await db.PurchaseRequest.AsNoTracking().Where(p => p.Id == prId).Select(p => p.RequesterId ?? p.CreatedBy).FirstOrDefaultAsync();
                var buyers = await Messaging.RecipientResolver.UsersInGroupsAsync(db, ["OPS-SCM", "OPS-SCM-HEAD"]);
                var to = buyers.Concat(requester is { } r ? [r] : []).Distinct().ToList();
                if (to.Count == 0)
                {
                    return;
                }
                await Messaging.BusinessNotifier.SafeEnqueueAsync(_notifications, new NotificationRequest
                {
                    Template = Messaging.NotificationCatalog.ManualText, UserIds = [.. to], BotCode = MessagingCode.BotVanHanh, Data = new { text = "🛒 " + message }, IdempotencyKey = $"PR_AUTO_PO:{prId:N}",
                });
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[PurchaseRequestService] NotifyAutoPo {Id}", prId);
            }
        }
    }
}
