using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Services.Implement.Messaging;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// UI-6.39 (D-120): người nhận đồng ý trước (thay D-55 i). Đơn đổi / nhường ca mới → "Chờ người nhận đồng ý" (chưa có yêu cầu duyệt), bot gửi người nhận
    /// (✅ Đồng ý / ❌ Từ chối) – đồng ý → kiểm lại 2 ca + tạo yêu cầu duyệt SHIFT_SWAP; từ chối → Từ chối, báo người xin; quá Hr:SwapConsentHours → tự hủy, báo người xin.
    /// </summary>
    public partial class ShiftSwapRequestService
    {
        private HrRulesOptions Hr => _services.GetRequiredService<IOptions<BusinessRulesOptions>>().Value.Hr;
        private INotificationService Notifications => _services.GetRequiredService<INotificationService>();

        private async Task NotifyTargetAsync(ShiftSwapRequest s)
        {
            try
            {
                var d = await DetailAsync(s.Id);
                string what = s.ProposedShiftAssignmentId == null
                    ? $"{d?.RequesterName} muốn nhường cho bạn ca {d?.CurrentAssignment?.ShiftCode} {HrRules.Day(d?.CurrentAssignment?.WorkDate ?? _clock.Today)} {d?.CurrentAssignment?.PlannedStart:HH:mm}–{d?.CurrentAssignment?.PlannedEnd:HH:mm}"
                    : $"{d?.RequesterName} muốn đổi ca {d?.CurrentAssignment?.ShiftCode} {HrRules.Day(d?.CurrentAssignment?.WorkDate ?? _clock.Today)} lấy ca {d?.ProposedAssignment?.ShiftCode} {HrRules.Day(d?.ProposedAssignment?.WorkDate ?? _clock.Today)} của bạn";
                await BusinessNotifier.SafeEnqueueAsync(Notifications, new NotificationRequest
                {
                    Template = NotificationCatalog.SwapConsent, EmployeeIds = [s.TargetEmployeeId],
                    Data = new { title = "🔁 Đề nghị đổi / nhường ca " + s.RequestNo, lines = new[] { what, "Lý do: " + s.Reason, $"Trả lời trong {Hr.SwapConsentHours} giờ – quá hạn đơn tự hủy." } },
                    Buttons = new[] { new NotificationButton("✅ Đồng ý", BusinessNotifier.Callback("hr", "swy", s.Id)), new NotificationButton("❌ Từ chối", BusinessNotifier.Callback("hr", "swn", s.Id)) },
                    IdempotencyKey = $"SWAP_CONSENT:{s.Id:N}",
                });
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[ShiftSwap] gửi đề nghị cho người nhận lỗi – bỏ qua");
            }
        }

        private async Task NotifyRequesterAsync(ShiftSwapRequest s, string text, string key)
        {
            try
            {
                await BusinessNotifier.SafeEnqueueAsync(Notifications, new NotificationRequest
                {
                    Template = NotificationCatalog.ManualText, EmployeeIds = [s.RequesterEmployeeId], Data = new { text }, IdempotencyKey = $"SWAP_CONSENT_RESULT:{s.Id:N}:{key}",
                });
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[ShiftSwap] báo người xin lỗi – bỏ qua");
            }
        }

        /// <summary>Web: người nhận (nhân viên của tài khoản) đồng ý / từ chối.</summary>
        public async Task<IResult<ShiftSwapDetailModel>> Consent(ShiftSwapConsentRequest request)
        {
            var self = await HrRules.ResolveEmployeeAsync(_unitOfWork.DbContext, _currentActor.RequireActor().UserId);
            if (self == null)
            {
                return Result<ShiftSwapDetailModel>.Fail(ConstantHelper.Message.HrUserNoEmployee);
            }
            return await ConsentForEmployee(self.Id, request);
        }

        /// <summary>Bot / web: người nhận <paramref name="employeeId"/> trả lời. Một giao dịch.</summary>
        public async Task<IResult<ShiftSwapDetailModel>> ConsentForEmployee(Guid employeeId, ShiftSwapConsentRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var s = _unitOfWork.ShiftSwapRequestRepository.GetSingle(x => x.Id == request.Id && !x.IsDeleted);
                if (s == null)
                {
                    return Fail(ConstantHelper.Message.HrSwapNotFound);
                }
                if (s.TargetEmployeeId != employeeId)
                {
                    return Fail(ConstantHelper.Message.HrSwapConsentNotTarget);
                }
                if (s.RequestStatus != ShiftSwapStatus.AwaitingConsent)
                {
                    return Fail(string.Format(ConstantHelper.Message.HrSwapConsentClosed, ShiftSwapStatus.Label(s.RequestStatus)));
                }
                string targetName = await _unitOfWork.DbContext.Employee.AsNoTracking().Where(e => e.Id == employeeId).Select(e => e.FullName).FirstOrDefaultAsync() ?? string.Empty;
                if (!request.Accept)
                {
                    s.RequestStatus = LeaveRequestStatus.Rejected;
                    s.Reason = Truncate(s.Reason + $" | Người nhận từ chối{(string.IsNullOrWhiteSpace(request.Note) ? "" : ": " + request.Note.Trim())}");
                    _unitOfWork.ShiftSwapRequestRepository.Update(s);
                    await _unitOfWork.ShiftSwapRequestRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                    _unitOfWork.CommitTransaction();
                    await NotifyRequesterAsync(s, $"❌ {targetName} không đồng ý đơn {s.RequestNo}{(string.IsNullOrWhiteSpace(request.Note) ? "" : ": " + request.Note.Trim())}.", "NO");
                    return Result<ShiftSwapDetailModel>.Success((await DetailAsync(s.Id))!, ConstantHelper.Message.HrSwapConsentRejected);
                }
                var db = _unitOfWork.DbContext;
                var current = await db.ShiftAssignment.AsNoTracking().FirstOrDefaultAsync(a => a.Id == s.CurrentShiftAssignmentId && !a.IsDeleted);
                var proposed = s.ProposedShiftAssignmentId is { } pid ? await db.ShiftAssignment.AsNoTracking().FirstOrDefaultAsync(a => a.Id == pid && !a.IsDeleted) : null;
                if (current == null || (s.ProposedShiftAssignmentId != null && proposed == null))
                {
                    return Fail(ConstantHelper.Message.HrSwapAssignmentNotFound);
                }
                if (await ValidateAsync(current, proposed, s.RequesterEmployeeId, s.TargetEmployeeId, s.Id) is { } invalid)
                {
                    return Fail(invalid);
                }
                s.RequestStatus = ShiftSwapStatus.Pending;
                _unitOfWork.ShiftSwapRequestRepository.Update(s);
                await _unitOfWork.ShiftSwapRequestRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                var (approval, approvalError) = await _approvalRequestService.CreateAsync(ShiftSwapStatus.BusinessType, s.Id);
                if (approvalError != null)
                {
                    return Fail(approvalError);
                }
                s.ApprovalRequestId = approval!.Id;
                _unitOfWork.ShiftSwapRequestRepository.Update(s);
                await _unitOfWork.ShiftSwapRequestRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                await NotifyRequesterAsync(s, $"✅ {targetName} đã đồng ý đơn {s.RequestNo} – đơn chuyển người duyệt.", "OK");
                return Result<ShiftSwapDetailModel>.Success((await DetailAsync(s.Id))!, string.Format(ConstantHelper.Message.HrSwapConsentAccepted, approval.RequestNo));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Consent: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }

            IResult<ShiftSwapDetailModel> Fail(string message)
            {
                _unitOfWork.RollbackTransaction();
                return Result<ShiftSwapDetailModel>.Fail(message);
            }
        }

        private static string Truncate(string? s) => s == null ? string.Empty : s.Length <= 1000 ? s : s[..1000];

        /// <summary>Vòng theo dõi bot: đơn chờ người nhận quá Hr:SwapConsentHours → Đã hủy + báo người xin (chạy lại không làm thêm).</summary>
        public async Task<int> ExpireConsentsAsync()
        {
            DateTime limit = _clock.Now.AddHours(-Hr.SwapConsentHours);
            var rows = _unitOfWork.ShiftSwapRequestRepository.GetAll().Where(s => !s.IsDeleted && s.RequestStatus == ShiftSwapStatus.AwaitingConsent && s.SubmittedAt != null && s.SubmittedAt < limit).ToList();
            foreach (var s in rows)
            {
                s.RequestStatus = LeaveRequestStatus.Cancelled;
                s.Reason = Truncate(s.Reason + " | Tự hủy: người nhận không trả lời");
                _unitOfWork.ShiftSwapRequestRepository.Update(s);
            }
            if (rows.Count > 0)
            {
                await _unitOfWork.ShiftSwapRequestRepository.CommitAsync(ConstantHelper.TypeSave.Update);
                foreach (var s in rows)
                {
                    await NotifyRequesterAsync(s, $"⌛ Đơn {s.RequestNo} tự hủy – người nhận không trả lời trong {Hr.SwapConsentHours} giờ.", "EXP");
                }
            }
            return rows.Count;
        }
    }
}
