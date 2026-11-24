#nullable enable
using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Services.Implement.Identity;
using AppDelicare.Services.Implement.Messaging;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Serilog;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// HR-ACC-1 (D-82 e–j): vòng đời tài khoản web của nhân viên.
    /// <list type="bullet">
    /// <item>Tuyển mới: NS đề nghị (ô "Cấp tài khoản web" / nút trên Nhân viên 360) → yêu cầu USER_ACCOUNT_CREATE cho FIN-IT, nhóm gợi ý theo chức danh
    /// (<c>BusinessRules:PositionDefaultGroups</c>), IT đổi nhóm được trước khi Duyệt → tạo Person (nếu chưa) + User INTERNAL (tên đăng nhập = mã NV, mật khẩu tạm 24 giờ,
    /// bắt đổi lần đầu – T45R.8) + UserClaim + Employee.PersonId; mật khẩu tạm gửi qua bot nếu nhân viên đã liên kết, không thì IT xem 1 lần (bộ nhớ 30′).</item>
    /// <item>Đổi chức danh → nhóm gợi ý mới khác nhóm hiện tại → USER_ACCOUNT_GROUP (giữ quyền cũ trong lúc chờ).</item>
    /// <item>Nghỉ việc → khóa ngay trong cùng transaction: User.IsActive = 0, thu hồi liên kết bot, xóa cache quyền (token đang có bị chặn ở request kế tiếp – PermissionFilter),
    /// yêu cầu người này đang giữ → leo thang (D-81 i), yêu cầu người này đã gửi → báo người duyệt. Quay lại Đang làm → USER_ACCOUNT_REOPEN.</item>
    /// <item>Sửa nhân viên → Person (họ tên, SĐT, ngày sinh, giới tính, địa chỉ) ghi cùng transaction (D-82 e).</item>
    /// </list>
    /// </summary>
    public class AccountLifecycleService(IUnitOfWork unitOfWork, IApprovalRequestService approvals, IPermissionService permissions, ICurrentActor currentActor,
        IClock clock, INotificationService notifications, IMemoryCache cache, IOptions<BusinessRulesOptions> rules) : IAccountLifecycleService
    {
        private readonly BusinessRulesOptions _rules = rules.Value;
        private Data.Connection.DatabaseContext Db => unitOfWork.DbContext;
        private static string CreatedKey(Guid employeeId) => "acct-created:" + employeeId.ToString("N");
        private static readonly TimeSpan CreatedTtl = TimeSpan.FromMinutes(30);

        // ================================================================ API

        public Task<IResult<Guid>> RequestCreate(EmployeeAccountRequest request) => RequestAsync(AccountLifecycleCodes.CreateBusinessType, request);

        public Task<IResult<Guid>> RequestReopen(EmployeeAccountRequest request) => RequestAsync(AccountLifecycleCodes.ReopenBusinessType, request);

        private async Task<IResult<Guid>> RequestAsync(string type, EmployeeAccountRequest request)
        {
            using var tran = await unitOfWork.OpenTransaction();
            try
            {
                var (id, error) = await CreateRequestAsync(type, request.EmployeeId, ParseGroups(request.Groups), request.Reason);
                if (error != null)
                {
                    unitOfWork.RollbackTransaction();
                    return Result<Guid>.Fail(error);
                }
                unitOfWork.CommitTransaction();
                return Result<Guid>.Success(id!.Value, string.Format("Đã gửi yêu cầu \"{0}\" cho IT duyệt", AccountLifecycleCodes.TypeLabel(type)));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " Request ", type, ": ", ex.ToString()));
                unitOfWork.RollbackTransaction();
                throw;
            }
        }

        public async Task<IResult<AccountCreatedModel>> CreateForEmployee(UserCreateForEmployeeRequest request)
        {
            using var tran = await unitOfWork.OpenTransaction();
            try
            {
                var groups = ParseGroups(request.Groups);
                var employee = await Db.Employee.AsNoTracking().FirstOrDefaultAsync(e => e.Id == request.EmployeeId && !e.IsDeleted);
                if (employee == null)
                {
                    unitOfWork.RollbackTransaction();
                    return Result<AccountCreatedModel>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
                }
                if (groups.Count == 0)
                {
                    groups = await SuggestedGroupsAsync(employee.PositionId);
                }
                string? error = await CreateAccountAsync(request.EmployeeId, groups);
                if (error != null)
                {
                    unitOfWork.RollbackTransaction();
                    return Result<AccountCreatedModel>.Fail(error);
                }
                unitOfWork.CommitTransaction();
                var created = cache.Get<AccountCreatedModel>(CreatedKey(request.EmployeeId));
                cache.Remove(CreatedKey(request.EmployeeId));
                return Result<AccountCreatedModel>.Success(created ?? new AccountCreatedModel(), ConstantHelper.Message.SaveSuccess);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " CreateForEmployee: ", ex.ToString()));
                unitOfWork.RollbackTransaction();
                throw;
            }
        }

        public Task<IResult<AccountCreatedModel>> TakeCreated(Guid employeeId)
        {
            var created = cache.Get<AccountCreatedModel>(CreatedKey(employeeId));
            cache.Remove(CreatedKey(employeeId));
            return Task.FromResult<IResult<AccountCreatedModel>>(created == null
                ? Result<AccountCreatedModel>.Fail("Không còn mật khẩu tạm để xem (đã xem, đã gửi qua bot hoặc quá 30 phút) – dùng Đặt lại mật khẩu ở màn Tài khoản")
                : Result<AccountCreatedModel>.Success(created, ConstantHelper.Message.GetAllSuccessfully));
        }

        public async Task<IResult<MyEmployeeLinkModel>> GetMyEmployee()
        {
            var actor = currentActor.RequireActor();
            var linked = await PersonLink.EmployeeOfUserAsync(Db, actor.UserId, legacy: false);
            var e = linked ?? await PersonLink.EmployeeOfUserAsync(Db, actor.UserId);
            return Result<MyEmployeeLinkModel>.Success(new MyEmployeeLinkModel
            {
                Linked = linked != null, Guessed = linked == null && e != null, EmployeeId = e?.Id, EmployeeCode = e?.Code ?? e?.EmployeeCode, EmployeeName = e?.FullName,
            }, ConstantHelper.Message.GetAllSuccessfully);
        }

        public IResult<Dictionary<string, List<string>>> GetPositionGroups()
            => Result<Dictionary<string, List<string>>>.Success(_rules.PositionDefaultGroups.ToDictionary(k => k.Key, k => ParseGroups(k.Value), StringComparer.OrdinalIgnoreCase),
                ConstantHelper.Message.GetAllSuccessfully);

        public async Task<IResult<EmployeeAccountModel>> GetEmployeeAccount(Guid employeeId)
        {
            try
            {
                var e = await Db.Employee.AsNoTracking().FirstOrDefaultAsync(x => x.Id == employeeId && !x.IsDeleted);
                if (e == null)
                {
                    return Result<EmployeeAccountModel>.Fail(ConstantHelper.Message.RecordNotFoundMessage);
                }
                return Result<EmployeeAccountModel>.Success(await AccountOfAsync(e), ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetEmployeeAccount: ", ex.ToString()));
                throw;
            }
        }

        public async Task<EmployeeAccountModel> AccountOfAsync(Employee e)
        {
            var m = new EmployeeAccountModel { SuggestedGroups = await SuggestedGroupsAsync(e.PositionId) };
            var user = await PersonLink.UserOfEmployeeAsync(Db, e);
            if (user != null)
            {
                m.UserId = user.Id;
                m.UserName = user.UserName;
                m.UserIsActive = user.IsActive;
                m.AccountType = user.AccountType;
                m.Linked = e.PersonId != null && user.PersonId == e.PersonId;
                m.Groups = await GroupsOfAsync(user.Id);
            }
            var pending = await Db.ApprovalRequest.AsNoTracking()
                .Where(a => !a.IsDeleted && a.BusinessId == e.Id && AccountLifecycleCodes.BusinessTypes.Contains(a.BusinessType!)
                            && (a.RequestStatus == "Pending" || a.RequestStatus == "InProgress"))
                .OrderByDescending(a => a.RequestedAt).FirstOrDefaultAsync();
            if (pending != null)
            {
                m.PendingRequestId = pending.Id;
                m.PendingRequestNo = pending.RequestNo;
                m.PendingType = pending.BusinessType;
                m.PendingTypeName = AccountLifecycleCodes.TypeLabel(pending.BusinessType);
            }
            m.Status = user == null ? (pending?.BusinessType == AccountLifecycleCodes.CreateBusinessType ? AccountLifecycleCodes.StatusPending : AccountLifecycleCodes.StatusNone)
                : pending?.BusinessType == AccountLifecycleCodes.ReopenBusinessType ? AccountLifecycleCodes.StatusPending
                : user.IsActive ? AccountLifecycleCodes.StatusActive : AccountLifecycleCodes.StatusLocked;
            m.StatusName = AccountLifecycleCodes.StatusLabel(m.Status);
            // Nút theo quyền người xem (D-82 j): NS đề nghị cấp / mở lại; IT tạo thẳng, khóa, đặt lại mật khẩu (màn Tài khoản).
            if (currentActor.Actor is { } actor)
            {
                var perms = await permissions.GetAsync(actor.UserId);
                bool hr = permissions.IsAllowed(perms, "adm-employee", PermissionAction.Edit);
                bool itAdd = permissions.IsAllowed(perms, AccountLifecycleCodes.ScreenId, PermissionAction.Add);
                bool itEdit = permissions.IsAllowed(perms, AccountLifecycleCodes.ScreenId, PermissionAction.Edit);
                bool working = EmployeeStatus.IsWorking(e.Status);
                if (user == null && pending == null && working && hr)
                {
                    m.Actions.Add("request");
                }
                if (user == null && working && itAdd)
                {
                    m.Actions.Add("create");
                }
                if (user is { IsActive: false } && pending == null && working && hr)
                {
                    m.Actions.Add("reopen");
                }
                if (user is { IsActive: true } && itEdit)
                {
                    m.Actions.Add("lock");
                    m.Actions.Add("reset");
                }
                if (pending != null && permissions.IsAllowed(perms, AccountLifecycleCodes.ScreenId, PermissionAction.Approve))
                {
                    m.Actions.Add("approve");
                }
            }
            return m;
        }

        public async Task<IResult<AccountLinkSummaryModel>> GetAccountLinks()
        {
            try
            {
                var users = await Db.User.AsNoTracking().Where(u => !u.IsDeleted).Select(u => new { u.Id, u.UserName, u.IsActive, u.AccountType, u.PersonId }).ToListAsync();
                var employees = await Db.Employee.AsNoTracking().Where(e => !e.IsDeleted).ToListAsync();
                var units = await Db.OrganizationUnit.AsNoTracking().Where(o => !o.IsDeleted).ToDictionaryAsync(o => o.Id, o => o.Name);
                var byPerson = employees.Where(e => e.PersonId != null).GroupBy(e => e.PersonId!.Value).ToDictionary(g => g.Key, g => g.First());
                var model = new AccountLinkSummaryModel();
                var withAccount = new HashSet<Guid>();
                foreach (var u in users.OrderBy(u => u.UserName))
                {
                    Employee? e = u.PersonId is { } pid && byPerson.TryGetValue(pid, out var linked) ? linked : null;
                    bool isLinked = e != null;
                    if (e == null && PersonLink.IsInternal(u.AccountType) && PersonLink.LegacyMatch)
                    {
                        e = await PersonLink.EmployeeOfUserAsync(Db, u.Id);
                    }
                    if (e != null)
                    {
                        withAccount.Add(e.Id);
                    }
                    var row = new AccountLinkRowModel
                    {
                        UserId = u.Id, UserName = u.UserName, IsActive = u.IsActive, AccountType = string.IsNullOrWhiteSpace(u.AccountType) ? PersonLink.Internal : u.AccountType,
                        KindName = AccountLifecycleCodes.KindLabel(u.AccountType, e != null),
                        EmployeeId = e?.Id, EmployeeCode = e?.Code ?? e?.EmployeeCode, EmployeeName = e?.FullName,
                        OrganizationUnitName = e?.OrganizationUnitId is { } ou ? units.GetValueOrDefault(ou) : null,
                        EmployeeStatus = e?.Status, EmployeeStatusName = e == null ? null : EmployeeStatus.IsWorking(e.Status) ? "Đang làm" : "Đã nghỉ",
                        Linked = isLinked,
                        ResignedButActive = e != null && u.IsActive && !EmployeeStatus.IsWorking(e.Status),
                    };
                    model.ListRow.Add(row);
                }
                model.ResignedButActiveCount = model.ListRow.Count(r => r.ResignedButActive);
                model.UnlinkedCount = model.ListRow.Count(r => !r.Linked && PersonLink.IsInternal(r.AccountType));
                model.EmployeeWithoutAccountCount = employees.Count(e => EmployeeStatus.IsWorking(e.Status) && !withAccount.Contains(e.Id));
                return Result<AccountLinkSummaryModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetAccountLinks: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<LinkReviewModel>> GetLinkReview(string? group)
        {
            try
            {
                var employees = await Db.Employee.AsNoTracking().Where(e => !e.IsDeleted).OrderBy(e => e.Code).ToListAsync();
                var units = await Db.OrganizationUnit.AsNoTracking().Where(o => !o.IsDeleted).ToDictionaryAsync(o => o.Id, o => o.Name);
                var model = new LinkReviewModel { LinkedCount = employees.Count(e => e.PersonId != null) };
                var rows = new List<LinkReviewRowModel>();
                foreach (var e in employees.Where(e => e.PersonId == null))
                {
                    var candidates = await PersonLink.CandidatesAsync(Db, e);
                    var free = candidates.Where(c => c.LinkedEmployee == null).ToList();
                    string g = candidates.Count == 0 ? "NONE" : free.Count == 1 && candidates.Count == 1 ? "SURE" : "MULTI";
                    rows.Add(new LinkReviewRowModel
                    {
                        EmployeeId = e.Id, EmployeeCode = e.Code ?? e.EmployeeCode, EmployeeName = e.FullName, Phone = e.Phone, Status = e.Status,
                        OrganizationUnitName = e.OrganizationUnitId is { } ou ? units.GetValueOrDefault(ou) : null,
                        Group = g, GroupName = g == "SURE" ? "Khớp chắc" : g == "MULTI" ? "Nhiều ứng viên" : "Không khớp",
                        ListCandidate = candidates.Select(c => new LinkCandidateModel
                        {
                            PersonId = c.PersonId, FullName = c.FullName, PhoneNumber = c.PhoneNumber, Sources = c.Sources,
                            SourceText = string.Join(", ", c.Sources.Select(s => s switch { "BOT" => "liên kết bot", "USERNAME" => "tên đăng nhập = mã NV", "PHONE" => "trùng SĐT", _ => s })),
                            UserId = c.UserId, UserName = c.UserName, UserIsActive = c.UserIsActive, LinkedEmployee = c.LinkedEmployee,
                        }).ToList(),
                    });
                }
                // Cùng 1 người là ứng viên chắc của ≥ 2 nhân viên → hạ xuống nhiều ứng viên (như script D82).
                foreach (var dup in rows.Where(r => r.Group == "SURE").GroupBy(r => r.ListCandidate[0].PersonId).Where(g => g.Count() > 1).SelectMany(g => g))
                {
                    dup.Group = "MULTI";
                    dup.GroupName = "Nhiều ứng viên";
                }
                model.SureCount = rows.Count(r => r.Group == "SURE");
                model.MultiCount = rows.Count(r => r.Group == "MULTI");
                model.NoneCount = rows.Count(r => r.Group == "NONE");
                model.ListRow = string.IsNullOrWhiteSpace(group) ? rows : rows.Where(r => string.Equals(r.Group, group.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
                return Result<LinkReviewModel>.Success(model, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetLinkReview: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<bool>> LinkPerson(EmployeeLinkPersonRequest request)
        {
            using var tran = await unitOfWork.OpenTransaction();
            try
            {
                string? error = await LinkPersonCoreAsync(request);
                if (error != null)
                {
                    unitOfWork.RollbackTransaction();
                    return Result<bool>.Fail(error);
                }
                unitOfWork.CommitTransaction();
                return Result<bool>.Success(true, request.Unlink ? "Đã gỡ liên kết nhân viên – con người" : "Đã gắn nhân viên với tài khoản / con người");
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " LinkPerson: ", ex.ToString()));
                unitOfWork.RollbackTransaction();
                throw;
            }
        }

        private async Task<string?> LinkPersonCoreAsync(EmployeeLinkPersonRequest request)
        {
            var e = await Db.Employee.FirstOrDefaultAsync(x => x.Id == request.EmployeeId && !x.IsDeleted);
            if (e == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            if (request.Unlink)
            {
                if (e.PersonId == null)
                {
                    return null;
                }
                e.PersonId = null;
                await Db.SaveChangesAsync(ConstantHelper.TypeSave.Update);
                return null;
            }
            if ((request.PersonId == null) == (request.UserId == null))
            {
                return ConstantHelper.Message.PersonLinkNeedOne;
            }
            Guid personId;
            if (request.UserId is { } uid)
            {
                var user = await Db.User.FirstOrDefaultAsync(u => u.Id == uid && !u.IsDeleted);
                if (user == null)
                {
                    return ConstantHelper.Message.RecordNotFoundMessage;
                }
                if (!PersonLink.IsInternal(user.AccountType))
                {
                    return ConstantHelper.Message.PersonLinkAccountNotInternal;
                }
                if (user.PersonId is { } existing)
                {
                    personId = existing;
                }
                else
                {
                    // Tài khoản chưa có hồ sơ Person (vd. tạo trước D-82) → tạo Person từ nhân viên rồi gắn cả hai.
                    var p = NewPersonFrom(e);
                    Db.Person.Add(p);
                    user.PersonId = p.Id;
                    personId = p.Id;
                }
            }
            else
            {
                personId = request.PersonId!.Value;
                if (!await Db.Person.AsNoTracking().AnyAsync(p => p.Id == personId && !p.IsDeleted))
                {
                    return ConstantHelper.Message.RecordNotFoundMessage;
                }
                var other = await Db.User.AsNoTracking().Where(u => u.PersonId == personId && !u.IsDeleted && u.AccountType != null && u.AccountType != PersonLink.Internal)
                    .Select(u => u.UserName).FirstOrDefaultAsync();
                if (other != null)
                {
                    return ConstantHelper.Message.PersonLinkAccountNotInternal;
                }
            }
            var taken = await Db.Employee.AsNoTracking().Where(x => x.PersonId == personId && x.Id != e.Id && !x.IsDeleted)
                .Select(x => (x.Code ?? x.EmployeeCode) + " – " + x.FullName).FirstOrDefaultAsync();
            if (taken != null)
            {
                return string.Format(ConstantHelper.Message.PersonLinkTaken, taken);
            }
            e.PersonId = personId;
            await Db.SaveChangesAsync(ConstantHelper.TypeSave.Update);
            if (await Db.User.AsNoTracking().Where(u => u.PersonId == personId && !u.IsDeleted).Select(u => (Guid?)u.Id).FirstOrDefaultAsync() is { } linkedUser)
            {
                permissions.Invalidate(linkedUser);
            }
            return null;
        }

        // ================================================================ trong transaction của nghiệp vụ khác

        public async Task<string?> AfterEmployeeSavedAsync(string? oldStatus, Guid? oldPositionId, Employee saved, bool requestAccount, string? accountGroups)
        {
            // D-82 (e): Person là gốc thông tin cá nhân – sửa nhân viên ghi cả Person (cùng transaction).
            if (saved.PersonId is { } pid && await Db.Person.FirstOrDefaultAsync(p => p.Id == pid && !p.IsDeleted) is { } person)
            {
                CopyToPerson(saved, person);
                await Db.SaveChangesAsync(ConstantHelper.TypeSave.Update);
            }
            bool wasWorking = oldStatus == null || EmployeeStatus.IsWorking(oldStatus), working = EmployeeStatus.IsWorking(saved.Status);
            if (wasWorking && !working)
            {
                await LockForResignedAsync(saved.Id);
                return null;
            }
            if (!working)
            {
                return null;
            }
            var user = await PersonLink.UserOfEmployeeAsync(Db, saved);
            if (!wasWorking && user is { IsActive: false })
            {
                var (_, reopenError) = await CreateRequestAsync(AccountLifecycleCodes.ReopenBusinessType, saved.Id, [], "Nhân viên quay lại làm việc");
                if (reopenError != null && reopenError != ConstantHelper.Message.ApprovalAlreadyPending)
                {
                    Log.Warning("[AccountLifecycle] Không tạo được yêu cầu mở lại tài khoản cho {Employee}: {Error}", saved.Id, reopenError);
                }
            }
            if (user == null && requestAccount)
            {
                var (_, createError) = await CreateRequestAsync(AccountLifecycleCodes.CreateBusinessType, saved.Id, ParseGroups(accountGroups), null);
                if (createError != null && createError != ConstantHelper.Message.ApprovalAlreadyPending)
                {
                    // Nhân viên vẫn lưu (EmployeeService.Save commit trước khi xét kết quả) – NS bấm lại "Đề nghị cấp" trên Nhân viên 360.
                    Log.Warning("[AccountLifecycle] Không tạo được yêu cầu cấp tài khoản cho {Employee}: {Error}", saved.Id, createError);
                }
            }
            // D-82 (g): đổi chức danh → nhóm gợi ý mới khác nhóm hiện tại → yêu cầu đổi nhóm (giữ quyền cũ trong lúc chờ).
            if (user is { IsActive: true } && oldPositionId != saved.PositionId && oldStatus != null)
            {
                var suggested = await SuggestedGroupsAsync(saved.PositionId);
                var current = await GroupsOfAsync(user.Id);
                if (suggested.Count > 0 && !new HashSet<string>(suggested, StringComparer.OrdinalIgnoreCase).SetEquals(current))
                {
                    var (_, groupError) = await CreateRequestAsync(AccountLifecycleCodes.GroupBusinessType, saved.Id, suggested, "Đổi chức danh");
                    if (groupError != null && groupError != ConstantHelper.Message.ApprovalAlreadyPending)
                    {
                        Log.Warning("[AccountLifecycle] Không tạo được yêu cầu đổi nhóm cho {Employee}: {Error}", saved.Id, groupError);
                    }
                }
            }
            return null;
        }

        public async Task<AccountLockResultModel> LockForResignedAsync(Guid employeeId)
        {
            var result = new AccountLockResultModel();
            var e = await Db.Employee.AsNoTracking().FirstOrDefaultAsync(x => x.Id == employeeId);
            if (e == null)
            {
                return result;
            }
            var found = await PersonLink.UserOfEmployeeAsync(Db, e);
            Guid? userId = found?.Id;
            List<Guid> held = [];
            if (userId is { } uid && found!.IsActive)
            {
                held = await approvals.OpenRequestsHeldByAsync(uid); // tính TRƯỚC khi khóa (sau khóa không còn trong danh sách người duyệt)
                var user = await Db.User.FirstAsync(u => u.Id == uid);
                user.IsActive = false;
                result.LockedUsers = 1;
            }
            var identities = await Db.UserExternalIdentity.Where(i => !i.IsDeleted && i.IdentityStatus != MessagingCode.Identity.Revoked
                                                                      && (i.EmployeeId == employeeId || (userId != null && i.UserId == userId))).ToListAsync();
            foreach (var i in identities)
            {
                i.IdentityStatus = MessagingCode.Identity.Revoked;
            }
            result.RevokedIdentities = identities.Count;
            if (result.LockedUsers > 0 || identities.Count > 0)
            {
                await Db.SaveChangesAsync(ConstantHelper.TypeSave.Update);
            }
            if (userId is { } locked)
            {
                permissions.Invalidate(locked); // request kế tiếp của token đang có → 401 (PermissionFilter)
                if (result.LockedUsers > 0)
                {
                    result.EscalatedRequests = await approvals.EscalateAfterLockAsync(held, locked);
                    result.NotifiedRequests = await approvals.NotifyRequesterLeftAsync(locked, e.FullName);
                    Log.Information("[AccountLifecycle] Khóa tài khoản {User} của nhân viên {Employee} (nghỉ việc) – thu hồi {Ids} liên kết bot, leo thang {Esc}, báo {Notified}",
                        locked, employeeId, identities.Count, result.EscalatedRequests, result.NotifiedRequests);
                }
            }
            return result;
        }

        public async Task<AccountLockResultModel> LockAllResignedAsync()
        {
            var total = new AccountLockResultModel();
            var resigned = await Db.Employee.AsNoTracking().Where(e => !e.IsDeleted && (e.Status == EmployeeStatus.Inactive || e.Status == EmployeeStatus.Resigned)).ToListAsync();
            var names = new List<string>();
            foreach (var e in resigned)
            {
                var user = await PersonLink.UserOfEmployeeAsync(Db, e);
                bool openIdentity = await Db.UserExternalIdentity.AsNoTracking().AnyAsync(i => !i.IsDeleted && i.IdentityStatus != MessagingCode.Identity.Revoked && i.EmployeeId == e.Id);
                if (user is not { IsActive: true } && !openIdentity)
                {
                    continue;
                }
                var r = await LockForResignedAsync(e.Id);
                total.LockedUsers += r.LockedUsers;
                total.RevokedIdentities += r.RevokedIdentities;
                total.EscalatedRequests += r.EscalatedRequests;
                total.NotifiedRequests += r.NotifiedRequests;
                if (r.LockedUsers > 0)
                {
                    names.Add($"{e.Code ?? e.EmployeeCode} – {e.FullName} ({user!.UserName})");
                }
            }
            if (names.Count > 0)
            {
                var it = await RecipientResolver.UsersInGroupsAsync(Db, ["FIN-IT"]);
                await BusinessNotifier.SafeEnqueueAsync(notifications, new NotificationRequest
                {
                    Template = NotificationCatalog.ManualText, UserIds = it, BotCode = MessagingCode.BotDieuHanh,
                    Data = new { text = $"🔒 Đã tự khóa {names.Count} tài khoản của nhân viên đã nghỉ (đổi trạng thái ngoài màn Nhân viên):\n" + string.Join("\n", names.Take(20).Select(n => "• " + n)) },
                    IdempotencyKey = $"ACCOUNT_LOCK_DAILY:{clock.Today:yyyyMMdd}",
                });
            }
            return total;
        }

        public async Task<string?> ApplyCreateAsync(Guid employeeId, string? proposalJson, Guid actorUserId)
        {
            var proposal = ParseProposal(proposalJson);
            return await CreateAccountAsync(employeeId, proposal.Groups);
        }

        public async Task<string?> ApplyGroupAsync(Guid employeeId, string? proposalJson)
        {
            var e = await Db.Employee.AsNoTracking().FirstOrDefaultAsync(x => x.Id == employeeId && !x.IsDeleted);
            var user = e == null ? null : await PersonLink.UserOfEmployeeAsync(Db, e);
            if (user == null)
            {
                return ConstantHelper.Message.AccountNoUser;
            }
            var (groupIds, error) = await GroupIdsAsync(ParseProposal(proposalJson).Groups);
            if (error != null)
            {
                return error;
            }
            await SetGroupsAsync(user.Id, groupIds);
            permissions.Invalidate(user.Id);
            return null;
        }

        public async Task<string?> ApplyReopenAsync(Guid employeeId)
        {
            var e = await Db.Employee.AsNoTracking().FirstOrDefaultAsync(x => x.Id == employeeId && !x.IsDeleted);
            if (e == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            if (!EmployeeStatus.IsWorking(e.Status))
            {
                return ConstantHelper.Message.AccountEmployeeNotWorking;
            }
            var found = await PersonLink.UserOfEmployeeAsync(Db, e);
            if (found == null)
            {
                return ConstantHelper.Message.AccountNoUser;
            }
            if (found.IsActive)
            {
                return null;
            }
            var user = await Db.User.FirstAsync(u => u.Id == found.Id);
            user.IsActive = true;
            await Db.SaveChangesAsync(ConstantHelper.TypeSave.Update);
            permissions.Invalidate(user.Id);
            return null;
        }

        // ================================================================ handler duyệt

        public async Task<string?> BuildStateAsync(string businessType, Guid employeeId)
        {
            var e = await Db.Employee.AsNoTracking().FirstOrDefaultAsync(x => x.Id == employeeId);
            if (e == null)
            {
                return null;
            }
            // Hash khi duyệt: nhân viên đổi mã / trạng thái / chức danh / người gắn sau khi gửi → phải gửi lại.
            return JsonConvert.SerializeObject(new
            {
                type = businessType, code = e.Code ?? e.EmployeeCode, status = e.Status, deleted = e.IsDeleted,
                position = businessType == AccountLifecycleCodes.GroupBusinessType ? e.PositionId : null, person = e.PersonId,
            });
        }

        public async Task<string?> DescribeAsync(string businessType, Guid employeeId)
        {
            var e = await Db.Employee.AsNoTracking().Where(x => x.Id == employeeId).Select(x => new { Code = x.Code ?? x.EmployeeCode, x.FullName }).FirstOrDefaultAsync();
            return e == null ? null : $"{AccountLifecycleCodes.TypeLabel(businessType)} – {e.Code} {e.FullName}";
        }

        public async Task<ApprovalSummaryModel?> SummaryAsync(string businessType, Guid employeeId, ApprovalSummaryContext ctx)
        {
            var e = await ctx.Db.Employee.AsNoTracking().FirstOrDefaultAsync(x => x.Id == employeeId);
            if (e == null)
            {
                return null;
            }
            var proposal = ParseProposal(ctx.ProposalJson);
            string? unit = e.OrganizationUnitId is { } ou ? await ctx.Db.OrganizationUnit.AsNoTracking().Where(o => o.Id == ou).Select(o => o.Name).FirstOrDefaultAsync() : null;
            var position = e.PositionId is { } po ? await ctx.Db.Position.AsNoTracking().Where(p => p.Id == po).Select(p => new { p.Code, p.Name }).FirstOrDefaultAsync() : null;
            var suggested = await SuggestedGroupsAsync(e.PositionId);
            var user = await PersonLink.UserOfEmployeeAsync(ctx.Db, e);
            string code = (e.Code ?? e.EmployeeCode ?? string.Empty).Trim();
            string login = code.ToLowerInvariant();
            var s = new ApprovalSummaryModel
            {
                DocumentNo = code,
                Counterparty = e.FullName,
                MainValue = businessType == AccountLifecycleCodes.ReopenBusinessType ? "Mở lại" : (proposal.Groups.Count == 0 ? "Chưa chọn nhóm" : string.Join(", ", proposal.Groups)),
                Title = $"{AccountLifecycleCodes.TypeLabel(businessType)} – {code} {e.FullName}{(unit != null ? " · " + unit : "")}",
                Reason = proposal.Reason ?? businessType switch
                {
                    AccountLifecycleCodes.CreateBusinessType => "Nhân viên mới cần tài khoản web",
                    AccountLifecycleCodes.GroupBusinessType => "Đổi chức danh – nhóm quyền gợi ý mới khác nhóm hiện tại",
                    _ => "Nhân viên quay lại làm việc",
                },
                OpenLink = "adm-employee-360?id=" + e.Id,
            };
            s.Info.Add(new() { Label = "Nhân viên", Value = $"{code} – {e.FullName}" });
            s.Info.Add(new() { Label = "Đơn vị", Value = unit ?? "–" });
            s.Info.Add(new() { Label = "Chức danh", Value = position == null ? "–" : $"{position.Name} ({position.Code})" });
            s.Info.Add(new() { Label = "Trạng thái làm việc", Value = EmployeeStatus.IsWorking(e.Status) ? "Đang làm" : "Đã nghỉ" });
            s.Info.Add(new() { Label = "Nhóm gợi ý theo chức danh", Value = suggested.Count == 0 ? "Chưa cấu hình (BusinessRules:PositionDefaultGroups)" : string.Join(", ", suggested) });
            if (businessType == AccountLifecycleCodes.CreateBusinessType)
            {
                s.Info.Add(new() { Label = "Tên đăng nhập sẽ tạo", Value = login.Length == 0 ? "–" : login });
                if (login.Length > 0 && await ctx.Db.User.AsNoTracking().AnyAsync(u => !u.IsDeleted && u.UserName != null && u.UserName.ToLower() == login))
                {
                    s.Flags.Add(new() { Text = string.Format(ConstantHelper.Message.AccountUserNameTaken, login) });
                }
            }
            else
            {
                s.Info.Add(new() { Label = "Tài khoản", Value = user == null ? "Chưa có" : $"{user.UserName} ({(user.IsActive ? "đang mở" : "đã khóa")})" });
                if (user != null)
                {
                    s.Info.Add(new() { Label = "Nhóm hiện tại", Value = string.Join(", ", await GroupsOfAsync(user.Id)) });
                }
            }
            if (!EmployeeStatus.IsWorking(e.Status))
            {
                s.Flags.Add(new() { Text = ConstantHelper.Message.AccountEmployeeNotWorking });
            }
            if (businessType != AccountLifecycleCodes.ReopenBusinessType)
            {
                if (proposal.Groups.Count == 0)
                {
                    s.Flags.Add(new() { Text = "Chưa có nhóm quyền – chọn nhóm trước khi duyệt" });
                }
                var options = await ctx.Db.UserGroup.AsNoTracking().Where(g => !g.IsDeleted && g.Name != null).OrderBy(g => g.Name).Select(g => g.Name!).ToListAsync();
                s.Edit = new ApprovalEditModel
                {
                    Kind = "groups", Label = "Nhóm quyền sẽ cấp (IT đổi được trước khi Duyệt)", Selected = proposal.Groups, Options = options,
                    ProposalJson = JsonConvert.SerializeObject(proposal),
                };
                s.LineHeaders = ["Nhóm quyền", "Đề xuất", "Gợi ý chức danh"];
                foreach (string g in proposal.Groups.Union(suggested, StringComparer.OrdinalIgnoreCase))
                {
                    s.Lines.Add(new ApprovalLineModel
                    {
                        Cells = [g, proposal.Groups.Contains(g, StringComparer.OrdinalIgnoreCase) ? "✓" : "", suggested.Contains(g, StringComparer.OrdinalIgnoreCase) ? "✓" : ""],
                        Highlight = !suggested.Contains(g, StringComparer.OrdinalIgnoreCase),
                    });
                }
                s.LineCount = s.Lines.Count;
            }
            return s;
        }

        public async Task<(string? Proposal, string? Error)> ReviseProposalAsync(string? submitted, string revised)
        {
            AccountProposalModel next;
            try
            {
                next = JsonConvert.DeserializeObject<AccountProposalModel>(revised) ?? new AccountProposalModel();
            }
            catch (JsonException)
            {
                // Cho phép gửi thẳng danh sách nhóm "A, B".
                next = new AccountProposalModel { Groups = ParseGroups(revised) };
            }
            var old = ParseProposal(submitted);
            old.Groups = next.Groups.Select(g => g.Trim()).Where(g => g.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (old.Groups.Count == 0)
            {
                return (null, ConstantHelper.Message.AccountNoGroup);
            }
            var (_, error) = await GroupIdsAsync(old.Groups);
            return error != null ? (null, error) : (JsonConvert.SerializeObject(old), null);
        }

        // ================================================================ nội bộ

        private async Task<(Guid? Id, string? Error)> CreateRequestAsync(string type, Guid employeeId, List<string> groups, string? reason)
        {
            var e = await Db.Employee.AsNoTracking().FirstOrDefaultAsync(x => x.Id == employeeId && !x.IsDeleted);
            if (e == null)
            {
                return (null, ConstantHelper.Message.RecordNotFoundMessage);
            }
            if (!EmployeeStatus.IsWorking(e.Status))
            {
                return (null, ConstantHelper.Message.AccountEmployeeNotWorking);
            }
            var user = await PersonLink.UserOfEmployeeAsync(Db, e);
            if (type == AccountLifecycleCodes.CreateBusinessType)
            {
                if (user != null)
                {
                    return (null, string.Format(ConstantHelper.Message.AccountAlreadyExists, user.UserName));
                }
                if (string.IsNullOrWhiteSpace(e.Code ?? e.EmployeeCode))
                {
                    return (null, ConstantHelper.Message.AccountEmployeeNoCode);
                }
            }
            else if (user == null)
            {
                return (null, ConstantHelper.Message.AccountNoUser);
            }
            else if (type == AccountLifecycleCodes.ReopenBusinessType && user.IsActive)
            {
                return (null, ConstantHelper.Message.AccountNotLocked);
            }
            // D-82 (f): 1 yêu cầu tài khoản đang mở / nhân viên (mọi loại).
            if (await Db.ApprovalRequest.AsNoTracking().AnyAsync(a => !a.IsDeleted && a.BusinessId == employeeId && AccountLifecycleCodes.BusinessTypes.Contains(a.BusinessType!)
                    && (a.RequestStatus == "Pending" || a.RequestStatus == "InProgress")))
            {
                return (null, ConstantHelper.Message.ApprovalAlreadyPending);
            }
            var position = e.PositionId is { } po ? await Db.Position.AsNoTracking().Where(p => p.Id == po).Select(p => new { p.Code, p.Name }).FirstOrDefaultAsync() : null;
            if (groups.Count == 0 && type != AccountLifecycleCodes.ReopenBusinessType)
            {
                groups = await SuggestedGroupsAsync(e.PositionId);
            }
            if (groups.Count > 0)
            {
                var (_, groupError) = await GroupIdsAsync(groups);
                if (groupError != null)
                {
                    return (null, groupError);
                }
            }
            var proposal = new AccountProposalModel
            {
                Groups = groups, PreviousGroups = user == null ? [] : await GroupsOfAsync(user.Id), PositionCode = position?.Code, PositionName = position?.Name,
                Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            };
            var (request, error) = await approvals.CreateAsync(type, employeeId, JsonConvert.SerializeObject(proposal));
            return error != null ? (null, error) : (request!.Id, null);
        }

        /// <summary>Tạo Person (nếu chưa) + User INTERNAL + nhóm + liên kết; mật khẩu tạm gửi bot hoặc giữ 30′ cho IT xem 1 lần.</summary>
        private async Task<string?> CreateAccountAsync(Guid employeeId, List<string> groups)
        {
            var e = await Db.Employee.FirstOrDefaultAsync(x => x.Id == employeeId && !x.IsDeleted);
            if (e == null)
            {
                return ConstantHelper.Message.RecordNotFoundMessage;
            }
            if (!EmployeeStatus.IsWorking(e.Status))
            {
                return ConstantHelper.Message.AccountEmployeeNotWorking;
            }
            if (await PersonLink.UserOfEmployeeAsync(Db, e) is { } existing)
            {
                return string.Format(ConstantHelper.Message.AccountAlreadyExists, existing.UserName);
            }
            // Tên đăng nhập = mã NV, chữ thường như màn Tài khoản (UserService.ValidateNewAccount) – đăng nhập không phân biệt hoa thường.
            string userName = (e.Code ?? e.EmployeeCode ?? string.Empty).Trim().ToLowerInvariant();
            if (userName.Length == 0)
            {
                return ConstantHelper.Message.AccountEmployeeNoCode;
            }
            if (await Db.User.AsNoTracking().AnyAsync(u => !u.IsDeleted && u.UserName != null && u.UserName.ToLower() == userName))
            {
                return string.Format(ConstantHelper.Message.AccountUserNameTaken, userName);
            }
            if (groups.Count == 0)
            {
                return ConstantHelper.Message.AccountNoGroup;
            }
            var (groupIds, groupError) = await GroupIdsAsync(groups);
            if (groupError != null)
            {
                return groupError;
            }
            Person? person = e.PersonId is { } pid ? await Db.Person.FirstOrDefaultAsync(p => p.Id == pid && !p.IsDeleted) : null;
            if (person == null)
            {
                person = NewPersonFrom(e);
                Db.Person.Add(person);
                e.PersonId = person.Id;
            }
            if (await Db.User.AsNoTracking().AnyAsync(u => u.PersonId == person.Id && !u.IsDeleted && (u.AccountType == null || u.AccountType == PersonLink.Internal)))
            {
                return string.Format(ConstantHelper.Message.PersonLinkUserTaken, userName);
            }
            string temp = EncriptFunctionsHelper.GenerateTemporaryPassword();
            var user = new User
            {
                Id = Guid.NewGuid(), UserName = userName, PersonId = person.Id, IsActive = true, IsAdmin = false, IsDefault = false, AccountType = PersonLink.Internal,
                PassWord = EncriptFunctionsHelper.GenerateTemporaryPasswordHash(temp, clock.UtcNow.Add(EncriptFunctionsHelper.TemporaryPasswordLifetime)),
            };
            Db.User.Add(user);
            await Db.SaveChangesAsync(ConstantHelper.TypeSave.Insert);
            await SetGroupsAsync(user.Id, groupIds);
            // Mật khẩu tạm: qua bot nếu nhân viên đã liên kết (định danh có EmployeeId), không thì IT xem 1 lần (không log, không lưu bản rõ ngoài tin bot).
            int sent = await BusinessNotifier.SafeEnqueueAsync(notifications, new NotificationRequest
            {
                Template = NotificationCatalog.ManualText, EmployeeIds = [e.Id], BotCode = MessagingCode.BotVanHanh,
                Data = new { text = $"🔐 Tài khoản web của bạn đã được tạo.\nTên đăng nhập: {userName}\nMật khẩu tạm: {temp}\nĐổi mật khẩu khi đăng nhập lần đầu (hạn 24 giờ)." },
                IdempotencyKey = $"ACCOUNT_CREATED:{user.Id:N}",
            });
            cache.Set(CreatedKey(e.Id), new AccountCreatedModel
            {
                UserId = user.Id, UserName = userName, Groups = groups, SentByBot = sent > 0, TemporaryPassword = sent > 0 ? null : temp,
            }, CreatedTtl);
            Log.Information("[AccountLifecycle] Tạo tài khoản {UserName} ({UserId}) cho nhân viên {Employee}, nhóm {Groups}, gửi bot: {Sent}", userName, user.Id, e.Id, groups, sent > 0);
            return null;
        }

        private static Person NewPersonFrom(Employee e)
        {
            var p = new Person { Id = Guid.NewGuid() };
            CopyToPerson(e, p);
            return p;
        }

        /// <summary>D-82 (e): bản sao trên Employee giữ nguyên tên cột cũ; Person là gốc – cùng giá trị.</summary>
        private static void CopyToPerson(Employee e, Person p)
        {
            p.FullName = e.FullName;
            p.PhoneNumber = e.Phone;
            p.DateOfBirth = e.DateOfBirth;
            p.Sex = e.Gender;
            p.Address = e.Address;
        }

        private async Task SetGroupsAsync(Guid userId, IReadOnlyCollection<Guid> groupIds)
        {
            var claims = await Db.UserClaim.Where(c => c.UserId == userId && !c.IsDeleted).ToListAsync();
            foreach (var c in claims.Where(c => c.UserGroupId is not { } g || !groupIds.Contains(g)))
            {
                c.IsDeleted = true;
            }
            foreach (Guid g in groupIds.Where(g => !claims.Any(c => c.UserGroupId == g)))
            {
                Db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = userId, UserGroupId = g });
            }
            await Db.SaveChangesAsync(ConstantHelper.TypeSave.Update);
        }

        private async Task<(List<Guid> Ids, string? Error)> GroupIdsAsync(IReadOnlyCollection<string> names)
        {
            var wanted = names.Select(n => n.Trim()).Where(n => n.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var found = await Db.UserGroup.AsNoTracking().Where(g => !g.IsDeleted && g.Name != null && wanted.Contains(g.Name)).Select(g => new { g.Id, g.Name }).ToListAsync();
            var missing = wanted.Where(w => !found.Any(f => string.Equals(f.Name, w, StringComparison.OrdinalIgnoreCase))).ToList();
            return missing.Count > 0 ? ([], string.Format(ConstantHelper.Message.AccountGroupUnknown, string.Join(", ", missing))) : (found.Select(f => f.Id).ToList(), null);
        }

        private async Task<List<string>> GroupsOfAsync(Guid userId)
            => await (from c in Db.UserClaim.AsNoTracking()
                      join g in Db.UserGroup.AsNoTracking() on c.UserGroupId equals g.Id
                      where c.UserId == userId && !c.IsDeleted && !g.IsDeleted && g.Name != null
                      orderby g.Name
                      select g.Name!).ToListAsync();

        private async Task<List<string>> SuggestedGroupsAsync(Guid? positionId)
        {
            if (positionId is not { } po || _rules.PositionDefaultGroups.Count == 0)
            {
                return [];
            }
            string? code = await Db.Position.AsNoTracking().Where(p => p.Id == po).Select(p => p.Code).FirstOrDefaultAsync();
            return code != null && _rules.PositionDefaultGroups.TryGetValue(code.Trim(), out var groups) ? ParseGroups(groups) : [];
        }

        private static List<string> ParseGroups(string? text)
            => (text ?? string.Empty).Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        private static AccountProposalModel ParseProposal(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new AccountProposalModel();
            }
            try
            {
                return JsonConvert.DeserializeObject<AccountProposalModel>(json) ?? new AccountProposalModel();
            }
            catch (JsonException)
            {
                return new AccountProposalModel();
            }
        }
    }
}
