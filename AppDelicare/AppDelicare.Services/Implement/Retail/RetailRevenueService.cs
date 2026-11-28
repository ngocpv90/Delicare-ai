using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Data.Interface;
using AppDelicare.Model.Helper;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Implement.Retail
{
    public sealed class RetailRevenueRowModel
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public int Orders { get; set; }
        public decimal Quantity { get; set; }
        /// <summary>Doanh thu đơn đã giao xong (Hoàn tất).</summary>
        public decimal Revenue { get; set; }
        /// <summary>Đơn đang xử lý (Mới / Đã xác nhận / Đang giao) – chưa tính doanh thu.</summary>
        public decimal Pipeline { get; set; }
    }

    public sealed class RetailRevenueModel
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public int Orders { get; set; }
        public int Completed { get; set; }
        public int Cancelled { get; set; }
        public int InProgress { get; set; }
        public decimal Revenue { get; set; }
        public decimal Pipeline { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal? AvgOrder { get; set; }
        public decimal? CancelRatePct { get; set; }
        public List<RetailRevenueRowModel> ListDay { get; set; } = [];
        public List<RetailRevenueRowModel> ListProduct { get; set; } = [];
        public List<RetailRevenueRowModel> ListMeal { get; set; } = [];
        public List<RetailRevenueRowModel> ListChannel { get; set; } = [];
        public List<RetailRevenueRowModel> ListPayment { get; set; } = [];
    }

    /// <summary>
    /// RETAIL-1 §8 – báo cáo doanh thu bán lẻ (tab "Doanh thu" của màn Đơn khách lẻ, quyền Xem sal-retail-order): theo ngày nhận, món, bữa, kênh, thanh toán.
    /// Doanh thu = đơn Hoàn tất (tổng tiền gồm phí giao); đơn Mới / Đã xác nhận / Đang giao = "đang xử lý"; đơn Hủy chỉ đếm. Tối đa 92 ngày. Chỉ đọc.
    /// </summary>
    public interface IRetailRevenueService
    {
        Task<IResult<RetailRevenueModel>> GetRevenue(DateTime? from, DateTime? to);
    }

    public sealed class RetailRevenueService(IUnitOfWork unitOfWork, IClock clock) : IRetailRevenueService
    {
        private sealed record O(Guid Id, DateTime DeliveryDate, string? OrderStatus, decimal TotalAmount, decimal ShippingFee, string? MealType, string? Channel, string? PaymentMethod);

        public async Task<IResult<RetailRevenueModel>> GetRevenue(DateTime? from, DateTime? to)
        {
            DateTime t = (to ?? clock.Today).Date, f = (from ?? new DateTime(t.Year, t.Month, 1)).Date;
            if (f > t || (t - f).TotalDays > 91)
            {
                return Result<RetailRevenueModel>.Fail("Khoảng ngày không hợp lệ: Từ ngày ≤ Đến ngày, tối đa 92 ngày");
            }
            var db = unitOfWork.DbContext;
            DateTime end = t.AddDays(1);
            var orders = await db.RetailOrder.AsNoTracking().Where(o => !o.IsDeleted && o.DeliveryDate >= f && o.DeliveryDate < end)
                .Select(o => new O(o.Id, o.DeliveryDate, o.OrderStatus, o.TotalAmount, o.ShippingFee, o.MealType, o.Channel, o.PaymentMethod)).ToListAsync();
            var ids = orders.Select(o => o.Id).ToList();
            var lines = ids.Count == 0 ? [] : await db.RetailOrderLine.AsNoTracking().Where(l => !l.IsDeleted && ids.Contains(l.RetailOrderId))
                .Select(l => new { l.RetailOrderId, l.RetailProductId, l.ProductName, l.Quantity, l.LineAmount }).ToListAsync();
            bool Done(string? s) => s == RetailOrderStatus.Completed;
            bool Open(string? s) => s is RetailOrderStatus.New or RetailOrderStatus.Confirmed or RetailOrderStatus.Delivering;
            var status = orders.ToDictionary(o => o.Id, o => o.OrderStatus);
            var live = orders.Where(o => o.OrderStatus != RetailOrderStatus.Cancelled).ToList();
            List<RetailRevenueRowModel> By(Func<O, string> key, Func<string, string> label) =>
                [.. live.GroupBy(o => key(o)).Select(g => new RetailRevenueRowModel
                {
                    Key = g.Key, Label = label(g.Key), Orders = g.Count(),
                    Revenue = g.Where(o => Done(o.OrderStatus)).Sum(o => o.TotalAmount), Pipeline = g.Where(o => Open(o.OrderStatus)).Sum(o => o.TotalAmount),
                }).OrderByDescending(r => r.Revenue + r.Pipeline)];
            var m = new RetailRevenueModel
            {
                From = f, To = t, Orders = orders.Count, Completed = orders.Count(o => Done(o.OrderStatus)), Cancelled = orders.Count(o => o.OrderStatus == RetailOrderStatus.Cancelled),
                InProgress = orders.Count(o => Open(o.OrderStatus)), Revenue = orders.Where(o => Done(o.OrderStatus)).Sum(o => o.TotalAmount),
                Pipeline = orders.Where(o => Open(o.OrderStatus)).Sum(o => o.TotalAmount), ShippingFee = orders.Where(o => Done(o.OrderStatus)).Sum(o => o.ShippingFee),
                ListMeal = By(o => o.MealType ?? "Khác", k => k),
                ListChannel = By(o => o.Channel ?? "WEB", k => k switch { "WEB" => "Trang web", "PHONE" => "Điện thoại", "ZALO" => "Zalo", "STORE" => "Tại quầy", _ => k }),
                ListPayment = By(o => o.PaymentMethod ?? "COD", k => k switch { "COD" => "Tiền mặt khi nhận", "BANK" or "TRANSFER" => "Chuyển khoản", _ => k }),
            };
            m.AvgOrder = m.Completed > 0 ? Math.Round(m.Revenue / m.Completed, 0) : null;
            m.CancelRatePct = m.Orders > 0 ? Math.Round(m.Cancelled * 100m / m.Orders, 1) : null;
            m.ListDay = [.. Enumerable.Range(0, (int)(t - f).TotalDays + 1).Select(i => f.AddDays(i)).Select(d =>
            {
                var g = live.Where(o => o.DeliveryDate.Date == d).ToList();
                return new RetailRevenueRowModel
                {
                    Key = d.ToString("yyyy-MM-dd"), Label = d.ToString("dd/MM"), Orders = g.Count, Revenue = g.Where(o => Done(o.OrderStatus)).Sum(o => o.TotalAmount),
                    Pipeline = g.Where(o => Open(o.OrderStatus)).Sum(o => o.TotalAmount),
                };
            })];
            m.ListProduct = [.. lines.Where(l => status.GetValueOrDefault(l.RetailOrderId) != RetailOrderStatus.Cancelled).GroupBy(l => l.RetailProductId).Select(g => new RetailRevenueRowModel
            {
                Key = g.Key.ToString(), Label = g.First().ProductName ?? string.Empty, Orders = g.Select(l => l.RetailOrderId).Distinct().Count(), Quantity = g.Sum(l => l.Quantity),
                Revenue = g.Where(l => Done(status.GetValueOrDefault(l.RetailOrderId))).Sum(l => l.LineAmount), Pipeline = g.Where(l => Open(status.GetValueOrDefault(l.RetailOrderId))).Sum(l => l.LineAmount),
            }).OrderByDescending(r => r.Revenue + r.Pipeline)];
            return Result<RetailRevenueModel>.Success(m, ConstantHelper.Message.GetAllSuccessfully);
        }
    }
}
