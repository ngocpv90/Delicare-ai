using System.Globalization;
using System.Text.Json;
using AppDelicare.Messaging.Callback;
using AppDelicare.Messaging.Channels;
using AppDelicare.Messaging.Conversations;
using AppDelicare.Messaging.Core;
using AppDelicare.Messaging.Experience;
using AppDelicare.Messaging.Presentation;

namespace AppDelicare.Messaging.Audiences.Internal.VanHanh
{
    /// <summary>
    /// BOT-1.5a (BOT_TELEGRAM §3.2 – tổ trưởng / trưởng ca bếp, OPS-KIT): 🍳 Mẻ cần làm hôm nay (<c>ProductionBatch/GetBoard</c>) → thẻ mẻ theo trạng thái:
    /// Kế hoạch [▶️ Bắt đầu] (<c>Start</c>); Đang nấu [🌡 Nhiệt độ lõi] (nút số 70–80 / gõ – <c>RecordCoreTemperature</c>, ngưỡng ≥ 70 °C kiểm ở api)
    /// [🧪 Lưu mẫu] (<c>RecordSampleRetained</c>) [✅ Xong mẻ] (số suất điền sẵn theo kế hoạch – <c>Finish</c>); 📈 Tiến độ ca (đếm theo trạng thái, suất đạt / kế hoạch).
    /// Quyền: mục <c>#/prod-batch</c> (hiện nút) + api kiểm lại khi ghi. Callback <c>bat:&lt;hành động&gt;:&lt;Id N&gt;[.giá trị]</c>.
    /// Ai chưa vào / chấm hộ / truy xuất mẻ / chia suất: BOT-1.5c (cần API "của tổ tôi").
    /// </summary>
    public sealed class BepActions(IDelicareApiClient web, UserTokenCache tokens, IChannelSender sender, ReplyContextStore replies, TimeProvider time)
        : WebActions(web, tokens, sender, replies), IBotActionProvider
    {
        public const string Kind = CallbackData.Kinds.Batch;
        public const string ReplyTemp = "bat-temp";
        public const string ReplyQty = "bat-qty";
        public const string Menu = "#/prod-batch";
        private static readonly decimal[] CoreTemps = [70m, 72m, 75m, 78m, 80m, 85m];

        public IEnumerable<BotAction> GetActions() =>
        [
            new BotAction { Code = "bep_batches", Module = "BEP", Label = "🍳 Mẻ cần làm", Command = "me", CommandDescription = "Mẻ cần làm hôm nay", Bots = ["VH"], MenuMetaLink = Menu, Order = 1, Handler = BatchesAsync },
            new BotAction { Code = "bep_progress", Module = "BEP", Bots = ["VH"], Label = "📈 Tiến độ ca", Command = "tiendo", CommandDescription = "Tiến độ sản xuất hôm nay", MenuMetaLink = Menu, Order = 2, Handler = ProgressAsync },
        ];

        private async Task<List<JsonElement>?> BoardAsync(BotActionContext ctx, string token, CancellationToken ct)
        {
            var r = await Web.GetAsUserAsync<JsonElement>("api/ProductionBatch/GetBoard?ProductionDate=" + VnTime.Today(time).ToString("yyyy-MM-dd"), token, ct);
            if (!r.IsOk || r.Data.ValueKind != JsonValueKind.Object)
            {
                await SayAsync(ctx, Problem(r), ct);
                return null;
            }
            return r.Data.TryGetProperty("listBatch", out var list) && list.ValueKind == JsonValueKind.Array ? [.. list.EnumerateArray()] : [];
        }

        public async Task BatchesAsync(BotActionContext ctx, CancellationToken ct)
        {
            if (await TokenAsync(ctx, ct) is not { } token || await BoardAsync(ctx, token, ct) is not { } batches)
            {
                return;
            }
            var open = batches.Where(b => S(b, "status") is not ("Packed" or "Failed" or "Cancelled")).OrderBy(b => S(b, "plannedStart")).Take(12).ToList();
            await ShowAsync(ctx, new NeutralMessage
            {
                Title = $"🍳 Mẻ cần làm hôm nay ({open.Count})",
                Lines = open.Count == 0 ? ["Không còn mẻ nào cần làm."] : [.. open.Select(Line)],
                Buttons = [.. open.Select(b => new NeutralButton($"{Icon(b)} {S(b, "batchNumber")}", CallbackData.Build(Kind, "view", G(b))))],
            }, ct);
        }

        /// <summary>BOT-2.4 (QR <c>start=LINE_&lt;mã&gt;</c>): mẻ đang nấu / chờ nấu hôm nay tại dây chuyền / bàn nấu → 1 mẻ thì mở thẻ ([▶️ Bắt đầu] [✅ Xong mẻ]), nhiều mẻ thì chọn.</summary>
        public async Task LineAsync(BotActionContext ctx, string code, CancellationToken ct)
        {
            if (await TokenAsync(ctx, ct) is not { } token)
            {
                return;
            }
            var r = await Web.GetAsUserAsync<JsonElement>($"api/ProductionBatch/GetBoard?ProductionDate={VnTime.Today(time):yyyy-MM-dd}&LineCode={Uri.EscapeDataString(code)}", token, ct);
            if (!r.IsOk || r.Data.ValueKind != JsonValueKind.Object)
            {
                await SayAsync(ctx, Problem(r), ct);
                return;
            }
            var open = (r.Data.TryGetProperty("listBatch", out var list) && list.ValueKind == JsonValueKind.Array ? [.. list.EnumerateArray()] : new List<JsonElement>())
                .Where(b => S(b, "status") is "Planned" or "Cooking").OrderBy(b => S(b, "status") == "Cooking" ? 0 : 1).ThenBy(b => S(b, "plannedStart")).Take(12).ToList();
            if (open.Count == 1 && Guid.TryParseExact(G(open[0]), "N", out Guid id))
            {
                await CardAsync(ctx, id, token, $"📟 {code}", ct);
                return;
            }
            await ShowAsync(ctx, new NeutralMessage
            {
                Title = $"📟 {code} – mẻ hôm nay ({open.Count})",
                Lines = open.Count == 0 ? ["Không có mẻ đang nấu / chờ nấu tại đây hôm nay."] : [.. open.Select(Line)],
                Buttons = [.. open.Select(b => new NeutralButton($"{Icon(b)} {S(b, "batchNumber")}", CallbackData.Build(Kind, "view", G(b))))],
            }, ct);
        }

        public async Task ProgressAsync(BotActionContext ctx, CancellationToken ct)
        {
            if (await TokenAsync(ctx, ct) is not { } token)
            {
                return;
            }
            var r = await Web.GetAsUserAsync<JsonElement>("api/ProductionBatch/GetBoard?ProductionDate=" + VnTime.Today(time).ToString("yyyy-MM-dd"), token, ct);
            if (!r.IsOk)
            {
                await SayAsync(ctx, Problem(r), ct);
                return;
            }
            var lines = new List<string> { $"Suất đạt / kế hoạch: {N(r.Data, "passedPortions")} / {N(r.Data, "plannedPortions")}" };
            if (r.Data.TryGetProperty("statusCounts", out var counts) && counts.ValueKind == JsonValueKind.Object)
            {
                lines.AddRange(counts.EnumerateObject().Select(c => $"• {StatusLabel(c.Name)}: {c.Value}"));
            }
            await SendAsync(ctx, new NeutralMessage { Title = "📈 Tiến độ hôm nay " + VnTime.Today(time).ToString("dd/MM"), Lines = lines }, ct);
        }

        public async Task HandleAsync(BotActionContext ctx, CallbackData data, CancellationToken ct)
        {
            string[] parts = (data.Id ?? string.Empty).Split('.', 2);
            if (!Guid.TryParseExact(parts[0], "N", out Guid id) || await TokenAsync(ctx, ct) is not { } token)
            {
                return;
            }
            string? value = parts.Length > 1 ? parts[1] : null;
            switch (data.Action)
            {
                case "view":
                    await CardAsync(ctx, id, token, null, ct);
                    break;
                case "start":
                    await PostAsync(ctx, id, token, "api/ProductionBatch/Start", [new("Id", id.ToString())], ct);
                    break;
                case "t":
                    await ShowAsync(ctx, new NeutralMessage
                    {
                        Title = "🌡 Nhiệt độ lõi (°C)",
                        Buttons = [.. CoreTemps.Select(t => new NeutralButton(t.ToString(CultureInfo.InvariantCulture), CallbackData.Build(Kind, "tv", id.ToString("N") + "." + t.ToString(CultureInfo.InvariantCulture)))),
                            new NeutralButton("Khác…", CallbackData.Build(Kind, "tx", id)), new NeutralButton("⬅️ Quay lại", CallbackData.Build(Kind, "view", id))],
                    }, ct);
                    break;
                case "tx":
                    await AskAsync(ctx, "Nhập nhiệt độ lõi (°C), vd. 73.5:", ReplyTemp, id.ToString("N"), ct);
                    break;
                case "tv" when value != null:
                    await TemperatureAsync(ctx, id, token, value, ct);
                    break;
                case "smp":
                    await PostAsync(ctx, id, token, "api/ProductionBatch/RecordSampleRetained", [new("Id", id.ToString()), new("ActualValue", "Đã lưu mẫu (Telegram)")], ct);
                    break;
                case "fin":
                    await FinishAskAsync(ctx, id, token, ct);
                    break;
                case "fq" when value != null:
                    await FinishAsync(ctx, id, token, value, ct);
                    break;
                case "fx":
                    await AskAsync(ctx, "Nhập số suất thực tế của mẻ:", ReplyQty, id.ToString("N"), ct);
                    break;
                default:
                    await SayAsync(ctx, "Nút đã hết hạn.", ct);
                    break;
            }
        }

        /// <summary>Trả lời gõ tay: nhiệt độ lõi / số suất.</summary>
        public async Task ReplyAsync(BotActionContext ctx, PendingReply pending, CancellationToken ct)
        {
            if (!Guid.TryParseExact(pending.Key, "N", out Guid id) || await TokenAsync(ctx, ct) is not { } token)
            {
                return;
            }
            string text = (ctx.Event.Text ?? string.Empty).Trim().Replace(',', '.');
            if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var v))
            {
                await AskAsync(ctx, "Chưa đúng số – nhập lại:", pending.Flow, pending.Key, ct);
                return;
            }
            if (pending.Flow == ReplyTemp)
            {
                await TemperatureAsync(ctx, id, token, v.ToString(CultureInfo.InvariantCulture), ct);
            }
            else
            {
                await FinishAsync(ctx, id, token, v.ToString(CultureInfo.InvariantCulture), ct);
            }
        }

        private Task TemperatureAsync(BotActionContext ctx, Guid id, string token, string value, CancellationToken ct)
            => PostAsync(ctx, id, token, "api/ProductionBatch/RecordCoreTemperature", [new("Id", id.ToString()), new("TemperatureC", value)], ct);

        private async Task FinishAskAsync(BotActionContext ctx, Guid id, string token, CancellationToken ct)
        {
            var batch = (await BoardAsync(ctx, token, ct))?.FirstOrDefault(b => G(b) == id.ToString("N"));
            decimal planned = batch is { } b && b.TryGetProperty("plannedQuantity", out var p) && p.TryGetDecimal(out var pq) ? pq : 0;
            // Điền sẵn số đã biết (kế hoạch) – bấm 1 chạm nếu đúng; lệch thì chọn / gõ.
            var presets = new[] { planned, Math.Floor(planned * 0.98m), Math.Floor(planned * 0.95m) }.Where(x => x > 0).Distinct().ToList();
            await ShowAsync(ctx, new NeutralMessage
            {
                Title = "✅ Xong mẻ – số suất thực tế",
                Lines = [$"Kế hoạch: {planned:0.##} suất"],
                Buttons = [.. presets.Select(q => new NeutralButton((q == planned ? "✅ Đúng " : "") + q.ToString("0.##", CultureInfo.InvariantCulture), CallbackData.Build(Kind, "fq", id.ToString("N") + "." + q.ToString("0.##", CultureInfo.InvariantCulture)))),
                    new NeutralButton("Khác…", CallbackData.Build(Kind, "fx", id)), new NeutralButton("⬅️ Quay lại", CallbackData.Build(Kind, "view", id))],
            }, ct);
        }

        /// <summary>
        /// UG-1 C-07: "✅ Xong mẻ" = Finish (Chờ QC) rồi RecordOutput (ProductionOutput) cùng số suất – như web (production.js).
        /// Trước đây bot chỉ gọi Finish → không có sản lượng → cho Đạt / đóng gói bị chặn "Chưa ghi sản lượng mẻ". Finish lỗi thì dừng.
        /// </summary>
        private async Task FinishAsync(BotActionContext ctx, Guid id, string token, string quantity, CancellationToken ct)
        {
            var fin = await Web.PostFormAsUserAsync<JsonElement>("api/ProductionBatch/Finish", [new("Id", id.ToString()), new("ActualQuantity", quantity)], token, ct);
            if (!fin.IsOk)
            {
                await CardAsync(ctx, id, token, "⚠️ " + Problem(fin), ct);
                return;
            }
            var output = await Web.PostFormAsUserAsync<JsonElement>("api/ProductionBatch/RecordOutput", [new("Id", id.ToString()), new("ProducedQuantity", quantity)], token, ct);
            await CardAsync(ctx, id, token, output.IsOk ? "✅ Đã kết thúc mẻ và ghi sản lượng " + quantity + " suất – chờ QC" : "⚠️ Đã kết thúc mẻ nhưng chưa ghi được sản lượng: " + Problem(output), ct);
        }

        private async Task PostAsync(BotActionContext ctx, Guid id, string token, string path, List<KeyValuePair<string, string>> form, CancellationToken ct)
        {
            var r = await Web.PostFormAsUserAsync<JsonElement>(path, form, token, ct);
            await CardAsync(ctx, id, token, r.IsOk ? "✅ " + (r.ResponseMessage ?? "Đã ghi") : "⚠️ " + Problem(r), ct);
        }

        private async Task CardAsync(BotActionContext ctx, Guid id, string token, string? note, CancellationToken ct)
        {
            var batch = (await BoardAsync(ctx, token, ct))?.FirstOrDefault(b => G(b) == id.ToString("N"));
            if (batch is not { } b)
            {
                await SayAsync(ctx, note ?? "Không tìm thấy mẻ (hoặc không thuộc hôm nay).", ct);
                return;
            }
            string status = S(b, "status") ?? string.Empty;
            var m = new NeutralMessage
            {
                Title = $"{Icon(b)} {S(b, "batchNumber")} – {S(b, "dishName")}",
                Lines =
                [
                    $"{S(b, "statusName")} · {S(b, "shiftCode")} · {Time(S(b, "plannedStart"))}",
                    $"Suất: kế hoạch {N(b, "plannedQuantity")}{(S(b, "actualQuantity") is { } aq ? ", thực tế " + aq : "")}",
                    $"Nhiệt độ lõi: {(S(b, "lastCoreTemperatureC") is { } t ? t + " °C" + (B(b, "coreTemperatureOk") == false ? " ⚠️ chưa đạt" : " ✅") : "chưa đo")}",
                    $"Lưu mẫu: {(B(b, "sampleRetained") == true ? "✅" : "chưa")}",
                ],
            };
            if (note != null)
            {
                m.Lines.Insert(0, note);
            }
            string n = id.ToString("N");
            if (status == "Planned")
            {
                m.Buttons.Add(new NeutralButton("▶️ Bắt đầu", CallbackData.Build(Kind, "start", n)));
            }
            if (status == "Cooking")
            {
                m.Buttons.Add(new NeutralButton("🌡 Nhiệt độ lõi", CallbackData.Build(Kind, "t", n)));
                m.Buttons.Add(new NeutralButton("✅ Xong mẻ", CallbackData.Build(Kind, "fin", n)));
            }
            if (status is "Cooking" or "QCPending" or "Passed" && B(b, "sampleRetained") != true)
            {
                m.Buttons.Add(new NeutralButton("🧪 Lưu mẫu", CallbackData.Build(Kind, "smp", n)));
            }
            m.Buttons.Add(new NeutralButton("🔄 Làm mới", CallbackData.Build(Kind, "view", n)));
            await ShowAsync(ctx, m, ct);
        }

        private static string Line(JsonElement b) => $"{Icon(b)} {S(b, "batchNumber")} {S(b, "dishName")} · {Time(S(b, "plannedStart"))} · {S(b, "statusName")}";

        private static string Icon(JsonElement b) => S(b, "status") switch
        {
            "Planned" => "⏳",
            "Cooking" => "🔥",
            "QCPending" => "🧪",
            "Passed" => "✅",
            _ => "•",
        };

        public static string StatusLabel(string code) => code switch
        {
            "Planned" => "Kế hoạch", "Cooking" => "Đang nấu", "QCPending" => "Chờ QC", "Passed" => "Đạt", "Failed" => "Không đạt", "Packed" => "Đã đóng gói", "Cancelled" => "Hủy",
            _ => code,
        };

        public static string? S(JsonElement e, string k)
            => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(k, out var v) && v.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)
                ? v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString() : null;

        private static bool? B(JsonElement e, string k) => e.TryGetProperty(k, out var v) ? v.ValueKind switch { JsonValueKind.True => true, JsonValueKind.False => false, _ => null } : null;

        private static string N(JsonElement e, string k) => decimal.TryParse(S(e, k), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d.ToString("0.##", CultureInfo.InvariantCulture) : "0";

        private static string G(JsonElement b) => Guid.TryParse(S(b, "id"), out var g) ? g.ToString("N") : string.Empty;

        private static string Time(string? s) => DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t.ToString("HH:mm") : "";
    }
}
