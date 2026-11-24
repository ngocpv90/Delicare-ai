using System.Collections.Concurrent;
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
    /// BOT-3.2 / 3.3 (D-72, UG-1 C-22): thao tác ghi bổ sung trên bot – API web có sẵn, JWT của chính người dùng (quyền như web, api kiểm lại khi ghi).
    /// <list type="bullet">
    /// <item>📋 Kế hoạch: 🔄 Lập lại kế hoạch ngày mai (<c>ProductionPlan/Generate</c>, xác nhận trước khi chạy).</item>
    /// <item>📋 Kế hoạch / 💼 Kinh doanh: ✏️ Đề xuất đổi món (<c>Dish/GetAll</c> tìm món cũ / mới → ngày ăn → lý do → <c>MenuChangeRequest/Create</c>).</item>
    /// <item>🍳 Bếp / 🧽 ATTP: 🧼 Lô vệ sinh hộp khay – lô đang vệ sinh, ✅ Đạt / ❌ Không đạt (<c>CleaningBatch/Complete</c>; còn bước chưa ghi → máy chủ báo bước nào).</item>
    /// <item>👥 Nhân sự: 📥 Nhập file máy chấm công – gửi file Excel trả lời tin bot → <c>ImportExcel/Import</c> loại AttendanceMachine.</item>
    /// </list>
    /// Callback <c>op:&lt;hành động&gt;:&lt;id&gt;</c>; lựa chọn trong danh sách mang số thứ tự (danh sách giữ theo chat) để callback ≤ 64 byte.
    /// </summary>
    public sealed class ThaoTacActions(IDelicareApiClient web, UserTokenCache tokens, IChannelSender sender, ReplyContextStore replies, TimeProvider time, ReasonCatalog reasons)
        : WebActions(web, tokens, sender, replies), IBotActionProvider
    {
        public const string Kind = CallbackData.Kinds.Op;
        public const string ReplyDishFrom = "op-dish-from";
        public const string ReplyDishTo = "op-dish-to";
        public const string ReplyChangeReason = "op-change-reason";
        public const string ReplyCleanReject = "op-clean-reject";
        public const string ReplyAttendanceFile = "op-mcc-file";
        public const int MaxFileBytes = 5 * 1024 * 1024;

        private sealed class ChangeDraft
        {
            public List<(Guid Id, string Name)> Options { get; set; } = [];
            public (Guid Id, string Name)? From { get; set; }
            public (Guid Id, string Name)? To { get; set; }
            public DateTime? MealDate { get; set; }
        }

        private readonly ConcurrentDictionary<string, ChangeDraft> _drafts = new();
        private readonly ConcurrentDictionary<string, List<(Guid Id, string Label)>> _cleaning = new();

        private DateTime Today => VnTime.Today(time);

        public IEnumerable<BotAction> GetActions() =>
        [
            new BotAction { Code = "kh_regen", Module = "KEHOACH", Label = "🔄 Lập lại kế hoạch", Command = "laplai", CommandDescription = "Lập lại kế hoạch sản xuất ngày mai", Bots = ["VH"], MenuMetaLink = "#/prod-plan", Order = 5, Handler = RegenerateAskAsync },
            new BotAction { Code = "kh_change", Module = "KEHOACH", Label = "✏️ Đề xuất đổi món", Command = "doimon", CommandDescription = "Đề xuất đổi món trong thực đơn", Bots = ["VH"], MenuMetaLink = "#/menu-change-request", Order = 6, Handler = ChangeStartAsync },
            // BOT-UX-2 (mục 14): mỗi chức năng 1 phân hệ chính – bỏ bản trùng "🔄 Đổi món – ý kiến khách" (Kinh doanh, cùng luồng + cùng quyền với ✏️ Đề xuất đổi món)
            // và "🧼 Lô vệ sinh hộp khay" ở Bếp (giữ ở ATTP / QC, lệnh /vesinh chuyển theo).
            new BotAction { Code = "qa_cleaning", Module = "ATTP", Label = "🧼 Lô vệ sinh hộp khay", Command = "vesinh", CommandDescription = "Lô vệ sinh hộp khay đang làm", Bots = ["VH"], MenuMetaLink = "#/qc-cleaning", Order = 6, Handler = CleaningAsync },
            new BotAction { Code = "ns_import", Module = "NHANSU", Label = "📥 Nhập file máy chấm công", Command = "nhapmcc", CommandDescription = "Gửi file Excel dữ liệu máy chấm công", Bots = ["VH"], MenuMetaLink = "#/hr-attendance", Order = 5, Handler = AttendanceAskAsync },
        ];

        // ------------------------------------------------------------ Lập lại kế hoạch
        public Task RegenerateAskAsync(BotActionContext ctx, CancellationToken ct)
        {
            DateTime day = Today.AddDays(1);
            return ShowAsync(ctx, new NeutralMessage
            {
                Title = $"🔄 Lập lại kế hoạch sản xuất {day:dd/MM}",
                Lines = ["Tính lại kế hoạch ngày mai theo đơn đã chốt (kế hoạch Nháp được làm lại; kế hoạch đã duyệt / phát hành máy chủ sẽ báo không lập lại được).", "Chắc chắn lập lại?"],
                Buttons = [new NeutralButton("✅ Lập lại", CallbackData.Build(Kind, "pg", day.ToString("yyyyMMdd"))), new NeutralButton("Hủy", CallbackData.Build(Kind, "x"))],
            }, ct);
        }

        private async Task RegenerateAsync(BotActionContext ctx, string day, CancellationToken ct)
        {
            if (!DateTime.TryParseExact(day, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) || await TokenAsync(ctx, ct) is not { } token)
            {
                return;
            }
            var r = await Web.PostFormAsUserAsync<JsonElement>("api/ProductionPlan/Generate", [new("ProductionDate", d.ToString("yyyy-MM-dd")), new("Note", "Lập lại qua Telegram")], token, ct);
            if (!r.IsOk)
            {
                await SayAsync(ctx, "⚠️ " + Problem(r), ct);
                return;
            }
            var plan = r.Data.ValueKind == JsonValueKind.Object && r.Data.TryGetProperty("plan", out var p) ? p : r.Data;
            var lines = new List<string> { $"Kế hoạch {S(plan, "planNumber") ?? S(plan, "code") ?? "-"} · {S(r.Data, "statusName") ?? S(plan, "status")}" };
            lines.AddRange(GiaoVanActions.Arr(r.Data, "listWarning").Take(5).Select(w => "⚠️ " + (w.ValueKind == JsonValueKind.String ? w.GetString() : w.ToString())));
            await SendAsync(ctx, new NeutralMessage { Title = "✅ " + (r.ResponseMessage ?? "Đã lập lại kế hoạch"), Lines = lines }, ct);
        }

        // ------------------------------------------------------------ Đề xuất đổi món
        public Task ChangeStartAsync(BotActionContext ctx, CancellationToken ct)
        {
            _drafts[ctx.Event.ChatId] = new ChangeDraft();
            return AskAsync(ctx, "✏️ Món CẦN ĐỔI – gõ tên món (≥ 2 ký tự):", ReplyDishFrom, "x", ct);
        }

        private async Task DishOptionsAsync(BotActionContext ctx, string search, string pick, string flow, CancellationToken ct)
        {
            if (await TokenAsync(ctx, ct) is not { } token)
            {
                return;
            }
            var r = await Web.GetAsUserAsync<JsonElement>("api/Dish/GetAll?Page=1&PageSize=8&SearchText=" + Uri.EscapeDataString(search), token, ct);
            var list = (r.ListData ?? []).Where(d => Guid.TryParse(S(d, "id"), out _)).Select(d => (Guid.Parse(S(d, "id")!), S(d, "name") ?? S(d, "code") ?? "?")).ToList();
            if (!r.IsOk || list.Count == 0)
            {
                await AskAsync(ctx, r.IsOk ? $"Không tìm thấy món \"{search}\" – gõ lại tên món:" : Problem(r), flow, "x", ct);
                return;
            }
            var draft = _drafts.GetOrAdd(ctx.Event.ChatId, _ => new ChangeDraft());
            draft.Options = list;
            await SendAsync(ctx, new NeutralMessage
            {
                Title = flow == ReplyDishFrom ? "Chọn món cần đổi" : "Chọn món thay",
                Buttons = [.. list.Select((d, i) => new NeutralButton("🍲 " + d.Item2, CallbackData.Build(Kind, pick, i.ToString(CultureInfo.InvariantCulture))))],
            }, ct);
        }

        private async Task PickDishAsync(BotActionContext ctx, bool from, int index, CancellationToken ct)
        {
            if (!_drafts.TryGetValue(ctx.Event.ChatId, out var draft) || index < 0 || index >= draft.Options.Count)
            {
                await SayAsync(ctx, "Phiên đổi món đã hết hạn – bấm lại ✏️ Đề xuất đổi món.", ct);
                return;
            }
            if (from)
            {
                draft.From = draft.Options[index];
                await AskAsync(ctx, $"Đổi \"{draft.From.Value.Name}\" sang món nào? Gõ tên món thay:", ReplyDishTo, "x", ct);
                return;
            }
            draft.To = draft.Options[index];
            var days = Enumerable.Range(1, 3).Select(i => Today.AddDays(i)).ToList();
            await ShowAsync(ctx, new NeutralMessage
            {
                Title = $"✏️ {draft.From?.Name} → {draft.To.Value.Name}",
                Lines = ["Áp dụng cho ngày ăn:"],
                Buttons = [.. days.Select(d => new NeutralButton(d.ToString("ddd dd/MM", new CultureInfo("vi-VN")), CallbackData.Build(Kind, "cd", d.ToString("yyyyMMdd"))))],
            }, ct);
        }

        private async Task ChangeCreateAsync(BotActionContext ctx, string reason, CancellationToken ct)
        {
            if (!_drafts.TryGetValue(ctx.Event.ChatId, out var draft) || draft.From == null || draft.To == null || draft.MealDate == null)
            {
                await SayAsync(ctx, "Phiên đổi món đã hết hạn – bấm lại ✏️ Đề xuất đổi món.", ct);
                return;
            }
            if (await TokenAsync(ctx, ct) is not { } token)
            {
                return;
            }
            var r = await Web.PostFormAsUserAsync<JsonElement>("api/MenuChangeRequest/Create",
            [
                new("OriginalDishId", draft.From.Value.Id.ToString()), new("RequestedDishId", draft.To.Value.Id.ToString()),
                new("MealDate", draft.MealDate.Value.ToString("yyyy-MM-dd")), new("Reason", reason[..Math.Min(reason.Length, 500)]),
            ], token, ct);
            if (!r.IsOk)
            {
                await SayAsync(ctx, "⚠️ " + Problem(r), ct);
                return;
            }
            _drafts.TryRemove(ctx.Event.ChatId, out _);
            await SendAsync(ctx, new NeutralMessage
            {
                Title = "✅ " + (r.ResponseMessage ?? "Đã gửi đề xuất đổi món"),
                Lines = [$"{draft.From.Value.Name} → {draft.To.Value.Name} · ngày ăn {draft.MealDate:dd/MM}", "Đề xuất chờ duyệt (⏳ Chờ tôi duyệt của người duyệt thực đơn)."],
            }, ct);
        }

        // ------------------------------------------------------------ Lô vệ sinh hộp khay
        public async Task CleaningAsync(BotActionContext ctx, CancellationToken ct)
        {
            if (await TokenAsync(ctx, ct) is not { } token)
            {
                return;
            }
            var r = await Web.GetAsUserAsync<JsonElement>("api/CleaningBatch/GetAll?Status=InProgress&Page=1&PageSize=20", token, ct);
            if (!r.IsOk)
            {
                await SayAsync(ctx, Problem(r), ct);
                return;
            }
            var list = (r.ListData ?? []).Where(b => Guid.TryParse(S(b, "id"), out _))
                .Select(b => (Id: Guid.Parse(S(b, "id")!), Label: $"{S(b, "cleaningBatchNumber") ?? S(b, "cleaningNo")} · {S(b, "containerQuantity") ?? "?"} khay")).ToList();
            _cleaning[ctx.Event.ChatId] = list;
            var buttons = new List<NeutralButton>();
            for (int i = 0; i < Math.Min(list.Count, 6); i++)
            {
                buttons.Add(new NeutralButton("✅ Đạt " + list[i].Label.Split(' ')[0], CallbackData.Build(Kind, "cp", i.ToString(CultureInfo.InvariantCulture))));
                buttons.Add(new NeutralButton("❌ Không đạt " + list[i].Label.Split(' ')[0], CallbackData.Build(Kind, "cr", i.ToString(CultureInfo.InvariantCulture))));
            }
            await ShowAsync(ctx, new NeutralMessage
            {
                Title = $"🧼 Lô vệ sinh đang làm ({list.Count})",
                Lines = list.Count == 0 ? ["Không có lô vệ sinh nào đang làm."] : [.. list.Select(x => "• " + x.Label), "Ghi từng bước (hóa chất, nhiệt độ) trên web hoặc QR CLEAN; ✅ Đạt khi mọi bước đã đạt."],
                Buttons = buttons,
            }, ct);
        }

        private async Task CleaningCompleteAsync(BotActionContext ctx, int index, bool pass, string? note, CancellationToken ct)
        {
            if (!_cleaning.TryGetValue(ctx.Event.ChatId, out var list) || index < 0 || index >= list.Count)
            {
                await SayAsync(ctx, "Danh sách đã hết hạn – bấm lại 🧼 Lô vệ sinh hộp khay.", ct);
                return;
            }
            if (await TokenAsync(ctx, ct) is not { } token)
            {
                return;
            }
            var form = new List<KeyValuePair<string, string>> { new("Id", list[index].Id.ToString()), new("QcResult", pass ? "PASS" : "REJECT") };
            if (note != null)
            {
                form.Add(new("Note", note[..Math.Min(note.Length, 2000)]));
            }
            var r = await Web.PostFormAsUserAsync<JsonElement>("api/CleaningBatch/Complete", form, token, ct);
            await SayAsync(ctx, r.IsOk ? $"✅ {r.ResponseMessage ?? "Đã kết luận"} – {list[index].Label}: {(pass ? "Đạt, sẵn sàng dùng lại" : "Không đạt")}" : "⚠️ " + Problem(r), ct);
        }

        // ------------------------------------------------------------ Nhập file máy chấm công
        public Task AttendanceAskAsync(BotActionContext ctx, CancellationToken ct)
            => AskAsync(ctx, "📥 Gửi file Excel (.xlsx) dữ liệu máy chấm công – TRẢ LỜI tin này bằng file.\nCột: Mã nhân viên*, Thời điểm* (yyyy-MM-dd HH:mm), Loại (Vào / Ra – trống = tự xác định), Mã máy. File mẫu: web › Nhập dữ liệu gốc (Excel).",
                ReplyAttendanceFile, "x", ct);

        private async Task AttendanceFileAsync(BotActionContext ctx, CancellationToken ct)
        {
            if (ctx.Event.FileId is not { } fileId)
            {
                await AskAsync(ctx, "Chưa thấy file – trả lời tin này bằng file Excel (.xlsx):", ReplyAttendanceFile, "x", ct);
                return;
            }
            string name = ctx.Event.FileName ?? "maychamcong.xlsx";
            if (!name.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) && !name.EndsWith(".xlsm", StringComparison.OrdinalIgnoreCase))
            {
                await AskAsync(ctx, "Chỉ nhận file .xlsx / .xlsm – gửi lại:", ReplyAttendanceFile, "x", ct);
                return;
            }
            if (await TokenAsync(ctx, ct) is not { } token)
            {
                return;
            }
            byte[]? bytes = await Sender.DownloadFileAsync(ctx.Bot, fileId, ct);
            if (bytes == null || bytes.Length == 0)
            {
                await SayAsync(ctx, "⚠️ Không tải được file từ Telegram – gửi lại hoặc nhập trên web.", ct);
                return;
            }
            if (bytes.Length > MaxFileBytes)
            {
                await SayAsync(ctx, "⚠️ File quá 5 MB – chia nhỏ hoặc nhập trên web.", ct);
                return;
            }
            var r = await Web.PostFileAsUserAsync<JsonElement>("api/ImportExcel/Import", [new("TableName", "AttendanceMachine")], bytes, name, token, ct);
            if (!r.IsOk)
            {
                await SayAsync(ctx, "⚠️ " + Problem(r), ct);
                return;
            }
            var lines = new List<string> { $"Đọc {S(r.Data, "totalRows") ?? "0"} dòng · thêm {S(r.Data, "inserted") ?? "0"} · bỏ qua (đã có) {S(r.Data, "skipped") ?? "0"} · lỗi {GiaoVanActions.Arr(r.Data, "listError").Count}" };
            lines.AddRange(GiaoVanActions.Arr(r.Data, "listError").Take(10).Select(e => $"⚠️ Dòng {S(e, "row")}: {S(e, "message")}"));
            await SendAsync(ctx, new NeutralMessage { Title = "📥 " + (r.ResponseMessage ?? "Đã nhập dữ liệu máy chấm công"), Lines = lines }, ct);
        }

        // ------------------------------------------------------------ Nút / trả lời
        public async Task HandleAsync(BotActionContext ctx, CallbackData data, CancellationToken ct)
        {
            string id = data.Id ?? string.Empty;
            int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index);
            switch (data.Action)
            {
                case "pg":
                    await RegenerateAsync(ctx, id, ct);
                    break;
                case "x":
                    await SayAsync(ctx, "Đã hủy.", ct);
                    break;
                case "df":
                    await PickDishAsync(ctx, true, index, ct);
                    break;
                case "dt":
                    await PickDishAsync(ctx, false, index, ct);
                    break;
                case "cd" when DateTime.TryParseExact(id, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day):
                    if (_drafts.TryGetValue(ctx.Event.ChatId, out var draft))
                    {
                        draft.MealDate = day;
                        // BOT-UX-2 (mục 7): lý do = nút lý do hay gặp (Messaging:Reasons:op-change-reason) + [✏️ Khác…].
                        await ShowAsync(ctx, new NeutralMessage { Title = "Lý do đổi món", Buttons = reasons.Buttons(ReasonCatalog.DishChange, CallbackData.Kinds.Op, "chr", "chx", "k") }, ct);
                    }
                    else
                    {
                        await SayAsync(ctx, "Phiên đổi món đã hết hạn – bấm lại ✏️ Đề xuất đổi món.", ct);
                    }
                    break;
                case "cp":
                    await CleaningCompleteAsync(ctx, index, true, "Kết luận qua Telegram", ct);
                    break;
                case "cr":
                    await ShowAsync(ctx, new NeutralMessage
                    {
                        Title = "❌ Lô vệ sinh không đạt – lý do",
                        Buttons = reasons.Buttons(ReasonCatalog.CleaningReject, CallbackData.Kinds.Op, "crr", "crx", index.ToString(CultureInfo.InvariantCulture)),
                    }, ct);
                    break;
                case "crr" when id.Split('.') is [var ci, var ri] && int.TryParse(ci, out int cleanIndex) && reasons.At(ReasonCatalog.CleaningReject, ri) is { } cleanReason:
                    await CleaningCompleteAsync(ctx, cleanIndex, false, cleanReason, ct);
                    break;
                case "crx":
                    await AskAsync(ctx, "❌ Lý do không đạt (bắt buộc):", ReplyCleanReject, index.ToString(CultureInfo.InvariantCulture), ct);
                    break;
                case "chr" when reasons.At(ReasonCatalog.DishChange, id.Split('.').Last()) is { } changeReason:
                    await ChangeCreateAsync(ctx, changeReason, ct);
                    break;
                case "chx":
                    await AskAsync(ctx, "Lý do đổi món (vd. khách đề nghị, thiếu nguyên liệu):", ReplyChangeReason, "x", ct);
                    break;
                default:
                    await SayAsync(ctx, "Nút đã hết hạn.", ct);
                    break;
            }
        }

        public async Task ReplyAsync(BotActionContext ctx, PendingReply pending, CancellationToken ct)
        {
            string text = (ctx.Event.Text ?? string.Empty).Trim();
            switch (pending.Flow)
            {
                case ReplyAttendanceFile:
                    await AttendanceFileAsync(ctx, ct);
                    return;
                case ReplyDishFrom or ReplyDishTo when text.Length < 2:
                    await AskAsync(ctx, "Gõ ít nhất 2 ký tự tên món:", pending.Flow, "x", ct);
                    return;
                case ReplyDishFrom:
                    await DishOptionsAsync(ctx, text, "df", ReplyDishFrom, ct);
                    return;
                case ReplyDishTo:
                    await DishOptionsAsync(ctx, text, "dt", ReplyDishTo, ct);
                    return;
                case ReplyChangeReason when text.Length < 3:
                case ReplyCleanReject when text.Length < 3:
                    await AskAsync(ctx, "Lý do quá ngắn – gõ lại:", pending.Flow, pending.Key, ct);
                    return;
                case ReplyChangeReason:
                    await ChangeCreateAsync(ctx, text, ct);
                    return;
                case ReplyCleanReject when int.TryParse(pending.Key, out int i):
                    await CleaningCompleteAsync(ctx, i, false, text, ct);
                    return;
            }
        }

        private static string? S(JsonElement e, string k) => BepActions.S(e, k);
    }
}
