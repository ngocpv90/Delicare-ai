using System.ComponentModel.DataAnnotations;

namespace AppDelicare.Model.Request.Custom
{
    /// <summary>BE2.6 (spec 11.1 shift-grid, Catalog 201): lưới ca nhân viên × ngày.</summary>
    public class ShiftGridRequest
    {
        /// <summary>Ngày đầu; trống = hôm nay.</summary>
        public DateTime? FromDate { get; set; }
        /// <summary>Số ngày (1–31); trống = <c>BusinessRules:Hr:GridDays</c> (7).</summary>
        public int? Days { get; set; }
        /// <summary>Đơn vị / tổ (OrganizationUnit) – gồm cả đơn vị con; trống = mọi nhân viên.</summary>
        public Guid? OrganizationUnitId { get; set; }
        /// <summary>BOT-3.2 (D-72): true = đơn vị của nhân viên gắn với tài khoản đang gọi (tổ trưởng xem "tổ tôi"); bỏ qua khi đã truyền OrganizationUnitId.</summary>
        public bool? MyUnit { get; set; }
        /// <summary>Tìm theo mã / tên nhân viên.</summary>
        [MaxLength(200)]
        public string? SearchText { get; set; }
        /// <summary>UI-6.37 (D-118): ngày bất kỳ trong tuần → lưới Thứ 2 – CN (7 ngày); bỏ qua FromDate / Days.</summary>
        public DateTime? WeekStart { get; set; }
        public Guid? PositionId { get; set; }
    }

    /// <summary>
    /// BE2.6 (spec 11.1 PUT shift-assignments, Catalog 202): phân / đổi / bỏ ca 1 nhân viên 1 ngày (upsert theo nhân viên + ngày). <c>ShiftId</c> trống = bỏ phân ca.
    /// <c>PlannedStart / PlannedEnd</c> trống = theo giờ ca (ca qua đêm → kết thúc ngày hôm sau).
    /// </summary>
    public class ShiftAssignRequest
    {
        [Required]
        public Guid EmployeeId { get; set; }
        [Required]
        public DateTime WorkDate { get; set; }
        public Guid? ShiftId { get; set; }
        public DateTime? PlannedStart { get; set; }
        public DateTime? PlannedEnd { get; set; }
        [MaxLength(1000)]
        public string? Note { get; set; }
    }

    /// <summary>
    /// BE2.6 (spec 11.1 bulk, Catalog 203): phân ca hàng loạt. Form: <c>EmployeeIds</c> lặp lại nhiều lần (trống = mọi nhân viên của <c>OrganizationUnitId</c>);
    /// <c>SkipWeekdays</c> lặp lại (1 = Thứ 2 … 7 = Chủ nhật).
    /// </summary>
    public class ShiftBulkAssignRequest
    {
        public List<Guid> EmployeeIds { get; set; } = [];
        public Guid? OrganizationUnitId { get; set; }
        [Required]
        public Guid ShiftId { get; set; }
        [Required]
        public DateTime FromDate { get; set; }
        [Required]
        public DateTime ToDate { get; set; }
        public List<int> SkipWeekdays { get; set; } = [];
        [MaxLength(1000)]
        public string? Note { get; set; }
    }

    /// <summary>BE2.6 (spec 11.1 confirm, Catalog 204): xác nhận lịch ca Kế hoạch → Xác nhận của 1 ngày (hoặc khoảng ngày). Không gửi Telegram (để sau – tích hợp bot).</summary>
    public class ShiftConfirmRequest
    {
        [Required]
        public DateTime WorkDate { get; set; }
        /// <summary>Đến ngày (tùy chọn, ≤ 31 ngày); trống = chỉ <see cref="WorkDate"/>.</summary>
        public DateTime? ToDate { get; set; }
        public Guid? OrganizationUnitId { get; set; }
        public Guid? ShiftId { get; set; }
    }

