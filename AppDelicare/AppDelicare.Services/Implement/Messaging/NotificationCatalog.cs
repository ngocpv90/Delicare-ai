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
        public const string CoreTempLow = "CORE_TEMP_LOW";
        public const string FoodSafetyCritical = "FOODSAFETY_CRITICAL";
        public const string IotOutOfRange = "IOT_OUT_OF_RANGE";
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

        private const string P = MessagingCode.Scope.Personal, D = MessagingCode.Scope.Department, X = MessagingCode.Scope.CrossDepartment,
            A = MessagingCode.Scope.Approval, C = MessagingCode.Scope.Critical;
        private const string Urgent = MessagingCode.Level.Urgent, Normal = MessagingCode.Level.Normal, Digest = MessagingCode.Level.Digest;

        private static readonly Dictionary<string, NotificationTemplate> Templates = new NotificationTemplate[]
        {
            new(ManualText, P, Normal, "Tin gửi tay từ màn Tin nhắn ra (#/int-outbound)"),
            new(SystemTest, P, Normal, "Tin thử kết nối bot"),
            new(ShiftConfirmed, P, Normal, "Phân ca đã xác nhận (ShiftAssignment/Confirm) – nhân viên được phân"),
            new(ApprovalPending, A, Normal, "Cấp duyệt mới (gửi duyệt / chuyển cấp) – người duyệt của cấp"),
            new(ApprovalResult, P, Normal, "Kết quả duyệt (đơn nghỉ / đổi ca / chứng từ) – người gửi"),
            new(BatchLate, D, Urgent, "Mẻ trễ > 15′ – trưởng ca"),
            new(CoreTempLow, X, Urgent, "Nhiệt độ lõi < 70 °C – trưởng ca, QA, bếp trưởng"),
            new(FoodSafetyCritical, C, Urgent, "Lỗi trọng yếu checklist ATTP – QA, trưởng ca, bếp trưởng, COO"),
            new(IotOutOfRange, D, Urgent, "Nhiệt độ kho / khu ngoài ngưỡng (IoT) – QA, thủ kho"),
            new(ChecklistDue, D, Normal, "Checklist sắp đến hạn – trưởng ca"),
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
            new(MenuPublished, D, Digest, "Thực đơn công bố – kế hoạch, KD, bếp"),
            new(NotCheckedIn, D, Normal, "Chưa vào ca > 10′ – trưởng ca, nhân sự"),
            new(MorningBrief, X, Digest, "Bản tin 07:00"),
            new(DailyReport, X, Digest, "Báo cáo 17:00"),
            new(PeriodReport, X, Digest, "Báo cáo tuần / tháng"),
            new(SystemJobFailed, D, Urgent, "Job tự động lỗi – IT"),
            new(OutboundFailing, D, Urgent, "Tin gửi lỗi nhiều lần – IT"),
            new(IncidentUnclaimed, X, Urgent, "Sự việc liên phòng quá hạn chưa ai nhận – COO, PMO"),
        }.ToDictionary(t => t.Code, StringComparer.OrdinalIgnoreCase);

        public static NotificationTemplate? Find(string? code)
            => code != null && Templates.TryGetValue(code, out var t) ? t : null;

        public static IReadOnlyCollection<NotificationTemplate> All => Templates.Values;

        /// <summary>Bot đích theo phạm vi (BOT_TELEGRAM §0): cá nhân / trong phòng → VH; liên phòng / duyệt / nghiêm trọng → DH (người không có DH → bot họ có).</summary>
        public static string BotFor(string scope) => scope switch
        {
            MessagingCode.Scope.CrossDepartment or MessagingCode.Scope.Approval or MessagingCode.Scope.Critical => MessagingCode.BotDieuHanh,
            _ => MessagingCode.BotVanHanh,
        };
    }
}
