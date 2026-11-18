using AppDelicare.Model.Helper;

namespace AppDelicare.Services.Implement.Messaging
{
    /// <summary>Một mẫu tin: phạm vi (chọn bot), mức (KHẨN / THƯỜNG / TỔNG HỢP), mô tả cho màn quản trị. Câu chữ nằm ở AppDelicare.Messaging/Presentation.</summary>
    public sealed record NotificationTemplate(string Code, string Scope, string Level, string Description);

    /// <summary>
    /// BOT-1.1 (D-62): KHUNG danh mục mẫu tin + vài mẫu đầu. Danh mục đủ theo BOT_TELEGRAM §5.1 (người nhận theo quyền, bậc leo thang, mức rủi ro duyệt)
    /// hoàn thiện ở BOT-1.7. Service nghiệp vụ chỉ gọi <see cref="NotificationService"/> với mã ở đây – không ghi câu chữ.
    /// </summary>
    public static class NotificationCatalog
    {
        public const string ManualText = "MANUAL_TEXT";
        public const string SystemTest = "SYSTEM_TEST";
        public const string ApprovalPending = "APPROVAL_PENDING";
        public const string ApprovalDecided = "APPROVAL_DECIDED";
        public const string ShiftAssigned = "SHIFT_ASSIGNED";
        public const string FoodSafetyCritical = "FOOD_SAFETY_CRITICAL";

        private static readonly Dictionary<string, NotificationTemplate> Templates = new NotificationTemplate[]
        {
            new(ManualText, MessagingCode.Scope.Personal, MessagingCode.Level.Normal, "Tin gửi tay từ màn Tin nhắn ra (#/int-outbound)"),
            new(SystemTest, MessagingCode.Scope.Personal, MessagingCode.Level.Normal, "Tin thử kết nối bot"),
            new(ApprovalPending, MessagingCode.Scope.Approval, MessagingCode.Level.Normal, "Có yêu cầu chờ bạn duyệt"),
            new(ApprovalDecided, MessagingCode.Scope.Personal, MessagingCode.Level.Normal, "Yêu cầu của bạn đã được duyệt / từ chối"),
            new(ShiftAssigned, MessagingCode.Scope.Personal, MessagingCode.Level.Normal, "Bạn được phân ca"),
            new(FoodSafetyCritical, MessagingCode.Scope.Critical, MessagingCode.Level.Urgent, "Lỗi an toàn thực phẩm nghiêm trọng"),
        }.ToDictionary(t => t.Code, StringComparer.OrdinalIgnoreCase);

        public static NotificationTemplate? Find(string? code)
            => code != null && Templates.TryGetValue(code, out var t) ? t : null;

        public static IReadOnlyCollection<NotificationTemplate> All => Templates.Values;

        /// <summary>Bot đích theo phạm vi (BOT_TELEGRAM §0): cá nhân / trong phòng → VH; liên phòng / duyệt / nghiêm trọng → DH.</summary>
        public static string BotFor(string scope) => scope switch
        {
            MessagingCode.Scope.CrossDepartment or MessagingCode.Scope.Approval or MessagingCode.Scope.Critical => MessagingCode.BotDieuHanh,
            _ => MessagingCode.BotVanHanh,
        };
    }
}
