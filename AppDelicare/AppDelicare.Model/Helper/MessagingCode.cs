namespace AppDelicare.Model.Helper
{
    /// <summary>
    /// BOT-1.1 (D-62): mã dùng chung cho kênh tin nhắn. Trạng thái mới (Processing, Ignored, Sending, Blocked, Cancelled) = PROPOSED D-62 (p2)
    /// – chưa có trong STATUS_CATALOG (BA rà khi UAT). Mọi API / outbox dùng <c>channelCode</c> / <c>botCode</c>, không viết cứng "telegram".
    /// </summary>
    public static class MessagingCode
    {
        public const string ProviderTelegram = "TELEGRAM";
        /// <summary>AI-19 (D-06): Zalo OA – bot khách hàng (ZALO_KH).</summary>
        public const string ProviderZalo = "ZALO";

        /// <summary>Mã bot (BotConfiguration.BotCode) – giống nhau ở mọi môi trường, chỉ khác token.</summary>
        public const string BotVanHanh = "VH";
        public const string BotDieuHanh = "DH";
        public const string BotTroLy = "TRO_LY";

        /// <summary>Khách nội bộ gọi <c>api/BotChannel/*</c> (header X-Client-Key).</summary>
        public const string ClientMessaging = "MESSAGING";

        /// <summary>Quyền vào bot (ObjectMenu.MetaLink seed BOT-1.1).</summary>
        public const string MenuBotVanHanh = "#/bot-van-hanh";
        public const string MenuBotDieuHanh = "#/bot-dieu-hanh";

        public static class Inbound
        {
            public const string Received = "Received";
            public const string Processing = "Processing";
            public const string Processed = "Processed";
            public const string Failed = "Failed";
            public const string Ignored = "Ignored";
        }

        public static class Outbound
        {
            public const string Queued = "Queued";
            public const string Sending = "Sending";
            public const string Sent = "Sent";
            public const string Failed = "Failed";
            /// <summary>Người dùng chặn bot (Telegram 403) – không thử lại.</summary>
            public const string Blocked = "Blocked";
            /// <summary>Hoàn tác / hết hạn – không gửi.</summary>
            public const string Cancelled = "Cancelled";
        }

        public static class Identity
        {
            public const string Active = "Active";
            public const string Revoked = "Revoked";
            /// <summary>Người dùng chặn bot.</summary>
            public const string Blocked = "Blocked";
            /// <summary>BOT-1.2 (PROPOSED D-62 p17): người có quyền duyệt / admin mới khớp số điện thoại – dùng bot nhưng CHƯA được duyệt / quản trị tới khi nhập mã web.</summary>
            public const string PendingCode = "PendingCode";

            /// <summary>Định danh còn dùng được (nhận tin, xin token).</summary>
            public static bool IsUsable(string? status) => status is Active or PendingCode;
        }

        /// <summary>BOT-1.2: claim trong JWT cấp thay mặt user cho container messaging.</summary>
        public static class Claims
        {
            /// <summary>Khách gọi (MESSAGING).</summary>
            public const string Client = "client";
            /// <summary>"1" = token hạn chế (định danh PendingCode): PermissionService bỏ quyền admin + quyền duyệt.</summary>
            public const string BotLimited = "bot_limited";
        }

        /// <summary>Loại mã liên kết.</summary>
        public static class LinkKind
        {
            public const char User = 'U';
            public const char Employee = 'E';
            /// <summary>AI-19: người liên hệ của khách (CustomerContact) – chỉ đổi được ở bot Zalo khách.</summary>
            public const char CustomerContact = 'C';
        }

        public const string StatusActive = "Active";

        /// <summary>IntegrationLog.DirectionCode / OperationCode.</summary>
        public static class Log
        {
            public const string In = "IN";
            public const string Out = "OUT";
            public const string Token = "TOKEN";
            public const string Link = "LINK";
            public const string OpReceive = "RECEIVE_UPDATE";
            public const string OpSend = "SEND";
            public const string OpLinkPhone = "LINK_PHONE";
            public const string OpLinkCode = "LINK_CODE";
            public const string OpLinkAdmin = "LINK_ADMIN";
            public const string OpUnlink = "UNLINK";
            public const string OpIssueToken = "ISSUE_USER_TOKEN";
            public const string StatusSuccess = "Success";
            public const string StatusDuplicate = "Duplicate";
            public const string StatusFailed = "Failed";
            public const string StatusBlocked = "Blocked";
        }

        /// <summary>Mức tin (BOT_TELEGRAM §1.2).</summary>
        public static class Level
        {
            public const string Urgent = "URGENT";
            public const string Normal = "NORMAL";
            public const string Digest = "DIGEST";
        }

        /// <summary>Phạm vi tin → chọn bot (BOT_TELEGRAM §0): cá nhân / trong phòng → VH; liên phòng / duyệt / nghiêm trọng → DH.</summary>
        public static class Scope
        {
            public const string Personal = "PERSONAL";
            public const string Department = "DEPARTMENT";
            public const string CrossDepartment = "CROSS_DEPARTMENT";
            public const string Approval = "APPROVAL";
            public const string Critical = "CRITICAL";
        }
    }
}
