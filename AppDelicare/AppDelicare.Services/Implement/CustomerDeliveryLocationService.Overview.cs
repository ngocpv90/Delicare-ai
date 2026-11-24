using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AppDelicare.Services.Implement
{
    public partial class CustomerDeliveryLocationService
    {
        /// <summary>UI-6 (6.2 nhóm A – D-80): trang chi tiết toàn màn điểm giao – khách hàng, suất / đơn / giao đúng giờ 30 ngày, đơn và đơn giao gần đây, phản ánh của các đơn tại điểm.</summary>
        public async Task<IResult<DeliveryLocationPageModel>> GetOverview(Guid id)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                var loc = await db.CustomerDeliveryLocation.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id && !l.IsDeleted);
                if (loc == null)
                {
                    return Result<DeliveryLocationPageModel>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
                }
                var today = _clock.Today;
                var from = today.AddDays(-30);
                var (code, responsible) = await DetailPageSales.CustomerInfoAsync(db, loc.CustomerId);
                var orders = db.Order.AsNoTracking().Where(o => !o.IsDeleted && o.DeliveryLocationId == id);
                var recent = await orders.Where(o => o.Status != OrderStatus.Cancelled && o.MealDate >= from && o.MealDate <= today)
                    .Select(o => new { o.MealDate, o.TotalQuantity }).ToListAsync();
                decimal meals = recent.Sum(o => o.TotalQuantity ?? 0m);
                int days = recent.Select(o => o.MealDate.Date).Distinct().Count();
                var deliveryQuery = db.DeliveryOrder.AsNoTracking().Where(d => !d.IsDeleted && (d.CustomerDeliveryLocationId == id || d.DeliveryLocationId == id));
                var done = await deliveryQuery.Where(d => d.DeliveryDate >= from && d.DeliveryDate <= today
                        && (d.Status == DeliveryOrderStatus.Delivered || d.Status == DeliveryOrderStatus.Failed))
                    .Select(d => new { d.Status, d.PlannedDeliveryTime, d.ActualDeliveryTime }).ToListAsync();
                int tolerance = _rules.Kpi.OnTimeToleranceMinutes;
                var orderIds = orders.Select(o => o.Id);
                var complaints = await DetailPageSales.ComplaintsAsync(db, db.CustomerComplaint.AsNoTracking().Where(c => c.OrderId != null && orderIds.Contains(c.OrderId.Value)), _clock.Now);
                return Result<DeliveryLocationPageModel>.Success(new DeliveryLocationPageModel
                {
                    Location = loc, CustomerCode = code, ResponsibleName = responsible,
                    CustomerName = await db.Customer.AsNoTracking().Where(c => c.Id == loc.CustomerId).Select(c => c.DisplayName ?? c.Name).FirstOrDefaultAsync(),
                    OrderCount30d = recent.Count, MealQuantity30d = meals, AvgMealsPerDay30d = days == 0 ? 0m : Math.Round(meals / days, 1),
                    DeliveryCount30d = done.Count,
                    OnTimePct30d = done.Count == 0 ? null : Math.Round(100m * done.Count(d => d.Status == DeliveryOrderStatus.Delivered && d.ActualDeliveryTime != null
                        && d.ActualDeliveryTime <= d.PlannedDeliveryTime.AddMinutes(tolerance)) / done.Count, 1),
                    ListRecentOrder = (await orders.OrderByDescending(o => o.MealDate).ThenByDescending(o => o.CreatedDate).Take(20).ToListAsync()).Select(DetailPageSales.Brief).ToList(),
                    ListRecentDelivery = await DetailPageSales.DeliveriesAsync(db, deliveryQuery, 20),
                    ListComplaint = complaints, ComplaintCount = complaints.Count, OpenComplaintCount = complaints.Count(c => !ComplaintStatus.Done.Contains(c.Status)),
                }, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetOverview: ", ex.ToString()));
                throw;
            }
        }
    }
}
