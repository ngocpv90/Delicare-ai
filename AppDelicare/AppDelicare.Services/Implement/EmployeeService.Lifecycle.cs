using AppDelicare.Data.Connection;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Services.Implement.Messaging;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.Text.RegularExpressions;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// UI-6.36 (D-117): luật nhân viên (họ tên bắt buộc, SĐT không trùng, công ty theo đơn vị, trạng thái chỉ qua action) + danh sách nghiệp vụ,
    /// cho nghỉ việc (hủy ca sau ngày nghỉ, khóa tài khoản đúng ngày nghỉ), tạm nghỉ / quay lại, thao tác hàng loạt, việc cần làm cho người mới.
    /// Không thêm cột: ngày + lý do nghỉ ghi lịch sử thay đổi (AuditChange "Resign" / hẹn "ResignScheduled").
    /// </summary>
    public static partial class EmployeeRules
    {
        public const string ResignAction = "Resign";
        public const string ScheduledAction = "ResignScheduled";
        public const string CancelledAction = "ResignCancelled";
        public const string StatusAction = "StatusChange";

        [GeneratedRegex(@"^0(3|5|7|8|9)\d{8}$")]
        private static partial Regex MobileVn();
        [GeneratedRegex(@"^02\d{9}$")]
        private static partial Regex LandVn();
        [GeneratedRegex(@"ResignDate: '(?<d>\d{4}-\d{2}-\d{2})'")]
        private static partial Regex DateInChanges();

        /// <summary>Bỏ khoảng trắng / dấu chấm / gạch; +84 / 84 → 0.</summary>
        public static string? NormalizePhone(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                return null;
            }
            string p = new([.. phone.Where(c => char.IsDigit(c) || c == '+')]);
            if (p.StartsWith("+84"))
            {
                p = "0" + p[3..];
            }
            else if (p.StartsWith("84") && p.Length == 11)
            {
                p = "0" + p[2..];
            }
            return p;
        }

        public static bool IsValidPhone(string? normalized) => normalized != null && (MobileVn().IsMatch(normalized) || LandVn().IsMatch(normalized));

        /// <summary>Nhân viên chưa nghỉ việc khác đang dùng số này (so theo số đã chuẩn hóa).</summary>
        public static async Task<Employee?> PhoneOwnerAsync(DatabaseContext db, string normalized, Guid? exceptId)
        {
            string tail = normalized.Length > 9 ? normalized[^9..] : normalized;
            var rows = await db.Employee.AsNoTracking().Where(e => !e.IsDeleted && e.Phone != null && e.Phone.Contains(tail) && e.Status != EmployeeStatus.Resigned && (exceptId == null || e.Id != exceptId)).ToListAsync();
            return rows.FirstOrDefault(e => NormalizePhone(e.Phone) == normalized);
        }

        /// <summary>Lỗi tiếng Việt khi SĐT sai định dạng / trùng; null = hợp lệ (trống cũng hợp lệ ở máy chủ – form bắt buộc).</summary>
        public static async Task<string?> PhoneErrorAsync(DatabaseContext db, string? phone, Guid? exceptId)
        {
            string? n = NormalizePhone(phone);
            if (n == null)
            {
                return null;
            }
            if (!IsValidPhone(n))
            {
                return string.Format(ConstantHelper.Message.EmployeePhoneInvalid, phone);
            }
            var owner = await PhoneOwnerAsync(db, n, exceptId);
            return owner == null ? null : string.Format(ConstantHelper.Message.EmployeePhoneDuplicate, owner.FullName, owner.Code ?? owner.EmployeeCode);
        }

        public static DateTime? ParseScheduled(string? changes)
            => changes != null && DateInChanges().Match(changes) is { Success: true } m && DateTime.TryParse(m.Groups["d"].Value, out var d) ? d : null;

        /// <summary>Hẹn nghỉ việc còn hiệu lực (chưa hủy / chưa áp dụng) theo nhân viên.</summary>
        public static async Task<Dictionary<Guid, DateTime>> ScheduledAsync(DatabaseContext db, IReadOnlyCollection<Guid>? ids = null)
        {
            var rows = await db.AuditChange.AsNoTracking()
                .Where(a => !a.IsDeleted && a.EntityName == nameof(Employee) && a.IdChange != null && (a.Action == ScheduledAction || a.Action == CancelledAction || a.Action == ResignAction)
                            && (ids == null || ids.Contains(a.IdChange!.Value)))
                .Select(a => new { Id = a.IdChange!.Value, a.Action, a.Changes, a.CreatedDate }).ToListAsync();
            return rows.GroupBy(r => r.Id).Select(g => g.OrderByDescending(r => r.CreatedDate).First())
                .Where(r => r.Action == ScheduledAction && ParseScheduled(r.Changes) != null)
                .ToDictionary(r => r.Id, r => ParseScheduled(r.Changes)!.Value);
        }

        public static string StatusLabel(string? status) => status switch
        {
            EmployeeStatus.Active or null or "" => "Đang làm việc",
            EmployeeStatus.Inactive => "Tạm nghỉ",
            EmployeeStatus.Resigned => "Đã nghỉ việc",
            _ => status,
        };
    }

    public partial class EmployeeService
    {
        private T Svc<T>() where T : notnull => services.GetRequiredService<T>();

        /// <summary>Luật chung Insert / Update (D-117 f). Trả lỗi tiếng Việt hoặc null.</summary>
        private async Task<string?> ValidateAsync(Model.Request.EmployeeRequest request, Guid? exceptId)
        {
            if (string.IsNullOrWhiteSpace(request.FullName))
            {
                return ConstantHelper.Message.EmployeeNameRequired;
            }
            request.FullName = request.FullName.Trim();
            if (await EmployeeRules.PhoneErrorAsync(_unitOfWork.DbContext, request.Phone, exceptId) is { } phoneError)
            {
                return phoneError;
            }
            if (request.OrganizationUnitId is { } ou)
            {
                var company = await _unitOfWork.DbContext.OrganizationUnit.AsNoTracking().Where(o => o.Id == ou && !o.IsDeleted).Select(o => (Guid?)o.CompanyId).FirstOrDefaultAsync();
                if (company != null && company != Guid.Empty)
                {
                    request.CompanyId = company;
                }
            }
            return null;
        }

        private void AddAudit(Guid employeeId, string action, string changes)
        {
            var actor = currentActor.Actor;
            _unitOfWork.DbContext.AuditChange.Add(new AuditChange
            {
                Id = Guid.NewGuid(), IdChange = employeeId, EntityName = nameof(Employee), Action = action, CreatedDate = clock.Now, CreatedBy = actor?.UserId,
                Changes = $"- Actor: {actor?.ToString() ?? "system"} {ConstantHelper.Break}" + Environment.NewLine + changes, IsDeleted = false,
            });
        }

        // ================================================================ Danh sách

        public async Task<IResult<EmployeeListModel>> GetList(EmployeeListRequest request)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                DateTime today = clock.Today, monthStart = new(today.Year, today.Month, 1);
                var all = await db.Employee.AsNoTracking().Where(e => !e.IsDeleted).ToListAsync();
                var units = await db.OrganizationUnit.AsNoTracking().Where(o => !o.IsDeleted).Select(o => new { o.Id, o.ParentId, o.Name }).ToListAsync();
                var positions = await db.Position.AsNoTracking().Where(p => !p.IsDeleted).ToDictionaryAsync(p => p.Id, p => p.Name);
                var personIds = all.Where(e => e.PersonId != null).Select(e => e.PersonId!.Value).ToHashSet();
                var users = (await db.User.AsNoTracking().Where(u => !u.IsDeleted && u.PersonId != null && (u.AccountType == null || u.AccountType == Identity.PersonLink.Internal))
                    .Select(u => new { u.Id, u.UserName, u.IsActive, u.PersonId }).ToListAsync()).Where(u => personIds.Contains(u.PersonId!.Value))
                    .GroupBy(u => u.PersonId!.Value).ToDictionary(g => g.Key, g => g.OrderByDescending(u => u.IsActive).First());
                var pending = (await db.ApprovalRequest.AsNoTracking().Where(a => !a.IsDeleted && a.BusinessId != null && AccountLifecycleCodes.BusinessTypes.Contains(a.BusinessType!)
                        && (a.RequestStatus == "Pending" || a.RequestStatus == "InProgress")).Select(a => new { a.BusinessId, a.BusinessType }).ToListAsync())
                    .GroupBy(a => a.BusinessId!.Value).ToDictionary(g => g.Key, g => g.First().BusinessType);
                var userIds = users.Values.Select(u => u.Id).ToHashSet();
                var idents = await db.UserExternalIdentity.AsNoTracking().Where(i => !i.IsDeleted && (i.IdentityStatus == null || i.IdentityStatus == "Active" || i.IdentityStatus == "PendingCode"))
                    .Select(i => new { i.EmployeeId, i.UserId }).ToListAsync();
                var botEmp = idents.Where(i => i.EmployeeId != null).Select(i => i.EmployeeId!.Value).ToHashSet();
                var botUser = idents.Where(i => i.UserId != null && userIds.Contains(i.UserId.Value)).Select(i => i.UserId!.Value).ToHashSet();
                var scheduled = await EmployeeRules.ScheduledAsync(db);

                List<EmployeeListItemModel> items = [.. all.Select(e =>
                {
                    var u = e.PersonId is { } pid && users.TryGetValue(pid, out var uu) ? uu : null;
                    pending.TryGetValue(e.Id, out var pt);
                    string acc = u == null ? (pt == AccountLifecycleCodes.CreateBusinessType ? AccountLifecycleCodes.StatusPending : AccountLifecycleCodes.StatusNone)
                        : pt == AccountLifecycleCodes.ReopenBusinessType ? AccountLifecycleCodes.StatusPending : u.IsActive ? AccountLifecycleCodes.StatusActive : AccountLifecycleCodes.StatusLocked;
                    return new EmployeeListItemModel
                    {
                        Id = e.Id, Code = e.Code ?? e.EmployeeCode, FullName = e.FullName, Phone = e.Phone, OrganizationUnitId = e.OrganizationUnitId,
                        OrganizationUnitName = units.FirstOrDefault(o => o.Id == e.OrganizationUnitId)?.Name, PositionId = e.PositionId,
                        PositionName = e.PositionId is { } po && positions.TryGetValue(po, out var pn) ? pn : null, HireDate = e.HireDate,
                        Status = string.IsNullOrEmpty(e.Status) ? EmployeeStatus.Active : e.Status, StatusName = EmployeeRules.StatusLabel(e.Status),
                        AccountStatus = acc, AccountStatusName = AccountLifecycleCodes.StatusLabel(acc), UserName = u?.UserName,
                        BotLinked = botEmp.Contains(e.Id) || (u != null && botUser.Contains(u.Id)),
                        ScheduledResignDate = scheduled.TryGetValue(e.Id, out var sd) ? sd : null,
                    };
                })];
                var working = items.Where(i => EmployeeStatus.IsWorking(i.Status)).ToList();
                var model = new EmployeeListModel
                {
                    TotalCount = items.Count, WorkingCount = working.Count,
                    NewMonthCount = working.Count(i => i.HireDate >= monthStart && i.HireDate <= today),
                    NoAccountCount = working.Count(i => i.AccountStatus is AccountLifecycleCodes.StatusNone or AccountLifecycleCodes.StatusPending),
                    PendingAccountCount = working.Count(i => i.AccountStatus == AccountLifecycleCodes.StatusPending),
                    NoBotCount = working.Count(i => !i.BotLinked),
                    StatusCounts = items.GroupBy(i => i.Status!).ToDictionary(g => g.Key, g => g.Count()),
                    UnlinkedCount = all.Count(e => e.PersonId == null && EmployeeStatus.IsWorking(e.Status)),
                };

                string status = string.IsNullOrWhiteSpace(request.Status) ? EmployeeStatus.Active : request.Status.Trim();
                IEnumerable<EmployeeListItemModel> q = status.Equals("ALL", StringComparison.OrdinalIgnoreCase) ? items : items.Where(i => string.Equals(i.Status, status, StringComparison.OrdinalIgnoreCase));
                if (request.OrganizationUnitId is { } root)
                {
                    var tree = new HashSet<Guid> { root };
                    for (bool grew = true; grew;)
                    {
                        grew = false;
                        foreach (var o in units.Where(o => o.ParentId != null && tree.Contains(o.ParentId.Value) && !tree.Contains(o.Id)))
                        {
                            tree.Add(o.Id);
                            grew = true;
                        }
                    }
                    q = q.Where(i => i.OrganizationUnitId != null && tree.Contains(i.OrganizationUnitId.Value));
                }
                if (request.PositionId is { } pos)
                {
                    q = q.Where(i => i.PositionId == pos);
                }
                if (!string.IsNullOrWhiteSpace(request.Keyword))
                {
                    string k = request.Keyword.Trim();
                    string? kp = EmployeeRules.NormalizePhone(k);
                    q = q.Where(i => (i.FullName ?? "").Contains(k, StringComparison.OrdinalIgnoreCase) || (i.Code ?? "").Contains(k, StringComparison.OrdinalIgnoreCase)
                                     || (kp != null && kp.Length >= 3 && (EmployeeRules.NormalizePhone(i.Phone) ?? "").Contains(kp)));
                }
                q = request.Flag?.Trim().ToUpperInvariant() switch
                {
                    "NEW_MONTH" => q.Where(i => i.HireDate >= monthStart && i.HireDate <= today),
                    "NO_ACCOUNT" => q.Where(i => i.AccountStatus is AccountLifecycleCodes.StatusNone or AccountLifecycleCodes.StatusPending),
                    "NO_BOT" => q.Where(i => !i.BotLinked),
                    _ => q,
                };
                var list = q.OrderBy(i => i.OrganizationUnitName).ThenBy(i => i.FullName).ToList();
                model.TotalRecords = list.Count;
                int size = Math.Clamp(request.PageSize, 1, 2000), page = Math.Max(1, request.Page);
                model.ListItem = [.. list.Skip((page - 1) * size).Take(size)];
                return Result<EmployeeListModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetList: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<EmployeePhoneCheckModel>> CheckPhone(string? phone, Guid? exceptId)
        {
            string? error = await EmployeeRules.PhoneErrorAsync(_unitOfWork.DbContext, phone, exceptId);
            string? n = EmployeeRules.NormalizePhone(phone);
            return Result<EmployeePhoneCheckModel>.Success(new EmployeePhoneCheckModel
            {
                Valid = error == null, Duplicate = error != null && EmployeeRules.IsValidPhone(n), Message = error,
            }, ConstantHelper.Message.GetAllSuccessfully);
        }

        // ================================================================ Nghỉ việc

        private async Task<(ResignPreviewModel? Model, Employee? Employee, string? Error)> PreviewCoreAsync(Guid id, DateTime? resignDate)
        {
            var db = _unitOfWork.DbContext;
            var e = await db.Employee.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            if (e == null)
            {
                return (null, null, ConstantHelper.Message.RecordNotFoundMessage);
            }
            if (e.Status == EmployeeStatus.Resigned)
            {
                return (null, e, ConstantHelper.Message.EmployeeAlreadyResigned);
            }
            DateTime day = (resignDate ?? clock.Today).Date;
            if (e.HireDate is { } hd && day < hd.Date)
            {
                return (null, e, string.Format(ConstantHelper.Message.EmployeeResignBeforeHire, HrRules.Day(hd)));
            }
            var m = new ResignPreviewModel { EmployeeId = e.Id, FullName = e.FullName, ResignDate = day, Immediate = day <= clock.Today };
            var user = await Identity.PersonLink.UserOfEmployeeAsync(db, e);
            m.UserName = user?.UserName;
            m.HasActiveAccount = user is { IsActive: true };
            m.BotLinks = await db.UserExternalIdentity.AsNoTracking().CountAsync(i => !i.IsDeleted && i.IdentityStatus != MessagingCode.Identity.Revoked && (i.EmployeeId == e.Id || (user != null && i.UserId == user.Id)));
            m.ListShiftDate = await db.ShiftAssignment.AsNoTracking()
                .Where(a => !a.IsDeleted && a.EmployeeId == e.Id && a.WorkDate > day && ShiftAssignmentStatus.Open.Contains(a.AssignmentStatus!))
                .OrderBy(a => a.WorkDate).Select(a => a.WorkDate).ToListAsync();
            if (user is { IsActive: true })
            {
                m.HeldApprovals = (await Svc<IApprovalRequestService>().OpenRequestsHeldByAsync(user.Id)).Count;
            }
            string when = m.Immediate ? "ngay" : "vào ngày " + HrRules.Day(day);
            if (m.HasActiveAccount)
            {
                m.ListMessage.Add($"Khóa tài khoản web {m.UserName} {when}");
            }
            if (m.BotLinks > 0)
            {
                m.ListMessage.Add($"Gỡ {m.BotLinks} liên kết bot {when}");
            }
            if (m.ListShiftDate.Count > 0)
            {
                m.ListMessage.Add($"Hủy {m.ListShiftDate.Count} ca đã phân sau ngày nghỉ: {string.Join(", ", m.ListShiftDate.Take(10).Select(d => d.ToString("dd/MM")))}{(m.ListShiftDate.Count > 10 ? "…" : "")} (báo người phân ca)");
            }
            if (m.HeldApprovals > 0)
            {
                m.ListMessage.Add($"Chuyển {m.HeldApprovals} yêu cầu duyệt đang giữ cho cấp trên {when}");
            }
            if (m.ListMessage.Count == 0)
            {
                m.ListMessage.Add("Không có tài khoản, liên kết bot, ca hay yêu cầu duyệt nào cần xử lý");
            }
            return (m, e, null);
        }

        public async Task<IResult<ResignPreviewModel>> PreviewResign(Guid id, DateTime? resignDate)
        {
            try
            {
                var (m, _, error) = await PreviewCoreAsync(id, resignDate);
                return error != null ? Result<ResignPreviewModel>.Fail(error) : Result<ResignPreviewModel>.Success(m!, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " PreviewResign: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<ResignPreviewModel>> Resign(EmployeeResignRequest request)
        {
            string? reason = request.ReasonCode?.Trim();
            if (string.IsNullOrWhiteSpace(reason))
            {
                return Result<ResignPreviewModel>.Fail(ConstantHelper.Message.EmployeeResignReasonRequired);
            }
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var (m, e, error) = await PreviewCoreAsync(request.Id, request.ResignDate);
                if (error != null)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<ResignPreviewModel>.Fail(error);
                }
                var db = _unitOfWork.DbContext;
                // Ca đã phân sau ngày nghỉ (chưa vào ca) → hủy (xóa mềm, ghi chú) + báo người phân ca.
                var shifts = await db.ShiftAssignment.Where(a => !a.IsDeleted && a.EmployeeId == e!.Id && a.WorkDate > m!.ResignDate && ShiftAssignmentStatus.Open.Contains(a.AssignmentStatus!)).ToListAsync();
                foreach (var a in shifts)
                {
                    ShiftAssignmentService.SoftDelete(a, $"Hủy do nghỉ việc từ {HrRules.Day(m!.ResignDate)}");
                    a.ModifiedDate = clock.Now;
                    a.ModifiedBy = currentActor.Actor?.UserId;
                }
                string changes = $"- ResignDate: '{m!.ResignDate:yyyy-MM-dd}' | Reason: '{reason}' | Note: '{request.Note?.Trim()}' | CancelledShifts: {shifts.Count} {ConstantHelper.Break}";
                if (m.Immediate)
                {
                    var entity = await db.Employee.FirstAsync(x => x.Id == e!.Id);
                    entity.Status = EmployeeStatus.Resigned;
                    entity.ModifiedDate = clock.Now;
                    entity.ModifiedBy = currentActor.Actor?.UserId;
                    AddAudit(e!.Id, EmployeeRules.ResignAction, changes);
                    await db.SaveChangesAsync();
                    await accountLifecycle.LockForResignedAsync(e.Id);
                }
                else
                {
                    AddAudit(e!.Id, EmployeeRules.ScheduledAction, changes);
                    await db.SaveChangesAsync();
                }
                _unitOfWork.CommitTransaction();
                if (shifts.Count > 0)
                {
                    await NotifyShiftsCancelledAsync(e!, m.ResignDate, shifts.Select(s => s.WorkDate).ToList());
                }
                return Result<ResignPreviewModel>.Success(m, m.Immediate ? string.Format(ConstantHelper.Message.EmployeeResigned, e!.FullName)
                    : string.Format(ConstantHelper.Message.EmployeeResignScheduled, e!.FullName, HrRules.Day(m.ResignDate)));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Resign: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        private async Task NotifyShiftsCancelledAsync(Employee e, DateTime day, List<DateTime> dates)
        {
            try
            {
                await BusinessNotifier.ToMenusAsync(Svc<INotificationService>(), _unitOfWork.DbContext, NotificationCatalog.ManualText, ["#/hr-shift-assign"],
                    new { text = $"🗓 {e.FullName} ({e.Code ?? e.EmployeeCode}) nghỉ việc từ {HrRules.Day(day)} – đã hủy {dates.Count} ca đã phân: {string.Join(", ", dates.Take(10).Select(d => d.ToString("dd/MM")))}. Cần phân người thay." },
                    $"RESIGN_SHIFTS:{e.Id:N}:{day:yyyyMMdd}");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[Employee] báo hủy ca do nghỉ việc lỗi – bỏ qua");
            }
        }

        /// <summary>Job hằng ngày: hẹn nghỉ việc tới ngày → Đã nghỉ việc + khóa tài khoản / gỡ bot / leo thang (chạy lại không nhân đôi).</summary>
        public async Task<int> ApplyScheduledResignationsAsync()
        {
            var db = _unitOfWork.DbContext;
            var due = (await EmployeeRules.ScheduledAsync(db)).Where(x => x.Value.Date <= clock.Today).ToList();
            int n = 0;
            foreach (var (id, day) in due)
            {
                var e = await db.Employee.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
                if (e == null)
                {
                    continue;
                }
                if (EmployeeStatus.IsWorking(e.Status))
                {
                    e.Status = EmployeeStatus.Resigned;
                    e.ModifiedDate = clock.Now;
                }
                AddAudit(id, EmployeeRules.ResignAction, $"- ResignDate: '{day:yyyy-MM-dd}' | Source: 'ResignScheduled' {ConstantHelper.Break}");
                await db.SaveChangesAsync();
                await accountLifecycle.LockForResignedAsync(id);
                n++;
            }
            return n;
        }

        // ================================================================ Tạm nghỉ / quay lại

        public async Task<IResult<bool>> SetStatus(EmployeeSetStatusRequest request)
        {
            string? status = request.Status?.Trim();
            if (status is not (EmployeeStatus.Inactive or EmployeeStatus.Active))
            {
                return Result<bool>.Fail(ConstantHelper.Message.EmployeeStatusInvalid);
            }
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var db = _unitOfWork.DbContext;
                var e = await db.Employee.FirstOrDefaultAsync(x => x.Id == request.Id && !x.IsDeleted);
                if (e == null)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<bool>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
                }
                string old = string.IsNullOrEmpty(e.Status) ? EmployeeStatus.Active : e.Status;
                bool scheduled = (await EmployeeRules.ScheduledAsync(db, [e.Id])).ContainsKey(e.Id);
                if (old == status && !(status == EmployeeStatus.Active && scheduled))
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<bool>.Fail(string.Format(ConstantHelper.Message.EmployeeStatusSame, EmployeeRules.StatusLabel(status)));
                }
                if (old != status)
                {
                    e.Status = status;
                    e.ModifiedDate = clock.Now;
                    e.ModifiedBy = currentActor.Actor?.UserId;
                    AddAudit(e.Id, EmployeeRules.StatusAction, $"- Status: '{old}' → '{status}' | Note: '{request.Note?.Trim()}' {ConstantHelper.Break}");
                }
                if (status == EmployeeStatus.Active && scheduled)
                {
                    AddAudit(e.Id, EmployeeRules.CancelledAction, $"- Hủy hẹn nghỉ việc | Note: '{request.Note?.Trim()}' {ConstantHelper.Break}");
                }
                await db.SaveChangesAsync();
                // D-82: tạm nghỉ → khóa / gỡ bot; quay lại → yêu cầu mở lại tài khoản.
                await accountLifecycle.AfterEmployeeSavedAsync(old, e.PositionId, e, false, null);
                _unitOfWork.CommitTransaction();
                return Result<bool>.Success(true, status == EmployeeStatus.Inactive ? ConstantHelper.Message.EmployeeSetInactive : ConstantHelper.Message.EmployeeSetActive);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " SetStatus: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        // ================================================================ Hàng loạt

        public async Task<IResult<EmployeeBulkResultModel>> RequestAccountMany(EmployeeIdsRequest request)
        {
            var result = new EmployeeBulkResultModel();
            var lifecycle = Svc<IAccountLifecycleService>();
            foreach (var id in request.ListId.Distinct())
            {
                var e = await _unitOfWork.DbContext.Employee.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
                if (e == null)
                {
                    continue;
                }
                var r = await lifecycle.RequestCreate(new EmployeeAccountRequest { EmployeeId = id, Reason = request.Reason });
                if (r.ResponseCode == 200)
                {
                    result.Done++;
                }
                else
                {
                    result.Skipped++;
                    result.ListSkipped.Add($"{e.FullName}: {r.ResponseMessage}");
                }
            }
            return Result<EmployeeBulkResultModel>.Success(result, string.Format(ConstantHelper.Message.EmployeeBulkAccount, result.Done, result.Skipped));
        }

        public async Task<IResult<EmployeeBulkResultModel>> CreateBotCodes(EmployeeIdsRequest request)
        {
            var result = new EmployeeBulkResultModel();
            var db = _unitOfWork.DbContext;
            var tg = Svc<ITelegramLinkService>();
            var linked = (await db.UserExternalIdentity.AsNoTracking().Where(i => !i.IsDeleted && i.EmployeeId != null && (i.IdentityStatus == null || i.IdentityStatus == "Active"))
                .Select(i => i.EmployeeId!.Value).ToListAsync()).ToHashSet();
            foreach (var id in request.ListId.Distinct())
            {
                var e = await db.Employee.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
                if (e == null)
                {
                    continue;
                }
                if (linked.Contains(id))
                {
                    result.Skipped++;
                    result.ListSkipped.Add($"{e.FullName}: đã liên kết bot");
                    continue;
                }
                var r = await tg.CreateCodeForEmployee(id);
                if (r.ResponseCode != 200 || r.Data == null)
                {
                    result.Skipped++;
                    result.ListSkipped.Add($"{e.FullName}: {r.ResponseMessage}");
                    continue;
                }
                result.Done++;
                result.ListCode.Add(new EmployeeLinkCodeModel
                {
                    EmployeeId = id, Code = e.Code ?? e.EmployeeCode, FullName = e.FullName, LinkCode = r.Data.Code, ExpiresAt = r.Data.ExpiresAt,
                    DeepLink = r.Data.DeepLinks.TryGetValue(MessagingCode.BotVanHanh, out var v) ? v : r.Data.DeepLinks.Values.FirstOrDefault(),
                });
            }
            return Result<EmployeeBulkResultModel>.Success(result, string.Format(ConstantHelper.Message.EmployeeBulkBot, result.Done, result.Skipped));
        }

        // ================================================================ Người mới

        private async Task<List<EmployeeOnboardingItemModel>> OnboardingAsync(Employee e, EmployeeOverviewModel m)
        {
            var db = _unitOfWork.DbContext;
            DateTime today = clock.Today;
            if (!EmployeeStatus.IsWorking(e.Status) || e.HireDate is not { } hd || hd.Date > today || (today - hd.Date).TotalDays > 30)
            {
                return [];
            }
            bool pendingAccount = await db.ApprovalRequest.AsNoTracking().AnyAsync(a => !a.IsDeleted && a.BusinessId == e.Id && a.BusinessType == AccountLifecycleCodes.CreateBusinessType
                                                                                       && (a.RequestStatus == "Pending" || a.RequestStatus == "InProgress"));
            DateTime monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7)), sunday = monday.AddDays(6);
            int weekShifts = await db.ShiftAssignment.AsNoTracking().CountAsync(a => !a.IsDeleted && a.EmployeeId == e.Id && a.WorkDate >= monday && a.WorkDate <= sunday);
            string prefix = $"{nameof(Employee)}/{e.Id:N}/health/";
            bool health = await db.DocumentFile.AsNoTracking().AnyAsync(f => !f.IsDeleted && f.FileNameHas != null && f.FileNameHas.StartsWith(prefix));
            List<EmployeeOnboardingItemModel> list =
            [
                new() { Code = "ACCOUNT", Label = "Đề nghị cấp tài khoản", Done = m.UserId != null || pendingAccount, Detail = m.UserId != null ? "Đã có tài khoản " + m.UserName : pendingAccount ? "Đang chờ IT duyệt" : null },
                new() { Code = "BOT", Label = "Liên kết bot", Done = m.BotLinked },
                new() { Code = "SHIFT", Label = "Phân ca tuần này", Done = weekShifts > 0, Detail = weekShifts > 0 ? $"{weekShifts} ca" : null },
                new() { Code = "HEALTH", Label = "Giấy khám sức khỏe", Done = health },
            ];
            return list.All(x => x.Done) ? [] : list;
        }
    }
}
