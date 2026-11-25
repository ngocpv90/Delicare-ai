using AppDelicare.Model.Helper;

namespace AppDelicare.Services.Implement.Messaging
{
    /// <summary>Một mẫu tin: phạm vi (chọn bot), mức (KHẨN / THƯỜNG / TỔNG HỢP), mô tả cho màn quản trị. Câu chữ nằm ở AppDelicare.Messaging/Presentation.</summary>
    public sealed record NotificationTemplate(string Code, string Scope, string Level, string Description);

    /// <summary>
    /// BOT-1.1 khung → BOT-1.7 đủ danh mục theo BOT_TELEGRAM §5.1 (mã mẫu, phạm vi → bot đích, mức). Service nghiệp vụ chỉ gọi
    /// <see cref="BusinessNotifier"/> / <see cref="NotificationService"/> với mã ở đây – không ghi câu chữ. Mẫu theo lịch (ProgressWatch, bản tin…) phát ở BOT-2.x.
    /// </summary>
    public static class NotificationCatalog
    {
        public const string ManualText = "MANUAL_TEXT";
        public const string SystemTest = "SYSTEM_TEST";
        // Cá nhân / duyệt
        public const string ShiftConfirmed = "SHIFT_CONFIRMED";
        public const string ApprovalPending = "APPROVAL_PENDING";
        public const string ApprovalResult = "APPROVAL_RESULT";
        // Bếp / ATTP
        public const string BatchLate = "BATCH_LATE";
        public const string ProductionVariance = "PRODUCTION_VARIANCE";
        public const string CoreTempLow = "CORE_TEMP_LOW";
        public const string FoodSafetyCritical = "FOODSAFETY_CRITICAL";
        public const string IotOutOfRange = "IOT_OUT_OF_RANGE";
        /// <summary>UI-6.31: khu đo tay đến giờ chưa đo / cảm biến mất tín hiệu.</summary>
        public const string TemperatureDue = "TEMPERATURE_DUE";
        /// <summary>UI-6.34: chuyến đủ hàng / sắp xuất bến mà chưa đủ / chưa tự lập được chuyến.</summary>
        public const string RouteReady = "ROUTE_READY";
        /// <summary>UI-6.33: sự cố NCC quá hạn xử lý.</summary>
        public const string SupplierIssueOverdue = "SUPPLIER_ISSUE_OVERDUE";
        public const string ChecklistDue = "CHECKLIST_DUE";
        // Kế hoạch / kho / mua hàng
        public const string PlanReady = "PLAN_READY";
        public const string ShortageMrp = "SHORTAGE_MRP";
        public const string StockLow = "STOCK_LOW";
        public const string LotExpiring = "LOT_EXPIRING";
        public const string PoLate = "PO_LATE";
        public const string QcIncomingFail = "QC_INCOMING_FAIL";
        // Giao vận / điểm phát
        public const string TripNotDeparted = "TRIP_NOT_DEPARTED";
        public const string TripDeparted = "TRIP_DEPARTED";
        public const string StopLate = "STOP_LATE";
        public const string DeliveryTempLow = "DELIVERY_TEMP_LOW";
        public const string ShortageReported = "SHORTAGE_REPORTED";
        // Khách hàng / kinh doanh
        public const string ComplaintNew = "COMPLAINT_NEW";
        public const string ComplaintSla = "COMPLAINT_SLA";
        public const string ContainerLoss = "CONTAINER_LOSS";
        public const string OrdersUnconfirmed = "ORDERS_UNCONFIRMED";
        public const string MenuPublished = "MENU_PUBLISHED";
        // Nhân sự / điều hành / hệ thống
        public const string NotCheckedIn = "NOT_CHECKED_IN";
        public const string MorningBrief = "MORNING_BRIEF";
        public const string DailyReport = "DAILY_REPORT";
        public const string PeriodReport = "PERIOD_REPORT";
        public const string SystemJobFailed = "SYSTEM_JOB_FAILED";
        public const string OutboundFailing = "OUTBOUND_FAILING";
        public const string IncidentUnclaimed = "INCIDENT_UNCLAIMED";
        // BOT-2.1: thẻ ghim đầu chat (messaging ghim sau khi gửi, sửa khi nội dung đổi).
        public const string ShiftCard = "SHIFT_CARD";
        public const string DayCard = "DAY_CARD";
        // BOT-2.2: nhắc người duyệt trước ngày nghỉ.
        public const string LeaveHandover = "LEAVE_HANDOVER";
        // BOT-2.3: tin theo lịch.
        public const string ApprovalOverdue = "APPROVAL_OVERDUE";
        /// <summary>APR-1.7 (D-81): tổng hợp duyệt cuối ngày – người duyệt (còn chờ, quá hạn, có cảnh báo).</summary>
        public const string ApprovalDigest = "APPROVAL_DIGEST";
        public const string IngredientsTonight = "INGREDIENTS_TONIGHT";
        /// <summary>BIZ-3.4: thứ Tư chưa có thực đơn chung tuần sau.</summary>
        public const string MenuMissing = "MENU_MISSING";
        /// <summary>BIZ-3.4: thứ Sáu khách chưa đăng ký suất tuần sau.</summary>
        public const string OrderWeekMissing = "ORDER_WEEK_MISSING";
        /// <summary>BIZ-3.3: giá NCC đổi làm thực đơn vượt FC %.</summary>
        public const string FoodCostOver = "FOOD_COST_OVER";

