namespace AppDelicare.Model.Helper
{
    /// <summary>
    /// BE2.6 (T21, spec 11.4 – STATUS_CATALOG): <c>ShiftAssignment.AssignmentStatus</c>. Planned (mặc định SQL) → Confirmed → CheckedIn → Completed / Absent (PROPOSED).
    /// </summary>
    public static class ShiftAssignmentStatus
    {
        public const string Planned = "Planned";
        public const string Confirmed = "Confirmed";
        public const string CheckedIn = "CheckedIn";
        public const string Completed = "Completed";
        public const string Absent = "Absent";

        /// <summary>Ca chưa có chấm công – còn đổi / bỏ / nghỉ được.</summary>
        public static readonly string[] Open = [Planned, Confirmed];

        public static string Label(string? status) => status switch
        {
            Planned => "Kế hoạch",
            Confirmed => "Xác nhận",
            CheckedIn => "Đã vào ca",
            Completed => "Hoàn thành",
            Absent => "Vắng",
            _ => status ?? string.Empty,
        };
    }

    /// <summary>BE2.6: <c>AttendanceEvent.EventType</c> (STATUS_CATALOG: CHECK_IN / CHECK_OUT – PROPOSED) và <c>SourceChannel</c> (BUSINESS_RULES §10).</summary>
    public static class AttendanceEventType
    {
        public const string CheckIn = "CHECK_IN";
        public const string CheckOut = "CHECK_OUT";

        public static string Label(string? type) => type switch
        {
            CheckIn => "Vào ca",
            CheckOut => "Ra ca",
            _ => type ?? string.Empty,
        };
    }

    /// <summary>
    /// BE2.6 (BUSINESS_RULES §10): hình thức chấm công GPS / QR / MACHINE / PROXY. Spec 11.2 gửi MOBILE / ZALO / KIOSK → quy về GPS / GPS / MACHINE
    /// (PROPOSED – D-55 (c)).
    /// </summary>
    public static class AttendanceSource
    {
        public const string Gps = "GPS";
        public const string Qr = "QR";
        public const string Machine = "MACHINE";
        public const string Proxy = "PROXY";

        /// <summary>Chuẩn hóa mã kênh; null = mã không hợp lệ.</summary>
        public static string? Normalize(string? channel) => channel?.Trim().ToUpperInvariant() switch
        {
            null or "" => null,
            Gps or "MOBILE" or "ZALO" or "APP" => Gps,
            Qr => Qr,
            Machine or "KIOSK" or "DEVICE" => Machine,
            Proxy or "MANUAL" => Proxy,
            _ => null,
        };

        public static string Label(string? channel) => channel switch
        {
            Gps => "GPS (điện thoại)",
            Qr => "Quét QR tại bếp",
            Machine => "Máy chấm công",
            Proxy => "Chấm hộ",
            _ => channel ?? string.Empty,
        };
    }

    /// <summary>BE2.6 (spec 11.3, STATUS_CATALOG): <c>LeaveRequest.RequestStatus</c> + ApprovalRequest LEAVE_REQUEST (quyền mục <c>#/wf-inbox?type=hr-leave</c>).</summary>
    public static class LeaveRequestStatus
    {
        public const string Pending = "Pending";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
        public const string Cancelled = "Cancelled";

        public const string BusinessType = "LEAVE_REQUEST";
        public const string ScreenId = "hr-leave";

        public static string Label(string? status) => status switch
        {
            Pending => "Chờ duyệt",
            Approved => "Đã duyệt",
            Rejected => "Từ chối",
            Cancelled => "Đã hủy",
            _ => status ?? string.Empty,
        };
    }

    /// <summary>BE2.6 (spec 11.3): <c>ShiftSwapRequest.RequestStatus</c> + ApprovalRequest SHIFT_SWAP (quyền mục <c>#/wf-inbox?type=hr-swap</c>).</summary>
    public static class ShiftSwapStatus
    {
        public const string Pending = "Pending";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
        public const string Cancelled = "Cancelled";
        /// <summary>UI-6.39 (D-120): người nhận chưa đồng ý – chưa có yêu cầu duyệt.</summary>
        public const string AwaitingConsent = "AwaitingConsent";

        public const string BusinessType = "SHIFT_SWAP";
        public const string ScreenId = "hr-swap";

        public static string Label(string? status) => status == AwaitingConsent ? "Chờ người nhận đồng ý" : LeaveRequestStatus.Label(status);
    }

    /// <summary>BE2.6: màn hình (screen-id) nhân sự dùng khi kiểm quyền trong service (thao tác tự phục vụ / chấm hộ).</summary>
    public static class HrScreen
    {
        public const string ShiftAssign = "hr-shift-assign";
        public const string Attendance = "hr-attendance";
        public const string Leave = "hr-leave";
        /// <summary>UI-6.39 / 6.46: Đơn đổi ca gộp vào "Nghỉ &amp; đổi ca" (mục hr-swap ẩn, quyền đã hợp vào hr-leave).</summary>
        public const string Swap = "hr-leave";
    }

    /// <summary>BE2.6: <c>Employee.Status</c> chưa có danh mục (STATUS_CATALOG) – coi Inactive / Resigned là đã nghỉ việc (PROPOSED – D-55 (a)).</summary>
    public static class EmployeeStatus
    {
        public const string Active = "Active";
        public const string Inactive = "Inactive";
        public const string Resigned = "Resigned";

        public static bool IsWorking(string? status) => status is not (Inactive or Resigned);
    }
}
