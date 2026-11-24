using AppDelicare.Data.Connection;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Option;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// UI-6.24 (D-105): tự Đóng lệnh SX ngay khi lệnh chuyển Hoàn thành (mẻ cuối đóng gói / loại – <see cref="ProductionRules.CompleteOrderIfDoneAsync"/>).
    /// Gọi SAU KHI thao tác mẻ / đóng gói đã commit, dùng đúng <c>ProductionOrder/Close</c> (chênh lệch tiêu hao, giá thành thực tế, giải phóng + phân bổ lại giữ chỗ dư).
    /// Lỗi khi tự đóng KHÔNG làm hỏng thao tác mẻ: ghi log, trả câu cảnh báo, lệnh giữ Hoàn thành (job cuối ngày đóng lại). Cờ <c>ProductionAutoCloseOrder</c>.
    /// </summary>
    public static class ProductionAutoClose
    {
        public const string Note = "Hệ thống tự đóng khi mẻ cuối xong";

        /// <summary>Lệnh của mẻ đã Hoàn thành → tự đóng. Trả câu báo (đã đóng / lỗi) hoặc null khi không có việc.</summary>
        public static async Task<string?> AfterBatchAsync(IServiceProvider services, BusinessRulesOptions rules, DatabaseContext db, Guid batchId)
        {
            if (!rules.ProductionAutoCloseOrder)
            {
                return null;
            }
            var order = await (from b in db.ProductionBatch.AsNoTracking()
                               join o in db.ProductionOrder.AsNoTracking() on b.ProductionOrderId equals o.Id
                               where b.Id == batchId && !o.IsDeleted
                               select new { o.Id, o.Status, Number = o.ProductionOrderNumber ?? o.ProductionOrderNo }).FirstOrDefaultAsync();
            if (order == null || order.Status != ProductionOrderStatus.Completed)
            {
                return null;
            }
            return await CloseAsync(services, order.Id, order.Number, Note);
        }

        public static async Task<string?> CloseAsync(IServiceProvider services, Guid orderId, string? number, string note)
        {
            try
            {
                var result = await services.GetRequiredService<IProductionOrderService>().Close(new ProductionActionRequest { Id = orderId, Note = note });
                return result.ResponseCode == 200
                    ? string.Format(ConstantHelper.Message.ProductionOrderAutoClosed, number)
                    : string.Format(ConstantHelper.Message.ProductionOrderAutoCloseFailed, number, result.ResponseMessage);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[ProductionAutoClose] tự đóng lệnh {Order}", orderId);
                return string.Format(ConstantHelper.Message.ProductionOrderAutoCloseFailed, number, ConstantHelper.Message.InternalServer);
            }
        }
    }
}
