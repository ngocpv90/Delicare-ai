using System.Globalization;
using System.Text.Json;
using AppDelicare.Messaging.Callback;
using AppDelicare.Messaging.Core;
using AppDelicare.Messaging.Presentation;

namespace AppDelicare.Messaging.Audiences.Internal.VanHanh
{
    /// <summary>
    /// UI-6.50 (D-132 h): nhận hàng theo NCC trên bot Vận hành – 📦 Nhận hàng theo NCC (thẻ NCC hẹn giao hôm nay, <c>PurchaseOrder/GetDueBySupplier</c>) hoặc QR in trên đơn mua
    /// <c>start=DOC_PO_&lt;Id&gt;</c> → gói nhận gộp mọi đơn còn mở của NCC (<c>GetReceivingBundle</c>) → [✓ Nhận đủ – Đạt tất cả] (hỏi nhiệt độ thùng xe nếu có hàng mát / đông)
    /// → <c>GoodsReceipt/CreateMany</c> bằng JWT người bấm (quyền Thêm "Nhận hàng"). Lệch số / lô / không đạt → sửa trên web Kho › Nhận hàng. Không camera trong web.
    /// Callback <c>op:rcs:&lt;S|P&gt;&lt;Id N&gt;</c> (xem gói), <c>op:rca:&lt;S|P&gt;&lt;Id N&gt;</c> (nhận đủ).
    /// </summary>
    public sealed partial class ThaoTacActions
    {
        /// <summary>Nhiệt độ thùng xe (°C) trước khi "Nhận đủ" – khóa = &lt;S|P&gt;&lt;Id N&gt;.</summary>
        public const string ReplyVehicleTemp = "op-rcv-temp";

        private static string? Js(JsonElement e, string k) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(k, out var v) && v.ValueKind != JsonValueKind.Null ? v.ToString() : null;
        private static decimal Jd(JsonElement e, string k) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : 0m;
        private static bool Jb(JsonElement e, string k) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.True;
        private static List<JsonElement> Ja(JsonElement e, string k) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Array ? [.. v.EnumerateArray()] : [];
        private static string Qty(decimal v) => v.ToString("#,##0.##", CultureInfo.GetCultureInfo("vi-VN"));

        private static string BundleUrl(string key) => key.Length == 33 && Guid.TryParseExact(key[1..], "N", out var id)
            ? (key[0] == 'P' ? "api/PurchaseOrder/GetReceivingBundle?purchaseOrderId=" : "api/PurchaseOrder/GetReceivingBundle?supplierId=") + id
            : string.Empty;

        /// <summary>📦 Nhận hàng theo NCC: thẻ NCC hẹn giao hôm nay (quá hẹn lên đầu).</summary>
        public async Task ReceiveListAsync(BotActionContext ctx, CancellationToken ct)
        {
            if (await TokenAsync(ctx, ct) is not { } token)
            {
                return;
            }
            var r = await Web.GetAsUserAsync<JsonElement>("api/PurchaseOrder/GetDueBySupplier", token, ct);
            if (!r.IsOk)
            {
                await SayAsync(ctx, "⚠️ " + Problem(r), ct);
                return;
            }
            var cards = Ja(r.Data, "listSupplier").Where(c => !Jb(c, "receivedFull")).Take(8).ToList();
            var m = new NeutralMessage
            {
                Title = $"📦 Nhận hàng hôm nay {Today:dd/MM}",
                Lines = cards.Count == 0 ? ["Không có NCC nào hẹn giao / quá hẹn chưa nhận.", "NCC mang đơn mua in theo → quét QR trên phiếu bằng camera điện thoại."]
                    : [$"{Jd(r.Data, "supplierCount"):0} NCC · {Jd(r.Data, "orderCount"):0} đơn · {Jd(r.Data, "lineCount"):0} dòng · quá hẹn {Jd(r.Data, "overdueCount"):0} · chờ QA {Jd(r.Data, "qcPendingCount"):0}", "Bấm NCC đang giao:"],
            };
            foreach (var c in cards)
            {
                string late = Js(c, "overdueMinutes") is { } mins ? $" · ⏰ quá {mins}′" : string.Empty;
                m.Buttons.Add(new NeutralButton($"📦 {Js(c, "supplierName")} · {Jd(c, "orderCount"):0} đơn{late}", CallbackData.Build(Kind, "rcs", "S" + Guid.Parse(Js(c, "supplierId")!).ToString("N"))));
            }
            await ShowAsync(ctx, m, ct);
        }

