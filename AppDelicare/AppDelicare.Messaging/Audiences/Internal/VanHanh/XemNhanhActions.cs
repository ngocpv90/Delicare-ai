using System.Globalization;
using System.Text.Json;
using AppDelicare.Messaging.Channels;
using AppDelicare.Messaging.Conversations;
using AppDelicare.Messaging.Core;
using AppDelicare.Messaging.Presentation;

namespace AppDelicare.Messaging.Audiences.Internal.VanHanh
{
    /// <summary>
    /// BOT-1.5c-2 (BOT_TELEGRAM §3.2): chức năng CHỈ XEM của Kho (OPS-SCM), Kế hoạch (OPS-PLAN), Kinh doanh (SAL-SALES), Kế toán (FIN-ACC), QA, Quản trị (FIN-IT).
    /// Mỗi chức năng = 1 API có sẵn (JWT của chính người dùng, quyền màn như web – nút ẩn khi không có mục menu) → 1 tin tóm tắt ≤ 15 dòng; muốn thao tác → mở web.
    /// Không ghi dữ liệu. Chỗ thiếu API (lọc "khách tôi phụ trách", nhiệt độ ngoài ngưỡng theo từng lần đo, PO theo khoảng ngày) – DECISIONS p29.
    /// </summary>
    public sealed class XemNhanhActions(IDelicareApiClient web, UserTokenCache tokens, IChannelSender sender, ReplyContextStore replies, TimeProvider time)
        : WebActions(web, tokens, sender, replies), IBotActionProvider
    {
        public const int MaxLines = 15;

        private DateTime Today => VnTime.Today(time);

        private static string D(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        public IEnumerable<BotAction> GetActions() =>
        [
            // Kho & mua hàng
            A("kho_low", "KHO", "📦 Tồn dưới mức", "ton", "Nguyên liệu dưới tồn tối thiểu", "#/scm-inv-balance", 1, LowStockAsync),
            A("kho_exp", "KHO", "⌛ Cận date", "candate", "Lô cận / hết hạn (7 ngày)", "#/scm-inv-balance", 2, ExpiringAsync),
            A("kho_mrp", "KHO", "🥬 NL ngày mai", "nlmai", "Nguyên liệu cần cho ngày mai + thiếu hụt", "#/scm-requirement", 3, (c, ct) => RequirementAsync(c, ct)),
            A("kho_po", "KHO", "🧾 PO sắp về / trễ", "po", "Đơn mua sắp về / trễ", "#/scm-po", 4, PoIncomingAsync),
            A("kho_gr", "KHO", "🚛 Hàng về hôm nay", "hangve", "Phiếu nhập hôm nay", "#/scm-gr", 5, ReceiptsTodayAsync),
            // Kế hoạch
            A("kh_plan", "KEHOACH", "📋 Kế hoạch ngày mai", "kehoach", "Mẻ sản xuất ngày mai", "#/prod-batch", 1, PlanTomorrowAsync),
            A("kh_menu", "KEHOACH", "📅 Thực đơn đang áp dụng", "thucdon", "Thực đơn đang áp dụng", "#/menu-menu", 2, MenusAsync),
            A("kh_waste", "KEHOACH", "♻️ Hao hụt 7 ngày", "haohut", "Suất thừa / hủy 7 ngày", "#/prod-waste", 3, WasteAsync),
            A("kh_cost", "KEHOACH", "💰 Giá thành suất", "giathanh", "Giá thành suất 7 ngày", "#/cost-meal", 4, MealCostAsync),
            // Kinh doanh
            A("kd_orders", "KINHDOANH", "📦 Đơn ngày mai", "donmai", "Đơn hàng ngày mai theo trạng thái", "#/crm-order", 1, (c, ct) => OrdersTomorrowAsync(c, false, ct)),
            A("kd_pending", "KINHDOANH", "⏰ Khách chưa chốt", "chuachot", "Đơn ngày mai chưa xác nhận", "#/crm-order", 2, (c, ct) => OrdersTomorrowAsync(c, true, ct)),
            // Kế toán
            A("kt_loss", "KETOAN", "📦 Hộp khay thất thoát", "thatthoat", "Hộp khay thất thoát vượt ngưỡng", "#/log-packaging-ledger", 1, PackagingLossAsync),
            A("kt_po", "KETOAN", "🧾 PO đã nhận", "podanhan", "Đơn mua đã nhận hàng", "#/scm-po", 2, PoReceivedAsync),
            // QA
            A("qa_incoming", "ATTP", "🧪 Lô chờ QC đầu vào", "chokiem", "Phiếu nhập chờ kiểm QC", "#/qc-inspection", 3, QcPendingAsync),
            A("qa_alerts", "ATTP", "🌡 Cảnh báo ATTP", "canhbao", "Nhiệt độ ngoài ngưỡng, lô giữ / cận date", "#/qc-temperature", 4, (c, ct) => AlertsAsync(c, "🌡 Cảnh báo ATTP", ["TEMPERATURE_OUT_OF_RANGE", "QC_INCOMING_PENDING", "LOT_ON_HOLD", "LOT_EXPIRING"], ct)),
            // Quản trị
            new BotAction { Code = "it_status", Module = "QUANTRI", Label = "🖥 Tình trạng hệ thống", Command = "hethong", CommandDescription = "Job tự động + tin gửi lỗi", Bots = ["VH"], Order = 1, Visible = i => i.IsAdmin, Handler = SystemAsync },
        ];

        private static BotAction A(string code, string module, string label, string command, string description, string menu, int order, Func<BotActionContext, CancellationToken, Task> handler)
            => new() { Code = code, Module = module, Label = label, Command = command, CommandDescription = description, Bots = ["VH"], MenuMetaLink = menu, Order = order, Handler = handler };

        /// <summary>GET bằng JWT của người dùng; lỗi → báo và trả null.</summary>
        private async Task<ApiData?> GetAsync(BotActionContext ctx, string path, CancellationToken ct)
        {
            if (await TokenAsync(ctx, ct) is not { } token)
            {
                return null;
            }
            var r = await Web.GetAsUserAsync<JsonElement>(path, token, ct);
            if (!r.IsOk)
            {
                await SayAsync(ctx, Problem(r), ct);
                return null;
            }
            return new ApiData(r.Data, r.ListData ?? [], r.TotalRecords);
        }

        private sealed record ApiData(JsonElement Data, List<JsonElement> ListData, int Total)
        {
            /// <summary>Danh sách: listData (Results) / data là mảng / data.listItem / data.listData.</summary>
            public List<JsonElement> Items => ListData.Count > 0 ? ListData
                : Data.ValueKind == JsonValueKind.Array ? [.. Data.EnumerateArray()]
                : GiaoVanActions.Arr(Data, "listItem") is { Count: > 0 } a ? a : GiaoVanActions.Arr(Data, "listData");
        }

        private Task ReplyListAsync(BotActionContext ctx, string title, IReadOnlyCollection<string> lines, string empty, CancellationToken ct, string? footer = null)
        {
            var m = new NeutralMessage { Title = $"{title} ({lines.Count})", Lines = lines.Count == 0 ? [empty] : [.. lines.Take(MaxLines)] };
            if (lines.Count > MaxLines)
            {
                m.Lines.Add($"… và {lines.Count - MaxLines} dòng nữa – xem đủ trên web.");
            }
            if (footer != null)
            {
                m.Lines.Add(footer);
            }
            return SendAsync(ctx, m, ct);
        }

        // ---------- Kho ----------

        public async Task LowStockAsync(BotActionContext ctx, CancellationToken ct)
        {
            if (await GetAsync(ctx, "api/InventoryBalance/GetSummary?belowMin=true", ct) is not { } r)
            {
                return;
            }
            var lines = r.Items.Select(i => $"• {S(i, "ingredientName")}: còn {N(i, "availableQuantity")} {S(i, "unitName")} / tối thiểu {N(i, "minimumStock")}").ToList();
            await ReplyListAsync(ctx, "📦 Tồn dưới mức", lines, "Không có nguyên liệu nào dưới tồn tối thiểu.", ct);
        }

        public async Task ExpiringAsync(BotActionContext ctx, CancellationToken ct)
        {
            if (await GetAsync(ctx, "api/InventoryBalance/GetByLot?expiringWithinDays=7", ct) is not { } r)
            {
                return;
            }
            var lines = r.Items.Where(i => Flag(i, "isExpired") || Flag(i, "isExpiring")).OrderBy(i => Dec(i, "daysToExpiry"))
                .Select(i => $"{(Flag(i, "isExpired") ? "⛔" : "⌛")} {S(i, "lotNumber")} {S(i, "ingredientName")} · HSD {Day(S(i, "expiryDate"))} · còn {N(i, "availableQuantity")} {S(i, "unitName")}").ToList();
            await ReplyListAsync(ctx, "⌛ Lô cận / hết hạn (7 ngày)", lines, "Không có lô nào cận hạn.", ct);
        }

        public async Task RequirementAsync(BotActionContext ctx, CancellationToken ct)
        {
            DateTime day = Today.AddDays(1);
            if (await GetAsync(ctx, "api/MaterialRequirement/GetByDate?productionDate=" + D(day), ct) is not { } r)
            {
                return;
            }
            var items = r.Items.OrderByDescending(i => Dec(i, "shortageQuantity")).ToList();
            var lines = items.Select(i => (Dec(i, "shortageQuantity") > 0 ? "⚠️ " : "• ") + $"{S(i, "ingredientName")}: cần {N(i, "requiredQuantity")} {S(i, "unitName")} · có {N(i, "availableQuantity")}"
                + (Dec(i, "shortageQuantity") > 0 ? $" · thiếu {N(i, "shortageQuantity")}" : "")).ToList();
            string head = $"Kế hoạch {S(r.Data, "planNumber") ?? "-"} · {N(r.Data, "totalMeals")} suất · thiếu {N(r.Data, "shortageCount")} NL";
            await ReplyListAsync(ctx, $"🥬 NL ngày {day:dd/MM}", [head, .. lines], "Chưa tính nhu cầu nguyên liệu cho ngày mai.", ct);
        }

        private async Task<List<JsonElement>?> PurchaseOrdersAsync(BotActionContext ctx, string[] statuses, CancellationToken ct)
        {
            var all = new List<JsonElement>();
            foreach (string s in statuses)
            {
                if (await GetAsync(ctx, "api/PurchaseOrder/GetAll?page=1&pageSize=100&status=" + s, ct) is not { } r)
                {
                    return null;
                }
                all.AddRange(r.Items);
            }
            return all;
        }

        public async Task PoIncomingAsync(BotActionContext ctx, CancellationToken ct)
        {
            if (await PurchaseOrdersAsync(ctx, ["Approved", "Sent", "PartiallyReceived"], ct) is not { } pos)
            {
                return;
            }
            DateTime soon = Today.AddDays(2);
            var lines = pos.Select(p => (P: p, Due: Date(S(p, "expectedDeliveryDate")))).Where(x => x.Due is { } d && d.Date <= soon).OrderBy(x => x.Due)
                .Select(x => $"{(x.Due!.Value.Date < Today ? "🔴 trễ" : x.Due.Value.Date == Today ? "🟡 hôm nay" : "⏳")} {S(x.P, "purchaseOrderNumber")} {S(x.P, "supplierName")} · hẹn {x.Due:dd/MM}").ToList();
            await ReplyListAsync(ctx, "🧾 PO sắp về / trễ", lines, "Không có PO nào trễ hoặc hẹn về trong 2 ngày.", ct);
        }

        public async Task ReceiptsTodayAsync(BotActionContext ctx, CancellationToken ct)
        {
            if (await GetAsync(ctx, "api/GoodsReceipt/GetAll?page=1&pageSize=100", ct) is not { } r)
            {
                return;
            }
            var lines = r.Items.Where(g => Date(S(g, "receiptDate"))?.Date == Today)
                .Select(g => $"• {S(g, "receiptNumber")} {S(g, "supplierName")} · {S(g, "warehouseName")} · {S(g, "status")}").ToList();
            await ReplyListAsync(ctx, "🚛 Hàng về hôm nay", lines, "Chưa có phiếu nhập nào hôm nay.", ct);
        }

        // ---------- Kế hoạch ----------

        public async Task PlanTomorrowAsync(BotActionContext ctx, CancellationToken ct)
        {
            DateTime day = Today.AddDays(1);
            if (await GetAsync(ctx, "api/ProductionBatch/GetBoard?ProductionDate=" + D(day), ct) is not { } r)
            {
                return;
            }
            var batches = GiaoVanActions.Arr(r.Data, "listBatch");
            var lines = batches.OrderBy(b => S(b, "plannedStart"))
                .Select(b => $"• {S(b, "batchNumber")} {S(b, "dishName")} · {S(b, "shiftCode")} · {N(b, "plannedQuantity")} suất · {S(b, "statusName")}").ToList();
            await ReplyListAsync(ctx, $"📋 Kế hoạch {day:dd/MM}", lines, "Chưa có mẻ sản xuất cho ngày mai.", ct, $"Tổng kế hoạch: {N(r.Data, "plannedPortions")} suất");
        }

        public async Task MenusAsync(BotActionContext ctx, CancellationToken ct)
        {
            if (await GetAsync(ctx, "api/Menu/GetList?effectiveOn=" + D(Today), ct) is not { } r)
            {
                return;
            }
            var lines = r.Items.Select(m => $"• {S(Obj(m, "menu"), "name") ?? S(Obj(m, "menu"), "code")} · {S(m, "statusName")} · {S(m, "dishCount") ?? S(m, "lineCount") ?? "0"} món · {S(m, "assignedCustomerCount") ?? "0"} khách").ToList();
            await ReplyListAsync(ctx, "📅 Thực đơn áp dụng hôm nay", lines, "Không có thực đơn nào đang áp dụng.", ct);
        }

        public async Task WasteAsync(BotActionContext ctx, CancellationToken ct)
        {
            if (await GetAsync(ctx, $"api/MealWasteRecord/GetSummary?from={D(Today.AddDays(-7))}&to={D(Today)}", ct) is not { } r)
            {
                return;
            }
            var lines = r.Items.OrderByDescending(i => Dec(i, "cost")).Select(i => $"• {S(i, "label")}: {N(i, "quantityMeals")} suất · {N(i, "quantityKg")} kg · {Money(i, "cost")}").ToList();
            await ReplyListAsync(ctx, "♻️ Hao hụt 7 ngày", lines, "Chưa ghi hao hụt nào trong 7 ngày.", ct,
                $"Tổng: {N(r.Data, "totalMeals")} suất · {N(r.Data, "totalKg")} kg · {Money(r.Data, "totalCost")}");
        }

        public async Task MealCostAsync(BotActionContext ctx, CancellationToken ct)
        {
            if (await GetAsync(ctx, $"api/MealCost/GetList?from={D(Today.AddDays(-7))}&to={D(Today)}", ct) is not { } r)
            {
                return;
            }
            var lines = r.Items.OrderByDescending(i => S(i, "mealDate")).Select(i => $"• {Day(S(i, "mealDate"))} {S(i, "orderNumber")} · {N(i, "acceptedMeals")} suất · {Money(i, "costPerMeal")}/suất"
                + (S(i, "grossMarginPercent") is { } gm ? $" · LN gộp {Num(gm)}%" : "")).ToList();
            await ReplyListAsync(ctx, "💰 Giá thành suất 7 ngày", lines, "Chưa có số liệu giá thành 7 ngày qua.", ct);
        }

        // ---------- Kinh doanh ----------

        public async Task OrdersTomorrowAsync(BotActionContext ctx, bool pendingOnly, CancellationToken ct)
        {
            DateTime day = Today.AddDays(1);
            if (await GetAsync(ctx, "api/Order/GetBoard?mealDate=" + D(day), ct) is not { } r)
            {
                return;
            }
            var columns = GiaoVanActions.Arr(r.Data, "listColumn");
            if (pendingOnly)
            {
                var items = columns.Where(c => S(c, "status") is "Draft" or "Submitted").SelectMany(c => GiaoVanActions.Arr(c, "listItem").Select(i => (Col: c, Item: i)))
                    .Select(x => $"⏰ {S(x.Item, "orderNumber")} {S(x.Item, "customerName")} · {N(x.Item, "totalQuantity")} suất · {S(x.Col, "statusName")}").ToList();
                await ReplyListAsync(ctx, $"⏰ Chưa chốt – ngày {day:dd/MM}", items, "Mọi đơn ngày mai đã chốt.", ct);
                return;
            }
            var lines = columns.Where(c => Dec(c, "count") > 0).Select(c => $"• {S(c, "statusName")}: {S(c, "count")} đơn · {N(c, "totalQuantity")} suất").ToList();
            await ReplyListAsync(ctx, $"📦 Đơn ngày {day:dd/MM}", lines, "Chưa có đơn nào cho ngày mai.", ct);
        }

        // ---------- Kế toán ----------

        public async Task PackagingLossAsync(BotActionContext ctx, CancellationToken ct)
        {
            if (await GetAsync(ctx, "api/PackagingLedger/GetBalance?overThresholdOnly=true", ct) is not { } r)
            {
                return;
            }
            var lines = r.Items.Select(i => $"• {S(i, "customerName")} · {S(i, "packagingType")}: mất {N(i, "lossQuantity")} ({Num(S(i, "lossRatePct") ?? "0")}% > {Num(S(i, "lossThresholdPct") ?? "0")}%) · còn nợ {N(i, "outstanding")}").ToList();
            await ReplyListAsync(ctx, "📦 Hộp khay thất thoát vượt ngưỡng", lines, "Không có khách nào thất thoát vượt ngưỡng.", ct, $"Tổng hộp khay khách còn giữ: {N(r.Data, "totalOutstanding")}");
        }

        public async Task PoReceivedAsync(BotActionContext ctx, CancellationToken ct)
        {
            if (await PurchaseOrdersAsync(ctx, ["Received", "PartiallyReceived"], ct) is not { } pos)
            {
                return;
            }
            var lines = pos.OrderByDescending(p => S(p, "expectedDeliveryDate")).Select(p => $"• {S(p, "purchaseOrderNumber")} {S(p, "supplierName")} · {Money(p, "totalAmount")} · {(S(p, "status") == "Received" ? "đã nhận đủ" : "nhận một phần")}").ToList();
            await ReplyListAsync(ctx, "🧾 PO đã nhận", lines, "Chưa có PO nào đã nhận hàng.", ct);
        }

        // ---------- QA ----------

        public async Task QcPendingAsync(BotActionContext ctx, CancellationToken ct)
        {
            if (await GetAsync(ctx, "api/GoodsReceipt/GetAll?page=1&pageSize=100&status=QCPending", ct) is not { } r)
            {
                return;
            }
            var lines = r.Items.Select(g => $"🧪 {S(g, "receiptNumber")} {S(g, "supplierName")} · {Day(S(g, "receiptDate"))} · {S(g, "warehouseName")}").ToList();
            await ReplyListAsync(ctx, "🧪 Phiếu nhập chờ kiểm QC", lines, "Không có lô nào chờ kiểm.", ct);
        }

        /// <summary>Chuông cảnh báo (<c>Common/GetAlerts</c> – chỉ màn người dùng có quyền xem) lọc theo mã.</summary>
        public async Task AlertsAsync(BotActionContext ctx, string title, string[] codes, CancellationToken ct)
        {
            if (await GetAsync(ctx, "api/Common/GetAlerts", ct) is not { } r)
            {
                return;
            }
            var lines = r.Items.Where(a => codes.Contains(S(a, "code")))
                .Select(a => $"{(S(a, "severity") is "High" or "Critical" ? "🔴" : "🟡")} {S(a, "title")}: {S(a, "count")}{(S(a, "detail") is { } d ? " – " + d : "")}").ToList();
            await ReplyListAsync(ctx, title, lines, "Không có cảnh báo nào.", ct);
        }

        // ---------- Quản trị ----------

        public async Task SystemAsync(BotActionContext ctx, CancellationToken ct)
        {
            if (await GetAsync(ctx, "api/Job/GetAll", ct) is not { } jobs || await GetAsync(ctx, "api/OutboundMessage/GetAll?page=1&pageSize=1&messageStatus=Failed", ct) is not { } failed)
            {
                return;
            }
            var lines = jobs.Items.Select(j =>
            {
                var last = Obj(j, "lastRun");
                string status = S(last, "runStatus") ?? "chưa chạy";
                string icon = status == "Failed" ? "🔴" : status == "Succeeded" || status == "Success" ? "✅" : "•";
                return $"{icon} {S(j, "jobName") ?? S(j, "jobCode")} · {status}{(S(last, "startedAt") is { } at ? " " + Time(at) : "")}{(Flag(j, "enabled") ? "" : " · tắt")}";
            }).ToList();
            await ReplyListAsync(ctx, "🖥 Job tự động", lines, "Chưa có job nào.", ct, $"Tin gửi lỗi (Failed): {failed.Total}");
        }

        // ---------- Đọc JSON ----------

        private static JsonElement Obj(JsonElement e, string k) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(k, out var v) ? v : default;

        private static string? S(JsonElement e, string k) => BepActions.S(e, k);

        private static bool Flag(JsonElement e, string k) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.True;

        private static decimal Dec(JsonElement e, string k) => decimal.TryParse(S(e, k), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : 0m;

        private static string N(JsonElement e, string k) => Dec(e, k).ToString("#,0.##", CultureInfo.InvariantCulture);

        private static string Num(string s) => decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d.ToString("0.#", CultureInfo.InvariantCulture) : s;

        private static string Money(JsonElement e, string k) => Dec(e, k).ToString("#,0", CultureInfo.InvariantCulture) + " đ";

        private static DateTime? Date(string? s) => DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

        private static string Day(string? s) => Date(s) is { } d ? d.ToString("dd/MM") : "-";

        private static string Time(string? s) => Date(s) is { } d ? d.ToString("HH:mm dd/MM") : "";
    }
}
