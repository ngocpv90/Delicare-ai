namespace AppDelicare.Tests.Integration
{
    /// <summary>BE2.3: bước duyệt dùng chung trong test (đề nghị mua 2 cấp D-46, điều chỉnh tồn STOCK_ADJUSTMENT).</summary>
    public static class ApprovalTestHelpers
    {
        /// <summary>
        /// Gửi duyệt đề nghị mua rồi duyệt đủ 2 cấp (scm-pr-l1 → scm-pr-l2) bằng client admin (admin có mọi quyền, được miễn luật "mỗi cấp 1 người").
        /// </summary>
        public static async Task ApprovePurchaseRequestAsync(this HttpClient admin, Guid purchaseRequestId)
        {
            (await admin.PostFormAsync("/api/PurchaseRequest/Submit", ("Id", purchaseRequestId), ("Note", "[DEMO] gửi duyệt"))).Ok();
            (await admin.PostFormAsync("/api/PurchaseRequest/Approve", ("Id", purchaseRequestId), ("Note", "[DEMO] cấp 1"))).Ok();
            (await admin.PostFormAsync("/api/PurchaseRequest/Approve", ("Id", purchaseRequestId), ("Note", "[DEMO] cấp 2"))).Ok();
        }

        /// <summary>
        /// Điều chỉnh tồn tay rồi duyệt yêu cầu STOCK_ADJUSTMENT (1 cấp, admin) – trả bút toán đã ghi (data.transaction của Adjust sau duyệt = đọc lại theo ReferenceId).
        /// </summary>
        public static async Task<Guid> AdjustAndApproveAsync(this HttpClient admin, params (string Key, object? Value)[] fields)
        {
            var adjust = (await admin.PostFormAsync("/api/InventoryTransaction/Adjust", fields)).Ok();
            Guid approvalId = adjust.GetProperty("approvalRequestId").GetGuid();
            (await admin.PostFormAsync("/api/ApprovalRequest/Decide", ("Id", approvalId), ("Approve", true), ("Note", "[DEMO] duyệt điều chỉnh"))).Ok();
            return adjust.GetProperty("referenceId").GetGuid();
        }
    }
}
