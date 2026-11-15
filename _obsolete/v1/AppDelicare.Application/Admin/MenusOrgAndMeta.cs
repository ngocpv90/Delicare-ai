using AppDelicare.Application.Common;
using AppDelicare.Data.Connection;
using AppDelicare.Data.Share;
using AppDelicare.Model.Helper;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Application.Admin
{
    // ================================================================ ObjectMenu: tree / reorder (spec 3.4)
    public sealed record MenuNode(Guid Id, string? Name, string? ShortName, string? MetaLink, Guid? ParentId, int? LevelShow, int? OrderShow, string? IconName, bool IsActive)
    {
        public List<MenuNode> Children { get; } = [];
    }

    public sealed record MenuTreeQuery(bool IncludeInactive = false) : IQuery<IReadOnlyList<MenuNode>>;

    public sealed class MenuTreeHandler(DatabaseContext db) : IRequestHandler<MenuTreeQuery, IReadOnlyList<MenuNode>>
    {
        public async Task<IReadOnlyList<MenuNode>> HandleAsync(MenuTreeQuery q, CancellationToken ct)
        {
            var menus = await db.ObjectMenu.AsNoTracking().Where(m => !m.IsDeleted && (q.IncludeInactive || m.IsActive))
                .OrderBy(m => m.LevelShow).ThenBy(m => m.OrderShow).ThenBy(m => m.Name)
                .Select(m => new MenuNode(m.Id, m.Name, m.ShortName, m.MetaLink, m.ParentId, m.LevelShow, m.OrderShow, m.IconName, m.IsActive))
                .ToListAsync(ct);
            var byId = menus.ToDictionary(m => m.Id);
            var roots = new List<MenuNode>();
            foreach (var m in menus)
            {
                if (m.ParentId is { } p && byId.TryGetValue(p, out var parent) && parent != m)
                {
                    parent.Children.Add(m);
                }
                else
                {
                    roots.Add(m);
                }
            }
            return roots;
        }
    }

    public sealed record MenuOrder(Guid Id, Guid? ParentId, int OrderShow);

    public sealed record ReorderMenusCommand(IReadOnlyList<MenuOrder> Items) : ICommand<IReadOnlyList<MenuNode>>;

    public sealed class ReorderMenusValidator : AbstractValidator<ReorderMenusCommand>
    {
        public ReorderMenusValidator()
        {
            RuleFor(x => x.Items).NotNull().Must(i => i.Count is > 0 and <= 1000).WithMessage("Cần 1–1000 dòng.");
            RuleFor(x => x.Items).Must(i => i == null || i.All(x => x.ParentId != x.Id)).WithMessage("Menu không thể là cha của chính nó.");
        }
    }

    /// <summary>Đổi cha / thứ tự; LevelShow = 1 nếu không có cha, 2 nếu có (cấu trúc 2 cấp của mockup). Chặn vòng lặp.</summary>
    public sealed class ReorderMenusHandler(DatabaseContext db, IDispatcher dispatcher) : IRequestHandler<ReorderMenusCommand, IReadOnlyList<MenuNode>>
    {
        public async Task<IReadOnlyList<MenuNode>> HandleAsync(ReorderMenusCommand c, CancellationToken ct)
        {
            var ids = c.Items.Select(i => i.Id).Concat(c.Items.Where(i => i.ParentId != null).Select(i => i.ParentId!.Value)).Distinct().ToList();
            var menus = await db.ObjectMenu.Where(m => ids.Contains(m.Id) && !m.IsDeleted).ToDictionaryAsync(m => m.Id, ct);
            var missing = ids.Where(i => !menus.ContainsKey(i)).ToList();
            if (missing.Count > 0)
            {
                throw new BusinessRuleException(422, "REF_NOT_FOUND", $"Menu không tồn tại: {string.Join(", ", missing)}.", "id");
            }
            foreach (var item in c.Items)
            {
                var m = menus[item.Id];
                m.ParentId = item.ParentId;
                m.OrderShow = item.OrderShow;
                m.LevelShow = item.ParentId == null ? 1 : 2;
            }
            // Chặn vòng lặp sau khi áp thay đổi.
            var parents = await db.ObjectMenu.AsNoTracking().Where(m => !m.IsDeleted).ToDictionaryAsync(m => m.Id, m => m.ParentId, ct);
            foreach (var item in c.Items)
            {
                parents[item.Id] = item.ParentId;
            }
            foreach (var item in c.Items)
            {
                var seen = new HashSet<Guid> { item.Id };
                Guid? cursor = item.ParentId;
                while (cursor != null)
                {
                    if (!seen.Add(cursor.Value))
                    {
                        throw new BusinessRuleException(422, "BUSINESS_RULE", "Sắp xếp tạo vòng lặp cha – con.", "parentId");
                    }
                    cursor = parents.GetValueOrDefault(cursor.Value);
                }
            }
            await db.SaveChangesAsync(ConstantHelper.TypeSave.Update, ct);
            return await dispatcher.SendAsync(new MenuTreeQuery(IncludeInactive: true), ct);
        }
    }

    // ================================================================ OrganizationUnit: tree / branches (spec 3.5)
    public sealed record OrgNode(Guid Id, string? Code, string? Name, string? UnitType, Guid? ManagerId, string? ManagerName, int EmployeeCount, int WarehouseCount, string? Status)
    {
        public List<OrgNode> Children { get; } = [];
    }

    public sealed record OrgTreeQuery(Guid? CompanyId) : IQuery<IReadOnlyList<OrgNode>>;

    public sealed class OrgTreeHandler(DatabaseContext db) : IRequestHandler<OrgTreeQuery, IReadOnlyList<OrgNode>>
    {
        public async Task<IReadOnlyList<OrgNode>> HandleAsync(OrgTreeQuery q, CancellationToken ct)
        {
            var units = await db.OrganizationUnit.AsNoTracking().Where(o => !o.IsDeleted && (q.CompanyId == null || o.CompanyId == q.CompanyId))
                .OrderBy(o => o.Code).ToListAsync(ct);
            var unitIds = units.Select(u => u.Id).ToList();
            var employees = await db.Employee.AsNoTracking().Where(e => !e.IsDeleted && e.OrganizationUnitId != null && unitIds.Contains(e.OrganizationUnitId.Value))
                .GroupBy(e => e.OrganizationUnitId!.Value).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
            var warehouses = await db.Warehouse.AsNoTracking().Where(w => !w.IsDeleted && w.OrganizationUnitId != null && unitIds.Contains(w.OrganizationUnitId.Value))
                .GroupBy(w => w.OrganizationUnitId!.Value).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
            var managerIds = units.Where(u => u.ManagerId != null).Select(u => u.ManagerId!.Value).Distinct().ToList();
            var managers = await db.Employee.AsNoTracking().Where(e => managerIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.FullName, ct);

            var nodes = units.ToDictionary(u => u.Id, u => new OrgNode(u.Id, u.Code, u.Name, u.UnitType, u.ManagerId,
                u.ManagerId is { } m ? managers.GetValueOrDefault(m) : null, employees.GetValueOrDefault(u.Id), warehouses.GetValueOrDefault(u.Id), u.Status));
            var roots = new List<OrgNode>();
            foreach (var u in units)
            {
                if (u.ParentId is { } p && nodes.TryGetValue(p, out var parent) && p != u.Id)
                {
                    parent.Children.Add(nodes[u.Id]);
                }
                else
                {
                    roots.Add(nodes[u.Id]);
                }
            }
            return roots;
        }
    }

    public sealed record BranchItem(Guid Id, string? Code, string? Name);

    public sealed record BranchesQuery : IQuery<IReadOnlyList<BranchItem>>;

    /// <summary>Spec 3.5: OrganizationUnit có UnitType = BRANCH (bộ chọn đơn vị trên header / X-Branch-Id).</summary>
    public sealed class BranchesHandler(DatabaseContext db) : IRequestHandler<BranchesQuery, IReadOnlyList<BranchItem>>
    {
        public const string BranchType = "BRANCH";

        public async Task<IReadOnlyList<BranchItem>> HandleAsync(BranchesQuery q, CancellationToken ct)
            => await db.OrganizationUnit.AsNoTracking().Where(o => !o.IsDeleted && o.UnitType == BranchType)
                .OrderBy(o => o.Code).Select(o => new BranchItem(o.Id, o.Code, o.Name)).ToListAsync(ct);
    }

    // ================================================================ CoreDataMergeConflict (spec 3.6) – Id bigint
    public sealed record MergeConflictDto(long Id, string? MigrationCode, string? EntityName, Guid? RecordId, string? FieldName,
        string? LegacyValue, string? CanonicalValue, DateTime DetectedAt, DateTime? ResolvedAt, string? ResolutionNote);

    public sealed record MergeConflictListQuery(bool? Resolved, int Page, int PageSize) : IQuery<(IReadOnlyList<MergeConflictDto> Items, int Total)>;

    public sealed class MergeConflictListHandler(DatabaseContext db) : IRequestHandler<MergeConflictListQuery, (IReadOnlyList<MergeConflictDto> Items, int Total)>
    {
        public async Task<(IReadOnlyList<MergeConflictDto> Items, int Total)> HandleAsync(MergeConflictListQuery q, CancellationToken ct)
        {
            var query = db.CoreDataMergeConflict.AsNoTracking().AsQueryable();
            if (q.Resolved != null)
            {
                query = q.Resolved.Value ? query.Where(x => x.ResolvedAt != null) : query.Where(x => x.ResolvedAt == null);
            }
            int total = await query.CountAsync(ct);
            var items = await query.OrderByDescending(x => x.DetectedAt).Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
                .Select(x => new MergeConflictDto(x.Id, x.MigrationCode, x.EntityName, x.RecordId, x.FieldName, x.LegacyValue, x.CanonicalValue, x.DetectedAt, x.ResolvedAt, x.ResolutionNote))
                .ToListAsync(ct);
            return (items, total);
        }
    }

    public sealed record ResolveMergeConflictCommand(long Id, string? ResolutionNote) : ICommand<MergeConflictDto>;

    public sealed class ResolveMergeConflictValidator : AbstractValidator<ResolveMergeConflictCommand>
    {
        public ResolveMergeConflictValidator()
            => RuleFor(x => x.ResolutionNote).NotEmpty().WithMessage("Ghi chú xử lý là bắt buộc.").MaximumLength(1000).WithMessage("Tối đa 1000 ký tự.");
    }

    public sealed class ResolveMergeConflictHandler(DatabaseContext db, IClock clock) : IRequestHandler<ResolveMergeConflictCommand, MergeConflictDto>
    {
        public async Task<MergeConflictDto> HandleAsync(ResolveMergeConflictCommand c, CancellationToken ct)
        {
            var x = await db.CoreDataMergeConflict.FirstOrDefaultAsync(m => m.Id == c.Id, ct)
                ?? throw new BusinessRuleException(404, "NOT_FOUND", "Không tìm thấy xung đột dữ liệu.");
            if (x.ResolvedAt != null)
            {
                throw new BusinessRuleException(409, "INVALID_TRANSITION", "Xung đột này đã được xử lý.");
            }
            x.ResolvedAt = clock.Now;
            x.ResolutionNote = c.ResolutionNote!.Trim();
            await db.SaveChangesAsync(ct);
            return new MergeConflictDto(x.Id, x.MigrationCode, x.EntityName, x.RecordId, x.FieldName, x.LegacyValue, x.CanonicalValue, x.DetectedAt, x.ResolvedAt, x.ResolutionNote);
        }
    }

    // ================================================================ /meta/me (spec 2.2)
    public sealed record MeDto(UserDto User, IReadOnlyList<BranchItem> Branches, IReadOnlyList<MenuPermissionNode> Permissions);

    public sealed record MeQuery(Guid UserId) : IQuery<MeDto>;

    /// <summary>Gọi một lần khi đăng nhập: user + person + nhóm, đơn vị (BRANCH), quyền hiệu lực. Đơn vị được phép theo user: chưa có liên kết (GAP-C-10) → mọi BRANCH.</summary>
    public sealed class MeHandler(IDispatcher dispatcher) : IRequestHandler<MeQuery, MeDto>
    {
        public async Task<MeDto> HandleAsync(MeQuery q, CancellationToken ct)
            => new(await dispatcher.SendAsync(new UserGetQuery(q.UserId), ct),
                await dispatcher.SendAsync(new BranchesQuery(), ct),
                await dispatcher.SendAsync(new EffectivePermissionsQuery(q.UserId), ct));
    }
}
