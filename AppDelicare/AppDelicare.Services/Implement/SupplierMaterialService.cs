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
                dynamicParameters.Add("@EffectiveOn", request.EffectiveOn?.Date); // T45R
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

        private async Task<string> Insert(SupplierMaterialRequest request, string typeSave)
        {
            // T45R: luật nghiệp vụ (spec) – trước khi ghi.
            var invalid = await CheckRules(request);
            if (!string.IsNullOrEmpty(invalid))
            {
                return invalid;
            }
            var insert = _mapper.Map<SupplierMaterial>(request);
            _unitOfWork.SupplierMaterialRepository.Add(insert);
            await _unitOfWork.SupplierMaterialRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Update(SupplierMaterialRequest request, string typeSave)
        {
            // T45R: luật nghiệp vụ (spec) – trước khi ghi.
            var invalid = await CheckRules(request);
            if (!string.IsNullOrEmpty(invalid))
            {
                return invalid;
            }
            var exist = _unitOfWork.SupplierMaterialRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (request.Id == null || exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
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
            // T07: ReferenceGuard (quan hệ logic, truy vấn có tham số) thay SQL động uspSystem_ExecuteQuery (A10).
            if (await _auditChangeService.IsReferencedAsync(nameof(SupplierMaterial), (Guid)id))
            {
                return ConstantHelper.Message.DeleteExistFail;
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

        /// <summary>
        /// T45R (spec 4.3): mỗi nguyên liệu chỉ 1 NCC ưu tiên – bật IsPreferred thì bỏ ưu tiên các dòng khác cùng nguyên liệu
        /// (ghi trong cùng transaction với lần lưu).
        /// </summary>
        private async Task<string> CheckRules(SupplierMaterialRequest request)
        {
            if (!request.IsPreferred)
            {
                return string.Empty;
            }
            var others = await _unitOfWork.SupplierMaterialRepository.GetAll()
                .Where(x => x.IngredientId == request.IngredientId && x.IsPreferred && x.Id != request.Id && !x.IsDeleted).ToListAsync();
            if (others.Count != 0)
            {
                others.ForEach(o =>
                {
                    o.IsPreferred = false;
                    _unitOfWork.SupplierMaterialRepository.Update(o);
                });
                await _unitOfWork.SupplierMaterialRepository.CommitAsync(ConstantHelper.TypeSave.Update);
            }
            return string.Empty;
        }
    }
}
