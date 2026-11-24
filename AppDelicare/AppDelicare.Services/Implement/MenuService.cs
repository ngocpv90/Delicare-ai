using AutoMapper;
using Dapper;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Data.Share;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using AppDelicare.Services.Interface;
using Serilog;
using System.Net;

using AppDelicare.Services.Implement.Messaging;

namespace AppDelicare.Services.Implement
{
    public partial class MenuService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper, IRecipeService recipeService
        , IPricePolicyService pricePolicyService, IClock clock, IOptions<BusinessRulesOptions> rules
        , IAuditChangeService auditChangeService, INotificationService notifications, IDocumentNumberService documentNumberService) : IMenuService
    {
        private readonly IDocumentNumberService _documentNumberService = documentNumberService;
        /// <summary>MA-1.3 (D-63): mã vừa cấp khi thêm mới – báo kèm trong thông báo thành công.</summary>
        private string? _createdCode;
        private readonly INotificationService _notifications = notifications; // BOT-1.7
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;
        private readonly IRecipeService _recipeService = recipeService;
        private readonly IPricePolicyService _pricePolicyService = pricePolicyService;
        private readonly IClock _clock = clock;
        private readonly BusinessRulesOptions _rules = rules.Value;

        public async Task<IResults<MenuViewModel>> GetAll(MenuSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@CompanyId", request.CompanyId);
                dynamicParameters.Add("@Code", request.Code);
                dynamicParameters.Add("@Name", request.Name);
                dynamicParameters.Add("@MenuType", request.MenuType);
                dynamicParameters.Add("@VersionCode", request.VersionCode);
                dynamicParameters.Add("@EffectiveFrom", request.EffectiveFrom);
                dynamicParameters.Add("@EffectiveTo", request.EffectiveTo);
                dynamicParameters.Add("@Status", request.Status);
                //dynamicParameters.Add("@Note", request.Note);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<MenuViewModel>("uspMenu_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<MenuViewModel>
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

        public IResults<Menu> GetAllData()
        {
            try
            {
                var list = _unitOfWork.MenuRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<Menu>
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

        public async Task<IResult<bool>> Save(MenuRequest request, string typeSave)
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
                        : _createdCode != null ? string.Format(ConstantHelper.Message.SaveSuccessWithCode, "thực đơn", _createdCode)
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

        private async Task<string> Insert(MenuRequest request, string typeSave)
        {
            if (request.EffectiveTo != null && request.EffectiveTo.Value.Date < request.EffectiveFrom.Date)
            {
                return ConstantHelper.Message.EffectiveRangeInvalid;
            }
            var insert = _mapper.Map<Menu>(request);
            // MA-1.3 (D-63): mã thực đơn NU-YYMMDD-NNN (ngày = ngày bắt đầu áp dụng) máy chủ tự sinh, bỏ qua giá trị gửi lên.
            insert.Code = await _documentNumberService.NextAsync(DocumentKind.Menu, request.EffectiveFrom.Date);
            _createdCode = insert.Code;
            // T13 (STATUS_CATALOG): thực đơn mới luôn là Nháp; trạng thái chỉ đổi qua Approve / Publish / Expire.
            insert.Status = MenuStatus.Draft;
            insert.VersionNo = request.VersionNo is > 0 ? request.VersionNo.Value : 1;
            _unitOfWork.MenuRepository.Add(insert);
            await _unitOfWork.MenuRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Update(MenuRequest request, string typeSave)
        {
            var exist = _unitOfWork.MenuRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (request.Id == null || exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // MA-1.3 (D-63): mã không đổi sau khi cấp – bỏ qua giá trị gửi lên.
            request.Code = exist.Code;
            // T13: chỉ sửa khi Nháp / Đã duyệt; không đổi trạng thái qua Update.
            if (exist.Status is not (null or MenuStatus.Draft or MenuStatus.Approved))
            {
                return string.Format(ConstantHelper.Message.StatusActionInvalid, "sửa", "thực đơn", "Nháp / Đã duyệt", MenuStatus.Label(exist.Status));
            }
            if (request.EffectiveTo != null && request.EffectiveTo.Value.Date < request.EffectiveFrom.Date)
            {
                return ConstantHelper.Message.EffectiveRangeInvalid;
            }
            var keep = (exist.Status, exist.VersionNo);
            var update = _mapper.Map(request, exist);
            (update.Status, update.VersionNo) = keep;
            _unitOfWork.MenuRepository.Update(update);
            await _unitOfWork.MenuRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Delete(Guid? id, string typeSave)
        {
            if (id == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // T07: ReferenceGuard (quan hệ logic, truy vấn có tham số) thay SQL động uspSystem_ExecuteQuery (A10).
            if (await _auditChangeService.IsReferencedAsync(nameof(Menu), (Guid)id))
            {
                return ConstantHelper.Message.DeleteExistFail;
            }
            var delete = _unitOfWork.MenuRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            if (delete.Status is not (null or MenuStatus.Draft))
            {
                return string.Format(ConstantHelper.Message.StatusActionInvalid, "xóa", "thực đơn", "Nháp", MenuStatus.Label(delete.Status));
            }
            _unitOfWork.MenuRepository.Update(delete);
            await _unitOfWork.MenuRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        /// <summary>
        /// BE2.1 (spec 6.2 GET menus, Catalog 82): thực đơn + số dòng / số món, số khách được phân công (MenuAssignment còn hiệu lực, không Inactive – khách khác nhau),
        /// suất đã đặt <c>orderedMeals</c> = Σ OrderLine.Quantity có MenuId này (đơn chưa xóa, không Hủy). Lọc trạng thái, hiệu lực tại ngày, tìm mã / tên.
        /// </summary>
        public async Task<IResults<MenuListItemModel>> GetList(MenuListRequest request)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                var query = db.Menu.AsNoTracking().Where(m => !m.IsDeleted);
                if (!string.IsNullOrWhiteSpace(request.Status))
                {
                    query = query.Where(m => m.Status == request.Status);
                }
                if (request.EffectiveOn is { } on)
                {
                    DateTime d = on.Date;
                    query = query.Where(m => (m.EffectiveFrom == null || m.EffectiveFrom <= d) && (m.EffectiveTo == null || m.EffectiveTo >= d));
                }
                if (!string.IsNullOrWhiteSpace(request.SearchText))
                {
                    string q = request.SearchText.Trim();
                    query = query.Where(m => (m.Code != null && m.Code.Contains(q)) || (m.Name != null && m.Name.Contains(q)));
                }
                var menus = await query.OrderByDescending(m => m.EffectiveFrom).ThenBy(m => m.Code).ToListAsync();
                var ids = menus.Select(m => m.Id).ToList();
                var lines = await db.MenuLine.AsNoTracking().Where(l => ids.Contains(l.MenuId) && !l.IsDeleted).Select(l => new { l.MenuId, l.DishId }).ToListAsync();
                DateTime? at = request.EffectiveOn?.Date;
                var assignments = await db.MenuAssignment.AsNoTracking()
                    .Where(a => ids.Contains(a.MenuId) && !a.IsDeleted && (a.Status == null || a.Status != "Inactive")
                        && (at == null || ((a.EffectiveFrom == null || a.EffectiveFrom <= at) && (a.EffectiveTo == null || a.EffectiveTo >= at))))
                    .Select(a => new { a.MenuId, a.CustomerId }).ToListAsync();
                var activeOrders = db.Order.Where(o => !o.IsDeleted && o.Status != OrderStatus.Cancelled).Select(o => o.Id);
                var ordered = await db.OrderLine.AsNoTracking()
                    .Where(l => !l.IsDeleted && l.MenuId != null && ids.Contains(l.MenuId.Value) && activeOrders.Contains(l.OrderId))
                    .GroupBy(l => l.MenuId!.Value).Select(g => new { MenuId = g.Key, Meals = g.Sum(l => l.Quantity) }).ToListAsync();
                var list = menus.Select(m => new MenuListItemModel
                {
                    Menu = m,
                    StatusName = MenuStatus.Label(m.Status),
                    LineCount = lines.Count(l => l.MenuId == m.Id),
                    DishCount = lines.Where(l => l.MenuId == m.Id).Select(l => l.DishId).Distinct().Count(),
                    AssignedCustomerCount = assignments.Where(a => a.MenuId == m.Id).Select(a => a.CustomerId).Distinct().Count(),
                    OrderedMeals = ordered.FirstOrDefault(o => o.MenuId == m.Id)?.Meals ?? 0m,
                }).ToList();
                return Results<MenuListItemModel>.Success(list, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetList: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<Menu>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.MenuRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<Menu>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<Menu>
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

        // ============================================================ T13 + T46B.1 – thực đơn tuần (spec 6.2)
        /// <summary>Thứ tự và nhãn buổi ăn (BUSINESS_RULES mục 0; mã theo ReferenceLists:MealType).</summary>
        private static readonly string[] MealOrder = ["SANG", "TRUA", "TOI", "DEM"];

        public static string MealLabel(string? mealType) => (mealType ?? string.Empty).ToUpperInvariant() switch
        {
            "SANG" => "SÁNG",
            "TRUA" => "TRƯA",
            "TOI" => "TỐI",
            "DEM" => "ĐÊM",
            _ => mealType ?? string.Empty,
        };

        public static string DayLabel(int dayOfWeek) => dayOfWeek == 7 ? "Chủ nhật" : $"Thứ {dayOfWeek + 1}";

        public static int MealRank(string? mealType)
        {
            int i = Array.FindIndex(MealOrder, m => m.Equals(mealType, StringComparison.OrdinalIgnoreCase));
            return i < 0 ? MealOrder.Length : i;
        }

        /// <summary>.NET DayOfWeek (CN = 0) → thứ của MenuLine (1 = Thứ 2 … 7 = CN).</summary>
        public static int MenuDayOf(DateTime date) => date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;

        /// <summary>Giá vốn NVL / suất của món theo công thức đang khóa (IsActive + Locked); null = chưa có công thức.</summary>
        private async Task<Dictionary<Guid, (decimal? Cost, bool Complete)>> DishCostsAsync(IEnumerable<Guid> dishIds, DateTime on)
        {
            var ids = dishIds.Distinct().ToList();
            var recipes = await _unitOfWork.RecipeRepository.GetAll().AsNoTracking()
                .Where(r => ids.Contains(r.DishId) && !r.IsDeleted && r.IsActive && r.Status == RecipeStatus.Locked)
                .Select(r => new { r.Id, r.DishId, r.EffectiveFrom }).ToListAsync();
            var result = new Dictionary<Guid, (decimal? Cost, bool Complete)>();
            foreach (Guid dishId in ids)
            {
                var recipe = recipes.Where(r => r.DishId == dishId).OrderByDescending(r => r.EffectiveFrom).FirstOrDefault();
                if (recipe == null)
                {
                    result[dishId] = (null, false);
                    continue;
                }
                var cost = await _recipeService.CalculateCostAsync(recipe.Id, 1, on);
                result[dishId] = (cost?.CostPerMeal, cost?.Complete ?? false);
            }
            return result;
        }

        /// <summary>
        /// Spec 6.2 calendar: pivot MenuLine theo thứ (1–7) × (loại suất, lựa chọn = SequenceNo, nhóm món), kèm giá vốn / suất từng món
        /// và tổng theo (loại suất, lựa chọn, thứ). FC% = giá vốn / giá bán × 100 (PROPOSED – BA/PO CONFIRM) khi biết giá bán:
        /// <paramref name="sellingPrice"/> nhập tay, hoặc tra giá (PricePolicy/Resolve) theo <paramref name="customerId"/> (+ hợp đồng) và loại suất.
        /// </summary>
        public async Task<IResult<MenuCalendarModel>> GetCalendar(Guid id, string? mealType, decimal? sellingPrice, Guid? customerId, Guid? contractId, DateTime? on)
        {
            try
            {
                var menu = await _unitOfWork.MenuRepository.GetAll().AsNoTracking().FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted);
                if (menu == null)
                {
                    return Result<MenuCalendarModel>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
                }
                DateTime day = (on ?? menu.EffectiveFrom ?? _clock.Today).Date;
                var lines = await _unitOfWork.MenuLineRepository.GetAll().AsNoTracking()
                    .Where(l => l.MenuId == id && !l.IsDeleted && (mealType == null || l.MealType == mealType)).ToListAsync();
                var dishIds = lines.Select(l => l.DishId).Distinct().ToList();
                var dishes = await _unitOfWork.DishRepository.GetAll().AsNoTracking().Where(d => dishIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id);
                var categoryIds = dishes.Values.Select(d => d.DishCategoryId ?? d.CategoryId).Where(c => c != null).Select(c => c!.Value).Distinct().ToList();
                var categories = await _unitOfWork.DishCategoryRepository.GetAll().AsNoTracking().Where(c => categoryIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name);
                var costs = await DishCostsAsync(dishIds, day);

                var model = new MenuCalendarModel
                {
                    MenuId = menu.Id, Code = menu.Code, Name = menu.Name, Status = menu.Status, EffectiveFrom = menu.EffectiveFrom, EffectiveTo = menu.EffectiveTo,
                    MealType = mealType, FoodCostTargetPct = _rules.KpiIngredientToRevenueMaxPct,
                };
                if (sellingPrice is > 0)
                {
                    model.SellingPrice = sellingPrice;
                    model.SellingPriceSource = "INPUT";
                }
                else if (customerId is { } cid && mealType != null)
                {
                    var price = await _pricePolicyService.Resolve(new PriceResolveRequest { CustomerId = cid, ContractId = contractId, MealType = mealType, Date = day });
                    if (price.ResponseCode == (int)HttpStatusCode.OK && price.Data != null)
                    {
                        model.SellingPrice = price.Data.UnitPrice;
                        model.SellingPriceSource = price.Data.Source;
                    }
                }

                var cells = lines.Select(l =>
                {
                    dishes.TryGetValue(l.DishId, out var dish);
                    Guid? categoryId = dish?.DishCategoryId ?? dish?.CategoryId;
                    var (cost, complete) = costs.GetValueOrDefault(l.DishId);
                    return new
                    {
                        l.MealType, Option = l.SequenceNo is > 0 ? l.SequenceNo.Value : 1, Day = l.DayOfWeek ?? 0, CategoryId = categoryId,
                        Dish = new MenuCalendarDishModel
                        {
                            MenuLineId = l.Id, DishId = l.DishId, DishCode = dish?.Code, DishName = dish?.Name, PortionQty = l.PortionQty, SortOrder = l.SortOrder,
                            CostPerMeal = cost, CostComplete = complete,
                        },
                    };
                }).Where(c => c.Day is >= 1 and <= 7).ToList();

                model.ListRow = cells
                    .GroupBy(c => (c.MealType, c.Option, c.CategoryId))
                    .Select(g => new MenuCalendarRowModel
                    {
                        MealType = g.Key.MealType, OptionNo = g.Key.Option, DishCategoryId = g.Key.CategoryId,
                        DishCategoryName = g.Key.CategoryId is { } c ? categories.GetValueOrDefault(c) : null,
                        Cells = g.GroupBy(x => x.Day).ToDictionary(d => d.Key, d => d.Select(x => x.Dish).OrderBy(x => x.SortOrder).ThenBy(x => x.DishName).ToList()),
                    })
                    .OrderBy(r => MealRank(r.MealType)).ThenBy(r => r.OptionNo).ThenBy(r => r.DishCategoryName).ToList();

                model.ListTotal = cells
                    .GroupBy(c => (c.MealType, c.Option, c.Day))
                    .Select(g =>
                    {
                        decimal cost = Math.Round(g.Sum(x => x.Dish.CostPerMeal ?? 0m), 2);
                        decimal? fc = model.SellingPrice is > 0 ? Math.Round(cost / model.SellingPrice.Value * 100m, 2) : null;
                        return new MenuCalendarTotalModel
                        {
                            MealType = g.Key.MealType, OptionNo = g.Key.Option, DayOfWeek = g.Key.Day, DishCount = g.Count(),
                            CostPerMeal = cost, CostComplete = g.All(x => x.Dish.CostComplete), FoodCostPercent = fc,
                            FoodCostOverTarget = fc != null && fc > _rules.KpiIngredientToRevenueMaxPct,
                        };
                    })
                    .OrderBy(t => MealRank(t.MealType)).ThenBy(t => t.OptionNo).ThenBy(t => t.DayOfWeek).ToList();
                return Result<MenuCalendarModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetCalendar: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>Đổi trạng thái thực đơn trong 1 transaction: kiểm trạng thái nguồn + điều kiện, ghi Note nếu có.</summary>
        private async Task<IResult<bool>> ChangeStatus(MenuActionRequest request, string[] from, string to, string action, Func<Menu, Task<string>>? check)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var menu = _unitOfWork.MenuRepository.GetSingle(m => m.Id == request.Id && !m.IsDeleted);
                string error = menu == null ? ConstantHelper.Message.RecordNotFoundMessage
                    : !from.Contains(menu.Status ?? MenuStatus.Draft)
                        ? string.Format(ConstantHelper.Message.StatusActionInvalid, action, "thực đơn", string.Join(" / ", from.Select(MenuStatus.Label)), MenuStatus.Label(menu.Status))
                    : check != null ? await check(menu) : string.Empty;
                if (!string.IsNullOrEmpty(error))
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<bool>.Fail(error, false);
                }
                menu!.Status = to;
                if (!string.IsNullOrWhiteSpace(request.Note))
                {
                    menu.Note = string.IsNullOrWhiteSpace(menu.Note) ? request.Note.Trim() : $"{menu.Note}\n{request.Note.Trim()}";
                }
                _unitOfWork.MenuRepository.Update(menu);
                await _unitOfWork.MenuRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                return Result<bool>.Success(true, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ChangeStatus: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        /// <summary>Spec 6.2: Draft → Approved (≥ 1 món).</summary>
        public Task<IResult<bool>> Approve(MenuActionRequest request)
            => ChangeStatus(request, [MenuStatus.Draft], MenuStatus.Approved, "duyệt", async menu =>
                await _unitOfWork.MenuLineRepository.GetAll().AnyAsync(l => l.MenuId == menu.Id && !l.IsDeleted) ? string.Empty : ConstantHelper.Message.MenuNeedsLine);

        /// <summary>
        /// Spec 6.2: Approved → Published. Mỗi thứ trong hiệu lực (hiệu lực dưới 7 ngày: các thứ trong khoảng; còn lại: đủ 7 thứ) phải có ≥ 1 món
        /// cho mỗi loại suất đã gán (MenuAssignment Active; chưa gán → các loại suất có trong thực đơn). Đẩy Zalo cho khách: DEFERRED(PILOT).
        /// </summary>
        public Task<IResult<bool>> Publish(MenuActionRequest request)
            => ChangeStatus(request, [MenuStatus.Approved], MenuStatus.Published, "công bố", async menu =>
            {
                var lines = await _unitOfWork.MenuLineRepository.GetAll().AsNoTracking().Where(l => l.MenuId == menu.Id && !l.IsDeleted)
                    .Select(l => new { l.MealType, l.DayOfWeek }).ToListAsync();
                if (lines.Count == 0)
                {
                    return ConstantHelper.Message.MenuNeedsLine;
                }
                // ORD-OPT-1.2 (D-74 f): không cho 2 thực đơn chung Đã công bố chồng hiệu lực.
                if (MenuScope.IsCommon(menu.MenuType))
                {
                    DateTime from = menu.EffectiveFrom?.Date ?? DateTime.MinValue, to = menu.EffectiveTo?.Date ?? DateTime.MaxValue.Date;
                    var clash = await _unitOfWork.MenuRepository.GetAll().AsNoTracking()
                        .Where(m => !m.IsDeleted && m.Id != menu.Id && m.MenuType == MenuScope.Common && m.Status == MenuStatus.Published
                            && (m.EffectiveFrom == null || m.EffectiveFrom <= to) && (m.EffectiveTo == null || m.EffectiveTo >= from))
                        .Select(m => m.Code ?? m.Name).FirstOrDefaultAsync();
                    if (clash != null)
                    {
                        return string.Format(ConstantHelper.Message.MenuCommonOverlap, clash);
                    }
                }
                var mealTypes = await _unitOfWork.MenuAssignmentRepository.GetAll().AsNoTracking()
                    .Where(a => a.MenuId == menu.Id && !a.IsDeleted && (a.Status == null || a.Status == "Active") && a.MealType != null)
                    .Select(a => a.MealType!).Distinct().ToListAsync();
                if (mealTypes.Count == 0)
                {
                    mealTypes = lines.Where(l => l.MealType != null).Select(l => l.MealType!).Distinct().ToList();
                }
                var days = Enumerable.Range(1, 7).ToList();
                if (menu.EffectiveFrom is { } f && menu.EffectiveTo is { } t && (t.Date - f.Date).TotalDays < 6)
                {
                    days = Enumerable.Range(0, (int)(t.Date - f.Date).TotalDays + 1).Select(i => MenuDayOf(f.Date.AddDays(i))).Distinct().OrderBy(d => d).ToList();
                }
                var missing = mealTypes.OrderBy(MealRank)
                    .Select(m => (Meal: m, Days: days.Where(d => !lines.Any(l => l.MealType == m && l.DayOfWeek == d)).ToList()))
                    .Where(x => x.Days.Count > 0)
                    .Select(x => $"{MealLabel(x.Meal)}: {string.Join(", ", x.Days.Select(DayLabel))}").ToList();
                if (missing.Count == 0)
                {
                    // BOT-1.7 (Catalog 85): MENU_PUBLISHED (tổng hợp) → kế hoạch, kinh doanh, bếp.
                    await BusinessNotifier.ToMenusAsync(_notifications, _unitOfWork.DbContext, NotificationCatalog.MenuPublished, ["#/menu-menu", "#/crm-order", "#/prod-batch"],
                        new { title = menu.Name ?? menu.Code, detail = $"Áp dụng {menu.EffectiveFrom:dd/MM/yyyy}" + (menu.EffectiveTo is { } to ? $" – {to:dd/MM/yyyy}" : string.Empty) },
                        $"{NotificationCatalog.MenuPublished}:{menu.Id:N}");
                }
                return missing.Count == 0 ? string.Empty : string.Format(ConstantHelper.Message.MenuPublishMissingDays, string.Join("; ", missing));
            });

        /// <summary>Spec 6.2: Approved / Published → Expired.</summary>
        public Task<IResult<bool>> Expire(MenuActionRequest request)
            => ChangeStatus(request, [MenuStatus.Approved, MenuStatus.Published], MenuStatus.Expired, "cho hết hiệu lực", null);

        /// <summary>Spec 6.2 clone: thực đơn nháp mới (VersionNo = 1, mã mới) + sao chép mọi dòng.</summary>
        public async Task<IResult<Menu>> Clone(MenuCloneRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var source = await _unitOfWork.MenuRepository.GetAll().AsNoTracking().FirstOrDefaultAsync(m => m.Id == request.Id && !m.IsDeleted);
                string error = source == null ? ConstantHelper.Message.RecordNotFoundMessage
                    : request.EffectiveTo != null && request.EffectiveTo.Value.Date < request.EffectiveFrom.Date ? ConstantHelper.Message.EffectiveRangeInvalid
                    : string.Empty;
                if (!string.IsNullOrEmpty(error))
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<Menu>.Fail(error);
                }
                // MA-1.3 (D-63): bản sao lấy mã mới NU-YYMMDD-NNN theo ngày bắt đầu áp dụng; Code gửi lên bị bỏ qua.
                string code = await _documentNumberService.NextAsync(DocumentKind.Menu, request.EffectiveFrom.Date);
                var copy = new Menu
                {
                    Id = Guid.NewGuid(), Code = code, Name = request.Name ?? source!.Name, VersionNo = 1, EffectiveFrom = request.EffectiveFrom.Date,
                    EffectiveTo = request.EffectiveTo?.Date, Status = MenuStatus.Draft, CompanyId = source!.CompanyId, MenuType = source.MenuType,
                    Note = $"Sao chép từ {source.Code}",
                };
                _unitOfWork.MenuRepository.Add(copy);
                foreach (var l in await _unitOfWork.MenuLineRepository.GetAll().AsNoTracking().Where(l => l.MenuId == source.Id && !l.IsDeleted).ToListAsync())
                {
                    _unitOfWork.MenuLineRepository.Add(new MenuLine
                    {
                        Id = Guid.NewGuid(), MenuId = copy.Id, DishId = l.DishId, MealType = l.MealType, SequenceNo = l.SequenceNo, PortionQty = l.PortionQty,
                        IsRequired = l.IsRequired, DayOfWeek = l.DayOfWeek, SortOrder = l.SortOrder, StandardQuantity = l.StandardQuantity, Note = l.Note,
                    });
                }
                await _unitOfWork.MenuRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
                _unitOfWork.CommitTransaction();
                return Result<Menu>.Success(copy, string.Format(ConstantHelper.Message.SaveSuccessWithCode, "thực đơn", copy.Code));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Clone: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        /// <summary>
        /// T46B.1 – in "Thực đơn gửi khách" (.xlsx, ClosedXML) theo bố cục mẫu thật (sheet "4. Menu_Sent", BUSINESS_DOCS.md):
        /// tên pháp nhân (bảng Company: Menu.CompanyId, không có thì công ty đầu tiên), tiêu đề, dự án / thực đơn + hiệu lực,
        /// bảng Thứ × buổi (SÁNG / TRƯA / TỐI / ĐÊM), mỗi buổi 2 cột "Món" + "Trọng lượng thành phẩm" (Dish.StandardServingSize).
        /// Nhiều lựa chọn trong 1 buổi → tiền tố "LC n:". Không in giá vốn / giá bán.
        /// </summary>
        public async Task<IResult<byte[]>> ExportForCustomer(Guid id, string? project)
        {
            try
            {
                var menu = await _unitOfWork.MenuRepository.GetAll().AsNoTracking().FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted);
                if (menu == null)
                {
                    return Result<byte[]>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
                }
                var lines = await _unitOfWork.MenuLineRepository.GetAll().AsNoTracking().Where(l => l.MenuId == id && !l.IsDeleted && l.DayOfWeek >= 1 && l.DayOfWeek <= 7).ToListAsync();
                if (lines.Count == 0)
                {
                    return Result<byte[]>.Fail(ConstantHelper.Message.MenuNeedsLine);
                }
                var dishIds = lines.Select(l => l.DishId).Distinct().ToList();
                var dishes = await _unitOfWork.DishRepository.GetAll().AsNoTracking().Where(d => dishIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id);
                var company = menu.CompanyId is { } companyId
                    ? await _unitOfWork.CompanyRepository.GetAll().AsNoTracking().FirstOrDefaultAsync(c => c.Id == companyId && !c.IsDeleted)
                    : null;
                company ??= await _unitOfWork.CompanyRepository.GetAll().AsNoTracking().Where(c => !c.IsDeleted).OrderBy(c => c.CreatedDate).FirstOrDefaultAsync();

                var meals = lines.Select(l => l.MealType ?? string.Empty).Distinct().OrderBy(MealRank).ToList();
                bool multiOption = lines.GroupBy(l => (l.MealType, l.DayOfWeek)).Any(g => g.Select(l => l.SequenceNo ?? 1).Distinct().Count() > 1);
                const string brand = "#91AF42";

                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("Thuc don gui khach");
                int lastCol = 1 + meals.Count * 2;
                ws.Cell(1, 1).Value = company?.Name ?? "DELICARE";
                ws.Cell(1, 1).Style.Font.Bold = true;
                ws.Cell(2, 1).Value = company?.Address ?? string.Empty;
                ws.Cell(3, 1).Value = string.Join(" · ", new[] { company?.Phone, company?.Email }.Where(x => !string.IsNullOrWhiteSpace(x)));
                ws.Range(5, 1, 5, lastCol).Merge().Value = "THỰC ĐƠN SUẤT ĂN";
                ws.Range(5, 1, 5, lastCol).Style.Font.SetBold().Font.SetFontSize(14).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                ws.Cell(6, 1).Value = "Dự án: " + (project ?? menu.Name ?? string.Empty);
                ws.Cell(7, 1).Value = $"Thực đơn: {menu.Code} – áp dụng từ {menu.EffectiveFrom:dd/MM/yyyy}" + (menu.EffectiveTo != null ? $" đến {menu.EffectiveTo:dd/MM/yyyy}" : string.Empty);

                int header = 9;
                ws.Range(header, 1, header + 1, 1).Merge().Value = "Thứ";
                for (int i = 0; i < meals.Count; i++)
                {
                    int col = 2 + i * 2;
                    ws.Range(header, col, header, col + 1).Merge().Value = MealLabel(meals[i]);
                    ws.Cell(header + 1, col).Value = "Món";
                    ws.Cell(header + 1, col + 1).Value = "Trọng lượng thành phẩm";
                }
                var head = ws.Range(header, 1, header + 1, lastCol);
                head.Style.Font.SetBold().Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(XLColor.FromHtml(brand))
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center).Alignment.SetVertical(XLAlignmentVerticalValues.Center).Alignment.SetWrapText(true);

                int row = header + 2;
                for (int day = 1; day <= 7; day++)
                {
                    var perMeal = meals.Select(m => lines.Where(l => (l.MealType ?? string.Empty) == m && l.DayOfWeek == day)
                        .OrderBy(l => l.SequenceNo ?? 1).ThenBy(l => l.SortOrder).ToList()).ToList();
                    int height = Math.Max(1, perMeal.Max(p => p.Count));
                    ws.Range(row, 1, row + height - 1, 1).Merge().Value = DayLabel(day);
                    ws.Range(row, 1, row + height - 1, 1).Style.Font.SetBold().Alignment.SetVertical(XLAlignmentVerticalValues.Center);
                    for (int i = 0; i < meals.Count; i++)
                    {
                        for (int k = 0; k < perMeal[i].Count; k++)
                        {
                            var l = perMeal[i][k];
                            dishes.TryGetValue(l.DishId, out var dish);
                            ws.Cell(row + k, 2 + i * 2).Value = (multiOption ? $"LC {l.SequenceNo ?? 1}: " : string.Empty) + (dish?.Name ?? string.Empty);
                            decimal? weight = dish?.StandardServingSize ?? dish?.StandardPortion;
                            if (weight is > 0)
                            {
                                ws.Cell(row + k, 3 + i * 2).Value = $"{weight:0.##} g";
                            }
                        }
                    }
                    row += height;
                }
                ws.Range(header, 1, row - 1, lastCol).Style.Border.SetInsideBorder(XLBorderStyleValues.Thin).Border.SetOutsideBorder(XLBorderStyleValues.Thin);
                ws.Column(1).Width = 12;
                for (int i = 0; i < meals.Count; i++)
                {
                    ws.Column(2 + i * 2).Width = 32;
                    ws.Column(3 + i * 2).Width = 14;
                }
                ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
                ws.PageSetup.FitToPages(1, 0);

                using var stream = new MemoryStream();
                wb.SaveAs(stream);
                return Result<byte[]>.Success(stream.ToArray(), ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ExportForCustomer: ", ex.ToString()));
                throw;
            }
        }
    }
}
