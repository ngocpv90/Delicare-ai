using AppDelicare.Model.Entity;

namespace AppDelicare.Model.Model
{
    /// <summary>T45R (spec 4.1): hồ sơ khách hàng 360°.</summary>
    public class CustomerOverviewModel
    {
        public Customer? Customer { get; set; }
        public List<CustomerContact> ListContact { get; set; } = [];
        public List<CustomerDeliveryLocation> ListLocation { get; set; } = [];
        public List<ContractOverviewModel> ListContract { get; set; } = [];
        /// <summary>15 đơn gần nhất theo ngày suất ăn.</summary>
        public List<OrderBriefModel> ListRecentOrder { get; set; } = [];
        public int OrderCount { get; set; }
        /// <summary>Doanh thu đã đối soát (tổng OrderReconciliation.FinalAmount).</summary>
        public decimal ReconciledRevenue { get; set; }
        public int ComplaintCount { get; set; }
        /// <summary>Số suất trung bình / ngày trong 7 ngày gần nhất (đơn Delivered / Reconciled).</summary>
        public decimal AvgMealsPerDay7d { get; set; }
        // ---- UI-1.3 (D-64): thẻ chỉ số hồ sơ 360 – chỉ thêm trường, không đổi trường cũ ----
        /// <summary>Hợp đồng hiệu lực hôm nay (Active, EffectiveFrom ≤ hôm nay ≤ EffectiveTo / không hạn).</summary>
        public int ActiveContractCount { get; set; }
        /// <summary>Phản ánh chưa xử lý xong (Mới / Đang xử lý).</summary>
        public int OpenComplaintCount { get; set; }
        /// <summary>% phản ánh đúng hạn SLA trên các phản ánh có hạn (xong trước hạn hoặc chưa tới hạn); không có phản ánh có hạn → null.</summary>
        public decimal? ComplaintOnTimePct { get; set; }
        /// <summary>Số đơn đã đối soát (có OrderReconciliation).</summary>
        public int ReconciledOrderCount { get; set; }
        // ---- UI-2 (D-66 f1): màn khách hàng 360 ----
        public string? BranchName { get; set; }
        /// <summary>Tên NV phụ trách (User → Person.FullName, không có thì tên đăng nhập).</summary>
        public string? UserAssignName { get; set; }
        /// <summary>Ngày hết hạn gần nhất của các hợp đồng hiệu lực (null = không hạn / không có).</summary>
        public DateTime? NearestContractEndDate { get; set; }
        public int? DaysToContractEnd { get; set; }
        /// <summary>Phản ánh chưa xong đã quá hạn SLA.</summary>
        public int OverdueComplaintCount { get; set; }
        /// <summary>Khay / hộp đang ở khách (sổ bao bì – tổng còn phải thu).</summary>
        public decimal ContainersAtCustomer { get; set; }
        /// <summary>Tỷ lệ thất thoát khay / hộp cao nhất theo loại (%); ngưỡng cảnh báo (%).</summary>
        public decimal? PackagingLossRatePct { get; set; }
        public decimal PackagingLossThresholdPct { get; set; }
        /// <summary>Khối "Cần chú ý" (D-66 d).</summary>
        public List<CustomerAlertModel> ListAlert { get; set; } = [];
    }

    /// <summary>UI-2 (D-66 d): 1 mục "Cần chú ý" của khách hàng.</summary>
    public class CustomerAlertModel
    {
        /// <summary>CONTRACT_EXPIRING · COMPLAINT_OVERDUE · PACKAGING_LOSS · ORDER_NOT_LOCKED · NO_DELIVERY_LOCATION · NO_PRIMARY_CONTACT.</summary>
        public string Code { get; set; } = string.Empty;
        /// <summary>HIGH / MEDIUM / LOW.</summary>
        public string Severity { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        /// <summary>Màn mở khi bấm (screen-id).</summary>
        public string? Screen { get; set; }
    }

