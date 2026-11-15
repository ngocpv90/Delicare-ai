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
