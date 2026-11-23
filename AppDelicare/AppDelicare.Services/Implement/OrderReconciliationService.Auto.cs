using AppDelicare.Model.Helper;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// BIZ-2.9 (D-77 – C8, C9): đối soát tự tạo, tự chốt, tự giá vốn.
    /// <list type="bullet">
    /// <item>Bàn giao xong đơn (Đã giao) → tự tạo đối soát: suất phục vụ = suất khách nhận (POD), lỗi Delicare 0 – tính tiền theo BIZ-1.4; nhân viên điểm sửa được tới khi chốt.</item>
    /// <item>Đối soát <b>Khớp</b>, đơn không còn phản ánh mở, đã quá <c>ReconciliationAutoCloseHours</c> (24) giờ → tự Chốt (ghi chú "Hệ thống tự chốt") – chạy trong job 20:00
    ///   và khi mở màn đối soát.</item>
    /// <item>Chốt (tay / tự) → tự tính giá vốn suất (<c>MealCost/Calculate</c>); lỗi chỉ cảnh báo.</item>
    /// </list>
    /// </summary>
    public partial class OrderReconciliationService
    {
        private static bool IsAutoRemark(string? remark) => remark != null && remark == ConstantHelper.Message.ReconcileAutoCreatedRemark;

        public async Task<string?> AutoCreateAsync(Guid orderId)
        {
            if (!_rules.ReconciliationAutoCreate)
            {
                return null;
            }
            try
            {
                var db = _unitOfWork.DbContext;
                if (await db.OrderReconciliation.AsNoTracking().AnyAsync(r => r.OrderId == orderId && !r.IsDeleted))
                {
                    return null;
                }
                var order = await db.Order.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted);
                if (order == null || order.Status != OrderStatus.Delivered)
                {
                    return null;
                }
                var (_, _, accepted, pods, _) = await FiguresAsync(order);
                if (pods.Count == 0)
                {
                    return null;
                }
                var result = await Reconcile(new OrderReconcileRequest { OrderId = orderId, ServedQuantity = accepted, Remark = ConstantHelper.Message.ReconcileAutoCreatedRemark });
                return result.ResponseCode == 200
                    ? string.Format(ConstantHelper.Message.ReconcileAutoCreated, order.OrderNumber ?? order.OrderNo)
                    : string.Format(ConstantHelper.Message.ReconcileAutoCreateFailed, order.OrderNumber ?? order.OrderNo, result.ResponseMessage);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[OrderReconciliationService] tự tạo đối soát đơn {Order}", orderId);
                return null;
            }
        }

        public async Task<int> AutoCloseDueAsync()
        {
            if (_rules.ReconciliationAutoCloseHours <= 0)
            {
                return 0;
            }
            var db = _unitOfWork.DbContext;
            DateTime limit = _clock.Now.AddHours(-_rules.ReconciliationAutoCloseHours);
            var due = await db.OrderReconciliation.AsNoTracking()
                .Where(r => !r.IsDeleted && r.Status == ReconciliationStatus.Matched && (r.ModifiedDate ?? r.CreatedDate) <= limit)
                .Select(r => new { r.Id, r.OrderId }).ToListAsync();
            if (due.Count == 0)
            {
                return 0;
            }
            var orderIds = due.Select(d => d.OrderId).ToList();
            var openComplaint = (await db.CustomerComplaint.AsNoTracking()
                .Where(c => !c.IsDeleted && c.OrderId != null && orderIds.Contains(c.OrderId.Value) && !ComplaintStatus.Done.Contains(c.ComplaintStatus))
                .Select(c => c.OrderId!.Value).ToListAsync()).ToHashSet();
            int closed = 0;
            foreach (var d in due.Where(d => !openComplaint.Contains(d.OrderId)))
            {
                try
                {
                    var r = await Close(new ReconciliationCloseRequest { Id = d.Id, Remark = string.Format(ConstantHelper.Message.ReconcileAutoClosedRemark, _rules.ReconciliationAutoCloseHours) });
                    if (r.ResponseCode == 200)
                    {
                        closed++;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[OrderReconciliationService] tự chốt đối soát {Id}", d.Id);
                }
            }
            return closed;
        }

        /// <summary>Sau khi chốt: tự tính giá vốn suất của đơn. Trả câu cảnh báo khi lỗi.</summary>
        private async Task<string?> AutoMealCostAsync(Guid orderId)
        {
            if (!_rules.MealCostAutoOnReconcileClose)
            {
                return null;
            }
            try
            {
                var r = await _services.GetRequiredService<IMealCostService>().Calculate(new MealCostCalculateRequest { OrderId = orderId });
                return r.ResponseCode == 200 ? null : string.Format(ConstantHelper.Message.MealCostAutoFailed, r.ResponseMessage);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[OrderReconciliationService] tự tính giá vốn đơn {Order}", orderId);
                return string.Format(ConstantHelper.Message.MealCostAutoFailed, ex.Message);
            }
        }
    }
}
