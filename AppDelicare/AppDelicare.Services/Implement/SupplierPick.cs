using AppDelicare.Data.Connection;
using AppDelicare.Model.Entity;
using AppDelicare.Model.Model;
using AppDelicare.Model.Option;
using AppDelicare.Services.Interface;
using Microsoft.EntityFrameworkCore;

namespace AppDelicare.Services.Implement
{
    /// <summary>
    /// UI-6.18 C (D-99): quy tắc gợi ý nhà cung cấp – MỘT hàm dùng chung cho form đề nghị mua, tự lập PO khi duyệt, màn Chọn nhà cung cấp (tính khi gọi, không lưu – GAP-MRP-02).
    /// 1. Đủ điều kiện: NCC đang hoạt động; giá Active phủ ngày cần, &gt; 0; giao kịp (LeadTimeDays ≤ số ngày tới ngày cần); không có sự cố nặng đang mở; quy đổi được đơn vị.
    /// 2. NCC ưu tiên đủ điều kiện → chọn, trừ khi giá tăng ≥ ApprovalFlagPricePct so giá liền trước VÀ có NCC khác rẻ hơn đạt chuẩn (3).
    /// 3. Không có / bị loại → giá thấp nhất (quy về đơn vị của dòng) trong các NCC đúng hẹn ≥ SupplierOnTimeMinPct (chưa có lịch sử = đạt), không sự cố mở; hòa → giao nhanh → ít sự cố.
    /// 4. Dưới MinimumOrderQuantity → làm tròn lên. Không NCC nào đủ điều kiện → "Cần chọn NCC" kèm lý do.
    /// </summary>
    public static class SupplierPick
    {
        public sealed record Need(Guid Key, Guid IngredientId, decimal Quantity, Guid UnitId, DateTime NeedDate);

        private static readonly string[] HeavySeverity = ["High", "HIGH", "Critical", "CRITICAL", "Nặng"];
        private static readonly string[] OpenIssue = ["Open", "OPEN", "InProgress", "IN_PROGRESS"];

