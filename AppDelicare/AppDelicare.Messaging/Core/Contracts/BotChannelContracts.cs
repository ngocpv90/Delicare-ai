using System.Text.Json;

namespace AppDelicare.Messaging.Core.Contracts
{
    // DTO tối thiểu của container messaging – KHÔNG tham chiếu AppDelicare.Model (D-62 §3). Tên trường phải trùng Request / Model phía api:
    // test AppDelicare.Tests/Messaging/ContractTests so tên thuộc tính với AppDelicare.Model.Model.*.

    /// <summary>Kết quả kiểu cũ của api: <c>{ data, listData, totalRecords, responseCode, responseMessage }</c>.</summary>
    public sealed class ApiResult<T>
    {
        public T? Data { get; set; }
        public List<T>? ListData { get; set; }
        public int TotalRecords { get; set; }
        public int ResponseCode { get; set; }
        public string? ResponseMessage { get; set; }

        public bool IsOk => ResponseCode == 200;
    }

    public sealed class BotUpdateRequest
    {
        public string? BotCode { get; set; }
        public long? UpdateId { get; set; }
        public string? ExternalUserId { get; set; }
        public string? ExternalConversationId { get; set; }
        public string? ExternalMessageId { get; set; }
        public string? MessageType { get; set; }
        public string? PayloadJson { get; set; }
    }

    public sealed class BotUpdateResultModel
    {
        public Guid Id { get; set; }
        public bool Duplicate { get; set; }
        public string? ProcessingStatus { get; set; }
    }

    public sealed class BotClaimRequest
    {
        public string? BotCode { get; set; }
        public int? Max { get; set; }
    }

    public sealed class BotInboundItemModel
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

    public sealed class BotCompleteInboundRequest
    {
        public Guid? Id { get; set; }
        public string? Status { get; set; }
        public string? Error { get; set; }
        public string? CorrelationId { get; set; }
    }

    public sealed class BotOutboundItemModel
    {
        public Guid Id { get; set; }
        public string? BotCode { get; set; }
        public string? RecipientExternalId { get; set; }
        public string? ExternalConversationId { get; set; }
        public string? MessageType { get; set; }
        public string? PayloadJson { get; set; }
        public string? CorrelationId { get; set; }
        public string? ProviderMessageId { get; set; }
        public int RetryCount { get; set; }
    }

    public sealed class BotMarkOutboundRequest
    {
        public Guid? Id { get; set; }
        public string? Status { get; set; }
        public string? ProviderMessageId { get; set; }
        public string? Error { get; set; }
        public int? HttpStatus { get; set; }
    }

    public class BotIdentityRequest
    {
        public string? BotCode { get; set; }
        public string? ExternalUserId { get; set; }
        public string? ExternalUsername { get; set; }
        public string? DisplayName { get; set; }
    }

    public sealed class BotLinkByPhoneRequest : BotIdentityRequest
    {
        public string? Phone { get; set; }
        public string? ContactUserId { get; set; }
    }

    public sealed class BotLinkByCodeRequest : BotIdentityRequest
    {
        public string? Code { get; set; }
    }

    public sealed class BotTouchRequest : BotIdentityRequest
    {
        public bool? Blocked { get; set; }
    }

    public sealed class BotIdentityModel
    {
        public bool Linked { get; set; }
        public Guid? IdentityId { get; set; }
        public Guid? UserId { get; set; }
        public Guid? EmployeeId { get; set; }
        public string? FullName { get; set; }
        public string? Status { get; set; }
        public bool NeedsCode { get; set; }
        public bool HasWebAccount { get; set; }
        public bool CanUseDieuHanh { get; set; }
        public bool CanUseBot { get; set; }
        /// <summary>BOT-1.4: MetaLink các menu được vào.</summary>
        public List<string> Menus { get; set; } = [];
        /// <summary>BOT-1.4: screen-id được Duyệt rõ (PendingCode → rỗng).</summary>
        public List<string> ApproveScreens { get; set; } = [];
        public bool IsAdmin { get; set; }
    }

    /// <summary>BOT-2.1: Thẻ ca – nội dung mới nhất (PayloadJson như outbox) + tin đã gửi.</summary>
    public sealed class ShiftCardItemModel
    {
        public Guid AssignmentId { get; set; }
        public string? BotCode { get; set; }
        public string? ChatId { get; set; }
        public string? ProviderMessageId { get; set; }
        public string PayloadJson { get; set; } = "{}";
    }

    public sealed class BotCorrelatedMessageModel
    {
        public Guid Id { get; set; }
        public string? BotCode { get; set; }
        public string? ExternalConversationId { get; set; }
        public string? ProviderMessageId { get; set; }
        public string? MessageType { get; set; }
    }

    public sealed class BotCorrelatedRequest
    {
        public string? CorrelationId { get; set; }
    }

    /// <summary>ReferenceLists (Code / Label).</summary>
    public sealed class ReferenceItemModel
    {
        public string Code { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }

    /// <summary>ApprovalRequest/GetMyPending (chỉ trường bot hiển thị).</summary>
    public sealed class ApprovalPendingModel
    {
        public Guid Id { get; set; }
        public string? RequestNo { get; set; }
        public string? BusinessType { get; set; }
        public string? ScreenId { get; set; }
        public string? Title { get; set; }
        public string? RequestedByName { get; set; }
        public string? StepName { get; set; }
        public int StepNo { get; set; }
        public int StepCount { get; set; }
        public DateTime? DueAt { get; set; }
        public bool IsOverdue { get; set; }
    }

    public sealed class UserTokenModel
    {
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public Guid UserId { get; set; }
        public bool Limited { get; set; }
    }

    /// <summary>Ca hôm nay (AttendanceEvent/GetMine – web; BotChannel/EmployeeGetToday – chỉ EmployeeId). Chỉ đọc vài trường để hiển thị.</summary>
    public sealed class MyAttendanceModel
    {
        public Guid EmployeeId { get; set; }
        public string? EmployeeName { get; set; }
        public List<JsonElement> ListAssignment { get; set; } = [];
        public List<string> AllowedActions { get; set; } = [];
    }
}
