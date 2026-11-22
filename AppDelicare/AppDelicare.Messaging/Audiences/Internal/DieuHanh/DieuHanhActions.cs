using System.Globalization;
using System.Text.Json;
using AppDelicare.Messaging.Audiences.Internal.VanHanh;
using AppDelicare.Messaging.Channels;
using AppDelicare.Messaging.Conversations;
using AppDelicare.Messaging.Core;
using AppDelicare.Messaging.Presentation;

namespace AppDelicare.Messaging.Audiences.Internal.DieuHanh
{
    /// <summary>
    /// BOT-1.6 (BOT_TELEGRAM §3.3 – bot Điều hành DH, quyền vào bot = <c>#/bot-dieu-hanh</c>): 📊 Tổng quan hôm nay (<c>Dashboard/GetExecutive</c>), 🚨 Việc đang mở
    /// (chuông cảnh báo <c>Common/GetAlerts</c> – chỉ màn người dùng có quyền xem), 🎯 KPI 7 ngày (<c>KpiSnapshot/GetDaily</c>), 📅 KPI tháng (<c>GetMonthly</c>),
    /// 📈 Tiến độ bếp, 🗺 Giao vận trực tiếp, 📦 Tồn &amp; thiếu hụt (dùng lại chức năng bot VH), 🔍 Truy xuất (gõ mã chứng từ – CodeSearch). ⏳ Chờ tôi duyệt + phần chung
    /// đã có ở mọi bot (ChungActionProvider). Chỉ xem; ghi (duyệt) qua ⏳. Nút mở bot DH từ bot VH: OnboardingActions (BOT-1.2).
    /// </summary>
    public sealed class DieuHanhActions(IDelicareApiClient web, UserTokenCache tokens, IChannelSender sender, ReplyContextStore replies, TimeProvider time,
        BepActions bep, GiaoVanActions giaoVan, XemNhanhActions xem) : WebActions(web, tokens, sender, replies), IBotActionProvider
    {
        private static readonly string[] Bots = ["DH"];

        public IEnumerable<BotAction> GetActions() =>
        [
            A("dh_overview", "📊 Tổng quan hôm nay", "tongquan", "Tổng quan vận hành hôm nay", "#/dash-exec", 1, OverviewAsync),
            A("dh_open", "🚨 Việc đang mở", "viecmo", "Cảnh báo / việc đang mở", null, 2, (c, ct) => xem.AlertsAsync(c, "🚨 Việc đang mở", null, ct)),
            A("dh_kpi", "🎯 KPI 7 ngày", "kpi", "Chỉ tiêu KPI 7 ngày", "#/kpi-daily", 3, KpiDailyAsync),
            A("dh_kpi_month", "📅 Chỉ số tháng", "kpithang", "Chỉ số tháng này", "#/kpi-monthly", 4, KpiMonthlyAsync),
            A("dh_kitchen", "📈 Tiến độ bếp", "tiendobep", "Tiến độ sản xuất hôm nay", BepActions.Menu, 5, bep.ProgressAsync),
            A("dh_delivery", "🗺 Giao vận trực tiếp", "giaovantt", "Chuyến giao hôm nay", "#/log-route", 6, giaoVan.BoardAsync),
            A("dh_stock", "📦 Tồn & thiếu hụt", "tonthieu", "Tồn dưới mức + thiếu hụt ngày mai", "#/scm-inv-balance", 7, StockAsync),
            A("dh_trace", "🔍 Truy xuất", "truyxuat", "Tra mã chứng từ", null, 8, (c, ct) => SayAsync(c, "🔍 Gõ mã để tra (vd. DH-261118-…, ME-261118-…, LO-…, PO-…, CX-…, khách KHB-… – số cũ PH-… / NL-… vẫn tra được) – hoặc gõ tên để tìm.", ct)),
        ];

        private static BotAction A(string code, string label, string command, string description, string? menu, int order, Func<BotActionContext, CancellationToken, Task> handler)
            => new() { Code = code, Module = "DIEUHANH", Label = label, Command = command, CommandDescription = description, Bots = Bots, MenuMetaLink = menu, Order = order, Handler = handler };

        private async Task<JsonElement?> GetAsync(BotActionContext ctx, string path, CancellationToken ct)
        {
            if (await TokenAsync(ctx, ct) is not { } token)
            {
                return null;
            }
            var r = await Web.GetAsUserAsync<JsonElement>(path, token, ct);
            if (!r.IsOk || r.Data.ValueKind != JsonValueKind.Object)
            {
                await SayAsync(ctx, Problem(r), ct);
                return null;
            }
            return r.Data;
        }

        public async Task OverviewAsync(BotActionContext ctx, CancellationToken ct)
        {
            DateTime today = VnTime.Today(time);
            if (await GetAsync(ctx, "api/Dashboard/GetExecutive?date=" + today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), ct) is not { } d)
            {
                return;
            }
            var k = Obj(d, "kpi");
            var prod = Obj(k, "productionProgress");
            var dlv = Obj(k, "delivery");
            var staff = Obj(k, "staffing");
            var qc = Obj(k, "qcDetail");
            var lines = new List<string>
            {
                $"🍱 Suất kế hoạch: {N(k, "plannedMeals")} · đơn {N(Obj(k, "orders"), "total")}",
                $"🍳 Sản xuất: {N(prod, "donePortions")} / {N(prod, "plannedPortions")} suất ({N(prod, "percent")}%) · {N(prod, "batchCount")} mẻ",
                $"🚚 Giao: {N(dlv, "delivered")} / {N(dlv, "total")} điểm",
                $"👥 Vào ca: {N(staff, "checkedIn")} / {N(staff, "assigned")}",
                $"🧪 QC giữ / loại: {N(qc, "batchHold")} / {N(qc, "batchReject")} mẻ · lô giữ {N(qc, "lotOnHold")}",
                $"📦 Cảnh báo tồn: {N(k, "stockAlerts")} · 📣 phản ánh mở: {N(k, "openComplaints")}",
            };
            foreach (var s in GiaoVanActions.Arr(d, "shiftProgress").Take(4))
            {
                lines.Add($"• {S(s, "name") ?? S(s, "code")}: {Summary(s)}");
            }
            var alerts = GiaoVanActions.Arr(d, "alerts").Take(5).Select(a => $"{(S(a, "severity") is "High" or "Critical" ? "🔴" : "🟡")} {S(a, "title")}: {S(a, "count")}").ToList();
            if (alerts.Count > 0)
            {
                lines.Add("Cảnh báo:");
                lines.AddRange(alerts);
            }
            await SendAsync(ctx, new NeutralMessage { Title = $"📊 Tổng quan {today:dd/MM} – {VnTime.Now(time):HH:mm}", Lines = lines }, ct);
        }

