using AppDelicare.Model.Helper;
using AppDelicare.Services.Implement.Messaging;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Implement
{
    /// <summary>UI-6.35 (D-116): khay quá hạn thu (phiếu Chờ thu hồi quá ContainerReturnDueDays ngày) → 1 tin gộp / ngày cho kinh doanh + giao hàng.</summary>
    public partial class PackagingLedgerService
    {
        public async Task<int> RemindOverdueReturnsAsync(DateTime now)
        {
            var db = _unitOfWork.DbContext;
            DateTime due = now.Date.AddDays(-_rules.ContainerReturnDueDays);
            var late = await (from c in db.ContainerReturn.AsNoTracking()
                              join cu in db.Customer.AsNoTracking() on c.CustomerId equals cu.Id
                              where !c.IsDeleted && c.Status == ContainerReturnStatus.Expected && c.ReturnDate < due
                              group c by new { cu.Id, Name = cu.DisplayName ?? cu.Name } into g
                              select new { g.Key.Name, Qty = g.Sum(x => x.ExpectedQuantity), Oldest = g.Min(x => x.ReturnDate) }).ToListAsync();
            if (late.Count == 0)
            {
                return 0;
            }
            return await BusinessNotifier.ToMenusAsync(_notifications, db, NotificationCatalog.RouteReady, ["#/log-packaging-ledger", "#/crm-customer"],
                new
                {
                    title = $"{late.Count} khách còn khay quá hạn thu (> {_rules.ContainerReturnDueDays} ngày)",
                    detail = "Mở Khay hộp theo khách – nhắc khách / xếp thu ở chuyến tới",
                    lines = late.OrderByDescending(x => x.Qty).Take(15).Select(x => $"📦 {x.Name}: {x.Qty} khay (từ {x.Oldest:dd/MM})").ToList(),
                },
                $"{NotificationCatalog.RouteReady}:TRAY:{now:yyyyMMdd}");
        }
    }
}
