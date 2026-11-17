using AppDelicare.Data.Connection;
using AppDelicare.Data.Interface;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// BE2.4 (spec 7.9 / 8.2 close / 8.5, Catalog 135, 144, 152–154) – giá thành thực tế theo mẻ / lệnh SX và chênh lệch tiêu hao (lớp tĩnh, DbContext của service gọi).
    /// PROPOSED – BA/PO CONFIRM (D-53 f, g).
    /// <list type="bullet">
    /// <item>NVL = Σ (dòng phiếu xuất Đã xuất × đơn giá lô <c>MaterialLot.UnitCost</c>); phiếu xuất gắn mẻ (Note "Mẻ ME-…", GAP-PRD-04) tính cho mẻ đó,
    /// phiếu không gắn mẻ chia cho các mẻ theo số phần kế hoạch.</item>
    /// <item>Nhân công: <c>ProductionLaborCostPerHour</c> &gt; 0 và mẻ có ghi công đoạn → Σ giờ công đoạn × số người × đơn giá; còn lại số phần nấu được × <c>RecipeLaborCostPerServing</c>.</item>
    /// <item>Chi phí chung = số phần nấu được × <c>RecipeOverheadCostPerServing</c>; khác (bao bì, vận chuyển, điện nước – GAP-C-01) = 0.</item>
    /// <item>Hao hụt = Σ suất hao hụt (MealWasteRecord) × giá thành chuẩn / phần (không có → NVL / phần nấu được) – <b>chỉ để phân tích</b>,
    /// không cộng vào tổng (chi phí NVL của phần hao hụt đã nằm trong NVL).</item>
    /// <item>Tổng = NVL + nhân công + chung + khác; giá thành / phần = tổng ÷ số phần đạt (đã đóng gói / đạt).</item>
    /// </list>
    /// </summary>
    public static class CostingRules
    {
        public const string Currency = "VND";

        private static decimal Money(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

        /// <summary>Giá thành chuẩn / phần đang hiệu lực của món (StandardCost Active mới nhất, không có thì Dish.StandardCost).</summary>
        public static async Task<decimal?> StandardPerPortionAsync(DatabaseContext db, Guid dishId)
        {
            var sc = await db.StandardCost.AsNoTracking().Where(s => !s.IsDeleted && s.DishId == dishId && s.Status != "Inactive")
                .OrderByDescending(s => s.EffectiveFrom).ThenByDescending(s => s.CreatedDate).FirstOrDefaultAsync();
            if (sc != null)
            {
                return sc.CostPerServing != 0 ? sc.CostPerServing : sc.TotalCost;
            }
            return await db.Dish.AsNoTracking().Where(d => d.Id == dishId).Select(d => d.StandardCost).FirstOrDefaultAsync();
        }

        /// <summary>Số phần nấu được / đạt của mẻ (sản lượng đã ghi, không có thì số thực nấu).</summary>
        public static (decimal Produced, decimal Accepted) Portions(ProductionBatch b, ProductionOutput? output)
        {
            decimal produced = output?.ProducedQuantity ?? b.ActualQuantity ?? b.ActualQty ?? 0m;
            decimal accepted = b.Status is ProductionBatchStatus.Packed or ProductionBatchStatus.Passed
                ? output?.AcceptedQuantity ?? b.AcceptedQty ?? produced : 0m;
            return (produced, accepted);
        }

        /// <summary>Giá thành từng mẻ (chưa ghi) của lệnh SX.</summary>
        public static async Task<(List<(ProductionBatch Batch, ActualCostModel Cost)> Batches, List<string> Warnings)> ComputeAsync(DatabaseContext db, ProductionOrder order, BusinessRulesOptions rules)
        {
            var warnings = new List<string>();
            var batches = await db.ProductionBatch.AsNoTracking()
                .Where(b => !b.IsDeleted && b.ProductionOrderId == order.Id && b.Status != ProductionBatchStatus.Cancelled).OrderBy(b => b.StartDate).ToListAsync();
            if (batches.Count == 0)
            {
                return ([], warnings);
            }
            var batchIds = batches.Select(b => b.Id).ToList();
            var outputs = (await db.ProductionOutput.AsNoTracking().Where(o => !o.IsDeleted && batchIds.Contains(o.ProductionBatchId)).ToListAsync())
                .GroupBy(o => o.ProductionBatchId).ToDictionary(g => g.Key, g => g.First());
            var wastes = (await db.MealWasteRecord.AsNoTracking().Where(w => !w.IsDeleted && w.ProductionBatchId != null && batchIds.Contains(w.ProductionBatchId.Value)).ToListAsync())
                .GroupBy(w => w.ProductionBatchId!.Value).ToDictionary(g => g.Key, g => g.Sum(w => w.QuantityMeals ?? 0m));
            var issues = await db.MaterialIssue.AsNoTracking().Where(i => !i.IsDeleted && i.ProductionOrderId == order.Id && i.Status == MaterialIssueStatus.Issued).ToListAsync();
            var issueIds = issues.Select(i => i.Id).ToList();
            var lines = await db.MaterialIssueLine.AsNoTracking().Where(l => !l.IsDeleted && issueIds.Contains(l.MaterialIssueId)).ToListAsync();
            var lotIds = lines.Where(l => (l.MaterialLotId ?? l.LotId) != null).Select(l => (l.MaterialLotId ?? l.LotId)!.Value).Distinct().ToList();
            var lots = await db.MaterialLot.AsNoTracking().Where(l => lotIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id);
            var material = batchIds.ToDictionary(id => id, _ => 0m);
            decimal pool = 0m;
            foreach (var issue in issues)
            {
                decimal cost = 0m;
                foreach (var line in lines.Where(l => l.MaterialIssueId == issue.Id))
                {
                    Guid? lotId = line.MaterialLotId ?? line.LotId;
                    var lot = lotId != null ? lots.GetValueOrDefault(lotId.Value) : null;
                    if (lot?.UnitCost == null)
                    {
                        warnings.Add(string.Format(ConstantHelper.Message.ProductionCostLotNoUnitCost, issue.IssueNumber ?? issue.IssueNo, lot?.LotNumber ?? lot?.LotNo ?? "–"));
                        continue;
                    }
                    cost += line.Quantity * lot.UnitCost.Value;
                }
                var owner = batches.FirstOrDefault(b => issue.Note != null && issue.Note.Contains(b.BatchNumber ?? b.BatchNo ?? b.Id.ToString()));
                if (owner != null)
                {
                    material[owner.Id] += cost;
                }
                else
                {
                    pool += cost;
                    warnings.Add(string.Format(ConstantHelper.Message.ProductionCostIssueNotAllocated, issue.IssueNumber ?? issue.IssueNo));
                }
            }
            if (pool != 0)
            {
                decimal planned = batches.Sum(b => b.PlannedQuantity ?? b.PlannedQty ?? 0m);
                foreach (var b in batches)
                {
                    material[b.Id] += planned > 0 ? pool * (b.PlannedQuantity ?? b.PlannedQty ?? 0m) / planned : pool / batches.Count;
                }
            }
            var stages = rules.ProductionLaborCostPerHour > 0 ? await ProductionStageRules.StagesAsync(db, batchIds, rules) : [];
            decimal? standard = await StandardPerPortionAsync(db, order.DishId);
            var shifts = await db.Shift.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Code);
            var dish = await db.Dish.AsNoTracking().Where(d => d.Id == order.DishId).Select(d => d.Name).FirstOrDefaultAsync();
            var plan = await db.ProductionPlan.AsNoTracking().FirstOrDefaultAsync(p => p.Id == order.ProductionPlanId);
            bool warnedStandard = false;
            var result = new List<(ProductionBatch, ActualCostModel)>();
            foreach (var b in batches)
            {
                var (produced, accepted) = Portions(b, outputs.GetValueOrDefault(b.Id));
                decimal mat = Money(material[b.Id]);
                var mineStages = stages.GetValueOrDefault(b.Id) ?? [];
                decimal hours = ProductionStageRules.LaborHours(mineStages);
                decimal labor = Money(rules.ProductionLaborCostPerHour > 0 && hours > 0 ? hours * rules.ProductionLaborCostPerHour : produced * rules.RecipeLaborCostPerServing);
                decimal overhead = Money(produced * rules.RecipeOverheadCostPerServing);
                decimal wasteMeals = wastes.GetValueOrDefault(b.Id);
                decimal perPortion = standard ?? (produced > 0 ? mat / produced : 0m);
                if (standard == null && wasteMeals > 0 && !warnedStandard)
                {
                    warnings.Add(ConstantHelper.Message.ProductionCostNoStandard);
                    warnedStandard = true;
                }
                decimal total = mat + labor + overhead;
                result.Add((b, new ActualCostModel
                {
                    ProductionOrderId = order.Id,
                    ProductionOrderNumber = order.ProductionOrderNumber ?? order.ProductionOrderNo,
                    ProductionBatchId = b.Id,
                    BatchNumber = b.BatchNumber ?? b.BatchNo,
                    ProductionDate = plan?.ProductionDate,
                    DishId = order.DishId,
                    DishName = dish,
                    ShiftCode = order.ShiftId != null ? shifts.GetValueOrDefault(order.ShiftId.Value) : null,
                    Status = b.Status,
                    MaterialCost = mat,
                    LaborCost = labor,
                    OverheadCost = overhead,
                    WasteCost = Money(wasteMeals * perPortion),
                    OtherCost = 0m,
                    TotalCost = total,
                    AcceptedPortions = accepted,
                    CostPerPortion = accepted > 0 ? Math.Round(total / accepted, 4) : null,
                    StandardCostPerPortion = standard,
                }));
            }
            return (result, [.. warnings.Distinct()]);
        }

        /// <summary>Tính + ghi ActualCost theo mẻ (cập nhật tại chỗ, 1 dòng / mẻ). Trả tổng của lệnh. Service gọi lưu.</summary>
        public static async Task<ActualCostModel> UpsertAsync(IUnitOfWork uow, ProductionOrder order, BusinessRulesOptions rules, DateTime now)
        {
            var (batches, warnings) = await ComputeAsync(uow.DbContext, order, rules);
            var batchIds = batches.Select(b => b.Batch.Id).ToList();
            var existing = uow.ActualCostRepository.GetAll().Where(c => !c.IsDeleted && batchIds.Contains(c.ProductionBatchId)).ToList();
            foreach (var (batch, cost) in batches)
            {
                var row = existing.FirstOrDefault(c => c.ProductionBatchId == batch.Id);
                if (row == null)
                {
                    row = new ActualCost { Id = Guid.NewGuid(), ProductionBatchId = batch.Id };
                    uow.ActualCostRepository.Add(row);
                }
                else
                {
                    uow.ActualCostRepository.Update(row);
                }
                row.ProductionOrderId = order.Id;
                row.MaterialCost = cost.MaterialCost;
                row.LaborCost = cost.LaborCost;
                row.OverheadCost = cost.OverheadCost;
                row.WasteCost = cost.WasteCost;
                row.OtherCost = cost.OtherCost;
                row.TotalCost = cost.TotalCost;
                row.CurrencyCode = Currency;
                row.CalculatedAt = now;
                cost.CalculatedAt = now;
            }
            var total = Aggregate(order, [.. batches.Select(b => b.Cost)]);
            total.CalculatedAt = now;
            total.ListWarning.AddRange(warnings);
            return total;
        }

        /// <summary>Cộng giá thành các mẻ thành giá thành lệnh.</summary>
        public static ActualCostModel Aggregate(ProductionOrder order, List<ActualCostModel> batches)
        {
            decimal accepted = batches.Sum(b => b.AcceptedPortions);
            decimal total = batches.Sum(b => b.TotalCost);
            var first = batches.FirstOrDefault();
            return new ActualCostModel
            {
                ProductionOrderId = order.Id,
                ProductionOrderNumber = order.ProductionOrderNumber ?? order.ProductionOrderNo,
                ProductionDate = first?.ProductionDate,
                DishId = order.DishId,
                DishName = first?.DishName,
                ShiftCode = first?.ShiftCode,
                Status = order.Status,
                MaterialCost = batches.Sum(b => b.MaterialCost),
                LaborCost = batches.Sum(b => b.LaborCost),
                OverheadCost = batches.Sum(b => b.OverheadCost),
                WasteCost = batches.Sum(b => b.WasteCost),
                OtherCost = batches.Sum(b => b.OtherCost),
                TotalCost = total,
                AcceptedPortions = accepted,
                CostPerPortion = accepted > 0 ? Math.Round(total / accepted, 4) : null,
                StandardCostPerPortion = first?.StandardCostPerPortion,
                CalculatedAt = batches.Count == 0 ? null : batches.Max(b => b.CalculatedAt),
            };
        }

        /// <summary>Giá thành đã ghi (ActualCost) của các lệnh – theo lệnh, cộng các mẻ. Lệnh chưa tính → không có trong kết quả.</summary>
        public static async Task<Dictionary<Guid, ActualCostModel>> StoredAsync(DatabaseContext db, IReadOnlyCollection<Guid> orderIds)
        {
            var rows = await db.ActualCost.AsNoTracking().Where(c => !c.IsDeleted && c.ProductionOrderId != null && orderIds.Contains(c.ProductionOrderId.Value)).ToListAsync();
            if (rows.Count == 0)
            {
                return [];
            }
            var ids = rows.Select(r => r.ProductionOrderId!.Value).Distinct().ToList();
            var orders = await db.ProductionOrder.AsNoTracking().Where(o => ids.Contains(o.Id)).ToDictionaryAsync(o => o.Id);
            var batchIds = rows.Select(r => r.ProductionBatchId).ToList();
            var batches = await db.ProductionBatch.AsNoTracking().Where(b => batchIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id);
            var outputs = (await db.ProductionOutput.AsNoTracking().Where(o => !o.IsDeleted && batchIds.Contains(o.ProductionBatchId)).ToListAsync())
                .GroupBy(o => o.ProductionBatchId).ToDictionary(g => g.Key, g => g.First());
            var planIds = orders.Values.Select(o => o.ProductionPlanId).Distinct().ToList();
            var plans = await db.ProductionPlan.AsNoTracking().Where(p => planIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.ProductionDate);
            var dishIds = orders.Values.Select(o => o.DishId).Distinct().ToList();
            var dishes = await db.Dish.AsNoTracking().Where(d => dishIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name);
            var shifts = await db.Shift.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Code);
            var result = new Dictionary<Guid, ActualCostModel>();
            foreach (var group in rows.GroupBy(r => r.ProductionOrderId!.Value))
            {
                var order = orders[group.Key];
                decimal? standard = await StandardPerPortionAsync(db, order.DishId);
                var parts = group.Where(r => batches.ContainsKey(r.ProductionBatchId)).Select(r =>
                {
                    var b = batches[r.ProductionBatchId];
                    var (_, accepted) = Portions(b, outputs.GetValueOrDefault(b.Id));
                    return new ActualCostModel
                    {
                        ProductionOrderId = order.Id,
                        ProductionBatchId = b.Id,
                        BatchNumber = b.BatchNumber ?? b.BatchNo,
                        ProductionDate = plans.GetValueOrDefault(order.ProductionPlanId),
                        DishId = order.DishId,
                        DishName = dishes.GetValueOrDefault(order.DishId),
                        ShiftCode = order.ShiftId != null ? shifts.GetValueOrDefault(order.ShiftId.Value) : null,
                        Status = b.Status,
                        MaterialCost = r.MaterialCost ?? 0m,
                        LaborCost = r.LaborCost ?? 0m,
                        OverheadCost = r.OverheadCost ?? 0m,
                        WasteCost = r.WasteCost ?? 0m,
                        OtherCost = r.OtherCost,
                        TotalCost = r.TotalCost ?? 0m,
                        AcceptedPortions = accepted,
                        StandardCostPerPortion = standard,
                        CalculatedAt = r.CalculatedAt,
                    };
                }).ToList();
                result[group.Key] = Aggregate(order, parts);
            }
            return result;
        }

        /// <summary>
        /// Chênh lệch tiêu hao (spec 7.9) của lệnh: theo nguyên liệu, đơn vị = đơn vị xuất kho. Chuẩn = định lượng (ảnh chụp) × số phần nấu được ÷ suất chuẩn × (1 + hao hụt %);
        /// thực tế = Σ dòng phiếu xuất Đã xuất của lệnh; chênh lệch = thực tế − chuẩn; % = chênh ÷ chuẩn × 100; giá trị = chênh × đơn giá bình quân đã xuất.
        /// </summary>
        public static async Task<(List<MaterialConsumptionVariance> Rows, List<string> Warnings)> VariancesAsync(DatabaseContext db, IUomConversionService conversion,
            ProductionOrder order, BusinessRulesOptions rules, DateTime today)
        {
            var warnings = new List<string>();
            var batches = await db.ProductionBatch.AsNoTracking()
                .Where(b => !b.IsDeleted && b.ProductionOrderId == order.Id && b.Status != ProductionBatchStatus.Cancelled).ToListAsync();
            var batchIds = batches.Select(b => b.Id).ToList();
            var outputs = (await db.ProductionOutput.AsNoTracking().Where(o => !o.IsDeleted && batchIds.Contains(o.ProductionBatchId)).ToListAsync())
                .GroupBy(o => o.ProductionBatchId).ToDictionary(g => g.Key, g => g.First());
            decimal produced = batches.Sum(b => Portions(b, outputs.GetValueOrDefault(b.Id)).Produced);
            var needs = await ReservationRules.OrderNeedsAsync(db, order, rules, produced);
            var issueIds = await db.MaterialIssue.AsNoTracking().Where(i => !i.IsDeleted && i.ProductionOrderId == order.Id && i.Status == MaterialIssueStatus.Issued)
                .Select(i => i.Id).ToListAsync();
            if (issueIds.Count == 0)
            {
                warnings.Add(string.Format(ConstantHelper.Message.ProductionOrderNoIssue, order.ProductionOrderNumber ?? order.ProductionOrderNo));
            }
            var lines = await db.MaterialIssueLine.AsNoTracking().Where(l => !l.IsDeleted && issueIds.Contains(l.MaterialIssueId)).ToListAsync();
            var lotIds = lines.Where(l => (l.MaterialLotId ?? l.LotId) != null).Select(l => (l.MaterialLotId ?? l.LotId)!.Value).Distinct().ToList();
            var lots = await db.MaterialLot.AsNoTracking().Where(l => lotIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.UnitCost);
            var ingredientIds = needs.Select(n => n.IngredientId).Concat(lines.Select(l => l.IngredientId)).Distinct().ToList();
            var ingredients = await db.Ingredient.AsNoTracking().Where(i => ingredientIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id);
            var units = await db.UnitMeasure.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.Name ?? u.Code);
            var rows = new List<MaterialConsumptionVariance>();
            foreach (Guid ingredientId in ingredientIds)
            {
                var ingredient = ingredients.GetValueOrDefault(ingredientId);
                var mineLines = lines.Where(l => l.IngredientId == ingredientId).ToList();
                Guid unit = mineLines.GroupBy(l => l.UnitId).OrderByDescending(g => g.Sum(l => l.Quantity)).Select(g => g.Key).FirstOrDefault();
                if (unit == Guid.Empty)
                {
                    unit = (ingredient != null ? await ReservationRules.StockUnitAsync(db, ingredient) : null) ?? needs.First(n => n.IngredientId == ingredientId).RecipeUnitId;
                }
                decimal actual = 0m, cost = 0m, costedQty = 0m;
                bool incomplete = false;
                foreach (var l in mineLines)
                {
                    decimal? q = await ProductionRules.ToUnitAsync(conversion, l.Quantity, l.UnitId, unit, today);
                    if (q == null)
                    {
                        incomplete = true;
                        continue;
                    }
                    actual += q.Value;
                    if ((l.MaterialLotId ?? l.LotId) is { } lotId && lots.GetValueOrDefault(lotId) is { } price)
                    {
                        cost += l.Quantity * price;
                        costedQty += q.Value;
                    }
                }
                decimal standard = 0m;
                foreach (var n in needs.Where(n => n.IngredientId == ingredientId))
                {
                    decimal? q = await ProductionRules.ToUnitAsync(conversion, n.Quantity, n.RecipeUnitId, unit, today);
                    if (q == null)
                    {
                        incomplete = true;
                        continue;
                    }
                    standard += q.Value;
                }
                if (incomplete)
                {
                    warnings.Add(string.Format(ConstantHelper.Message.VarianceUnitMissing, ingredient?.Name, "đơn vị công thức / xuất", units.GetValueOrDefault(unit)));
                }
                standard = ProductionRules.Round4(standard);
                actual = ProductionRules.Round4(actual);
                decimal variance = actual - standard;
                decimal? pct = standard > 0 ? Math.Round(variance / standard * 100m, 2) : actual > 0 ? 100m : 0m;
                decimal? avg = costedQty > 0 ? cost / costedQty : null;
                rows.Add(new MaterialConsumptionVariance
                {
                    ProductionOrderId = order.Id,
                    IngredientId = ingredientId,
                    StandardQuantity = standard,
                    TheoreticalQty = standard,
                    ActualQuantity = actual,
                    ActualQty = actual,
                    VarianceQuantity = variance,
                    VarianceQty = variance,
                    VariancePercent = pct,
                    VarianceCost = avg != null ? Math.Round(variance * avg.Value, 4) : null,
                    VarianceValue = avg != null ? Money(variance * avg.Value) : null,
                    Reason = $"Đóng lệnh {order.ProductionOrderNumber ?? order.ProductionOrderNo} – đơn vị {units.GetValueOrDefault(unit)}; định mức theo {ProductionRules.Num(produced)} phần nấu được",
                });
            }
            return (rows, [.. warnings.Distinct()]);
        }

        /// <summary>Model chênh lệch tiêu hao (kèm lệnh, món, ngày SX, cờ vượt ngưỡng).</summary>
        public static async Task<List<ConsumptionVarianceModel>> VarianceModelsAsync(DatabaseContext db, List<MaterialConsumptionVariance> rows, BusinessRulesOptions rules)
        {
            var orderIds = rows.Select(r => r.ProductionOrderId).Distinct().ToList();
            var orders = await db.ProductionOrder.AsNoTracking().Where(o => orderIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id);
            var planIds = orders.Values.Select(o => o.ProductionPlanId).Distinct().ToList();
            var plans = await db.ProductionPlan.AsNoTracking().Where(p => planIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.ProductionDate);
            var dishIds = orders.Values.Select(o => o.DishId).Distinct().ToList();
            var dishes = await db.Dish.AsNoTracking().Where(d => dishIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name);
            var ingredientIds = rows.Select(r => r.IngredientId).Distinct().ToList();
            var ingredients = await db.Ingredient.AsNoTracking().Where(i => ingredientIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id);
            return [.. rows.Select(r =>
            {
                var order = orders.GetValueOrDefault(r.ProductionOrderId);
                string? reason = r.Reason;
                int idx = reason?.IndexOf("đơn vị ", StringComparison.Ordinal) ?? -1;
                string? unitName = idx >= 0 ? reason![(idx + 7)..].Split(';')[0] : null;
                return new ConsumptionVarianceModel
                {
                    Id = r.Id,
                    ProductionOrderId = r.ProductionOrderId,
                    ProductionOrderNumber = order?.ProductionOrderNumber ?? order?.ProductionOrderNo,
                    ProductionDate = order != null && plans.TryGetValue(order.ProductionPlanId, out var d) ? d : null,
                    DishId = order?.DishId,
                    DishName = order != null ? dishes.GetValueOrDefault(order.DishId) : null,
                    IngredientId = r.IngredientId,
                    IngredientCode = ingredients.GetValueOrDefault(r.IngredientId)?.Code,
                    IngredientName = ingredients.GetValueOrDefault(r.IngredientId)?.Name,
                    UnitName = unitName,
                    StandardQuantity = r.StandardQuantity,
                    ActualQuantity = r.ActualQuantity,
                    VarianceQuantity = r.VarianceQuantity,
                    VariancePercent = r.VariancePercent,
                    VarianceCost = r.VarianceCost,
                    IsOverThreshold = r.VariancePercent is { } p && Math.Abs(p) > rules.ConsumptionVariancePctWarning,
                    Reason = r.Reason,
                };
            }).OrderBy(v => v.ProductionOrderNumber).ThenBy(v => v.IngredientName)];
        }
    }
}
