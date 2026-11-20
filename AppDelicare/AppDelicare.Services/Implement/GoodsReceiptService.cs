// Sinh bởi tools/AppDelicare.CodeGen (T05), nay là file của dev (T15: nhận hàng theo PO, ghi sổ sau QC) – CodeGen không ghi đè.
using AutoMapper;
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
using Serilog;
using System.Globalization;
using System.Net;

namespace AppDelicare.Services.Implement
{
    public class GoodsReceiptService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService, IDocumentNumberService documentNumberService, IInventoryLedgerService ledger
        , IPurchaseOrderService purchaseOrderService, IClock clock, ICurrentActor currentActor, IOptions<BusinessRulesOptions> rules) : IGoodsReceiptService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;
        private readonly IDocumentNumberService _documentNumberService = documentNumberService;
        private readonly IInventoryLedgerService _ledger = ledger;
        private readonly IPurchaseOrderService _purchaseOrderService = purchaseOrderService;
        private readonly IClock _clock = clock;
        private readonly ICurrentActor _currentActor = currentActor;
        private readonly BusinessRulesOptions _rules = rules.Value;

        public async Task<IResults<GoodsReceiptViewModel>> GetAll(GoodsReceiptSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@PurchaseOrderId", request.PurchaseOrderId == Guid.Empty ? null : request.PurchaseOrderId);
                // T15: SupplierId trống = không lọc (trước đây Guid rỗng lọc mất mọi dòng).
                dynamicParameters.Add("@SupplierId", request.SupplierId == Guid.Empty ? null : request.SupplierId);
                dynamicParameters.Add("@ReceiptNumber", request.ReceiptNumber);
                dynamicParameters.Add("@ReceiptDate", request.ReceiptDate);
                dynamicParameters.Add("@WarehouseId", request.WarehouseId == Guid.Empty ? null : request.WarehouseId);
                dynamicParameters.Add("@Status", request.Status);
                dynamicParameters.Add("@FromReceiptDate", request.FromReceiptDate?.Date);
                dynamicParameters.Add("@ToReceiptDate", request.ToReceiptDate?.Date);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<GoodsReceiptViewModel>("uspGoodsReceipt_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<GoodsReceiptViewModel>
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

        public IResults<GoodsReceipt> GetAllData()
        {
            try
            {
                var list = _unitOfWork.GoodsReceiptRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<GoodsReceipt>
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

        public async Task<IResult<bool>> Save(GoodsReceiptRequest request, string typeSave)
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

        /// <summary>T15: phiếu nhập chỉ lập qua <c>GoodsReceipt/Create</c> (theo PO đã duyệt, sinh số PH-, phiếu QC) – CRUD Insert không ghi.</summary>
        private Task<string> Insert(GoodsReceiptRequest request, string typeSave)
            => Task.FromResult(ConstantHelper.Message.GoodsReceiptInsertBlocked);

        /// <summary>T15: CRUD Update chỉ sửa ghi chú (phiếu chưa hủy); số, PO, NCC, kho, ngày, trạng thái chỉ đổi qua thao tác nghiệp vụ.</summary>
        private async Task<string> Update(GoodsReceiptRequest request, string typeSave)
        {
            var exist = _unitOfWork.GoodsReceiptRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (request.Id == null || exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            if (exist.Status == GoodsReceiptStatus.Cancelled)
            {
                return InvalidStatus(exist, "sửa ghi chú phiếu nhập", GoodsReceiptStatus.QcPending, GoodsReceiptStatus.Accepted, GoodsReceiptStatus.PartiallyAccepted,
                    GoodsReceiptStatus.Rejected, GoodsReceiptStatus.Posted);
            }
            exist.Note = request.Note;
            _unitOfWork.GoodsReceiptRepository.Update(exist);
            await _unitOfWork.GoodsReceiptRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        /// <summary>T15: không xóa phiếu nhập (chứng từ kho / truy xuất) – dùng Cancel khi chưa ghi sổ.</summary>
        private Task<string> Delete(Guid? id, string typeSave)
            => Task.FromResult(ConstantHelper.Message.GoodsReceiptDeleteBlocked);

        public async Task<IResult<GoodsReceipt>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.GoodsReceiptRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<GoodsReceipt>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<GoodsReceipt>
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

        // ============================================================ T15 – nhận hàng + ghi sổ sau QC (spec 7.3 receive, 7.4 post, BUSINESS_RULES §4)
        private static string InvalidStatus(GoodsReceipt receipt, string action, params string[] expected)
            => string.Format(ConstantHelper.Message.StatusActionInvalid, action, "phiếu nhập", string.Join(" / ", expected.Select(GoodsReceiptStatus.Label)), GoodsReceiptStatus.Label(receipt.Status));

        private static string Day(DateTime? date) => date?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty;

        /// <summary>
        /// T15 (spec 7.3 receive, Pilot P6), 1 transaction: PO Đã duyệt / Đã gửi NCC / Nhận một phần (PO chưa duyệt → chặn). Mỗi dòng = 1 dòng PO,
        /// số nhận ≤ số đặt × (1 + <c>BusinessRules:GoodsReceiptOverReceivePct</c>%) − đã nhận − đang chờ (PROPOSED – GAP-PUR-04).
        /// Ghi: GoodsReceipt Chờ QC số <c>PH-YYMMDD-NNNN</c> (T11) + dòng (Quantity = ReceivedQuantity, UnitCost = đơn giá PO, số lô NCC, NSX / HSD –
        /// HSD trống = ngày nhận + ShelfLifeDays) + QCInspection (cùng số) + QCInspectionLine (nhiệt độ nhận). Nguyên liệu <c>QcRequired = false</c> → tự DAT.
        /// </summary>
        public async Task<IResult<GoodsReceiptDetailModel>> Create(GoodsReceiptCreateRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var db = _unitOfWork.DbContext;
                // Rà soát BE: khóa theo PO – 2 phiếu nhập cùng PO tạo cùng lúc không cùng qua kiểm "nhập vượt".
                await DbLocks.DocumentAsync(db, "PurchaseOrderReceipt", request.PurchaseOrderId);
                var po = await db.PurchaseOrder.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.PurchaseOrderId && !x.IsDeleted);
                var inputs = (request.ListLine ?? []).Where(l => l.ReceivedQuantity != 0).ToList();
                string? error = po == null ? ConstantHelper.Message.RecordNotFoundMessage
                    : !PurchaseOrderStatus.Receivable.Contains(po.Status)
                        ? string.Format(ConstantHelper.Message.GoodsReceiptPoNotReceivable, po.PurchaseOrderNumber ?? po.PONo, PurchaseOrderStatus.Label(po.Status))
                    : inputs.Count == 0 || inputs.Any(l => l.ReceivedQuantity < 0) ? ConstantHelper.Message.GoodsReceiptNeedsLine
                    : await InventoryLotRules.CheckPlaceAsync(db, request.WarehouseId, null);
                var duplicate = inputs.GroupBy(l => l.PurchaseOrderLineId).FirstOrDefault(g => g.Count() > 1);
                if (error == null && duplicate != null)
                {
                    error = string.Format(ConstantHelper.Message.GoodsReceiptDuplicateLine, duplicate.Key);
                }
                var poLines = po == null ? new Dictionary<Guid, PurchaseOrderLine>() : await db.PurchaseOrderLine.AsNoTracking().Where(l => l.PurchaseOrderId == po.Id && !l.IsDeleted).ToDictionaryAsync(l => l.Id);
                var ingredientIds = poLines.Values.Select(l => l.IngredientId).Distinct().ToList();
                var ingredients = await db.Ingredient.AsNoTracking().Where(i => ingredientIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id);
                DateTime receiptDate = request.ReceiptDate ?? _clock.Now;
                var warnings = new List<string>();
                if (error == null)
                {
                    var totals = await PurchasingRules.ReceiptTotalsAsync(db, po!.Id);
                    foreach (var input in inputs)
                    {
                        if (!poLines.TryGetValue(input.PurchaseOrderLineId, out var poLine))
                        {
                            error = string.Format(ConstantHelper.Message.PurchaseOrderLineNotInOrder, input.PurchaseOrderLineId);
                            break;
                        }
                        string name = ingredients.GetValueOrDefault(poLine.IngredientId)?.Name ?? poLine.IngredientId.ToString();
                        var (received, pending) = totals.TryGetValue(poLine.IngredientId, out var t) ? t : (0m, 0m);
                        decimal allowed = poLine.Quantity * (1m + _rules.GoodsReceiptOverReceivePct / 100m) - received - pending;
                        if (input.ReceivedQuantity > allowed)
                        {
                            error = string.Format(ConstantHelper.Message.GoodsReceiptOverReceive, name, PurchasingRules.Qty(input.ReceivedQuantity), PurchasingRules.Qty(Math.Max(0m, allowed)),
                                PurchasingRules.Qty(poLine.Quantity), PurchasingRules.Qty(_rules.GoodsReceiptOverReceivePct));
                            break;
                        }
                        if (input.ManufactureDate != null && input.ExpiryDate != null && input.ManufactureDate.Value.Date > input.ExpiryDate.Value.Date)
                        {
                            error = string.Format(ConstantHelper.Message.GoodsReceiptDateInvalid, name);
                            break;
                        }
                    }
                }
                if (error != null)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<GoodsReceiptDetailModel>.Fail(error);
                }

                string number = await _documentNumberService.NextAsync(DocumentKind.GoodsReceipt, receiptDate.Date);
                var receipt = new GoodsReceipt
                {
                    Id = Guid.NewGuid(), ReceiptNumber = number, ReceiptNo = number, PurchaseOrderId = po!.Id, SupplierId = po.SupplierId, WarehouseId = request.WarehouseId,
                    ReceiptDate = receiptDate, Status = GoodsReceiptStatus.QcPending,
                    Note = PurchasingRules.AppendNote($"Nhận theo PO {po.PurchaseOrderNumber ?? po.PONo}", request.Note),
                };
                // MA-1.7 (D-63): phiếu QC có số riêng QC-YYMMDD-NNNN (trước đây dùng chung số phiếu nhập).
                string inspectionNumber = await _documentNumberService.NextAsync(DocumentKind.QcInspection, receiptDate.Date);
                var inspection = new QcInspection { Id = Guid.NewGuid(), GoodsReceiptId = receipt.Id, InspectionDate = receiptDate, InspectionNumber = inspectionNumber };
                _unitOfWork.GoodsReceiptRepository.Add(receipt);
                _unitOfWork.QcInspectionRepository.Add(inspection);
                bool allAuto = true;
                foreach (var input in inputs)
                {
                    var poLine = poLines[input.PurchaseOrderLineId];
                    ingredients.TryGetValue(poLine.IngredientId, out var ingredient);
                    DateTime? expiry = input.ExpiryDate ?? (ingredient?.ShelfLifeDays is > 0 ? receiptDate.Date.AddDays(ingredient.ShelfLifeDays.Value) : null);
                    var tempRule = PurchasingRules.ReceivingTempRuleOf(_rules, ingredient?.StorageType);
                    string zone = PurchasingRules.TempZone(tempRule, input.TemperatureC);
                    // spec 7.3: không yêu cầu QC → tự đạt (PROPOSED); T15.1: hàng mát / đông lạnh chỉ tự đạt khi nhiệt độ đã đo và trong vùng đạt.
                    bool autoPass = ingredient?.QcRequired == false && zone is ReceivingTempZone.Accept or ReceivingTempZone.NotChecked;
                    allAuto &= autoPass;
                    var line = new GoodsReceiptLine
                    {
                        Id = Guid.NewGuid(), GoodsReceiptId = receipt.Id, IngredientId = poLine.IngredientId, Quantity = input.ReceivedQuantity,
                        ReceivedQuantity = input.ReceivedQuantity, AcceptedQuantity = autoPass ? input.ReceivedQuantity : 0m, RejectedQuantity = 0m,
                        UnitId = poLine.UnitId, UomId = poLine.UnitId, UnitCost = poLine.UnitPrice,
                        LotNumber = string.IsNullOrWhiteSpace(input.SupplierLotNumber) ? null : input.SupplierLotNumber.Trim(),
                        ManufactureDate = input.ManufactureDate, ExpiryDate = expiry, QualityStatus = autoPass ? IncomingQcResult.Dat : null,
                    };
                    _unitOfWork.GoodsReceiptLineRepository.Add(line);
                    _unitOfWork.QcInspectionLineRepository.Add(new QcInspectionLine
                    {
                        Id = Guid.NewGuid(), QCInspectionId = inspection.Id, IngredientId = poLine.IngredientId, GoodsReceiptLineId = line.Id,
                        Temperature = input.TemperatureC, ExpiryDate = expiry,
                        Criterion = tempRule != null
                            ? $"Nhiệt độ nhận ({ingredient?.StorageType}): đạt ≤ {PurchasingRules.Qty(tempRule.AcceptMax)} °C, có điều kiện ≤ {PurchasingRules.Qty(tempRule.ConditionalMax)} °C, lớn hơn → loại"
                            : null,
                        Result = autoPass ? IncomingQcResult.Dat : null, Remark = autoPass ? "Nguyên liệu không yêu cầu QC – tự đạt" : null,
                    });
                    switch (zone)
                    {
                        case ReceivingTempZone.Missing:
                            warnings.Add(string.Format(ConstantHelper.Message.ReceivingTempMissingWarning, ingredient?.Name, ingredient?.StorageType));
                            break;
                        case ReceivingTempZone.Conditional:
                            warnings.Add(string.Format(ConstantHelper.Message.ReceivingTempConditional, ingredient?.Name, PurchasingRules.Qty(input.TemperatureC!.Value),
                                PurchasingRules.Qty(tempRule!.AcceptMax), PurchasingRules.Qty(tempRule.ConditionalMax)));
                            break;
                        case ReceivingTempZone.Reject:
                            warnings.Add(string.Format(ConstantHelper.Message.ReceivingTempOutOfRange, ingredient?.Name, PurchasingRules.Qty(input.TemperatureC!.Value),
                                PurchasingRules.Qty(tempRule!.ConditionalMax)));
                            break;
                    }
                }
                if (allAuto)
                {
                    receipt.Status = GoodsReceiptStatus.Accepted;
                    inspection.Result = IncomingQcResult.Dat;
                }
                await _unitOfWork.GoodsReceiptRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
                _unitOfWork.CommitTransaction();
                var detail = await DetailAsync(receipt.Id);
                detail!.ListWarning.AddRange(warnings);
                return Result<GoodsReceiptDetailModel>.Success(detail, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Create: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        /// <summary>
        /// T15 (spec 7.4 post, BUSINESS_RULES §4, Pilot P6), 1 transaction – phiếu đã kết luận QC (Đạt / Đạt một phần / Không đạt):
        /// <list type="bullet">
        /// <item>mỗi dòng 1 lô <c>NL-YYMMDD-&lt;mã NCC&gt;-NN</c> (T11; NCC chưa có mã → hậu tố NCC) – QcStatus = kết quả QC; KHONG_DAT → lô Tạm giữ (bị chặn xuất,
        /// chỉ trả NCC / hủy), còn lại Khả dụng; số lô NCC ghi vào Note của lô;</item>
        /// <item>bút toán RECEIPT qua <c>InventoryLedgerService</c> CHỈ cho số đạt (DAT_MOT_PHAN chỉ nhập phần đạt; KHONG_DAT không nhập);</item>
        /// <item>TemperatureLog RECEIVING (gắn lô) cho dòng có nhiệt độ – ngưỡng theo kiểu bảo quản, IsWithinRange tính ở server;</item>
        /// <item>có dòng DAT_MOT_PHAN / KHONG_DAT → 1 SupplierIssue (QUALITY, hoặc TEMPERATURE nếu có dòng vượt nhiệt độ; High nếu có KHONG_DAT);</item>
        /// <item>phiếu → Đã ghi sổ; PO → Nhận một phần / Đã nhận đủ (theo số đạt).</item>
        /// </list>
        /// </summary>
        public async Task<IResult<GoodsReceiptDetailModel>> Post(GoodsReceiptPostRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var db = _unitOfWork.DbContext;
                await DbLocks.DocumentAsync(db, "GoodsReceipt", request.Id); // Rà soát BE: 2 lần ghi sổ cùng lúc chạy lần lượt.
                var receipt = _unitOfWork.GoodsReceiptRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
                var po = receipt?.PurchaseOrderId is { } poId ? await db.PurchaseOrder.AsNoTracking().FirstOrDefaultAsync(p => p.Id == poId && !p.IsDeleted) : null;
                string? error = receipt == null ? ConstantHelper.Message.RecordNotFoundMessage
                    : receipt.Status == GoodsReceiptStatus.QcPending ? ConstantHelper.Message.GoodsReceiptNotConcluded
                    : !GoodsReceiptStatus.Concluded.Contains(receipt.Status)
                        ? InvalidStatus(receipt, "ghi sổ phiếu nhập", GoodsReceiptStatus.Accepted, GoodsReceiptStatus.PartiallyAccepted, GoodsReceiptStatus.Rejected)
                    : po == null || !PurchaseOrderStatus.Receivable.Contains(po.Status)
                        ? string.Format(ConstantHelper.Message.GoodsReceiptPoNotReceivable, po?.PurchaseOrderNumber ?? po?.PONo, PurchaseOrderStatus.Label(po?.Status))
                    : await InventoryLotRules.CheckPlaceAsync(db, receipt.WarehouseId, request.StorageLocationId);
                var lines = receipt == null ? [] : _unitOfWork.GoodsReceiptLineRepository.GetAll().Where(l => l.GoodsReceiptId == receipt.Id && !l.IsDeleted).ToList();
                var ingredientIds = lines.Select(l => l.IngredientId).Distinct().ToList();
                var ingredients = await db.Ingredient.AsNoTracking().Where(i => ingredientIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id);
                if (error == null)
                {
                    var missing = lines.Where(l => IncomingQcResult.Normalize(l.QualityStatus) == null).Select(l => ingredients.GetValueOrDefault(l.IngredientId)?.Name).ToList();
                    if (missing.Count != 0)
                    {
                        error = string.Format(ConstantHelper.Message.QcLineMissing, string.Join(", ", missing));
                    }
                }
                if (error != null)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<GoodsReceiptDetailModel>.Fail(error);
                }
                var supplier = await db.Supplier.AsNoTracking().FirstOrDefaultAsync(s => s.Id == receipt!.SupplierId);
                var qcLines = await (from q in db.QcInspectionLine.AsNoTracking()
                                     join i in db.QcInspection.AsNoTracking() on q.QCInspectionId equals i.Id
                                     where i.GoodsReceiptId == receipt!.Id && !i.IsDeleted && !q.IsDeleted && q.GoodsReceiptLineId != null
                                     select q).ToListAsync();
                string number = receipt!.ReceiptNumber ?? receipt.ReceiptNo ?? receipt.Id.ToString("N")[..8];
                string suffix = string.IsNullOrWhiteSpace(supplier?.Code) ? "NCC" : supplier!.Code!;
                Guid actor = _currentActor.RequireActor().UserId;
                DateTime now = _clock.Now;
                var issueLines = new List<string>();
                bool temperatureIssue = false;
                bool anyRejected = false;
                var created = new List<(GoodsReceiptLine Line, MaterialLot Lot, string Result)>();

                // 1) Lô nội bộ NL- cho mọi dòng (kể cả KHONG_DAT – để truy xuất, trả NCC).
                foreach (var line in lines.OrderBy(l => l.CreatedDate))
                {
                    string result = IncomingQcResult.Normalize(line.QualityStatus)!;
                    ingredients.TryGetValue(line.IngredientId, out var ingredient);
                    string lotNumber = await _documentNumberService.NextAsync(DocumentKind.MaterialLot, receipt.ReceiptDate.Date, suffix);
                    var qc = qcLines.FirstOrDefault(q => q.GoodsReceiptLineId == line.Id);
                    var tempRule = PurchasingRules.ReceivingTempRuleOf(_rules, ingredient?.StorageType);
                    string zone = PurchasingRules.TempZone(tempRule, qc?.Temperature);
                    // T15.1: nhận có điều kiện nhiệt độ (không phải KHONG_DAT) → lô "ưu tiên dùng trước" (Note – GAP-PUR-10) + sự cố NCC.
                    bool conditional = zone == ReceivingTempZone.Conditional && result != IncomingQcResult.KhongDat;
                    string? conditionalNote = conditional
                        ? (qc?.Note?.Contains(ReceivingCode.UseFirstMarker) == true ? qc.Note : PurchasingRules.ConditionalNote(tempRule!, qc!.Temperature!.Value, false))
                        : null;
                    var lot = new MaterialLot
                    {
                        Id = Guid.NewGuid(), LotNumber = lotNumber, LotNo = lotNumber, IngredientId = line.IngredientId, SupplierId = receipt.SupplierId,
                        ManufactureDate = line.ManufactureDate, ExpiryDate = line.ExpiryDate, ReceivedDate = receipt.ReceiptDate, UnitCost = line.UnitCost,
                        QcStatus = result, QualityStatus = result, GoodsReceiptLineId = line.Id,
                        Status = result == IncomingQcResult.KhongDat ? MaterialLotStatus.OnHold : MaterialLotStatus.Available,
                        Note = PurchasingRules.AppendNote(
                            PurchasingRules.AppendNote($"Phiếu nhập {number}", string.IsNullOrWhiteSpace(line.LotNumber) ? null : $"Lô NCC: {line.LotNumber}"),
                            conditionalNote),
                    };
                    _unitOfWork.MaterialLotRepository.Add(lot);
                    line.LotId = lot.Id;
                    _unitOfWork.GoodsReceiptLineRepository.Update(line);
                    created.Add((line, lot, result));
                    if (result != IncomingQcResult.Dat || conditional)
                    {
                        anyRejected |= result == IncomingQcResult.KhongDat;
                        issueLines.Add($"{ingredient?.Name} – lô {lotNumber}: {IncomingQcResult.Label(result)}, nhận {PurchasingRules.Qty(line.ReceivedQuantity)}, đạt {PurchasingRules.Qty(line.AcceptedQuantity)}, trả {PurchasingRules.Qty(line.RejectedQuantity)}"
                            + (zone is ReceivingTempZone.Conditional or ReceivingTempZone.Reject ? $", nhiệt độ {PurchasingRules.Qty(qc!.Temperature!.Value)} °C ({ReceivingTempZone.Label(zone)})" : string.Empty)
                            + (string.IsNullOrWhiteSpace(qc?.Remark) ? string.Empty : $" ({qc.Remark})"));
                    }
                }
                await _unitOfWork.MaterialLotRepository.CommitAsync(ConstantHelper.TypeSave.Insert);

                // 2) Nhập kho phần đạt + nhiệt độ nhận hàng.
                foreach (var (line, lot, result) in created)
                {
                    ingredients.TryGetValue(line.IngredientId, out var ingredient);
                    if (line.AcceptedQuantity > 0)
                    {
                        await _ledger.PostAsync(new InventoryPostRequest
                        {
                            TransactionType = "RECEIPT", TransactionNumber = number, WarehouseId = receipt.WarehouseId, StorageLocationId = request.StorageLocationId,
                            IngredientId = line.IngredientId, MaterialLotId = lot.Id, UnitId = line.UnitId, QuantityIn = line.AcceptedQuantity, UnitCost = line.UnitCost,
                            ReferenceType = ReceivingCode.GoodsReceiptReference, ReferenceId = receipt.Id, TransactionDate = receipt.ReceiptDate,
                            Note = $"Nhập kho theo phiếu {number} – lô {lot.LotNumber} ({IncomingQcResult.Label(result)})",
                        });
                    }
                    var qc = qcLines.FirstOrDefault(q => q.GoodsReceiptLineId == line.Id);
                    if (qc?.Temperature is { } temperature)
                    {
                        decimal? max = PurchasingRules.MaxReceivingTemp(_rules, ingredient?.StorageType);
                        bool within = max == null || temperature <= max;
                        temperatureIssue |= !within;
                        _unitOfWork.TemperatureLogRepository.Add(new TemperatureLog
                        {
                            Id = Guid.NewGuid(), MaterialLotId = lot.Id, StorageLocationId = request.StorageLocationId, MeasurementType = ReceivingCode.ReceivingMeasurement,
                            TemperatureC = temperature, MaxAllowedC = max, IsWithinRange = within, MeasuredAt = receipt.ReceiptDate, SourceChannel = "MANUAL",
                            RecordedBy = actor, CorrectiveAction = within ? null : (qc.Remark ?? IncomingQcResult.Label(result)),
                        });
                    }
                }

                // 3) Sự cố NCC khi có dòng không đạt / đạt một phần.
                Guid? issueId = null;
                if (issueLines.Count != 0)
                {
                    string description = $"Phiếu nhập {number} (PO {po!.PurchaseOrderNumber ?? po.PONo}): " + string.Join("; ", issueLines);
                    var issue = new SupplierIssue
                    {
                        Id = Guid.NewGuid(), SupplierId = receipt.SupplierId, PurchaseOrderId = po.Id, GoodsReceiptId = receipt.Id,
                        IssueType = temperatureIssue ? ReceivingCode.IssueTypeTemperature : ReceivingCode.IssueTypeQuality,
                        Severity = anyRejected ? ReceivingCode.SeverityHigh : ReceivingCode.SeverityMedium,
                        Description = description.Length > 2000 ? description[..2000] : description,
                        ReportedAt = now, DueAt = now.AddDays(_rules.SupplierIssueDueDays), IssueStatus = ReceivingCode.IssueOpen,
                    };
                    _unitOfWork.SupplierIssueRepository.Add(issue);
                    issueId = issue.Id;
                }

                receipt.Status = GoodsReceiptStatus.Posted;
                receipt.Note = PurchasingRules.AppendNote(receipt.Note, request.Note);
                _unitOfWork.GoodsReceiptRepository.Update(receipt);
                await _unitOfWork.GoodsReceiptRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                await _purchaseOrderService.RefreshReceiptStatusAsync(po!.Id);
                _unitOfWork.CommitTransaction();
                var detail = await DetailAsync(receipt.Id);
                detail!.SupplierIssueId = issueId;
                return Result<GoodsReceiptDetailModel>.Success(detail, ConstantHelper.Message.SaveSuccess);
            }
            catch (BusinessRuleException ex)
            {
                _unitOfWork.RollbackTransaction();
                return Result<GoodsReceiptDetailModel>.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Post: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        /// <summary>T15: hủy phiếu nhập chưa ghi sổ (Chờ QC / đã kết luận) – lý do bắt buộc (PROPOSED – GAP-PUR-05). Phiếu đã ghi sổ: sửa bằng bút toán đảo / trả NCC (sau Pilot).</summary>
        public async Task<IResult<GoodsReceiptDetailModel>> Cancel(PurchaseCancelRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                string? reason = request.Reason?.Trim();
                await DbLocks.DocumentAsync(_unitOfWork.DbContext, "GoodsReceipt", request.Id); // Rà soát BE: hủy và ghi sổ cùng lúc chạy lần lượt.
                var receipt = _unitOfWork.GoodsReceiptRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
                string? error = string.IsNullOrEmpty(reason) || reason.Length < 3 ? ConstantHelper.Message.CancelReasonRequired
                    : receipt == null ? ConstantHelper.Message.RecordNotFoundMessage
                    : receipt.Status != GoodsReceiptStatus.QcPending && !GoodsReceiptStatus.Concluded.Contains(receipt.Status)
                        ? InvalidStatus(receipt, "hủy phiếu nhập", GoodsReceiptStatus.QcPending, GoodsReceiptStatus.Accepted, GoodsReceiptStatus.PartiallyAccepted, GoodsReceiptStatus.Rejected)
                    : null;
                if (error != null)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<GoodsReceiptDetailModel>.Fail(error);
                }
                receipt!.Status = GoodsReceiptStatus.Cancelled;
                receipt.Note = PurchasingRules.AppendNote(receipt.Note, "Hủy: " + reason);
                _unitOfWork.GoodsReceiptRepository.Update(receipt);
                await _unitOfWork.GoodsReceiptRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                return Result<GoodsReceiptDetailModel>.Success(await DetailAsync(receipt.Id), ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Cancel: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        /// <summary>Chi tiết phiếu nhập (spec 7.4 / 9.1 GET): dòng + số đặt, kết quả QC, nhiệt độ + ngưỡng + gợi ý kết quả, lô đã tạo, thao tác được phép.</summary>
        public async Task<IResult<GoodsReceiptDetailModel>> GetDetail(Guid id)
        {
            try
            {
                var detail = await DetailAsync(id);
                return detail == null
                    ? Result<GoodsReceiptDetailModel>.Fail(ConstantHelper.Message.RecordNotFoundMessage)
                    : Result<GoodsReceiptDetailModel>.Success(detail, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetDetail: ", ex.ToString()));
                throw;
            }
        }

        public async Task<GoodsReceiptDetailModel?> DetailAsync(Guid id)
        {
            var db = _unitOfWork.DbContext;
            var receipt = await db.GoodsReceipt.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            if (receipt == null)
            {
                return null;
            }
            var lines = await db.GoodsReceiptLine.AsNoTracking().Where(l => l.GoodsReceiptId == id && !l.IsDeleted).OrderBy(l => l.CreatedDate).ToListAsync();
            var inspection = await db.QcInspection.AsNoTracking().Where(q => q.GoodsReceiptId == id && !q.IsDeleted).OrderByDescending(q => q.CreatedDate).FirstOrDefaultAsync();
            var qcLines = inspection == null ? [] : await db.QcInspectionLine.AsNoTracking().Where(q => q.QCInspectionId == inspection.Id && !q.IsDeleted).ToListAsync();
            var po = receipt.PurchaseOrderId is { } poId ? await db.PurchaseOrder.AsNoTracking().FirstOrDefaultAsync(p => p.Id == poId) : null;
            var poLines = po == null ? [] : await db.PurchaseOrderLine.AsNoTracking().Where(l => l.PurchaseOrderId == po.Id && !l.IsDeleted).ToListAsync();
            var ingredientIds = lines.Select(l => l.IngredientId).Distinct().ToList();
            var unitIds = lines.Select(l => l.UnitId).Distinct().ToList();
            var lotIds = lines.Where(l => l.LotId != null).Select(l => l.LotId!.Value).ToList();
            var ingredients = await db.Ingredient.AsNoTracking().Where(i => ingredientIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id);
            var units = await db.UnitMeasure.AsNoTracking().Where(u => unitIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name);
            var lots = await db.MaterialLot.AsNoTracking().Where(l => lotIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id);
            var supplier = await db.Supplier.AsNoTracking().FirstOrDefaultAsync(s => s.Id == receipt.SupplierId);
            var warehouse = await db.Warehouse.AsNoTracking().Where(w => w.Id == receipt.WarehouseId).Select(w => w.Name).FirstOrDefaultAsync();
            var detail = new GoodsReceiptDetailModel
            {
                GoodsReceipt = receipt, StatusName = GoodsReceiptStatus.Label(receipt.Status), PurchaseOrderNumber = po?.PurchaseOrderNumber ?? po?.PONo,
                SupplierCode = supplier?.Code, SupplierName = supplier?.Name, WarehouseName = warehouse,
                QcInspectionId = inspection?.Id, InspectionNumber = inspection?.InspectionNumber, QcResult = inspection?.Result, QcResultName = IncomingQcResult.Label(inspection?.Result),
                SupplierIssueId = await db.SupplierIssue.AsNoTracking().Where(s => s.GoodsReceiptId == id && !s.IsDeleted).Select(s => (Guid?)s.Id).FirstOrDefaultAsync(),
            };
            foreach (var l in lines)
            {
                ingredients.TryGetValue(l.IngredientId, out var ingredient);
                var qc = qcLines.FirstOrDefault(q => q.GoodsReceiptLineId == l.Id);
                var poLine = poLines.FirstOrDefault(p => p.IngredientId == l.IngredientId);
                var tempRule = PurchasingRules.ReceivingTempRuleOf(_rules, ingredient?.StorageType);
                decimal? max = tempRule?.AcceptMax;
                string zone = PurchasingRules.TempZone(tempRule, qc?.Temperature);
                MaterialLot? lot = l.LotId is { } lotId ? lots.GetValueOrDefault(lotId) : null;
                detail.ListLine.Add(new GoodsReceiptLineDetailModel
                {
                    Id = l.Id, PurchaseOrderLineId = poLine?.Id, IngredientId = l.IngredientId, IngredientCode = ingredient?.Code, IngredientName = ingredient?.Name,
                    StorageType = ingredient?.StorageType, UnitId = l.UnitId, UnitName = units.GetValueOrDefault(l.UnitId), OrderedQuantity = poLine?.Quantity,
                    ReceivedQuantity = l.ReceivedQuantity, AcceptedQuantity = l.AcceptedQuantity, RejectedQuantity = l.RejectedQuantity, UnitCost = l.UnitCost,
                    SupplierLotNumber = l.LotNumber, ManufactureDate = l.ManufactureDate, ExpiryDate = l.ExpiryDate,
                    QcInspectionLineId = qc?.Id, QualityStatus = l.QualityStatus, QualityStatusName = IncomingQcResult.Label(l.QualityStatus),
                    Temperature = qc?.Temperature, MaxAllowedTemperature = max,
                    TemperatureWithinRange = qc?.Temperature is { } temp && max != null ? temp <= max : null,
                    ConditionalMaxTemperature = tempRule?.ConditionalMax, TemperatureZone = zone, TemperatureZoneName = ReceivingTempZone.Label(zone),
                    NoThawConfirmationRequired = zone == ReceivingTempZone.Conditional && tempRule!.RequireNoThawConfirmation,
                    UseFirst = (qc?.Note?.Contains(ReceivingCode.UseFirstMarker) ?? false) || (lot?.Note?.Contains(ReceivingCode.UseFirstMarker) ?? false),
                    SuggestedResult = PurchasingRules.SuggestResult(zone, l.ExpiryDate, receipt.ReceiptDate),
                    Weight = qc?.Weight, Appearance = qc?.Appearance, Packaging = qc?.Packaging, Remark = qc?.Remark,
                    MaterialLotId = lot?.Id, LotNumber = lot?.LotNumber ?? lot?.LotNo, LotStatus = lot?.Status,
                });
            }
            switch (receipt.Status)
            {
                case GoodsReceiptStatus.QcPending:
                    detail.AllowedActions.AddRange(["conclude", "cancel"]);
                    break;
                case GoodsReceiptStatus.Accepted:
                case GoodsReceiptStatus.PartiallyAccepted:
                case GoodsReceiptStatus.Rejected:
                    detail.AllowedActions.AddRange(["conclude", "post", "cancel"]);
                    break;
            }
            return detail;
        }
    }
}
