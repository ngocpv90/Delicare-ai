using AutoMapper;
using AppDelicare.Data.Share;
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
    public class InventoryTransactionService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService
        , IInventoryLedgerService inventoryLedgerService, IDocumentNumberService documentNumberService, IClock clock) : IInventoryTransactionService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IInventoryLedgerService _inventoryLedgerService = inventoryLedgerService;
        private readonly IDocumentNumberService _documentNumberService = documentNumberService;
        private readonly IClock _clock = clock;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;

        public async Task<IResults<InventoryTransactionViewModel>> GetAll(InventoryTransactionSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@WarehouseId", request.WarehouseId);
                dynamicParameters.Add("@IngredientId", request.IngredientId);
                dynamicParameters.Add("@MaterialLotId", request.MaterialLotId);
                dynamicParameters.Add("@StorageLocationId", request.StorageLocationId);
                dynamicParameters.Add("@TransactionNumber", request.TransactionNumber);
                dynamicParameters.Add("@TransactionType", request.TransactionType);
                dynamicParameters.Add("@TransactionDate", request.TransactionDate);
                //dynamicParameters.Add("@QuantityIn", request.QuantityIn);
                //dynamicParameters.Add("@QuantityOut", request.QuantityOut);
                dynamicParameters.Add("@UnitId", request.UnitId);
                dynamicParameters.Add("@ReferenceType", request.ReferenceType);
                dynamicParameters.Add("@ReferenceId", request.ReferenceId);
                //dynamicParameters.Add("@Reason", request.Reason);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<InventoryTransactionViewModel>("uspInventoryTransaction_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<InventoryTransactionViewModel>
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

        public IResults<InventoryTransaction> GetAllData()
        {
            try
            {
                var list = _unitOfWork.InventoryTransactionRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<InventoryTransaction>
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

        public async Task<IResult<bool>> Save(InventoryTransactionRequest request, string typeSave)
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

        /// <summary>T16: sổ chỉ ghi thêm – CRUD Insert không ghi thẳng vào sổ (không cập nhật tồn, không kiểm lô). Ghi qua Adjust / Transfer / MaterialIssue / StockCount.</summary>
        private Task<string> Insert(InventoryTransactionRequest request, string typeSave)
            => Task.FromResult(ConstantHelper.Message.LedgerInsertBlocked);

        /// <summary>T08 / T16: sổ chỉ ghi thêm – không sửa. Sửa sai bằng Reverse.</summary>
        private Task<string> Update(InventoryTransactionRequest request, string typeSave)
            => Task.FromResult(ConstantHelper.Message.LedgerInsertBlocked);

        /// <summary>T08 / T16: sổ chỉ ghi thêm – không xóa. Sửa sai bằng Reverse.</summary>
        private Task<string> Delete(Guid? id, string typeSave)
            => Task.FromResult(ConstantHelper.Message.LedgerInsertBlocked);

        public async Task<IResult<InventoryTransaction>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.InventoryTransactionRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<InventoryTransaction>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<InventoryTransaction>
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

        /// <summary>
        /// T45R (T08, chuyển từ lớp v1 cũ): đảo một bút toán kho – ghi bút toán ngược dấu tham chiếu dòng gốc, tồn kho trở lại như trước.
        /// Sổ chỉ ghi thêm: không sửa / xóa bút toán. Lỗi nghiệp vụ (đã đảo, đảo bút toán đảo, không đủ tồn) → ResponseCode 400.
        /// </summary>
        public async Task<IResult<InventoryTransaction>> Reverse(InventoryReverseRequest request)
        {
            try
            {
                string reason = request.Reason?.Trim() ?? string.Empty;
                if (reason.Length is < 3 or > 1000)
                {
                    return Result<InventoryTransaction>.Fail(ConstantHelper.Message.ReasonRequired);
                }
                var reversal = await _inventoryLedgerService.ReverseAsync(request.Id, reason);
                return Result<InventoryTransaction>.Success(reversal, ConstantHelper.Message.SaveSuccess);
            }
            catch (BusinessRuleException ex)
            {
                return Result<InventoryTransaction>.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Reverse: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>
        /// T16 (spec 7.6 inventory-transactions): sổ cái có lọc khoảng ngày, loại, chứng từ nguồn – SP <c>uspInventoryTransaction_GetLedger</c> (chỉ đọc).
        /// </summary>
        public async Task<IResults<InventoryTransactionViewModel>> GetLedger(InventoryLedgerSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@WarehouseId", request.WarehouseId);
                dynamicParameters.Add("@IngredientId", request.IngredientId);
                dynamicParameters.Add("@MaterialLotId", request.MaterialLotId);
                dynamicParameters.Add("@TransactionType", request.TransactionType);
                dynamicParameters.Add("@ReferenceType", request.ReferenceType);
                dynamicParameters.Add("@ReferenceId", request.ReferenceId);
                dynamicParameters.Add("@FromDate", request.FromDate);
                dynamicParameters.Add("@ToDate", request.ToDate);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<InventoryTransactionViewModel>("uspInventoryTransaction_GetLedger", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<InventoryTransactionViewModel>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.GetAllSuccessfully,
                    ListData = list,
                    TotalRecords = list.FirstOrDefault()?.TotalRow ?? 0
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetLedger: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>
        /// T16 (spec 7.6 adjust, GAP-F-04): bút toán ADJUSTMENT tay, 1 transaction: kiểm kho / vị trí / nguyên liệu / lô / đơn vị,
        /// số PH- (T11), <c>ReferenceType = ManualAdjustment</c>, lý do bắt buộc; giảm quá tồn → lỗi. Được giảm cả lô hết hạn / QC không đạt (hủy hàng).
        /// Duyệt điều chỉnh (STOCK_ADJUSTMENT) → DEFERRED(PILOT): quyền Sửa trên scm-inv-txn (PROPOSED – BA/PO CONFIRM).
        /// </summary>
        public async Task<IResult<InventoryTransaction>> Adjust(InventoryAdjustRequest request)
        {
            string reason = request.Reason?.Trim() ?? string.Empty;
            if (reason.Length is < 3 or > 1000)
            {
                return Result<InventoryTransaction>.Fail(ConstantHelper.Message.ReasonRequired);
            }
            if (request.Quantity == 0)
            {
                return Result<InventoryTransaction>.Fail(ConstantHelper.Message.AdjustQuantityNonZero);
            }
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var db = _unitOfWork.DbContext;
                string? error = await InventoryLotRules.CheckPlaceAsync(db, request.WarehouseId, request.StorageLocationId);
                var (ingredient, lot, lotError) = error == null ? await InventoryLotRules.CheckIngredientLotAsync(db, request.IngredientId, request.MaterialLotId) : (null, null, null);
                error ??= lotError;
                Guid unitId = Guid.Empty;
                if (error == null)
                {
                    (unitId, error) = await InventoryLotRules.ResolveUnitAsync(db, request.WarehouseId, request.StorageLocationId, ingredient!, request.MaterialLotId, request.UnitId);
                }
                if (error != null)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<InventoryTransaction>.Fail(error);
                }
                string number = await _documentNumberService.NextAsync(DocumentKind.Voucher, _clock.Today);
                var entity = await _inventoryLedgerService.PostAsync(new InventoryPostRequest
                {
                    TransactionType = InventoryLedgerService.AdjustmentType,
                    TransactionNumber = number,
                    TransactionDate = _clock.Now,
                    WarehouseId = request.WarehouseId,
                    StorageLocationId = request.StorageLocationId,
                    IngredientId = request.IngredientId,
                    MaterialLotId = request.MaterialLotId,
                    UnitId = unitId,
                    QuantityIn = request.Quantity > 0 ? request.Quantity : 0,
                    QuantityOut = request.Quantity < 0 ? -request.Quantity : 0,
                    UnitCost = request.UnitCost ?? lot?.UnitCost,
                    ReferenceType = InventoryReferenceType.ManualAdjustment,
                    Reason = reason,
                    Note = request.Note,
                });
                _unitOfWork.CommitTransaction();
                return Result<InventoryTransaction>.Success(entity, ConstantHelper.Message.SaveSuccess);
            }
            catch (BusinessRuleException ex)
            {
                _unitOfWork.RollbackTransaction();
                return Result<InventoryTransaction>.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Adjust: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        /// <summary>
        /// T16 (spec 7.6 transfer, GAP-F-03 – không có bảng phiếu chuyển): ghi cặp TRANSFER_OUT (nguồn) / TRANSFER_IN (đích) trong 1 transaction,
        /// cùng số PH-, <c>ReferenceType = Transfer</c>, <c>ReferenceId</c> = GUID mới chung cho cặp. Không chuyển quá tồn khả dụng của nguồn.
        /// </summary>
        public async Task<IResults<InventoryTransaction>> Transfer(InventoryTransferRequest request)
        {
            string reason = request.Reason?.Trim() ?? string.Empty;
            if (reason.Length is < 3 or > 1000)
            {
                return Results<InventoryTransaction>.Fail(ConstantHelper.Message.ReasonRequired);
            }
            if (request.Quantity <= 0)
            {
                return Results<InventoryTransaction>.Fail(ConstantHelper.Message.QuantityPositive);
            }
            if (request.FromWarehouseId == request.ToWarehouseId && request.FromStorageLocationId == request.ToStorageLocationId)
            {
                return Results<InventoryTransaction>.Fail(ConstantHelper.Message.TransferSameLocation);
            }
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var db = _unitOfWork.DbContext;
                string? error = await InventoryLotRules.CheckPlaceAsync(db, request.FromWarehouseId, request.FromStorageLocationId)
                    ?? await InventoryLotRules.CheckPlaceAsync(db, request.ToWarehouseId, request.ToStorageLocationId);
                var (ingredient, lot, lotError) = error == null ? await InventoryLotRules.CheckIngredientLotAsync(db, request.IngredientId, request.MaterialLotId) : (null, null, null);
                error ??= lotError;
                var source = error == null
                    ? await db.InventoryBalance.AsNoTracking().FirstOrDefaultAsync(b => !b.IsDeleted && b.WarehouseId == request.FromWarehouseId
                        && b.StorageLocationId == request.FromStorageLocationId && b.IngredientId == request.IngredientId && b.MaterialLotId == request.MaterialLotId)
                    : null;
                if (error == null && (source == null || source.AvailableQuantity < request.Quantity))
                {
                    error = string.Format(ConstantHelper.Message.InsufficientAvailable, ingredient!.Name, (source?.AvailableQuantity ?? 0).ToString("0.####"), request.Quantity.ToString("0.####"));
                }
                Guid unitId = source?.UnitId ?? Guid.Empty;
                if (error == null)
                {
                    (_, error) = await InventoryLotRules.ResolveUnitAsync(db, request.ToWarehouseId, request.ToStorageLocationId, ingredient!, request.MaterialLotId, unitId);
                }
                if (error != null)
                {
                    _unitOfWork.RollbackTransaction();
                    return Results<InventoryTransaction>.Fail(error);
                }
                string number = await _documentNumberService.NextAsync(DocumentKind.Voucher, _clock.Today);
                Guid reference = Guid.NewGuid();
                InventoryPostRequest Leg(string type, Guid warehouse, Guid? location, decimal qtyIn, decimal qtyOut) => new()
                {
                    TransactionType = type,
                    TransactionNumber = number,
                    TransactionDate = _clock.Now,
                    WarehouseId = warehouse,
                    StorageLocationId = location,
                    IngredientId = request.IngredientId,
                    MaterialLotId = request.MaterialLotId,
                    UnitId = unitId,
                    QuantityIn = qtyIn,
                    QuantityOut = qtyOut,
                    UnitCost = lot?.UnitCost,
                    ReferenceType = InventoryReferenceType.Transfer,
                    ReferenceId = reference,
                    Reason = reason,
                };
                var outLeg = await _inventoryLedgerService.PostAsync(Leg("TRANSFER_OUT", request.FromWarehouseId, request.FromStorageLocationId, 0, request.Quantity));
                var inLeg = await _inventoryLedgerService.PostAsync(Leg("TRANSFER_IN", request.ToWarehouseId, request.ToStorageLocationId, request.Quantity, 0));
                _unitOfWork.CommitTransaction();
                return new Results<InventoryTransaction>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.SaveSuccess,
                    ListData = [outLeg, inLeg],
                    TotalRecords = 2,
                };
            }
            catch (BusinessRuleException ex)
            {
                _unitOfWork.RollbackTransaction();
                return Results<InventoryTransaction>.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Transfer: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }
    }
}
