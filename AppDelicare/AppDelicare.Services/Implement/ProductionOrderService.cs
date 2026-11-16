using AutoMapper;
using Dapper;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Model.Request;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using System.Net;

namespace AppDelicare.Services.Implement
{
    public class ProductionOrderService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService, IOptions<BusinessRulesOptions> rules) : IProductionOrderService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;
        private readonly BusinessRulesOptions _rules = rules.Value;

        public async Task<IResults<ProductionOrderViewModel>> GetAll(ProductionOrderSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                dynamicParameters.Add("@ProductionPlanId", request.ProductionPlanId == Guid.Empty ? null : request.ProductionPlanId);
                dynamicParameters.Add("@ProductionOrderNumber", request.ProductionOrderNumber);
                dynamicParameters.Add("@PlannedStartDate", request.PlannedStartDate);
                dynamicParameters.Add("@ActualStartDate", request.ActualStartDate);
                dynamicParameters.Add("@ActualEndDate", request.ActualEndDate);
                dynamicParameters.Add("@ShiftId", request.ShiftId);
                dynamicParameters.Add("@ResponsibleEmployeeId", request.ResponsibleEmployeeId);
                dynamicParameters.Add("@Status", request.Status);
                //dynamicParameters.Add("@Note", request.Note);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<ProductionOrderViewModel>("uspProductionOrder_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<ProductionOrderViewModel>
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

        public IResults<ProductionOrder> GetAllData()
        {
            try
            {
                var list = _unitOfWork.ProductionOrderRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<ProductionOrder>
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

        public async Task<IResult<bool>> Save(ProductionOrderRequest request, string typeSave)
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

        /// <summary>T17: lệnh SX chỉ sinh khi phát hành kế hoạch (ProductionPlan/Release – số PH-, công thức + ảnh chụp định lượng) – CRUD Insert không ghi.</summary>
        private Task<string> Insert(ProductionOrderRequest request, string typeSave)
            => Task.FromResult(string.Format(ConstantHelper.Message.ProductionCrudBlocked, "Lệnh sản xuất", "ProductionPlan/Release"));

        /// <summary>T17: CRUD Update chỉ sửa ghi chú / người phụ trách; trạng thái, món, công thức, số lượng, lịch chỉ đổi qua phát hành kế hoạch và thao tác mẻ.</summary>
        private async Task<string> Update(ProductionOrderRequest request, string typeSave)
        {
            var exist = _unitOfWork.ProductionOrderRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
            if (request.Id == null || exist == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            exist.Note = request.Note;
            exist.ResponsibleEmployeeId = request.ResponsibleEmployeeId is { } e && e != Guid.Empty ? e : null;
            _unitOfWork.ProductionOrderRepository.Update(exist);
            await _unitOfWork.ProductionOrderRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        /// <summary>T17: lệnh SX không xóa (đã có mẻ / phiếu xuất) – hủy lệnh để sau Pilot.</summary>
        private Task<string> Delete(Guid? id, string typeSave)
            => Task.FromResult(string.Format(ConstantHelper.Message.ProductionCrudBlocked, "Lệnh sản xuất", "không xóa"));

        public async Task<IResult<ProductionOrder>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.ProductionOrderRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<ProductionOrder>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<ProductionOrder>
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

        /// <summary>T17 (spec 8.2 orders/{id}): chi tiết lệnh SX – ảnh chụp định lượng, mẻ (lịch + ATTP), phiếu xuất của lệnh.</summary>
        public async Task<IResult<ProductionOrderDetailModel>> GetDetail(Guid id)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                var order = await db.ProductionOrder.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted);
                if (order == null)
                {
                    return Result<ProductionOrderDetailModel>.Fail(ConstantHelper.Message.ProductionOrderNotFound);
                }
                var batches = await db.ProductionBatch.AsNoTracking().Where(b => b.ProductionOrderId == id && !b.IsDeleted).ToListAsync();
                var issues = await db.MaterialIssue.AsNoTracking().Where(i => i.ProductionOrderId == id && !i.IsDeleted).OrderBy(i => i.CreatedDate).ToListAsync();
                var ingredientIds = ProductionRules.ParseSnapshot(order.RecipeSnapshotJson).Select(l => l.IngredientId).Distinct().ToList();
                var ingredients = await db.Ingredient.AsNoTracking().Where(i => ingredientIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.Name);
                var units = await db.UnitMeasure.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.Name ?? u.Code);
                return Result<ProductionOrderDetailModel>.Success(new ProductionOrderDetailModel
                {
                    ProductionOrder = order,
                    Order = (await ProductionRules.OrderBriefsAsync(db, [order])).Single(),
                    ListRecipeLine = [.. ProductionRules.ParseSnapshot(order.RecipeSnapshotJson).Select(l => new ProductionRecipeLineModel
                    {
                        IngredientId = l.IngredientId,
                        IngredientName = ingredients.GetValueOrDefault(l.IngredientId),
                        Quantity = l.Quantity,
                        UnitId = l.UnitId,
                        UnitName = units.GetValueOrDefault(l.UnitId),
                        WastePercent = l.WastePercent,
                    })],
                    ListBatch = await ProductionRules.BatchBriefsAsync(db, batches, _rules),
                    ListIssue = [.. issues.Select(i => new ProductionIssueBriefModel
                    {
                        Id = i.Id,
                        IssueNumber = i.IssueNumber ?? i.IssueNo,
                        IssueDate = i.IssueDate,
                        Status = i.Status,
                        StatusName = MaterialIssueStatus.Label(i.Status),
                        WarehouseId = i.WarehouseId,
                    })],
                }, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetDetail: ", ex.ToString()));
                throw;
            }
        }
    }
}
