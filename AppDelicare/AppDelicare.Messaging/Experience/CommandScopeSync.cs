using System.Collections.Concurrent;
using AppDelicare.Messaging.Audiences.Internal;
using AppDelicare.Messaging.Channels.Telegram;
using AppDelicare.Messaging.Channels.Telegram.Models;
using AppDelicare.Messaging.Core.Contracts;
using Microsoft.Extensions.Logging;

namespace AppDelicare.Messaging.Experience
{
    /// <summary>
    /// BOT-1.4 (BOT_TELEGRAM §1.4): danh sách lệnh "/" cho từng chat (<c>setMyCommands</c> scope chat). Chỉ gọi Telegram khi danh sách đổi (nhớ theo chat).
    /// BOT-UX-1 (mục 8): mọi chat chỉ 4 lệnh /menu /ca /chamcong /help – mỗi lệnh mở cùng bảng điều khiển. Lệnh / mã chức năng khác (/nlca, /cho…) gõ vẫn chạy nhưng không liệt kê.
    /// </summary>
    public sealed class CommandScopeSync(BotActionRegistry registry, ITelegramClient telegram, ILogger<CommandScopeSync> logger)
    {
        private readonly ConcurrentDictionary<string, string> _last = new();

        public static readonly TgBotCommand[] Panel =
        [
            new() { Command = "menu", Description = "Mở bảng chức năng" },
            new() { Command = "ca", Description = "Ca của tôi" },
            new() { Command = "chamcong", Description = "Chấm công vào / ra" },
            new() { Command = "help", Description = "Trợ giúp" },
        ];

        public List<TgBotCommand> CommandsFor(string bot, BotIdentityModel identity) => [.. Panel];

        /// <summary>true = đã gọi setMyCommands (danh sách đổi).</summary>
        public async Task<bool> SyncAsync(string bot, string chatId, BotIdentityModel identity, CancellationToken ct)
        {
            var commands = CommandsFor(bot, identity);
            string signature = string.Join(',', commands.Select(c => c.Command));
            string key = bot + ":" + chatId;
            if (_last.TryGetValue(key, out var last) && last == signature)
            {
                return false;
            }
            try
            {
                await telegram.SetMyCommandsAsync(bot, commands, new { type = "chat", chat_id = chatId }, ct);
                _last[key] = signature;
                return true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning("setMyCommands chat {Chat} lỗi: {Error}", chatId, ex.Message);
                return false;
            }
        }
    }
}
