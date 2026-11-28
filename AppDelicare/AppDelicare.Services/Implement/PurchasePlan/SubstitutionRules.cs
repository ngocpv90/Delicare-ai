using System.Security.Cryptography;
using System.Text;
using AppDelicare.Data.Connection;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Option;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace AppDelicare.Services.Implement.PurchasePlan
{
    /// <summary>
    /// UI-6.51 (D-137 e, GAP-PUR-SUB): nguyên liệu thay thế theo giá – MỘT chỗ cho tính nhu cầu (MRP / kế hoạch mua) và chọn công thức của sản xuất.
    /// <list type="bullet">
    /// <item>Công thức: dòng chuẩn (định lượng &gt; 0) và dòng thay thế (định lượng 0, <c>MaxSharePct</c>) cùng <c>SubstituteGroup</c>.</item>
    /// <item>So <b>giá trên 1 đơn vị dùng được</b> = giá mua (NCC theo quy tắc gợi ý chung <see cref="SupplierPick"/>, quy về đơn vị cơ sở) ÷ (1 − % hao sơ chế).
    ///   Thay thế rẻ hơn ≥ <c>PurchasePlan:SubstituteMinSavingPct</c> → dùng tới <c>MaxSharePct</c>; ít hơn / NCC thay thế không đạt → giữ chuẩn.</item>
    /// <item>Phần suất của khách chỉ dùng chuẩn (<c>Contract.StandardIngredientOnly</c>) không bao giờ thay: tỷ lệ thật của món = tỷ lệ × (1 − phần suất khách chuẩn).</item>
    /// <item>Quyết định của từng ngày cần hàng lưu lịch sử ở <c>AuditChange</c> (EntityName <c>PurchasePlanSubstitution</c>, không thêm cột / bảng):
    ///   <c>Decide</c> (hệ thống, khi lập đề nghị / tính nhu cầu chính thức), <c>Override</c> (người mua: giữ 100% chuẩn / đổi tỷ lệ) – có Override thì Override mới nhất thắng.
    ///   Sản xuất chỉ áp quyết định đã lưu (đúng tỷ lệ đã mua); chưa có → nguyên liệu chuẩn.</item>
    /// </list>
    /// </summary>
    public static class SubstitutionRules
    {
        public const string EntityName = "PurchasePlanSubstitution";
        public const string ActionDecide = "Decide";
        public const string ActionOverride = "Override";

        /// <summary>Một dòng định lượng (ảnh chụp công thức / dòng công thức) có thông tin nhóm thay thế.</summary>
        public sealed class Line
        {
            public Guid IngredientId { get; set; }
            public decimal Quantity { get; set; }
            public Guid UnitId { get; set; }
            public decimal? WastePercent { get; set; }
            public decimal? ExpectedWasteRate { get; set; }
            public int? SequenceNo { get; set; }
            public string? SubstituteGroup { get; set; }
            public decimal? MaxSharePct { get; set; }
            /// <summary>Dòng sinh ra do thay thế (không có trong công thức gốc).</summary>
            public bool Substituted { get; set; }
        }

        /// <summary>Quyết định của một cặp (chuẩn, thay thế) cho một ngày cần hàng.</summary>
        public sealed class Decision
        {
            public DateTime Date { get; set; }
            public Guid StdIngredientId { get; set; }
            public Guid SubIngredientId { get; set; }
            public string? Group { get; set; }
            /// <summary>% phần dùng được của dòng chuẩn được thay (trước khi trừ phần khách chỉ dùng chuẩn). 0 = giữ chuẩn.</summary>
            public decimal SharePct { get; set; }
            public decimal? MaxSharePct { get; set; }
            public decimal? StdUnitPrice { get; set; }
            public decimal? SubUnitPrice { get; set; }
            public decimal? StdUsablePrice { get; set; }
            public decimal? SubUsablePrice { get; set; }
            public decimal? StdWastePct { get; set; }
            public decimal? SubWastePct { get; set; }
            public decimal? SavingPct { get; set; }
            public string? StdSupplierName { get; set; }
            public string? SubSupplierName { get; set; }
            /// <summary>AUTO (theo giá) / OVERRIDE (người mua).</summary>
            public string Source { get; set; } = "AUTO";
            public string? Reason { get; set; }
            /// <summary>Phần suất (0..1) của khách chỉ dùng chuẩn theo món – để sản xuất áp đúng tỷ lệ đã mua.</summary>
            public Dictionary<Guid, decimal> StdOnlyByDish { get; set; } = [];
            public bool Stored { get; set; }
            /// <summary>Lượng thay thế (đơn vị dòng chuẩn) và tiết kiệm ước tính của ngày – chỉ số "Tiết kiệm nhờ thay thế".</summary>
            public decimal? SubQuantity { get; set; }
            public decimal? SavingAmount { get; set; }
        }

        public static Guid KeyOf(DateTime date, Guid std, Guid sub)
            => new(MD5.HashData(Encoding.UTF8.GetBytes($"{date:yyyyMMdd}|{std:N}|{sub:N}")));

        /// <summary>Các cặp (chuẩn, thay thế, nhóm, % tối đa, hao sơ chế 2 bên) có trong danh sách dòng.</summary>
        public static IEnumerable<(Line Std, Line Sub)> Pairs(IEnumerable<Line> lines)
        {
            foreach (var g in lines.Where(l => !string.IsNullOrWhiteSpace(l.SubstituteGroup) && !l.Substituted).GroupBy(l => l.SubstituteGroup!.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                var std = g.FirstOrDefault(l => l.Quantity > 0);
                if (std == null)
                {
                    continue;
                }
                foreach (var sub in g.Where(l => l.Quantity <= 0 && l.MaxSharePct is > 0 && l.IngredientId != std.IngredientId))
                {
                    yield return (std, sub);
                }
            }
        }

        /// <summary>Quyết định đã lưu (Override mới nhất nếu có, không thì Decide mới nhất; phần khách chuẩn theo bản ghi mới nhất).</summary>
        public static async Task<Decision?> StoredAsync(DatabaseContext db, DateTime date, Guid std, Guid sub)
        {
            Guid key = KeyOf(date.Date, std, sub);
            var rows = await db.AuditChange.AsNoTracking().Where(a => a.EntityName == EntityName && a.IdChange == key && !a.IsDeleted)
                .OrderByDescending(a => a.CreatedDate).Select(a => new { a.Action, a.Changes }).ToListAsync();
            if (rows.Count == 0)
            {
                return null;
            }
            var latest = Read(rows[0].Changes);
            var chosen = Read(rows.FirstOrDefault(r => r.Action == ActionOverride)?.Changes) ?? latest;
            if (chosen == null)
            {
                return null;
            }
            if (latest != null)
            {
                chosen.StdOnlyByDish = latest.StdOnlyByDish;
            }
            chosen.Stored = true;
            return chosen;
        }

        private static Decision? Read(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }
            try
            {
                return JsonConvert.DeserializeObject<Decision>(json);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>Ghi quyết định (bỏ qua nếu bản Decide mới nhất giống hệt – chạy lại job không sinh lịch sử thừa). Không SaveChanges – nơi gọi lưu trong transaction của mình.</summary>
        public static async Task<bool> SaveAsync(DatabaseContext db, Decision d, string action, Guid? by, DateTime now)
        {
            Guid key = KeyOf(d.Date.Date, d.StdIngredientId, d.SubIngredientId);
            d.Stored = false;
            string json = JsonConvert.SerializeObject(d);
            if (action == ActionDecide)
            {
                var last = await db.AuditChange.AsNoTracking().Where(a => a.EntityName == EntityName && a.IdChange == key && !a.IsDeleted)
                    .OrderByDescending(a => a.CreatedDate).Select(a => a.Changes).FirstOrDefaultAsync();
                if (last == json)
                {
                    return false;
                }
            }
            db.AuditChange.Add(new AuditChange { Id = Guid.NewGuid(), IdChange = key, EntityName = EntityName, Action = action, Changes = json, CreatedDate = now, CreatedBy = by, IsDeleted = false });
            return true;
        }

        /// <summary>
        /// Quyết định theo giá tại ngày cần hàng (không ghi). Giá mỗi bên = NCC theo quy tắc gợi ý chung cho 1 đơn vị cơ sở; NCC thay thế không đạt → giữ chuẩn.
        /// </summary>
        public static async Task<Decision> LiveAsync(DatabaseContext db, IUomConversionService uom, BusinessRulesOptions rules, PurchasePlanOptions plan, DateTime today, DateTime date,
            Line std, Line sub, Dictionary<Guid, Ingredient>? ingredients = null)
        {
            var ids = new[] { std.IngredientId, sub.IngredientId };
            ingredients ??= await db.Ingredient.AsNoTracking().Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id);
            Guid BaseOf(Guid id, Guid fallback) => ingredients.GetValueOrDefault(id) is { } i
                ? (i.BaseUnitId is { } b && b != Guid.Empty ? b : i.BaseUomId is { } o && o != Guid.Empty ? o : fallback) : fallback;
            Guid kStd = Guid.NewGuid(), kSub = Guid.NewGuid();
            var picks = await SupplierPick.SuggestAsync(db, uom, rules, today,
                [new SupplierPick.Need(kStd, std.IngredientId, 1m, BaseOf(std.IngredientId, std.UnitId), date), new SupplierPick.Need(kSub, sub.IngredientId, 1m, BaseOf(sub.IngredientId, sub.UnitId), date)]);
            var ps = picks.GetValueOrDefault(kStd);
            var pb = picks.GetValueOrDefault(kSub);
            decimal stdWaste = std.WastePercent ?? ingredients.GetValueOrDefault(std.IngredientId)?.StandardWastePercent ?? 0m;
            decimal subWaste = sub.WastePercent ?? ingredients.GetValueOrDefault(sub.IngredientId)?.StandardWastePercent ?? 0m;
            var d = new Decision
            {
                Date = date.Date, StdIngredientId = std.IngredientId, SubIngredientId = sub.IngredientId, Group = std.SubstituteGroup?.Trim(), MaxSharePct = sub.MaxSharePct,
                StdUnitPrice = ps?.UnitPrice, SubUnitPrice = pb?.UnitPrice, StdWastePct = stdWaste, SubWastePct = subWaste,
                StdSupplierName = ps?.SupplierName, SubSupplierName = pb?.SupplierName,
                StdUsablePrice = Usable(ps?.UnitPrice, stdWaste), SubUsablePrice = Usable(pb?.UnitPrice, subWaste),
            };
            if (pb?.SupplierId == null || d.SubUsablePrice == null)
            {
                d.Reason = "NCC nguyên liệu thay thế không đạt" + (string.IsNullOrWhiteSpace(pb?.Reason) ? string.Empty : " – " + pb!.Reason) + " → giữ chuẩn";
                return d;
            }
            if (d.StdUsablePrice is not > 0)
            {
                d.Reason = "Nguyên liệu chuẩn chưa có giá NCC đạt → giữ chuẩn";
                return d;
            }
            d.SavingPct = Math.Round((d.StdUsablePrice.Value - d.SubUsablePrice.Value) / d.StdUsablePrice.Value * 100m, 1);
            if (d.SavingPct >= plan.SubstituteMinSavingPct)
            {
                d.SharePct = Math.Clamp(sub.MaxSharePct ?? 0m, 0m, 100m);
                d.Reason = $"Rẻ hơn {d.SavingPct:0.#}% (≥ {plan.SubstituteMinSavingPct:0.#}%) → dùng tới {d.SharePct:0.#}%";
            }
            else
            {
                d.Reason = $"Chênh {d.SavingPct:0.#}% (< {plan.SubstituteMinSavingPct:0.#}%) → giữ chuẩn";
            }
            return d;
        }

        private static decimal? Usable(decimal? price, decimal wastePct) => price is > 0 && wastePct < 100m ? Math.Round(price.Value / (1m - wastePct / 100m), 2) : null;

        /// <summary>
        /// Áp quyết định vào danh sách dòng của MỘT món: dòng chuẩn giảm theo tỷ lệ thật, thêm dòng thay thế = phần định lượng tinh được thay
        /// (hao sơ chế của dòng thay thế áp khi tính lượng mua → đủ phần dùng được). Trả danh sách mới (không sửa danh sách vào).
        /// </summary>
        public static List<Line> Apply(IReadOnlyList<Line> lines, Func<Line, Line, Decision?> decisionOf, decimal stdOnlyFraction)
        {
            var result = lines.Select(l => new Line
            {
                IngredientId = l.IngredientId, Quantity = l.Quantity, UnitId = l.UnitId, WastePercent = l.WastePercent, ExpectedWasteRate = l.ExpectedWasteRate,
                SequenceNo = l.SequenceNo, SubstituteGroup = l.SubstituteGroup, MaxSharePct = l.MaxSharePct,
            }).ToList();
            foreach (var group in Pairs(lines).GroupBy(p => p.Std))
            {
                // Nhiều thay thế cho một chuẩn: dùng cái có quyết định thay nhiều nhất (hòa → rẻ hơn nhiều nhất).
                var best = group.Select(p => (p.Sub, D: decisionOf(p.Std, p.Sub))).Where(x => x.D is { SharePct: > 0 })
                    .OrderByDescending(x => Math.Min(x.D!.SharePct, x.Sub.MaxSharePct ?? 0m)).ThenByDescending(x => x.D!.SavingPct ?? 0m).FirstOrDefault();
                if (best.D == null)
                {
                    continue;
                }
                decimal pct = EffectivePct(best.D, best.Sub.MaxSharePct, stdOnlyFraction);
                if (pct <= 0)
                {
                    continue;
                }
                var std = result.First(l => l.IngredientId == group.Key.IngredientId && l.Quantity == group.Key.Quantity && l.SubstituteGroup == group.Key.SubstituteGroup);
                decimal moved = group.Key.Quantity * pct / 100m;
                std.Quantity = group.Key.Quantity - moved;
                result.Add(new Line
                {
                    IngredientId = best.Sub.IngredientId, Quantity = moved, UnitId = std.UnitId, WastePercent = best.Sub.WastePercent, ExpectedWasteRate = std.ExpectedWasteRate,
                    SequenceNo = std.SequenceNo, SubstituteGroup = std.SubstituteGroup, MaxSharePct = best.Sub.MaxSharePct, Substituted = true,
                });
            }
            return result;
        }

        /// <summary>Tỷ lệ thật của món = min(quyết định, % tối đa của dòng) – người mua đổi tỷ lệ (OVERRIDE) được vượt % tối đa (khi đó không tự duyệt) × (1 − phần suất khách chỉ dùng chuẩn).</summary>
        public static decimal EffectivePct(Decision d, decimal? maxSharePct, decimal stdOnlyFraction)
            => Math.Round((d.Source == "OVERRIDE" ? Math.Clamp(d.SharePct, 0m, 100m) : Math.Min(d.SharePct, Math.Clamp(maxSharePct ?? 0m, 0m, 100m))) * (1m - Math.Clamp(stdOnlyFraction, 0m, 1m)), 4);

        /// <summary>Khách chỉ dùng nguyên liệu chuẩn tại ngày (hợp đồng chưa xóa phủ ngày, bật cờ).</summary>
        public static async Task<HashSet<Guid>> StandardOnlyCustomersAsync(DatabaseContext db, IEnumerable<Guid> customerIds, DateTime date)
        {
            var ids = customerIds.Distinct().ToList();
            if (ids.Count == 0)
            {
                return [];
            }
            DateTime d = date.Date;
            return [.. await db.Contract.AsNoTracking()
                .Where(c => !c.IsDeleted && c.StandardIngredientOnly && ids.Contains(c.CustomerId) && c.EffectiveFrom <= d && (c.EffectiveTo == null || c.EffectiveTo >= d)
                    && c.Status != "Cancelled" && c.Status != "Terminated")
                .Select(c => c.CustomerId).Distinct().ToListAsync()];
        }

        /// <summary>Sản xuất: áp quyết định ĐÃ LƯU của ngày vào ảnh chụp định lượng của món (chưa có quyết định → giữ nguyên).</summary>
        public static async Task<List<ProductionRules.SnapshotLine>> ApplyStoredAsync(DatabaseContext db, DateTime date, Guid dishId, List<ProductionRules.SnapshotLine> lines)
        {
            if (!lines.Any(l => !string.IsNullOrWhiteSpace(l.SubstituteGroup)))
            {
                return lines;
            }
            var input = lines.Select(l => new Line
            {
                IngredientId = l.IngredientId, Quantity = l.Quantity, UnitId = l.UnitId, WastePercent = l.WastePercent, ExpectedWasteRate = l.ExpectedWasteRate,
                SequenceNo = l.SequenceNo, SubstituteGroup = l.SubstituteGroup, MaxSharePct = l.MaxSharePct,
            }).ToList();
            var stored = new Dictionary<(Guid, Guid), Decision?>();
            foreach (var (std, sub) in Pairs(input))
            {
                stored[(std.IngredientId, sub.IngredientId)] = await StoredAsync(db, date, std.IngredientId, sub.IngredientId);
            }
            if (stored.Values.All(d => d is not { SharePct: > 0 }))
            {
                return [.. lines.Where(l => !(l.Quantity <= 0 && !string.IsNullOrWhiteSpace(l.SubstituteGroup)))]; // dòng khai thay thế (định lượng 0) không vào sản xuất
            }
            var fractions = stored.Values.Where(d => d != null).Select(d => d!.StdOnlyByDish.GetValueOrDefault(dishId)).DefaultIfEmpty(0m).Max();
            return [.. Apply(input, (s, b) => stored.GetValueOrDefault((s.IngredientId, b.IngredientId)), fractions).Where(l => l.Quantity > 0).Select(l => new ProductionRules.SnapshotLine
            {
                IngredientId = l.IngredientId, Quantity = Math.Round(l.Quantity, 6), UnitId = l.UnitId, WastePercent = l.WastePercent, ExpectedWasteRate = l.ExpectedWasteRate,
                SequenceNo = l.SequenceNo, SubstituteGroup = l.SubstituteGroup, MaxSharePct = l.MaxSharePct, Substituted = l.Substituted,
            })];
        }
    }
}
