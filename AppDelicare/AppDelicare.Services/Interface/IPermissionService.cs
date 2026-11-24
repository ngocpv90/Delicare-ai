namespace AppDelicare.Services.Interface
{
    /// <summary>Thao tác cần quyền – ứng với cờ <c>UserRole.Allow*</c>; <see cref="Approve"/> = AllowEdit trên mục menu "Duyệt …"; <see cref="AdminOnly"/> chỉ <c>User.IsAdmin</c>.</summary>
    public enum PermissionAction
    {
        View,
        Add,
        Edit,
        Delete,
        Print,
        Export,
        Import,
        /// <summary>Duyệt (D-31, CLAUDE.md 4.3): UserRole.AllowEdit trên ObjectMenu có MetaLink <c>#/wf-inbox?type=&lt;screen-id&gt;</c> – hộp duyệt lọc theo loại (nhóm "Phê duyệt").</summary>
        Approve,
        /// <summary>Thao tác nguy hiểm / leo thang quyền – chỉ admin.</summary>
        AdminOnly,
    }

    /// <summary>Cờ quyền trên một menu (OR qua mọi nhóm của user).</summary>
    public sealed record MenuRights(bool Access, bool Add, bool Edit, bool Delete, bool Print, bool Export, bool Import);

    /// <summary>Quyền của một user: admin, các nhóm (UserGroup.Name), quyền theo MetaLink.</summary>
    public sealed record UserPermissions(bool IsAdmin, IReadOnlySet<string> Groups, IReadOnlyDictionary<string, MenuRights> Menus)
    {
        public static readonly UserPermissions None = new(false, new HashSet<string>(), new Dictionary<string, MenuRights>());

        /// <summary>HR-ACC-1.4 / 1.7 (D-82 b, h): false = tài khoản không tồn tại / đã khóa / đã xóa → mọi API đăng nhập bị chặn ngay (PermissionFilter, 401).</summary>
        public bool Active { get; init; } = true;

        /// <summary>HR-ACC-1.4 (D-82 b): loại tài khoản (User.AccountType). Khác INTERNAL → không gọi được API nội bộ (PermissionFilter, 403), kể cả khi gán nhóm admin.</summary>
        public string AccountType { get; init; } = "INTERNAL";

        public static readonly UserPermissions Blocked = None with { Active = false };
    }

    /// <summary>
    /// Kiểm tra quyền phía server theo cơ chế cũ (D-31): User → UserClaim → UserGroup → UserRole × ObjectMenu, IsAdmin có mọi quyền.
    /// Service quản trị (User, UserClaim, UserGroup, UserRole, ObjectMenu) gọi <see cref="Invalidate"/> / <see cref="InvalidateAll"/> sau khi ghi.
    /// </summary>
    public interface IPermissionService
    {
        /// <summary>Tiền tố MetaLink của mục menu duyệt.</summary>
        public const string ApproveMenuPrefix = "#/wf-inbox?type=";

        Task<UserPermissions> GetAsync(Guid userId, CancellationToken cancellationToken = default);
        bool IsAllowed(UserPermissions permissions, string screenId, PermissionAction action);
        /// <summary>Xóa cache quyền của 1 user (đổi nhóm, khóa, cấp admin…).</summary>
        void Invalidate(Guid userId);
        /// <summary>Xóa cache quyền của mọi user (đổi ma trận quyền, menu, nhóm).</summary>
        void InvalidateAll();
    }
}
