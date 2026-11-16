using AutoMapper;
using Dapper;
using System.Globalization;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Model.Request;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Model.SearchRequest;
using AppDelicare.Model.ViewModels;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using System.Net;

namespace AppDelicare.Services.Implement
{
    public class ProductionPlanService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService, IDocumentNumberService documentNumberService, IClock clock, ICurrentActor currentActor
        , IOptions<BusinessRulesOptions> rules) : IProductionPlanService
    {
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IDapperRepository _dapperRepository = dapperRepository;
        private readonly IMapper _mapper = mapper;
        private readonly IAuditChangeService _auditChangeService = auditChangeService;
        private readonly IDocumentNumberService _documentNumberService = documentNumberService;
        private readonly IClock _clock = clock;
        private readonly ICurrentActor _currentActor = currentActor;
        private readonly BusinessRulesOptions _rules = rules.Value;

        public async Task<IResults<ProductionPlanViewModel>> GetAll(ProductionPlanSearchRequest request)
        {
            try
            {
                DynamicParameters dynamicParameters = new();
                dynamicParameters.Add("@SearchText", request.SearchText);
                //dynamicParameters.Add("@PlanNumber", request.PlanNumber);
                dynamicParameters.Add("@PlanDate", request.PlanDate);
                dynamicParameters.Add("@OrganizationUnitId", request.OrganizationUnitId);
                dynamicParameters.Add("@Status", request.Status);
                //dynamicParameters.Add("@Note", request.Note);
                dynamicParameters.Add(ConstantHelper.Page, request.Page);
                dynamicParameters.Add(ConstantHelper.PageSize, request.PageSize);
                dynamicParameters.Add(ConstantHelper.SortField, request.SortField);
                dynamicParameters.Add(ConstantHelper.SortOrder, request.SortOrder);
                var list = (await _dapperRepository.QueryMultipleUsingStoreProcAsync<ProductionPlanViewModel>("uspProductionPlan_GetAll", dynamicParameters)).ToList();
                if (list.Count != 0)
                {
                    int i = 0;
                    list.ForEach(x =>
                    {
                        i++;
                        x.STT = (request.Page - 1) * request.PageSize + i;
                    });
                }
                return new Results<ProductionPlanViewModel>
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

        public IResults<ProductionPlan> GetAllData()
        {
            try
            {
                var list = _unitOfWork.ProductionPlanRepository.GetAll().Where(x => !x.IsDeleted).ToList();
                return new Results<ProductionPlan>
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

        public async Task<IResult<bool>> Save(ProductionPlanRequest request, string typeSave)
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

        /// <summary>T17: kế hoạch chỉ sinh từ MaterialRequirement/Calculate (KH- theo ngày, kèm nhu cầu NL) – CRUD Insert không ghi.</summary>
        private Task<string> Insert(ProductionPlanRequest request, string typeSave)
            => Task.FromResult(ConstantHelper.Message.PlanInsertBlocked);

        /// <summary>T17: trạng thái / số / ngày chỉ đổi qua Approve / Release; Note giữ kết quả MRP (JSON) – CRUD Update không ghi.</summary>
        private Task<string> Update(ProductionPlanRequest request, string typeSave)
            => Task.FromResult(ConstantHelper.Message.PlanUpdateBlocked);

        /// <summary>T17: chỉ xóa (mềm) kế hoạch Nháp chưa có lệnh SX / nhu cầu NL tham chiếu (ReferenceGuard).</summary>
        private async Task<string> Delete(Guid? id, string typeSave)
        {
            if (id == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            var delete = _unitOfWork.ProductionPlanRepository.GetSingle(x => x.Id == id && !x.IsDeleted);
            if (delete == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            if (delete.Status != ProductionPlanStatus.Draft)
            {
                return InvalidStatus(delete, "xóa kế hoạch", ProductionPlanStatus.Draft);
            }
            // T07: ReferenceGuard (quan hệ logic, truy vấn có tham số) thay SQL động uspSystem_ExecuteQuery (A10).
            if (await _auditChangeService.IsReferencedAsync(nameof(ProductionPlan), (Guid)id))
            {
                return ConstantHelper.Message.DeleteExistFail;
            }
            _unitOfWork.ProductionPlanRepository.Update(delete);
            await _unitOfWork.ProductionPlanRepository.CommitAsync(typeSave);
            return string.Empty;
        }

        public async Task<IResult<ProductionPlan>> GetById(Guid id)
        {
            try
            {
                var data = await _unitOfWork.ProductionPlanRepository.GetSingleAsync(x => x.Id == id && !x.IsDeleted);
                if (data == null)
                {
                    return new Result<ProductionPlan>
                    {
                        ResponseCode = Convert.ToInt32(HttpStatusCode.BadRequest),
                        ResponseMessage = ConstantHelper.Message.RecordNotFoundMessage,
                        Data = null,
                    };
                }
                return new Result<ProductionPlan>
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

        private static string InvalidStatus(ProductionPlan plan, string action, params string[] expected)
            => string.Format(ConstantHelper.Message.StatusActionInvalid, action, "kế hoạch " + (plan.PlanNumber ?? plan.PlanNo),
                string.Join(" / ", expected.Select(ProductionPlanStatus.Label)), ProductionPlanStatus.Label(plan.Status));

        /// <summary>
        /// T17 (spec 8.1 approve, Pilot P7): xác nhận kế hoạch KH- của ngày – Nháp → Đã duyệt (1 cấp, quyền Duyệt trên mục "Duyệt – Kế hoạch sản xuất").
        /// Từ lúc này MRP không tính lại kế hoạch (T14). Người duyệt = ModifiedBy (ProductionPlan không có cột ApprovedBy – GAP-PRD-06).
        /// </summary>
        public async Task<IResult<ProductionPlanDetailModel>> Approve(ProductionActionRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                _currentActor.RequireActor();
                var plan = _unitOfWork.ProductionPlanRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
                if (plan == null || plan.Status != ProductionPlanStatus.Draft)
                {
                    return Fail(plan == null ? ConstantHelper.Message.RecordNotFoundMessage : InvalidStatus(plan, "duyệt kế hoạch", ProductionPlanStatus.Draft));
                }
                if (!_unitOfWork.ProductionPlanLineRepository.GetAll().Any(l => l.ProductionPlanId == plan.Id && !l.IsDeleted && l.PlannedQuantity > 0))
                {
                    return Fail(ConstantHelper.Message.PlanNeedsLine);
                }
                plan.Status = ProductionPlanStatus.Approved;
                _unitOfWork.ProductionPlanRepository.Update(plan);
                await _unitOfWork.ProductionPlanRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                return Result<ProductionPlanDetailModel>.Success(await DetailAsync(plan.Id), ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Approve: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }

            IResult<ProductionPlanDetailModel> Fail(string message)
            {
                _unitOfWork.RollbackTransaction();
                return Result<ProductionPlanDetailModel>.Fail(message);
            }
        }

        /// <summary>
        /// T17 (spec 8.1 release, BUSINESS_RULES §6, Pilot P7) – phát hành kế hoạch Đã duyệt trong 1 transaction:
        /// <list type="number">
        /// <item>Ca: chọn hoặc mặc định <c>BusinessRules:ProductionDefaultShiftCode</c> (Pilot 1 ca – kế hoạch MRP theo ngày, GAP-PRD-01).</item>
        /// <item>Giờ xuất bếp: nhập tay, hoặc giờ giao sớm nhất của điểm giao (đơn Đã chốt của ngày, dòng thuộc ca) − <c>TransportMinutesBeforeCutoff</c> (60′),
        /// không có thì giờ bắt đầu ca − 60′. Mẻ cuối mỗi món xong lúc giờ xuất bếp − <c>ProductionPackingMinutes</c> (chia suất / đóng gói).</item>
        /// <item>Mỗi dòng món → 1 lệnh SX <c>PH-</c> (Released) với công thức khóa tại ngày (thiếu → chặn, Đã duyệt → cảnh báo) + ảnh chụp định lượng.</item>
        /// <item>Chia mẻ theo dây chuyền (<c>CapacityMealPerBatch</c>, <c>MinutesPerBatch</c>; không chọn → 1 mẻ / món, <c>ProductionDefaultMinutesPerBatch</c>);
        /// lịch ngược, các mẻ của món nối tiếp; mã <c>ME-YYMMDD-&lt;CA&gt;-NN</c> theo giờ bắt đầu (StartDate / EndDate = giờ kế hoạch – GAP-PRD-03).</item>
        /// <item>Kế hoạch → Đã phát hành; dòng kế hoạch gắn ca; đơn hàng Đã chốt của ngày (có dòng thuộc ca) → Đang sản xuất.</item>
        /// </list>
        /// </summary>
        public async Task<IResult<ProductionPlanDetailModel>> Release(ProductionPlanReleaseRequest request)
        {
            TimeSpan? dispatchTime = null;
            if (!string.IsNullOrWhiteSpace(request.DispatchTime))
            {
                if (!TimeSpan.TryParseExact(request.DispatchTime.Trim(), [@"hh\:mm", @"h\:mm"], CultureInfo.InvariantCulture, out var t))
                {
                    return Result<ProductionPlanDetailModel>.Fail(ConstantHelper.Message.DispatchTimeInvalid);
                }
                dispatchTime = t;
            }
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                _currentActor.RequireActor();
                var db = _unitOfWork.DbContext;
                var plan = _unitOfWork.ProductionPlanRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
                if (plan == null || plan.Status != ProductionPlanStatus.Approved)
                {
                    return Fail(plan == null ? ConstantHelper.Message.RecordNotFoundMessage : InvalidStatus(plan, "phát hành kế hoạch", ProductionPlanStatus.Approved));
                }
                var planLines = _unitOfWork.ProductionPlanLineRepository.GetAll().Where(l => l.ProductionPlanId == plan.Id && !l.IsDeleted && l.PlannedQuantity > 0).ToList();
                if (planLines.Count == 0)
                {
                    return Fail(ConstantHelper.Message.PlanNeedsLine);
                }
                var shift = request.ShiftId is { } sid && sid != Guid.Empty
                    ? await db.Shift.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sid && !s.IsDeleted)
                    : await db.Shift.AsNoTracking().FirstOrDefaultAsync(s => s.Code == _rules.ProductionDefaultShiftCode && !s.IsDeleted);
                if (shift == null)
                {
                    return Fail(ConstantHelper.Message.ShiftNotFound);
                }
                ProductionLine? line = null;
                if (request.ProductionLineId is { } lid && lid != Guid.Empty)
                {
                    line = await db.ProductionLine.AsNoTracking().FirstOrDefaultAsync(l => l.Id == lid && !l.IsDeleted && l.Status != "Inactive");
                    if (line == null)
                    {
                        return Fail(ConstantHelper.Message.ProductionLineNotFound);
                    }
                }
                DateTime date = plan.ProductionDate.Date;
                var (dispatchAt, source) = await ProductionRules.ResolveDispatchAsync(db, date, shift, dispatchTime, _rules);
                if (dispatchAt == null)
                {
                    return Fail(ConstantHelper.Message.DispatchTimeMissing);
                }
                DateTime cookEnd = dispatchAt.Value.AddMinutes(-_rules.ProductionPackingMinutes);
                int minutes = line?.MinutesPerBatch is > 0 ? (int)Math.Ceiling(line.MinutesPerBatch.Value) : _rules.ProductionDefaultMinutesPerBatch;

                // 1) Công thức + lịch theo món.
                var dishIds = planLines.Select(l => l.DishId).Distinct().ToList();
                var dishNames = await db.Dish.AsNoTracking().Where(d => dishIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name ?? d.Code ?? string.Empty);
                var warnings = new List<string>();
                var missing = new List<string>();
                var work = new List<(ProductionPlanLine Line, Recipe Recipe, string Snapshot, List<ProductionRules.ScheduledBatch> Batches)>();
                foreach (var planLine in planLines.OrderBy(l => dishNames.GetValueOrDefault(l.DishId)))
                {
                    string dishName = dishNames.GetValueOrDefault(planLine.DishId) ?? planLine.DishId.ToString();
                    var (recipe, locked, snapshot) = await ProductionRules.SelectRecipeAsync(db, planLine.DishId, date);
                    if (recipe == null || snapshot.Count == 0)
                    {
                        missing.Add(dishName);
                        continue;
                    }
                    if (!locked)
                    {
                        warnings.Add(string.Format(ConstantHelper.Message.ReleaseRecipeNotLocked, dishName));
                    }
                    string json = locked && ProductionRules.ParseSnapshot(recipe.SnapshotJson).Count != 0
                        ? recipe.SnapshotJson! : Newtonsoft.Json.JsonConvert.SerializeObject(snapshot);
                    work.Add((planLine, recipe, json, ProductionRules.Schedule(planLine.PlannedQuantity, line?.CapacityMealPerBatch, minutes, cookEnd)));
                }
                if (missing.Count != 0)
                {
                    return Fail(string.Format(ConstantHelper.Message.ReleaseNoRecipe, string.Join(", ", missing)));
                }

                // 2) Lệnh SX (PH-) + mẻ (ME-…-<CA>-NN) – sinh mã đầu rồi cộng dần (sp_getapplock giữ tới khi commit).
                string planNo = plan.PlanNumber ?? plan.PlanNo ?? string.Empty;
                string firstOrderNo = await _documentNumberService.NextAsync(DocumentKind.Voucher, _clock.Today);
                string firstBatchNo = await _documentNumberService.NextAsync(DocumentKind.ProductionBatch, date, shift.Code);
                string orderNote = $"Kế hoạch {planNo} – ca {shift.Code} – xuất bếp {dispatchAt.Value.ToString("HH:mm dd/MM/yyyy", CultureInfo.InvariantCulture)} ({source})"
                    + (string.IsNullOrWhiteSpace(request.Note) ? string.Empty : " – " + request.Note.Trim());
                var allBatches = new List<(ProductionOrder Order, string DishName, ProductionRules.ScheduledBatch Batch)>();
                int orderIndex = 0;
                foreach (var (planLine, recipe, snapshot, batches) in work)
                {
                    string number = ProductionRules.Increment(firstOrderNo, orderIndex++);
                    var order = new ProductionOrder
                    {
                        Id = Guid.NewGuid(),
                        ProductionOrderNumber = number,
                        ProductionOrderNo = number,
                        ProductionPlanId = plan.Id,
                        DishId = planLine.DishId,
                        RecipeId = recipe.Id,
                        ShiftId = shift.Id,
                        ProductionLineId = line?.Id,
                        PlannedQty = planLine.PlannedQuantity,
                        Status = ProductionOrderStatus.Released,
                        PlannedStartDate = batches.Min(b => b.Start),
                        StartTime = batches.Min(b => b.Start),
                        EndTime = batches.Max(b => b.End),
                        RecipeSnapshotJson = snapshot,
                        Note = orderNote,
                    };
                    _unitOfWork.ProductionOrderRepository.Add(order);
                    allBatches.AddRange(batches.Select(b => (order, dishNames.GetValueOrDefault(planLine.DishId) ?? string.Empty, b)));
                    planLine.ShiftId = shift.Id;
                    planLine.Status = ProductionPlanStatus.Released;
                    _unitOfWork.ProductionPlanLineRepository.Update(planLine);
                }
                int batchIndex = 0;
                foreach (var (order, dishName, scheduled) in allBatches.OrderBy(x => x.Batch.Start).ThenBy(x => x.DishName).ThenBy(x => x.Batch.Index))
                {
                    string number = ProductionRules.Increment(firstBatchNo, batchIndex++);
                    _unitOfWork.ProductionBatchRepository.Add(new ProductionBatch
                    {
                        Id = Guid.NewGuid(),
                        BatchNumber = number,
                        BatchNo = number,
                        ProductionOrderId = order.Id,
                        DishId = order.DishId,
                        RecipeId = order.RecipeId,
                        PlannedQty = scheduled.Quantity,
                        PlannedQuantity = scheduled.Quantity,
                        StartDate = scheduled.Start,
                        EndDate = scheduled.End,
                        Status = ProductionBatchStatus.Planned,
                    });
                    if (scheduled.Start < _clock.Now)
                    {
                        warnings.Add(string.Format(ConstantHelper.Message.ReleaseStartInPast, number, ProductionRules.Time(scheduled.Start)));
                    }
                }

                // 3) Kế hoạch → Đã phát hành; đơn Đã chốt của ngày (có dòng thuộc ca) → Đang sản xuất.
                plan.Status = ProductionPlanStatus.Released;
                _unitOfWork.ProductionPlanRepository.Update(plan);
                DateTime next = date.AddDays(1);
                var lockedOrders = _unitOfWork.OrderRepository.GetAll().Where(o => !o.IsDeleted && o.MealDate >= date && o.MealDate < next && o.Status == OrderStatus.Locked).ToList();
                var lockedIds = lockedOrders.Select(o => o.Id).ToList();
                var inShift = db.OrderLine.AsNoTracking().Where(l => lockedIds.Contains(l.OrderId) && !l.IsDeleted && (l.ShiftId == null || l.ShiftId == shift.Id))
                    .Select(l => l.OrderId).Distinct().ToHashSet();
                foreach (var customerOrder in lockedOrders.Where(o => inShift.Contains(o.Id)))
                {
                    customerOrder.Status = OrderStatus.InProduction;
                    _unitOfWork.OrderRepository.Update(customerOrder);
                }
                await _unitOfWork.ProductionPlanRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                var detail = await DetailAsync(plan.Id);
                detail!.ListWarning.AddRange(warnings);
                return Result<ProductionPlanDetailModel>.Success(detail, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Release: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }

            IResult<ProductionPlanDetailModel> Fail(string message)
            {
                _unitOfWork.RollbackTransaction();
                return Result<ProductionPlanDetailModel>.Fail(message);
            }
        }

        /// <summary>T17: chi tiết kế hoạch – dòng món, lệnh SX, mẻ (lịch + ATTP), thao tác được phép.</summary>
        public async Task<IResult<ProductionPlanDetailModel>> GetDetail(Guid id)
        {
            try
            {
                var detail = await DetailAsync(id);
                return detail == null ? Result<ProductionPlanDetailModel>.Fail(ConstantHelper.Message.RecordNotFoundMessage)
                    : Result<ProductionPlanDetailModel>.Success(detail, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetDetail: ", ex.ToString()));
                throw;
            }
        }

        private async Task<ProductionPlanDetailModel?> DetailAsync(Guid id)
        {
            var db = _unitOfWork.DbContext;
            var plan = await db.ProductionPlan.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
            if (plan == null)
            {
                return null;
            }
            var lines = await db.ProductionPlanLine.AsNoTracking().Where(l => l.ProductionPlanId == id && !l.IsDeleted).ToListAsync();
            var dishIds = lines.Select(l => l.DishId).Distinct().ToList();
            var dishes = await db.Dish.AsNoTracking().Where(d => dishIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id);
            var orders = await db.ProductionOrder.AsNoTracking().Where(o => o.ProductionPlanId == id && !o.IsDeleted).ToListAsync();
            var orderIds = orders.Select(o => o.Id).ToList();
            var batches = await db.ProductionBatch.AsNoTracking().Where(b => orderIds.Contains(b.ProductionOrderId) && !b.IsDeleted).ToListAsync();
            return new ProductionPlanDetailModel
            {
                ProductionPlan = plan,
                StatusName = ProductionPlanStatus.Label(plan.Status),
                TotalPortions = lines.Sum(l => l.PlannedQuantity),
                ListLine = [.. lines.Select(l => new ProductionPlanLineDetailModel
                {
                    Id = l.Id,
                    DishId = l.DishId,
                    DishCode = dishes.GetValueOrDefault(l.DishId)?.Code,
                    DishName = dishes.GetValueOrDefault(l.DishId)?.Name,
                    PlannedQuantity = l.PlannedQuantity,
                    ShiftId = l.ShiftId,
                    Status = l.Status,
                }).OrderBy(l => l.DishName)],
                ListOrder = await ProductionRules.OrderBriefsAsync(db, orders),
                ListBatch = await ProductionRules.BatchBriefsAsync(db, batches, _rules),
                DispatchAt = batches.Count == 0 ? null : batches.Max(b => b.EndDate ?? b.StartDate).AddMinutes(_rules.ProductionPackingMinutes),
                AllowedActions = plan.Status switch
                {
                    ProductionPlanStatus.Draft => ["approve"],
                    ProductionPlanStatus.Approved => ["release"],
                    ProductionPlanStatus.Released or ProductionPlanStatus.Completed => ["exportProductionSheet"],
                    _ => [],
                },
            };
        }

        /// <summary>T17: in "Phiếu sản xuất" (.xlsx) cho cả kế hoạch – mỗi mẻ 1 nhóm dòng nguyên liệu (định mức / SL cần / thực xuất).</summary>
        public async Task<IResult<byte[]>> ExportProductionSheet(Guid id)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                var plan = await db.ProductionPlan.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
                if (plan == null)
                {
                    return Result<byte[]>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
                }
                var orders = await db.ProductionOrder.AsNoTracking().Where(o => o.ProductionPlanId == id && !o.IsDeleted).ToListAsync();
                if (orders.Count == 0)
                {
                    return Result<byte[]>.Fail(InvalidStatus(plan, "in phiếu sản xuất", ProductionPlanStatus.Released, ProductionPlanStatus.Completed));
                }
                var orderIds = orders.Select(o => o.Id).ToList();
                var batches = await db.ProductionBatch.AsNoTracking().Where(b => orderIds.Contains(b.ProductionOrderId) && !b.IsDeleted).ToListAsync();
                var sheet = await ProductionRules.SheetBatchesAsync(db, orders, batches, _rules);
                var company = await db.Company.AsNoTracking().Where(c => !c.IsDeleted).OrderBy(c => c.CreatedDate).FirstOrDefaultAsync();
                byte[] file = ProductionRules.BuildProductionSheet(company?.Name, plan.ProductionDate, $"Kế hoạch {plan.PlanNumber ?? plan.PlanNo}", sheet);
                return Result<byte[]>.Success(file, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ExportProductionSheet: ", ex.ToString()));
                throw;
            }
        }
    }
}
