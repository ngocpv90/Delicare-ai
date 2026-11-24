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
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Model.Request.Custom;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AppDelicare.Services.Implement
{
    public class MaterialConsumptionVarianceService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService, IOptions<BusinessRulesOptions> rules) : IMaterialConsumptionVarianceService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;
        private readonly BusinessRulesOptions _rules = rules.Value;

        public async Task<IResults<MaterialConsumptionVarianceViewModel>> GetAll(MaterialConsumptionVarianceSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@ProductionOrderId", request.ProductionOrderId == Guid.Empty ? null : request.ProductionOrderId);
                dynamicParameters.Add("@IngredientId", request.IngredientId == Guid.Empty ? null : request.IngredientId);
                //dynamicParameters.Add("@StandardQuantity", request.StandardQuantity);
                //dynamicParameters.Add("@ActualQuantity", request.ActualQuantity);
                //dynamicParameters.Add("@VarianceQuantity", request.VarianceQuantity);
                //dynamicParameters.Add("@VarianceCost", request.VarianceCost);
                //dynamicParameters.Add("@Reason", request.Reason);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<MaterialConsumptionVarianceViewModel>("uspMaterialConsumptionVariance_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<MaterialConsumptionVarianceViewModel>
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

        public IResults<MaterialConsumptionVariance> GetAllData()
        {
            try
            {
                var list = _unitOfWork.MaterialConsumptionVarianceRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<MaterialConsumptionVariance>
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

        public async Task<IResult<bool>> Save(MaterialConsumptionVarianceRequest request, string typeSave)
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

        /// <summary>BE2.4: Chênh lệch tiêu hao chỉ sinh / cập nhật qua nghiệp vụ (ProductionOrder/Close) – CRUD Insert không ghi.</summary>
        private Task<string> Insert(MaterialConsumptionVarianceRequest request, string typeSave)
            => Task.FromResult(string.Format(ConstantHelper.Message.ProductionCrudBlocked, "Chênh lệch tiêu hao", "được hệ thống tính khi đóng lệnh sản xuất."));

        /// <summary>BE2.4: Chênh lệch tiêu hao chỉ sinh / cập nhật qua nghiệp vụ (ProductionOrder/Close) – CRUD Update không ghi.</summary>
        private Task<string> Update(MaterialConsumptionVarianceRequest request, string typeSave)
            => Task.FromResult(string.Format(ConstantHelper.Message.ProductionCrudBlocked, "Chênh lệch tiêu hao", "được hệ thống tính khi đóng lệnh sản xuất."));

        private async Task<string> Delete(Guid? id, string typeSave)
        {
            if (id == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // T07: ReferenceGuard (quan hệ logic, truy vấn có tham số) thay SQL động uspSystem_ExecuteQuery (A10).
            if (await _auditChangeService.IsReferencedAsync(nameof(MaterialConsumptionVariance), (Guid)id))
            {
                return ConstantHelper.Message.DeleteExistFail;
            }
            var delete = _unitOfWork.MaterialConsumptionVarianceRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            _unitOfWork.MaterialConsumptionVarianceRepository.Update(delete);
            await _unitOfWork.MaterialConsumptionVarianceRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        public async Task<IResult<MaterialConsumptionVariance>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.MaterialConsumptionVarianceRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<MaterialConsumptionVariance>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<MaterialConsumptionVariance>
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
        /// BE2.4 (spec 7.9, Catalog 135): chênh lệch tiêu hao (chỉ đọc – sinh khi đóng lệnh SX) theo lệnh / ngày sản xuất (từ / đến) / nguyên liệu,
        /// kèm lệnh, món, ngày, cờ vượt ngưỡng <c>ConsumptionVariancePctWarning</c>.
        /// </summary>
        public async Task<IResults<ConsumptionVarianceModel>> GetList(ConsumptionVarianceListRequest request)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                List<Guid>? orderIds = null;
                if (request.From != null || request.To != null)
                {
                    var planIds = await db.ProductionPlan.AsNoTracking()
                        .Where(p => !p.IsDeleted && (request.From == null || p.ProductionDate >= request.From.Value.Date)
                            && (request.To == null || p.ProductionDate < request.To.Value.Date.AddDays(1)))
                        .Select(p => p.Id).ToListAsync();
                    orderIds = await db.ProductionOrder.AsNoTracking().Where(o => planIds.Contains(o.ProductionPlanId)).Select(o => o.Id).ToListAsync();
                }
                var rows = await db.MaterialConsumptionVariance.AsNoTracking()
                    .Where(v => !v.IsDeleted
                        && (request.ProductionOrderId == null || request.ProductionOrderId == Guid.Empty || v.ProductionOrderId == request.ProductionOrderId)
                        && (request.IngredientId == null || request.IngredientId == Guid.Empty || v.IngredientId == request.IngredientId)
                        && (orderIds == null || orderIds.Contains(v.ProductionOrderId)))
                    .ToListAsync();
                var list = await CostingRules.VarianceModelsAsync(db, rows, _rules);
                if (request.OverThresholdOnly)
                {
                    list = [.. list.Where(v => v.IsOverThreshold)];
                }
                return new Results<ConsumptionVarianceModel>
                {
                    ResponseCode = Convert.ToInt32(HttpStatusCode.OK),
                    ResponseMessage = ConstantHelper.Message.GetAllSuccessfully,
                    ListData = [.. list.OrderBy(v => v.ProductionDate).ThenBy(v => v.ProductionOrderNumber).ThenBy(v => v.IngredientName)],
                    TotalRecords = list.Count,
                };
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetList: ", ex.ToString()));
                throw;
            }
        }
    }
}
