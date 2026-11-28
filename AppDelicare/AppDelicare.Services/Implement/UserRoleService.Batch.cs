using AppDelicare.Data.Interface;
using AppDelicare.Data.Repositories;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using AppDelicare.Model.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace AppDelicare.Model.Model
{
    /// <summary>UI-6.43 (D-124): một màn trong tab "Phân quyền" – mức quyền thay ma trận 7 cờ.</summary>
    public class RoleLevelItemModel
    {
        public Guid ObjectMenuId { get; set; }
        public Guid? ParentId { get; set; }
        public string? ParentName { get; set; }
        public string? Name { get; set; }
        public string? MetaLink { get; set; }
        public int LevelShow { get; set; }
        public int OrderShow { get; set; }
        /// <summary>NONE · X (Xem) · IX (Xem, in / xuất) · S (Xem &amp; sửa) · Q (Toàn quyền) · CUSTOM (Tùy chỉnh).</summary>
        public string Level { get; set; } = UserRoleLevels.None;
        public bool AllowAccess { get; set; }
        public bool AllowAdd { get; set; }
        public bool AllowEdit { get; set; }
        public bool AllowDelete { get; set; }
        public bool AllowPrint { get; set; }
        public bool AllowExport { get; set; }
        public bool AllowImport { get; set; }
    }

    public class RoleLevelsModel
    {
        public Guid UserGroupId { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }
        public List<RoleLevelItemModel> ListItem { get; set; } = [];
        /// <summary>Mã mức → nhãn hiển thị (để FE không tự đặt tên).</summary>
        public Dictionary<string, string> Levels { get; set; } = UserRoleLevels.Labels;
    }

    public class RoleChangeItem
    {
        public Guid ObjectMenuId { get; set; }
        /// <summary>Mức (NONE / X / IX / S / Q). Trống hoặc CUSTOM → dùng 7 cờ gửi kèm.</summary>
        public string? Level { get; set; }
        public bool AllowAccess { get; set; }
        public bool AllowAdd { get; set; }
        public bool AllowEdit { get; set; }
        public bool AllowDelete { get; set; }
        public bool AllowPrint { get; set; }
        public bool AllowExport { get; set; }
        public bool AllowImport { get; set; }
    }

    public class RoleSaveManyRequest
    {
        public Guid UserGroupId { get; set; }
        public List<RoleChangeItem> Items { get; set; } = [];
    }

    public class RoleCompareItemModel
    {
        public Guid ObjectMenuId { get; set; }
        public string? ParentName { get; set; }
        public string? Name { get; set; }
        public string LevelA { get; set; } = UserRoleLevels.None;
        public string LevelB { get; set; } = UserRoleLevels.None;
    }

    public class RoleCompareModel
    {
        public string? GroupA { get; set; }
        public string? GroupB { get; set; }
        public int Same { get; set; }
        public List<RoleCompareItemModel> ListItem { get; set; } = [];
    }

    /// <summary>UI-6.43 (D-124): 7 cờ ↔ mức quyền. Quyền duyệt (D-31) = Truy cập + Sửa trên mục "Phê duyệt › loại" → mức "D" (Được duyệt); "Xem" = chỉ xem.</summary>
    public static class UserRoleLevels
    {
        public const string None = "NONE", View = "X", ViewPrint = "IX", Edit = "S", Full = "Q", Approve = "D", Custom = "CUSTOM";
        public static readonly Dictionary<string, string> Labels = new()
        {
            [None] = "Không", [View] = "Xem", [ViewPrint] = "Xem, in & xuất", [Edit] = "Xem & sửa", [Full] = "Toàn quyền", [Approve] = "Được duyệt", [Custom] = "Tùy chỉnh",
        };

        public static string Of(UserRole? r)
        {
            if (r == null || !r.AllowAccess)
            {
                return None;
            }
            var f = (r.AllowAdd, r.AllowEdit, r.AllowDelete, r.AllowPrint, r.AllowExport, r.AllowImport);
            return f switch
            {
                (false, false, false, false, false, false) => View,
                (false, true, false, false, false, false) => Approve,
                (false, false, false, true, true, false) => ViewPrint,
                (true, true, false, true, true, false) => Edit,
                (true, true, true, true, true, true) => Full,
                _ => Custom,
            };
        }

        /// <summary>Áp mức vào dòng quyền; false nếu mã mức không hợp lệ.</summary>
        public static bool Apply(string level, UserRole r)
        {
            switch (level.ToUpperInvariant())
            {
                case None: Set(r, false, false, false, false, false, false, false); return true;
                case View: Set(r, true, false, false, false, false, false, false); return true;
                case ViewPrint: Set(r, true, false, false, false, true, true, false); return true;
                case Edit: Set(r, true, true, true, false, true, true, false); return true;
                case Full: Set(r, true, true, true, true, true, true, true); return true;
                case Approve: Set(r, true, false, true, false, false, false, false); return true;
                default: return false;
            }
        }

        private static void Set(UserRole r, bool access, bool add, bool edit, bool del, bool print, bool export, bool import)
        {
            r.AllowAccess = access; r.AllowAdd = add; r.AllowEdit = edit; r.AllowDelete = del; r.AllowPrint = print; r.AllowExport = export; r.AllowImport = import;
        }
    }
}

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// UI-6.43 (D-124): tab "Phân quyền" của trang nhóm – mức quyền theo màn (GetLevels), lưu nhiều thay đổi một lần có xem lại (SaveMany – 1 giao dịch,
    /// ghi AuditChange, xóa bộ nhớ quyền của người trong nhóm), xem trước menu với thay đổi nháp (GetPreviewDraft), so sánh 2 nhóm (Compare).
    /// </summary>
    public partial class UserRoleService
    {
        private async Task<List<RoleLevelItemModel>> LevelsAsync(Guid userGroupId, IEnumerable<RoleChangeItem>? draft)
        {
            var menus = await ActiveMenusAsync();
            var rows = await _unitOfWork.UserRoleRepository.GetAll().AsNoTracking().Where(x => x.UserGroupId == userGroupId && !x.IsDeleted).ToListAsync();
            var changes = (draft ?? []).GroupBy(d => d.ObjectMenuId).ToDictionary(g => g.Key, g => g.Last());
            var names = menus.ToDictionary(m => m.Id, m => m.Name);
            return [.. menus.Where(m => m.LevelShow == 2 || m.LevelShow == 1).Select(m =>
            {
                var r = rows.FirstOrDefault(x => x.ObjectMenuId == m.Id) is { } e ? Clone(e) : new UserRole();
                if (changes.TryGetValue(m.Id, out var c))
                {
                    ApplyChange(c, r);
                }
                return new RoleLevelItemModel
                {
                    ObjectMenuId = m.Id, ParentId = m.ParentId, ParentName = m.ParentId != null && names.TryGetValue(m.ParentId.Value, out var p) ? p : null,
                    Name = m.Name, MetaLink = m.MetaLink, LevelShow = m.LevelShow ?? 0, OrderShow = m.OrderShow ?? 0, Level = UserRoleLevels.Of(r),
                    AllowAccess = r.AllowAccess, AllowAdd = r.AllowAdd, AllowEdit = r.AllowEdit, AllowDelete = r.AllowDelete,
                    AllowPrint = r.AllowPrint, AllowExport = r.AllowExport, AllowImport = r.AllowImport,
                };
            })];
        }

        private static UserRole Clone(UserRole e)
        {
            var r = new UserRole();
            CopyFlags(e, r);
            return r;
        }

        private static void ApplyChange(RoleChangeItem c, UserRole r)
        {
            if (string.IsNullOrWhiteSpace(c.Level) || string.Equals(c.Level, UserRoleLevels.Custom, StringComparison.OrdinalIgnoreCase) || !UserRoleLevels.Apply(c.Level, r))
            {
                r.AllowAccess = c.AllowAccess; r.AllowAdd = c.AllowAdd; r.AllowEdit = c.AllowEdit; r.AllowDelete = c.AllowDelete;
                r.AllowPrint = c.AllowPrint; r.AllowExport = c.AllowExport; r.AllowImport = c.AllowImport;
            }
            Normalize(r);
        }

        public async Task<IResult<RoleLevelsModel>> GetLevels(Guid userGroupId)
        {
            var g = await _unitOfWork.UserGroupRepository.GetAll().AsNoTracking().FirstOrDefaultAsync(x => x.Id == userGroupId && !x.IsDeleted);
            if (g == null)
            {
                return Result<RoleLevelsModel>.Fail(ConstantHelper.Message.GroupNotFound);
            }
            return Result<RoleLevelsModel>.Success(new RoleLevelsModel { UserGroupId = g.Id, Code = g.Code, Name = g.Name, ListItem = await LevelsAsync(userGroupId, null) }, ConstantHelper.Message.GetAllSuccessfully);
        }

        public async Task<IResult<int>> SaveMany(RoleSaveManyRequest request)
        {
            var g = await _unitOfWork.UserGroupRepository.GetAll().AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.UserGroupId && !x.IsDeleted);
            if (g == null)
            {
                return Result<int>.Fail(ConstantHelper.Message.GroupNotFound);
            }
            var menus = await ActiveMenusAsync();
            var bad = request.Items.Where(i => !menus.Any(m => m.Id == i.ObjectMenuId)
                || (!string.IsNullOrWhiteSpace(i.Level) && !UserRoleLevels.Labels.ContainsKey(i.Level.ToUpperInvariant()))).ToList();
            if (bad.Count != 0)
            {
                return Result<int>.Fail(string.Format(ConstantHelper.Message.MenuNotFound, string.Join(ConstantHelper.CommaSpace, bad.Select(b => b.ObjectMenuId))));
            }
            using var tran = await _unitOfWork.OpenTransaction();
            try
            {
                var db = _unitOfWork.DbContext;
                var rows = await db.UserRole.Where(x => x.UserGroupId == g.Id && !x.IsDeleted).ToListAsync();
                var log = new List<string>();
                int changed = 0;
                bool anyInsert = false;
                UserRole Row(Guid menuId)
                {
                    var row = rows.FirstOrDefault(x => x.ObjectMenuId == menuId);
                    if (row == null)
                    {
                        row = new UserRole { Id = Guid.NewGuid(), UserGroupId = g.Id, ObjectMenuId = menuId };
                        db.UserRole.Add(row);
                        rows.Add(row);
                        anyInsert = true;
                    }
                    return row;
                }
                foreach (var c in request.Items.GroupBy(i => i.ObjectMenuId).Select(x => x.Last()))
                {
                    var existing = rows.FirstOrDefault(x => x.ObjectMenuId == c.ObjectMenuId);
                    string before = UserRoleLevels.Of(existing);
                    var probe = existing == null ? new UserRole() : Clone(existing);
                    ApplyChange(c, probe);
                    if (existing != null && SameFlags(existing, probe) || existing == null && !probe.AllowAccess)
                    {
                        continue;
                    }
                    CopyFlags(probe, Row(c.ObjectMenuId));
                    changed++;
                    var m = menus.First(x => x.Id == c.ObjectMenuId);
                    log.Add(string.Concat("- ", m.Name, ": ", UserRoleLevels.Labels[before], " => ", UserRoleLevels.Labels[UserRoleLevels.Of(probe)]));
                    // Sidebar cần quyền Xem ở nhóm menu cấp 1 khi có màn con được xem.
                    if (probe.AllowAccess && m.ParentId is { } pid && menus.Any(x => x.Id == pid && x.LevelShow == 1))
                    {
                        var parent = Row(pid);
                        if (!parent.AllowAccess)
                        {
                            parent.AllowAccess = true;
                            log.Add(string.Concat("- ", menus.First(x => x.Id == pid).Name, ": bật Xem nhóm menu"));
                        }
                    }
                    // Màn con cuối cùng của nhóm menu bị bỏ quyền → tắt luôn nhóm menu (không để nhóm rỗng trên sidebar).
                    if (!probe.AllowAccess && m.ParentId is { } qid && menus.Any(x => x.Id == qid && x.LevelShow == 1)
                        && rows.FirstOrDefault(x => x.ObjectMenuId == qid) is { AllowAccess: true } pr
                        && !rows.Any(x => x.AllowAccess && menus.Any(k => k.Id == x.ObjectMenuId && k.ParentId == qid)))
                    {
                        CopyFlags(new UserRole(), pr);
                        log.Add(string.Concat("- ", menus.First(x => x.Id == qid).Name, ": tắt nhóm menu (không còn màn nào)"));
                    }
                }
                if (changed > 0)
                {
                    var actor = _services.GetService<ICurrentActor>()?.Actor?.UserId;
                    db.AuditChange.Add(new AuditChange
                    {
                        Id = Guid.NewGuid(), IdChange = g.Id, EntityName = nameof(UserGroup), Action = "RoleSaveMany",
                        Changes = string.Join("\n", log), CreatedDate = _services.GetRequiredService<IClock>().Now, CreatedBy = actor,
                    });
                    await db.SaveChangesAsync(anyInsert ? ConstantHelper.TypeSave.Insert : ConstantHelper.TypeSave.Update);
                }
                _unitOfWork.CommitTransaction();
                var users = await db.UserClaim.AsNoTracking().Where(c => !c.IsDeleted && c.UserGroupId == g.Id && c.UserId != null).Select(c => c.UserId!.Value).Distinct().ToListAsync();
                users.ForEach(_permissionService.Invalidate);
                return Result<int>.Success(changed, string.Format(ConstantHelper.Message.RoleSavedMany, changed));
            }
            catch (Exception ex)
            {
                Log.Error(string.Concat("[", GetType().Name, "]", " SaveMany: ", ex.ToString()));
                _unitOfWork.RollbackTransaction();
                throw;
            }
        }

        private static bool SameFlags(UserRole a, UserRole b)
            => a.AllowAccess == b.AllowAccess && a.AllowAdd == b.AllowAdd && a.AllowEdit == b.AllowEdit && a.AllowDelete == b.AllowDelete
               && a.AllowPrint == b.AllowPrint && a.AllowExport == b.AllowExport && a.AllowImport == b.AllowImport;

        /// <summary>Menu người trong nhóm sẽ thấy nếu lưu các thay đổi nháp (không ghi gì).</summary>
        public async Task<IResult<UserGroupPreviewModel>> GetPreviewDraft(RoleSaveManyRequest request)
        {
            var g = await _unitOfWork.UserGroupRepository.GetAll().AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.UserGroupId && !x.IsDeleted);
            if (g == null)
            {
                return Result<UserGroupPreviewModel>.Fail(ConstantHelper.Message.GroupNotFound);
            }
            var levels = await LevelsAsync(g.Id, request.Items);
            var menus = (await ActiveMenusAsync()).ToDictionary(m => m.Id);
            var visible = levels.Where(l => l.AllowAccess).ToList();
            // Nhóm menu cấp 1 hiện khi có màn con được xem.
            var parents = visible.Where(v => v.ParentId != null).Select(v => v.ParentId!.Value).ToHashSet();
            var flat = levels.Where(l => l.AllowAccess || (l.LevelShow == 1 && parents.Contains(l.ObjectMenuId)))
                .Select(l => ToViewModel(menus[l.ObjectMenuId], new UserRole
                {
                    AllowAccess = true, AllowAdd = l.AllowAdd, AllowEdit = l.AllowEdit, AllowDelete = l.AllowDelete, AllowPrint = l.AllowPrint, AllowExport = l.AllowExport, AllowImport = l.AllowImport,
                })).ToList();
            return Result<UserGroupPreviewModel>.Success(new UserGroupPreviewModel
            {
                UserGroupId = g.Id, UserGroupName = g.Name, ScreenCount = flat.Count(x => x.LevelShow == 2 && !MenuTabs.IsTab(x.MetaLink)), ListMenu = BuildTree(flat),
            }, ConstantHelper.Message.GetAllSuccessfully);
        }

        public async Task<IResult<RoleCompareModel>> Compare(Guid groupA, Guid groupB)
        {
            var groups = await _unitOfWork.UserGroupRepository.GetAll().AsNoTracking().Where(x => (x.Id == groupA || x.Id == groupB) && !x.IsDeleted).ToListAsync();
            var a = groups.FirstOrDefault(x => x.Id == groupA);
            var b = groups.FirstOrDefault(x => x.Id == groupB);
            if (a == null || b == null)
            {
                return Result<RoleCompareModel>.Fail(ConstantHelper.Message.GroupNotFound);
            }
            var la = await LevelsAsync(groupA, null);
            var lb = (await LevelsAsync(groupB, null)).ToDictionary(x => x.ObjectMenuId);
            var diff = la.Where(x => x.LevelShow == 2).Select(x => new RoleCompareItemModel
            {
                ObjectMenuId = x.ObjectMenuId, ParentName = x.ParentName, Name = x.Name, LevelA = x.Level, LevelB = lb.TryGetValue(x.ObjectMenuId, out var y) ? y.Level : UserRoleLevels.None,
            }).ToList();
            return Result<RoleCompareModel>.Success(new RoleCompareModel
            {
                GroupA = a.Name, GroupB = b.Name,
                Same = diff.Count(d => d.LevelA == d.LevelB && d.LevelA != UserRoleLevels.None),
                ListItem = [.. diff.Where(d => d.LevelA != d.LevelB)],
            }, ConstantHelper.Message.GetAllSuccessfully);
        }
    }
}
