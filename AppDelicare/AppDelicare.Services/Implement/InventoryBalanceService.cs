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
    public class InventoryBalanceService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService, IInventoryLedgerService inventoryLedgerService, IClock clock, IOptions<BusinessRulesOptions> rules) : IInventoryBalanceService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;
        private readonly IInventoryLedgerService _inventoryLedgerService = inventoryLedgerService;
        private readonly IClock _clock = clock;
        private readonly BusinessRulesOptions _rules = rules.Value;

        public async Task<IResults<InventoryBalanceViewModel>> GetAll(InventoryBalanceSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@WarehouseId", request.WarehouseId == Guid.Empty ? null : request.WarehouseId);
                dynamicParameters.Add("@IngredientId", request.IngredientId == Guid.Empty ? null : request.IngredientId);
                dynamicParameters.Add("@MaterialLotId", request.MaterialLotId);
                dynamicParameters.Add("@StorageLocationId", request.StorageLocationId);
                //dynamicParameters.Add("@OnHandQuantity", request.OnHandQuantity);
                //dynamicParameters.Add("@ReservedQuantity", request.ReservedQuantity);
                //dynamicParameters.Add("@AvailableQuantity", request.AvailableQuantity);
                dynamicParameters.Add("@UnitId", request.UnitId == Guid.Empty ? null : request.UnitId);
                dynamicParameters.Add("@LastCalculatedDate", request.LastCalculatedDate);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<InventoryBalanceViewModel>("uspInventoryBalance_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<InventoryBalanceViewModel>
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

        public IResults<InventoryBalance> GetAllData()
        {
            try
            {
                var list = _unitOfWork.InventoryBalanceRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<InventoryBalance>
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

        public async Task<IResult<bool>> Save(InventoryBalanceRequest request, string typeSave)
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

        /// <summary>T16: tồn chỉ là bảng tổng hợp tính từ sổ cái (BUSINESS_RULES §5) – CRUD không ghi.</summary>
        private Task<string> Insert(InventoryBalanceRequest request, string typeSave)
            => Task.FromResult(ConstantHelper.Message.BalanceCrudBlocked);

        private Task<string> Update(InventoryBalanceRequest request, string typeSave)
            => Task.FromResult(ConstantHelper.Message.BalanceCrudBlocked);

        private Task<string> Delete(Guid? id, string typeSave)
            => Task.FromResult(ConstantHelper.Message.BalanceCrudBlocked);

        public async Task<IResult<InventoryBalance>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.InventoryBalanceRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<InventoryBalance>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<InventoryBalance>
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
        /// T16 (spec 7.6 summary): tồn theo (kho, nguyên liệu) từ InventoryBalance + lô + InventoryPolicie: tồn, giữ chỗ, khả dụng,
        /// xuất được (trừ lô bị chặn), số lô sắp hết hạn (≤ ExpiryWarningDays của chính sách, mặc định BusinessRules:ExpiringLotDays), mức tồn.
        /// Mức (PROPOSED – BA/PO CONFIRM): critical = xuất được ≤ CriticalLevel hoặc &lt; MinimumStock; reorder = ≤ ReorderPoint (hoặc ≤ SafetyStock); còn lại ok.
        /// </summary>
        public async Task<IResults<InventorySummaryModel>> GetSummary(InventorySummaryRequest request)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                var rows = await InventoryLotRules.LoadAsync(db, _clock.Today, _rules.ExpiringLotDays, request.WarehouseId, request.IngredientId,
                    includeZero: request.IncludeZero == true);
                // REAL-1 (D-129): màn tồn kho bỏ dòng của kho / nguyên liệu đã xóa mềm (danh mục [DEMO] đã dọn) – sổ kho giữ nguyên; giao diện không xóa được kho / NL còn tồn.
                var whIds = rows.Select(r => r.WarehouseId).Distinct().ToList();
                var ingIds = rows.Select(r => r.IngredientId).Distinct().ToList();
                var liveWh = (await db.Warehouse.AsNoTracking().Where(w => whIds.Contains(w.Id) && !w.IsDeleted).Select(w => w.Id).ToListAsync()).ToHashSet();
                var liveIng = (await db.Ingredient.AsNoTracking().Where(i => ingIds.Contains(i.Id) && !i.IsDeleted).Select(i => i.Id).ToListAsync()).ToHashSet();
                rows = [.. rows.Where(r => liveWh.Contains(r.WarehouseId) && liveIng.Contains(r.IngredientId))];
                if (request.IngredientCategoryId != null)
                {
                    var ids = rows.Select(r => r.IngredientId).Distinct().ToList();
                    var inCategory = await db.Ingredient.AsNoTracking()
                        .Where(i => ids.Contains(i.Id) && (i.IngredientCategoryId == request.IngredientCategoryId || i.CategoryId == request.IngredientCategoryId))
                        .Select(i => i.Id).ToListAsync();
                    rows = [.. rows.Where(r => inCategory.Contains(r.IngredientId))];
                }
                if (!string.IsNullOrWhiteSpace(request.SearchText))
                {
                    string text = request.SearchText.Trim();
                    rows = [.. rows.Where(r => (r.IngredientName ?? string.Empty).Contains(text, StringComparison.OrdinalIgnoreCase)
                        || (r.IngredientCode ?? string.Empty).Contains(text, StringComparison.OrdinalIgnoreCase))];
                }
                var ingredientIds = rows.Select(r => r.IngredientId).Distinct().ToList();
                var policies = (await db.InventoryPolicy.AsNoTracking()
                        .Where(p => !p.IsDeleted && ingredientIds.Contains(p.IngredientId))
                        .OrderByDescending(p => p.CreatedDate).ToListAsync())
                    .GroupBy(p => (p.WarehouseId, p.IngredientId)).ToDictionary(g => g.Key, g => g.First());

                var list = new List<InventorySummaryModel>();
                foreach (var group in rows.GroupBy(r => (r.WarehouseId, r.IngredientId)))
                {
                    var first = group.First();
                    policies.TryGetValue(group.Key, out var policy);
                    decimal usable = group.Where(r => !r.IsBlocked).Sum(r => r.AvailableQuantity);
                    var lotRows = group.Where(r => r.MaterialLotId != null && r.OnHandQuantity > 0).GroupBy(r => r.MaterialLotId).Select(g => g.First()).ToList();
                    var model = new InventorySummaryModel
                    {
                        WarehouseId = first.WarehouseId,
                        WarehouseName = first.WarehouseName,
                        IngredientId = first.IngredientId,
                        IngredientCode = first.IngredientCode,
                        IngredientName = first.IngredientName,
                        UnitId = first.UnitId,
                        UnitName = first.UnitName,
                        OnHandQuantity = group.Sum(r => r.OnHandQuantity),
                        ReservedQuantity = group.Sum(r => r.ReservedQuantity),
                        AvailableQuantity = group.Sum(r => r.AvailableQuantity),
                        UsableQuantity = usable,
                        BlockedQuantity = group.Where(r => r.IsBlocked).Sum(r => r.OnHandQuantity),
                        ExpiredQuantity = group.Where(r => r.IsExpired).Sum(r => r.OnHandQuantity),
                        LotCount = lotRows.Count,
                        ExpiringLotCount = lotRows.Count(r => r.IsExpiring),
                        ExpiredLotCount = lotRows.Count(r => r.IsExpired),
                        NearestExpiryDate = lotRows.Where(r => !r.IsExpired && r.ExpiryDate != null).Min(r => r.ExpiryDate),
                        MinimumStock = policy?.MinimumStock ?? policy?.MinStock,
                        SafetyStock = policy?.SafetyStock,
                        ReorderPoint = policy?.ReorderPoint,
                        CriticalLevel = policy?.CriticalLevel,
                        UseFefo = policy?.UseFefo ?? true,
                    };
                    model.Level = (model.CriticalLevel != null && usable <= model.CriticalLevel) || (model.MinimumStock != null && usable < model.MinimumStock) ? "critical"
                        : (model.ReorderPoint != null && usable <= model.ReorderPoint) || (model.SafetyStock != null && usable <= model.SafetyStock) ? "reorder"
                        : "ok";
                    list.Add(model);
                }
                if (request.BelowMin == true)
                {
                    list = [.. list.Where(x => x.Level != "ok")];
                }
                list = [.. list.OrderBy(x => x.Level == "critical" ? 0 : x.Level == "reorder" ? 1 : 2).ThenBy(x => x.WarehouseName).ThenBy(x => x.IngredientName)];
                int i = 0;
                list.ForEach(x => x.STT = ++i);
                return Results<InventorySummaryModel>.Success(list, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetSummary: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>
        /// T16 (spec 7.6 by-lot, 7.5 lots): tồn theo lô / vị trí, sắp theo FEFO / FIFO (PickRank), kèm hạn dùng, QC, lý do chặn xuất,
        /// lọc lô hết hạn trong N ngày (ExpiringWithinDays, kể cả lô đã hết hạn).
        /// </summary>
        public async Task<IResults<InventoryLotBalanceModel>> GetByLot(InventoryByLotRequest request)
        {
            try
            {
                var rows = await InventoryLotRules.LoadAsync(_unitOfWork.DbContext, _clock.Today, _rules.ExpiringLotDays,
                    request.WarehouseId, request.IngredientId, request.MaterialLotId, request.IncludeZero == true);
                if (request.ExpiringWithinDays is { } days)
                {
                    rows = [.. rows.Where(r => r.DaysToExpiry != null && r.DaysToExpiry <= days)];
                }
                if (!string.IsNullOrWhiteSpace(request.StorageLocationCode))
                {
                    // BOT-2.4: QR kệ / vị trí kho.
                    string code = request.StorageLocationCode.Trim();
                    var locations = await _unitOfWork.DbContext.StorageLocation.AsNoTracking().Where(l => !l.IsDeleted && l.Code == code).Select(l => l.Id).ToListAsync();
                    rows = [.. rows.Where(r => r.StorageLocationId != null && locations.Contains(r.StorageLocationId.Value))];
                }
                int i = 0;
                rows.ForEach(x => x.STT = ++i);
                return Results<InventoryLotBalanceModel>.Success(rows, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetByLot: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>T16 (spec 7.6 rebuild-balance): quản trị – tính lại InventoryBalance từ sổ cái (InventoryLedgerService), 1 transaction.</summary>
        public async Task<IResult<InventoryRebuildModel>> RebuildBalance(InventoryRebuildRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var (rows, changed) = await _inventoryLedgerService.RebuildBalancesAsync(request.WarehouseId);
                _unitOfWork.CommitTransaction();
                return Result<InventoryRebuildModel>.Success(new InventoryRebuildModel { Rows = rows, Changed = changed }, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " RebuildBalance: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }
    }
}
