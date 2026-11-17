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
    public class ActualCostService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService) : IActualCostService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;

        public async Task<IResults<ActualCostViewModel>> GetAll(ActualCostSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@ProductionBatchId", request.ProductionBatchId);
                dynamicParameters.Add("@ProductionOrderId", request.ProductionOrderId);
                //dynamicParameters.Add("@MaterialCost", request.MaterialCost);
                //dynamicParameters.Add("@LaborCost", request.LaborCost);
                //dynamicParameters.Add("@OverheadCost", request.OverheadCost);
                //dynamicParameters.Add("@OtherCost", request.OtherCost);
                dynamicParameters.Add("@TotalCost", request.TotalCost);
                dynamicParameters.Add("@CurrencyCode", request.CurrencyCode);
                dynamicParameters.Add("@CalculatedAt", request.CalculatedAt);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<ActualCostViewModel>("uspActualCost_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<ActualCostViewModel>
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

        public IResults<ActualCost> GetAllData()
        {
            try
            {
                var list = _unitOfWork.ActualCostRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<ActualCost>
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

        public async Task<IResult<bool>> Save(ActualCostRequest request, string typeSave)
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

        /// <summary>BE2.4: Giá thành thực tế chỉ sinh / cập nhật qua nghiệp vụ (ProductionOrder/CalculateCost, Close) – CRUD Insert không ghi.</summary>
        private Task<string> Insert(ActualCostRequest request, string typeSave)
            => Task.FromResult(string.Format(ConstantHelper.Message.ProductionCrudBlocked, "Giá thành thực tế", "ProductionOrder/CalculateCost, Close"));

        /// <summary>BE2.4: Giá thành thực tế chỉ sinh / cập nhật qua nghiệp vụ (ProductionOrder/CalculateCost, Close) – CRUD Update không ghi.</summary>
        private Task<string> Update(ActualCostRequest request, string typeSave)
            => Task.FromResult(string.Format(ConstantHelper.Message.ProductionCrudBlocked, "Giá thành thực tế", "ProductionOrder/CalculateCost, Close"));

        private async Task<string> Delete(Guid? id, string typeSave)
        {
            if (id == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            // T07: ReferenceGuard (quan hệ logic, truy vấn có tham số) thay SQL động uspSystem_ExecuteQuery (A10).
            if (await _auditChangeService.IsReferencedAsync(nameof(ActualCost), (Guid)id))
            {
                return ConstantHelper.Message.DeleteExistFail;
            }
            var delete = _unitOfWork.ActualCostRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            _unitOfWork.ActualCostRepository.Update(delete);
            await _unitOfWork.ActualCostRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        public async Task<IResult<ActualCost>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.ActualCostRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<ActualCost>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<ActualCost>
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
        /// BE2.4 (spec 8.5 GET actual-costs, Catalog 152): giá thành thực tế đã tính theo lệnh SX (cộng các mẻ) – lọc ngày sản xuất (từ / đến), món, lệnh;
        /// <c>Breakdown</c> = cơ cấu chi phí cộng cả danh sách (NVL / nhân công / chung / hao hụt / khác), giá thành / phần + giá thành chuẩn / phần.
        /// </summary>
        public async Task<IResult<ActualCostListModel>> GetList(ActualCostListRequest request)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                var planIds = await db.ProductionPlan.AsNoTracking()
                    .Where(p => !p.IsDeleted && (request.From == null || p.ProductionDate >= request.From.Value.Date)
                        && (request.To == null || p.ProductionDate < request.To.Value.Date.AddDays(1)))
                    .Select(p => p.Id).ToListAsync();
                var orderIds = await db.ProductionOrder.AsNoTracking()
                    .Where(o => !o.IsDeleted && planIds.Contains(o.ProductionPlanId)
                        && (request.DishId == null || request.DishId == Guid.Empty || o.DishId == request.DishId)
                        && (request.ProductionOrderId == null || request.ProductionOrderId == Guid.Empty || o.Id == request.ProductionOrderId))
                    .Select(o => o.Id).ToListAsync();
                var stored = await CostingRules.StoredAsync(db, orderIds);
                var list = stored.Values.OrderBy(c => c.ProductionDate).ThenBy(c => c.ShiftCode).ThenBy(c => c.DishName).ToList();
                decimal accepted = list.Sum(c => c.AcceptedPortions);
                decimal total = list.Sum(c => c.TotalCost);
                var standardRows = list.Where(c => c.StandardCostPerPortion != null).ToList();
                return Result<ActualCostListModel>.Success(new ActualCostListModel
                {
                    ListData = list,
                    Breakdown = new ActualCostModel
                    {
                        MaterialCost = list.Sum(c => c.MaterialCost),
                        LaborCost = list.Sum(c => c.LaborCost),
                        OverheadCost = list.Sum(c => c.OverheadCost),
                        WasteCost = list.Sum(c => c.WasteCost),
                        OtherCost = list.Sum(c => c.OtherCost),
                        TotalCost = total,
                        AcceptedPortions = accepted,
                        CostPerPortion = accepted > 0 ? Math.Round(total / accepted, 4) : null,
                        StandardCostPerPortion = standardRows.Sum(c => c.AcceptedPortions) is > 0 and var sp
                            ? Math.Round(standardRows.Sum(c => c.StandardCostPerPortion!.Value * c.AcceptedPortions) / sp, 4) : null,
                    },
                }, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetList: ", ex.ToString()));
                throw;
            }
        }
    }
}
