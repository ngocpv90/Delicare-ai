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
    /// Kế hoạch [▶️ Bắt đầu] (<c>Start</c>); Đang nấu: hàng nhiệt độ lõi bấm 1 chạm (<c>RecordCoreTemperature</c>, ngưỡng ≥ 70 °C kiểm ở api)
    /// [🧪 Lưu mẫu] (<c>RecordSampleRetained</c>) [✅ Xong mẻ] (số suất điền sẵn theo kế hoạch – <c>Finish</c>); 📈 Tiến độ ca (đếm theo trạng thái, suất đạt / kế hoạch).
    /// UG-1 C-15: mẻ Đang nấu có công đoạn (<c>GetDetail.listStage</c>) → dòng tiến độ công đoạn + nút công đoạn hiện tại
    /// [▶️ Bắt đầu &lt;công đoạn&gt;] (<c>StartStage</c>) / [⏹ Xong &lt;công đoạn&gt;] (<c>FinishStage</c>) – thứ tự kiểm ở api.
    /// BOT-UX-2: ⭐ Mẻ cần làm → ME → 78° = 3 chạm; số khác / số suất = màn số (−10 −1 +1 +10, không gõ tay); khắc phục / lý do nấu thêm = nút lý do + [Khác…];
    /// danh sách mặc định ca hiện tại (mẻ trễ / nhiệt độ chưa đạt lên đầu), 6 mẻ / trang, [Xem tất cả]; ghi xong → dòng "✅ … · HH:mm" đầu thẻ, giữ nút bước kế tiếp.
    /// Quyền: mục <c>#/prod-batch</c> (hiện nút) + api kiểm lại khi ghi. Callback <c>bat:&lt;hành động&gt;:&lt;Id N&gt;[.giá trị]</c>.
    /// </summary>
    public sealed class BepActions(IDelicareApiClient web, UserTokenCache tokens, IChannelSender sender, ReplyContextStore replies, TimeProvider time, ReasonCatalog reasons)
        : WebActions(web, tokens, sender, replies), IBotActionProvider
    {
        public const string Kind = CallbackData.Kinds.Batch;
        public const string ReplyTemp = "bat-temp";
        public const string ReplyQty = "bat-qty";
        /// <summary>UG-1 C-09: nhiệt độ lõi dưới ngưỡng → hành động khắc phục (khóa = "&lt;Id N&gt;.&lt;nhiệt độ&gt;").</summary>
        public const string ReplyCorrective = "bat-fix";
        /// <summary>BOT-3.2 / 3.3 (D-72): số suất nấu thêm → lý do; số suất thừa.</summary>
        public const string ReplyExtraQty = "bat-exq";
        public const string ReplyExtraReason = "bat-exr";
        public const string ReplyWaste = "bat-waste";
        public const string Menu = "#/prod-batch";
        public const string ListCode = "bep_batches";
        /// <summary>Hàng nhiệt độ lõi bấm 1 chạm trên thẻ mẻ Đang nấu.</summary>
        public static readonly decimal[] CoreTemps = [70m, 72m, 75m, 78m, 80m];
        public const string TempRow = "bat-temp";

        public IEnumerable<BotAction> GetActions() =>
        [
            new BotAction { Code = ListCode, Module = "BEP", Label = "🍳 Mẻ cần làm", Command = "me", CommandDescription = "Mẻ cần làm hôm nay", Bots = ["VH"], MenuMetaLink = Menu, Order = 1, Handler = BatchesAsync },
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

        /// <summary>Mẻ gấp: kế hoạch đã quá giờ bắt đầu, hoặc nhiệt độ lõi đo chưa đạt.</summary>
        private bool Urgent(JsonElement b, DateTime now)
            => (S(b, "status") == "Planned" && At(S(b, "plannedStart")) is { } start && start < now) || B(b, "coreTemperatureOk") == false;

        /// <summary>Ca hiện tại: đang nấu / chờ QC, mẻ gấp, hoặc giờ bắt đầu kế hoạch trong khoảng 2 giờ trước → 4 giờ tới.</summary>
        private bool CurrentShift(JsonElement b, DateTime now)
            => S(b, "status") is "Cooking" or "QCPending" || Urgent(b, now) || (At(S(b, "plannedStart")) is { } start && start >= now.AddHours(-2) && start <= now.AddHours(4));

        public async Task BatchesAsync(BotActionContext ctx, CancellationToken ct)
        {
            if (await TokenAsync(ctx, ct) is not { } token || await BoardAsync(ctx, token, ct) is not { } batches)
            {
                return;
            }
            var now = VnTime.Now(time);
            var open = batches.Where(b => S(b, "status") is not ("Packed" or "Failed" or "Cancelled"))
                .OrderBy(b => Urgent(b, now) ? 0 : S(b, "status") == "Cooking" ? 1 : 2).ThenBy(b => S(b, "plannedStart")).ToList();
            bool all = ctx.Filter == ListPager.All;
            var shown = all ? open : [.. open.Where(b => CurrentShift(b, now))];
            var m = new NeutralMessage
            {
                Title = $"🍳 Mẻ cần làm hôm nay ({shown.Count}{(all ? "" : " · ca hiện tại")})",
                Lines = shown.Count == 0 ? [open.Count == 0 ? "Không còn mẻ nào cần làm." : "Ca hiện tại không còn mẻ – bấm 📋 Xem tất cả."] : [],
            };
            (string, string)? toggle = open.Count == shown.Count && !all ? null : all ? ("🕒 Chỉ ca hiện tại", string.Empty) : ($"📋 Xem tất cả ({open.Count})", ListPager.All);
            ListPager.Fill(m, ctx, ListCode, shown, b => (Urgent(b, now) ? "❗ " : "") + Line(b), b => new NeutralButton($"{Icon(b)} {S(b, "batchNumber")}", CallbackData.Build(Kind, "view", G(b))), toggle);
            await ShowAsync(ctx, m, ct);
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
                .Where(b => S(b, "status") is "Planned" or "Cooking").OrderBy(b => S(b, "status") == "Cooking" ? 0 : 1).ThenBy(b => S(b, "plannedStart")).Take(ListPager.PageSize * 2).ToList();
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
            string[] parts = (data.Id ?? string.Empty).Split('.', 3);
            if (!Guid.TryParseExact(parts[0], "N", out Guid id) || await TokenAsync(ctx, ct) is not { } token)
            {
                return;
            }
            string? value = parts.Length > 1 ? parts[1] : null;
            string? extra = parts.Length > 2 ? parts[2] : null;
            string n = id.ToString("N");
            switch (data.Action)
            {
                case "view":
                    await CardAsync(ctx, id, token, null, ct);
                    break;
                case "start":
                    await PostAsync(ctx, id, token, "api/ProductionBatch/Start", [new("Id", id.ToString())], "Đã bắt đầu mẻ", ct);
                    break;
                case "t" or "tn":
                    await NumberScreenAsync(ctx, id, token, "tn", "tv", value, ct);
                    break;
                case "tx":
                    // Nút cũ (trước BOT-UX-2) – vẫn nhận gõ tay.
                    await AskAsync(ctx, "Nhập nhiệt độ lõi (°C), vd. 73.5:", ReplyTemp, n, ct);
                    break;
                case "tv" when value != null:
                    await TemperatureAsync(ctx, id, token, value, ct);
                    break;
                case "fix" when value != null:
                    await CorrectiveAskAsync(ctx, id, value, null, ct);
                    break;
                case "fixr" when value != null && reasons.At(ReasonCatalog.CoreTempFix, extra) is { } fix:
                    await CorrectiveAsync(ctx, id, token, value, fix, ct);
                    break;
                case "fixx" when value != null:
                    await AskAsync(ctx, "Nhập hành động khắc phục (vd. gia nhiệt lại 10 phút, đo lại):", ReplyCorrective, n + "." + value, ct);
                    break;
                case "smp":
                    await PostAsync(ctx, id, token, "api/ProductionBatch/RecordSampleRetained", [new("Id", id.ToString()), new("ActualValue", "Đã lưu mẫu (Telegram)")], "Đã ghi lưu mẫu", ct);
                    break;
                case "fin" or "fn":
                    await NumberScreenAsync(ctx, id, token, "fn", "fq", value, ct);
                    break;
                case "fq" when value != null:
                    await FinishAsync(ctx, id, token, value, ct);
                    break;
                case "fx":
                    await AskAsync(ctx, "Nhập số suất thực tế của mẻ:", ReplyQty, n, ct);
                    break;
                case "ex" or "exn":
                    await NumberScreenAsync(ctx, id, token, "exn", "exq", value, ct);
                    break;
                case "exq" when value != null:
                    await ExtraReasonAskAsync(ctx, id, value, ct);
                    break;
                case "exr" when value != null && reasons.At(ReasonCatalog.ExtraBatch, extra) is { } why:
                    await CreateExtraAsync(ctx, id, token, value, why, ct);
                    break;
                case "exx" when value != null:
                    await AskAsync(ctx, $"➕ Nấu thêm {value} suất – lý do (vd. thiếu suất khách A, mẻ hỏng):", ReplyExtraReason, n + "." + value, ct);
                    break;
                case "wx" or "wxn":
                    await NumberScreenAsync(ctx, id, token, "wxn", "wq", value, ct);
                    break;
                case "wq" when value != null:
                    await PostAsync(ctx, id, token, "api/ProductionBatch/RecordWaste", [new("Id", id.ToString()), new("QuantityMeals", value), new("Note", "Suất thừa – Telegram")], $"Đã ghi {value} suất thừa", ct);
                    break;
                case "pk":
                    await PostAsync(ctx, id, token, "api/ProductionBatch/Pack", [new("Id", id.ToString()), new("Note", "Chia suất xong – Telegram")], "Đã chia suất xong", ct);
                    break;
                case "ss" when value != null:
                    await PostAsync(ctx, id, token, "api/ProductionBatch/StartStage", [new("Id", id.ToString()), new("StageCode", value)], "Đã bắt đầu công đoạn", ct);
                    break;
                case "sf" when value != null:
                    await PostAsync(ctx, id, token, "api/ProductionBatch/FinishStage", [new("Id", id.ToString()), new("StageCode", value)], "Đã xong công đoạn", ct);
                    break;
                default:
                    await SayAsync(ctx, "Nút đã hết hạn.", ct);
                    break;
            }
        }

        /// <summary>
        /// BOT-UX-2 (mục 6): màn số của mẻ – nhiệt độ lõi (điền sẵn lần đo trước, không có thì 75), số suất xong mẻ (kế hoạch), nấu thêm (10), suất thừa (1).
        /// <paramref name="value"/> = giá trị đang chọn (bấm −1 / +1); null = giá trị điền sẵn.
        /// </summary>
        private async Task NumberScreenAsync(BotActionContext ctx, Guid id, string token, string screen, string confirm, string? value, CancellationToken ct)
        {
            var batch = (await BoardAsync(ctx, token, ct))?.FirstOrDefault(b => G(b) == id.ToString("N"));
            if (batch is not { } b)
            {
                await SayAsync(ctx, "Không tìm thấy mẻ (hoặc không thuộc hôm nay).", ct);
                return;
            }
            decimal planned = Dec(b, "plannedQuantity");
            var m = new NeutralMessage { Title = $"{Icon(b)} {S(b, "batchNumber")} – {S(b, "dishName")}" };
            string key = id.ToString("N");
            decimal? chosen = InputWidgets.Value(value);
            switch (screen)
            {
                case "tn":
                    m.Lines.Add("🌡 Nhiệt độ lõi – bấm để chỉnh rồi ✅ Xác nhận (ngưỡng ≥ 70 °C kiểm ở máy chủ):");
                    InputWidgets.Number(m, Kind, screen, confirm, key, chosen ?? (Dec(b, "lastCoreTemperatureC") is > 0m and var last ? last : 75m), "°C", CoreTemps, 0, 150, "✅ Ghi");
                    break;
                case "fn":
                    m.Lines.Add($"✅ Xong mẻ – số suất thực tế (kế hoạch {planned:0.##}):");
                    InputWidgets.Number(m, Kind, screen, confirm, key, chosen ?? planned, "suất", [planned, Math.Floor(planned * 0.98m), Math.Floor(planned * 0.95m)], 1);
                    break;
                case "exn":
                    m.Lines.Add("➕ Nấu thêm bao nhiêu suất?");
                    InputWidgets.Number(m, Kind, screen, confirm, key, chosen ?? 10m, "suất", [5m, 10m, 20m, 50m], 1);
                    break;
                default:
                    m.Lines.Add("♻️ Số suất thừa (nấu dư, không giao):");
                    InputWidgets.Number(m, Kind, screen, confirm, key, chosen ?? 1m, "suất", [1m, 2m, 5m, 10m], 1);
                    break;
            }
            await ShowAsync(ctx, m, ct);
        }

        /// <summary>Trả lời gõ tay (nút cũ / [✏️ Khác…]): nhiệt độ lõi / số suất / lý do.</summary>
        public async Task ReplyAsync(BotActionContext ctx, PendingReply pending, CancellationToken ct)
        {
            if (pending.Flow == ReplyCorrective)
            {
                await CorrectiveReplyAsync(ctx, pending, ct);
                return;
            }
            if (pending.Flow == ReplyExtraReason)
            {
                await ExtraReasonAsync(ctx, pending, ct);
                return;
            }
            if (!Guid.TryParseExact(pending.Key, "N", out Guid id) || await TokenAsync(ctx, ct) is not { } token)
            {
                return;
            }
            string text = (ctx.Event.Text ?? string.Empty).Trim().Replace(',', '.');
            if (pending.Flow is ReplyExtraQty or ReplyWaste)
            {
                if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var q) || q <= 0)
                {
                    await AskAsync(ctx, "Số suất phải > 0 – nhập lại:", pending.Flow, pending.Key, ct);
                    return;
                }
                string qty = q.ToString("0.##", CultureInfo.InvariantCulture);
                if (pending.Flow == ReplyWaste)
                {
                    await PostAsync(ctx, id, token, "api/ProductionBatch/RecordWaste", [new("Id", id.ToString()), new("QuantityMeals", qty), new("Note", "Suất thừa – Telegram")], $"Đã ghi {qty} suất thừa", ct);
                }
                else
                {
                    await ExtraReasonAskAsync(ctx, id, qty, ct);
                }
                return;
            }
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

        /// <summary>BOT-UX-2 (mục 7): lý do nấu thêm = nút lý do hay gặp (<c>Messaging:Reasons:bat-exr</c>) + [✏️ Khác…].</summary>
        private Task ExtraReasonAskAsync(BotActionContext ctx, Guid id, string qty, CancellationToken ct)
        {
            Screens.Show(CallbackData.Build(Kind, "exq", $"{id:N}.{qty}"));
            return ShowAsync(ctx, new NeutralMessage
            {
                Title = $"➕ Nấu thêm {qty} suất – lý do",
                Buttons = reasons.Buttons(ReasonCatalog.ExtraBatch, Kind, "exr", "exx", $"{id:N}.{qty}"),
            }, ct);
        }

        private async Task ExtraReasonAsync(BotActionContext ctx, PendingReply pending, CancellationToken ct)
        {
            string[] parts = pending.Key.Split('.', 2);
            if (parts.Length < 2 || !Guid.TryParseExact(parts[0], "N", out Guid id) || await TokenAsync(ctx, ct) is not { } token)
            {
                return;
            }
            string reason = (ctx.Event.Text ?? string.Empty).Trim();
            if (reason.Length < 3)
            {
                await AskAsync(ctx, "Lý do quá ngắn – gõ lại:", ReplyExtraReason, pending.Key, ct);
                return;
            }
            await CreateExtraAsync(ctx, id, token, parts[1], reason, ct);
        }

        /// <summary>BOT-3.3 (D-72): ➕ nấu thêm – ProductionBatch/CreateExtra, báo mẻ mới (không còn bước nào → phiếu kết quả [▶ Tiếp tục]).</summary>
        private async Task CreateExtraAsync(BotActionContext ctx, Guid id, string token, string qty, string reason, CancellationToken ct)
        {
            var r = await Web.PostFormAsUserAsync<JsonElement>("api/ProductionBatch/CreateExtra",
                [new("Id", id.ToString()), new("Quantity", qty), new("Reason", reason[..Math.Min(reason.Length, 500)])], token, ct);
            if (!r.IsOk)
            {
                await SayAsync(ctx, "⚠️ " + Problem(r), ct);
                return;
            }
            var lines = new List<string> { $"{S(r.Data.TryGetProperty("batch", out var b) ? b : default, "batchNumber")} · {qty} suất · Kế hoạch" };
            lines.AddRange(GiaoVanActions.Arr(r.Data, "listWarning").Select(w => "⚠️ " + (w.ValueKind == JsonValueKind.String ? w.GetString() : w.ToString())));
            CallbackAnswer.Set($"Đã tạo mẻ nấu thêm {qty} suất");
            await SendAsync(ctx, new NeutralMessage { Title = "✅ " + (r.ResponseMessage ?? "Đã tạo mẻ nấu thêm"), Lines = lines }, ct);
        }

        /// <summary>
        /// Ghi nhiệt độ lõi. UG-1 C-09: dưới ngưỡng (máy chủ báo cần hành động khắc phục – CoreTempCorrectiveRequired) → chọn hành động khắc phục (nút lý do + [✏️ Khác…])
        /// rồi ghi lại kèm CorrectiveAction.
        /// </summary>
        private async Task TemperatureAsync(BotActionContext ctx, Guid id, string token, string value, CancellationToken ct)
        {
            var r = await Web.PostFormAsUserAsync<JsonElement>("api/ProductionBatch/RecordCoreTemperature", [new("Id", id.ToString()), new("TemperatureC", value)], token, ct);
            if (!r.IsOk && (r.ResponseMessage ?? string.Empty).Contains("khắc phục", StringComparison.OrdinalIgnoreCase))
            {
                await CorrectiveAskAsync(ctx, id, value, r.ResponseMessage, ct);
                return;
            }
            if (r.IsOk)
            {
                CallbackAnswer.Set($"✅ Đã ghi {value} °C");
            }
            await CardAsync(ctx, id, token, r.IsOk ? InputWidgets.Done($"Đã ghi nhiệt độ lõi {value} °C", time) : "⚠️ " + Problem(r), ct);
        }

        private Task CorrectiveAskAsync(BotActionContext ctx, Guid id, string value, string? message, CancellationToken ct)
        {
            CallbackAnswer.Set($"⚠️ {value} °C chưa đạt – chọn hành động khắc phục");
            Screens.Show(CallbackData.Build(Kind, "fix", $"{id:N}.{value}"));
            return ShowAsync(ctx, new NeutralMessage
            {
                Banner = "⚠️ " + (message ?? $"Nhiệt độ lõi {value} °C dưới ngưỡng – cần hành động khắc phục."),
                Title = $"🌡 {value} °C – hành động khắc phục",
                Lines = ["Chọn hành động đã làm (ghi kèm lần đo):"],
                Buttons = reasons.Buttons(ReasonCatalog.CoreTempFix, Kind, "fixr", "fixx", $"{id:N}.{value}"),
            }, ct);
        }

        private async Task CorrectiveReplyAsync(BotActionContext ctx, PendingReply pending, CancellationToken ct)
        {
            int dot = pending.Key.IndexOf('.');
            if (dot <= 0 || !Guid.TryParseExact(pending.Key[..dot], "N", out Guid id) || await TokenAsync(ctx, ct) is not { } token)
            {
                return;
            }
            string action = (ctx.Event.Text ?? string.Empty).Trim();
            if (action.Length < 3)
            {
                await AskAsync(ctx, "Nhập rõ hành động khắc phục (ít nhất 3 ký tự):", ReplyCorrective, pending.Key, ct);
                return;
            }
            await CorrectiveAsync(ctx, id, token, pending.Key[(dot + 1)..], action, ct);
        }

        private Task CorrectiveAsync(BotActionContext ctx, Guid id, string token, string value, string action, CancellationToken ct)
            => PostAsync(ctx, id, token, "api/ProductionBatch/RecordCoreTemperature",
                [new("Id", id.ToString()), new("TemperatureC", value), new("CorrectiveAction", action.Length > 500 ? action[..500] : action)], $"Đã ghi {value} °C kèm khắc phục", ct);

        /// <summary>
        /// UG-1 C-07: "✅ Xong mẻ" = Finish (Chờ QC) rồi RecordOutput (ProductionOutput) cùng số suất – như web (production.js). Finish lỗi thì dừng.
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
            CallbackAnswer.Set(output.IsOk ? $"✅ Xong mẻ – {quantity} suất" : "⚠️ Chưa ghi được sản lượng");
            await CardAsync(ctx, id, token, output.IsOk ? InputWidgets.Done("Đã kết thúc mẻ và ghi sản lượng " + quantity + " suất – chờ QC", time)
                : "⚠️ Đã kết thúc mẻ nhưng chưa ghi được sản lượng: " + Problem(output), ct);
        }

        private async Task PostAsync(BotActionContext ctx, Guid id, string token, string path, List<KeyValuePair<string, string>> form, string done, CancellationToken ct)
        {
            var r = await Web.PostFormAsUserAsync<JsonElement>(path, form, token, ct);
            CallbackAnswer.Set(r.IsOk ? "✅ " + done : "⚠️ " + Problem(r));
            await CardAsync(ctx, id, token, r.IsOk ? InputWidgets.Done(r.ResponseMessage ?? done, time) : "⚠️ " + Problem(r), ct);
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
            string n = id.ToString("N");
            // BOT-UX-2 (mục 2, 9): thẻ mẻ là 1 màn – vẽ lại sau thao tác ghi thì ngăn xếp lùi về đúng thẻ này; dòng kết quả nằm đầu thẻ, nút bước kế tiếp giữ nguyên.
            Screens.Show(CallbackData.Build(Kind, "view", n));
            var m = new NeutralMessage
            {
                Banner = note,
                Title = $"{Icon(b)} {S(b, "batchNumber")} – {S(b, "dishName")}",
                Lines =
                [
                    $"{S(b, "statusName")} · {S(b, "shiftCode")} · {Time(S(b, "plannedStart"))}",
                    $"Suất: kế hoạch {N(b, "plannedQuantity")}{(S(b, "actualQuantity") is { } aq ? ", thực tế " + aq : "")}",
                    $"Nhiệt độ lõi: {(S(b, "lastCoreTemperatureC") is { } t ? t + " °C" + (B(b, "coreTemperatureOk") == false ? " ⚠️ chưa đạt" : " ✅") : "chưa đo")}",
                    $"Lưu mẫu: {(B(b, "sampleRetained") == true ? "✅" : "chưa")}",
                ],
            };
            if (status == "Planned")
            {
                m.Buttons.Add(new NeutralButton("▶️ Bắt đầu", CallbackData.Build(Kind, "start", n)));
            }
            if (status == "Cooking")
            {
                // BOT-UX-2: nhiệt độ lõi bấm 1 chạm (⭐ Mẻ cần làm → ME → 78° = 3 chạm); số khác → màn số.
                m.Lines.Add("🌡 Bấm nhiệt độ lõi vừa đo để ghi:");
                m.Buttons.AddRange(CoreTemps.Select(t => new NeutralButton(t.ToString("0", CultureInfo.InvariantCulture) + "°",
                    CallbackData.Build(Kind, "tv", n + "." + t.ToString("0", CultureInfo.InvariantCulture)), Row: TempRow)));
                m.Buttons.Add(new NeutralButton("🌡 Nhiệt độ khác", CallbackData.Build(Kind, "tn", n)));
                await StagesAsync(m, id, token, ct);
                m.Buttons.Add(new NeutralButton("✅ Xong mẻ", CallbackData.Build(Kind, "fin", n)));
            }
            // BOT-3.2 / 3.3 (D-72): chia suất xong (mẻ đạt QC → Đã đóng gói), ghi suất thừa, nấu thêm (mẻ bổ sung cùng lệnh).
            if (status == "Passed")
            {
                m.Buttons.Add(new NeutralButton("🍱 Chia suất xong", CallbackData.Build(Kind, "pk", n)));
            }
            if (status is "Cooking" or "QCPending" or "Passed" && B(b, "sampleRetained") != true)
            {
                m.Buttons.Add(new NeutralButton("🧪 Lưu mẫu", CallbackData.Build(Kind, "smp", n)));
            }
            if (status is "Passed" or "Packed")
            {
                m.Buttons.Add(new NeutralButton("♻️ Ghi thừa", CallbackData.Build(Kind, "wx", n)));
            }
            if (status is "Cooking" or "QCPending" or "Passed" or "Packed" or "Failed")
            {
                m.Buttons.Add(new NeutralButton("➕ Nấu thêm", CallbackData.Build(Kind, "ex", n)));
            }
            m.Buttons.Add(new NeutralButton("🔄 Làm mới", CallbackData.Build(Kind, "view", n)));
            await ShowAsync(ctx, m, ct);
        }

        /// <summary>
        /// UG-1 C-15: công đoạn của mẻ (Sơ chế → Nấu → Chia suất…): 1 dòng tiến độ + nút cho công đoạn đang làm (Xong) hoặc công đoạn kế tiếp (Bắt đầu).
        /// Mẻ không theo dõi công đoạn / api lỗi → bỏ qua (thẻ vẫn hiện như cũ).
        /// </summary>
        private async Task StagesAsync(NeutralMessage m, Guid id, string token, CancellationToken ct)
        {
            var r = await Web.GetAsUserAsync<JsonElement>("api/ProductionBatch/GetDetail?id=" + id, token, ct);
            if (!r.IsOk || r.Data.ValueKind != JsonValueKind.Object || !r.Data.TryGetProperty("listStage", out var list) || list.ValueKind != JsonValueKind.Array || list.GetArrayLength() == 0)
            {
                return;
            }
            var stages = list.EnumerateArray().ToList();
            string Name(JsonElement s) => S(s, "stageName") ?? S(s, "stageCode") ?? "?";
            m.Lines.Add("Công đoạn: " + string.Join(" → ", stages.Select(s => Name(s) + (S(s, "status") switch { "Done" => " ✅", "InProgress" => " ⏳", _ => "" }))));
            var cur = stages.FirstOrDefault(s => S(s, "status") == "InProgress");
            bool doing = cur.ValueKind == JsonValueKind.Object;
            var target = doing ? cur : stages.FirstOrDefault(s => S(s, "status") != "Done");
            if (target.ValueKind != JsonValueKind.Object || S(target, "stageCode") is not { Length: > 0 } code)
            {
                return;
            }
            string data = id.ToString("N") + "." + code;
            if (System.Text.Encoding.UTF8.GetByteCount(Kind + ":ss:" + data) <= CallbackData.MaxBytes)
            {
                m.Buttons.Add(doing ? new NeutralButton("⏹ Xong " + Name(target), CallbackData.Build(Kind, "sf", data)) : new NeutralButton("▶️ Bắt đầu " + Name(target), CallbackData.Build(Kind, "ss", data)));
            }
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

        private static bool? B(JsonElement e, string k) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(k, out var v) ? v.ValueKind switch { JsonValueKind.True => true, JsonValueKind.False => false, _ => null } : null;

        private static decimal Dec(JsonElement e, string k) => decimal.TryParse(S(e, k), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : 0m;

        private static string N(JsonElement e, string k) => Dec(e, k).ToString("0.##", CultureInfo.InvariantCulture);

        private static string G(JsonElement b) => Guid.TryParse(S(b, "id"), out var g) ? g.ToString("N") : string.Empty;

        private static DateTime? At(string? s) => DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t : null;

        private static string Time(string? s) => At(s) is { } t ? t.ToString("HH:mm") : "";
    }
}
