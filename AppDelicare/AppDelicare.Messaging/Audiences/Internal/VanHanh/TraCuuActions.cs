using System.Text.Json;
using AppDelicare.Messaging.Channels;
using AppDelicare.Messaging.Conversations;
using AppDelicare.Messaging.Core;
using AppDelicare.Messaging.Presentation;

namespace AppDelicare.Messaging.Audiences.Internal.VanHanh
{
    /// <summary>
    /// BOT-3.1 (UG-1 C-22, anh chọn 2026-11-19): 🔍 Truy xuất lô / mẻ cho Bếp (OPS-KIT), QA và Kho. Hỏi số lô (LO-… / số lô cũ NL-…) hoặc số mẻ (ME-…)
    /// → <c>Common/GlobalSearch</c> (lọc theo quyền) → <c>MaterialLot/Trace</c> (xuôi: lô → phiếu xuất → lệnh → mẻ → đơn / khách) hoặc <c>ProductionBatch/Trace</c>
    /// (ngược: mẻ → lô → phiếu nhập → NCC). JWT của chính người dùng; chỉ xem.
    /// </summary>
    public sealed class TraCuuActions(IDelicareApiClient web, UserTokenCache tokens, IChannelSender sender, ReplyContextStore replies)
        : WebActions(web, tokens, sender, replies), IBotActionProvider
    {
        public const string ReplyTrace = "trace-code";
        private const int MaxPerGroup = 4;

        public IEnumerable<BotAction> GetActions() =>
        [
            // Lệnh gõ phải duy nhất toàn bot (/truyxuat là của bot DH) → /truyxuatlo.
            // BOT-UX-2 (mục 14): mỗi chức năng 1 phân hệ chính – 🔍 Truy xuất chỉ ở ATTP / QC (trước có thêm bản ở Kho và Bếp); ai có quyền xem lô (#/scm-lot)
            // hoặc mẻ (#/prod-batch) đều thấy, mở nhanh qua ⭐ Hay dùng hoặc /truyxuatlo.
            T("qa_trace", "ATTP", 5, "truyxuatlo"),
        ];

        private BotAction T(string code, string module, int order, string? command) => new()
        {
            Code = code, Module = module, Label = "🔍 Truy xuất", Command = command, CommandDescription = command == null ? null : "Truy xuất lô / mẻ", Bots = ["VH"], Order = order,
            Visible = i => i.IsAdmin || i.Menus.Any(m => string.Equals(m, "#/scm-lot", StringComparison.OrdinalIgnoreCase) || string.Equals(m, "#/scm-inv-balance", StringComparison.OrdinalIgnoreCase) || string.Equals(m, "#/prod-batch", StringComparison.OrdinalIgnoreCase)),
            Handler = (ctx, ct) => AskAsync(ctx, "🔍 Gõ số lô (vd. LO-…, NL-…) hoặc số mẻ (ME-…):", ReplyTrace, "x", ct),
        };

        public async Task ReplyAsync(BotActionContext ctx, PendingReply pending, CancellationToken ct)
        {
            string code = (ctx.Event.Text ?? string.Empty).Trim();
            if (code.Length < 3 || await TokenAsync(ctx, ct) is not { } token)
            {
                await AskAsync(ctx, "Mã quá ngắn – gõ lại số lô / số mẻ:", ReplyTrace, "x", ct);
                return;
            }
            var s = await Web.GetAsUserAsync<JsonElement>("api/Common/GlobalSearch?q=" + Uri.EscapeDataString(code), token, ct);
            var records = s.Data.ValueKind == JsonValueKind.Object ? GiaoVanActions.Arr(s.Data, "records") : [];
            var hit = records.FirstOrDefault(r => S(r, "type") is "MaterialLot" or "ProductionBatch" && string.Equals(S(r, "code"), code, StringComparison.OrdinalIgnoreCase));
            if (hit.ValueKind != JsonValueKind.Object)
            {
                hit = records.FirstOrDefault(r => S(r, "type") is "MaterialLot" or "ProductionBatch");
            }
            if (hit.ValueKind != JsonValueKind.Object || S(hit, "id") is not { } id)
            {
                await SayAsync(ctx, s.IsOk ? $"Không tìm thấy lô / mẻ \"{code}\" (hoặc bạn không có quyền xem)." : Problem(s), ct);
                return;
            }
            bool lot = S(hit, "type") == "MaterialLot";
            var t = await Web.GetAsUserAsync<JsonElement>((lot ? "api/MaterialLot/Trace?id=" : "api/ProductionBatch/Trace?id=") + id, token, ct);
            if (!t.IsOk || t.Data.ValueKind != JsonValueKind.Object)
            {
                await SayAsync(ctx, Problem(t), ct);
                return;
            }
            await SendAsync(ctx, Card(t.Data, S(hit, "code") ?? code, lot), ct);
        }

        /// <summary>Thẻ truy xuất: mỗi nhóm (NCC, PO, phiếu nhập, lô, phiếu xuất, lệnh, mẻ, đơn, khách…) tối đa 4 mục + cảnh báo.</summary>
        public static NeutralMessage Card(JsonElement d, string code, bool lot)
        {
            var lines = new List<string> { lot ? "Truy xuôi: lô → phiếu xuất → lệnh SX → mẻ → đơn / khách" : "Truy ngược: mẻ → lô nguyên liệu → phiếu nhập → nhà cung cấp" };
            (string Key, string Title)[] groups =
            [
                ("listSupplier", "🏭 NCC"), ("listPurchaseOrder", "🧾 PO"), ("listGoodsReceipt", "🚛 Phiếu nhập"), ("listLot", "📦 Lô"), ("listIssue", "📤 Phiếu xuất"),
                ("listProductionOrder", "📋 Lệnh SX"), ("listBatch", "🍳 Mẻ"), ("listPackingOrder", "🍱 Đóng gói"), ("listOrder", "🧾 Đơn hàng"),
                ("listDeliveryOrder", "🚚 Giao"), ("listCustomer", "👥 Khách"),
            ];
            foreach (var (key, title) in groups)
            {
                var items = GiaoVanActions.Arr(d, key);
                if (items.Count == 0)
                {
                    continue;
                }
                string names = string.Join(", ", items.Take(MaxPerGroup).Select(i => S(i, "number") ?? S(i, "name") ?? "?"));
                lines.Add($"{title} ({items.Count}): {names}{(items.Count > MaxPerGroup ? "…" : "")}");
            }
            int temps = GiaoVanActions.Arr(d, "listTemperature").Count;
            if (temps > 0)
            {
                lines.Add($"🌡 Nhiệt độ: {temps} lần đo");
            }
            lines.AddRange(GiaoVanActions.Arr(d, "listWarning").Take(5).Select(w => "⚠️ " + (w.ValueKind == JsonValueKind.String ? w.GetString() : w.ToString())));
            return new NeutralMessage { Title = $"🔍 Truy xuất {code}", Lines = lines };
        }

        private static string? S(JsonElement e, string k) => BepActions.S(e, k);
    }
}
