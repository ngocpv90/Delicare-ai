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
    public class SupplierMaterialService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService) : ISupplierMaterialService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;

        public async Task<IResults<SupplierMaterialViewModel>> GetAll(SupplierMaterialSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@SupplierId", request.SupplierId);
                dynamicParameters.Add("@IngredientId", request.IngredientId);
                dynamicParameters.Add("@SupplierMaterialCode", request.SupplierMaterialCode);
                //dynamicParameters.Add("@UnitPrice", request.UnitPrice);
                //dynamicParameters.Add("@MinimumOrderQuantity", request.MinimumOrderQuantity);
                //dynamicParameters.Add("@LeadTimeDays", request.LeadTimeDays);
                //dynamicParameters.Add("@QualityRequirement", request.QualityRequirement);
                dynamicParameters.Add("@EffectiveFrom", request.EffectiveFrom);
                dynamicParameters.Add("@EffectiveTo", request.EffectiveTo);
                //dynamicParameters.Add("@IsPreferred", request.IsPreferred);
                dynamicParameters.Add("@Status", request.Status);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<SupplierMaterialViewModel>("uspSupplierMaterial_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<SupplierMaterialViewModel>
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

        public IResults<SupplierMaterial> GetAllData()
        {
            try
            {
                var list = _unitOfWork.SupplierMaterialRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<SupplierMaterial>
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

        public async Task<IResult<bool>> Save(SupplierMaterialRequest request, string typeSave)
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

        private string Validate(SupplierMaterialRequest request)
        {
            if (_unitOfWork.SupplierRepository.GetSingle(x => x.Id == request.SupplierId && !x.IsDeleted) == null)
            {
                return "Nhà cung cấp không tồn tại.";
            }
            if (_unitOfWork.IngredientRepository.GetSingle(x => x.Id == request.IngredientId && !x.IsDeleted) == null)
            {
                return "Nguyên liệu không tồn tại.";
            }
            if (request.EffectiveFrom.HasValue && request.EffectiveTo.HasValue && request.EffectiveFrom.Value.Date > request.EffectiveTo.Value.Date)
            {
                return "Ngày hiệu lực đến phải sau ngày hiệu lực từ.";
            }
            if ((request.UnitPrice ?? 0) < 0 || (request.MinimumOrderQuantity ?? 0) < 0 || (request.LeadTimeDays ?? 0) < 0)
            {
                return "Đơn giá, MOQ và lead time không được âm.";
            }
            // Cung NCC + nguyen lieu: khong duoc trung khoang thoi gian hieu luc (null = khong gioi han)
            var overlap = _unitOfWork.SupplierMaterialRepository
                .FindBy(x => x.SupplierId == request.SupplierId && x.IngredientId == request.IngredientId && x.Id != request.Id && !x.IsDeleted)
                .Any(x => (!request.EffectiveFrom.HasValue || !x.EffectiveTo.HasValue || request.EffectiveFrom.Value.Date <= x.EffectiveTo.Value.Date)
                       && (!x.EffectiveFrom.HasValue || !request.EffectiveTo.HasValue || x.EffectiveFrom.Value.Date <= request.EffectiveTo.Value.Date));
            if (overlap)
            {
                return "Nhà cung cấp đã có thông tin cho nguyên liệu này trong khoảng thời gian hiệu lực đã chọn.";
            }
            return string.Empty;
        }

        // Moi nguyen lieu chi co 1 nha cung cap uu tien
        private void ClearOtherPreferred(SupplierMaterialRequest request)
        {
            if (!request.IsPreferred)
            {
                return;
            }
            var others = _unitOfWork.SupplierMaterialRepository
                .FindBy(x => x.IngredientId == request.IngredientId && x.Id != request.Id && x.IsPreferred && !x.IsDeleted).ToList();
            foreach (var item in others)
            {
                item.IsPreferred = false;
                _unitOfWork.SupplierMaterialRepository.Update(item);
            }
        }

        private async Task<string> Insert(SupplierMaterialRequest request, string typeSave)
        {
            var error = Validate(request);
            if (!string.IsNullOrEmpty(error))
            {
                return error;
            }
            ClearOtherPreferred(request);
            var insert = _mapper.Map<SupplierMaterial>(request);
            _unitOfWork.SupplierMaterialRepository.Add(insert);
            await _unitOfWork.SupplierMaterialRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Update(SupplierMaterialRequest request, string typeSave)
        {
            var exist = _unitOfWork.SupplierMaterialRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (request.Id == null || exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            var error = Validate(request);
            if (!string.IsNullOrEmpty(error))
            {
                return error;
            }
            ClearOtherPreferred(request);
            var update = _mapper.Map(request, exist);
            _unitOfWork.SupplierMaterialRepository.Update(update);
            await _unitOfWork.SupplierMaterialRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Delete(Guid? id, string typeSave)
        {
            if (id == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            var checkForeignKey = (await _auditChangeService.GetColumnNameForeignKey()).ListData
                .Where(x => (x.ColumnName ?? string.Empty).Contains(nameof(SupplierMaterial))).ToList();
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
            var delete = _unitOfWork.SupplierMaterialRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            _unitOfWork.SupplierMaterialRepository.Update(delete);
            await _unitOfWork.SupplierMaterialRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        public async Task<IResult<SupplierMaterial>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.SupplierMaterialRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<SupplierMaterial>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<SupplierMaterial>
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
