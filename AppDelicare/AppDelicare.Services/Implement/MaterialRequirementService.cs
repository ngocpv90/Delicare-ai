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
using Newtonsoft.Json;
using Serilog;
using System.Globalization;
using System.Net;

namespace AppDelicare.Services.Implement
{
    public class MaterialRequirementService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService, IUomConversionService uomConversionService, IDocumentNumberService documentNumberService
        , IClock clock, ICurrentActor currentActor, IOptions<BusinessRulesOptions> rules) : IMaterialRequirementService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;
        private readonly IUomConversionService _uomConversionService = uomConversionService;
        private readonly IDocumentNumberService _documentNumberService = documentNumberService;
        private readonly IClock _clock = clock;
        private readonly ICurrentActor _currentActor = currentActor;
        private readonly BusinessRulesOptions _rules = rules.Value;

        public async Task<IResults<MaterialRequirementViewModel>> GetAll(MaterialRequirementSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@ProductionPlanId", request.ProductionPlanId);
                dynamicParameters.Add("@IngredientId", request.IngredientId);
                //dynamicParameters.Add("@RequiredQuantity", request.RequiredQuantity);
                //dynamicParameters.Add("@AvailableQuantity", request.AvailableQuantity);
                //dynamicParameters.Add("@ExpectedIncomingQuantity", request.ExpectedIncomingQuantity);
                //dynamicParameters.Add("@PurchaseQuantity", request.PurchaseQuantity);
                dynamicParameters.Add("@UnitId", request.UnitId);
                dynamicParameters.Add("@Status", request.Status);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<MaterialRequirementViewModel>("uspMaterialRequirement_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<MaterialRequirementViewModel>
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

        public IResults<MaterialRequirement> GetAllData()
        {
            try
            {
                var list = _unitOfWork.MaterialRequirementRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<MaterialRequirement>
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

        public async Task<IResult<bool>> Save(MaterialRequirementRequest request, string typeSave)
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

        /// <summary>T14: dòng nhu cầu chỉ sinh / cập nhật qua MaterialRequirement/Calculate (gắn kế hoạch, đề nghị mua) – CRUD không ghi.</summary>
        private Task<string> Insert(MaterialRequirementRequest request, string typeSave)
            => Task.FromResult(ConstantHelper.Message.MrpCrudBlocked);

        /// <summary>T14: không sửa tay số nhu cầu / tồn / số mua – tính lại bằng Calculate.</summary>
        private Task<string> Update(MaterialRequirementRequest request, string typeSave)
            => Task.FromResult(ConstantHelper.Message.MrpCrudBlocked);

        /// <summary>T14: không xóa tay – lần tính lại tự xóa mềm dòng không còn nhu cầu.</summary>
        private Task<string> Delete(Guid? id, string typeSave)
            => Task.FromResult(ConstantHelper.Message.MrpCrudBlocked);

        public async Task<IResult<MaterialRequirement>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.MaterialRequirementRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<MaterialRequirement>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<MaterialRequirement>
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

        // ============================== T14 – MRP (Pilot P5) ==============================

        /// <summary>Tóm tắt lần tính gần nhất – lưu ở ProductionPlan.Note dạng JSON (GAP-MRP-02, không có cột riêng).</summary>
        private sealed class PlanNote
        {
            public string Source { get; set; } = "MRP";
            public DateTime CalculatedAt { get; set; }
            public Guid CalculatedBy { get; set; }
            public Guid? WarehouseId { get; set; }
            public int OrderCount { get; set; }
            public decimal TotalMeals { get; set; }
            public Guid? PurchaseRequestId { get; set; }
            public string? UserNote { get; set; }
            public List<MaterialRequirementDishModel> Dishes { get; set; } = [];
            public List<string> Warnings { get; set; } = [];
        }

        /// <summary>Chi tiết 1 dòng nhu cầu không có cột (tồn an toàn, tồn bị chặn, thiếu, NCC gợi ý, dòng đề nghị mua) – MaterialRequirement.Note JSON (GAP-MRP-02).</summary>
        private sealed class RowNote
        {
            public decimal SafetyStock { get; set; }
            public decimal BlockedQuantity { get; set; }
            public decimal ShortageQuantity { get; set; }
            public bool Incomplete { get; set; }
            public string? Warning { get; set; }
            public Guid? PurchaseRequestId { get; set; }
            public Guid? PurchaseRequestLineId { get; set; }
            public MaterialRequirementSupplierModel? Supplier { get; set; }
        }

        private sealed class RecipeLineInput
        {
            public Guid IngredientId { get; set; }
            public decimal Quantity { get; set; }
            public Guid UnitId { get; set; }
            public decimal? WastePercent { get; set; }
        }

        /// <summary>Kết quả tính của 1 nguyên liệu (trước khi ghi).</summary>
        private sealed class ItemCalc
        {
            public Guid IngredientId { get; set; }
            public Guid UnitId { get; set; }
            public decimal Required { get; set; }
            public decimal Available { get; set; }
            public decimal Blocked { get; set; }
            public decimal Safety { get; set; }
            public decimal Incoming { get; set; }
            public decimal Shortage { get; set; }
            public decimal Purchase { get; set; }
            public bool Incomplete { get; set; }
            public List<string> Warnings { get; } = [];
            public MaterialRequirementSupplierModel? Supplier { get; set; }
        }

        private static string Day(DateTime date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

        private static decimal Round4(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);

        /// <summary>Làm tròn lên 4 chữ số (số mua không bao giờ thấp hơn số thiếu).</summary>
        private static decimal Ceil4(decimal value) => Math.Ceiling(value * 10000m) / 10000m;

        private static bool SameText(string? a, string? b) => string.Equals((a ?? string.Empty).Trim(), (b ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);

        private static T? ReadJson<T>(string? json) where T : class
        {
            if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith('{'))
            {
                return null;
            }
            try
            {
                return JsonConvert.DeserializeObject<T>(json);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>Quy đổi có nhớ đệm trong 1 lần tính (UomConversionService đọc toàn bộ bảng quy đổi mỗi lần gọi).</summary>
        private sealed class Converter(IUomConversionService service, DateTime on)
        {
            private readonly Dictionary<(Guid, Guid), decimal?> _factor = [];

            public async Task<decimal?> ConvertAsync(decimal quantity, Guid from, Guid to)
            {
                if (from == to)
                {
                    return quantity;
                }
                if (!_factor.TryGetValue((from, to), out var factor))
                {
                    factor = await service.ConvertAsync(1m, from, to, on);
                    _factor[(from, to)] = factor;
                }
                return factor == null ? null : quantity * factor.Value;
            }
        }

        /// <summary>Kế hoạch neo của (ngày sản xuất, chi nhánh): ProductionPlan chưa xóa, không gắn ca (GAP-MRP-01).</summary>
        private IQueryable<ProductionPlan> PlansOf(DateTime date, Guid? organizationUnitId)
        {
            DateTime next = date.AddDays(1);
            return _unitOfWork.ProductionPlanRepository.GetAll()
                .Where(p => !p.IsDeleted && p.ProductionDate >= date && p.ProductionDate < next && p.OrganizationUnitId == organizationUnitId && p.ShiftId == null)
                .OrderBy(p => p.CreatedDate);
        }

        /// <summary>
        /// T14 (spec 7.1 calculate, BUSINESS_RULES §3, Pilot P5) – bấm tay thay job 20:30, trong 1 transaction:
        /// <list type="number">
        /// <item>Đơn <b>Đã chốt</b> (Locked) có ngày ăn = ngày sản xuất × thực đơn của dòng đơn (MenuLine cùng loại suất, thứ của ngày) → món × suất.
        /// Thực đơn có nhiều lựa chọn (SequenceNo) → suất chia đều cho các lựa chọn (PROPOSED, GAP-MRP-03).</item>
        /// <item>Món × công thức: bản đã khóa hiệu lực tại ngày (SnapshotJson; thiếu thì dòng định lượng), không có thì bản Đã duyệt mới nhất (cảnh báo).
        /// Nhu cầu = định lượng × suất ÷ suất chuẩn (StandardOutputQty, mặc định BusinessRules 100) × (1 + hao hụt %), hao hụt = RecipeLine.WastePercent,
        /// trống thì Ingredient.StandardWastePercent; quy đổi về đơn vị cơ sở của nguyên liệu qua UomConversion.</item>
        /// <item>Khả dụng = Σ AvailableQuantity (đã trừ giữ chỗ) của lô không bị chặn tại ngày sản xuất (InventoryLotRules: hết hạn / QC / tạm giữ), theo kho / chi nhánh.</item>
        /// <item>Thiếu = max(0, nhu cầu + tồn an toàn (D-12, PROPOSED) − khả dụng − hàng sắp về (Pilot = 0)); số mua làm tròn lên MOQ của NCC gợi ý.</item>
        /// <item>Ghi: ProductionPlan Draft (KH-, tạo nếu chưa có) + ProductionPlanLine theo món, MaterialRequirement theo nguyên liệu (cập nhật tại chỗ, dòng thừa xóa mềm),
        /// PurchaseRequest Draft (PH-) + dòng cho phần thiếu (đề nghị nháp của lần trước được cập nhật; đề nghị đã gửi duyệt / duyệt / lập PO → không tính lại).</item>
        /// </list>
        /// </summary>
        public async Task<IResult<MaterialRequirementResultModel>> Calculate(MaterialRequirementCalculateRequest request)
        {
            if (request.ProductionDate == default)
            {
                return Result<MaterialRequirementResultModel>.Fail(ConstantHelper.Message.MrpDateRequired);
            }
            DateTime date = request.ProductionDate.Date;
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var actor = _currentActor.RequireActor();
                var db = _unitOfWork.DbContext;
                Guid? unitId = request.OrganizationUnitId is { } ou && ou != Guid.Empty ? ou : null;
                Guid? warehouseId = request.WarehouseId is { } wh && wh != Guid.Empty ? wh : null;
                if (unitId != null && !await db.OrganizationUnit.AsNoTracking().AnyAsync(o => o.Id == unitId && !o.IsDeleted))
                {
                    return Fail(ConstantHelper.Message.OrganizationUnitNotFound);
                }
                if (warehouseId != null)
                {
                    var warehouse = await db.Warehouse.AsNoTracking().FirstOrDefaultAsync(w => w.Id == warehouseId && !w.IsDeleted);
                    if (warehouse == null)
                    {
                        return Fail(ConstantHelper.Message.WarehouseNotFound);
                    }
                    if (unitId != null && warehouse.OrganizationUnitId != unitId)
                    {
                        return Fail(ConstantHelper.Message.MrpWarehouseNotInUnit);
                    }
                }

                // 1) Khóa theo ngày (sp_getapplock của mã KH- – 2 lần tính cùng ngày chạy lần lượt) rồi tìm kế hoạch neo + kết quả lần trước.
                string planNumber = await _documentNumberService.NextAsync(DocumentKind.ProductionPlan, date);
                var plan = PlansOf(date, unitId).FirstOrDefault();
                if (plan != null && plan.Status != ProductionPlanStatus.Draft)
                {
                    return Fail(string.Format(ConstantHelper.Message.MrpPlanNotDraft, plan.PlanNumber ?? plan.PlanNo, ProductionPlanStatus.Label(plan.Status)));
                }
                var oldRows = plan == null ? [] : _unitOfWork.MaterialRequirementRepository.GetAll().Where(r => r.ProductionPlanId == plan.Id && !r.IsDeleted).ToList();
                var oldPlanLines = plan == null ? [] : _unitOfWork.ProductionPlanLineRepository.GetAll().Where(l => l.ProductionPlanId == plan.Id && !l.IsDeleted).ToList();
                Guid? oldPrId = ReadJson<PlanNote>(plan?.Note)?.PurchaseRequestId
                    ?? oldRows.Select(r => ReadJson<RowNote>(r.Note)?.PurchaseRequestId).FirstOrDefault(x => x != null);
                PurchaseRequest? purchaseRequest = oldPrId == null ? null : _unitOfWork.PurchaseRequestRepository.GetSingle(p => p.Id == oldPrId && !p.IsDeleted);
                if (purchaseRequest != null && purchaseRequest.Status != PurchaseRequestStatus.Draft)
                {
                    if (purchaseRequest.Status is PurchaseRequestStatus.PendingApproval or PurchaseRequestStatus.Approved or PurchaseRequestStatus.Converted)
                    {
                        return Fail(string.Format(ConstantHelper.Message.MrpPurchaseRequestLocked, purchaseRequest.RequestNumber ?? purchaseRequest.RequestNo,
                            PurchaseRequestStatus.Label(purchaseRequest.Status)));
                    }
                    purchaseRequest = null; // Từ chối / Hủy → lần này lập đề nghị mới, đề nghị cũ giữ nguyên.
                }

                // 2) Đơn đã chốt × thực đơn → món × suất.
                var warnings = new List<string>();
                DateTime next = date.AddDays(1);
                var orders = await db.Order.AsNoTracking().Where(o => !o.IsDeleted && o.MealDate >= date && o.MealDate < next).ToListAsync();
                var lockedOrders = orders.Where(o => o.Status == OrderStatus.Locked).ToList();
                if (lockedOrders.Count == 0)
                {
                    return Fail(string.Format(ConstantHelper.Message.MrpNoLockedOrder, Day(date)));
                }
                int notLocked = orders.Count(o => o.Status is OrderStatus.Draft or OrderStatus.Submitted or OrderStatus.Confirmed);
                if (notLocked > 0)
                {
                    warnings.Add(string.Format(ConstantHelper.Message.MrpOrdersNotLocked, notLocked, Day(date)));
                }
                var orderIds = lockedOrders.Select(o => o.Id).ToList();
                var orderLines = await db.OrderLine.AsNoTracking().Where(l => orderIds.Contains(l.OrderId) && !l.IsDeleted && l.Quantity > 0).ToListAsync();
                var menuIds = orderLines.Where(l => l.MenuId != null).Select(l => l.MenuId!.Value).Distinct().ToList();
                int dayOfWeek = MenuService.MenuDayOf(date);
                var menuLines = await db.MenuLine.AsNoTracking().Where(m => menuIds.Contains(m.MenuId) && !m.IsDeleted && m.DayOfWeek == dayOfWeek).ToListAsync();
                var menuNames = await db.Menu.AsNoTracking().Where(m => menuIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.Code ?? m.Name);
                var orderNo = lockedOrders.ToDictionary(o => o.Id, o => o.OrderNumber ?? o.OrderNo ?? o.Id.ToString("N")[..8]);
                var portions = new Dictionary<Guid, decimal>();
                var firstOrderLine = new Dictionary<Guid, Guid>();
                foreach (var line in orderLines.OrderBy(l => orderNo[l.OrderId]).ThenBy(l => l.MealType))
                {
                    if (line.MenuId == null)
                    {
                        warnings.Add(string.Format(ConstantHelper.Message.MrpOrderLineNoMenu, orderNo[line.OrderId], line.MealType, Round4(line.Quantity)));
                        continue;
                    }
                    var options = menuLines.Where(m => m.MenuId == line.MenuId && SameText(m.MealType, line.MealType))
                        .GroupBy(m => m.SequenceNo is > 0 ? m.SequenceNo.Value : 1).ToList();
                    if (options.Count == 0)
                    {
                        warnings.Add(string.Format(ConstantHelper.Message.MrpMenuNoDish, menuNames.GetValueOrDefault(line.MenuId.Value), line.MealType, dayOfWeek == 7 ? "CN" : (dayOfWeek + 1).ToString(CultureInfo.InvariantCulture),
                            Round4(line.Quantity), orderNo[line.OrderId]));
                        continue;
                    }
                    // PROPOSED (GAP-MRP-03): chưa có số suất theo lựa chọn → chia đều số suất của dòng đơn cho các lựa chọn.
                    decimal perOption = line.Quantity / options.Count;
                    foreach (var option in options)
                    {
                        foreach (var dish in option)
                        {
                            portions[dish.DishId] = portions.GetValueOrDefault(dish.DishId) + perOption;
                            firstOrderLine.TryAdd(dish.DishId, line.Id);
                        }
                    }
                }

                // 3) Món × công thức → nhu cầu theo nguyên liệu (đơn vị cơ sở).
                var dishIds = portions.Keys.ToList();
                var dishes = await db.Dish.AsNoTracking().Where(d => dishIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id);
                var recipes = await db.Recipe.AsNoTracking()
                    .Where(r => dishIds.Contains(r.DishId) && !r.IsDeleted
                        && (r.Status == RecipeStatus.Locked || r.Status == RecipeStatus.Obsolete || r.Status == RecipeStatus.Approved))
                    .ToListAsync();
                var dishModels = new List<MaterialRequirementDishModel>();
                var inputs = new List<(Guid DishId, decimal Portions, decimal Servings, RecipeLineInput Line)>();
                foreach (var (dishId, qty) in portions.OrderBy(p => dishes.GetValueOrDefault(p.Key)?.Name))
                {
                    var dish = dishes.GetValueOrDefault(dishId);
                    var model = new MaterialRequirementDishModel { DishId = dishId, DishCode = dish?.Code, DishName = dish?.Name, Portions = Round4(qty) };
                    dishModels.Add(model);
                    var mine = recipes.Where(r => r.DishId == dishId).ToList();
                    var recipe = mine.Where(r => r.Status is RecipeStatus.Locked or RecipeStatus.Obsolete && r.LockedAt != null
                                && (r.EffectiveFrom == null || r.EffectiveFrom.Value.Date <= date) && (r.EffectiveTo == null || r.EffectiveTo.Value.Date >= date))
                            .OrderByDescending(r => r.Status == RecipeStatus.Locked).ThenByDescending(r => r.EffectiveFrom).ThenByDescending(r => r.VersionNo).FirstOrDefault()
                        ?? mine.Where(r => r.Status == RecipeStatus.Locked && r.IsActive).OrderByDescending(r => r.VersionNo).FirstOrDefault();
                    bool lockedRecipe = recipe != null;
                    recipe ??= mine.Where(r => r.Status == RecipeStatus.Approved).OrderByDescending(r => r.VersionNo).FirstOrDefault();
                    if (recipe == null)
                    {
                        model.Warning = ConstantHelper.Message.MrpDishNoRecipe;
                        warnings.Add($"Cảnh báo: món {dish?.Name ?? dishId.ToString()} – {ConstantHelper.Message.MrpDishNoRecipe}");
                        continue;
                    }
                    model.RecipeId = recipe.Id;
                    model.RecipeVersion = recipe.VersionCode ?? "v" + recipe.VersionNo.ToString(CultureInfo.InvariantCulture);
                    List<RecipeLineInput>? lines = null;
                    if (lockedRecipe && !string.IsNullOrWhiteSpace(recipe.SnapshotJson))
                    {
                        try
                        {
                            lines = JsonConvert.DeserializeObject<List<RecipeLineInput>>(recipe.SnapshotJson);
                        }
                        catch (JsonException)
                        {
                            lines = null;
                        }
                    }
                    if (lines is { Count: > 0 })
                    {
                        model.RecipeSource = "Snapshot";
                    }
                    else
                    {
                        model.RecipeSource = "Lines";
                        lines = await db.RecipeLine.AsNoTracking().Where(l => l.RecipeId == recipe.Id && !l.IsDeleted)
                            .Select(l => new RecipeLineInput { IngredientId = l.IngredientId, Quantity = l.Quantity, UnitId = l.UnitId != Guid.Empty ? l.UnitId : l.UomId, WastePercent = l.WastePercent })
                            .ToListAsync();
                    }
                    if (!lockedRecipe)
                    {
                        model.Warning = ConstantHelper.Message.MrpRecipeNotLocked;
                        warnings.Add($"Cảnh báo: món {dish?.Name} – {ConstantHelper.Message.MrpRecipeNotLocked}");
                    }
                    decimal servings = recipe.StandardOutputQty is > 0 ? recipe.StandardOutputQty.Value : _rules.RecipeStandardServings;
                    inputs.AddRange(lines.Where(l => l.Quantity > 0).Select(l => (dishId, qty, servings, l)));
                }

                var ingredientIds = inputs.Select(i => i.Line.IngredientId).Distinct().ToList();
                var ingredients = await db.Ingredient.AsNoTracking().Where(i => ingredientIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id);
                var unitNames = await db.UnitMeasure.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.Name ?? u.Code);
                var converter = new Converter(_uomConversionService, date);
                var items = new Dictionary<Guid, ItemCalc>();
                foreach (var (dishId, qty, servings, line) in inputs)
                {
                    var ingredient = ingredients.GetValueOrDefault(line.IngredientId);
                    if (!items.TryGetValue(line.IngredientId, out var item))
                    {
                        Guid baseUnit = ingredient?.BaseUnitId is { } b && b != Guid.Empty ? b : ingredient?.BaseUomId is { } o && o != Guid.Empty ? o : line.UnitId;
                        item = new ItemCalc { IngredientId = line.IngredientId, UnitId = baseUnit };
                        items[line.IngredientId] = item;
                    }
                    decimal waste = line.WastePercent ?? ingredient?.StandardWastePercent ?? 0m;
                    decimal gross = line.Quantity * qty / servings * (1 + waste / 100m);
                    decimal? converted = await converter.ConvertAsync(gross, line.UnitId, item.UnitId);
                    if (converted == null)
                    {
                        item.Incomplete = true;
                        string message = string.Format(ConstantHelper.Message.MrpUnitMissing, ingredient?.Name, unitNames.GetValueOrDefault(line.UnitId), unitNames.GetValueOrDefault(item.UnitId));
                        if (!warnings.Contains(message))
                        {
                            warnings.Add(message);
                            item.Warnings.Add(message);
                        }
                        continue;
                    }
                    item.Required += converted.Value;
                }

                // 4) Tồn xuất được tại ngày sản xuất (lô hết hạn trước ngày SX không tính), tồn an toàn, NCC gợi ý.
                var warehouseIds = warehouseId != null ? [warehouseId.Value]
                    : unitId != null ? await db.Warehouse.AsNoTracking().Where(w => w.OrganizationUnitId == unitId && !w.IsDeleted).Select(w => w.Id).ToListAsync()
                    : null;
                DateTime stockDay = date > _clock.Today ? date : _clock.Today;
                var stockRows = (await InventoryLotRules.LoadAsync(db, stockDay, _rules.ExpiringLotDays, warehouseId))
                    .Where(r => items.ContainsKey(r.IngredientId) && (warehouseIds == null || warehouseIds.Contains(r.WarehouseId))).ToList();
                foreach (var row in stockRows)
                {
                    var item = items[row.IngredientId];
                    bool usable = row.BlockReason == null;
                    decimal quantity = usable ? Math.Max(row.AvailableQuantity, 0) : Math.Max(row.OnHandQuantity, 0);
                    if (quantity == 0)
                    {
                        continue;
                    }
                    decimal? converted = await converter.ConvertAsync(quantity, row.UnitId, item.UnitId);
                    if (converted == null)
                    {
                        string message = string.Format(ConstantHelper.Message.MrpUnitMissing, row.IngredientName, row.UnitName, unitNames.GetValueOrDefault(item.UnitId));
                        if (!warnings.Contains(message))
                        {
                            warnings.Add(message);
                            item.Warnings.Add(message);
                        }
                        continue;
                    }
                    if (usable)
                    {
                        item.Available += converted.Value;
                    }
                    else
                    {
                        item.Blocked += converted.Value;
                    }
                }
                if (_rules.MrpIncludeSafetyStock)
                {
                    var policies = (await db.InventoryPolicy.AsNoTracking()
                            .Where(p => !p.IsDeleted && ingredientIds.Contains(p.IngredientId) && p.SafetyStock != null)
                            .OrderByDescending(p => p.CreatedDate).ToListAsync())
                        .Where(p => (warehouseIds == null || warehouseIds.Contains(p.WarehouseId)) && !SameText(p.Status, "Inactive"))
                        .GroupBy(p => (p.WarehouseId, p.IngredientId)).Select(g => g.First()).ToList();
                    foreach (var item in items.Values)
                    {
                        var mine = policies.Where(p => p.IngredientId == item.IngredientId).ToList();
                        item.Safety = mine.Count != 0 ? mine.Sum(p => p.SafetyStock ?? 0m) : ingredients.GetValueOrDefault(item.IngredientId)?.SafetyStock ?? 0m;
                    }
                }
                var supplierRows = (await db.SupplierMaterial.AsNoTracking()
                        .Where(m => !m.IsDeleted && ingredientIds.Contains(m.IngredientId)
                            && (m.EffectiveFrom == null || m.EffectiveFrom <= date) && (m.EffectiveTo == null || m.EffectiveTo >= date))
                        .ToListAsync())
                    .Where(m => !SameText(m.Status, "Inactive")).ToList();
                var supplierIds = supplierRows.Select(m => m.SupplierId).Distinct().ToList();
                var suppliers = (await db.Supplier.AsNoTracking().Where(s => supplierIds.Contains(s.Id) && !s.IsDeleted).ToListAsync())
                    .Where(s => !SameText(s.Status, "Inactive")).ToDictionary(s => s.Id);
                foreach (var item in items.Values)
                {
                    item.Required = Round4(item.Required);
                    item.Available = Round4(item.Available);
                    item.Blocked = Round4(item.Blocked);
                    item.Shortage = Round4(Math.Max(0, item.Required + item.Safety - item.Available - item.Incoming));
                    var best = supplierRows.Where(m => m.IngredientId == item.IngredientId && suppliers.ContainsKey(m.SupplierId))
                        .OrderByDescending(m => m.IsPreferred).ThenBy(m => m.UnitPrice == null).ThenBy(m => m.UnitPrice).ThenBy(m => m.LeadTimeDays ?? int.MaxValue)
                        .FirstOrDefault();
                    decimal purchase = item.Shortage;
                    if (best != null)
                    {
                        var supplier = suppliers[best.SupplierId];
                        Guid purchaseUnit = best.PurchaseUomId is { } pu && pu != Guid.Empty ? pu : item.UnitId;
                        decimal? moq = best.MinimumOrderQuantity ?? best.MinimumOrderQty;
                        if (purchase > 0 && moq is > 0)
                        {
                            decimal? moqBase = await converter.ConvertAsync(moq.Value, purchaseUnit, item.UnitId);
                            if (moqBase != null && purchase < moqBase.Value)
                            {
                                purchase = moqBase.Value; // PROPOSED: số mua tối thiểu = MOQ (chưa có cột quy cách đóng gói – GAP-MRP-04).
                            }
                        }
                        purchase = Ceil4(purchase);
                        decimal? inPurchaseUnit = purchase > 0 ? await converter.ConvertAsync(purchase, item.UnitId, purchaseUnit) : 0m;
                        item.Supplier = new MaterialRequirementSupplierModel
                        {
                            SupplierMaterialId = best.Id,
                            SupplierId = best.SupplierId,
                            SupplierCode = supplier.Code,
                            SupplierName = supplier.Name,
                            IsPreferred = best.IsPreferred,
                            UnitPrice = best.UnitPrice,
                            PurchaseUnitId = purchaseUnit,
                            PurchaseUnitName = unitNames.GetValueOrDefault(purchaseUnit),
                            MinimumOrderQuantity = moq,
                            LeadTimeDays = best.LeadTimeDays,
                            PurchaseQuantityInPurchaseUnit = inPurchaseUnit == null ? null : Round4(inPurchaseUnit.Value),
                            EstimatedAmount = inPurchaseUnit != null && best.UnitPrice != null ? Math.Round(inPurchaseUnit.Value * best.UnitPrice.Value, 2) : null,
                        };
                        if (purchase > 0 && best.LeadTimeDays is > 0 && _clock.Today.AddDays(best.LeadTimeDays.Value) > date)
                        {
                            string message = string.Format(ConstantHelper.Message.MrpLeadTimeWarning, supplier.Name, best.LeadTimeDays, Day(date));
                            item.Warnings.Add(message);
                            warnings.Add($"{message} ({ingredients.GetValueOrDefault(item.IngredientId)?.Name})");
                        }
                    }
                    else if (purchase > 0)
                    {
                        item.Warnings.Add(ConstantHelper.Message.MrpNoSupplier);
                    }
                    item.Purchase = Ceil4(purchase);
                }

                // 5) Ghi trong cùng transaction. Xóa mềm trước (CommitAsync Delete đánh dấu mọi bản ghi đang sửa) rồi thêm / cập nhật.
                bool needPurchase = items.Values.Any(i => i.Purchase > 0);
                var oldPrLines = purchaseRequest == null ? [] : _unitOfWork.PurchaseRequestLineRepository.GetAll()
                    .Where(l => l.PurchaseRequestId == purchaseRequest.Id && !l.IsDeleted).ToList();
                var toDelete = new List<BaseEntity>();
                toDelete.AddRange(oldRows.Where(r => !items.ContainsKey(r.IngredientId)));
                toDelete.AddRange(oldPlanLines.Where(l => !portions.ContainsKey(l.DishId)));
                toDelete.AddRange(oldPrLines.Where(l => !needPurchase || !items.TryGetValue(l.IngredientId, out var it) || it.Purchase <= 0));
                if (purchaseRequest != null && !needPurchase)
                {
                    toDelete.Add(purchaseRequest);
                }
                if (toDelete.Count != 0)
                {
                    foreach (var entity in toDelete)
                    {
                        db.Update(entity);
                    }
                    await _unitOfWork.MaterialRequirementRepository.CommitAsync(ConstantHelper.TypeSave.Delete);
                }

                DateTime now = _clock.Now;
                if (plan == null)
                {
                    plan = new ProductionPlan
                    {
                        Id = Guid.NewGuid(),
                        PlanNumber = planNumber,
                        PlanNo = planNumber,
                        ProductionDate = date,
                        PlanDate = now,
                        OrganizationUnitId = unitId,
                        Status = ProductionPlanStatus.Draft,
                    };
                    _unitOfWork.ProductionPlanRepository.Add(plan);
                }
                foreach (var (dishId, qty) in portions)
                {
                    var line = oldPlanLines.FirstOrDefault(l => l.DishId == dishId);
                    if (line == null)
                    {
                        _unitOfWork.ProductionPlanLineRepository.Add(new ProductionPlanLine
                        {
                            Id = Guid.NewGuid(),
                            ProductionPlanId = plan.Id,
                            DishId = dishId,
                            OrderLineId = firstOrderLine.GetValueOrDefault(dishId),
                            PlannedQuantity = Round4(qty),
                            PlannedQty = Round4(qty),
                            Status = ProductionPlanStatus.Draft,
                        });
                    }
                    else
                    {
                        line.PlannedQuantity = Round4(qty);
                        line.PlannedQty = Round4(qty);
                        line.OrderLineId = firstOrderLine.GetValueOrDefault(dishId);
                        _unitOfWork.ProductionPlanLineRepository.Update(line);
                    }
                }

                if (needPurchase && purchaseRequest == null)
                {
                    string number = await _documentNumberService.NextAsync(DocumentKind.Voucher, _clock.Today);
                    purchaseRequest = new PurchaseRequest
                    {
                        Id = Guid.NewGuid(),
                        RequestNumber = number,
                        RequestNo = number,
                        RequestDate = now,
                        RequiredDate = date,
                        Status = PurchaseRequestStatus.Draft,
                    };
                    _unitOfWork.PurchaseRequestRepository.Add(purchaseRequest);
                }
                else if (needPurchase)
                {
                    purchaseRequest!.RequiredDate = date;
                    _unitOfWork.PurchaseRequestRepository.Update(purchaseRequest);
                }
                if (needPurchase)
                {
                    purchaseRequest!.Note = $"Tự động từ tính nhu cầu NL ngày {Day(date)} – kế hoạch {plan.PlanNumber ?? plan.PlanNo}";
                }

                foreach (var item in items.Values)
                {
                    PurchaseRequestLine? prLine = null;
                    if (item.Purchase > 0)
                    {
                        prLine = oldPrLines.FirstOrDefault(l => l.IngredientId == item.IngredientId && !toDelete.Contains(l));
                        string lineNote = item.Supplier == null ? ConstantHelper.Message.MrpNoSupplier
                            : $"NCC gợi ý: {item.Supplier.SupplierName}"
                              + (item.Supplier.UnitPrice != null ? $" – giá {item.Supplier.UnitPrice.Value.ToString("0.##", CultureInfo.InvariantCulture)}/{item.Supplier.PurchaseUnitName}" : string.Empty)
                              + (item.Supplier.MinimumOrderQuantity != null ? $" – MOQ {item.Supplier.MinimumOrderQuantity.Value.ToString("0.####", CultureInfo.InvariantCulture)}" : string.Empty);
                        if (prLine == null)
                        {
                            prLine = new PurchaseRequestLine { Id = Guid.NewGuid(), PurchaseRequestId = purchaseRequest!.Id, IngredientId = item.IngredientId };
                            _unitOfWork.PurchaseRequestLineRepository.Add(prLine);
                        }
                        else
                        {
                            _unitOfWork.PurchaseRequestLineRepository.Update(prLine);
                        }
                        prLine.Quantity = item.Purchase;
                        prLine.UnitId = item.UnitId;
                        prLine.UomId = item.UnitId;
                        prLine.RequiredDate = date;
                        prLine.Note = lineNote;
                    }
                    var note = new RowNote
                    {
                        SafetyStock = item.Safety,
                        BlockedQuantity = item.Blocked,
                        ShortageQuantity = item.Shortage,
                        Incomplete = item.Incomplete,
                        Warning = item.Warnings.Count == 0 ? null : string.Join(" | ", item.Warnings),
                        PurchaseRequestId = prLine != null ? purchaseRequest!.Id : null,
                        PurchaseRequestLineId = prLine?.Id,
                        Supplier = item.Supplier,
                    };
                    var row = oldRows.FirstOrDefault(r => r.IngredientId == item.IngredientId);
                    if (row == null)
                    {
                        row = new MaterialRequirement { Id = Guid.NewGuid(), ProductionPlanId = plan.Id, IngredientId = item.IngredientId };
                        _unitOfWork.MaterialRequirementRepository.Add(row);
                    }
                    else
                    {
                        _unitOfWork.MaterialRequirementRepository.Update(row);
                    }
                    row.RequirementNo = plan.PlanNumber ?? plan.PlanNo;
                    row.UnitId = item.UnitId;
                    row.RequiredQuantity = item.Required;
                    row.RequiredQty = item.Required;
                    row.AvailableQuantity = item.Available;
                    row.AvailableQty = item.Available;
                    row.ExpectedIncomingQuantity = item.Incoming;
                    row.ExpectedIncomingQty = item.Incoming;
                    row.PurchaseQuantity = item.Purchase;
                    row.PurchaseQty = item.Purchase;
                    row.Status = item.Purchase > 0 ? (prLine != null ? MaterialRequirementStatus.Requested : MaterialRequirementStatus.Shortage) : MaterialRequirementStatus.Sufficient;
                    row.Note = JsonConvert.SerializeObject(note);
                }

                plan.Note = JsonConvert.SerializeObject(new PlanNote
                {
                    CalculatedAt = now,
                    CalculatedBy = actor.UserId,
                    WarehouseId = warehouseId,
                    OrderCount = lockedOrders.Count,
                    TotalMeals = Round4(orderLines.Sum(l => l.Quantity)),
                    PurchaseRequestId = needPurchase ? purchaseRequest!.Id : null,
                    UserNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
                    Dishes = dishModels,
                    Warnings = warnings,
                });
                if (_unitOfWork.DbContext.Entry(plan).State != EntityState.Added)
                {
                    _unitOfWork.ProductionPlanRepository.Update(plan);
                }
                await _unitOfWork.MaterialRequirementRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                var detail = await DetailAsync(plan.Id);
                return Result<MaterialRequirementResultModel>.Success(detail, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Calculate: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }

            IResult<MaterialRequirementResultModel> Fail(string message)
            {
                _unitOfWork.RollbackTransaction();
                return Result<MaterialRequirementResultModel>.Fail(message);
            }
        }

        /// <summary>T14: kết quả đã lưu của ngày sản xuất (+ chi nhánh). Chưa tính → ResponseCode 200, Data = null kèm thông báo.</summary>
        public async Task<IResult<MaterialRequirementResultModel>> GetByDate(MaterialRequirementByDateRequest request)
        {
            try
            {
                if (request.ProductionDate == default)
                {
                    return Result<MaterialRequirementResultModel>.Fail(ConstantHelper.Message.MrpDateRequired);
                }
                DateTime date = request.ProductionDate.Date;
                Guid? unitId = request.OrganizationUnitId is { } ou && ou != Guid.Empty ? ou : null;
                Guid? planId = await PlansOf(date, unitId).AsNoTracking().Select(p => (Guid?)p.Id).FirstOrDefaultAsync();
                if (planId == null)
                {
                    return Result<MaterialRequirementResultModel>.Success(null, string.Format(ConstantHelper.Message.MrpNotCalculated, Day(date)));
                }
                return Result<MaterialRequirementResultModel>.Success(await DetailAsync(planId.Value), ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetByDate: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>T14: kết quả đã lưu theo kế hoạch sản xuất.</summary>
        public async Task<IResult<MaterialRequirementResultModel>> GetDetail(Guid productionPlanId)
        {
            try
            {
                var detail = await DetailAsync(productionPlanId);
                return detail == null ? Result<MaterialRequirementResultModel>.Fail(ConstantHelper.Message.RecordNotFoundMessage)
                    : Result<MaterialRequirementResultModel>.Success(detail, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetDetail: ", ex.ToString()));
                throw;
            }
        }

        private async Task<MaterialRequirementResultModel?> DetailAsync(Guid planId)
        {
            var db = _unitOfWork.DbContext;
            var plan = await db.ProductionPlan.AsNoTracking().FirstOrDefaultAsync(p => p.Id == planId && !p.IsDeleted);
            if (plan == null)
            {
                return null;
            }
            var summary = ReadJson<PlanNote>(plan.Note);
            var rows = await db.MaterialRequirement.AsNoTracking().Where(r => r.ProductionPlanId == planId && !r.IsDeleted).ToListAsync();
            var ingredientIds = rows.Select(r => r.IngredientId).Distinct().ToList();
            var ingredients = await db.Ingredient.AsNoTracking().Where(i => ingredientIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id);
            var unitIds = rows.Select(r => r.UnitId).Distinct().ToList();
            var units = await db.UnitMeasure.AsNoTracking().Where(u => unitIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name ?? u.Code);
            var result = new MaterialRequirementResultModel
            {
                ProductionPlanId = plan.Id,
                PlanNumber = plan.PlanNumber ?? plan.PlanNo,
                ProductionDate = plan.ProductionDate,
                OrganizationUnitId = plan.OrganizationUnitId,
                OrganizationUnitName = plan.OrganizationUnitId == null ? null
                    : await db.OrganizationUnit.AsNoTracking().Where(o => o.Id == plan.OrganizationUnitId).Select(o => o.Name).FirstOrDefaultAsync(),
                PlanStatus = plan.Status,
                PlanStatusName = ProductionPlanStatus.Label(plan.Status),
                WarehouseId = summary?.WarehouseId,
                WarehouseName = summary?.WarehouseId == null ? null
                    : await db.Warehouse.AsNoTracking().Where(w => w.Id == summary.WarehouseId).Select(w => w.Name).FirstOrDefaultAsync(),
                CalculatedAt = summary?.CalculatedAt,
                CalculatedBy = summary?.CalculatedBy,
                OrderCount = summary?.OrderCount ?? 0,
                TotalMeals = summary?.TotalMeals ?? 0,
                ListWarning = summary?.Warnings ?? [],
            };
            if (summary?.Dishes is { Count: > 0 } dishes)
            {
                result.ListDish = dishes;
            }
            else
            {
                var planLines = await db.ProductionPlanLine.AsNoTracking().Where(l => l.ProductionPlanId == planId && !l.IsDeleted).ToListAsync();
                var dishIds = planLines.Select(l => l.DishId).ToList();
                var dishNames = await db.Dish.AsNoTracking().Where(d => dishIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id);
                result.ListDish = [.. planLines.Select(l => new MaterialRequirementDishModel
                {
                    DishId = l.DishId, DishCode = dishNames.GetValueOrDefault(l.DishId)?.Code, DishName = dishNames.GetValueOrDefault(l.DishId)?.Name, Portions = l.PlannedQuantity,
                }).OrderBy(d => d.DishName)];
            }
            result.DishCount = result.ListDish.Count;
            int stt = 0;
            foreach (var row in rows.OrderByDescending(r => r.PurchaseQuantity > 0).ThenBy(r => ingredients.GetValueOrDefault(r.IngredientId)?.Name))
            {
                var note = ReadJson<RowNote>(row.Note) ?? new RowNote();
                var ingredient = ingredients.GetValueOrDefault(row.IngredientId);
                result.ListItem.Add(new MaterialRequirementItemModel
                {
                    STT = ++stt,
                    Id = row.Id,
                    IngredientId = row.IngredientId,
                    IngredientCode = ingredient?.Code,
                    IngredientName = ingredient?.Name,
                    UnitId = row.UnitId,
                    UnitName = units.GetValueOrDefault(row.UnitId),
                    RequiredQuantity = row.RequiredQuantity,
                    AvailableQuantity = row.AvailableQuantity,
                    BlockedQuantity = note.BlockedQuantity,
                    SafetyStock = note.SafetyStock,
                    ExpectedIncomingQuantity = row.ExpectedIncomingQuantity,
                    ShortageQuantity = note.ShortageQuantity,
                    PurchaseQuantity = row.PurchaseQuantity,
                    CoveragePercent = row.RequiredQuantity <= 0 ? 100 : Math.Round(Math.Min(100m, row.AvailableQuantity / row.RequiredQuantity * 100m), 2),
                    Status = row.Status,
                    StatusName = MaterialRequirementStatus.Label(row.Status),
                    PurchaseRequestLineId = note.PurchaseRequestLineId,
                    Supplier = note.Supplier,
                    Incomplete = note.Incomplete,
                    Warning = note.Warning,
                });
            }
            result.ShortageCount = result.ListItem.Count(i => i.PurchaseQuantity > 0);
            result.EstimatedAmount = result.ListItem.Where(i => i.PurchaseQuantity > 0).Sum(i => i.Supplier?.EstimatedAmount ?? 0m);
            Guid? prId = summary?.PurchaseRequestId ?? rows.Select(r => ReadJson<RowNote>(r.Note)?.PurchaseRequestId).FirstOrDefault(x => x != null);
            if (prId != null)
            {
                var pr = await db.PurchaseRequest.AsNoTracking().FirstOrDefaultAsync(p => p.Id == prId && !p.IsDeleted);
                if (pr != null)
                {
                    result.PurchaseRequest = new MaterialRequirementPurchaseRequestModel
                    {
                        Id = pr.Id,
                        RequestNumber = pr.RequestNumber ?? pr.RequestNo,
                        RequestDate = pr.RequestDate,
                        RequiredDate = pr.RequiredDate,
                        Status = pr.Status,
                        StatusName = PurchaseRequestStatus.Label(pr.Status),
                        LineCount = await db.PurchaseRequestLine.AsNoTracking().CountAsync(l => l.PurchaseRequestId == pr.Id && !l.IsDeleted),
                        EstimatedAmount = result.EstimatedAmount,
                    };
                }
            }
            return result;
        }
    }
}
