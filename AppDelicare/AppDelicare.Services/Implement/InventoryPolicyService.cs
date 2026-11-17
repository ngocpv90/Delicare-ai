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
    public class InventoryPolicyService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService) : IInventoryPolicyService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;

        public async Task<IResults<InventoryPolicyViewModel>> GetAll(InventoryPolicySearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@IngredientId", request.IngredientId == Guid.Empty ? null : request.IngredientId);
                dynamicParameters.Add("@WarehouseId", request.WarehouseId == Guid.Empty ? null : request.WarehouseId);
                //dynamicParameters.Add("@MinimumStock", request.MinimumStock);
                //dynamicParameters.Add("@MaximumStock", request.MaximumStock);
                //dynamicParameters.Add("@SafetyStock", request.SafetyStock);
                //dynamicParameters.Add("@UseFefo", request.UseFefo);
                dynamicParameters.Add("@ExpiryWarningDays", request.ExpiryWarningDays);
                dynamicParameters.Add("@Status", request.Status);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<InventoryPolicyViewModel>("uspInventoryPolicy_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<InventoryPolicyViewModel>
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

        public IResults<InventoryPolicy> GetAllData()
        {
            try
            {
                var list = _unitOfWork.InventoryPolicyRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<InventoryPolicy>
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

        public async Task<IResult<bool>> Save(InventoryPolicyRequest request, string typeSave)
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

        private async Task<string> Insert(InventoryPolicyRequest request, string typeSave)
        {
            // T45R: luật nghiệp vụ (spec) – trước khi ghi.
            var invalid = await CheckRules(request);
            if (!string.IsNullOrEmpty(invalid))
            {
                return invalid;
            }
            var insert = _mapper.Map<InventoryPolicy>(request);
            _unitOfWork.InventoryPolicyRepository.Add(insert);
            await _unitOfWork.InventoryPolicyRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Update(InventoryPolicyRequest request, string typeSave)
        {
            // T45R: luật nghiệp vụ (spec) – trước khi ghi.
            var invalid = await CheckRules(request);
            if (!string.IsNullOrEmpty(invalid))
            {
                return invalid;
            }
            var exist = _unitOfWork.InventoryPolicyRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (request.Id == null || exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            var update = _mapper.Map(request, exist);
            _unitOfWork.InventoryPolicyRepository.Update(update);
            await _unitOfWork.InventoryPolicyRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Delete(Guid? id, string typeSave)
        {
            if (id == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // T07: ReferenceGuard (quan hệ logic, truy vấn có tham số) thay SQL động uspSystem_ExecuteQuery (A10).
            if (await _auditChangeService.IsReferencedAsync(nameof(InventoryPolicy), (Guid)id))
            {
                return ConstantHelper.Message.DeleteExistFail;
            }
            var delete = _unitOfWork.InventoryPolicyRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            _unitOfWork.InventoryPolicyRepository.Update(delete);
            await _unitOfWork.InventoryPolicyRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        public async Task<IResult<InventoryPolicy>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.InventoryPolicyRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<InventoryPolicy>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<InventoryPolicy>
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

        /// <summary>T45R (spec 4.6): Min ≤ Safety ≤ Reorder ≤ Max; một dòng cho (nguyên liệu, kho).</summary>
        private async Task<string> CheckRules(InventoryPolicyRequest request)
        {
            var chain = new[] { request.MinimumStock, request.SafetyStock, request.ReorderPoint, request.MaximumStock }.Where(x => x != null).ToList();
            for (int i = 1; i < chain.Count; i++)
            {
                if (chain[i - 1] > chain[i])
                {
                    return ConstantHelper.Message.StockChainInvalid;
                }
            }
            if (await _unitOfWork.InventoryPolicyRepository.GetAll()
                .AnyAsync(x => x.IngredientId == request.IngredientId && x.WarehouseId == request.WarehouseId && x.Id != request.Id && !x.IsDeleted))
            {
                return ConstantHelper.Message.InventoryPolicyExist;
            }
            return string.Empty;
        }
    }
}
