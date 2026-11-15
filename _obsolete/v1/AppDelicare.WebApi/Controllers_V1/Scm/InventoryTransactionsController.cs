using System.ComponentModel.DataAnnotations;
using AppDelicare.Model.Entity;
using AppDelicare.Services.Interface;
using AppDelicare.WebApi.Api;
using AppDelicare.WebApi.Security;
using Microsoft.AspNetCore.Mvc;

namespace AppDelicare.WebApi.Controllers.V1.Scm
{
    /// <summary>Yêu cầu đảo bút toán kho.</summary>
    public sealed class ReverseInventoryRequest
    {
        /// <summary>Lý do đảo (bắt buộc, lưu ở cột Reason của bút toán đảo).</summary>
        /// <example>Nhập nhầm số lượng phiếu PN-2611-031</example>
        [Required(ErrorMessage = "Lý do là bắt buộc."), StringLength(1000, MinimumLength = 3, ErrorMessage = "Lý do 3–1000 ký tự.")]
        public string? Reason { get; init; }
    }

    /// <summary>Bút toán kho trả về.</summary>
    public sealed class InventoryTransactionDto
    {
        public Guid Id { get; init; }
        /// <example>GD-PN-2611-031-NL001-R</example>
        public string? TransactionNumber { get; init; }
        /// <example>ADJUSTMENT</example>
        public string? TransactionType { get; init; }
        public DateTime TransactionDate { get; init; }
        public Guid WarehouseId { get; init; }
        public Guid IngredientId { get; init; }
        public Guid? MaterialLotId { get; init; }
        public Guid UnitId { get; init; }
        /// <summary>Số lượng có dấu (nhập dương, xuất âm).</summary>
        /// <example>-120</example>
        public decimal Quantity { get; init; }
        public decimal QuantityIn { get; init; }
        public decimal QuantityOut { get; init; }
        /// <example>REVERSAL</example>
        public string? ReferenceType { get; init; }
        public Guid? ReferenceId { get; init; }
        public string? Reason { get; init; }

        public static InventoryTransactionDto From(InventoryTransaction x) => new()
        {
            Id = x.Id, TransactionNumber = x.TransactionNumber, TransactionType = x.TransactionType, TransactionDate = x.TransactionDate,
            WarehouseId = x.WarehouseId, IngredientId = x.IngredientId, MaterialLotId = x.MaterialLotId, UnitId = x.UnitId,
            Quantity = x.Quantity, QuantityIn = x.QuantityIn, QuantityOut = x.QuantityOut,
            ReferenceType = x.ReferenceType, ReferenceId = x.ReferenceId, Reason = x.Reason,
        };
    }

    /// <summary>Sổ cái kho – <c>/api/v1/scm/inventory-transactions</c> (Spec 7). Sổ chỉ ghi thêm: sửa sai bằng bút toán đảo.</summary>
    [Route(RoutePrefix + "/scm/inventory-transactions")]
    public sealed class InventoryTransactionsController(IInventoryLedgerService ledger) : ApiControllerBase
    {
        public const string Screen = "scm-inv-txn";

        /// <summary>Đảo một bút toán kho: ghi bút toán ngược dấu tham chiếu dòng gốc, tồn kho trở lại như trước.</summary>
        /// <response code="201">Bút toán đảo vừa ghi.</response>
        /// <response code="404">NOT_FOUND.</response>
        /// <response code="409">INVALID_TRANSITION – đã đảo rồi / đảo một bút toán đảo.</response>
        /// <response code="422">INSUFFICIENT_STOCK – đảo phiếu nhập khi hàng đã xuất bớt.</response>
        [HttpPost("{id:guid}/reverse")]
        [Idempotent]
        [Permission(Screen, PermissionAction.Edit)]
        [ProducesResponseType(typeof(ApiEnvelope<InventoryTransactionDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiEnvelope<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiEnvelope<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiEnvelope<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> Reverse(Guid id, [FromBody] ReverseInventoryRequest request, CancellationToken cancellationToken)
        {
            var reversal = await ledger.ReverseAsync(id, request.Reason!, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, InventoryTransactionDto.From(reversal));
        }
    }
}
