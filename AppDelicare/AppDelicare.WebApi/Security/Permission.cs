using AppDelicare.Services.Interface;

namespace AppDelicare.WebApi.Security
{
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

        /// <summary>MENU-4.3: thao tác thay thế – có MỘT trong các quyền này trên cùng màn cũng được (vd. thêm dòng con = Sửa hoặc Thêm chứng từ cha).</summary>
        public PermissionAction[] OrActions { get; init; } = [];
    }

    /// <summary>Chỉ admin (vd. xóa vật lý dữ liệu, cấp quyền admin).</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class AdminOnlyAttribute() : PermissionAttribute("*", PermissionAction.AdminOnly);

    /// <summary>Chỉ cần đăng nhập, không kiểm tra UserRole (thao tác trên chính tài khoản mình).</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class SkipPermissionAttribute : Attribute;
}
