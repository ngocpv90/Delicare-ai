namespace AppDelicare.Model.Model
{
    /// <summary>UI-6.38 (D-119): bổ sung giờ thực tế khi quên vào / ra ca (AttendanceEvent/Supplement).</summary>
    public class AttendanceSupplementRequest
    {
        public Guid EmployeeId { get; set; }
        public Guid ShiftAssignmentId { get; set; }
        /// <summary>CHECK_IN | CHECK_OUT.</summary>
        public string? EventType { get; set; }
        public DateTime EventAt { get; set; }
        public string? ReasonCode { get; set; }
        public string? Note { get; set; }
    }

    public class AttendanceConfirmValidRequest
    {
        public Guid Id { get; set; }
        public string? Note { get; set; }
    }

    public class AttendanceReportAbsentRequest
    {
        public Guid ShiftAssignmentId { get; set; }
        public string? Note { get; set; }
    }

    /// <summary>UI-6.38: tab "Hôm nay" – theo ca: chưa vào ca / đã vào ca / nghỉ phép / cần xử lý.</summary>
    public class AttendanceDayBoardModel
    {
        public DateTime Date { get; set; }
        public Guid? ShiftId { get; set; }
        public List<AttendanceDayShiftModel> ListShift { get; set; } = [];
        public int Assigned { get; set; }
        public int Present { get; set; }
        public int NotCheckedInOverdue { get; set; }
        public int LateMinutesTotal { get; set; }
        public int NeedActionCount { get; set; }
        public List<AttendancePersonModel> ListNotCheckedIn { get; set; } = [];
        public List<AttendancePersonModel> ListCheckedIn { get; set; } = [];
        public List<AttendancePersonModel> ListOnLeave { get; set; } = [];
        public List<AttendancePersonModel> ListNeedAction { get; set; } = [];
    }

    public class AttendanceDayShiftModel
    {
        public Guid Id { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }
        public DateTime? Start { get; set; }
        public DateTime? End { get; set; }
        /// <summary>RUNNING | UPCOMING | DONE.</summary>
        public string State { get; set; } = "UPCOMING";
        public int Assigned { get; set; }
        public int Present { get; set; }
    }

    public class AttendancePersonModel
    {
        /// <summary>Cần xử lý: MISSING_OUT | OUTSIDE.</summary>
        public string? Kind { get; set; }
        public Guid EmployeeId { get; set; }
        public string? Code { get; set; }
        public string? FullName { get; set; }
        public string? Phone { get; set; }
        public string? UnitName { get; set; }
        public Guid? AssignmentId { get; set; }
        public Guid? EventId { get; set; }
        public DateTime? WorkDate { get; set; }
        public string? ShiftCode { get; set; }
        public DateTime? PlannedStart { get; set; }
        public DateTime? PlannedEnd { get; set; }
        public DateTime? CheckInAt { get; set; }
        public DateTime? CheckOutAt { get; set; }
        public int LateMinutes { get; set; }
        public int OverdueMinutes { get; set; }
        public bool Outside { get; set; }
        public decimal? DistanceMeters { get; set; }
        public string? LeaveRequestNo { get; set; }
        public string? LeaveTypeCode { get; set; }
    }

    /// <summary>UI-6.38: bảng công tháng dạng ma trận người × ngày.</summary>
    public class AttendanceMonthGridModel
    {
        public string Month { get; set; } = string.Empty;
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public List<string> Days { get; set; } = [];
        public List<AttendanceMonthRowModel> ListRow { get; set; } = [];
        public int MissingTime { get; set; }
        public int OutsideUnconfirmed { get; set; }
        public int AbsentNoLeave { get; set; }
    }

    public class AttendanceMonthRowModel
    {
        public Guid EmployeeId { get; set; }
        public string? Code { get; set; }
        public string? FullName { get; set; }
        public Guid? UnitId { get; set; }
        public string? UnitName { get; set; }
        public Dictionary<string, AttendanceMonthCellModel> Cells { get; set; } = [];
        public int Work { get; set; }
        public decimal Hours { get; set; }
        public int Late { get; set; }
        public int Absent { get; set; }
        public int Leave { get; set; }
    }

    public class AttendanceMonthCellModel
    {
        /// <summary>✓ | M | V | P | T | ! | – | … | (trống = ca tương lai).</summary>
        public string Symbol { get; set; } = "–";
        public Guid? AssignmentId { get; set; }
        public string? ShiftCode { get; set; }
        public DateTime? CheckInAt { get; set; }
        public DateTime? CheckOutAt { get; set; }
        public int LateMinutes { get; set; }
        public int EarlyLeaveMinutes { get; set; }
        public bool Supplemented { get; set; }
        public Guid? OutsideEventId { get; set; }
    }
}
