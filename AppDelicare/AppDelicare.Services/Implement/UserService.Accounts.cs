using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Services.Implement.Identity;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;

namespace AppDelicare.Model.Model
{
    /// <summary>UI-6.42 (D-123): danh sách "Tài khoản" (User/GetList).</summary>
    public class AccountListRequest
    {
        /// <summary>EMPLOYEE | SYSTEM | CUSTOMER.</summary>
        public string? Type { get; set; }
        /// <summary>ACTIVE | MUST_CHANGE | LOCKED | RESIGNED_OPEN.</summary>
        public string? Status { get; set; }
        public Guid? GroupId { get; set; }
        public Guid? OrganizationUnitId { get; set; }
        public string? Keyword { get; set; }
        /// <summary>RESIGNED_ACTIVE | INACTIVE_LONG.</summary>
        public string? Flag { get; set; }
    }

    public class AccountListModel
    {
        public List<AccountItemModel> ListItem { get; set; } = [];
        public int Active { get; set; }
        public int ActiveEmployee { get; set; }
        public int ActiveSystem { get; set; }
        public int PendingIt { get; set; }
        public int ResignedOpen { get; set; }
        public int InactiveLong { get; set; }
        public int InactiveWarnDays { get; set; }
    }

    public class AccountItemModel
    {
        public Guid Id { get; set; }
        public string? UserName { get; set; }
        /// <summary>EMPLOYEE | SYSTEM | CUSTOMER.</summary>
        public string Kind { get; set; } = "SYSTEM";
        public bool IsActive { get; set; }
        public bool IsAdmin { get; set; }
        public string? FullName { get; set; }
        public Guid? EmployeeId { get; set; }
        public string? EmployeeCode { get; set; }
        public string? PositionName { get; set; }
        public string? UnitName { get; set; }
        public string? EmployeeStatus { get; set; }
        public List<string> ListGroup { get; set; } = [];
        public DateTime? LastAccess { get; set; }
        public int? InactiveDays { get; set; }
        public bool BotLinked { get; set; }
        /// <summary>ACTIVE | MUST_CHANGE | LOCKED | RESIGNED_OPEN.</summary>
        public string Status { get; set; } = "ACTIVE";
        public string? StatusName { get; set; }
    }

    public class AccountIdsRequest
    {
        public List<Guid> UserIds { get; set; } = [];
        public List<Guid> AddGroupIds { get; set; } = [];
        public List<Guid> RemoveGroupIds { get; set; } = [];
    }

    /// <summary>UI-6.49 (7) (QĐ-63, D-123 c): trang chi tiết tài khoản toàn màn (User/GetOverview).</summary>
    public class AccountOverviewModel
    {
        public AccountItemModel Account { get; set; } = new();
        public Guid? EmployeeId { get; set; }
        public List<Guid> ListGroupId { get; set; } = [];
        public int AccessCount30d { get; set; }
        public List<AccountAccessModel> ListAccess { get; set; } = [];
        public List<AccountBotModel> ListBot { get; set; } = [];
    }

    public class AccountAccessModel
    {
        public DateTime? At { get; set; }
        public string? Ip { get; set; }
        public string? Device { get; set; }
    }

    public class AccountBotModel
    {
        public string? Channel { get; set; }
        public string? Username { get; set; }
        public string? DisplayName { get; set; }
        public string? Status { get; set; }
        public DateTime LinkedAt { get; set; }
        public DateTime? LastSeenAt { get; set; }
    }

    public class AccountBulkResultModel
    {
        public int Done { get; set; }
        public List<string> ListSkipped { get; set; } = [];
    }

