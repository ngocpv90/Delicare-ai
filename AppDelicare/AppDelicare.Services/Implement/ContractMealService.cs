using AutoMapper;
using Dapper;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using Microsoft.EntityFrameworkCore;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using AppDelicare.Services.Interface;
using Serilog;
using System.Net;

namespace AppDelicare.Services.Implement
{
    public class ContractMealService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService, Microsoft.Extensions.Options.IOptions<AppDelicare.Model.Option.ReferenceListsOptions> referenceLists) : IContractMealService
    {
        private readonly AppDelicare.Model.Option.ReferenceListsOptions _referenceLists = referenceLists.Value;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;

        public async Task<IResults<ContractMealViewModel>> GetAll(ContractMealSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@ContractId", request.ContractId == Guid.Empty ? null : request.ContractId);
                dynamicParameters.Add("@MealType", request.MealType);
                //dynamicParameters.Add("@MealName", request.MealName);
                //dynamicParameters.Add("@UnitPrice", request.UnitPrice);
                dynamicParameters.Add("@UnitId", request.UnitId);
                dynamicParameters.Add("@Status", request.Status);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<ContractMealViewModel>("uspContractMeal_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<ContractMealViewModel>
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

        public IResults<ContractMeal> GetAllData()
        {
            try
            {
                var list = _unitOfWork.ContractMealRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<ContractMeal>
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

        public async Task<IResult<bool>> Save(ContractMealRequest request, string typeSave)
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
        /// BE2.2: loại suất chỉ thêm / sửa / xóa khi hợp đồng còn Nháp (hợp đồng đã gửi duyệt / hiệu lực: giá đã duyệt không đổi tay – đổi giá qua chính sách giá
        /// hoặc hợp đồng mới). MealType bắt buộc, không trùng trong hợp đồng; đơn giá (nếu có) &gt; 0. PROPOSED – BA/PO CONFIRM (D-51).
        /// </summary>
        private async Task<string?> RuleErrorAsync(Guid contractId, string? mealType, decimal? unitPrice, Guid? selfId, string? storedMealType = null)
        {
            var contract = await _unitOfWork.DbContext.Contract.AsNoTracking().FirstOrDefaultAsync(c => c.Id == contractId && !c.IsDeleted);
            if (contract == null)
            {
                return ConstantHelper.Message.ContractNotFound;
            }
            if (contract.Status != ContractStatus.Draft)
            {
                return string.Format(ConstantHelper.Message.ContractEditOnlyDraft, ContractStatus.Label(contract.Status));
            }
            if (selfId != null && mealType == null)
            {
                return null; // xóa
            }
            if (string.IsNullOrWhiteSpace(mealType))
            {
                return ConstantHelper.Message.ContractMealTypeRequired;
            }
            // Lỗi UAT 2026-11-21: loại suất lấy từ danh mục ReferenceLists:MealType (giá trị cũ ngoài danh mục giữ được khi không đổi).
            var (code, catalogError) = ReferenceCatalogs.CheckColumn(_referenceLists, "ContractMeal.MealType", mealType, storedMealType);
            if (catalogError != null)
            {
                return catalogError;
            }
            if (unitPrice is <= 0)
            {
                return string.Format(ConstantHelper.Message.ContractMealPriceInvalid, mealType);
            }
            string meal = (code ?? mealType).Trim().ToUpper();
            bool duplicate = await _unitOfWork.DbContext.ContractMeal.AsNoTracking()
                .AnyAsync(m => m.ContractId == contractId && !m.IsDeleted && m.Id != selfId && m.MealType != null && m.MealType.Trim().ToUpper() == meal);
            return duplicate ? string.Format(ConstantHelper.Message.ContractMealTypeDuplicate, mealType.Trim()) : null;
        }

        private async Task<string> Insert(ContractMealRequest request, string typeSave)
        {
            if (await RuleErrorAsync(request.ContractId, request.MealType, request.UnitPrice, null) is { } error)
            {
                return error;
            }
            var insert = _mapper.Map<ContractMeal>(request);
            insert.MealType = ReferenceCatalogs.CheckColumn(_referenceLists, "ContractMeal.MealType", request.MealType).Code ?? request.MealType!.Trim();
            insert.Status ??= ContractStatus.Active;
            _unitOfWork.ContractMealRepository.Add(insert);
            await _unitOfWork.ContractMealRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Update(ContractMealRequest request, string typeSave)
        {
            var exist = _unitOfWork.ContractMealRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (request.Id == null || exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            string? storedMealType = exist.MealType;
            if (await RuleErrorAsync(exist.ContractId, request.MealType, request.UnitPrice, exist.Id, storedMealType) is { } error)
            {
                return error;
            }
            Guid contractId = exist.ContractId;
            var update = _mapper.Map(request, exist);
            update.ContractId = contractId; // không chuyển loại suất sang hợp đồng khác
            update.MealType = ReferenceCatalogs.CheckColumn(_referenceLists, "ContractMeal.MealType", request.MealType, storedMealType).Code ?? request.MealType!.Trim();
            _unitOfWork.ContractMealRepository.Update(update);
            await _unitOfWork.ContractMealRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Delete(Guid? id, string typeSave)
        {
            if (id == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // T07: ReferenceGuard (quan hệ logic, truy vấn có tham số) thay SQL động uspSystem_ExecuteQuery (A10).
            if (await _auditChangeService.IsReferencedAsync(nameof(ContractMeal), (Guid)id))
            {
                return ConstantHelper.Message.DeleteExistFail;
            }
            var delete = _unitOfWork.ContractMealRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            if (await RuleErrorAsync(delete.ContractId, null, null, delete.Id) is { } error)
            {
                return error;
            }
            _unitOfWork.ContractMealRepository.Update(delete);
            await _unitOfWork.ContractMealRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        public async Task<IResult<ContractMeal>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.ContractMealRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<ContractMeal>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<ContractMeal>
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
    }
}
