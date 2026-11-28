using System.Globalization;
using System.Text.RegularExpressions;
using AppDelicare.Model.Entity;

namespace AppDelicare.Services.Implement.PurchasePlan
{
    /// <summary>
    /// UI-6.51 (D-137): quy ước của đề nghị mua do kế hoạch mua tự động lập (không thêm cột – nhận biết qua ghi chú đầu đề nghị / dòng).
    /// </summary>
    public static partial class PurchasePlanRules
    {
        /// <summary>Đầu ghi chú đề nghị do hệ thống lập (job / nút "Tính lại ngay" / "Đặt phần hôm nay").</summary>
        public const string Marker = "[Kế hoạch mua tự động]";

        /// <summary>Ghi chú khi đề nghị / đơn mua đúng định mức được hệ thống tự duyệt.</summary>
        public const string AutoApprovedNote = "Hệ thống tự duyệt – đúng định mức";

        public static bool IsSystem(PurchaseRequest? pr) => pr?.Note?.StartsWith(Marker, StringComparison.Ordinal) == true;

        public static bool IsAutoApproved(PurchaseRequest? pr) => IsSystem(pr) && pr!.Note!.Contains(AutoApprovedNote, StringComparison.Ordinal);

        /// <summary>Ghi chú dòng: "[tính=12.5]" = nhu cầu hệ thống tính lúc lập (so định mức khi gửi duyệt).</summary>
        public static string ComputedTag(decimal quantity) => "[tính=" + quantity.ToString("0.####", CultureInfo.InvariantCulture) + "]";

        public static decimal? ComputedOf(string? note)
        {
            var m = ComputedRx().Match(note ?? string.Empty);
            return m.Success && decimal.TryParse(m.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;
        }

        [GeneratedRegex(@"\[tính=([0-9.]+)\]")]
        private static partial Regex ComputedRx();

        public static TimeSpan ParseTime(string? value, TimeSpan fallback)
            => TimeSpan.TryParseExact((value ?? string.Empty).Trim(), [@"hh\:mm", @"h\:mm"], CultureInfo.InvariantCulture, out var t) ? t : fallback;
    }
}

namespace AppDelicare.Services.Implement.PurchasePlan
{
    using AppDelicare.Data.Connection;
    using AppDelicare.Model.Model;
    using AppDelicare.Model.Option;
    using AppDelicare.Services.Interface;
    using Microsoft.EntityFrameworkCore;

    public static partial class PurchasePlanRules
    {
        /// <summary>Gợi ý NCC (có phần chia) cho mọi dòng của đề nghị + tổng tiền ước tính – một chỗ cho kiểm định mức và số cấp duyệt.</summary>
        public static async Task<(decimal Total, List<(PurchaseRequestLine Line, SupplierPickLineModel Pick)> Lines)> EstimateAsync(DatabaseContext db, IUomConversionService uom,
            BusinessRulesOptions rules, PurchasePlanOptions plan, DateTime today, Guid purchaseRequestId)
        {
            var pr = await db.PurchaseRequest.AsNoTracking().FirstOrDefaultAsync(p => p.Id == purchaseRequestId);
            var lines = await db.PurchaseRequestLine.AsNoTracking().Where(l => l.PurchaseRequestId == purchaseRequestId && !l.IsDeleted && l.Quantity > 0).ToListAsync();
            var picks = await SupplierPick.SuggestAsync(db, uom, rules, today,
                lines.Select(l => new SupplierPick.Need(l.Id, l.IngredientId, l.Quantity, l.UnitId != Guid.Empty ? l.UnitId : l.UomId, (l.RequiredDate ?? pr?.RequiredDate ?? today).Date)).ToList(),
                plan.SplitMaxCorrectionPct);
            decimal total = 0m;
            var result = new List<(PurchaseRequestLine, SupplierPickLineModel)>();
            foreach (var l in lines)
            {
                var pk = picks[l.Id];
                result.Add((l, pk));
                total += pk.ListSplit.Count > 0 ? pk.ListSplit.Sum(s => s.Quantity * (s.UnitPrice ?? 0m)) : (pk.OrderQuantity ?? l.Quantity) * (pk.UnitPrice ?? 0m);
            }
            return (Math.Round(total, 0), result);
        }
    }
}
