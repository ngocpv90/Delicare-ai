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
    public class InventoryTransactionService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService
        , IInventoryLedgerService inventoryLedgerService) : IInventoryTransactionService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IInventoryLedgerService _inventoryLedgerService = inventoryLedgerService;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;

        public async Task<IResults<InventoryTransactionViewModel>> GetAll(InventoryTransactionSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@WarehouseId", request.WarehouseId);
                dynamicParameters.Add("@IngredientId", request.IngredientId);
                dynamicParameters.Add("@MaterialLotId", request.MaterialLotId);
                dynamicParameters.Add("@StorageLocationId", request.StorageLocationId);
                dynamicParameters.Add("@TransactionNumber", request.TransactionNumber);
                dynamicParameters.Add("@TransactionType", request.TransactionType);
                dynamicParameters.Add("@TransactionDate", request.TransactionDate);
                //dynamicParameters.Add("@QuantityIn", request.QuantityIn);
                //dynamicParameters.Add("@QuantityOut", request.QuantityOut);
                dynamicParameters.Add("@UnitId", request.UnitId);
                dynamicParameters.Add("@ReferenceType", request.ReferenceType);
                dynamicParameters.Add("@ReferenceId", request.ReferenceId);
                //dynamicParameters.Add("@Reason", request.Reason);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<InventoryTransactionViewModel>("uspInventoryTransaction_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<InventoryTransactionViewModel>
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

        public IResults<InventoryTransaction> GetAllData()
        {
            try
            {
                var list = _unitOfWork.InventoryTransactionRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<InventoryTransaction>
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

        public async Task<IResult<bool>> Save(InventoryTransactionRequest request, string typeSave)
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

        private async Task<string> Insert(InventoryTransactionRequest request, string typeSave)
        {
            var insert = _mapper.Map<InventoryTransaction>(request);
            _unitOfWork.InventoryTransactionRepository.Add(insert);
            await _unitOfWork.InventoryTransactionRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Update(InventoryTransactionRequest request, string typeSave)
        {
            var exist = _unitOfWork.InventoryTransactionRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (request.Id == null || exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            var update = _mapper.Map(request, exist);
            _unitOfWork.InventoryTransactionRepository.Update(update);
            await _unitOfWork.InventoryTransactionRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string> Delete(Guid? id, string typeSave)
        {
            if (id == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // T07: ReferenceGuard (quan hệ logic, truy vấn có tham số) thay SQL động uspSystem_ExecuteQuery (A10).
            if (await _auditChangeService.IsReferencedAsync(nameof(InventoryTransaction), (Guid)id))
            {
                return ConstantHelper.Message.DeleteExistFail;
            }
            var delete = _unitOfWork.InventoryTransactionRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            _unitOfWork.InventoryTransactionRepository.Update(delete);
            await _unitOfWork.InventoryTransactionRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        public async Task<IResult<InventoryTransaction>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.InventoryTransactionRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<InventoryTransaction>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<InventoryTransaction>
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
        /// T45R (T08, chuyển từ lớp v1 cũ): đảo một bút toán kho – ghi bút toán ngược dấu tham chiếu dòng gốc, tồn kho trở lại như trước.
        /// Sổ chỉ ghi thêm: không sửa / xóa bút toán. Lỗi nghiệp vụ (đã đảo, đảo bút toán đảo, không đủ tồn) → ResponseCode 400.
        /// </summary>
        public async Task<IResult<InventoryTransaction>> Reverse(InventoryReverseRequest request)
        {
            try
            {
                string reason = request.Reason?.Trim() ?? string.Empty;
                if (reason.Length is < 3 or > 1000)
                {
                    return Result<InventoryTransaction>.Fail(ConstantHelper.Message.ReasonRequired);
                }
                var reversal = await _inventoryLedgerService.ReverseAsync(request.Id, reason);
                return Result<InventoryTransaction>.Success(reversal, ConstantHelper.Message.SaveSuccess);
            }
            catch (BusinessRuleException ex)
            {
                return Result<InventoryTransaction>.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Reverse: ", ex.ToString()));
                throw;
            }
        }
    }
}
