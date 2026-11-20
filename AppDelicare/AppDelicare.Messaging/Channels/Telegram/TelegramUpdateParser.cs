using System.Text.Json;
using AppDelicare.Messaging.Channels.Telegram.Models;

namespace AppDelicare.Messaging.Channels.Telegram
{
    public enum IncomingKind
    {
        Command,
        Text,
        Contact,
        Location,
        Callback,
        Photo,
        Document,
        /// <summary>my_chat_member: người dùng chặn (kicked) / mở lại (member) bot.</summary>
        ChatMember,
        Other,
    }

    /// <summary>Sự kiện trung tính rút từ 1 update.</summary>
    public sealed record IncomingEvent(IncomingKind Kind, long UpdateId, string FromId, string ChatId, string? ChatType, string? Username, string? DisplayName)
    {
        public string? Command { get; init; }
        public string? Argument { get; init; }
        public string? Text { get; init; }
        public TgContact? Contact { get; init; }
        public TgLocation? Location { get; init; }
        public string? CallbackId { get; init; }
        public string? CallbackData { get; init; }
        public long? MessageId { get; init; }
        public long? ReplyToMessageId { get; init; }
        public string? FileId { get; init; }
        /// <summary>BOT-3.3: tên file của document (kiểm tra đuôi .xlsx khi nhập file máy chấm công).</summary>
        public string? FileName { get; init; }
        public string? MemberStatus { get; init; }
        /// <summary>Giờ gửi (Unix giây) – chấm công: vị trí cũ hơn 120″ bị coi là cũ.</summary>
        public long Date { get; init; }
        /// <summary>Tin chuyển tiếp.</summary>
        public bool Forwarded { get; init; }
        /// <summary>BOT-2.4: update là edited_message (vị trí trực tiếp cập nhật bằng cách sửa tin vị trí ban đầu).</summary>
        public bool Edited { get; init; }
    }

    /// <summary>BOT-1.3: update Telegram → <see cref="IncomingEvent"/> (lệnh / chữ / liên hệ / vị trí (kể cả trực tiếp) / nút / ảnh / tệp / trả lời).</summary>
    public static class TelegramUpdateParser
    {
        public static TgUpdate? Deserialize(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }
            try
            {
                return JsonSerializer.Deserialize<TgUpdate>(json, TgJson.Options);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        public static IncomingEvent? Parse(TgUpdate? update)
        {
            if (update == null)
            {
                return null;
            }
            if (update.CallbackQuery is { } cb)
            {
                var chat = cb.Message?.Chat.Id.ToString() ?? cb.From.Id.ToString();
                return new IncomingEvent(IncomingKind.Callback, update.UpdateId, cb.From.Id.ToString(), chat, cb.Message?.Chat.Type, cb.From.Username, Name(cb.From))
                {
                    CallbackId = cb.Id, CallbackData = cb.Data, MessageId = cb.Message?.MessageId,
                };
            }
            if (update.MyChatMember is { } member)
            {
                return new IncomingEvent(IncomingKind.ChatMember, update.UpdateId, member.From.Id.ToString(), member.Chat.Id.ToString(), member.Chat.Type,
                    member.From.Username, Name(member.From)) { MemberStatus = member.NewChatMember?.Status };
            }
            var m = update.Message ?? update.EditedMessage;
            if (m?.From == null)
            {
                return null;
            }
            var e = new IncomingEvent(IncomingKind.Other, update.UpdateId, m.From.Id.ToString(), m.Chat.Id.ToString(), m.Chat.Type, m.From.Username, Name(m.From))
            {
                MessageId = m.MessageId, ReplyToMessageId = m.ReplyToMessage?.MessageId, Date = m.Date,
                Forwarded = m.ForwardOrigin is { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined },
                Edited = update.Message == null && update.EditedMessage != null,
            };
            if (m.Contact != null)
            {
                return e with { Kind = IncomingKind.Contact, Contact = m.Contact };
            }
            if (m.Location != null)
            {
                return e with { Kind = IncomingKind.Location, Location = m.Location };
            }
            if (m.Photo is { Count: > 0 })
            {
                return e with { Kind = IncomingKind.Photo, FileId = m.Photo[^1].FileId, Text = m.Caption };
            }
            if (m.Document != null)
            {
                return e with { Kind = IncomingKind.Document, FileId = m.Document.FileId, FileName = m.Document.FileName, Text = m.Caption };
            }
            string text = (m.Text ?? string.Empty).Trim();
            if (text.StartsWith('/'))
            {
                int space = text.IndexOf(' ');
                string cmd = (space < 0 ? text : text[..space])[1..];
                int at = cmd.IndexOf('@'); // /start@delicare_bot trong nhóm
                if (at >= 0)
                {
                    cmd = cmd[..at];
                }
                return e with { Kind = IncomingKind.Command, Command = cmd.ToLowerInvariant(), Argument = space < 0 ? null : text[(space + 1)..].Trim(), Text = text };
            }
            return e with { Kind = text.Length > 0 ? IncomingKind.Text : IncomingKind.Other, Text = text };
        }

        /// <summary>Đọc nhanh update_id + người gửi + chat từ JSON thô (webhook / polling – trước khi lưu).</summary>
        public static (long UpdateId, string? FromId, string? ChatId, string? MessageId, string Type)? Peek(string json)
        {
            var update = Deserialize(json);
            if (update == null || update.UpdateId == 0)
            {
                return null;
            }
            var e = Parse(update);
            string type = update.CallbackQuery != null ? "callback_query" : update.MyChatMember != null ? "my_chat_member"
                : update.EditedMessage != null ? "edited_message" : update.Message != null ? "message" : "other";
            return (update.UpdateId, e?.FromId, e?.ChatId, e?.MessageId?.ToString(), type);
        }

        private static string Name(TgUser u) => string.Join(' ', new[] { u.FirstName, u.LastName }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }
}
