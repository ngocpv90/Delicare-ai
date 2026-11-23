// Sinh bởi tools/AppDelicare.CodeGen (T05), nay là file của dev (T15: máy trạng thái PO, duyệt 1 cấp, in Đơn đặt hàng NCC) – CodeGen không ghi đè.
using AutoMapper;
using ClosedXML.Excel;
using Dapper;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Model.Request;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Serilog;
using System.Globalization;
using System.Net;

namespace AppDelicare.Services.Implement
{
    public class PurchaseOrderService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService, IDocumentNumberService documentNumberService, IApprovalRequestService approvalRequestService
        , IClock clock, IOptions<BusinessRulesOptions> rules) : IPurchaseOrderService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;
        private readonly IDocumentNumberService _documentNumberService = documentNumberService;
        private readonly IApprovalRequestService _approvalRequestService = approvalRequestService;
        private readonly IClock _clock = clock;
        private readonly BusinessRulesOptions _rules = rules.Value;

        public async Task<IResults<PurchaseOrderViewModel>> GetAll(PurchaseOrderSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                // T15: lọc NCC / đề nghị mua / trạng thái (trống = không lọc).
                dynamicParameters.Add("@SupplierId", request.SupplierId == Guid.Empty ? null : request.SupplierId);
                dynamicParameters.Add("@PurchaseRequestId", request.PurchaseRequestId == Guid.Empty ? null : request.PurchaseRequestId);
                dynamicParameters.Add("@Status", request.Status);
                dynamicParameters.Add("@FromOrderDate", request.FromOrderDate?.Date);
                dynamicParameters.Add("@ToOrderDate", request.ToOrderDate?.Date);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<PurchaseOrderViewModel>("uspPurchaseOrder_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<PurchaseOrderViewModel>
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

        public IResults<PurchaseOrder> GetAllData()
        {
            try
            {
                var list = _unitOfWork.PurchaseOrderRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<PurchaseOrder>
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

        public async Task<IResult<bool>> Save(PurchaseOrderRequest request, string typeSave)
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

        private async Task<string?> SupplierErrorAsync(Guid supplierId)
        {
            var supplier = await _unitOfWork.DbContext.Supplier.AsNoTracking().FirstOrDefaultAsync(s => s.Id == supplierId && !s.IsDeleted);
            return supplier == null ? ConstantHelper.Message.SupplierNotFound : PurchasingRules.IsInactive(supplier.Status) ? ConstantHelper.Message.SupplierInactive : null;
        }

        /// <summary>
        /// T15: PO tay (không qua đề nghị mua) – luôn Nháp, số <c>PO-YYMMDD-NNNN</c> (T11, ghi song song PONo), tổng tiền 0; dòng thêm qua
        /// <c>PurchaseOrder/UpdateLines</c>. NCC phải tồn tại và không ngừng giao dịch.
        /// </summary>
        private async Task<string> Insert(PurchaseOrderRequest request, string typeSave)
        {
            if (await SupplierErrorAsync(request.SupplierId) is { } error)
            {
                return error;
            }
            var insert = _mapper.Map<PurchaseOrder>(request);
            DateTime orderDate = request.OrderDate == default ? _clock.Today : request.OrderDate.Date;
            string number = await _documentNumberService.NextAsync(DocumentKind.PurchaseOrder, orderDate);
            insert.PurchaseOrderNumber = insert.PONo = number;
            insert.OrderDate = orderDate;
            insert.ExpectedDate = insert.ExpectedDeliveryDate;
            insert.Status = PurchaseOrderStatus.Draft;
            insert.TotalAmount = 0m;
            insert.PurchaseRequestId = null;
            _unitOfWork.PurchaseOrderRepository.Add(insert);
            await _unitOfWork.PurchaseOrderRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        /// <summary>
        /// T15: CRUD Update chỉ sửa PO Nháp: ngày đặt, ngày giao dự kiến, ghi chú; đổi NCC chỉ khi chưa có dòng. Số, trạng thái, tổng tiền chỉ đổi qua
        /// thao tác nghiệp vụ.
        /// </summary>
        private async Task<string> Update(PurchaseOrderRequest request, string typeSave)
        {
            var exist = _unitOfWork.PurchaseOrderRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (request.Id == null || exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            if (exist.Status != PurchaseOrderStatus.Draft)
            {
                return InvalidStatus(exist, "sửa PO", PurchaseOrderStatus.Draft);
            }
            if (!string.IsNullOrWhiteSpace(request.Status) && request.Status != exist.Status)
            {
                return ConstantHelper.Message.PurchaseOrderStatusLocked;
            }
            if (request.SupplierId != Guid.Empty && request.SupplierId != exist.SupplierId)
            {
                if (await _unitOfWork.DbContext.PurchaseOrderLine.AsNoTracking().AnyAsync(l => l.PurchaseOrderId == exist.Id && !l.IsDeleted))
                {
                    return "Đã có dòng nguyên liệu – không đổi NCC của PO (lập PO mới)";
                }
                if (await SupplierErrorAsync(request.SupplierId) is { } error)
                {
                    return error;
                }
                exist.SupplierId = request.SupplierId;
            }
            if (request.OrderDate != default)
            {
                exist.OrderDate = request.OrderDate.Date;
            }
            exist.ExpectedDeliveryDate = exist.ExpectedDate = request.ExpectedDeliveryDate;
            exist.Note = request.Note;
            _unitOfWork.PurchaseOrderRepository.Update(exist);
            await _unitOfWork.PurchaseOrderRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        /// <summary>T15: chỉ xóa (mềm) PO Nháp lập tay, kèm dòng. PO lập từ đề nghị mua → Cancel (giữ dấu vết).</summary>
        private async Task<string> Delete(Guid? id, string typeSave)
        {
            if (id == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            var delete = _unitOfWork.PurchaseOrderRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            if (delete.Status != PurchaseOrderStatus.Draft)
            {
                return InvalidStatus(delete, "xóa PO", PurchaseOrderStatus.Draft);
            }
            if (delete.PurchaseRequestId != null)
            {
                return ConstantHelper.Message.PurchaseOrderFromRequestNoDelete;
            }
            // T07: ReferenceGuard (quan hệ logic, truy vấn có tham số); dòng của chính PO xóa cùng.
            if (await _auditChangeService.IsReferencedAsync(nameof(PurchaseOrder), (Guid)id, nameof(PurchaseOrderLine)))
            {
                return ConstantHelper.Message.DeleteExistFail;
            }
            foreach (var line in _unitOfWork.PurchaseOrderLineRepository.GetAll().Where(l => l.PurchaseOrderId == delete.Id && !l.IsDeleted).ToList())
            {
                _unitOfWork.PurchaseOrderLineRepository.Update(line);
            }
            _unitOfWork.PurchaseOrderRepository.Update(delete);
            await _unitOfWork.PurchaseOrderRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        public async Task<IResult<PurchaseOrder>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.PurchaseOrderRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<PurchaseOrder>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<PurchaseOrder>
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

        // ============================================================ T15 – đơn mua NCC (spec 7.3, BUSINESS_RULES §4, Pilot P6)
        private static string InvalidStatus(PurchaseOrder po, string action, params string[] expected)
            => string.Format(ConstantHelper.Message.StatusActionInvalid, action, "PO", string.Join(" / ", expected.Select(PurchaseOrderStatus.Label)), PurchaseOrderStatus.Label(po.Status));

        private static string Number(PurchaseOrder po) => po.PurchaseOrderNumber ?? po.PONo ?? po.Id.ToString("N")[..8];

        private Task<ApprovalRequest?> PendingApprovalAsync(Guid id)
            => _unitOfWork.ApprovalRequestRepository.GetAll().AsNoTracking()
                .Where(a => !a.IsDeleted && a.BusinessType == PurchaseOrderStatus.BusinessType && a.BusinessId == id && ApprovalRequestService.OpenStatuses.Contains(a.RequestStatus))
                .OrderByDescending(a => a.RequestedAt).FirstOrDefaultAsync();

        /// <summary>
        /// T15: sửa toàn bộ dòng PO Nháp (JSON). Mỗi nguyên liệu 1 dòng (GAP-PUR-01: phiếu nhập khớp dòng PO theo nguyên liệu). Đơn vị trống = đơn vị mua
        /// của bảng giá NCC của PO (không có thì đơn vị cơ sở); đơn giá trống = giá bảng giá NCC đang hiệu lực (không có thì 0). Dòng không gửi lên bị xóa mềm.
        /// Tổng tiền tính lại.
        /// </summary>
        public async Task<IResult<PurchaseOrderDetailModel>> UpdateLines(PurchaseOrderUpdateLinesRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var db = _unitOfWork.DbContext;
                var po = _unitOfWork.PurchaseOrderRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
                var inputs = request.ListLine ?? [];
                string? error = po == null ? ConstantHelper.Message.RecordNotFoundMessage
                    : po.Status != PurchaseOrderStatus.Draft ? InvalidStatus(po, "sửa dòng PO", PurchaseOrderStatus.Draft)
                    : inputs.Count == 0 ? ConstantHelper.Message.PurchaseOrderNeedsLine
                    : inputs.Any(l => l.Quantity <= 0 || l.UnitPrice < 0) ? ConstantHelper.Message.PurchaseOrderLineInvalid
                    : null;
                var ingredientIds = inputs.Select(l => l.IngredientId).Distinct().ToList();
                var ingredients = await db.Ingredient.AsNoTracking().Where(i => ingredientIds.Contains(i.Id) && !i.IsDeleted).ToDictionaryAsync(i => i.Id);
                List<PurchaseOrderLine> existing = po == null ? [] : _unitOfWork.PurchaseOrderLineRepository.GetAll().Where(l => l.PurchaseOrderId == po.Id && !l.IsDeleted).ToList();
                if (error == null)
                {
                    var duplicate = inputs.GroupBy(l => l.IngredientId).FirstOrDefault(g => g.Count() > 1);
                    var unknownLine = inputs.FirstOrDefault(l => l.Id != null && existing.All(e => e.Id != l.Id));
                    var unknownIngredient = ingredientIds.FirstOrDefault(i => !ingredients.ContainsKey(i));
                    error = duplicate != null ? string.Format(ConstantHelper.Message.PurchaseOrderDuplicateIngredient, ingredients.GetValueOrDefault(duplicate.Key)?.Name ?? duplicate.Key.ToString())
                        : unknownLine != null ? string.Format(ConstantHelper.Message.PurchaseOrderLineNotInOrder, unknownLine.Id)
                        : unknownIngredient != Guid.Empty ? ConstantHelper.Message.IngredientNotFound
                        : null;
                }
                if (error != null)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<PurchaseOrderDetailModel>.Fail(error);
                }
                var offers = await PurchasingRules.SupplierOffersAsync(db, ingredientIds, po!.OrderDate, po.SupplierId);
                var removed = existing.Where(e => inputs.All(l => l.Id != e.Id)).ToList();
                if (removed.Count != 0)
                {
                    foreach (var line in removed)
                    {
                        _unitOfWork.PurchaseOrderLineRepository.Update(line);
                    }
                    await _unitOfWork.PurchaseOrderLineRepository.CommitAsync(ConstantHelper.TypeSave.Delete);
                }
                decimal total = 0m;
                foreach (var input in inputs)
                {
                    var offer = offers.FirstOrDefault(o => o.Material.IngredientId == input.IngredientId).Material;
                    var ingredient = ingredients[input.IngredientId];
                    Guid unit = input.UnitId is { } u && u != Guid.Empty ? u
                        : offer?.PurchaseUomId is { } pu && pu != Guid.Empty ? pu
                        : ingredient.BaseUnitId ?? ingredient.BaseUomId ?? Guid.Empty;
                    if (unit == Guid.Empty)
                    {
                        _unitOfWork.RollbackTransaction();
                        return Result<PurchaseOrderDetailModel>.Fail($"{ingredient.Name}: chọn đơn vị tính");
                    }
                    decimal price = input.UnitPrice ?? offer?.UnitPrice ?? 0m;
                    decimal amount = Math.Round(input.Quantity * price, 2, MidpointRounding.AwayFromZero);
                    var line = input.Id is { } lineId ? existing.First(e => e.Id == lineId) : null;
                    if (line == null)
                    {
                        line = new PurchaseOrderLine { Id = Guid.NewGuid(), PurchaseOrderId = po.Id };
                        _unitOfWork.PurchaseOrderLineRepository.Add(line);
                    }
                    else
                    {
                        _unitOfWork.PurchaseOrderLineRepository.Update(line);
                    }
                    line.IngredientId = input.IngredientId;
                    line.Quantity = input.Quantity;
                    line.UnitId = line.UomId = unit;
                    line.UnitPrice = price;
                    line.LineAmount = amount;
                    line.Amount = amount;
                    line.Note = input.Note;
                    total += amount;
                }
                po.TotalAmount = total;
                if (request.ExpectedDeliveryDate != null)
                {
                    po.ExpectedDeliveryDate = po.ExpectedDate = request.ExpectedDeliveryDate;
                }
                po.Note = PurchasingRules.AppendNote(po.Note, request.Note);
                _unitOfWork.PurchaseOrderRepository.Update(po);
                await _unitOfWork.PurchaseOrderRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                return Result<PurchaseOrderDetailModel>.Success(await DetailAsync(po.Id), ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " UpdateLines: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        /// <summary>
        /// T15 (spec 7.3, D-11, PROPOSED – GAP-PUR-03): gửi duyệt PO Nháp (≥ 1 dòng, số lượng &gt; 0). Tổng tiền tính lại. Tổng ≤
        /// <c>BusinessRules:PurchaseOrderAutoApproveMaxAmount</c> (&gt; 0) → Đã duyệt ngay; còn lại → Chờ duyệt + ApprovalRequest PURCHASE_ORDER (1 cấp,
        /// quyền duyệt = mục menu <c>#/wf-inbox?type=scm-po</c>).
        /// </summary>
        public async Task<IResult<PurchaseOrderDetailModel>> Submit(PurchaseActionRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var po = _unitOfWork.PurchaseOrderRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
                List<PurchaseOrderLine> lines = po == null ? [] : await _unitOfWork.DbContext.PurchaseOrderLine.AsNoTracking().Where(l => l.PurchaseOrderId == po.Id && !l.IsDeleted).ToListAsync();
                string? error = po == null ? ConstantHelper.Message.RecordNotFoundMessage
                    : po.Status != PurchaseOrderStatus.Draft ? InvalidStatus(po, "gửi duyệt PO", PurchaseOrderStatus.Draft)
                    : lines.Count == 0 || lines.Any(l => l.Quantity <= 0) ? ConstantHelper.Message.PurchaseOrderNeedsLine
                    : await SupplierErrorAsync(po.SupplierId);
                if (error != null)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<PurchaseOrderDetailModel>.Fail(error);
                }
                decimal total = lines.Sum(l => l.LineAmount);
                po!.TotalAmount = total;
                po.Note = PurchasingRules.AppendNote(po.Note, request.Note);
                string message;
                // T15.1: PO ≥ ngưỡng GĐ không bao giờ tự duyệt (kể cả khi cấu hình tự duyệt cao hơn).
                if (_rules.PurchaseOrderAutoApproveMaxAmount > 0 && total <= _rules.PurchaseOrderAutoApproveMaxAmount && !NeedsDirector(total))
                {
                    po.Status = PurchaseOrderStatus.Approved;
                    message = string.Format(ConstantHelper.Message.PurchaseOrderAutoApproved, PurchasingRules.Money(total), PurchasingRules.Money(_rules.PurchaseOrderAutoApproveMaxAmount));
                    po.Note = PurchasingRules.AppendNote(po.Note, message);
                    _unitOfWork.PurchaseOrderRepository.Update(po);
                    await _unitOfWork.PurchaseOrderRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                }
                else
                {
                    // Lưu trạng thái Chờ duyệt TRƯỚC khi tạo yêu cầu: nội dung được duyệt (hash) đọc từ DB.
                    po.Status = PurchaseOrderStatus.PendingApproval;
                    _unitOfWork.PurchaseOrderRepository.Update(po);
                    await _unitOfWork.PurchaseOrderRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                    var (approval, approvalError) = await _approvalRequestService.CreateAsync(PurchaseOrderStatus.BusinessType, po.Id);
                    if (approvalError != null)
                    {
                        _unitOfWork.RollbackTransaction();
                        return Result<PurchaseOrderDetailModel>.Fail(approvalError);
                    }
                    message = string.Format(ConstantHelper.Message.PurchaseOrderSubmitted, approval!.RequestNo);
                    if (NeedsDirector(total))
                    {
                        message += " – " + string.Format(ConstantHelper.Message.PurchaseOrderDirectorRequired, PurchasingRules.Money(total), PurchasingRules.Money(_rules.PurchaseOrderDirectorApprovalAmount));
                    }
                }
                _unitOfWork.CommitTransaction();
                return Result<PurchaseOrderDetailModel>.Success(await DetailAsync(po.Id), message);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Submit: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        /// <summary>T15: duyệt / từ chối PO qua engine T12 (<c>ApprovalRequestService.Decide</c>: kiểm quyền mục "Phê duyệt › Đơn mua hàng", hash nội dung, ghi ApprovalDecision).</summary>
        public async Task<IResult<bool>> Decide(PurchaseActionRequest request, bool approve)
        {
            try
            {
                var pending = await PendingApprovalAsync(request.Id);
                if (pending == null)
                {
                    return Result<bool>.Fail(ConstantHelper.Message.PurchaseOrderNotPending, false);
                }
                return await _approvalRequestService.Decide(new ApprovalDecideRequest { Id = pending.Id, Approve = approve, Note = request.Note });
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Decide: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>Nội dung được duyệt của PURCHASE_ORDER: trạng thái, NCC, tổng tiền, từng dòng (nguyên liệu, số lượng, đơn vị, đơn giá). Sửa sau khi gửi → không duyệt được.</summary>
        public async Task<string?> BuildApprovalStateAsync(Guid id)
        {
            var po = await _unitOfWork.DbContext.PurchaseOrder.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            if (po == null)
            {
                return null;
            }
            var lines = await _unitOfWork.DbContext.PurchaseOrderLine.AsNoTracking().Where(l => l.PurchaseOrderId == id && !l.IsDeleted)
                .OrderBy(l => l.Id).ToListAsync();
            static string D(decimal v) => v.ToString("0.######", CultureInfo.InvariantCulture);
            return JsonConvert.SerializeObject(new
            {
                po.Id, po.Status, po.SupplierId, TotalAmount = D(po.TotalAmount ?? 0m),
                Lines = lines.Select(l => new { l.Id, l.IngredientId, Quantity = D(l.Quantity), l.UnitId, UnitPrice = D(l.UnitPrice) }),
            });
        }

        /// <summary>Tiêu đề trong hộp duyệt: số PO – NCC – tổng tiền (+ ghi chú vượt ngưỡng GĐ duyệt, D-11).</summary>
        public async Task<string?> DescribeAsync(Guid id)
        {
            var row = await (from po in _unitOfWork.DbContext.PurchaseOrder.AsNoTracking()
                             join s in _unitOfWork.DbContext.Supplier.AsNoTracking() on po.SupplierId equals s.Id into ss
                             from s in ss.DefaultIfEmpty()
                             where po.Id == id
                             select new { Number = po.PurchaseOrderNumber ?? po.PONo, Supplier = s != null ? s.Name : null, po.TotalAmount }).FirstOrDefaultAsync();
            if (row == null)
            {
                return null;
            }
            decimal total = row.TotalAmount ?? 0m;
            string text = $"PO {row.Number} – {row.Supplier} – {PurchasingRules.Money(total)} đ";
            return NeedsDirector(total)
                ? $"{text} (≥ {PurchasingRules.Money(_rules.PurchaseOrderDirectorApprovalAmount)} đ: Giám đốc duyệt – D-11)"
                : text;
        }

        /// <summary>T15.1 (D-11, anh chốt 2026-11-16): tổng ≥ <c>BusinessRules:PurchaseOrderDirectorApprovalAmount</c> → Giám đốc duyệt.</summary>
        private bool NeedsDirector(decimal total) => total >= _rules.PurchaseOrderDirectorApprovalAmount;

        /// <summary>
        /// T15.1: mục quyền duyệt của PO (gọi từ <c>PurchaseOrderApprovalHandler.RequiredScreenIdAsync</c>): tổng ≥ ngưỡng GĐ → <c>scm-po-director</c>, còn lại <c>scm-po</c>.
        /// Tổng tiền đọc từ PO hiện tại – nếu bị sửa sau khi gửi thì hash khác, không duyệt được.
        /// </summary>
        public async Task<string> ApprovalScreenIdAsync(Guid id)
        {
            decimal total = await _unitOfWork.DbContext.PurchaseOrder.AsNoTracking().Where(x => x.Id == id).Select(x => x.TotalAmount).FirstOrDefaultAsync() ?? 0m;
            return NeedsDirector(total) ? PurchaseOrderStatus.DirectorScreenId : PurchaseOrderStatus.ScreenId;
        }

        /// <summary>
        /// BE2.3 – PO nhiều cấp theo ngưỡng: dưới ngưỡng GĐ → [scm-po]; ≥ ngưỡng → [scm-po-director] (D-11, 1 cấp) hoặc [scm-po, scm-po-director] khi bật
        /// <c>BusinessRules:PurchaseOrderDirectorTwoLevel</c> (PROPOSED – D-52). Cấu hình <c>ApprovalSteps:PURCHASE_ORDER</c> (nếu có) thay toàn bộ (engine).
        /// </summary>
        public async Task<IReadOnlyList<string>> ApprovalStepScreenIdsAsync(Guid id)
        {
            decimal total = await _unitOfWork.DbContext.PurchaseOrder.AsNoTracking().Where(x => x.Id == id).Select(x => x.TotalAmount).FirstOrDefaultAsync() ?? 0m;
            return !NeedsDirector(total) ? [PurchaseOrderStatus.ScreenId]
                : _rules.PurchaseOrderDirectorTwoLevel ? [PurchaseOrderStatus.ScreenId, PurchaseOrderStatus.DirectorScreenId]
                : [PurchaseOrderStatus.DirectorScreenId];
        }

        /// <summary>
        /// Thực thi quyết định duyệt (gọi từ <c>PurchaseOrderApprovalHandler</c>, TRONG transaction của engine): Chờ duyệt → Đã duyệt; từ chối → về Nháp
        /// để sửa và gửi lại (PROPOSED – GAP-PUR-02), lý do ghi vào Note.
        /// </summary>
        public Task<string?> ApplyDecisionAsync(Guid id, bool approved, string? note)
        {
            var po = _unitOfWork.PurchaseOrderRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (po == null || po.Status != PurchaseOrderStatus.PendingApproval)
            {
                return Task.FromResult<string?>(po == null ? ConstantHelper.Message.RecordNotFoundMessage
                    : InvalidStatus(po, approved ? "duyệt PO" : "từ chối PO", PurchaseOrderStatus.PendingApproval));
            }
            po.Status = approved ? PurchaseOrderStatus.Approved : PurchaseOrderStatus.Draft;
            po.Note = PurchasingRules.AppendNote(po.Note, string.IsNullOrWhiteSpace(note) ? null : (approved ? "Duyệt: " : "Từ chối: ") + note.Trim());
            _unitOfWork.PurchaseOrderRepository.Update(po);
            return Task.FromResult<string?>(null);
        }

        /// <summary>Đổi trạng thái 1 PO trong 1 transaction: trạng thái nguồn, điều kiện riêng; ghi chú nối vào Note.</summary>
        private async Task<IResult<PurchaseOrderDetailModel>> ChangeStatus(Guid id, string[] from, string to, string action, string? note, Func<PurchaseOrder, Task<string?>>? check)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var po = _unitOfWork.PurchaseOrderRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
                string? error = po == null ? ConstantHelper.Message.RecordNotFoundMessage
                    : !from.Contains(po.Status) ? InvalidStatus(po, action, from)
                    : check != null ? await check(po) : null;
                if (!string.IsNullOrEmpty(error))
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<PurchaseOrderDetailModel>.Fail(error);
                }
                po!.Status = to;
                po.Note = PurchasingRules.AppendNote(po.Note, note);
                _unitOfWork.PurchaseOrderRepository.Update(po);
                await _unitOfWork.PurchaseOrderRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                return Result<PurchaseOrderDetailModel>.Success(await DetailAsync(po.Id), ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ChangeStatus: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        private async Task<List<GoodsReceipt>> ReceiptsAsync(Guid id)
            => await _unitOfWork.DbContext.GoodsReceipt.AsNoTracking()
                .Where(r => r.PurchaseOrderId == id && !r.IsDeleted && r.Status != GoodsReceiptStatus.Cancelled).ToListAsync();

        /// <summary>T15 (spec 7.3 send): Đã duyệt → Đã gửi NCC (gửi file in Đơn đặt hàng; gửi qua email / Zalo để sau Pilot).</summary>
        public Task<IResult<PurchaseOrderDetailModel>> Send(PurchaseActionRequest request)
            => ChangeStatus(request.Id, [PurchaseOrderStatus.Approved], PurchaseOrderStatus.Sent, "gửi PO cho NCC", request.Note, null);

        /// <summary>T15 (spec 7.3 cancel): Nháp / Đã duyệt / Đã gửi NCC, chưa có phiếu nhập; lý do bắt buộc. PO đang chờ duyệt → người duyệt từ chối trước.</summary>
        public Task<IResult<PurchaseOrderDetailModel>> Cancel(PurchaseCancelRequest request)
        {
            string? reason = request.Reason?.Trim();
            if (string.IsNullOrEmpty(reason) || reason.Length < 3)
            {
                return Task.FromResult<IResult<PurchaseOrderDetailModel>>(Result<PurchaseOrderDetailModel>.Fail(ConstantHelper.Message.CancelReasonRequired));
            }
            return ChangeStatus(request.Id, [PurchaseOrderStatus.Draft, PurchaseOrderStatus.Approved, PurchaseOrderStatus.Sent], PurchaseOrderStatus.Cancelled, "hủy PO",
                "Hủy: " + reason, async po =>
                {
                    var receipts = await ReceiptsAsync(po.Id);
                    return receipts.Count != 0 ? string.Format(ConstantHelper.Message.PurchaseOrderHasReceipt, string.Join(", ", receipts.Select(r => r.ReceiptNumber ?? r.ReceiptNo))) : null;
                });
        }

        /// <summary>T15 (spec 7.3 close): Nhận một phần / Đã nhận đủ → Đóng (không nhận thêm); không còn phiếu nhập chưa ghi sổ.</summary>
        public Task<IResult<PurchaseOrderDetailModel>> Close(PurchaseActionRequest request)
            => ChangeStatus(request.Id, [PurchaseOrderStatus.PartiallyReceived, PurchaseOrderStatus.Received], PurchaseOrderStatus.Closed, "đóng PO", request.Note, async po =>
            {
                var open = (await ReceiptsAsync(po.Id)).Where(r => r.Status != GoodsReceiptStatus.Posted).ToList();
                return open.Count != 0 ? string.Format(ConstantHelper.Message.PurchaseOrderPendingReceipt, string.Join(", ", open.Select(r => r.ReceiptNumber ?? r.ReceiptNo))) : null;
            });

        /// <summary>
        /// T15: cập nhật trạng thái nhận hàng của PO sau khi ghi sổ phiếu nhập (gọi TRONG transaction của GoodsReceipt/Post, sau khi phiếu đã lưu Posted):
        /// mọi dòng đã nhận (số đạt) ≥ số đặt → Đã nhận đủ; có dòng đã nhận &gt; 0 → Nhận một phần; còn lại giữ nguyên (PROPOSED – GAP-PUR-01).
        /// </summary>
        public async Task RefreshReceiptStatusAsync(Guid id)
        {
            var po = _unitOfWork.PurchaseOrderRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (po == null || !PurchaseOrderStatus.Receivable.Contains(po.Status))
            {
                return;
            }
            var lines = await _unitOfWork.DbContext.PurchaseOrderLine.AsNoTracking().Where(l => l.PurchaseOrderId == id && !l.IsDeleted).ToListAsync();
            var totals = await PurchasingRules.ReceiptTotalsAsync(_unitOfWork.DbContext, id);
            decimal Received(Guid ingredientId) => totals.TryGetValue(ingredientId, out var t) ? t.Received : 0m;
            string status = lines.Count != 0 && lines.All(l => Received(l.IngredientId) >= l.Quantity) ? PurchaseOrderStatus.Received
                : lines.Any(l => Received(l.IngredientId) > 0) ? PurchaseOrderStatus.PartiallyReceived
                : po.Status!;
            if (status != po.Status)
            {
                po.Status = status;
                _unitOfWork.PurchaseOrderRepository.Update(po);
                await _unitOfWork.PurchaseOrderRepository.CommitAsync(ConstantHelper.TypeSave.Update);
            }
        }

        /// <summary>Chi tiết PO (spec 7.3 GET {id}): dòng + đã nhận / đang chờ / còn lại, phiếu nhập, yêu cầu duyệt đang chờ, thao tác được phép.</summary>
        public async Task<IResult<PurchaseOrderDetailModel>> GetDetail(Guid id)
        {
            try
            {
                var detail = await DetailAsync(id);
                return detail == null
                    ? Result<PurchaseOrderDetailModel>.Fail(ConstantHelper.Message.RecordNotFoundMessage)
                    : Result<PurchaseOrderDetailModel>.Success(detail, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetDetail: ", ex.ToString()));
                throw;
            }
        }

        private async Task<PurchaseOrderDetailModel?> DetailAsync(Guid id)
        {
            var db = _unitOfWork.DbContext;
            var po = await db.PurchaseOrder.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            if (po == null)
            {
                return null;
            }
            var lines = await db.PurchaseOrderLine.AsNoTracking().Where(l => l.PurchaseOrderId == id && !l.IsDeleted).OrderBy(l => l.CreatedDate).ToListAsync();
            var ingredientIds = lines.Select(l => l.IngredientId).ToList();
            var unitIds = lines.Select(l => l.UnitId).Distinct().ToList();
            var ingredients = await db.Ingredient.AsNoTracking().Where(i => ingredientIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id);
            var units = await db.UnitMeasure.AsNoTracking().Where(u => unitIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name);
            var supplier = await db.Supplier.AsNoTracking().FirstOrDefaultAsync(s => s.Id == po.SupplierId);
            var totals = await PurchasingRules.ReceiptTotalsAsync(db, id);
            var receipts = await db.GoodsReceipt.AsNoTracking().Where(r => r.PurchaseOrderId == id && !r.IsDeleted).OrderBy(r => r.ReceiptDate).ToListAsync();
            var pending = await PendingApprovalAsync(id);
            var detail = new PurchaseOrderDetailModel
            {
                PurchaseOrder = po, StatusName = PurchaseOrderStatus.Label(po.Status),
                SupplierCode = supplier?.Code, SupplierName = supplier?.Name, SupplierAddress = supplier?.Address, SupplierPhone = supplier?.ContactPhone ?? supplier?.Phone,
                PurchaseRequestNumber = po.PurchaseRequestId is { } prId
                    ? await db.PurchaseRequest.AsNoTracking().Where(p => p.Id == prId).Select(p => p.RequestNumber ?? p.RequestNo).FirstOrDefaultAsync()
                    : null,
                PendingApprovalId = pending?.Id, PendingApprovalNo = pending?.RequestNo,
                PendingApproval = pending == null ? null : await _approvalRequestService.OpenProgressAsync(PurchaseOrderStatus.BusinessType, id),
                NeedsDirectorApproval = NeedsDirector(po.TotalAmount ?? 0m),
                ApprovalScreenId = NeedsDirector(po.TotalAmount ?? 0m) && !_rules.PurchaseOrderDirectorTwoLevel ? PurchaseOrderStatus.DirectorScreenId : PurchaseOrderStatus.ScreenId,
                ListGoodsReceipt = receipts.Select(r => new GoodsReceiptBriefModel
                {
                    Id = r.Id, ReceiptNumber = r.ReceiptNumber ?? r.ReceiptNo, ReceiptDate = r.ReceiptDate, Status = r.Status, StatusName = GoodsReceiptStatus.Label(r.Status),
                }).ToList(),
            };
            // T15.1 (VAT phương án A – GAP-PUR-09): % VAT theo nhóm nguyên liệu / mặc định, chỉ tính khi đọc, không lưu.
            var vatPct = await PurchasingRules.VatPctByIngredientAsync(db, _rules, ingredientIds);
            foreach (var l in lines)
            {
                ingredients.TryGetValue(l.IngredientId, out var ingredient);
                var (received, pendingQty) = totals.TryGetValue(l.IngredientId, out var t) ? t : (0m, 0m);
                decimal pct = vatPct.TryGetValue(l.IngredientId, out var p) ? p : _rules.PurchaseVat.DefaultPct;
                decimal vat = PurchasingRules.VatAmount(l.LineAmount, pct);
                detail.ListLine.Add(new PurchaseOrderLineDetailModel
                {
                    Id = l.Id, IngredientId = l.IngredientId, IngredientCode = ingredient?.Code, IngredientName = ingredient?.Name,
                    Quantity = l.Quantity, UnitId = l.UnitId, UnitName = units.GetValueOrDefault(l.UnitId), UnitPrice = l.UnitPrice, LineAmount = l.LineAmount, Note = l.Note,
                    VatPct = pct, VatAmount = vat, AmountInclVat = l.LineAmount + vat,
                    ReceivedQuantity = received, PendingQuantity = pendingQty, RemainingQuantity = Math.Max(0m, l.Quantity - received - pendingQty),
                });
            }
            detail.SubtotalAmount = detail.ListLine.Sum(l => l.LineAmount);
            detail.VatAmount = detail.ListLine.Sum(l => l.VatAmount);
            detail.TotalAmountInclVat = detail.SubtotalAmount + detail.VatAmount;
            if (detail.NeedsDirectorApproval)
            {
                detail.ListWarning.Add(string.Format(ConstantHelper.Message.PurchaseOrderDirectorRequired, PurchasingRules.Money(po.TotalAmount ?? 0m),
                    PurchasingRules.Money(_rules.PurchaseOrderDirectorApprovalAmount)));
            }
            bool hasReceipt = receipts.Any(r => r.Status != GoodsReceiptStatus.Cancelled);
            switch (po.Status)
            {
                case PurchaseOrderStatus.Draft:
                    detail.AllowedActions.AddRange(["update", "updateLines", "submit", "cancel"]);
                    if (po.PurchaseRequestId == null) detail.AllowedActions.Add("delete");
                    break;
                case PurchaseOrderStatus.PendingApproval:
                    detail.AllowedActions.AddRange(["approve", "reject"]);
                    break;
                case PurchaseOrderStatus.Approved:
                    detail.AllowedActions.AddRange(["send", "receive"]);
                    if (!hasReceipt) detail.AllowedActions.Add("cancel");
                    break;
                case PurchaseOrderStatus.Sent:
                    detail.AllowedActions.Add("receive");
                    if (!hasReceipt) detail.AllowedActions.Add("cancel");
                    break;
                case PurchaseOrderStatus.PartiallyReceived:
                    detail.AllowedActions.AddRange(["receive", "close"]);
                    break;
                case PurchaseOrderStatus.Received:
                    detail.AllowedActions.Add("close");
                    break;
            }
            if (po.Status != PurchaseOrderStatus.Cancelled)
            {
                detail.AllowedActions.Add("export");
            }
            return detail;
        }

        /// <summary>
        /// T15 – in "Đơn đặt hàng NCC" (.xlsx, ClosedXML) theo bố cục sheet "2. Form_PO" của file thật (BUSINESS_DOCS.md – chỉ lấy cấu trúc, không chép dữ liệu):
        /// tên / địa chỉ / điện thoại pháp nhân (bảng Company đầu tiên – PO không có cột công ty), tiêu đề ĐƠN ĐẶT HÀNG, số + ngày, NCC (tên, địa chỉ), nơi giao /
        /// thời gian giao, bảng STT · Mã · Tên · Đơn vị tính chính · Số lượng · Đơn giá (chưa VAT) · Thành tiền (chưa VAT) · % VAT · Tiền VAT · Ghi chú,
        /// chân bảng tổng chưa VAT / tiền VAT / tổng gồm VAT, chữ ký. T15.1 (anh chốt 2026-11-16, VAT phương án A): % VAT theo <c>BusinessRules:PurchaseVat</c>,
        /// tính khi in – không lưu DB (GAP-PUR-09).
        /// </summary>
        public async Task<IResult<byte[]>> ExportOrder(Guid id)
        {
            try
            {
                var detail = await DetailAsync(id);
                if (detail == null)
                {
                    return Result<byte[]>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
                }
                if (detail.ListLine.Count == 0)
                {
                    return Result<byte[]>.Fail(ConstantHelper.Message.PurchaseOrderNeedsLine);
                }
                var company = await _unitOfWork.DbContext.Company.AsNoTracking().Where(c => !c.IsDeleted).OrderBy(c => c.CreatedDate).FirstOrDefaultAsync();
                var po = detail.PurchaseOrder;
                const string brand = "#91AF42";
                const int lastCol = 10;

                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("Don dat hang");
                ws.Cell(1, 1).Value = company?.Name ?? "DELICARE";
                ws.Cell(1, 1).Style.Font.Bold = true;
                ws.Cell(2, 1).Value = "Địa chỉ: " + (company?.Address ?? string.Empty);
                ws.Cell(3, 1).Value = string.Join(" | ", new[] { string.IsNullOrWhiteSpace(company?.Phone) ? null : "Tel: " + company!.Phone, company?.Email }.Where(x => !string.IsNullOrWhiteSpace(x)));
                ws.Range(5, 1, 5, lastCol).Merge().Value = "ĐƠN ĐẶT HÀNG";
                ws.Range(5, 1, 5, lastCol).Style.Font.SetBold().Font.SetFontSize(16).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                ws.Range(6, 1, 6, lastCol).Merge().Value = $"Số: {Number(po)} – Ngày {po.OrderDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}";
                ws.Range(6, 1, 6, lastCol).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center).Font.SetItalic();
                ws.Cell(8, 1).Value = "Tên nhà cung cấp: " + detail.SupplierName;
                ws.Cell(8, 6).Value = "Số điện thoại nhận hàng: " + (company?.Phone ?? string.Empty);
                ws.Cell(9, 1).Value = "Địa chỉ: " + (detail.SupplierAddress ?? string.Empty);
                ws.Cell(10, 1).Value = "Địa điểm giao hàng: Tại kho " + (company?.Name ?? string.Empty);
                ws.Cell(10, 6).Value = "Thời gian giao hàng: " + (po.ExpectedDeliveryDate?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty);

                int header = 12;
                // T15.1 (VAT phương án A): thêm cột % VAT + Tiền VAT mỗi dòng, chân bảng: tổng chưa VAT / tiền VAT / tổng gồm VAT (tính khi in, không lưu).
                string[] titles = ["STT", "Mã", "Tên", "Đơn vị tính chính", "Số lượng", "Đơn giá\n(chưa VAT)", "Thành tiền\n(chưa VAT)", "% VAT", "Tiền VAT", "Ghi chú"];
                for (int c = 0; c < titles.Length; c++)
                {
                    ws.Cell(header, c + 1).Value = titles[c];
                }
                ws.Range(header, 1, header, lastCol).Style.Font.SetBold().Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(XLColor.FromHtml(brand))
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center).Alignment.SetVertical(XLAlignmentVerticalValues.Center).Alignment.SetWrapText(true);
                int row = header + 1;
                int stt = 0;
                foreach (var l in detail.ListLine)
                {
                    ws.Cell(row, 1).Value = ++stt;
                    ws.Cell(row, 2).Value = l.IngredientCode ?? string.Empty;
                    ws.Cell(row, 3).Value = l.IngredientName ?? string.Empty;
                    ws.Cell(row, 4).Value = l.UnitName ?? string.Empty;
                    ws.Cell(row, 5).Value = l.Quantity;
                    ws.Cell(row, 6).Value = l.UnitPrice;
                    ws.Cell(row, 7).Value = l.LineAmount;
                    ws.Cell(row, 8).Value = l.VatPct;
                    ws.Cell(row, 9).Value = l.VatAmount;
                    ws.Cell(row, 10).Value = l.Note ?? string.Empty;
                    row++;
                }
                int firstTotal = row;
                foreach (var (label, value) in new[]
                {
                    ("Tổng cộng (chưa VAT)", detail.SubtotalAmount), ("Tiền thuế VAT", detail.VatAmount), ("Tổng cộng (gồm VAT)", detail.TotalAmountInclVat),
                })
                {
                    ws.Range(row, 1, row, 6).Merge().Value = label;
                    ws.Range(row, 1, row, 6).Style.Font.SetBold().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
                    ws.Cell(row, 7).Value = value;
                    ws.Cell(row, 7).Style.Font.Bold = true;
                    row++;
                }
                row--;
                ws.Range(header, 1, row, lastCol).Style.Border.SetInsideBorder(XLBorderStyleValues.Thin).Border.SetOutsideBorder(XLBorderStyleValues.Thin);
                ws.Range(header + 1, 5, row, 5).Style.NumberFormat.Format = "#,##0.####";
                ws.Range(header + 1, 6, row, 7).Style.NumberFormat.Format = "#,##0";
                ws.Range(header + 1, 8, firstTotal - 1, 8).Style.NumberFormat.Format = "0.##\"%\"";
                ws.Range(header + 1, 9, firstTotal - 1, 9).Style.NumberFormat.Format = "#,##0";

                int sign = row + 2;
                ws.Range(sign, 1, sign, 3).Merge().Value = "Người lập";
                ws.Range(sign, 4, sign, 6).Merge().Value = "Người duyệt";
                ws.Range(sign, 7, sign, lastCol).Merge().Value = "Nhà cung cấp xác nhận";
                ws.Range(sign, 1, sign, lastCol).Style.Font.SetBold().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                ws.Range(sign + 1, 1, sign + 1, 3).Merge().Value = "(Ký, ghi rõ họ tên)";
                ws.Range(sign + 1, 4, sign + 1, 6).Merge().Value = "(Ký, ghi rõ họ tên)";
                ws.Range(sign + 1, 7, sign + 1, lastCol).Merge().Value = "(Ký, ghi rõ họ tên)";
                ws.Range(sign + 1, 1, sign + 1, lastCol).Style.Font.SetItalic().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

                ws.Column(1).Width = 6;
                ws.Column(2).Width = 12;
                ws.Column(3).Width = 32;
                ws.Column(4).Width = 12;
                ws.Column(5).Width = 12;
                ws.Column(6).Width = 14;
                ws.Column(7).Width = 16;
                ws.Column(8).Width = 8;
                ws.Column(9).Width = 14;
                ws.Column(10).Width = 22;
                ws.PageSetup.PageOrientation = XLPageOrientation.Portrait;
                ws.PageSetup.FitToPages(1, 0);

                using var stream = new MemoryStream();
                wb.SaveAs(stream);
                return Result<byte[]>.Success(stream.ToArray(), ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ExportOrder: ", ex.ToString()));
                throw;
            }
        }
    }
}
