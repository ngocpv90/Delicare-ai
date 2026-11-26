using System.Globalization;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// ACC-3 (D-131 f): bước 3 – 4 của bảng kê: gửi khách xác nhận (email – cần hộp thư gửi IT cấu hình như D-87 c; chưa có → báo chưa nâng cấp),
    /// kế toán ghi nhận hàng loạt "khách đã xác nhận" (chỉ khách Sẵn sàng – đã chốt hết đối soát) → phiếu bán tháng Sẵn sàng ở Chứng từ kế toán (ACC-2);
    /// "khách phản hồi lệch" kèm ghi chú bắt buộc. Lưu bằng AuditChange (BillingConfirmation) – không bảng mới. Link xác nhận cho khách không làm (GAP-PORTAL-01).
    /// </summary>
    public partial class BillingStatementService
    {
        private async Task<(BillingMonthModel? Month, string Key)> MonthModelAsync(DateTime? month)
        {
            var (from, _) = MonthOf(month);
            var m = (await GetMonthOverview(new BillingMonthRequest { Month = from })).Data;
            return (m, from.ToString("yyyy-MM", CultureInfo.InvariantCulture));
        }

        public async Task<IResult<int>> ConfirmCustomers(BillingConfirmRequest request)
        {
            try
            {
                var actor = _currentActor.RequireActor();
                var (m, key) = await MonthModelAsync(request.Month);
                if (m == null)
                {
                    return Result<int>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
                }
                var ids = request.ListCustomerId.ToHashSet();
                var ok = m.ListCustomer.Where(c => ids.Contains(c.CustomerId) && c.Status == StatusReady && c.ConfirmState != BillingConfirmation.StateConfirmed
                    && c.ConfirmState != BillingConfirmation.StateInvoiced).ToList();
                if (ok.Count == 0)
                {
                    return Result<int>.Fail(ConstantHelper.Message.BillingConfirmNothing);
                }
                var db = _unitOfWork.DbContext;
                foreach (var c in ok)
                {
                    db.AuditChange.Add(BillingConfirmation.Log(c.CustomerId, BillingConfirmation.Confirmed, key, string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(), actor.UserId, _clock.Now));
                }
                await db.SaveChangesAsync();
                int skipped = ids.Count - ok.Count;
                return Result<int>.Success(ok.Count, string.Format(ConstantHelper.Message.BillingConfirmed, ok.Count) + (skipped > 0 ? string.Format(ConstantHelper.Message.BillingConfirmSkipped, skipped) : string.Empty));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " ConfirmCustomers: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<int>> DisputeCustomer(BillingConfirmRequest request)
        {
            try
            {
                var actor = _currentActor.RequireActor();
                if (string.IsNullOrWhiteSpace(request.Note))
                {
                    return Result<int>.Fail(ConstantHelper.Message.BillingDisputeNoteRequired);
                }
                var (m, key) = await MonthModelAsync(request.Month);
                var ids = request.ListCustomerId.ToHashSet();
                var rows = m?.ListCustomer.Where(c => ids.Contains(c.CustomerId) && c.Status != StatusNone).ToList() ?? [];
                if (rows.Count == 0)
                {
                    return Result<int>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
                }
                var db = _unitOfWork.DbContext;
                foreach (var c in rows)
                {
                    db.AuditChange.Add(BillingConfirmation.Log(c.CustomerId, BillingConfirmation.Disputed, key, request.Note.Trim(), actor.UserId, _clock.Now));
                }
                await db.SaveChangesAsync();
                return Result<int>.Success(rows.Count, string.Format(ConstantHelper.Message.BillingDisputed, rows.Count));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " DisputeCustomer: ", ex.ToString()));
                throw;
            }
        }

        /// <summary>Gửi email bảng kê cho kế toán khách – cần hộp thư gửi (IT cấu hình máy chủ, D-87 c); chưa có → báo chưa nâng cấp, không ghi gì.</summary>
        public Task<IResult<int>> SendToCustomers(BillingConfirmRequest request)
            => Task.FromResult<IResult<int>>(Result<int>.Fail(ConstantHelper.Message.BillingSendNotReady));
    }
}