        private const string P = MessagingCode.Scope.Personal, D = MessagingCode.Scope.Department, X = MessagingCode.Scope.CrossDepartment,
            A = MessagingCode.Scope.Approval, C = MessagingCode.Scope.Critical;
        private const string Urgent = MessagingCode.Level.Urgent, Normal = MessagingCode.Level.Normal, Digest = MessagingCode.Level.Digest;

        private static readonly Dictionary<string, NotificationTemplate> Templates = new NotificationTemplate[]
        {
            new(ManualText, P, Normal, "Tin gửi tay từ màn Tin nhắn đi (#/int-outbound)"),
            new(SystemTest, P, Normal, "Tin thử kết nối bot"),
            new(ShiftConfirmed, P, Normal, "Phân ca đã xác nhận (ShiftAssignment/Confirm) – nhân viên được phân"),
            new(ApprovalPending, A, Normal, "Cấp duyệt mới (gửi duyệt / chuyển cấp) – người duyệt của cấp"),
            new(ApprovalResult, P, Normal, "Kết quả duyệt (đơn nghỉ / đổi ca / chứng từ) – người gửi"),
            new(BatchLate, D, Urgent, "Mẻ trễ > 15′ – trưởng ca"),
            new(ProductionVariance, D, Normal, "Chênh lệch tiêu hao vượt ngưỡng của ngày (1 tin gộp lúc đóng lệnh cuối ngày) – Bếp trưởng + COO"),
            new(CoreTempLow, X, Urgent, "Nhiệt độ lõi < 70 °C – trưởng ca, QA, bếp trưởng"),
            new(FoodSafetyCritical, C, Urgent, "Lỗi trọng yếu checklist ATTP – QA, trưởng ca, bếp trưởng, COO"),
            new(IotOutOfRange, D, Urgent, "Nhiệt độ kho / khu ngoài ngưỡng (cảm biến hoặc đo tay) – QA, thủ kho"),
            new(SupplierIssueOverdue, D, Normal, "Sự cố nhà cung cấp quá hạn xử lý – QA, mua hàng"),
            new(RouteReady, D, Normal, "Chuyến đủ hàng / sắp xuất bến mà chưa đủ thực đơn – điều phối"),
            new(TemperatureDue, D, Normal, "Khu chưa đo nhiệt độ trong ca / cảm biến mất tín hiệu – trưởng ca, QA"),
            new(ChecklistDue, D, Normal, "Kiểm tra ATTP theo ca quá hạn – trưởng ca, QA"),
            new(PlanReady, X, Normal, "Khóa đơn → kế hoạch / lịch xe chờ duyệt"),
            new(ShortageMrp, X, Normal, "Thiếu nguyên liệu sau MRP – mua hàng, thủ kho"),
            new(StockLow, D, Normal, "Dưới tồn tối thiểu – thủ kho"),
            new(LotExpiring, D, Normal, "Lô cận / hết hạn – thủ kho"),
            new(PoLate, X, Normal, "PO trễ hẹn giao – thủ kho, mua hàng"),
            new(QcIncomingFail, X, Urgent, "QC đầu vào không đạt – thủ kho, QA, mua hàng"),
            new(TripNotDeparted, D, Urgent, "Xe chưa rời bếp quá giờ – điều phối"),
            new(TripDeparted, D, Normal, "Xe xuất phát – điểm phát suất"),
            new(StopLate, X, Urgent, "Điểm giao trễ – điều phối"),
            new(DeliveryTempLow, X, Urgent, "Nhiệt độ giao < 60 °C – điều phối"),
            new(ShortageReported, X, Urgent, "Báo thiếu suất – bếp trưởng, điều phối, TP KD"),
            new(ComplaintNew, D, Normal, "Phản ánh mới – CSKH (mức cao: liên phòng)"),
            new(ComplaintSla, X, Urgent, "Phản ánh sắp quá hạn – CSKH, TP KD"),
            new(ContainerLoss, X, Digest, "Thất thoát hộp khay vượt ngưỡng – kế toán"),
            new(OrdersUnconfirmed, D, Urgent, "19:00 khách chưa chốt – NV KD"),
            new(MenuMissing, X, Urgent, "Thứ Tư 09:00 chưa có thực đơn chung tuần sau – Kế hoạch, COO"),
            new(OrderWeekMissing, D, Normal, "Thứ Sáu 16:00 khách chưa đăng ký suất tuần sau – NV KD"),
            new(FoodCostOver, X, Urgent, "Giá NCC đổi → thực đơn vượt FC % – Kế hoạch, COO"),
            new(MenuPublished, D, Digest, "Thực đơn công bố – kế hoạch, KD, bếp"),
            new(NotCheckedIn, D, Normal, "Chưa vào ca > 10′ – trưởng ca, nhân sự"),
            new(MorningBrief, X, Digest, "Bản tin 07:00"),
            new(DailyReport, X, Digest, "Báo cáo 17:00"),
            new(PeriodReport, X, Digest, "Báo cáo tuần / tháng"),
            new(SystemJobFailed, D, Urgent, "Job tự động lỗi – IT"),
            new(OutboundFailing, D, Urgent, "Tin gửi lỗi nhiều lần – IT"),
            new(IncidentUnclaimed, X, Urgent, "Sự việc liên phòng quá hạn chưa ai nhận – COO, PMO"),
            new(ShiftCard, P, Normal, "Thẻ ca (15′ trước ca, ghim, cập nhật trong ca) – BOT-2.1"),
            new(DayCard, X, Digest, "Thẻ ngày 07:00 cho quản lý (bot DH, ghim) – BOT-2.1"),
            new(LeaveHandover, P, Normal, "Người duyệt sắp nghỉ: còn việc chờ duyệt – BOT-2.2"),
            new(ApprovalOverdue, A, Normal, "Duyệt quá hạn – người duyệt của cấp; quá thêm 1 ngày → COO (bot DH) – BOT-2.3"),
            new(ApprovalDigest, A, Digest, "Tổng hợp duyệt cuối ngày (Messaging:ApprovalDigestHour) – người duyệt còn việc chờ – APR-1.7"),
            new(IngredientsTonight, D, Normal, "21:00 nguyên liệu cho sản xuất ngày mai – thủ kho – BOT-2.3"),
        }.ToDictionary(t => t.Code, StringComparer.OrdinalIgnoreCase);

