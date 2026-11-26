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
    public class DishService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService, IRecipeService recipeService, IDocumentNumberService documentNumberService) : IDishService
    {
        private readonly IDocumentNumberService _documentNumberService = documentNumberService;
        /// <summary>MA-1.3 (D-63): mã vừa cấp khi thêm mới – báo kèm trong thông báo thành công.</summary>
        private string? _createdCode;
        private Guid? _createdId;

        /// <summary>UI-6.17 B: thêm món (form gọn) – trả id + mã để FE tạo tiếp công thức V1 (chép từ món khác / nhập mới) rồi mở trang món.</summary>
        public async Task<IResult<DishCreatedModel>> Create(DishRequest request)
        {
            var saved = await Save(request, ConstantHelper.TypeSave.Insert);
            if (saved.ResponseCode != (int)HttpStatusCode.OK || _createdId == null)
            {
                return Result<DishCreatedModel>.Fail(saved.ResponseMessage);
            }
            return Result<DishCreatedModel>.Success(new DishCreatedModel { Id = _createdId.Value, Code = _createdCode }, saved.ResponseMessage);
        }
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IRecipeService _recipeService = recipeService;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;

        public async Task<IResults<DishViewModel>> GetAll(DishSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@DishCategoryId", request.DishCategoryId);
                dynamicParameters.Add("@DefaultUnitId", request.DefaultUnitId);
                dynamicParameters.Add("@Code", request.Code);
                dynamicParameters.Add("@Name", request.Name);
                //dynamicParameters.Add("@Description", request.Description);
                //dynamicParameters.Add("@StandardServingSize", request.StandardServingSize);
                dynamicParameters.Add("@Status", request.Status);
                //dynamicParameters.Add("@Note", request.Note);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<DishViewModel>("uspDish_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<DishViewModel>
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

        public IResults<Dish> GetAllData()
        {
            try
            {
                var list = _unitOfWork.DishRepository.GetAll().Where(x => !x.IsDeleted && (x.Code == null || !x.Code.StartsWith(PackagingNormService.DishPrefix))).ToList(); // ACC-6: món bao bì BB-<buổi> ẩn – sửa ở Định mức bao bì
                return new Results<Dish>
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

        public async Task<IResult<bool>> Save(DishRequest request, string typeSave)
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
                        : _createdCode != null ? string.Format(ConstantHelper.Message.SaveSuccessWithCode, "món ăn", _createdCode)
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

        /// <summary>
        /// UI-6.17 A (D-98): form món còn 5 ô (Tên*, Nhóm món*, Định lượng 1 suất + đơn vị, Mô tả, Trạng thái). Máy chủ tự ghi cột ẩn:
        /// MealCategory = mã nhóm món; StandardPortion = StandardServingSize; Ghi chú gộp vào Mô tả khi Mô tả trống. Giá thành chuẩn chỉ đọc
        /// (StandardCost/Recalculate tính từ công thức) – bỏ qua giá trị gửi lên (thêm mới = trống, sửa = giữ giá đang có).
        /// </summary>
        private async Task<string> NormalizeAsync(DishRequest request, Dish? exist)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return string.Format(ConstantHelper.Message.NotExist, "Tên món").Trim();
            }
            request.Name = request.Name.Trim();
            request.DishCategoryId ??= exist?.DishCategoryId ?? exist?.CategoryId;
            if (request.DishCategoryId == null)
            {
                return ConstantHelper.Message.DishCategoryRequired;
            }
            var category = await _unitOfWork.DbContext.DishCategory.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.DishCategoryId && !c.IsDeleted);
            if (category == null)
            {
                return ConstantHelper.Message.DishCategoryRequired;
            }
            request.MealCategory = category.Code;
            if (request.StandardServingSize is { } size)
            {
                if (size <= 0)
                {
                    return ConstantHelper.Message.DishServingSizeInvalid;
                }
                request.StandardPortion = size;
            }
            else
            {
                request.StandardPortion = exist?.StandardPortion;
            }
            if (string.IsNullOrWhiteSpace(request.Description) && !string.IsNullOrWhiteSpace(request.Note))
            {
                request.Description = request.Note.Trim();
            }
            request.StandardCost = exist?.StandardCost;
            return string.Empty;
        }

        private async Task<string> Insert(DishRequest request, string typeSave)
        {
            string invalid = await NormalizeAsync(request, null);
            if (invalid.Length > 0)
            {
                return invalid;
            }
            var insert = _mapper.Map<Dish>(request);
            insert.CategoryId ??= insert.DishCategoryId;
            insert.Status ??= "Active";
            _createdId = insert.Id == Guid.Empty ? (insert.Id = Guid.NewGuid()) : insert.Id;
            // MA-1.3 (D-63): mã danh mục máy chủ tự sinh (…), bỏ qua giá trị gửi lên; không đổi sau khi cấp.
            insert.Code = await _documentNumberService.NextCodeAsync(DocumentKind.Dish);
            _createdCode = insert.Code;
            _unitOfWork.DishRepository.Add(insert);
            await _unitOfWork.DishRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Update(DishRequest request, string typeSave)
        {
            var exist = _unitOfWork.DishRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (request.Id == null || exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // MA-1.3 (D-63): mã không đổi sau khi cấp – bỏ qua giá trị gửi lên.
            request.Code = exist.Code;
            string invalid = await NormalizeAsync(request, exist);
            if (invalid.Length > 0)
            {
                return invalid;
            }
            var update = _mapper.Map(request, exist);
            update.CategoryId = update.DishCategoryId ?? update.CategoryId;
            _unitOfWork.DishRepository.Update(update);
            await _unitOfWork.DishRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Delete(Guid? id, string typeSave)
        {
            if (id == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // T07: ReferenceGuard (quan hệ logic, truy vấn có tham số) thay SQL động uspSystem_ExecuteQuery (A10).
            if (await _auditChangeService.IsReferencedAsync(nameof(Dish), (Guid)id))
            {
                return ConstantHelper.Message.DeleteExistFail;
            }
            var delete = _unitOfWork.DishRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            _unitOfWork.DishRepository.Update(delete);
            await _unitOfWork.DishRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        public async Task<IResult<Dish>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.DishRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<Dish>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<Dish>
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

        /// <summary>T45R (spec 5.1): công thức đang hiệu lực của món (Locked, IsActive) và giá vốn NVL / suất ước tính.</summary>
        public async Task<IResult<RecipeVersionModel>> GetActiveRecipe(Guid id)
        {
            try
            {
                var recipe = await _unitOfWork.RecipeRepository.GetAll().AsNoTracking().FirstOrDefaultAsync(r => r.DishId == id && r.IsActive && !r.IsDeleted);
                if (recipe == null)
                {
                    return Result<RecipeVersionModel>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
                }
                var cost = await _recipeService.CalculateCostAsync(recipe.Id, 1, null);
                var lines = await _unitOfWork.RecipeLineRepository.GetAll().AsNoTracking().Where(l => l.RecipeId == recipe.Id && !l.IsDeleted).OrderBy(l => l.SortOrder).ToListAsync();
                return Result<RecipeVersionModel>.Success(new RecipeVersionModel
                {
                    Recipe = recipe, ListLine = lines, CostPerMeal = cost?.CostPerMeal ?? 0, CostComplete = cost?.Complete ?? false,
                }, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetActiveRecipe: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>
        /// UI-6 (D-80): hồ sơ món ăn toàn màn – công thức hiệu lực + giá vốn NVL / suất (như GetActiveRecipe), các phiên bản công thức, menu đang dùng món,
        /// lịch sử nấu (mẻ) + tỷ lệ mẻ đạt QC. Đánh giá món của khách: chưa có dữ liệu (không bảng nào gắn món – GAP).
        /// </summary>
        public async Task<IResult<DishOverviewModel>> GetOverview(Guid id)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                var dish = await db.Dish.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted);
                if (dish == null)
                {
                    return Result<DishOverviewModel>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
                }
                Guid? catId = dish.DishCategoryId ?? dish.CategoryId;
                var recipes = await db.Recipe.AsNoTracking().Where(r => r.DishId == id && !r.IsDeleted).OrderByDescending(r => r.VersionNo).ToListAsync();
                var recipeIds = recipes.Select(r => r.Id).ToList();
                var lineCounts = await db.RecipeLine.AsNoTracking().Where(l => recipeIds.Contains(l.RecipeId) && !l.IsDeleted)
                    .GroupBy(l => l.RecipeId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
                var active = recipes.FirstOrDefault(r => r.IsActive);
                var menuLines = await (from l in db.MenuLine.AsNoTracking()
                                       where !l.IsDeleted && l.DishId == id
                                       join m in db.Menu.AsNoTracking() on l.MenuId equals m.Id
                                       where !m.IsDeleted
                                       select new { m.Id, m.Code, m.Name, m.Status, m.EffectiveFrom, m.EffectiveTo, l.MealType }).ToListAsync();
                var batches = await (from b in db.ProductionBatch.AsNoTracking()
                                     where !b.IsDeleted && b.DishId == id && b.Status != ProductionBatchStatus.Cancelled
                                     join o in db.ProductionOrder.AsNoTracking() on b.ProductionOrderId equals o.Id into oj
                                     from o in oj.DefaultIfEmpty()
                                     orderby b.StartDate descending
                                     select new DishBatchBriefModel
                                     {
                                         Id = b.Id, BatchNumber = b.BatchNumber ?? b.BatchNo, ProductionOrderNumber = o != null ? (o.ProductionOrderNumber ?? o.ProductionOrderNo) : null,
                                         StartDate = b.StartDate, PlannedQuantity = b.PlannedQuantity ?? b.PlannedQty, ActualQuantity = b.ActualQuantity ?? b.ActualQty,
                                         AcceptedQuantity = b.AcceptedQty, Status = b.Status, QcStatus = b.QcStatus,
                                     }).ToListAsync();
                int passed = batches.Count(b => b.Status is ProductionBatchStatus.Passed or ProductionBatchStatus.Packed);
                int concluded = passed + batches.Count(b => b.Status == ProductionBatchStatus.Failed);
                return Result<DishOverviewModel>.Success(new DishOverviewModel
                {
                    Dish = dish,
                    CategoryName = catId is { } c ? await db.DishCategory.AsNoTracking().Where(x => x.Id == c).Select(x => x.Name).FirstOrDefaultAsync() : null,
                    UnitName = dish.DefaultUnitId is { } u ? await db.UnitMeasure.AsNoTracking().Where(x => x.Id == u).Select(x => x.Name ?? x.Code).FirstOrDefaultAsync() : null,
                    ActiveRecipe = active,
                    Cost = active == null ? null : await _recipeService.CalculateCostAsync(active.Id, 1, null),
                    IngredientCount = active == null ? 0 : lineCounts.GetValueOrDefault(active.Id),
                    ListRecipe = [.. recipes.Select(r => new DishRecipeBriefModel
                    {
                        Id = r.Id, VersionCode = r.VersionCode, VersionNo = r.VersionNo, Status = r.Status, IsActive = r.IsActive,
                        EffectiveFrom = r.EffectiveFrom, EffectiveTo = r.EffectiveTo, LineCount = lineCounts.GetValueOrDefault(r.Id),
                    })],
                    MenuCount = menuLines.Select(m => m.Id).Distinct().Count(),
                    ListMenu = [.. menuLines.GroupBy(m => m.Id).Select(g => new DishMenuBriefModel
                    {
                        Id = g.Key, Code = g.First().Code, Name = g.First().Name, Status = g.First().Status, EffectiveFrom = g.First().EffectiveFrom, EffectiveTo = g.First().EffectiveTo,
                        LineCount = g.Count(), ListMealType = [.. g.Select(x => x.MealType).Where(x => !string.IsNullOrEmpty(x)).Select(x => x!).Distinct().Order()],
                    }).OrderByDescending(m => m.EffectiveFrom)],
                    BatchCount = batches.Count,
                    PortionsAccepted = batches.Sum(b => b.AcceptedQuantity ?? 0m),
                    QcPassPercent = concluded == 0 ? null : Math.Round(100m * passed / concluded, 1),
                    ListBatch = [.. batches.Take(50)],
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
