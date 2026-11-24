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
    public class StandardCostService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService, IRecipeService recipeService, IClock clock) : IStandardCostService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IRecipeService _recipeService = recipeService;
        private readonly IClock _clock = clock;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;

        public async Task<IResults<StandardCostViewModel>> GetAll(StandardCostSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@DishId", request.DishId == Guid.Empty ? null : request.DishId);
                dynamicParameters.Add("@RecipeId", request.RecipeId);
                //dynamicParameters.Add("@CostPerServing", request.CostPerServing);
                dynamicParameters.Add("@EffectiveFrom", request.EffectiveFrom);
                dynamicParameters.Add("@EffectiveTo", request.EffectiveTo);
                dynamicParameters.Add("@CurrencyCode", request.CurrencyCode);
                dynamicParameters.Add("@Status", request.Status);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<StandardCostViewModel>("uspStandardCost_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<StandardCostViewModel>
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

        public IResults<StandardCost> GetAllData()
        {
            try
            {
                var list = _unitOfWork.StandardCostRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<StandardCost>
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

        public async Task<IResult<bool>> Save(StandardCostRequest request, string typeSave)
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

        /// <summary>UI-6.27 (D-108): giá thành chuẩn CHỈ tự tính (khóa công thức – RecipeService.UpsertStandardCostAsync, đổi giá NCC, StandardCost/Recalculate) – không thêm tay.</summary>
        private Task<string> Insert(StandardCostRequest request, string typeSave)
            => Task.FromResult(ConstantHelper.Message.StandardCostManualBlocked);

        /// <summary>UI-6.27 (D-108): không sửa tay giá thành chuẩn.</summary>
        private Task<string> Update(StandardCostRequest request, string typeSave)
            => Task.FromResult(ConstantHelper.Message.StandardCostManualBlocked);

        private async Task<string> Delete(Guid? id, string typeSave)
        {
            if (id == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // T07: ReferenceGuard (quan hệ logic, truy vấn có tham số) thay SQL động uspSystem_ExecuteQuery (A10).
            if (await _auditChangeService.IsReferencedAsync(nameof(StandardCost), (Guid)id))
            {
                return ConstantHelper.Message.DeleteExistFail;
            }
            var delete = _unitOfWork.StandardCostRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            _unitOfWork.StandardCostRepository.Update(delete);
            await _unitOfWork.StandardCostRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        public async Task<IResult<StandardCost>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.StandardCostRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<StandardCost>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<StandardCost>
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
        /// T45R (spec 5.3): tính lại giá thành chuẩn theo giá NCC hiện hành cho món có công thức đang khóa (Locked, IsActive);
        /// <paramref name="request"/>.ListDishId trống = mọi món. Bản cũ → Inactive. Trả số món đã tính.
        /// </summary>
        public async Task<IResult<int>> Recalculate(StandardCostRecalculateRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var dishIds = (request.ListDishId ?? string.Empty).Split(ConstantHelper.Comma, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(x => Guid.TryParse(x, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).ToList();
                var active = _unitOfWork.RecipeRepository.GetAll().AsNoTracking().Where(r => r.IsActive && !r.IsDeleted && r.Status == RecipeStatus.Locked);
                if (dishIds.Count != 0)
                {
                    active = active.Where(r => dishIds.Contains(r.DishId));
                }
                var recipes = await active.ToListAsync();
                foreach (var r in recipes)
                {
                    await _recipeService.UpsertStandardCostAsync(r.DishId, r.Id, _clock.Today);
                }
                _unitOfWork.CommitTransaction();
                return Result<int>.Success(recipes.Count, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Recalculate: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }
    }
}
