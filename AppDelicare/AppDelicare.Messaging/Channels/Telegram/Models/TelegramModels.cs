using System.Text.Json;
using System.Text.Json.Serialization;

namespace AppDelicare.Messaging.Channels.Telegram.Models
{
    /// <summary>Kiểu dữ liệu Telegram Bot API (chỉ trường dùng tới). JSON snake_case – xem <see cref="TgJson.Options"/>.</summary>
    public static class TgJson
    {
        public static readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
    }

    public sealed class TgResponse<T>
    {
        public bool Ok { get; set; }
        public T? Result { get; set; }
        public int? ErrorCode { get; set; }
        public string? Description { get; set; }
        public TgResponseParameters? Parameters { get; set; }
    }

    public sealed class TgResponseParameters
    {
        public int? RetryAfter { get; set; }
        public long? MigrateToChatId { get; set; }
    }

    public sealed class TgUpdate
    {
        public long UpdateId { get; set; }
        public TgMessage? Message { get; set; }
        public TgMessage? EditedMessage { get; set; }
        public TgCallbackQuery? CallbackQuery { get; set; }
        public TgChatMemberUpdated? MyChatMember { get; set; }
    }

    public sealed class TgMessage
    {
        public long MessageId { get; set; }
        public TgUser? From { get; set; }
        public TgChat Chat { get; set; } = new();
        public long Date { get; set; }
        public string? Text { get; set; }
        public string? Caption { get; set; }
        public TgContact? Contact { get; set; }
        public TgLocation? Location { get; set; }
        public TgMessage? ReplyToMessage { get; set; }
        public List<TgPhotoSize>? Photo { get; set; }
        public TgDocument? Document { get; set; }
    }

    public sealed class TgUser
    {
        public long Id { get; set; }
        public bool IsBot { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Username { get; set; }
    }

    public sealed class TgChat
    {
        public long Id { get; set; }
        /// <summary>private / group / supergroup / channel.</summary>
        public string? Type { get; set; }
    }

    public sealed class TgContact
    {
        public string PhoneNumber { get; set; } = string.Empty;
        public string? FirstName { get; set; }
        /// <summary>Có khi liên hệ là tài khoản Telegram – phải bằng from.id (số của chính người gửi).</summary>
        public long? UserId { get; set; }
    }

    public sealed class TgLocation
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        /// <summary>Có = vị trí trực tiếp (live location).</summary>
        public int? LivePeriod { get; set; }
        public double? HorizontalAccuracy { get; set; }
    }

    public sealed class TgPhotoSize
    {
        public string FileId { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public sealed class TgDocument
    {
        public string FileId { get; set; } = string.Empty;
        public string? FileName { get; set; }
        public string? MimeType { get; set; }
    }

    public sealed class TgCallbackQuery
    {
        public string Id { get; set; } = string.Empty;
        public TgUser From { get; set; } = new();
        public TgMessage? Message { get; set; }
        public string? Data { get; set; }
    }

    public sealed class TgChatMemberUpdated
    {
        public TgChat Chat { get; set; } = new();
        public TgUser From { get; set; } = new();
        public TgChatMember? NewChatMember { get; set; }
    }

    public sealed class TgChatMember
    {
        /// <summary>member / kicked (người dùng chặn bot) / left…</summary>
        public string? Status { get; set; }
    }

    public sealed class TgFile
    {
        public string FileId { get; set; } = string.Empty;
        public string? FilePath { get; set; }
    }

    // ------------------------------------------------------------ bàn phím

    public sealed class TgInlineKeyboard
    {
        public List<List<TgInlineButton>> InlineKeyboard { get; set; } = [];
    }

    public sealed class TgInlineButton
    {
        public string Text { get; set; } = string.Empty;
        public string? CallbackData { get; set; }
        public string? Url { get; set; }
    }

    public sealed class TgReplyKeyboard
    {
        public List<List<TgKeyboardButton>> Keyboard { get; set; } = [];
        public bool ResizeKeyboard { get; set; } = true;
        public bool? OneTimeKeyboard { get; set; }
        public bool? IsPersistent { get; set; }
    }

    public sealed class TgKeyboardButton
    {
        public string Text { get; set; } = string.Empty;
        public bool? RequestContact { get; set; }
        public bool? RequestLocation { get; set; }
    }

    public sealed class TgReplyKeyboardRemove
    {
        public bool RemoveKeyboard { get; set; } = true;
    }

    public sealed class TgForceReply
    {
        public bool ForceReply { get; set; } = true;
        public string? InputFieldPlaceholder { get; set; }
    }

    public sealed class TgBotCommand
    {
        public string Command { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
