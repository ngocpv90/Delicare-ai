using System.ComponentModel.DataAnnotations;

namespace AppDelicare.Model.Request.Custom
{
    /// <summary>T45R: cấp / thu hồi truy cập toàn bộ menu cấp 2 của nhóm.</summary>
    public class UserRoleAccessAllRequest
    {
        [Required]
        public Guid UserGroupId { get; set; }
        /// <summary>true = cấp truy cập mọi màn hình; false = thu hồi (tắt mọi cờ).</summary>
        public bool AllowAccess { get; set; }
    }

    /// <summary>T45R: sao chép quyền nhóm nguồn sang nhóm đích.</summary>
    public class UserRoleCopyRequest
    {
        [Required]
        public Guid SourceUserGroupId { get; set; }
        [Required]
        public Guid TargetUserGroupId { get; set; }
    }
}
