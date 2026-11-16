namespace AppDelicare.Model.Option
{
    /// <summary>Cấu hình <c>Permissions</c> trong appsettings. Quyền (kể cả quyền duyệt) KHÔNG cấu hình ở đây – nằm trong UserRole (D-31).</summary>
    public sealed class PermissionOptions
    {
        public const string Section = "Permissions";

        /// <summary>Thời gian cache quyền của mỗi user (phút).</summary>
        public int CacheMinutes { get; set; } = 5;
    }
}
