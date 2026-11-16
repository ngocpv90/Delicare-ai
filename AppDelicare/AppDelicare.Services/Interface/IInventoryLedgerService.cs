using AppDelicare.Model.Entity;

namespace AppDelicare.Services.Interface
{
    /// <summary>Một biến động kho cần ghi sổ (Spec 7 – <c>InventoryLedgerService.Post(txn)</c>).</summary>
    public sealed class InventoryPostRequest
    {
        /// <summary>RECEIPT, ISSUE, TRANSFER_IN, TRANSFER_OUT, ADJUSTMENT, WASTE, RETURN (BRD).</summary>
        public string TransactionType { get; init; } = string.Empty;
        public string? TransactionNumber { get; init; }
        public Guid WarehouseId { get; init; }
        public Guid? StorageLocationId { get; init; }
        public Guid IngredientId { get; init; }
        public Guid? MaterialLotId { get; init; }
        public Guid UnitId { get; init; }
        /// <summary>Số lượng nhập (≥ 0).</summary>
        public decimal QuantityIn { get; init; }
        /// <summary>Số lượng xuất (≥ 0).</summary>
        public decimal QuantityOut { get; init; }
        public decimal? UnitCost { get; init; }
        public string? ReferenceType { get; init; }
        public Guid? ReferenceId { get; init; }
        public string? Reason { get; init; }
        public string? Note { get; init; }
        public DateTime? TransactionDate { get; init; }
    }

    /// <summary>
    /// Sổ cái kho (T08). Mọi biến động kho đi qua đây: ghi <c>InventoryTransaction</c> (chỉ ghi thêm) + cập nhật <c>InventoryBalance</c>
    /// trong cùng transaction; chặn tồn âm. Sửa sai = bút toán đảo (<see cref="ReverseAsync"/>), không bao giờ Update / Delete sổ.
    /// </summary>
    public interface IInventoryLedgerService
    {
        Task<InventoryTransaction> PostAsync(InventoryPostRequest request, CancellationToken cancellationToken = default);

        /// <summary>Ghi bút toán đảo dòng <paramref name="transactionId"/>: số lượng ngược dấu, <c>ReferenceType = REVERSAL</c>, <c>ReferenceId</c> = dòng gốc.</summary>
        Task<InventoryTransaction> ReverseAsync(Guid transactionId, string reason, CancellationToken cancellationToken = default);
    }
}
