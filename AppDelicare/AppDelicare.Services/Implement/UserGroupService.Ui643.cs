using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Model.Request;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;

namespace AppDelicare.Model.Model
{
    /// <summary>UI-6.43 (D-124): danh sách "Nhóm quyền" theo khối (UserGroup/GetList).</summary>
    public class UserGroupListModel
    {
        public List<UserGroupItemModel> ListItem { get; set; } = [];
        public int Groups { get; set; }
        public int Blocks { get; set; }
        public int AccountsWithGroup { get; set; }
        public int AccountsNoGroup { get; set; }
        public int EmptyGroups { get; set; }
        public int ChangedLast7Days { get; set; }
    }

    public class UserGroupItemModel
    {
        public Guid Id { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? GroupBlock { get; set; }
        public string? GroupBlockName { get; set; }
        public int Accounts { get; set; }
        public int Screens { get; set; }
        public int ApprovalTypes { get; set; }
        public DateTime? LastModified { get; set; }
        public string? LastModifiedBy { get; set; }
    }

    public class UserGroupUsersRequest
    {
        public Guid UserGroupId { get; set; }
        public List<Guid> UserIds { get; set; } = [];
    }

    public class UserGroupStatusRequest
    {
        public Guid Id { get; set; }
    }

    /// <summary>UI-6.43: trang chi tiết nhóm – thông tin, tài khoản trong nhóm, lịch sử (đổi nhóm + đổi quyền).</summary>
    public class UserGroupDetailModel
    {
        public UserGroupItemModel Group { get; set; } = new();
        public bool CodeLocked { get; set; }
        public List<UserGroupMemberRow> ListUser { get; set; } = [];
        public List<UserGroupHistoryRow> ListHistory { get; set; } = [];
    }

    public class UserGroupMemberRow
    {
        public Guid Id { get; set; }
        public string? UserName { get; set; }
        public string? FullName { get; set; }
        public bool IsActive { get; set; }
        public int GroupCount { get; set; }
    }

    public class UserGroupHistoryRow
    {
        public DateTime? At { get; set; }
        public string? By { get; set; }
        public string? Action { get; set; }
        public string? Changes { get; set; }
    }
}

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// UI-6.43 (D-124): nhóm quyền có Mã (Code – luồng duyệt / bot / nhóm gợi ý tìm theo mã), tên tiếng Việt, mô tả, khối.
    /// Insert / Update: tên không trùng; mã G2 không trùng, khóa khi đã dùng (bước duyệt / cấu hình); khối theo danh mục UserGroupBlock.
    /// </summary>
    public partial class UserGroupService
    {
        public const string BlockList = "UserGroupBlock";
        private T Svc<T>() where T : notnull => _services.GetRequiredService<T>();

        private Dictionary<string, string> BlockLabels()
            => Svc<IOptions<ReferenceListsOptions>>().Value.Lists.TryGetValue(BlockList, out var l)
                ? l.Where(x => x.Code != null).GroupBy(x => x.Code!, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().Label ?? g.Key, StringComparer.OrdinalIgnoreCase)
                : new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Mã đang được dùng ở bước duyệt hoặc cấu hình (nhóm gợi ý chức vụ, nhóm leo thang duyệt).</summary>
        private async Task<bool> CodeInUseAsync(string code)
        {
            var db = _unitOfWork.DbContext;
            if (await db.ApprovalStep.AsNoTracking().AnyAsync(s => !s.IsDeleted && s.ApproverRoleCode == code))
            {
                return true;
            }
            var rules = Svc<IOptions<BusinessRulesOptions>>().Value;
            bool inMap = rules.PositionDefaultGroups.Values.Any(v => (v ?? string.Empty).Split(',', ';').Select(x => x.Trim()).Contains(code, StringComparer.OrdinalIgnoreCase));
            var w = Svc<IOptionsMonitor<MessagingOptions>>().CurrentValue.Watch;
            IEnumerable<string> over = (w.ApprovalOverdueGroups.Count > 0 ? w.ApprovalOverdueGroups : WatchOptions.DefaultApprovalOverdueGroups)
                .Concat(w.ApprovalOverdueTopGroups.Count > 0 ? w.ApprovalOverdueTopGroups : WatchOptions.DefaultApprovalOverdueTopGroups);
            return inMap || over.Contains(code, StringComparer.OrdinalIgnoreCase);
        }

