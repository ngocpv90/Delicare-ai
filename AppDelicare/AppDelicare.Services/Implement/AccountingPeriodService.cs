using System.Globalization;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// ACC-4 (D-131 h – anh chốt 2026-11-26): Kế toán › Khóa kỳ. 5 điều kiện (đối soát chốt hết · bảng kê khách xác nhận · kiểm kê cuối tháng ghi sổ ·
    /// mọi chứng từ đã lập trên AMIS · không còn chứng từ lỗi / cần điều chỉnh), hạn khóa ngày <c>Integration:Misa:CloseDueDay</c> (10) tháng sau.
    /// Kế toán khóa (quyền Sửa #/acc-period, đủ điều kiện, kỳ đã hết); mở khóa gửi duyệt <c>PERIOD_UNLOCK</c> ("Phê duyệt › Mở khóa kỳ" – COO), lý do bắt buộc.
    /// Trạng thái lưu AuditChange <c>AccountingPeriod</c> (AccountingPeriodRules) – không bảng mới. Chứng từ đã gửi AMIS của kỳ đã khóa bị sửa / hủy →
    /// chứng từ điều chỉnh "ĐC-…" ghi vào kỳ đang mở (liệt kê ở đây; số liệu ở Chứng từ kế toán).
    /// </summary>
    public class AccountingPeriodService(IUnitOfWork unitOfWork, IClock clock, ICurrentActor currentActor, IOptions<MisaOptions> misa,
        IAccountingVoucherService vouchers, IBillingStatementService billing, IServiceProvider services) : IAccountingPeriodService
    {
        public const string UnlockBusinessType = "PERIOD_UNLOCK";
        public const string UnlockScreenId = "acc-period-unlock";
        private readonly IUnitOfWork _unitOfWork = unitOfWork;
        private readonly IClock _clock = clock;
        private readonly ICurrentActor _currentActor = currentActor;
        private readonly MisaOptions _misa = misa.Value;
        private readonly IAccountingVoucherService _vouchers = vouchers;
        private readonly IBillingStatementService _billing = billing;
        private readonly IServiceProvider _services = services;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private (DateTime From, DateTime To, string Key) Period(string? month)
        {
            DateTime first = DateTime.TryParseExact(month ?? string.Empty, "yyyy-MM", Inv, DateTimeStyles.None, out var m) ? m : new DateTime(_clock.Today.Year, _clock.Today.Month, 1).AddMonths(-1);
            return (first, first.AddMonths(1).AddDays(-1), first.ToString("yyyy-MM", Inv));
        }

        private async Task<(bool Locked, string? By, DateTime? At)> StateAsync(DateTime month)
        {
            var db = _unitOfWork.DbContext;
            var id = AccountingPeriodRules.PeriodId(month);
            var last = await db.AuditChange.AsNoTracking().Where(a => !a.IsDeleted && a.EntityName == AccountingPeriodRules.Entity && a.IdChange == id)
                .OrderByDescending(a => a.CreatedDate).Select(a => new { a.Action, a.CreatedBy, a.CreatedDate }).FirstOrDefaultAsync();
            if (last == null || last.Action != AccountingPeriodRules.ActionLock)
            {
                return (false, null, null);
            }
            string? by = (await CommonAccess.UserNamesAsync(db, [last.CreatedBy])).GetValueOrDefault(last.CreatedBy ?? Guid.Empty);
            return (true, by, last.CreatedDate);
        }

        private Task<bool> UnlockPendingAsync(DateTime month)
        {
            var id = AccountingPeriodRules.PeriodId(month);
            return _unitOfWork.DbContext.ApprovalRequest.AsNoTracking().AnyAsync(a => !a.IsDeleted && a.BusinessType == UnlockBusinessType && a.BusinessId == id
                && (a.RequestStatus == "Pending" || a.RequestStatus == "InProgress"));
        }

        private async Task<List<PeriodConditionModel>> ConditionsAsync(DateTime from, DateTime to, string key)
        {
            var db = _unitOfWork.DbContext;
            DateTime end = to.AddDays(1);
            var list = new List<PeriodConditionModel>();
            int pendingRecon = await db.Order.AsNoTracking().CountAsync(o => !o.IsDeleted && o.MealDate >= from && o.MealDate < end && o.Status == OrderStatus.Delivered);
            list.Add(new PeriodConditionModel
            {
                Code = "RECON", Title = "Đối soát mọi đơn của tháng đã chốt", Ok = pendingRecon == 0, Detail = pendingRecon == 0 ? "Đạt" : $"Còn {pendingRecon} đơn chờ đối soát",
                Link = "#/crm-reconciliation", ActionLabel = "Xem",
            });
            // Chứng từ của kỳ tính 1 lần (gồm bảng kê từng khách) – điều kiện bảng kê lấy khách từ chứng từ bán hàng tháng, không tính lại bảng kê.
            var vm = (await _vouchers.GetMonth(key, "all")).Data;
            var all = vm?.ListVoucher.Where(v => v.Type != VoucherType.Timesheet).ToList() ?? [];
            var states = await BillingConfirmation.StatesAsync(db, key);
            var saleCustomers = all.Where(v => v.Type == VoucherType.Sale).Select(v => v.Key.Split('|')).Where(k => k.Length >= 2 && Guid.TryParseExact(k[1], "N", out _))
                .Select(k => Guid.ParseExact(k[1], "N")).Distinct().ToList();
            int notConfirmed = saleCustomers.Count(cu => !(states.TryGetValue(cu, out var s) && s.Code is BillingConfirmation.StateConfirmed or BillingConfirmation.StateInvoiced));
            list.Add(new PeriodConditionModel
            {
                Code = "BILLING", Title = $"Bảng kê của {saleCustomers.Count} khách đã được xác nhận", Ok = notConfirmed == 0, Detail = notConfirmed == 0 ? "Đạt" : $"Còn {notConfirmed} khách",
                Link = "#/crm-billing", ActionLabel = "Xem",
            });
            var warehouses = await db.Warehouse.AsNoTracking().Where(w => !w.IsDeleted && (w.Status == null || w.Status == "Active")).Select(w => w.Id).ToListAsync();
            DateTime cFrom = to.AddDays(-4), cTo = to.AddDays(4);
            var counted = await db.StockCount.AsNoTracking().Where(s => !s.IsDeleted && s.Status == "Posted" && s.CountDate >= cFrom && s.CountDate < cTo).Select(s => s.WarehouseId).Distinct().ToListAsync();
            int missingCount = warehouses.Count(w => !counted.Contains(w));
            list.Add(new PeriodConditionModel
            {
                Code = "STOCKCOUNT", Title = $"Kiểm kê cuối tháng đã ghi sổ ({warehouses.Count} kho)", Ok = missingCount == 0, Detail = missingCount == 0 ? "Đạt" : $"Còn {missingCount} kho chưa kiểm kê",
                Link = "#/scm-stockcount", ActionLabel = "Xem",
            });
            int notCreated = all.Count(v => v.Status is VoucherStatus.Ready or VoucherStatus.Waiting);
            list.Add(new PeriodConditionModel
            {
                Code = "SENT", Title = "Mọi chứng từ đã gửi và đã lập trên AMIS", Ok = notCreated == 0, Detail = notCreated == 0 ? "Đạt" : $"{notCreated} chờ gửi / chờ lập",
                Link = $"#/acc-voucher?month={key}&tab=all", ActionLabel = "Xem",
            });
            int bad = all.Count(v => v.Status is VoucherStatus.Error or VoucherStatus.Changed or VoucherStatus.Pending);
            list.Add(new PeriodConditionModel
            {
                Code = "ERRORS", Title = "Không còn chứng từ lỗi / cần điều chỉnh", Ok = bad == 0, Detail = bad == 0 ? "Đạt" : $"{bad} chứng từ",
                Link = $"#/acc-voucher?month={key}&tab=need", ActionLabel = "Xử lý",
            });
            return list;
        }

        public async Task<IResult<PeriodOverviewModel>> GetOverview(string? month)
        {
            try
            {
                var (from, to, key) = Period(month);
                var (locked, by, at) = await StateAsync(from);
                var model = new PeriodOverviewModel
                {
                    Month = key, Locked = locked, LockedBy = by, LockedAt = at, UnlockPending = await UnlockPendingAsync(from), Ended = _clock.Today > to,
                    DueDate = from.AddMonths(1).AddDays(Math.Clamp(_misa.CloseDueDay, 1, 28) - 1),
                };
                if (!locked)
                {
                    model.ListCondition = await ConditionsAsync(from, to, key);
                    model.Remaining = model.ListCondition.Count(c => !c.Ok);
                    model.CanLock = model.Ended && model.Remaining == 0;
                }
                var thisMonth = new DateTime(_clock.Today.Year, _clock.Today.Month, 1);
                for (int i = 0; i < 12; i++)
                {
                    var m = thisMonth.AddMonths(-i);
                    var (l, b, t) = await StateAsync(m);
                    model.ListPeriod.Add(new PeriodRowModel { Month = m.ToString("yyyy-MM", Inv), Locked = l, By = b, At = t, UnlockPending = l && await UnlockPendingAsync(m) });
                }
                // Chứng từ điều chỉnh: chứng từ đã gửi của kỳ đã khóa bị sửa / hủy → ghi vào kỳ đang mở (tháng hiện tại).
                foreach (var p in model.ListPeriod.Where(p => p.Locked))
                {
                    var changed = (await _vouchers.GetMonth(p.Month, "need")).Data?.ListVoucher.Where(v => v.Status == VoucherStatus.Changed).ToList() ?? [];
                    foreach (var v in changed)
                    {
                        model.ListAdjustment.Add(new PeriodAdjustmentModel
                        {
                            Number = "ĐC-" + v.Number, SourceNumber = v.Number, SourceType = v.TypeName, SourceMonth = p.Month, Reason = v.Note,
                            Difference = v.Amount, WriteTo = thisMonth.ToString("yyyy-MM", Inv),
                        });
                    }
                }
                return Result<PeriodOverviewModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetOverview: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<bool>> Lock(PeriodActionRequest request)
        {
            try
            {
                var actor = _currentActor.RequireActor();
                var (from, to, key) = Period(request.Month);
                if ((await StateAsync(from)).Locked)
                {
                    return Result<bool>.Fail(ConstantHelper.Message.PeriodAlreadyLocked);
                }
                if (_clock.Today <= to)
                {
                    return Result<bool>.Fail(ConstantHelper.Message.PeriodNotEnded);
                }
                var conditions = await ConditionsAsync(from, to, key);
                if (conditions.Where(c => !c.Ok).ToList() is { Count: > 0 } left)
                {
                    return Result<bool>.Fail(string.Format(ConstantHelper.Message.PeriodConditionsLeft, left.Count, string.Join("; ", left.Select(c => c.Title + " – " + c.Detail))));
                }
                var db = _unitOfWork.DbContext;
                db.AuditChange.Add(new AuditChange
                {
                    Id = Guid.NewGuid(), IdChange = AccountingPeriodRules.PeriodId(from), EntityName = AccountingPeriodRules.Entity, Action = AccountingPeriodRules.ActionLock,
                    Changes = $"- Khóa kỳ kế toán tháng {from:MM/yyyy}" + (string.IsNullOrWhiteSpace(request.Reason) ? string.Empty : ": " + request.Reason.Trim()),
                    CreatedDate = await AccountingPeriodRules.NextStampAsync(db, AccountingPeriodRules.PeriodId(from), _clock.Now), CreatedBy = actor.UserId, IsDeleted = false,
                });
                await db.SaveChangesAsync();
                return Result<bool>.Success(true, string.Format(ConstantHelper.Message.PeriodLocked, from.ToString("MM/yyyy", Inv)));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Lock: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<string>> RequestUnlock(PeriodActionRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                _currentActor.RequireActor();
                var (from, _, _) = Period(request.Month);
                if (string.IsNullOrWhiteSpace(request.Reason))
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<string>.Fail(ConstantHelper.Message.PeriodUnlockReasonRequired);
                }
                if (!(await StateAsync(from)).Locked)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<string>.Fail(ConstantHelper.Message.PeriodNotLocked);
                }
                if (await UnlockPendingAsync(from))
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<string>.Fail(ConstantHelper.Message.PeriodUnlockPending);
                }
                var (approval, error) = await _services.GetRequiredService<IApprovalRequestService>().CreateAsync(UnlockBusinessType, AccountingPeriodRules.PeriodId(from),
                    Newtonsoft.Json.JsonConvert.SerializeObject(new { month = from.ToString("yyyy-MM", Inv), reason = request.Reason.Trim() }));
                if (error != null)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<string>.Fail(error);
                }
                _unitOfWork.CommitTransaction();
                return Result<string>.Success(approval!.RequestNo, string.Format(ConstantHelper.Message.PeriodUnlockSubmitted, from.ToString("MM/yyyy", Inv), approval.RequestNo));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " RequestUnlock: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        /// <summary>COO duyệt mở khóa (gọi từ handler – engine đã mở transaction).</summary>
        public async Task<string?> ApplyUnlockAsync(Guid periodId, Guid actorUserId, string? proposalJson, string? note)
        {
            var db = _unitOfWork.DbContext;
            string? reason = null;
            try
            {
                reason = proposalJson == null ? null : Newtonsoft.Json.Linq.JObject.Parse(proposalJson)["reason"]?.ToString();
            }
            catch (Newtonsoft.Json.JsonException)
            {
            }
            db.AuditChange.Add(new AuditChange
            {
                Id = Guid.NewGuid(), IdChange = periodId, EntityName = AccountingPeriodRules.Entity, Action = AccountingPeriodRules.ActionUnlock,
                Changes = $"- Mở khóa kỳ kế toán (COO duyệt): {reason ?? note}", CreatedDate = await AccountingPeriodRules.NextStampAsync(db, periodId, _clock.Now), CreatedBy = actorUserId, IsDeleted = false,
            });
            await db.SaveChangesAsync();
            return null;
        }

        public static string DescribePeriod(Guid periodId)
        {
            string s = periodId.ToString("N")[20..26];
            return $"Mở khóa kỳ kế toán tháng {s[4..6]}/{s[..4]}";
        }
    }
}
