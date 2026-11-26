using System.Text.Json;
using AppDelicare.Data.Connection;
using AppDelicare.Model.Entity;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// ACC-3 (D-131 f): trạng thái khách xác nhận bảng kê tháng – lưu bằng <c>AuditChange</c> (EntityName <c>BillingStatement</c>, IdChange = khách,
    /// Changes = JSON { m: "yyyy-MM", note }) – không bảng mới. Trạng thái = hành động mới nhất của (khách, tháng):
    /// Sent → Đã gửi – chờ khách · CustomerConfirmed → Khách đã xác nhận · CustomerDisputed → Khách phản hồi lệch;
    /// đã tải chứng từ bán hàng tháng sang AMIS (ACC-2) → Đã lập phiếu bán.
    /// </summary>
    public static class BillingConfirmation
    {
        public const string Entity = "BillingStatement";
        public const string Sent = "SentToCustomer";
        public const string Confirmed = "CustomerConfirmed";
        public const string Disputed = "CustomerDisputed";

        public const string StateNone = "NOT_SENT";
        public const string StateSent = "SENT";
        public const string StateConfirmed = "CONFIRMED";
        public const string StateDisputed = "DISPUTED";
        public const string StateInvoiced = "INVOICED";

        public static string Label(string? s, bool ready) => s switch
        {
            StateSent => "Đã gửi – chờ khách", StateConfirmed => "Khách đã xác nhận", StateDisputed => "Khách phản hồi lệch", StateInvoiced => "Đã lập phiếu bán",
            _ => ready ? "Sẵn sàng gửi" : "Chưa sẵn sàng",
        };

        public sealed record State(string Code, DateTime At, string? Note);

        public static string Json(string month, string? note) => JsonSerializer.Serialize(new Dictionary<string, string?> { ["m"] = month, ["note"] = note });

        private static string? Month(string? changes)
        {
            try
            {
                return changes != null && JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(changes) is { } j && j.TryGetValue("m", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string? Note(string? changes)
        {
            try
            {
                return changes != null && JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(changes) is { } j && j.TryGetValue("note", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>Khách → trạng thái xác nhận bảng kê của tháng (yyyy-MM).</summary>
        public static async Task<Dictionary<Guid, State>> StatesAsync(DatabaseContext db, string month)
        {
            var rows = await db.AuditChange.AsNoTracking()
                .Where(a => !a.IsDeleted && a.IdChange != null && a.Changes != null && a.Changes.Contains("\"m\":\"" + month + "\"")
                    && ((a.EntityName == Entity && (a.Action == Sent || a.Action == Confirmed || a.Action == Disputed))
                        || (a.EntityName == AccountingVoucherService.LogEntity && a.Action == AccountingVoucherService.ActionExported && a.Changes.Contains("\"t\":\"SALE\""))))
                .OrderBy(a => a.CreatedDate).Select(a => new { a.IdChange, a.EntityName, a.Action, a.Changes, a.CreatedDate }).ToListAsync();
            var outMap = new Dictionary<Guid, State>();
            foreach (var r in rows.Where(r => Month(r.Changes) == month))
            {
                if (r.EntityName == Entity)
                {
                    string code = r.Action == Confirmed ? StateConfirmed : r.Action == Disputed ? StateDisputed : StateSent;
                    if (outMap.TryGetValue(r.IdChange!.Value, out var cur) && cur.Code == StateInvoiced && code != StateDisputed)
                    {
                        continue; // đã lập phiếu bán – chỉ phản hồi lệch mới đổi trạng thái
                    }
                    outMap[r.IdChange!.Value] = new State(code, r.CreatedDate, Note(r.Changes));
                }
                else if (Customer(r.Changes) is { } cus && outMap.TryGetValue(cus, out var cur) && cur.Code == StateConfirmed)
                {
                    outMap[cus] = new State(StateInvoiced, r.CreatedDate, cur.Note);
                }
            }
            return outMap;
        }

        /// <summary>Khách của chứng từ bán hàng (khóa ACC-2 "SALE|&lt;khách N&gt;|yyyyMM").</summary>
        private static Guid? Customer(string? changes)
        {
            try
            {
                if (changes != null && JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(changes) is { } j && j.TryGetValue("k", out var k) && k.ValueKind == JsonValueKind.String)
                {
                    var parts = k.GetString()!.Split('|');
                    return parts.Length >= 2 && Guid.TryParseExact(parts[1], "N", out var g) ? g : null;
                }
            }
            catch (JsonException)
            {
            }
            return null;
        }

        public static AuditChange Log(Guid customerId, string action, string month, string? note, Guid actor, DateTime now)
            => new() { Id = Guid.NewGuid(), IdChange = customerId, EntityName = Entity, Action = action, Changes = Json(month, note), CreatedDate = now, CreatedBy = actor, IsDeleted = false };
    }
}
