using AppDelicare.Data.Connection;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Model;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// UI-6 (D-80) nhóm B – đọc dùng chung cho trang chi tiết Lô hàng (MaterialLot/GetOverview) và Nguyên liệu (Ingredient/GetOverview):
    /// tồn theo kho / lô từ InventoryBalance, giao dịch kho từ sổ InventoryTransaction (cột mới MaterialLotId / TransactionNumber, cột cũ LotId / TransactionNo dự phòng).
    /// </summary>
    public static class ScmOverviewRules
    {
        public static async Task<List<ScmBalanceModel>> BalancesAsync(DatabaseContext db, IQueryable<InventoryBalance> query)
        {
            var rows = await query.Where(b => !b.IsDeleted)
                .Select(b => new { b.WarehouseId, LotId = b.MaterialLotId ?? b.LotId, b.OnHandQuantity, b.ReservedQuantity, b.AvailableQuantity }).ToListAsync();
            var whIds = rows.Select(r => r.WarehouseId).Distinct().ToList();
            var lotIds = rows.Where(r => r.LotId != null).Select(r => r.LotId!.Value).Distinct().ToList();
            var warehouses = await db.Warehouse.AsNoTracking().Where(w => whIds.Contains(w.Id)).ToDictionaryAsync(w => w.Id, w => w.Name);
            var lots = await db.MaterialLot.AsNoTracking().Where(l => lotIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.LotNumber ?? l.LotNo);
            return rows.GroupBy(r => r.WarehouseId).Select(g => new ScmBalanceModel
            {
                WarehouseId = g.Key, WarehouseName = warehouses.GetValueOrDefault(g.Key),
                OnHandQuantity = g.Sum(r => r.OnHandQuantity), ReservedQuantity = g.Sum(r => r.ReservedQuantity), AvailableQuantity = g.Sum(r => r.AvailableQuantity),
                MaterialLotId = g.Select(r => r.LotId).Distinct().Count() == 1 ? g.First().LotId : null,
                LotNumber = g.Select(r => r.LotId).Distinct().Count() == 1 && g.First().LotId is { } lid ? lots.GetValueOrDefault(lid) : null,
            }).OrderBy(b => b.WarehouseName, StringComparer.Ordinal).ToList();
        }

        public static async Task<List<ScmTransactionModel>> TransactionsAsync(DatabaseContext db, IQueryable<InventoryTransaction> query, int take)
        {
            var rows = await query.Where(t => !t.IsDeleted).OrderByDescending(t => t.TransactionDate).ThenByDescending(t => t.CreatedDate).Take(take).ToListAsync();
            var whIds = rows.Select(r => r.WarehouseId).Distinct().ToList();
            var lotIds = rows.Select(r => r.MaterialLotId ?? r.LotId).Where(x => x != null).Select(x => x!.Value).Distinct().ToList();
            var warehouses = await db.Warehouse.AsNoTracking().Where(w => whIds.Contains(w.Id)).ToDictionaryAsync(w => w.Id, w => w.Name);
            var lots = await db.MaterialLot.AsNoTracking().Where(l => lotIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.LotNumber ?? l.LotNo);
            return rows.Select(t =>
            {
                Guid? lotId = t.MaterialLotId ?? t.LotId;
                return new ScmTransactionModel
                {
                    Id = t.Id, TransactionNumber = t.TransactionNumber ?? t.TransactionNo, TransactionType = t.TransactionType, TransactionDate = t.TransactionDate,
                    WarehouseName = warehouses.GetValueOrDefault(t.WarehouseId), MaterialLotId = lotId, LotNumber = lotId is { } l ? lots.GetValueOrDefault(l) : null,
                    QuantityIn = t.QuantityIn, QuantityOut = t.QuantityOut, UnitCost = t.UnitCost, ReferenceType = t.ReferenceType, ReferenceId = t.ReferenceId, Reason = t.Reason ?? t.Note,
                };
            }).ToList();
        }
    }
}
