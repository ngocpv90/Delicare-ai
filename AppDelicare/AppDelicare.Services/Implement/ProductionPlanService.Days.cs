using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Request.Custom;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// UI-6.23 (D-104): màn "7 ngày tới" của Kế hoạch sản xuất – một kế hoạch / ngày (khóa = ngày, không theo đơn vị).
    /// <list type="bullet">
    /// <item><see cref="GetDays"/>: mỗi ngày – suất đã chốt / chưa chốt, kế hoạch duy nhất của ngày, suất theo ca + ca đã ra lệnh, việc gợi ý.</item>
    /// <item><see cref="Recalculate"/>: Lập / Tính lại – máy chủ tự quyết: mọi đơn của ngày đã chốt + bật tự ra lệnh → luồng tự động sau chốt (BIZ-2.4); còn đơn chưa chốt → chỉ lập Nháp.</item>
    /// <item><see cref="GetShortage"/>: nguyên liệu thiếu nếu ra lệnh lúc này (chạy thử giữ chỗ trong bộ nhớ – KHÔNG ghi) + đơn mua đang mở.</item>
    /// </list>
    /// </summary>
    public partial class ProductionPlanService
    {
        public const string DayReleased = "released";
        public const string DayApproved = "approved";
        public const string DayDraft = "draft";
        public const string DayHeld = "held";
        public const string DayNone = "none";
        public const string DayOpen = "open";
        public const string DayEmpty = "empty";
        public const string HeldMarker = "chờ COO duyệt";

        private static readonly string[] LockedStatuses = [OrderStatus.Locked, OrderStatus.InProduction, OrderStatus.Delivered, OrderStatus.Reconciled];
        private static readonly string[] OpenStatuses = [OrderStatus.Draft, OrderStatus.Submitted, OrderStatus.Confirmed];
        private static readonly string[] OpenPoStatuses = [PurchaseOrderStatus.Draft, PurchaseOrderStatus.PendingApproval, PurchaseOrderStatus.Approved, PurchaseOrderStatus.Sent, PurchaseOrderStatus.PartiallyReceived];

        public static string DayLabel(string status) => status switch
        {
            DayReleased => "Đã ra lệnh",
            DayApproved => "Đã duyệt",
            DayDraft => "Nháp",
            DayHeld => "Bị giữ lại",
            DayNone => "Chưa có kế hoạch",
            DayOpen => "Chưa chốt đơn",
            _ => "Không có đơn",
        };

        private string LockAt()
        {
            string? at = _services.GetService<IConfiguration>()?["Jobs:LockOrders:At"];
            return string.IsNullOrWhiteSpace(at) ? "20:00" : at.Trim();
        }

        private static JObject? NoteJson(string? note)
        {
            if (string.IsNullOrWhiteSpace(note) || !note.TrimStart().StartsWith('{'))
            {
                return null;
            }
            try
            {
                return JObject.Parse(note);
            }
            catch (Newtonsoft.Json.JsonException)
            {
                return null;
            }
        }

        /// <summary>Lý do giữ lại của hệ thống (Note.AutoRun "… chờ COO duyệt: …") – chỉ khi kế hoạch còn Nháp.</summary>
        public static string? HeldReasonOf(ProductionPlan plan)
        {
            if (plan.Status != ProductionPlanStatus.Draft)
            {
                return null;
            }
            string? auto = (string?)NoteJson(plan.Note)?["AutoRun"];
            if (auto == null || !auto.Contains(HeldMarker, StringComparison.Ordinal))
            {
                return null;
            }
            int i = auto.IndexOf(HeldMarker, StringComparison.Ordinal) + HeldMarker.Length;
            string reason = auto[i..].TrimStart(':', ' ');
            return reason.Length == 0 ? auto : reason;
        }

        public async Task<IResult<ProductionPlanDaysModel>> GetDays(ProductionPlanDaysRequest request)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                DateTime today = _clock.Today.Date;
                DateTime from = (request.From ?? today).Date;
                DateTime to = (request.To ?? from.AddDays(6)).Date;
                if (to < from)
                {
                    return Result<ProductionPlanDaysModel>.Fail(ConstantHelper.Message.PlanDaysRangeInvalid);
                }
                if ((to - from).TotalDays > 13)
                {
                    return Result<ProductionPlanDaysModel>.Fail(ConstantHelper.Message.PlanDaysRangeTooLong);
                }
                DateTime end = to.AddDays(1);
                var model = new ProductionPlanDaysModel { From = from, To = to, Today = today, LockAt = LockAt(), AutoRelease = _rules.ProductionAutoReleaseAfterLock };

                var orders = await db.Order.AsNoTracking()
                    .Where(o => !o.IsDeleted && o.MealDate >= from && o.MealDate < end && o.Status != OrderStatus.Cancelled)
                    .Select(o => new { o.Id, o.MealDate, o.Status, o.TotalQuantity }).ToListAsync();
                var lockedIds = orders.Where(o => LockedStatuses.Contains(o.Status)).Select(o => o.Id).ToList();
                var lockedLines = await db.OrderLine.AsNoTracking().Where(l => lockedIds.Contains(l.OrderId) && !l.IsDeleted && l.Quantity > 0)
                    .Select(l => new { l.OrderId, l.ShiftId, l.Quantity }).ToListAsync();
                var fallback = await db.Shift.AsNoTracking().FirstOrDefaultAsync(s => s.Code == _rules.ProductionDefaultShiftCode && !s.IsDeleted);
                var orderDay = orders.ToDictionary(o => o.Id, o => o.MealDate.Date);

                // Một kế hoạch / ngày: bản không đơn vị, không ca, lập sớm nhất (bản trùng cũ – script UI623_plan_dedupe.sql).
                var plans = (await db.ProductionPlan.AsNoTracking()
                        .Where(p => !p.IsDeleted && p.ProductionDate >= from && p.ProductionDate < end && p.ShiftId == null)
                        .ToListAsync())
                    .GroupBy(p => p.ProductionDate.Date)
                    .ToDictionary(g => g.Key, g => g.OrderBy(p => p.OrganizationUnitId == null ? 0 : 1).ThenBy(p => p.CreatedDate).First());
                var planIds = plans.Values.Select(p => p.Id).ToList();
                var planLines = await db.ProductionPlanLine.AsNoTracking().Where(l => planIds.Contains(l.ProductionPlanId) && !l.IsDeleted && l.PlannedQuantity > 0)
                    .Select(l => new { l.ProductionPlanId, l.DishId, l.PlannedQuantity }).ToListAsync();
                var prodOrders = await db.ProductionOrder.AsNoTracking()
                    .Where(o => planIds.Contains(o.ProductionPlanId) && !o.IsDeleted && o.Status != ProductionOrderStatus.Cancelled)
                    .Select(o => new { PlanId = o.ProductionPlanId, o.ShiftId }).ToListAsync();

                var shiftIds = lockedLines.Select(l => l.ShiftId ?? fallback?.Id).Where(s => s != null).Select(s => s!.Value)
                    .Concat(prodOrders.Where(o => o.ShiftId != null).Select(o => o.ShiftId!.Value)).Distinct().ToList();
                var shifts = await db.Shift.AsNoTracking().Where(s => shiftIds.Contains(s.Id)).ToListAsync();
                model.ListShift = [.. shifts.OrderBy(s => s.StartTime ?? TimeSpan.MaxValue).ThenBy(s => s.Code)
                    .Select(s => new ProductionPlanDayShiftModel { ShiftId = s.Id, ShiftCode = s.Code, ShiftName = s.Name ?? s.Code, StartTime = s.StartTime })];

                for (DateTime day = from; day < end; day = day.AddDays(1))
                {
                    var dayOrders = orders.Where(o => o.MealDate.Date == day).ToList();
                    var x = new ProductionPlanDayModel
                    {
                        Date = day,
                        LockedMeals = dayOrders.Where(o => LockedStatuses.Contains(o.Status)).Sum(o => o.TotalQuantity ?? 0m),
                        OpenMeals = dayOrders.Where(o => OpenStatuses.Contains(o.Status)).Sum(o => o.TotalQuantity ?? 0m),
                        LockedOrderCount = dayOrders.Count(o => LockedStatuses.Contains(o.Status)),
                        OpenOrderCount = dayOrders.Count(o => OpenStatuses.Contains(o.Status)),
                    };
                    var mealsByShift = lockedLines.Where(l => orderDay.GetValueOrDefault(l.OrderId) == day && (l.ShiftId ?? fallback?.Id) != null)
                        .GroupBy(l => (l.ShiftId ?? fallback?.Id)!.Value).ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));
                    var plan = plans.GetValueOrDefault(day);
                    var released = plan == null ? [] : prodOrders.Where(o => o.PlanId == plan.Id && o.ShiftId != null).GroupBy(o => o.ShiftId!.Value).ToDictionary(g => g.Key, g => g.Count());
                    string planState = DayNone;
                    if (plan != null)
                    {
                        x.PlanId = plan.Id;
                        x.PlanNumber = plan.PlanNumber ?? plan.PlanNo;
                        x.PlanStatus = plan.Status;
                        var mine = planLines.Where(l => l.ProductionPlanId == plan.Id).ToList();
                        x.DishCount = mine.Select(l => l.DishId).Distinct().Count();
                        x.PlanPortions = mine.Sum(l => l.PlannedQuantity);
                        x.ProductionOrderCount = prodOrders.Count(o => o.PlanId == plan.Id);
                        var json = NoteJson(plan.Note);
                        x.AutoRun = (string?)json?["AutoRun"];
                        x.HeldReason = HeldReasonOf(plan);
                        if (plan.Status == ProductionPlanStatus.Draft && json?["TotalMeals"] is { } tm && tm.Type is JTokenType.Integer or JTokenType.Float)
                        {
                            x.LateMeals = Math.Max(0m, x.LockedMeals - (decimal)tm);
                        }
                        planState = plan.Status switch
                        {
                            ProductionPlanStatus.Released or ProductionPlanStatus.Completed => DayReleased,
                            ProductionPlanStatus.Approved => DayApproved,
                            ProductionPlanStatus.Draft => x.HeldReason != null ? DayHeld : DayDraft,
                            _ => DayNone,
                        };
                    }
                    foreach (var s in model.ListShift)
                    {
                        decimal meals = mealsByShift.GetValueOrDefault(s.ShiftId);
                        int n = released.GetValueOrDefault(s.ShiftId);
                        if (meals <= 0 && n == 0)
                        {
                            continue;
                        }
                        x.ListShift.Add(new ProductionPlanDayShiftModel
                        {
                            ShiftId = s.ShiftId, ShiftCode = s.ShiftCode, ShiftName = s.ShiftName, StartTime = s.StartTime, Meals = meals, ProductionOrderCount = n,
                            Status = n > 0 || planState == DayReleased ? DayReleased : planState,
                        });
                    }
                    x.ShiftTotal = x.ListShift.Count;
                    x.ShiftReleased = x.ListShift.Count(s => s.Status == DayReleased);
                    if (plan == null)
                    {
                        x.DayStatus = x.LockedOrderCount > 0 ? DayNone : x.OpenOrderCount > 0 ? DayOpen : DayEmpty;
                    }
                    else
                    {
                        // Đã duyệt mà mọi ca đã có lệnh → coi như đã ra lệnh (kế hoạch chuyển Đã phát hành ở lần ra lệnh ca cuối). Không còn suất đã chốt theo ca
                        // (đơn bị mở lại / hủy sau khi duyệt) mà đã có lệnh → đã ra lệnh; chưa có lệnh → vẫn "Ra lệnh" (máy chủ chia ca khi phát hành).
                        bool allOut = x.ShiftTotal > 0 ? x.ShiftReleased == x.ShiftTotal : x.ProductionOrderCount > 0;
                        x.DayStatus = planState == DayApproved && allOut ? DayReleased : planState;
                    }
                    x.DayStatusName = DayLabel(x.DayStatus);
                    (x.Action, x.AllowedActions) = x.DayStatus switch
                    {
                        DayHeld => ("viewReason", new List<string> { "viewReason", "approve", "recalc" }),
                        DayDraft => ("recalc", ["recalc", "approve"]),
                        DayApproved => ("release", ["release"]),
                        DayReleased => ("view", []),
                        DayNone => ("recalc", ["recalc"]),
                        _ => ("none", []),
                    };
                    model.ListDay.Add(x);
                }
                return Result<ProductionPlanDaysModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetDays: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<ProductionPlanRecalculateModel>> Recalculate(ProductionPlanRecalculateRequest request)
        {
            try
            {
                _currentActor.RequireActor();
                var db = _unitOfWork.DbContext;
                DateTime date = request.ProductionDate.Date, next = date.AddDays(1);
                var dayOrders = await db.Order.AsNoTracking().Where(o => !o.IsDeleted && o.MealDate >= date && o.MealDate < next && o.Status != OrderStatus.Cancelled)
                    .Select(o => new { o.Status, o.TotalQuantity }).ToListAsync();
                decimal openMeals = dayOrders.Where(o => OpenStatuses.Contains(o.Status)).Sum(o => o.TotalQuantity ?? 0m);
                bool anyOpen = dayOrders.Any(o => OpenStatuses.Contains(o.Status));
                if (!dayOrders.Any(o => o.Status == OrderStatus.Locked || o.Status == OrderStatus.InProduction))
                {
                    return Result<ProductionPlanRecalculateModel>.Fail(string.Format(ConstantHelper.Message.PlanGenerateNoOrders, ProductionRules.Day(date)));
                }
                var result = new ProductionPlanRecalculateModel { ProductionDate = date };
                if (!anyOpen && _rules.ProductionAutoReleaseAfterLock)
                {
                    var auto = await AutoPlanAfterLockAsync(date);
                    (result.Status, result.PlanId, result.PlanNumber, result.ProductionOrderCount) = (auto.Status, auto.PlanId, auto.PlanNumber, auto.ProductionOrderCount);
                    result.ListReason.AddRange(auto.ListReason);
                    result.ListWarning.AddRange(auto.ListWarning);
                    result.Message = auto.Status switch
                    {
                        AutoReleased => string.Format(ConstantHelper.Message.PlanRecalcReleased, auto.PlanNumber, auto.ProductionOrderCount),
                        AutoHeld => string.Format(ConstantHelper.Message.PlanRecalcHeld, auto.PlanNumber, string.Join("; ", auto.ListReason)),
                        _ => string.Join("; ", auto.ListReason),
                    };
                    if (auto.Status == AutoNone || (auto.Status == AutoSkipped && auto.PlanId == null))
                    {
                        return Result<ProductionPlanRecalculateModel>.Fail(result.Message.Length == 0 ? ConstantHelper.Message.AutoPlanError : result.Message);
                    }
                }
                else
                {
                    var generated = await Generate(new ProductionPlanGenerateRequest { ProductionDate = date, Note = "Lập từ màn 7 ngày tới" });
                    if (generated.ResponseCode != 200 || generated.Data?.ProductionPlan is not { } plan)
                    {
                        return Result<ProductionPlanRecalculateModel>.Fail(generated.ResponseMessage ?? ConstantHelper.Message.AutoPlanError);
                    }
                    (result.Status, result.PlanId, result.PlanNumber) = ("DRAFT", plan.Id, plan.PlanNumber ?? plan.PlanNo);
                    result.ListWarning.AddRange(generated.Data.ListWarning);
                    result.Message = anyOpen
                        ? string.Format(ConstantHelper.Message.PlanRecalcDraftOpen, result.PlanNumber, ProductionRules.Num(openMeals), LockAt())
                        : string.Format(ConstantHelper.Message.PlanRecalcDraft, result.PlanNumber);
                }
                if (result.PlanId is { } id)
                {
                    var lines = await db.ProductionPlanLine.AsNoTracking().Where(l => l.ProductionPlanId == id && !l.IsDeleted && l.PlannedQuantity > 0).Select(l => new { l.DishId, l.PlannedQuantity }).ToListAsync();
                    result.DishCount = lines.Select(l => l.DishId).Distinct().Count();
                    result.Portions = lines.Sum(l => l.PlannedQuantity);
                }
                return Result<ProductionPlanRecalculateModel>.Success(result, result.Message);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Recalculate: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<ProductionPlanShortageModel>> GetShortage(Guid id)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                var plan = await db.ProductionPlan.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
                if (plan == null)
                {
                    return Result<ProductionPlanShortageModel>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
                }
                DateTime date = plan.ProductionDate.Date;
                var model = new ProductionPlanShortageModel { PlanId = plan.Id, PlanNumber = plan.PlanNumber ?? plan.PlanNo, ProductionDate = date, HeldReason = HeldReasonOf(plan) };
                var lines = await db.ProductionPlanLine.AsNoTracking().Where(l => l.ProductionPlanId == id && !l.IsDeleted && l.PlannedQuantity > 0).ToListAsync();
                var existingOrders = await db.ProductionOrder.AsNoTracking().Where(o => o.ProductionPlanId == id && !o.IsDeleted && o.Status != ProductionOrderStatus.Cancelled).ToListAsync();
                var releasedShifts = existingOrders.Where(o => o.ShiftId != null).Select(o => o.ShiftId!.Value).ToHashSet();
                var (parts, _, splitWarnings, splitError) = await ProductionRules.SplitByShiftAsync(db, date, lines, _rules);
                if (splitError != null)
                {
                    model.ListWarning.Add(splitError);
                }
                model.ListWarning.AddRange(splitWarnings);

                // Ca chưa ra lệnh: lệnh SX giả trong bộ nhớ (không Add vào DbContext) → chạy đúng hàm giữ chỗ khi ra lệnh; dòng giữ chỗ chỉ gom vào danh sách tạm.
                var pseudo = new List<ProductionOrder>();
                var noRecipe = new List<string>();
                var lineDishIds = lines.Select(l => l.DishId).Distinct().ToList();
                var dishNames = await db.Dish.AsNoTracking().Where(d => lineDishIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name ?? d.Code ?? string.Empty);
                foreach (var part in parts.Where(p => !releasedShifts.Contains(p.Shift.Id)))
                {
                    foreach (var (line, qty) in part.Lines)
                    {
                        var (recipe, _, snapshot) = await ProductionRules.SelectRecipeAsync(db, line.DishId, date);
                        if (recipe == null || snapshot.Count == 0)
                        {
                            noRecipe.Add(dishNames.GetValueOrDefault(line.DishId) ?? line.DishId.ToString());
                            continue;
                        }
                        pseudo.Add(new ProductionOrder
                        {
                            Id = Guid.NewGuid(), ProductionOrderNumber = $"{part.Shift.Code} · {dishNames.GetValueOrDefault(line.DishId)}", DishId = line.DishId, RecipeId = recipe.Id,
                            ShiftId = part.Shift.Id, PlannedQty = qty, RecipeSnapshotJson = Newtonsoft.Json.JsonConvert.SerializeObject(snapshot),
                        });
                    }
                }
                if (noRecipe.Count != 0)
                {
                    model.ListWarning.Add(string.Format(ConstantHelper.Message.ReleaseNoRecipe, string.Join(", ", noRecipe.Distinct())));
                }
                var trial = new List<MaterialReservation>();
                if (pseudo.Count != 0)
                {
                    var (_, warnings) = await ReservationRules.ReserveAsync(db, _uomConversionService, pseudo, date, _clock.Today, _rules, trial.Add);
                    model.ListWarning.AddRange(warnings.Where(w => !w.StartsWith(Prefix(ConstantHelper.Message.ReservationShort), StringComparison.Ordinal)));
                    // Chỉ đọc: bỏ mọi thay đổi bộ nhớ (ReserveAsync có thể chạm dòng giữ chỗ đang theo dõi).
                    db.ChangeTracker.Clear();
                }
                var rows = trial.GroupBy(r => (r.IngredientId, r.UnitId))
                    .Select(g => (g.Key.IngredientId, g.Key.UnitId, Required: g.Sum(r => r.RequiredQty), Reserved: g.Sum(r => r.ReservedQuantity))).ToList();
                // Ca đã ra lệnh: phần giữ chỗ còn thiếu của lệnh hiện có.
                if (existingOrders.Count != 0)
                {
                    foreach (var r in (await ReservationRules.ModelsAsync(db, existingOrders.Select(o => o.Id).ToList())).Where(r => r.ShortQuantity > 0))
                    {
                        rows.Add((r.IngredientId, r.UnitId, r.RequiredQuantity, r.ReservedQuantity));
                    }
                }
                var shortRows = rows.GroupBy(r => (r.IngredientId, r.UnitId))
                    .Select(g => (g.Key.IngredientId, g.Key.UnitId, Required: g.Sum(r => r.Required), Reserved: g.Sum(r => r.Reserved)))
                    .Where(r => r.Required - r.Reserved > 0.0001m).ToList();
                if (shortRows.Count == 0)
                {
                    return Result<ProductionPlanShortageModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
                }
                var ingIds = shortRows.Select(r => r.IngredientId).Distinct().ToList();
                var ingNames = await db.Ingredient.AsNoTracking().Where(i => ingIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.Name ?? i.Code ?? string.Empty);
                var units = await db.UnitMeasure.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.Symbol ?? u.Name ?? u.Code ?? string.Empty);
                var poLines = await (from l in db.PurchaseOrderLine.AsNoTracking()
                                     join p in db.PurchaseOrder.AsNoTracking() on l.PurchaseOrderId equals p.Id
                                     where !l.IsDeleted && !p.IsDeleted && ingIds.Contains(l.IngredientId) && OpenPoStatuses.Contains(p.Status!)
                                     select new { l.IngredientId, l.Quantity, l.UnitId, l.UomId, p.Id, p.PurchaseOrderNumber, p.PONo, p.Status, p.ExpectedDeliveryDate, p.ExpectedDate }).ToListAsync();
                model.ListLine = [.. shortRows.Select(r => new ProductionPlanShortageLineModel
                {
                    IngredientId = r.IngredientId, IngredientName = ingNames.GetValueOrDefault(r.IngredientId), UnitName = units.GetValueOrDefault(r.UnitId),
                    Required = ProductionRules.Round4(r.Required), Reserved = ProductionRules.Round4(r.Reserved), Short = ProductionRules.Round4(r.Required - r.Reserved),
                    ListIncoming = [.. poLines.Where(p => p.IngredientId == r.IngredientId).OrderBy(p => p.ExpectedDeliveryDate ?? p.ExpectedDate).Select(p => new ProductionPlanIncomingModel
                    {
                        PurchaseOrderId = p.Id, PurchaseOrderNumber = p.PurchaseOrderNumber ?? p.PONo, Status = PurchaseOrderStatus.Label(p.Status), Quantity = p.Quantity,
                        UnitName = units.GetValueOrDefault(p.UnitId != Guid.Empty ? p.UnitId : p.UomId), ExpectedDate = p.ExpectedDeliveryDate ?? p.ExpectedDate,
                    })],
                }).OrderByDescending(l => l.Short)];
                return Result<ProductionPlanShortageModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetShortage: ", ex.ToString()));
                throw;
            }
        }
    }
}
