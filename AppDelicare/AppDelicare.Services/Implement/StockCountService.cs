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
using AppDelicare.Data.Share;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Model.Request.Custom;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AppDelicare.Services.Implement
{
    public class StockCountService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService, IInventoryLedgerService inventoryLedgerService, IDocumentNumberService documentNumberService
        , IPermissionService permissionService, ICurrentActor currentActor, IClock clock, IOptions<BusinessRulesOptions> rules) : IStockCountService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;
        private readonly IInventoryLedgerService _inventoryLedgerService = inventoryLedgerService;
        private readonly IDocumentNumberService _documentNumberService = documentNumberService;
        private readonly IPermissionService _permissionService = permissionService;
        private readonly ICurrentActor _currentActor = currentActor;
        private readonly IClock _clock = clock;
        private readonly BusinessRulesOptions _rules = rules.Value;

        public async Task<IResults<StockCountViewModel>> GetAll(StockCountSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@WarehouseId", request.WarehouseId == Guid.Empty ? null : request.WarehouseId);
                dynamicParameters.Add("@CountNumber", request.CountNumber);
                dynamicParameters.Add("@CountDate", request.CountDate);
                //dynamicParameters.Add("@CountType", request.CountType);
                dynamicParameters.Add("@Status", request.Status);
                dynamicParameters.Add("@ResponsibleEmployeeId", request.ResponsibleEmployeeId);
                //dynamicParameters.Add("@Note", request.Note);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<StockCountViewModel>("uspStockCount_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<StockCountViewModel>
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

        public IResults<StockCount> GetAllData()
        {
            try
            {
                var list = _unitOfWork.StockCountRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<StockCount>
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

        public async Task<IResult<bool>> Save(StockCountRequest request, string typeSave)
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

        /// <summary>T16: tạo phiếu kiểm kê chỉ qua StockCount/Create (số PH-, dòng lấy từ tồn hệ thống) – CRUD Insert không ghi.</summary>
        private Task<string> Insert(StockCountRequest request, string typeSave)
            => Task.FromResult(ConstantHelper.Message.StockCountInsertBlocked);

        /// <summary>T16: CRUD Update chỉ sửa ghi chú / người phụ trách của phiếu Draft / Counting; trạng thái, số, kho chỉ đổi qua action nghiệp vụ.</summary>
        private async Task<string> Update(StockCountRequest request, string typeSave)
        {
            var exist = _unitOfWork.StockCountRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (request.Id == null || exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            if (exist.Status != StockCountStatus.Draft && exist.Status != StockCountStatus.Counting)
            {
                return InvalidStatus(exist, "sửa phiếu kiểm kê", StockCountStatus.Draft, StockCountStatus.Counting);
            }
            exist.Note = request.Note;
            exist.ResponsibleEmployeeId = request.ResponsibleEmployeeId;
            _unitOfWork.StockCountRepository.Update(exist);
            await _unitOfWork.StockCountRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        /// <summary>T16: chỉ xóa (mềm) phiếu Draft / Counting (chưa ghi sổ), kèm dòng.</summary>
        private async Task<string> Delete(Guid? id, string typeSave)
        {
            if (id == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            var delete = _unitOfWork.StockCountRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            if (delete.Status != StockCountStatus.Draft && delete.Status != StockCountStatus.Counting)
            {
                return InvalidStatus(delete, "xóa phiếu kiểm kê", StockCountStatus.Draft, StockCountStatus.Counting);
            }
            // T07: ReferenceGuard (quan hệ logic, truy vấn có tham số); dòng của chính phiếu xóa cùng phiếu.
            if (await _auditChangeService.IsReferencedAsync(nameof(StockCount), (Guid)id, nameof(StockCountLine)))
            {
                return ConstantHelper.Message.DeleteExistFail;
            }
            foreach (var line in _unitOfWork.StockCountLineRepository.GetAll().Where(l => l.StockCountId == delete.Id && !l.IsDeleted).ToList())
            {
                _unitOfWork.StockCountLineRepository.Update(line);
            }
            _unitOfWork.StockCountRepository.Update(delete);
            await _unitOfWork.StockCountRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        public async Task<IResult<StockCount>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.StockCountRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<StockCount>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<StockCount>
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

        private static string InvalidStatus(StockCount count, string action, params string[] expected)
            => string.Format(ConstantHelper.Message.StatusActionInvalid, action, "phiếu kiểm kê", string.Join(" / ", expected.Select(StockCountStatus.Label)), StockCountStatus.Label(count.Status));

        private static string Qty(decimal value) => value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>% chênh lệch của 1 dòng (GAP-C-06: không có cột – tính khi dùng). Tồn hệ thống 0 mà có lệch → 100%.</summary>
        public static decimal VariancePercent(decimal system, decimal variance)
            => variance == 0 ? 0 : system == 0 ? 100 : Math.Round(Math.Abs(variance) / Math.Abs(system) * 100, 2);

        private static void SetPhysical(StockCountLine line, decimal physical, decimal? unitCost)
        {
            line.PhysicalQuantity = physical;
            line.PhysicalQty = physical;
            line.VarianceQuantity = physical - line.SystemQuantity;
            line.VarianceQty = line.VarianceQuantity;
            line.VarianceValue = unitCost == null ? null : Math.Round(line.VarianceQuantity * unitCost.Value, 2);
        }

        /// <summary>
        /// T16 (spec 7.8 create): tạo phiếu kiểm kê Draft, 1 transaction – số PH- (T11, ghi cả CountNumber / CountNo), loại DAILY / WEEKLY / MONTHLY / ADHOC;
        /// mỗi (nguyên liệu, lô) đang có tồn trong kho → 1 dòng: SystemQuantity = Σ OnHand các vị trí, PhysicalQuantity mặc định = SystemQuantity
        /// (dòng chưa đếm coi như khớp – PROPOSED, GAP-INV-03: không có cột "đã đếm"). Ghi song song cột cũ SystemQty / PhysicalQty / VarianceQty / LotId.
        /// </summary>
        public async Task<IResult<StockCountDetailModel>> Create(StockCountCreateRequest request)
        {
            string countType = string.IsNullOrWhiteSpace(request.CountType) ? "ADHOC" : request.CountType.Trim().ToUpperInvariant();
            if (!StockCountStatus.CountTypes.Contains(countType))
            {
                return Result<StockCountDetailModel>.Fail(ConstantHelper.Message.CountTypeInvalid);
            }
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var db = _unitOfWork.DbContext;
                string? error = await InventoryLotRules.CheckPlaceAsync(db, request.WarehouseId, null);
                if (error == null && request.IngredientId != null)
                {
                    (_, _, error) = await InventoryLotRules.CheckIngredientLotAsync(db, request.IngredientId.Value, null);
                }
                if (error != null)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<StockCountDetailModel>.Fail(error);
                }
                var balances = await db.InventoryBalance.AsNoTracking()
                    .Where(b => !b.IsDeleted && b.WarehouseId == request.WarehouseId && b.OnHandQuantity != 0
                        && (request.IngredientId == null || b.IngredientId == request.IngredientId))
                    .ToListAsync();
                var lotIds = balances.Where(b => b.MaterialLotId != null).Select(b => b.MaterialLotId!.Value).Distinct().ToList();
                var costs = await db.MaterialLot.AsNoTracking().Where(l => lotIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.UnitCost);
                var count = new StockCount
                {
                    Id = Guid.NewGuid(),
                    WarehouseId = request.WarehouseId,
                    CountType = countType,
                    CountDate = (request.CountDate ?? _clock.Today).Date,
                    Status = StockCountStatus.Draft,
                    ResponsibleEmployeeId = request.ResponsibleEmployeeId,
                    Note = request.Note,
                };
                string number = await _documentNumberService.NextAsync(DocumentKind.Voucher, _clock.Today);
                count.CountNumber = number;
                count.CountNo = number;
                _unitOfWork.StockCountRepository.Add(count);
                foreach (var group in balances.GroupBy(b => (b.IngredientId, b.MaterialLotId, b.UnitId)))
                {
                    decimal system = group.Sum(b => b.OnHandQuantity);
                    var line = new StockCountLine
                    {
                        Id = Guid.NewGuid(),
                        StockCountId = count.Id,
                        IngredientId = group.Key.IngredientId,
                        MaterialLotId = group.Key.MaterialLotId,
                        LotId = group.Key.MaterialLotId,
                        UnitId = group.Key.UnitId,
                        SystemQuantity = system,
                        SystemQty = system,
                    };
                    SetPhysical(line, system, group.Key.MaterialLotId != null ? costs.GetValueOrDefault(group.Key.MaterialLotId.Value) : null);
                    _unitOfWork.StockCountLineRepository.Add(line);
                }
                await _unitOfWork.StockCountRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
                _unitOfWork.CommitTransaction();
                return Result<StockCountDetailModel>.Success(await DetailAsync(count.Id), ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Create: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        /// <summary>
        /// T16 (spec 7.8 lines): ghi số đếm thực tế (Draft / Counting → Counting), 1 transaction. VarianceQuantity = thực tế − hệ thống,
        /// VarianceValue = chênh lệch × đơn giá lô (không có đơn giá → trống). Dòng không có Id = hàng tìm thấy ngoài sổ (tồn hệ thống 0);
        /// trùng (nguyên liệu, lô) với dòng có sẵn thì ghi vào dòng đó.
        /// </summary>
        public async Task<IResult<StockCountDetailModel>> RecordCounts(StockCountRecordRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var db = _unitOfWork.DbContext;
                var count = _unitOfWork.StockCountRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
                string? error = count == null ? ConstantHelper.Message.RecordNotFoundMessage
                    : count.Status != StockCountStatus.Draft && count.Status != StockCountStatus.Counting
                        ? InvalidStatus(count, "ghi số đếm", StockCountStatus.Draft, StockCountStatus.Counting)
                    : request.ListLine.Any(l => l.PhysicalQuantity < 0) ? ConstantHelper.Message.PhysicalQuantityNegative
                    : null;
                if (error != null)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<StockCountDetailModel>.Fail(error);
                }
                var lines = _unitOfWork.StockCountLineRepository.GetAll().Where(l => l.StockCountId == count!.Id && !l.IsDeleted).ToList();
                foreach (var item in request.ListLine)
                {
                    StockCountLine? line = item.Id != null ? lines.FirstOrDefault(l => l.Id == item.Id) : null;
                    MaterialLot? lot = null;
                    if (item.Id != null && line == null)
                    {
                        error = string.Format(ConstantHelper.Message.CountLineNotInCount, item.Id);
                    }
                    else if (line == null)
                    {
                        if (item.IngredientId == null)
                        {
                            error = ConstantHelper.Message.IngredientNotFound;
                        }
                        else
                        {
                            (var ingredient, lot, error) = await InventoryLotRules.CheckIngredientLotAsync(db, item.IngredientId.Value, item.MaterialLotId);
                            line = lines.FirstOrDefault(l => l.IngredientId == item.IngredientId && l.MaterialLotId == item.MaterialLotId);
                            if (error == null && line == null)
                            {
                                Guid unitId;
                                (unitId, error) = await InventoryLotRules.ResolveUnitAsync(db, count!.WarehouseId, null, ingredient!, item.MaterialLotId, item.UnitId);
                                if (error == null)
                                {
                                    line = new StockCountLine
                                    {
                                        Id = Guid.NewGuid(),
                                        StockCountId = count.Id,
                                        IngredientId = item.IngredientId.Value,
                                        MaterialLotId = item.MaterialLotId,
                                        LotId = item.MaterialLotId,
                                        UnitId = unitId,
                                        SystemQuantity = 0,
                                        SystemQty = 0,
                                    };
                                    _unitOfWork.StockCountLineRepository.Add(line);
                                    lines.Add(line);
                                }
                            }
                        }
                    }
                    if (error != null)
                    {
                        _unitOfWork.RollbackTransaction();
                        return Result<StockCountDetailModel>.Fail(error);
                    }
                    if (lot == null && line!.MaterialLotId != null)
                    {
                        lot = await db.MaterialLot.AsNoTracking().FirstOrDefaultAsync(l => l.Id == line.MaterialLotId);
                    }
                    SetPhysical(line!, item.PhysicalQuantity, lot?.UnitCost);
                    if (item.VarianceReason != null)
                    {
                        line!.VarianceReason = line.Reason = string.IsNullOrWhiteSpace(item.VarianceReason) ? null : item.VarianceReason.Trim();
                    }
                    if (_unitOfWork.DbContext.Entry(line!).State != EntityState.Added)
                    {
                        _unitOfWork.StockCountLineRepository.Update(line!);
                    }
                }
                count!.Status = StockCountStatus.Counting;
                _unitOfWork.StockCountRepository.Update(count);
                await _unitOfWork.StockCountRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                return Result<StockCountDetailModel>.Success(await DetailAsync(count.Id), ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " RecordCounts: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        /// <summary>
        /// T16 (spec 7.8 post): ghi sổ phiếu kiểm kê Draft / Counting → Posted, 1 transaction. Dòng lệch ≠ 0 phải có lý do;
        /// mỗi dòng lệch → bút toán ADJUSTMENT qua InventoryLedgerService (<c>ReferenceType = StockCount</c>, <c>Reason</c> = lý do lệch, số = số phiếu):
        /// thừa → nhập vào vị trí đang giữ nhiều nhất của lô, thiếu → giảm lần lượt các vị trí. Điều chỉnh theo chênh lệch đã ghi (tồn lúc tạo phiếu).
        /// Lệch &gt; <c>BusinessRules:StockCountVariancePctNeedApproval</c> (2%) ở bất kỳ dòng nào: Pilot chỉ admin ghi sổ được
        /// (duyệt kiểm kê STOCK_COUNT → DEFERRED(PILOT), PROPOSED – BA/PO CONFIRM). ApprovedBy / ApprovedAt = người ghi sổ.
        /// </summary>
        public async Task<IResult<StockCountDetailModel>> Post(InventoryDocumentActionRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var db = _unitOfWork.DbContext;
                var count = _unitOfWork.StockCountRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
                if (count == null || (count.Status != StockCountStatus.Draft && count.Status != StockCountStatus.Counting))
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<StockCountDetailModel>.Fail(count == null ? ConstantHelper.Message.RecordNotFoundMessage
                        : InvalidStatus(count, "ghi sổ kiểm kê", StockCountStatus.Draft, StockCountStatus.Counting));
                }
                var lines = await db.StockCountLine.AsNoTracking().Where(l => l.StockCountId == count.Id && !l.IsDeleted && l.VarianceQuantity != 0).ToListAsync();
                var ingredientIds = lines.Select(l => l.IngredientId).Distinct().ToList();
                var names = await db.Ingredient.AsNoTracking().Where(i => ingredientIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.Name);
                var lotIds = lines.Where(l => l.MaterialLotId != null).Select(l => l.MaterialLotId!.Value).Distinct().ToList();
                var lots = await db.MaterialLot.AsNoTracking().Where(l => lotIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id);
                string Label(StockCountLine l) => names.GetValueOrDefault(l.IngredientId) + (l.MaterialLotId != null && lots.TryGetValue(l.MaterialLotId.Value, out var lot) ? " – lô " + (lot.LotNumber ?? lot.LotNo) : string.Empty);

                var missingReason = lines.FirstOrDefault(l => string.IsNullOrWhiteSpace(l.VarianceReason ?? l.Reason));
                if (missingReason != null)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<StockCountDetailModel>.Fail(string.Format(ConstantHelper.Message.VarianceReasonRequired, Label(missingReason), Qty(missingReason.VarianceQuantity)));
                }
                decimal threshold = _rules.StockCountVariancePctNeedApproval;
                decimal maxPercent = lines.Count == 0 ? 0 : lines.Max(l => VariancePercent(l.SystemQuantity, l.VarianceQuantity));
                Guid actor = _currentActor.RequireActor().UserId;
                if (maxPercent > threshold && !(await _permissionService.GetAsync(actor)).IsAdmin)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<StockCountDetailModel>.Fail(string.Format(ConstantHelper.Message.VarianceNeedsAdmin, maxPercent, threshold));
                }

                // 1) Lập kế hoạch bút toán, kiểm đủ tồn cho dòng thiếu trước khi ghi.
                var plan = new List<(StockCountLine Line, Guid? StorageLocationId, decimal QuantityIn, decimal QuantityOut)>();
                foreach (var line in lines)
                {
                    var balances = await db.InventoryBalance.AsNoTracking()
                        .Where(b => !b.IsDeleted && b.WarehouseId == count.WarehouseId && b.IngredientId == line.IngredientId && b.MaterialLotId == line.MaterialLotId)
                        .ToListAsync();
                    if (line.VarianceQuantity > 0)
                    {
                        plan.Add((line, balances.OrderByDescending(b => b.OnHandQuantity).FirstOrDefault()?.StorageLocationId, line.VarianceQuantity, 0));
                        continue;
                    }
                    var chunks = InventoryLotRules.AllocateOverLocations(
                        balances.Select(b => new InventoryBalance { Id = b.Id, StorageLocationId = b.StorageLocationId, UnitId = b.UnitId, AvailableQuantity = b.OnHandQuantity }),
                        -line.VarianceQuantity);
                    if (chunks.Count == 0)
                    {
                        _unitOfWork.RollbackTransaction();
                        return Result<StockCountDetailModel>.Fail(string.Format(ConstantHelper.Message.InsufficientAvailable,
                            Label(line), Qty(balances.Sum(b => b.OnHandQuantity)), Qty(-line.VarianceQuantity)));
                    }
                    plan.AddRange(chunks.Select(c => (line, c.StorageLocationId, 0m, c.Quantity)));
                }
                // 2) Ghi sổ.
                string number = count.CountNumber ?? count.CountNo ?? count.Id.ToString("N")[..8];
                foreach (var (line, location, quantityIn, quantityOut) in plan)
                {
                    await _inventoryLedgerService.PostAsync(new InventoryPostRequest
                    {
                        TransactionType = InventoryLedgerService.AdjustmentType,
                        TransactionNumber = number,
                        TransactionDate = _clock.Now,
                        WarehouseId = count.WarehouseId,
                        StorageLocationId = location,
                        IngredientId = line.IngredientId,
                        MaterialLotId = line.MaterialLotId,
                        UnitId = line.UnitId,
                        QuantityIn = quantityIn,
                        QuantityOut = quantityOut,
                        UnitCost = line.MaterialLotId != null && lots.TryGetValue(line.MaterialLotId.Value, out var lot) ? lot.UnitCost : null,
                        ReferenceType = InventoryReferenceType.StockCount,
                        ReferenceId = count.Id,
                        Reason = (line.VarianceReason ?? line.Reason)!.Trim(),
                        Note = $"Kiểm kê {number}: hệ thống {Qty(line.SystemQuantity)}, thực tế {Qty(line.PhysicalQuantity)}",
                    });
                }
                count.Status = StockCountStatus.Posted;
                count.ApprovedBy = actor;
                count.ApprovedAt = _clock.Now;
                if (!string.IsNullOrWhiteSpace(request.Note))
                {
                    count.Note = string.IsNullOrWhiteSpace(count.Note) ? request.Note.Trim() : $"{count.Note}\n{request.Note.Trim()}";
                }
                _unitOfWork.StockCountRepository.Update(count);
                await _unitOfWork.StockCountRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                return Result<StockCountDetailModel>.Success(await DetailAsync(count.Id), ConstantHelper.Message.SaveSuccess);
            }
            catch (BusinessRuleException ex)
            {
                _unitOfWork.RollbackTransaction();
                return Result<StockCountDetailModel>.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Post: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        /// <summary>T16: chi tiết phiếu kiểm kê (dòng + % lệch + vượt ngưỡng + bút toán đã ghi + thao tác được phép).</summary>
        public async Task<IResult<StockCountDetailModel>> GetDetail(Guid id)
        {
            try
            {
                var detail = await DetailAsync(id);
                return detail == null ? Result<StockCountDetailModel>.Fail(ConstantHelper.Message.RecordNotFoundMessage)
                    : Result<StockCountDetailModel>.Success(detail, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetDetail: ", ex.ToString()));
                throw;
            }
        }

        private async Task<StockCountDetailModel?> DetailAsync(Guid id)
        {
            var db = _unitOfWork.DbContext;
            var count = await db.StockCount.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            if (count == null)
            {
                return null;
            }
            var lines = await db.StockCountLine.AsNoTracking().Where(l => l.StockCountId == id && !l.IsDeleted).ToListAsync();
            var ingredientIds = lines.Select(l => l.IngredientId).Distinct().ToList();
            var lotIds = lines.Where(l => l.MaterialLotId != null).Select(l => l.MaterialLotId!.Value).Distinct().ToList();
            var unitIds = lines.Select(l => l.UnitId).Distinct().ToList();
            var ingredients = await db.Ingredient.AsNoTracking().Where(i => ingredientIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id);
            var lots = await db.MaterialLot.AsNoTracking().Where(l => lotIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id);
            var units = await db.UnitMeasure.AsNoTracking().Where(u => unitIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name);
            decimal threshold = _rules.StockCountVariancePctNeedApproval;
            var detailLines = lines.Select(l =>
            {
                var lot = l.MaterialLotId != null ? lots.GetValueOrDefault(l.MaterialLotId.Value) : null;
                decimal percent = VariancePercent(l.SystemQuantity, l.VarianceQuantity);
                return new StockCountLineDetailModel
                {
                    Id = l.Id,
                    IngredientId = l.IngredientId,
                    IngredientCode = ingredients.GetValueOrDefault(l.IngredientId)?.Code,
                    IngredientName = ingredients.GetValueOrDefault(l.IngredientId)?.Name,
                    MaterialLotId = l.MaterialLotId,
                    LotNumber = lot?.LotNumber ?? lot?.LotNo,
                    ExpiryDate = lot?.ExpiryDate,
                    UnitId = l.UnitId,
                    UnitName = units.GetValueOrDefault(l.UnitId),
                    SystemQuantity = l.SystemQuantity,
                    PhysicalQuantity = l.PhysicalQuantity,
                    VarianceQuantity = l.VarianceQuantity,
                    VarianceValue = l.VarianceValue,
                    VariancePercent = percent,
                    OverThreshold = percent > threshold,
                    VarianceReason = l.VarianceReason ?? l.Reason,
                };
            }).OrderBy(l => l.IngredientName).ThenBy(l => l.ExpiryDate ?? DateTime.MaxValue).ToList();
            bool open = count.Status == StockCountStatus.Draft || count.Status == StockCountStatus.Counting;
            return new StockCountDetailModel
            {
                StockCount = count,
                StatusName = StockCountStatus.Label(count.Status),
                WarehouseName = await db.Warehouse.AsNoTracking().Where(w => w.Id == count.WarehouseId).Select(w => w.Name).FirstOrDefaultAsync(),
                ListLine = detailLines,
                MaxVariancePercent = detailLines.Count == 0 ? 0 : detailLines.Max(l => l.VariancePercent),
                VarianceThresholdPercent = threshold,
                NeedsApproval = detailLines.Any(l => l.OverThreshold),
                TotalVarianceValue = detailLines.Sum(l => l.VarianceValue ?? 0),
                AllowedActions = open ? ["recordCounts", "post"] : [],
                ListTransaction = await db.InventoryTransaction.AsNoTracking()
                    .Where(t => !t.IsDeleted && t.ReferenceType == InventoryReferenceType.StockCount && t.ReferenceId == id)
                    .OrderBy(t => t.CreatedDate).ToListAsync(),
            };
        }
    }
}
