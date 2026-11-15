using AppDelicare.Application.Common;
using AppDelicare.Data.Connection;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Application.Admin
{
    /// <summary>Cờ quyền trên một menu.</summary>
    public sealed record MenuFlags(bool AllowAccess, bool AllowAdd, bool AllowEdit, bool AllowDelete, bool AllowPrint, bool AllowExport, bool AllowImport)
    {
        public static readonly MenuFlags None = new(false, false, false, false, false, false, false);
        public bool Any => AllowAccess || AllowAdd || AllowEdit || AllowDelete || AllowPrint || AllowExport || AllowImport;

        /// <summary>Spec 3.3: bật bất kỳ cờ nào → tự bật AllowAccess; tắt AllowAccess → tắt toàn bộ.</summary>
        public MenuFlags Normalize()
        {
            bool other = AllowAdd || AllowEdit || AllowDelete || AllowPrint || AllowExport || AllowImport;
            if (!AllowAccess && !other)
            {
                return None;
            }
            if (!AllowAccess && other)
            {
                return this with { AllowAccess = true };
            }
            return this;
        }

        public MenuFlags Or(MenuFlags o) => new(AllowAccess || o.AllowAccess, AllowAdd || o.AllowAdd, AllowEdit || o.AllowEdit,
            AllowDelete || o.AllowDelete, AllowPrint || o.AllowPrint, AllowExport || o.AllowExport, AllowImport || o.AllowImport);

        public static MenuFlags Of(UserRole r) => new(r.AllowAccess, r.AllowAdd, r.AllowEdit, r.AllowDelete, r.AllowPrint, r.AllowExport, r.AllowImport);
    }

    /// <summary>Một dòng của cây menu kèm cờ quyền.</summary>
    public sealed record MenuPermissionNode(Guid MenuId, string? Name, string? MetaLink, Guid? ParentId, int? LevelShow, int? OrderShow, string? IconName,
        bool AllowAccess, bool AllowAdd, bool AllowEdit, bool AllowDelete, bool AllowPrint, bool AllowExport, bool AllowImport,
        IReadOnlyList<string>? GrantedBy = null)
    {
        public List<MenuPermissionNode> Children { get; } = [];
    }

    /// <summary>Phát sau khi quyền của nhóm / thành viên nhóm thay đổi → WebApi xóa cache quyền của các user liên quan.</summary>
    public sealed record PermissionsChanged(IReadOnlyList<Guid> UserIds) : IDomainEvent;

    internal static class MenuTree
    {
        public static async Task<List<ObjectMenu>> ActiveMenusAsync(DatabaseContext db, CancellationToken ct)
            => await db.ObjectMenu.AsNoTracking().Where(m => !m.IsDeleted && m.IsActive)
                .OrderBy(m => m.LevelShow).ThenBy(m => m.OrderShow).ThenBy(m => m.Name).ToListAsync(ct);

        /// <summary>Dựng cây theo ParentId; nút cha không có trong danh sách → nút gốc.</summary>
        public static List<MenuPermissionNode> Build(IEnumerable<MenuPermissionNode> nodes)
        {
            var list = nodes.ToList();
            var byId = list.ToDictionary(n => n.MenuId);
            var roots = new List<MenuPermissionNode>();
            foreach (var n in list)
            {
                if (n.ParentId is { } p && byId.TryGetValue(p, out var parent) && parent != n)
                {
                    parent.Children.Add(n);
                }
                else
                {
                    roots.Add(n);
                }
            }
            return roots;
        }

        public static MenuPermissionNode Node(ObjectMenu m, MenuFlags f, IReadOnlyList<string>? grantedBy = null)
            => new(m.Id, m.Name, m.MetaLink, m.ParentId, m.LevelShow, m.OrderShow, m.IconName,
                f.AllowAccess, f.AllowAdd, f.AllowEdit, f.AllowDelete, f.AllowPrint, f.AllowExport, f.AllowImport, grantedBy);

        public static async Task<Dictionary<Guid, MenuFlags>> GroupFlagsAsync(DatabaseContext db, IReadOnlyCollection<Guid> groupIds, CancellationToken ct)
        {
            var rows = await db.UserRole.AsNoTracking()
                .Where(r => !r.IsDeleted && r.UserGroupId != null && groupIds.Contains(r.UserGroupId.Value) && r.ObjectMenuId != null)
                .ToListAsync(ct);
            return rows.GroupBy(r => r.ObjectMenuId!.Value)
                .ToDictionary(g => g.Key, g => g.Select(MenuFlags.Of).Aggregate(MenuFlags.None, (a, b) => a.Or(b)));
        }

        public static async Task<Guid> RequireGroupAsync(DatabaseContext db, Guid groupId, CancellationToken ct)
            => await db.UserGroup.AsNoTracking().AnyAsync(g => g.Id == groupId && !g.IsDeleted, ct)
                ? groupId
                : throw new BusinessRuleException(404, "NOT_FOUND", "Không tìm thấy nhóm người dùng.");

        public static Task<List<Guid>> UsersOfGroupAsync(DatabaseContext db, Guid groupId, CancellationToken ct)
            => db.UserClaim.AsNoTracking().Where(c => !c.IsDeleted && c.UserGroupId == groupId && c.UserId != null)
                .Select(c => c.UserId!.Value).Distinct().ToListAsync(ct);
    }

    // ---------------------------------------------------------------- GET /admin/user-groups/{id}/permissions
    public sealed record GroupPermissionsQuery(Guid GroupId) : IQuery<IReadOnlyList<MenuPermissionNode>>;

    public sealed class GroupPermissionsHandler(DatabaseContext db) : IRequestHandler<GroupPermissionsQuery, IReadOnlyList<MenuPermissionNode>>
    {
        public async Task<IReadOnlyList<MenuPermissionNode>> HandleAsync(GroupPermissionsQuery q, CancellationToken ct)
        {
            await MenuTree.RequireGroupAsync(db, q.GroupId, ct);
            var flags = await MenuTree.GroupFlagsAsync(db, [q.GroupId], ct);
            var menus = await MenuTree.ActiveMenusAsync(db, ct);
            return MenuTree.Build(menus.Select(m => MenuTree.Node(m, flags.GetValueOrDefault(m.Id, MenuFlags.None))));
        }
    }

    // ---------------------------------------------------------------- PUT /admin/user-groups/{id}/permissions
    public sealed record PermissionChange(Guid MenuId, bool AllowAccess, bool AllowAdd, bool AllowEdit, bool AllowDelete, bool AllowPrint, bool AllowExport, bool AllowImport);

    public sealed record UpdateGroupPermissionsCommand(Guid GroupId, IReadOnlyList<PermissionChange> Changes) : ICommand<IReadOnlyList<MenuPermissionNode>>;

    public sealed class UpdateGroupPermissionsValidator : AbstractValidator<UpdateGroupPermissionsCommand>
    {
        public UpdateGroupPermissionsValidator()
        {
            RuleFor(x => x.Changes).NotNull().Must(c => c.Count is > 0 and <= 500).WithMessage("Cần 1–500 dòng thay đổi.");
            RuleFor(x => x.Changes).Must(c => c == null || c.Select(x => x.MenuId).Distinct().Count() == c.Count).WithMessage("Mỗi menu chỉ một dòng.");
        }
    }

    /// <summary>Upsert UserRole theo spec 3.3 (chuẩn hóa cờ), ghi audit từng dòng (qua DatabaseContext), phát <see cref="PermissionsChanged"/>.</summary>
    public sealed class UpdateGroupPermissionsHandler(DatabaseContext db, IDispatcher dispatcher, IDomainEventCollector events)
        : IRequestHandler<UpdateGroupPermissionsCommand, IReadOnlyList<MenuPermissionNode>>
    {
        public async Task<IReadOnlyList<MenuPermissionNode>> HandleAsync(UpdateGroupPermissionsCommand c, CancellationToken ct)
        {
            await MenuTree.RequireGroupAsync(db, c.GroupId, ct);
            var menuIds = c.Changes.Select(x => x.MenuId).ToList();
            var known = await db.ObjectMenu.AsNoTracking().Where(m => menuIds.Contains(m.Id) && !m.IsDeleted).Select(m => m.Id).ToListAsync(ct);
            var unknown = menuIds.Except(known).ToList();
            if (unknown.Count > 0)
            {
                throw new BusinessRuleException(422, "REF_NOT_FOUND", $"Menu không tồn tại: {string.Join(", ", unknown)}.", "menuId");
            }
            var existing = await db.UserRole.Where(r => !r.IsDeleted && r.UserGroupId == c.GroupId && r.ObjectMenuId != null && menuIds.Contains(r.ObjectMenuId.Value))
                .ToListAsync(ct);
            bool anyInsert = false;
            foreach (var change in c.Changes)
            {
                var f = new MenuFlags(change.AllowAccess, change.AllowAdd, change.AllowEdit, change.AllowDelete, change.AllowPrint, change.AllowExport, change.AllowImport).Normalize();
                var row = existing.FirstOrDefault(r => r.ObjectMenuId == change.MenuId);
                if (row == null)
                {
                    if (!f.Any)
                    {
                        continue;
                    }
                    row = new UserRole { Id = Guid.NewGuid(), UserGroupId = c.GroupId, ObjectMenuId = change.MenuId };
                    db.UserRole.Add(row);
                    anyInsert = true;
                }
                (row.AllowAccess, row.AllowAdd, row.AllowEdit, row.AllowDelete, row.AllowPrint, row.AllowExport, row.AllowImport)
                    = (f.AllowAccess, f.AllowAdd, f.AllowEdit, f.AllowDelete, f.AllowPrint, f.AllowExport, f.AllowImport);
            }
            await db.SaveChangesAsync(anyInsert ? ConstantHelper.TypeSave.Insert : ConstantHelper.TypeSave.Update, ct);
            events.Raise(new PermissionsChanged(await MenuTree.UsersOfGroupAsync(db, c.GroupId, ct)));
            return await dispatcher.SendAsync(new GroupPermissionsQuery(c.GroupId), ct);
        }
    }

    // ---------------------------------------------------------------- POST /admin/user-groups/{id}/permissions/bulk
    public sealed record BulkGroupAccessCommand(Guid GroupId, bool AllowAccess) : ICommand<int>;

    /// <summary>Cấp / thu hồi truy cập toàn bộ menu cấp 2 (spec 3.3). Thu hồi → tắt mọi cờ.</summary>
    public sealed class BulkGroupAccessHandler(DatabaseContext db, IDomainEventCollector events) : IRequestHandler<BulkGroupAccessCommand, int>
    {
        public async Task<int> HandleAsync(BulkGroupAccessCommand c, CancellationToken ct)
        {
            await MenuTree.RequireGroupAsync(db, c.GroupId, ct);
            var level2 = await db.ObjectMenu.AsNoTracking().Where(m => !m.IsDeleted && m.IsActive && m.LevelShow == 2).Select(m => m.Id).ToListAsync(ct);
            var existing = await db.UserRole.Where(r => !r.IsDeleted && r.UserGroupId == c.GroupId && r.ObjectMenuId != null && level2.Contains(r.ObjectMenuId.Value)).ToListAsync(ct);
            int changed = 0;
            foreach (var menuId in level2)
            {
                var row = existing.FirstOrDefault(r => r.ObjectMenuId == menuId);
                if (row == null)
                {
                    if (!c.AllowAccess)
                    {
                        continue;
                    }
                    db.UserRole.Add(new UserRole { Id = Guid.NewGuid(), UserGroupId = c.GroupId, ObjectMenuId = menuId, AllowAccess = true });
                    changed++;
                }
                else if (row.AllowAccess != c.AllowAccess)
                {
                    row.AllowAccess = c.AllowAccess;
                    if (!c.AllowAccess)
                    {
                        row.AllowAdd = row.AllowEdit = row.AllowDelete = row.AllowPrint = row.AllowExport = row.AllowImport = false;
                    }
                    changed++;
                }
            }
            await db.SaveChangesAsync(c.AllowAccess ? ConstantHelper.TypeSave.Insert : ConstantHelper.TypeSave.Update, ct);
            events.Raise(new PermissionsChanged(await MenuTree.UsersOfGroupAsync(db, c.GroupId, ct)));
            return changed;
        }
    }

    // ---------------------------------------------------------------- GET /admin/user-groups/{id}/preview
    public sealed record GroupPreview(Guid GroupId, string? GroupName, IReadOnlyList<MenuPermissionNode> Menu, int ScreenCount, IReadOnlyList<UserBrief> Users);

    public sealed record UserBrief(Guid Id, string? UserName);

    public sealed record GroupPreviewQuery(Guid GroupId) : IQuery<GroupPreview>;

    /// <summary>Xem trước menu + cờ quyền mà nhóm nhận (chỉ mục AllowAccess = 1) – không đăng nhập thay người khác.</summary>
    public sealed class GroupPreviewHandler(DatabaseContext db) : IRequestHandler<GroupPreviewQuery, GroupPreview>
    {
        public async Task<GroupPreview> HandleAsync(GroupPreviewQuery q, CancellationToken ct)
        {
            var group = await db.UserGroup.AsNoTracking().FirstOrDefaultAsync(g => g.Id == q.GroupId && !g.IsDeleted, ct)
                ?? throw new BusinessRuleException(404, "NOT_FOUND", "Không tìm thấy nhóm người dùng.");
            var flags = await MenuTree.GroupFlagsAsync(db, [q.GroupId], ct);
            var menus = (await MenuTree.ActiveMenusAsync(db, ct)).Where(m => flags.TryGetValue(m.Id, out var f) && f.AllowAccess).ToList();
            var userIds = await MenuTree.UsersOfGroupAsync(db, q.GroupId, ct);
            var users = await db.User.AsNoTracking().Where(u => userIds.Contains(u.Id) && !u.IsDeleted).OrderBy(u => u.UserName)
                .Select(u => new UserBrief(u.Id, u.UserName)).ToListAsync(ct);
            return new GroupPreview(group.Id, group.Name, MenuTree.Build(menus.Select(m => MenuTree.Node(m, flags[m.Id]))),
                menus.Count(m => m.LevelShow == 2), users);
        }
    }

    // ---------------------------------------------------------------- GET /admin/users/{id}/effective-permissions
    public sealed record EffectivePermissionsQuery(Guid UserId) : IQuery<IReadOnlyList<MenuPermissionNode>>;

    /// <summary>Quyền hiệu lực = OR cờ trên mọi nhóm (PROPOSED); <c>grantedBy</c> = nhóm cấp quyền truy cập. Admin → mọi menu, mọi cờ.</summary>
    public sealed class EffectivePermissionsHandler(DatabaseContext db) : IRequestHandler<EffectivePermissionsQuery, IReadOnlyList<MenuPermissionNode>>
    {
        public async Task<IReadOnlyList<MenuPermissionNode>> HandleAsync(EffectivePermissionsQuery q, CancellationToken ct)
        {
            var user = await db.User.AsNoTracking().FirstOrDefaultAsync(u => u.Id == q.UserId && !u.IsDeleted, ct)
                ?? throw new BusinessRuleException(404, "NOT_FOUND", "Không tìm thấy người dùng.");
            var menus = await MenuTree.ActiveMenusAsync(db, ct);
            if (user.IsAdmin)
            {
                var all = new MenuFlags(true, true, true, true, true, true, true);
                return menus.Select(m => MenuTree.Node(m, all, ["(admin)"])).ToList();
            }
            var groups = await (from c in db.UserClaim.AsNoTracking()
                                join g in db.UserGroup.AsNoTracking() on c.UserGroupId equals g.Id
                                where c.UserId == q.UserId && !c.IsDeleted && !g.IsDeleted
                                select new { g.Id, g.Name }).ToListAsync(ct);
            var groupIds = groups.Select(g => g.Id).ToList();
            var rows = await db.UserRole.AsNoTracking()
                .Where(r => !r.IsDeleted && r.UserGroupId != null && groupIds.Contains(r.UserGroupId.Value) && r.ObjectMenuId != null).ToListAsync(ct);
            var result = new List<MenuPermissionNode>();
            foreach (var m in menus)
            {
                var mine = rows.Where(r => r.ObjectMenuId == m.Id).ToList();
                var f = mine.Select(MenuFlags.Of).Aggregate(MenuFlags.None, (a, b) => a.Or(b));
                if (!f.AllowAccess)
                {
                    continue;
                }
                var by = mine.Where(r => r.AllowAccess).Select(r => groups.First(g => g.Id == r.UserGroupId).Name ?? "?").Distinct().OrderBy(n => n).ToList();
                result.Add(MenuTree.Node(m, f, by));
            }
            return result;
        }
    }

    // ---------------------------------------------------------------- GET /meta/menu
    public sealed record MyMenuQuery(Guid UserId) : IQuery<IReadOnlyList<MenuPermissionNode>>;

    /// <summary>Spec 2.2 <c>/meta/menu</c>: cây ObjectMenu chỉ gồm mục user được truy cập, kèm cờ quyền (Angular dựng sidebar).</summary>
    public sealed class MyMenuHandler(IDispatcher dispatcher) : IRequestHandler<MyMenuQuery, IReadOnlyList<MenuPermissionNode>>
    {
        public async Task<IReadOnlyList<MenuPermissionNode>> HandleAsync(MyMenuQuery q, CancellationToken ct)
            => MenuTree.Build((await dispatcher.SendAsync(new EffectivePermissionsQuery(q.UserId), ct))
                .Select(n => n with { GrantedBy = null }));
    }
}
