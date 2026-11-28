using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Share;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Implement.Agent
{
    /// <summary>
    /// AI-10 (D-133 §4): công cụ ghi 2 pha của WH, KITCHEN, QA, SALES, LOG, HR. Mỗi lệnh = ĐÚNG MỘT thao tác web (cùng method Service, cùng màn + thao tác người duyệt
    /// phải có). Đề xuất chỉ kiểm nhẹ + bản xem trước (không ghi); duyệt → method chạy dưới người duyệt, mọi quy tắc của Service áp lại.
    /// </summary>
    public static partial class AgentCommandRegistry
    {
        private static IUnitOfWork Uow(IServiceProvider sp) => Svc<IUnitOfWork>(sp);

        private static IEnumerable<JsonElement> Rows(JsonElement a, string key, string label)
        {
            if (a.ValueKind != JsonValueKind.Object || !a.TryGetProperty(key, out var arr) || arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() == 0)
            {
                throw new AgentToolException($"Thiếu {label} ({key})");
            }
            return arr.EnumerateArray().ToList();
        }

        private static decimal? Dec(JsonElement e, string key) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var v)
            ? v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d) ? d
            : v.ValueKind == JsonValueKind.String && decimal.TryParse(v.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var s) ? s : null
            : null;

        private static DateTime? Day(JsonElement a, string key) => AgentToolRegistry.Str(a, key) is { } s && DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d.Date : null;

        private static string S(JsonObject p, string k) => p[k]?.GetValue<string>() ?? string.Empty;

        private static Guid G(JsonObject p, string k) => Guid.Parse(S(p, k));

        private static string Done<T>(IResult<T> r, string ok, out string? error)
        {
            error = r.ResponseCode == 200 ? null : r.ResponseMessage ?? "Không thực hiện được";
            return ok;
        }

        private static async Task<AppDelicare.Model.Entity.Warehouse> WarehouseAsync(IServiceProvider sp, JsonElement a)
        {
            var db = Uow(sp).DbContext;
            string? code = AgentToolRegistry.Str(a, "ma_kho");
            var list = await db.Warehouse.AsNoTracking().Where(w => !w.IsDeleted && (w.Status == null || w.Status == "Active")).ToListAsync();
            return (code == null ? list.Count == 1 ? list[0] : null : list.FirstOrDefault(w => string.Equals(w.Code, code, StringComparison.OrdinalIgnoreCase)))
                ?? throw new AgentToolException(code == null ? "Có nhiều kho – ghi mã kho (ma_kho)" : $"Không có kho «{code}»");
        }

        private static async Task<AppDelicare.Model.Entity.Employee> EmployeeAsync(IServiceProvider sp, JsonElement a, string key = "ma_nv")
        {
            string code = Need(a, key, "mã nhân viên");
            return await Uow(sp).DbContext.Employee.AsNoTracking().FirstOrDefaultAsync(e => !e.IsDeleted && (e.EmployeeCode == code || e.Code == code))
                ?? throw new AgentToolException($"Không có nhân viên «{code}»");
        }

        private static async Task<AppDelicare.Model.Entity.ShiftAssignment> AssignmentAsync(IServiceProvider sp, Guid employeeId, DateTime day)
        {
            DateTime next = day.AddDays(1);
            return await Uow(sp).DbContext.ShiftAssignment.AsNoTracking().Where(s => !s.IsDeleted && s.EmployeeId == employeeId && s.WorkDate >= day && s.WorkDate < next)
                .OrderBy(s => s.PlannedStart).FirstOrDefaultAsync() ?? throw new AgentToolException($"Nhân viên không có ca ngày {day:dd/MM/yyyy}");
        }

        private static IEnumerable<AgentCommandDef> Ops() =>
        [
            // ------------------------------------------------------------ WH – kho
            new AgentCommandDef
            {
                Code = "propose_goods_receipt", Title = "Lập phiếu nhập kho theo đơn mua", Agents = ["WH"], Screen = "scm-gr", Action = PermissionAction.Add,
                Description = "Đề xuất LẬP phiếu nhập (Nháp) theo 1 đơn mua đã gửi NCC: dòng nguyên liệu, số nhận, lô NCC, hạn dùng, nhiệt độ – thủ kho xác nhận (như Nhận hàng trên web, QC + ghi sổ làm sau).",
                InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"so_po\":{\"type\":\"string\",\"description\":\"Số đơn mua\"},\"ma_kho\":{\"type\":\"string\"},"
                    + "\"dong\":{\"type\":\"array\",\"items\":{\"type\":\"object\",\"properties\":{\"ma_nl\":{\"type\":\"string\"},\"so_luong\":{\"type\":\"number\"},\"so_lo_ncc\":{\"type\":\"string\"},\"han_dung\":{\"type\":\"string\"},\"nhiet_do\":{\"type\":\"number\"}},\"required\":[\"ma_nl\",\"so_luong\"]}}," + Common + "},"
                    + "\"required\":[\"so_po\",\"dong\",\"ly_do\"]}",
                PrepareAsync = async (sp, a) =>
                {
                    var db = Uow(sp).DbContext;
                    string no = Need(a, "so_po", "số đơn mua");
                    var po = await db.PurchaseOrder.AsNoTracking().FirstOrDefaultAsync(p => !p.IsDeleted && (p.PurchaseOrderNumber == no || p.PONo == no)) ?? throw new AgentToolException($"Không có đơn mua «{no}»");
                    if (!PurchaseOrderStatus.Receivable.Contains(po.Status))
                    {
                        throw new AgentToolException($"Đơn mua {no} đang {PurchaseOrderStatus.Label(po.Status)} – chưa nhận hàng được");
                    }
                    var wh = await WarehouseAsync(sp, a);
                    var poLines = await (from l in db.PurchaseOrderLine.AsNoTracking() join i in db.Ingredient.AsNoTracking() on l.IngredientId equals i.Id
                                         where l.PurchaseOrderId == po.Id && !l.IsDeleted select new { l.Id, l.Quantity, i.Code, i.Name }).ToListAsync();
                    var pv = new AgentPreview { LineHeaders = ["Nguyên liệu", "Đặt", "Nhận", "Lô NCC", "Hạn dùng"], DocumentNo = no, OpenLink = "#/scm-po-detail?id=" + po.Id };
                    var outLines = new JsonArray();
                    foreach (var r in Rows(a, "dong", "dòng nhận"))
                    {
                        string code = AgentToolRegistry.Str(r, "ma_nl") ?? throw new AgentToolException("Dòng thiếu mã nguyên liệu");
                        var pl = poLines.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase)) ?? throw new AgentToolException($"Đơn mua {no} không có nguyên liệu «{code}»");
                        decimal qty = Dec(r, "so_luong") is > 0 and var q ? q : throw new AgentToolException($"Số nhận {code} phải > 0");
                        if (qty > pl.Quantity)
                        {
                            pv.Warnings.Add($"{code}: nhận {qty:0.###} > đặt {pl.Quantity:0.###}");
                        }
                        pv.Lines.Add([$"{pl.Code} {pl.Name}", $"{pl.Quantity:0.###}", $"{qty:0.###}", AgentToolRegistry.Str(r, "so_lo_ncc"), AgentToolRegistry.Str(r, "han_dung")]);
                        outLines.Add(new JsonObject { ["poLineId"] = pl.Id.ToString(), ["qty"] = qty, ["lot"] = AgentToolRegistry.Str(r, "so_lo_ncc"), ["exp"] = Day(r, "han_dung")?.ToString("yyyy-MM-dd"), ["temp"] = Dec(r, "nhiet_do") });
                    }
                    pv.Summary = $"Phiếu nhập theo {no} vào kho {wh.Code}: {pv.Lines.Count} dòng";
                    pv.Payload = new JsonObject { ["poId"] = po.Id.ToString(), ["warehouseId"] = wh.Id.ToString(), ["lines"] = outLines, ["reason"] = AgentToolRegistry.Str(a, "ly_do") };
                    return pv;
                },
                ExecuteAsync = async (sp, p, approver, note) =>
                {
                    var r = await Svc<IGoodsReceiptService>(sp).Create(new GoodsReceiptCreateRequest
                    {
                        PurchaseOrderId = G(p, "poId"), WarehouseId = G(p, "warehouseId"), ReceiptDate = Svc<IClock>(sp).Today, Note = $"[Agent WH] {S(p, "reason")}",
                        ListLine = [.. (p["lines"] as JsonArray ?? []).OfType<JsonObject>().Select(l => new GoodsReceiptLineInput
                        {
                            PurchaseOrderLineId = G(l, "poLineId"), ReceivedQuantity = l["qty"]!.GetValue<decimal>(), SupplierLotNumber = l["lot"]?.GetValue<string?>(),
                            ExpiryDate = l["exp"]?.GetValue<string?>() is { } e ? DateTime.Parse(e, CultureInfo.InvariantCulture) : null, TemperatureC = l["temp"]?.GetValue<decimal?>(),
                        })],
                    });
                    return r.ResponseCode == 200 ? (null, $"Đã lập phiếu nhập {r.Data?.GoodsReceipt?.ReceiptNumber}") : (r.ResponseMessage ?? "Không lập được phiếu nhập", null);
                },
            },
            new AgentCommandDef
            {
                Code = "propose_issue", Title = "Lập phiếu xuất kho cho sản xuất", Agents = ["WH"], Screen = "scm-issue", Action = PermissionAction.Add,
                Description = "Đề xuất LẬP phiếu xuất kho (Nháp) cho 1 lệnh sản xuất: nguyên liệu, số lượng, lô (trống = hệ thống gợi ý FEFO) – thủ kho xác nhận, ghi sổ làm sau như web.",
                InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"so_lenh\":{\"type\":\"string\",\"description\":\"Số lệnh sản xuất\"},\"ma_kho\":{\"type\":\"string\"},"
                    + "\"dong\":{\"type\":\"array\",\"items\":{\"type\":\"object\",\"properties\":{\"ma_nl\":{\"type\":\"string\"},\"so_luong\":{\"type\":\"number\"},\"so_lo\":{\"type\":\"string\"}},\"required\":[\"ma_nl\",\"so_luong\"]}}," + Common + "},"
                    + "\"required\":[\"so_lenh\",\"dong\",\"ly_do\"]}",
                PrepareAsync = async (sp, a) =>
                {
                    var db = Uow(sp).DbContext;
                    string no = Need(a, "so_lenh", "số lệnh sản xuất");
                    var po = await db.ProductionOrder.AsNoTracking().FirstOrDefaultAsync(o => !o.IsDeleted && (o.ProductionOrderNumber == no || o.ProductionOrderNo == no)) ?? throw new AgentToolException($"Không có lệnh sản xuất «{no}»");
                    var wh = await WarehouseAsync(sp, a);
                    var pv = new AgentPreview { LineHeaders = ["Nguyên liệu", "Số lượng", "Lô"], DocumentNo = no };
                    var outLines = new JsonArray();
                    foreach (var r in Rows(a, "dong", "dòng xuất"))
                    {
                        string code = AgentToolRegistry.Str(r, "ma_nl") ?? throw new AgentToolException("Dòng thiếu mã nguyên liệu");
                        var ing = await db.Ingredient.AsNoTracking().FirstOrDefaultAsync(i => !i.IsDeleted && i.Code == code) ?? throw new AgentToolException($"Không có nguyên liệu «{code}»");
                        decimal qty = Dec(r, "so_luong") is > 0 and var q ? q : throw new AgentToolException($"Số lượng {code} phải > 0");
                        string? lotNo = AgentToolRegistry.Str(r, "so_lo");
                        Guid? lotId = lotNo == null ? null : await db.MaterialLot.AsNoTracking().Where(l => !l.IsDeleted && l.IngredientId == ing.Id && (l.LotNumber == lotNo || l.LotNo == lotNo)).Select(l => (Guid?)l.Id).FirstOrDefaultAsync()
                            ?? throw new AgentToolException($"Không có lô «{lotNo}» của {code}");
                        pv.Lines.Add([$"{ing.Code} {ing.Name}", $"{qty:0.###}", lotNo ?? "gợi ý FEFO"]);
                        outLines.Add(new JsonObject { ["ingredientId"] = ing.Id.ToString(), ["qty"] = qty, ["lotId"] = lotId?.ToString(), ["unitId"] = (ing.BaseUnitId ?? ing.BaseUomId)?.ToString() });
                    }
                    pv.Summary = $"Phiếu xuất kho {wh.Code} cho lệnh {no}: {pv.Lines.Count} dòng";
                    pv.Payload = new JsonObject { ["orderId"] = po.Id.ToString(), ["warehouseId"] = wh.Id.ToString(), ["lines"] = outLines, ["reason"] = AgentToolRegistry.Str(a, "ly_do") };
                    return pv;
                },
                ExecuteAsync = async (sp, p, approver, note) =>
                {
                    var r = await Svc<IMaterialIssueService>(sp).Create(new MaterialIssueCreateRequest
                    {
                        WarehouseId = G(p, "warehouseId"), ProductionOrderId = G(p, "orderId"), IssueDate = Svc<IClock>(sp).Today, Note = $"[Agent WH] {S(p, "reason")}",
                        ListLine = [.. (p["lines"] as JsonArray ?? []).OfType<JsonObject>().Select(l => new MaterialIssueCreateLineRequest
                        {
                            IngredientId = G(l, "ingredientId"), Quantity = l["qty"]!.GetValue<decimal>(), MaterialLotId = l["lotId"]?.GetValue<string?>() is { } x ? Guid.Parse(x) : null,
                            UnitId = l["unitId"]?.GetValue<string?>() is { } u ? Guid.Parse(u) : null,
                        })],
                    });
                    return r.ResponseCode == 200 ? (null, "Đã lập phiếu xuất kho") : (r.ResponseMessage ?? "Không lập được phiếu xuất", null);
                },
            },
            new AgentCommandDef
            {
                Code = "propose_stock_count", Title = "Lập phiếu kiểm kê", Agents = ["WH"], Screen = "scm-stockcount", Action = PermissionAction.Add,
                Description = "Đề xuất LẬP phiếu kiểm kê (Nháp) cho 1 kho – toàn kho hoặc 1 nguyên liệu (vd lệch tồn) – thủ kho xác nhận, đếm / ghi sổ trên web.",
                InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"ma_kho\":{\"type\":\"string\"},\"ma_nl\":{\"type\":\"string\",\"description\":\"Trống = toàn kho\"},\"ngay\":{\"type\":\"string\"}," + Common + "},\"required\":[\"ly_do\"]}",
                PrepareAsync = async (sp, a) =>
                {
                    var db = Uow(sp).DbContext;
                    var wh = await WarehouseAsync(sp, a);
                    string? code = AgentToolRegistry.Str(a, "ma_nl");
                    var ing = code == null ? null : await db.Ingredient.AsNoTracking().FirstOrDefaultAsync(i => !i.IsDeleted && i.Code == code) ?? throw new AgentToolException($"Không có nguyên liệu «{code}»");
                    DateTime day = Day(a, "ngay") ?? Svc<IClock>(sp).Today;
                    return new AgentPreview
                    {
                        Summary = $"Kiểm kê kho {wh.Code} ngày {day:dd/MM/yyyy}" + (ing == null ? " – toàn kho" : $" – {ing.Code} {ing.Name}"),
                        Payload = new JsonObject { ["warehouseId"] = wh.Id.ToString(), ["ingredientId"] = ing?.Id.ToString(), ["day"] = day.ToString("yyyy-MM-dd"), ["reason"] = AgentToolRegistry.Str(a, "ly_do") },
                    };
                },
                ExecuteAsync = async (sp, p, approver, note) =>
                {
                    var r = await Svc<IStockCountService>(sp).Create(new StockCountCreateRequest
                    {
                        WarehouseId = G(p, "warehouseId"), IngredientId = p["ingredientId"]?.GetValue<string?>() is { } i ? Guid.Parse(i) : null, CountType = "ADHOC", // StockCountStatus.CountTypes = DAILY / WEEKLY / MONTHLY / ADHOC – kiểm kê do agent đề xuất là ngoài lịch
                        CountDate = DateTime.Parse(S(p, "day"), CultureInfo.InvariantCulture), Note = $"[Agent WH] {S(p, "reason")}",
                    });
                    return r.ResponseCode == 200 ? (null, "Đã lập phiếu kiểm kê") : (r.ResponseMessage ?? "Không lập được phiếu kiểm kê", null);
                },
            },
            // ------------------------------------------------------------ KITCHEN – bếp
            new AgentCommandDef
            {
                Code = "propose_batch_event", Title = "Ghi sự kiện mẻ nấu", Agents = ["KITCHEN"], Screen = "prod-batch", Action = PermissionAction.Edit,
                Description = "Đề xuất ghi 1 sự kiện của mẻ nấu – bat_dau / xong (sản lượng) / nhiet_do (nhiệt độ lõi °C) / luu_mau – trưởng ca xác nhận, như bấm trên Bảng sản xuất.",
                InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"so_me\":{\"type\":\"string\"},\"su_kien\":{\"type\":\"string\",\"enum\":[\"bat_dau\",\"xong\",\"nhiet_do\",\"luu_mau\"]},\"nhiet_do\":{\"type\":\"number\"},\"so_luong\":{\"type\":\"number\"}," + Common + "},\"required\":[\"so_me\",\"su_kien\",\"ly_do\"]}",
                PrepareAsync = async (sp, a) =>
                {
                    var db = Uow(sp).DbContext;
                    string no = Need(a, "so_me", "số mẻ");
                    var b = await db.ProductionBatch.AsNoTracking().FirstOrDefaultAsync(x => !x.IsDeleted && (x.BatchNumber == no || x.BatchNo == no)) ?? throw new AgentToolException($"Không có mẻ «{no}»");
                    string ev = (AgentToolRegistry.Str(a, "su_kien") ?? string.Empty).Trim().ToLowerInvariant();
                    decimal? t = Dec(a, "nhiet_do"), q = Dec(a, "so_luong");
                    string what = ev switch
                    {
                        "bat_dau" => "Bắt đầu mẻ",
                        "xong" => "Xong mẻ" + (q is { } qq ? $" – sản lượng {qq:0.##}" : string.Empty),
                        "nhiet_do" => t is { } tt ? $"Nhiệt độ lõi {tt:0.#}°C" : throw new AgentToolException("Thiếu nhiệt độ (nhiet_do)"),
                        "luu_mau" => "Đã lưu mẫu",
                        _ => throw new AgentToolException("su_kien phải là bat_dau / xong / nhiet_do / luu_mau"),
                    };
                    var pv = new AgentPreview { Summary = $"Mẻ {no}: {what}", DocumentNo = no, MainValue = what, Payload = new JsonObject { ["batchId"] = b.Id.ToString(), ["event"] = ev, ["temp"] = t, ["qty"] = q, ["reason"] = AgentToolRegistry.Str(a, "ly_do") } };
                    if (ev == "nhiet_do" && t < 70)
                    {
                        pv.Warnings.Add("Nhiệt độ lõi < 70°C – hệ thống sẽ yêu cầu xử lý như ghi trên web");
                    }
                    return pv;
                },
                ExecuteAsync = async (sp, p, approver, note) =>
                {
                    var svc = Svc<IProductionBatchService>(sp);
                    Guid id = G(p, "batchId");
                    string n = $"[Agent KITCHEN] {S(p, "reason")}";
                    IResult<AppDelicare.Model.Model.ProductionBatchDetailModel> r = S(p, "event") switch
                    {
                        "bat_dau" => await svc.Start(new ProductionBatchStartRequest { Id = id, Note = n }),
                        "xong" => await svc.Finish(new ProductionBatchFinishRequest { Id = id, ActualQuantity = p["qty"]?.GetValue<decimal?>(), Note = n }),
                        "nhiet_do" => await svc.RecordCoreTemperature(new ProductionBatchTemperatureRequest { Id = id, TemperatureC = p["temp"]!.GetValue<decimal>(), MeasuredAt = Svc<IClock>(sp).Now, CorrectiveAction = note }),
                        _ => await svc.RecordSampleRetained(new ProductionBatchSampleRequest { Id = id, CheckedAt = Svc<IClock>(sp).Now, ActualValue = "Đã lưu mẫu" }),
                    };
                    return r.ResponseCode == 200 ? (null, "Đã ghi sự kiện mẻ") : (r.ResponseMessage ?? "Không ghi được sự kiện mẻ", null);
                },
            },
            // ------------------------------------------------------------ QA
            new AgentCommandDef
            {
                Code = "propose_qc_result", Title = "Kết luận QC nhận hàng", Agents = ["QA"], Screen = "qc-inspection", Action = PermissionAction.Approve,
                Description = "Đề xuất KẾT LUẬN QC cho phiếu nhập: từng dòng DAT / KHONG_DAT (+ số chấp nhận, nhiệt độ) – QA lead xác nhận, như Kết luận QC trên web (đạt → lô dùng được).",
                InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"so_phieu_nhap\":{\"type\":\"string\"},"
                    + "\"dong\":{\"type\":\"array\",\"items\":{\"type\":\"object\",\"properties\":{\"ma_nl\":{\"type\":\"string\"},\"ket_qua\":{\"type\":\"string\",\"enum\":[\"DAT\",\"KHONG_DAT\"]},\"sl_chap_nhan\":{\"type\":\"number\"},\"nhiet_do\":{\"type\":\"number\"},\"ghi_chu\":{\"type\":\"string\"}},\"required\":[\"ma_nl\",\"ket_qua\"]}}," + Common + "},"
                    + "\"required\":[\"so_phieu_nhap\",\"dong\",\"ly_do\"]}",
                PrepareAsync = async (sp, a) =>
                {
                    var db = Uow(sp).DbContext;
                    string no = Need(a, "so_phieu_nhap", "số phiếu nhập");
                    var gr = await db.GoodsReceipt.AsNoTracking().FirstOrDefaultAsync(g => !g.IsDeleted && (g.ReceiptNumber == no || g.ReceiptNo == no)) ?? throw new AgentToolException($"Không có phiếu nhập «{no}»");
                    // Conclude nhận QCInspection.Id (không phải GoodsReceipt.Id) – như màn Kết luận QC.
                    Guid inspectionId = await db.QcInspection.AsNoTracking().Where(q => !q.IsDeleted && q.GoodsReceiptId == gr.Id).OrderByDescending(q => q.CreatedDate).Select(q => (Guid?)q.Id).FirstOrDefaultAsync()
                        ?? throw new AgentToolException($"Phiếu nhập {no} chưa có phiếu kiểm QC");
                    var grLines = await (from l in db.GoodsReceiptLine.AsNoTracking() join i in db.Ingredient.AsNoTracking() on l.IngredientId equals i.Id
                                         where l.GoodsReceiptId == gr.Id && !l.IsDeleted select new { l.Id, i.Code, i.Name, l.ReceivedQuantity }).ToListAsync();
                    var pv = new AgentPreview { LineHeaders = ["Nguyên liệu", "Nhận", "Kết quả", "Chấp nhận"], DocumentNo = no };
                    var outLines = new JsonArray();
                    foreach (var r in Rows(a, "dong", "dòng QC"))
                    {
                        string code = AgentToolRegistry.Str(r, "ma_nl") ?? throw new AgentToolException("Dòng thiếu mã nguyên liệu");
                        var gl = grLines.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase)) ?? throw new AgentToolException($"Phiếu nhập {no} không có nguyên liệu «{code}»");
                        string res = (AgentToolRegistry.Str(r, "ket_qua") ?? string.Empty).Trim().ToUpperInvariant();
                        string result = res switch { "DAT" => "PASS", "KHONG_DAT" => "FAIL", _ => throw new AgentToolException($"{code}: ket_qua phải DAT / KHONG_DAT") };
                        pv.Lines.Add([$"{gl.Code} {gl.Name}", $"{gl.ReceivedQuantity:0.###}", res == "DAT" ? "Đạt" : "Không đạt", Dec(r, "sl_chap_nhan")?.ToString("0.###")]);
                        outLines.Add(new JsonObject { ["lineId"] = gl.Id.ToString(), ["result"] = result, ["accepted"] = Dec(r, "sl_chap_nhan"), ["temp"] = Dec(r, "nhiet_do"), ["remark"] = AgentToolRegistry.Str(r, "ghi_chu") });
                    }
                    pv.Summary = $"Kết luận QC phiếu nhập {no}: {pv.Lines.Count} dòng";
                    pv.Payload = new JsonObject { ["receiptId"] = gr.Id.ToString(), ["inspectionId"] = inspectionId.ToString(), ["lines"] = outLines, ["reason"] = AgentToolRegistry.Str(a, "ly_do") };
                    return pv;
                },
                ExecuteAsync = async (sp, p, approver, note) =>
                {
                    var r = await Svc<IQcInspectionService>(sp).Conclude(new QcInspectionConcludeRequest
                    {
                        Id = G(p, "inspectionId"), Note = $"[Agent QA] {S(p, "reason")}",
                        ListLine = [.. (p["lines"] as JsonArray ?? []).OfType<JsonObject>().Select(l => new QcInspectionLineInput
                        {
                            GoodsReceiptLineId = G(l, "lineId"), Result = S(l, "result"), AcceptedQuantity = l["accepted"]?.GetValue<decimal?>(), Temperature = l["temp"]?.GetValue<decimal?>(), Remark = l["remark"]?.GetValue<string?>(),
                        })],
                    });
                    return r.ResponseCode == 200 ? (null, "Đã kết luận QC") : (r.ResponseMessage ?? "Không kết luận được QC", null);
                },
            },
            // ------------------------------------------------------------ SALES
            new AgentCommandDef
            {
                Code = "propose_order_change", Title = "Đổi số suất đơn hàng", Agents = ["SALES"], Screen = "crm-order", Action = PermissionAction.Edit,
                Description = "Đề xuất ĐỔI SỐ SUẤT các dòng của 1 đơn (theo loại suất). Trước giờ chốt → sửa đơn như web; đơn đã chốt → tạo yêu cầu sửa sau chốt (thêm bước duyệt quá giờ chốt như web).",
                InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"so_don\":{\"type\":\"string\"},"
                    + "\"dong\":{\"type\":\"array\",\"items\":{\"type\":\"object\",\"properties\":{\"loai_suat\":{\"type\":\"string\",\"description\":\"Mã loại suất của dòng (vd TRUA)\"},\"so_suat\":{\"type\":\"number\"}},\"required\":[\"loai_suat\",\"so_suat\"]}}," + Common + "},"
                    + "\"required\":[\"so_don\",\"dong\",\"ly_do\"]}",
                PrepareAsync = async (sp, a) =>
                {
                    var db = Uow(sp).DbContext;
                    string no = Need(a, "so_don", "số đơn");
                    var o = await db.Order.AsNoTracking().FirstOrDefaultAsync(x => !x.IsDeleted && (x.OrderNumber == no || x.OrderNo == no)) ?? throw new AgentToolException($"Không có đơn «{no}»");
                    if (o.Status is OrderStatus.Cancelled)
                    {
                        throw new AgentToolException($"Đơn {no} đã hủy");
                    }
                    var lines = await db.OrderLine.AsNoTracking().Where(l => l.OrderId == o.Id && !l.IsDeleted).ToListAsync();
                    bool afterCutoff = o.Status is OrderStatus.Locked or OrderStatus.InProduction || Svc<IClock>(sp).Now >= Svc<IOrderService>(sp).CutoffOf(o.MealDate);
                    var pv = new AgentPreview { LineHeaders = ["Loại suất", "Hiện tại", "Đề xuất"], DocumentNo = no, OpenLink = "#/crm-order-detail?id=" + o.Id };
                    var outLines = new JsonArray();
                    foreach (var r in Rows(a, "dong", "dòng đổi"))
                    {
                        string meal = (AgentToolRegistry.Str(r, "loai_suat") ?? string.Empty).Trim().ToUpperInvariant();
                        var l = lines.FirstOrDefault(x => string.Equals((x.MealType ?? string.Empty).Trim(), meal, StringComparison.OrdinalIgnoreCase)) ?? throw new AgentToolException($"Đơn {no} không có dòng loại suất «{meal}»");
                        decimal qty = Dec(r, "so_suat") is >= 0 and var q ? q : throw new AgentToolException($"Số suất {meal} không hợp lệ");
                        pv.Lines.Add([meal, $"{l.Quantity:0.##}", $"{qty:0.##}"]);
                        outLines.Add(new JsonObject { ["lineId"] = l.Id.ToString(), ["qty"] = qty });
                    }
                    pv.Summary = $"Đổi số suất đơn {no} ngày {o.MealDate:dd/MM}" + (afterCutoff ? " – SAU GIỜ CHỐT: tạo yêu cầu sửa sau chốt, cần thêm bước duyệt" : string.Empty);
                    if (afterCutoff)
                    {
                        pv.Warnings.Add("Đơn đã chốt – duyệt lệnh này chỉ tạo yêu cầu sửa sau chốt (duyệt quá giờ chốt như web)");
                    }
                    pv.Payload = new JsonObject { ["orderId"] = o.Id.ToString(), ["after"] = afterCutoff, ["lines"] = outLines, ["reason"] = AgentToolRegistry.Str(a, "ly_do") };
                    return pv;
                },
                ExecuteAsync = async (sp, p, approver, note) =>
                {
                    var svc = Svc<IOrderService>(sp);
                    Guid id = G(p, "orderId");
                    var lines = (p["lines"] as JsonArray ?? []).OfType<JsonObject>().Select(l => new OrderLineQuantityRequest { Id = G(l, "lineId"), Quantity = l["qty"]!.GetValue<decimal>() }).ToList();
                    string reason = $"[Agent SALES] {S(p, "reason")}";
                    // Trạng thái đơn có thể đã đổi từ lúc đề xuất (vd chốt 20:00) → xét lại lúc duyệt.
                    // Như web: đã tới giờ chốt (kể cả đơn chưa bấm chốt) → xin sửa sau chốt; UpdateLines chặn sau giờ chốt.
                    var cur = await Uow(sp).DbContext.Order.AsNoTracking().Where(o => o.Id == id).Select(o => new { o.Status, o.MealDate }).FirstOrDefaultAsync();
                    if (cur != null && (cur.Status is OrderStatus.Locked or OrderStatus.InProduction || Svc<IClock>(sp).Now >= svc.CutoffOf(cur.MealDate)))
                    {
                        var rc = await svc.RequestChange(new OrderChangeAfterCutoffRequest { Id = id, Reason = reason, ListLine = lines });
                        return rc.ResponseCode == 200 ? (null, "Đã tạo yêu cầu sửa đơn sau giờ chốt – chờ duyệt") : (rc.ResponseMessage ?? "Không tạo được yêu cầu sửa sau chốt", null);
                    }
                    var r = await svc.UpdateLines(new OrderUpdateLinesRequest { Id = id, Note = reason, ListLine = lines });
                    return r.ResponseCode == 200 ? (null, "Đã đổi số suất đơn") : (r.ResponseMessage ?? "Không đổi được số suất", null);
                },
            },
            // ------------------------------------------------------------ LOG – giao vận
            new AgentCommandDef
            {
                Code = "propose_route", Title = "Chuyển điểm giao sang chuyến khác", Agents = ["LOG"], Screen = "log-route", Action = PermissionAction.Edit,
                Description = "Đề xuất CHUYỂN 1 đơn giao (điểm giao) sang chuyến khác cùng ngày – điều phối xác nhận, như kéo điểm trên Bảng điều phối.",
                InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"so_don_giao\":{\"type\":\"string\"},\"so_chuyen\":{\"type\":\"string\",\"description\":\"Chuyến đích\"}," + Common + "},\"required\":[\"so_don_giao\",\"so_chuyen\",\"ly_do\"]}",
                PrepareAsync = async (sp, a) =>
                {
                    var db = Uow(sp).DbContext;
                    string dn = Need(a, "so_don_giao", "số đơn giao"), rn = Need(a, "so_chuyen", "số chuyến");
                    var d = await db.DeliveryOrder.AsNoTracking().FirstOrDefaultAsync(x => !x.IsDeleted && (x.DeliveryNumber == dn || x.DeliveryNo == dn)) ?? throw new AgentToolException($"Không có đơn giao «{dn}»");
                    var r = await db.DeliveryRoute.AsNoTracking().FirstOrDefaultAsync(x => !x.IsDeleted && (x.RouteNo == rn || x.Code == rn)) ?? throw new AgentToolException($"Không có chuyến «{rn}»");
                    if (r.DeliveryDate.Date != d.DeliveryDate.Date)
                    {
                        throw new AgentToolException($"Chuyến {rn} ngày {r.DeliveryDate:dd/MM} khác ngày giao của đơn {d.DeliveryDate:dd/MM}");
                    }
                    var from = d.DeliveryRouteId is { } fr ? await db.DeliveryRoute.AsNoTracking().Where(x => x.Id == fr).Select(x => x.RouteNo ?? x.Code).FirstOrDefaultAsync() : null;
                    return new AgentPreview
                    {
                        Summary = $"Chuyển đơn giao {dn} từ {from ?? "(chưa xếp)"} sang chuyến {rn}", DocumentNo = dn,
                        Payload = new JsonObject { ["deliveryOrderId"] = d.Id.ToString(), ["routeId"] = r.Id.ToString(), ["reason"] = AgentToolRegistry.Str(a, "ly_do") },
                    };
                },
                ExecuteAsync = async (sp, p, approver, note) =>
                {
                    var r = await Svc<IDeliveryRouteService>(sp).MovePoint(new AppDelicare.Model.Model.DispatchMovePointRequest { DeliveryOrderId = G(p, "deliveryOrderId"), DeliveryRouteId = G(p, "routeId") });
                    return r.ResponseCode == 200 ? (null, "Đã chuyển điểm giao") : (r.ResponseMessage ?? "Không chuyển được điểm giao", null);
                },
            },
            new AgentCommandDef
            {
                Code = "propose_delivery_event", Title = "Ghi sự kiện giao hàng", Agents = ["LOG"], Screen = "log-route", Action = PermissionAction.Edit,
                Description = "Đề xuất ghi sự kiện tại 1 điểm dừng của chuyến: den (đã đến, nhiệt độ) / giao (số suất giao, người nhận, nhiệt độ) – điều phối xác nhận, như tài xế ghi trên web.",
                InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"so_chuyen\":{\"type\":\"string\"},\"thu_tu\":{\"type\":\"integer\",\"description\":\"Thứ tự điểm dừng trong chuyến\"},\"su_kien\":{\"type\":\"string\",\"enum\":[\"den\",\"giao\"]},"
                    + "\"so_suat\":{\"type\":\"number\"},\"nguoi_nhan\":{\"type\":\"string\"},\"nhiet_do\":{\"type\":\"number\"}," + Common + "},\"required\":[\"so_chuyen\",\"thu_tu\",\"su_kien\",\"ly_do\"]}",
                PrepareAsync = async (sp, a) =>
                {
                    var db = Uow(sp).DbContext;
                    string rn = Need(a, "so_chuyen", "số chuyến");
                    var r = await db.DeliveryRoute.AsNoTracking().FirstOrDefaultAsync(x => !x.IsDeleted && (x.RouteNo == rn || x.Code == rn)) ?? throw new AgentToolException($"Không có chuyến «{rn}»");
                    int seq = (int)(Dec(a, "thu_tu") ?? throw new AgentToolException("Thiếu thứ tự điểm dừng (thu_tu)"));
                    var s = await db.DeliveryStop.AsNoTracking().FirstOrDefaultAsync(x => !x.IsDeleted && x.DeliveryRouteId == r.Id && x.SequenceNo == seq) ?? throw new AgentToolException($"Chuyến {rn} không có điểm dừng số {seq}");
                    string ev = (AgentToolRegistry.Str(a, "su_kien") ?? string.Empty).Trim().ToLowerInvariant();
                    if (ev is not ("den" or "giao"))
                    {
                        throw new AgentToolException("su_kien phải là den / giao");
                    }
                    decimal? qty = Dec(a, "so_suat");
                    string? receiver = AgentToolRegistry.Str(a, "nguoi_nhan");
                    if (ev == "giao" && (qty == null || string.IsNullOrWhiteSpace(receiver)))
                    {
                        throw new AgentToolException("Giao hàng cần số suất (so_suat) và người nhận (nguoi_nhan)");
                    }
                    var pv = new AgentPreview
                    {
                        Summary = ev == "den" ? $"Chuyến {rn} – điểm {seq}: đã đến" : $"Chuyến {rn} – điểm {seq}: giao {qty:0.##} suất (kế hoạch {s.PlannedMealQty:0.##}), người nhận {receiver}",
                        DocumentNo = rn, Payload = new JsonObject { ["stopId"] = s.Id.ToString(), ["event"] = ev, ["qty"] = qty, ["receiver"] = receiver, ["temp"] = Dec(a, "nhiet_do"), ["reason"] = AgentToolRegistry.Str(a, "ly_do") },
                    };
                    if (ev == "giao" && s.PlannedMealQty is { } pl && qty < pl)
                    {
                        pv.Warnings.Add($"Giao thiếu {pl - qty:0.##} suất so kế hoạch");
                    }
                    return pv;
                },
                ExecuteAsync = async (sp, p, approver, note) =>
                {
                    var svc = Svc<IDeliveryStopService>(sp);
                    Guid id = G(p, "stopId");
                    decimal? temp = p["temp"]?.GetValue<decimal?>();
                    var r = S(p, "event") == "den"
                        ? await svc.Arrive(new DeliveryStopArriveRequest { Id = id, TemperatureC = temp, Note = $"[Agent LOG] {S(p, "reason")}" })
                        : await svc.Deliver(new DeliveryStopDeliverRequest
                        {
                            Id = id, DeliveredQuantity = p["qty"]!.GetValue<decimal>(), ReceiverName = S(p, "receiver"), TemperatureC = temp, ReceivedAt = Svc<IClock>(sp).Now, Remark = $"[Agent LOG] {S(p, "reason")}",
                        });
                    return r.ResponseCode == 200 ? (null, "Đã ghi sự kiện giao hàng") : (r.ResponseMessage ?? "Không ghi được sự kiện giao hàng", null);
                },
            },
            // ------------------------------------------------------------ HR – nhân sự
            new AgentCommandDef
            {
                Code = "propose_leave", Title = "Lập đơn nghỉ phép thay nhân viên", Agents = ["HR"], Screen = HrScreen.Leave, Action = PermissionAction.Add,
                Description = "Đề xuất LẬP đơn nghỉ cho 1 nhân viên (từ ngày – đến ngày, loại nghỉ, lý do) – trưởng bộ phận xác nhận; đơn sau đó đi duyệt nghỉ như web.",
                InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"ma_nv\":{\"type\":\"string\"},\"tu_ngay\":{\"type\":\"string\"},\"den_ngay\":{\"type\":\"string\"},\"loai_nghi\":{\"type\":\"string\",\"description\":\"Mã loại nghỉ (vd PHEP_NAM)\"}," + Common + "},\"required\":[\"ma_nv\",\"tu_ngay\",\"den_ngay\",\"loai_nghi\",\"ly_do\"]}",
                PrepareAsync = async (sp, a) =>
                {
                    var e = await EmployeeAsync(sp, a);
                    DateTime from = Day(a, "tu_ngay") ?? throw new AgentToolException("Thiếu từ ngày (tu_ngay)"), to = Day(a, "den_ngay") ?? throw new AgentToolException("Thiếu đến ngày (den_ngay)");
                    if (to < from)
                    {
                        throw new AgentToolException("Đến ngày phải ≥ từ ngày");
                    }
                    string type = Need(a, "loai_nghi", "loại nghỉ").ToUpperInvariant();
                    return new AgentPreview
                    {
                        Summary = $"Đơn nghỉ {type} cho {e.EmployeeCode ?? e.Code} {e.FullName}: {from:dd/MM} – {to:dd/MM} ({(to - from).Days + 1} ngày)", Counterparty = e.FullName,
                        Payload = new JsonObject { ["employeeId"] = e.Id.ToString(), ["from"] = from.ToString("yyyy-MM-dd"), ["to"] = to.ToString("yyyy-MM-dd"), ["type"] = type, ["reason"] = AgentToolRegistry.Str(a, "ly_do") },
                    };
                },
                ExecuteAsync = async (sp, p, approver, note) =>
                {
                    var r = await Svc<ILeaveRequestService>(sp).Create(new LeaveCreateRequest
                    {
                        EmployeeId = G(p, "employeeId"), FromDate = DateTime.Parse(S(p, "from"), CultureInfo.InvariantCulture), ToDate = DateTime.Parse(S(p, "to"), CultureInfo.InvariantCulture),
                        LeaveTypeCode = S(p, "type"), Reason = $"[Agent HR] {S(p, "reason")}",
                    });
                    return r.ResponseCode == 200 ? (null, "Đã lập đơn nghỉ – chờ duyệt nghỉ") : (r.ResponseMessage ?? "Không lập được đơn nghỉ", null);
                },
            },
            new AgentCommandDef
            {
                Code = "propose_shift_swap", Title = "Lập đơn đổi / nhường ca", Agents = ["HR"], Screen = HrScreen.Swap, Action = PermissionAction.Add,
                Description = "Đề xuất LẬP đơn đổi ca cho nhân viên ngày X (có / không người nhận ca) – trưởng bộ phận xác nhận; đơn đi tiếp luồng đồng ý + duyệt như web.",
                InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"ma_nv\":{\"type\":\"string\"},\"ngay\":{\"type\":\"string\"},\"ma_nv_nhan\":{\"type\":\"string\",\"description\":\"Người nhận ca (trống = nhường)\"},\"ngay_doi\":{\"type\":\"string\",\"description\":\"Ngày ca của người nhận để đổi lại (trống = chỉ nhường)\"}," + Common + "},\"required\":[\"ma_nv\",\"ngay\",\"ly_do\"]}",
                PrepareAsync = async (sp, a) =>
                {
                    var e = await EmployeeAsync(sp, a);
                    DateTime day = Day(a, "ngay") ?? throw new AgentToolException("Thiếu ngày (ngay)");
                    var asg = await AssignmentAsync(sp, e.Id, day);
                    AppDelicare.Model.Entity.Employee? t = AgentToolRegistry.Str(a, "ma_nv_nhan") != null ? await EmployeeAsync(sp, a, "ma_nv_nhan") : null;
                    AppDelicare.Model.Entity.ShiftAssignment? back = t != null && Day(a, "ngay_doi") is { } bd ? await AssignmentAsync(sp, t.Id, bd) : null;
                    return new AgentPreview
                    {
                        Summary = $"Đổi ca {e.FullName} ngày {day:dd/MM}" + (t == null ? " – nhường ca" : $" ↔ {t.FullName}" + (back == null ? string.Empty : $" (ca {back.WorkDate:dd/MM})")), Counterparty = e.FullName,
                        Payload = new JsonObject { ["assignmentId"] = asg.Id.ToString(), ["targetId"] = t?.Id.ToString(), ["backId"] = back?.Id.ToString(), ["reason"] = AgentToolRegistry.Str(a, "ly_do") },
                    };
                },
                ExecuteAsync = async (sp, p, approver, note) =>
                {
                    var r = await Svc<IShiftSwapRequestService>(sp).Create(new ShiftSwapCreateRequest
                    {
                        CurrentShiftAssignmentId = G(p, "assignmentId"), TargetEmployeeId = p["targetId"]?.GetValue<string?>() is { } t ? Guid.Parse(t) : null,
                        ProposedShiftAssignmentId = p["backId"]?.GetValue<string?>() is { } b ? Guid.Parse(b) : null, Reason = $"[Agent HR] {S(p, "reason")}",
                    });
                    return r.ResponseCode == 200 ? (null, "Đã lập đơn đổi ca") : (r.ResponseMessage ?? "Không lập được đơn đổi ca", null);
                },
            },
            new AgentCommandDef
            {
                Code = "propose_attendance_fix", Title = "Bổ sung chấm công", Agents = ["HR"], Screen = HrScreen.Attendance, Action = PermissionAction.Add,
                Description = "Đề xuất BỔ SUNG giờ vào / ra ca cho nhân viên quên chấm (loai VAO / RA, giờ HH:mm, mã lý do bổ sung) – trưởng bộ phận xác nhận, như Bổ sung chấm công trên web.",
                InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"ma_nv\":{\"type\":\"string\"},\"ngay\":{\"type\":\"string\"},\"loai\":{\"type\":\"string\",\"enum\":[\"VAO\",\"RA\"]},\"gio\":{\"type\":\"string\",\"description\":\"HH:mm\"},"
                    + "\"ma_ly_do\":{\"type\":\"string\",\"description\":\"Mã lý do bổ sung (danh mục AttendanceSupplementReason)\"}," + Common + "},\"required\":[\"ma_nv\",\"ngay\",\"loai\",\"gio\",\"ma_ly_do\",\"ly_do\"]}",
                PrepareAsync = async (sp, a) =>
                {
                    var e = await EmployeeAsync(sp, a);
                    DateTime day = Day(a, "ngay") ?? throw new AgentToolException("Thiếu ngày (ngay)");
                    var asg = await AssignmentAsync(sp, e.Id, day);
                    string kind = (AgentToolRegistry.Str(a, "loai") ?? string.Empty).Trim().ToUpperInvariant() switch
                    {
                        "VAO" => AttendanceEventType.CheckIn, "RA" => AttendanceEventType.CheckOut, _ => throw new AgentToolException("loai phải VAO / RA"),
                    };
                    TimeSpan at = TimeSpan.TryParseExact(AgentToolRegistry.Str(a, "gio") ?? string.Empty, [@"hh\:mm", @"h\:mm"], CultureInfo.InvariantCulture, out var ts) ? ts : throw new AgentToolException("Giờ phải dạng HH:mm");
                    return new AgentPreview
                    {
                        Summary = $"Bổ sung {(kind == AttendanceEventType.CheckIn ? "giờ vào" : "giờ ra")} {at:hh\\:mm} ngày {day:dd/MM} cho {e.FullName}", Counterparty = e.FullName,
                        Payload = new JsonObject { ["employeeId"] = e.Id.ToString(), ["assignmentId"] = asg.Id.ToString(), ["type"] = kind, ["at"] = (day + at).ToString("yyyy-MM-ddTHH:mm:ss"), ["code"] = Need(a, "ma_ly_do", "mã lý do"), ["reason"] = AgentToolRegistry.Str(a, "ly_do") },
                    };
                },
                ExecuteAsync = async (sp, p, approver, note) =>
                {
                    var r = await Svc<IAttendanceEventService>(sp).Supplement(new AppDelicare.Model.Model.AttendanceSupplementRequest
                    {
                        EmployeeId = G(p, "employeeId"), ShiftAssignmentId = G(p, "assignmentId"), EventType = S(p, "type"), EventAt = DateTime.Parse(S(p, "at"), CultureInfo.InvariantCulture),
                        ReasonCode = S(p, "code"), Note = $"[Agent HR] {S(p, "reason")}",
                    });
                    return r.ResponseCode == 200 ? (null, "Đã bổ sung chấm công") : (r.ResponseMessage ?? "Không bổ sung được chấm công", null);
                },
            },
        ];
    }
}
