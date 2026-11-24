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
    public partial class IngredientService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService, IDocumentNumberService documentNumberService, AppDelicare.Data.Share.IClock clock, IUomConversionService uomConversion, Microsoft.Extensions.Options.IOptions<AppDelicare.Model.Option.BusinessRulesOptions> rules) : IIngredientService
    {
        private readonly IUomConversionService _uom = uomConversion;
        private readonly AppDelicare.Model.Option.BusinessRulesOptions _rules = rules.Value;
        /// <summary>UI-6.20: cảnh báo kèm thông báo lưu (ngừng dùng nguyên liệu còn trong công thức / chứng từ chưa xong).</summary>
        private string? _warning;
        /// <summary>UI-6.20 (D-101): 3 loại bảo quản chuẩn – AMBIENT cũ = DRY.</summary>
        public static readonly string[] StorageTypes = ["DRY", "CHILLED", "FROZEN"];
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
                    ResponseMessage = _warning != null ? (_createdCode != null ? string.Format(ConstantHelper.Message.SaveSuccessWithCode, "nguyên liệu", _createdCode) : ConstantHelper.Message.SaveSuccess) + " – " + _warning
                        : typeSave == ConstantHelper.TypeSave.Delete ? ConstantHelper.Message.DeleteSuccess
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
            var invalid = await CheckRules(request, null);
            if (!string.IsNullOrEmpty(invalid))
            {
                return invalid;
            }
            var insert = _mapper.Map<Ingredient>(request);
            // UI-6.20: cột cũ (deprecated) ghi song song.
            insert.CategoryId = insert.IngredientCategoryId;
            insert.BaseUomId = insert.BaseUnitId;
            // MA-1.3 (D-63): mã danh mục máy chủ tự sinh (…), bỏ qua giá trị gửi lên; không đổi sau khi cấp.
            insert.Code = await _documentNumberService.NextCodeAsync(DocumentKind.Ingredient);
            _createdCode = insert.Code;
            _unitOfWork.IngredientRepository.Add(insert);
            await _unitOfWork.IngredientRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Update(IngredientRequest request, string typeSave)
        {
            var exist = _unitOfWork.IngredientRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (request.Id == null || exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // T45R: luật nghiệp vụ (spec) – trước khi ghi.
            var invalid = await CheckRules(request, exist);
            if (!string.IsNullOrEmpty(invalid))
            {
                return invalid;
            }
            if (request.Status == "Inactive" && exist.Status != "Inactive")
            {
                _warning = await InUseWarningAsync(exist.Id);
            }
            // MA-1.3 (D-63): mã không đổi sau khi cấp – bỏ qua giá trị gửi lên.
            request.Code = exist.Code;
            var update = _mapper.Map(request, exist);
            update.CategoryId = update.IngredientCategoryId;
            update.BaseUomId = update.BaseUnitId;
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

        /// <summary>
        /// T45R (spec 4.4) + UI-6.20 (D-101): Tên*, Nhóm nguyên liệu*, Đơn vị kho*; Loại bảo quản* ∈ {DRY, CHILLED, FROZEN} (AMBIENT cũ = DRY);
        /// 1 công tắc hạn dùng (ghi cả ExpiryControl + IsExpiryManaged) → ShelfLifeDays &gt; 0; LotControl mặc định = có hạn dùng; QcRequired mặc định
        /// = Mát / Đông lạnh; ĐVT mua / xuất mặc định = đơn vị kho, khác thì phải quy đổi được về đơn vị kho; YieldPercent = 100 − hao hụt chuẩn;
        /// CompanyId / cột cũ (CategoryId, BaseUomId) máy chủ tự ghi.
        /// </summary>
        private async Task<string> CheckRules(IngredientRequest request, Ingredient? exist)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return string.Format(ConstantHelper.Message.NotExist, "Tên nguyên liệu").Trim();
            }
            request.Name = request.Name.Trim();
            request.IngredientCategoryId ??= exist?.IngredientCategoryId ?? exist?.CategoryId;
            if (request.IngredientCategoryId == null)
            {
                return ConstantHelper.Message.IngredientCategoryRequired;
            }
            request.BaseUnitId ??= exist?.BaseUnitId ?? exist?.BaseUomId;
            if (request.BaseUnitId == null)
            {
                return ConstantHelper.Message.IngredientBaseUnitRequired;
            }
            string storage = (request.StorageType ?? exist?.StorageType ?? string.Empty).Trim().ToUpperInvariant();
            if (storage == "AMBIENT")
            {
                storage = "DRY";
            }
            if (!StorageTypes.Contains(storage))
            {
                return ConstantHelper.Message.IngredientStorageTypeInvalid;
            }
            request.StorageType = storage;
            if (request.StandardWastePercent is { } w && (w < 0 || w >= 100))
            {
                return ConstantHelper.Message.StandardWasteRange;
            }
            if (request.StandardWastePercent is { } waste)
            {
                request.YieldPercent = 100m - waste;
            }
            else if (request.YieldPercent is { } y && (y <= 0 || y > 100))
            {
                return ConstantHelper.Message.YieldPercentRange;
            }
            bool expiry = request.IsExpiryManaged || request.ExpiryControl == true;
            if (expiry && !(request.ShelfLifeDays > 0))
            {
                return ConstantHelper.Message.ShelfLifeRequired;
            }
            request.IsExpiryManaged = expiry;
            request.ExpiryControl = expiry;
            request.LotControl ??= expiry;
            request.QcRequired ??= storage is "CHILLED" or "FROZEN";
            request.PurchaseUomId ??= request.BaseUnitId;
            request.IssueUomId ??= request.BaseUnitId;
            foreach (var (unit, label) in new[] { (request.PurchaseUomId.Value, "mua"), (request.IssueUomId.Value, "xuất") })
            {
                if (unit != request.BaseUnitId && await _uom.ConvertAsync(1m, unit, request.BaseUnitId.Value) == null)
                {
                    return string.Format(ConstantHelper.Message.IngredientUnitNotConvertible, label);
                }
            }
            request.CompanyId ??= exist?.CompanyId ?? await _unitOfWork.DbContext.Company.AsNoTracking().Where(c => !c.IsDeleted).OrderBy(c => c.CreatedDate).Select(c => (Guid?)c.Id).FirstOrDefaultAsync();
            if (request.MinimumStock is < 0 || request.SafetyStock is < 0)
            {
                return ConstantHelper.Message.IngredientStockNegative;
            }
            return string.Empty;
        }

        /// <summary>UI-6.20: nguyên liệu còn trong công thức đang dùng / nháp, đề nghị mua hoặc đơn mua chưa xong → câu cảnh báo kèm số chứng từ (không chặn).</summary>
        private async Task<string?> InUseWarningAsync(Guid id)
        {
            var db = _unitOfWork.DbContext;
            var recipes = await (from l in db.RecipeLine.AsNoTracking()
                                 join r in db.Recipe.AsNoTracking() on l.RecipeId equals r.Id
                                 join d in db.Dish.AsNoTracking() on r.DishId equals d.Id
                                 where l.IngredientId == id && !l.IsDeleted && !r.IsDeleted && (r.IsActive || r.Status == RecipeStatus.Draft || r.Status == RecipeStatus.PendingApproval)
                                 select d.Name + " " + (r.VersionCode ?? "V" + r.VersionNo)).Distinct().Take(5).ToListAsync();
            var prs = await (from l in db.PurchaseRequestLine.AsNoTracking()
                             join p in db.PurchaseRequest.AsNoTracking() on l.PurchaseRequestId equals p.Id
                             where l.IngredientId == id && !l.IsDeleted && !p.IsDeleted && p.Status != "Cancelled" && p.Status != "Rejected" && p.Status != "Converted" && p.Status != "Closed"
                             select p.RequestNumber ?? p.RequestNo).Distinct().Take(5).ToListAsync();
            var pos = await (from l in db.PurchaseOrderLine.AsNoTracking()
                             join p in db.PurchaseOrder.AsNoTracking() on l.PurchaseOrderId equals p.Id
                             where l.IngredientId == id && !l.IsDeleted && !p.IsDeleted && p.Status != "Cancelled" && p.Status != "Closed" && p.Status != "Received" && p.Status != "Completed"
                             select p.PurchaseOrderNumber ?? p.PONo).Distinct().Take(5).ToListAsync();
            var parts = new List<string>();
            if (recipes.Count > 0) parts.Add("công thức: " + string.Join(", ", recipes));
            if (prs.Count > 0) parts.Add("đề nghị mua: " + string.Join(", ", prs));
            if (pos.Count > 0) parts.Add("đơn mua: " + string.Join(", ", pos));
            return parts.Count == 0 ? null : string.Format(ConstantHelper.Message.IngredientInUseWarning, string.Join("; ", parts));
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
