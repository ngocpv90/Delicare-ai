using AppDelicare.Model.Entity;

namespace AppDelicare.Model.Model
{
    /// <summary>BE2.6 (spec 11.1 shift-grid, Catalog 201): lưới ca nhân viên × ngày + số liệu hôm nay theo ca.</summary>
    public class ShiftGridModel
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        /// <summary>Các ngày của lưới (yyyy-MM-dd) – khóa của <see cref="ShiftGridEmployeeModel.Cells"/>.</summary>
        public List<string> Days { get; set; } = [];
        public List<ShiftGridShiftModel> Shifts { get; set; } = [];
        public List<ShiftGridEmployeeModel> Employees { get; set; } = [];
        /// <summary>Hôm nay theo ca: số được phân / số đã vào ca (Đã vào ca + Hoàn thành).</summary>
        public List<ShiftGridKpiModel> KpiToday { get; set; } = [];
        // UI-6.37 (D-118): tuần Thứ 2 – CN
        public DateTime? WeekStart { get; set; }
        public List<ShiftUnitTotalModel> UnitTotals { get; set; } = [];
        public ShiftWeekStatusModel? WeekStatus { get; set; }
        public ShiftWeekKpiModel? Kpi { get; set; }
    }

    public class ShiftGridShiftModel
    {
        public Guid Id { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
    }

    public class ShiftGridEmployeeModel
    {
        public Guid Id { get; set; }
        public string? Code { get; set; }
        public string? FullName { get; set; }
        public Guid? PositionId { get; set; }
        public string? PositionName { get; set; }
        public Guid? OrganizationUnitId { get; set; }
        public string? OrganizationUnitName { get; set; }
        /// <summary>Khóa = ngày yyyy-MM-dd (đủ mọi ngày của lưới; ô trống = chưa phân ca).</summary>
        public Dictionary<string, ShiftGridCellModel> Cells { get; set; } = [];
        /// <summary>UI-6.37: số ca / giờ công trong tuần + cảnh báo (quá nhiều ca, không có ngày nghỉ).</summary>
        public int WeekShiftCount { get; set; }
        public decimal WeekHours { get; set; }
        public List<string> Warnings { get; set; } = [];
    }

    public class ShiftGridCellModel
    {
        public Guid? AssignmentId { get; set; }
        public Guid? ShiftId { get; set; }
        public string? ShiftCode { get; set; }
        public string? ShiftName { get; set; }
        public DateTime? PlannedStart { get; set; }
        public DateTime? PlannedEnd { get; set; }
        public string? Status { get; set; }
        public string? StatusName { get; set; }
        /// <summary>Có đơn nghỉ Đã duyệt bao ngày này (spec).</summary>
        public bool OnLeave { get; set; }
        /// <summary>Có đơn nghỉ Chờ duyệt bao ngày này.</summary>
        public bool LeavePending { get; set; }
        public Guid? LeaveRequestId { get; set; }
        public string? LeaveRequestNo { get; set; }
        public bool HasAttendance { get; set; }
        /// <summary>Ca đang nằm trong đơn đổi ca Chờ duyệt.</summary>
        public bool PendingSwap { get; set; }
        /// <summary>UI-6.37: ca đã chốt bị đổi / bỏ, chưa gửi thông báo (Confirm gửi SHIFT_CHANGED).</summary>
        public bool ChangedAfterConfirm { get; set; }
        public string? ChangedFrom { get; set; }
    }

    public class ShiftGridKpiModel
    {
        public Guid ShiftId { get; set; }
        public string? ShiftCode { get; set; }
        public string? ShiftName { get; set; }
        public int Assigned { get; set; }
        public int CheckedIn { get; set; }
    }

    /// <summary>BE2.6: 1 ca được phân (dùng ở kết quả phân ca, đơn nghỉ, đơn đổi ca, chấm công).</summary>
    public class HrAssignmentModel
    {
        public Guid Id { get; set; }
        public Guid EmployeeId { get; set; }
        public string? EmployeeCode { get; set; }
        public string? EmployeeName { get; set; }
        public DateTime WorkDate { get; set; }
        public Guid ShiftId { get; set; }
        public string? ShiftCode { get; set; }
        public string? ShiftName { get; set; }
        public DateTime? PlannedStart { get; set; }
        public DateTime? PlannedEnd { get; set; }
        public string? Status { get; set; }
        public string? StatusName { get; set; }
        public bool IsDeleted { get; set; }
        public bool HasAttendance { get; set; }
        public string? Note { get; set; }
    }

    /// <summary>BE2.6 (Catalog 202): kết quả phân / đổi / bỏ ca.</summary>
    public class ShiftAssignResultModel
    {
        /// <summary>Created / Updated / Removed / Unchanged.</summary>
        public string Action { get; set; } = string.Empty;
        public HrAssignmentModel? Assignment { get; set; }
        public List<string> ListWarning { get; set; } = [];
    }

    /// <summary>BE2.6 (Catalog 203): kết quả phân ca hàng loạt.</summary>
    public class ShiftBulkAssignResultModel
    {
        public int Created { get; set; }
        public int Skipped { get; set; }
        public List<ShiftBulkSkippedModel> ListSkipped { get; set; } = [];
    }

    public class ShiftBulkSkippedModel
    {
        public Guid EmployeeId { get; set; }
        public string? EmployeeName { get; set; }
        public DateTime WorkDate { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>BE2.6 (Catalog 204): kết quả xác nhận lịch ca.</summary>
    public class ShiftConfirmResultModel
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int Confirmed { get; set; }
        /// <summary>Đã Xác nhận / đã chấm công từ trước (bỏ qua).</summary>
        public int AlreadyConfirmed { get; set; }
        /// <summary>Spec: gửi Telegram cho nhân viên – để sau (tích hợp bot, D-44); luôn false.</summary>
        public bool NotificationSent { get; set; }
        /// <summary>UI-6.37: số người được báo đổi / hủy ca sau chốt (SHIFT_CHANGED).</summary>
        public int ChangedNotified { get; set; }
    }

    /// <summary>BE2.6: kết quả đánh Vắng (thao tác tay).</summary>
    public class ShiftMarkAbsentResultModel
    {
        public int Marked { get; set; }
        public List<HrAssignmentModel> ListAssignment { get; set; } = [];
    }

    /// <summary>
    /// BE2.6 (Catalog 205): kết quả vào / ra ca. Không trả lại tọa độ (dữ liệu cá nhân – chỉ lưu ở AttendanceEvent), chỉ trả khoảng cách tới trạm bếp gần nhất.
    /// </summary>
    public class AttendanceResultModel
    {
        public Guid EventId { get; set; }
        public Guid EmployeeId { get; set; }
        public string? EmployeeName { get; set; }
        public string? EventType { get; set; }
        public string? EventTypeName { get; set; }
        public DateTime EventAt { get; set; }
        public string? SourceChannel { get; set; }
        public string? SourceChannelName { get; set; }
        /// <summary>true = hợp lệ (trong bán kính / QR / máy / chấm hộ); false = ngoài bán kính (HR xem lại).</summary>
        public bool? IsValid { get; set; }
        public decimal? DistanceMeters { get; set; }
        public decimal? RadiusMeters { get; set; }
        public string? WorkstationCode { get; set; }
        public string? WorkstationName { get; set; }
        public HrAssignmentModel? Assignment { get; set; }
        public int LateMinutes { get; set; }
        public int EarlyLeaveMinutes { get; set; }
        /// <summary>Gửi lại cùng ExternalEventId → sự kiện đã ghi trước đó, không ghi thêm.</summary>
        public bool IsDuplicate { get; set; }
        public string? Note { get; set; }
        public List<string> ListWarning { get; set; } = [];
    }

    /// <summary>BE2.6: ca hôm nay (và ca đêm hôm qua chưa ra ca) của nhân viên đang đăng nhập – màn chấm công trên điện thoại.</summary>
    public class MyAttendanceModel
    {
        public Guid EmployeeId { get; set; }
        public string? EmployeeCode { get; set; }
        public string? EmployeeName { get; set; }
        public List<TimesheetDetailModel> ListAssignment { get; set; } = [];
        /// <summary>Hành động làm được lúc này: checkIn / checkOut.</summary>
        public List<string> AllowedActions { get; set; } = [];
    }

    /// <summary>BE2.6 (Catalog 206): 1 dòng bảng công (1 nhân viên trong khoảng ngày).</summary>
    public class TimesheetRowModel
    {
        public int STT { get; set; }
        public Guid EmployeeId { get; set; }
        public string? EmployeeCode { get; set; }
        public string? FullName { get; set; }
        public string? OrganizationUnitName { get; set; }
        public string? PositionName { get; set; }
        /// <summary>Số ca được phân (không tính ca đã bỏ).</summary>
        public int AssignedShifts { get; set; }
        /// <summary>Số ca có vào ca.</summary>
        public int Present { get; set; }
        public int Late { get; set; }
        public int LateMinutes { get; set; }
        public int EarlyLeave { get; set; }
        public int EarlyLeaveMinutes { get; set; }
        /// <summary>Vắng = trạng thái Vắng, hoặc ca đã hết giờ mà chưa vào ca.</summary>
        public int Absent { get; set; }
        /// <summary>Đã vào ca nhưng chưa ra ca (ca đã hết giờ).</summary>
        public int MissingCheckOut { get; set; }
        /// <summary>Lần chấm ngoài bán kính (IsValid = false).</summary>
        public int InvalidEvents { get; set; }
        /// <summary>Số ngày nghỉ (đơn Đã duyệt) trong khoảng.</summary>
        public int LeaveDays { get; set; }
        public int WorkedMinutes { get; set; }
        public decimal WorkedHours { get; set; }
    }

    /// <summary>BE2.6 (Catalog 206): 1 ca của bảng công chi tiết.</summary>
    public class TimesheetDetailModel
    {
        public Guid AssignmentId { get; set; }
        public Guid EmployeeId { get; set; }
        public string? EmployeeCode { get; set; }
        public string? EmployeeName { get; set; }
        public DateTime WorkDate { get; set; }
        public Guid ShiftId { get; set; }
        public string? ShiftCode { get; set; }
        public string? ShiftName { get; set; }
        public DateTime? PlannedStart { get; set; }
        public DateTime? PlannedEnd { get; set; }
        public string? Status { get; set; }
        public string? StatusName { get; set; }
        public DateTime? CheckInAt { get; set; }
        public DateTime? CheckOutAt { get; set; }
        public string? CheckInSource { get; set; }
        public string? CheckOutSource { get; set; }
        public bool? CheckInValid { get; set; }
        public bool? CheckOutValid { get; set; }
        public decimal? CheckInDistanceMeters { get; set; }
        public decimal? CheckOutDistanceMeters { get; set; }
        public int LateMinutes { get; set; }
        public int EarlyLeaveMinutes { get; set; }
        public int WorkedMinutes { get; set; }
        public bool IsAbsent { get; set; }
        public bool MissingCheckOut { get; set; }
    }

    /// <summary>BE2.6 (Catalog 207, 209): chi tiết đơn nghỉ.</summary>
    public class LeaveRequestDetailModel
    {
        public LeaveRequest? Request { get; set; }
        public string? EmployeeCode { get; set; }
        public string? EmployeeName { get; set; }
        public string? LeaveTypeName { get; set; }
        public string? StatusName { get; set; }
        public int Days { get; set; }
        public string? ApprovalRequestNo { get; set; }
        public ApprovalProgressModel? PendingApproval { get; set; }
        /// <summary>Ca của nhân viên trong khoảng nghỉ: còn hiệu lực (sẽ bỏ khi duyệt) hoặc đã bỏ theo đơn này (<c>IsDeleted</c>).</summary>
        public List<HrAssignmentModel> ListAffectedAssignment { get; set; } = [];
        /// <summary>Cảnh báo ca thiếu người (spec): ca bị bỏ do nghỉ cần người thay.</summary>
        public List<string> ListWarning { get; set; } = [];
        /// <summary>UI-6.39 (D-120): hệ quả (phép năm còn, ca bị bỏ, tổ còn người, người nghỉ trùng, người có thể thay).</summary>
        public LeaveImpactModel? Impact { get; set; }
        public List<string> AllowedActions { get; set; } = [];
    }

    /// <summary>BE2.6 (Catalog 208, 209): chi tiết đơn đổi ca.</summary>
    public class ShiftSwapDetailModel
    {
        public ShiftSwapRequest? Request { get; set; }
        public string? RequesterName { get; set; }
        public string? TargetName { get; set; }
        public string? StatusName { get; set; }
        /// <summary>SWAP = hoán đổi 2 ca; GIVE = nhường ca.</summary>
        public string? SwapType { get; set; }
        public HrAssignmentModel? CurrentAssignment { get; set; }
        public HrAssignmentModel? ProposedAssignment { get; set; }
        public string? ApprovalRequestNo { get; set; }
        public ApprovalProgressModel? PendingApproval { get; set; }
        public List<string> ListWarning { get; set; } = [];
        public List<string> AllowedActions { get; set; } = [];
    }
}

namespace AppDelicare.Model.Model
{
    /// <summary>UI-6.37 (D-118): một ô của "Tô ca" (ShiftAssignment/AssignMany) – ShiftId trống = bỏ ca.</summary>
    public class ShiftCellRequest
    {
        public Guid EmployeeId { get; set; }
        public DateTime WorkDate { get; set; }
        public Guid? ShiftId { get; set; }
    }

    public class ShiftAssignManyRequest
    {
        public List<ShiftCellRequest> ListCell { get; set; } = [];
    }

    public class ShiftCellResultModel
    {
        public Guid EmployeeId { get; set; }
        public DateTime WorkDate { get; set; }
        public Guid? ShiftId { get; set; }
        public bool Ok { get; set; }
        public string? Message { get; set; }
    }

    public class ShiftAssignManyResultModel
    {
        public int Done { get; set; }
        public int Failed { get; set; }
        public List<ShiftCellResultModel> ListResult { get; set; } = [];
    }

    /// <summary>UI-6.37: chép ca tuần → tuần (chỉ ô trống).</summary>
    public class ShiftCopyWeekRequest
    {
        public DateTime FromWeekStart { get; set; }
        public DateTime ToWeekStart { get; set; }
        public Guid? OrganizationUnitId { get; set; }
        public List<Guid> EmployeeIds { get; set; } = [];
    }

    /// <summary>UI-6.37: tổng tổ × ngày × ca – đã phân / cần (định biên cấu hình Hr:ShiftMinStaff).</summary>
    public class ShiftUnitTotalModel
    {
        public Guid? UnitId { get; set; }
        public string? UnitName { get; set; }
        public string Day { get; set; } = string.Empty;
        public Guid ShiftId { get; set; }
        public string? ShiftCode { get; set; }
        public int Assigned { get; set; }
        public int? Required { get; set; }
        public bool Short => Required is { } r && Assigned < r;
    }

    public class ShiftWeekStatusModel
    {
        public int DraftCount { get; set; }
        public int ChangedAfterConfirmCount { get; set; }
        public int AssignedCount { get; set; }
        public bool Confirmed { get; set; }
    }

    public class ShiftWeekKpiModel
    {
        public int Assigned { get; set; }
        public int ShortSlots { get; set; }
        public int PeopleWithoutShift { get; set; }
        public int Draft { get; set; }
    }
}

namespace AppDelicare.Model.Model
{
    /// <summary>UI-6.39 (D-120): hệ quả của một đơn nghỉ (xem trước khi tạo / khi duyệt).</summary>
    public class LeaveImpactModel
    {
        public int Days { get; set; }
        public bool IsAnnual { get; set; }
        public int AnnualTotal { get; set; }
        public int AnnualUsed { get; set; }
        public int AnnualRemaining { get; set; }
        public int AnnualAfter { get; set; }
        public bool AnnualOver { get; set; }
        public List<HrAssignmentModel> ListShiftDrop { get; set; } = [];
        public List<LeaveOverlapModel> ListOverlap { get; set; } = [];
        public List<LeaveStaffingModel> ListStaffing { get; set; } = [];
        public List<LeaveReplacementModel> ListReplacement { get; set; } = [];
        public List<string> ListWarning { get; set; } = [];
    }

    public class LeaveOverlapModel
    {
        public Guid EmployeeId { get; set; }
        public string? FullName { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string? Status { get; set; }
        public string? StatusName { get; set; }
    }

    public class LeaveStaffingModel
    {
        public DateTime Day { get; set; }
        public string? ShiftCode { get; set; }
        public int Remaining { get; set; }
        public int Assigned { get; set; }
        public int? Required { get; set; }
        public bool Short => Required is { } r && Remaining < r;
    }

    public class LeaveReplacementModel
    {
        public DateTime Day { get; set; }
        public string? ShiftCode { get; set; }
        public List<HrPersonBriefModel> ListPerson { get; set; } = [];
    }

    public class HrPersonBriefModel
    {
        public Guid Id { get; set; }
        public string? Code { get; set; }
        public string? FullName { get; set; }
    }

    public class LeavePreviewRequest
    {
        public Guid? EmployeeId { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string? LeaveTypeCode { get; set; }
    }

    /// <summary>UI-6.39: đồng ý / từ chối nhận ca (đơn đổi / nhường ca).</summary>
    public class ShiftSwapConsentRequest
    {
        public Guid Id { get; set; }
        public bool Accept { get; set; }
        public string? Note { get; set; }
    }

    /// <summary>UI-6.39: danh sách gộp đơn nghỉ + đổi / nhường ca (HrRequest/GetList).</summary>
    public class HrRequestListRequest
    {
        /// <summary>PENDING (mặc định – gồm Chờ người nhận đồng ý) | APPROVED | CLOSED (từ chối / hủy) | ALL.</summary>
        public string? Status { get; set; }
        /// <summary>LEAVE | SWAP | GIVE.</summary>
        public string? Type { get; set; }
        public string? Keyword { get; set; }
        public Guid? OrganizationUnitId { get; set; }
    }

    public class HrRequestItemModel
    {
        public Guid Id { get; set; }
        /// <summary>LEAVE | SWAP | GIVE.</summary>
        public string Type { get; set; } = "LEAVE";
        public string? TypeName { get; set; }
        public string? RequestNo { get; set; }
        public Guid EmployeeId { get; set; }
        public string? EmployeeName { get; set; }
        public string? UnitName { get; set; }
        public string? Content { get; set; }
        public List<string> ListImpact { get; set; } = [];
        public DateTime? SubmittedAt { get; set; }
        public bool Overdue { get; set; }
        public string? Status { get; set; }
        public string? StatusName { get; set; }
        public List<string> AllowedActions { get; set; } = [];
    }

    public class HrRequestSummaryModel
    {
        public int Pending { get; set; }
        public int PendingOverdue { get; set; }
        public int PendingLeave { get; set; }
        public int PendingSwap { get; set; }
        public List<string> ListOffToday { get; set; } = [];
        public int OffNext7People { get; set; }
        public int OffNext7Days { get; set; }
        public int EmptyShifts { get; set; }
        public List<DateTime> ListEmptyShiftDay { get; set; } = [];
    }

    public class HrCalendarModel
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public List<HrCalendarDayModel> ListDay { get; set; } = [];
    }

    public class HrCalendarDayModel
    {
        public DateTime Day { get; set; }
        public List<HrCalendarEntryModel> ListEntry { get; set; } = [];
        /// <summary>Tổ sẽ thiếu nếu duyệt hết đơn chờ: "Bếp 1 thiếu 2".</summary>
        public List<string> ListShort { get; set; } = [];
    }

    public class HrCalendarEntryModel
    {
        public Guid Id { get; set; }
        /// <summary>LEAVE_APPROVED | LEAVE_PENDING | SWAP.</summary>
        public string Kind { get; set; } = "LEAVE_APPROVED";
        public string? FullName { get; set; }
        public string? Detail { get; set; }
    }
}
