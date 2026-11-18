namespace AppDelicare.Model.Model
{
    /// <summary>BOT-1.1: container messaging gửi 1 update thô (JSON) – đã kiểm secret webhook / polling bên messaging.</summary>
    public class BotUpdateRequest
    {
        /// <summary>VH / DH (BotConfiguration.BotCode).</summary>
        public string? BotCode { get; set; }
        /// <summary>update_id của Telegram – khóa chống trùng TG:{botCode}:{updateId}.</summary>
        public long? UpdateId { get; set; }
        public string? ExternalUserId { get; set; }
        public string? ExternalConversationId { get; set; }
        public string? ExternalMessageId { get; set; }
        /// <summary>message / callback_query / my_chat_member…</summary>
        public string? MessageType { get; set; }
        /// <summary>Update gốc (chuỗi JSON).</summary>
        public string? PayloadJson { get; set; }
    }

    public class BotUpdateResultModel
    {
        public Guid Id { get; set; }
        /// <summary>true = update đã nhận trước đó (gửi lại an toàn, không tạo dòng mới).</summary>
        public bool Duplicate { get; set; }
        public string? ProcessingStatus { get; set; }
    }

    public class BotClaimRequest
    {
        /// <summary>Lọc theo bot; trống = mọi bot Telegram.</summary>
        public string? BotCode { get; set; }
        public int? Max { get; set; }
    }

    public class BotInboundItemModel
    {
        public Guid Id { get; set; }
        public string? BotCode { get; set; }
        public string? ExternalUserId { get; set; }
        public string? ExternalConversationId { get; set; }
        public string? ExternalMessageId { get; set; }
        public string? MessageType { get; set; }
        public string? PayloadJson { get; set; }
        public DateTime ReceivedAt { get; set; }
    }

    public class BotCompleteInboundRequest
    {
        public Guid? Id { get; set; }
        /// <summary>Processed / Failed / Ignored.</summary>
        public string? Status { get; set; }
        public string? Error { get; set; }
        public string? CorrelationId { get; set; }
    }

    public class BotOutboundItemModel
    {
        public Guid Id { get; set; }
        public string? BotCode { get; set; }
        public string? RecipientExternalId { get; set; }
        public string? ExternalConversationId { get; set; }
        public string? MessageType { get; set; }
        /// <summary>{ template, data, buttons, scope, level, notBefore?, replyContext?, pin? } – câu chữ do messaging dựng (Presentation).</summary>
        public string? PayloadJson { get; set; }
        public string? CorrelationId { get; set; }
        public string? ProviderMessageId { get; set; }
        public int RetryCount { get; set; }
    }

    public class BotMarkOutboundRequest
    {
        public Guid? Id { get; set; }
        /// <summary>Sent / Failed / Blocked.</summary>
        public string? Status { get; set; }
        public string? ProviderMessageId { get; set; }
        public string? Error { get; set; }
        public int? HttpStatus { get; set; }
    }

    /// <summary>BOT-1.1 (Catalog 217): gửi tay 1 tin văn bản cho người dùng đã liên kết bot (màn #/int-outbound).</summary>
    public class OutboundMessageEnqueueRequest
    {
        public string? BotCode { get; set; }
        public Guid? UserId { get; set; }
        public Guid? EmployeeId { get; set; }
        public string? Text { get; set; }
    }

    public class OutboundEnqueueResultModel
    {
        /// <summary>Số tin đã xếp hàng (mỗi định danh đang Active 1 tin).</summary>
        public int Queued { get; set; }
        public List<Guid> MessageIds { get; set; } = [];
    }

    /// <summary>BOT-1.1 (Catalog 219): hội thoại của 1 người trên 1 bot (tin vào + tin ra, mới nhất trước).</summary>
    public class ConversationSearchRequest
    {
        public Guid? IdentityId { get; set; }
        public Guid? UserId { get; set; }
        public Guid? EmployeeId { get; set; }
        public string? BotCode { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public class ConversationItemModel
    {
        public Guid Id { get; set; }
        /// <summary>IN / OUT.</summary>
        public string Direction { get; set; } = string.Empty;
        public DateTime At { get; set; }
        public string? BotCode { get; set; }
        public string? ExternalUserId { get; set; }
        public string? MessageType { get; set; }
        public string? Status { get; set; }
        public string? PayloadJson { get; set; }
        public string? CorrelationId { get; set; }
        public string? Error { get; set; }
    }

    public class ConversationResultModel
    {
        public int Total { get; set; }
        public List<ConversationItemModel> Items { get; set; } = [];
    }

    /// <summary>
    /// BOT-1.1: yêu cầu xếp tin vào outbox (NotificationService.Enqueue). Người nhận theo UserId / EmployeeId (định danh đang Active trên bot đích);
    /// <c>BotCode</c> trống → chọn theo phạm vi mẫu tin (NotificationCatalog).
    /// </summary>
    public class NotificationRequest
    {
        public string Template { get; set; } = string.Empty;
        public List<Guid> UserIds { get; set; } = [];
        public List<Guid> EmployeeIds { get; set; } = [];
        public object? Data { get; set; }
        public object? Buttons { get; set; }
        public string? BotCode { get; set; }
        /// <summary>Mã sự việc – gom các tin cùng sự việc để sửa đồng loạt.</summary>
        public string? CorrelationId { get; set; }
        /// <summary>Khóa chống trùng của sự kiện (mỗi người nhận thêm hậu tố định danh).</summary>
        public string? IdempotencyKey { get; set; }
        /// <summary>Không gửi trước thời điểm này (giờ yên lặng – GAP-INT-03, lưu PayloadJson.notBefore).</summary>
        public DateTime? NotBefore { get; set; }
        /// <summary>BOT-2.3: tin leo thang – bỏ qua giờ yên lặng theo ca (BOT_TELEGRAM §5.3: người ngoài ca vẫn nhận 🔴 khi là bậc leo thang).</summary>
        public bool Escalation { get; set; }
    }
}
