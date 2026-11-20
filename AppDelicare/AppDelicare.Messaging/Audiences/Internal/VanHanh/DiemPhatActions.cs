using System.Globalization;
using System.Text.Json;
using AppDelicare.Messaging.Callback;
using AppDelicare.Messaging.Channels;
using AppDelicare.Messaging.Conversations;
using AppDelicare.Messaging.Core;
using AppDelicare.Messaging.Presentation;

namespace AppDelicare.Messaging.Audiences.Internal.VanHanh
{
    /// <summary>
    /// BOT-1.5b (BOT_TELEGRAM §3.2 – điểm phát suất, OPS-POS): 🚚 Xe đến hôm nay (điểm dừng của mọi chuyến hôm nay – <c>DeliveryRoute/GetBoard</c>: giờ dự kiến, trạng thái, số suất);
    /// 🧮 Đối soát suất (<c>OrderReconciliation/GetPendingOrders</c> ngày hôm nay → số suất thực tế phục vụ điền sẵn = số khách nhận khi bàn giao (POD) → <c>Reconcile</c>).
    /// Ký nhận tại điểm = nút ✅ Giao trên thẻ điểm của tài xế (GiaoVanActions); ghi phản ánh tại điểm: BOT-1.5c. Callback <c>rcn:&lt;hành động&gt;:&lt;OrderId N&gt;[.số]</c>.
    /// </summary>
    public sealed class DiemPhatActions(IDelicareApiClient web, UserTokenCache tokens, IChannelSender sender, ReplyContextStore replies, TimeProvider time)
        : WebActions(web, tokens, sender, replies), IBotActionProvider
    {
        public const string Kind = CallbackData.Kinds.Reconciliation;
        public const string ReplyServed = "rcn-served";
        public const string ReplyReceive = "rcn-receive";
        public const string Menu = "#/crm-reconciliation";

        public IEnumerable<BotAction> GetActions() =>
        [
            new BotAction { Code = "dp_arrivals", Module = "DIEMPHAT", Label = "🚚 Xe đến hôm nay", Command = "xeden", CommandDescription = "Xe / suất sắp đến điểm hôm nay", Bots = ["VH"], MenuMetaLink = Menu, Order = 1, Handler = ArrivalsAsync },
            new BotAction { Code = "dp_reconcile", Module = "DIEMPHAT", Label = "🧮 Đối soát suất", Command = "doisoat", CommandDescription = "Đối soát suất thực tế phục vụ", Bots = ["VH"], MenuMetaLink = Menu, Order = 2, Handler = PendingAsync },
        ];

        private string Today => VnTime.Today(time).ToString("yyyy-MM-dd");

        public async Task ArrivalsAsync(BotActionContext ctx, CancellationToken ct)
        {
            if (await TokenAsync(ctx, ct) is not { } token)
            {
                return;
            }
            var r = await Web.GetAsUserAsync<JsonElement>("api/DeliveryRoute/GetBoard?DeliveryDate=" + Today, token, ct);
            if (!r.IsOk)
            {
                await SayAsync(ctx, Problem(r), ct);
                return;
            }
            var stops = GiaoVanActions.Arr(r.Data, "listRoute")
                .SelectMany(route => GiaoVanActions.Arr(route, "listStop").Select(s => (Route: route, Stop: s)))
                .OrderBy(x => S(x.Stop, "plannedArrival")).ToList();
            // BOT-3.2 (D-72): ✅ Nhận hàng / ký nhận tại điểm – điểm chưa giao xong có nút (quyền ghi kiểm ở api DeliveryStop/Deliver).
            var open = stops.Where(x => S(x.Stop, "stopStatus") is not ("Delivered" or "Failed") && Guid.TryParse(S(x.Stop, "id"), out _)).Take(10).ToList();
            await SendAsync(ctx, new NeutralMessage
            {
                Title = $"🚚 Xe đến hôm nay {VnTime.Today(time):dd/MM} ({stops.Count} điểm)",
                Lines = stops.Count == 0 ? ["Chưa có chuyến giao nào hôm nay."] : [.. stops.Take(20).Select(x => Line(x.Route, x.Stop))],
                Buttons = [.. open.Select(x => new NeutralButton($"✅ Nhận {S(x.Stop, "customerName") ?? S(x.Stop, "locationName")}", CallbackData.Build(Kind, "rv", Guid.Parse(S(x.Stop, "id")!))))],
            }, ct);
        }

        /// <summary>BOT-3.2: chọn số suất nhận (điền sẵn số kế hoạch của điểm) → DeliveryStop/Deliver, người nhận = người dùng bot.</summary>
        private async Task AskReceiveAsync(BotActionContext ctx, Guid stopId, string token, CancellationToken ct)
        {
            var r = await Web.GetAsUserAsync<JsonElement>("api/DeliveryRoute/GetBoard?DeliveryDate=" + Today, token, ct);
            var stop = GiaoVanActions.Arr(r.Data, "listRoute").SelectMany(route => GiaoVanActions.Arr(route, "listStop"))
                .FirstOrDefault(s => Guid.TryParse(S(s, "id"), out var g) && g == stopId);
            if (stop.ValueKind != JsonValueKind.Object)
            {
                await SayAsync(ctx, r.IsOk ? "Điểm giao không còn trong danh sách hôm nay." : Problem(r), ct);
                return;
            }
            decimal planned = Dec(stop, "plannedMealQty");
            var presets = new[] { planned, Math.Floor(planned * 0.98m), Math.Floor(planned * 0.95m) }.Where(q => q > 0).Distinct().ToList();
            await ShowAsync(ctx, new NeutralMessage
            {
                Title = $"✅ Nhận hàng – {S(stop, "customerName") ?? S(stop, "locationName")}",
                Lines = [$"Kế hoạch {planned:0.##} suất · {S(stop, "statusName")}", "Số suất điểm đã nhận:"],
                Buttons = [.. presets.Select(q => new NeutralButton((q == planned ? "✅ Đủ " : "") + q.ToString("0.##", CultureInfo.InvariantCulture), CallbackData.Build(Kind, "rq", $"{stopId:N}.{q.ToString("0.##", CultureInfo.InvariantCulture)}"))),
                    new NeutralButton("Khác…", CallbackData.Build(Kind, "rx", stopId))],
            }, ct);
        }

        private async Task ReceiveAsync(BotActionContext ctx, Guid stopId, string token, string quantity, CancellationToken ct)
        {
            string receiver = string.IsNullOrWhiteSpace(ctx.Identity.FullName) ? "Điểm phát" : ctx.Identity.FullName!;
            var r = await Web.PostFormAsUserAsync<JsonElement>("api/DeliveryStop/Deliver",
                [new("Id", stopId.ToString()), new("DeliveredQuantity", quantity), new("AcceptedQuantity", quantity), new("ReceiverName", receiver[..Math.Min(receiver.Length, 100)]), new("Remark", "Ký nhận tại điểm (Telegram)")], token, ct);
            await SayAsync(ctx, r.IsOk ? $"✅ {r.ResponseMessage ?? "Đã ký nhận"} – {quantity} suất, người nhận {receiver}." : "⚠️ " + Problem(r), ct);
        }

        public async Task PendingAsync(BotActionContext ctx, CancellationToken ct)
        {
            if (await TokenAsync(ctx, ct) is not { } token)
            {
                return;
            }
            var r = await Web.GetAsUserAsync<JsonElement>("api/OrderReconciliation/GetPendingOrders?MealDate=" + Today, token, ct);
            if (!r.IsOk)
            {
                await SayAsync(ctx, Problem(r), ct);
                return;
            }
            var orders = (r.ListData ?? []).Where(o => Guid.TryParse(S(o, "orderId"), out _)).ToList();
            await ShowAsync(ctx, new NeutralMessage
            {
                Title = $"🧮 Chờ đối soát hôm nay ({orders.Count})",
                Lines = orders.Count == 0 ? ["Không có đơn nào chờ đối soát."]
                    : [.. orders.Take(12).Select(o => $"• {S(o, "orderNumber")} {S(o, "customerName")} · giao {N(o, "deliveredQuantity")} · nhận {N(o, "podAcceptedQuantity")}{(S(o, "reconciliationStatus") is { } st ? " · " + st : "")}")],
                Buttons = [.. orders.Take(12).Select(o => new NeutralButton($"🧮 {S(o, "orderNumber")}", CallbackData.Build(Kind, "o", Guid.Parse(S(o, "orderId")!))))],
            }, ct);
        }

        public async Task HandleAsync(BotActionContext ctx, CallbackData data, CancellationToken ct)
        {
            string[] parts = (data.Id ?? string.Empty).Split('.', 2);
            if (!Guid.TryParseExact(parts[0], "N", out Guid orderId) || await TokenAsync(ctx, ct) is not { } token)
            {
                return;
            }
            switch (data.Action)
            {
                case "rv":
                    await AskReceiveAsync(ctx, orderId, token, ct);   // id ở đây là DeliveryStop
                    break;
                case "rq" when parts.Length > 1:
                    await ReceiveAsync(ctx, orderId, token, parts[1], ct);
                    break;
                case "rx":
                    await AskAsync(ctx, "Nhập số suất điểm đã nhận:", ReplyReceive, orderId.ToString("N"), ct);
                    break;
                case "o":
                    await AskServedAsync(ctx, orderId, token, ct);
                    break;
                case "q" when parts.Length > 1:
                    await ReconcileAsync(ctx, orderId, token, parts[1], ct);
                    break;
                case "qx":
                    await AskAsync(ctx, "Nhập số suất thực tế đã phục vụ:", ReplyServed, orderId.ToString("N"), ct);
                    break;
                default:
                    await SayAsync(ctx, "Nút đã hết hạn.", ct);
                    break;
            }
        }

        public async Task ReplyAsync(BotActionContext ctx, PendingReply pending, CancellationToken ct)
        {
            if (!Guid.TryParseExact(pending.Key, "N", out Guid orderId) || await TokenAsync(ctx, ct) is not { } token)
            {
                return;
            }
            string text = (ctx.Event.Text ?? string.Empty).Trim().Replace(',', '.');
            if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) || v < 0)
            {
                await AskAsync(ctx, "Chưa đúng số – nhập lại:", pending.Flow, pending.Key, ct);
                return;
            }
            if (pending.Flow == ReplyReceive)
            {
                await ReceiveAsync(ctx, orderId, token, v.ToString("0.##", CultureInfo.InvariantCulture), ct);
                return;
            }
            await ReconcileAsync(ctx, orderId, token, v.ToString("0.##", CultureInfo.InvariantCulture), ct);
        }

        private async Task AskServedAsync(BotActionContext ctx, Guid orderId, string token, CancellationToken ct)
        {
            var r = await Web.GetAsUserAsync<JsonElement>("api/OrderReconciliation/GetPendingOrders?MealDate=" + Today, token, ct);
            var o = (r.ListData ?? []).FirstOrDefault(x => Guid.TryParse(S(x, "orderId"), out var g) && g == orderId);
            if (o.ValueKind != JsonValueKind.Object)
            {
                await SayAsync(ctx, r.IsOk ? "Đơn không còn chờ đối soát." : Problem(r), ct);
                return;
            }
            // Điền sẵn: số khách nhận khi bàn giao (POD), không có thì số đã giao.
            decimal accepted = Dec(o, "podAcceptedQuantity"), delivered = Dec(o, "deliveredQuantity");
            decimal known = accepted > 0 ? accepted : delivered;
            var presets = new[] { known, Math.Floor(known * 0.98m), Math.Floor(known * 0.95m) }.Where(q => q > 0).Distinct().ToList();
            await ShowAsync(ctx, new NeutralMessage
            {
                Title = $"🧮 {S(o, "orderNumber")} – {S(o, "customerName")}",
                Lines = [$"Đặt {N(o, "orderedQuantity")} · giao {delivered:0.##} · khách nhận {accepted:0.##}", "Số suất thực tế đã phục vụ:"],
                Buttons = [.. presets.Select(q => new NeutralButton((q == known ? "✅ Đúng " : "") + q.ToString("0.##", CultureInfo.InvariantCulture), CallbackData.Build(Kind, "q", $"{orderId:N}.{q.ToString("0.##", CultureInfo.InvariantCulture)}"))),
                    new NeutralButton("Khác…", CallbackData.Build(Kind, "qx", orderId))],
            }, ct);
        }

        private async Task ReconcileAsync(BotActionContext ctx, Guid orderId, string token, string served, CancellationToken ct)
        {
            var r = await Web.PostFormAsUserAsync<JsonElement>("api/OrderReconciliation/Reconcile",
                [new("OrderId", orderId.ToString()), new("ServedQuantity", served), new("Remark", "Telegram")], token, ct);
            if (!r.IsOk)
            {
                await SayAsync(ctx, "⚠️ " + Problem(r), ct);
                return;
            }
            await SendAsync(ctx, new NeutralMessage
            {
                Title = "✅ " + (r.ResponseMessage ?? "Đã đối soát"),
                Lines = [$"{S(r.Data, "orderNumber")} – {S(r.Data, "customerName")}: phục vụ {served} suất · tính tiền {N(r.Data, "billingQuantity")} suất ({S(r.Data, "billingBasis")})"],
            }, ct);
        }

        private static string Line(JsonElement route, JsonElement s)
        {
            string icon = S(s, "stopStatus") switch { "Arrived" => "📍", "Delivered" => "✅", "Failed" => "❌", _ => S(route.TryGetProperty("route", out var rt) ? rt : default, "status") == "InTransit" ? "🚚" : "⏳" };
            string eta = DateTime.TryParse(S(s, "plannedArrival"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t.ToString("HH:mm") : "--:--";
            return $"{icon} {eta} {S(s, "customerName") ?? S(s, "locationName")} · {N(s, "plannedMealQty")} suất · {S(s, "statusName")}";
        }

        private static string? S(JsonElement e, string k) => BepActions.S(e, k);

        private static decimal Dec(JsonElement e, string k) => decimal.TryParse(S(e, k), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : 0m;

        private static string N(JsonElement e, string k) => Dec(e, k).ToString("0.##", CultureInfo.InvariantCulture);
    }
}
