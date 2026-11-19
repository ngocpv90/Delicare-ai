using System.Text.Json;
using System.Text.RegularExpressions;
using AppDelicare.Messaging.Audiences.Internal;
using AppDelicare.Messaging.Audiences.Internal.Chung;
using AppDelicare.Messaging.Channels;
using AppDelicare.Messaging.Core;
using AppDelicare.Messaging.Options;
using AppDelicare.Messaging.Presentation;
using Microsoft.Extensions.Options;

namespace AppDelicare.Messaging.Experience
{
    /// <summary>
    /// BOT-1.4 (BOT_TELEGRAM §1.4): gõ mã → mở thẳng. Tiền tố = mã chứng từ ĐANG SINH trong DocumentNumberService (<c>Messaging:CodePrefixes</c>, mặc định PH, XN, PA, PO,
    /// DN, NL, ME, CX, KH, VS, HD – không đặt mới). Gõ mã hoặc chữ (vd. "tồn gạo", ≥ 3 ký tự) → <c>Common/GlobalSearch</c> bằng JWT của chính user (kết quả đã lọc
    /// theo quyền ở api). Không dùng AI. Người chỉ có EmployeeId (không JWT) → không tra.
    /// </summary>
    public sealed class CodeSearch(IDelicareApiClient web, UserTokenCache tokens, IChannelSender sender, IOptionsMonitor<MessagingHostOptions> options)
    {
        public bool LooksLikeCode(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }
            string prefixes = string.Join('|', options.CurrentValue.EffectiveCodePrefixes.Select(Regex.Escape));
            return Regex.IsMatch(text.Trim(), $"^({prefixes})-\\d{{5,6}}", RegexOptions.IgnoreCase);
        }

        public static bool LooksLikeSearch(string? text) => text != null && text.Trim().Length >= 3 && !text.TrimStart().StartsWith('/');

        public async Task SearchAsync(BotActionContext ctx, string text, CancellationToken ct)
        {
            if (!ctx.Identity.HasWebAccount)
            {
                await BotReply.TextAsync(sender, ctx.Bot, ctx.Event.ChatId, "Tra cứu dành cho tài khoản có quyền trên web. Bấm " + KeyboardBuilder.Functions + " để xem chức năng.", ct: ct);
                return;
            }
            var (token, error) = await tokens.GetAsync(ctx.Bot, ctx.Event.FromId, ct);
            if (token == null)
            {
                await BotReply.TextAsync(sender, ctx.Bot, ctx.Event.ChatId, error ?? "Không lấy được quyền truy cập.", ct: ct);
                return;
            }
            var r = await web.GetAsUserAsync<JsonElement>("api/Common/GlobalSearch?q=" + Uri.EscapeDataString(text.Trim()), token.Token, ct);
            var records = r.Data.ValueKind == JsonValueKind.Object && r.Data.TryGetProperty("records", out var rec) && rec.ValueKind == JsonValueKind.Array
                ? rec.EnumerateArray().Take(8).ToList() : [];
            if (!r.IsOk || records.Count == 0)
            {
                await BotReply.TextAsync(sender, ctx.Bot, ctx.Event.ChatId, r.IsOk ? $"Không tìm thấy \"{text.Trim()}\"." : r.ResponseMessage ?? "Không tra được.", ct: ct);
                return;
            }
            await BotReply.SendAsync(sender, ctx.Bot, ctx.Event.ChatId, new NeutralMessage
            {
                Title = "Kết quả: " + text.Trim(),
                Lines = [.. records.Select(x => $"• {S(x, "typeTitle")}: {S(x, "code") ?? ""} – {S(x, "label") ?? ""}{(S(x, "statusName") is { } st ? " (" + st + ")" : "")}")],
            }, ct: ct);
        }

        private static string? S(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }
}
