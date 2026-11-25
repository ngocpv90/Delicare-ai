using AppDelicare.Data.Share;
using AppDelicare.Data.Connection;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Services.Implement.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// UI-6.35 (D-116): tài xế bàn giao suất và thu khay kỳ trước tại cùng điểm (một giao dịch) + dữ liệu form bàn giao.
    /// Thu khay trừ dần vào các phiếu Chờ thu hồi cũ nhất của cùng khách × loại (ưu tiên cùng điểm giao), ghi sổ bao bì IN tốt / hỏng; không thu quá số còn ở khách.
    /// </summary>
    public partial class DeliveryStopService
    {
        private string TypeLabel(string? code)
            => _services.GetRequiredService<IOptions<ReferenceListsOptions>>().Value.Lists.TryGetValue("PackagingType", out var l)
               && l.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase)) is { } i ? i.Label : code ?? string.Empty;

        /// <summary>Trong giao dịch bàn giao đang mở; trả lỗi (tiếng Việt) → không ghi gì.</summary>
        private async Task<string?> ApplyReturnsAsync(DatabaseContext db, DeliveryOrder current, ActorInfo actor, DateTime now, List<DeliveryReturnItem> items, List<string> warnings)
        {
            foreach (var item in items.Where(i => i.ReturnedQty > 0))
            {
                string type = DeliveryRules.Clean(item.PackagingType) ?? await PackagingRules.DefaultTypeAsync(db, current.OrderId, _rules);
                if (item.DamagedQty < 0 || item.DamagedQty > item.ReturnedQty)
                {
                    return ConstantHelper.Message.ContainerQuantityInvalid;
                }
                // Số còn ở khách trước lần bàn giao này (bỏ dòng giao vừa thêm trong cùng giao dịch).
                decimal before = (await db.PackagingLedger.AsNoTracking().Where(l => !l.IsDeleted && l.CustomerId == current.CustomerId && l.PackagingType == type)
                    .Select(l => new { l.TransactionType, l.Quantity }).ToListAsync()).Sum(l => PackagingRules.Sign(l.TransactionType) * l.Quantity);
                if (item.ReturnedQty > before)
                {
                    var customer = await db.Customer.AsNoTracking().FirstOrDefaultAsync(c => c.Id == current.CustomerId);
                    return string.Format(ConstantHelper.Message.ContainerOverOutstanding, customer?.DisplayName ?? customer?.Name, PackagingRules.Num(Math.Max(0, before)), type, item.ReturnedQty);
                }
                Guid? locationId = current.CustomerDeliveryLocationId ?? current.DeliveryLocationId;
                var open = (await (from c in db.ContainerReturn.AsNoTracking()
                                   join d in db.DeliveryOrder.AsNoTracking() on c.DeliveryOrderId equals d.Id
                                   where !c.IsDeleted && c.CustomerId == current.CustomerId && c.Status == ContainerReturnStatus.Expected
                                   select new { c.Id, c.ReturnDate, c.Note, c.ExpectedQuantity, Location = d.CustomerDeliveryLocationId ?? d.DeliveryLocationId, Delivery = d }).ToListAsync())
                    .Where(x => string.Equals(PackagingRules.TypeOf(x.Note) ?? _rules.PackagingDefaultType, type, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(x => x.Location == locationId ? 0 : 1).ThenBy(x => x.ReturnDate).ToList();
                int remaining = item.ReturnedQty, damagedLeft = item.DamagedQty;
                foreach (var x in open)
                {
                    if (remaining <= 0)
                    {
                        break;
                    }
                    int take = Math.Min(remaining, x.ExpectedQuantity), dmg = Math.Min(damagedLeft, take);
                    remaining -= take;
                    damagedLeft -= dmg;
                    var cr = _unitOfWork.ContainerReturnRepository.GetSingle(c => c.Id == x.Id)!;
                    cr.ReturnedQuantity = take;
                    cr.DamagedQuantity = dmg;
                    cr.Quantity = take;
                    cr.ReturnDate = now;
                    cr.Status = ContainerReturnStatus.Returned;
                    _unitOfWork.ContainerReturnRepository.Update(cr);
                    string number = x.Delivery.DeliveryNumber ?? x.Delivery.DeliveryNo ?? string.Empty;
                    if (take - dmg > 0)
                    {
                        PackagingRules.Add(_unitOfWork, actor, now, PackagingTransactionType.In, type, take - dmg, x.Delivery, null, PackagingCondition.Good, $"Thu khay khi bàn giao {current.DeliveryNumber} – phiếu đơn giao {number}");
                    }
                    if (dmg > 0)
                    {
                        PackagingRules.Add(_unitOfWork, actor, now, PackagingTransactionType.In, type, dmg, x.Delivery, null, PackagingCondition.Damaged, $"Thu khay khi bàn giao {current.DeliveryNumber} – hỏng");
                    }
                }
                if (remaining > 0)
                {
                    // Khay của khách không thuộc phiếu Chờ thu hồi nào (tồn cũ) – vẫn ghi sổ thu về theo đơn giao hiện tại.
                    int dmg = Math.Min(damagedLeft, remaining);
                    if (remaining - dmg > 0)
                    {
                        PackagingRules.Add(_unitOfWork, actor, now, PackagingTransactionType.In, type, remaining - dmg, current, null, PackagingCondition.Good, $"Thu khay khi bàn giao {current.DeliveryNumber} (ngoài phiếu)");
                    }
                    if (dmg > 0)
                    {
                        PackagingRules.Add(_unitOfWork, actor, now, PackagingTransactionType.In, type, dmg, current, null, PackagingCondition.Damaged, $"Thu khay khi bàn giao {current.DeliveryNumber} – hỏng");
                    }
                }
                warnings.Add(string.Format(ConstantHelper.Message.HandoverTraysReturned, item.ReturnedQty, TypeLabel(type), item.DamagedQty));
            }
            return null;
        }

        /// <summary>Form bàn giao: số kế hoạch, người liên hệ của điểm, khay còn ở khách theo loại.</summary>
        public async Task<IResult<StopHandoverModel>> GetHandover(Guid id)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                var stop = await db.DeliveryStop.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
                var d = stop?.DeliveryOrderId is { } did ? await db.DeliveryOrder.AsNoTracking().FirstOrDefaultAsync(x => x.Id == did) : null;
                if (stop == null || d == null)
                {
                    return Result<StopHandoverModel>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
                }
                var loc = (d.CustomerDeliveryLocationId ?? d.DeliveryLocationId) is { } lid ? await db.CustomerDeliveryLocation.AsNoTracking().FirstOrDefaultAsync(l => l.Id == lid) : null;
                var customer = await db.Customer.AsNoTracking().FirstOrDefaultAsync(c => c.Id == d.CustomerId);
                var rows = await db.PackagingLedger.AsNoTracking().Where(l => !l.IsDeleted && l.CustomerId == d.CustomerId).Select(l => new { l.PackagingType, l.TransactionType, l.Quantity }).ToListAsync();
                var model = new StopHandoverModel
                {
                    StopId = stop.Id, CustomerName = customer?.DisplayName ?? customer?.Name, LocationName = loc?.Name, PlannedMeals = stop.PlannedMealQty ?? DeliveryRules.TaggedMeals(d.Note) ?? 0m,
                    ContactName = loc?.ContactName, ContactPhone = loc?.ContactPhone, DefaultPackagingType = await PackagingRules.DefaultTypeAsync(db, d.OrderId, _rules),
                    ListTray = [.. rows.Where(r => r.PackagingType != null).GroupBy(r => r.PackagingType!).Select(g => new StopTrayModel
                    {
                        PackagingType = g.Key, Label = TypeLabel(g.Key), AtCustomer = Math.Max(0, g.Sum(r => PackagingRules.Sign(r.TransactionType) * r.Quantity)),
                    }).Where(t => t.AtCustomer > 0)],
                };
                return Result<StopHandoverModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetHandover: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>Giao thất bại → tin bot cho điều phối (một tin / điểm) – điểm hiện ở cột "Cần giao lại".</summary>
        private async Task NotifyFailedAsync(DeliveryStop stop, DeliveryRoute route, string reason)
        {
            try
            {
                await BusinessNotifier.ToMenusAsync(_notifications, _unitOfWork.DbContext, NotificationCatalog.RouteReady, ["#/log-route"],
                    new { title = $"Giao thất bại – chuyến {DeliveryRules.RouteNumber(route)} điểm #{stop.SequenceNo}", detail = $"{reason}. Điểm ở cột \"Cần giao lại\" của Điều phối giao hàng – gán chuyến khác" },
                    $"{NotificationCatalog.RouteReady}:FAIL:{stop.Id:N}");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[DeliveryStop] gửi tin giao thất bại lỗi – bỏ qua");
            }
        }
    }
}