    /// <summary>BE2.6 (spec 11.2 "job cuối ngày đánh Absent" – D-45 không job): bấm tay đánh Vắng cho ca đã hết giờ mà không vào ca.</summary>
    public class ShiftMarkAbsentRequest
    {
        [Required]
        public DateTime WorkDate { get; set; }
        public Guid? OrganizationUnitId { get; set; }
    }

    /// <summary>
    /// BE2.6 (spec 11.2 check-in / check-out, Catalog 205). <c>EmployeeId</c> trống = nhân viên của tài khoản đang đăng nhập (GAP-HR-02). Chấm cho người khác
    /// (máy chấm công / chấm hộ / kênh bot) cần quyền Thêm trên <c>#/hr-attendance</c> và phải ghi rõ <c>SourceChannel</c>.
    /// <c>SourceChannel</c>: GPS / QR / MACHINE / PROXY (MOBILE, ZALO → GPS; KIOSK → MACHINE). QR: <c>WorkstationCode</c> = mã trên QR của trạm bếp.
    /// </summary>
    public class AttendanceCheckRequest
    {
        public Guid? EmployeeId { get; set; }
        [Range(-90, 90)]
        public decimal? Latitude { get; set; }
        [Range(-180, 180)]
        public decimal? Longitude { get; set; }
        [MaxLength(50)]
        public string? SourceChannel { get; set; }
        [MaxLength(50)]
        public string? WorkstationCode { get; set; }
        /// <summary>Chống trùng (gửi lại cùng mã → trả sự kiện đã ghi).</summary>
        [MaxLength(200)]
        public string? ExternalEventId { get; set; }
        [MaxLength(500)]
        public string? Note { get; set; }
    }

    /// <summary>BE2.6 (spec 11.2 report, Catalog 206 – dạng danh sách): bảng công theo khoảng ngày.</summary>
    public class TimesheetRequest
    {
        [Required]
        public DateTime FromDate { get; set; }
        [Required]
        public DateTime ToDate { get; set; }
        public Guid? OrganizationUnitId { get; set; }
        public Guid? EmployeeId { get; set; }
        [MaxLength(200)]
        public string? SearchText { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    /// <summary>BE2.6 (spec 11.3 leave-requests, Catalog 207): đơn xin nghỉ. <c>EmployeeId</c> trống = nhân viên của tài khoản đang đăng nhập.</summary>
    public class LeaveCreateRequest
    {
        public Guid? EmployeeId { get; set; }
        [Required]
        public DateTime FromDate { get; set; }
        [Required]
        public DateTime ToDate { get; set; }
        [Required]
        [MaxLength(50)]
        public string? LeaveTypeCode { get; set; }
        [MaxLength(1000)]
        public string? Reason { get; set; }
    }

    /// <summary>
    /// BE2.6 (spec 11.3 shift-swap-requests, Catalog 208): đơn đổi ca. Người xin = nhân viên của <c>CurrentShiftAssignmentId</c>.
    /// Có <c>ProposedShiftAssignmentId</c> → hoán đổi 2 ca (người nhận = nhân viên của ca đó); trống → nhường ca cho <c>TargetEmployeeId</c> (bắt buộc).
    /// </summary>
    public class ShiftSwapCreateRequest
    {
        [Required]
        public Guid CurrentShiftAssignmentId { get; set; }
        public Guid? ProposedShiftAssignmentId { get; set; }
        public Guid? TargetEmployeeId { get; set; }
        [MaxLength(1000)]
        public string? Reason { get; set; }
    }

    /// <summary>BE2.6: duyệt / từ chối đơn nghỉ / đổi ca qua engine (lý do bắt buộc khi từ chối).</summary>
    public class HrDecisionRequest
    {
        [Required]
        public Guid Id { get; set; }
        [MaxLength(1000)]
        public string? Note { get; set; }
    }

    /// <summary>BE2.6 (spec 11.3 cancel, Catalog 209): người tạo hủy đơn còn Chờ duyệt (hủy luôn ApprovalRequest).</summary>
    public class HrCancelRequest
    {
        [Required]
        public Guid Id { get; set; }
        [MaxLength(1000)]
        public string? Reason { get; set; }
    }
}
