using AppDelicare.Data.Connection;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// T08 – sổ cái kho (Spec 7: "InventoryBalance được tính từ sổ cái InventoryTransaction, không có API sửa trực tiếp tồn").
    /// Ghi song song cột deprecated NOT NULL (DEPRECATED_COLUMNS: UomId ↔ UnitId, LotId ↔ MaterialLotId, TransactionNo ↔ TransactionNumber,
    /// OnHandQty / ReservedQty / AvailableQty ↔ *Quantity).
    /// </summary>
    public sealed class InventoryLedgerService(DatabaseContext db) : IInventoryLedgerService
    {
        public const string ReversalReferenceType = "REVERSAL";
        public const string AdjustmentType = "ADJUSTMENT";

        public static readonly IReadOnlySet<string> TransactionTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "RECEIPT", "ISSUE", "TRANSFER_IN", "TRANSFER_OUT", AdjustmentType, "WASTE", "RETURN",
        };

        public async Task<InventoryTransaction> PostAsync(InventoryPostRequest request, CancellationToken cancellationToken = default)
        {
            Validate(request);
            // Đã có transaction ngoài (vd. [Idempotent]) thì dùng chung, không mở lồng.
            await using var transaction = db.Database.IsRelational() && db.Database.CurrentTransaction == null ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
            var entity = await PostCoreAsync(request, cancellationToken);
            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
            return entity;
        }

        public async Task<InventoryTransaction> ReverseAsync(Guid transactionId, string reason, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new BusinessRuleException(400, "VALIDATION_FAILED", "Lý do đảo bút toán là bắt buộc.", "reason");
            }
            // Đã có transaction ngoài (vd. [Idempotent]) thì dùng chung, không mở lồng.
            await using var transaction = db.Database.IsRelational() && db.Database.CurrentTransaction == null ? await db.Database.BeginTransactionAsync(cancellationToken) : null;

            var original = await db.InventoryTransaction.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == transactionId && !x.IsDeleted, cancellationToken)
                ?? throw new BusinessRuleException(404, "NOT_FOUND", "Không tìm thấy bút toán kho.");
            if (original.ReferenceType == ReversalReferenceType)
            {
                throw new BusinessRuleException(409, "INVALID_TRANSITION", "Không đảo một bút toán đảo. Hãy ghi bút toán mới.");
            }
            bool alreadyReversed = await db.InventoryTransaction.AsNoTracking()
                .AnyAsync(x => !x.IsDeleted && x.ReferenceType == ReversalReferenceType && x.ReferenceId == original.Id, cancellationToken);
            if (alreadyReversed)
            {
                throw new BusinessRuleException(409, "INVALID_TRANSITION", "Bút toán này đã được đảo trước đó.");
            }

            var reversal = await PostCoreAsync(new InventoryPostRequest
            {
                // PROPOSED – BA/PO CONFIRM: bút toán đảo dùng loại ADJUSTMENT + ReferenceType REVERSAL; số chứng từ = số gốc + "-R" (mã chứng từ chuẩn ở T11).
                TransactionType = AdjustmentType,
                TransactionNumber = (original.TransactionNumber ?? original.TransactionNo ?? original.Id.ToString("N")[..8]) + "-R",
                WarehouseId = original.WarehouseId,
                StorageLocationId = original.StorageLocationId,
                IngredientId = original.IngredientId,
                MaterialLotId = original.MaterialLotId ?? original.LotId,
                UnitId = original.UnitId,
                QuantityIn = original.QuantityOut,
                QuantityOut = original.QuantityIn,
                UnitCost = original.UnitCost,
                ReferenceType = ReversalReferenceType,
                ReferenceId = original.Id,
                Reason = reason.Trim(),
                Note = $"Đảo bút toán {original.TransactionNumber ?? original.TransactionNo} ({original.TransactionType})",
            }, cancellationToken);

            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
            return reversal;
        }

        /// <summary>
        /// T16 (spec 7.6 rebuild-balance): tính lại OnHand của InventoryBalance từ sổ cái theo khóa (kho, vị trí, nguyên liệu, lô).
        /// Giữ ReservedQuantity; Available = OnHand − Reserved; dòng tồn không còn bút toán → 0. Ghi song song cột deprecated.
        /// </summary>
        public async Task<(int Rows, int Changed)> RebuildBalancesAsync(Guid? warehouseId, CancellationToken cancellationToken = default)
        {
            await using var transaction = db.Database.IsRelational() && db.Database.CurrentTransaction == null ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
            DateTime now = DateTime.Now;
            var sums = (await db.InventoryTransaction.AsNoTracking()
                    .Where(t => !t.IsDeleted && (warehouseId == null || t.WarehouseId == warehouseId))
                    .Select(t => new { t.WarehouseId, t.StorageLocationId, t.IngredientId, t.MaterialLotId, t.UnitId, t.QuantityIn, t.QuantityOut, t.CreatedDate })
                    .ToListAsync(cancellationToken))
                .GroupBy(t => (t.WarehouseId, t.StorageLocationId, t.IngredientId, t.MaterialLotId))
                .ToDictionary(g => g.Key, g => (OnHand: g.Sum(x => x.QuantityIn - x.QuantityOut), UnitId: g.OrderByDescending(x => x.CreatedDate).First().UnitId));
            var balances = await db.InventoryBalance
                .Where(b => !b.IsDeleted && (warehouseId == null || b.WarehouseId == warehouseId))
                .ToListAsync(cancellationToken);
            int changed = 0;
            var seen = new HashSet<(Guid, Guid?, Guid, Guid?)>();
            foreach (var balance in balances)
            {
                var key = (balance.WarehouseId, balance.StorageLocationId, balance.IngredientId, balance.MaterialLotId);
                decimal onHand = sums.TryGetValue(key, out var sum) ? sum.OnHand : 0;
                seen.Add(key);
                if (balance.OnHandQuantity != onHand || balance.OnHandQty != onHand || balance.AvailableQuantity != onHand - balance.ReservedQuantity)
                {
                    changed++;
                    balance.OnHandQuantity = balance.OnHandQty = onHand;
                    balance.ReservedQty = balance.ReservedQuantity;
                    balance.AvailableQuantity = balance.AvailableQty = onHand - balance.ReservedQuantity;
                    balance.LastCalculatedDate = now;
                    balance.UpdatedDate = now;
                }
            }
            foreach (var (key, sum) in sums.Where(s => !seen.Contains(s.Key)))
            {
                changed++;
                db.InventoryBalance.Add(new InventoryBalance
                {
                    Id = Guid.NewGuid(),
                    WarehouseId = key.WarehouseId,
                    StorageLocationId = key.StorageLocationId,
                    IngredientId = key.IngredientId,
                    MaterialLotId = key.MaterialLotId,
                    LotId = key.MaterialLotId,
                    UnitId = sum.UnitId,
                    OnHandQuantity = sum.OnHand,
                    OnHandQty = sum.OnHand,
                    AvailableQuantity = sum.OnHand,
                    AvailableQty = sum.OnHand,
                    LastCalculatedDate = now,
                    UpdatedDate = now,
                });
            }
            if (changed > 0)
            {
                await db.SaveChangesAsync(ConstantHelper.TypeSave.Update, cancellationToken);
            }
            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
            return (balances.Count + sums.Keys.Count(k => !seen.Contains(k)), changed);
        }

        private static void Validate(InventoryPostRequest r)
        {
            if (!TransactionTypes.Contains(r.TransactionType))
            {
                throw new BusinessRuleException(400, "VALIDATION_FAILED", $"Loại giao dịch kho không hợp lệ: {r.TransactionType}.", "transactionType");
            }
            if (r.QuantityIn < 0 || r.QuantityOut < 0 || (r.QuantityIn == 0 && r.QuantityOut == 0) || (r.QuantityIn > 0 && r.QuantityOut > 0))
            {
                throw new BusinessRuleException(400, "VALIDATION_FAILED", "Mỗi bút toán chỉ có số lượng nhập HOẶC xuất, lớn hơn 0.", "quantity");
            }
            if (r.WarehouseId == Guid.Empty || r.IngredientId == Guid.Empty || r.UnitId == Guid.Empty)
            {
                throw new BusinessRuleException(400, "VALIDATION_FAILED", "Thiếu kho / nguyên liệu / đơn vị tính.");
            }
        }

        private async Task<InventoryTransaction> PostCoreAsync(InventoryPostRequest r, CancellationToken cancellationToken)
        {
            DateTime now = DateTime.Now;
            decimal delta = r.QuantityIn - r.QuantityOut;

            var balance = await db.InventoryBalance.FirstOrDefaultAsync(b => !b.IsDeleted
                && b.WarehouseId == r.WarehouseId && b.StorageLocationId == r.StorageLocationId
                && b.IngredientId == r.IngredientId && b.MaterialLotId == r.MaterialLotId, cancellationToken);
            if (balance != null && balance.UnitId != r.UnitId)
            {
                // T16: 1 dòng tồn = 1 đơn vị; không quy đổi ngầm (PROPOSED – quy đổi qua UomConversion để sau Pilot).
                throw new BusinessRuleException(422, "UNIT_MISMATCH", "Đơn vị tính khác đơn vị của dòng tồn hiện có – không cộng / trừ được.", "unitId");
            }
            decimal onHand = balance?.OnHandQuantity ?? 0;
            decimal reserved = balance?.ReservedQuantity ?? 0;
            if (onHand + delta < 0)
            {
                throw new BusinessRuleException(422, "INSUFFICIENT_STOCK",
                    $"Không đủ tồn: hiện có {onHand:0.####}, cần xuất {-delta:0.####}.", "quantityOut");
            }

            var entity = new InventoryTransaction
            {
                Id = Guid.NewGuid(),
                TransactionNumber = r.TransactionNumber,
                TransactionNo = r.TransactionNumber,
                TransactionType = r.TransactionType,
                TransactionDate = r.TransactionDate ?? now,
                WarehouseId = r.WarehouseId,
                StorageLocationId = r.StorageLocationId,
                IngredientId = r.IngredientId,
                MaterialLotId = r.MaterialLotId,
                LotId = r.MaterialLotId,
                UnitId = r.UnitId,
                UomId = r.UnitId,
                QuantityIn = r.QuantityIn,
                QuantityOut = r.QuantityOut,
                Quantity = delta,
                UnitCost = r.UnitCost,
                ReferenceType = r.ReferenceType,
                ReferenceId = r.ReferenceId,
                Reason = r.Reason,
                Note = r.Note,
            };
            db.InventoryTransaction.Add(entity);

            if (balance == null)
            {
                balance = new InventoryBalance
                {
                    Id = Guid.NewGuid(),
                    WarehouseId = r.WarehouseId,
                    StorageLocationId = r.StorageLocationId,
                    IngredientId = r.IngredientId,
                    MaterialLotId = r.MaterialLotId,
                    LotId = r.MaterialLotId,
                    UnitId = r.UnitId,
                };
                db.InventoryBalance.Add(balance);
            }
            balance.OnHandQuantity = balance.OnHandQty = onHand + delta;
            balance.ReservedQuantity = balance.ReservedQty = reserved;
            balance.AvailableQuantity = balance.AvailableQty = onHand + delta - reserved;
            balance.LastCalculatedDate = now;
            balance.UpdatedDate = now;

            await db.SaveChangesAsync(balance.CreatedDate == default ? ConstantHelper.TypeSave.Insert : ConstantHelper.TypeSave.Update, cancellationToken);
            return entity;
        }
    }
}
