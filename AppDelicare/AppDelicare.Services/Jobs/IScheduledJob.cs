using AppDelicare.Data.Interface;

namespace AppDelicare.Services.Jobs
{
    /// <summary>
    /// T23 (D-48): 1 job hằng ngày. Job <b>không chứa nghiệp vụ</b> – chỉ chọn ngày nghiệp vụ và gọi method Service cũ
    /// (nút bấm tay trên web và job chạy cùng một code). Chạy lại phải không sai dữ liệu (idempotent).
    /// </summary>
    public interface IScheduledJob
    {
        /// <summary>Mã job = khóa cấu hình <c>Jobs:&lt;Code&gt;</c> = <c>AnalyticsRefreshLog.JobCode</c>.</summary>
        string Code { get; }
        string Name { get; }
        string Description { get; }
        /// <summary>Giờ chạy mặc định "HH:mm" (giờ VN) khi cấu hình không ghi <c>At</c>.</summary>
        string DefaultAt { get; }

        /// <param name="now">Giờ VN lúc chạy (IClock) – job tính ngày nghiệp vụ từ đây.</param>
        Task<JobOutcome> RunAsync(DateTime now, CancellationToken cancellationToken);
    }

    public static class JobRunStatus
    {
        public const string Running = "Running";
        public const string Succeeded = "Succeeded";
        /// <summary>Nghiệp vụ từ chối (Result 400) hoặc không có gì để làm – không phải lỗi hệ thống.</summary>
        public const string Skipped = "Skipped";
        public const string Failed = "Failed";
        /// <summary>Đang có lần chạy khác của cùng job (sp_getapplock) – lần này không chạy.</summary>
        public const string Locked = "Locked";
    }

    public static class JobTrigger
    {
        public const string Schedule = "SCHEDULE";
        public const string Manual = "MANUAL";
    }

    /// <summary>Kết quả của job (trạng thái, số dòng xử lý, ngày nghiệp vụ, thông báo).</summary>
    public sealed record JobOutcome(string Status, long RowsProcessed, DateTime? TargetDate, string? Message)
    {
        /// <summary>Result 200 → Succeeded; khác 200 (lỗi nghiệp vụ 400) → Skipped kèm thông báo của Service.</summary>
        public static JobOutcome From<T>(IResult<T> result, DateTime? targetDate, Func<T, long> rows, Func<T, string?>? message = null)
            => result.ResponseCode == 200 && result.Data is { } data
                ? new JobOutcome(JobRunStatus.Succeeded, rows(data), targetDate, message?.Invoke(data) ?? result.ResponseMessage)
                : new JobOutcome(JobRunStatus.Skipped, 0, targetDate, result.ResponseMessage);
    }
}
