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
    public partial class MaterialRequirementService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
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
                dynamicParameters.Add("@IngredientId", request.IngredientId == Guid.Empty ? null : request.IngredientId);
                //dynamicParameters.Add("@RequiredQuantity", request.RequiredQuantity);
                //dynamicParameters.Add("@AvailableQuantity", request.AvailableQuantity);
                //dynamicParameters.Add("@ExpectedIncomingQuantity", request.ExpectedIncomingQuantity);
                //dynamicParameters.Add("@PurchaseQuantity", request.PurchaseQuantity);
                dynamicParameters.Add("@UnitId", request.UnitId == Guid.Empty ? null : request.UnitId);
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
            /// <summary>T14.1: đề nghị mua bổ sung (CalculateTopUp) của kế hoạch, cũ → mới (không có cột liên kết đề nghị mua ↔ kế hoạch – GAP-MRP-02).</summary>
            public List<Guid> TopUpPurchaseRequestIds { get; set; } = [];
            public DateTime? TopUpAt { get; set; }
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
            /// <summary>T14.1: số đã đề nghị còn chờ về (đơn vị cơ sở) lúc tính bổ sung gần nhất.</summary>
            public decimal? RequestedQuantity { get; set; }
            public Guid? TopUpPurchaseRequestId { get; set; }
            public Guid? TopUpPurchaseRequestLineId { get; set; }
            /// <summary>T14.1: NCC gợi ý + tiền ước tính của dòng đề nghị mua bổ sung.</summary>
            public MaterialRequirementSupplierModel? TopUpSupplier { get; set; }
        }

        private sealed class RecipeLineInput
        {
            public Guid IngredientId { get; set; }
            public decimal Quantity { get; set; }
            public Guid UnitId { get; set; }
            public decimal? WastePercent { get; set; }
            /// <summary>BIZ-1.1: % hao hụt chế biến (RecipeLine.ExpectedWasteRate).</summary>
            public decimal? ExpectedWasteRate { get; set; }
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
            /// <summary>Bảng giá NCC gợi ý (ưu tiên → giá thấp → giao nhanh) và MOQ quy về đơn vị cơ sở.</summary>
            public SupplierMaterial? Best { get; set; }
            public Supplier? BestSupplier { get; set; }
            public Guid PurchaseUnit { get; set; }
            public decimal? MoqBase { get; set; }
        }

        /// <summary>Kết quả tính chung của Calculate / CalculateTopUp (chưa ghi, chưa tính số mua).</summary>
        private sealed class Computation(Converter converter)
        {
            public Converter Converter { get; } = converter;
            public List<Order> LockedOrders { get; set; } = [];
            public List<OrderLine> OrderLines { get; set; } = [];
            public Dictionary<Guid, decimal> Portions { get; } = [];
            public Dictionary<Guid, Guid> FirstOrderLine { get; } = [];
            public List<MaterialRequirementDishModel> Dishes { get; } = [];
            public Dictionary<Guid, ItemCalc> Items { get; } = [];
            public Dictionary<Guid, Ingredient> Ingredients { get; set; } = [];
            public Dictionary<Guid, string?> UnitNames { get; set; } = [];
            public List<string> Warnings { get; } = [];
        }

        private static string Day(DateTime date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

        private static string Num(decimal value) => value.ToString("0.####", CultureInfo.InvariantCulture);

        private static decimal Round4(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);

        /// <summary>Làm tròn lên 4 chữ số (số mua không bao giờ thấp hơn số thiếu).</summary>
        private static decimal Ceil4(decimal value) => Math.Ceiling(value * 10000m) / 10000m;

        private static bool SameText(string? a, string? b) => string.Equals((a ?? string.Empty).Trim(), (b ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);

        private static Guid UnitOf(Guid unitId, Guid uomId) => unitId != Guid.Empty ? unitId : uomId;

        private static bool IsActivePurchaseRequest(string? status) => status != PurchaseRequestStatus.Cancelled && status != PurchaseRequestStatus.Rejected;

        private static bool IsMovedOn(string? status)
            => status is PurchaseRequestStatus.PendingApproval or PurchaseRequestStatus.Approved or PurchaseRequestStatus.Converted;

        /// <summary>
        /// MA-1.7 (D-63, BUSINESS_RULES §11.3): số lần tính nhu cầu <c>MR-YYMMDD-NNN</c> – cấp 1 lần cho kế hoạch ở lần tính đầu, các lần tính lại giữ số đó.
        /// Dòng tính trước D-63 (RequirementNo = số kế hoạch) được gán số MR- ở lần tính sau.
        /// </summary>
        private async Task<string> RequirementNumberAsync(IEnumerable<MaterialRequirement> rows)
            => rows.Select(r => r.RequirementNo).FirstOrDefault(n => n != null && n.StartsWith("MR-", StringComparison.Ordinal))
                ?? await _documentNumberService.NextAsync(DocumentKind.MaterialRequirement, _clock.Today);

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
            // UI-6.23 (D-104): một kế hoạch / ngày – chi nhánh chỉ giới hạn kho khi tính tồn, không tách kế hoạch (tham số giữ cho tương thích).
            _ = organizationUnitId;
            return _unitOfWork.ProductionPlanRepository.GetAll()
                .Where(p => !p.IsDeleted && p.ProductionDate >= date && p.ProductionDate < next && p.OrganizationUnitId == null && p.ShiftId == null)
                .OrderBy(p => p.CreatedDate);
        }

        /// <summary>Kiểm tra chi nhánh / kho của lần tính. Trả thông báo lỗi hoặc null.</summary>
        private async Task<string?> ValidateScopeAsync(Guid? unitId, Guid? warehouseId)
        {
            var db = _unitOfWork.DbContext;
            if (unitId != null && !await db.OrganizationUnit.AsNoTracking().AnyAsync(o => o.Id == unitId && !o.IsDeleted))
            {
                return ConstantHelper.Message.OrganizationUnitNotFound;
            }
            if (warehouseId != null)
            {
                var warehouse = await db.Warehouse.AsNoTracking().FirstOrDefaultAsync(w => w.Id == warehouseId && !w.IsDeleted);
                if (warehouse == null)
                {
                    return ConstantHelper.Message.WarehouseNotFound;
                }
                if (unitId != null && warehouse.OrganizationUnitId != unitId)
                {
                    return ConstantHelper.Message.MrpWarehouseNotInUnit;
                }
            }
            return null;
        }

        /// <summary>
        /// Bước tính chung (không ghi): đơn Đã chốt × thực đơn → món × suất (chia đều lựa chọn + dự phòng), món × công thức → nhu cầu theo nguyên liệu,
        /// tồn xuất được / bị chặn, tồn an toàn, thiếu, NCC gợi ý + MOQ. <paramref name="extraIngredients"/> (nguyên liệu → đơn vị dự phòng): nguyên liệu
        /// phải có dòng kết quả dù không còn nhu cầu (CalculateTopUp – dòng nhu cầu / đề nghị mua cũ), nhu cầu = 0.
        /// </summary>
        /// <param name="demand">BIZ-1.3: nguồn nhu cầu do nơi gọi dựng sẵn (dòng đơn chốt / đăng ký / dự báo quy ra dòng – nhãn hiển thị theo OrderId).
        /// Trống = tính chính thức: chỉ đơn Đã chốt của ngày (A9).</param>
        private async Task<(Computation? Data, string? Error)> ComputeAsync(DateTime date, Guid? unitId, Guid? warehouseId, IReadOnlyDictionary<Guid, Guid>? extraIngredients = null,
            IReadOnlyCollection<Guid>? excludePurchaseRequestIds = null, (List<OrderLine> Lines, Dictionary<Guid, string> Labels)? demand = null)
        {
            var db = _unitOfWork.DbContext;
            // 2) Đơn đã chốt × thực đơn → món × suất.
            DateTime next = date.AddDays(1);
            Computation c;
            List<OrderLine> orderLines;
            Dictionary<Guid, string> orderNo;
            if (demand is { } given)
            {
                c = new Computation(new Converter(_uomConversionService, date));
                orderLines = [.. given.Lines.Where(l => l.Quantity > 0)];
                orderNo = given.Labels;
            }
            else
            {
                var orders = await db.Order.AsNoTracking().Where(o => !o.IsDeleted && o.MealDate >= date && o.MealDate < next).ToListAsync();
                var lockedOrders = orders.Where(o => o.Status == OrderStatus.Locked).ToList();
                if (lockedOrders.Count == 0)
                {
                    return (null, string.Format(ConstantHelper.Message.MrpNoLockedOrder, Day(date)));
                }
                c = new Computation(new Converter(_uomConversionService, date)) { LockedOrders = lockedOrders };
                // Anh chốt 2026-11-16: chỉ đơn Locked được tính; đơn Confirmed (và Draft / Submitted) chỉ cảnh báo.
                int notLocked = orders.Count(o => o.Status is OrderStatus.Draft or OrderStatus.Submitted or OrderStatus.Confirmed);
                if (notLocked > 0)
                {
                    c.Warnings.Add(string.Format(ConstantHelper.Message.MrpOrdersNotLocked, notLocked, Day(date)));
                }
                var orderIds = lockedOrders.Select(o => o.Id).ToList();
                orderLines = await db.OrderLine.AsNoTracking().Where(l => orderIds.Contains(l.OrderId) && !l.IsDeleted && l.Quantity > 0).ToListAsync();
                orderNo = lockedOrders.ToDictionary(o => o.Id, o => o.OrderNumber ?? o.OrderNo ?? o.Id.ToString("N")[..8]);
            }
            var warnings = c.Warnings;
            c.OrderLines = orderLines;
            var menuIds = orderLines.Where(l => l.MenuId != null).Select(l => l.MenuId!.Value).Distinct().ToList();
            int dayOfWeek = MenuService.MenuDayOf(date);
            var menuLines = await db.MenuLine.AsNoTracking().Where(m => menuIds.Contains(m.MenuId) && !m.IsDeleted && m.DayOfWeek == dayOfWeek).ToListAsync();
            var menuNames = await db.Menu.AsNoTracking().Where(m => menuIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.Code ?? m.Name);
            var portions = c.Portions;
            var bufferPortions = new Dictionary<Guid, decimal>();
            var lineDemands = new List<ProductionRules.DishDemand>();
            foreach (var line in orderLines.OrderBy(l => orderNo[l.OrderId]).ThenBy(l => l.MealType))
            {
                if (line.MenuId == null)
                {
                    warnings.Add(string.Format(ConstantHelper.Message.MrpOrderLineNoMenu, orderNo[line.OrderId], line.MealType, Round4(line.Quantity)));
                    continue;
                }
                var linePortions = ProductionRules.LinePortions(line, menuLines, _rules, out bool optionMissing);
                if (linePortions.Count == 0)
                {
                    warnings.Add(string.Format(ConstantHelper.Message.MrpMenuNoDish, menuNames.GetValueOrDefault(line.MenuId.Value), line.MealType, dayOfWeek == 7 ? "CN" : (dayOfWeek + 1).ToString(CultureInfo.InvariantCulture),
                        Round4(line.Quantity), orderNo[line.OrderId]));
                    continue;
                }
                // ORD-OPT-1.6 (D-74 e): dòng theo lựa chọn → đúng món của lựa chọn × số suất (dự phòng MrpOptionQtyBufferPct, mặc định 0);
                // dòng cũ NULL → GAP-MRP-03 (chia đều + MrpOptionBufferPct khi ≥ 2 lựa chọn) – ProductionRules.LinePortions.
                if (optionMissing)
                {
                    warnings.Add(string.Format(ConstantHelper.Message.MrpOptionNotInMenu, orderNo[line.OrderId], line.MealType, line.MenuOptionNo, menuNames.GetValueOrDefault(line.MenuId.Value)));
                }
                foreach (var p in linePortions)
                {
                    lineDemands.Add(new ProductionRules.DishDemand(line.OrderId, line.Id, line.ShiftId ?? Guid.Empty, p.DishId, p.Quantity));
                    if (p.Quantity > p.Even)
                    {
                        bufferPortions[p.DishId] = bufferPortions.GetValueOrDefault(p.DishId) + p.Quantity - p.Even;
                    }
                }
            }
            // BE2.2 (spec 6.4): đổi món đã duyệt (MenuChangeRequest) chuyển suất món cũ → món mới trước khi tính nguyên liệu.
            var menuChanges = lineDemands.Count == 0 ? [] : await ProductionRules.ApprovedMenuChangesAsync(db, date);
            if (menuChanges.Count != 0)
            {
                lineDemands = ProductionRules.ApplyMenuChanges(lineDemands, menuChanges);
                warnings.Add($"Đã áp {menuChanges.Count} yêu cầu đổi món đã duyệt của ngày {Day(date)}");
            }
            foreach (var d in lineDemands)
            {
                portions[d.DishId] = portions.GetValueOrDefault(d.DishId) + d.Quantity;
                c.FirstOrderLine.TryAdd(d.DishId, d.OrderLineId);
            }

            // 3) Món × công thức → nhu cầu theo nguyên liệu (đơn vị cơ sở).
            var dishIds = portions.Keys.ToList();
            var dishes = await db.Dish.AsNoTracking().Where(d => dishIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id);
            var recipes = await db.Recipe.AsNoTracking()
                .Where(r => dishIds.Contains(r.DishId) && !r.IsDeleted
                    && (r.Status == RecipeStatus.Locked || r.Status == RecipeStatus.Obsolete || r.Status == RecipeStatus.Approved))
                .ToListAsync();
            var inputs = new List<(Guid DishId, decimal Portions, decimal Servings, RecipeLineInput Line)>();
            foreach (var (dishId, qty) in portions.OrderBy(p => dishes.GetValueOrDefault(p.Key)?.Name))
            {
                var dish = dishes.GetValueOrDefault(dishId);
                var model = new MaterialRequirementDishModel
                {
                    DishId = dishId, DishCode = dish?.Code, DishName = dish?.Name, Portions = Round4(qty), BufferPortions = Round4(bufferPortions.GetValueOrDefault(dishId)),
                };
                c.Dishes.Add(model);
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
                        .Select(l => new RecipeLineInput { IngredientId = l.IngredientId, Quantity = l.Quantity, UnitId = l.UnitId != Guid.Empty ? l.UnitId : l.UomId, WastePercent = l.WastePercent, ExpectedWasteRate = l.ExpectedWasteRate })
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

            var ingredientIds = inputs.Select(i => i.Line.IngredientId).Concat(extraIngredients?.Keys ?? Enumerable.Empty<Guid>()).Distinct().ToList();
            var ingredients = await db.Ingredient.AsNoTracking().Where(i => ingredientIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id);
            var unitNames = await db.UnitMeasure.AsNoTracking().ToDictionaryAsync(u => u.Id, u => (string?)(u.Name ?? u.Code));
            c.Ingredients = ingredients;
            c.UnitNames = unitNames;
            var converter = c.Converter;
            var items = c.Items;
            Guid BaseUnitOf(Ingredient? ingredient, Guid fallback)
                => ingredient?.BaseUnitId is { } b && b != Guid.Empty ? b : ingredient?.BaseUomId is { } o && o != Guid.Empty ? o : fallback;
            // BIZ-1.1 (D-77 a): nhu cầu = định lượng tinh × suất ÷ suất chuẩn ÷ (1 − % chế biến) ÷ (1 − % sơ chế) – WasteRules (thay "× (1 + hao hụt %)" T14.1).
            foreach (var (dishId, qty, servings, line) in inputs)
            {
                var ingredient = ingredients.GetValueOrDefault(line.IngredientId);
                if (!items.TryGetValue(line.IngredientId, out var item))
                {
                    item = new ItemCalc { IngredientId = line.IngredientId, UnitId = BaseUnitOf(ingredient, line.UnitId) };
                    items[line.IngredientId] = item;
                }
                decimal gross = WasteRules.Gross(line.Quantity * qty / servings, line.WastePercent ?? ingredient?.StandardWastePercent, line.ExpectedWasteRate);
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
            foreach (var (ingredientId, fallbackUnit) in extraIngredients ?? new Dictionary<Guid, Guid>())
            {
                if (!items.ContainsKey(ingredientId))
                {
                    items[ingredientId] = new ItemCalc { IngredientId = ingredientId, UnitId = BaseUnitOf(ingredients.GetValueOrDefault(ingredientId), fallbackUnit) };
                }
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
            // BE2.4 (spec 7.1 bước 5): hàng sắp về = Σ dòng PO Đã duyệt / Đã gửi / Nhận một phần có ngày giao dự kiến ≤ ngày sản xuất, trừ phần đã nhận đạt
            // (phiếu nhập đã ghi sổ – đã nằm trong tồn). PO chưa có ngày giao dự kiến → không tính, cảnh báo. PO lập từ đề nghị mua của chính kế hoạch (tính bổ sung) bỏ qua
            // vì đã tính trong "đã đề nghị" (GAP-MRP-08). PROPOSED – BA/PO CONFIRM (D-53 b).
            if (_rules.MrpIncludeIncomingPurchaseOrders)
            {
                var receivable = PurchaseOrderStatus.Receivable.ToList();
                var pos = (await db.PurchaseOrder.AsNoTracking().Where(p => !p.IsDeleted && p.Status != null && receivable.Contains(p.Status)).ToListAsync())
                    .Where(p => excludePurchaseRequestIds == null || p.PurchaseRequestId == null || !excludePurchaseRequestIds.Contains(p.PurchaseRequestId.Value)).ToList();
                var poIds = pos.Select(p => p.Id).ToList();
                var itemIds = items.Keys.ToList();
                var poLines = await db.PurchaseOrderLine.AsNoTracking().Where(l => poIds.Contains(l.PurchaseOrderId) && !l.IsDeleted && itemIds.Contains(l.IngredientId)).ToListAsync();
                var incomingPos = new List<string>();
                foreach (var po in pos.Where(p => poLines.Any(l => l.PurchaseOrderId == p.Id)).OrderBy(p => p.PurchaseOrderNumber ?? p.PONo))
                {
                    string poNo = po.PurchaseOrderNumber ?? po.PONo ?? po.Id.ToString("N")[..8];
                    DateTime? expected = po.ExpectedDeliveryDate ?? po.ExpectedDate;
                    if (expected == null)
                    {
                        warnings.Add(string.Format(ConstantHelper.Message.MrpIncomingNoDate, poNo));
                        continue;
                    }
                    if (expected.Value.Date > date)
                    {
                        continue;
                    }
                    var totals = await PurchasingRules.ReceiptTotalsAsync(db, po.Id);
                    bool counted = false;
                    foreach (var line in poLines.Where(l => l.PurchaseOrderId == po.Id))
                    {
                        decimal remaining = line.Quantity - (totals.TryGetValue(line.IngredientId, out var t) ? t.Received : 0m);
                        if (remaining <= 0)
                        {
                            continue;
                        }
                        var item = items[line.IngredientId];
                        Guid lineUnit = UnitOf(line.UnitId, line.UomId);
                        decimal? converted = await converter.ConvertAsync(remaining, lineUnit, item.UnitId);
                        if (converted == null)
                        {
                            string message = string.Format(ConstantHelper.Message.MrpUnitMissing, ingredients.GetValueOrDefault(line.IngredientId)?.Name, unitNames.GetValueOrDefault(lineUnit), unitNames.GetValueOrDefault(item.UnitId));
                            if (!warnings.Contains(message))
                            {
                                warnings.Add(message);
                                item.Warnings.Add(message);
                            }
                            continue;
                        }
                        item.Incoming += converted.Value;
                        counted = true;
                    }
                    if (counted)
                    {
                        incomingPos.Add(poNo);
                    }
                }
                foreach (var item in items.Values)
                {
                    item.Incoming = Round4(item.Incoming);
                }
                if (incomingPos.Count != 0)
                {
                    warnings.Add(string.Format(ConstantHelper.Message.MrpIncomingCounted, string.Join(", ", incomingPos)));
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
                if (best != null)
                {
                    item.Best = best;
                    item.BestSupplier = suppliers[best.SupplierId];
                    item.PurchaseUnit = best.PurchaseUomId is { } pu && pu != Guid.Empty ? pu : item.UnitId;
                    decimal? moq = best.MinimumOrderQuantity ?? best.MinimumOrderQty;
                    item.MoqBase = moq is > 0 ? await converter.ConvertAsync(moq.Value, item.PurchaseUnit, item.UnitId) : null;
                }
            }
            return (c, null);
        }

        /// <summary>
        /// Số mua của 1 nguyên liệu cho số cần <paramref name="need"/> (đơn vị cơ sở): làm tròn lên MOQ của NCC gợi ý (PROPOSED – GAP-MRP-04), giá / tiền ước tính
        /// theo đơn vị mua, cảnh báo NCC giao không kịp / chưa có NCC.
        /// </summary>
        private async Task PriceAsync(Computation c, ItemCalc item, decimal need, DateTime date)
        {
            decimal purchase = need;
            item.Supplier = null;
            if (item.Best != null && item.BestSupplier != null)
            {
                var best = item.Best;
                var supplier = item.BestSupplier;
                if (purchase > 0 && item.MoqBase is { } moqBase && purchase < moqBase)
                {
                    purchase = moqBase; // PROPOSED: số mua tối thiểu = MOQ (chưa có cột quy cách đóng gói – GAP-MRP-04).
                }
                purchase = Ceil4(purchase);
                decimal? inPurchaseUnit = purchase > 0 ? await c.Converter.ConvertAsync(purchase, item.UnitId, item.PurchaseUnit) : 0m;
                item.Supplier = new MaterialRequirementSupplierModel
                {
                    SupplierMaterialId = best.Id,
                    SupplierId = best.SupplierId,
                    SupplierCode = supplier.Code,
                    SupplierName = supplier.Name,
                    IsPreferred = best.IsPreferred,
                    UnitPrice = best.UnitPrice,
                    PurchaseUnitId = item.PurchaseUnit,
                    PurchaseUnitName = c.UnitNames.GetValueOrDefault(item.PurchaseUnit),
                    MinimumOrderQuantity = best.MinimumOrderQuantity ?? best.MinimumOrderQty,
                    LeadTimeDays = best.LeadTimeDays,
                    PurchaseQuantityInPurchaseUnit = inPurchaseUnit == null ? null : Round4(inPurchaseUnit.Value),
                    EstimatedAmount = inPurchaseUnit != null && best.UnitPrice != null ? Math.Round(inPurchaseUnit.Value * best.UnitPrice.Value, 2) : null,
                };
                if (purchase > 0 && best.LeadTimeDays is > 0 && _clock.Today.AddDays(best.LeadTimeDays.Value) > date)
                {
                    string message = string.Format(ConstantHelper.Message.MrpLeadTimeWarning, supplier.Name, best.LeadTimeDays, Day(date));
                    item.Warnings.Add(message);
                    c.Warnings.Add($"{message} ({c.Ingredients.GetValueOrDefault(item.IngredientId)?.Name})");
                }
            }
            else if (purchase > 0)
            {
                item.Warnings.Add(ConstantHelper.Message.MrpNoSupplier);
            }
            item.Purchase = Ceil4(purchase);
        }

        /// <summary>Ghi chú dòng đề nghị mua: NCC gợi ý dạng chữ (PurchaseRequestLine không có cột NCC / đơn giá – GAP-MRP-02).</summary>
        private static string PurchaseLineNote(ItemCalc item)
            => item.Supplier == null ? ConstantHelper.Message.MrpNoSupplier
                : $"NCC gợi ý: {item.Supplier.SupplierName}"
                  + (item.Supplier.UnitPrice != null ? $" – giá {item.Supplier.UnitPrice.Value.ToString("0.##", CultureInfo.InvariantCulture)}/{item.Supplier.PurchaseUnitName}" : string.Empty)
                  + (item.Supplier.MinimumOrderQuantity != null ? $" – MOQ {item.Supplier.MinimumOrderQuantity.Value.ToString("0.####", CultureInfo.InvariantCulture)}" : string.Empty);

        /// <summary>Dòng kế hoạch theo món: cập nhật tại chỗ / thêm (dòng không còn món đã được xóa mềm trước đó).</summary>
        private void UpsertPlanLines(ProductionPlan plan, List<ProductionPlanLine> oldPlanLines, Computation c)
        {
            foreach (var (dishId, qty) in c.Portions)
            {
                var line = oldPlanLines.FirstOrDefault(l => l.DishId == dishId);
                if (line == null)
                {
                    _unitOfWork.ProductionPlanLineRepository.Add(new ProductionPlanLine
                    {
                        Id = Guid.NewGuid(),
                        ProductionPlanId = plan.Id,
                        DishId = dishId,
                        OrderLineId = c.FirstOrderLine.GetValueOrDefault(dishId),
                        PlannedQuantity = Round4(qty),
                        PlannedQty = Round4(qty),
                        Status = ProductionPlanStatus.Draft,
                    });
                }
                else
                {
                    line.PlannedQuantity = Round4(qty);
                    line.PlannedQty = Round4(qty);
                    line.OrderLineId = c.FirstOrderLine.GetValueOrDefault(dishId);
                    _unitOfWork.ProductionPlanLineRepository.Update(line);
                }
            }
        }

        /// <summary>Xóa mềm (CommitAsync Delete đánh dấu mọi bản ghi đang sửa – phải chạy trước khi thêm / cập nhật).</summary>
        private async Task SoftDeleteAsync(List<BaseEntity> toDelete)
        {
            if (toDelete.Count == 0)
            {
                return;
            }
            foreach (var entity in toDelete)
            {
                _unitOfWork.DbContext.Update(entity);
            }
            await _unitOfWork.MaterialRequirementRepository.CommitAsync(ConstantHelper.TypeSave.Delete);
        }

        /// <summary>
        /// T14 (spec 7.1 calculate, BUSINESS_RULES §3, Pilot P5) – bấm tay thay job 20:30, trong 1 transaction:
        /// <list type="number">
        /// <item>Đơn <b>Đã chốt</b> (Locked) có ngày ăn = ngày sản xuất × thực đơn của dòng đơn (MenuLine cùng loại suất, thứ của ngày) → món × suất.
        /// Thực đơn có nhiều lựa chọn (SequenceNo) → suất chia đều cho các lựa chọn rồi cộng dự phòng <c>MrpOptionBufferPct</c> mỗi lựa chọn (T14.1, GAP-MRP-03).</item>
        /// <item>Món × công thức: bản đã khóa hiệu lực tại ngày (SnapshotJson; thiếu thì dòng định lượng), không có thì bản Đã duyệt mới nhất (cảnh báo).
        /// Nhu cầu = định lượng tinh × suất ÷ suất chuẩn (StandardOutputQty, mặc định BusinessRules 100) ÷ (1 − % chế biến ExpectedWasteRate) ÷ (1 − % sơ chế) (BIZ-1.1), % sơ chế = RecipeLine.WastePercent,
        /// trống thì Ingredient.StandardWastePercent; quy đổi về đơn vị cơ sở của nguyên liệu qua UomConversion.</item>
        /// <item>Khả dụng = Σ AvailableQuantity (đã trừ giữ chỗ) của lô không bị chặn tại ngày sản xuất (InventoryLotRules: hết hạn / QC / tạm giữ), theo kho / chi nhánh.</item>
        /// <item>Thiếu = max(0, nhu cầu + tồn an toàn (D-12, PROPOSED) − khả dụng − hàng sắp về (Pilot = 0)); số mua làm tròn lên MOQ của NCC gợi ý.</item>
        /// <item>Ghi: ProductionPlan Draft (KH-, tạo nếu chưa có) + ProductionPlanLine theo món, MaterialRequirement theo nguyên liệu (cập nhật tại chỗ, dòng thừa xóa mềm),
        /// PurchaseRequest Draft (PH-) + dòng cho phần thiếu (đề nghị nháp của lần trước được cập nhật; đề nghị đã gửi duyệt / duyệt / lập PO → không tính lại,
        /// dùng <see cref="CalculateTopUp"/>).</item>
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
                string? scopeError = await ValidateScopeAsync(unitId, warehouseId);
                if (scopeError != null)
                {
                    return Fail(scopeError);
                }

                // 1) Khóa theo ngày (sp_getapplock của mã KH- – 2 lần tính cùng ngày chạy lần lượt) rồi tìm kế hoạch neo + kết quả lần trước.
                string planNumber = await _documentNumberService.NextAsync(DocumentKind.ProductionPlan, date);
                var plan = PlansOf(date, unitId).FirstOrDefault();
                if (plan != null && plan.Status != ProductionPlanStatus.Draft)
                {
                    return Fail(string.Format(ConstantHelper.Message.MrpPlanNotDraft, plan.PlanNumber ?? plan.PlanNo, ProductionPlanStatus.Label(plan.Status)));
                }
                var planNote = ReadJson<PlanNote>(plan?.Note);
                // T14.1: kế hoạch đã có đề nghị mua bổ sung còn hiệu lực → tính lại sẽ lập trùng → chỉ được tính bổ sung.
                if (planNote?.TopUpPurchaseRequestIds is { Count: > 0 } topUpIds)
                {
                    var activeTopUps = _unitOfWork.PurchaseRequestRepository.GetAll()
                        .Where(p => topUpIds.Contains(p.Id) && !p.IsDeleted && p.Status != PurchaseRequestStatus.Cancelled && p.Status != PurchaseRequestStatus.Rejected).ToList();
                    if (activeTopUps.Count != 0)
                    {
                        return Fail(string.Format(ConstantHelper.Message.MrpTopUpExists, plan!.PlanNumber ?? plan.PlanNo,
                            string.Join(", ", activeTopUps.Select(p => p.RequestNumber ?? p.RequestNo))));
                    }
                }
                var oldRows = plan == null ? [] : _unitOfWork.MaterialRequirementRepository.GetAll().Where(r => r.ProductionPlanId == plan.Id && !r.IsDeleted).ToList();
                var oldPlanLines = plan == null ? [] : _unitOfWork.ProductionPlanLineRepository.GetAll().Where(l => l.ProductionPlanId == plan.Id && !l.IsDeleted).ToList();
                Guid? oldPrId = planNote?.PurchaseRequestId
                    ?? oldRows.Select(r => ReadJson<RowNote>(r.Note)?.PurchaseRequestId).FirstOrDefault(x => x != null);
                PurchaseRequest? purchaseRequest = oldPrId == null ? null : _unitOfWork.PurchaseRequestRepository.GetSingle(p => p.Id == oldPrId && !p.IsDeleted);
                if (purchaseRequest != null && purchaseRequest.Status != PurchaseRequestStatus.Draft)
                {
                    if (IsMovedOn(purchaseRequest.Status))
                    {
                        return Fail(string.Format(ConstantHelper.Message.MrpPurchaseRequestLocked, purchaseRequest.RequestNumber ?? purchaseRequest.RequestNo,
                            PurchaseRequestStatus.Label(purchaseRequest.Status)));
                    }
                    purchaseRequest = null; // Từ chối / Hủy → lần này lập đề nghị mới, đề nghị cũ giữ nguyên.
                }

                // 2–4) Đơn đã chốt × thực đơn × công thức → nhu cầu, tồn, thiếu, NCC gợi ý.
                var (calc, error) = await ComputeAsync(date, unitId, warehouseId);
                if (calc == null)
                {
                    return Fail(error!);
                }
                var items = calc.Items;
                foreach (var item in items.Values)
                {
                    await PriceAsync(calc, item, item.Shortage, date);
                }

                // 5) Ghi trong cùng transaction. Xóa mềm trước (CommitAsync Delete đánh dấu mọi bản ghi đang sửa) rồi thêm / cập nhật.
                bool needPurchase = items.Values.Any(i => i.Purchase > 0);
                var oldPrLines = purchaseRequest == null ? [] : _unitOfWork.PurchaseRequestLineRepository.GetAll()
                    .Where(l => l.PurchaseRequestId == purchaseRequest.Id && !l.IsDeleted).ToList();
                var toDelete = new List<BaseEntity>();
                toDelete.AddRange(oldRows.Where(r => !items.ContainsKey(r.IngredientId)));
                toDelete.AddRange(oldPlanLines.Where(l => !calc.Portions.ContainsKey(l.DishId)));
                toDelete.AddRange(oldPrLines.Where(l => !needPurchase || !items.TryGetValue(l.IngredientId, out var it) || it.Purchase <= 0));
                if (purchaseRequest != null && !needPurchase)
                {
                    toDelete.Add(purchaseRequest);
                }
                await SoftDeleteAsync(toDelete);

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
                        OrganizationUnitId = null, // UI-6.23: một kế hoạch / ngày
                        Status = ProductionPlanStatus.Draft,
                    };
                    _unitOfWork.ProductionPlanRepository.Add(plan);
                }
                UpsertPlanLines(plan, oldPlanLines, calc);

                if (needPurchase && purchaseRequest == null)
                {
                    string number = await _documentNumberService.NextAsync(DocumentKind.PurchaseRequest, _clock.Today);
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

                string? requirementNo = null; // MA-1.7: 1 số MR- cho mỗi kế hoạch (giữ số đã cấp)
                foreach (var item in items.Values)
                {
                    PurchaseRequestLine? prLine = null;
                    if (item.Purchase > 0)
                    {
                        prLine = oldPrLines.FirstOrDefault(l => l.IngredientId == item.IngredientId && !toDelete.Contains(l));
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
                        prLine.Note = PurchaseLineNote(item);
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
                    row.RequirementNo = requirementNo ??= await RequirementNumberAsync(oldRows);
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
                    OrderCount = calc.LockedOrders.Count,
                    TotalMeals = Round4(calc.OrderLines.Sum(l => l.Quantity)),
                    PurchaseRequestId = needPurchase ? purchaseRequest!.Id : null,
                    // Đề nghị bổ sung cũ (đã hủy / từ chối) giữ lại để tra cứu.
                    TopUpPurchaseRequestIds = planNote?.TopUpPurchaseRequestIds ?? [],
                    TopUpAt = planNote?.TopUpAt,
                    UserNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
                    Dishes = calc.Dishes,
                    Warnings = calc.Warnings,
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

        /// <summary>
        /// T14.1 (anh chốt 2026-11-16) – tính bổ sung khi đề nghị mua của kế hoạch đã gửi duyệt / duyệt / lập PO (Calculate bị chặn), trong 1 transaction:
        /// <list type="number">
        /// <item>Tính lại nhu cầu của ngày như Calculate (đơn Locked, chia lựa chọn + dự phòng, công thức, tồn, tồn an toàn).</item>
        /// <item>Đã đề nghị = Σ dòng các đề nghị mua <b>chưa hủy / từ chối</b> của kế hoạch (đề nghị chính + các đề nghị bổ sung trước) quy về đơn vị cơ sở,
        /// trừ phần đã nhận đạt (phiếu nhập đã ghi sổ của PO lập từ các đề nghị đó – vì phần này đã nằm trong tồn xuất được). PROPOSED – BA/PO CONFIRM.</item>
        /// <item>Chênh lệch = thiếu mới − đã đề nghị còn chờ về. Dương → 1 đề nghị mua <b>Nháp mới</b> (PH-, Note "BỔ SUNG …") chỉ cho phần chênh lệch,
        /// làm tròn MOQ + NCC gợi ý như Calculate. Âm → chỉ cảnh báo (không sửa đề nghị cũ). Không thiếu thêm → không lập đề nghị, trả thông báo.</item>
        /// <item>Cập nhật tại chỗ dòng nhu cầu (không DELETE; nguyên liệu không còn nhu cầu giữ dòng với nhu cầu 0) + dòng kế hoạch theo món.</item>
        /// </list>
        /// Quyền = Sửa trên <c>scm-requirement</c> (PermissionConvention, như Calculate).
        /// </summary>
        public async Task<IResult<MaterialRequirementResultModel>> CalculateTopUp(MaterialRequirementCalculateRequest request)
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
                string? scopeError = await ValidateScopeAsync(unitId, warehouseId);
                if (scopeError != null)
                {
                    return Fail(scopeError);
                }

                // 1) Cùng khóa theo ngày với Calculate (sp_getapplock của mã KH-), kế hoạch neo phải có và còn Nháp.
                await _documentNumberService.NextAsync(DocumentKind.ProductionPlan, date);
                var plan = PlansOf(date, unitId).FirstOrDefault();
                if (plan == null)
                {
                    return Fail(string.Format(ConstantHelper.Message.MrpTopUpNotCalculated, Day(date)));
                }
                string? planNo = plan.PlanNumber ?? plan.PlanNo;
                if (plan.Status != ProductionPlanStatus.Draft)
                {
                    return Fail(string.Format(ConstantHelper.Message.MrpPlanNotDraft, planNo, ProductionPlanStatus.Label(plan.Status)));
                }
                var planNote = ReadJson<PlanNote>(plan.Note);
                var oldRows = _unitOfWork.MaterialRequirementRepository.GetAll().Where(r => r.ProductionPlanId == plan.Id && !r.IsDeleted).ToList();
                var oldPlanLines = _unitOfWork.ProductionPlanLineRepository.GetAll().Where(l => l.ProductionPlanId == plan.Id && !l.IsDeleted).ToList();
                Guid? mainPrId = planNote?.PurchaseRequestId ?? oldRows.Select(r => ReadJson<RowNote>(r.Note)?.PurchaseRequestId).FirstOrDefault(x => x != null);
                var topUpIds = planNote?.TopUpPurchaseRequestIds ?? [];
                var prIds = topUpIds.ToList();
                if (mainPrId != null)
                {
                    prIds.Insert(0, mainPrId.Value);
                }
                var activePrs = _unitOfWork.PurchaseRequestRepository.GetAll().Where(p => prIds.Contains(p.Id) && !p.IsDeleted).ToList()
                    .Where(p => IsActivePurchaseRequest(p.Status)).OrderBy(p => prIds.IndexOf(p.Id)).ToList();
                if (!activePrs.Any(p => IsMovedOn(p.Status)))
                {
                    return Fail(string.Format(ConstantHelper.Message.MrpTopUpNotNeeded, planNo));
                }

                // 2) Đã đề nghị (chưa hủy / từ chối) và đã nhận đạt qua phiếu nhập đã ghi sổ.
                var activeIds = activePrs.Select(p => p.Id).ToList();
                var prLines = await db.PurchaseRequestLine.AsNoTracking().Where(l => activeIds.Contains(l.PurchaseRequestId) && !l.IsDeleted).ToListAsync();
                var poIds = await db.PurchaseOrder.AsNoTracking()
                    .Where(p => p.PurchaseRequestId != null && activeIds.Contains(p.PurchaseRequestId.Value) && !p.IsDeleted).Select(p => p.Id).ToListAsync();
                var grIds = await db.GoodsReceipt.AsNoTracking()
                    .Where(g => g.PurchaseOrderId != null && poIds.Contains(g.PurchaseOrderId.Value) && !g.IsDeleted && g.Status == GoodsReceiptStatus.Posted).Select(g => g.Id).ToListAsync();
                var grLines = await db.GoodsReceiptLine.AsNoTracking().Where(l => grIds.Contains(l.GoodsReceiptId) && !l.IsDeleted && l.AcceptedQuantity > 0).ToListAsync();

                // 3) Tính lại nhu cầu (giữ cả nguyên liệu đã có dòng nhu cầu / đã đề nghị dù không còn nhu cầu).
                var extras = new Dictionary<Guid, Guid>();
                foreach (var row in oldRows)
                {
                    extras.TryAdd(row.IngredientId, row.UnitId);
                }
                foreach (var line in prLines)
                {
                    extras.TryAdd(line.IngredientId, UnitOf(line.UnitId, line.UomId));
                }
                var (calc, error) = await ComputeAsync(date, unitId, warehouseId, extras, activeIds);
                if (calc == null)
                {
                    return Fail(error!);
                }
                var items = calc.Items;
                var requested = new Dictionary<Guid, decimal>();
                var received = new Dictionary<Guid, decimal>();
                async Task SumAsync(Dictionary<Guid, decimal> target, Guid ingredientId, decimal quantity, Guid unit)
                {
                    var item = items[ingredientId];
                    decimal? converted = await calc.Converter.ConvertAsync(quantity, unit, item.UnitId);
                    if (converted == null)
                    {
                        item.Incomplete = true;
                        string message = string.Format(ConstantHelper.Message.MrpUnitMissing, calc.Ingredients.GetValueOrDefault(ingredientId)?.Name,
                            calc.UnitNames.GetValueOrDefault(unit), calc.UnitNames.GetValueOrDefault(item.UnitId));
                        if (!calc.Warnings.Contains(message))
                        {
                            calc.Warnings.Add(message);
                            item.Warnings.Add(message);
                        }
                        return;
                    }
                    target[ingredientId] = target.GetValueOrDefault(ingredientId) + converted.Value;
                }
                foreach (var line in prLines)
                {
                    await SumAsync(requested, line.IngredientId, line.Quantity, UnitOf(line.UnitId, line.UomId));
                }
                foreach (var line in grLines.Where(l => items.ContainsKey(l.IngredientId)))
                {
                    await SumAsync(received, line.IngredientId, line.AcceptedQuantity, UnitOf(line.UnitId, line.UomId));
                }

                // 4) Chênh lệch theo nguyên liệu → số bổ sung (dương) / cảnh báo (âm).
                var topUp = new MaterialRequirementTopUpModel { ListComparedRequestNumber = [.. activePrs.Select(p => p.RequestNumber ?? p.RequestNo ?? p.Id.ToString())] };
                var openQty = new Dictionary<Guid, decimal>();
                foreach (var item in items.Values.OrderBy(i => calc.Ingredients.GetValueOrDefault(i.IngredientId)?.Name))
                {
                    var ingredient = calc.Ingredients.GetValueOrDefault(item.IngredientId);
                    decimal open = Round4(Math.Max(0m, requested.GetValueOrDefault(item.IngredientId) - received.GetValueOrDefault(item.IngredientId)));
                    openQty[item.IngredientId] = open;
                    decimal diff = Round4(item.Shortage - open);
                    await PriceAsync(calc, item, diff > 0 ? diff : 0m, date);
                    string? warning = null;
                    if (diff < 0)
                    {
                        string unitName = calc.UnitNames.GetValueOrDefault(item.UnitId) ?? string.Empty;
                        warning = string.Format(ConstantHelper.Message.MrpTopUpOverRequested, ingredient?.Name, Num(open), unitName, Num(item.Shortage), Num(-diff));
                        topUp.ListWarning.Add(warning);
                        item.Warnings.Add(warning);
                    }
                    topUp.ListLine.Add(new MaterialRequirementTopUpLineModel
                    {
                        IngredientId = item.IngredientId,
                        IngredientCode = ingredient?.Code,
                        IngredientName = ingredient?.Name,
                        UnitId = item.UnitId,
                        UnitName = calc.UnitNames.GetValueOrDefault(item.UnitId),
                        ShortageQuantity = item.Shortage,
                        RequestedQuantity = open,
                        DifferenceQuantity = diff,
                        TopUpQuantity = item.Purchase,
                        Supplier = item.Purchase > 0 ? item.Supplier : null,
                        Warning = item.Purchase > 0 && item.Warnings.Count != 0 ? string.Join(" | ", item.Warnings) : warning,
                    });
                }
                bool needTopUp = items.Values.Any(i => i.Purchase > 0);

                // 5) Ghi: dòng kế hoạch (xóa mềm món không còn), đề nghị mua bổ sung Nháp mới, dòng nhu cầu cập nhật tại chỗ.
                await SoftDeleteAsync([.. oldPlanLines.Where(l => !calc.Portions.ContainsKey(l.DishId))]);
                DateTime now = _clock.Now;
                UpsertPlanLines(plan, oldPlanLines, calc);
                PurchaseRequest? topUpPr = null;
                if (needTopUp)
                {
                    string number = await _documentNumberService.NextAsync(DocumentKind.PurchaseRequest, _clock.Today);
                    string userNote = string.IsNullOrWhiteSpace(request.Note) ? string.Empty : " – " + request.Note.Trim();
                    topUpPr = new PurchaseRequest
                    {
                        Id = Guid.NewGuid(),
                        RequestNumber = number,
                        RequestNo = number,
                        RequestDate = now,
                        RequiredDate = date,
                        Status = PurchaseRequestStatus.Draft,
                        Note = $"BỔ SUNG – Tự động từ tính nhu cầu bổ sung NL ngày {Day(date)} – kế hoạch {planNo} – bổ sung cho {string.Join(", ", topUp.ListComparedRequestNumber)}{userNote}",
                    };
                    _unitOfWork.PurchaseRequestRepository.Add(topUpPr);
                }
                string? requirementNo = null; // MA-1.7: 1 số MR- cho mỗi kế hoạch (giữ số đã cấp)
                foreach (var item in items.Values)
                {
                    PurchaseRequestLine? prLine = null;
                    if (topUpPr != null && item.Purchase > 0)
                    {
                        prLine = new PurchaseRequestLine
                        {
                            Id = Guid.NewGuid(),
                            PurchaseRequestId = topUpPr.Id,
                            IngredientId = item.IngredientId,
                            Quantity = item.Purchase,
                            UnitId = item.UnitId,
                            UomId = item.UnitId,
                            RequiredDate = date,
                            Note = PurchaseLineNote(item),
                        };
                        _unitOfWork.PurchaseRequestLineRepository.Add(prLine);
                    }
                    var row = oldRows.FirstOrDefault(r => r.IngredientId == item.IngredientId);
                    var oldNote = row == null ? null : ReadJson<RowNote>(row.Note);
                    decimal requestedTotal = Round4(requested.GetValueOrDefault(item.IngredientId));
                    var note = new RowNote
                    {
                        SafetyStock = item.Safety,
                        BlockedQuantity = item.Blocked,
                        ShortageQuantity = item.Shortage,
                        Incomplete = item.Incomplete,
                        Warning = item.Warnings.Count == 0 ? null : string.Join(" | ", item.Warnings),
                        PurchaseRequestId = oldNote?.PurchaseRequestId,
                        PurchaseRequestLineId = oldNote?.PurchaseRequestLineId,
                        Supplier = oldNote?.Supplier ?? item.Supplier,
                        RequestedQuantity = openQty.GetValueOrDefault(item.IngredientId),
                        TopUpPurchaseRequestId = prLine != null ? topUpPr!.Id : oldNote?.TopUpPurchaseRequestId,
                        TopUpPurchaseRequestLineId = prLine?.Id ?? oldNote?.TopUpPurchaseRequestLineId,
                        TopUpSupplier = prLine != null ? item.Supplier : oldNote?.TopUpSupplier,
                    };
                    if (row == null)
                    {
                        row = new MaterialRequirement { Id = Guid.NewGuid(), ProductionPlanId = plan.Id, IngredientId = item.IngredientId };
                        _unitOfWork.MaterialRequirementRepository.Add(row);
                    }
                    else
                    {
                        _unitOfWork.MaterialRequirementRepository.Update(row);
                    }
                    // Số đề nghị mua của dòng = Σ đã đề nghị (chưa hủy / từ chối) + bổ sung lần này.
                    decimal purchaseTotal = Round4(requestedTotal + (prLine != null ? item.Purchase : 0m));
                    row.RequirementNo = requirementNo ??= await RequirementNumberAsync(oldRows);
                    row.UnitId = item.UnitId;
                    row.RequiredQuantity = item.Required;
                    row.RequiredQty = item.Required;
                    row.AvailableQuantity = item.Available;
                    row.AvailableQty = item.Available;
                    row.ExpectedIncomingQuantity = item.Incoming;
                    row.ExpectedIncomingQty = item.Incoming;
                    row.PurchaseQuantity = purchaseTotal;
                    row.PurchaseQty = purchaseTotal;
                    row.Status = purchaseTotal > 0 ? MaterialRequirementStatus.Requested : MaterialRequirementStatus.Sufficient;
                    row.Note = JsonConvert.SerializeObject(note);
                }

                if (topUpPr != null)
                {
                    topUpIds.Add(topUpPr.Id);
                }
                plan.Note = JsonConvert.SerializeObject(new PlanNote
                {
                    CalculatedAt = now,
                    CalculatedBy = actor.UserId,
                    WarehouseId = warehouseId,
                    OrderCount = calc.LockedOrders.Count,
                    TotalMeals = Round4(calc.OrderLines.Sum(l => l.Quantity)),
                    PurchaseRequestId = mainPrId,
                    TopUpPurchaseRequestIds = topUpIds,
                    TopUpAt = now,
                    UserNote = string.IsNullOrWhiteSpace(request.Note) ? planNote?.UserNote : request.Note.Trim(),
                    Dishes = calc.Dishes,
                    Warnings = [.. calc.Warnings, .. topUp.ListWarning],
                });
                _unitOfWork.ProductionPlanRepository.Update(plan);
                await _unitOfWork.MaterialRequirementRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();

                var detail = await DetailAsync(plan.Id);
                detail!.TopUp = topUp;
                if (topUpPr != null)
                {
                    topUp.PurchaseRequest = detail.ListTopUpPurchaseRequest.FirstOrDefault(p => p.Id == topUpPr.Id);
                    return Result<MaterialRequirementResultModel>.Success(detail,
                        string.Format(ConstantHelper.Message.MrpTopUpCreated, topUpPr.RequestNumber, topUp.ListLine.Count(l => l.TopUpQuantity > 0)));
                }
                return Result<MaterialRequirementResultModel>.Success(detail,
                    string.Format(ConstantHelper.Message.MrpTopUpNothingShort, string.Join(", ", topUp.ListComparedRequestNumber)));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " CalculateTopUp: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }

            IResult<MaterialRequirementResultModel> Fail(string message)
            {
                _unitOfWork.RollbackTransaction();
                return Result<MaterialRequirementResultModel>.Fail(message);
            }
        }

        /// <summary>
        /// BE2.1 (spec 7.1 create-purchase-request, Catalog 107) – lập / làm lại đề nghị mua <b>Nháp</b> của kế hoạch chỉ từ các dòng thiếu được chọn, cộng % dự phòng, trong 1 transaction:
        /// <list type="bullet">
        /// <item>Dòng chọn phải thuộc kế hoạch và có số mua &gt; 0 (trống = mọi dòng thiếu). Số đề nghị = Ceil4(số mua × (1 + BufferPercent / 100)) – spec ghi ceil;
        /// em làm tròn lên 4 số lẻ như MRP (PROPOSED – BA/PO CONFIRM: làm tròn lên số nguyên theo đơn vị mua nếu anh muốn).</item>
        /// <item>Đề nghị chính của kế hoạch (PlanNote.PurchaseRequestId) còn Nháp → cập nhật tại chỗ (giữ số PH-), dòng không chọn xóa mềm; Chờ duyệt / Đã duyệt / Đã lập PO → chặn
        /// (dùng CalculateTopUp); Từ chối / Hủy hoặc chưa có → lập đề nghị Nháp PH- mới.</item>
        /// <item>Dòng nhu cầu được chọn → Đã đề nghị mua; dòng thiếu không chọn → Thiếu. Duyệt đề nghị mua 2 cấp (D-46) làm ở BE2.3 – chưa gọi ApprovalService ở đây.</item>
        /// </list>
        /// </summary>
        public async Task<IResult<MaterialRequirementResultModel>> CreatePurchaseRequest(MaterialRequirementPurchaseRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var actor = _currentActor.RequireActor();
                if (request.BufferPercent < 0 || request.BufferPercent > 100)
                {
                    return Fail(ConstantHelper.Message.MrpBufferInvalid);
                }
                var plan = _unitOfWork.ProductionPlanRepository.GetSingle(p => p.Id == request.ProductionPlanId && !p.IsDeleted);
                if (plan == null)
                {
                    return Fail(ConstantHelper.Message.MrpPlanNotFound);
                }
                var rows = _unitOfWork.MaterialRequirementRepository.GetAll().Where(r => r.ProductionPlanId == plan.Id && !r.IsDeleted).ToList();
                List<MaterialRequirement> selected;
                if (request.ListRequirementId.Count == 0)
                {
                    selected = [.. rows.Where(r => r.PurchaseQuantity > 0)];
                }
                else
                {
                    selected = [];
                    foreach (var id in request.ListRequirementId.Distinct())
                    {
                        var row = rows.FirstOrDefault(r => r.Id == id);
                        if (row == null)
                        {
                            return Fail(ConstantHelper.Message.MrpRequirementNotInPlan);
                        }
                        if (row.PurchaseQuantity > 0)
                        {
                            selected.Add(row);
                        }
                    }
                }
                if (selected.Count == 0)
                {
                    return Fail(ConstantHelper.Message.MrpNoShortageSelected);
                }
                var planNote = ReadJson<PlanNote>(plan.Note) ?? new PlanNote { CalculatedAt = _clock.Now, CalculatedBy = actor.UserId };
                PurchaseRequest? purchaseRequest = planNote.PurchaseRequestId is { } prId ? _unitOfWork.PurchaseRequestRepository.GetSingle(p => p.Id == prId && !p.IsDeleted) : null;
                if (purchaseRequest != null && purchaseRequest.Status != PurchaseRequestStatus.Draft)
                {
                    if (IsMovedOn(purchaseRequest.Status))
                    {
                        return Fail(string.Format(ConstantHelper.Message.MrpPurchaseRequestLocked, purchaseRequest.RequestNumber ?? purchaseRequest.RequestNo,
                            PurchaseRequestStatus.Label(purchaseRequest.Status)));
                    }
                    purchaseRequest = null;
                }
                var selectedIngredients = selected.Select(r => r.IngredientId).ToHashSet();
                var oldLines = purchaseRequest == null ? [] : _unitOfWork.PurchaseRequestLineRepository.GetAll()
                    .Where(l => l.PurchaseRequestId == purchaseRequest.Id && !l.IsDeleted).ToList();
                await SoftDeleteAsync([.. oldLines.Where(l => !selectedIngredients.Contains(l.IngredientId))]);

                DateTime now = _clock.Now;
                DateTime date = plan.ProductionDate.Date;
                if (purchaseRequest == null)
                {
                    string number = await _documentNumberService.NextAsync(DocumentKind.PurchaseRequest, _clock.Today);
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
                else
                {
                    purchaseRequest.RequiredDate = date;
                    _unitOfWork.PurchaseRequestRepository.Update(purchaseRequest);
                }
                purchaseRequest.Note = $"Từ nhu cầu NL ngày {Day(date)} – kế hoạch {plan.PlanNumber ?? plan.PlanNo} – {selected.Count} dòng chọn, dự phòng {Num(request.BufferPercent)}%"
                    + (string.IsNullOrWhiteSpace(request.Note) ? string.Empty : " – " + request.Note.Trim());
                decimal rate = 1m + request.BufferPercent / 100m;
                foreach (var row in rows)
                {
                    var rowNote = ReadJson<RowNote>(row.Note) ?? new RowNote();
                    if (!selectedIngredients.Contains(row.IngredientId) || !selected.Contains(row))
                    {
                        if (row.PurchaseQuantity > 0 && rowNote.PurchaseRequestId == purchaseRequest.Id)
                        {
                            rowNote.PurchaseRequestId = null;
                            rowNote.PurchaseRequestLineId = null;
                            row.Status = MaterialRequirementStatus.Shortage;
                            row.Note = JsonConvert.SerializeObject(rowNote);
                            _unitOfWork.MaterialRequirementRepository.Update(row);
                        }
                        continue;
                    }
                    var line = oldLines.FirstOrDefault(l => l.IngredientId == row.IngredientId);
                    if (line == null)
                    {
                        line = new PurchaseRequestLine { Id = Guid.NewGuid(), PurchaseRequestId = purchaseRequest.Id, IngredientId = row.IngredientId };
                        _unitOfWork.PurchaseRequestLineRepository.Add(line);
                    }
                    else
                    {
                        _unitOfWork.PurchaseRequestLineRepository.Update(line);
                    }
                    line.Quantity = Ceil4(row.PurchaseQuantity * rate);
                    line.UnitId = row.UnitId;
                    line.UomId = row.UnitId;
                    line.RequiredDate = date;
                    var supplier = rowNote.Supplier;
                    line.Note = (supplier == null ? ConstantHelper.Message.MrpNoSupplier : $"NCC gợi ý: {supplier.SupplierName}")
                        + (request.BufferPercent > 0 ? $" – gồm dự phòng {Num(request.BufferPercent)}% (số mua {Num(row.PurchaseQuantity)})" : string.Empty);
                    rowNote.PurchaseRequestId = purchaseRequest.Id;
                    rowNote.PurchaseRequestLineId = line.Id;
                    row.Status = MaterialRequirementStatus.Requested;
                    row.Note = JsonConvert.SerializeObject(rowNote);
                    _unitOfWork.MaterialRequirementRepository.Update(row);
                }
                planNote.PurchaseRequestId = purchaseRequest.Id;
                plan.Note = JsonConvert.SerializeObject(planNote);
                _unitOfWork.ProductionPlanRepository.Update(plan);
                await _unitOfWork.MaterialRequirementRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                return Result<MaterialRequirementResultModel>.Success((await DetailAsync(plan.Id))!, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " CreatePurchaseRequest: ", ex.ToString()));
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
            // T14.1: đề nghị mua bổ sung (CalculateTopUp), cũ → mới; tiền ước tính theo NCC gợi ý của dòng bổ sung.
            if (summary?.TopUpPurchaseRequestIds is { Count: > 0 } topUpIds)
            {
                var notes = rows.Select(r => ReadJson<RowNote>(r.Note)).Where(n => n != null).Select(n => n!).ToList();
                var topUps = await db.PurchaseRequest.AsNoTracking().Where(p => topUpIds.Contains(p.Id) && !p.IsDeleted).ToListAsync();
                foreach (var pr in topUps.OrderBy(p => topUpIds.IndexOf(p.Id)))
                {
                    result.ListTopUpPurchaseRequest.Add(new MaterialRequirementPurchaseRequestModel
                    {
                        Id = pr.Id,
                        RequestNumber = pr.RequestNumber ?? pr.RequestNo,
                        RequestDate = pr.RequestDate,
                        RequiredDate = pr.RequiredDate,
                        Status = pr.Status,
                        StatusName = PurchaseRequestStatus.Label(pr.Status),
                        LineCount = await db.PurchaseRequestLine.AsNoTracking().CountAsync(l => l.PurchaseRequestId == pr.Id && !l.IsDeleted),
                        EstimatedAmount = notes.Where(n => n.TopUpPurchaseRequestId == pr.Id).Sum(n => n.TopUpSupplier?.EstimatedAmount ?? 0m),
                    });
                }
            }
            return result;
        }

        /// <summary>
        /// T15 – in "Phiếu trước sản xuất" (.xlsx, ClosedXML) từ kết quả tính nhu cầu đã lưu, theo bố cục file thật <c>Phieu_Truoc sx.xlsx</c>
        /// (BUSINESS_DOCS.md – chỉ lấy cấu trúc, không chép dữ liệu): tên pháp nhân, PHIẾU TRƯỚC SẢN XUẤT, ngày SX / kế hoạch, số suất order (suất đơn đã chốt) /
        /// số suất thực tế (để trống ghi tay); bảng món × số suất; bảng nguyên liệu: cần / tồn xuất được / tồn bị chặn / tồn an toàn, Tình trạng hàng (Đủ /
        /// Thiếu – số thiếu, SL đề nghị mua, NCC gợi ý), cột ghi tay Tình trạng sơ chế (Đã SC / Chưa SC), Chất lượng hàng (Đạt / Không đạt – trả về), Ghi chú.
        /// Định lượng theo từng món (cột "Định mức (g)" của file thật) không lưu riêng – MRP gộp theo nguyên liệu (GAP-MRP-02).
        /// </summary>
        public async Task<IResult<byte[]>> ExportPreProduction(Guid productionPlanId)
        {
            try
            {
                var data = await DetailAsync(productionPlanId);
                if (data == null)
                {
                    return Result<byte[]>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
                }
                var company = await _unitOfWork.DbContext.Company.AsNoTracking().Where(c => !c.IsDeleted).OrderBy(c => c.CreatedDate).FirstOrDefaultAsync();
                const string brand = "#91AF42";
                const int lastCol = 15;

                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("Phieu truoc SX");
                ws.Cell(1, 1).Value = company?.Name ?? "DELICARE";
                ws.Cell(1, 1).Style.Font.Bold = true;
                ws.Cell(1, 11).Value = "Số suất order:";
                ws.Cell(1, 13).Value = data.TotalMeals;
                ws.Cell(2, 11).Value = "Số suất thực tế:";
                ws.Range(3, 1, 3, lastCol).Merge().Value = "PHIẾU TRƯỚC SẢN XUẤT";
                ws.Range(3, 1, 3, lastCol).Style.Font.SetBold().Font.SetFontSize(16).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                ws.Range(4, 1, 4, lastCol).Merge().Value = $"Ngày sản xuất: {Day(data.ProductionDate)} – Kế hoạch {data.PlanNumber}"
                    + (string.IsNullOrEmpty(data.WarehouseName) ? string.Empty : $" – Kho {data.WarehouseName}")
                    + (data.CalculatedAt != null ? $" – tính lúc {data.CalculatedAt.Value.ToString("HH:mm dd/MM/yyyy", CultureInfo.InvariantCulture)}" : string.Empty);
                ws.Range(4, 1, 4, lastCol).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center).Font.SetItalic();

                void Header(IXLRange range) => range.Style.Font.SetBold().Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(XLColor.FromHtml(brand))
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center).Alignment.SetVertical(XLAlignmentVerticalValues.Center).Alignment.SetWrapText(true);

                // Món × số suất.
                int row = 6;
                ws.Cell(row, 1).Value = "STT";
                ws.Range(row, 2, row, 4).Merge().Value = "Tên món";
                ws.Cell(row, 5).Value = "Số suất";
                ws.Range(row, 6, row, 8).Merge().Value = "Công thức";
                Header(ws.Range(row, 1, row, 8));
                int dishStart = row;
                int stt = 0;
                foreach (var dish in data.ListDish.OrderBy(d => d.DishName))
                {
                    row++;
                    ws.Cell(row, 1).Value = ++stt;
                    ws.Range(row, 2, row, 4).Merge().Value = dish.DishName ?? dish.DishCode ?? string.Empty;
                    ws.Cell(row, 5).Value = dish.Portions;
                    ws.Range(row, 6, row, 8).Merge().Value = dish.RecipeVersion is { Length: > 0 } v ? $"{v}" + (dish.Warning != null ? $" – {dish.Warning}" : string.Empty) : dish.Warning ?? string.Empty;
                }
                ws.Range(dishStart, 1, row, 8).Style.Border.SetInsideBorder(XLBorderStyleValues.Thin).Border.SetOutsideBorder(XLBorderStyleValues.Thin);

                // Nguyên liệu: số lượng / tình trạng hàng / sơ chế / chất lượng (2 dòng tiêu đề như mẫu).
                row += 2;
                int head = row;
                ws.Range(head, 1, head + 1, 1).Merge().Value = "STT";
                ws.Range(head, 2, head + 1, 2).Merge().Value = "Mã NL";
                ws.Range(head, 3, head + 1, 3).Merge().Value = "Tên nguyên liệu";
                ws.Range(head, 4, head + 1, 4).Merge().Value = "ĐVT";
                ws.Range(head, 5, head, 8).Merge().Value = "Số lượng";
                ws.Cell(head + 1, 5).Value = "SL cần";
                ws.Cell(head + 1, 6).Value = "Tồn xuất được";
                ws.Cell(head + 1, 7).Value = "Tồn bị chặn";
                ws.Cell(head + 1, 8).Value = "Tồn an toàn";
                ws.Range(head, 9, head, 12).Merge().Value = "Tình trạng hàng";
                ws.Cell(head + 1, 9).Value = "Đủ";
                ws.Cell(head + 1, 10).Value = "Thiếu\n(SL thiếu)";
                ws.Cell(head + 1, 11).Value = "SL đề nghị mua";
                ws.Cell(head + 1, 12).Value = "NCC gợi ý";
                ws.Range(head, 13, head, 14).Merge().Value = "Tình trạng sơ chế / chất lượng";
                ws.Cell(head + 1, 13).Value = "Đã SC / Chưa SC";
                ws.Cell(head + 1, 14).Value = "Đạt / Không đạt\n(trả về)";
                ws.Range(head, 15, head + 1, 15).Merge().Value = "Ghi chú";
                Header(ws.Range(head, 1, head + 1, lastCol));
                row = head + 1;
                stt = 0;
                foreach (var item in data.ListItem.OrderBy(i => i.IngredientName))
                {
                    row++;
                    ws.Cell(row, 1).Value = ++stt;
                    ws.Cell(row, 2).Value = item.IngredientCode ?? string.Empty;
                    ws.Cell(row, 3).Value = item.IngredientName ?? string.Empty;
                    ws.Cell(row, 4).Value = item.UnitName ?? string.Empty;
                    ws.Cell(row, 5).Value = item.RequiredQuantity;
                    ws.Cell(row, 6).Value = item.AvailableQuantity;
                    ws.Cell(row, 7).Value = item.BlockedQuantity;
                    ws.Cell(row, 8).Value = item.SafetyStock;
                    if (item.ShortageQuantity > 0)
                    {
                        ws.Cell(row, 10).Value = item.ShortageQuantity;
                        ws.Cell(row, 10).Style.Font.SetFontColor(XLColor.FromHtml("#F07825")).Font.SetBold();
                    }
                    else
                    {
                        ws.Cell(row, 9).Value = "✓";
                    }
                    if (item.PurchaseQuantity > 0)
                    {
                        ws.Cell(row, 11).Value = item.PurchaseQuantity;
                    }
                    ws.Cell(row, 12).Value = item.Supplier?.SupplierName ?? string.Empty;
                    ws.Cell(row, 15).Value = item.Warning ?? string.Empty;
                }
                ws.Range(head, 1, row, lastCol).Style.Border.SetInsideBorder(XLBorderStyleValues.Thin).Border.SetOutsideBorder(XLBorderStyleValues.Thin);
                ws.Range(head + 2, 5, row, 11).Style.NumberFormat.Format = "#,##0.####";
                ws.Range(head + 2, 9, row, 9).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                if (data.ListWarning.Count != 0)
                {
                    row += 2;
                    ws.Cell(row, 1).Value = "Cảnh báo:";
                    ws.Cell(row, 1).Style.Font.Bold = true;
                    foreach (string warning in data.ListWarning)
                    {
                        row++;
                        ws.Range(row, 1, row, lastCol).Merge().Value = "• " + warning;
                    }
                }
                row += 2;
                ws.Range(row, 1, row, 4).Merge().Value = "Thời gian cần có hàng: ........................";
                ws.Range(row, 9, row, 15).Merge().Value = "Người lập phiếu                    Bếp trưởng                    Thủ kho";
                ws.Range(row, 9, row, 15).Style.Font.SetBold().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

                ws.Column(1).Width = 6;
                ws.Column(2).Width = 12;
                ws.Column(3).Width = 30;
                ws.Column(4).Width = 8;
                for (int c = 5; c <= 11; c++)
                {
                    ws.Column(c).Width = 12;
                }
                ws.Column(12).Width = 24;
                ws.Column(13).Width = 14;
                ws.Column(14).Width = 14;
                ws.Column(15).Width = 30;
                ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
                ws.PageSetup.FitToPages(1, 0);

                using var stream = new MemoryStream();
                wb.SaveAs(stream);
                return Result<byte[]>.Success(stream.ToArray(), ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ExportPreProduction: ", ex.ToString()));
                throw;
            }
        }
    }
}
