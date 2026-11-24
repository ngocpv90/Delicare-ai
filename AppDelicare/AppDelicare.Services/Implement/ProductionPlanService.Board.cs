using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// UI-6.24 (D-105): "Bảng sản xuất ngày" của trang kế hoạch – ma trận Món (nhóm theo nhóm món) × Ca: suất, lệnh (thao tác được phép), mẻ xong / tổng,
    /// trạng thái ô theo mẻ (chờ nấu · đang nấu · chờ QC · đạt · có mẻ loại / tạm giữ · chưa ra lệnh), dây chuyền, giờ dự kiến, dòng giữ chỗ thiếu.
    /// Kèm nguyên liệu & giữ chỗ gộp cả ngày, chênh lệch tiêu hao và giá thành theo lệnh. Chỉ đọc – thao tác dùng API lệnh hiện có.
    /// </summary>
    public partial class ProductionPlanService
    {
        public static string CellLabel(string state) => state switch
        {
            "waiting" => "Chờ nấu",
            "cooking" => "Đang nấu",
            "qc" => "Chờ QC",
            "passed" => "Đạt / đã đóng gói",
            "problem" => "Có mẻ loại / tạm giữ",
            _ => "Chưa ra lệnh",
        };

        public async Task<IResult<ProductionDayBoardModel>> GetDayBoard(Guid id)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                var plan = await db.ProductionPlan.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
                if (plan == null)
                {
                    return Result<ProductionDayBoardModel>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
                }
                DateTime date = plan.ProductionDate.Date;
                var lines = await db.ProductionPlanLine.AsNoTracking().Where(l => l.ProductionPlanId == id && !l.IsDeleted).ToListAsync();
                var orders = await db.ProductionOrder.AsNoTracking().Where(o => o.ProductionPlanId == id && !o.IsDeleted && o.Status != ProductionOrderStatus.Cancelled).ToListAsync();
                var orderIds = orders.Select(o => o.Id).ToList();
                var batches = await db.ProductionBatch.AsNoTracking().Where(b => orderIds.Contains(b.ProductionOrderId) && !b.IsDeleted && b.Status != ProductionBatchStatus.Cancelled).ToListAsync();
                var (parts, _, _, _) = await ProductionRules.SplitByShiftAsync(db, date, lines, _rules);
                var releasedShifts = orders.Where(o => o.ShiftId != null).Select(o => o.ShiftId!.Value).ToHashSet();

                var shiftIds = parts.Select(p => p.Shift.Id).Concat(releasedShifts).Distinct().ToList();
                var shifts = await db.Shift.AsNoTracking().Where(s => shiftIds.Contains(s.Id)).ToListAsync();
                var dishIds = lines.Where(l => l.PlannedQuantity > 0).Select(l => l.DishId).Concat(orders.Select(o => o.DishId)).Distinct().ToList();
                var dishes = await db.Dish.AsNoTracking().Where(d => dishIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id);
                var catIds = dishes.Values.Where(d => d.DishCategoryId != null).Select(d => d.DishCategoryId!.Value).Distinct().ToList();
                var cats = await db.DishCategory.AsNoTracking().Where(c => catIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name ?? c.Code ?? string.Empty);
                var lineIds = orders.Where(o => o.ProductionLineId != null).Select(o => o.ProductionLineId!.Value).Distinct().ToList();
                var lineNames = await db.ProductionLine.AsNoTracking().Where(l => lineIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.Name ?? l.Code ?? string.Empty);
                var reservations = orderIds.Count == 0 ? [] : await ReservationRules.ModelsAsync(db, orderIds);

                var model = new ProductionDayBoardModel
                {
                    PlanId = plan.Id, PlanNumber = plan.PlanNumber ?? plan.PlanNo, ProductionDate = date, Status = plan.Status, StatusName = ProductionPlanStatus.Label(plan.Status),
                    TotalPortions = lines.Where(l => l.PlannedQuantity > 0).Sum(l => l.PlannedQuantity), AutoClose = _rules.ProductionAutoCloseOrder,
                    BatchTotal = batches.Count,
                    BatchDone = batches.Count(b => b.Status is ProductionBatchStatus.Passed or ProductionBatchStatus.Packed),
                    BatchQcOrProblem = batches.Count(b => b.Status is ProductionBatchStatus.QcPending or ProductionBatchStatus.Failed || b.QcStatus == BatchQcStatus.Hold),
                    OrderTotal = orders.Count, OrderClosed = orders.Count(o => o.Status == ProductionOrderStatus.Closed),
                    PendingShiftCount = parts.Count(p => !releasedShifts.Contains(p.Shift.Id)),
                };
                model.AllowedActions = plan.Status switch
                {
                    ProductionPlanStatus.Draft => ["approve"],
                    ProductionPlanStatus.Approved => [.. (model.PendingShiftCount > 0 ? new[] { "release" } : []), .. (orders.Count != 0 ? new[] { "exportProductionSheet" } : [])],
                    ProductionPlanStatus.Released or ProductionPlanStatus.Completed => ["exportProductionSheet"],
                    _ => [],
                };
                model.ListShift = [.. shifts.OrderBy(s => s.StartTime ?? TimeSpan.MaxValue).ThenBy(s => s.Code).Select(s => new ProductionDayBoardShiftModel
                {
                    ShiftId = s.Id, ShiftCode = s.Code, ShiftName = s.Name ?? s.Code, StartTime = s.StartTime, Released = releasedShifts.Contains(s.Id),
                    Portions = releasedShifts.Contains(s.Id) ? orders.Where(o => o.ShiftId == s.Id).Sum(o => o.PlannedQty) : parts.FirstOrDefault(p => p.Shift.Id == s.Id)?.Portions ?? 0m,
                })];

                var rows = new List<(Guid? Cat, ProductionDayBoardRowModel Row)>();
                foreach (Guid dishId in dishIds)
                {
                    var dish = dishes.GetValueOrDefault(dishId);
                    var row = new ProductionDayBoardRowModel { DishId = dishId, DishName = dish?.Name ?? dish?.Code };
                    foreach (var s in model.ListShift)
                    {
                        var mine = orders.Where(o => o.DishId == dishId && o.ShiftId == s.ShiftId).ToList();
                        var cell = new ProductionDayBoardCellModel { ShiftId = s.ShiftId };
                        if (mine.Count == 0)
                        {
                            cell.Portions = parts.Where(p => p.Shift.Id == s.ShiftId).SelectMany(p => p.Lines).Where(l => l.Line.DishId == dishId).Sum(l => l.Quantity);
                            if (cell.Portions <= 0)
                            {
                                continue;
                            }
                            cell.State = "planned";
                        }
                        else
                        {
                            var mineIds = mine.Select(o => o.Id).ToHashSet();
                            var bs = batches.Where(b => mineIds.Contains(b.ProductionOrderId)).ToList();
                            cell.Portions = mine.Sum(o => o.PlannedQty);
                            cell.BatchTotal = bs.Count;
                            cell.BatchDone = bs.Count(b => b.Status is ProductionBatchStatus.Passed or ProductionBatchStatus.Packed or ProductionBatchStatus.Failed);
                            cell.StartAt = bs.Count == 0 ? mine.Min(o => o.StartTime) : bs.Min(b => (DateTime?)b.StartDate);
                            cell.EndAt = bs.Count == 0 ? mine.Max(o => o.EndTime) : bs.Max(b => b.EndDate);
                            cell.LineName = string.Join(", ", mine.Where(o => o.ProductionLineId != null).Select(o => lineNames.GetValueOrDefault(o.ProductionLineId!.Value)).Where(n => !string.IsNullOrEmpty(n)).Distinct());
                            cell.ShortQuantityLines = reservations.Count(r => mineIds.Contains(r.ProductionOrderId) && r.ShortQuantity > 0);
                            cell.State = bs.Any(b => b.Status == ProductionBatchStatus.Failed || b.QcStatus == BatchQcStatus.Hold) ? "problem"
                                : bs.Count != 0 && bs.All(b => b.Status is ProductionBatchStatus.Passed or ProductionBatchStatus.Packed) ? "passed"
                                : bs.Any(b => b.Status == ProductionBatchStatus.QcPending) ? "qc"
                                : bs.Any(b => b.Status == ProductionBatchStatus.Cooking) ? "cooking"
                                : mine.All(o => o.Status is ProductionOrderStatus.Completed or ProductionOrderStatus.Closed) ? "passed" : "waiting";
                            cell.ListOrder = [.. mine.OrderBy(o => o.ProductionOrderNumber).Select(o =>
                            {
                                var acts = o.Status switch
                                {
                                    ProductionOrderStatus.Planned or ProductionOrderStatus.Released => new List<string> { "reserve", "cancel" },
                                    ProductionOrderStatus.InProgress => ["reserve", "calculateCost"],
                                    ProductionOrderStatus.Completed => ["calculateCost", "close"],
                                    ProductionOrderStatus.Closed => ["calculateCost"],
                                    _ => [],
                                };
                                if (batches.Any(b => b.ProductionOrderId == o.Id && b.Status != ProductionBatchStatus.Planned))
                                {
                                    acts.Remove("cancel");
                                }
                                return new ProductionDayBoardOrderModel { Id = o.Id, Number = o.ProductionOrderNumber ?? o.ProductionOrderNo, Status = o.Status, StatusName = ProductionOrderStatus.Label(o.Status), PlannedQty = o.PlannedQty, AllowedActions = acts };
                            })];
                        }
                        cell.StateName = CellLabel(cell.State);
                        row.ListCell.Add(cell);
                    }
                    if (row.ListCell.Count != 0)
                    {
                        rows.Add((dish?.DishCategoryId, row));
                    }
                }
                model.ListGroup = [.. rows.GroupBy(r => r.Cat)
                    .Select(g => new ProductionDayBoardGroupModel
                    {
                        CategoryId = g.Key, CategoryName = g.Key is { } c && cats.TryGetValue(c, out var n) ? n : "Chưa có nhóm món",
                        ListRow = [.. g.Select(r => r.Row).OrderBy(r => r.DishName)],
                    })
                    .OrderBy(g => g.CategoryId == null ? 1 : 0).ThenBy(g => g.CategoryName)];

                model.ListMaterial = [.. reservations.GroupBy(r => (r.IngredientId, r.UnitId))
                    .Select(g => new ProductionDayBoardMaterialModel
                    {
                        IngredientId = g.Key.IngredientId, IngredientName = g.First().IngredientName ?? g.First().IngredientCode, UnitName = g.First().UnitName,
                        Required = g.Sum(r => r.RequiredQuantity), Reserved = g.Sum(r => r.ReservedQuantity), Short = g.Sum(r => r.ShortQuantity), OrderCount = g.Select(r => r.ProductionOrderId).Distinct().Count(),
                    })
                    .OrderByDescending(m => m.Short).ThenBy(m => m.IngredientName)];

                var orderMap = orders.ToDictionary(o => o.Id);
                var variances = orderIds.Count == 0 ? [] : await db.MaterialConsumptionVariance.AsNoTracking().Where(v => orderIds.Contains(v.ProductionOrderId) && !v.IsDeleted).ToListAsync();
                var ingIds = variances.Select(v => v.IngredientId).Distinct().ToList();
                var ingNames = await db.Ingredient.AsNoTracking().Where(i => ingIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.Name ?? i.Code ?? string.Empty);
                model.ListVariance = [.. variances.Select(v => new ProductionDayBoardVarianceModel
                {
                    ProductionOrderId = v.ProductionOrderId, OrderNumber = orderMap[v.ProductionOrderId].ProductionOrderNumber ?? orderMap[v.ProductionOrderId].ProductionOrderNo,
                    DishName = dishes.GetValueOrDefault(orderMap[v.ProductionOrderId].DishId)?.Name, IngredientName = ingNames.GetValueOrDefault(v.IngredientId),
                    StandardQuantity = v.StandardQuantity, ActualQuantity = v.ActualQuantity, VariancePercent = v.VariancePercent, VarianceCost = v.VarianceCost,
                    IsOverThreshold = v.VariancePercent is { } p && Math.Abs(p) > _rules.ConsumptionVariancePctWarning,
                }).OrderByDescending(v => v.IsOverThreshold).ThenByDescending(v => Math.Abs(v.VariancePercent ?? 0))];

                var costs = orderIds.Count == 0 ? [] : await db.ActualCost.AsNoTracking().Where(c => c.ProductionOrderId != null && orderIds.Contains(c.ProductionOrderId.Value) && !c.IsDeleted)
                    .GroupBy(c => c.ProductionOrderId!.Value).Select(g => new { g.Key, Total = g.Sum(c => c.TotalCost ?? 0m) }).ToListAsync();
                var shiftCode = shifts.ToDictionary(s => s.Id, s => s.Code);
                model.ListCost = [.. costs.Where(c => orderMap.ContainsKey(c.Key)).Select(c =>
                {
                    var o = orderMap[c.Key];
                    decimal accepted = o.ActualQty ?? 0m;
                    return new ProductionDayBoardCostModel
                    {
                        ProductionOrderId = o.Id, OrderNumber = o.ProductionOrderNumber ?? o.ProductionOrderNo, DishName = dishes.GetValueOrDefault(o.DishId)?.Name,
                        ShiftCode = o.ShiftId is { } sid ? shiftCode.GetValueOrDefault(sid) : null, Status = ProductionOrderStatus.Label(o.Status), AcceptedPortions = accepted,
                        TotalCost = Math.Round(c.Total, 0), CostPerPortion = accepted > 0 ? Math.Round(c.Total / accepted, 0) : null,
                    };
                }).OrderBy(c => c.ShiftCode).ThenBy(c => c.DishName)];
                return Result<ProductionDayBoardModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetDayBoard: ", ex.ToString()));
                throw;
            }
        }
    }
}
