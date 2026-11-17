using System.Globalization;
using AppDelicare.Data.Connection;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// T16 – quy tắc lô dùng chung cho tồn kho, gợi ý / xuất kho, kiểm kê (BUSINESS_RULES §4–5, D-24).
    /// <list type="bullet">
    /// <item><b>Chặn xuất</b>: lô đã hết hạn (ExpiryDate &lt; hôm nay), QC KHONG_DAT / REJECT / HOLD, lô tạm giữ (OnHold) / Expired.</item>
    /// <item><b>Thứ tự xuất</b>: FEFO (hạn dùng gần nhất trước) hoặc FIFO (ngày nhận sớm nhất trước) theo <c>InventoryPolicie.UseFefo</c>
    /// của (kho, nguyên liệu); không có chính sách → FEFO (D-24). Tồn không gắn lô xếp cuối.</item>
    /// <item><b>Cảnh báo</b> (không chặn): chọn lô trong khi còn lô xuất được có hạn (FEFO) / ngày nhận (FIFO) sớm hơn.</item>
    /// </list>
    /// Lớp tĩnh (không phải service) – đọc qua <see cref="DatabaseContext"/> của service gọi để dùng chung transaction.
    /// </summary>
    public static class InventoryLotRules
    {
        /// <summary>Lý do lô không được xuất tại ngày <paramref name="today"/>; null = xuất được (tồn không gắn lô luôn xuất được).</summary>
        public static string? BlockReason(MaterialLot? lot, DateTime today)
        {
            if (lot == null)
            {
                return null;
            }
            string number = lot.LotNumber ?? lot.LotNo ?? lot.Id.ToString("N")[..8];
            if (lot.IsDeleted)
            {
                return $"Lô {number} đã bị xóa";
            }
            if (lot.ExpiryDate is { } expiry && expiry.Date < today.Date)
            {
                return $"Lô {number} đã hết hạn ({expiry.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)})";
            }
            string? qc = lot.QcStatus ?? lot.QualityStatus;
            if (LotQcStatus.IsBlocked(qc))
            {
                return $"Lô {number} có kết quả QC {qc!.Trim().ToUpperInvariant()} – không được xuất";
            }
            if (string.Equals(lot.Status, MaterialLotStatus.OnHold, StringComparison.OrdinalIgnoreCase))
            {
                return $"Lô {number} đang tạm giữ";
            }
            if (string.Equals(lot.Status, MaterialLotStatus.Expired, StringComparison.OrdinalIgnoreCase))
            {
                return $"Lô {number} đã hết hạn";
            }
            return null;
        }

        /// <summary>FEFO (true) / FIFO (false) theo InventoryPolicie của (kho, nguyên liệu); không có chính sách → FEFO (D-24).</summary>
        public static async Task<bool> UseFefoAsync(DatabaseContext db, Guid warehouseId, Guid ingredientId)
        {
            var policy = await db.InventoryPolicy.AsNoTracking()
                .Where(p => !p.IsDeleted && p.WarehouseId == warehouseId && p.IngredientId == ingredientId)
                .OrderByDescending(p => p.CreatedDate)
                .Select(p => (bool?)p.UseFefo)
                .FirstOrDefaultAsync();
            return policy ?? true;
        }

        /// <summary>Khóa sắp xếp thứ tự xuất (nhỏ hơn = xuất trước).</summary>
        private static (int NoLot, DateTime First, DateTime Second, string Lot) SortKey(InventoryLotBalanceModel row, bool useFefo)
        {
            DateTime expiry = row.ExpiryDate?.Date ?? DateTime.MaxValue;
            DateTime received = row.ReceivedDate ?? DateTime.MaxValue;
            return (row.MaterialLotId == null ? 1 : 0, useFefo ? expiry : received, useFefo ? received : expiry, row.LotNumber ?? string.Empty);
        }

        public static IOrderedEnumerable<InventoryLotBalanceModel> OrderForPick(IEnumerable<InventoryLotBalanceModel> rows, bool useFefo)
            => rows.OrderBy(r => SortKey(r, useFefo).NoLot)
                .ThenBy(r => SortKey(r, useFefo).First)
                .ThenBy(r => SortKey(r, useFefo).Second)
                .ThenBy(r => SortKey(r, useFefo).Lot, StringComparer.Ordinal);

        /// <summary><paramref name="other"/> nên xuất trước <paramref name="chosen"/>: hạn dùng (FEFO) / ngày nhận (FIFO) sớm hơn hẳn (theo ngày).</summary>
        public static bool IsEarlier(InventoryLotBalanceModel other, InventoryLotBalanceModel chosen, bool useFefo)
        {
            DateTime? a = useFefo ? other.ExpiryDate?.Date : other.ReceivedDate?.Date;
            DateTime? b = useFefo ? chosen.ExpiryDate?.Date : chosen.ReceivedDate?.Date;
            return a != null && (b == null || a < b);
        }

        /// <summary>
        /// Tồn theo dòng InventoryBalance + thông tin lô + lý do chặn + thứ tự xuất (PickRank theo lô trong từng (kho, nguyên liệu)).
        /// <paramref name="defaultExpiringDays"/> = BusinessRules:ExpiringLotDays (chính sách tồn có ExpiryWarningDays thì dùng số đó).
        /// </summary>
        public static async Task<List<InventoryLotBalanceModel>> LoadAsync(DatabaseContext db, DateTime today, int defaultExpiringDays,
            Guid? warehouseId = null, Guid? ingredientId = null, Guid? materialLotId = null, bool includeZero = false)
        {
            today = today.Date;
            var balances = await db.InventoryBalance.AsNoTracking()
                .Where(b => !b.IsDeleted
                    && (warehouseId == null || b.WarehouseId == warehouseId)
                    && (ingredientId == null || b.IngredientId == ingredientId)
                    && (materialLotId == null || b.MaterialLotId == materialLotId)
                    && (includeZero || b.OnHandQuantity != 0))
                .ToListAsync();
            if (balances.Count == 0)
            {
                return [];
            }
            var lotIds = balances.Where(b => b.MaterialLotId != null).Select(b => b.MaterialLotId!.Value).Distinct().ToList();
            var ingredientIds = balances.Select(b => b.IngredientId).Distinct().ToList();
            var warehouseIds = balances.Select(b => b.WarehouseId).Distinct().ToList();
            var locationIds = balances.Where(b => b.StorageLocationId != null).Select(b => b.StorageLocationId!.Value).Distinct().ToList();
            var unitIds = balances.Select(b => b.UnitId).Distinct().ToList();

            var lots = await db.MaterialLot.AsNoTracking().Where(l => lotIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id);
            var ingredients = await db.Ingredient.AsNoTracking().Where(i => ingredientIds.Contains(i.Id))
                .Select(i => new { i.Id, i.Code, i.Name }).ToDictionaryAsync(i => i.Id);
            var warehouses = await db.Warehouse.AsNoTracking().Where(w => warehouseIds.Contains(w.Id))
                .Select(w => new { w.Id, w.Name }).ToDictionaryAsync(w => w.Id);
            var locations = await db.StorageLocation.AsNoTracking().Where(s => locationIds.Contains(s.Id))
                .Select(s => new { s.Id, s.Name }).ToDictionaryAsync(s => s.Id);
            var units = await db.UnitMeasure.AsNoTracking().Where(u => unitIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Name }).ToDictionaryAsync(u => u.Id);
            var policies = (await db.InventoryPolicy.AsNoTracking()
                    .Where(p => !p.IsDeleted && ingredientIds.Contains(p.IngredientId) && warehouseIds.Contains(p.WarehouseId))
                    .OrderByDescending(p => p.CreatedDate)
                    .ToListAsync())
                .GroupBy(p => (p.WarehouseId, p.IngredientId))
                .ToDictionary(g => g.Key, g => g.First());

            var rows = new List<InventoryLotBalanceModel>(balances.Count);
            foreach (var b in balances)
            {
                MaterialLot? lot = b.MaterialLotId != null && lots.TryGetValue(b.MaterialLotId.Value, out var l) ? l : null;
                policies.TryGetValue((b.WarehouseId, b.IngredientId), out var policy);
                int warnDays = policy?.ExpiryWarningDays ?? defaultExpiringDays;
                DateTime? expiry = lot?.ExpiryDate?.Date;
                bool expired = expiry != null && expiry < today;
                var ingredient = ingredients.GetValueOrDefault(b.IngredientId);
                rows.Add(new InventoryLotBalanceModel
                {
                    BalanceId = b.Id,
                    WarehouseId = b.WarehouseId,
                    WarehouseName = warehouses.GetValueOrDefault(b.WarehouseId)?.Name,
                    StorageLocationId = b.StorageLocationId,
                    StorageLocationName = b.StorageLocationId != null ? locations.GetValueOrDefault(b.StorageLocationId.Value)?.Name : null,
                    IngredientId = b.IngredientId,
                    IngredientCode = ingredient?.Code,
                    IngredientName = ingredient?.Name,
                    MaterialLotId = b.MaterialLotId,
                    LotNumber = lot?.LotNumber ?? lot?.LotNo,
                    ExpiryDate = lot?.ExpiryDate,
                    ReceivedDate = lot?.ReceivedDate ?? (lot != null ? (DateTime?)lot.CreatedDate : null),
                    QcStatus = lot?.QcStatus ?? lot?.QualityStatus,
                    LotStatus = lot?.Status,
                    LotStatusName = MaterialLotStatus.Label(lot?.Status),
                    UnitCost = lot?.UnitCost,
                    UnitId = b.UnitId,
                    UnitName = units.GetValueOrDefault(b.UnitId)?.Name,
                    OnHandQuantity = b.OnHandQuantity,
                    ReservedQuantity = b.ReservedQuantity,
                    AvailableQuantity = b.AvailableQuantity,
                    DaysToExpiry = expiry != null ? (expiry.Value - today).Days : null,
                    IsExpired = expired,
                    IsExpiring = expiry != null && !expired && expiry <= today.AddDays(warnDays),
                    BlockReason = BlockReason(lot, today) ?? (b.MaterialLotId != null && lot == null ? "Không tìm thấy lô" : null),
                    UseFefo = policy?.UseFefo ?? true,
                });
            }

            // Thứ tự xuất theo lô trong từng (kho, nguyên liệu): các dòng cùng lô (khác vị trí) cùng thứ tự.
            foreach (var group in rows.Where(r => r.BlockReason == null).GroupBy(r => (r.WarehouseId, r.IngredientId)))
            {
                bool useFefo = group.First().UseFefo;
                int rank = 0;
                object? previous = null;
                foreach (var row in OrderForPick(group, useFefo))
                {
                    object key = (object?)row.MaterialLotId ?? "no-lot";
                    if (!Equals(key, previous))
                    {
                        rank++;
                        previous = key;
                    }
                    row.PickRank = rank;
                }
            }
            return [.. rows
                .OrderBy(r => r.WarehouseName).ThenBy(r => r.IngredientName)
                .ThenBy(r => r.PickRank ?? int.MaxValue)
                .ThenBy(r => r.ExpiryDate ?? DateTime.MaxValue)];
        }

        /// <summary>
        /// Gợi ý lô xuất: lấy lô xuất được (không bị chặn, khả dụng &gt; 0) theo thứ tự FEFO / FIFO cho tới đủ số lượng.
        /// <paramref name="allocated"/>: số đã phân bổ trước đó trong cùng phiếu theo lô (Guid.Empty = tồn không gắn lô), trừ khỏi khả dụng.
        /// </summary>
        public static LotSuggestionModel Suggest(IReadOnlyCollection<InventoryLotBalanceModel> rows, Guid warehouseId, Guid ingredientId, decimal quantity,
            bool useFefo, IReadOnlyDictionary<Guid, decimal>? allocated = null)
        {
            var mine = rows.Where(r => r.WarehouseId == warehouseId && r.IngredientId == ingredientId).ToList();
            var result = new LotSuggestionModel
            {
                WarehouseId = warehouseId,
                IngredientId = ingredientId,
                IngredientName = mine.FirstOrDefault()?.IngredientName,
                RequestedQuantity = quantity,
                UseFefo = useFefo,
                ListBlocked = [.. mine.Where(r => r.BlockReason != null && r.OnHandQuantity > 0)],
            };
            decimal remaining = quantity;
            foreach (var lot in LotsInPickOrder(mine, useFefo))
            {
                decimal available = lot.Available - (allocated != null && allocated.TryGetValue(lot.Row.MaterialLotId ?? Guid.Empty, out var used) ? used : 0);
                if (remaining <= 0)
                {
                    break;
                }
                if (available <= 0)
                {
                    continue;
                }
                decimal take = Math.Min(available, remaining);
                result.ListPick.Add(new LotPickModel
                {
                    MaterialLotId = lot.Row.MaterialLotId,
                    LotNumber = lot.Row.LotNumber,
                    ExpiryDate = lot.Row.ExpiryDate,
                    ReceivedDate = lot.Row.ReceivedDate,
                    UnitId = lot.Row.UnitId,
                    AvailableQuantity = available,
                    Quantity = take,
                    PickRank = lot.Row.PickRank ?? 0,
                });
                remaining -= take;
            }
            result.PickedQuantity = quantity - Math.Max(remaining, 0);
            result.ShortQuantity = Math.Max(remaining, 0);
            return result;
        }

        /// <summary>Lô xuất được của 1 (kho, nguyên liệu) theo thứ tự xuất, cộng khả dụng các vị trí.</summary>
        public static List<(InventoryLotBalanceModel Row, decimal Available)> LotsInPickOrder(IEnumerable<InventoryLotBalanceModel> rows, bool useFefo)
            => [.. OrderForPick(rows.Where(r => r.BlockReason == null), useFefo)
                .GroupBy(r => r.MaterialLotId)
                .Select(g => (Row: g.First(), Available: g.Sum(x => x.AvailableQuantity)))];

        // ---------------- Kiểm tra dữ liệu đầu vào dùng chung (trả thông báo lỗi tiếng Việt, null = hợp lệ) ----------------

        /// <summary>Kho tồn tại; vị trí (nếu có) thuộc kho.</summary>
        public static async Task<string?> CheckPlaceAsync(DatabaseContext db, Guid warehouseId, Guid? storageLocationId)
        {
            if (!await db.Warehouse.AsNoTracking().AnyAsync(w => w.Id == warehouseId && !w.IsDeleted))
            {
                return ConstantHelper.Message.WarehouseNotFound;
            }
            if (storageLocationId != null
                && !await db.StorageLocation.AsNoTracking().AnyAsync(s => s.Id == storageLocationId && !s.IsDeleted && s.WarehouseId == warehouseId))
            {
                return ConstantHelper.Message.StorageLocationNotInWarehouse;
            }
            return null;
        }

        /// <summary>Nguyên liệu tồn tại; lô (nếu có) tồn tại và thuộc nguyên liệu.</summary>
        public static async Task<(Ingredient? Ingredient, MaterialLot? Lot, string? Error)> CheckIngredientLotAsync(DatabaseContext db, Guid ingredientId, Guid? lotId)
        {
            var ingredient = await db.Ingredient.AsNoTracking().FirstOrDefaultAsync(i => i.Id == ingredientId && !i.IsDeleted);
            if (ingredient == null)
            {
                return (null, null, ConstantHelper.Message.IngredientNotFound);
            }
            if (lotId == null)
            {
                return (ingredient, null, null);
            }
            var lot = await db.MaterialLot.AsNoTracking().FirstOrDefaultAsync(l => l.Id == lotId && !l.IsDeleted);
            if (lot == null)
            {
                return (ingredient, null, ConstantHelper.Message.LotNotFound);
            }
            if (lot.IngredientId != ingredientId)
            {
                return (ingredient, lot, string.Format(ConstantHelper.Message.LotIngredientMismatch, lot.LotNumber ?? lot.LotNo));
            }
            return (ingredient, lot, null);
        }

        /// <summary>
        /// Đơn vị ghi sổ: đơn vị của dòng tồn đang có cho (kho, nguyên liệu, lô) – ưu tiên đúng vị trí; chưa có tồn thì đơn vị yêu cầu,
        /// rồi đơn vị cơ sở của nguyên liệu (BaseUnitId, cột cũ BaseUomId). Yêu cầu khác đơn vị tồn → lỗi (Pilot chưa quy đổi).
        /// </summary>
        public static async Task<(Guid UnitId, string? Error)> ResolveUnitAsync(DatabaseContext db, Guid warehouseId, Guid? storageLocationId,
            Ingredient ingredient, Guid? lotId, Guid? requestedUnitId)
        {
            var units = await db.InventoryBalance.AsNoTracking()
                .Where(b => !b.IsDeleted && b.WarehouseId == warehouseId && b.IngredientId == ingredient.Id && b.MaterialLotId == lotId)
                .Select(b => new { b.StorageLocationId, b.UnitId })
                .ToListAsync();
            Guid? existing = units.FirstOrDefault(u => u.StorageLocationId == storageLocationId)?.UnitId ?? units.FirstOrDefault()?.UnitId;
            if (existing != null)
            {
                if (requestedUnitId != null && requestedUnitId != existing)
                {
                    string? name = await db.UnitMeasure.AsNoTracking().Where(u => u.Id == existing).Select(u => u.Name).FirstOrDefaultAsync();
                    return (Guid.Empty, string.Format(ConstantHelper.Message.UnitMismatch, name ?? existing.ToString()));
                }
                return (existing.Value, null);
            }
            Guid? unit = requestedUnitId ?? ingredient.BaseUnitId ?? ingredient.BaseUomId;
            return unit == null || unit == Guid.Empty ? (Guid.Empty, ConstantHelper.Message.UnitRequired) : (unit.Value, null);
        }

        /// <summary>Phân bổ lượng xuất trên các dòng tồn (vị trí) của 1 (kho, nguyên liệu, lô): vị trí có nhiều khả dụng trước.</summary>
        public static List<(Guid? StorageLocationId, Guid UnitId, decimal Quantity)> AllocateOverLocations(IEnumerable<InventoryBalance> balances, decimal quantity)
        {
            var result = new List<(Guid?, Guid, decimal)>();
            decimal remaining = quantity;
            foreach (var b in balances.Where(b => b.AvailableQuantity > 0).OrderByDescending(b => b.AvailableQuantity))
            {
                if (remaining <= 0)
                {
                    break;
                }
                decimal take = Math.Min(b.AvailableQuantity, remaining);
                result.Add((b.StorageLocationId, b.UnitId, take));
                remaining -= take;
            }
            return remaining > 0 ? [] : result;
        }
    }
}