        public static async Task<Dictionary<Guid, SupplierPickLineModel>> SuggestAsync(DatabaseContext db, IUomConversionService uom, BusinessRulesOptions rules, DateTime today, IReadOnlyCollection<Need> needs)
        {
            var result = new Dictionary<Guid, SupplierPickLineModel>();
            if (needs.Count == 0)
            {
                return result;
            }
            var ingIds = needs.Select(n => n.IngredientId).Distinct().ToList();
            var prices = await db.SupplierMaterial.AsNoTracking().Where(m => !m.IsDeleted && ingIds.Contains(m.IngredientId)).ToListAsync();
            var supIds = prices.Select(p => p.SupplierId).Distinct().ToList();
            var sups = await db.Supplier.AsNoTracking().Where(s => supIds.Contains(s.Id) && !s.IsDeleted).ToDictionaryAsync(s => s.Id);
            DateTime since = today.AddDays(-Math.Max(1, rules.SupplierOnTimeLookbackDays));
            var issues = await db.SupplierIssue.AsNoTracking().Where(i => !i.IsDeleted && supIds.Contains(i.SupplierId)).Select(i => new { i.SupplierId, i.Severity, i.IssueStatus, i.CreatedDate }).ToListAsync();
            var onTime = await SupplierMaterialService.OnTimeAsync(db, supIds, today, rules.SupplierOnTimeLookbackDays);
            var ings = await db.Ingredient.AsNoTracking().Where(i => ingIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id);
            var unitNames = await db.UnitMeasure.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.Symbol ?? u.Code ?? u.Name ?? string.Empty);
            foreach (var n in needs)
            {
                var line = new SupplierPickLineModel { Key = n.Key, IngredientId = n.IngredientId, IngredientName = ings.GetValueOrDefault(n.IngredientId)?.Name, Quantity = n.Quantity, UnitId = n.UnitId,
                    UnitName = unitNames.GetValueOrDefault(n.UnitId), NeedDate = n.NeedDate.Date };
                int daysLeft = (n.NeedDate.Date - today).Days;
                var cards = new List<SupplierPickCardModel>();
                foreach (var g in prices.Where(p => p.IngredientId == n.IngredientId).GroupBy(p => p.SupplierId))
                {
                    var sup = sups.GetValueOrDefault(g.Key);
                    var cur = g.Where(p => p.Status is null or "Active" && (p.EffectiveFrom == null || p.EffectiveFrom.Value.Date <= n.NeedDate.Date) && (p.EffectiveTo == null || p.EffectiveTo.Value.Date >= n.NeedDate.Date))
                        .OrderByDescending(p => p.EffectiveFrom).FirstOrDefault();
                    var card = new SupplierPickCardModel { SupplierId = g.Key, SupplierName = sup?.Name ?? sup?.Code, OnTimePct = onTime.GetValueOrDefault(g.Key),
                        IssueCount90 = issues.Count(i => i.SupplierId == g.Key && i.CreatedDate >= since) };
                    string? reason = null;
                    if (sup == null || (sup.Status != null && sup.Status != "Active")) reason = "NCC ngừng hoạt động";
                    else if (cur == null) reason = "Hết hiệu lực giá";
                    else if (!(cur.UnitPrice > 0)) reason = "Chưa có đơn giá";
                    if (cur != null)
                    {
                        card.PriceId = cur.Id; card.IsPreferred = cur.IsPreferred; card.LeadTimeDays = cur.LeadTimeDays; card.UnitPrice = cur.UnitPrice;
                        card.PriceUnitName = cur.PurchaseUomId is { } pu ? unitNames.GetValueOrDefault(pu) : line.UnitName;
                        // Giá quy về đơn vị của dòng: 1 đơn vị dòng = f đơn vị giá.
                        decimal? f = cur.PurchaseUomId is { } pu2 && pu2 != n.UnitId ? await uom.ConvertAsync(1m, n.UnitId, pu2, n.NeedDate) : 1m;
                        if (f == null) reason ??= "Không quy đổi được đơn vị";
                        else if (cur.UnitPrice is { } up)
                        {
                            card.PricePerLineUnit = Math.Round(up * f.Value, 2);
                            decimal? moq = cur.MinimumOrderQuantity ?? cur.MinimumOrderQty;
                            decimal qtyInPriceUnit = n.Quantity * f.Value;
                            card.OrderQuantity = moq is > 0 && qtyInPriceUnit < moq.Value ? Math.Round(moq.Value / f.Value, 4) : n.Quantity;
                            if (card.OrderQuantity > n.Quantity)
                            {
                                card.Note = $"Cần {n.Quantity:0.###} – mua tối thiểu {card.OrderQuantity:0.###}";
                            }
                            card.Amount = Math.Round(card.OrderQuantity * card.PricePerLineUnit.Value, 0);
                            var prev = g.Where(p => p.Id != cur.Id && p.EffectiveFrom < cur.EffectiveFrom && p.UnitPrice > 0).OrderByDescending(p => p.EffectiveFrom).FirstOrDefault();
                            if (prev != null && prev.PurchaseUomId == cur.PurchaseUomId)
                            {
                                card.ChangePct = Math.Round((up - prev.UnitPrice!.Value) / prev.UnitPrice.Value * 100m, 1);
                            }
                        }
                        if (reason == null && cur.LeadTimeDays is { } lt && lt > daysLeft) reason = $"Không giao kịp (cần {lt} ngày)";
                    }
                    if (reason == null && issues.Any(i => i.SupplierId == g.Key && HeavySeverity.Contains(i.Severity) && OpenIssue.Contains(i.IssueStatus))) reason = "Có sự cố nặng đang mở";
                    card.Eligible = reason == null;
                    card.Reason = reason;
                    cards.Add(card);
                }
                bool Ok(SupplierPickCardModel c) => c.Eligible && (c.OnTimePct ?? 100m) >= rules.SupplierOnTimeMinPct && !issues.Any(i => i.SupplierId == c.SupplierId && OpenIssue.Contains(i.IssueStatus));
                var pref = cards.FirstOrDefault(c => c.IsPreferred && c.Eligible);
                var cheapestOk = cards.Where(Ok).OrderBy(c => c.PricePerLineUnit ?? decimal.MaxValue).ThenBy(c => c.LeadTimeDays ?? int.MaxValue).ThenBy(c => c.IssueCount90).FirstOrDefault();
                SupplierPickCardModel? pick = null;
                if (pref != null && !(pref.ChangePct >= rules.ApprovalFlagPricePct && cheapestOk != null && cheapestOk.SupplierId != pref.SupplierId && cheapestOk.PricePerLineUnit < pref.PricePerLineUnit))
                {
                    pick = pref;
                }
                else
                {
                    pick = cheapestOk;
                    if (pref != null && pick != null && pick.SupplierId != pref.SupplierId)
                    {
                        line.Reason = "NCC ưu tiên tăng giá ≥ " + rules.ApprovalFlagPricePct.ToString("0.#") + "%";
                    }
                }
                if (pick != null)
                {
                    pick.Suggested = true;
                    line.SupplierId = pick.SupplierId;
                    line.SupplierName = pick.SupplierName;
                    line.PriceId = pick.PriceId;
                    line.UnitPrice = pick.PricePerLineUnit;
                    line.OrderQuantity = pick.OrderQuantity;
                }
                else
                {
                    line.Reason = cards.Count == 0 ? "Chưa có NCC" : string.Join(" · ", cards.Where(c => c.Reason != null).Select(c => c.Reason).Distinct());
                    if (string.IsNullOrEmpty(line.Reason)) line.Reason = "NCC chưa đạt chuẩn đúng hẹn";
                }
                line.ListCard = [.. cards.OrderByDescending(c => c.Suggested).ThenByDescending(c => c.Eligible).ThenBy(c => c.PricePerLineUnit ?? decimal.MaxValue).Take(3)];
                result[n.Key] = line;
            }
            return result;
        }
    }
}
