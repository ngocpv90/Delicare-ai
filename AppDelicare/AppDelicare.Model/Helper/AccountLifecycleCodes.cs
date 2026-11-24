namespace AppDelicare.Model.Helper
{
    /// <summary>
    /// HR-ACC-1 (D-82 f–h): vòng đời tài khoản web của nhân viên qua engine duyệt. Người duyệt FIN-IT – mục quyền <c>#/wf-inbox?type=adm-user</c> "Phê duyệt › Tài khoản".
    /// BusinessId của cả 3 loại = Employee.Id; nội dung đề xuất (nhóm quyền, lý do) ở PayloadJson.proposal.
    /// </summary>
    public static class AccountLifecycleCodes
    {
        public const string CreateBusinessType = "USER_ACCOUNT_CREATE";
        public const string GroupBusinessType = "USER_ACCOUNT_GROUP";
        public const string ReopenBusinessType = "USER_ACCOUNT_REOPEN";
        public const string ScreenId = "adm-user";
        public static readonly string[] BusinessTypes = [CreateBusinessType, GroupBusinessType, ReopenBusinessType];

        // Trạng thái khối Tài khoản trên Nhân viên 360 (D-82 j) – tính ra, không lưu.
        public const string StatusNone = "None";
        public const string StatusPending = "Pending";
        public const string StatusActive = "Active";
        public const string StatusLocked = "Locked";

        public static string StatusLabel(string? status) => status switch
        {
            StatusNone => "Chưa có tài khoản",
            StatusPending => "Đang chờ IT duyệt",
            StatusActive => "Hoạt động",
            StatusLocked => "Đã khóa",
            _ => status ?? string.Empty,
        };

        public static string TypeLabel(string? businessType) => businessType switch
        {
            CreateBusinessType => "Cấp tài khoản",
            GroupBusinessType => "Đổi nhóm quyền",
            ReopenBusinessType => "Mở lại tài khoản",
            _ => businessType ?? string.Empty,
        };

        /// <summary>Nhãn loại tài khoản trên màn Tài khoản (D-82 j): nội bộ có nhân viên = Nhân viên, nội bộ không có = Hệ thống.</summary>
        public static string KindLabel(string? accountType, bool hasEmployee) => (accountType ?? "INTERNAL").ToUpperInvariant() switch
        {
            "CUSTOMER" => "Khách hàng",
            "SUPPLIER" => "Nhà cung cấp",
            _ => hasEmployee ? "Nhân viên" : "Hệ thống",
        };
    }
}
