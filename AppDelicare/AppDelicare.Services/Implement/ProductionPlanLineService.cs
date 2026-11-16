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
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Net;

namespace AppDelicare.Services.Implement
{
    public class ProductionPlanLineService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService) : IProductionPlanLineService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;

        public async Task<IResults<ProductionPlanLineViewModel>> GetAll(ProductionPlanLineSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@ProductionPlanId", request.ProductionPlanId);
                dynamicParameters.Add("@OrderLineId", request.OrderLineId);
                dynamicParameters.Add("@DishId", request.DishId);
                //dynamicParameters.Add("@PlannedQuantity", request.PlannedQuantity);
                //dynamicParameters.Add("@ActualQuantity", request.ActualQuantity);
                dynamicParameters.Add("@ShiftId", request.ShiftId);
                dynamicParameters.Add("@Status", request.Status);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<ProductionPlanLineViewModel>("uspProductionPlanLine_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<ProductionPlanLineViewModel>
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

        public IResults<ProductionPlanLine> GetAllData()
        {
            try
            {
                var list = _unitOfWork.ProductionPlanLineRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<ProductionPlanLine>
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

        public async Task<IResult<bool>> Save(ProductionPlanLineRequest request, string typeSave)
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

        /// <summary>T17 (spec 8.1 lines): thêm món vào kế hoạch Nháp (cộng dự phòng) – 1 món 1 dòng, số phần &gt; 0, ghi song song PlannedQty.</summary>
        private async Task<string> Insert(ProductionPlanLineRequest request, string typeSave)
        {
            string? error = await CheckDraftPlanAsync(request.ProductionPlanId);
            if (error != null)
            {
                return error;
            }
            if (request.PlannedQuantity <= 0)
            {
                return ConstantHelper.Message.QuantityPositive;
            }
            if (_unitOfWork.ProductionPlanLineRepository.GetAll().Any(l => l.ProductionPlanId == request.ProductionPlanId && l.DishId == request.DishId && !l.IsDeleted))
            {
                return ConstantHelper.Message.PlanLineDuplicateDish;
            }
            _unitOfWork.ProductionPlanLineRepository.Add(new ProductionPlanLine
            {
                Id = Guid.NewGuid(),
                ProductionPlanId = request.ProductionPlanId,
                DishId = request.DishId,
                OrderLineId = request.OrderLineId,
                PlannedQuantity = request.PlannedQuantity,
                PlannedQty = request.PlannedQuantity,
                Status = ProductionPlanStatus.Draft,
            });
            await _unitOfWork.ProductionPlanLineRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        /// <summary>T17: chỉ sửa số phần của dòng thuộc kế hoạch Nháp (ghi song song PlannedQty); món / kế hoạch / ca / trạng thái không đổi qua CRUD.</summary>
        private async Task<string> Update(ProductionPlanLineRequest request, string typeSave)
        {
            var exist = _unitOfWork.ProductionPlanLineRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (request.Id == null || exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            string? error = await CheckDraftPlanAsync(exist.ProductionPlanId);
            if (error != null)
            {
                return error;
            }
            if (request.PlannedQuantity <= 0)
            {
                return ConstantHelper.Message.QuantityPositive;
            }
            exist.PlannedQuantity = request.PlannedQuantity;
            exist.PlannedQty = request.PlannedQuantity;
            _unitOfWork.ProductionPlanLineRepository.Update(exist);
            await _unitOfWork.ProductionPlanLineRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        /// <summary>T17: chỉ xóa (mềm) dòng của kế hoạch Nháp.</summary>
        private async Task<string> Delete(Guid? id, string typeSave)
        {
            var delete = id == null ? null : _unitOfWork.ProductionPlanLineRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            string? error = await CheckDraftPlanAsync(delete.ProductionPlanId);
            if (error != null)
            {
                return error;
            }
            _unitOfWork.ProductionPlanLineRepository.Update(delete);
            await _unitOfWork.ProductionPlanLineRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        private async Task<string?> CheckDraftPlanAsync(Guid planId)
        {
            var plan = await _unitOfWork.DbContext.ProductionPlan.AsNoTracking().FirstOrDefaultAsync(p => p.Id == planId && !p.IsDeleted);
            return plan == null ? ConstantHelper.Message.RecordNotFoundMessage
                : plan.Status != ProductionPlanStatus.Draft ? ConstantHelper.Message.PlanLineDraftOnly : null;
        }

        public async Task<IResult<ProductionPlanLine>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.ProductionPlanLineRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<ProductionPlanLine>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<ProductionPlanLine>
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
