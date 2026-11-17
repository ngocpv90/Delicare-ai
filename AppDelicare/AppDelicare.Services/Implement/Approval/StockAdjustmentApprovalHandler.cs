using AppDelicare.Model.Helper;
using AppDelicare.Services.Interface;

namespace AppDelicare.Services.Implement.Approval
{
    /// <summary>
    /// BE2.3 (spec 2.3 / 7.6 adjust, GAP-F-04) – duyệt điều chỉnh tồn tay STOCK_ADJUSTMENT, 1 cấp "Quản lý kho" (mục <c>#/wf-inbox?type=scm-adjustment</c>).
    /// Không có bảng phiếu điều chỉnh: BusinessId = khóa nối mới (= <c>InventoryTransaction.ReferenceId</c> khi ghi), nội dung điều chỉnh nằm ở đề xuất
    /// (<c>PayloadJson.proposal</c>, không sửa được qua CRUD). Duyệt → ghi bút toán ADJUSTMENT; từ chối / hủy → không ghi gì.
    /// </summary>
    public class StockAdjustmentApprovalHandler(IInventoryTransactionService inventoryTransactionService) : IApprovalHandler
    {
        private readonly IInventoryTransactionService _inventoryTransactionService = inventoryTransactionService;

        public string BusinessType => StockAdjustmentStatus.BusinessType;

        public string ScreenId => StockAdjustmentStatus.ScreenId;

        /// <summary>Không có bản ghi nghiệp vụ để dựng lại: nội dung được duyệt = khóa nối (đề xuất lưu cùng yêu cầu, CRUD không sửa được PayloadJson).</summary>
        public Task<string?> BuildPayloadAsync(Guid businessId) => Task.FromResult<string?>($"{{\"type\":\"{StockAdjustmentStatus.BusinessType}\",\"referenceId\":\"{businessId}\"}}");

        public Task<string?> DescribeAsync(Guid businessId) => _inventoryTransactionService.DescribeAdjustmentAsync(businessId);

        public Task<string?> ApproveAsync(Guid businessId, Guid actorUserId, string? note) => _inventoryTransactionService.ApplyApprovedAdjustmentAsync(businessId, null, note);

        public Task<string?> ApproveAsync(Guid businessId, Guid actorUserId, string? note, string? proposalJson)
            => _inventoryTransactionService.ApplyApprovedAdjustmentAsync(businessId, proposalJson, note);

        public Task<string?> RejectAsync(Guid businessId, Guid actorUserId, string note) => Task.FromResult<string?>(null);
    }
}
