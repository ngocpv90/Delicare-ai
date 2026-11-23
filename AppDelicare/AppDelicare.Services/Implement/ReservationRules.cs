using System.Globalization;
using AppDelicare.Data.Connection;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// BE2.4 (spec 7.7 / 8.1 release bước 4 / 8.2 close – cancel, Catalog 126) – giữ chỗ nguyên liệu cho lệnh SX (lớp tĩnh, dùng DbContext / transaction của service gọi).
    /// <list type="bullet">
    /// <item><c>MaterialReservation</c> không có cột kho / lô (GAP-INV-13): giữ chỗ theo (lệnh SX, nguyên liệu, đơn vị tồn). <c>RequiredQty</c> = số cần lúc giữ,
    /// <c>ReservedQuantity</c> = số còn đang giữ (giảm khi xuất cho lệnh, về 0 khi giải phóng).</item>
    /// <item><c>InventoryBalance.ReservedQuantity</c> là số <b>suy ra</b>: tổng giữ chỗ còn hiệu lực của nguyên liệu phân bổ FEFO lên các dòng tồn xuất được
    /// (mọi kho, lô không bị chặn hôm nay), <c>AvailableQuantity</c> = OnHand − Reserved. Phân bổ lại sau mỗi lần ghi sổ của nguyên liệu (InventoryLedgerService)
    /// và mỗi lần giữ / xuất / giải phóng. PROPOSED – BA/PO CONFIRM (D-53 a).</item>
    /// <item>Xuất cho chính lệnh đã giữ: khả dụng = OnHand − phần giữ của các lệnh khác (<see cref="AllocationAsync"/> loại trừ lệnh).</item>
    /// </list>
    /// </summary>
    public static class ReservationRules
    {
        private static string Num(decimal value) => value.ToString("0.####", CultureInfo.InvariantCulture);

        /// <summary>Giữ chỗ còn hiệu lực (Đã giữ / Đã xuất một phần).</summary>
        public static IQueryable<MaterialReservation> Open(DatabaseContext db)
            => db.MaterialReservation.Where(r => !r.IsDeleted && (r.Status == MaterialReservationStatus.Reserved || r.Status == MaterialReservationStatus.PartiallyIssued));

        /// <summary>Nguyên liệu có giữ chỗ còn hiệu lực (số giữ &gt; 0) – trong <paramref name="ingredientIds"/>.</summary>
        public static Task<bool> AnyOpenAsync(DatabaseContext db, IReadOnlyCollection<Guid> ingredientIds, Guid? productionOrderId = null)
            => Open(db).AsNoTracking().AnyAsync(r => ingredientIds.Contains(r.IngredientId) && r.ReservedQuantity > 0
                && (productionOrderId == null || r.ProductionOrderId == productionOrderId));

        private sealed record Row(Guid BalanceId, Guid WarehouseId, Guid? StorageLocationId, Guid IngredientId, Guid UnitId, decimal OnHand, Guid? LotId);

        /// <summary>Thứ tự FEFO để phân bổ giữ chỗ: lô có hạn gần trước, rồi ngày nhận, số lô; tồn không gắn lô cuối.</summary>
        private static async Task<List<(Row Row, MaterialLot? Lot)>> UsableRowsAsync(DatabaseContext db, IReadOnlyCollection<Guid> ingredientIds, DateTime day)
        {
            var rows = await db.InventoryBalance.AsNoTracking()
                .Where(b => !b.IsDeleted && ingredientIds.Contains(b.IngredientId) && b.OnHandQuantity > 0)
                .Select(b => new Row(b.Id, b.WarehouseId, b.StorageLocationId, b.IngredientId, b.UnitId, b.OnHandQuantity, b.MaterialLotId))
                .ToListAsync();
            var lotIds = rows.Where(r => r.LotId != null).Select(r => r.LotId!.Value).Distinct().ToList();
            var lots = await db.MaterialLot.AsNoTracking().Where(l => lotIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id);
            return [.. rows
                .Select(r => (Row: r, Lot: r.LotId != null ? lots.GetValueOrDefault(r.LotId.Value) : null))
                .Where(x => (x.Row.LotId == null || x.Lot != null) && InventoryLotRules.BlockReason(x.Lot, day) == null)
                .OrderBy(x => x.Row.LotId == null ? 1 : 0)
                .ThenBy(x => x.Lot?.ExpiryDate?.Date ?? DateTime.MaxValue)
                .ThenBy(x => x.Lot?.ReceivedDate ?? x.Lot?.CreatedDate ?? DateTime.MaxValue)
                .ThenBy(x => x.Lot?.LotNumber ?? x.Lot?.LotNo ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(x => x.Row.WarehouseId)
                .ThenByDescending(x => x.Row.OnHand)];
        }

        /// <summary>
        /// Phân bổ giữ chỗ còn hiệu lực của các nguyên liệu lên dòng tồn (BalanceId → số giữ), FEFO, không vượt OnHand; lệnh <paramref name="excludeOrderId"/> không tính.
        /// Phần giữ không phân bổ được (tồn thực tế giảm sau khi giữ) bỏ qua – lệnh xem ở chi tiết giữ chỗ.
        /// </summary>
        public static async Task<Dictionary<Guid, decimal>> AllocationAsync(DatabaseContext db, IReadOnlyCollection<Guid> ingredientIds, DateTime today, Guid? excludeOrderId)
        {
            var result = new Dictionary<Guid, decimal>();
            if (ingredientIds.Count == 0)
            {
                return result;
            }
            var totals = (await Open(db).AsNoTracking()
                    .Where(r => ingredientIds.Contains(r.IngredientId) && r.ReservedQuantity > 0 && (excludeOrderId == null || r.ProductionOrderId != excludeOrderId))
                    .Select(r => new { r.IngredientId, r.UnitId, r.ReservedQuantity }).ToListAsync())
                .GroupBy(r => (r.IngredientId, r.UnitId)).ToDictionary(g => g.Key, g => g.Sum(x => x.ReservedQuantity));
            if (totals.Count == 0)
            {
                return result;
            }
            var usable = await UsableRowsAsync(db, [.. totals.Keys.Select(k => k.IngredientId).Distinct()], today);
            foreach (var ((ingredientId, unitId), total) in totals)
            {
                decimal remaining = total;
                foreach (var (row, _) in usable.Where(x => x.Row.IngredientId == ingredientId && x.Row.UnitId == unitId))
                {
                    if (remaining <= 0)
                    {
                        break;
                    }
                    decimal take = Math.Min(row.OnHand, remaining);
                    result[row.BalanceId] = take;
                    remaining -= take;
                }
            }
            return result;
        }

        /// <summary>Ghi lại InventoryBalance.ReservedQuantity / AvailableQuantity của các nguyên liệu theo phân bổ (lưu ngay – gọi sau khi đã lưu giữ chỗ / bút toán).</summary>
        public static async Task ReallocateAsync(DatabaseContext db, IReadOnlyCollection<Guid> ingredientIds, DateTime today)
        {
            if (ingredientIds.Count == 0)
            {
                return;
            }
            var alloc = await AllocationAsync(db, ingredientIds, today, null);
            var ids = alloc.Keys.ToList();
            var balances = await db.InventoryBalance
                .Where(b => !b.IsDeleted && ingredientIds.Contains(b.IngredientId) && (b.ReservedQuantity != 0 || ids.Contains(b.Id)))
                .ToListAsync();
            bool changed = false;
            DateTime now = DateTime.Now;
            foreach (var b in balances)
            {
                decimal reserved = alloc.GetValueOrDefault(b.Id);
                if (b.ReservedQuantity == reserved && b.AvailableQuantity == b.OnHandQuantity - reserved)
                {
                    continue;
                }
                b.ReservedQuantity = b.ReservedQty = reserved;
                b.AvailableQuantity = b.AvailableQty = b.OnHandQuantity - reserved;
                b.UpdatedDate = now;
                changed = true;
            }
            if (changed)
            {
                await db.SaveChangesAsync(ConstantHelper.TypeSave.Update);
            }
        }

        /// <summary>
        /// Xuất cho lệnh <paramref name="productionOrderId"/>: khả dụng của từng dòng tồn = OnHand − phần giữ của các lệnh khác (phần lệnh này đã giữ được dùng).
        /// Trả BalanceId → khả dụng; null = lệnh không có giữ chỗ (dùng AvailableQuantity đã lưu).
        /// </summary>
        public static async Task<Dictionary<Guid, decimal>?> AvailableForOrderAsync(DatabaseContext db, IReadOnlyCollection<Guid> ingredientIds, DateTime today, Guid? productionOrderId)
        {
            if (productionOrderId is not { } orderId || orderId == Guid.Empty || !await AnyOpenAsync(db, ingredientIds, orderId))
            {
                return null;
            }
            var alloc = await AllocationAsync(db, ingredientIds, today, orderId);
            return await db.InventoryBalance.AsNoTracking()
                .Where(b => !b.IsDeleted && ingredientIds.Contains(b.IngredientId))
                .ToDictionaryAsync(b => b.Id, b => b.OnHandQuantity - alloc.GetValueOrDefault(b.Id));
        }

        /// <summary>Đơn vị tồn của nguyên liệu (mọi kho): đơn vị của dòng tồn còn nhiều nhất, không có thì đơn vị cơ sở.</summary>
        public static async Task<Guid?> StockUnitAsync(DatabaseContext db, Ingredient ingredient)
        {
            var unit = await db.InventoryBalance.AsNoTracking()
                .Where(b => !b.IsDeleted && b.IngredientId == ingredient.Id && b.OnHandQuantity > 0)
                .OrderByDescending(b => b.OnHandQuantity).Select(b => (Guid?)b.UnitId).FirstOrDefaultAsync();
            return unit ?? (ingredient.BaseUnitId is { } b && b != Guid.Empty ? b : ingredient.BaseUomId is { } o && o != Guid.Empty ? o : null);
        }

        /// <summary>Nhu cầu nguyên liệu của cả lệnh SX theo đơn vị công thức: định lượng (ảnh chụp) × phần kế hoạch ÷ suất chuẩn → lượng mua hao hụt 2 tầng (WasteRules – BIZ-1.1) – cùng công thức mẻ / MRP.</summary>
        public static async Task<List<(Guid IngredientId, Guid RecipeUnitId, decimal Quantity)>> OrderNeedsAsync(DatabaseContext db, ProductionOrder order, BusinessRulesOptions rules, decimal? portions = null)
        {
            var lines = ProductionRules.ParseSnapshot(order.RecipeSnapshotJson);
            var recipe = await db.Recipe.AsNoTracking().FirstOrDefaultAsync(r => r.Id == order.RecipeId);
            decimal servings = recipe?.StandardOutputQty is > 0 ? recipe.StandardOutputQty.Value : rules.RecipeStandardServings;
            decimal qty = portions ?? order.PlannedQty;
            var ingredientIds = lines.Select(l => l.IngredientId).Distinct().ToList();
            var ingredients = await db.Ingredient.AsNoTracking().Where(i => ingredientIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id);
            return [.. lines.Where(l => l.Quantity > 0).GroupBy(l => (l.IngredientId, l.UnitId))
                .Select(g => (g.Key.IngredientId, g.Key.UnitId, g.Sum(l => WasteRules.Gross(l.Quantity * qty / servings,
                    l.WastePercent ?? ingredients.GetValueOrDefault(l.IngredientId)?.StandardWastePercent, l.ExpectedWasteRate))))];
        }

        /// <summary>
        /// Giữ chỗ cho các lệnh SX vừa phát hành / giữ bổ sung (spec 8.1 bước 4): mỗi (lệnh, nguyên liệu) 1 dòng, số giữ = min(cần, khả dụng còn lại) trên lô xuất được
        /// tại ngày sản xuất (trừ giữ chỗ còn hiệu lực của các lệnh khác). Thiếu → vẫn phát hành, trả cảnh báo. Lệnh đã có dòng giữ (chưa xuất) → giữ thêm tới đủ.
        /// Chỉ thêm / sửa entity (service gọi lưu rồi gọi <see cref="ReallocateAsync"/>). Trả (nguyên liệu đã đụng tới, cảnh báo).
        /// </summary>
        public static async Task<(HashSet<Guid> Ingredients, List<string> Warnings)> ReserveAsync(DatabaseContext db, IUomConversionService conversion,
            IReadOnlyList<ProductionOrder> orders, DateTime productionDate, DateTime today, BusinessRulesOptions rules, Action<MaterialReservation> add)
        {
            var touched = new HashSet<Guid>();
            var warnings = new List<string>();
            if (orders.Count == 0)
            {
                return (touched, warnings);
            }
            DateTime day = productionDate.Date > today.Date ? productionDate.Date : today.Date;
            var needs = new List<(ProductionOrder Order, Guid IngredientId, Guid RecipeUnitId, decimal Quantity)>();
            foreach (var order in orders)
            {
                needs.AddRange((await OrderNeedsAsync(db, order, rules)).Select(n => (order, n.IngredientId, n.RecipeUnitId, n.Quantity)));
            }
            var ingredientIds = needs.Select(n => n.IngredientId).Distinct().ToList();
            var ingredients = await db.Ingredient.AsNoTracking().Where(i => ingredientIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id);
            var units = await db.UnitMeasure.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.Name ?? u.Code);
            var usable = await UsableRowsAsync(db, ingredientIds, day);
            var orderIds = orders.Select(o => o.Id).ToList();
            var openRows = await Open(db).Where(r => ingredientIds.Contains(r.IngredientId)).ToListAsync();
            // Khả dụng còn giữ được theo (nguyên liệu, đơn vị) = Σ OnHand lô xuất được − Σ đang giữ (mọi lệnh, kể cả dòng cũ của chính các lệnh này).
            var pool = usable.GroupBy(x => (x.Row.IngredientId, x.Row.UnitId)).ToDictionary(g => g.Key, g => g.Sum(x => x.Row.OnHand));
            foreach (var r in openRows)
            {
                pool[(r.IngredientId, r.UnitId)] = pool.GetValueOrDefault((r.IngredientId, r.UnitId)) - r.ReservedQuantity;
            }
            var numbers = orders.ToDictionary(o => o.Id, o => o.ProductionOrderNumber ?? o.ProductionOrderNo ?? o.Id.ToString("N")[..8]);
            foreach (var group in needs.GroupBy(n => (n.Order.Id, n.IngredientId)))
            {
                var ingredient = ingredients.GetValueOrDefault(group.Key.IngredientId);
                if (ingredient == null)
                {
                    continue;
                }
                Guid stockUnit = await StockUnitAsync(db, ingredient) ?? group.First().RecipeUnitId;
                decimal required = 0m;
                bool missingUnit = false;
                foreach (var n in group)
                {
                    decimal? converted = await ProductionRules.ToUnitAsync(conversion, n.Quantity, n.RecipeUnitId, stockUnit, today);
                    if (converted == null)
                    {
                        missingUnit = true;
                        break;
                    }
                    required += converted.Value;
                }
                if (missingUnit)
                {
                    warnings.Add(string.Format(ConstantHelper.Message.ReservationUnitMissing, numbers[group.Key.Id], ingredient.Name,
                        units.GetValueOrDefault(group.First().RecipeUnitId), units.GetValueOrDefault(stockUnit)));
                    continue;
                }
                required = ProductionRules.Ceil4(required);
                var existing = openRows.FirstOrDefault(r => r.ProductionOrderId == group.Key.Id && r.IngredientId == ingredient.Id && r.UnitId == stockUnit);
                if (existing != null && existing.Status != MaterialReservationStatus.Reserved)
                {
                    continue; // đã xuất một phần – không giữ thêm.
                }
                decimal current = existing?.ReservedQuantity ?? 0m;
                decimal free = Math.Max(0m, pool.GetValueOrDefault((ingredient.Id, stockUnit)));
                decimal reserve = Math.Min(Math.Max(0m, required - current), free);
                pool[(ingredient.Id, stockUnit)] = pool.GetValueOrDefault((ingredient.Id, stockUnit)) - reserve;
                decimal held = current + reserve;
                if (existing == null)
                {
                    add(new MaterialReservation
                    {
                        Id = Guid.NewGuid(),
                        ProductionOrderId = group.Key.Id,
                        IngredientId = ingredient.Id,
                        UnitId = stockUnit,
                        RequiredQty = required,
                        ReservedQuantity = held,
                        ReservedQty = held,
                        Status = MaterialReservationStatus.Reserved,
                    });
                }
                else
                {
                    existing.RequiredQty = required;
                    existing.ReservedQuantity = existing.ReservedQty = held;
                }
                touched.Add(ingredient.Id);
                if (held < required)
                {
                    warnings.Add(string.Format(ConstantHelper.Message.ReservationShort, numbers[group.Key.Id], ingredient.Name,
                        Num(held), Num(required), units.GetValueOrDefault(stockUnit), Num(required - held)));
                }
            }
            return (touched, warnings);
        }

        /// <summary>Xuất cho lệnh: giảm số đang giữ theo số đã xuất (cùng nguyên liệu + đơn vị); hết → Đã xuất, còn → Đã xuất một phần. Chỉ sửa entity.</summary>
        public static async Task<HashSet<Guid>> ConsumeAsync(DatabaseContext db, Guid productionOrderId, IEnumerable<(Guid IngredientId, Guid UnitId, decimal Quantity)> issued)
        {
            var touched = new HashSet<Guid>();
            var rows = await Open(db).Where(r => r.ProductionOrderId == productionOrderId).ToListAsync();
            foreach (var group in issued.GroupBy(i => (i.IngredientId, i.UnitId)))
            {
                decimal remaining = group.Sum(i => i.Quantity);
                foreach (var row in rows.Where(r => r.IngredientId == group.Key.IngredientId && r.UnitId == group.Key.UnitId))
                {
                    decimal take = Math.Min(row.ReservedQuantity, remaining);
                    row.ReservedQuantity = row.ReservedQty = row.ReservedQuantity - take;
                    remaining -= take;
                    row.Status = row.ReservedQuantity <= 0 ? MaterialReservationStatus.Issued : MaterialReservationStatus.PartiallyIssued;
                    touched.Add(row.IngredientId);
                }
            }
            return touched;
        }

        /// <summary>Giải phóng mọi giữ chỗ còn hiệu lực của lệnh (hủy / đóng lệnh – spec 8.2): số giữ về 0, trạng thái Đã giải phóng. Chỉ sửa entity.</summary>
        public static async Task<HashSet<Guid>> ReleaseAsync(DatabaseContext db, Guid productionOrderId)
        {
            var rows = await Open(db).Where(r => r.ProductionOrderId == productionOrderId).ToListAsync();
            foreach (var row in rows)
            {
                row.ReservedQuantity = row.ReservedQty = 0;
                row.Status = MaterialReservationStatus.Released;
            }
            return [.. rows.Select(r => r.IngredientId)];
        }

        /// <summary>Danh sách giữ chỗ của các lệnh (chi tiết lệnh SX).</summary>
        public static async Task<List<MaterialReservationModel>> ModelsAsync(DatabaseContext db, IReadOnlyCollection<Guid> productionOrderIds)
        {
            var rows = await db.MaterialReservation.AsNoTracking().Where(r => !r.IsDeleted && productionOrderIds.Contains(r.ProductionOrderId)).ToListAsync();
            var ingredientIds = rows.Select(r => r.IngredientId).Distinct().ToList();
            var ingredients = await db.Ingredient.AsNoTracking().Where(i => ingredientIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id);
            var units = await db.UnitMeasure.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.Name ?? u.Code);
            var orders = await db.ProductionOrder.AsNoTracking().Where(o => productionOrderIds.Contains(o.Id))
                .ToDictionaryAsync(o => o.Id, o => o.ProductionOrderNumber ?? o.ProductionOrderNo);
            var issuedBy = await IssuedQuantitiesAsync(db, productionOrderIds);
            return [.. rows.Select(r =>
            {
                decimal issued = issuedBy.GetValueOrDefault((r.ProductionOrderId, r.IngredientId, r.UnitId));
                decimal initiallyHeld = r.Status == MaterialReservationStatus.Released ? 0m : r.ReservedQuantity + Math.Min(issued, r.RequiredQty);
                return new MaterialReservationModel
                {
                    Id = r.Id,
                    ProductionOrderId = r.ProductionOrderId,
                    ProductionOrderNumber = orders.GetValueOrDefault(r.ProductionOrderId),
                    IngredientId = r.IngredientId,
                    IngredientCode = ingredients.GetValueOrDefault(r.IngredientId)?.Code,
                    IngredientName = ingredients.GetValueOrDefault(r.IngredientId)?.Name,
                    UnitId = r.UnitId,
                    UnitName = units.GetValueOrDefault(r.UnitId),
                    RequiredQuantity = r.RequiredQty,
                    ReservedQuantity = r.ReservedQuantity,
                    ShortQuantity = r.Status == MaterialReservationStatus.Reserved ? Math.Max(0m, r.RequiredQty - r.ReservedQuantity) : 0m,
                    Status = r.Status,
                    StatusName = MaterialReservationStatus.Label(r.Status),
                };
            }).OrderBy(r => r.ProductionOrderNumber).ThenBy(r => r.IngredientName)];
        }

        /// <summary>Số đã xuất (phiếu xuất Đã xuất) theo (lệnh, nguyên liệu, đơn vị).</summary>
        public static async Task<Dictionary<(Guid OrderId, Guid IngredientId, Guid UnitId), decimal>> IssuedQuantitiesAsync(DatabaseContext db, IReadOnlyCollection<Guid> productionOrderIds)
            => (await (from i in db.MaterialIssue.AsNoTracking()
                       join l in db.MaterialIssueLine.AsNoTracking() on i.Id equals l.MaterialIssueId
                       where !i.IsDeleted && !l.IsDeleted && i.Status == MaterialIssueStatus.Issued && productionOrderIds.Contains(i.ProductionOrderId)
                       select new { i.ProductionOrderId, l.IngredientId, l.UnitId, l.Quantity }).ToListAsync())
                .GroupBy(x => (x.ProductionOrderId, x.IngredientId, x.UnitId)).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
    }
}
