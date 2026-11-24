using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AppDelicare.Services.Implement
{
    public partial class OrderService
    {
        /// <summary>UI-6 (6.2 nhóm A – D-80): trang chi tiết toàn màn đơn đặt suất – dùng lại DetailAsync (GetDetail) + yêu cầu duyệt mới nhất + giao hàng / POD + đối soát + phản ánh.</summary>
        public async Task<IResult<OrderPageModel>> GetOverview(Guid id)
        {
            try
            {
                var detail = await DetailAsync(id);
                if (detail?.Order is not { } order)
                {
                    return Result<OrderPageModel>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
                }
                var db = _unitOfWork.DbContext;
                var (code, responsible) = await DetailPageSales.CustomerInfoAsync(db, order.CustomerId);
                var deliveries = await DetailPageSales.DeliveriesAsync(db, db.DeliveryOrder.AsNoTracking().Where(d => d.OrderId == id));
                var active = deliveries.Where(d => DeliveryOrderStatus.Active.Contains(d.Status)).ToList();
                var rec = await db.OrderReconciliation.AsNoTracking().Where(r => r.OrderId == id && !r.IsDeleted).OrderByDescending(r => r.ReconciliationDate).FirstOrDefaultAsync();
                var complaints = await DetailPageSales.ComplaintsAsync(db, db.CustomerComplaint.AsNoTracking().Where(c => c.OrderId == id), _clock.Now);
                return Result<OrderPageModel>.Success(new OrderPageModel
                {
                    Detail = detail,
                    ApprovalRequestId = await db.ApprovalRequest.AsNoTracking()
                        .Where(a => !a.IsDeleted && OrderStatus.ApprovalBusinessTypes.Contains(a.BusinessType) && a.BusinessId == id)
                        .OrderByDescending(a => a.RequestedAt).Select(a => (Guid?)a.Id).FirstOrDefaultAsync(),
                    ResponsibleName = responsible, CustomerCode = code,
                    CreatedByName = order.CreatedBy is { } u ? (await DetailPageSales.UserNamesAsync(db, [u])).GetValueOrDefault(u) : null,
                    LineCount = detail.ListLine.Count,
                    DeliveryCount = active.Count,
                    AcceptedQuantity = active.Any(d => d.AcceptedQuantity != null) ? active.Sum(d => d.AcceptedQuantity ?? 0m) : null,
                    ListDelivery = deliveries,
                    Reconciliation = rec == null ? null : new SalesReconciliationBriefModel
                    {
                        Id = rec.Id, Status = rec.Status, StatusName = ReconciliationStatus.Label(rec.Status), ReconciliationDate = rec.ReconciliationDate,
                        OrderedQuantity = rec.OrderedQuantity, ProducedQty = rec.ProducedQty, PackedQty = rec.PackedQty, DeliveredQuantity = rec.DeliveredQuantity,
                        AcceptedQuantity = rec.AcceptedQuantity, VariancePercent = rec.VariancePercent, FinalAmount = rec.FinalAmount,
                    },
                    ListComplaint = complaints, ComplaintCount = complaints.Count,
                    OpenComplaintCount = complaints.Count(c => !ComplaintStatus.Done.Contains(c.Status)),
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
