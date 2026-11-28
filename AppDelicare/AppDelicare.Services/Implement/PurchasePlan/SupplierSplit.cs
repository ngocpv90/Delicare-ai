using AppDelicare.Data.Connection;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Helper;
using AppDelicare.Model.Model;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace AppDelicare.Services.Implement.PurchasePlan
{
    /// <summary>
    /// UI-6.51 (D-137 g, GAP-PUR-SPLIT): chia số lượng một dòng cho nhiều NCC theo <c>SupplierMaterial.AllocationPct</c>.
    /// <list type="number">
    /// <item>Chỉ NCC có tỷ lệ; NCC bị loại (không đủ điều kiện của quy tắc gợi ý: hết giá, không kịp, sự cố nặng…) → phần của NCC đó chia lại cho NCC còn lại theo tỷ lệ.</item>
    /// <item>Bù lệch lũy kế tháng (số lượng PO không hủy trong tháng của nguyên liệu): tỷ lệ lần này = mục tiêu + (mục tiêu − lũy kế), chặn ± <c>SplitMaxCorrectionPct</c> điểm.</item>
    /// <item>Phần chia dưới mức mua tối thiểu của NCC → dồn cho NCC còn lại; còn 1 NCC → lấy hết.</item>
    /// <item>Người mua sửa phần chia cho (nguyên liệu, ngày cần) → lưu lịch sử AuditChange <c>PurchasePlanSplit</c>, dùng thay tỷ lệ tính (không tự duyệt).</item>
    /// </list>
    /// </summary>
    public static class SupplierSplit
    {
        public const string EntityName = "PurchasePlanSplit";

        public sealed record Input(Guid SupplierId, string? SupplierName, Guid? PriceId, decimal? UnitPrice, int? LeadTimeDays, bool IsPreferred, decimal TargetPct, decimal CumQuantity,
            decimal? MinQuantity, bool Eligible, string? Reason);

        public static List<SupplierSplitModel> Split(decimal quantity, IReadOnlyList<Input> rows, decimal maxCorrectionPct, IReadOnlyDictionary<Guid, decimal>? manual = null)
        {
            var active = rows.Where(r => r.TargetPct > 0).ToList();
            if (active.Count == 0 || quantity <= 0)
            {
                return [];
            }
            var eligible = active.Where(r => r.Eligible).ToList();
            if (eligible.Count == 0)
            {
                return [];
            }
            decimal targetSum = eligible.Sum(r => r.TargetPct);
            decimal cumTotal = eligible.Sum(r => Math.Max(0m, r.CumQuantity));
            var parts = eligible.Select(r =>
            {
                decimal t = r.TargetPct / targetSum * 100m;
                decimal? cum = cumTotal > 0 ? Math.Round(Math.Max(0m, r.CumQuantity) / cumTotal * 100m, 1) : null;
                decimal p = t;
                if (manual != null)
                {
                    p = manual.GetValueOrDefault(r.SupplierId);
                }
                else if (cum is { } a)
                {
                    p = Math.Clamp(t + (t - a), Math.Max(0m, t - maxCorrectionPct), Math.Min(100m, t + maxCorrectionPct));
                }
                return new SupplierSplitModel
                {
                    SupplierId = r.SupplierId, SupplierName = r.SupplierName, PriceId = r.PriceId, UnitPrice = r.UnitPrice, LeadTimeDays = r.LeadTimeDays, IsPreferred = r.IsPreferred,
                    TargetPct = Math.Round(r.TargetPct, 2), CumPct = cum, Pct = p, MinQuantity = r.MinQuantity,
                };
            }).Where(p => manual == null || p.Pct > 0).ToList();
            if (parts.Count == 0)
            {
                return [];
            }
            var notes = active.Where(r => !r.Eligible).Select(r => $"{r.SupplierName} bị loại ({r.Reason}) – phần {r.TargetPct:0.#}% chia lại").ToList();
            // Dưới mức mua tối thiểu → bỏ NCC đó, dồn cho NCC còn lại (lặp tới khi ổn định; còn 1 NCC → lấy hết).
            while (true)
            {
                Normalize(parts, quantity);
                var under = parts.Count > 1 ? parts.Where(p => p.MinQuantity is > 0 && p.Quantity < p.MinQuantity).OrderBy(p => p.Quantity).FirstOrDefault() : null;
                if (under == null)
                {
                    break;
                }
                notes.Add($"{under.SupplierName}: phần {under.Quantity:0.##} dưới mức tối thiểu {under.MinQuantity:0.##} – dồn NCC khác");
                parts.Remove(under);
            }
            if (notes.Count > 0)
            {
                parts[0].Note = string.Join(" · ", notes);
            }
            return parts;
        }

        /// <summary>Chuẩn hóa tỷ lệ về tổng 100, chia số lượng (4 chữ số), phần dư dồn NCC lớn nhất – tổng đúng bằng số cần.</summary>
        private static void Normalize(List<SupplierSplitModel> parts, decimal quantity)
        {
            decimal sum = parts.Sum(p => p.Pct);
            foreach (var p in parts)
            {
                p.Pct = sum > 0 ? Math.Round(p.Pct / sum * 100m, 2) : Math.Round(100m / parts.Count, 2);
                p.Quantity = Math.Round(quantity * p.Pct / 100m, 4);
            }
            var biggest = parts.OrderByDescending(p => p.Pct).First();
            biggest.Quantity += quantity - parts.Sum(p => p.Quantity);
            biggest.Pct += 100m - parts.Sum(p => p.Pct);
        }

        public static Guid KeyOf(DateTime date, Guid ingredientId) => SubstitutionRules.KeyOf(date, ingredientId, Guid.Empty);

        /// <summary>Phần chia người mua đã sửa cho (ngày cần, nguyên liệu): NCC → %. Không có → null.</summary>
        public static async Task<Dictionary<Guid, decimal>?> ManualAsync(DatabaseContext db, DateTime date, Guid ingredientId)
        {
            Guid key = KeyOf(date.Date, ingredientId);
            string? json = await db.AuditChange.AsNoTracking().Where(a => a.EntityName == EntityName && a.IdChange == key && !a.IsDeleted)
                .OrderByDescending(a => a.CreatedDate).Select(a => a.Changes).FirstOrDefaultAsync();
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }
            try
            {
                var m = JsonConvert.DeserializeObject<ManualSplit>(json);
                return m?.Pct is { Count: > 0 } p ? p : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        public sealed class ManualSplit
        {
            public DateTime Date { get; set; }
            public Guid IngredientId { get; set; }
            public Dictionary<Guid, decimal> Pct { get; set; } = [];
            public string? Reason { get; set; }
        }

        /// <summary>Số lượng PO không hủy trong tháng của <paramref name="today"/> theo (NCC, nguyên liệu) – đơn vị của dòng PO (nơi gọi quy đổi).</summary>
        public static async Task<List<(Guid SupplierId, Guid IngredientId, decimal Quantity, Guid UnitId)>> MonthQuantitiesAsync(DatabaseContext db, IReadOnlyCollection<Guid> ingredientIds, DateTime today)
        {
            DateTime monthStart = new(today.Year, today.Month, 1), monthEnd = monthStart.AddMonths(1);
            var ids = ingredientIds.ToList();
            var rows = await (from l in db.PurchaseOrderLine.AsNoTracking()
                              join o in db.PurchaseOrder.AsNoTracking() on l.PurchaseOrderId equals o.Id
                              where !l.IsDeleted && !o.IsDeleted && o.Status != PurchaseOrderStatus.Cancelled && ids.Contains(l.IngredientId) && o.OrderDate >= monthStart && o.OrderDate < monthEnd
                              select new { o.SupplierId, l.IngredientId, l.Quantity, Unit = l.UnitId != Guid.Empty ? l.UnitId : l.UomId }).ToListAsync();
            return [.. rows.Select(r => (r.SupplierId, r.IngredientId, r.Quantity, r.Unit))];
        }
    }
}
