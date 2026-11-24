using AutoMapper;
using Dapper;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Model.Request;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Data.Share;
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
        , IAuditChangeService auditChangeService, IOptions<BusinessRulesOptions> rules, IClock clock, ICurrentActor currentActor
        , IUomConversionService uomConversionService) : IProductionOrderService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;
        private readonly BusinessRulesOptions _rules = rules.Value;
        private readonly IClock _clock = clock;
        private readonly ICurrentActor _currentActor = currentActor;
        private readonly IUomConversionService _uomConversionService = uomConversionService;

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

        /// <summary>T17 (spec 8.2 orders/{id}) + BE2.4: chi tiết lệnh SX – ảnh chụp định lượng, mẻ, phiếu xuất, giữ chỗ, chênh lệch tiêu hao, giá thành thực tế.</summary>
        public async Task<IResult<ProductionOrderDetailModel>> GetDetail(Guid id)
        {
            try
            {
                var detail = await DetailAsync(id);
                return detail == null ? Result<ProductionOrderDetailModel>.Fail(ConstantHelper.Message.ProductionOrderNotFound)
                    : Result<ProductionOrderDetailModel>.Success(detail, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetDetail: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>UI-6 (D-80): trang chi tiết toàn màn lệnh SX – dùng lại DetailAsync (GetDetail) + kế hoạch / ca / dây chuyền / người phụ trách + đếm mẻ, giữ chỗ thiếu, chênh lệch vượt ngưỡng.</summary>
        public async Task<IResult<ProductionOrderOverviewModel>> GetOverview(Guid id)
        {
            try
            {
                var detail = await DetailAsync(id);
                if (detail == null)
                {
                    return Result<ProductionOrderOverviewModel>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
                }
                var db = _unitOfWork.DbContext;
                var o = detail.ProductionOrder;
                var plan = await db.ProductionPlan.AsNoTracking().Where(p => p.Id == o.ProductionPlanId)
                    .Select(p => new { No = p.PlanNumber ?? p.PlanNo, p.ProductionDate }).FirstOrDefaultAsync();
                var batches = detail.ListBatch;
                return Result<ProductionOrderOverviewModel>.Success(new ProductionOrderOverviewModel
                {
                    Detail = detail,
                    PlanNumber = plan?.No,
                    ProductionDate = plan?.ProductionDate,
                    ShiftName = o.ShiftId is { } sid ? await db.Shift.AsNoTracking().Where(s => s.Id == sid).Select(s => s.Name ?? s.Code).FirstOrDefaultAsync() : null,
                    ProductionLineName = o.ProductionLineId is { } lid ? await db.ProductionLine.AsNoTracking().Where(l => l.Id == lid).Select(l => l.Name).FirstOrDefaultAsync() : null,
                    ResponsibleName = o.ResponsibleEmployeeId is { } eid ? await db.Employee.AsNoTracking().Where(e => e.Id == eid).Select(e => e.FullName).FirstOrDefaultAsync() : null,
                    BatchCount = batches.Count(b => b.Status != ProductionBatchStatus.Cancelled),
                    BatchDoneCount = batches.Count(b => b.Status is ProductionBatchStatus.Passed or ProductionBatchStatus.Packed),
                    BatchFailedCount = batches.Count(b => b.Status == ProductionBatchStatus.Failed),
                    AcceptedPortions = batches.Sum(b => b.AcceptedQuantity ?? 0m),
                    ShortReservationCount = detail.ListReservation.Count(r => r.ShortQuantity > 0),
                    VarianceOverCount = detail.ListVariance.Count(v => v.IsOverThreshold),
                }, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetOverview: ", ex.ToString()));
                throw;
            }
        }

        private async Task<ProductionOrderDetailModel?> DetailAsync(Guid id)
        {
            var db = _unitOfWork.DbContext;
            var order = await db.ProductionOrder.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted);
            if (order == null)
            {
                return null;
            }
            var batches = await db.ProductionBatch.AsNoTracking().Where(b => b.ProductionOrderId == id && !b.IsDeleted).ToListAsync();
            var issues = await db.MaterialIssue.AsNoTracking().Where(i => i.ProductionOrderId == id && !i.IsDeleted).OrderBy(i => i.CreatedDate).ToListAsync();
            var ingredientIds = ProductionRules.ParseSnapshot(order.RecipeSnapshotJson).Select(l => l.IngredientId).Distinct().ToList();
            var ingredients = await db.Ingredient.AsNoTracking().Where(i => ingredientIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.Name);
            var units = await db.UnitMeasure.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.Name ?? u.Code);
            var variances = await db.MaterialConsumptionVariance.AsNoTracking().Where(v => !v.IsDeleted && v.ProductionOrderId == id).ToListAsync();
            var stored = await CostingRules.StoredAsync(db, [id]);
            var detail = new ProductionOrderDetailModel
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
                ListReservation = await ReservationRules.ModelsAsync(db, [id]),
                ListVariance = await CostingRules.VarianceModelsAsync(db, variances, _rules),
                ActualCost = stored.GetValueOrDefault(id),
                AllowedActions = order.Status switch
                {
                    ProductionOrderStatus.Planned or ProductionOrderStatus.Released => ["reserve", "cancel"],
                    ProductionOrderStatus.InProgress => ["reserve", "calculateCost"],
                    ProductionOrderStatus.Completed => ["calculateCost", "close"],
                    ProductionOrderStatus.Closed => ["calculateCost"],
                    _ => [],
                },
            };
            if (order.Status == ProductionOrderStatus.Released && batches.Any(b => b.Status != ProductionBatchStatus.Planned))
            {
                detail.AllowedActions.Remove("cancel");
            }
            return detail;
        }

        /// <summary>
        /// BE2.4 (spec 8.2 cancel, Catalog 145): hủy lệnh SX Phát hành chưa nấu (mọi mẻ còn Kế hoạch) và chưa xuất kho (phiếu xuất Đã xuất → chặn, hủy phiếu trước),
        /// lý do bắt buộc, 1 transaction: phiếu xuất Nháp → Hủy; mẻ → Hủy; giải phóng giữ chỗ; lệnh → Hủy (Note "Hủy: …").
        /// Ca của kế hoạch không còn lệnh nào → kế hoạch Đã phát hành trở về Đã duyệt (phát hành lại ca đó được); mọi lệnh còn lại đã xong → kế hoạch Hoàn thành.
        /// Đơn hàng đã Đang SX giữ nguyên (cảnh báo). PROPOSED – BA/PO CONFIRM (D-53 c).
        /// </summary>
        public async Task<IResult<ProductionOrderDetailModel>> Cancel(ProductionOrderCancelRequest request)
        {
            string? reason = request.Reason?.Trim();
            if (string.IsNullOrEmpty(reason) || reason.Length < 3)
            {
                return Result<ProductionOrderDetailModel>.Fail(ConstantHelper.Message.ProductionOrderCancelReasonRequired);
            }
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                _currentActor.RequireActor();
                var db = _unitOfWork.DbContext;
                var order = _unitOfWork.ProductionOrderRepository.GetSingle(o => o.Id == request.Id && !o.IsDeleted);
                if (order == null)
                {
                    return Fail(ConstantHelper.Message.ProductionOrderNotFound);
                }
                string number = order.ProductionOrderNumber ?? order.ProductionOrderNo ?? order.Id.ToString("N")[..8];
                if (order.Status is not (ProductionOrderStatus.Planned or ProductionOrderStatus.Released))
                {
                    return Fail(InvalidStatus(order, "hủy lệnh sản xuất", ProductionOrderStatus.Released));
                }
                var batches = _unitOfWork.ProductionBatchRepository.GetAll().Where(b => b.ProductionOrderId == order.Id && !b.IsDeleted).ToList();
                var started = batches.Where(b => b.Status != ProductionBatchStatus.Planned && b.Status != ProductionBatchStatus.Cancelled).ToList();
                if (started.Count != 0)
                {
                    return Fail(string.Format(ConstantHelper.Message.ProductionOrderCancelStarted, number, string.Join(", ", started.Select(b => b.BatchNumber ?? b.BatchNo))));
                }
                var issues = _unitOfWork.MaterialIssueRepository.GetAll().Where(i => i.ProductionOrderId == order.Id && !i.IsDeleted).ToList();
                var issued = issues.Where(i => i.Status == MaterialIssueStatus.Issued).ToList();
                if (issued.Count != 0)
                {
                    return Fail(string.Format(ConstantHelper.Message.ProductionOrderCancelIssued, number, string.Join(", ", issued.Select(i => i.IssueNumber ?? i.IssueNo))));
                }
                var warnings = new List<string>();
                foreach (var issue in issues.Where(i => i.Status == MaterialIssueStatus.Draft))
                {
                    issue.Status = MaterialIssueStatus.Cancelled;
                    issue.Note = PurchasingRules.AppendNote(issue.Note, $"Hủy theo lệnh SX {number}: {reason}");
                    _unitOfWork.MaterialIssueRepository.Update(issue);
                }
                foreach (var batch in batches.Where(b => b.Status == ProductionBatchStatus.Planned))
                {
                    batch.Status = ProductionBatchStatus.Cancelled;
                    _unitOfWork.ProductionBatchRepository.Update(batch);
                }
                var released = await ReservationRules.ReleaseAsync(db, order.Id);
                order.Status = ProductionOrderStatus.Cancelled;
                order.Note = PurchasingRules.AppendNote(order.Note, "Hủy: " + reason);
                _unitOfWork.ProductionOrderRepository.Update(order);

                var plan = _unitOfWork.ProductionPlanRepository.GetSingle(p => p.Id == order.ProductionPlanId && !p.IsDeleted);
                var others = await db.ProductionOrder.AsNoTracking()
                    .Where(o => o.ProductionPlanId == order.ProductionPlanId && !o.IsDeleted && o.Id != order.Id && o.Status != ProductionOrderStatus.Cancelled).ToListAsync();
                if (plan != null && plan.Status == ProductionPlanStatus.Released && order.ShiftId != null && !others.Any(o => o.ShiftId == order.ShiftId))
                {
                    plan.Status = ProductionPlanStatus.Approved;
                    _unitOfWork.ProductionPlanRepository.Update(plan);
                    string shiftCode = await db.Shift.AsNoTracking().Where(s => s.Id == order.ShiftId).Select(s => s.Code).FirstOrDefaultAsync() ?? string.Empty;
                    warnings.Add(string.Format(ConstantHelper.Message.ProductionOrderPlanReopened, plan.PlanNumber ?? plan.PlanNo, shiftCode));
                }
                else if (plan != null && plan.Status == ProductionPlanStatus.Released && others.Count != 0
                    && others.All(o => o.Status is ProductionOrderStatus.Completed or ProductionOrderStatus.Closed))
                {
                    plan.Status = ProductionPlanStatus.Completed;
                    _unitOfWork.ProductionPlanRepository.Update(plan);
                }
                await _unitOfWork.ProductionOrderRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                await ReservationRules.ReallocateAsync(db, released, _clock.Today);
                _unitOfWork.CommitTransaction();
                var detail = await DetailAsync(order.Id);
                detail!.ListWarning.AddRange(warnings);
                return Result<ProductionOrderDetailModel>.Success(detail,
                    string.Format(ConstantHelper.Message.ProductionOrderCancelled, number, batches.Count(b => b.Status == ProductionBatchStatus.Cancelled), released.Count));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Cancel: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }

            IResult<ProductionOrderDetailModel> Fail(string message)
            {
                _unitOfWork.RollbackTransaction();
                return Result<ProductionOrderDetailModel>.Fail(message);
            }
        }

        /// <summary>
        /// BE2.4 (spec 8.2 close, Catalog 144): Hoàn thành → Đóng trong 1 transaction – sinh chênh lệch tiêu hao (MaterialConsumptionVariance, cập nhật tại chỗ theo nguyên liệu),
        /// tính giá thành thực tế (ActualCost theo mẻ), giải phóng giữ chỗ còn dư (Đã giải phóng), lệnh → Đóng.
        /// </summary>
        public async Task<IResult<ProductionOrderCloseResultModel>> Close(ProductionActionRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                _currentActor.RequireActor();
                var db = _unitOfWork.DbContext;
                var order = _unitOfWork.ProductionOrderRepository.GetSingle(o => o.Id == request.Id && !o.IsDeleted);
                if (order == null)
                {
                    return Fail(ConstantHelper.Message.ProductionOrderNotFound);
                }
                if (order.Status != ProductionOrderStatus.Completed)
                {
                    return Fail(InvalidStatus(order, "đóng lệnh sản xuất", ProductionOrderStatus.Completed));
                }
                var (rows, varianceWarnings) = await CostingRules.VariancesAsync(db, _uomConversionService, order, _rules, _clock.Today);
                var existing = _unitOfWork.MaterialConsumptionVarianceRepository.GetAll().Where(v => v.ProductionOrderId == order.Id && !v.IsDeleted).ToList();
                foreach (var row in rows)
                {
                    var target = existing.FirstOrDefault(v => v.IngredientId == row.IngredientId);
                    if (target == null)
                    {
                        row.Id = Guid.NewGuid();
                        _unitOfWork.MaterialConsumptionVarianceRepository.Add(row);
                        target = row;
                    }
                    else
                    {
                        target.StandardQuantity = row.StandardQuantity;
                        target.TheoreticalQty = row.TheoreticalQty;
                        target.ActualQuantity = row.ActualQuantity;
                        target.ActualQty = row.ActualQty;
                        target.VarianceQuantity = row.VarianceQuantity;
                        target.VarianceQty = row.VarianceQty;
                        target.VariancePercent = row.VariancePercent;
                        target.VarianceCost = row.VarianceCost;
                        target.VarianceValue = row.VarianceValue;
                        target.Reason = row.Reason;
                        _unitOfWork.MaterialConsumptionVarianceRepository.Update(target);
                    }
                }
                var cost = await CostingRules.UpsertAsync(_unitOfWork, order, _rules, _clock.Now);
                var released = await ReservationRules.ReleaseAsync(db, order.Id);
                order.Status = ProductionOrderStatus.Closed;
                if (!string.IsNullOrWhiteSpace(request.Note))
                {
                    order.Note = PurchasingRules.AppendNote(order.Note, "Đóng: " + request.Note.Trim());
                }
                _unitOfWork.ProductionOrderRepository.Update(order);
                await _unitOfWork.ProductionOrderRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                await ReservationRules.ReallocateAsync(db, released, _clock.Today);
                _unitOfWork.CommitTransaction();
                var detail = (await DetailAsync(order.Id))!;
                detail.ListWarning.AddRange(varianceWarnings);
                detail.ListWarning.AddRange(cost.ListWarning);
                var over = detail.ListVariance.Where(v => v.IsOverThreshold).ToList();
                if (over.Count != 0)
                {
                    detail.ListWarning.Add(string.Format(ConstantHelper.Message.VarianceOverThreshold, ProductionRules.Num(_rules.ConsumptionVariancePctWarning),
                        string.Join(", ", over.Select(v => $"{v.IngredientName} {ProductionRules.Num(v.VariancePercent ?? 0)}%"))));
                }
                return Result<ProductionOrderCloseResultModel>.Success(new ProductionOrderCloseResultModel
                {
                    Detail = detail,
                    ActualCost = detail.ActualCost ?? cost,
                    ListVariance = detail.ListVariance,
                }, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Close: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }

            IResult<ProductionOrderCloseResultModel> Fail(string message)
            {
                _unitOfWork.RollbackTransaction();
                return Result<ProductionOrderCloseResultModel>.Fail(message);
            }
        }

        /// <summary>
        /// BE2.4 (spec 8.5 orders/{id}/calculate-cost, Catalog 153): tính (lại) giá thành thực tế của lệnh Đang SX / Hoàn thành / Đóng – ActualCost 1 dòng / mẻ
        /// (cập nhật tại chỗ), trả tổng của lệnh + từng mẻ.
        /// </summary>
        public async Task<IResult<ActualCostListModel>> CalculateCost(ProductionActionRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                _currentActor.RequireActor();
                var order = _unitOfWork.ProductionOrderRepository.GetSingle(o => o.Id == request.Id && !o.IsDeleted);
                if (order == null)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<ActualCostListModel>.Fail(ConstantHelper.Message.ProductionOrderNotFound);
                }
                if (order.Status is not (ProductionOrderStatus.InProgress or ProductionOrderStatus.Completed or ProductionOrderStatus.Closed))
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<ActualCostListModel>.Fail(InvalidStatus(order, "tính giá thành thực tế",
                        ProductionOrderStatus.InProgress, ProductionOrderStatus.Completed, ProductionOrderStatus.Closed));
                }
                var total = await CostingRules.UpsertAsync(_unitOfWork, order, _rules, _clock.Now);
                await _unitOfWork.ActualCostRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                var (batches, _) = await CostingRules.ComputeAsync(_unitOfWork.DbContext, order, _rules);
                foreach (var b in batches)
                {
                    b.Cost.CalculatedAt = total.CalculatedAt;
                }
                return Result<ActualCostListModel>.Success(new ActualCostListModel { Breakdown = total, ListData = [.. batches.Select(b => b.Cost)] }, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " CalculateCost: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        /// <summary>
        /// BE2.4 (Catalog 126): giữ chỗ (bổ sung) nguyên liệu cho lệnh Phát hành / Đang SX – dòng giữ chưa xuất được giữ thêm tới đủ số cần (sau khi có hàng về),
        /// nguyên liệu chưa có dòng giữ thì tạo mới. Thiếu → cảnh báo.
        /// </summary>
        public async Task<IResult<ProductionOrderDetailModel>> Reserve(ProductionActionRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                _currentActor.RequireActor();
                var db = _unitOfWork.DbContext;
                var order = await db.ProductionOrder.AsNoTracking().FirstOrDefaultAsync(o => o.Id == request.Id && !o.IsDeleted);
                if (order == null)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<ProductionOrderDetailModel>.Fail(ConstantHelper.Message.ProductionOrderNotFound);
                }
                if (order.Status is not (ProductionOrderStatus.Released or ProductionOrderStatus.InProgress))
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<ProductionOrderDetailModel>.Fail(InvalidStatus(order, "giữ chỗ nguyên liệu", ProductionOrderStatus.Released, ProductionOrderStatus.InProgress));
                }
                DateTime date = await db.ProductionPlan.AsNoTracking().Where(p => p.Id == order.ProductionPlanId).Select(p => p.ProductionDate).FirstOrDefaultAsync();
                var (touched, warnings) = await ReservationRules.ReserveAsync(db, _uomConversionService, [order], date, _clock.Today, _rules,
                    r => _unitOfWork.MaterialReservationRepository.Add(r));
                await _unitOfWork.MaterialReservationRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                await ReservationRules.ReallocateAsync(db, touched, _clock.Today);
                _unitOfWork.CommitTransaction();
                var detail = (await DetailAsync(order.Id))!;
                detail.ListWarning.AddRange(warnings);
                return Result<ProductionOrderDetailModel>.Success(detail, string.Format(ConstantHelper.Message.ReservationDone, order.ProductionOrderNumber ?? order.ProductionOrderNo));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Reserve: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        private static string InvalidStatus(ProductionOrder order, string action, params string[] expected)
            => string.Format(ConstantHelper.Message.StatusActionInvalid, action, "lệnh " + (order.ProductionOrderNumber ?? order.ProductionOrderNo),
                string.Join(" / ", expected.Select(ProductionOrderStatus.Label)), ProductionOrderStatus.Label(order.Status));
    }
}