        private async Task<string?> RulesAsync(UserGroupRequest request, UserGroup? exist)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return ConstantHelper.Message.GroupNameRequired;
            }
            request.Name = request.Name.Trim();
            // Mã trống: nhóm cũ giữ mã; nhóm mới có tên đúng mẫu mã (vd. FIN-IT) → dùng tên làm mã.
            string? raw = string.IsNullOrWhiteSpace(request.Code) ? exist?.Code ?? (exist == null ? request.Name : exist.Name) : request.Code;
            var (code, codeError) = CodeRules.NormalizeUserCode(raw);
            if (codeError != null)
            {
                return string.IsNullOrWhiteSpace(request.Code) ? ConstantHelper.Message.GroupCodeRequired : codeError;
            }
            request.Code = code;
            var db = _unitOfWork.DbContext;
            if (await db.UserGroup.AsNoTracking().AnyAsync(g => !g.IsDeleted && g.Id != request.Id && g.Code == code))
            {
                return string.Format(ConstantHelper.Message.GroupCodeExist, code);
            }
            if (exist?.Code is { } old && !string.Equals(old, code, StringComparison.OrdinalIgnoreCase) && await CodeInUseAsync(old))
            {
                return string.Format(ConstantHelper.Message.GroupCodeLocked, old);
            }
            var blocks = BlockLabels();
            if (!string.IsNullOrWhiteSpace(request.GroupBlock) && blocks.Count > 0 && !blocks.ContainsKey(request.GroupBlock.Trim()))
            {
                return string.Format(ConstantHelper.Message.GroupBlockInvalid, string.Join(", ", blocks.Values));
            }
            request.GroupBlock = string.IsNullOrWhiteSpace(request.GroupBlock) ? exist?.GroupBlock : request.GroupBlock.Trim().ToUpperInvariant();
            request.Description = request.Description == null ? exist?.Description : request.Description.Trim();
            return null;
        }

        public async Task<IResult<UserGroupListModel>> GetList(string? keyword, bool emptyOnly)
        {
            try
            {
                var db = _unitOfWork.DbContext;
                var groups = await db.UserGroup.AsNoTracking().Where(g => !g.IsDeleted).ToListAsync();
                var claims = await db.UserClaim.AsNoTracking().Where(c => !c.IsDeleted && c.UserGroupId != null && c.UserId != null)
                    .Join(db.User.AsNoTracking().Where(u => !u.IsDeleted), c => c.UserId, u => (Guid?)u.Id, (c, u) => new { c.UserGroupId, u.Id }).ToListAsync();
                var roles = await (from r in db.UserRole.AsNoTracking()
                                   join om in db.ObjectMenu.AsNoTracking() on r.ObjectMenuId equals om.Id
                                   where !r.IsDeleted && r.AllowAccess && !om.IsDeleted && om.IsActive && om.MetaLink != null
                                   select new { r.UserGroupId, om.MetaLink, om.LevelShow, r.AllowEdit }).ToListAsync();
                var lastRole = (await db.UserRole.AsNoTracking().Where(r => !r.IsDeleted && r.UserGroupId != null).GroupBy(r => r.UserGroupId!.Value)
                    .Select(g => new { g.Key, At = g.Max(x => x.ModifiedDate ?? x.CreatedDate) }).ToListAsync()).ToDictionary(x => x.Key, x => x.At);
                var blocks = BlockLabels();
                DateTime since = Svc<IClock>().Now.AddDays(-7);
                var items = groups.Select(g =>
                {
                    DateTime? last = new[] { g.ModifiedDate ?? g.CreatedDate, lastRole.GetValueOrDefault(g.Id) }.Max();
                    return new UserGroupItemModel
                    {
                        Id = g.Id, Code = g.Code, Name = g.Name, Description = g.Description, GroupBlock = g.GroupBlock,
                        GroupBlockName = g.GroupBlock != null && blocks.TryGetValue(g.GroupBlock, out var b) ? b : g.GroupBlock == null ? "Khác" : g.GroupBlock,
                        Accounts = claims.Where(c => c.UserGroupId == g.Id).Select(c => c.Id).Distinct().Count(),
                        Screens = roles.Count(r => r.UserGroupId == g.Id && r.LevelShow == 2 && !r.MetaLink!.StartsWith("#/wf-inbox?")),
                        ApprovalTypes = roles.Count(r => r.UserGroupId == g.Id && r.AllowEdit && r.MetaLink!.StartsWith("#/wf-inbox?type=")), // D-31: duyệt = Truy cập + Sửa
                        LastModified = last,
                    };
                }).ToList();
                var users = await db.User.AsNoTracking().Where(u => !u.IsDeleted && u.IsActive).Select(u => u.Id).ToListAsync();
                var withGroup = claims.Select(c => c.Id).ToHashSet();
                var m = new UserGroupListModel
                {
                    Groups = items.Count, Blocks = items.Select(i => i.GroupBlock).Distinct().Count(), AccountsWithGroup = users.Count(withGroup.Contains), AccountsNoGroup = users.Count(u => !withGroup.Contains(u)),
                    EmptyGroups = items.Count(i => i.Accounts == 0), ChangedLast7Days = items.Count(i => i.LastModified >= since),
                };
                IEnumerable<UserGroupItemModel> q = items;
                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    string k = keyword.Trim();
                    q = q.Where(i => (i.Name ?? "").Contains(k, StringComparison.OrdinalIgnoreCase) || (i.Code ?? "").Contains(k, StringComparison.OrdinalIgnoreCase));
                }
                if (emptyOnly)
                {
                    q = q.Where(i => i.Accounts == 0);
                }
                string[] order = ["BOARD", "OPS", "SAL", "FIN", "QA"];
                m.ListItem = [.. q.OrderBy(i => i.GroupBlock == null ? 99 : Array.IndexOf(order, i.GroupBlock) is var x && x >= 0 ? x : 50).ThenBy(i => i.Name)];
                return Result<UserGroupListModel>.Success(m, ConstantHelper.Message.GetAllSuccessfully);
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " GetList: ", ex.ToString()));
                throw;
            }
        }

        public async Task<IResult<UserGroupDetailModel>> GetDetail(Guid id)
        {
            var list = await GetList(null, false);
            var item = list.Data?.ListItem.FirstOrDefault(x => x.Id == id);
            if (item == null)
            {
                return Result<UserGroupDetailModel>.Fail(ConstantHelper.Message.GroupNotFound);
            }
            var db = _unitOfWork.DbContext;
            var members = await (from c in db.UserClaim.AsNoTracking()
                                 join u in db.User.AsNoTracking() on c.UserId equals u.Id
                                 join p in db.Person.AsNoTracking() on u.PersonId equals p.Id into pj
                                 from p in pj.DefaultIfEmpty()
                                 where !c.IsDeleted && c.UserGroupId == id && !u.IsDeleted
                                 select new UserGroupMemberRow { Id = u.Id, UserName = u.UserName, FullName = p != null ? p.FullName : null, IsActive = u.IsActive }).Distinct().ToListAsync();
            var mids = members.Select(m => m.Id).ToList();
            var counts = await db.UserClaim.AsNoTracking().Where(c => !c.IsDeleted && c.UserId != null && mids.Contains(c.UserId.Value)).GroupBy(c => c.UserId!.Value).Select(g => new { g.Key, N = g.Count() }).ToListAsync();
            members.ForEach(m => m.GroupCount = counts.FirstOrDefault(c => c.Key == m.Id)?.N ?? 1);
            var roleIds = await db.UserRole.AsNoTracking().Where(r => r.UserGroupId == id).Select(r => r.Id).ToListAsync();
            var hist = await db.AuditChange.AsNoTracking().Where(a => !a.IsDeleted && a.IdChange != null && (a.IdChange == id || roleIds.Contains(a.IdChange.Value)))
                .OrderByDescending(a => a.CreatedDate).Take(100).ToListAsync();
            var byIds = hist.Where(h => h.CreatedBy != null).Select(h => h.CreatedBy!.Value).Distinct().ToList();
            var names = await db.User.AsNoTracking().Where(u => byIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.UserName);
            return Result<UserGroupDetailModel>.Success(new UserGroupDetailModel
            {
                Group = item,
                CodeLocked = item.Code != null && await CodeInUseAsync(item.Code),
                ListUser = [.. members.OrderBy(m => m.UserName)],
                ListHistory = [.. hist.Select(h => new UserGroupHistoryRow
                {
                    At = h.CreatedDate, By = h.CreatedBy != null && names.TryGetValue(h.CreatedBy.Value, out var n) ? n : null,
                    Action = h.Action == "RoleSaveMany" ? "Đổi quyền" : h.EntityName == nameof(UserRole) ? "Đổi quyền (ma trận cũ)" : h.Action == "Added" ? "Tạo nhóm" : "Sửa nhóm",
                    Changes = h.Changes,
                })],
            }, ConstantHelper.Message.GetAllSuccessfully);
        }

        public async Task<IResult<AccountBulkResultModel>> AddUsers(UserGroupUsersRequest request)
        {
            var db = _unitOfWork.DbContext;
            if (!await db.UserGroup.AnyAsync(g => g.Id == request.UserGroupId && !g.IsDeleted))
            {
                return Result<AccountBulkResultModel>.Fail(ConstantHelper.Message.GroupNotFound);
            }
            var r = new AccountBulkResultModel();
            foreach (var uid in request.UserIds.Distinct())
            {
                if (await db.UserClaim.AnyAsync(c => !c.IsDeleted && c.UserId == uid && c.UserGroupId == request.UserGroupId))
                {
                    continue;
                }
                db.UserClaim.Add(new UserClaim { Id = Guid.NewGuid(), UserId = uid, UserGroupId = request.UserGroupId, CreatedDate = Svc<IClock>().Now, CreatedBy = Svc<ICurrentActor>().Actor?.UserId });
                r.Done++;
            }
            await db.SaveChangesAsync(ConstantHelper.TypeSave.Update);
            request.UserIds.ForEach(u => Svc<IPermissionService>().Invalidate(u));
            return Result<AccountBulkResultModel>.Success(r, string.Format(ConstantHelper.Message.GroupUsersAdded, r.Done));
        }

        public async Task<IResult<AccountBulkResultModel>> RemoveUsers(UserGroupUsersRequest request)
        {
            var db = _unitOfWork.DbContext;
            var r = new AccountBulkResultModel();
            foreach (var uid in request.UserIds.Distinct())
            {
                var mine = await db.UserClaim.Where(c => !c.IsDeleted && c.UserId == uid).ToListAsync();
                var row = mine.FirstOrDefault(c => c.UserGroupId == request.UserGroupId);
                if (row == null)
                {
                    continue;
                }
                if (mine.Count <= 1)
                {
                    string name = await db.User.Where(u => u.Id == uid).Select(u => u.UserName).FirstOrDefaultAsync() ?? string.Empty;
                    r.ListSkipped.Add(string.Format(ConstantHelper.Message.AccountBulkNoGroupLeft, name));
                    continue;
                }
                row.IsDeleted = true;
                r.Done++;
            }
            await db.SaveChangesAsync(ConstantHelper.TypeSave.Update);
            request.UserIds.ForEach(u => Svc<IPermissionService>().Invalidate(u));
            return Result<AccountBulkResultModel>.Success(r, string.Format(ConstantHelper.Message.GroupUsersRemoved, r.Done, r.ListSkipped.Count));
        }

        /// <summary>Ngừng dùng nhóm (xóa mềm) – chặn khi còn tài khoản hoặc nhóm đang là người duyệt / trong cấu hình.</summary>
        public async Task<IResult<bool>> Retire(UserGroupStatusRequest request)
        {
            var db = _unitOfWork.DbContext;
            var g = await db.UserGroup.FirstOrDefaultAsync(x => x.Id == request.Id && !x.IsDeleted);
            if (g == null)
            {
                return Result<bool>.Fail(ConstantHelper.Message.GroupNotFound);
            }
            int accounts = await db.UserClaim.CountAsync(c => !c.IsDeleted && c.UserGroupId == g.Id && db.User.Any(u => u.Id == c.UserId && !u.IsDeleted));
            bool approver = (g.Code != null && await CodeInUseAsync(g.Code)) || (g.Name != null && await db.ApprovalStep.AnyAsync(s => !s.IsDeleted && s.ApproverRoleCode == g.Name));
            if (accounts > 0 || approver)
            {
                return Result<bool>.Fail(string.Format(ConstantHelper.Message.GroupRetireBlocked, accounts, approver ? "đang là người duyệt / trong cấu hình" : "không là người duyệt"));
            }
            g.IsDeleted = true;
            g.ModifiedDate = Svc<IClock>().Now;
            g.ModifiedBy = Svc<ICurrentActor>().Actor?.UserId;
            await db.SaveChangesAsync(ConstantHelper.TypeSave.Update);
            return Result<bool>.Success(true, ConstantHelper.Message.GroupRetired);
        }
    }
}
