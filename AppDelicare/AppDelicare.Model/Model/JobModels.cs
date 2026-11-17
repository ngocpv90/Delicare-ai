namespace AppDelicare.Model.Model
{
    /// <summary>T23: kết quả 1 lần chạy job (trả về cho <c>api/Job/Run</c> và ghi <c>AnalyticsRefreshLog</c>).</summary>
    public class JobRunResultModel
    {
        public string JobCode { get; set; } = string.Empty;
        public string JobName { get; set; } = string.Empty;
        public Guid RunId { get; set; }
        /// <summary>SCHEDULE (AppDelicare.Job tự chạy) / MANUAL (admin bấm <c>api/Job/Run</c>).</summary>
        public string Trigger { get; set; } = string.Empty;
        /// <summary>Succeeded / Skipped (nghiệp vụ từ chối hoặc không có gì để làm – không phải lỗi) / Failed / Locked (đang có lần chạy khác).</summary>
        public string RunStatus { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public long RowsProcessed { get; set; }
        /// <summary>Ngày nghiệp vụ job xử lý (vd. ngày ăn được khóa đơn).</summary>
        public DateTime? TargetDate { get; set; }
        public string? Message { get; set; }
    }

    /// <summary>T23: 1 job trong danh sách <c>api/Job/GetAll</c>.</summary>
    public class JobInfoModel
    {
        public string JobCode { get; set; } = string.Empty;
        public string JobName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool Enabled { get; set; }
        /// <summary>Giờ chạy hằng ngày "HH:mm" (giờ VN).</summary>
        public string At { get; set; } = string.Empty;
        public DateTime NextRunAt { get; set; }
        public JobRunResultModel? LastRun { get; set; }
    }

    /// <summary>T23 (Catalog 81 ContractExpiryJob): hợp đồng Hiệu lực đã qua ngày hết hiệu lực → Hết hạn.</summary>
    public class ContractExpireResultModel
    {
        public bool Preview { get; set; }
        public DateTime Today { get; set; }
        public int ExpiredCount { get; set; }
        public List<string> ListContractNumber { get; set; } = [];
    }
}