        /// <summary>Gói nhận gộp của NCC (key S…) hoặc theo đơn mua quét QR (key P…). Trả false khi không xem được (không quyền / không còn đơn mở) để QR hiện thông tin đơn.</summary>
        public async Task<bool> ReceiveBundleAsync(BotActionContext ctx, string key, CancellationToken ct, bool quiet = false)
        {
            if (await TokenAsync(ctx, ct) is not { } token)
            {
                return true;
            }
            var r = await Web.GetAsUserAsync<JsonElement>(BundleUrl(key), token, ct);
            if (!r.IsOk || r.Data.ValueKind != JsonValueKind.Object)
            {
                if (!quiet)
                {
                    await SayAsync(ctx, "⚠️ " + Problem(r), ct);
                }
                else if (r.ResponseCode is not (401 or 403))
                {
                    await SayAsync(ctx, "ℹ️ " + Problem(r), ct);
                }
                return false;
            }
            var d = r.Data;
            var orders = Ja(d, "listOrder").Where(o => Jb(o, "due") || Jb(o, "matched")).ToList();
            var ids = orders.Select(o => Js(o, "purchaseOrderId")).ToHashSet();
            var lines = Ja(d, "listLine").Select(l => (Line: l, Parts: Ja(l, "listPart").Where(p => ids.Contains(Js(p, "purchaseOrderId"))).ToList())).Where(x => x.Parts.Count > 0).ToList();
            var m = new NeutralMessage
            {
                Title = $"📦 Nhận hàng – {Js(d, "supplierName")}",
                Lines = [$"Gộp {orders.Count} đơn: {string.Join(", ", orders.Select(o => Js(o, "purchaseOrderNumber")))} · {lines.Count} nguyên liệu"],
            };
            foreach (var (l, parts) in lines.Take(15))
            {
                bool lot = Jb(l, "lotControl") || Jb(l, "expiryControl");
                m.Lines.Add($"• {Js(l, "ingredientName")}: {Qty(parts.Sum(p => Jd(p, "remaining")))} {Js(l, "unitName")}" + (parts.Count > 1 ? $" ({parts.Count} đơn)" : string.Empty) + (lot ? " · lô / hạn: tự sinh – sửa trên web nếu cần" : string.Empty));
            }
            if (lines.Count > 15)
            {
                m.Lines.Add($"… và {lines.Count - 15} nguyên liệu nữa.");
            }
            m.Lines.Add("Nhận đủ: mọi dòng = số còn phải nhận, kết quả Đạt; tự đạt nhập kho ngay, cần QA gửi QA. Lệch số / lô / không đạt → web Kho › Nhận hàng.");
            m.Buttons.Add(new NeutralButton("✓ Nhận đủ – Đạt tất cả", CallbackData.Build(Kind, "rca", key)));
            await ShowAsync(ctx, m, ct);
            return true;
        }

        private async Task ReceiveAllAskAsync(BotActionContext ctx, string key, CancellationToken ct)
        {
            if (await TokenAsync(ctx, ct) is not { } token)
            {
                return;
            }
            var r = await Web.GetAsUserAsync<JsonElement>(BundleUrl(key), token, ct);
            if (!r.IsOk)
            {
                await SayAsync(ctx, "⚠️ " + Problem(r), ct);
                return;
            }
            if (Jb(r.Data, "needsVehicleTemperature"))
            {
                await AskAsync(ctx, "🌡 Nhiệt độ thùng xe (°C) – áp cho mọi dòng hàng mát / đông lạnh:", ReplyVehicleTemp, key, ct);
                return;
            }
            await ReceiveAllAsync(ctx, key, null, ct);
        }

        private async Task ReceiveAllAsync(BotActionContext ctx, string key, decimal? vehicleTemp, CancellationToken ct)
        {
            if (await TokenAsync(ctx, ct) is not { } token)
            {
                return;
            }
            var r = await Web.GetAsUserAsync<JsonElement>(BundleUrl(key), token, ct);
            if (!r.IsOk)
            {
                await SayAsync(ctx, "⚠️ " + Problem(r), ct);
                return;
            }
            var d = r.Data;
            var ids = Ja(d, "listOrder").Where(o => Jb(o, "due") || Jb(o, "matched")).Select(o => Js(o, "purchaseOrderId")).ToHashSet();
            var lines = Ja(d, "listLine").Select(l => new
            {
                ingredientId = Guid.Parse(Js(l, "ingredientId")!),
                parts = Ja(l, "listPart").Where(p => ids.Contains(Js(p, "purchaseOrderId"))).ToList(),
            }).Where(x => x.parts.Count > 0).Select(x => new
            {
                x.ingredientId, listPurchaseOrderLineId = x.parts.Select(p => Guid.Parse(Js(p, "purchaseOrderLineId")!)).ToList(), receivedQuantity = x.parts.Sum(p => Jd(p, "remaining")), result = "DAT",
            }).ToList();
            Guid? wh = Js(d, "defaultWarehouseId") is { } w ? Guid.Parse(w) : null;
            if (wh == null)
            {
                var ws = await Web.GetAsUserAsync<JsonElement>("api/Warehouse/GetAllData", token, ct);
                var first = (ws.ListData ?? []).FirstOrDefault(x => Js(x, "status") is null or "Active");
                wh = Js(first, "id") is { } fid ? Guid.Parse(fid) : null;
            }
            if (wh == null)
            {
                await SayAsync(ctx, "⚠️ Chưa có kho nhận – nhận hàng trên web Kho › Nhận hàng.", ct);
                return;
            }
            var res = await Web.PostJsonAsUserAsync<JsonElement>("api/GoodsReceipt/CreateMany", new { warehouseId = wh, vehicleTemperatureC = vehicleTemp, listLine = lines }, token, ct);
            if (!res.IsOk)
            {
                await SayAsync(ctx, "⚠️ " + Problem(res), ct);
                return;
            }
            var warn = Ja(res.Data, "listWarning").Select(x => x.GetString()).Where(x => x != null).Take(5).ToList();
            await SayAsync(ctx, "✅ " + (res.ResponseMessage ?? "Đã nhận hàng") + (warn.Count > 0 ? "\n⚠️ " + string.Join("\n⚠️ ", warn) : string.Empty), ct);
        }
    }
}
