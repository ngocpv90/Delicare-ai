using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AppDelicare.Services.Implement
{
    public partial class ContractService
    {
        /// <summary>UI-6 (6.2 nhóm A – D-80): trang chi tiết toàn màn hợp đồng – dùng lại DetailAsync (GetDetail) + yêu cầu duyệt mới nhất + đơn gần đây + suất 30 ngày + tiền đã đối soát.</summary>
        public async Task<IResult<ContractPageModel>> GetOverview(Guid id)
        {
            try
            {
                var detail = await DetailAsync(id);
                if (detail?.Contract is not { } contract)
                {
                    return Result<ContractPageModel>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
                }
                var db = _unitOfWork.DbContext;
                var today = _clock.Today;
                var (code, responsible) = await DetailPageSales.CustomerInfoAsync(db, contract.CustomerId);
                var orders = db.Order.AsNoTracking().Where(o => !o.IsDeleted && o.ContractId == id);
                var from = today.AddDays(-30);
                var orderIds = orders.Select(o => o.Id);
                return Result<ContractPageModel>.Success(new ContractPageModel
                {
                    Detail = detail, ApprovalRequestId = await _approvalRequestService.LatestRequestIdAsync(ContractStatus.BusinessType, id),
                    ResponsibleName = responsible, CustomerCode = code, MealCount = detail.ListMeal.Count,
                    OrderCount = await orders.CountAsync(),
                    DaysToEnd = contract.EffectiveTo is { } end ? (int)(end.Date - today).TotalDays : null,
                    OrderedQuantity30d = await orders.Where(o => o.Status != OrderStatus.Cancelled && o.MealDate >= from && o.MealDate <= today).SumAsync(o => o.TotalQuantity ?? 0m),
                    ReconciledAmount = await db.OrderReconciliation.AsNoTracking().Where(r => !r.IsDeleted && orderIds.Contains(r.OrderId)).SumAsync(r => r.FinalAmount ?? 0m),
                    LocationCount = await db.CustomerDeliveryLocation.AsNoTracking().CountAsync(l => !l.IsDeleted && l.CustomerId == contract.CustomerId),
                    ListRecentOrder = (await orders.OrderByDescending(o => o.MealDate).ThenByDescending(o => o.CreatedDate).Take(20).ToListAsync()).Select(DetailPageSales.Brief).ToList(),
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