    /// <summary>UI-2 (D-66 f2): xu hướng suất của 1 khách theo ngày ăn + cơ cấu loại suất.</summary>
    public class CustomerTrendModel
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public List<CustomerTrendDayModel> ListDay { get; set; } = [];
        public List<CustomerTrendMealTypeModel> ListMealType { get; set; } = [];
    }

    public class CustomerTrendDayModel
    {
        public DateTime Date { get; set; }
        /// <summary>Suất đặt: Σ dòng đơn (đơn chưa hủy).</summary>
        public decimal Ordered { get; set; }
        /// <summary>Suất giao: Σ POD DeliveredQuantity.</summary>
        public decimal Delivered { get; set; }
        /// <summary>Suất khách nhận: Σ POD AcceptedQuantity.</summary>
        public decimal Accepted { get; set; }
        /// <summary>Suất phục vụ (tính tiền): Σ đối soát AcceptedQuantity.</summary>
        public decimal Served { get; set; }
    }

    public class CustomerTrendMealTypeModel
    {
        public string MealType { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
    }

    /// <summary>UI-1.2 (D-64): 4 thẻ chỉ số của danh sách khách hàng – theo đơn vị làm việc (chi nhánh) và bộ lọc.</summary>
    public class CustomerSummaryModel
    {
        public int TotalCount { get; set; }
        public int ActiveCount { get; set; }
        /// <summary>Số hợp đồng hiệu lực hôm nay của các khách trong danh sách.</summary>
        public int ActiveContractCount { get; set; }
        /// <summary>Tổng suất đơn Đã giao / Đã đối soát 7 ngày gần nhất (không gồm hôm nay) ÷ 7.</summary>
        public decimal AvgMealsPerDay7d { get; set; }
    }

    /// <summary>UI-5 (D-63 r): dữ liệu cho form tạo / sửa khách hàng – 1 lần gọi <c>Customer/GetFormOptions</c>.</summary>
    public class CustomerFormOptionsModel
    {
        /// <summary>Hệ thống đã khai báo chi nhánh (OrganizationUnit.UnitType = BRANCH) → Chi nhánh phục vụ bắt buộc.</summary>
        public bool BranchRequired { get; set; }
        /// <summary>Chỉ đơn vị loại chi nhánh, chưa xóa.</summary>
        public List<CustomerFormOptionItem> ListBranch { get; set; } = [];
        /// <summary>NV phụ trách (<c>Customer.UserAssignId</c> → <c>User.Id</c>): tài khoản đang hoạt động, tên lấy từ hồ sơ Person.</summary>
        public List<CustomerFormOptionItem> ListAssignee { get; set; } = [];
        /// <summary>Danh mục theo tên cột: Industry, CompanySize, Gender, Source, Status.</summary>
        public Dictionary<string, List<CustomerFormOptionItem>> Catalogs { get; set; } = [];
    }

    public class CustomerFormOptionItem
    {
        /// <summary>Id (chi nhánh / người dùng) hoặc mã danh mục.</summary>
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        /// <summary>Thông tin phụ để tìm kiếm (mã chi nhánh / tên đăng nhập).</summary>
        public string? Hint { get; set; }
    }

    public class ContractOverviewModel
    {
        public Guid Id { get; set; }
        public string? ContractNumber { get; set; }
        public string? Name { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Status { get; set; }
        public decimal? ContractValue { get; set; }
        public List<ContractMeal> ListMeal { get; set; } = [];
    }

    public class OrderBriefModel
    {
        public Guid Id { get; set; }
        public string? OrderNumber { get; set; }
        public DateTime? MealDate { get; set; }
        public DateTime? DeliveryDate { get; set; }
        public decimal? TotalQuantity { get; set; }
        public decimal? TotalAmount { get; set; }
        public string? Status { get; set; }
    }