        public static NotificationTemplate? Find(string? code)
            => code != null && Templates.TryGetValue(code, out var t) ? t : null;

        public static IReadOnlyCollection<NotificationTemplate> All => Templates.Values;

        /// <summary>Mẫu được ghim đầu chat sau khi gửi (BOT-2.1).</summary>
        public static bool IsPinned(string? code) => code is ShiftCard or DayCard;

        /// <summary>BOT-2.2: mẫu là "sự việc" – tin có nút Nhận xử lý / Ghi chú / Đã xử lý, có leo thang.</summary>
        public static bool IsIncident(string? code) => code is FoodSafetyCritical or CoreTempLow or IotOutOfRange or QcIncomingFail or DeliveryTempLow or StopLate
            or TripNotDeparted or ShortageReported or IncidentUnclaimed;

        /// <summary>Bot đích theo phạm vi (BOT_TELEGRAM §0): cá nhân / trong phòng → VH; liên phòng / duyệt / nghiêm trọng → DH (người không có DH → bot họ có).</summary>
        public static string BotFor(string scope) => scope switch
        {
            MessagingCode.Scope.CrossDepartment or MessagingCode.Scope.Approval or MessagingCode.Scope.Critical => MessagingCode.BotDieuHanh,
            _ => MessagingCode.BotVanHanh,
        };
    }
}
