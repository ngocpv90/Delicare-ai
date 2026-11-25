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
    /// BOT-1.5a (BOT_TELEGRAM §3.2 – QA / QC, tổ trưởng bếp): 🧽 Checklist ATTP – chọn bộ checklist (<c>FoodSafetyChecklist/GetAllData</c>, đang hiệu lực)
    /// → <c>FoodSafetyChecklistResult/GetRun</c> hôm nay → CHECKLIST TRONG 1 TIN (bấm từng mục ✅ / ❌, kết quả đã ghi trước đó điền sẵn) → [✔️ Hoàn tất]
    /// → <c>Record</c> (JSON) – lỗi trọng yếu giữ mẻ / cảnh báo theo quy tắc ở api. ⛔ Mẻ đang giữ: mẻ hôm nay QC HOLD / không đạt.
    /// Quyền: mục <c>#/qc-checklist-result</c>, <c>#/prod-batch</c> + api kiểm lại. Callback <c>chk:open:&lt;Id&gt;</c>, <c>chk:&lt;stt&gt;:&lt;1|0&gt;</c>, <c>chk:done</c>.
    /// </summary>
    public sealed class AttpActions(IDelicareApiClient web, UserTokenCache tokens, IChannelSender sender, ReplyContextStore replies, ConversationStore store, TimeProvider time)
        : WebActions(web, tokens, sender, replies), IBotActionProvider
    {
        public const string Kind = CallbackData.Kinds.Checklist;
        public const string Flow = "checklist";

        public sealed class Session
        {
            public Guid ChecklistId { get; set; }
            public string Title { get; set; } = string.Empty;
            public List<(Guid ItemId, string Label, bool? Passed)> Items { get; set; } = [];
            /// <summary>UI-6.45 (UI-6.30): tiêu chí trọng yếu Không đạt → mẻ bị ảnh hưởng (chọn từ mẻ hôm nay) hoặc lý do "không ảnh hưởng mẻ nào".</summary>
            public List<(Guid Id, string Label)> BatchChoices { get; set; } = [];
            public HashSet<Guid> Batches { get; set; } = [];
            public string? NoBatchReason { get; set; }
            public bool CriticalFailed => Items.Any(x => x.Passed == false && x.Label.StartsWith('❗'));
        }

        public const string NoBatchText = "Không ảnh hưởng mẻ nào (xác nhận trên bot)";

        public IEnumerable<BotAction> GetActions() =>
        [
            new BotAction { Code = "attp_checklist", Module = "ATTP", Bots = ["VH"], Label = "🧽 Checklist ATTP", Command = "checklist", CommandDescription = "Checklist an toàn thực phẩm", MenuMetaLink = "#/qc-checklist-result", Order = 1, Handler = ChecklistsAsync },
            new BotAction { Code = "attp_hold", Module = "ATTP", Bots = ["VH"], Label = "⛔ Mẻ đang giữ", Command = "giu", CommandDescription = "Mẻ đang giữ / không đạt hôm nay", MenuMetaLink = "#/prod-batch", Order = 2, Handler = HoldAsync },
        ];

        public async Task ChecklistsAsync(BotActionContext ctx, CancellationToken ct)
        {
            if (await TokenAsync(ctx, ct) is not { } token)
            {
                return;
            }
            var r = await Web.GetAsUserAsync<JsonElement>("api/FoodSafetyChecklist/GetAllData", token, ct);
            if (!r.IsOk)
            {
                await SayAsync(ctx, Problem(r), ct);
                return;
            }
            var lists = (r.ListData ?? []).Where(c => BepActions.S(c, "status") is null or "Active").Take(10).ToList();
            await ShowAsync(ctx, new NeutralMessage
            {
                Title = "🧽 Chọn checklist",
                Lines = lists.Count == 0 ? ["Chưa có checklist đang hiệu lực."] : [],
                Buttons = [.. lists.Where(c => Guid.TryParse(BepActions.S(c, "id"), out _)).Select(c => new NeutralButton(BepActions.S(c, "name") ?? BepActions.S(c, "code") ?? "Checklist",
                    CallbackData.Build(Kind, "open", Guid.Parse(BepActions.S(c, "id")!))))],
            }, ct);
        }

        public async Task HandleAsync(BotActionContext ctx, CallbackData data, CancellationToken ct)
        {
            if (data.Action == "open" && data.Guid is { } checklistId)
            {
                await OpenAsync(ctx, checklistId, ct);
                return;
            }
            var s = store.Get<Session>(ctx.Bot, ctx.Event.FromId, Flow);
            if (s == null)
            {
                await SayAsync(ctx, "Phiên checklist đã hết hạn – mở lại 🧽 Checklist ATTP.", ct);
                return;
            }
            if (data.Action == "done")
            {
                await SubmitAsync(ctx, s, ct);
                return;
            }
            if (data.Action == "all")
            {
                s.Items = [.. s.Items.Select(x => (x.ItemId, x.Label, x.Passed ?? true))];
                await ShowAsync(ctx, InputWidgets.Checklist(s.Title, [.. s.Items.Select(x => (x.Label, x.Passed))]), ct);
                return;
            }
            if (data.Action == "b" && int.TryParse(data.Id, out int bi) && bi >= 0 && bi < s.BatchChoices.Count)
            {
                Guid id = s.BatchChoices[bi].Id;
                if (!s.Batches.Remove(id))
                {
                    s.Batches.Add(id);
                }
                await ShowAsync(ctx, BatchPicker(s), ct);
                return;
            }
            if (data.Action == "nb")
            {
                s.Batches.Clear();
                s.NoBatchReason = NoBatchText;
                await SubmitAsync(ctx, s, ct);
                return;
            }
            if (int.TryParse(data.Action, out int index) && index >= 0 && index < s.Items.Count)
            {
                var it = s.Items[index];
                s.Items[index] = (it.ItemId, it.Label, data.Id == "1");
                await ShowAsync(ctx, InputWidgets.Checklist(s.Title, [.. s.Items.Select(x => (x.Label, x.Passed))]), ct);
            }
        }

        private async Task OpenAsync(BotActionContext ctx, Guid checklistId, CancellationToken ct)
        {
            if (await TokenAsync(ctx, ct) is not { } token)
            {
                return;
            }
            var r = await Web.GetAsUserAsync<JsonElement>($"api/FoodSafetyChecklistResult/GetRun?ChecklistId={checklistId}&CheckDate={VnTime.Today(time):yyyy-MM-dd}", token, ct);
            if (!r.IsOk || r.Data.ValueKind != JsonValueKind.Object)
            {
                await SayAsync(ctx, Problem(r), ct);
                return;
            }
            var items = r.Data.TryGetProperty("listItem", out var li) && li.ValueKind == JsonValueKind.Array ? li.EnumerateArray().ToList() : [];
            var session = new Session
            {
                ChecklistId = checklistId,
                Title = "🧽 " + (r.Data.TryGetProperty("checklist", out var c) ? BepActions.S(c, "name") : null) + " – " + VnTime.Today(time).ToString("dd/MM"),
                Items = [.. items.Take(20).Select(i =>
                {
                    var item = i.TryGetProperty("item", out var x) ? x : default;
                    var result = i.TryGetProperty("result", out var rr) ? rr : default;
                    string? code = BepActions.S(result, "resultCode");
                    bool critical = item.ValueKind == JsonValueKind.Object && item.TryGetProperty("isCritical", out var ic) && ic.ValueKind == JsonValueKind.True;
                    return (Guid.Parse(BepActions.S(item, "id")!), (critical ? "❗" : "") + (BepActions.S(item, "itemName") ?? BepActions.S(item, "itemCode") ?? "Mục"),
                        code == "PASS" ? true : code == "FAIL" ? (bool?)false : null);
                })],
            };
            if (session.Items.Count == 0)
            {
                await SayAsync(ctx, "Checklist chưa có mục nào.", ct);
                return;
            }
            store.Set(ctx.Bot, ctx.Event.FromId, Flow, session);
            await ShowAsync(ctx, InputWidgets.Checklist(session.Title, [.. session.Items.Select(x => (x.Label, x.Passed))]), ct);
        }

        private async Task SubmitAsync(BotActionContext ctx, Session s, CancellationToken ct)
        {
            if (s.Items.Any(x => x.Passed == null) || await TokenAsync(ctx, ct) is not { } token)
            {
                return;
            }
            // UI-6.45 (UI-6.30): lỗi trọng yếu → bắt buộc chọn mẻ bị ảnh hưởng hoặc xác nhận không ảnh hưởng mẻ nào (máy chủ chặn nếu thiếu).
            if (s.CriticalFailed && s.Batches.Count == 0 && s.NoBatchReason == null)
            {
                var board = await Web.GetAsUserAsync<JsonElement>("api/ProductionBatch/GetBoard?ProductionDate=" + VnTime.Today(time).ToString("yyyy-MM-dd"), token, ct);
                var list = board.IsOk && board.Data.ValueKind == JsonValueKind.Object && board.Data.TryGetProperty("listBatch", out var lb) && lb.ValueKind == JsonValueKind.Array ? lb.EnumerateArray().ToList() : [];
                s.BatchChoices = [.. list.Where(b => BepActions.S(b, "status") is not ("Cancelled" or "Planned") && Guid.TryParse(BepActions.S(b, "id"), out _)).Take(8)
                    .Select(b => (Guid.Parse(BepActions.S(b, "id")!), $"{BepActions.S(b, "batchNumber")} {BepActions.S(b, "dishName")}".Trim()))];
                await ShowAsync(ctx, BatchPicker(s), ct);
                return;
            }
            var r = await Web.PostJsonAsUserAsync<JsonElement>("api/FoodSafetyChecklistResult/Record", new
            {
                checklistId = s.ChecklistId,
                checkDate = VnTime.Today(time).ToString("yyyy-MM-dd"),
                listItem = s.Items.Select(x => new { checklistItemId = x.ItemId, resultCode = x.Passed == true ? "PASS" : "FAIL" }),
                listProductionBatchId = s.Batches.ToList(),
                noBatchReason = s.NoBatchReason,
            }, token, ct);
            if (r.IsOk)
            {
                store.Clear(ctx.Bot, ctx.Event.FromId, Flow);
            }
            int fail = s.Items.Count(x => x.Passed == false);
            await ShowAsync(ctx, new NeutralMessage
            {
                Title = r.IsOk ? "✅ Đã ghi checklist" : "⚠️ Chưa ghi được checklist",
                Lines = r.IsOk ? [$"{s.Title}: {s.Items.Count - fail} đạt, {fail} không đạt.", r.ResponseMessage ?? ""] : [Problem(r)],
            }, ct);
        }

        /// <summary>UI-6.45: chọn mẻ bị ảnh hưởng bởi lỗi trọng yếu (bấm để chọn / bỏ) – hoặc "Không ảnh hưởng mẻ nào".</summary>
        private static NeutralMessage BatchPicker(Session s)
        {
            var m = new NeutralMessage
            {
                Title = "❗ Lỗi trọng yếu – mẻ nào bị ảnh hưởng?",
                Lines = s.BatchChoices.Count == 0 ? ["Hôm nay chưa có mẻ đang nấu / đã nấu – xác nhận không ảnh hưởng mẻ nào."] : ["Chọn mẻ bị ảnh hưởng (mẻ được chọn sẽ bị giữ chờ QC xử lý)."],
            };
            for (int i = 0; i < s.BatchChoices.Count; i++)
            {
                var (id, label) = s.BatchChoices[i];
                m.Buttons.Add(new NeutralButton((s.Batches.Contains(id) ? "☑️ " : "⬜ ") + label, CallbackData.Build(Kind, "b", i.ToString(CultureInfo.InvariantCulture))));
            }
            m.Buttons.Add(new NeutralButton("🚫 Không ảnh hưởng mẻ nào", CallbackData.Build(Kind, "nb")));
            if (s.Batches.Count != 0)
            {
                m.Buttons.Add(new NeutralButton($"✔️ Gửi ({s.Batches.Count} mẻ)", CallbackData.Build(Kind, "done")));
            }
            return m;
        }

        public async Task HoldAsync(BotActionContext ctx, CancellationToken ct)
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
            var list = r.Data.TryGetProperty("listBatch", out var lb) && lb.ValueKind == JsonValueKind.Array ? lb.EnumerateArray().ToList() : [];
            var held = list.Where(b => BepActions.S(b, "qcStatus")?.ToUpperInvariant() is "HOLD" or "REJECT" || BepActions.S(b, "status") == "Failed").ToList();
            // BOT-UX-2 (mục 10): 6 mẻ / trang.
            var m = new NeutralMessage { Title = $"⛔ Mẻ đang giữ / không đạt ({held.Count})", Lines = held.Count == 0 ? ["Không có mẻ nào đang giữ hôm nay."] : [] };
            ListPager.Fill(m, ctx, "attp_hold", held, b => $"• {BepActions.S(b, "batchNumber")} {BepActions.S(b, "dishName")} · QC {BepActions.S(b, "qcStatus") ?? "-"} · {BepActions.S(b, "statusName")}",
                b => Guid.TryParse(BepActions.S(b, "id"), out var bid) ? new NeutralButton("🔍 " + BepActions.S(b, "batchNumber"), CallbackData.Build(BepActions.Kind, "view", bid)) : null);
            await ShowAsync(ctx, m, ct);
        }
    }
}
