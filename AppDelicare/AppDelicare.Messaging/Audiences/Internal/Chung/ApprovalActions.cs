using System.Collections.Concurrent;
using System.Text.Json;
using AppDelicare.Messaging.Callback;
using AppDelicare.Messaging.Channels;
using AppDelicare.Messaging.Channels.Telegram;
using AppDelicare.Messaging.Channels.Telegram.Models;
using AppDelicare.Messaging.Conversations;
using AppDelicare.Messaging.Core;
using AppDelicare.Messaging.Core.Contracts;
using AppDelicare.Messaging.Experience;
using AppDelicare.Messaging.Options;
using AppDelicare.Messaging.Presentation;
using AppDelicare.Messaging.Processing;
using Microsoft.Extensions.Options;

namespace AppDelicare.Messaging.Audiences.Internal.Chung
{
    /// <summary>
    /// BOT-1.4 (BOT_TELEGRAM §3.1, §4): ⏳ Chờ tôi duyệt – <c>ApprovalRequest/GetMyPending?explicitOnly=true</c> (chỉ bước người đó đúng là người duyệt; admin không ngập),
    /// chi tiết (<c>GetDetail</c>), quyết định (<c>Decide</c>) bằng JWT của chính user. Theo mức rủi ro (<c>Messaging:ApprovalRiskLow</c>):
    /// <list type="bullet">
    /// <item><b>Thấp</b> (nghỉ, đổi ca, đổi món): 1 chạm → "Đang duyệt… [↩️ Hoàn tác]" <c>Sending:UndoSeconds</c> giây rồi mới gọi api; duyệt hàng loạt có xem trước.</item>
    /// <item><b>Cao</b> (còn lại / loại chưa khai báo): xác nhận 2 bước.</item>
    /// <item><b>Từ chối</b>: 2 bước + bắt buộc lý do (ForceReply – <see cref="ReplyContextStore"/>).</item>
    /// </list>
    /// Bấm 2 lần → 1 quyết định (UndoWindow theo khóa + api khóa sp_getapplock theo yêu cầu). Xong → thẻ cùng sự việc ở mọi người đổi trạng thái (TaskCard).
    /// Thiếu quyền / token hạn chế (PendingCode) → api 403 → bot báo rõ.
    /// </summary>
    public sealed class ApprovalActions(IDelicareApiClient web, UserTokenCache tokens, IChannelSender sender, UndoWindow undo, ReplyContextStore replies,
        TaskCard cards, IOptionsMonitor<MessagingHostOptions> options, TimeProvider time)
    {
        public const string RejectFlow = "apv-reject";
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
        private readonly ConcurrentDictionary<string, byte> _inFlight = new();

        public bool IsLowRisk(string? businessType)
            => businessType != null && options.CurrentValue.EffectiveRiskLow.Contains(businessType, StringComparer.OrdinalIgnoreCase);

        // ------------------------------------------------------------ danh sách

        public async Task ListAsync(BotActionContext ctx, CancellationToken ct)
        {
            var token = await TokenAsync(ctx, ct);
            if (token == null)
            {
                return;
            }
            var r = await web.GetAsUserAsync<ApprovalPendingModel>("api/ApprovalRequest/GetMyPending?explicitOnly=true", token, ct);
            if (!r.IsOk)
            {
                await SayAsync(ctx, ForbiddenText(r), ct);
                return;
            }
            var items = r.ListData ?? [];
            if (items.Count == 0)
            {
                await SayAsync(ctx, "Không có yêu cầu nào chờ bạn duyệt.", ct);
                return;
            }
            var message = new NeutralMessage
            {
                Title = $"⏳ Chờ tôi duyệt ({items.Count})",
                Lines = [.. items.Take(10).Select(i => $"• {i.RequestNo} – {i.Title ?? i.StepName}{(i.RequestedByName != null ? " · " + i.RequestedByName : "")}{(i.IsOverdue ? " ⚠️ quá hạn" : "")}")],
                Buttons = [.. items.Take(8).Select(i => new NeutralButton("📄 " + (i.RequestNo ?? "Xem"), CallbackData.Build(CallbackData.Kinds.Approval, "view", i.Id)))],
            };
            int low = items.Count(i => IsLowRisk(i.BusinessType));
            if (low >= 2)
            {
                message.Buttons.Add(new NeutralButton($"✅ Duyệt nhanh {low} mục rủi ro thấp", CallbackData.Build(CallbackData.Kinds.Approval, "bulk")));
            }
            await BotReply.SendAsync(sender, ctx.Bot, ctx.Event.ChatId, message, ct: ct);
        }

        // ------------------------------------------------------------ nút apv:* / undo:apv:*

        public async Task HandleAsync(BotActionContext ctx, CallbackData data, CancellationToken ct)
        {
            Guid? id = data.Guid;
            switch (data.Action)
            {
                case "view" when id != null:
                    await ViewAsync(ctx, id.Value, edit: true, ct);
                    break;
                case "ok" when id != null:
                    await ApproveTapAsync(ctx, id.Value, ct);
                    break;
                case "cf" when id != null:
                    await DecideAsync(ctx, id.Value, true, null, ctx.Event.MessageId?.ToString(), ct);
                    break;
                case "no" when id != null:
                    await AskReasonAsync(ctx, id.Value, ct);
                    break;
                case "bulk":
                    await BulkPreviewAsync(ctx, ct);
                    break;
                case "bulkok":
                    await BulkConfirmAsync(ctx, ct);
                    break;
                default:
                    await SayAsync(ctx, "Nút đã hết hạn.", ct);
                    break;
            }
        }

        public async Task UndoAsync(BotActionContext ctx, CallbackData data, CancellationToken ct)
        {
            string key = UndoKey(ctx, data.Id ?? string.Empty);
            if (undo.Cancel(key))
            {
                await EditAsync(ctx, new NeutralMessage
                {
                    Lines = ["↩️ Đã hoàn tác – chưa duyệt."],
                    Buttons = data.Guid is { } g ? [new NeutralButton("📄 Xem lại", CallbackData.Build(CallbackData.Kinds.Approval, "view", g))] : [],
                }, ct);
            }
            else
            {
                await SayAsync(ctx, "Đã quá thời gian hoàn tác.", ct);
            }
        }

        /// <summary>Người dùng trả lời câu hỏi lý do từ chối.</summary>
        public async Task ReplyAsync(BotActionContext ctx, PendingReply pending, CancellationToken ct)
        {
            if (!Guid.TryParseExact(pending.Key, "N", out var id))
            {
                return;
            }
            string? reason = ctx.Event.Text?.Trim();
            if (string.IsNullOrEmpty(reason))
            {
                await AskReasonAsync(ctx, id, ct);
                return;
            }
            await DecideAsync(ctx, id, false, reason, pending.Extra, ct);
        }

        // ------------------------------------------------------------ nội bộ

        private async Task ViewAsync(BotActionContext ctx, Guid id, bool edit, CancellationToken ct)
        {
            var token = await TokenAsync(ctx, ct);
            if (token == null)
            {
                return;
            }
            var d = await DetailAsync(id, token, ct);
            if (d == null)
            {
                await SayAsync(ctx, "Không xem được yêu cầu (đã xử lý hoặc không có quyền).", ct);
                return;
            }
            var m = Card(d.Value, id);
            if (edit && ctx.Event.MessageId != null)
            {
                await EditAsync(ctx, m, ct);
            }
            else
            {
                await BotReply.SendAsync(sender, ctx.Bot, ctx.Event.ChatId, m, ct: ct);
            }
        }

        private NeutralMessage Card(JsonElement d, Guid id)
        {
            var req = d.TryGetProperty("request", out var r) ? r : default;
            string? type = Str(req, "businessType");
            bool canDecide = d.TryGetProperty("canDecide", out var c) && c.ValueKind == JsonValueKind.True;
            var lines = new List<string>
            {
                $"{Str(req, "requestNo")} · {Str(d, "statusName")}",
                $"Người gửi: {Str(d, "requestedByName") ?? "-"}",
                $"Cấp {Num(d, "currentStepNo")}/{Num(d, "stepCount")}{(d.TryGetProperty("isOverdue", out var o) && o.ValueKind == JsonValueKind.True ? " ⚠️ quá hạn" : "")}",
            };
            var m = new NeutralMessage { Title = Str(d, "title") ?? "Yêu cầu duyệt", Lines = lines };
            if (!canDecide)
            {
                lines.Add("Bạn không duyệt được ở cấp này.");
                return m;
            }
            lines.Add(IsLowRisk(type) ? "Mức rủi ro: thấp (duyệt 1 chạm, hoàn tác trong " + undo.Delay.TotalSeconds + "″)" : "Mức rủi ro: cao (xác nhận 2 bước)");
            m.Buttons.Add(new NeutralButton("✅ Duyệt", CallbackData.Build(CallbackData.Kinds.Approval, "ok", id)));
            m.Buttons.Add(new NeutralButton("❌ Từ chối", CallbackData.Build(CallbackData.Kinds.Approval, "no", id)));
            return m;
        }

        private async Task ApproveTapAsync(BotActionContext ctx, Guid id, CancellationToken ct)
        {
            var token = await TokenAsync(ctx, ct);
            if (token == null)
            {
                return;
            }
            var d = await DetailAsync(id, token, ct);
            string? type = d is { } dd && dd.TryGetProperty("request", out var req) ? Str(req, "businessType") : null;
            string no = d is { } d2 && d2.TryGetProperty("request", out var rq) ? Str(rq, "requestNo") ?? "" : "";
            if (!IsLowRisk(type))
            {
                await EditAsync(ctx, new NeutralMessage
                {
                    Title = "Xác nhận duyệt " + no + "?",
                    Lines = [d is { } d3 ? Str(d3, "title") ?? "" : ""],
                    Buttons =
                    [
                        new NeutralButton("✅ Xác nhận duyệt", CallbackData.Build(CallbackData.Kinds.Approval, "cf", id)),
                        new NeutralButton("⬅️ Quay lại", CallbackData.Build(CallbackData.Kinds.Approval, "view", id)),
                    ],
                }, ct);
                return;
            }
            string key = UndoKey(ctx, id.ToString("N"));
            string? messageId = ctx.Event.MessageId?.ToString();
            if (!undo.Schedule(key, t => DecideAsync(ctx, id, true, null, messageId, t)))
            {
                return; // bấm lặp trong lúc đang chờ → 1 quyết định
            }
            await EditAsync(ctx, new NeutralMessage
            {
                Lines = [$"⏳ Đang duyệt {no}… ({undo.Delay.TotalSeconds}″)"],
                Buttons = [new NeutralButton("↩️ Hoàn tác", CallbackData.Build(CallbackData.Kinds.Undo, "apv", id))],
            }, ct);
        }

        private async Task AskReasonAsync(BotActionContext ctx, Guid id, CancellationToken ct)
        {
            var (html, _) = TelegramFormatter.Render(NeutralMessage.Text("Nhập lý do từ chối (trả lời vào tin này):"));
            var sent = await sender.SendAsync(ctx.Bot, new OutgoingMessage(ctx.Event.ChatId, html, new TgForceReply { InputFieldPlaceholder = "Lý do từ chối" }), ct);
            if (sent.ProviderMessageId != null)
            {
                replies.Remember(ctx.Bot, ctx.Event.ChatId, sent.ProviderMessageId, RejectFlow, id.ToString("N"), ctx.Event.MessageId?.ToString());
            }
        }

        /// <summary>Gọi api Decide (1 lần / yêu cầu / người – chặn bấm chồng), sửa thẻ của mình + thẻ cùng sự việc ở người khác.</summary>
        public async Task<bool> DecideAsync(BotActionContext ctx, Guid id, bool approve, string? note, string? cardMessageId, CancellationToken ct)
        {
            string flight = UndoKey(ctx, id.ToString("N")) + ":decide";
            if (!_inFlight.TryAdd(flight, 0))
            {
                return false;
            }
            try
            {
                var token = await TokenAsync(ctx, ct);
                if (token == null)
                {
                    return false;
                }
                var form = new List<KeyValuePair<string, string>> { new("Id", id.ToString()), new("Approve", approve ? "true" : "false") };
                if (!string.IsNullOrWhiteSpace(note))
                {
                    form.Add(new("Note", note));
                }
                var r = await web.PostFormAsUserAsync<bool>("api/ApprovalRequest/Decide", form, token, ct);
                if (!r.IsOk)
                {
                    await SayAsync(ctx, ForbiddenText(r), ct);
                    return false;
                }
                string done = $"{(approve ? "✅ Đã duyệt" : "❌ Đã từ chối")} bởi {ctx.Identity.FullName ?? "bạn"} – {Now():HH:mm}";
                if (cardMessageId != null)
                {
                    var (html, _) = TelegramFormatter.Render(NeutralMessage.Text(done));
                    await sender.EditAsync(ctx.Bot, ctx.Event.ChatId, cardMessageId, html, null, ct);
                }
                else
                {
                    await SayAsync(ctx, done, ct);
                }
                await cards.CloseCorrelatedAsync(TaskCard.ApprovalCorrelation(id), done, ctx.Event.ChatId, cardMessageId, ct);
                return true;
            }
            finally
            {
                _inFlight.TryRemove(flight, out _);
            }
        }

        private async Task BulkPreviewAsync(BotActionContext ctx, CancellationToken ct)
        {
            var low = await LowPendingAsync(ctx, ct);
            if (low == null)
            {
                return;
            }
            if (low.Count == 0)
            {
                await SayAsync(ctx, "Không còn mục rủi ro thấp chờ duyệt.", ct);
                return;
            }
            await EditAsync(ctx, new NeutralMessage
            {
                Title = $"Duyệt nhanh {low.Count} mục rủi ro thấp?",
                Lines = [.. low.Take(15).Select(i => $"• {i.RequestNo} – {i.Title}")],
                Buttons =
                [
                    new NeutralButton($"✅ Xác nhận duyệt {low.Count}", CallbackData.Build(CallbackData.Kinds.Approval, "bulkok")),
                    new NeutralButton("⬅️ Hủy", CallbackData.Build(CallbackData.Kinds.Menu, "act", "approvals")),
                ],
            }, ct);
        }

        private async Task BulkConfirmAsync(BotActionContext ctx, CancellationToken ct)
        {
            var low = await LowPendingAsync(ctx, ct);
            if (low is not { Count: > 0 })
            {
                return;
            }
            string key = UndoKey(ctx, "bulk");
            string? messageId = ctx.Event.MessageId?.ToString();
            bool scheduled = undo.Schedule(key, async t =>
            {
                int ok = 0;
                foreach (var item in low)
                {
                    if (await DecideAsync(ctx with { Event = ctx.Event with { MessageId = null } }, item.Id, true, null, null, t))
                    {
                        ok++;
                    }
                }
                if (messageId != null)
                {
                    var (html, _) = TelegramFormatter.Render(NeutralMessage.Text($"✅ Đã duyệt {ok}/{low.Count} mục – {Now():HH:mm}"));
                    await sender.EditAsync(ctx.Bot, ctx.Event.ChatId, messageId, html, null, t);
                }
            });
            if (!scheduled)
            {
                return;
            }
            await EditAsync(ctx, new NeutralMessage
            {
                Lines = [$"⏳ Đang duyệt {low.Count} mục… ({undo.Delay.TotalSeconds}″)"],
                Buttons = [new NeutralButton("↩️ Hoàn tác", CallbackData.Build(CallbackData.Kinds.Undo, "apv", "bulk"))],
            }, ct);
        }

        private async Task<List<ApprovalPendingModel>?> LowPendingAsync(BotActionContext ctx, CancellationToken ct)
        {
            var token = await TokenAsync(ctx, ct);
            if (token == null)
            {
                return null;
            }
            var r = await web.GetAsUserAsync<ApprovalPendingModel>("api/ApprovalRequest/GetMyPending?explicitOnly=true", token, ct);
            if (!r.IsOk)
            {
                await SayAsync(ctx, ForbiddenText(r), ct);
                return null;
            }
            return [.. (r.ListData ?? []).Where(i => IsLowRisk(i.BusinessType))];
        }

        private async Task<JsonElement?> DetailAsync(Guid id, string token, CancellationToken ct)
        {
            var r = await web.GetAsUserAsync<JsonElement>("api/ApprovalRequest/GetDetail?id=" + id, token, ct);
            return r.IsOk && r.Data.ValueKind == JsonValueKind.Object ? r.Data : null;
        }

        private async Task<string?> TokenAsync(BotActionContext ctx, CancellationToken ct)
        {
            if (!ctx.Identity.HasWebAccount)
            {
                await SayAsync(ctx, "Chức năng duyệt dành cho tài khoản web.", ct);
                return null;
            }
            var (token, error) = await tokens.GetAsync(ctx.Bot, ctx.Event.FromId, ct);
            if (token == null)
            {
                await SayAsync(ctx, error ?? "Không lấy được quyền truy cập.", ct);
            }
            return token?.Token;
        }

        private string ForbiddenText<T>(ApiResult<T> r)
            => r.ResponseCode is 401 or 403
                ? "Bạn chưa có quyền duyệt việc này trên bot. Nếu bạn có quyền duyệt trên web: lấy mã liên kết (Hồ sơ cá nhân → Liên kết Telegram) rồi gửi /start <mã>."
                : r.ResponseMessage ?? "Không thực hiện được.";

        private Task SayAsync(BotActionContext ctx, string text, CancellationToken ct) => BotReply.TextAsync(sender, ctx.Bot, ctx.Event.ChatId, text, ct: ct);

        private async Task EditAsync(BotActionContext ctx, NeutralMessage message, CancellationToken ct)
        {
            var (html, kb) = TelegramFormatter.Render(message);
            if (ctx.Event.MessageId is { } mid)
            {
                await sender.EditAsync(ctx.Bot, ctx.Event.ChatId, mid.ToString(), html, kb, ct);
            }
            else
            {
                await sender.SendAsync(ctx.Bot, new OutgoingMessage(ctx.Event.ChatId, html, kb), ct);
            }
        }

        private static string UndoKey(BotActionContext ctx, string id) => $"{ctx.Bot}:{ctx.Event.FromId}:apv:{id}";

        private DateTime Now() => TimeZoneInfo.ConvertTime(time.GetUtcNow(), Vn).DateTime;

        private static readonly TimeZoneInfo Vn = TryZone("Asia/Ho_Chi_Minh") ?? TryZone("SE Asia Standard Time") ?? TimeZoneInfo.Local;

        private static TimeZoneInfo? TryZone(string id)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string? Str(JsonElement e, string k)
            => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(k, out var v) ? v.ValueKind == JsonValueKind.String ? v.GetString() : v.ValueKind is JsonValueKind.Null ? null : v.ToString() : null;

        private static string Num(JsonElement e, string k) => Str(e, k) ?? "?";
    }
}
