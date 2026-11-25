namespace AppDelicare.Model.Option
{
    /// <summary>
    /// BE2.6 (T21 – nhân sự): ngưỡng phân ca / chấm công / nghỉ phép, section <c>BusinessRules:Hr</c> của appsettings. Mọi giá trị PROPOSED – BA/PO CONFIRM (D-55).
    /// </summary>
    public sealed class HrRulesOptions
    {
        /// <summary>Được vào ca sớm nhất bao nhiêu phút trước giờ bắt đầu ca.</summary>
        public int CheckInEarlyMinutes { get; set; } = 120;
        /// <summary>Sau giờ kết thúc ca bao nhiêu phút vẫn còn vào ca được (đi muộn rất nhiều); quá → không tìm thấy ca.</summary>
        public int CheckInLateMinutes { get; set; } = 0;
        /// <summary>Đi muộn quá N phút mới ghi "Đi muộn".</summary>
        public int LateGraceMinutes { get; set; } = 5;
        /// <summary>Về sớm hơn giờ kết thúc ca quá N phút mới ghi "Về sớm".</summary>
        public int EarlyLeaveGraceMinutes { get; set; } = 5;
        /// <summary>true = chấm GPS ngoài bán kính bếp bị chặn (không ghi); false = vẫn ghi với IsValid = false + cảnh báo (HR xem lại trên bảng công).</summary>
        public bool RejectOutsideRadius { get; set; }
        /// <summary>Bán kính mặc định (m) khi trạm bếp có tọa độ nhưng chưa nhập RadiusMeters.</summary>
        public decimal DefaultRadiusMeters { get; set; } = 150m;
        /// <summary>Phân ca hàng loạt tối đa N ngày / lần.</summary>
        public int BulkAssignMaxDays { get; set; } = 31;
        /// <summary>Bảng công tối đa N ngày / lần.</summary>
        public int TimesheetMaxDays { get; set; } = 62;
        /// <summary>Đơn nghỉ được lập lùi tối đa N ngày (nghỉ ốm đột xuất).</summary>
        public int LeaveMaxBackdateDays { get; set; } = 7;
        /// <summary>Một đơn nghỉ tối đa N ngày.</summary>
        public int LeaveMaxDays { get; set; } = 30;
        /// <summary>Lưới ca: số ngày mặc định (spec: 7).</summary>
        public int GridDays { get; set; } = 7;
        /// <summary>UI-6.37 (D-118 d): cảnh báo khi một người có ≥ N ca / tuần (chỉ cảnh báo).</summary>
        public int MaxShiftsPerWeek { get; set; } = 6;
        /// <summary>UI-6.37 (D-118 c): định biên { "mã đơn vị": { "mã ca": n, "T7:mã ca": n, "CN:mã ca": n } } – thiếu cấu hình → không tính thiếu.</summary>
        public Dictionary<string, Dictionary<string, int>> ShiftMinStaff { get; set; } = [];
        /// <summary>UI-6.37 (D-118 f): nhắc lịch tuần sau – thứ (1 = Thứ 2 … 7 = CN; mặc định 5 = Thứ 6) và giờ.</summary>
        public int ScheduleRemindDay { get; set; } = 5;
        public string ScheduleRemindTime { get; set; } = "15:00";
        /// <summary>
        /// HR-ACC-1.3 (D-82 i): true = khi tài khoản chưa gắn nhân viên qua Person (Employee.PersonId), dùng tạm cách đoán cũ (liên kết bot → tên đăng nhập = mã NV → SĐT)
        /// và ghi nhật ký cảnh báo mỗi lần. Giữ 1 bản phát hành rồi đặt false (NS gắn hết ở tab Đối chiếu).
        /// </summary>
        public bool LegacyEmployeeMatch { get; set; } = true;
    }
}
