using System.Text.Json;
using System.Text.Json.Nodes;
using AppDelicare.Data.Share;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Request.Custom;
using AppDelicare.Services.Implement.Messaging;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Implement.Agent
{
    /// <summary>
    /// AI-19 (D-133 đợt 4, D-06): lệnh của KHÁCH qua Zalo (agent CSKH). Khách KHÔNG tự ghi: mỗi yêu cầu là 1 đề xuất ở Phê duyệt › Lệnh của agent, KD / điều phối
    /// (người có quyền thao tác web tương ứng) xác nhận. Mã khách lấy từ người hỏi (<see cref="CustomerArg"/>, máy chủ đặt) – khách chỉ chạm được đơn của chính mình.
    /// </summary>
    public static partial class AgentCommandRegistry
    {
        /// <summary>Tham số do MÁY CHỦ gắn (Id khách của người hỏi) – giá trị agent gửi lên luôn bị bỏ (AgentCommandService.CustomerArgs).</summary>
        public const string CustomerArg = "_khach";
        /// <summary>Đề xuất của khách chờ KD / điều phối tối đa 12 giờ (mặc định lệnh agent 15 phút).</summary>
        public const int CustomerTtlMinutes = 720;
        public static readonly string[] ComplaintCategories = ["QUALITY", "LATE", "HYGIENE", "PACKAGING", "SERVICE", "OTHER"];

        private static Guid CustomerOf(JsonElement a) => AgentToolRegistry.Str(a, CustomerArg) is { } s && Guid.TryParse(s, out var g) ? g
            : throw new AgentToolException("Lệnh này chỉ dùng khi khách hàng hỏi qua Zalo");

        /// <summary>Đơn của chính khách (số đơn) – đơn khách khác / không có → cùng 1 thông báo (không lộ đơn của khách khác).</summary>
        private static async Task<AppDelicare.Model.Entity.Order> CustomerOrderAsync(IServiceProvider sp, JsonElement a, bool required = true)
        {
            Guid customer = CustomerOf(a);
            var db = Uow(sp).DbContext;
            string? no = AgentToolRegistry.Str(a, "so_don");
            if (no == null && !required)
            {
                DateTime today = Svc<IClock>(sp).Today;
                return await db.Order.AsNoTracking().Where(o => !o.IsDeleted && o.CustomerId == customer && o.MealDate >= today && o.MealDate < today.AddDays(1) && o.Status != OrderStatus.Cancelled)
                    .OrderBy(o => o.OrderNumber).FirstOrDefaultAsync() ?? throw new AgentToolException("Hôm nay quý khách không có đơn – ghi số đơn (so_don)");
            }
            no ??= Need(a, "so_don", "số đơn");
            return await db.Order.AsNoTracking().FirstOrDefaultAsync(o => !o.IsDeleted && o.CustomerId == customer && (o.OrderNumber == no || o.OrderNo == no))
                ?? throw new AgentToolException($"Không tìm thấy đơn «{no}» của quý khách");
        }

        private static IEnumerable<AgentCommandDef> CustomerCommands() =>
        [
            new AgentCommandDef
            {
                Code = "propose_portion_confirm", Title = "Khách xác nhận / đổi số suất", Agents = ["CSKH"], Screen = "crm-order", Action = PermissionAction.Edit,
                CustomerScoped = true, TtlMinutes = CustomerTtlMinutes,
                Description = "KHÁCH xác nhận đơn suất (không ghi dòng) hoặc báo số suất mới theo loại suất (dong). Trước giờ chốt: KD xác nhận = Chốt đơn / sửa số suất như web; "
                    + "sau giờ chốt: tạo yêu cầu sửa sau chốt (duyệt quá giờ chốt). Chỉ đơn của chính khách đang hỏi.",
                InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"so_don\":{\"type\":\"string\"},\"dong\":{\"type\":\"array\",\"items\":{\"type\":\"object\",\"properties\":{\"loai_suat\":{\"type\":\"string\"},\"so_suat\":{\"type\":\"number\"}},"
                    + "\"required\":[\"loai_suat\",\"so_suat\"]}}," + Common + "},\"required\":[\"so_don\",\"ly_do\"]}",
                PrepareAsync = async (sp, a) =>
                {
                    var o = await CustomerOrderAsync(sp, a);
                    var db = Uow(sp).DbContext;
                    if (o.Status is OrderStatus.Cancelled)
                    {
                        throw new AgentToolException($"Đơn {o.OrderNumber} đã hủy");
                    }
                    var cus = await db.Customer.AsNoTracking().Where(c => c.Id == o.CustomerId).Select(c => c.DisplayName ?? c.Name).FirstOrDefaultAsync();
                    var lines = await db.OrderLine.AsNoTracking().Where(l => l.OrderId == o.Id && !l.IsDeleted).ToListAsync();
                    bool afterCutoff = o.Status is OrderStatus.Locked or OrderStatus.InProduction || Svc<IClock>(sp).Now >= Svc<IOrderService>(sp).CutoffOf(o.MealDate);
                    var pv = new AgentPreview { DocumentNo = o.OrderNumber, Counterparty = cus, OpenLink = "#/crm-order-detail?id=" + o.Id, LineHeaders = ["Loại suất", "Hiện tại", "Khách báo"] };
                    bool hasLines = a.TryGetProperty("dong", out var d) && d.ValueKind == JsonValueKind.Array && d.GetArrayLength() > 0;
                    var outLines = new JsonArray();
                    if (hasLines)
                    {
                        foreach (var r in d.EnumerateArray())
                        {
                            string meal = (AgentToolRegistry.Str(r, "loai_suat") ?? string.Empty).Trim().ToUpperInvariant();
                            var l = lines.FirstOrDefault(x => string.Equals((x.MealType ?? string.Empty).Trim(), meal, StringComparison.OrdinalIgnoreCase)) ?? throw new AgentToolException($"Đơn {o.OrderNumber} không có suất «{meal}»");
                            decimal qty = Dec(r, "so_suat") is >= 0 and var q ? q : throw new AgentToolException($"Số suất {meal} không hợp lệ");
                            pv.Lines.Add([meal, $"{l.Quantity:0.##}", $"{qty:0.##}"]);
                            outLines.Add(new JsonObject { ["lineId"] = l.Id.ToString(), ["qty"] = qty });
                        }
                    }
                    else
                    {
                        if (o.Status is not (OrderStatus.Draft or OrderStatus.Submitted))
                        {
                            throw new AgentToolException($"Đơn {o.OrderNumber} đang {OrderStatus.Label(o.Status)} – không cần xác nhận lại");
                        }
                        if (afterCutoff)
                        {
                            throw new AgentToolException($"Đơn {o.OrderNumber} đã qua giờ chốt – liên hệ nhân viên kinh doanh");
                        }
                        foreach (var l in lines)
                        {
                            pv.Lines.Add([l.MealType, $"{l.Quantity:0.##}", "giữ nguyên"]);
                        }
                    }
                    pv.Summary = $"{cus} {(hasLines ? "báo số suất mới" : "xác nhận")} đơn {o.OrderNumber} ngày {o.MealDate:dd/MM}" + (hasLines && afterCutoff ? " – SAU GIỜ CHỐT: tạo yêu cầu sửa sau chốt" : string.Empty);
                    if (hasLines && afterCutoff)
                    {
                        pv.Warnings.Add("Đơn đã chốt – xác nhận chỉ tạo yêu cầu sửa sau chốt (duyệt quá giờ chốt như web)");
                    }
                    pv.Payload = new JsonObject { ["orderId"] = o.Id.ToString(), ["mode"] = hasLines ? "lines" : "confirm", ["lines"] = outLines, ["reason"] = AgentToolRegistry.Str(a, "ly_do") };
                    return pv;
                },
                ExecuteAsync = async (sp, p, approver, note) =>
                {
                    var svc = Svc<IOrderService>(sp);
                    Guid id = G(p, "orderId");
                    string reason = $"[Khách qua Zalo] {S(p, "reason")}";
                    if (S(p, "mode") == "confirm")
                    {
                        var c = await svc.Confirm(new OrderActionRequest { Id = id, Note = reason });
                        return c.ResponseCode == 200 ? (null, "Đã chốt đơn theo xác nhận của khách") : (c.ResponseMessage ?? "Không chốt được đơn", null);
                    }
                    var lines = (p["lines"] as JsonArray ?? []).OfType<JsonObject>().Select(l => new OrderLineQuantityRequest { Id = G(l, "lineId"), Quantity = l["qty"]!.GetValue<decimal>() }).ToList();
                    var cur = await Uow(sp).DbContext.Order.AsNoTracking().Where(o => o.Id == id).Select(o => new { o.Status, o.MealDate }).FirstOrDefaultAsync();
                    if (cur != null && (cur.Status is OrderStatus.Locked or OrderStatus.InProduction || Svc<IClock>(sp).Now >= svc.CutoffOf(cur.MealDate)))
                    {
                        var rc = await svc.RequestChange(new OrderChangeAfterCutoffRequest { Id = id, Reason = reason, ListLine = lines });
                        return rc.ResponseCode == 200 ? (null, "Đã tạo yêu cầu sửa đơn sau giờ chốt – chờ duyệt") : (rc.ResponseMessage ?? "Không tạo được yêu cầu sửa sau chốt", null);
                    }
                    var r = await svc.UpdateLines(new OrderUpdateLinesRequest { Id = id, Note = reason, ListLine = lines });
                    return r.ResponseCode == 200 ? (null, "Đã đổi số suất theo khách báo") : (r.ResponseMessage ?? "Không đổi được số suất", null);
                },
            },
            new AgentCommandDef
            {
                Code = "propose_shortage_report", Title = "Khách báo thiếu suất", Agents = ["CSKH"], Screen = "log-complaint", Action = PermissionAction.Add,
                CustomerScoped = true, TtlMinutes = CustomerTtlMinutes,
                Description = "KHÁCH báo thiếu suất khi nhận hàng (đơn hôm nay nếu không ghi số đơn): số suất thiếu + mô tả. Điều phối / CSKH xác nhận = ghi phản ánh loại Thiếu suất "
                    + "(như màn Phản ánh) và báo bếp / điều phối nấu bù.",
                InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"so_don\":{\"type\":\"string\"},\"so_suat_thieu\":{\"type\":\"number\"}," + Common + "},\"required\":[\"so_suat_thieu\",\"ly_do\"]}",
                PrepareAsync = async (sp, a) =>
                {
                    var o = await CustomerOrderAsync(sp, a, required: false);
                    decimal miss = Dec(a, "so_suat_thieu") is > 0 and var m ? m : throw new AgentToolException("Số suất thiếu phải > 0");
                    var cus = await Uow(sp).DbContext.Customer.AsNoTracking().Where(c => c.Id == o.CustomerId).Select(c => c.DisplayName ?? c.Name).FirstOrDefaultAsync();
                    var pv = new AgentPreview
                    {
                        Summary = $"{cus} báo THIẾU {miss:0.#} suất đơn {o.OrderNumber} ngày {o.MealDate:dd/MM}", DocumentNo = o.OrderNumber, Counterparty = cus, MainValue = $"{miss:0.#} suất",
                        OpenLink = "#/crm-order-detail?id=" + o.Id,
                        Payload = new JsonObject { ["customerId"] = o.CustomerId.ToString(), ["orderId"] = o.Id.ToString(), ["orderNo"] = o.OrderNumber, ["miss"] = miss, ["reason"] = AgentToolRegistry.Str(a, "ly_do") },
                    };
                    if (miss > (o.TotalQuantity ?? 0))
                    {
                        pv.Warnings.Add($"Số thiếu {miss:0.#} > số suất của đơn {o.TotalQuantity:0.#}");
                    }
                    return pv;
                },
                ExecuteAsync = async (sp, p, approver, note) =>
                {
                    var r = await Svc<ICustomerComplaintService>(sp).Create(new ComplaintCreateRequest
                    {
                        CustomerId = G(p, "customerId"), OrderId = G(p, "orderId"), SourceChannel = "ZALO", CategoryCode = DeliveryCode.ComplaintCategoryShortage, Severity = "High",
                        Description = $"Khách báo thiếu {p["miss"]!.GetValue<decimal>():0.#} suất qua Zalo: {S(p, "reason")}",
                    });
                    if (r.ResponseCode != 200)
                    {
                        return (r.ResponseMessage ?? "Không ghi được phản ánh thiếu suất", null);
                    }
                    // Báo bếp / điều phối (mẫu SHORTAGE_REPORTED – người có quyền Thêm màn Thực hiện giao hàng).
                    await BusinessNotifier.ToMenusAsync(Svc<INotificationService>(sp), Uow(sp).DbContext, NotificationCatalog.ShortageReported, ["#/log-execution"], new
                    {
                        title = $"Khách báo thiếu {p["miss"]!.GetValue<decimal>():0.#} suất – đơn {S(p, "orderNo")}", body = S(p, "reason"),
                    }, $"{NotificationCatalog.ShortageReported}:{S(p, "orderId")}:{r.Data?.Complaint?.ComplaintNo}", requireAdd: true);
                    return (null, $"Đã ghi phản ánh thiếu suất {r.Data?.Complaint?.ComplaintNo} và báo điều phối");
                },
            },
            new AgentCommandDef
            {
                Code = "propose_complaint", Title = "Khách gửi phản ánh", Agents = ["CSKH"], Screen = "log-complaint", Action = PermissionAction.Add,
                CustomerScoped = true, TtlMinutes = CustomerTtlMinutes,
                Description = "KHÁCH gửi phản ánh (chất lượng, giao trễ, vệ sinh, bao bì, phục vụ, khác) kèm đơn nếu có. CSKH / điều phối xác nhận = ghi phản ánh như màn Phản ánh khách hàng.",
                InputSchemaJson = "{\"type\":\"object\",\"properties\":{\"so_don\":{\"type\":\"string\"},\"loai\":{\"type\":\"string\",\"enum\":[\"QUALITY\",\"LATE\",\"HYGIENE\",\"PACKAGING\",\"SERVICE\",\"OTHER\"]},"
                    + "\"noi_dung\":{\"type\":\"string\"}," + Common + "},\"required\":[\"noi_dung\",\"ly_do\"]}",
                PrepareAsync = async (sp, a) =>
                {
                    Guid customer = CustomerOf(a);
                    var order = AgentToolRegistry.Str(a, "so_don") == null ? null : await CustomerOrderAsync(sp, a);
                    string cat = (AgentToolRegistry.Str(a, "loai") ?? "OTHER").ToUpperInvariant();
                    if (!ComplaintCategories.Contains(cat))
                    {
                        throw new AgentToolException("Loại phản ánh phải là " + string.Join(" / ", ComplaintCategories));
                    }
                    string text = Need(a, "noi_dung", "nội dung phản ánh");
                    var cus = await Uow(sp).DbContext.Customer.AsNoTracking().Where(c => c.Id == customer).Select(c => c.DisplayName ?? c.Name).FirstOrDefaultAsync();
                    return new AgentPreview
                    {
                        Summary = $"{cus} phản ánh ({cat})" + (order != null ? $" đơn {order.OrderNumber}" : string.Empty) + $": {(text.Length > 120 ? text[..120] + "…" : text)}",
                        Counterparty = cus, DocumentNo = order?.OrderNumber, OpenLink = "#/log-complaint",
                        Payload = new JsonObject { ["customerId"] = customer.ToString(), ["orderId"] = order?.Id.ToString(), ["cat"] = cat, ["text"] = text },
                    };
                },
                ExecuteAsync = async (sp, p, approver, note) =>
                {
                    var r = await Svc<ICustomerComplaintService>(sp).Create(new ComplaintCreateRequest
                    {
                        CustomerId = G(p, "customerId"), OrderId = p["orderId"]?.GetValue<string?>() is { } o ? Guid.Parse(o) : null, SourceChannel = "ZALO", CategoryCode = S(p, "cat"),
                        Severity = "Medium", Description = $"[Khách qua Zalo] {S(p, "text")}",
                    });
                    return r.ResponseCode == 200 ? (null, $"Đã ghi phản ánh {r.Data?.Complaint?.ComplaintNo}") : (r.ResponseMessage ?? "Không ghi được phản ánh", null);
                },
            },
        ];
    }

    /// <summary>AI-19: công cụ đọc của KHÁCH (CSKH – Zalo) – đơn / thực đơn của chính khách đang hỏi (ctx.CustomerId), không bao giờ khách khác.</summary>
    public static partial class AgentToolRegistry
    {
        private static Guid CustomerOf(AgentToolContext ctx) => ctx.CustomerId ?? throw new AgentToolException("Công cụ này dành cho khách hàng hỏi qua Zalo");

        /// <summary>Đơn + dòng suất của khách trong khoảng ngày (dùng cho bot Zalo và Hermes CSKH).</summary>
        public static async Task<List<CustomerOrderView>> CustomerOrdersAsync(IServiceProvider sp, Guid customer, DateTime from, int days)
        {
            var db = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<AppDelicare.Data.Interface.IUnitOfWork>(sp).DbContext;
            DateTime to = from.AddDays(Math.Clamp(days, 1, 14));
            var orders = await db.Order.AsNoTracking().Where(o => !o.IsDeleted && o.CustomerId == customer && o.MealDate >= from && o.MealDate < to && o.Status != OrderStatus.Cancelled)
                .OrderBy(o => o.MealDate).ThenBy(o => o.OrderNumber).Take(60).ToListAsync();
            var ids = orders.Select(o => o.Id).ToList();
            var lines = await db.OrderLine.AsNoTracking().Where(l => ids.Contains(l.OrderId) && !l.IsDeleted).ToListAsync();
            return [.. orders.Select(o => new CustomerOrderView(o.OrderNumber ?? o.OrderNo ?? string.Empty, o.MealDate, OrderStatus.Label(o.Status), o.TotalQuantity ?? 0,
                [.. lines.Where(l => l.OrderId == o.Id).Select(l => new CustomerOrderLineView(l.MealType ?? string.Empty, l.Quantity, l.MenuId, l.MenuOptionNo))]))];
        }

        /// <summary>Món theo đơn của khách (thực đơn áp cho dòng suất: ngày trong tuần × buổi × lựa chọn).</summary>
        public static async Task<List<CustomerMenuView>> CustomerMenuAsync(IServiceProvider sp, Guid customer, DateTime from, int days)
        {
            var db = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<AppDelicare.Data.Interface.IUnitOfWork>(sp).DbContext;
            var orders = await CustomerOrdersAsync(sp, customer, from, days);
            var menuIds = orders.SelectMany(o => o.Lines).Where(l => l.MenuId != null).Select(l => l.MenuId!.Value).Distinct().ToList();
            var menuLines = await (from m in db.MenuLine.AsNoTracking() join d in db.Dish.AsNoTracking() on m.DishId equals d.Id
                                   where menuIds.Contains(m.MenuId) && !m.IsDeleted select new { m.MenuId, m.MealType, m.DayOfWeek, m.SequenceNo, d.Name, m.SortOrder }).ToListAsync();
            var list = new List<CustomerMenuView>();
            foreach (var o in orders)
            {
                int dow = o.MealDate.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)o.MealDate.DayOfWeek;
                foreach (var l in o.Lines.Where(x => x.MenuId != null))
                {
                    var dishes = menuLines.Where(m => m.MenuId == l.MenuId && m.DayOfWeek == dow && string.Equals(m.MealType, l.MealType, StringComparison.OrdinalIgnoreCase)
                            && (l.OptionNo == null || m.SequenceNo == null || m.SequenceNo == l.OptionNo)).OrderBy(m => m.SortOrder).Select(m => m.Name ?? string.Empty).Distinct().ToList();
                    if (dishes.Count > 0)
                    {
                        list.Add(new CustomerMenuView(o.MealDate, l.MealType, o.OrderNumber, dishes));
                    }
                }
            }
            return list;
        }

        private static IEnumerable<AgentToolDef> CustomerTools() =>
        [
            new AgentToolDef
            {
                Name = "khach_don_hang", ScreenId = "crm-order", Agents = ["CSKH"], CustomerScoped = true,
                Description = "Đơn suất của CHÍNH khách đang hỏi qua Zalo trong N ngày (mặc định hôm nay + 6 ngày): số đơn, ngày ăn, trạng thái, số suất theo loại suất.",
                InputSchemaJson = Schema(("tu_ngay", "string", "Từ ngày (trống = hôm nay)"), ("so_ngay", "integer", "Số ngày (mặc định 7, tối đa 14)")),
                RunAsync = async (ctx, a) => await CustomerOrdersAsync(ctx.Services, CustomerOf(ctx), Date(ctx, a, "tu_ngay"), Int(a, "so_ngay") ?? 7),
            },
            new AgentToolDef
            {
                Name = "khach_thuc_don", ScreenId = "menu-menu", Agents = ["CSKH"], CustomerScoped = true,
                Description = "Thực đơn theo đơn của CHÍNH khách đang hỏi qua Zalo (ngày × buổi → các món) trong N ngày (mặc định 7).",
                InputSchemaJson = Schema(("tu_ngay", "string", "Từ ngày (trống = hôm nay)"), ("so_ngay", "integer", "Số ngày (mặc định 7, tối đa 14)")),
                RunAsync = async (ctx, a) => await CustomerMenuAsync(ctx.Services, CustomerOf(ctx), Date(ctx, a, "tu_ngay"), Int(a, "so_ngay") ?? 7),
            },
        ];
    }

    public sealed record CustomerOrderLineView(string MealType, decimal Quantity, Guid? MenuId, int? OptionNo);

    public sealed record CustomerOrderView(string OrderNumber, DateTime MealDate, string Status, decimal TotalQuantity, List<CustomerOrderLineView> Lines);

    public sealed record CustomerMenuView(DateTime MealDate, string MealType, string OrderNumber, List<string> Dishes);
}
