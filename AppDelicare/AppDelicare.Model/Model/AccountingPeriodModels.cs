namespace AppDelicare.Model.Model
{
    /// <summary>ACC-4 (D-131 h): điều kiện khóa kỳ kế toán.</summary>
    public sealed class PeriodConditionModel
    {
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public bool Ok { get; set; }
        public string? Detail { get; set; }
        public string? Link { get; set; }
        public string? ActionLabel { get; set; }
    }

    public sealed class PeriodRowModel
    {
        public string Month { get; set; } = string.Empty;
        public bool Locked { get; set; }
        public string? By { get; set; }
        public DateTime? At { get; set; }
        public bool UnlockPending { get; set; }
    }

    public sealed class PeriodAdjustmentModel
    {
        public string Number { get; set; } = string.Empty;
        public string? SourceNumber { get; set; }
        public string? SourceType { get; set; }
        public string SourceMonth { get; set; } = string.Empty;
        public string? Reason { get; set; }
        public decimal? Difference { get; set; }
        public string WriteTo { get; set; } = string.Empty;
    }

    public sealed class PeriodOverviewModel
    {
        public string Month { get; set; } = string.Empty;
        public bool Locked { get; set; }
        public string? LockedBy { get; set; }
        public DateTime? LockedAt { get; set; }
        public bool UnlockPending { get; set; }
        public bool Ended { get; set; }
        public DateTime DueDate { get; set; }
        public int Remaining { get; set; }
        public bool CanLock { get; set; }
        public List<PeriodConditionModel> ListCondition { get; set; } = [];
        public List<PeriodRowModel> ListPeriod { get; set; } = [];
        public List<PeriodAdjustmentModel> ListAdjustment { get; set; } = [];
    }

    public sealed class PeriodActionRequest
    {
        public string? Month { get; set; }
        public string? Reason { get; set; }
    }
}
