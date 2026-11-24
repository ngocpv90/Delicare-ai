using AppDelicare.Data.Connection;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Implement
{
    /// <summary>UI-6 (6.2 nhóm A – D-80): phần dùng chung của 4 trang chi tiết nhóm Kinh doanh (đơn đặt suất, hợp đồng, phản ánh, điểm giao) – chỉ đọc.</summary>
    internal static class DetailPageSales
    {
        /// <summary>Tên người dùng: Person.FullName, không có thì tên đăng nhập.</summary>
        public static async Task<Dictionary<Guid, string>> UserNamesAsync(DatabaseContext db, IEnumerable<Guid?> ids)
        {
            var list = ids.Where(i => i != null).Select(i => i!.Value).Distinct().ToList();
            if (list.Count == 0)
            {
                return [];
            }
            var users = await db.User.AsNoTracking().Where(u => list.Contains(u.Id)).Select(u => new { u.Id, u.UserName, u.PersonId }).ToListAsync();
            var personIds = users.Where(u => u.PersonId != null).Select(u => u.PersonId!.Value).Distinct().ToList();
            var persons = personIds.Count == 0 ? [] : await db.Person.AsNoTracking().Where(p => personIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.FullName);
            return users.ToDictionary(u => u.Id, u => (u.PersonId is { } pid ? persons.GetValueOrDefault(pid) : null) ?? u.UserName ?? string.Empty);
        }

        /// <summary>NV phụ trách khách hàng (Customer.UserAssignId).</summary>
        public static async Task<(string? Code, string? Responsible)> CustomerInfoAsync(DatabaseContext db, Guid customerId)
        {
            var c = await db.Customer.AsNoTracking().Where(x => x.Id == customerId).Select(x => new { x.Code, x.UserAssignId }).FirstOrDefaultAsync();
            if (c == null)
            {
                return (null, null);
            }
            string? name = c.UserAssignId is { } u ? (await UserNamesAsync(db, [u])).GetValueOrDefault(u) : null;
            return (c.Code, name);
        }

        public static async Task<List<SalesComplaintBriefModel>> ComplaintsAsync(DatabaseContext db, IQueryable<CustomerComplaint> query, DateTime now, int take = 50)
        {
            var rows = await query.Where(c => !c.IsDeleted).OrderByDescending(c => c.ReceivedAt).Take(take).ToListAsync();
            var orderIds = rows.Where(r => r.OrderId != null).Select(r => r.OrderId!.Value).Distinct().ToList();
            var orders = orderIds.Count == 0 ? [] : await db.Order.AsNoTracking().Where(o => orderIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.OrderNumber ?? o.OrderNo);
            return rows.Select(c => new SalesComplaintBriefModel
            {
                Id = c.Id, ComplaintNo = c.ComplaintNo, OrderNumber = c.OrderId is { } o ? orders.GetValueOrDefault(o) : null, CategoryCode = c.CategoryCode,
                Severity = c.Severity, SeverityName = ComplaintSeverity.Label(c.Severity), Status = c.ComplaintStatus, StatusName = ComplaintStatus.Label(c.ComplaintStatus),
                ReceivedAt = c.ReceivedAt, SlaDueAt = c.SlaDueAt, Description = c.Description,
                IsOverdue = c.SlaDueAt != null && c.SlaDueAt < now && !ComplaintStatus.Done.Contains(c.ComplaintStatus),
            }).ToList();
        }

        /// <summary>Đơn giao + xác nhận giao (POD) mới nhất + chuyến + điểm giao.</summary>
        public static async Task<List<SalesDeliveryBriefModel>> DeliveriesAsync(DatabaseContext db, IQueryable<DeliveryOrder> query, int take = 50)
        {
            var rows = await query.Where(d => !d.IsDeleted).OrderByDescending(d => d.DeliveryDate).ThenByDescending(d => d.PlannedDeliveryTime).Take(take).ToListAsync();
            var ids = rows.Select(r => r.Id).ToList();
            var pods = ids.Count == 0 ? [] : (await db.ProofOfDelivery.AsNoTracking().Where(p => ids.Contains(p.DeliveryOrderId) && !p.IsDeleted).ToListAsync())
                .GroupBy(p => p.DeliveryOrderId).ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.ConfirmedAt).First());
            var routeIds = rows.Where(r => r.DeliveryRouteId != null).Select(r => r.DeliveryRouteId!.Value).Distinct().ToList();
            var routes = routeIds.Count == 0 ? [] : await db.DeliveryRoute.AsNoTracking().Where(r => routeIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.RouteNo);
            var locIds = rows.Select(r => r.CustomerDeliveryLocationId ?? r.DeliveryLocationId).Where(i => i != null).Select(i => i!.Value).Distinct().ToList();
            var locs = locIds.Count == 0 ? [] : await db.CustomerDeliveryLocation.AsNoTracking().Where(l => locIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.Name ?? l.Code);
            var orderIds = rows.Select(r => r.OrderId).Distinct().ToList();
            var orders = orderIds.Count == 0 ? [] : await db.Order.AsNoTracking().Where(o => orderIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.OrderNumber ?? o.OrderNo);
            return rows.Select(d =>
            {
                var pod = pods.GetValueOrDefault(d.Id);
                return new SalesDeliveryBriefModel
                {
                    Id = d.Id, DeliveryNumber = d.DeliveryNumber ?? d.DeliveryNo, OrderNumber = orders.GetValueOrDefault(d.OrderId), Status = d.Status, StatusName = DeliveryOrderStatus.Label(d.Status),
                    LocationName = (d.CustomerDeliveryLocationId ?? d.DeliveryLocationId) is { } l ? locs.GetValueOrDefault(l) : null,
                    RouteNo = d.DeliveryRouteId is { } r ? routes.GetValueOrDefault(r) : null,
                    DeliveryDate = d.DeliveryDate, PlannedDeliveryTime = d.PlannedDeliveryTime, ActualDeliveryTime = d.ActualDeliveryTime,
                    AcceptedQuantity = pod == null ? null : pod.AcceptedQuantity ?? pod.AcceptedQty, RejectedQuantity = pod == null ? null : pod.RejectedQuantity ?? pod.RejectedQty,
                    ReceiverName = pod?.ReceiverName,
                };
            }).ToList();
        }

        public static OrderBriefModel Brief(Order o) => new()
        {
            Id = o.Id, OrderNumber = o.OrderNumber ?? o.OrderNo, MealDate = o.MealDate, DeliveryDate = o.DeliveryDate, TotalQuantity = o.TotalQuantity, TotalAmount = o.TotalAmount, Status = o.Status,
        };
    }
}
