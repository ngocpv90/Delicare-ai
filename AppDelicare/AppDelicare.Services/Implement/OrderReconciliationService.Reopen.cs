using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Helper;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// ACC-3 (D-131 g – anh chốt 2026-11-26): mở lại đối soát Đã chốt – Kế toán / Trưởng KD gửi (lý do bắt buộc) → yêu cầu duyệt <c>RECON_REOPEN</c>
    /// (mục "Phê duyệt › Mở lại đối soát" – COO, engine D-79 / D-31). Duyệt → đối soát về Khớp (chênh lệch 0) / Chênh lệch, đơn Đã đối soát → Đã giao,
    /// ghi chú "[Mở lại: …]" giữ lịch sử; chứng từ bán hàng đã gửi AMIS thành "Đã sửa – cần điều chỉnh" khi số đổi (ACC-2). Kỳ đã khóa (ACC-4) → chặn.
    /// </summary>
    public partial class OrderReconciliationService
    {
        public const string ReopenBusinessType = "RECON_REOPEN";
        public const string ReopenScreenId = "crm-recon-reopen";

        public async Task<IResult<string>> RequestReopen(Guid id, string? reason)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                _currentActor.RequireActor();
                string? why = DeliveryRules.Clean(reason);
                if (why == null)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<string>.Fail(ConstantHelper.Message.ReconReopenReasonRequired);
                }
                var db = _unitOfWork.DbContext;
                var r = await db.OrderReconciliation.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
                if (r == null)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<string>.Fail(ConstantHelper.Message.ReconciliationNotFound);
                }
                if (r.Status != ReconciliationStatus.Closed)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<string>.Fail(ConstantHelper.Message.ReconReopenNotClosed);
                }
                var mealDate = await db.Order.AsNoTracking().Where(o => o.Id == r.OrderId).Select(o => (DateTime?)o.MealDate).FirstOrDefaultAsync();
                if (mealDate is { } md && await AccountingPeriodRules.LockedMessageAsync(db, md, "mở lại đối soát") is { } locked)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<string>.Fail(locked);
                }
                if (await db.ApprovalRequest.AsNoTracking().AnyAsync(a => !a.IsDeleted && a.BusinessType == ReopenBusinessType && a.BusinessId == id
                        && (a.RequestStatus == "Pending" || a.RequestStatus == "InProgress")))
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<string>.Fail(ConstantHelper.Message.ReconReopenPending);
                }
                var (approval, error) = await Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<AppDelicare.Services.Interface.IApprovalRequestService>(_services).CreateAsync(ReopenBusinessType, id, Newtonsoft.Json.JsonConvert.SerializeObject(new { reason = why }));
                if (error != null)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<string>.Fail(error);
                }
                _unitOfWork.CommitTransaction();
                return Result<string>.Success(approval!.RequestNo, string.Format(ConstantHelper.Message.ReconReopenSubmitted, approval.RequestNo));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " RequestReopen: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        /// <summary>Thực thi khi COO duyệt (gọi từ handler – engine đã mở transaction).</summary>
        public async Task<string?> ApplyReopenAsync(Guid id, string? proposalJson, string? note)
        {
            var db = _unitOfWork.DbContext;
            var r = await db.OrderReconciliation.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            if (r == null)
            {
                return ConstantHelper.Message.ReconciliationNotFound;
            }
            if (r.Status != ReconciliationStatus.Closed)
            {
                return ConstantHelper.Message.ReconReopenNotClosed;
            }
            string? reason = null;
            try
            {
                reason = proposalJson == null ? null : Newtonsoft.Json.Linq.JObject.Parse(proposalJson)["reason"]?.ToString();
            }
            catch (Newtonsoft.Json.JsonException)
            {
            }
            r.Status = (r.VarianceQty ?? 0m) == 0m ? ReconciliationStatus.Matched : ReconciliationStatus.Variance;
            string mark = $"[Mở lại {_clock.Now:dd/MM/yyyy}: {reason ?? note ?? "COO duyệt"}]";
            r.Remark = string.IsNullOrWhiteSpace(r.Remark) ? mark : r.Remark + " " + mark;
            _unitOfWork.OrderReconciliationRepository.Update(r);
            var order = await db.Order.FirstOrDefaultAsync(o => o.Id == r.OrderId && !o.IsDeleted);
            if (order != null && order.Status == OrderStatus.Reconciled)
            {
                order.Status = OrderStatus.Delivered;
                _unitOfWork.OrderRepository.Update(order);
            }
            await _unitOfWork.OrderReconciliationRepository.CommitAsync(ConstantHelper.TypeSave.Update);
            return null;
        }

        public async Task<string?> DescribeReopenAsync(Guid id)
        {
            var db = _unitOfWork.DbContext;
            var x = await (from r in db.OrderReconciliation.AsNoTracking()
                           join o in db.Order.AsNoTracking() on r.OrderId equals o.Id
                           join c in db.Customer.AsNoTracking() on o.CustomerId equals c.Id into cs
                           from c in cs.DefaultIfEmpty()
                           where r.Id == id
                           select new { o.OrderNumber, o.MealDate, Customer = c != null ? c.Name : null, r.FinalAmount }).FirstOrDefaultAsync();
            return x == null ? null : $"Mở lại đối soát đơn {x.OrderNumber} · {x.Customer} · ngày ăn {x.MealDate:dd/MM/yyyy} · {x.FinalAmount:#,0} đ";
        }
    }
}
