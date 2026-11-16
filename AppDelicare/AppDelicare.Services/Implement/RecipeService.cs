using AutoMapper;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Data.Share;
using Newtonsoft.Json;
using Microsoft.Extensions.Options;
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

namespace AppDelicare.Services.Implement
{
    public class RecipeService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService
        , IUomConversionService uomConversionService, IClock clock, ICurrentActor currentActor, IOptions<BusinessRulesOptions> rules
        , IDocumentNumberService documentNumberService, IApprovalRequestService approvalRequestService) : IRecipeService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;
        private readonly IUomConversionService _uomConversionService = uomConversionService;
        private readonly IClock _clock = clock;
        private readonly ICurrentActor _currentActor = currentActor;
        private readonly BusinessRulesOptions _rules = rules.Value;
        private readonly IDocumentNumberService _documentNumberService = documentNumberService;
        private readonly IApprovalRequestService _approvalRequestService = approvalRequestService;

        public async Task<IResults<RecipeViewModel>> GetAll(RecipeSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@DishId", request.DishId);
                dynamicParameters.Add("@VersionCode", request.VersionCode);
                //dynamicParameters.Add("@YieldQuantity", request.YieldQuantity);
                dynamicParameters.Add("@YieldUnitId", request.YieldUnitId);
                dynamicParameters.Add("@EffectiveFrom", request.EffectiveFrom);
                dynamicParameters.Add("@EffectiveTo", request.EffectiveTo);
                //dynamicParameters.Add("@IsActive", request.IsActive);
                //dynamicParameters.Add("@Instructions", request.Instructions);
                dynamicParameters.Add("@Status", request.Status);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<RecipeViewModel>("uspRecipe_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<RecipeViewModel>
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

        public IResults<Recipe> GetAllData()
        {
            try
            {
                var list = _unitOfWork.RecipeRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<Recipe>
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

        public async Task<IResult<bool>> Save(RecipeRequest request, string typeSave)
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

        /// <summary>
        /// T45R (spec 5.2): mỗi phiên bản công thức là một Recipe cùng DishId (không có bảng RecipeVersion). Thêm mới = tạo phiên bản Draft:
        /// VersionNo = max + 1, VersionCode = {mã món}-V{n} nếu trống; chặn khi món đang có bản Draft / PendingApproval.
        /// Trạng thái / hiệu lực chỉ đổi qua Submit / Approve / Reject / Lock.
        /// </summary>
        private async Task<string> Insert(RecipeRequest request, string typeSave)
        {
            var dish = await _unitOfWork.DishRepository.GetSingleAsync(x => x.Id == request.DishId && !x.IsDeleted);
            if (dish == null)
            {
                return string.Format(ConstantHelper.Message.NotExist, "Món");
            }
            var existing = _unitOfWork.RecipeRepository.GetAll().Where(x => x.DishId == request.DishId && !x.IsDeleted).ToList();
            if (existing.Any(x => x.Status is RecipeStatus.Draft or RecipeStatus.PendingApproval))
            {
                return ConstantHelper.Message.RecipeHasOpenVersion;
            }
            var insert = _mapper.Map<Recipe>(request);
            insert.VersionNo = existing.Count == 0 ? 1 : existing.Max(x => x.VersionNo) + 1;
            insert.VersionCode = string.IsNullOrWhiteSpace(request.VersionCode) ? $"{dish.Code}-V{insert.VersionNo}" : request.VersionCode;
            insert.Status = RecipeStatus.Draft;
            insert.IsActive = false;
            // Hiệu lực chỉ đặt khi khóa (Lock). RecipeRequest.EffectiveFrom là DateTime không null → bỏ giá trị mặc định 0001-01-01.
            insert.EffectiveFrom = request.EffectiveFrom == default ? null : request.EffectiveFrom;
            insert.EffectiveTo = null;
            insert.StandardOutputQty ??= _rules.RecipeStandardServings;
            _unitOfWork.RecipeRepository.Add(insert);
            await _unitOfWork.RecipeRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        /// <summary>T45R: chỉ sửa khi Draft; không đổi món, trạng thái, hiệu lực, số phiên bản qua Update.</summary>
        private async Task<string> Update(RecipeRequest request, string typeSave)
        {
            var exist = _unitOfWork.RecipeRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (request.Id == null || exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            if (exist.Status != RecipeStatus.Draft)
            {
                return InvalidStatus(exist, RecipeStatus.Draft, "sửa công thức");
            }
            var keep = (exist.DishId, exist.Status, exist.IsActive, exist.VersionNo, exist.EffectiveFrom, exist.EffectiveTo);
            var update = _mapper.Map(request, exist);
            (update.DishId, update.Status, update.IsActive, update.VersionNo, update.EffectiveFrom, update.EffectiveTo) = keep;
            _unitOfWork.RecipeRepository.Update(update);
            await _unitOfWork.RecipeRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        /// <summary>T45R: chỉ xóa bản Draft (xóa dòng định lượng trước – ReferenceGuard).</summary>
        private async Task<string> Delete(Guid? id, string typeSave)
        {
            if (id == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            var delete = _unitOfWork.RecipeRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            if (delete.Status != RecipeStatus.Draft)
            {
                return InvalidStatus(delete, RecipeStatus.Draft, "xóa công thức");
            }
            // T07: ReferenceGuard (quan hệ logic, truy vấn có tham số) thay SQL động uspSystem_ExecuteQuery (A10).
            if (await _auditChangeService.IsReferencedAsync(nameof(Recipe), (Guid)id))
            {
                return ConstantHelper.Message.DeleteExistFail;
            }
            _unitOfWork.RecipeRepository.Update(delete);
            await _unitOfWork.RecipeRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        public async Task<IResult<Recipe>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.RecipeRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<Recipe>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<Recipe>
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

        private static string InvalidStatus(Recipe r, string expected, string action)
            => string.Format(ConstantHelper.Message.InvalidStatusForAction, action, expected, r.Status ?? "(trống)");

        /// <summary>T45R (spec 5.2): các phiên bản công thức của món (mới nhất trước) kèm dòng định lượng và giá vốn NVL / suất.</summary>
        public async Task<IResults<RecipeVersionModel>> GetByDishId(Guid dishId)
        {
            try
            {
                var recipes = await _unitOfWork.RecipeRepository.GetAll().AsNoTracking().Where(x => x.DishId == dishId && !x.IsDeleted)
                    .OrderByDescending(x => x.VersionNo).ToListAsync();
                var ids = recipes.Select(x => x.Id).ToList();
                var lines = await _unitOfWork.RecipeLineRepository.GetAll().AsNoTracking().Where(l => ids.Contains(l.RecipeId) && !l.IsDeleted)
                    .OrderBy(l => l.SortOrder).ToListAsync();
                var list = new List<RecipeVersionModel>();
                foreach (var r in recipes)
                {
                    var cost = await CalculateCostAsync(r.Id, 1, null);
                    list.Add(new RecipeVersionModel { Recipe = r, ListLine = lines.Where(l => l.RecipeId == r.Id).ToList(), CostPerMeal = cost?.CostPerMeal ?? 0, CostComplete = cost?.Complete ?? false });
                }
                return new Results<RecipeVersionModel>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.GetAllSuccessfully,
                    ListData = list,
                    TotalRecords = list.Count
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetByDishId: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>T45R (spec 5.2): tạo phiên bản mới (Draft), có thể sao chép dòng định lượng từ một phiên bản cũ của cùng món.</summary>
        public async Task<IResult<Recipe>> CreateVersion(RecipeVersionRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                Recipe? source = null;
                if (request.CopyFromRecipeId is { } srcId)
                {
                    source = await _unitOfWork.RecipeRepository.GetSingleAsync(x => x.Id == srcId && x.DishId == request.DishId && !x.IsDeleted);
                    if (source == null)
                    {
                        _unitOfWork.RollbackTransaction();
                        return Result<Recipe>.Fail(ConstantHelper.Message.SourceRecipeNotInDish);
                    }
                }
                var recipeRequest = new RecipeRequest
                {
                    DishId = request.DishId,
                    StandardOutputQty = source?.StandardOutputQty,
                    YieldQuantity = source?.YieldQuantity ?? _rules.RecipeStandardServings,
                    YieldUnitId = source?.YieldUnitId,
                    YieldPercent = source?.YieldPercent,
                    Instructions = source?.Instructions,
                };
                string error = await Insert(recipeRequest, ConstantHelper.TypeSave.Insert);
                if (!string.IsNullOrEmpty(error))
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<Recipe>.Fail(error);
                }
                var recipe = _unitOfWork.RecipeRepository.GetAll().Where(x => x.DishId == request.DishId && x.Status == RecipeStatus.Draft && !x.IsDeleted)
                    .OrderByDescending(x => x.VersionNo).First();
                if (source != null)
                {
                    var lines = await _unitOfWork.RecipeLineRepository.GetAll().AsNoTracking().Where(l => l.RecipeId == source.Id && !l.IsDeleted).ToListAsync();
                    foreach (var l in lines)
                    {
                        _unitOfWork.RecipeLineRepository.Add(new RecipeLine
                        {
                            Id = Guid.NewGuid(), RecipeId = recipe.Id, IngredientId = l.IngredientId, Quantity = l.Quantity, UnitId = l.UnitId, UomId = l.UnitId,
                            WastePercent = l.WastePercent, ExpectedWasteRate = l.ExpectedWasteRate, YieldPercent = l.YieldPercent,
                            SequenceNo = l.SequenceNo, SortOrder = l.SortOrder, PreparationNote = l.PreparationNote, Note = l.Note,
                        });
                    }
                    await _unitOfWork.RecipeLineRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
                }
                _unitOfWork.CommitTransaction();
                return Result<Recipe>.Success(recipe, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " CreateVersion: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        /// <summary>T45R (spec 5.2): Draft → PendingApproval (≥ 1 dòng định lượng) + ApprovalRequest RECIPE_VERSION.</summary>
        public async Task<IResult<bool>> Submit(Guid id)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var recipe = _unitOfWork.RecipeRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
                string error = recipe == null ? ConstantHelper.Message.RecordNotFoundMessage
                    : recipe.Status != RecipeStatus.Draft ? InvalidStatus(recipe, RecipeStatus.Draft, "gửi duyệt")
                    : !await _unitOfWork.RecipeLineRepository.GetAll().AnyAsync(l => l.RecipeId == id && !l.IsDeleted) ? ConstantHelper.Message.RecipeNeedsLine
                    : string.Empty;
                if (!string.IsNullOrEmpty(error))
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<bool>.Fail(error, false);
                }
                recipe!.Status = RecipeStatus.PendingApproval;
                _unitOfWork.RecipeRepository.Update(recipe);
                // T12: yêu cầu duyệt qua engine chung (mã XN-, bước duyệt, nội dung được duyệt để chống sửa sau khi gửi).
                var (_, approvalError) = await _approvalRequestService.CreateAsync(RecipeStatus.BusinessType, recipe.Id);
                if (approvalError != null)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<bool>.Fail(approvalError, false);
                }
                await _unitOfWork.RecipeRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
                _unitOfWork.CommitTransaction();
                return Result<bool>.Success(true, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Submit: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        /// <summary>T45R (spec 5.2): duyệt (PendingApproval → Approved) hoặc từ chối (→ Draft, bắt buộc lý do). Cập nhật ApprovalRequest.</summary>
        public async Task<IResult<bool>> Decide(RecipeDecisionRequest request, bool approve)
        {
            try
            {
                // T12: duyệt / từ chối qua engine chung (ghi ApprovalDecision, kiểm quyền + nội dung, thực thi RecipeApprovalHandler).
                var recipe = await _unitOfWork.RecipeRepository.GetAll().AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.Id && !x.IsDeleted);
                if (recipe == null)
                {
                    return Result<bool>.Fail(ConstantHelper.Message.RecordNotFoundMessage, false);
                }
                var approval = await _unitOfWork.ApprovalRequestRepository.GetAll().AsNoTracking()
                    .Where(a => !a.IsDeleted && a.BusinessType == RecipeStatus.BusinessType && a.BusinessId == recipe.Id && a.RequestStatus == "Pending")
                    .OrderByDescending(a => a.RequestedAt).FirstOrDefaultAsync();
                if (approval == null)
                {
                    return Result<bool>.Fail(InvalidStatus(recipe, RecipeStatus.PendingApproval, approve ? "duyệt" : "từ chối"), false);
                }
                return await _approvalRequestService.Decide(new ApprovalDecideRequest { Id = approval.Id, Approve = approve, Note = request.Note });
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Decide: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>
        /// T45R (spec 5.2 bước 1–5), trong 1 transaction: bản hiệu lực cũ → Obsolete (EffectiveTo = từ ngày − 1); bản này → Locked, IsActive,
        /// LockedAt / LockedBy, SnapshotJson; StandardCost mới (cũ → Inactive); Dish.StandardCost.
        /// </summary>
        public async Task<IResult<bool>> Lock(RecipeLockRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var recipe = _unitOfWork.RecipeRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
                string error = recipe == null ? ConstantHelper.Message.RecordNotFoundMessage
                    : recipe.Status != RecipeStatus.Approved ? InvalidStatus(recipe, RecipeStatus.Approved, "khóa")
                    : string.Empty;
                if (!string.IsNullOrEmpty(error))
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<bool>.Fail(error, false);
                }
                DateTime from = (request.EffectiveFrom ?? _clock.Today).Date;
                foreach (var old in _unitOfWork.RecipeRepository.GetAll().Where(x => x.DishId == recipe!.DishId && x.Id != recipe.Id && x.IsActive && !x.IsDeleted).ToList())
                {
                    old.IsActive = false;
                    old.Status = RecipeStatus.Obsolete;
                    old.EffectiveTo = from.AddDays(-1);
                    _unitOfWork.RecipeRepository.Update(old);
                }
                recipe!.Status = RecipeStatus.Locked;
                recipe.IsActive = true;
                recipe.EffectiveFrom = from;
                recipe.EffectiveTo = null;
                recipe.LockedAt = _clock.Now;
                recipe.LockedBy = _currentActor.RequireActor().UserId;
                var lines = await _unitOfWork.RecipeLineRepository.GetAll().AsNoTracking().Where(l => l.RecipeId == recipe.Id && !l.IsDeleted).OrderBy(l => l.SortOrder).ToListAsync();
                recipe.SnapshotJson = JsonConvert.SerializeObject(lines.Select(l => new { l.IngredientId, l.Quantity, l.UnitId, l.WastePercent, l.SequenceNo }));
                _unitOfWork.RecipeRepository.Update(recipe);
                await _unitOfWork.RecipeRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                await UpsertStandardCostAsync(recipe.DishId, recipe.Id, from);
                _unitOfWork.CommitTransaction();
                return Result<bool>.Success(true, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Lock: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        /// <summary>
        /// T45R (spec 5.2 bước 4–5): StandardCost mới theo giá vốn hiện hành (+ nhân công, chi phí chung / suất từ BusinessRules), bản cũ → Inactive;
        /// Dish.StandardCost. Gọi trong transaction của Lock / StandardCostService.Recalculate.
        /// </summary>
        public async Task<StandardCost> UpsertStandardCostAsync(Guid dishId, Guid recipeId, DateTime from)
        {
            var cost = await CalculateCostAsync(recipeId, 1, from);
            foreach (var old in _unitOfWork.StandardCostRepository.GetAll().Where(s => s.DishId == dishId && !s.IsDeleted && s.Status != StandardCostInactive).ToList())
            {
                old.Status = StandardCostInactive;
                old.EffectiveTo ??= from.AddDays(-1);
                _unitOfWork.StandardCostRepository.Update(old);
            }
            decimal material = Math.Round(cost?.CostPerMeal ?? 0, 2);
            decimal total = material + _rules.RecipeLaborCostPerServing + _rules.RecipeOverheadCostPerServing;
            var sc = new StandardCost
            {
                Id = Guid.NewGuid(), DishId = dishId, RecipeId = recipeId, MaterialCost = material,
                LaborCost = _rules.RecipeLaborCostPerServing, OverheadCost = _rules.RecipeOverheadCostPerServing,
                TotalCost = total, CostPerServing = total, CurrencyCode = "VND", EffectiveFrom = from, Status = StandardCostActive,
            };
            _unitOfWork.StandardCostRepository.Add(sc);
            var dish = _unitOfWork.DishRepository.GetSingle(d => d.Id == dishId);
            if (dish != null)
            {
                dish.StandardCost = Math.Round(total, 2);
                _unitOfWork.DishRepository.Update(dish);
            }
            await _unitOfWork.StandardCostRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
            return sc;
        }

        private const string StandardCostActive = "Active";
        private const string StandardCostInactive = "Inactive";

        /// <summary>T45R (spec 5.2 bước 6): giá vốn NVL cho <paramref name="mealCount"/> suất – API.</summary>
        public async Task<IResult<RecipeCostModel>> CalculateCost(Guid id, decimal mealCount, DateTime? on)
        {
            try
            {
                if (mealCount <= 0)
                {
                    return Result<RecipeCostModel>.Fail(ConstantHelper.Message.QuantityMustBePositive);
                }
                var cost = await CalculateCostAsync(id, mealCount, on);
                return cost == null
                    ? Result<RecipeCostModel>.Fail(ConstantHelper.Message.RecordNotFoundMessage)
                    : Result<RecipeCostModel>.Success(cost, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " CalculateCost: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>
        /// Giá vốn NVL: định lượng cho số suất chuẩn (Recipe.StandardOutputQty, mặc định BusinessRules 100) × giá NCC ưu tiên đang hiệu lực
        /// (quy đổi sang đơn vị giá NCC). Dòng thiếu giá / thiếu quy đổi có cảnh báo, Complete = false. Không có công thức → null.
        /// </summary>
        public async Task<RecipeCostModel?> CalculateCostAsync(Guid recipeId, decimal mealCount, DateTime? on)
        {
            var recipe = await _unitOfWork.RecipeRepository.GetAll().AsNoTracking().FirstOrDefaultAsync(r => r.Id == recipeId && !r.IsDeleted);
            if (recipe == null)
            {
                return null;
            }
            decimal servings = recipe.StandardOutputQty is > 0 ? recipe.StandardOutputQty.Value : _rules.RecipeStandardServings;
            DateTime day = (on ?? _clock.Today).Date;
            var lines = await _unitOfWork.RecipeLineRepository.GetAll().AsNoTracking().Where(l => l.RecipeId == recipeId && !l.IsDeleted)
                .OrderBy(l => l.SortOrder).ThenBy(l => l.SequenceNo).ToListAsync();
            var ingredientIds = lines.Select(l => l.IngredientId).Distinct().ToList();
            var names = await _unitOfWork.IngredientRepository.GetAll().AsNoTracking().Where(i => ingredientIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.Name);
            var prices = await _unitOfWork.SupplierMaterialRepository.GetAll().AsNoTracking()
                .Where(m => !m.IsDeleted && m.IsPreferred && ingredientIds.Contains(m.IngredientId)
                    && (m.EffectiveFrom == null || m.EffectiveFrom <= day) && (m.EffectiveTo == null || m.EffectiveTo >= day))
                .OrderByDescending(m => m.EffectiveFrom).ToListAsync();
            var result = new List<RecipeLineCostModel>();
            foreach (var l in lines)
            {
                var item = new RecipeLineCostModel { LineId = l.Id, IngredientId = l.IngredientId, IngredientName = names.GetValueOrDefault(l.IngredientId), Quantity = l.Quantity, UnitId = l.UnitId };
                var price = prices.FirstOrDefault(p => p.IngredientId == l.IngredientId);
                if (price?.UnitPrice is not { } unitPrice)
                {
                    item.Warning = "Chưa có giá NCC ưu tiên đang hiệu lực.";
                }
                else
                {
                    item.UnitPrice = unitPrice;
                    item.PriceUnitId = price.PurchaseUomId ?? l.UnitId;
                    decimal? qty = await _uomConversionService.ConvertAsync(l.Quantity, l.UnitId, item.PriceUnitId.Value, day);
                    if (qty == null)
                    {
                        item.Warning = "Thiếu quy đổi đơn vị giữa định lượng và đơn vị giá NCC.";
                    }
                    else
                    {
                        item.Amount = Math.Round(qty.Value * unitPrice, 2);
                    }
                }
                result.Add(item);
            }
            decimal perMeal = servings > 0 ? Math.Round(result.Sum(r => r.Amount ?? 0m) / servings, 4) : 0m;
            return new RecipeCostModel
            {
                RecipeId = recipeId, MealCount = mealCount, StandardServings = servings, ListLine = result,
                CostPerMeal = perMeal, TotalMaterialCost = Math.Round(perMeal * mealCount, 2), Complete = result.All(r => r.Warning == null),
            };
        }
    }
}
