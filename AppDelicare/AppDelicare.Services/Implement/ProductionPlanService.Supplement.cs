using System.Globalization;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Services.Implement.Messaging;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// D-78 (1) – anh chốt 2026-11-23: đơn chốt SAU khi kế hoạch của ngày đã duyệt / phát hành (duyệt sửa / thêm đơn sau giờ chốt, đơn bù BIZ-3.1) →
    /// giữ 1 KH / ngày, hệ thống tự sinh <b>lệnh SX + mẻ bổ sung</b> gắn kế hoạch đó cho riêng đơn này (công thức khóa tại ngày, lịch ngược theo giờ xuất bếp của ca;
    /// không kịp → bắt đầu ngay), cộng vào dòng kế hoạch, giữ chỗ nguyên liệu, phiếu xuất Nháp theo ca, đơn → Đang sản xuất. Lệnh ghi
    /// <c>[BO_SUNG:&lt;số đơn&gt;]</c> + nhãn 🔴 (Đơn bù / Bổ sung sau chốt) – bảng điều độ hiện nhãn, xếp ưu tiên. Kế hoạch còn Nháp / chưa có → tự lập như sau chốt (BIZ-2.4).
    /// Thiếu công thức khóa → không sinh, bot 🔴 báo Kế hoạch + COO.
    /// </summary>
    public partial class ProductionPlanService
    {
        public const string SupplementTag = "[BO_SUNG:";

        public async Task<ProductionAutoPlanModel> SupplementForOrderAsync(Guid orderId, string? reason)
        {
            var db = _unitOfWork.DbContext;
            var result = new ProductionAutoPlanModel();
            try
            {
                var order = await db.Order.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted);
                if (order == null || order.Status != OrderStatus.Locked)
                {
                    result.ListReason.Add(ConstantHelper.Message.SupplementOrderNotLocked);
                    return result;
                }
                DateTime date = order.MealDate.Date, next = date.AddDays(1);
                result.ProductionDate = date;
                string orderNo = order.OrderNumber ?? order.OrderNo ?? order.Id.ToString("N")[..8];
                var plan = await db.ProductionPlan.AsNoTracking()
                    .Where(p => !p.IsDeleted && p.ProductionDate >= date && p.ProductionDate < next && p.OrganizationUnitId == null && p.ShiftId == null)
                    .OrderBy(p => p.CreatedDate).FirstOrDefaultAsync();
                if (plan == null || plan.Status == ProductionPlanStatus.Draft)
                {
                    // Chưa có / còn Nháp → lập lại cả ngày như sau chốt (đơn mới nằm trong kế hoạch).
                    return _rules.ProductionAutoReleaseAfterLock ? await AutoPlanAfterLockAsync(date) : result;
                }
                (result.PlanId, result.PlanNumber) = (plan.Id, plan.PlanNumber ?? plan.PlanNo);
                if (plan.Status is not (ProductionPlanStatus.Approved or ProductionPlanStatus.Released))
                {
                    result.Status = AutoSkipped;
                    result.ListReason.Add(string.Format(ConstantHelper.Message.AutoPlanAlreadyApproved, result.PlanNumber, ProductionPlanStatus.Label(plan.Status)));
                    return result;
                }
                string tag = SupplementTag + orderNo + "]";
                if (await db.ProductionOrder.AsNoTracking().AnyAsync(p => !p.IsDeleted && p.Note != null && p.Note.Contains(tag) && p.Status != ProductionOrderStatus.Cancelled))
                {
                    result.Status = AutoSkipped;
                    result.ListReason.Add(string.Format(ConstantHelper.Message.SupplementExists, orderNo));
                    return result;
                }
                var (demands, _, _, fallback) = await ProductionRules.DishDemandsAsync(db, date, [OrderStatus.Locked], _rules);
                var mine = demands.Where(d => d.OrderId == orderId).GroupBy(d => (d.DishId, d.ShiftId))
                    .Select(g => (g.Key.DishId, g.Key.ShiftId, Quantity: ProductionRules.Round4(g.Sum(x => x.Quantity)))).Where(x => x.Quantity > 0).ToList();
                if (mine.Count == 0)
                {
                    result.Status = AutoNone;
                    result.ListReason.Add(ConstantHelper.Message.SupplementNoDemand);
                    return result;
                }
                var dishIds = mine.Select(m => m.DishId).Distinct().ToList();
                var dishNames = await db.Dish.AsNoTracking().Where(d => dishIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name ?? d.Code ?? string.Empty);
                var recipes = new Dictionary<Guid, (Recipe Recipe, string Snapshot)>();
                var missing = new List<string>();
                foreach (Guid dishId in dishIds)
                {
                    var (recipe, locked, snapshot) = await ProductionRules.SelectRecipeAsync(db, dishId, date);
                    if (recipe == null || !locked || snapshot.Count == 0)
                    {
                        missing.Add(dishNames.GetValueOrDefault(dishId) ?? dishId.ToString());
                        continue;
                    }
                    recipes[dishId] = (recipe, ProductionRules.ParseSnapshot(recipe.SnapshotJson).Count != 0 ? recipe.SnapshotJson! : Newtonsoft.Json.JsonConvert.SerializeObject(snapshot));
                }
                bool compensation = order.Note?.Contains("[DON_BU:", StringComparison.Ordinal) == true;
                string label = compensation ? "🔴 Đơn bù" : "🔴 Bổ sung sau chốt";
                if (missing.Count != 0)
                {
                    return await HoldAsync(result, [string.Format(ConstantHelper.Message.AutoPlanNoLockedRecipe, string.Join(", ", missing)) + $" – {label} {orderNo}"]);
                }

                var newOrders = new List<ProductionOrder>();
                var shiftCodes = new Dictionary<Guid, string?>();
                var warnings = new List<string>();
                using (var tran = await _unitOfWork.OpenTransaction())
                {
                    try
                    {
                        DateTime now = _clock.Now;
                        string firstOrderNo = await _documentNumberService.NextAsync(DocumentKind.ProductionOrder, _clock.Today);
                        int orderIndex = 0;
                        int minutes = _rules.ProductionDefaultMinutesPerBatch;
                        // UI-6.26 (D-107): cùng cách chọn dây chuyền theo nhóm món như Ra lệnh kế hoạch.
                        var dishLines = await ProductionLineRules.ForDishesAsync(db, [.. mine.Select(m => m.DishId).Distinct()]);
                        warnings.AddRange(mine.Select(m => dishLines.GetValueOrDefault(m.DishId)).Where(x => x?.Line == null && x?.Warning != null).Select(x => x!.Warning!).Distinct());
                        var planLines = _unitOfWork.ProductionPlanLineRepository.GetAll().Where(l => l.ProductionPlanId == plan.Id && !l.IsDeleted).ToList();
                        foreach (var byShift in mine.GroupBy(m => m.ShiftId))
                        {
                            var shift = await db.Shift.AsNoTracking().FirstOrDefaultAsync(s => s.Id == byShift.Key) ?? fallback;
                            if (shift == null)
                            {
                                continue;
                            }
                            shiftCodes[shift.Id] = shift.Code;
                            var (dispatchAt, _) = await ProductionRules.ResolveDispatchAsync(db, date, shift, null, _rules, fallback?.Id);
                            DateTime cookEnd = (dispatchAt ?? now.AddMinutes(minutes + _rules.ProductionPackingMinutes)).AddMinutes(-_rules.ProductionPackingMinutes);
                            string firstBatchNo = await _documentNumberService.NextAsync(DocumentKind.ProductionBatch, date, shift.Code);
                            int batchIndex = 0;
                            foreach (var item in byShift.OrderBy(m => dishNames.GetValueOrDefault(m.DishId)))
                            {
                                var (recipe, snapshot) = recipes[item.DishId];
                                var itemLine = dishLines.GetValueOrDefault(item.DishId)?.Line;
                                int itemMinutes = ProductionLineRules.Minutes(itemLine, minutes);
                                var batches = ProductionRules.Schedule(item.Quantity, itemLine?.CapacityMealPerBatch, itemMinutes, cookEnd);
                                if (batches.Min(b => b.Start) < now)
                                {
                                    // Không kịp lịch ngược → ưu tiên bắt đầu ngay.
                                    batches = ProductionRules.Schedule(item.Quantity, itemLine?.CapacityMealPerBatch, itemMinutes, now.AddMinutes(itemMinutes * batches.Count));
                                }
                                string number = ProductionRules.Increment(firstOrderNo, orderIndex++);
                                var po = new ProductionOrder
                                {
                                    Id = Guid.NewGuid(), ProductionOrderNumber = number, ProductionOrderNo = number, ProductionPlanId = plan.Id, DishId = item.DishId, RecipeId = recipe.Id,
                                    ShiftId = shift.Id, ProductionLineId = itemLine?.Id, PlannedQty = item.Quantity, Status = ProductionOrderStatus.Released,
                                    PlannedStartDate = batches.Min(b => b.Start), StartTime = batches.Min(b => b.Start), EndTime = batches.Max(b => b.End), RecipeSnapshotJson = snapshot,
                                    Note = $"{tag} {label} – đơn {orderNo} – ca {shift.Code} – kế hoạch {result.PlanNumber}" + (string.IsNullOrWhiteSpace(reason) ? string.Empty : " – " + reason.Trim()),
                                };
                                _unitOfWork.ProductionOrderRepository.Add(po);
                                newOrders.Add(po);
                                foreach (var b in batches)
                                {
                                    string batchNo = ProductionRules.Increment(firstBatchNo, batchIndex++);
                                    _unitOfWork.ProductionBatchRepository.Add(new ProductionBatch
                                    {
                                        Id = Guid.NewGuid(), BatchNumber = batchNo, BatchNo = batchNo, ProductionOrderId = po.Id, DishId = po.DishId, RecipeId = po.RecipeId,
                                        PlannedQty = b.Quantity, PlannedQuantity = b.Quantity, StartDate = b.Start, EndDate = b.End, Status = ProductionBatchStatus.Planned,
                                    });
                                }
                                var line = planLines.FirstOrDefault(l => l.DishId == item.DishId);
                                if (line == null)
                                {
                                    line = new ProductionPlanLine { Id = Guid.NewGuid(), ProductionPlanId = plan.Id, DishId = item.DishId, ShiftId = shift.Id, Status = ProductionPlanStatus.Released };
                                    _unitOfWork.ProductionPlanLineRepository.Add(line);
                                    planLines.Add(line);
                                }
                                else
                                {
                                    _unitOfWork.ProductionPlanLineRepository.Update(line);
                                }
                                line.PlannedQuantity += item.Quantity;
                                line.PlannedQty = line.PlannedQuantity;
                            }
                        }
                        var customerOrder = _unitOfWork.OrderRepository.GetSingle(o => o.Id == orderId && !o.IsDeleted);
                        if (customerOrder != null && newOrders.Count != 0)
                        {
                            customerOrder.Status = OrderStatus.InProduction;
                            _unitOfWork.OrderRepository.Update(customerOrder);
                        }
                        HashSet<Guid> reserved = [];
                        if (_rules.ProductionReserveOnRelease && newOrders.Count != 0)
                        {
                            var (touched, reserveWarnings) = await ReservationRules.ReserveAsync(db, _uomConversionService, newOrders, date, _clock.Today, _rules,
                                r => _unitOfWork.MaterialReservationRepository.Add(r));
                            reserved = touched;
                            warnings.AddRange(reserveWarnings);
                        }
                        await _unitOfWork.ProductionOrderRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
                        await ReservationRules.ReallocateAsync(db, reserved, _clock.Today);
                        _unitOfWork.CommitTransaction();
                    }
                    catch
                    {
                        _unitOfWork.RollbackTransaction();
                        db.ChangeTracker.Clear();
                        throw;
                    }
                }
                warnings.AddRange(await CreateShiftIssueDraftsAsync(result.PlanNumber, [.. newOrders.Select(o => (o.Id, o.ShiftId ?? Guid.Empty, shiftCodes.GetValueOrDefault(o.ShiftId ?? Guid.Empty), o.ProductionOrderNumber))]));
                result.Status = AutoReleased;
                result.ProductionOrderCount = newOrders.Count;
                result.ListWarning.AddRange(warnings);
                await BusinessNotifier.ToMenusAsync(_notifications, db, NotificationCatalog.PlanReady, ["#/prod-plan", "#/prod-batch", "#/log-route"],
                    new
                    {
                        title = $"{label} {orderNo}: {newOrders.Count} lệnh SX bổ sung – nấu ưu tiên",
                        lines = newOrders.Select(o => $"{o.ProductionOrderNumber} · {dishNames.GetValueOrDefault(o.DishId)} · {ProductionRules.Num(o.PlannedQty)} phần · bắt đầu {o.StartTime?.ToString("HH:mm", CultureInfo.InvariantCulture)}").Concat(warnings.Take(3)).ToList(),
                    },
                    $"{NotificationCatalog.PlanReady}:BO_SUNG:{orderNo}");
                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[ProductionPlanService] SupplementForOrderAsync {Order}", orderId);
                result.Status = AutoHeld;
                result.ListReason.Add(ConstantHelper.Message.AutoPlanError);
                return result;
            }
        }
    }
}
