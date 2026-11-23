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
    public partial class ProductionPlanService(IUnitOfWork unitOfWork, IDapperRepository dapperRepository, IMapper mapper
        , IAuditChangeService auditChangeService, IDocumentNumberService documentNumberService, IClock clock, ICurrentActor currentActor
        , IOptions<BusinessRulesOptions> rules, IUomConversionService uomConversionService, INotificationService notifications, IServiceProvider services) : IProductionPlanService
    {
        private readonly IServiceProvider _services = services;
        private readonly INotificationService _notifications = notifications;
        private readonly IUomConversionService _uomConversionService = uomConversionService;
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
        /// T17 (spec 8.1 approve, Pilot P7): xác nhận kế hoạch KH- của ngày – Nháp → Đã duyệt (1 cấp, quyền Duyệt trên mục "Phê duyệt › Kế hoạch sản xuất").
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
        /// T17 (spec 8.1 release, BUSINESS_RULES §6, Pilot P7) + T17.1 (anh chốt 2026-11-16: Pilot chạy 4 ca / ngày) – phát hành kế hoạch Đã duyệt trong 1 transaction:
        /// <list type="number">
        /// <item>Kế hoạch MRP vẫn 1 KH- / ngày; số phần từng món chia theo ca của đơn Đã chốt (<c>OrderLine.ShiftId</c>; chưa gán ca → <c>ProductionDefaultShiftCode</c>)
        /// – <see cref="ProductionRules.SplitByShiftAsync"/>. <c>ShiftId</c> = chỉ phát hành ca đó; bỏ trống = mọi ca chưa phát hành.</item>
        /// <item>Mỗi ca giờ xuất bếp riêng: nhập tay (chỉ khi phát hành 1 ca), hoặc giờ giao sớm nhất của điểm giao của ca − <c>TransportMinutesBeforeCutoff</c> (60′),
        /// không có thì giờ bắt đầu ca − 60′. Mẻ cuối mỗi món xong lúc giờ xuất bếp − <c>ProductionPackingMinutes</c> (30′); các món song song (anh chốt, GAP-PRD-07).</item>
        /// <item>Mỗi ca × món → 1 lệnh SX <c>PH-</c> (Released) với công thức khóa tại ngày (thiếu → chặn, Đã duyệt → cảnh báo) + ảnh chụp định lượng.</item>
        /// <item>Chia mẻ đều theo dây chuyền (<c>CapacityMealPerBatch</c>, <c>MinutesPerBatch</c>; không chọn → 1 mẻ / món, <c>ProductionDefaultMinutesPerBatch</c>);
        /// lịch ngược, các mẻ của món nối tiếp; mã <c>ME-YYMMDD-&lt;CA&gt;-NN</c> theo ca, theo giờ bắt đầu (StartDate / EndDate = giờ kế hoạch – GAP-PRD-03).</item>
        /// <item>Ca đã phát hành = có lệnh SX của kế hoạch gắn ca (không cột mới). Kế hoạch → Đã phát hành khi mọi ca có nhu cầu đã phát hành, còn ca thì giữ Đã duyệt
        /// (PROPOSED – BA/PO CONFIRM); dòng kế hoạch Released khi mọi ca của món đã phát hành; đơn Đã chốt có dòng thuộc ca vừa phát hành → Đang sản xuất.</item>
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
                DateTime date = plan.ProductionDate.Date;
                var warnings = new List<string>();

                // 0) Chia dòng món theo ca (T17.1) → ca cần phát hành ở lần gọi này.
                var (parts, fallback, splitWarnings, splitError) = await ProductionRules.SplitByShiftAsync(db, date, planLines, _rules);
                if (splitError != null)
                {
                    return Fail(splitError);
                }
                warnings.AddRange(splitWarnings);
                var released = await ReleasedShiftIdsAsync(plan.Id);
                var pending = parts.Where(p => !released.Contains(p.Shift.Id)).ToList();
                List<ProductionRules.ShiftPart> toRelease;
                if (request.ShiftId is { } sid && sid != Guid.Empty)
                {
                    var chosen = await db.Shift.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sid && !s.IsDeleted);
                    if (chosen == null)
                    {
                        return Fail(ConstantHelper.Message.ShiftNotFound);
                    }
                    if (released.Contains(sid))
                    {
                        return Fail(string.Format(ConstantHelper.Message.ReleaseShiftAlready, chosen.Code));
                    }
                    var part = pending.FirstOrDefault(p => p.Shift.Id == sid);
                    if (part == null)
                    {
                        return Fail(string.Format(ConstantHelper.Message.ReleaseShiftNoDemand, chosen.Code));
                    }
                    toRelease = [part];
                }
                else
                {
                    if (pending.Count == 0)
                    {
                        return Fail(ConstantHelper.Message.ReleaseNothingPending);
                    }
                    if (dispatchTime != null && pending.Count > 1)
                    {
                        return Fail(string.Format(ConstantHelper.Message.ReleaseDispatchNeedsShift, pending.Count, string.Join(", ", pending.Select(p => p.Shift.Code))));
                    }
                    toRelease = pending;
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
                int minutes = line?.MinutesPerBatch is > 0 ? (int)Math.Ceiling(line.MinutesPerBatch.Value) : _rules.ProductionDefaultMinutesPerBatch;

                // 1) Công thức theo món (các món của ca sẽ phát hành).
                var dishIds = toRelease.SelectMany(p => p.Lines).Select(l => l.Line.DishId).Distinct().ToList();
                var dishNames = await db.Dish.AsNoTracking().Where(d => dishIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name ?? d.Code ?? string.Empty);
                var missing = new List<string>();
                var recipes = new Dictionary<Guid, (Recipe Recipe, string Snapshot)>();
                foreach (Guid dishId in dishIds.OrderBy(d => dishNames.GetValueOrDefault(d)))
                {
                    string dishName = dishNames.GetValueOrDefault(dishId) ?? dishId.ToString();
                    var (recipe, locked, snapshot) = await ProductionRules.SelectRecipeAsync(db, dishId, date);
                    if (recipe == null || snapshot.Count == 0)
                    {
                        missing.Add(dishName);
                        continue;
                    }
                    if (!locked)
                    {
                        warnings.Add(string.Format(ConstantHelper.Message.ReleaseRecipeNotLocked, dishName));
                    }
                    recipes[dishId] = (recipe, locked && ProductionRules.ParseSnapshot(recipe.SnapshotJson).Count != 0
                        ? recipe.SnapshotJson! : Newtonsoft.Json.JsonConvert.SerializeObject(snapshot));
                }
                if (missing.Count != 0)
                {
                    return Fail(string.Format(ConstantHelper.Message.ReleaseNoRecipe, string.Join(", ", missing)));
                }

                // 2) Mỗi ca: giờ xuất bếp riêng → lệnh SX (PH-) theo món + mẻ (ME-yyMMdd-<CA>-NN) lịch ngược riêng.
                // Sinh mã đầu rồi cộng dần (sp_getapplock giữ tới khi commit); PH- cộng dần qua mọi ca, ME- theo từng ca.
                string planNo = plan.PlanNumber ?? plan.PlanNo ?? string.Empty;
                string firstOrderNo = await _documentNumberService.NextAsync(DocumentKind.ProductionOrder, _clock.Today);
                int orderIndex = 0;
                var newOrders = new List<ProductionOrder>();
                foreach (var part in toRelease)
                {
                    var shift = part.Shift;
                    var (dispatchAt, source) = await ProductionRules.ResolveDispatchAsync(db, date, shift, dispatchTime, _rules, fallback?.Id);
                    if (dispatchAt == null)
                    {
                        return Fail(ConstantHelper.Message.DispatchTimeMissing + $" (ca {shift.Code})");
                    }
                    DateTime cookEnd = dispatchAt.Value.AddMinutes(-_rules.ProductionPackingMinutes);
                    string firstBatchNo = await _documentNumberService.NextAsync(DocumentKind.ProductionBatch, date, shift.Code);
                    string orderNote = $"Kế hoạch {planNo} – ca {shift.Code} – xuất bếp {dispatchAt.Value.ToString("HH:mm dd/MM/yyyy", CultureInfo.InvariantCulture)} ({source})"
                        + (string.IsNullOrWhiteSpace(request.Note) ? string.Empty : " – " + request.Note.Trim());
                    var allBatches = new List<(ProductionOrder Order, string DishName, ProductionRules.ScheduledBatch Batch)>();
                    foreach (var (planLine, quantity) in part.Lines.OrderBy(l => dishNames.GetValueOrDefault(l.Line.DishId)))
                    {
                        var (recipe, snapshot) = recipes[planLine.DishId];
                        var batches = ProductionRules.Schedule(quantity, line?.CapacityMealPerBatch, minutes, cookEnd);
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
                            PlannedQty = quantity,
                            Status = ProductionOrderStatus.Released,
                            PlannedStartDate = batches.Min(b => b.Start),
                            StartTime = batches.Min(b => b.Start),
                            EndTime = batches.Max(b => b.End),
                            RecipeSnapshotJson = snapshot,
                            Note = orderNote,
                        };
                        _unitOfWork.ProductionOrderRepository.Add(order);
                        newOrders.Add(order);
                        allBatches.AddRange(batches.Select(b => (order, dishNames.GetValueOrDefault(planLine.DishId) ?? string.Empty, b)));
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
                    released.Add(shift.Id);
                }

                // 3) Dòng kế hoạch: Đã phát hành khi mọi ca của món đã phát hành; gắn ca khi món chỉ thuộc 1 ca.
                foreach (var planLine in planLines)
                {
                    var lineShifts = parts.Where(p => p.Lines.Any(l => l.Line.Id == planLine.Id)).Select(p => p.Shift.Id).ToList();
                    if (lineShifts.Count != 0 && lineShifts.All(released.Contains))
                    {
                        planLine.ShiftId = lineShifts.Count == 1 ? lineShifts[0] : null;
                        planLine.Status = ProductionPlanStatus.Released;
                        _unitOfWork.ProductionPlanLineRepository.Update(planLine);
                    }
                }

                // 4) Kế hoạch → Đã phát hành khi mọi ca có nhu cầu đã phát hành (còn ca → giữ Đã duyệt);
                // đơn Đã chốt của ngày có dòng thuộc ca vừa phát hành (dòng chưa gán ca = ca mặc định) → Đang sản xuất.
                if (parts.All(p => released.Contains(p.Shift.Id)))
                {
                    plan.Status = ProductionPlanStatus.Released;
                    _unitOfWork.ProductionPlanRepository.Update(plan);
                }
                var releasedNow = toRelease.Select(p => p.Shift.Id).ToHashSet();
                DateTime next = date.AddDays(1);
                var lockedOrders = _unitOfWork.OrderRepository.GetAll().Where(o => !o.IsDeleted && o.MealDate >= date && o.MealDate < next && o.Status == OrderStatus.Locked).ToList();
                var lockedIds = lockedOrders.Select(o => o.Id).ToList();
                var inShift = db.OrderLine.AsNoTracking().Where(l => lockedIds.Contains(l.OrderId) && !l.IsDeleted && l.Quantity > 0)
                    .Select(l => new { l.OrderId, l.ShiftId }).ToList()
                    .Where(l => (l.ShiftId ?? fallback?.Id) is { } s && releasedNow.Contains(s))
                    .Select(l => l.OrderId).ToHashSet();
                foreach (var customerOrder in lockedOrders.Where(o => inShift.Contains(o.Id)))
                {
                    customerOrder.Status = OrderStatus.InProduction;
                    _unitOfWork.OrderRepository.Update(customerOrder);
                }
                // 5) BE2.4 (spec 8.1 bước 4, Catalog 126): giữ chỗ nguyên liệu cho các lệnh SX vừa phát hành – thiếu vẫn phát hành, trả cảnh báo (D-53 a).
                HashSet<Guid> reservedIngredients = [];
                if (_rules.ProductionReserveOnRelease)
                {
                    var (touched, reserveWarnings) = await ReservationRules.ReserveAsync(db, _uomConversionService, newOrders, date, _clock.Today, _rules,
                        r => _unitOfWork.MaterialReservationRepository.Add(r));
                    reservedIngredients = touched;
                    warnings.AddRange(reserveWarnings);
                }
                await _unitOfWork.ProductionPlanRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                await ReservationRules.ReallocateAsync(db, reservedIngredients, _clock.Today);
                _unitOfWork.CommitTransaction();
                // BIZ-2.5: phiếu xuất Nháp theo ca (sau khi phát hành đã ghi – lỗi chỉ cảnh báo).
                var shiftCodes = toRelease.ToDictionary(p => p.Shift.Id, p => p.Shift.Code);
                warnings.AddRange(await CreateShiftIssueDraftsAsync(planNo, [.. newOrders.Select(o => (o.Id, o.ShiftId ?? Guid.Empty, shiftCodes.GetValueOrDefault(o.ShiftId ?? Guid.Empty), o.ProductionOrderNumber ?? o.ProductionOrderNo))]));
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

        /// <summary>T17.1: ca đã phát hành của kế hoạch = ca của các lệnh SX (chưa hủy) thuộc kế hoạch.</summary>
        private async Task<HashSet<Guid>> ReleasedShiftIdsAsync(Guid planId)
            => (await _unitOfWork.DbContext.ProductionOrder.AsNoTracking()
                    .Where(o => o.ProductionPlanId == planId && !o.IsDeleted && o.ShiftId != null && o.Status != ProductionOrderStatus.Cancelled)
                    .Select(o => o.ShiftId!.Value).Distinct().ToListAsync()).ToHashSet();

        /// <summary>
        /// BE2.1 (spec 8.1 generate, Catalog 137) – sinh / làm lại kế hoạch Nháp của ngày trong 1 transaction. Theo GAP-MRP-01 / GAP-PRD-01 (anh chốt): <b>1 KH- / ngày</b>
        /// (không tách kế hoạch theo ca như spec), chia theo ca ở dòng kế hoạch (ShiftId khi món thuộc đúng 1 ca) và ở <c>ListShift</c> của chi tiết; phát hành theo từng ca
        /// bằng Release như T17.1. PROPOSED – BA/PO CONFIRM (D-49): spec "mỗi ca một kế hoạch" mâu thuẫn quyết định KH- theo ngày → giữ quyết định của anh.
        /// <list type="number">
        /// <item>Nguồn: dòng đơn của đơn <b>Đã xác nhận / Đã chốt</b> có ngày ăn = ngày × thực đơn (MenuLine cùng loại suất, thứ của ngày), chia đều lựa chọn + dự phòng
        /// (như MRP – <see cref="ProductionRules.DishDemandsAsync"/>) → cộng dồn theo món; OrderLineId = dòng đơn đầu tiên.</item>
        /// <item>Kế hoạch neo (ngày, chi nhánh, không ca): chưa có → tạo Nháp KH-; Nháp chưa tính nhu cầu NL → cập nhật dòng tại chỗ (dòng món không còn → xóa mềm);
        /// đã Duyệt trở đi → chặn; đã tính nhu cầu NL (MaterialRequirement) → chặn (tính lại bằng MaterialRequirement/Calculate).</item>
        /// <item>BE2.2: áp yêu cầu đổi món Đã duyệt (MenuChangeRequest) qua <see cref="ProductionRules.DishDemandsAsync"/>.</item>
        /// </list>
        /// </summary>
        public async Task<IResult<ProductionPlanDetailModel>> Generate(ProductionPlanGenerateRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var actor = _currentActor.RequireActor();
                var db = _unitOfWork.DbContext;
                DateTime date = request.ProductionDate.Date, next = date.AddDays(1);
                Guid? unitId = request.OrganizationUnitId is { } ou && ou != Guid.Empty ? ou : null;
                if (unitId != null && !await db.OrganizationUnit.AsNoTracking().AnyAsync(o => o.Id == unitId && !o.IsDeleted))
                {
                    return Fail(ConstantHelper.Message.OrganizationUnitNotFound);
                }
                // Khóa theo ngày (sp_getapplock của mã KH-) như MaterialRequirement/Calculate – 2 lần sinh / tính cùng ngày chạy lần lượt.
                string planNumber = await _documentNumberService.NextAsync(DocumentKind.ProductionPlan, date);
                var plan = _unitOfWork.ProductionPlanRepository.GetAll()
                    .Where(p => !p.IsDeleted && p.ProductionDate >= date && p.ProductionDate < next && p.OrganizationUnitId == unitId && p.ShiftId == null)
                    .OrderBy(p => p.CreatedDate).FirstOrDefault();
                if (plan != null && plan.Status != ProductionPlanStatus.Draft)
                {
                    return Fail(string.Format(ConstantHelper.Message.PlanGenerateNotDraft, plan.PlanNumber ?? plan.PlanNo, ProductionPlanStatus.Label(plan.Status)));
                }
                if (plan != null && await db.MaterialRequirement.AsNoTracking().AnyAsync(r => r.ProductionPlanId == plan.Id && !r.IsDeleted))
                {
                    return Fail(string.Format(ConstantHelper.Message.PlanGenerateHasMrp, plan.PlanNumber ?? plan.PlanNo));
                }
                // BIZ-1.3 / A9 (D-77 c): kế hoạch SX chính thức chỉ lấy đơn Đã chốt – cùng nguồn với tính nhu cầu chính thức (số dự kiến ở MaterialRequirement/GetHorizon).
                var (demands, orders, skipped, _) = await ProductionRules.DishDemandsAsync(db, date, [OrderStatus.Locked], _rules);
                if (demands.Count == 0)
                {
                    return Fail(string.Format(ConstantHelper.Message.PlanGenerateNoOrders, ProductionRules.Day(date)));
                }
                var warnings = new List<string>();
                if (skipped.Count != 0)
                {
                    warnings.Add($"{skipped.Count} dòng đơn không có thực đơn / thực đơn không có món cho ngày này – không đưa vào kế hoạch");
                }
                int pending = await db.Order.AsNoTracking().CountAsync(o => !o.IsDeleted && o.MealDate >= date && o.MealDate < next
                    && (o.Status == OrderStatus.Draft || o.Status == OrderStatus.Submitted || o.Status == OrderStatus.Confirmed));
                if (pending > 0)
                {
                    warnings.Add($"{pending} đơn Nháp / Đã gửi / Đã xác nhận của ngày chưa được tính (chỉ đơn Đã chốt)");
                }
                var byDish = demands.GroupBy(d => d.DishId)
                    .Select(g => (DishId: g.Key, Quantity: ProductionRules.Round4(g.Sum(x => x.Quantity)), FirstLine: g.First().OrderLineId, Shifts: g.Select(x => x.ShiftId).Distinct().ToList()))
                    .ToList();

                var oldLines = plan == null ? [] : _unitOfWork.ProductionPlanLineRepository.GetAll().Where(l => l.ProductionPlanId == plan.Id && !l.IsDeleted).ToList();
                var toDelete = oldLines.Where(l => byDish.All(d => d.DishId != l.DishId)).ToList();
                if (toDelete.Count != 0)
                {
                    foreach (var line in toDelete)
                    {
                        _unitOfWork.ProductionPlanLineRepository.Update(line);
                    }
                    await _unitOfWork.ProductionPlanLineRepository.CommitAsync(ConstantHelper.TypeSave.Delete);
                }
                DateTime now = _clock.Now;
                if (plan == null)
                {
                    plan = new ProductionPlan
                    {
                        Id = Guid.NewGuid(),
                        PlanNumber = planNumber,
                        PlanNo = planNumber,
                        ProductionDate = date,
                        PlanDate = now,
                        OrganizationUnitId = unitId,
                        Status = ProductionPlanStatus.Draft,
                    };
                    _unitOfWork.ProductionPlanRepository.Add(plan);
                }
                else
                {
                    _unitOfWork.ProductionPlanRepository.Update(plan);
                }
                foreach (var dish in byDish)
                {
                    var line = oldLines.FirstOrDefault(l => l.DishId == dish.DishId && !toDelete.Contains(l));
                    if (line == null)
                    {
                        line = new ProductionPlanLine { Id = Guid.NewGuid(), ProductionPlanId = plan.Id, DishId = dish.DishId };
                        _unitOfWork.ProductionPlanLineRepository.Add(line);
                    }
                    else
                    {
                        _unitOfWork.ProductionPlanLineRepository.Update(line);
                    }
                    line.PlannedQuantity = dish.Quantity;
                    line.PlannedQty = dish.Quantity;
                    line.OrderLineId = dish.FirstLine;
                    line.ShiftId = dish.Shifts.Count == 1 ? dish.Shifts[0] : null;
                    line.Status = ProductionPlanStatus.Draft;
                }
                var orderIds = demands.Select(d => d.OrderId).Distinct().ToList();
                plan.Note = Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    Source = "GENERATE",
                    GeneratedAt = now,
                    GeneratedBy = actor.UserId,
                    OrderCount = orderIds.Count,
                    TotalMeals = orders.Where(o => orderIds.Contains(o.Id)).Sum(o => o.TotalQuantity ?? 0m),
                    UserNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
                    Warnings = warnings,
                });
                await _unitOfWork.ProductionPlanRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                var detail = (await DetailAsync(plan.Id))!;
                detail.ListWarning.InsertRange(0, warnings);
                return Result<ProductionPlanDetailModel>.Success(detail, ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Generate: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }

            IResult<ProductionPlanDetailModel> Fail(string message)
            {
                _unitOfWork.RollbackTransaction();
                return Result<ProductionPlanDetailModel>.Fail(message);
            }
        }

        /// <summary>T17: chi tiết kế hoạch – dòng món, lệnh SX, mẻ (lịch + ATTP), thao tác được phép. T17.1: + phần theo ca.</summary>
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

            // T17.1: phần theo ca – ca đã phát hành lấy theo lệnh SX, ca chưa phát hành theo phép chia hiện tại (đơn Đã chốt / Đang SX).
            var (parts, _, splitWarnings, _) = await ProductionRules.SplitByShiftAsync(db, plan.ProductionDate.Date, lines, _rules);
            var liveOrders = orders.Where(o => o.Status != ProductionOrderStatus.Cancelled && o.ShiftId != null).ToList();
            var releasedIds = liveOrders.Select(o => o.ShiftId!.Value).ToHashSet();
            var shiftIds = parts.Select(p => p.Shift.Id).Concat(releasedIds).Distinct().ToList();
            var shiftMap = await db.Shift.AsNoTracking().Where(s => shiftIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id);
            var listShift = shiftIds.Where(shiftMap.ContainsKey).Select(id =>
            {
                var shift = shiftMap[id];
                var mine = liveOrders.Where(o => o.ShiftId == id).ToList();
                var mineIds = mine.Select(o => o.Id).ToHashSet();
                var mineBatches = batches.Where(b => mineIds.Contains(b.ProductionOrderId)).ToList();
                bool isReleased = releasedIds.Contains(id);
                return new ProductionPlanShiftModel
                {
                    ShiftId = id,
                    ShiftCode = shift.Code,
                    ShiftName = shift.Name,
                    StartTime = shift.StartTime,
                    Portions = isReleased ? mine.Sum(o => o.PlannedQty) : parts.FirstOrDefault(p => p.Shift.Id == id)?.Portions ?? 0m,
                    Released = isReleased,
                    DispatchAt = mineBatches.Count == 0 ? null : mineBatches.Max(b => b.EndDate ?? b.StartDate).AddMinutes(_rules.ProductionPackingMinutes),
                    OrderCount = mine.Count,
                    BatchCount = mineBatches.Count,
                };
            }).OrderBy(s => s.StartTime ?? TimeSpan.MaxValue).ThenBy(s => s.ShiftCode).ToList();
            bool hasPending = listShift.Any(s => !s.Released);
            var lineShifts = parts.SelectMany(p => p.Lines.Select(l => (l.Line.Id, p.Shift.Id, p.Shift.Code, l.Quantity))).ToList();

            var detail = new ProductionPlanDetailModel
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
                    ListShift = [.. lineShifts.Where(x => x.Item1 == l.Id).Select(x => new ProductionPlanLineShiftModel { ShiftId = x.Item2, ShiftCode = x.Code, Quantity = x.Quantity })],
                }).OrderBy(l => l.DishName)],
                ListOrder = await ProductionRules.OrderBriefsAsync(db, orders),
                ListBatch = await ProductionRules.BatchBriefsAsync(db, batches, _rules),
                DispatchAt = batches.Count == 0 ? null : batches.Max(b => b.EndDate ?? b.StartDate).AddMinutes(_rules.ProductionPackingMinutes),
                ListShift = listShift,
                ListReservation = orderIds.Count == 0 ? [] : await ReservationRules.ModelsAsync(db, orderIds),
                AllowedActions = plan.Status switch
                {
                    ProductionPlanStatus.Draft => ["approve"],
                    // T17.1: phát hành từng ca – còn ca chưa phát hành thì giữ "release"; đã có lệnh SX thì in phiếu được.
                    ProductionPlanStatus.Approved => [.. (hasPending ? new[] { "release" } : []), .. (orders.Count != 0 ? new[] { "exportProductionSheet" } : [])],
                    ProductionPlanStatus.Released or ProductionPlanStatus.Completed => ["exportProductionSheet"],
                    _ => [],
                },
            };
            if (plan.Status is ProductionPlanStatus.Draft or ProductionPlanStatus.Approved)
            {
                detail.ListWarning.AddRange(splitWarnings);
            }
            return detail;
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
