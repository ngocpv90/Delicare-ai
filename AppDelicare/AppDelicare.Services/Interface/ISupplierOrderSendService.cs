namespace AppDelicare.Services.Interface
{
    /// <summary>UI-6.51 (D-137 d): gửi đơn mua đã duyệt cho NCC qua kênh tích hợp (email / Zalo).</summary>
    public interface ISupplierOrderSendService
    {
        /// <summary>Gửi phiếu đặt hàng (PDF có QR – UI-6.50) của PO. NotConfigured = chưa cấu hình kênh / NCC chưa có địa chỉ → chỉ đánh dấu chờ gửi tay.</summary>
        Task<SupplierSendResult> SendAsync(Guid purchaseOrderId);
    }

    public sealed record SupplierSendResult(bool Ok, bool NotConfigured, string? Channel, string? Message);
}
