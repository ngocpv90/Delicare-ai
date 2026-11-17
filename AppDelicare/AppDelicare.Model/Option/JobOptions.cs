namespace AppDelicare.Model.Option
{
    /// <summary>
    /// T23 (D-48): lịch job tự động – mục <c>Jobs</c> trong appsettings, mỗi job 1 khóa theo mã job:
    /// <c>"Jobs": { "LockOrders": { "Enabled": true, "At": "20:00" }, … }</c>. Giờ theo <c>BusinessRules:TimeZone</c> (giờ VN).
    /// Không lưu lịch trong DB (D-05, không thêm bảng).
    /// </summary>
    public class JobScheduleOptions
    {
        public const string Section = "Jobs";

        /// <summary>false = AppDelicare.Job không tự chạy (vẫn bấm tay được qua <c>api/Job/Run</c>).</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Giờ chạy hằng ngày "HH:mm" (giờ VN). Trống = dùng giờ mặc định của job.</summary>
        public string? At { get; set; }
    }

    /// <summary>T23: tài khoản dịch vụ ghi CreatedBy / ModifiedBy cho job (CurrentActor ActorType.System).</summary>
    public class SystemUsersOptions
    {
        public const string Section = "SystemUsers";

        /// <summary>User.Id của tài khoản <c>system</c> (seed <c>DB/seed/SystemUser_seed.sql</c>). Trống = job không được ghi.</summary>
        public string? JobUserId { get; set; }
    }
}
