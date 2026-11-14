using AutoMapper;
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
    public class IngredientService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService) : IIngredientService
    {
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

        private async Task<string> Insert(IngredientRequest request, string typeSave)
        {
            if (string.IsNullOrWhiteSpace(request.Code))
            {
                return "Mã không được để trống.";
            }
            var checkDuplicate = _unitOfWork.IngredientRepository.GetSingle(x => x.Code == request.Code && !x.IsDeleted);
            if (checkDuplicate != null)
            {
                return string.Format(ConstantHelper.Message.CodeExist, request.Code);
            }
            var insert = _mapper.Map<Ingredient>(request);
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
            if (string.IsNullOrWhiteSpace(request.Code))
            {
                return "Mã không được để trống.";
            }
            var checkDuplicate = _unitOfWork.IngredientRepository.GetSingle(x => x.Id != request.Id && x.Code == request.Code && !x.IsDeleted);
            if (checkDuplicate != null)
            {
                return string.Format(ConstantHelper.Message.CodeExist, request.Code);
            }
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
            var checkForeignKey = (await _auditChangeService.GetColumnNameForeignKey()).ListData
                .Where(x => (x.ColumnName ?? string.Empty).Contains(nameof(Ingredient))).ToList();
            if (checkForeignKey.Count != 0)
            {
                foreach (var item in checkForeignKey)
                {
                    var strSql = string.Concat("SELECT COUNT(*) FROM ", item.TableName, " where ", item.ColumnName, " = '", id, "' AND IsDeleted = 0");
                    DynamicParameters dynamicParameters = new();
                    dynamicParameters.Add("@StrSql", strSql);
                    var checkExist = _dapperRepository.QueryFirstOrDefaultUsingStoreProc<long>("uspSystem_ExecuteQuery", dynamicParameters);
                    if (checkExist > 0)
                    {
                        return ConstantHelper.Message.DeleteExistFail;
                    }
                }
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
    }
}
