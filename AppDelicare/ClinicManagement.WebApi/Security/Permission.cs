namespace AppDelicare.WebApi.Security
{
    /// <summary>Thao tác cần quyền – ứng với cờ <c>UserRole.Allow*</c>; <see cref="Approve"/> theo cấu hình; <see cref="AdminOnly"/> chỉ <c>User.IsAdmin</c>.</summary>
    public enum PermissionAction
    {
        View,
        Add,
        Edit,
        Delete,
        Print,
        Export,
        Import,
        /// <summary>Duyệt: UserRole không có cột AllowApprove (GAP-PERM-01) → nhóm trong <c>Permissions:ApproverGroups</c>.</summary>
        Approve,
        /// <summary>Thao tác nguy hiểm / leo thang quyền – chỉ admin.</summary>
        AdminOnly,
    }

    /// <summary>
    /// Yêu cầu quyền trên một màn hình (screen-id của mockup; MetaLink = <c>#/&lt;screen-id&gt;</c>, Spec 1.2).
    /// Đặt ở action (ưu tiên) hoặc controller. Controller CRUD cũ được gắn tự động bởi <see cref="PermissionConvention"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class PermissionAttribute(string screenId, PermissionAction action) : Attribute
    {
        public string ScreenId { get; } = screenId;
        public PermissionAction Action { get; } = action;
        public string MetaLink => "#/" + ScreenId;
    }

    /// <summary>Chỉ admin (vd. xóa vật lý dữ liệu, cấp quyền admin).</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class AdminOnlyAttribute() : PermissionAttribute("*", PermissionAction.AdminOnly);

    /// <summary>Chỉ cần đăng nhập, không kiểm tra UserRole (thao tác trên chính tài khoản mình).</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class SkipPermissionAttribute : Attribute;

    /// <summary>Cấu hình <c>Permissions</c> trong appsettings.</summary>
    public sealed class PermissionOptions
    {
        public const string Section = "Permissions";

        /// <summary>Thời gian cache quyền của mỗi user (phút).</summary>
        public int CacheMinutes { get; set; } = 5;

        /// <summary>Cột ma trận (admin, sales, menu, scm, production, qc, logistics, hr, kpi) → tên UserGroup được duyệt (cột "A" – ORG_STRUCTURE mục 2, PROPOSED D-25).</summary>
        public Dictionary<string, string[]> ApproverGroups { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
