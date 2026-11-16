using AutoMapper;
using Dapper;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Net;

namespace AppDelicare.Services.Implement
{
    public class PurchaseRequestService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService, IDocumentNumberService documentNumberService, IUomConversionService uomConversionService
        , IClock clock) : IPurchaseRequestService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;
        private readonly IDocumentNumberService _documentNumberService = documentNumberService;
        private readonly IUomConversionService _uomConversionService = uomConversionService;
        private readonly IClock _clock = clock;

        public async Task<IResults<PurchaseRequestViewModel>> GetAll(PurchaseRequestSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@RequestNumber", request.RequestNumber);
                dynamicParameters.Add("@RequestedByEmployeeId", request.RequestedByEmployeeId);
                dynamicParameters.Add("@RequestDate", request.RequestDate);
                dynamicParameters.Add("@RequiredDate", request.RequiredDate);
                dynamicParameters.Add("@Status", request.Status);
                //dynamicParameters.Add("@Note", request.Note);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<PurchaseRequestViewModel>("uspPurchaseRequest_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<PurchaseRequestViewModel>
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

        public IResults<PurchaseRequest> GetAllData()
        {
            try
            {
                var list = _unitOfWork.PurchaseRequestRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<PurchaseRequest>
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

        public async Task<IResult<bool>> Save(PurchaseRequestRequest request, string typeSave)
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
                    ResponseMessage = typeSave == ConstantHelper.TypeSave.Delete ? ConstantHelper.Message.DeleteSuccess : ConstantHelper.Message.SaveSuccess,
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

        /// <summary>T15: đề nghị mua tay – luôn Nháp, số PH- (T11) khi bỏ trống, ghi song song RequestNo. Dòng thêm qua PurchaseRequestLine (khi Nháp).</summary>
        private async Task<string> Insert(PurchaseRequestRequest request, string typeSave)
        {
            var insert = _mapper.Map<PurchaseRequest>(request);
            string number = string.IsNullOrWhiteSpace(request.RequestNumber)
                ? await _documentNumberService.NextAsync(DocumentKind.Voucher, _clock.Today)
                : request.RequestNumber.Trim();
            insert.RequestNumber = insert.RequestNo = number;
            insert.Status = PurchaseRequestStatus.Draft;
            if (insert.RequestDate == default)
            {
                insert.RequestDate = _clock.Now;
            }
            _unitOfWork.PurchaseRequestRepository.Add(insert);
            await _unitOfWork.PurchaseRequestRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        /// <summary>T15: chỉ sửa đề nghị Nháp (ngày cần, ghi chú, người đề nghị); số / trạng thái chỉ đổi qua thao tác nghiệp vụ.</summary>
        private async Task<string> Update(PurchaseRequestRequest request, string typeSave)
        {
            var exist = _unitOfWork.PurchaseRequestRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (request.Id == null || exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            if (exist.Status != PurchaseRequestStatus.Draft)
            {
                return InvalidStatus(exist, "sửa đề nghị mua", PurchaseRequestStatus.Draft);
            }
            if (!string.IsNullOrWhiteSpace(request.Status) && request.Status != exist.Status)
            {
                return ConstantHelper.Message.PurchaseRequestStatusLocked;
            }
            exist.RequiredDate = request.RequiredDate;
            exist.Note = request.Note;
            exist.RequestedByEmployeeId = request.RequestedByEmployeeId;
            exist.RequesterId = request.RequestedByEmployeeId;
            _unitOfWork.PurchaseRequestRepository.Update(exist);
            await _unitOfWork.PurchaseRequestRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        /// <summary>T15: chỉ xóa (mềm) đề nghị Nháp, kèm dòng.</summary>
        private async Task<string> Delete(Guid? id, string typeSave)
        {
            if (id == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            var delete = _unitOfWork.PurchaseRequestRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            if (delete.Status != PurchaseRequestStatus.Draft)
            {
                return InvalidStatus(delete, "xóa đề nghị mua", PurchaseRequestStatus.Draft);
            }
            // T07: ReferenceGuard (quan hệ logic, truy vấn có tham số); dòng của chính đề nghị xóa cùng.
            if (await _auditChangeService.IsReferencedAsync(nameof(PurchaseRequest), (Guid)id, nameof(PurchaseRequestLine)))
            {
                return ConstantHelper.Message.DeleteExistFail;
            }
            foreach (var line in _unitOfWork.PurchaseRequestLineRepository.GetAll().Where(l => l.PurchaseRequestId == delete.Id && !l.IsDeleted).ToList())
            {
                _unitOfWork.PurchaseRequestLineRepository.Update(line);
            }
            _unitOfWork.PurchaseRequestRepository.Update(delete);
            await _unitOfWork.PurchaseRequestRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        public async Task<IResult<PurchaseRequest>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.PurchaseRequestRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<PurchaseRequest>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<PurchaseRequest>
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

        // ============================================================ T15 – đề nghị mua → PO (spec 7.2, Pilot P6)
        private static string InvalidStatus(PurchaseRequest pr, string action, params string[] expected)
            => string.Format(ConstantHelper.Message.StatusActionInvalid, action, "đề nghị mua", string.Join(" / ", expected.Select(PurchaseRequestStatus.Label)), PurchaseRequestStatus.Label(pr.Status));

        private static decimal Ceil4(decimal value) => Math.Ceiling(value * 10000m) / 10000m;

        /// <summary>Chi tiết đề nghị mua: dòng + NCC gợi ý (bảng giá đang hiệu lực tại ngày cần hàng) + tiền ước tính + PO đã lập.</summary>
        public async Task<IResult<PurchaseRequestDetailModel>> GetDetail(Guid id)
        {
            try
            {
                var detail = await DetailAsync(id);
                return detail == null
                    ? Result<PurchaseRequestDetailModel>.Fail(ConstantHelper.Message.RecordNotFoundMessage)
                    : Result<PurchaseRequestDetailModel>.Success(detail, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetDetail: ", ex.ToString()));
                throw;
            }
        }

        private async Task<PurchaseRequestDetailModel?> DetailAsync(Guid id)
        {
            var db = _unitOfWork.DbContext;
            var pr = await db.PurchaseRequest.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            if (pr == null)
            {
                return null;
            }
            var lines = await db.PurchaseRequestLine.AsNoTracking().Where(l => l.PurchaseRequestId == id && !l.IsDeleted).OrderBy(l => l.CreatedDate).ToListAsync();
            var ingredientIds = lines.Select(l => l.IngredientId).Distinct().ToList();
            var ingredients = await db.Ingredient.AsNoTracking().Where(i => ingredientIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id);
            var offers = await PurchasingRules.SupplierOffersAsync(db, ingredientIds, pr.RequiredDate ?? _clock.Today);
            var unitIds = lines.Select(l => l.UnitId).Concat(offers.Where(o => o.Material.PurchaseUomId != null).Select(o => o.Material.PurchaseUomId!.Value)).Distinct().ToList();
            var units = await db.UnitMeasure.AsNoTracking().Where(u => unitIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name);
            var detail = new PurchaseRequestDetailModel { PurchaseRequest = pr, StatusName = PurchaseRequestStatus.Label(pr.Status) };
            foreach (var l in lines)
            {
                ingredients.TryGetValue(l.IngredientId, out var ingredient);
                var best = offers.FirstOrDefault(o => o.Material.IngredientId == l.IngredientId);
                var row = new PurchaseRequestLineDetailModel
                {
                    Id = l.Id, IngredientId = l.IngredientId, IngredientCode = ingredient?.Code, IngredientName = ingredient?.Name, Quantity = l.Quantity,
                    UnitId = l.UnitId, UnitName = units.GetValueOrDefault(l.UnitId), RequiredDate = l.RequiredDate, Note = l.Note,
                };
                if (best.Material != null)
                {
                    Guid purchaseUnit = best.Material.PurchaseUomId is { } pu && pu != Guid.Empty ? pu : l.UnitId;
                    row.SupplierId = best.Supplier.Id;
                    row.SupplierName = best.Supplier.Name;
                    row.UnitPrice = best.Material.UnitPrice;
                    row.PurchaseUnitName = units.GetValueOrDefault(purchaseUnit);
                    decimal? inPurchaseUnit = await _uomConversionService.ConvertAsync(l.Quantity, l.UnitId, purchaseUnit, pr.RequiredDate ?? _clock.Today);
                    row.EstimatedAmount = inPurchaseUnit != null && best.Material.UnitPrice != null ? Math.Round(inPurchaseUnit.Value * best.Material.UnitPrice.Value, 2) : null;
                }
                detail.ListLine.Add(row);
            }
            detail.EstimatedAmount = detail.ListLine.Sum(l => l.EstimatedAmount ?? 0m);
            detail.ListPurchaseOrder = await (from po in db.PurchaseOrder.AsNoTracking()
                                              join s in db.Supplier.AsNoTracking() on po.SupplierId equals s.Id into ss
                                              from s in ss.DefaultIfEmpty()
                                              where po.PurchaseRequestId == id && !po.IsDeleted
                                              orderby po.PurchaseOrderNumber
                                              select new PurchaseOrderBriefModel
                                              {
                                                  Id = po.Id, PurchaseOrderNumber = po.PurchaseOrderNumber ?? po.PONo, SupplierId = po.SupplierId, SupplierName = s != null ? s.Name : null,
                                                  Status = po.Status, TotalAmount = po.TotalAmount ?? 0m,
                                                  LineCount = db.PurchaseOrderLine.Count(x => x.PurchaseOrderId == po.Id && !x.IsDeleted),
                                              }).ToListAsync();
            detail.ListPurchaseOrder.ForEach(p => p.StatusName = PurchaseOrderStatus.Label(p.Status));
            if (pr.Status is PurchaseRequestStatus.Draft or PurchaseRequestStatus.Approved)
            {
                detail.AllowedActions.AddRange(["convertToPurchaseOrder", "cancel"]);
            }
            return detail;
        }

        /// <summary>
        /// T15 (spec 7.2 convert-to-po, PROPOSED – GAP-PUR-06), 1 transaction: đề nghị Nháp / Đã duyệt (Pilot không bắt buộc duyệt đề nghị – PO được duyệt 1 cấp).
        /// Mỗi nguyên liệu chọn NCC theo bảng giá đang hiệu lực tại ngày cần hàng (ưu tiên → giá → lead time); nguyên liệu chưa có NCC → lỗi kèm danh sách.
        /// Mỗi NCC 1 PO Nháp số <c>PO-YYMMDD-NNNN</c>; dòng cùng nguyên liệu gộp lại; số lượng quy đổi sang đơn vị mua của bảng giá (thiếu quy đổi → giữ đơn vị
        /// của đề nghị, cảnh báo); đơn giá = SupplierMaterial.UnitPrice; dưới MOQ → cảnh báo. Đề nghị → Converted.
        /// </summary>
        public async Task<IResult<PurchaseRequestConvertResultModel>> ConvertToPurchaseOrder(PurchaseRequestConvertRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var db = _unitOfWork.DbContext;
                var pr = _unitOfWork.PurchaseRequestRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
                List<PurchaseRequestLine> lines = pr == null ? [] : await db.PurchaseRequestLine.AsNoTracking()
                    .Where(l => l.PurchaseRequestId == pr.Id && !l.IsDeleted && l.Quantity > 0).OrderBy(l => l.CreatedDate).ToListAsync();
                string? error = pr == null ? ConstantHelper.Message.RecordNotFoundMessage
                    : pr.Status is not (PurchaseRequestStatus.Draft or PurchaseRequestStatus.Approved)
                        ? InvalidStatus(pr, "lập PO từ đề nghị mua", PurchaseRequestStatus.Draft, PurchaseRequestStatus.Approved)
                    : lines.Count == 0 ? ConstantHelper.Message.PurchaseRequestNeedsLine
                    : null;
                var ingredientIds = lines.Select(l => l.IngredientId).Distinct().ToList();
                var ingredients = await db.Ingredient.AsNoTracking().Where(i => ingredientIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id);
                DateTime date = (pr?.RequiredDate ?? _clock.Today).Date;
                List<(SupplierMaterial Material, Supplier Supplier)> offers = error != null ? [] : await PurchasingRules.SupplierOffersAsync(db, ingredientIds, date);
                if (error == null)
                {
                    var missing = ingredientIds.Where(i => offers.All(o => o.Material.IngredientId != i)).Select(i => ingredients.GetValueOrDefault(i)?.Name ?? i.ToString()).ToList();
                    if (missing.Count != 0)
                    {
                        error = string.Format(ConstantHelper.Message.PurchaseRequestNoSupplier, string.Join(", ", missing));
                    }
                }
                if (error != null)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<PurchaseRequestConvertResultModel>.Fail(error);
                }
                string prNumber = pr!.RequestNumber ?? pr.RequestNo ?? string.Empty;
                var result = new PurchaseRequestConvertResultModel { PurchaseRequestId = pr.Id, RequestNumber = prNumber };
                DateTime orderDate = request.OrderDate?.Date ?? _clock.Today;
                var bestByIngredient = ingredientIds.ToDictionary(i => i, i => offers.First(o => o.Material.IngredientId == i));
                foreach (var bySupplier in ingredientIds.GroupBy(i => bestByIngredient[i].Supplier.Id))
                {
                    var supplier = bestByIngredient[bySupplier.First()].Supplier;
                    string number = await _documentNumberService.NextAsync(DocumentKind.PurchaseOrder, orderDate);
                    var po = new PurchaseOrder
                    {
                        Id = Guid.NewGuid(), PurchaseOrderNumber = number, PONo = number, SupplierId = supplier.Id, PurchaseRequestId = pr.Id,
                        OrderDate = orderDate, ExpectedDeliveryDate = pr.RequiredDate, ExpectedDate = pr.RequiredDate, Status = PurchaseOrderStatus.Draft,
                        Note = PurchasingRules.AppendNote($"Lập từ đề nghị mua {prNumber}", request.Note),
                    };
                    decimal total = 0m;
                    int lineCount = 0;
                    foreach (Guid ingredientId in bySupplier)
                    {
                        var offer = bestByIngredient[ingredientId].Material;
                        string name = ingredients.GetValueOrDefault(ingredientId)?.Name ?? ingredientId.ToString();
                        var mine = lines.Where(l => l.IngredientId == ingredientId).ToList();
                        Guid purchaseUnit = offer.PurchaseUomId is { } pu && pu != Guid.Empty ? pu : mine[0].UnitId;
                        Guid unit = purchaseUnit;
                        decimal quantity = 0m;
                        foreach (var l in mine)
                        {
                            decimal? converted = await _uomConversionService.ConvertAsync(l.Quantity, l.UnitId, purchaseUnit, date);
                            if (converted == null)
                            {
                                unit = Guid.Empty;
                                break;
                            }
                            quantity += converted.Value;
                        }
                        if (unit == Guid.Empty)
                        {
                            // Thiếu quy đổi sang đơn vị mua: giữ đơn vị của đề nghị (các dòng phải cùng đơn vị).
                            if (mine.Select(l => l.UnitId).Distinct().Count() > 1)
                            {
                                _unitOfWork.RollbackTransaction();
                                return Result<PurchaseRequestConvertResultModel>.Fail($"{name}: các dòng khác đơn vị và thiếu quy đổi sang đơn vị mua của NCC – sửa đề nghị mua hoặc thêm quy đổi đơn vị");
                            }
                            unit = mine[0].UnitId;
                            quantity = mine.Sum(l => l.Quantity);
                            result.ListWarning.Add($"{name}: thiếu quy đổi sang đơn vị mua của NCC – PO giữ đơn vị của đề nghị, kiểm tra lại đơn giá");
                        }
                        quantity = Ceil4(quantity);
                        decimal price = offer.UnitPrice ?? 0m;
                        if (offer.UnitPrice == null)
                        {
                            result.ListWarning.Add($"{name}: bảng giá NCC {supplier.Name} chưa có đơn giá – PO ghi 0, sửa bằng PurchaseOrder/UpdateLines");
                        }
                        decimal? moq = offer.MinimumOrderQuantity ?? offer.MinimumOrderQty;
                        if (moq is > 0 && unit == purchaseUnit && quantity < moq.Value)
                        {
                            result.ListWarning.Add($"{name}: số đặt {PurchasingRules.Qty(quantity)} dưới MOQ {PurchasingRules.Qty(moq.Value)} của NCC {supplier.Name}");
                        }
                        decimal amount = Math.Round(quantity * price, 2, MidpointRounding.AwayFromZero);
                        _unitOfWork.PurchaseOrderLineRepository.Add(new PurchaseOrderLine
                        {
                            Id = Guid.NewGuid(), PurchaseOrderId = po.Id, IngredientId = ingredientId, Quantity = quantity, UnitId = unit, UomId = unit,
                            UnitPrice = price, LineAmount = amount, Amount = amount,
                            Note = string.Join("; ", mine.Select(l => l.Note).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct()) is { Length: > 0 } n ? n : null,
                        });
                        total += amount;
                        lineCount++;
                    }
                    po.TotalAmount = total;
                    _unitOfWork.PurchaseOrderRepository.Add(po);
                    result.ListPurchaseOrder.Add(new PurchaseOrderBriefModel
                    {
                        Id = po.Id, PurchaseOrderNumber = number, SupplierId = supplier.Id, SupplierName = supplier.Name, Status = po.Status,
                        StatusName = PurchaseOrderStatus.Label(po.Status), TotalAmount = total, LineCount = lineCount,
                    });
                }
                pr.Status = PurchaseRequestStatus.Converted;
                _unitOfWork.PurchaseRequestRepository.Update(pr);
                await _unitOfWork.PurchaseRequestRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                return Result<PurchaseRequestConvertResultModel>.Success(result, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ConvertToPurchaseOrder: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        /// <summary>T15: hủy đề nghị Nháp / Đã duyệt (lý do bắt buộc, ghi "Hủy: …" vào Note). Tính lại MRP sẽ lập đề nghị mới (GAP-MRP-06).</summary>
        public async Task<IResult<PurchaseRequestDetailModel>> Cancel(PurchaseCancelRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                string? reason = request.Reason?.Trim();
                var pr = _unitOfWork.PurchaseRequestRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
                string? error = string.IsNullOrEmpty(reason) || reason.Length < 3 ? ConstantHelper.Message.CancelReasonRequired
                    : pr == null ? ConstantHelper.Message.RecordNotFoundMessage
                    : pr.Status is not (PurchaseRequestStatus.Draft or PurchaseRequestStatus.Approved)
                        ? InvalidStatus(pr, "hủy đề nghị mua", PurchaseRequestStatus.Draft, PurchaseRequestStatus.Approved)
                    : null;
                if (error != null)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<PurchaseRequestDetailModel>.Fail(error);
                }
                pr!.Status = PurchaseRequestStatus.Cancelled;
                pr.Note = PurchasingRules.AppendNote(pr.Note, "Hủy: " + reason);
                _unitOfWork.PurchaseRequestRepository.Update(pr);
                await _unitOfWork.PurchaseRequestRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                return Result<PurchaseRequestDetailModel>.Success(await DetailAsync(pr.Id), ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Cancel: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }
    }
}
