using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// UI-6.13 (D-91 b): khách xin đổi món riêng – trên trang chi tiết đơn. Món của đơn (theo thực đơn của từng dòng, buổi, lựa chọn), số suất mặc định = cả lựa chọn,
    /// tùy chọn áp cho các đơn của khách trong khoảng ngày (mọi ô có món đó, món thay sửa được từng ô), nguồn (Email / Điện thoại / Zalo / Trực tiếp).
    /// Mỗi ô một yêu cầu gắn đơn; tự duyệt theo cùng 4 điều kiện như đổi cho mọi khách, còn lại COO. Quyền theo màn Đơn đặt suất (KD).
    /// </summary>
    public partial class MenuChangeRequestService
    {
        private readonly INotificationService _notifications = notifications;

        private async Task AlertReleasedPlanAsync(MenuChangeRequest r, ProductionPlan plan)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                var ids = new[] { r.OriginalDishId, r.RequestedDishId }.Where(x => x != null).Select(x => x!.Value).ToList();
                var names = await db.Dish.AsNoTracking().Where(d => ids.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name);
                var users = await Messaging.RecipientResolver.UsersInGroupsAsync(db, ["OPS-KIT", "OPS-WH", "OPS-PLAN"]);
                if (users.Count == 0)
                {
                    return;
                }
                string text = $"🔴 Đổi món đã duyệt {r.RequestNo} cho ngày {r.MealDate:dd/MM} nhưng kế hoạch SX {plan.PlanNumber ?? plan.PlanNo} đã phát hành – hệ thống KHÔNG tự sửa kế hoạch.\n"
                    + $"{(r.OriginalDishId is { } o ? names.GetValueOrDefault(o) : "")} → {(r.RequestedDishId is { } n ? names.GetValueOrDefault(n) : "")} · {r.Quantity:#,0.##} suất. Bếp / kho điều chỉnh nguyên liệu và lệnh SX bằng tay.";
                await Messaging.BusinessNotifier.SafeEnqueueAsync(_notifications, new NotificationRequest
                {
                    Template = Messaging.NotificationCatalog.ManualText, UserIds = [.. users], BotCode = MessagingCode.BotVanHanh, Data = new { text }, IdempotencyKey = $"MENU_CHANGE_RELEASED:{r.Id:N}",
                });
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[MenuChangeRequest] báo kế hoạch đã phát hành {Id}", r.Id);
            }
        }

        public static readonly IReadOnlyDictionary<string, string> CustomerSources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["EMAIL"] = "Email", ["PHONE"] = "Điện thoại", ["ZALO"] = "Zalo", ["DIRECT"] = "Trực tiếp",
        };

        /// <summary>Món của các dòng đơn (đơn còn mở): thực đơn của dòng × thứ của ngày ăn × buổi × lựa chọn (dòng không lựa chọn → mọi lựa chọn của buổi).</summary>
        private async Task<List<OrderDishModel>> OrderDishesAsync(Order order)
        {
            var db = _unitOfWork.DbContext;
            int day = MenuService.MenuDayOf(order.MealDate);
            var lines = await db.OrderLine.AsNoTracking().Where(l => l.OrderId == order.Id && !l.IsDeleted && l.MenuId != null && l.Quantity > 0).ToListAsync();
            var menuIds = lines.Select(l => l.MenuId!.Value).Distinct().ToList();
            var mls = await db.MenuLine.AsNoTracking().Where(m => menuIds.Contains(m.MenuId) && !m.IsDeleted && m.DayOfWeek == day).ToListAsync();
            var dishIds = mls.Select(m => m.DishId).Distinct().ToList();
            var dishes = await db.Dish.AsNoTracking().Where(d => dishIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => new { d.Name, Cat = d.DishCategoryId ?? d.CategoryId });
            var result = new List<OrderDishModel>();
            foreach (var l in lines)
            {
                foreach (var m in mls.Where(m => m.MenuId == l.MenuId && string.Equals(m.MealType, l.MealType, StringComparison.OrdinalIgnoreCase) && (l.MenuOptionNo == null || (m.SequenceNo ?? 1) == l.MenuOptionNo)))
                {
                    var ex = result.FirstOrDefault(r => r.MenuId == m.MenuId && r.DishId == m.DishId && r.MealType == l.MealType && r.OptionNo == (m.SequenceNo ?? 1));
                    if (ex != null)
                    {
                        ex.Quantity += l.Quantity;
                        continue;
                    }
                    var d = dishes.GetValueOrDefault(m.DishId);
                    result.Add(new OrderDishModel
                    {
                        OrderId = order.Id, OrderNumber = order.OrderNumber ?? order.OrderNo, MealDate = order.MealDate.Date, MenuId = m.MenuId, MealType = l.MealType, MealName = MenuService.MealLabel(l.MealType),
                        OptionNo = m.SequenceNo ?? 1, DishId = m.DishId, DishName = d?.Name, CategoryId = d?.Cat, Quantity = l.Quantity,
                    });
                }
            }
            return result.OrderBy(r => MenuService.MealRank(r.MealType)).ThenBy(r => r.OptionNo).ThenBy(r => r.DishName).ToList();
        }

        public async Task<IResult<OrderDishChangeModel>> GetOrderDishChange(OrderDishChangeRequest request)
        {
            try
            {
                var (model, error) = await OrderPreviewAsync(request);
                return error != null ? Result<OrderDishChangeModel>.Fail(error) : Result<OrderDishChangeModel>.Success(model!, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetOrderDishChange: ", ex.ToString()));
                throw;
            }
        }

        private async Task<(OrderDishChangeModel? Model, string? Error)> OrderPreviewAsync(OrderDishChangeRequest request)
        {
            var db = _unitOfWork.DbContext;
            var order = await db.Order.AsNoTracking().FirstOrDefaultAsync(o => o.Id == request.OrderId && !o.IsDeleted);
            if (order == null)
            {
                return (null, ConstantHelper.Message.ReconcileOrderNotFound);
            }
            if (ClosedOrderStatuses.Contains(order.Status))
            {
                return (null, string.Format(ConstantHelper.Message.MenuChangeOrderInvalid, order.OrderNumber ?? order.OrderNo, OrderStatus.Label(order.Status)));
            }
            var model = new OrderDishChangeModel { OrderId = order.Id, ListDish = await OrderDishesAsync(order) };
            if (request.OriginalDishId is not { } oldDish)
            {
                return (model, null);
            }
            var source = model.ListDish.FirstOrDefault(d => d.DishId == oldDish && (request.MealType == null || string.Equals(d.MealType, request.MealType, StringComparison.OrdinalIgnoreCase)));
            if (source == null)
            {
                return (null, string.Format(ConstantHelper.Message.MenuChangeDishNotInMenu, order.MealDate.ToString("dd/MM/yyyy")));
            }
            // Các đơn trong phạm vi: đơn này + (nếu có khoảng ngày) các đơn còn mở của cùng khách có món cũ ở cùng buổi.
            var targets = new List<Order> { order };
            if (request.ApplyFrom is { } from && request.ApplyTo is { } to && to >= from)
            {
                DateTime end = to.Date.AddDays(1);
                targets.AddRange(await db.Order.AsNoTracking().Where(o => !o.IsDeleted && o.CustomerId == order.CustomerId && o.Id != order.Id && o.MealDate >= from.Date && o.MealDate < end
                    && !ClosedOrderStatuses.Contains(o.Status!)).OrderBy(o => o.MealDate).ToListAsync());
            }
            foreach (var o in targets)
            {
                foreach (var d in (await OrderDishesAsync(o)).Where(d => d.DishId == oldDish && string.Equals(d.MealType, source.MealType, StringComparison.OrdinalIgnoreCase)))
                {
                    Guid? newDish = request.ListOverride.FirstOrDefault(x => x.OrderId == o.Id)?.RequestedDishId ?? request.RequestedDishId;
                    var item = new OrderDishChangeItemModel
                    {
                        OrderId = o.Id, OrderNumber = d.OrderNumber, MealDate = d.MealDate, MealName = d.MealName, OptionNo = d.OptionNo, MenuId = d.MenuId,
                        Quantity = o.Id == order.Id && request.Quantity is > 0 ? Math.Min(request.Quantity.Value, d.Quantity) : d.Quantity, RequestedDishId = newDish,
                    };
                    if (newDish is { } nd)
                    {
                        var (p, _) = await PreviewAsync(new MenuChangeCellRequest
                        {
                            MenuId = d.MenuId, WeekStart = OrderService.MondayOf(d.MealDate), MealType = d.MealType, OptionNo = d.OptionNo, Day = MenuService.MenuDayOf(d.MealDate),
                            OriginalDishId = oldDish, RequestedDishId = nd,
                        });
                        item.AutoApprove = p?.AutoApprove == true;
                        item.ListBlock = p?.ListBlock ?? [];
                    }
                    model.ListItem.Add(item);
                }
            }
            model.Source = source;
            return (model, null);
        }

        /// <summary>Gửi đổi món cho đơn (và các đơn của khách trong khoảng ngày): mỗi ô một yêu cầu gắn đơn; tự duyệt từng ô khi đủ 4 điều kiện, còn lại → duyệt COO.</summary>
        public async Task<IResult<OrderDishChangeResultModel>> CreateForOrder(OrderDishChangeRequest request)
        {
            try
            {
                if (request.OriginalDishId == null || request.RequestedDishId == null || request.RequestedDishId == request.OriginalDishId)
                {
                    return Result<OrderDishChangeResultModel>.Fail(ConstantHelper.Message.MenuChangeDishRequired);
                }
                string? src = request.Source != null && CustomerSources.TryGetValue(request.Source, out var sn) ? sn : null;
                if (src == null)
                {
                    return Result<OrderDishChangeResultModel>.Fail(ConstantHelper.Message.MenuChangeSourceRequired);
                }
                var (p, error) = await OrderPreviewAsync(request);
                if (error != null)
                {
                    return Result<OrderDishChangeResultModel>.Fail(error);
                }
                if (p!.ListItem.Count == 0)
                {
                    return Result<OrderDishChangeResultModel>.Fail(ConstantHelper.Message.MenuChangeNothingInRange);
                }
                var actor = _currentActor.RequireActor();
                var result = new OrderDishChangeResultModel();
                foreach (var item in p.ListItem.Where(i => i.RequestedDishId != null && i.RequestedDishId != request.OriginalDishId))
                {
                    string reason = $"Khách xin đổi ({src}){(string.IsNullOrWhiteSpace(request.Note) ? string.Empty : " – " + request.Note.Trim())}";
                    var entity = new MenuChangeRequest
                    {
                        Id = Guid.NewGuid(), OrderId = item.OrderId, MenuId = item.MenuId, OriginalDishId = request.OriginalDishId, RequestedDishId = item.RequestedDishId,
                        MealDate = item.MealDate, Quantity = item.Quantity, Reason = reason, RequestedBy = actor.UserId, RequestStatus = MenuChangeStatus.Pending,
                    };
                    using var tran = await _unitOfWork.OpenTransaction();
                    entity.RequestNo = await _documentNumberService.NextAsync(DocumentKind.MenuChange, _clock.Today);
                    _unitOfWork.MenuChangeRequestRepository.Add(entity);
                    await _unitOfWork.MenuChangeRequestRepository.CommitAsync(ConstantHelper.TypeSave.Insert);
                    if (item.AutoApprove)
                    {
                        var applyError = await ApproveFromEngineAsync(entity.Id, actor.UserId);
                        if (applyError != null)
                        {
                            _unitOfWork.RollbackTransaction();
                            result.ListError.Add($"{item.OrderNumber}: {applyError}");
                            continue;
                        }
                        var saved = _unitOfWork.MenuChangeRequestRepository.GetSingle(x => x.Id == entity.Id);
                        saved!.Reason = reason + " · " + AutoApprovedNote;
                        _unitOfWork.MenuChangeRequestRepository.Update(saved);
                        await _unitOfWork.MenuChangeRequestRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                        result.AutoApproved++;
                    }
                    else
                    {
                        var (_, approvalError) = await _approvalRequestService.CreateAsync(MenuChangeStatus.BusinessType, entity.Id);
                        if (approvalError != null)
                        {
                            _unitOfWork.RollbackTransaction();
                            result.ListError.Add($"{item.OrderNumber}: {approvalError}");
                            continue;
                        }
                        result.SentToApprove++;
                    }
                    _unitOfWork.CommitTransaction();
                    result.ListRequestNo.Add(entity.RequestNo!);
                }
                return Result<OrderDishChangeResultModel>.Success(result, string.Format(ConstantHelper.Message.MenuChangeOrderDone, result.ListRequestNo.Count, result.AutoApproved, result.SentToApprove, result.ListError.Count));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " CreateForOrder: ", ex.ToString()));
                throw;
            }
        }
    }
}