        /// <summary>Tiến độ 1 ca: các số trong đối tượng (trừ id / mã / tên) – không phụ thuộc tên trường cụ thể.</summary>
        private static string Summary(JsonElement s)
            => string.Join(" · ", s.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.Number && p.Name is not ("shiftId" or "code" or "name"))
                .Take(4).Select(p => $"{Label(p.Name)} {p.Value}"));

        private static string Label(string name) => name switch
        {
            "plannedPortions" => "KH",
            "donePortions" => "xong",
            "percent" => "%",
            "batchCount" => "mẻ",
            "assigned" => "phân ca",
            "checkedIn" => "vào ca",
            _ => name,
        };

        public async Task KpiDailyAsync(BotActionContext ctx, CancellationToken ct)
        {
            DateTime today = VnTime.Today(time);
            if (await GetAsync(ctx, $"api/KpiSnapshot/GetDaily?From={today.AddDays(-7):yyyy-MM-dd}&To={today:yyyy-MM-dd}", ct) is { } d)
            {
                await KpiAsync(ctx, "🎯 KPI 7 ngày", d, ct);
            }
        }

        public async Task KpiMonthlyAsync(BotActionContext ctx, CancellationToken ct)
        {
            string month = VnTime.Today(time).ToString("yyyy-MM", CultureInfo.InvariantCulture);
            if (await GetAsync(ctx, $"api/KpiSnapshot/GetMonthly?FromMonth={month}&ToMonth={month}", ct) is { } d)
            {
                await KpiAsync(ctx, "📅 Chỉ số tháng " + month, d, ct);
            }
        }

        private Task KpiAsync(BotActionContext ctx, string title, JsonElement d, CancellationToken ct)
        {
            var metrics = GiaoVanActions.Arr(d, "metrics");
            var lines = metrics.Select(m =>
            {
                var def = Obj(m, "definition");
                var latest = Obj(m, "latest");
                string icon = latest.ValueKind != JsonValueKind.Object || S(latest, "value") == null ? "⚪" : S(latest, "met") switch { "True" or "true" => "✅", "False" or "false" => "⚠️", _ => "•" };
                string value = S(latest, "value") is { } v ? Num(v) : "chưa có";
                string? targetValue = S(latest, "target") ?? S(def, "targetValue");
                string target = targetValue != null ? $" / mục tiêu {Num(targetValue)}" : "";
                return $"{icon} {S(def, "metricName") ?? S(def, "metricCode")}: {value}{target}{(S(def, "unitCode") is { } u ? " " + u : "")}";
            }).ToList();
            return SendAsync(ctx, new NeutralMessage
            {
                Title = $"{title} ({metrics.Count})",
                Lines = lines.Count == 0 ? ["Chưa có số liệu KPI (job KPI chưa chạy hoặc chưa có chỉ tiêu)."] : [.. lines.Take(XemNhanhActions.MaxLines)],
            }, ct);
        }

        public async Task StockAsync(BotActionContext ctx, CancellationToken ct)
        {
            await xem.LowStockAsync(ctx, ct);
            await xem.RequirementAsync(ctx, ct);
        }

        private static JsonElement Obj(JsonElement e, string k) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(k, out var v) ? v : default;

        private static string? S(JsonElement e, string k) => BepActions.S(e, k);

        private static string Num(string s) => decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d.ToString("#,0.##", CultureInfo.InvariantCulture) : s;

        private static string N(JsonElement e, string k) => S(e, k) is { } v ? Num(v) : "0";
    }
}
