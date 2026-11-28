using AppDelicare.Data.Interface;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Implement.PurchasePlan
{
    /// <summary>
    /// UI-6.51 (D-137 d): gửi đơn mua cho NCC theo <c>IntegrationChannel</c> hiện có – mã kênh <c>SUPPLIER_EMAIL</c> (email NCC) / <c>SUPPLIER_ZALO</c> (SĐT / Zalo NCC), trạng thái Active.
    /// <list type="bullet">
    /// <item>Chưa có kênh Active / NCC không có địa chỉ của kênh → NotConfigured: đơn giữ "Chờ gửi NCC", bot báo Mua hàng gửi tay.</item>
    /// <item>Nhà cung cấp kênh <c>LOG</c> (môi trường thử / UAT): chỉ ghi IntegrationLog, coi như đã gửi.</item>
    /// <item>Kênh thật (SMTP / Zalo OA) chưa có bộ gửi trong hệ thống → lỗi "chưa nâng cấp" (giữ chờ gửi tay + bot) – GAP-PUR-SEND.</item>
    /// </list>
    /// Mỗi lần gửi ghi IntegrationLog (OUT, PO_SEND_SUPPLIER).
    /// </summary>
    public sealed class SupplierOrderSendService(IUnitOfWork unitOfWork, IClock clock) : ISupplierOrderSendService
    {
        public const string EmailChannel = "SUPPLIER_EMAIL";
        public const string ZaloChannel = "SUPPLIER_ZALO";

        public async Task<SupplierSendResult> SendAsync(Guid purchaseOrderId)
        {
            var db = unitOfWork.DbContext;
            var po = await db.PurchaseOrder.AsNoTracking().FirstOrDefaultAsync(p => p.Id == purchaseOrderId && !p.IsDeleted);
            if (po == null)
            {
                return new(false, false, null, "Không tìm thấy đơn mua");
            }
            var supplier = await db.Supplier.AsNoTracking().FirstOrDefaultAsync(s => s.Id == po.SupplierId);
            var channels = await db.IntegrationChannel.AsNoTracking()
                .Where(c => !c.IsDeleted && (c.ChannelCode == ZaloChannel || c.ChannelCode == EmailChannel) && c.ChannelStatus == "Active").ToListAsync();
            string? zalo = supplier?.ContactPhone ?? supplier?.Phone, email = supplier?.Email;
            var channel = channels.FirstOrDefault(c => c.ChannelCode == ZaloChannel && !string.IsNullOrWhiteSpace(zalo))
                ?? channels.FirstOrDefault(c => c.ChannelCode == EmailChannel && !string.IsNullOrWhiteSpace(email));
            if (channel == null)
            {
                return new(false, true, null, channels.Count == 0 ? "Chưa cấu hình kênh gửi NCC (email / Zalo)" : $"NCC {supplier?.Name} chưa có email / số Zalo");
            }
            string label = channel.ChannelCode == ZaloChannel ? "Zalo" : "Email";
            bool logOnly = string.Equals(channel.ProviderCode, "LOG", StringComparison.OrdinalIgnoreCase);
            string? error = logOnly ? null : $"Kênh {label} ({channel.ProviderCode}) chưa có bộ gửi – gửi tay";
            db.IntegrationLog.Add(new IntegrationLog
            {
                Id = Guid.NewGuid(), IntegrationChannelId = channel.Id, DirectionCode = "OUT", OperationCode = "PO_SEND_SUPPLIER", CorrelationId = po.PurchaseOrderNumber ?? po.PONo,
                IdempotencyKey = "PO_SEND:" + po.Id.ToString("N"), RequestAt = clock.Now, ResponseAt = clock.Now, HttpStatus = error == null ? 200 : 501,
                LogStatus = error == null ? "Succeeded" : "Failed", ErrorCode = error == null ? null : "NOT_IMPLEMENTED", ErrorMessage = error,
                CreatedDate = clock.Now, IsDeleted = false,
            });
            await db.SaveChangesAsync(AppDelicare.Model.Helper.ConstantHelper.TypeSave.Update);
            return error == null ? new(true, false, label, $"Đã gửi {label} cho {supplier?.Name}") : new(false, false, label, error);
        }
    }
}
