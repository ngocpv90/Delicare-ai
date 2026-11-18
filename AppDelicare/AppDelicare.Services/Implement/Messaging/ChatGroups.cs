using System.Text.Json;
using AppDelicare.Data.Connection;
using AppDelicare.Model.Helper;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AppDelicare.Services.Implement.Messaging
{
    /// <summary>Một nhóm chat của bot: mã, chat id Telegram (số âm), mẫu tin gửi vào nhóm, gửi thay (replace) hay gửi thêm (copy) tin riêng.</summary>
    public sealed record ChatGroup(string Code, string ChatId, IReadOnlySet<string> Templates, bool Copy);

    /// <summary>
    /// BOT-1.7d (BOT_TELEGRAM §5.1 "Nhóm chat", PROPOSED – DECISIONS p40): cấu hình trong <c>IntegrationChannel.ConfigJson</c> của bot:
    /// <code>{ "groups": [ { "code": "BEP_ATTP", "chatId": "-1001234567890", "templates": ["FOODSAFETY_CRITICAL", "CORE_TEMP_LOW"], "mode": "replace" } ] }</code>
    /// Tin có mẫu nằm trong <c>templates</c> → 1 tin vào nhóm; <c>mode</c> = <c>replace</c> (mặc định – người nhận trên CÙNG bot không nhận tin riêng nữa)
    /// hoặc <c>copy</c> (vẫn gửi riêng). KHÔNG BAO GIỜ gửi vào nhóm: tin cá nhân / tin duyệt (có nút duyệt, dữ liệu cá nhân), thẻ ghim, tin gửi tay.
    /// Chưa cấu hình nhóm → gửi riêng như cũ.
    /// </summary>
    public static class ChatGroups
    {
        public const string Section = "groups";

        /// <summary>Mẫu được phép gửi vào nhóm (không có dữ liệu cá nhân / nút duyệt).</summary>
        public static bool Allowed(NotificationTemplate template)
            => template.Scope is not (MessagingCode.Scope.Personal or MessagingCode.Scope.Approval)
               && !NotificationCatalog.IsPinned(template.Code) && template.Code is not (NotificationCatalog.ManualText or NotificationCatalog.SystemTest);

        /// <summary>Đọc danh sách nhóm từ ConfigJson; JSON sai / thiếu chatId → bỏ qua nhóm đó (ghi log), không lỗi.</summary>
        public static List<ChatGroup> Parse(string? configJson)
        {
            var list = new List<ChatGroup>();
            if (string.IsNullOrWhiteSpace(configJson))
            {
                return list;
            }
            try
            {
                using var doc = JsonDocument.Parse(configJson);
                if (!doc.RootElement.TryGetProperty(Section, out var groups) || groups.ValueKind != JsonValueKind.Array)
                {
                    return list;
                }
                foreach (var g in groups.EnumerateArray().Where(g => g.ValueKind == JsonValueKind.Object))
                {
                    string? chat = g.TryGetProperty("chatId", out var c) ? (c.ValueKind == JsonValueKind.Number ? c.GetRawText() : c.GetString()) : null;
                    if (string.IsNullOrWhiteSpace(chat))
                    {
                        continue;
                    }
                    var templates = g.TryGetProperty("templates", out var t) && t.ValueKind == JsonValueKind.Array
                        ? t.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToHashSet(StringComparer.OrdinalIgnoreCase)
                        : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    string code = g.TryGetProperty("code", out var k) && k.ValueKind == JsonValueKind.String ? k.GetString()! : chat.Trim();
                    bool copy = g.TryGetProperty("mode", out var m) && m.ValueKind == JsonValueKind.String && string.Equals(m.GetString(), "copy", StringComparison.OrdinalIgnoreCase);
                    list.Add(new ChatGroup(code, chat.Trim(), templates, copy));
                }
            }
            catch (JsonException ex)
            {
                Log.Warning(ex, "[ChatGroups] ConfigJson không phải JSON hợp lệ – bỏ qua nhóm chat");
            }
            return list;
        }

        /// <summary>Kênh (bot) → các nhóm nhận mẫu <paramref name="template"/>.</summary>
        public static async Task<Dictionary<Guid, List<ChatGroup>>> ForTemplateAsync(DatabaseContext db, IReadOnlyCollection<Guid> channelIds, NotificationTemplate template,
            CancellationToken ct = default)
        {
            var result = new Dictionary<Guid, List<ChatGroup>>();
            if (!Allowed(template) || channelIds.Count == 0)
            {
                return result;
            }
            var configs = await db.IntegrationChannel.AsNoTracking().Where(c => channelIds.Contains(c.Id) && !c.IsDeleted && c.ConfigJson != null && c.ConfigJson.Contains(Section))
                .Select(c => new { c.Id, c.ConfigJson }).ToListAsync(ct);
            foreach (var c in configs)
            {
                var groups = Parse(c.ConfigJson).Where(g => g.Templates.Contains(template.Code)).ToList();
                if (groups.Count > 0)
                {
                    result[c.Id] = groups;
                }
            }
            return result;
        }
    }
}