    public class AccountPendingModel
    {
        public Guid ApprovalId { get; set; }
        public string? RequestNo { get; set; }
        public string? BusinessType { get; set; }
        public string? TypeName { get; set; }
        public Guid? EmployeeId { get; set; }
        public string? EmployeeName { get; set; }
        public string? EmployeeCode { get; set; }
        public string? PositionName { get; set; }
        public string? UnitName { get; set; }
        public string? RequestedByName { get; set; }
        public DateTime RequestedAt { get; set; }
        public List<string> ListGroup { get; set; } = [];
        public List<string> ListPreviousGroup { get; set; } = [];
        public bool BotLinked { get; set; }
        public bool CanApprove { get; set; }
    }
}

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// UI-6.42 (D-123): Tài khoản – danh sách có nhóm quyền / truy cập gần nhất / trạng thái, 4 chỉ số, thao tác hàng loạt (thêm / bỏ nhóm, khóa),
    /// tab Chờ IT xử lý (yêu cầu tài khoản D-82 đang chờ – duyệt qua cùng engine). Không xóa tài khoản; tạo tài khoản không gắn nhân viên chỉ quản trị.
    /// </summary>
    public partial class UserService
    {
        private T Svc<T>() where T : notnull => _services.GetRequiredService<T>();

        private async Task<bool> ActorIsAdminAsync()
            => _currentActor.Actor is { } a && (await _permissionService.GetAsync(a.UserId)).IsAdmin;

        private static string StatusName(string s) => s switch
        {
            "MUST_CHANGE" => "Phải đổi mật khẩu",
            "LOCKED" => "Đã khóa",
            "RESIGNED_OPEN" => "Nghỉ việc – còn mở",
            _ => "Hoạt động",
        };

        public async Task<IResult<AccountListModel>> GetList(AccountListRequest request)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                int warnDays = Svc<IOptions<BusinessRulesOptions>>().Value.AccountInactiveWarnDays;
                DateTime now = _clock.Now;
                var users = await db.User.AsNoTracking().Where(u => !u.IsDeleted).ToListAsync();
                var persons = await db.Person.AsNoTracking().Where(p => !p.IsDeleted).ToDictionaryAsync(p => p.Id, p => p.FullName);
                var emps = await db.Employee.AsNoTracking().Where(e => !e.IsDeleted && e.PersonId != null).ToListAsync();
                var empByPerson = emps.GroupBy(e => e.PersonId!.Value).ToDictionary(g => g.Key, g => g.First());
                var positions = await db.Position.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Name);
                var units = await db.OrganizationUnit.AsNoTracking().ToDictionaryAsync(o => o.Id, o => o.Name);
                var claims = await (from c in db.UserClaim.AsNoTracking()
                                    join g in db.UserGroup.AsNoTracking() on c.UserGroupId equals g.Id
                                    where !c.IsDeleted && !g.IsDeleted
                                    select new { c.UserId, g.Id, g.Name }).ToListAsync();
                var access = (await db.LocationTracking.AsNoTracking().Where(l => l.CreatedBy != null).GroupBy(l => l.CreatedBy!.Value)
                    .Select(g => new { g.Key, At = g.Max(x => x.CreatedDate) }).ToListAsync()).ToDictionary(x => x.Key, x => x.At);
                var bots = (await db.UserExternalIdentity.AsNoTracking().Where(i => !i.IsDeleted && i.UserId != null && (i.IdentityStatus == null || i.IdentityStatus == "Active"))
                    .Select(i => i.UserId!.Value).ToListAsync()).ToHashSet();
                var scope = await HrRules.OrganizationScopeAsync(db, request.OrganizationUnitId);
                var items = users.Select(u =>
                {
                    var e = u.PersonId is { } pid ? empByPerson.GetValueOrDefault(pid) : null;
                    string kind = !PersonLink.IsInternal(u.AccountType) ? "CUSTOMER" : e != null ? "EMPLOYEE" : "SYSTEM";
                    DateTime? last = access.TryGetValue(u.Id, out var at) ? at : null;
                    string status = !u.IsActive ? "LOCKED" : e != null && !EmployeeStatus.IsWorking(e.Status) ? "RESIGNED_OPEN" : EncriptFunctionsHelper.IsTemporary(u.PassWord) ? "MUST_CHANGE" : "ACTIVE";
                    return new AccountItemModel
                    {
                        Id = u.Id, UserName = u.UserName, Kind = kind, IsActive = u.IsActive, IsAdmin = u.IsAdmin,
                        FullName = e?.FullName ?? (u.PersonId is { } p2 ? persons.GetValueOrDefault(p2) : null),
                        EmployeeId = e?.Id, EmployeeCode = e?.Code ?? e?.EmployeeCode, EmployeeStatus = e?.Status,
                        PositionName = e?.PositionId is { } po ? positions.GetValueOrDefault(po) : null, UnitName = e?.OrganizationUnitId is { } ou ? units.GetValueOrDefault(ou) : null,
                        ListGroup = [.. claims.Where(c => c.UserId == u.Id).Select(c => c.Name ?? string.Empty).OrderBy(x => x)],
                        LastAccess = last, InactiveDays = last is { } l ? (int)(now - l).TotalDays : null, BotLinked = bots.Contains(u.Id),
                        Status = status, StatusName = StatusName(status),
                    };
                }).ToList();
                var m = new AccountListModel { InactiveWarnDays = warnDays };
                m.Active = items.Count(i => i.IsActive);
                m.ActiveEmployee = items.Count(i => i.IsActive && i.Kind == "EMPLOYEE");
                m.ActiveSystem = items.Count(i => i.IsActive && i.Kind == "SYSTEM");
                m.ResignedOpen = items.Count(i => i.Status == "RESIGNED_OPEN");
                bool Long(AccountItemModel i) => i.IsActive && (i.InactiveDays == null || i.InactiveDays >= warnDays);
                m.InactiveLong = items.Count(Long);
                m.PendingIt = await db.ApprovalRequest.AsNoTracking().CountAsync(a => !a.IsDeleted && AccountLifecycleCodes.BusinessTypes.Contains(a.BusinessType!) && (a.RequestStatus == "Pending" || a.RequestStatus == "InProgress"));
                IEnumerable<AccountItemModel> q = items;
                if (!string.IsNullOrWhiteSpace(request.Type))
                {
                    q = q.Where(i => string.Equals(i.Kind, request.Type, StringComparison.OrdinalIgnoreCase));
                }
                if (!string.IsNullOrWhiteSpace(request.Status))
                {
                    q = q.Where(i => string.Equals(i.Status, request.Status, StringComparison.OrdinalIgnoreCase));
                }
                if (request.GroupId is { } gid)
                {
                    var ids = claims.Where(c => c.Id == gid).Select(c => c.UserId).ToHashSet();
                    q = q.Where(i => ids.Contains(i.Id));
                }
                if (scope != null)
                {
                    var inUnit = emps.Where(e => e.OrganizationUnitId != null && scope.Contains(e.OrganizationUnitId.Value)).Select(e => e.Id).ToHashSet();
                    q = q.Where(i => i.EmployeeId != null && inUnit.Contains(i.EmployeeId.Value));
                }
                if (!string.IsNullOrWhiteSpace(request.Keyword))
                {
                    string k = request.Keyword.Trim();
                    q = q.Where(i => (i.UserName ?? "").Contains(k, StringComparison.OrdinalIgnoreCase) || (i.FullName ?? "").Contains(k, StringComparison.OrdinalIgnoreCase) || (i.EmployeeCode ?? "").Contains(k, StringComparison.OrdinalIgnoreCase));
                }
                q = request.Flag?.Trim().ToUpperInvariant() switch
                {
                    "RESIGNED_ACTIVE" => q.Where(i => i.Status == "RESIGNED_OPEN"),
                    "INACTIVE_LONG" => q.Where(Long),
                    _ => q,
                };
                m.ListItem = [.. q.OrderBy(i => i.Kind == "EMPLOYEE" ? 0 : 1).ThenBy(i => i.UserName)];
                return Result<AccountListModel>.Success(m, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetList: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<AccountBulkResultModel>> SetGroupsMany(AccountIdsRequest request)
        {
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var db = _unitOfWork.DbContext;
                var result = new AccountBulkResultModel();
                var users = await db.User.AsNoTracking().Where(u => request.UserIds.Contains(u.Id) && !u.IsDeleted).ToListAsync();
                var add = request.AddGroupIds.Distinct().ToList();
                var remove = request.RemoveGroupIds.Distinct().ToHashSet();
                if (add.Count == 0 && remove.Count == 0)
                {
                    _unitOfWork.RollbackTransaction();
                    return Result<AccountBulkResultModel>.Fail(ConstantHelper.Message.AccountBulkNoGroup);
                }
                foreach (var u in users)
                {
                    var current = await db.UserClaim.Where(c => c.UserId == u.Id && !c.IsDeleted).ToListAsync();
                    var after = current.Where(c => c.UserGroupId != null).Select(c => c.UserGroupId!.Value).Where(g => !remove.Contains(g)).Union(add).Distinct().ToList();
                    if (after.Count == 0)
                    {
                        result.ListSkipped.Add(string.Format(ConstantHelper.Message.AccountBulkNoGroupLeft, u.UserName));
                        continue;
                    }
                    foreach (var c in current.Where(c => c.UserGroupId is { } cg && remove.Contains(cg)))
                    {
                        c.IsDeleted = true;
                    }
                    foreach (var g in add.Where(g => !current.Any(c => c.UserGroupId == g)))
                    {
                        db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = u.Id, UserGroupId = g, CreatedDate = _clock.Now, CreatedBy = _currentActor.Actor?.UserId });
                    }
                    result.Done++;
                }
                await db.SaveChangesAsync(ConstantHelper.TypeSave.Update);
                _unitOfWork.CommitTransaction();
                foreach (var u in users)
                {
                    _permissionService.Invalidate(u.Id);
                }
                return Result<AccountBulkResultModel>.Success(result, string.Format(ConstantHelper.Message.AccountBulkGroupsDone, result.Done, result.ListSkipped.Count));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " SetGroupsMany: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        public async Task<IResult<AccountBulkResultModel>> LockMany(AccountIdsRequest request)
        {
            var db = _unitOfWork.DbContext;
            var result = new AccountBulkResultModel();
            Guid? me = _currentActor.Actor?.UserId;
            int admins = await db.User.CountAsync(u => !u.IsDeleted && u.IsActive && u.IsAdmin);
            var users = await db.User.Where(u => request.UserIds.Contains(u.Id) && !u.IsDeleted && u.IsActive).ToListAsync();
            foreach (var u in users)
            {
                if (u.Id == me)
                {
                    result.ListSkipped.Add(ConstantHelper.Message.CannotLockYourself);
                    continue;
                }
                if (u.IsAdmin && admins <= 1)
                {
                    result.ListSkipped.Add(string.Format(ConstantHelper.Message.AccountLastAdmin, u.UserName));
                    continue;
                }
                u.IsActive = false;
                if (u.IsAdmin)
                {
                    admins--;
                }
                result.Done++;
            }
            await db.SaveChangesAsync(ConstantHelper.TypeSave.Update);
            foreach (var u in users.Where(x => !x.IsActive))
            {
                _permissionService.Invalidate(u.Id);
            }
            return Result<AccountBulkResultModel>.Success(result, string.Format(ConstantHelper.Message.AccountBulkLockDone, result.Done, result.ListSkipped.Count));
        }

        /// <summary>UI-6.49 (7): trang chi tiết tài khoản – dòng như danh sách + nhóm + 30 lần truy cập gần nhất + liên kết bot.</summary>
        public async Task<IResult<AccountOverviewModel>> GetOverview(Guid id)
        {
            var db = _unitOfWork.DbContext;
            var user = await db.User.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted);
            if (user == null)
            {
                return Result<AccountOverviewModel>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
            }
            var list = await GetList(new AccountListRequest());
            var item = list.Data?.ListItem.FirstOrDefault(x => x.Id == id);
            if (item == null)
            {
                return Result<AccountOverviewModel>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
            }
            DateTime since = _clock.Now.AddDays(-30);
            var access = await db.LocationTracking.AsNoTracking().Where(l => l.CreatedBy == id && !l.IsDeleted).OrderByDescending(l => l.CreatedDate)
                .Take(30).Select(l => new AccountAccessModel { At = l.CreatedDate, Ip = l.IPv4, Device = l.PcName }).ToListAsync();
            var bots = await (from i in db.UserExternalIdentity.AsNoTracking()
                              join c in db.IntegrationChannel.AsNoTracking() on i.IntegrationChannelId equals c.Id into cj
                              from c in cj.DefaultIfEmpty()
                              where !i.IsDeleted && i.UserId == id
                              orderby i.LinkedAt descending
                              select new AccountBotModel
                              {
                                  Channel = c != null ? c.ChannelName ?? c.ChannelCode : null, Username = i.ExternalUsername, DisplayName = i.DisplayName,
                                  Status = i.IdentityStatus == null || i.IdentityStatus == "Active" ? "Đang liên kết" : "Đã hủy", LinkedAt = i.LinkedAt, LastSeenAt = i.LastSeenAt,
                              }).ToListAsync();
            var emp = user.PersonId is { } pid ? await db.Employee.AsNoTracking().Where(e => e.PersonId == pid && !e.IsDeleted).Select(e => (Guid?)e.Id).FirstOrDefaultAsync() : null;
            return Result<AccountOverviewModel>.Success(new AccountOverviewModel
            {
                Account = item, EmployeeId = emp,
                ListGroupId = await db.UserClaim.AsNoTracking().Where(c => !c.IsDeleted && c.UserId == id && c.UserGroupId != null).Select(c => c.UserGroupId!.Value).ToListAsync(),
                AccessCount30d = await db.LocationTracking.AsNoTracking().CountAsync(l => l.CreatedBy == id && !l.IsDeleted && l.CreatedDate >= since),
                ListAccess = access, ListBot = bots,
            }, ConstantHelper.Message.GetAllSuccessfully);
        }

        public async Task<IResults<AccountPendingModel>> GetPendingRequests()
        {
            try
            {
                var db = _unitOfWork.DbContext;
                var reqs = await db.ApprovalRequest.AsNoTracking().Where(a => !a.IsDeleted && AccountLifecycleCodes.BusinessTypes.Contains(a.BusinessType!) && (a.RequestStatus == "Pending" || a.RequestStatus == "InProgress"))
                    .OrderBy(a => a.RequestedAt).ToListAsync();
                var empIds = reqs.Where(r => r.BusinessId != null).Select(r => r.BusinessId!.Value).ToList();
                var emps = await db.Employee.AsNoTracking().Where(e => empIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id);
                var positions = await db.Position.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Name);
                var units = await db.OrganizationUnit.AsNoTracking().ToDictionaryAsync(o => o.Id, o => o.Name);
                var bots = (await db.UserExternalIdentity.AsNoTracking().Where(i => !i.IsDeleted && i.EmployeeId != null && (i.IdentityStatus == null || i.IdentityStatus == "Active")).Select(i => i.EmployeeId!.Value).ToListAsync()).ToHashSet();
                var names = await InfoPanelRules.UserNamesAsync(db, [.. reqs.Select(r => r.RequestedBy)]);
                var perms = await _permissionService.GetAsync(_currentActor.RequireActor().UserId);
                bool canApprove = _permissionService.IsAllowed(perms, AccountLifecycleCodes.ScreenId, PermissionAction.Approve) || perms.IsAdmin;
                var list = reqs.Select(r =>
                {
                    var e = r.BusinessId is { } id ? emps.GetValueOrDefault(id) : null;
                    var (_, proposal) = ApprovalRequestService.UnwrapPayload(r.PayloadJson);
                    AccountProposalModel? p = null;
                    try { p = proposal == null ? null : Newtonsoft.Json.JsonConvert.DeserializeObject<AccountProposalModel>(proposal); } catch { p = null; }
                    return new AccountPendingModel
                    {
                        ApprovalId = r.Id, RequestNo = r.RequestNo, BusinessType = r.BusinessType, TypeName = r.BusinessType switch
                        {
                            AccountLifecycleCodes.CreateBusinessType => "Tuyển mới", AccountLifecycleCodes.GroupBusinessType => "Đổi chức vụ", AccountLifecycleCodes.ReopenBusinessType => "Quay lại làm", _ => AccountLifecycleCodes.TypeLabel(r.BusinessType),
                        },
                        EmployeeId = e?.Id, EmployeeName = e?.FullName, EmployeeCode = e?.Code ?? e?.EmployeeCode,
                        PositionName = e?.PositionId is { } po ? positions.GetValueOrDefault(po) : null, UnitName = e?.OrganizationUnitId is { } ou ? units.GetValueOrDefault(ou) : null,
                        RequestedByName = names.NameOf(r.RequestedBy), RequestedAt = r.RequestedAt, ListGroup = p?.Groups ?? [], ListPreviousGroup = p?.PreviousGroups ?? [],
                        BotLinked = e != null && bots.Contains(e.Id), CanApprove = canApprove,
                    };
                }).ToList();
                return Results<AccountPendingModel>.Success(list, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetPendingRequests: ", ex.ToString()));
                throw;
            }
        }
    }
}