    /// <summary>T45R (spec 4.3): tổng quan nhà cung cấp.</summary>
    public class SupplierOverviewModel
    {
        public Supplier? Supplier { get; set; }
        public List<SupplierMaterialBriefModel> ListMaterial { get; set; } = [];
        public List<PurchaseOrder> ListOpenPurchaseOrder { get; set; } = [];
        /// <summary>20 sự cố gần nhất.</summary>
        public List<SupplierIssue> ListIssue { get; set; } = [];
        /// <summary>Tỷ lệ giao đúng hạn 90 ngày (%), null nếu chưa có phiếu nhập gắn PO có ngày dự kiến.</summary>
        public decimal? OnTimeRate { get; set; }
        /// <summary>Tỷ lệ trả hàng 90 ngày (% số lượng), null nếu chưa nhập.</summary>
        public decimal? RejectRate { get; set; }

        // ---- UI-3 (D-67 f1): thẻ chỉ số + khối Cần chú ý + Đánh giá NCC ----
        /// <summary>PO đang mở (chưa Nhận đủ / Đóng / Hủy) và tổng giá trị.</summary>
        public int OpenPoCount { get; set; }
        public decimal OpenPoAmount { get; set; }
        /// <summary>PO đã duyệt / đã gửi / nhận một phần mà hẹn giao &lt; hôm nay.</summary>
        public int OverduePoCount { get; set; }
        /// <summary>Số phiếu nhập 90 ngày (số phụ thẻ "Giao đúng hạn").</summary>
        public int ReceiptCount90d { get; set; }
        /// <summary>% số lượng đạt QC 90 ngày (= 100 − tỷ lệ trả hàng), null nếu chưa nhập.</summary>
        public decimal? QcPassRate { get; set; }
        /// <summary>Ngày nhận hàng gần nhất.</summary>
        public DateTime? LastReceiptDate { get; set; }
        public int IssueCount { get; set; }
        /// <summary>Sự cố Mới / Đang xử lý.</summary>
        public int OpenIssueCount { get; set; }
        /// <summary>Sự cố chưa xử lý xong (Mới / Đang xử lý) đã quá hạn.</summary>
        public int OverdueIssueCount { get; set; }
        /// <summary>% sự cố đúng hạn trên các sự cố có hạn: Đóng trước hạn, hoặc chưa đóng mà chưa tới hạn; null nếu không có sự cố có hạn.</summary>
        public decimal? IssueOnTimePct { get; set; }
        /// <summary>Lô của NCC đang tạm giữ (còn tồn).</summary>
        public int LotOnHoldCount { get; set; }
        /// <summary>Lô của NCC khả dụng, còn tồn, hết hạn trong <see cref="LotExpiryDays"/> ngày.</summary>
        public int LotNearExpiryCount { get; set; }
        public int LotExpiryDays { get; set; }
        /// <summary>Giá nguyên liệu đang hiệu lực mà hết hiệu lực trong <see cref="PriceExpiryDays"/> ngày.</summary>
        public int PriceExpiringCount { get; set; }
        public int PriceExpiryDays { get; set; }
        /// <summary>Khối "Cần chú ý" (D-67 d) – cùng dạng với khách hàng (Code, Severity, Message, Screen).</summary>
        public List<CustomerAlertModel> ListAlert { get; set; } = [];
    }

