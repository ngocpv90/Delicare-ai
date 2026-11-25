namespace AppDelicare.Model.Model
{
    /// <summary>UI-6.36 (D-117): danh sách "Nhân viên" theo nghiệp vụ nhân sự (Employee/GetList).</summary>
    public class EmployeeListRequest
    {
        /// <summary>Active (mặc định) | Inactive | Resigned | ALL.</summary>
        public string? Status { get; set; }
        /// <summary>Gồm cả đơn vị con.</summary>
        public Guid? OrganizationUnitId { get; set; }
        public Guid? PositionId { get; set; }
        public string? Keyword { get; set; }
        /// <summary>NEW_MONTH | NO_ACCOUNT | NO_BOT.</summary>
        public string? Flag { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 500;
    }

    public class EmployeeListModel
    {
        public List<EmployeeListItemModel> ListItem { get; set; } = [];
        public int TotalRecords { get; set; }
        public int WorkingCount { get; set; }
        public int TotalCount { get; set; }
        public int NewMonthCount { get; set; }
        public int NoAccountCount { get; set; }
        public int PendingAccountCount { get; set; }
        public int NoBotCount { get; set; }
        public Dictionary<string, int> StatusCounts { get; set; } = [];
        /// <summary>Nhân viên đang làm chưa gắn con người (tab Đối chiếu tài khoản).</summary>
        public int UnlinkedCount { get; set; }
    }

    public class EmployeeListItemModel
    {
        public Guid Id { get; set; }
        public string? Code { get; set; }
        public string? FullName { get; set; }
        public string? Phone { get; set; }
        public Guid? OrganizationUnitId { get; set; }
        public string? OrganizationUnitName { get; set; }
        public Guid? PositionId { get; set; }
        public string? PositionName { get; set; }
        public DateTime? HireDate { get; set; }
        public string? Status { get; set; }
        public string? StatusName { get; set; }
        /// <summary>ACTIVE | LOCKED | PENDING | NONE.</summary>
        public string AccountStatus { get; set; } = "NONE";
        public string? AccountStatusName { get; set; }
        public string? UserName { get; set; }
        public bool BotLinked { get; set; }
        /// <summary>Đã hẹn nghỉ việc (ngày tương lai).</summary>
        public DateTime? ScheduledResignDate { get; set; }
    }

    public class EmployeeResignRequest
    {
        public Guid Id { get; set; }
        public DateTime? ResignDate { get; set; }
        public string? ReasonCode { get; set; }
        public string? Note { get; set; }
    }

    public class EmployeeSetStatusRequest
    {
        public Guid Id { get; set; }
        /// <summary>Inactive (tạm nghỉ) | Active (quay lại làm).</summary>
        public string? Status { get; set; }
        public string? Note { get; set; }
    }

    public class EmployeeIdsRequest
    {
        public List<Guid> ListId { get; set; } = [];
        public string? Reason { get; set; }
    }

    public class ResignPreviewModel
    {
        public Guid EmployeeId { get; set; }
        public string? FullName { get; set; }
        public DateTime ResignDate { get; set; }
        public bool Immediate { get; set; }
        public string? UserName { get; set; }
        public bool HasActiveAccount { get; set; }
        public int BotLinks { get; set; }
        public List<DateTime> ListShiftDate { get; set; } = [];
        public int HeldApprovals { get; set; }
        public List<string> ListMessage { get; set; } = [];
    }

    public class EmployeeBulkResultModel
    {
        public int Done { get; set; }
        public int Skipped { get; set; }
        public List<string> ListSkipped { get; set; } = [];
        /// <summary>Mã liên kết bot vừa tạo (thao tác hàng loạt).</summary>
        public List<EmployeeLinkCodeModel> ListCode { get; set; } = [];
    }

    public class EmployeeLinkCodeModel
    {
        public Guid EmployeeId { get; set; }
        public string? Code { get; set; }
        public string? FullName { get; set; }
        public string? LinkCode { get; set; }
        public string? DeepLink { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }

    public class EmployeePhoneCheckModel
    {
        public bool Valid { get; set; }
        public bool Duplicate { get; set; }
        public string? Message { get; set; }
    }

    /// <summary>UI-6.36: dải "Việc cần làm cho người mới" (hồ sơ – vào làm ≤ 30 ngày).</summary>
    public class EmployeeOnboardingItemModel
    {
        /// <summary>ACCOUNT | BOT | SHIFT | HEALTH.</summary>
        public string Code { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public bool Done { get; set; }
        public string? Detail { get; set; }
    }
}
