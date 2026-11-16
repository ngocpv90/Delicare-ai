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

namespace AppDelicare.Services.Implement
{
    public class RecipeLineService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService) : IRecipeLineService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;

        public async Task<IResults<RecipeLineViewModel>> GetAll(RecipeLineSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@RecipeId", request.RecipeId);
                dynamicParameters.Add("@IngredientId", request.IngredientId);
                //dynamicParameters.Add("@Quantity", request.Quantity);
                dynamicParameters.Add("@UnitId", request.UnitId);
                //dynamicParameters.Add("@ExpectedWasteRate", request.ExpectedWasteRate);
                //dynamicParameters.Add("@PreparationNote", request.PreparationNote);
                dynamicParameters.Add("@SortOrder", request.SortOrder);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<RecipeLineViewModel>("uspRecipeLine_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<RecipeLineViewModel>
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

        public IResults<RecipeLine> GetAllData()
        {
            try
            {
                var list = _unitOfWork.RecipeLineRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<RecipeLine>
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

        public async Task<IResult<bool>> Save(RecipeLineRequest request, string typeSave)
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

        private async Task<string> Insert(RecipeLineRequest request, string typeSave)
        {
            // T45R: luật nghiệp vụ (spec) – trước khi ghi.
            var invalid = await CheckRules(request);
            if (!string.IsNullOrEmpty(invalid))
            {
                return invalid;
            }
            var insert = _mapper.Map<RecipeLine>(request);
            insert.UomId = insert.UnitId; // T45R: cột cũ UomId (NOT NULL, DEPRECATED_COLUMNS) ghi song song
            _unitOfWork.RecipeLineRepository.Add(insert);
            await _unitOfWork.RecipeLineRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Update(RecipeLineRequest request, string typeSave)
        {
            // T45R: luật nghiệp vụ (spec) – trước khi ghi.
            var invalid = await CheckRules(request);
            if (!string.IsNullOrEmpty(invalid))
            {
                return invalid;
            }
            var exist = _unitOfWork.RecipeLineRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (request.Id == null || exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            var update = _mapper.Map(request, exist);
            update.UomId = update.UnitId; // T45R: ghi song song cột cũ
            _unitOfWork.RecipeLineRepository.Update(update);
            await _unitOfWork.RecipeLineRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Delete(Guid? id, string typeSave)
        {
            if (id == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // T07: ReferenceGuard (quan hệ logic, truy vấn có tham số) thay SQL động uspSystem_ExecuteQuery (A10).
            if (await _auditChangeService.IsReferencedAsync(nameof(RecipeLine), (Guid)id))
            {
                return ConstantHelper.Message.DeleteExistFail;
            }
            var delete = _unitOfWork.RecipeLineRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            string draft = await EnsureDraftAsync(delete.RecipeId); // T45R: chỉ xóa định lượng khi công thức Draft
            if (!string.IsNullOrEmpty(draft))
            {
                return draft;
            }
            _unitOfWork.RecipeLineRepository.Update(delete);
            await _unitOfWork.RecipeLineRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        public async Task<IResult<RecipeLine>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.RecipeLineRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<RecipeLine>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<RecipeLine>
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
        /// T45R (spec 5.2): chỉ sửa định lượng khi công thức Draft; nguyên liệu / đơn vị phải tồn tại; định lượng &gt; 0; hao hụt ∈ [0, 100).
        /// Dòng mới không có thứ tự → xếp cuối.
        /// </summary>
        private async Task<string> CheckRules(RecipeLineRequest request)
        {
            if (request.Id != null)
            {
                // Sửa dòng: công thức hiện tại của dòng phải Draft và không chuyển dòng sang công thức khác.
                var current = await _unitOfWork.RecipeLineRepository.GetAll().AsNoTracking().FirstOrDefaultAsync(l => l.Id == request.Id && !l.IsDeleted);
                if (current != null)
                {
                    request.RecipeId = current.RecipeId;
                }
            }
            string draft = await EnsureDraftAsync(request.RecipeId);
            if (!string.IsNullOrEmpty(draft))
            {
                return draft;
            }
            if (request.Quantity <= 0)
            {
                return ConstantHelper.Message.QuantityMustBePositive;
            }
            if (request.WastePercent is { } w && (w < 0 || w >= 100))
            {
                return ConstantHelper.Message.WastePercentRange;
            }
            if (!await _unitOfWork.IngredientRepository.GetAll().AnyAsync(i => i.Id == request.IngredientId && !i.IsDeleted))
            {
                return string.Format(ConstantHelper.Message.NotExist, "Nguyên liệu");
            }
            if (!await _unitOfWork.UnitMeasureRepository.GetAll().AnyAsync(u => u.Id == request.UnitId && !u.IsDeleted))
            {
                return string.Format(ConstantHelper.Message.NotExist, "Đơn vị tính");
            }
            if (request.Id == null && request.SortOrder == 0)
            {
                request.SortOrder = (await _unitOfWork.RecipeLineRepository.GetAll().Where(l => l.RecipeId == request.RecipeId && !l.IsDeleted)
                    .MaxAsync(l => (int?)l.SortOrder) ?? 0) + 1;
            }
            return string.Empty;
        }

        private async Task<string> EnsureDraftAsync(Guid? recipeId)
        {
            var recipe = await _unitOfWork.RecipeRepository.GetAll().AsNoTracking().FirstOrDefaultAsync(r => r.Id == recipeId && !r.IsDeleted);
            if (recipe == null)
            {
                return string.Format(ConstantHelper.Message.NotExist, "Công thức");
            }
            return recipe.Status == RecipeStatus.Draft
                ? string.Empty
                : string.Format(ConstantHelper.Message.InvalidStatusForAction, "sửa định lượng", RecipeStatus.Draft, recipe.Status ?? "(trống)");
        }
    }
}