    /// <summary>UI-4 (D-68 g1): hồ sơ nhân viên – ngày sinh / giới tính / địa chỉ chỉ có khi người xem có quyền Sửa adm-employee (D-68 e).</summary>
    public class EmployeeOverviewModel
    {
        public Employee? Employee { get; set; }
        /// <summary>true = người xem có quyền Sửa adm-employee → có thông tin cá nhân.</summary>
        public bool CanViewPersonal { get; set; }
        public string? OrganizationUnitName { get; set; }
        public string? PositionName { get; set; }
        /// <summary>Thâm niên (tháng tròn) tính từ ngày vào làm.</summary>
        public int? TenureMonths { get; set; }
        // Tài khoản web (liên kết bot có UserId, hoặc tên đăng nhập = mã NV) + nhóm quyền.
        public Guid? UserId { get; set; }
        public string? UserName { get; set; }
        public bool? UserIsActive { get; set; }
        public List<string> ListUserGroupName { get; set; } = [];
        // Liên kết bot Telegram.
        public bool BotLinked { get; set; }
        public string? BotStatus { get; set; }
        public string? BotUsername { get; set; }
        public DateTime? BotLinkedAt { get; set; }
        // Chỉ số tháng này (theo bảng công).
        public int MonthShifts { get; set; }
        public decimal MonthWorkedHours { get; set; }
        /// <summary>% ca có vào ca mà không muộn, trên số ca đã vào ca; null nếu chưa vào ca nào.</summary>
        public decimal? MonthOnTimePct { get; set; }
        public int MonthAbsent { get; set; }
        public int MonthLate { get; set; }
        // Nghỉ phép năm nay (đơn Đã duyệt) + đơn chờ duyệt (nghỉ + đổi ca).
        public int YearLeaveDays { get; set; }
        public List<EmployeeLeaveTypeModel> ListLeaveByType { get; set; } = [];
        public int PendingRequestCount { get; set; }
        /// <summary>Ca hôm nay → 7 ngày tới (giờ vào / ra nếu đã chấm).</summary>
        public List<TimesheetDetailModel> ListUpcomingShift { get; set; } = [];
        /// <summary>Ngày nghỉ (đơn Đã duyệt) trong 7 ngày tới – tô màu lịch.</summary>
        public List<DateTime> ListLeaveDate { get; set; } = [];
        /// <summary>Giờ công 30 ngày gần nhất theo ngày.</summary>
        public List<EmployeeDayHoursModel> ListDayHours { get; set; } = [];
        /// <summary>10 ca gần nhất đã chấm công (giờ vào / ra, hình thức, hợp lệ, muộn).</summary>
        public List<TimesheetDetailModel> ListRecentAttendance { get; set; } = [];
        /// <summary>Khối "Cần chú ý" (D-68 d).</summary>
        public List<CustomerAlertModel> ListAlert { get; set; } = [];
    }

    public class EmployeeLeaveTypeModel
    {
        public string? LeaveTypeCode { get; set; }
        public int Days { get; set; }
    }

    public class EmployeeDayHoursModel
    {
        public DateTime Date { get; set; }
        public decimal Hours { get; set; }
    }

    /// <summary>UI-3 (D-67 f2): xu hướng mua của 1 NCC theo tháng.</summary>
    public class SupplierTrendModel
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public List<SupplierTrendMonthModel> ListMonth { get; set; } = [];
    }

    public class SupplierTrendMonthModel
    {
        /// <summary>Ngày đầu tháng.</summary>
        public DateTime Month { get; set; }
        /// <summary>Σ giá trị PO (chưa hủy) đặt trong tháng.</summary>
        public decimal PoAmount { get; set; }
        /// <summary>Σ số lượng đạt × đơn giá của phiếu nhập Đã ghi sổ trong tháng.</summary>
        public decimal ReceivedAmount { get; set; }
        public int ReceiptCount { get; set; }
        /// <summary>% phiếu nhập (gắn PO có hẹn giao) nhận đúng hạn; null nếu không có.</summary>
        public decimal? OnTimePct { get; set; }
        /// <summary>% số lượng bị loại; null nếu không nhận.</summary>
        public decimal? RejectPct { get; set; }
    }

    public class SupplierMaterialBriefModel
    {
        public Guid Id { get; set; }
        public Guid IngredientId { get; set; }
        public string? IngredientName { get; set; }
        public decimal? UnitPrice { get; set; }
        public bool IsPreferred { get; set; }
        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
        public string? Status { get; set; }
    }

    /// <summary>T45R (spec 4.6): sơ đồ kho – khu bảo quản → vị trí.</summary>
    public class WarehouseLayoutModel
    {
        public Warehouse? Warehouse { get; set; }
        public List<StorageZoneLayoutModel> ListZone { get; set; } = [];
    }

    public class StorageZoneLayoutModel
    {
        public StorageZone? Zone { get; set; }
        public List<StorageLocation> ListLocation { get; set; } = [];
    }
}
