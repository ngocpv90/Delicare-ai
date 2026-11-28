using System.Text.Json;
using System.Text.Json.Nodes;
using AppDelicare.Data.Interface;
using AppDelicare.Data.Share;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AppDelicare.Services.Implement.Agent
{
    /// <summary>Bản xem trước của 1 lệnh (không ghi gì): tóm tắt, chứng từ, dòng, cảnh báo; <see cref="Payload"/> = tham số đã chuẩn hóa sẽ chạy khi duyệt.</summary>
    public sealed class AgentPreview
    {
        public string Summary { get; set; } = string.Empty;
        public string? DocumentNo { get; set; }
        public string? Counterparty { get; set; }
        public string? MainValue { get; set; }
        public List<string> LineHeaders { get; set; } = [];
        public List<List<string?>> Lines { get; set; } = [];
        public List<string> Warnings { get; set; } = [];
        public JsonObject Payload { get; set; } = [];
        public string? OpenLink { get; set; }
    }

    /// <summary>
    /// AI-9 (D-133): 1 lệnh ghi của agent. <see cref="PrepareAsync"/> = KIỂM TRA + xem trước bằng chính quy tắc Service (không ghi bảng nghiệp vụ);
    /// <see cref="ExecuteAsync"/> = gọi method Service THẬT khi người duyệt bấm Duyệt (chạy dưới người duyệt – quy tắc Service, bước duyệt sau vẫn áp).
    /// Người duyệt phải có <see cref="Action"/> trên <see cref="Screen"/> (như tự thao tác trên web) – cấu hình trên ma trận quyền, không hard-code người.
    /// </summary>
    public sealed class AgentCommandDef
    {
        public required string Code { get; init; }
        public required string Title { get; init; }
        public required string Description { get; init; }
        public required IReadOnlyList<string> Agents { get; init; }
        public required string Screen { get; init; }
        /// <summary>Màn kiểm quyền Xem khi agent GỌI công cụ (mặc định = Screen; màn ẩn MENU-4 dùng màn cha, vd lô → scm-inv-balance).</summary>
        public string? ToolScreen { get; init; }
        public PermissionAction Action { get; init; } = PermissionAction.Add;
        public string InputSchemaJson { get; init; } = """{"type":"object","properties":{}}""";
        public required Func<IServiceProvider, JsonElement, Task<AgentPreview>> PrepareAsync { get; init; }
        /// <summary>Trả null = thành công (kèm thông điệp ở <c>out</c> message), chuỗi = lỗi tiếng Việt.</summary>
        public required Func<IServiceProvider, JsonObject, Guid, string?, Task<(string? Error, string? Result)>> ExecuteAsync { get; init; }
    }

    public sealed class AgentProposalModel
    {
        public string RequestNo { get; set; } = string.Empty;
        public Guid ApprovalRequestId { get; set; }
        public DateTime? DueAt { get; set; }
        public string Summary { get; set; } = string.Empty;
        public List<string> Warnings { get; set; } = [];
        public string Note { get; set; } = "Đã gửi đề xuất – chờ người có quyền xác nhận (Phê duyệt › Lệnh của agent / bot Điều hành). Chưa ghi gì vào nghiệp vụ.";
    }

    /// <summary>AI-9: tạo đề xuất (không ghi nghiệp vụ) + quét hết hạn. Dùng bởi công cụ <c>propose_*</c> (ToolGateway) và job.</summary>
    public interface IAgentCommandService
    {
        Task<AgentProposalModel> ProposeAsync(AgentCommandDef def, AgentToolContext ctx, JsonElement args);
        Task<int> ExpireOverdueAsync();
    }

    public class AgentCommandService(IServiceProvider services, IUnitOfWork unitOfWork, IClock clock, IApprovalRequestService approvals, IOptionsMonitor<AgentOptions> options) : IAgentCommandService
    {
        public const string BusinessType = "AGENT_WRITE";
        public const string ScreenId = "ai-agent-cmd";

        public async Task<AgentProposalModel> ProposeAsync(AgentCommandDef def, AgentToolContext ctx, JsonElement args)
        {
            string reason = AgentToolRegistry.Str(args, "ly_do") ?? throw new AgentToolException("Thiếu lý do đề xuất (ly_do) – người duyệt cần biết vì sao");
            var preview = await def.PrepareAsync(services, args);
            string commandId = Guid.NewGuid().ToString("N");
            var proposal = new JsonObject
            {
                ["command"] = def.Code, ["title"] = def.Title, ["agent"] = ctx.AgentCode, ["askedBy"] = ctx.AskingUserId?.ToString(), ["reason"] = reason,
                ["sop"] = AgentToolRegistry.Str(args, "sop_dan_chieu"), ["correlationId"] = ctx.CorrelationId, ["idempotencyKey"] = $"AGENT:{ctx.AgentCode}:{commandId}",
                ["payload"] = preview.Payload,
                ["preview"] = JsonSerializer.SerializeToNode(new { preview.Summary, preview.DocumentNo, preview.Counterparty, preview.MainValue, preview.LineHeaders, preview.Lines, preview.Warnings, preview.OpenLink }),
            };
            // Sinh số chứng từ cần transaction đang mở (khóa giữ tới khi ghi xong) – như mọi Service gửi duyệt.
            using var tran = await unitOfWork.OpenTransaction();
            var (request, error) = await approvals.CreateAsync(BusinessType, Guid.NewGuid(), proposal.ToJsonString());
            if (error != null || request == null)
            {
                unitOfWork.RollbackTransaction();
                throw new AgentToolException(error ?? "Không tạo được đề xuất");
            }
            // GAP-AI-EXP (tạm): hạn đề xuất theo phút (Agent:ProposalTtlMinutes) – ghi đè hạn theo giờ của bộ máy duyệt.
            DateTime due = clock.Now.AddMinutes(Math.Max(1, options.CurrentValue.ProposalTtlMinutes));
            var db = unitOfWork.DbContext;
            var entity = await db.ApprovalRequest.FirstAsync(a => a.Id == request.Id);
            entity.DueAt = due;
            foreach (var step in await db.ApprovalStep.Where(s => s.ApprovalRequestId == request.Id && s.StepNo == 1).ToListAsync())
            {
                step.DueAt = due;
            }
            await db.SaveChangesAsync();
            unitOfWork.CommitTransaction();
            return new AgentProposalModel { RequestNo = request.RequestNo ?? string.Empty, ApprovalRequestId = request.Id, DueAt = due, Summary = preview.Summary, Warnings = preview.Warnings };
        }

        /// <summary>Đề xuất quá hạn chưa ai duyệt → Đã hủy + quyết định hệ thống "Hết hạn" (GAP-AI-EXP tạm – không có trạng thái Expired). Trả số đề xuất đóng.</summary>
        public async Task<int> ExpireOverdueAsync()
        {
            var db = unitOfWork.DbContext;
            DateTime now = clock.Now;
            var overdue = await db.ApprovalRequest.Where(a => !a.IsDeleted && a.BusinessType == BusinessType && a.RequestStatus == "Pending" && a.DueAt != null && a.DueAt < now).ToListAsync();
            Guid by = await db.User.AsNoTracking().Where(u => u.UserName == "system" && !u.IsDeleted).Select(u => u.Id).FirstOrDefaultAsync();
            foreach (var a in overdue)
            {
                // Cùng khuôn hủy của bộ máy duyệt (ApprovalRequestService.Cancel): bước đang chờ → Đã hủy, bước sau → Bỏ qua, quyết định CANCEL, mục tuyến → Đã hủy.
                var steps = await db.ApprovalStep.Where(s => s.ApprovalRequestId == a.Id && !s.IsDeleted).OrderBy(s => s.StepNo).ToListAsync();
                var step = steps.FirstOrDefault(s => s.DecisionStatus == ApprovalRequestService.StepWaiting);
                if (step != null)
                {
                    step.DecisionStatus = ApprovalRequestService.Cancelled;
                    step.DecidedAt = now;
                    step.DecisionNote = "Hết hạn (agent)";
                    foreach (var rest in steps.Where(s => s.StepNo > step.StepNo && s.DecisionStatus == ApprovalRequestService.StepWaiting))
                    {
                        rest.DecisionStatus = ApprovalRequestService.StepSkipped;
                    }
                    db.ApprovalDecision.Add(new ApprovalDecision
                    {
                        Id = Guid.NewGuid(), ApprovalRequestId = a.Id, ApprovalStepId = step.Id, DecisionCode = "CANCEL", DecidedBy = by == Guid.Empty ? a.RequestedBy ?? Guid.Empty : by, DecidedAt = now,
                        Comment = "Hết hạn – đề xuất của agent quá thời gian xác nhận (Agent:ProposalTtlMinutes), agent đề xuất lại nếu còn cần",
                    });
                }
                a.RequestStatus = ApprovalRequestService.Cancelled;
                a.CompletedAt = now;
                foreach (var item in await db.ApprovalItem.Where(i => i.ApprovalRequestId == a.Id && !i.IsDeleted).ToListAsync())
                {
                    item.ItemStatus = ApprovalRequestService.Cancelled;
                }
            }
            if (overdue.Count > 0)
            {
                await db.SaveChangesAsync();
            }
            return overdue.Count;
        }
    }

    /// <summary>
    /// AI-9: duyệt lệnh agent (<c>AGENT_WRITE</c>, mục "Phê duyệt › Lệnh của agent" <c>#/wf-inbox?type=ai-agent-cmd</c>). BusinessId = mã lệnh (ngẫu nhiên);
    /// nội dung lệnh ở phần đề xuất của yêu cầu duyệt. Duyệt: hết hạn → từ chối; người duyệt thiếu quyền thao tác trên màn đích → từ chối; đã chạy (khóa chống trùng) →
    /// không chạy lại; còn lại gọi method Service thật dưới người duyệt và ghi AuditChange <c>AgentCommand</c> (mã agent, khóa chống trùng, kết quả).
    /// </summary>
    public class AgentWriteApprovalHandler(IServiceProvider services, IUnitOfWork unitOfWork, IClock clock, IPermissionService permissions) : IApprovalHandler
    {
        public string BusinessType => AgentCommandService.BusinessType;

        public string ScreenId => AgentCommandService.ScreenId;

        public Task<string?> BuildPayloadAsync(Guid businessId) => Task.FromResult<string?>($"{{\"type\":\"{BusinessType}\",\"commandId\":\"{businessId:N}\"}}");

        private async Task<(ApprovalRequest? Request, JsonObject? Proposal)> LoadAsync(Guid businessId)
        {
            var r = await unitOfWork.DbContext.ApprovalRequest.AsNoTracking().Where(a => a.BusinessType == BusinessType && a.BusinessId == businessId && !a.IsDeleted)
                .OrderByDescending(a => a.RequestedAt).FirstOrDefaultAsync();
            return (r, ProposalOf(r?.PayloadJson));
        }

        /// <summary>PayloadJson của yêu cầu = {payload: …, proposal: …} (WrapPayload) – lấy phần đề xuất.</summary>
        public static JsonObject? ProposalOf(string? payloadJson)
        {
            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return null;
            }
            try
            {
                var (_, proposal) = ApprovalRequestService.UnwrapPayload(payloadJson);
                var node = JsonNode.Parse(proposal ?? payloadJson) as JsonObject;
                return node?["command"] != null ? node : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        public async Task<string?> DescribeAsync(Guid businessId)
        {
            var (_, p) = await LoadAsync(businessId);
            return p == null ? "Lệnh của agent" : $"{p["agent"]}: {p["title"]} – {p["preview"]?["Summary"]}";
        }

        public async Task<ApprovalSummaryModel?> GetSummaryAsync(Guid businessId, ApprovalSummaryContext ctx)
        {
            var p = ProposalOf(ctx.ProposalJson) ?? (await LoadAsync(businessId)).Proposal;
            if (p == null)
            {
                return null;
            }
            var pv = p["preview"];
            var m = new ApprovalSummaryModel
            {
                Title = $"{p["title"]} – agent {p["agent"]}", TypeName = "Lệnh của agent", ModuleName = "Trợ lý AI",
                DocumentNo = pv?["DocumentNo"]?.GetValue<string?>(), Counterparty = pv?["Counterparty"]?.GetValue<string?>(), MainValue = pv?["MainValue"]?.GetValue<string?>(),
                Reason = p["reason"]?.GetValue<string>(), OpenLink = pv?["OpenLink"]?.GetValue<string?>(),
            };
            m.Info.Add(new ApprovalInfoModel { Label = "Lệnh", Value = pv?["Summary"]?.GetValue<string>() });
            m.Info.Add(new ApprovalInfoModel { Label = "Agent đề xuất", Value = p["agent"]?.GetValue<string>() });
            if (p["sop"]?.GetValue<string?>() is { Length: > 0 } sop)
            {
                m.Info.Add(new ApprovalInfoModel { Label = "SOP dẫn chiếu", Value = sop });
            }
            if (pv?["LineHeaders"] is JsonArray heads)
            {
                m.LineHeaders = [.. heads.Select(h => h?.GetValue<string>() ?? string.Empty)];
            }
            if (pv?["Lines"] is JsonArray lines)
            {
                m.Lines = [.. lines.OfType<JsonArray>().Select(l => new ApprovalLineModel { Cells = [.. l.Select(c => c?.ToString())] })];
                m.LineCount = m.Lines.Count;
            }
            if (pv?["Warnings"] is JsonArray warns)
            {
                m.Flags = [.. warns.Select(w => new ApprovalFlagModel { Text = w?.GetValue<string>() ?? string.Empty, Severity = "warn" })];
            }
            return m;
        }

        public Task<string?> ApproveAsync(Guid businessId, Guid actorUserId, string? note) => ApproveAsync(businessId, actorUserId, note, null);

        public async Task<string?> ApproveAsync(Guid businessId, Guid actorUserId, string? note, string? proposalJson)
        {
            var (request, stored) = await LoadAsync(businessId);
            var p = ProposalOf(proposalJson) ?? stored;
            if (request == null || p == null)
            {
                return "Không tìm thấy nội dung lệnh của agent";
            }
            if (request.DueAt is { } due && due < clock.Now)
            {
                return $"Đề xuất đã hết hạn lúc {due:HH:mm dd/MM} – agent đề xuất lại nếu còn cần";
            }
            string code = p["command"]?.GetValue<string>() ?? string.Empty;
            var def = AgentCommandRegistry.Find(code);
            if (def == null)
            {
                return $"Lệnh «{code}» không còn được hỗ trợ";
            }
            var perms = await permissions.GetAsync(actorUserId);
            if (!permissions.IsAllowed(perms, def.Screen, def.Action))
            {
                return $"Người duyệt cần quyền {def.Action} trên màn {def.Screen} để xác nhận lệnh này (như tự thao tác trên web)";
            }
            string key = p["idempotencyKey"]?.GetValue<string>() ?? $"AGENT:{businessId:N}";
            var db = unitOfWork.DbContext;
            if (await db.AuditChange.AsNoTracking().AnyAsync(a => a.EntityName == "AgentCommand" && a.IdChange == businessId && a.Action == "Executed" && !a.IsDeleted))
            {
                return null; // đã chạy – không ghi lần 2
            }
            var payload = p["payload"] as JsonObject ?? [];
            var (error, result) = await def.ExecuteAsync(services, (JsonObject)payload.DeepClone(), actorUserId, note);
            if (error != null)
            {
                return error;
            }
            db.AuditChange.Add(new AuditChange
            {
                Id = Guid.NewGuid(), IdChange = businessId, EntityName = "AgentCommand", Action = "Executed",
                Changes = JsonSerializer.Serialize(new { command = code, agent = p["agent"]?.ToString(), key, result, approvedBy = actorUserId, note }),
                CreatedDate = clock.Now, CreatedBy = actorUserId, IsDeleted = false,
            });
            await db.SaveChangesAsync();
            return null;
        }

        public Task<string?> RejectAsync(Guid businessId, Guid actorUserId, string note) => Task.FromResult<string?>(null);
    }

    /// <summary>AI-9 / AI-10: danh bạ lệnh ghi của agent (tên công cụ <c>propose_*</c>).</summary>
    public static partial class AgentCommandRegistry
    {
        public static IReadOnlyList<AgentCommandDef> All => _all ??= [.. Core(), .. Ops(), .. KnowledgeCommands()];
        private static List<AgentCommandDef>? _all;

        public static AgentCommandDef? Find(string? code) => All.FirstOrDefault(c => string.Equals(c.Code, code?.Trim(), StringComparison.OrdinalIgnoreCase));

        private static T Svc<T>(IServiceProvider sp) where T : notnull => sp.GetRequiredService<T>();

        private static string Need(JsonElement a, string k, string label) => AgentToolRegistry.Str(a, k) ?? throw new AgentToolException($"Thiếu {label} ({k})");

        private const string Common = "\"ly_do\":{\"type\":\"string\",\"description\":\"Lý do đề xuất (bắt buộc) – số liệu + mã chứng từ\"},\"sop_dan_chieu\":{\"type\":\"string\",\"description\":\"id / tiêu đề SOP dẫn chiếu (nếu có)\"}";

        private static IEnumerable<AgentCommandDef> Core() =>
        [
            new AgentCommandDef
            {
                Code = "propose_hold_lot", Title = "Giữ lô nguyên liệu", Agents = ["QA"], Screen = "scm-lot", ToolScreen = "scm-inv-balance", Action = PermissionAction.Approve,
                Description = "Đề xuất TẠM GIỮ 1 lô nguyên liệu (không cho xuất dùng) – QA lead xác nhận mới có hiệu lực. Chưa ghi gì khi đề xuất.",
                InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"so_lo\":{\"type\":\"string\",\"description\":\"Số lô\"}," + Common + "},\"required\":[\"so_lo\",\"ly_do\"]}",
                PrepareAsync = async (sp, a) =>
                {
                    string no = Need(a, "so_lo", "số lô");
                    var lots = (await Svc<IMaterialLotService>(sp).GetList(new AppDelicare.Model.Request.Custom.MaterialLotListRequest { SearchText = no })).ListData ?? [];
                    var lot = lots.FirstOrDefault(l => string.Equals(l.Lot.LotNumber, no, StringComparison.OrdinalIgnoreCase)) ?? throw new AgentToolException($"Không tìm thấy lô «{no}»");
                    if (lot.IsBlocked)
                    {
                        throw new AgentToolException($"Lô {no} đang tạm giữ / bị chặn – không cần giữ thêm");
                    }
                    return new AgentPreview
                    {
                        Summary = $"Tạm giữ lô {lot.Lot.LotNumber} – {lot.IngredientName} (còn {lot.OnHandQuantity:0.###})", DocumentNo = lot.Lot.LotNumber, Counterparty = lot.SupplierName,
                        MainValue = $"Còn {lot.OnHandQuantity:#,0.###}", OpenLink = "#/scm-lot-detail?id=" + lot.Lot.Id,
                        Payload = new JsonObject { ["lotId"] = lot.Lot.Id.ToString(), ["lotNumber"] = lot.Lot.LotNumber, ["reason"] = AgentToolRegistry.Str(a, "ly_do") },
                    };
                },
                ExecuteAsync = async (sp, p, approver, note) =>
                {
                    var r = await Svc<IMaterialLotService>(sp).Hold(new AppDelicare.Model.Request.Custom.MaterialLotHoldRequest
                    {
                        Id = Guid.Parse(p["lotId"]!.GetValue<string>()), Reason = $"[Agent QA] {p["reason"]}{(string.IsNullOrWhiteSpace(note) ? string.Empty : " – " + note)}",
                    });
                    return r.ResponseCode == 200 ? (null, $"Đã giữ lô {p["lotNumber"]}") : (r.ResponseMessage ?? "Không giữ được lô", null);
                },
            },
            new AgentCommandDef
            {
                Code = "propose_purchase_order", Title = "Lập đơn mua", Agents = ["PROC"], Screen = "scm-po", Action = PermissionAction.Add,
                Description = "Đề xuất LẬP + GỬI DUYỆT đơn mua cho 1 NCC (nguyên liệu, số lượng, giá) – người mua hàng xác nhận; đơn ≥ ngưỡng vẫn qua bước Giám đốc như web.",
                InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"ma_ncc\":{\"type\":\"string\",\"description\":\"Mã NCC\"},\"ngay_giao\":{\"type\":\"string\",\"description\":\"Ngày giao yyyy-MM-dd\"},"
                    + "\"dong\":{\"type\":\"array\",\"items\":{\"type\":\"object\",\"properties\":{\"ma_nl\":{\"type\":\"string\"},\"so_luong\":{\"type\":\"number\"},\"don_gia\":{\"type\":\"number\"}},\"required\":[\"ma_nl\",\"so_luong\"]}}," + Common + "},"
                    + "\"required\":[\"ma_ncc\",\"dong\",\"ly_do\"]}",
                PrepareAsync = async (sp, a) =>
                {
                    var db = Svc<IUnitOfWork>(sp).DbContext;
                    string supCode = Need(a, "ma_ncc", "mã NCC");
                    var sup = await db.Supplier.AsNoTracking().FirstOrDefaultAsync(s => !s.IsDeleted && s.Code == supCode) ?? throw new AgentToolException($"Không có NCC «{supCode}»");
                    if (a.ValueKind != JsonValueKind.Object || !a.TryGetProperty("dong", out var lines) || lines.ValueKind != JsonValueKind.Array || lines.GetArrayLength() == 0)
                    {
                        throw new AgentToolException("Thiếu dòng nguyên liệu (dong)");
                    }
                    var outLines = new JsonArray();
                    var pv = new AgentPreview { LineHeaders = ["Nguyên liệu", "Số lượng", "ĐVT", "Đơn giá", "Thành tiền"], Counterparty = $"{sup.Code} {sup.Name}" };
                    decimal total = 0;
                    foreach (var l in lines.EnumerateArray())
                    {
                        string code = AgentToolRegistry.Str(l, "ma_nl") ?? throw new AgentToolException("Dòng thiếu mã nguyên liệu");
                        var ing = await db.Ingredient.AsNoTracking().FirstOrDefaultAsync(i => !i.IsDeleted && i.Code == code) ?? throw new AgentToolException($"Không có nguyên liệu «{code}»");
                        decimal qty = l.TryGetProperty("so_luong", out var q) && q.TryGetDecimal(out var qv) && qv > 0 ? qv : throw new AgentToolException($"Số lượng {code} phải > 0");
                        decimal? price = l.TryGetProperty("don_gia", out var pr) && pr.TryGetDecimal(out var pp) ? pp : null;
                        if (price == null)
                        {
                            price = await db.SupplierMaterial.AsNoTracking().Where(sm => !sm.IsDeleted && sm.SupplierId == sup.Id && sm.IngredientId == ing.Id)
                                .OrderByDescending(sm => sm.CreatedDate).Select(sm => (decimal?)sm.UnitPrice).FirstOrDefaultAsync();
                        }
                        if (price == null)
                        {
                            pv.Warnings.Add($"{code}: chưa có giá NCC – người duyệt kiểm giá");
                        }
                        Guid? unit = ing.PurchaseUomId ?? ing.BaseUomId;
                        string? unitName = unit == null ? null : await db.UnitMeasure.AsNoTracking().Where(u => u.Id == unit).Select(u => u.Name).FirstOrDefaultAsync();
                        total += qty * (price ?? 0);
                        pv.Lines.Add([$"{ing.Code} {ing.Name}", $"{qty:0.###}", unitName, price?.ToString("#,0"), (qty * (price ?? 0)).ToString("#,0")]);
                        outLines.Add(new JsonObject { ["ingredientId"] = ing.Id.ToString(), ["quantity"] = qty, ["unitId"] = unit?.ToString(), ["unitPrice"] = price });
                    }
                    DateTime? delivery = AgentToolRegistry.Str(a, "ngay_giao") is { } d && DateTime.TryParse(d, out var dd) ? dd.Date : null;
                    pv.Summary = $"Đơn mua {sup.Name}: {pv.Lines.Count} dòng, {total:#,0} đ";
                    pv.MainValue = $"{total:#,0} đ";
                    pv.Payload = new JsonObject { ["supplierId"] = sup.Id.ToString(), ["expected"] = delivery?.ToString("yyyy-MM-dd"), ["lines"] = outLines, ["reason"] = AgentToolRegistry.Str(a, "ly_do") };
                    return pv;
                },
                ExecuteAsync = async (sp, p, approver, note) =>
                {
                    var po = Svc<IPurchaseOrderService>(sp);
                    Guid id = Guid.NewGuid();
                    DateTime? expected = p["expected"]?.GetValue<string?>() is { } e ? DateTime.Parse(e) : null;
                    var saved = await po.Save(new AppDelicare.Model.Request.PurchaseOrderRequest
                    {
                        Id = id, SupplierId = Guid.Parse(p["supplierId"]!.GetValue<string>()), OrderDate = Svc<IClock>(sp).Today, ExpectedDeliveryDate = expected,
                        Note = $"[Agent PROC] {p["reason"]}",
                    }, AppDelicare.Model.Helper.ConstantHelper.TypeSave.Insert);
                    if (saved.ResponseCode != 200)
                    {
                        return (saved.ResponseMessage ?? "Không lập được đơn mua", null);
                    }
                    var lines = (p["lines"] as JsonArray ?? []).OfType<JsonObject>().Select(l => new AppDelicare.Model.Request.Custom.PurchaseOrderLineInput
                    {
                        IngredientId = Guid.Parse(l["ingredientId"]!.GetValue<string>()), Quantity = l["quantity"]!.GetValue<decimal>(),
                        UnitId = l["unitId"]?.GetValue<string?>() is { } u ? Guid.Parse(u) : null, UnitPrice = l["unitPrice"]?.GetValue<decimal?>(),
                    }).ToList();
                    var upd = await po.UpdateLines(new AppDelicare.Model.Request.Custom.PurchaseOrderUpdateLinesRequest { Id = id, ExpectedDeliveryDate = expected, ListLine = lines });
                    if (upd.ResponseCode != 200)
                    {
                        return (upd.ResponseMessage ?? "Không ghi được dòng đơn mua", null);
                    }
                    var sub = await po.Submit(new AppDelicare.Model.Request.Custom.PurchaseActionRequest { Id = id, Note = note });
                    return sub.ResponseCode == 200 ? (null, $"Đã lập + gửi duyệt đơn mua {sub.Data?.PurchaseOrder?.PurchaseOrderNumber}") : (sub.ResponseMessage ?? "Không gửi duyệt được đơn mua", null);
                },
            },
        ];
    }
}

namespace AppDelicare.Services.Implement.Agent
{
    /// <summary>AI-9 (D-133): đóng đề xuất ghi của agent quá hạn (khi Hermes tắt vẫn đóng; khi Hermes chạy, lay_su_kien 5 phút / lần cũng đóng).</summary>
    public sealed class AgentProposalExpiryJob(IAgentCommandService commands) : AppDelicare.Services.Jobs.IScheduledJob
    {
        public string Code => "AgentProposalExpiry";
        public string Name => "Đóng đề xuất agent quá hạn";
        public string Description => "Đề xuất ghi của agent quá hạn xác nhận (Agent:ProposalTtlMinutes) → Đã hủy, ghi quyết định hệ thống \"Hết hạn\"";
        public string DefaultAt => "00:05";

        public async Task<AppDelicare.Services.Jobs.JobOutcome> RunAsync(DateTime now, CancellationToken cancellationToken)
        {
            int n = await commands.ExpireOverdueAsync();
            return new AppDelicare.Services.Jobs.JobOutcome(AppDelicare.Services.Jobs.JobRunStatus.Succeeded, n, now.Date, $"{n} đề xuất hết hạn");
        }
    }
}
