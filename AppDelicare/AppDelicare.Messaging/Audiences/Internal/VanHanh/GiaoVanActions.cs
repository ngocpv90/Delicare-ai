using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using AppDelicare.Messaging.Callback;
using AppDelicare.Messaging.Channels;
using AppDelicare.Messaging.Conversations;
using AppDelicare.Messaging.Core;
using AppDelicare.Messaging.Experience;
using AppDelicare.Messaging.Presentation;
using Microsoft.Extensions.Configuration;

namespace AppDelicare.Messaging.Audiences.Internal.VanHanh
{
    /// <summary>
    /// BOT-1.5b (BOT_TELEGRAM §3.2 – điều phối giao nhận + tài xế, OPS-LOG):
    /// 🚚 Chuyến của tôi (<c>DeliveryRoute/GetMine</c> – DriverId = nhân viên của tài khoản) → thẻ chuyến (<c>GetDetail</c>): điểm dừng, điều kiện xuất phát còn thiếu,
    /// [🚀 Xuất phát] kèm nhiệt độ (nút ≥ 60 °C / gõ – <c>Dispatch</c>) → thẻ điểm: [📍 Đã đến] (<c>DeliveryStop/Arrive</c>), [✅ Giao] (số suất điền sẵn = đã xếp / kế hoạch
    /// → nhiệt độ bàn giao → tên người nhận gõ tay – <c>Deliver</c>), [❌ Không giao được] (lý do chọn sẵn – <c>Fail</c>); 📥 Nhận hộp về (<c>ContainerReturn/Record</c>, số hộp gõ tay);
    /// 🗺 Giao vận hôm nay (điều phối – <c>GetBoard</c>). Nút hiện theo <c>AllowedActions</c> của api; api kiểm quyền khi ghi.
    /// Callback <c>dlv:&lt;hành động&gt;:&lt;Id N&gt;[.giá trị…]</c> (≤ 64 byte). Ảnh ký nhận / đổi xe / vị trí trực tiếp: BOT-1.5c / BOT-2.4.
    /// </summary>
    /// <remarks>
    /// BOT-UX-2: tài xế giao đủ 1 điểm = 3 chạm (⭐ Chuyến của tôi → điểm → [✅ Giao đủ N · t °C]): số suất điền sẵn = đã xếp / kế hoạch, nhiệt độ bàn giao điền sẵn
    /// = lần đo tại điểm, không có thì lần đo lúc xuất phát (hiện rõ trên nút), người nhận điền sẵn = người nhận gần nhất tại điểm đó (bộ nhớ) hoặc "Đại diện &lt;khách&gt;"
    /// ([👤 Người nhận khác…] mới gõ tay). Chưa có lần đo nào → chọn nhiệt độ (1 chạm). Số khác / số hộp nhận về / nhiệt độ khác = màn số; lý do không giao được = nút
    /// (<c>Messaging:Reasons:dlv-fail</c>) + [✏️ Khác…]. Danh sách 6 mục / trang, chuyến trễ lên đầu.
    /// </remarks>
    public sealed class GiaoVanActions(IDelicareApiClient web, UserTokenCache tokens, IChannelSender sender, ReplyContextStore replies, TimeProvider time, IConfiguration configuration,
        ReasonCatalog reasons)
        : WebActions(web, tokens, sender, replies), IBotActionProvider
    {
        public const string Kind = CallbackData.Kinds.Delivery;
        /// <summary>BOT-UX-2: gõ tên người nhận khác (khóa = Id điểm N) – nhớ cho điểm đó rồi mở lại thẻ điểm.</summary>
        public const string ReplyReceiverName = "dlv-rname";
        /// <summary>BOT-UX-2: lý do không giao được gõ tay ([✏️ Khác…], khóa = Id điểm N).</summary>
        public const string ReplyFail = "dlv-fail";
        /// <summary>Người nhận gần nhất theo (bot, điểm giao / điểm dừng) – bộ nhớ, khởi động lại thì về "Đại diện &lt;khách&gt;".</summary>
        private readonly ConcurrentDictionary<string, string> _receivers = new();
        public const string ReplyDispatchTemp = "dlv-dtemp";
        public const string ReplyQty = "dlv-qty";
        public const string ReplyReceiver = "dlv-recv";
        /// <summary>BOT-3.2: tìm tài xế mới theo tên / mã (khóa = Id chuyến N).</summary>
        public const string ReplyDriver = "dlv-driver";
        /// <summary>BOT-3.2: danh sách xe / tài xế vừa hiện theo chat (nút mang số thứ tự – callback ≤ 64 byte).</summary>
        private readonly ConcurrentDictionary<string, List<(Guid Id, string Label)>> _picks = new();

        private async Task PickVehicleAsync(BotActionContext ctx, Guid routeId, string token, CancellationToken ct)
        {
            var r = await Web.GetAsUserAsync<JsonElement>("api/Vehicle/GetAll?Status=Active&Page=1&PageSize=30", token, ct);
            if (!r.IsOk)
            {
                await SayAsync(ctx, Problem(r), ct);
                return;
            }
            var list = (r.ListData ?? []).Where(v => Guid.TryParse(S(v, "id"), out _))
                .Select(v => (Id: Guid.Parse(S(v, "id")!), Label: $"{S(v, "licensePlate") ?? S(v, "code")}{(S(v, "capacity") is { } c ? " · " + Num(c) + " kg" : "")}")).Take(20).ToList();
            _picks[ctx.Event.ChatId] = list;
            await ShowAsync(ctx, new NeutralMessage
            {
                Title = "🔁 Chọn xe mới",
                Lines = list.Count == 0 ? ["Không có xe đang hoạt động."] : ["Chọn xe thay cho chuyến:"],
                Buttons = [.. list.Select((v, i) => new NeutralButton("🚚 " + v.Label, CallbackData.Build(Kind, "pv", $"{routeId:N}.{i}"))), .. Screens.Back("⬅️ Quay lại", CallbackData.Build(Kind, "r", routeId))],
            }, ct);
        }

        private async Task PickDriverAsync(BotActionContext ctx, Guid routeId, string token, string search, CancellationToken ct)
        {
            var r = await Web.GetAsUserAsync<JsonElement>("api/Employee/GetAll?Status=Active&Page=1&PageSize=8&SearchText=" + Uri.EscapeDataString(search), token, ct);
            if (!r.IsOk)
            {
                await SayAsync(ctx, Problem(r), ct);
                return;
            }
            var list = (r.ListData ?? []).Where(e => Guid.TryParse(S(e, "id"), out _))
                .Select(e => (Id: Guid.Parse(S(e, "id")!), Label: $"{S(e, "fullName")} · {S(e, "code")}")).ToList();
            if (list.Count == 0)
            {
                await AskAsync(ctx, $"Không tìm thấy \"{search}\" – gõ lại tên / mã tài xế:", ReplyDriver, routeId.ToString("N"), ct);
                return;
            }
            _picks[ctx.Event.ChatId] = list;
            await SendAsync(ctx, new NeutralMessage
            {
                Title = "👤 Chọn tài xế mới",
                Buttons = [.. list.Select((v, i) => new NeutralButton("👤 " + v.Label, CallbackData.Build(Kind, "pd", $"{routeId:N}.{i}"))), .. Screens.Back("⬅️ Quay lại", CallbackData.Build(Kind, "r", routeId))],
            }, ct);
        }

        private async Task ReassignPickedAsync(BotActionContext ctx, Guid routeId, string token, int index, string field, CancellationToken ct)
        {
            if (!_picks.TryGetValue(ctx.Event.ChatId, out var list) || index < 0 || index >= list.Count)
            {
                await SayAsync(ctx, "Danh sách đã hết hạn – bấm lại 🔁 Đổi xe / 👤 Đổi tài xế.", ct);
                return;
            }
            var r = await Web.PostFormAsUserAsync<JsonElement>("api/DeliveryRoute/Reassign", [new("Id", routeId.ToString()), new(field, list[index].Id.ToString()), new("Note", "Đổi qua Telegram")], token, ct);
            string note = r.IsOk ? InputWidgets.Done(r.ResponseMessage ?? "Đã đổi", time) + string.Concat(Arr(r.Data, "listWarning").Select(w => "\n⚠️ " + (w.ValueKind == JsonValueKind.String ? w.GetString() : w.ToString())))
                : "⚠️ " + Problem(r);
            await RouteCardAsync(ctx, routeId, token, note, ct);
        }
        public const string ReplyBoxes = "dlv-box";
        public static readonly decimal[] Temps = [60m, 62m, 65m, 68m, 70m, 75m];

        public IEnumerable<BotAction> GetActions() =>
        [
            new BotAction { Code = "gv_mine", Module = "GIAOVAN", Label = "🚚 Chuyến của tôi", Command = "chuyen", CommandDescription = "Chuyến giao hôm nay của tôi", Bots = ["VH"], MenuMetaLink = "#/log-stop", Order = 1, Handler = MineAsync },
            new BotAction { Code = "gv_board", Module = "GIAOVAN", Label = "🗺 Giao vận hôm nay", Command = "giaovan", CommandDescription = "Mọi chuyến giao hôm nay", Bots = ["VH"], MenuMetaLink = "#/log-route", Order = 2, Handler = BoardAsync },
            new BotAction { Code = "gv_box", Module = "GIAOVAN", Label = "📥 Nhận hộp về", Command = "hop", CommandDescription = "Ghi số hộp khay nhận về", Bots = ["VH"], MenuMetaLink = "#/log-packaging-ledger" /* UI-6.45: Thu hồi khay hộp trong Khay hộp theo khách (UI-6.35) */, Order = 3, Handler = BoxesAsync },
            // BOT-3.1 (UG-1 C-22): lịch xe ngày mai, hộp khay hôm nay ("Chuyến trực tiếp" = 🗺 Giao vận hôm nay → thẻ chuyến).
            new BotAction { Code = "gv_schedule", Module = "GIAOVAN", Label = "🧭 Lịch xe", Command = "lichxe", CommandDescription = "Lịch xe ngày mai", Bots = ["VH"], MenuMetaLink = "#/log-route", Order = 4, Handler = ScheduleAsync },
            new BotAction { Code = "gv_boxes_today", Module = "GIAOVAN", Label = "📦 Hộp khay hôm nay", Command = "hopkhay", CommandDescription = "Hộp khay phải thu / đã thu hôm nay", Bots = ["VH"], MenuMetaLink = "#/log-packaging-ledger" /* UI-6.45: Thu hồi khay hộp trong Khay hộp theo khách (UI-6.35) */, Order = 5, Handler = BoxesTodayAsync },
        ];

        private string Today => VnTime.Today(time).ToString("yyyy-MM-dd");

        /// <summary>BOT-3.1 (UG-1 C-22): 🧭 Lịch xe ngày mai – xe, tài xế, giờ, số điểm / suất (DeliveryRoute/GetBoard ngày mai); bấm mở thẻ chuyến.</summary>
        public async Task ScheduleAsync(BotActionContext ctx, CancellationToken ct)
        {
            DateTime day = VnTime.Today(time).AddDays(1);
            if (await TokenAsync(ctx, ct) is not { } token || await RoutesAsync(ctx, token, "api/DeliveryRoute/GetBoard?DeliveryDate=" + day.ToString("yyyy-MM-dd"), ct) is not { } routes)
            {
                return;
            }
            var m = new NeutralMessage { Title = $"🧭 Lịch xe {day:dd/MM} ({routes.Count} chuyến)", Lines = routes.Count == 0 ? ["Chưa lập chuyến nào cho ngày mai."] : [] };
            // BOT-UX-2 (mục 10, 11): chuyến chưa có xe / tài xế lên đầu, 6 chuyến / trang.
            var sorted = routes.OrderBy(r => S(r, "driverName") == null || (S(r, "licensePlate") ?? S(r, "vehicleCode")) == null ? 0 : 1).ToList();
            ListPager.Fill(m, ctx, "gv_schedule", sorted, r => $"{RouteLine(r)} · {S(r, "licensePlate") ?? S(r, "vehicleCode") ?? "chưa có xe"} · {S(r, "driverName") ?? "chưa có tài xế"}",
                r => new NeutralButton($"{RouteIcon(r)} {RouteNo(r)}", CallbackData.Build(Kind, "r", RouteId(r))));
            await ShowAsync(ctx, m, ct);
        }

        /// <summary>BOT-3.1: 📦 Hộp khay hôm nay – phải thu / đã thu / hỏng theo đơn giao (ContainerReturn/GetList hôm nay).</summary>
        public async Task BoxesTodayAsync(BotActionContext ctx, CancellationToken ct)
        {
            if (await TokenAsync(ctx, ct) is not { } token)
            {
                return;
            }
            var r = await Web.GetAsUserAsync<JsonElement>($"api/ContainerReturn/GetList?from={Today}&to={Today}&page=1&pageSize=100", token, ct);
            if (!r.IsOk)
            {
                await SayAsync(ctx, Problem(r), ct);
                return;
            }
            var items = Arr(r.Data, "listItem");
            decimal Q(JsonElement x, string k) => decimal.TryParse(S(x.TryGetProperty("return", out var rt) ? rt : x, k), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : 0m;
            decimal expected = items.Sum(x => Q(x, "expectedQuantity")), returned = items.Sum(x => Q(x, "returnedQuantity")), damaged = items.Sum(x => Q(x, "damagedQuantity"));
            var lines = new List<string> { $"Phải thu {expected:#,0} · đã thu {returned:#,0} · hỏng {damaged:#,0} · còn thiếu {Math.Max(0, expected - returned):#,0}" };
            lines.AddRange(items.Take(14).Select(x => $"• {S(x, "customerName") ?? S(x, "deliveryNumber")} · {S(x, "packagingType")} {Q(x, "returnedQuantity"):#,0}/{Q(x, "expectedQuantity"):#,0} · {S(x, "statusName")}"));
            await ShowAsync(ctx, new NeutralMessage { Title = $"📦 Hộp khay hôm nay ({items.Count} lượt)", Lines = items.Count == 0 ? ["Chưa có lượt thu hồi hộp khay nào hôm nay."] : lines }, ct);
        }

        private async Task<List<JsonElement>?> RoutesAsync(BotActionContext ctx, string token, string path, CancellationToken ct)
        {
            var r = await Web.GetAsUserAsync<JsonElement>(path, token, ct);
            if (!r.IsOk || r.Data.ValueKind != JsonValueKind.Object)
            {
                await SayAsync(ctx, Problem(r), ct);
                return null;
            }
            return Arr(r.Data, "listRoute");
        }

        public async Task MineAsync(BotActionContext ctx, CancellationToken ct)
        {
            if (await TokenAsync(ctx, ct) is not { } token || await RoutesAsync(ctx, token, "api/DeliveryRoute/GetMine?deliveryDate=" + Today, ct) is not { } routes)
            {
                return;
            }
            if (routes.Count == 1)
            {
                await RouteCardAsync(ctx, RouteId(routes[0]), token, null, ct);
                return;
            }
            var m = new NeutralMessage { Title = $"🚚 Chuyến của tôi hôm nay ({routes.Count})", Lines = routes.Count == 0 ? ["Hôm nay bạn chưa được phân chuyến nào."] : [] };
            // BOT-UX-2 (mục 10, 11): chuyến đang chạy / chưa xuất phát trước, chuyến xong cuối; 6 chuyến / trang.
            ListPager.Fill(m, ctx, "gv_mine", [.. routes.OrderBy(RouteOrder)], RouteLine, r => new NeutralButton($"{RouteIcon(r)} {RouteNo(r)}", CallbackData.Build(Kind, "r", RouteId(r))));
            await ShowAsync(ctx, m, ct);
        }

        /// <summary>Thứ tự việc gấp: đang chạy → trễ giờ xuất phát → chưa xuất phát → xong.</summary>
        private int RouteOrder(JsonElement r)
        {
            string? status = S(Route(r), "status");
            bool late = status is "Planned" or "Loading" && DateTime.TryParse(S(Route(r), "plannedStart"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var start) && start < VnTime.Now(time);
            return late ? 0 : status switch { "InTransit" => 1, "Planned" or "Loading" => 2, _ => 3 };
        }

        public async Task BoardAsync(BotActionContext ctx, CancellationToken ct)
        {
            if (await TokenAsync(ctx, ct) is not { } token || await RoutesAsync(ctx, token, "api/DeliveryRoute/GetBoard?DeliveryDate=" + Today, ct) is not { } routes)
            {
                return;
            }
            var m = new NeutralMessage { Title = $"🗺 Giao vận {VnTime.Today(time):dd/MM} ({routes.Count} chuyến)", Lines = routes.Count == 0 ? ["Chưa có chuyến nào hôm nay."] : [] };
            ListPager.Fill(m, ctx, "gv_board", [.. routes.OrderBy(RouteOrder)], r => RouteLine(r) + (S(r, "driverName") is { } d ? " · " + d : ""),
                r => new NeutralButton($"{RouteIcon(r)} {RouteNo(r)}", CallbackData.Build(Kind, "r", RouteId(r))));
            await ShowAsync(ctx, m, ct);
        }

        public async Task BoxesAsync(BotActionContext ctx, CancellationToken ct)
        {
            if (await TokenAsync(ctx, ct) is not { } token || await RoutesAsync(ctx, token, "api/DeliveryRoute/GetMine?deliveryDate=" + Today, ct) is not { } routes)
            {
                return;
            }
            var stops = routes.SelectMany(r => Arr(r, "listStop")).Where(s => S(s, "stopStatus") == "Delivered" && Guid.TryParse(S(s, "deliveryOrderId"), out _)).ToList();
            var m = new NeutralMessage
            {
                Title = "📥 Nhận hộp khay về",
                Lines = stops.Count == 0 ? ["Chưa có điểm nào đã giao hôm nay trên chuyến của bạn."] : ["Chọn điểm đã giao để ghi số hộp nhận về:"],
            };
            ListPager.Fill(m, ctx, "gv_box", stops, null, s => new NeutralButton($"📦 {S(s, "customerName") ?? S(s, "deliveryNumber")}", CallbackData.Build(Kind, "bx", Guid.Parse(S(s, "deliveryOrderId")!))));
            await ShowAsync(ctx, m, ct);
        }

        /// <summary>BOT-2.4 (QR <c>start=VEH_&lt;mã&gt;</c> dán trên xe): chuyến hôm nay của xe – chuyến của tôi trước, không có thì bảng giao vận (nếu có quyền) → thẻ chuyến [🚀 Xuất phát].</summary>
        public async Task VehicleAsync(BotActionContext ctx, string code, CancellationToken ct)
        {
            if (await TokenAsync(ctx, ct) is not { } token)
            {
                return;
            }
            // Mã xe hoặc biển số (bỏ dấu - . khoảng trắng: QR chỉ chứa chữ / số).
            static string Plain(string? v) => new((v ?? string.Empty).Where(char.IsAsciiLetterOrDigit).ToArray());
            bool Match(JsonElement r) => string.Equals(S(r, "vehicleCode"), code, StringComparison.OrdinalIgnoreCase)
                || (Plain(S(r, "licensePlate")) is { Length: > 0 } plate && string.Equals(plate, Plain(code), StringComparison.OrdinalIgnoreCase));
            var routes = new List<JsonElement>();
            foreach (string path in new[] { "api/DeliveryRoute/GetMine?deliveryDate=" + Today, "api/DeliveryRoute/GetBoard?DeliveryDate=" + Today })
            {
                var r = await Web.GetAsUserAsync<JsonElement>(path, token, ct);
                if (r.IsOk && r.Data.ValueKind == JsonValueKind.Object)
                {
                    routes = [.. Arr(r.Data, "listRoute").Where(Match)];
                }
                if (routes.Count > 0)
                {
                    break;
                }
            }
            if (routes.Count == 1)
            {
                await RouteCardAsync(ctx, RouteId(routes[0]), token, $"🚐 Xe {code}", ct);
                return;
            }
            await ShowAsync(ctx, new NeutralMessage
            {
                Title = $"🚐 Xe {code} – chuyến hôm nay ({routes.Count})",
                Lines = routes.Count == 0 ? ["Hôm nay xe này không có chuyến (trong phạm vi bạn được xem)."] : [.. routes.Select(RouteLine)],
                Buttons = [.. routes.Take(8).Select(r => new NeutralButton($"{RouteIcon(r)} {RouteNo(r)}", CallbackData.Build(Kind, "r", RouteId(r))))],
            }, ct);
        }

        // ---------- BOT-2.4: vị trí trực tiếp tài xế ----------

        private sealed class LiveSession
        {
            public readonly SemaphoreSlim Gate = new(1, 1);
            public DateTimeOffset LoadedAt = DateTimeOffset.MinValue;
            public JsonElement Board;
            public bool InKitchen;
            public readonly HashSet<Guid> Arrived = [];
            public readonly HashSet<Guid> Departed = [];
        }

        private readonly ConcurrentDictionary<string, LiveSession> _live = new();

        /// <summary>Bán kính mặc định (m) – <c>Messaging:LiveLocation:RadiusMeters</c> (PROPOSED 150 m, BOT_TELEGRAM §6); bếp có RadiusMeters riêng thì dùng của bếp.</summary>
        private double Radius => double.TryParse(configuration["Messaging:LiveLocation:RadiusMeters"], NumberStyles.Number, CultureInfo.InvariantCulture, out var m) && m > 0 ? m : 150;

        /// <summary>
        /// BOT-2.4 (BOT_TELEGRAM §6): tài xế chia sẻ vị trí trực tiếp → mỗi cập nhật so với điểm giao của chuyến đang giao (tọa độ CustomerDeliveryLocation) và các bếp
        /// (KitchenWorkstation có tọa độ – DeliveryRoute/GetMine). Chỉ gọi api khi VÀO bán kính điểm giao (ghi Đã đến 1 lần – DeliveryStop/Arrive kèm tọa độ, gửi thẻ điểm
        /// [✅ Giao] [❌ Không giao được]) hoặc RA khỏi bếp (chuyến chưa xuất phát: đã đo nhiệt độ đạt → ghi xuất phát; chưa đo → hỏi nhiệt độ). KHÔNG lưu từng điểm vị trí.
        /// Mất tín hiệu / không chia sẻ → nút 📍 Đã đến / 🚀 Xuất phát vẫn dùng như cũ. Quyền: JWT của chính tài xế (như bấm nút).
        /// </summary>
        public async Task LiveLocationAsync(BotActionContext ctx, CancellationToken ct)
        {
            if (ctx.Event.Location is not { } loc)
            {
                return;
            }
            string key = ctx.Bot + ":" + ctx.Event.FromId;
            if (!ctx.Event.Edited)
            {
                _live.TryRemove(key, out _); // tin vị trí trực tiếp mới = bắt đầu chia sẻ lại
            }
            bool first = !_live.ContainsKey(key);
            var session = _live.GetOrAdd(key, _ => new LiveSession());
            if (!await session.Gate.WaitAsync(0, ct))
            {
                return; // đang xử lý cập nhật trước – bỏ cập nhật này (cập nhật sau sẽ tới)
            }
            try
            {
                string? token = first ? await TokenAsync(ctx, ct) : await QuietTokenAsync(ctx, ct);
                if (token == null)
                {
                    return;
                }
                var now = time.GetUtcNow();
                if (now - session.LoadedAt > TimeSpan.FromMinutes(2))
                {
                    var r = await Web.GetAsUserAsync<JsonElement>("api/DeliveryRoute/GetMine?deliveryDate=" + Today, token, ct);
                    if (!r.IsOk || r.Data.ValueKind != JsonValueKind.Object)
                    {
                        if (first)
                        {
                            await SayAsync(ctx, Problem(r), ct);
                        }
                        return;
                    }
                    session.Board = r.Data.Clone();
                    session.LoadedAt = now;
                }
                var routes = Arr(session.Board, "listRoute");
                if (first)
                {
                    await SayAsync(ctx, routes.Count == 0
                        ? "Hôm nay bạn chưa có chuyến – vị trí trực tiếp không được dùng."
                        : $"📡 Đã nhận vị trí trực tiếp. Bot tự ghi xuất phát khi xe rời bếp và ghi đã đến khi vào bán kính {Radius:0} m của điểm giao. Nút 📍 Đã đến vẫn dùng được khi mất tín hiệu.", ct);
                }
                bool inKitchen = Arr(session.Board, "listKitchen")
                    .Any(k => Distance(loc.Latitude, loc.Longitude, Dec(k, "latitude"), Dec(k, "longitude")) <= (Dec(k, "radiusMeters") > 0 ? (double)Dec(k, "radiusMeters") : Radius));
                bool leftKitchen = session.InKitchen && !inKitchen;
                session.InKitchen = inKitchen;
                bool changed = false;
                if (leftKitchen)
                {
                    foreach (var route in routes.Where(r => S(Route(r), "status") is "Planned" or "Loading"))
                    {
                        Guid id = RouteId(route);
                        if (!session.Departed.Add(id))
                        {
                            continue;
                        }
                        changed = true;
                        decimal min = Dec(route, "deliveryTempMinC") > 0 ? Dec(route, "deliveryTempMinC") : 60m;
                        if (S(route, "departureTemperatureC") != null && Dec(route, "departureTemperatureC") >= min)
                        {
                            var form = new List<KeyValuePair<string, string>> { new("Id", id.ToString()), new("Note", "Telegram – vị trí trực tiếp"), new("Latitude", Coord(loc.Latitude)), new("Longitude", Coord(loc.Longitude)) };
                            var d = await Web.PostFormAsUserAsync<JsonElement>("api/DeliveryRoute/Dispatch", form, token, ct);
                            await RouteCardAsync(ctx, id, token, d.IsOk ? "📡 Xe đã rời bếp – đã tự ghi xuất phát." : "📡 Xe đã rời bếp nhưng chưa ghi được xuất phát: " + Problem(d), ct);
                        }
                        else
                        {
                            await SayAsync(ctx, $"📡 Xe đã rời bếp – chuyến {RouteNo(route)} chưa ghi xuất phát (chưa đo nhiệt độ). Chọn nhiệt độ để ghi:", ct);
                            await DispatchAskAsync(ctx, id, token, ct);
                        }
                    }
                }
                foreach (var route in routes.Where(r => S(Route(r), "status") == "InTransit"))
                {
                    foreach (var stop in Arr(route, "listStop").Where(s => S(s, "stopStatus") == "Planned" && S(s, "latitude") != null && S(s, "longitude") != null))
                    {
                        if (!Guid.TryParse(S(stop, "id"), out Guid stopId) || session.Arrived.Contains(stopId)
                            || Distance(loc.Latitude, loc.Longitude, Dec(stop, "latitude"), Dec(stop, "longitude")) > Radius)
                        {
                            continue;
                        }
                        session.Arrived.Add(stopId);
                        changed = true;
                        var form = new List<KeyValuePair<string, string>> { new("Id", stopId.ToString()), new("Latitude", Coord(loc.Latitude)), new("Longitude", Coord(loc.Longitude)), new("Note", "Telegram – vị trí trực tiếp") };
                        var a = await Web.PostFormAsUserAsync<JsonElement>("api/DeliveryStop/Arrive", form, token, ct);
                        await StopCardAsync(ctx, stopId, token, a.IsOk ? $"📡 Đã tự ghi đến điểm lúc {VnTime.Now(time):HH:mm} (vị trí trực tiếp)." : "📡 Đã tới điểm nhưng chưa ghi được: " + Problem(a), ct);
                    }
                }
                if (changed)
                {
                    session.LoadedAt = DateTimeOffset.MinValue; // trạng thái đổi → lần sau tải lại
                }
            }
            finally
            {
                session.Gate.Release();
            }
        }

        /// <summary>Khoảng cách (m) giữa 2 tọa độ – công thức haversine.</summary>
        public static double Distance(double lat1, double lng1, decimal lat2, decimal lng2)
        {
            const double R = 6371000;
            double la2 = (double)lat2, lo2 = (double)lng2;
            double dLat = (la2 - lat1) * Math.PI / 180, dLng = (lo2 - lng1) * Math.PI / 180;
            double h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(la2 * Math.PI / 180) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
            return 2 * R * Math.Asin(Math.Min(1, Math.Sqrt(h)));
        }

        private static string Coord(double v) => v.ToString("0.0000000", CultureInfo.InvariantCulture);

        public async Task HandleAsync(BotActionContext ctx, CallbackData data, CancellationToken ct)
        {
            string[] parts = (data.Id ?? string.Empty).Split('.');
            if (!Guid.TryParseExact(parts[0], "N", out Guid id) || await TokenAsync(ctx, ct) is not { } token)
            {
                return;
            }
            string? v1 = parts.Length > 1 ? parts[1] : null, v2 = parts.Length > 2 ? parts[2] : null;
            switch (data.Action)
            {
                case "r":
                    await RouteCardAsync(ctx, id, token, null, ct);
                    break;
                case "rv":
                    await PickVehicleAsync(ctx, id, token, ct);
                    break;
                case "rd":
                    await AskAsync(ctx, "👤 Gõ tên hoặc mã tài xế mới để tìm:", ReplyDriver, id.ToString("N"), ct);
                    break;
                case "pv" when int.TryParse(v1, out int vi):
                    await ReassignPickedAsync(ctx, id, token, vi, "VehicleId", ct);
                    break;
                case "pd" when int.TryParse(v1, out int di):
                    await ReassignPickedAsync(ctx, id, token, di, "DriverId", ct);
                    break;
                case "dsp":
                    await DispatchAskAsync(ctx, id, token, ct);
                    break;
                case "dtn":
                    await DispatchNumberAsync(ctx, id, token, v1, ct);
                    break;
                case "dt" when v1 != null:
                    await DispatchAsync(ctx, id, token, v1 == "-" ? null : v1, ct);
                    break;
                case "dtx":
                    // Nút cũ (trước BOT-UX-2) – vẫn nhận gõ tay.
                    await AskAsync(ctx, "Nhập nhiệt độ thức ăn lúc xuất phát (°C), vd. 66.5:", ReplyDispatchTemp, id.ToString("N"), ct);
                    break;
                case "s":
                    await StopCardAsync(ctx, id, token, null, ct);
                    break;
                case "arr":
                    await StopPostAsync(ctx, id, token, "api/DeliveryStop/Arrive", [new("Id", id.ToString())], "Đã ghi đến điểm", ct);
                    break;
                case "full":
                    await DeliverFullAsync(ctx, id, token, ct);
                    break;
                case "dl" or "dqn":
                    await DeliverQtyAsync(ctx, id, token, v1, ct);
                    break;
                case "dq" when v1 != null:
                    await DeliverTempAsync(ctx, id, v1, ct);
                    break;
                case "dqx":
                    await AskAsync(ctx, "Nhập số suất giao thực tế:", ReplyQty, id.ToString("N"), ct);
                    break;
                case "dv" when v1 != null && v2 != null:
                    // BOT-UX-2: người nhận điền sẵn (đổi bằng 👤 Người nhận khác… trên thẻ điểm) – không hỏi gõ tên mỗi lần giao.
                    await DeliverAsync(ctx, id, token, v1, v2 == "-" ? null : v2, ct);
                    break;
                case "rcv":
                    await AskAsync(ctx, "👤 Tên người nhận hàng tại điểm này (gõ tên):", ReplyReceiverName, id.ToString("N"), ct);
                    break;
                case "fl":
                    await ShowAsync(ctx, new NeutralMessage
                    {
                        Title = "❌ Không giao được – lý do",
                        Buttons = [.. reasons.Buttons(ReasonCatalog.DeliveryFail, Kind, "fr", "flx", id.ToString("N")), .. Screens.Back("⬅️ Quay lại", CallbackData.Build(Kind, "s", id))],
                    }, ct);
                    break;
                case "fr" when reasons.At(ReasonCatalog.DeliveryFail, v1) is { } reason:
                    await StopPostAsync(ctx, id, token, "api/DeliveryStop/Fail", [new("Id", id.ToString()), new("Reason", reason + " (Telegram)")], "Đã ghi không giao được", ct);
                    break;
                case "flx":
                    await AskAsync(ctx, "❌ Lý do không giao được (gõ ngắn gọn):", ReplyFail, id.ToString("N"), ct);
                    break;
                case "bx" or "bxn":
                    await BoxesNumberAsync(ctx, id, token, v1, ct);
                    break;
                case "bxq" when InputWidgets.Value(v1) is { } boxes:
                    await RecordBoxesAsync(ctx, id, token, boxes, ct);
                    break;
                default:
                    await SayAsync(ctx, "Nút đã hết hạn.", ct);
                    break;
            }
        }

        /// <summary>Trả lời gõ tay: nhiệt độ xuất phát / số suất giao / tên người nhận / số hộp nhận về / lý do không giao được (nút cũ hoặc [✏️ Khác…]).</summary>
        public async Task ReplyAsync(BotActionContext ctx, PendingReply pending, CancellationToken ct)
        {
            string[] parts = pending.Key.Split('.');
            if (!Guid.TryParseExact(parts[0], "N", out Guid id) || await TokenAsync(ctx, ct) is not { } token)
            {
                return;
            }
            string text = (ctx.Event.Text ?? string.Empty).Trim();
            if (pending.Flow == ReplyDriver)
            {
                if (text.Length < 2)
                {
                    await AskAsync(ctx, "Gõ ít nhất 2 ký tự tên / mã tài xế:", pending.Flow, pending.Key, ct);
                    return;
                }
                await PickDriverAsync(ctx, id, token, text, ct);
                return;
            }
            if (pending.Flow is ReplyReceiverName or ReplyFail)
            {
                if (text.Length < 2)
                {
                    await AskAsync(ctx, "Quá ngắn – gõ lại:", pending.Flow, pending.Key, ct);
                    return;
                }
                string clean = text[..Math.Min(text.Length, 100)];
                if (pending.Flow == ReplyFail)
                {
                    await StopPostAsync(ctx, id, token, "api/DeliveryStop/Fail", [new("Id", id.ToString()), new("Reason", clean + " (Telegram)")], "Đã ghi không giao được", ct);
                    return;
                }
                _receivers[ReceiverKey(ctx, id)] = clean;
                await StopCardAsync(ctx, id, token, "👤 Người nhận: " + clean, ct);
                return;
            }
            if (pending.Flow == ReplyReceiver)
            {
                if (text.Length < 2 || parts.Length < 3)
                {
                    await AskAsync(ctx, "Tên người nhận quá ngắn – gõ lại:", pending.Flow, pending.Key, ct);
                    return;
                }
                _receivers[ReceiverKey(ctx, id)] = text[..Math.Min(text.Length, 100)];
                await DeliverAsync(ctx, id, token, parts[1], parts[2] == "-" ? null : parts[2], ct);
                return;
            }
            if (!decimal.TryParse(text.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var v) || v < 0)
            {
                await AskAsync(ctx, "Chưa đúng số – nhập lại:", pending.Flow, pending.Key, ct);
                return;
            }
            string num = v.ToString("0.##", CultureInfo.InvariantCulture);
            switch (pending.Flow)
            {
                case ReplyDispatchTemp:
                    await DispatchAsync(ctx, id, token, num, ct);
                    break;
                case ReplyQty:
                    // Suất là số nguyên (khóa callback tách bằng dấu chấm).
                    await DeliverTempAsync(ctx, id, ((int)Math.Round(v)).ToString(CultureInfo.InvariantCulture), ct);
                    break;
                case ReplyBoxes:
                    await RecordBoxesAsync(ctx, id, token, Math.Round(v), ct);
                    break;
            }
        }

        private static string ReceiverKey(BotActionContext ctx, Guid stopId) => ctx.Bot + ":" + stopId.ToString("N");

        /// <summary>BOT-UX-2 (mục 6): số hộp / khay nhận về – màn số, điền sẵn số phải thu của đơn giao (ContainerReturn/GetList hôm nay), không có thì 0.</summary>
        private async Task BoxesNumberAsync(BotActionContext ctx, Guid deliveryOrderId, string token, string? value, CancellationToken ct)
        {
            decimal expected = 0;
            string? who = null;
            var r = await Web.GetAsUserAsync<JsonElement>($"api/ContainerReturn/GetList?from={Today}&to={Today}&page=1&pageSize=100", token, ct);
            var item = Arr(r.Data, "listItem").FirstOrDefault(x => x.TryGetProperty("return", out var rt) && Guid.TryParse(S(rt, "deliveryOrderId"), out var g) && g == deliveryOrderId);
            if (item.ValueKind == JsonValueKind.Object)
            {
                expected = Dec(item.GetProperty("return"), "expectedQuantity");
                who = S(item, "customerName");
            }
            var m = new NeutralMessage { Title = "📥 Số hộp / khay nhận về" + (who != null ? " – " + who : string.Empty), Lines = expected > 0 ? [$"Phải thu {expected:0.##}"] : [] };
            InputWidgets.Number(m, Kind, "bxn", "bxq", deliveryOrderId.ToString("N"), InputWidgets.Value(value) ?? expected, "hộp", expected > 0 ? [expected] : [10m, 20m, 50m], 0, 9999, "✅ Ghi");
            await ShowAsync(ctx, m, ct);
        }

        private async Task RecordBoxesAsync(BotActionContext ctx, Guid deliveryOrderId, string token, decimal boxes, CancellationToken ct)
        {
            int n = (int)Math.Round(boxes);
            var r = await Web.PostFormAsUserAsync<JsonElement>("api/ContainerReturn/Record",
                [new("DeliveryOrderId", deliveryOrderId.ToString()), new("ReturnedQuantity", n.ToString(CultureInfo.InvariantCulture)), new("Note", "Telegram")], token, ct);
            CallbackAnswer.Set(r.IsOk ? $"✅ Đã ghi {n} hộp / khay về" : "⚠️ " + Problem(r));
            await SayAsync(ctx, r.IsOk ? $"✅ Đã ghi nhận {n} hộp / khay về." : "⚠️ " + Problem(r), ct);
        }

        // ---------- Chuyến ----------

        private async Task<JsonElement?> DetailAsync(BotActionContext ctx, Guid routeId, string token, CancellationToken ct)
        {
            var r = await Web.GetAsUserAsync<JsonElement>("api/DeliveryRoute/GetDetail?id=" + routeId, token, ct);
            if (!r.IsOk || r.Data.ValueKind != JsonValueKind.Object)
            {
                await SayAsync(ctx, Problem(r), ct);
                return null;
            }
            return r.Data;
        }

        private async Task RouteCardAsync(BotActionContext ctx, Guid routeId, string token, string? note, CancellationToken ct)
        {
            if (await DetailAsync(ctx, routeId, token, ct) is not { } d)
            {
                return;
            }
            var route = d.TryGetProperty("route", out var rt) ? rt : default;
            var stops = Arr(d, "listStop");
            var allowed = Strings(d, "allowedActions");
            // BOT-UX-2 (mục 2, 9): thẻ chuyến là 1 màn; dòng kết quả đầu thẻ.
            Screens.Show(CallbackData.Build(Kind, "r", routeId));
            var m = new NeutralMessage
            {
                Banner = note,
                Title = $"{RouteIcon(d)} {RouteNo(d)} – {S(d, "statusName") ?? S(route, "status")}",
                Lines =
                [
                    $"Xe {S(d, "licensePlate") ?? S(d, "vehicleCode") ?? "-"} · tài xế {S(d, "driverName") ?? "-"} · xuất phát {Time(S(route, "plannedStart"))}",
                    $"Suất: {N(d, "loadMeals")} · nhiệt độ xuất phát: {(S(d, "departureTemperatureC") is { } t ? Num(t) + " °C" : "chưa đo")} (tối thiểu {N(d, "deliveryTempMinC")} °C)",
                    .. stops.Select(StopLine),
                ],
            };
            foreach (string b in Strings(d, "listBlocker"))
            {
                m.Lines.Add("⛔ " + b);
            }
            if (allowed.Contains("dispatch"))
            {
                m.Buttons.Add(new NeutralButton("🚀 Xuất phát", CallbackData.Build(Kind, "dsp", routeId)));
            }
            // BOT-3.2 (D-72): đổi xe / tài xế khi chuyến chưa xuất phát – người điều phối (mục #/log-route; api kiểm quyền Sửa).
            if (S(route, "status") is "Planned" or "Loading" && (ctx.Identity.IsAdmin || ctx.Identity.Menus.Contains("#/log-route", StringComparer.OrdinalIgnoreCase)))
            {
                m.Buttons.Add(new NeutralButton("🔁 Đổi xe", CallbackData.Build(Kind, "rv", routeId)));
                m.Buttons.Add(new NeutralButton("👤 Đổi tài xế", CallbackData.Build(Kind, "rd", routeId)));
            }
            // Điểm còn việc (theo thứ tự chạy) – điểm kế tiếp đứng đầu.
            foreach (var s in stops.Where(s => Strings(s, "allowedActions").Count > 0).Take(8))
            {
                m.Buttons.Add(new NeutralButton($"📍 #{S(s, "sequenceNo")} {S(s, "customerName") ?? S(s, "locationName")}", CallbackData.Build(Kind, "s", Guid.Parse(S(s, "id")!))));
            }
            m.Buttons.Add(new NeutralButton("🔄 Làm mới", CallbackData.Build(Kind, "r", routeId)));
            await ShowAsync(ctx, m, ct);
        }

        private async Task DispatchAskAsync(BotActionContext ctx, Guid routeId, string token, CancellationToken ct)
        {
            if (await DetailAsync(ctx, routeId, token, ct) is not { } d)
            {
                return;
            }
            decimal min = MinTemp(d);
            var m = new NeutralMessage { Title = $"🚀 {RouteNo(d)} – nhiệt độ thức ăn lúc xuất phát", Lines = [$"Tối thiểu {min:0.##} °C. Bấm nhiệt độ vừa đo để ghi và xuất phát."] };
            // Đã đo đạt ngưỡng → cho xuất phát 1 chạm (không đo lại).
            if (decimal.TryParse(S(d, "departureTemperatureC"), NumberStyles.Number, CultureInfo.InvariantCulture, out var last) && last >= min)
            {
                m.Buttons.Add(new NeutralButton($"✅ Xuất phát (đã đo {last:0.##} °C)", CallbackData.Build(Kind, "dt", $"{routeId:N}.-")));
            }
            m.Buttons.AddRange(Temps.Where(t => t >= min).Select(t => new NeutralButton(t.ToString("0", CultureInfo.InvariantCulture) + "°",
                CallbackData.Build(Kind, "dt", $"{routeId:N}.{t.ToString("0", CultureInfo.InvariantCulture)}"), Row: "dlv-temp")));
            m.Buttons.Add(new NeutralButton("🌡 Nhiệt độ khác", CallbackData.Build(Kind, "dtn", routeId)));
            m.Buttons.AddRange(Screens.Back("⬅️ Quay lại", CallbackData.Build(Kind, "r", routeId)));
            await ShowAsync(ctx, m, ct);
        }

        /// <summary>BOT-UX-2 (mục 6): nhiệt độ xuất phát khác – màn số (điền sẵn lần đo trước, không có thì ngưỡng tối thiểu).</summary>
        private async Task DispatchNumberAsync(BotActionContext ctx, Guid routeId, string token, string? value, CancellationToken ct)
        {
            if (await DetailAsync(ctx, routeId, token, ct) is not { } d)
            {
                return;
            }
            decimal min = MinTemp(d);
            var m = new NeutralMessage { Title = $"🚀 {RouteNo(d)} – nhiệt độ xuất phát", Lines = [$"Tối thiểu {min:0.##} °C."] };
            InputWidgets.Number(m, Kind, "dtn", "dt", routeId.ToString("N"), InputWidgets.Value(value) ?? (Dec(d, "departureTemperatureC") is > 0m and var last ? last : min), "°C",
                Temps, 0, 150, "✅ Ghi & xuất phát");
            await ShowAsync(ctx, m, ct);
        }

        private async Task DispatchAsync(BotActionContext ctx, Guid routeId, string token, string? temperature, CancellationToken ct)
        {
            var form = new List<KeyValuePair<string, string>> { new("Id", routeId.ToString()), new("Note", "Telegram") };
            if (temperature != null)
            {
                form.Add(new("TemperatureC", temperature));
            }
            var r = await Web.PostFormAsUserAsync<JsonElement>("api/DeliveryRoute/Dispatch", form, token, ct);
            CallbackAnswer.Set(r.IsOk ? "✅ Đã xuất phát" + (temperature != null ? $" · {temperature} °C" : "") : "⚠️ " + Problem(r));
            await RouteCardAsync(ctx, routeId, token, r.IsOk ? InputWidgets.Done(r.ResponseMessage ?? "Đã xuất phát", time) : "⚠️ " + Problem(r), ct);
        }

        // ---------- Điểm dừng ----------

        private async Task<(Guid RouteId, JsonElement Stop, JsonElement Route)?> StopAsync(BotActionContext ctx, Guid stopId, string token, CancellationToken ct)
        {
            var s = await Web.GetAsUserAsync<JsonElement>("api/DeliveryStop/GetById?id=" + stopId, token, ct);
            if (!s.IsOk || !Guid.TryParse(S(s.Data, "deliveryRouteId"), out Guid routeId))
            {
                await SayAsync(ctx, s.IsOk ? "Không tìm thấy điểm dừng." : Problem(s), ct);
                return null;
            }
            if (await DetailAsync(ctx, routeId, token, ct) is not { } d)
            {
                return null;
            }
            var stop = Arr(d, "listStop").FirstOrDefault(x => Guid.TryParse(S(x, "id"), out var g) && g == stopId);
            if (stop.ValueKind != JsonValueKind.Object)
            {
                await SayAsync(ctx, "Không tìm thấy điểm dừng.", ct);
                return null;
            }
            return (routeId, stop, d);
        }

        /// <summary>Số suất điền sẵn: đã xếp lên xe (nếu có), không thì kế hoạch.</summary>
        private static decimal KnownQty(JsonElement stop) => Dec(stop, "loadedQty") is > 0m and var loaded ? loaded : Dec(stop, "plannedMealQty");

        /// <summary>
        /// Nhiệt độ bàn giao điền sẵn: đã đo tại điểm (api tự dùng – không gửi lại), không có thì lần đo lúc xuất phát của chuyến (gửi kèm, hiện rõ trên nút).
        /// null = chưa có lần đo nào đạt ngưỡng → phải chọn nhiệt độ.
        /// </summary>
        private static (decimal Value, bool AtStop)? KnownTemp(JsonElement stop, JsonElement route)
        {
            decimal min = MinTemp(route);
            if (Dec(stop, "handoverTemperatureC") is > 0m and var atStop && atStop >= min)
            {
                return (atStop, true);
            }
            return Dec(route, "departureTemperatureC") is > 0m and var dep && dep >= min ? (Math.Round(dep, MidpointRounding.AwayFromZero), false) : null;
        }

        private static decimal MinTemp(JsonElement route) => Dec(route, "deliveryTempMinC") is > 0m and var m ? m : 60m;

        /// <summary>Người nhận điền sẵn: người nhận gần nhất tại điểm (bộ nhớ) hoặc "Đại diện &lt;khách&gt;".</summary>
        private string Receiver(BotActionContext ctx, Guid stopId, JsonElement stop)
        {
            if (_receivers.TryGetValue(ReceiverKey(ctx, stopId), out var name))
            {
                return name;
            }
            string who = "Đại diện " + (S(stop, "customerName") ?? S(stop, "locationName") ?? "khách");
            return who[..Math.Min(who.Length, 100)];
        }

        private async Task StopCardAsync(BotActionContext ctx, Guid stopId, string token, string? note, CancellationToken ct)
        {
            if (await StopAsync(ctx, stopId, token, ct) is not { } x)
            {
                if (note != null)
                {
                    await SayAsync(ctx, note, ct);
                }
                return;
            }
            var s = x.Stop;
            var allowed = Strings(s, "allowedActions");
            Screens.Show(CallbackData.Build(Kind, "s", stopId));
            var m = new NeutralMessage
            {
                Banner = note,
                Title = $"{StopIcon(s)} #{S(s, "sequenceNo")} {S(s, "customerName")} – {S(s, "statusName")}",
                Lines =
                [
                    $"{S(s, "locationName")}{(S(s, "address") is { } a ? " · " + a : "")}",
                    $"Giờ đến dự kiến {Time(S(s, "plannedArrival"))}{(S(s, "actualArrival") is { } aa ? " · đã đến " + Time(aa) : "")}",
                    $"Suất: kế hoạch {N(s, "plannedMealQty")}{(S(s, "loadedQty") is { } l ? " · đã xếp " + Num(l) : "")}{(S(s, "deliveredMealQty") is { } dq ? " · đã giao " + Num(dq) : "")}",
                ],
            };
            if (S(s, "handoverTemperatureC") is { } ht)
            {
                m.Lines.Add($"Nhiệt độ bàn giao: {Num(ht)} °C");
            }
            string n = stopId.ToString("N");
            if (allowed.Contains("deliver"))
            {
                // BOT-UX-2: ✅ Giao đủ N 1 chạm – số suất, nhiệt độ, người nhận điền sẵn và hiện rõ trước khi bấm.
                decimal qty = KnownQty(s);
                var temp = KnownTemp(s, x.Route);
                string receiver = Receiver(ctx, stopId, s);
                m.Lines.Add($"Bàn giao điền sẵn: {qty:0.##} suất · {(temp is { } t ? $"{t.Value:0.##} °C ({(t.AtStop ? "đo tại điểm" : "lần đo lúc xuất phát")})" : "chưa đo nhiệt độ")} · người nhận: {receiver}");
                if (qty > 0)
                {
                    m.Buttons.Add(new NeutralButton($"✅ Giao đủ {qty:0.##}" + (temp is { } tt ? $" · {tt.Value:0.##}°" : string.Empty), CallbackData.Build(Kind, "full", n)));
                }
                m.Buttons.Add(new NeutralButton("✏️ Giao số khác", CallbackData.Build(Kind, "dl", n)));
            }
            if (allowed.Contains("arrive"))
            {
                m.Buttons.Add(new NeutralButton("📍 Đã đến", CallbackData.Build(Kind, "arr", n)));
            }
            if (allowed.Contains("fail"))
            {
                m.Buttons.Add(new NeutralButton("❌ Không giao được", CallbackData.Build(Kind, "fl", n)));
            }
            if (allowed.Contains("deliver"))
            {
                m.Buttons.Add(new NeutralButton("👤 Người nhận khác…", CallbackData.Build(Kind, "rcv", n)));
            }
            m.Buttons.AddRange(Screens.Back("⬅️ Chuyến", CallbackData.Build(Kind, "r", x.RouteId)));
            await ShowAsync(ctx, m, ct);
        }

        private async Task StopPostAsync(BotActionContext ctx, Guid stopId, string token, string path, List<KeyValuePair<string, string>> form, string done, CancellationToken ct)
        {
            var r = await Web.PostFormAsUserAsync<JsonElement>(path, form, token, ct);
            CallbackAnswer.Set(r.IsOk ? "✅ " + done : "⚠️ " + Problem(r));
            await StopCardAsync(ctx, stopId, token, r.IsOk ? InputWidgets.Done(r.ResponseMessage ?? done, time) : "⚠️ " + Problem(r), ct);
        }

        /// <summary>BOT-UX-2: [✅ Giao đủ N] – giao đúng số điền sẵn; chưa có lần đo nhiệt độ nào đạt ngưỡng → chọn nhiệt độ bàn giao (1 chạm nữa).</summary>
        private async Task DeliverFullAsync(BotActionContext ctx, Guid stopId, string token, CancellationToken ct)
        {
            if (await StopAsync(ctx, stopId, token, ct) is not { } x)
            {
                return;
            }
            string qty = KnownQty(x.Stop).ToString("0", CultureInfo.InvariantCulture);
            if (KnownTemp(x.Stop, x.Route) is not { } temp)
            {
                await DeliverTempAsync(ctx, stopId, qty, ct);
                return;
            }
            await DeliverAsync(ctx, stopId, token, qty, temp.AtStop ? null : temp.Value.ToString("0.##", CultureInfo.InvariantCulture), ct, x.Stop);
        }

        /// <summary>Ghi bàn giao (DeliveryStop/Deliver): số suất, nhiệt độ (null = dùng lần đo tại điểm), người nhận điền sẵn.</summary>
        private async Task DeliverAsync(BotActionContext ctx, Guid stopId, string token, string qty, string? temperature, CancellationToken ct, JsonElement? stop = null)
        {
            if (stop == null)
            {
                if (await StopAsync(ctx, stopId, token, ct) is not { } x)
                {
                    return;
                }
                stop = x.Stop;
            }
            string receiver = Receiver(ctx, stopId, stop.Value);
            var form = new List<KeyValuePair<string, string>> { new("Id", stopId.ToString()), new("DeliveredQuantity", qty), new("ReceiverName", receiver), new("Remark", "Telegram") };
            if (temperature != null)
            {
                form.Add(new("TemperatureC", temperature));
            }
            await StopPostAsync(ctx, stopId, token, "api/DeliveryStop/Deliver", form, $"Đã giao {qty} suất – {receiver}", ct);
        }

        /// <summary>BOT-UX-2 (mục 6): giao số khác (thiếu / thừa) – màn số điền sẵn số đã xếp / kế hoạch → [✅ Xác nhận] → chọn nhiệt độ bàn giao.</summary>
        private async Task DeliverQtyAsync(BotActionContext ctx, Guid stopId, string token, string? value, CancellationToken ct)
        {
            if (await StopAsync(ctx, stopId, token, ct) is not { } x)
            {
                return;
            }
            decimal planned = Dec(x.Stop, "plannedMealQty"), loaded = Dec(x.Stop, "loadedQty");
            decimal known = KnownQty(x.Stop);
            var m = new NeutralMessage
            {
                Title = $"✅ Giao – #{S(x.Stop, "sequenceNo")} {S(x.Stop, "customerName")}",
                Lines = [$"Số suất giao thực tế (kế hoạch {planned:0.##}{(loaded > 0 ? $" · đã xếp {loaded:0.##}" : "")}):"],
            };
            InputWidgets.Number(m, Kind, "dqn", "dq", stopId.ToString("N"), InputWidgets.Value(value) ?? known, "suất",
                [known, planned, Math.Floor(known * 0.98m), Math.Floor(known * 0.95m)], 1);
            m.Buttons.AddRange(Screens.Back("⬅️ Quay lại", CallbackData.Build(Kind, "s", stopId)));
            await ShowAsync(ctx, m, ct);
        }

        private Task DeliverTempAsync(BotActionContext ctx, Guid stopId, string qty, CancellationToken ct)
            => ShowAsync(ctx, new NeutralMessage
            {
                Title = $"🌡 Nhiệt độ bàn giao – {qty} suất",
                Lines = ["Bấm nhiệt độ đo lúc bàn giao để ghi (thấp hơn ngưỡng → ghi trên web kèm hành động khắc phục)."],
                Buttons = [.. Temps.Select(t => new NeutralButton(t.ToString("0", CultureInfo.InvariantCulture) + "°", CallbackData.Build(Kind, "dv", $"{stopId:N}.{qty}.{t.ToString("0", CultureInfo.InvariantCulture)}"), Row: "dlv-temp")),
                    new NeutralButton("Không đo", CallbackData.Build(Kind, "dv", $"{stopId:N}.{qty}.-")), .. Screens.Back("⬅️ Quay lại", CallbackData.Build(Kind, "s", stopId))],
            }, ct);

        // ---------- Hiển thị ----------

        private static string RouteLine(JsonElement r)
        {
            var stops = Arr(r, "listStop");
            int done = stops.Count(s => S(s, "stopStatus") is "Delivered" or "Failed");
            return $"{RouteIcon(r)} {RouteNo(r)} · {S(r, "licensePlate") ?? S(r, "vehicleCode") ?? "-"} · {Time(S(Route(r), "plannedStart"))} · {S(r, "statusName")} · {done}/{stops.Count} điểm";
        }

        private static string StopLine(JsonElement s)
            => $"{StopIcon(s)} #{S(s, "sequenceNo")} {S(s, "customerName") ?? S(s, "locationName")} · {Time(S(s, "plannedArrival"))} · {N(s, "plannedMealQty")} suất";

        private static string RouteIcon(JsonElement r) => S(Route(r), "status") switch
        {
            "Planned" => "⏳",
            "Loading" => "📦",
            "InTransit" => "🚚",
            "Completed" => "✅",
            _ => "•",
        };

        private static string StopIcon(JsonElement s) => S(s, "stopStatus") switch
        {
            "Arrived" => "📍",
            "Delivered" => "✅",
            "Failed" => "❌",
            _ => "⏳",
        };

        private static JsonElement Route(JsonElement r) => r.ValueKind == JsonValueKind.Object && r.TryGetProperty("route", out var x) ? x : default;

        private static string RouteNo(JsonElement r) => S(Route(r), "routeNo") ?? S(Route(r), "code") ?? "Chuyến";

        private static Guid RouteId(JsonElement r) => Guid.TryParse(S(Route(r), "id"), out var g) ? g : Guid.Empty;

        public static List<JsonElement> Arr(JsonElement e, string k)
            => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Array ? [.. v.EnumerateArray()] : [];

        private static List<string> Strings(JsonElement e, string k) => [.. Arr(e, k).Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!)];

        private static string? S(JsonElement e, string k) => BepActions.S(e, k);

        private static decimal Dec(JsonElement e, string k) => decimal.TryParse(S(e, k), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : 0m;

        private static string N(JsonElement e, string k) => Dec(e, k).ToString("0.##", CultureInfo.InvariantCulture);

        private static string Num(string s) => decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d.ToString("0.##", CultureInfo.InvariantCulture) : s;

        private static string Time(string? s) => DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t.ToString("HH:mm") : "--:--";
    }
}
