using AutoMapper;
using Microsoft.EntityFrameworkCore;
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
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;

namespace AppDelicare.Services.Implement
{
    public class IngredientService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService, IDocumentNumberService documentNumberService, AppDelicare.Data.Share.IClock clock) : IIngredientService
    {
        private readonly AppDelicare.Data.Share.IClock _clock = clock;
        private readonly IDocumentNumberService _documentNumberService = documentNumberService;
        /// <summary>MA-1.3 (D-63): mã vừa cấp khi thêm mới – báo kèm trong thông báo thành công.</summary>
        private string? _createdCode;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;

        public async Task<IResults<IngredientViewModel>> GetAll(IngredientSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<IngredientViewModel>("uspIngredient_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<IngredientViewModel>
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

        public IResults<Ingredient> GetAllData()
        {
            try
            {
                var list = _unitOfWork.IngredientRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<Ingredient>
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

        public async Task<IResult<bool>> Save(IngredientRequest request, string typeSave)
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
                        : _createdCode != null ? string.Format(ConstantHelper.Message.SaveSuccessWithCode, "nguyên liệu", _createdCode)
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

        private async Task<string> Insert(IngredientRequest request, string typeSave)
        {
            // T45R: luật nghiệp vụ (spec) – trước khi ghi.
            var invalid = await CheckRules(request);
            if (!string.IsNullOrEmpty(invalid))
            {
                return invalid;
            }
            var insert = _mapper.Map<Ingredient>(request);
            // MA-1.3 (D-63): mã danh mục máy chủ tự sinh (…), bỏ qua giá trị gửi lên; không đổi sau khi cấp.
            insert.Code = await _documentNumberService.NextCodeAsync(DocumentKind.Ingredient);
            _createdCode = insert.Code;
            _unitOfWork.IngredientRepository.Add(insert);
            await _unitOfWork.IngredientRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Update(IngredientRequest request, string typeSave)
        {
            // T45R: luật nghiệp vụ (spec) – trước khi ghi.
            var invalid = await CheckRules(request);
            if (!string.IsNullOrEmpty(invalid))
            {
                return invalid;
            }
            var exist = _unitOfWork.IngredientRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (request.Id == null || exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // MA-1.3 (D-63): mã không đổi sau khi cấp – bỏ qua giá trị gửi lên.
            request.Code = exist.Code;
            var update = _mapper.Map(request, exist);
            _unitOfWork.IngredientRepository.Update(update);
            await _unitOfWork.IngredientRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Delete(Guid? id, string typeSave)
        {
            if (id == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // T07: ReferenceGuard (quan hệ logic, truy vấn có tham số) thay SQL động uspSystem_ExecuteQuery (A10).
            if (await _auditChangeService.IsReferencedAsync(nameof(Ingredient), (Guid)id))
            {
                return ConstantHelper.Message.DeleteExistFail;
            }
            var delete = _unitOfWork.IngredientRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            _unitOfWork.IngredientRepository.Update(delete);
            await _unitOfWork.IngredientRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        public async Task<IResult<Ingredient>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.IngredientRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<Ingredient>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<Ingredient>
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

        /// <summary>T45R (spec 4.4): YieldPercent ∈ (0, 100]; StandardWastePercent ∈ [0, 100); ShelfLifeDays &gt; 0 khi quản lý hạn.</summary>
        private Task<string> CheckRules(IngredientRequest request)
        {
            if (request.YieldPercent is { } y && (y <= 0 || y > 100))
            {
                return Task.FromResult(ConstantHelper.Message.YieldPercentRange);
            }
            if (request.StandardWastePercent is { } w && (w < 0 || w >= 100))
            {
                return Task.FromResult(ConstantHelper.Message.StandardWasteRange);
            }
            if (request.IsExpiryManaged && !(request.ShelfLifeDays > 0))
            {
                return Task.FromResult(ConstantHelper.Message.ShelfLifeRequired);
            }
            return Task.FromResult(string.Empty);
        }

        /// <summary>T45R (spec 4.4): tồn khả dụng của nguyên liệu (tổng các kho) và cờ dưới mức tối thiểu.</summary>
        public async Task<IResult<IngredientStockModel>> GetStock(Guid id)
        {
            try
            {
                var ingredient = await _unitOfWork.IngredientRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (ingredient == null)
                {
                    return Result<IngredientStockModel>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
                }
                var balances = await _unitOfWork.InventoryBalanceRepository.GetAll().AsNoTracking()
                    .Where(b => b.IngredientId == id && !b.IsDeleted)
                    .GroupBy(b => b.WarehouseId).Select(g => new IngredientStockByWarehouse { WarehouseId = g.Key, AvailableQuantity = g.Sum(b => b.AvailableQuantity) })
                    .ToListAsync();
                decimal available = balances.Sum(b => b.AvailableQuantity);
                return Result<IngredientStockModel>.Success(new IngredientStockModel
                {
                    IngredientId = id,
                    AvailableQuantity = available,
                    MinimumStock = ingredient.MinimumStock,
                    BelowMinimum = ingredient.MinimumStock is { } min && available < min,
                    ListWarehouse = balances,
                }, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetStock: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>
        /// UI-6 (D-80): trang chi tiết toàn màn nguyên liệu – tồn (tổng + theo kho), lô (còn tồn trước, FEFO), giá mua gần nhất (dòng PO đã duyệt trở đi mới nhất),
        /// bảng giá NCC, lịch sử giá (30 dòng PO gần nhất), giao dịch kho (100 dòng mới nhất). Đọc dữ liệu có sẵn, không bảng / cột mới.
        /// </summary>
        public async Task<IResult<IngredientOverviewModel>> GetOverview(Guid id)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                var ingredient = await db.Ingredient.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
                if (ingredient == null)
                {
                    return Result<IngredientOverviewModel>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
                }
                DateTime today = _clock.Today;
                Guid? categoryId = ingredient.IngredientCategoryId ?? ingredient.CategoryId, unitId = ingredient.BaseUnitId ?? ingredient.BaseUomId;
                var policies = await db.InventoryPolicy.AsNoTracking().Where(p => p.IngredientId == id && !p.IsDeleted).ToListAsync();
                int warnDays = policies.Where(p => p.ExpiryWarningDays is > 0).Select(p => p.ExpiryWarningDays!.Value).DefaultIfEmpty(3).Min();
                decimal? minimum = ingredient.MinimumStock ?? policies.Select(p => p.MinimumStock ?? p.MinStock).Where(v => v != null).Max();
                decimal? safety = ingredient.SafetyStock ?? policies.Select(p => p.SafetyStock).Where(v => v != null).Max();

                var balances = await ScmOverviewRules.BalancesAsync(db, db.InventoryBalance.AsNoTracking().Where(b => b.IngredientId == id));
                var lotRows = await db.InventoryBalance.AsNoTracking().Where(b => b.IngredientId == id && !b.IsDeleted && (b.MaterialLotId != null || b.LotId != null))
                    .Select(b => new { LotId = b.MaterialLotId ?? b.LotId, b.OnHandQuantity, b.AvailableQuantity }).ToListAsync();
                var lots = await db.MaterialLot.AsNoTracking().Where(l => l.IngredientId == id && !l.IsDeleted).ToListAsync();
                var lotSupplierIds = lots.Where(l => l.SupplierId != null).Select(l => l.SupplierId!.Value).Distinct().ToList();
                var lotSuppliers = await db.Supplier.AsNoTracking().Where(s => lotSupplierIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name);
                var listLot = lots.Select(l => new ScmIngredientLotModel
                {
                    Id = l.Id, LotNumber = l.LotNumber ?? l.LotNo, SupplierName = l.SupplierId is { } s ? lotSuppliers.GetValueOrDefault(s) : null,
                    ReceivedDate = l.ReceivedDate, ExpiryDate = l.ExpiryDate, DaysToExpiry = l.ExpiryDate is { } e ? (int)(e.Date - today).TotalDays : null,
                    OnHandQuantity = lotRows.Where(b => b.LotId == l.Id).Sum(b => b.OnHandQuantity), AvailableQuantity = lotRows.Where(b => b.LotId == l.Id).Sum(b => b.AvailableQuantity),
                    Status = l.Status, StatusName = MaterialLotStatus.Label(l.Status), QcStatus = l.QcStatus ?? l.QualityStatus, IsBlocked = InventoryLotRules.BlockReason(l, today) != null,
                }).OrderByDescending(l => l.OnHandQuantity > 0).ThenBy(l => l.OnHandQuantity > 0 ? l.ExpiryDate ?? DateTime.MaxValue : DateTime.MaxValue)
                .ThenByDescending(l => l.ReceivedDate).Take(100).ToList();

                var history = await (from l in db.PurchaseOrderLine.AsNoTracking()
                                     join po in db.PurchaseOrder.AsNoTracking() on l.PurchaseOrderId equals po.Id
                                     join s in db.Supplier.AsNoTracking() on po.SupplierId equals s.Id into ss
                                     from s in ss.DefaultIfEmpty()
                                     where l.IngredientId == id && !l.IsDeleted && !po.IsDeleted
                                           && po.Status != PurchaseOrderStatus.Draft && po.Status != PurchaseOrderStatus.Cancelled
                                     orderby po.OrderDate descending, po.CreatedDate descending
                                     select new ScmPriceHistoryModel
                                     {
                                         PurchaseOrderId = po.Id, PurchaseOrderNumber = po.PurchaseOrderNumber ?? po.PONo, OrderDate = po.OrderDate, SupplierName = s != null ? s.Name : null,
                                         Quantity = l.Quantity, UnitName = db.UnitMeasure.Where(u => u.Id == l.UnitId).Select(u => u.Name).FirstOrDefault(), UnitPrice = l.UnitPrice, Status = po.Status,
                                     }).Take(30).ToListAsync();
                history.ForEach(h => h.StatusName = PurchaseOrderStatus.Label(h.Status));

                var materials = await db.SupplierMaterial.AsNoTracking().Where(m => m.IngredientId == id && !m.IsDeleted).ToListAsync();
                var matSupplierIds = materials.Select(m => m.SupplierId).Distinct().ToList();
                var matSuppliers = await db.Supplier.AsNoTracking().Where(s => matSupplierIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id);
                var puIds = materials.Where(m => m.PurchaseUomId != null).Select(m => m.PurchaseUomId!.Value).Distinct().ToList();
                var units = await db.UnitMeasure.AsNoTracking().Where(u => puIds.Contains(u.Id) || u.Id == unitId).ToDictionaryAsync(u => u.Id, u => u.Name);
                var prices = materials.Select(m => new ScmSupplierPriceModel
                {
                    Id = m.Id, SupplierId = m.SupplierId, SupplierName = matSuppliers.GetValueOrDefault(m.SupplierId)?.Name, UnitPrice = m.UnitPrice,
                    PurchaseUnitName = m.PurchaseUomId is { } pu ? units.GetValueOrDefault(pu) : null, MinimumOrderQuantity = m.MinimumOrderQuantity ?? m.MinimumOrderQty,
                    LeadTimeDays = m.LeadTimeDays, EffectiveFrom = m.EffectiveFrom, EffectiveTo = m.EffectiveTo, IsPreferred = m.IsPreferred, Status = m.Status,
                    IsEffective = !PurchasingRules.IsInactive(m.Status) && (m.EffectiveFrom == null || m.EffectiveFrom.Value.Date <= today) && (m.EffectiveTo == null || m.EffectiveTo.Value.Date >= today)
                        && matSuppliers.TryGetValue(m.SupplierId, out var sup) && !sup.IsDeleted && !PurchasingRules.IsInactive(sup.Status),
                }).OrderByDescending(p => p.IsEffective).ThenByDescending(p => p.IsPreferred).ThenBy(p => p.UnitPrice ?? decimal.MaxValue).ToList();

                decimal available = balances.Sum(b => b.AvailableQuantity);
                var last = history.FirstOrDefault();
                return Result<IngredientOverviewModel>.Success(new IngredientOverviewModel
                {
                    Ingredient = ingredient,
                    CategoryName = categoryId is { } c ? await db.IngredientCategory.AsNoTracking().Where(x => x.Id == c).Select(x => x.Name).FirstOrDefaultAsync() : null,
                    BaseUnitName = unitId is { } u2 ? units.GetValueOrDefault(u2) : null,
                    OnHandQuantity = balances.Sum(b => b.OnHandQuantity), AvailableQuantity = available, MinimumStock = minimum, SafetyStock = safety,
                    BelowMinimum = minimum is { } min && available < min,
                    ActiveLotCount = listLot.Count(l => l.OnHandQuantity > 0),
                    ExpiringLotCount = listLot.Count(l => l.OnHandQuantity > 0 && l.DaysToExpiry is { } d && d <= warnDays), ExpiryWarningDays = warnDays,
                    LastPurchasePrice = last?.UnitPrice, LastPurchaseDate = last?.OrderDate, LastPurchaseSupplierName = last?.SupplierName,
                    LastPurchaseOrderNumber = last?.PurchaseOrderNumber, LastPurchaseUnitName = last?.UnitName,
                    PreferredSupplierName = prices.FirstOrDefault(p => p.IsEffective && p.IsPreferred)?.SupplierName ?? prices.FirstOrDefault(p => p.IsEffective)?.SupplierName,
                    SupplierCount = prices.Where(p => p.IsEffective).Select(p => p.SupplierId).Distinct().Count(),
                    ListWarehouse = balances, ListLot = listLot, ListSupplierPrice = prices, ListPriceHistory = history,
                    ListTransaction = await ScmOverviewRules.TransactionsAsync(db, db.InventoryTransaction.AsNoTracking().Where(t => t.IngredientId == id), 100),
                }, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetOverview: ", ex.ToString()));
                throw;
            }
        }
    }
}
