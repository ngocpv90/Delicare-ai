using AppDelicare.Messaging.Channels.Telegram;
using AppDelicare.Messaging.Core.Contracts;

namespace AppDelicare.Messaging.Audiences.Internal
{
    /// <summary>Ngữ cảnh khi chạy 1 chức năng: bot, sự kiện (người, chat), định danh.</summary>
    public sealed record BotActionContext(string Bot, IncomingEvent Event, BotIdentityModel Identity);

    /// <summary>
    /// BOT-1.4 (D-62 §5.1): 1 chức năng của bot – bot dùng (VH / DH / cả 2), phân hệ, nhãn nút, lệnh "/", mục ObjectMenu cần quyền vào (null = ai liên kết cũng dùng),
    /// điều kiện thêm (vd. có mục Duyệt, chỉ người có EmployeeId), thứ tự. Bot chỉ ẨN nút theo quyền cho gọn – api vẫn kiểm quyền khi chạy.
    /// </summary>
    public sealed class BotAction
    {
        public required string Code { get; init; }
        public required string Module { get; init; }
        public required string Label { get; init; }
        public string? Command { get; init; }
        public string? CommandDescription { get; init; }
        /// <summary>VH / DH; trống = cả 2 bot.</summary>
        public IReadOnlyList<string> Bots { get; init; } = [];
        /// <summary>MetaLink ObjectMenu cần quyền vào (vd. #/hr-attendance); null = không cần.</summary>
        public string? MenuMetaLink { get; init; }
        /// <summary>Điều kiện thêm theo định danh (vd. có ít nhất 1 mục Duyệt).</summary>
        public Func<BotIdentityModel, bool>? Visible { get; init; }
        public int Order { get; init; }
        public required Func<BotActionContext, CancellationToken, Task> Handler { get; init; }

        public bool IsAllowed(string bot, BotIdentityModel identity)
            => identity.Linked && identity.CanUseBot
               && (Bots.Count == 0 || Bots.Contains(bot, StringComparer.OrdinalIgnoreCase))
               && (MenuMetaLink == null || identity.IsAdmin || identity.Menus.Contains(MenuMetaLink, StringComparer.OrdinalIgnoreCase))
               && (Visible == null || Visible(identity));
    }
}
