using System.Collections.Concurrent;
using AppDelicare.Messaging.Audiences.Internal;
using AppDelicare.Messaging.Channels.Telegram;
using AppDelicare.Messaging.Channels.Telegram.Models;
using AppDelicare.Messaging.Core.Contracts;
using Microsoft.Extensions.Logging;

namespace AppDelicare.Messaging.Experience
{
    /// <summary>
    /// BOT-1.4 (BOT_TELEGRAM §1.4): danh sách lệnh "/" RIÊNG cho từng chat (<c>setMyCommands</c> scope chat) – mỗi người chỉ thấy lệnh mình dùng được
    /// (chức năng có <see cref="BotAction.Command"/> trong quyền + /start /menu /help /huylienket). Chỉ gọi Telegram khi danh sách đổi (nhớ theo chat).
    /// </summary>
    public sealed class CommandScopeSync(BotActionRegistry registry, ITelegramClient telegram, ILogger<CommandScopeSync> logger)
    {
        private readonly ConcurrentDictionary<string, string> _last = new();

        public static readonly TgBotCommand[] Base =
        [
            new() { Command = "start", Description = "Liên kết / mở lại bot" },
            new() { Command = "menu", Description = "Chức năng" },
        ];

        public static readonly TgBotCommand[] Tail =
        [
            new() { Command = "help", Description = "Trợ giúp" },
            new() { Command = "huylienket", Description = "Hủy liên kết tài khoản" },
        ];

        public List<TgBotCommand> CommandsFor(string bot, BotIdentityModel identity)
        {
            var list = new List<TgBotCommand>(Base);
            list.AddRange(registry.Available(bot, identity).Where(a => a.Command != null && a.Command != "help")
                .Select(a => new TgBotCommand { Command = a.Command!, Description = a.CommandDescription ?? a.Label }));
            list.AddRange(Tail);
            return [.. list.GroupBy(c => c.Command).Select(g => g.First())];
        }

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
