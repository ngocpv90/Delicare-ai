using AppDelicare.Data.Connection;
using AppDelicare.Model.Entity;
using AppDelicare.Services.Implement.Messaging;
using AppDelicare.Services.Interface;

namespace AppDelicare.Services.Implement.Retail
{
    /// <summary>
    /// RETAIL-1: báo đơn khách lẻ mới qua bot (mẫu RETAIL_ORDER_NEW) cho người có quyền Thêm ở màn "Đơn khách lẻ".
    /// Gọi TRONG transaction (tin chỉ tồn tại khi đơn đã lưu); lỗi xếp tin không làm hỏng việc nhận đơn (BusinessNotifier nuốt lỗi).
    /// </summary>
    public static class RetailNotify
    {
        public const string Screen = "sal-retail-order";

        public static Task<int> NewOrderAsync(INotificationService notifications, DatabaseContext db, RetailOrder o, IEnumerable<RetailRules.PricedLine> lines)
        {
            var data = new
            {
                title = $"{o.OrderNumber} · {o.CustomerName} · {o.PhoneNumber}",
                detail = $"Nhận bữa {o.MealType?.ToLowerInvariant()} ngày {o.DeliveryDate:dd/MM/yyyy} · tổng {RetailRules.Money(o.TotalAmount)}",
                lines = lines.Select(l => $"• {l.Product.Name} × {l.Quantity:0.##} {l.Product.Unit}")
                    .Append($"📍 {o.Address}")
                    .Concat(string.IsNullOrWhiteSpace(o.Note) ? [] : [$"📝 {o.Note}"])
                    .Append("Gọi khách xác nhận tại màn Bán lẻ › Đơn khách lẻ.")
                    .ToArray(),
            };
            return BusinessNotifier.ToMenusAsync(notifications, db, NotificationCatalog.RetailOrderNew, [Screen], data,
                idempotencyKey: $"{NotificationCatalog.RetailOrderNew}:{o.Id:N}", requireAdd: true);
        }
    }
}
