using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AppDelicare.Services.Implement
{
    public partial class CustomerComplaintService
    {
        /// <summary>UI-6 (6.2 nhóm A – D-80): trang chi tiết toàn màn phản ánh – dùng lại DetailAsync (GetDetail) + hạn SLA còn lại + diễn biến kèm tên người xử lý + đơn bù.</summary>
        public async Task<IResult<ComplaintPageModel>> GetOverview(Guid id)
        {
            try
            {
                var detail = await DetailAsync(id);
                if (detail?.Complaint is not { } c)
                {
                    return Result<ComplaintPageModel>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
                }
                var db = _unitOfWork.DbContext;
                var now = _clock.Now;
                var (code, responsible) = await DetailPageSales.CustomerInfoAsync(db, c.CustomerId);
                var actors = await DetailPageSales.UserNamesAsync(db, detail.ListHistory.Select(h => h.ActorId).Append(c.AssignedTo));
                if (c.AssignedTo is { } a && actors.GetValueOrDefault(a) is { Length: > 0 } an)
                {
                    detail.AssignedToName = an;
                }
                string tag = "|" + c.ComplaintNo + "]";
                var compensation = string.IsNullOrEmpty(c.ComplaintNo) ? [] : await db.Order.AsNoTracking()
                    .Where(o => !o.IsDeleted && o.Note != null && o.Note.Contains("[DON_BU:") && o.Note.Contains(tag)).OrderByDescending(o => o.CreatedDate).ToListAsync();
                bool open = !ComplaintStatus.Done.Contains(c.ComplaintStatus);
                return Result<ComplaintPageModel>.Success(new ComplaintPageModel
                {
                    Detail = detail, ResponsibleName = responsible, CustomerCode = code,
                    HoursToDue = open && c.SlaDueAt is { } due ? Math.Round((decimal)(due - now).TotalHours, 1) : null,
                    ResolveHours = c.ResolvedAt is { } done ? Math.Round((decimal)(done - c.ReceivedAt).TotalHours, 1) : null,
                    HistoryCount = detail.ListHistory.Count,
                    CustomerOpenComplaintCount = await db.CustomerComplaint.AsNoTracking()
                        .CountAsync(x => !x.IsDeleted && x.CustomerId == c.CustomerId && x.Id != id && !ComplaintStatus.Done.Contains(x.ComplaintStatus)),
                    CanCompensate = c.OrderId != null && (c.ComplaintStatus == ComplaintStatus.Open || c.ComplaintStatus == ComplaintStatus.InProgress)
                        && CompensableCategories.Contains(c.CategoryCode ?? string.Empty) && !compensation.Any(o => o.Status != OrderStatus.Cancelled),
                    ListHistory = detail.ListHistory.Select(h => new ComplaintHistoryItemModel
                    {
                        EventAt = h.EventAt, EventType = h.EventType, OldStatusName = h.OldStatus == null ? null : ComplaintStatus.Label(h.OldStatus),
                        NewStatusName = h.NewStatus == null ? null : ComplaintStatus.Label(h.NewStatus), Comment = h.Comment,
                        ActorName = h.ActorId is { } u ? actors.GetValueOrDefault(u) : null,
                    }).ToList(),
                    ListCompensationOrder = compensation.Select(DetailPageSales.Brief).ToList(),
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
